using Burdock.Domain.Time;

namespace Burdock.Domain.Rewards;

public enum Currency
{
    Thorns,
    Xp,
    None,
}

/// <summary>Ledger event kinds (spec 07 §3.7.1 subset). New kinds are appended: the SQLite store keeps the integer value.</summary>
public enum LedgerEventType
{
    Earn,
    StreakMilestone,
    AchievementUnlock,
    Spend,
    FreezeGrant,
    FreezeConsume,
    CalendarClaim,
    TaskComplete,
    CalendarTrack,
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

    /// <summary>Owned and worn cosmetics (spec 07 §3.2.3).</summary>
    public Inventory Inventory { get; init; } = Inventory.Empty;

    /// <summary>Distinct learning days ever counted.</summary>
    public int LearningDays { get; init; }

    /// <summary>Login calendar (spec 07 §3.4).</summary>
    public CalendarState Calendar { get; init; } = CalendarState.Empty;

    /// <summary>Selected and completed micro-tasks (spec 07 §3.5).</summary>
    public TaskState Tasks { get; init; } = TaskState.Empty;

    /// <summary>Uncapped counters of the current learning day, for task progress; null before the first session.</summary>
    public DayActivity? Activity { get; init; }

    /// <summary>Counters of the current week, for the weekly task.</summary>
    public WeekActivity? Week { get; init; }

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
    bool WarmupCompleted,
    int NewIntroduced = 0);

