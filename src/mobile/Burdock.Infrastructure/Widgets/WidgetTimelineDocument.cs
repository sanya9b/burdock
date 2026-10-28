using System.Globalization;
using System.Text.Json.Serialization;
using Burdock.Application.Ports;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;
using Burdock.Domain.Widgets;

namespace Burdock.Infrastructure.Widgets;

/// <summary>On-disk shape of <c>Timeline/word-timeline.v1.json</c> (spec 04 §3.2.2): camelCase, ISO-8601 dates with offset and whole seconds, read by the Swift extension with a plain <c>JSONDecoder</c>.</summary>
public sealed record WidgetTimelineDocument(
    int SchemaVersion,
    string GeneratedAt,
    string PlannedUntil,
    int CadenceMinutes,
    string TranslationMode,
    WidgetPairDocument Pair,
    WidgetFallbackDocument Fallback,
    IReadOnlyList<WidgetEntryDocument> Entries);

public sealed record WidgetPairDocument(string Native, string Target);

public sealed record WidgetFallbackDocument(string Kind, string MascotState);

public sealed record WidgetEntryDocument(
    int EntryIndex,
    string FireDate,
    string PlannedUntil,
    Guid LexemeId,
    string Word,
    string? Ipa,
    string? Pos,
    string Translation,
    string? Example,
    string ThemeId,
    bool IsFreeTier,
    string? CategoryId);

/// <summary><c>Timeline/word-cursor.v1.json</c> (spec 04 §3.2.3), written by the «Далі» intent.</summary>
public sealed record WidgetCursorDocument(int SchemaVersion, int CursorIndex, string AdvancedAt, string Source);

/// <summary><c>Timeline/extension-marker.v1.json</c> (spec 04 §3.2.1), written by the extension's timeline provider.</summary>
public sealed record WidgetMarkerDocument(int SchemaVersion, string RequestedAt, IReadOnlyList<string> Families);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(WidgetTimelineDocument))]
[JsonSerializable(typeof(WidgetCursorDocument))]
[JsonSerializable(typeof(WidgetMarkerDocument))]
public sealed partial class WidgetJsonContext : JsonSerializerContext;

/// <summary>Maps between the Domain timeline and the App Group documents; the only place that knows both shapes.</summary>
public static class WidgetTimelineMapper
{
    /// <summary>ISO-8601 without fractional seconds, in the learner's zone so the file is readable and Swift's <c>.iso8601</c> strategy parses it.</summary>
    public const string DateFormat = "yyyy-MM-dd'T'HH:mm:sszzz";

    public static WidgetTimelineDocument ToDocument(WordTimeline timeline, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        ArgumentNullException.ThrowIfNull(timeZone);
        return new WidgetTimelineDocument(
            timeline.SchemaVersion,
            Format(timeline.GeneratedAt, timeZone),
            Format(timeline.PlannedUntil, timeZone),
            timeline.CadenceMinutes,
            timeline.TranslationMode == TranslationMode.Hidden ? "hidden" : "visible",
            new WidgetPairDocument(timeline.Pair.Native.Value, timeline.Pair.Target.Value),
            new WidgetFallbackDocument(timeline.Fallback.Kind, timeline.Fallback.MascotState),
            timeline.Entries.Select(e => new WidgetEntryDocument(
                e.EntryIndex, Format(e.FireDate, timeZone), Format(e.PlannedUntil, timeZone), e.LexemeId.Value, e.Word, e.Ipa, e.Pos, e.Translation, e.Example, e.ThemeId, e.IsFreeTier, e.CategoryId)).ToArray());
    }

    /// <summary>Null for a schema the app does not understand or a malformed pair; unknown fields are ignored.</summary>
    public static WordTimeline? ToDomain(WidgetTimelineDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.SchemaVersion is < 1 or > WordTimeline.CurrentSchemaVersion)
        {
            return null;
        }

        var pair = LanguagePair.Parse($"{document.Pair.Native}-{document.Pair.Target}");
        if (pair.IsFailure || !TryParse(document.GeneratedAt, out var generatedAt) || !TryParse(document.PlannedUntil, out var plannedUntil))
        {
            return null;
        }

        var entries = new List<TimelineEntry>(document.Entries.Count);
        foreach (var e in document.Entries)
        {
            if (!TryParse(e.FireDate, out var fireDate) || !TryParse(e.PlannedUntil, out var until))
            {
                return null;
            }

            entries.Add(new TimelineEntry(e.EntryIndex, fireDate, until, LexemeId.From(e.LexemeId), e.Word, e.Ipa, e.Pos, e.Translation, e.Example, e.ThemeId, e.IsFreeTier, e.CategoryId));
        }

        var mode = string.Equals(document.TranslationMode, "hidden", StringComparison.OrdinalIgnoreCase) ? TranslationMode.Hidden : TranslationMode.Visible;
        return new WordTimeline(document.SchemaVersion, generatedAt, plannedUntil, document.CadenceMinutes, mode, pair.Value, new TimelineFallback(document.Fallback.Kind, document.Fallback.MascotState), entries);
    }

    public static WidgetCursor? ToDomain(WidgetCursorDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return TryParse(document.AdvancedAt, out var advancedAt) ? new WidgetCursor(document.CursorIndex, advancedAt, document.Source) : null;
    }

    public static WidgetMarker? ToDomain(WidgetMarkerDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return TryParse(document.RequestedAt, out var requestedAt) ? new WidgetMarker(requestedAt, document.Families) : null;
    }

    public static string Format(DateTimeOffset value, TimeZoneInfo timeZone)
        => TimeZoneInfo.ConvertTime(value, timeZone).ToString(DateFormat, CultureInfo.InvariantCulture);

    private static bool TryParse(string? value, out DateTimeOffset result)
        => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out result);
}
