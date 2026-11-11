using Burdock.Domain.Exposure;
using Burdock.Domain.Learning;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Time;
using Burdock.Domain.Vocabulary;

namespace Burdock.Domain.Widgets;

/// <summary>
/// A word the surfaces may show: today's plan entry with its priority (lower first) and how often it was already exposed
/// today. A <paramref name="Filler"/> is one of the learner's own earlier words that only takes a slot when no word of the
/// day is eligible any more (daily cap reached), so the surfaces keep changing instead of freezing on the last word.
/// </summary>
public sealed record TimelineCandidate(WordEntry Entry, bool IsNew, double Priority, int ExposuresToday, bool Filler = false);

/// <summary>
/// Everything the planner needs; the caller supplies the clock value so the plan is reproducible. <paramref name="Current"/>
/// is the word the surfaces show at <paramref name="Now"/> according to the previous timeline: a replan keeps it in the
/// current slot when it is still in the pool, so opening the app never visibly swaps the widget's word.
/// </summary>
public sealed record TimelineRequest(
    DateTimeOffset Now,
    TimeZoneInfo TimeZone,
    LearnerSettings Settings,
    IReadOnlyList<TimelineCandidate> Candidates,
    bool FreeTier = false,
    string ThemeId = WordTimelinePlanner.DefaultThemeId,
    LexemeId? Current = null);

/// <summary>
/// Deterministic, pure planning of the widget timeline (spec 04 §3.3, spec 06 §3.5.2, FR-04-4). Slots follow the cadence
/// on local clock boundaries for 48 hours; quiet hours produce no slot, so the last evening word stays until the first
/// morning slot; every lexeme stays within the learner's daily exposure cap across surfaces, at most
/// <see cref="MaxDistinctWordsPerDay"/> different words appear per learning day, repeats of one word are spread out and
/// never adjacent; new words take about <see cref="NewShare"/> of the slots. The free tier shows a fixed pool at the
/// default cadence. The widget renders the fallback after <see cref="WordTimeline.PlannedUntil"/>.
/// A replan continues the day rather than restarting it: the word currently on the surfaces stays in the current slot and
/// the words shown fewest times today (planned or already exposed) come next; fillers are used only once every word of
/// the day is at its cap.
/// </summary>
public static class WordTimelinePlanner
{
    public const string DefaultThemeId = "burdock.default";
    public const int HorizonHours = 48;
    public const int MinEntrySpacingMinutes = 5;
    public const int MaxEntries = 200;
    public const int MaxDistinctWordsPerDay = 12;
    public const int FreeTierWordsPerDay = 5;
    public const double NewShare = 0.4;

    public static WordTimeline Plan(TimelineRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.TimeZone);
        ArgumentNullException.ThrowIfNull(request.Settings);
        ArgumentNullException.ThrowIfNull(request.Candidates);

        var settings = request.Settings;
        var timeZone = request.TimeZone;
        var cadence = request.FreeTier ? RotationCadence.FreeTier : settings.Cadence;
        var now = Truncate(request.Now);
        var horizon = now.AddHours(HorizonHours);
        var pool = request.FreeTier ? request.Candidates.Take(FreeTierWordsPerDay).ToArray() : request.Candidates;
        var budget = Math.Max(1, settings.ExposuresPerWordPerDay);
        var minGap = MinGap(settings, cadence, budget);
        var mode = settings.TranslationHiddenOnSurfaces ? TranslationMode.Hidden : TranslationMode.Visible;

        var entries = new List<TimelineEntry>();
        if (pool.Count == 0)
        {
            return new WordTimeline(WordTimeline.CurrentSchemaVersion, now, now, cadence.Minutes, mode, settings.Pair, TimelineFallback.OpenApp, entries);
        }

        var today = LearningDay.From(now, timeZone);
        var state = pool.ToDictionary(c => c.Entry.Id, c => new CandidateState(c) { FirstDay = today });
        var distinctPerDay = new Dictionary<LearningDay, HashSet<LexemeId>>();
        var exposuresPerDay = new Dictionary<(LearningDay Day, LexemeId Lexeme), int>();
        var localNow = TimeZoneInfo.ConvertTime(now, timeZone);
        var minutesIntoDay = (int)localNow.TimeOfDay.TotalMinutes;
        var slotLocal = localNow.Date.AddMinutes(minutesIntoDay - (minutesIntoDay % cadence.Minutes));
        var slotIndex = 0;
        var newUsed = 0;
        LexemeId? previous = null;

