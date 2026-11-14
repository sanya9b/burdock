using Burdock.Domain.Time;

namespace Burdock.Domain.Rewards;

/// <summary>
/// Login calendar (spec 07 §3.4, FR-07-8, FR-07-9): a 7-position cycle of thorns claimed automatically on the first app open of a learning day,
/// a missed day never resets the position, and a 30-day track of claimed days (not consecutive) with its own rewards.
/// </summary>
public sealed record CalendarState(int Position, LearningDay? LastClaimedDay, int ClaimedDays)
{
    public static CalendarState Empty { get; } = new(0, null, 0);

    public bool IsClaimedOn(LearningDay day) => LastClaimedDay == day;
}

public enum CalendarTrackKind
{
    /// <summary>Day 30: the widget theme «Луг» becomes available in the shop (its unlock condition is 30 claimed days).</summary>
    ThemeMeadow,

    /// <summary>Day 60: the frame «Небесна» is added to the inventory as a gift.</summary>
    FrameSky,

    /// <summary>Day 90: 100 thorns; every 30 days after: 50 thorns.</summary>
    Thorns,
}

public sealed record CalendarTrackReward(CalendarTrackKind Kind, int Thorns, int Day);

/// <summary>One claim: the state after it, what the cycle paid, the 1-based position paid, and the track reward reached, if any.</summary>
public sealed record CalendarClaim(CalendarState State, int Reward, int Position, CalendarTrackReward? Track);

public static class CalendarRules
{
    public const int ThemeDay = 30;
    public const int FrameDay = 60;
    public const int BonusDay = 90;
    public const int BonusThorns = 100;
    public const int RepeatEvery = 30;
    public const int RepeatThorns = 50;

    /// <summary>`economy.calendar.cycle` of spec 07 §3.4.</summary>
    public static IReadOnlyList<int> Cycle { get; } = [5, 5, 10, 10, 15, 15, 30];

    /// <summary>Claims the day once; a second call on the same learning day returns null.</summary>
    public static CalendarClaim? Claim(CalendarState state, LearningDay day)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.IsClaimedOn(day))
        {
            return null;
        }

        var reward = Cycle[state.Position];
        var claimedDays = state.ClaimedDays + 1;
        var next = new CalendarState((state.Position + 1) % Cycle.Count, day, claimedDays);
        return new CalendarClaim(next, reward, state.Position + 1, TrackRewardAt(claimedDays));
    }

    /// <summary>The track reward that lands exactly on this count of claimed days, if any.</summary>
    public static CalendarTrackReward? TrackRewardAt(int claimedDays) => claimedDays switch
    {
        ThemeDay => new CalendarTrackReward(CalendarTrackKind.ThemeMeadow, 0, ThemeDay),
        FrameDay => new CalendarTrackReward(CalendarTrackKind.FrameSky, 0, FrameDay),
        BonusDay => new CalendarTrackReward(CalendarTrackKind.Thorns, BonusThorns, BonusDay),
        > BonusDay when claimedDays % RepeatEvery == 0 => new CalendarTrackReward(CalendarTrackKind.Thorns, RepeatThorns, claimedDays),
        _ => null,
    };

    /// <summary>The next track milestone after this many claimed days («18 / 30 днів до теми «Луг»»), never a deadline.</summary>
    public static CalendarTrackReward NextTrack(int claimedDays)
    {
        if (claimedDays < ThemeDay)
        {
            return new CalendarTrackReward(CalendarTrackKind.ThemeMeadow, 0, ThemeDay);
        }

        if (claimedDays < FrameDay)
        {
            return new CalendarTrackReward(CalendarTrackKind.FrameSky, 0, FrameDay);
        }

        if (claimedDays < BonusDay)
        {
            return new CalendarTrackReward(CalendarTrackKind.Thorns, BonusThorns, BonusDay);
        }

        return new CalendarTrackReward(CalendarTrackKind.Thorns, RepeatThorns, (claimedDays / RepeatEvery + 1) * RepeatEvery);
    }
}

/// <summary>What claiming the calendar did to the economy state.</summary>
public sealed record CalendarSettlement(RewardsState State, IReadOnlyList<LedgerEvent> Events, CalendarClaim? Claim)
{
    public bool Claimed => Claim is not null;
}
