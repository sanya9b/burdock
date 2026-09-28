using Burdock.Domain.Time;

namespace Burdock.Domain.Rewards;

/// <summary>Daily streak of learning days (spec 07 §3.3, Daily mode). Freezes, pauses and repair arrive with the full economy; a gap simply starts a new streak, and nothing is ever lost besides the count.</summary>
public sealed record StreakState(int Length, int Longest, LearningDay? LastCountedDay)
{
    public static StreakState Empty { get; } = new(0, 0, null);

    /// <summary>Length as seen on a given day: a streak whose last counted day is older than yesterday has ended.</summary>
    public int LengthOn(LearningDay today)
        => LastCountedDay is { } last && today.DaysSince(last) <= 1 ? Length : 0;

    public bool IsCountedOn(LearningDay day) => LastCountedDay == day;
}

public static class StreakRules
{
    private static readonly int[] FixedMilestones = [3, 7, 14, 30, 60, 100, 365];

    /// <summary>Counts a learning day: +1 when it follows the last counted day, unchanged for the same day, a fresh 1 after a gap.</summary>
    public static StreakState Count(StreakState state, LearningDay day)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.LastCountedDay is { } last)
        {
            var gap = day.DaysSince(last);
            if (gap <= 0)
            {
                return state;
            }

            var length = gap == 1 ? state.Length + 1 : 1;
            return new StreakState(length, Math.Max(state.Longest, length), day);
        }

        return new StreakState(1, Math.Max(state.Longest, 1), day);
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
