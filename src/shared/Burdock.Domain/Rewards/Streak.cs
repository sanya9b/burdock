using Burdock.Domain.Time;

namespace Burdock.Domain.Rewards;

/// <summary>
/// Daily streak of learning days (spec 07 §3.3, Daily mode) with freezes (FR-07-3): one free freeze a week is banked automatically, at most
/// <see cref="StreakRules.MaxBankedFreezes"/> are kept, and a missed day consumes one silently. Pauses and repair arrive later; a gap the bank
/// cannot cover simply starts a new streak, and nothing is ever lost besides the count.
/// </summary>
public sealed record StreakState(int Length, int Longest, LearningDay? LastCountedDay)
{
    public static StreakState Empty { get; } = new(0, 0, null);

    /// <summary>Freezes in the bank (0..<see cref="StreakRules.MaxBankedFreezes"/>).</summary>
    public int FreezesBanked { get; init; }

    /// <summary>Week number (Monday-based, DayNumber / 7) of the last automatic grant; null before the first.</summary>
    public int? LastFreezeGrantWeek { get; init; }

    /// <summary>Day of the last freeze bought in the shop (purchase cooldown, spec 07 §3.2.3).</summary>
    public LearningDay? LastFreezePurchaseDay { get; init; }

    /// <summary>Length as seen on a given day: alive when yesterday or today was counted, or when the bank covers every missed day since.</summary>
    public int LengthOn(LearningDay today)
    {
        if (LastCountedDay is not { } last)
        {
            return 0;
        }

        var gap = today.DaysSince(last);
        return gap <= 1 || gap - 1 <= FreezesBanked ? Length : 0;
    }

    public bool IsCountedOn(LearningDay day) => LastCountedDay == day;
}

/// <summary>What counting a day did to the freezes, for the ledger and the summary.</summary>
public sealed record StreakCount(StreakState State, int FreezesConsumed);

public static class StreakRules
{
    private static readonly int[] FixedMilestones = [3, 7, 14, 30, 60, 100, 365];

    /// <summary>streak.freeze.maxBanked (spec 07 §3.12).</summary>
    public const int MaxBankedFreezes = 2;

    /// <summary>Counts a learning day: +1 when it follows the last counted day, unchanged for the same day, a fresh 1 after a gap.</summary>
    public static StreakState Count(StreakState state, LearningDay day) => CountWithFreezes(state, day).State;

    /// <summary>
    /// Counts a learning day and lets banked freezes cover the missed days in between (one per missed day, FR-07-3). A gap the bank cannot
    /// cover starts a new streak and keeps the bank: freezes are never lost.
    /// </summary>
    public static StreakCount CountWithFreezes(StreakState state, LearningDay day)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.LastCountedDay is { } last)
        {
            var gap = day.DaysSince(last);
            if (gap <= 0)
            {
                return new StreakCount(state, 0);
            }

            var missed = gap - 1;
            if (missed > 0 && missed <= state.FreezesBanked)
            {
                var continued = state.Length + 1;
                return new StreakCount(state with { Length = continued, Longest = Math.Max(state.Longest, continued), LastCountedDay = day, FreezesBanked = state.FreezesBanked - missed }, missed);
            }

            var length = gap == 1 ? state.Length + 1 : 1;
            return new StreakCount(state with { Length = length, Longest = Math.Max(state.Longest, length), LastCountedDay = day }, 0);
        }

        return new StreakCount(state with { Length = 1, Longest = Math.Max(state.Longest, 1), LastCountedDay = day }, 0);
    }

    /// <summary>Monday-based week number of a day (DateOnly.MinValue is a Monday).</summary>
    public static int WeekOf(LearningDay day) => day.Date.DayNumber / 7;

    /// <summary>The weekly free freeze (FR-07-3): once per week, only while the bank is below the cap; returns null when nothing was granted.</summary>
    public static StreakState? GrantWeeklyFreeze(StreakState state, LearningDay day)
    {
        ArgumentNullException.ThrowIfNull(state);
        var week = WeekOf(day);
        if (state.LastFreezeGrantWeek == week || state.FreezesBanked >= MaxBankedFreezes)
        {
            return null;
        }

        return state with { FreezesBanked = state.FreezesBanked + 1, LastFreezeGrantWeek = week };
    }

    /// <summary>{3, 7, 14, 30, 60, 100, 365} and every further hundred (spec 07 FR-07-6).</summary>
    public static bool IsMilestone(int length) => length > 0 && (FixedMilestones.Contains(length) || (length % 100 == 0 && length >= 200));

    /// <summary>Thorns for a milestone (spec 07 §3.2.2): 15 / 30 / 50 / 100 / 150 / 250 / 500, later hundreds 250.</summary>
    public static int MilestoneThorns(int length) => length switch
    {
        3 => 15,
        7 => 30,
        14 => 50,
        30 => 100,
        60 => 150,
        100 => 250,
        365 => 500,
        _ when IsMilestone(length) => 250,
        _ => 0,
    };

    /// <summary>Celebration ladder level for a milestone (spec 07 §3.8.1): 3-14 → L2, 30-60 → L3, 100+ → L4.</summary>
    public static int CelebrationLevel(int length) => length switch
    {
        >= 100 => 4,
        >= 30 => 3,
        >= 3 => 2,
        _ => 0,
    };
}
