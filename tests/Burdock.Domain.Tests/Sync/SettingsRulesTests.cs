using Burdock.Domain.Sync;

namespace Burdock.Domain.Tests.Sync;

/// <summary>Last-writer-wins rules shared by the app and the server (spec 09 section 3.6, FR-09-13).</summary>
public sealed class SettingsRulesTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 11, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_later_stamp_wins_and_a_tie_goes_to_the_larger_client_id()
    {
        var older = new SettingsEntry("level", "\"A2\"", T0, "aaa");
        var newer = new SettingsEntry("level", "\"B1\"", T0.AddSeconds(1), "aaa");
        var tie = new SettingsEntry("level", "\"B2\"", T0, "bbb");

        newer.Supersedes(older).ShouldBeTrue();
        older.Supersedes(newer).ShouldBeFalse();
        tie.Supersedes(older).ShouldBeTrue();
        older.Supersedes(tie).ShouldBeFalse();
        older.Supersedes(older).ShouldBeFalse();
    }

    [Theory]
    [InlineData("", "1", "c", "settings.key_invalid")]
    [InlineData("has space", "1", "c", "settings.key_invalid")]
    [InlineData("ok", "", "c", "settings.value_invalid")]
    [InlineData("ok", "1", "", "settings.client_invalid")]
    public void Create_rejects_malformed_entries(string key, string value, string client, string code)
    {
        var result = SettingsEntry.Create(key, value, T0, client);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe(code);
    }

    [Fact]
    public void Create_rejects_oversized_values_and_trims_the_client()
    {
        SettingsEntry.Create("k", new string('x', SettingsEntry.MaxValueBytes + 1), T0, "c").Error!.Code.ShouldBe("settings.value_invalid");
        SettingsEntry.Create("k", "1", T0, " c ").Value.ClientId.ShouldBe("c");
    }

    [Fact]
    public void Merge_applies_only_superseding_entries_and_the_etag_is_order_independent()
    {
        var current = new Dictionary<string, SettingsEntry>(StringComparer.Ordinal)
        {
            ["a"] = new("a", "1", T0, "x"),
            ["b"] = new("b", "2", T0, "x"),
        };

        var (merged, changed) = SettingsDocumentRules.Merge(current, [new("a", "9", T0.AddMinutes(-1), "y"), new("b", "8", T0.AddMinutes(1), "y"), new("c", "7", T0, "y")]);

        merged["a"].ValueJson.ShouldBe("1");
        merged["b"].ValueJson.ShouldBe("8");
        merged["c"].ValueJson.ShouldBe("7");
        changed.Select(e => e.Key).ShouldBe(["b", "c"]);
        SettingsDocumentRules.ComputeEtag(merged.Values).ShouldBe(SettingsDocumentRules.ComputeEtag(merged.Values.Reverse()));
        SettingsDocumentRules.ComputeEtag(merged.Values).ShouldNotBe(SettingsDocumentRules.ComputeEtag(current.Values));
        SettingsDocumentRules.ComputeEtag([]).Length.ShouldBe(16);
    }

    [Fact]
    public void Stamps_compare_and_persist_at_millisecond_resolution()
    {
        var fine = new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.Zero).AddTicks(4_567);
        var coarse = SettingsEntry.NormalizeStamp(fine);
        var local = new SettingsEntry("level", "\"B2\"", fine, "same");
        var stored = new SettingsEntry("level", "\"B2\"", coarse, "same");

        coarse.ShouldBe(new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.Zero));
        local.Supersedes(stored).ShouldBeFalse();
        stored.Supersedes(local).ShouldBeFalse();
        (local with { UpdatedAt = fine.AddMilliseconds(1) }).Supersedes(stored).ShouldBeTrue();
        SettingsEntry.Create("level", "1", fine, "c").Value.UpdatedAt.ShouldBe(coarse);
        SettingsEntry.NormalizeStamp(new DateTimeOffset(2026, 10, 12, 12, 0, 0, 123, TimeSpan.FromHours(3))).ShouldBe(new DateTimeOffset(2026, 10, 12, 9, 0, 0, 123, TimeSpan.Zero));
    }

    [Fact]
    public void The_event_type_catalogue_is_closed()
    {
        ProgressEventTypes.All.ShouldBe([ProgressEventTypes.ReviewLog, ProgressEventTypes.ExposureConfirmed, ProgressEventTypes.ExposureDaily, ProgressEventTypes.CardIntroduced, ProgressEventTypes.WordListChanged], ignoreOrder: true);
    }
}
