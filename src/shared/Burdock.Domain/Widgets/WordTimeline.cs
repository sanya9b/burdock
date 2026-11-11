using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;

namespace Burdock.Domain.Widgets;

/// <summary>Whether time-driven surfaces show the translation next to the word (spec 06 «спочатку вгадай»).</summary>
public enum TranslationMode
{
    Visible,
    Hidden,
}

/// <summary>What the surface shows once the timeline is exhausted (spec 04 FR-04-8); the extension localises the text itself.</summary>
public sealed record TimelineFallback(string Kind, string MascotState)
{
    public static TimelineFallback OpenApp { get; } = new("openApp", "sleep");
}

/// <summary>One slot of the widget timeline (spec 04 §3.2.2): everything a surface needs without touching content storage.</summary>
public sealed record TimelineEntry(
    int EntryIndex,
    DateTimeOffset FireDate,
    DateTimeOffset PlannedUntil,
    LexemeId LexemeId,
    string Word,
    string? Ipa,
    string? Pos,
    string Translation,
    string? Example,
    string ThemeId,
    bool IsFreeTier,
    string? CategoryId);

/// <summary>
/// The planned timeline of time-driven surfaces (spec 04 §3.2, FR-04-3): entries sorted by fire date, each valid until the
/// next one, nothing after <see cref="PlannedUntil"/> except the fallback. Written by the app, read by the widget extension.
/// </summary>
public sealed record WordTimeline(
    int SchemaVersion,
    DateTimeOffset GeneratedAt,
    DateTimeOffset PlannedUntil,
    int CadenceMinutes,
    TranslationMode TranslationMode,
    LanguagePair Pair,
    TimelineFallback Fallback,
    IReadOnlyList<TimelineEntry> Entries)
{
    public const int CurrentSchemaVersion = 1;

    /// <summary>Stable fingerprint of what the surfaces would show; a replan with the same hash needs no reload (spec 04 §3.3.2).</summary>
    public string ContentHash()
    {
        var builder = new StringBuilder();
        builder.Append(CadenceMinutes).Append('|').Append(TranslationMode).Append('|').Append(PlannedUntil.ToUnixTimeSeconds()).Append('|');
        foreach (var entry in Entries)
        {
            builder.Append(entry.LexemeId.Value.ToString("D")).Append('@').Append(entry.FireDate.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)).Append(';');
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())))[..16];
    }

    /// <summary>Number of entries from the current one whose change justifies a rewrite and a WidgetKit reload (spec 04 §3.3.2: reload only when the next six entries changed).</summary>
    public const int LeadingEntries = 6;

    /// <summary>
    /// Fingerprint of what the surfaces show from <paramref name="at"/> on: the current word (whenever its slot began) and the
    /// next entries with their times. Two timelines with the same leading hash look identical to the learner, so a replan
    /// with an equal hash is not written and triggers no reload.
    /// </summary>
    public string LeadingHash(DateTimeOffset at)
    {
        var builder = new StringBuilder();
        builder.Append(CadenceMinutes).Append('|').Append(TranslationMode).Append('|');
        var current = EntryAt(at);
        var taken = 0;
        if (current is not null)
        {
            builder.Append(current.LexemeId.Value.ToString("D")).Append(';');
            taken++;
        }

        foreach (var entry in Entries.Where(e => e.FireDate > at).Take(LeadingEntries - taken))
        {
            builder.Append(entry.LexemeId.Value.ToString("D")).Append('@').Append(entry.FireDate.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)).Append(';');
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())))[..16];
    }

    /// <summary>The entry a surface should show at <paramref name="at"/>, or null before the first entry or after <see cref="PlannedUntil"/>.</summary>
    public TimelineEntry? EntryAt(DateTimeOffset at)
    {
        if (at >= PlannedUntil)
        {
            return null;
        }

        TimelineEntry? current = null;
        foreach (var entry in Entries)
        {
            if (entry.FireDate > at)
            {
                break;
            }

            current = entry;
        }

        return current;
    }
}