        while (entries.Count < MaxEntries)
        {
            var fireDate = ToInstant(slotLocal, timeZone);
            if (fireDate >= horizon)
            {
                break;
            }

            var isFirst = entries.Count == 0;
            var quiet = settings.QuietHours.Contains(TimeOnly.FromDateTime(slotLocal));
            if (!isFirst && quiet)
            {
                slotLocal = slotLocal.Add(cadence.Interval);
                slotIndex++;
                continue;
            }

            var day = LearningDay.From(fireDate, timeZone);
            var distinct = distinctPerDay.TryGetValue(day, out var set) ? set : distinctPerDay[day] = [];
            var preferNew = newUsed < NewShare * (entries.Count + 1);
            var pick = (isFirst && request.Current is { } current && state.TryGetValue(current, out var kept) ? kept : null)
                       ?? Pick(state.Values, preferNew, previous, slotIndex, minGap, budget, day, distinct, exposuresPerDay, strictGap: true, fillers: false)
                       ?? Pick(state.Values, preferNew, previous, slotIndex, minGap, budget, day, distinct, exposuresPerDay, strictGap: true, fillers: true)
                       ?? Pick(state.Values, preferNew, previous, slotIndex, minGap, budget, day, distinct, exposuresPerDay, strictGap: false, fillers: false)
                       ?? Pick(state.Values, preferNew, previous, slotIndex, minGap, budget, day, distinct, exposuresPerDay, strictGap: false, fillers: true)
                       // The current slot must never be empty: a word past its daily cap beats «Реп’яшок спить» in the middle of the day.
                       ?? (isFirst ? Pick(state.Values, preferNew, previous, slotIndex, minGap, int.MaxValue, day, distinct, exposuresPerDay, strictGap: false, fillers: null) : null);
            if (pick is not null)
            {
                var candidate = pick.Candidate;
                var lexeme = candidate.Entry.Lexeme;
                entries.Add(new TimelineEntry(
                    entries.Count,
                    fireDate,
                    horizon,
                    candidate.Entry.Id,
                    lexeme.Lemma,
                    candidate.Entry.IpaGeneralAmerican ?? candidate.Entry.IpaReceivedPronunciation,
                    lexeme.PartOfSpeech.ToString().ToLowerInvariant(),
                    candidate.Entry.Translation,
                    candidate.Entry.ExampleTarget,
                    request.ThemeId,
                    request.FreeTier,
                    lexeme.Topics.Count > 0 ? lexeme.Topics[0].Slug : null));
                pick.TimesPlanned++;
                pick.LastSlot = slotIndex;
                distinct.Add(candidate.Entry.Id);
                exposuresPerDay[(day, candidate.Entry.Id)] = exposuresPerDay.GetValueOrDefault((day, candidate.Entry.Id)) + 1;
                previous = candidate.Entry.Id;
                if (candidate.IsNew)
                {
                    newUsed++;
                }
            }

            slotLocal = slotLocal.Add(cadence.Interval);
            slotIndex++;
        }

        for (var i = 0; i < entries.Count - 1; i++)
        {
            entries[i] = entries[i] with { PlannedUntil = entries[i + 1].FireDate };
        }

        return new WordTimeline(WordTimeline.CurrentSchemaVersion, now, horizon, cadence.Minutes, mode, settings.Pair, TimelineFallback.OpenApp, entries);
    }

    /// <summary>Slots between two showings of the same word: spread the daily cap over the active part of the day (spec 06 §3.5.2).</summary>
    public static int MinGap(LearnerSettings settings, RotationCadence cadence, int budget)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var quietMinutes = ((settings.QuietHours.End - settings.QuietHours.Start).TotalMinutes + 1440) % 1440;
        var activeSlots = Math.Max(1, (int)((1440 - quietMinutes) / cadence.Minutes));
        return Math.Max(2, (int)Math.Ceiling(activeSlots / (double)Math.Max(1, budget)));
    }

    /// <summary>Candidates for one slot; <paramref name="fillers"/> selects the tier (false = words of the day, true = fillers, null = any). Fewest showings that day first, so a replan continues the rotation instead of restarting it.</summary>
    private static CandidateState? Pick(
        IEnumerable<CandidateState> candidates,
        bool preferNew,
        LexemeId? previous,
        int slotIndex,
        int minGap,
        int budget,
        LearningDay day,
        HashSet<LexemeId> distinctToday,
        Dictionary<(LearningDay Day, LexemeId Lexeme), int> exposuresPerDay,
        bool strictGap,
        bool? fillers)
    {
        var eligible = candidates.Where(c =>
                c.Candidate.Entry.Id != previous
                && (fillers is null || c.Candidate.Filler == fillers)
                && Exposures(c, day, exposuresPerDay) < budget
                && (distinctToday.Contains(c.Candidate.Entry.Id) || distinctToday.Count < MaxDistinctWordsPerDay)
                && (!strictGap || c.LastSlot < 0 || slotIndex - c.LastSlot >= minGap))
            .OrderBy(c => Exposures(c, day, exposuresPerDay))
            .ThenBy(c => c.TimesPlanned)
            .ThenBy(c => c.LastSlot)
            .ThenBy(c => c.Candidate.Priority)
            .ThenBy(c => c.Candidate.Entry.Lexeme.Lemma, StringComparer.Ordinal)
            .ToList();
        if (eligible.Count == 0)
        {
            return null;
        }

        return eligible.FirstOrDefault(c => c.Candidate.IsNew == preferNew) ?? eligible[0];
    }

    private static int Exposures(CandidateState state, LearningDay day, Dictionary<(LearningDay Day, LexemeId Lexeme), int> planned)
    {
        var baseline = state.Candidate.ExposuresToday;
        return planned.GetValueOrDefault((day, state.Candidate.Entry.Id)) + (state.FirstDay == day ? baseline : 0);
    }

    private static DateTimeOffset Truncate(DateTimeOffset value) => new(value.UtcTicks - (value.UtcTicks % TimeSpan.TicksPerSecond), TimeSpan.Zero);

    private static DateTimeOffset ToInstant(DateTime local, TimeZoneInfo timeZone)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (timeZone.IsInvalidTime(unspecified))
        {
            unspecified = unspecified.AddHours(1);
        }

        return new DateTimeOffset(unspecified, timeZone.GetUtcOffset(unspecified)).ToUniversalTime();
    }

    private sealed class CandidateState(TimelineCandidate candidate)
    {
        public TimelineCandidate Candidate { get; } = candidate;

        public int TimesPlanned { get; set; }

        public int LastSlot { get; set; } = -1;

        /// <summary>The learning day the candidate's existing exposures belong to: the day of the request.</summary>
        public LearningDay? FirstDay { get; set; }
    }
}
