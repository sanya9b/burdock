using Burdock.Domain.Time;

namespace Burdock.Domain.Rewards;

public enum Currency
{
    Thorns,
    Xp,
}

public enum LedgerEventType
{
    Earn,
    StreakMilestone,
    AchievementUnlock,
}

/// <summary>Append-only economy event (spec 07 §3.7.1 subset: no signatures or suspicion flags until the server reconciles).</summary>
public sealed record LedgerEvent(
    Guid Id,
    LedgerEventType Type,
    int Amount,
    Currency Currency,
    string Reason,
    Guid? SourceEventId,
    DateTimeOffset OccurredAt,
    LearningDay LearningDay);

/// <summary>Repeatable earnings of one learning day, for the caps of spec 07 §3.2.2.</summary>
public sealed record DailyEarn(LearningDay Day, int RepeatableThorns, int Sessions, int WordsLearned, bool LearningDayBonusGiven)
{
    public static DailyEarn For(LearningDay day) => new(day, 0, 0, 0, false);
}

/// <summary>Folded economy state kept per learner (spec 07 §3.7.1 EconomyState, phase-1 subset).</summary>
public sealed record RewardsState(
    int Thorns,
    int Xp,
    StreakState Streak,
    IReadOnlySet<string> Achievements,
    int SessionsCompleted,
    DailyEarn? Today,
    Guid? LastSessionId)
{
    public static RewardsState Empty { get; } = new(0, 0, StreakState.Empty, new HashSet<string>(StringComparer.Ordinal), 0, null, null);

    public int Level => XpRules.LevelFor(Xp);
}

/// <summary>What a finished session reports to the economy.</summary>
public sealed record SessionReport(
    Guid SessionId,
    int Graded,
    int Correct,
    int AgainCount,
    int TypedCorrect,
    int WordsLearned,
    bool LearningDayCounted,
    bool WarmupCompleted);

/// <summary>Totals the application reads from its stores before settling (they already include the session being settled).</summary>
public sealed record ProgressCounters(int LearnedWords, int TypedCorrectTotal, int GradedTotal);

/// <summary>Everything the summary screen celebrates.</summary>
public sealed record SessionSettlement(
    RewardsState State,
    IReadOnlyList<LedgerEvent> Events,
    int XpGained,
    int ThornsGained,
    int LevelBefore,
    int LevelAfter,
    int StreakBefore,
    int StreakAfter,
    int? StreakMilestone,
    IReadOnlyList<AchievementDefinition> NewAchievements)
{
    public bool LeveledUp => LevelAfter > LevelBefore;

    public bool IsEmpty => Events.Count == 0;
}

/// <summary>Pure settlement of a session (spec 07 §3.2, §3.3, §3.6). Idempotent per session id; the daily cap applies to repeatable sources only.</summary>
public static class RewardsEngine
{
    public const int SessionThorns = 10;
    public const int SessionsCappedPerDay = 3;
    public const int LearningDayThorns = 5;
    public const int WordLearnedThorns = 5;
    public const int WordsLearnedCappedPerDay = 10;
    public const int DailyEarnCap = 150;

    public static SessionSettlement Settle(RewardsState state, SessionReport report, ProgressCounters counters, LearningDay day, DateTimeOffset now, Func<Guid> newId)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(counters);
        ArgumentNullException.ThrowIfNull(newId);
        if (state.LastSessionId == report.SessionId)
        {
            var current = state.Streak.LengthOn(day);
            return new SessionSettlement(state, [], 0, 0, state.Level, state.Level, current, current, null, []);
        }

        var events = new List<LedgerEvent>();
        var today = state.Today is { } t && t.Day == day ? t : DailyEarn.For(day);
        var levelBefore = state.Level;
        var streakBefore = state.Streak.LengthOn(day);
        var xp = 0;
        var thorns = 0;