/// <summary>Totals the application reads from its stores before settling (they already include the session being settled).</summary>
public sealed record ProgressCounters(int LearnedWords, int TypedCorrectTotal, int GradedTotal, int CardsInLearning = 0);

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
    IReadOnlyList<AchievementDefinition> NewAchievements,
    int FreezesConsumed = 0,
    bool FreezeGranted = false)
{
    public bool LeveledUp => LevelAfter > LevelBefore;

    /// <summary>Micro-tasks this session completed (spec 07 §3.5), in catalogue order.</summary>
    public IReadOnlyList<TaskDefinition> TasksCompleted { get; init; } = [];

    public bool IsEmpty => Events.Count == 0;

    /// <summary>Mascot growth before this session (spec 07 §3.2.7); XP never decreases, so this is the state minus what was just earned.</summary>
    public GrowthSnapshot GrowthBefore => GrowthRules.For(State.Xp - XpGained);

    public GrowthSnapshot GrowthAfter => GrowthRules.For(State.Xp);

    /// <summary>The burr moved to a new stage; the summary celebrates growth instead of a plain level-up.</summary>
    public bool StageChanged => GrowthAfter.Stage != GrowthBefore.Stage;
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
        => Settle(state, report, counters, day, now, newId, null, Guid.Empty);

    /// <summary>Settlement that also selects and evaluates micro-tasks (spec 07 §3.5) when a <paramref name="tasks"/> context is given.</summary>
    public static SessionSettlement Settle(RewardsState state, SessionReport report, ProgressCounters counters, LearningDay day, DateTimeOffset now, Func<Guid> newId, TaskContext? tasks, Guid learnerKey)
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
        var learningDays = state.LearningDays;
        var freezesConsumed = 0;
        var freezeGranted = false;
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

            // The weekly free freeze is banked before counting, so the first learning day of a week can already cover the gap before it.
            if (StreakRules.GrantWeeklyFreeze(streak, day) is { } granted)
            {
                streak = granted;
                freezeGranted = true;
                events.Add(new LedgerEvent(newId(), LedgerEventType.FreezeGrant, 0, Currency.None, "streak.freeze.weekly", report.SessionId, now, day));
            }

            if (!streak.IsCountedOn(day))
            {
                learningDays++;
            }

            var counted = StreakRules.CountWithFreezes(streak, day);
            streak = counted.State;
            freezesConsumed = counted.FreezesConsumed;
            for (var i = 0; i < freezesConsumed; i++)
            {
                events.Add(new LedgerEvent(newId(), LedgerEventType.FreezeConsume, 0, Currency.None, "streak.freeze.applied", report.SessionId, now, day));
            }
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

        // Micro-tasks (spec 07 §3.5): activity counters first, then selection for the day and the week, then completion.
        var sessions = state.SessionsCompleted + 1;
        var newLearningDay = report.LearningDayCounted && !state.Streak.IsCountedOn(day);
        var activity = (state.Activity is { } act && act.Day == day ? act : DayActivity.For(day)).Record(report);
        var weekNo = StreakRules.WeekOf(day);
        var week = (state.Week is { } wk && wk.Week == weekNo ? wk : WeekActivity.For(weekNo)).Record(report, newLearningDay);
        var taskState = state.Tasks;
        var tasksCompleted = new List<TaskDefinition>();
        if (tasks is not null)
        {
            taskState = TaskRules.EnsureSelection(taskState, learnerKey, day, tasks);
            var totals = new TaskTotals(sessions, counters.TypedCorrectTotal, counters.LearnedWords);
            var dailyDone = new HashSet<string>(taskState.DailyDone, StringComparer.Ordinal);
            foreach (var id in taskState.Daily)
            {
                if (dailyDone.Contains(id) || TaskCatalog.Find(id) is not { } task || !TaskRules.IsComplete(task, activity, week, totals))
                {
                    continue;
                }

                dailyDone.Add(id);
                tasksCompleted.Add(task);
                Earn(Currency.Thorns, Capped(task.Thorns), $"task:{id}", LedgerEventType.TaskComplete);
                Earn(Currency.Xp, task.Xp, $"xp.task:{id}", LedgerEventType.TaskComplete);
            }

            var weeklyDone = taskState.WeeklyDone;
            if (!weeklyDone && taskState.WeeklyId is { } weeklyId && TaskCatalog.Find(weeklyId) is { } weekly && TaskRules.IsComplete(weekly, activity, week, totals))
            {
                weeklyDone = true;
                tasksCompleted.Add(weekly);
                Earn(Currency.Thorns, weekly.Thorns, $"task:{weeklyId}", LedgerEventType.TaskComplete);
                Earn(Currency.Xp, weekly.Xp, $"xp.task:{weeklyId}", LedgerEventType.TaskComplete);
            }

            var oneOffDone = new HashSet<string>(taskState.OneOffDone, StringComparer.Ordinal);
            foreach (var oneOff in TaskCatalog.OneOff)
            {
                if (oneOffDone.Contains(oneOff.Id) || !TaskRules.IsComplete(oneOff, activity, week, totals))
                {
                    continue;
                }

                oneOffDone.Add(oneOff.Id);
                tasksCompleted.Add(oneOff);
                Earn(Currency.Thorns, oneOff.Thorns, $"task:{oneOff.Id}", LedgerEventType.TaskComplete);
                Earn(Currency.Xp, oneOff.Xp, $"xp.task:{oneOff.Id}", LedgerEventType.TaskComplete);
            }

            taskState = taskState with { DailyDone = dailyDone, WeeklyDone = weeklyDone, OneOffDone = oneOffDone };
        }

        // Achievements: evaluated on the state after this session.
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

        var next = new RewardsState(state.Thorns + thorns, state.Xp + xp, streak, unlocked, sessions, today, report.SessionId)
        {
            Inventory = state.Inventory,
            LearningDays = learningDays,
            Calendar = state.Calendar,
            Tasks = taskState,
            Activity = activity,
            Week = week,
        };
        return new SessionSettlement(next, events, xp, thorns, levelBefore, next.Level, streakBefore, streakAfter, milestone, fresh, freezesConsumed, freezeGranted) { TasksCompleted = tasksCompleted };
    }

    /// <summary>
    /// Claims the login calendar for the day (spec 07 §3.4, FR-07-9): the cycle reward as a <see cref="LedgerEventType.CalendarClaim"/> outside the daily cap,
    /// and the 30-day track reward when the count lands on it (day 60 gifts the frame «Небесна», day 90 and every 30 days after pay thorns). Idempotent per day.
    /// </summary>
    public static CalendarSettlement ClaimCalendar(RewardsState state, LearningDay day, DateTimeOffset now, Func<Guid> newId)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(newId);
        var claim = CalendarRules.Claim(state.Calendar, day);
        if (claim is null)
        {
            return new CalendarSettlement(state, [], null);
        }

        var events = new List<LedgerEvent>
        {
            new(newId(), LedgerEventType.CalendarClaim, claim.Reward, Currency.Thorns, $"economy.calendar.cycle:{claim.Position}", null, now, day),
        };
        var thorns = state.Thorns + claim.Reward;
        var inventory = state.Inventory;
        if (claim.Track is { } track)
        {
            switch (track.Kind)
            {
                case CalendarTrackKind.FrameSky when Catalog.Find(Catalog.CalendarFrameKey) is { } frame:
                    inventory = inventory.With(frame);
                    if (inventory.Frame is null)
                    {
                        inventory = inventory with { Frame = frame.Key };
                    }

                    events.Add(new LedgerEvent(newId(), LedgerEventType.CalendarTrack, 0, Currency.None, $"economy.calendar.track:{track.Day}", null, now, day));
                    break;
                case CalendarTrackKind.Thorns:
                    thorns += track.Thorns;
                    events.Add(new LedgerEvent(newId(), LedgerEventType.CalendarTrack, track.Thorns, Currency.Thorns, $"economy.calendar.track:{track.Day}", null, now, day));
                    break;
                default:
                    events.Add(new LedgerEvent(newId(), LedgerEventType.CalendarTrack, 0, Currency.None, $"economy.calendar.track:{track.Day}", null, now, day));
                    break;
            }
        }

        var next = state with { Thorns = thorns, Inventory = inventory, Calendar = claim.State };
        return new CalendarSettlement(next, events, claim);
    }
}
