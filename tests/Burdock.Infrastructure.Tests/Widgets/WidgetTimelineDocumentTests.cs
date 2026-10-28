using System.Text.Json;
using System.Text.RegularExpressions;
using Burdock.Application.Ports;
using Burdock.Domain.Learning;
using Burdock.Domain.Vocabulary;
using Burdock.Domain.Widgets;
using Burdock.Infrastructure.Widgets;
using Burdock.TestKit;
using Burdock.TestKit.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Burdock.Infrastructure.Tests.Widgets;

/// <summary>The App Group documents the Swift extension decodes (spec 04 §3.2.2–3.2.4): camelCase keys, ISO-8601 dates without fractions, forward-compatible reading.</summary>
public sealed partial class WidgetTimelineDocumentTests
{
    private static readonly TimeZoneInfo Kyiv = TestTimeZones.KyivFixed;
    private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 10, 27, 7, 17, 42, TimeSpan.Zero).AddTicks(1234567);

    [Fact]
    public void A_planned_timeline_serialises_to_the_spec_shape_and_round_trips()
    {
        var timeline = Plan();

        var json = JsonSerializer.Serialize(WidgetTimelineMapper.ToDocument(timeline, Kyiv), WidgetJsonContext.Default.WidgetTimelineDocument);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.GetProperty("schemaVersion").GetInt32().ShouldBe(1);
        root.GetProperty("cadenceMinutes").GetInt32().ShouldBe(60);
        root.GetProperty("translationMode").GetString().ShouldBe("visible");
        root.GetProperty("pair").GetProperty("native").GetString().ShouldBe("uk");
        root.GetProperty("pair").GetProperty("target").GetString().ShouldBe("en");
        root.GetProperty("fallback").GetProperty("kind").GetString().ShouldBe("openApp");
        var first = root.GetProperty("entries")[0];
        first.GetProperty("entryIndex").GetInt32().ShouldBe(0);
        first.GetProperty("word").GetString().ShouldBe("word1");
        first.GetProperty("translation").GetString().ShouldBe("слово1");
        first.GetProperty("pos").GetString().ShouldBe("noun");
        first.GetProperty("ipa").GetString().ShouldBe("/wɜrd/");
        first.GetProperty("themeId").GetString().ShouldBe("burdock.default");
        first.TryGetProperty("example", out _).ShouldBeFalse("null fields are omitted");
        foreach (var key in new[] { "generatedAt", "plannedUntil" })
        {
            IsoWithOffset().IsMatch(root.GetProperty(key).GetString()!).ShouldBeTrue(root.GetProperty(key).GetString());
        }

        IsoWithOffset().IsMatch(first.GetProperty("fireDate").GetString()!).ShouldBeTrue();
        root.GetProperty("generatedAt").GetString().ShouldEndWith("+02:00");

        var back = WidgetTimelineMapper.ToDomain(JsonSerializer.Deserialize(json, WidgetJsonContext.Default.WidgetTimelineDocument)!);
        back.ShouldNotBeNull();
        back.Entries.Count.ShouldBe(timeline.Entries.Count);
        back.Entries[0].FireDate.ShouldBe(timeline.Entries[0].FireDate);
        back.PlannedUntil.ShouldBe(timeline.PlannedUntil);
        back.ContentHash().ShouldBe(timeline.ContentHash());
    }

    [Fact]
    public void Unknown_schema_versions_and_extra_fields_are_handled_forward_compatibly()
    {
        var known = """{"schemaVersion":1,"generatedAt":"2026-10-27T09:17:42+02:00","plannedUntil":"2026-10-29T09:17:42+02:00","cadenceMinutes":60,"translationMode":"hidden","pair":{"native":"uk","target":"en"},"fallback":{"kind":"openApp","mascotState":"sleep"},"entries":[],"futureField":true}""";
        var tooNew = known.Replace("\"schemaVersion\":1", "\"schemaVersion\":7", StringComparison.Ordinal);

        var parsed = WidgetTimelineMapper.ToDomain(JsonSerializer.Deserialize(known, WidgetJsonContext.Default.WidgetTimelineDocument)!);
        var rejected = WidgetTimelineMapper.ToDomain(JsonSerializer.Deserialize(tooNew, WidgetJsonContext.Default.WidgetTimelineDocument)!);

        parsed.ShouldNotBeNull();
        parsed.TranslationMode.ShouldBe(TranslationMode.Hidden);
        parsed.Entries.ShouldBeEmpty();
        rejected.ShouldBeNull();
    }

    [Fact]
    public void Cursor_and_marker_documents_map_to_the_ports()
    {
        var cursor = WidgetTimelineMapper.ToDomain(JsonSerializer.Deserialize("""{"schemaVersion":1,"cursorIndex":12,"advancedAt":"2026-09-27T10:12:31+03:00","source":"nextWordIntent"}""", WidgetJsonContext.Default.WidgetCursorDocument)!);
        var marker = WidgetTimelineMapper.ToDomain(JsonSerializer.Deserialize("""{"schemaVersion":1,"requestedAt":"2026-09-27T10:00:00Z","families":["systemSmall","accessoryInline"]}""", WidgetJsonContext.Default.WidgetMarkerDocument)!);

        cursor.ShouldBe(new WidgetCursor(12, new DateTimeOffset(2026, 9, 27, 7, 12, 31, TimeSpan.Zero), "nextWordIntent"));
        marker.ShouldNotBeNull();
        marker.RequestedAt.ShouldBe(new DateTimeOffset(2026, 9, 27, 10, 0, 0, TimeSpan.Zero));
        marker.Families.ShouldBe(["systemSmall", "accessoryInline"]);
    }

    [Fact]
    public async Task Without_an_app_group_container_the_bridge_is_unsupported_and_reads_nothing()
    {
        var bridge = new AppGroupWidgetBridge(new FakeClock(Now, Kyiv), NullLogger<AppGroupWidgetBridge>.Instance);

        bridge.IsSupported.ShouldBeFalse();
        (await bridge.ReadTimelineAsync(TestContext.Current.CancellationToken)).ShouldBeNull();
        (await bridge.ReadCursorAsync(TestContext.Current.CancellationToken)).ShouldBeNull();
        (await bridge.ReadMarkerAsync(TestContext.Current.CancellationToken)).ShouldBeNull();
        await bridge.RequestReloadAsync("start", TestContext.Current.CancellationToken);
    }

    private static WordTimeline Plan()
    {
        var settings = LearnerSettings.Moderate(LanguagePair.UkrainianToEnglish, CefrLevel.A1, []);
        var candidates = Enumerable.Range(1, 6).Select(n => new TimelineCandidate(new WordEntry(
            LexemeBuilder.ALexeme().WithId(Guid.Parse($"0199a4c0-0000-7000-8000-0000000000{n:x2}")).WithLemma($"word{n}").Build(),
            $"слово{n}", [], [$"word{n}"], "/wɜrd/", null, null, null), false, n, 0)).ToArray();
        return WordTimelinePlanner.Plan(new TimelineRequest(Now, Kyiv, settings, candidates));
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}[+-]\d{2}:\d{2}$")]
    private static partial Regex IsoWithOffset();
}