        void Earn(Currency currency, int amount, string reason, LedgerEventType type = LedgerEventType.Earn)
        {
            if (amount <= 0)
            {
                return;
            }

            events.Add(new LedgerEvent(newId(), type, amount, currency, reason, report.SessionId, now, day));
            if (currency == Currency.Xp)
            {
                xp += amount;
            }
            else
            {
                thorns += amount;
            }
        }

        int Capped(int amount)
        {
            var allowed = Math.Max(0, Math.Min(amount, DailyEarnCap - today.RepeatableThorns));
            today = today with { RepeatableThorns = today.RepeatableThorns + allowed };
            return allowed;
        }

        // XP: graded cards (an Again still counts), the learning day, the warm-up.
        Earn(Currency.Xp, report.AgainCount * XpRules.GradedCardAgain + Math.Max(0, report.Graded - report.AgainCount) * XpRules.GradedCardHardOrBetter, "xp.cards");
        if (report.LearningDayCounted)
        {
            Earn(Currency.Xp, XpRules.SessionWithLearningDay, "xp.session");
        }

        if (report.WarmupCompleted)
        {
            Earn(Currency.Xp, XpRules.WarmupCompleted, "xp.warmup");
        }

        // Thorns: session, first session of the learning day, learned words; all under the daily cap.
        var streak = state.Streak;
        if (report.LearningDayCounted)
        {
            if (today.Sessions < SessionsCappedPerDay)
            {
                Earn(Currency.Thorns, Capped(SessionThorns), "economy.earn.session");
            }

            today = today with { Sessions = today.Sessions + 1 };
            if (!today.LearningDayBonusGiven)
            {
                Earn(Currency.Thorns, Capped(LearningDayThorns), "economy.earn.learningDay");
                today = today with { LearningDayBonusGiven = true };
            }

            streak = StreakRules.Count(streak, day);
        }

        var rewardableWords = Math.Max(0, Math.Min(report.WordsLearned, WordsLearnedCappedPerDay - today.WordsLearned));
        if (rewardableWords > 0)
        {
            Earn(Currency.Thorns, Capped(rewardableWords * WordLearnedThorns), "economy.earn.wordLearned");
            today = today with { WordsLearned = today.WordsLearned + rewardableWords };
        }

        // Streak milestone: once per streak length, outside the daily cap.
        int? milestone = null;
        var streakAfter = streak.LengthOn(day);
        if (streakAfter > streakBefore && StreakRules.IsMilestone(streakAfter))
        {
            milestone = streakAfter;
            Earn(Currency.Thorns, StreakRules.MilestoneThorns(streakAfter), $"economy.earn.streakMilestone:{streakAfter}", LedgerEventType.StreakMilestone);
        }

        // Achievements: evaluated on the state after this session.
        var sessions = state.SessionsCompleted + 1;
        var level = XpRules.LevelFor(state.Xp + xp);
        var progress = new ProgressSnapshot(sessions, counters.LearnedWords, streakAfter, counters.TypedCorrectTotal, counters.GradedTotal, level, report.Graded, report.AgainCount > 0);
        var unlocked = new HashSet<string>(state.Achievements, StringComparer.Ordinal);
        var fresh = AchievementCatalog.NewlyUnlocked(progress, unlocked);
        foreach (var achievement in fresh)
        {
            unlocked.Add(achievement.Id);
            Earn(Currency.Thorns, achievement.Thorns, $"economy.earn.achievement:{achievement.Id}", LedgerEventType.AchievementUnlock);
            Earn(Currency.Xp, XpRules.Achievement, $"xp.achievement:{achievement.Id}", LedgerEventType.AchievementUnlock);
        }

        var next = new RewardsState(state.Thorns + thorns, state.Xp + xp, streak, unlocked, sessions, today, report.SessionId);
        return new SessionSettlement(next, events, xp, thorns, levelBefore, next.Level, streakBefore, streakAfter, milestone, fresh);
    }
}
