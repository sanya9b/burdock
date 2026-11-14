using Burdock.Application.Ports;
using Burdock.Domain.Rewards;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Time;
using SQLite;

namespace Burdock.Infrastructure.Persistence;

[Table("rewards_state")]
public sealed class RewardsStateRow
{
    [PrimaryKey]
    public Guid LearnerId { get; set; }

    public int Thorns { get; set; }

    public int Xp { get; set; }

    public int StreakLength { get; set; }

    public int StreakLongest { get; set; }

    /// <summary>Day number of the last counted learning day (DateOnly.DayNumber), 0 when none.</summary>
    public int StreakLastDay { get; set; }

    /// <summary>Comma-separated achievement ids.</summary>
    public string Achievements { get; set; } = string.Empty;

    public int SessionsCompleted { get; set; }

    public int TodayDay { get; set; }

    public int TodayRepeatableThorns { get; set; }

    public int TodaySessions { get; set; }

    public int TodayWordsLearned { get; set; }

    public bool TodayLearningDayBonus { get; set; }

    public Guid? LastSessionId { get; set; }

    /// <summary>Comma-separated catalogue keys the learner owns (spec 07 §3.2.3); the default theme is implicit.</summary>
    public string OwnedItems { get; set; } = string.Empty;

    public string? EquippedOutfit { get; set; }

    public string? EquippedFrame { get; set; }

    public string? EquippedTheme { get; set; }

    public string? EquippedScene { get; set; }

    public string? EquippedRide { get; set; }

    public int FreezesBanked { get; set; }

    /// <summary>Monday-based week number of the last automatic freeze grant, -1 when none.</summary>
    public int FreezeGrantWeek { get; set; } = -1;

    /// <summary>Day number of the last freeze bought in the shop, 0 when none.</summary>
    public int FreezePurchaseDay { get; set; }

    public int LearningDays { get; set; }

    /// <summary>Login calendar (spec 07 §3.4): next position 0..6, day number of the last claim (0 = none), days claimed ever.</summary>
    public int CalendarPosition { get; set; }

    public int CalendarLastDay { get; set; }

    public int CalendarClaimedDays { get; set; }

    /// <summary>Micro-tasks (spec 07 §3.5): the day of the selection (0 = none), comma-separated ids, the week (-1 = none).</summary>
    public int TasksDay { get; set; }

    public string TasksDaily { get; set; } = string.Empty;

    public string TasksDailyDone { get; set; } = string.Empty;

    public string TasksPreviousDaily { get; set; } = string.Empty;

    public int TasksWeek { get; set; } = -1;

    public string? TasksWeeklyId { get; set; }

    public bool TasksWeeklyDone { get; set; }

    public string TasksOneOffDone { get; set; } = string.Empty;

    /// <summary>Activity counters of the current day (0 = none) and week (-1 = none).</summary>
    public int ActivityDay { get; set; }

    public int ActivityGraded { get; set; }

    public int ActivityNew { get; set; }

    public int ActivityTyped { get; set; }

    public int ActivitySessions { get; set; }

    public int ActivityWarmups { get; set; }

    public int ActivityLearned { get; set; }

    public int ActivityAccurate { get; set; }

    public int ActivityLongest { get; set; }

    public int WeekNumber { get; set; } = -1;

    public int WeekGraded { get; set; }

    public int WeekNew { get; set; }

    public int WeekTyped { get; set; }

    public int WeekLearned { get; set; }

    public int WeekDays { get; set; }
}

[Table("ledger_events")]
public sealed class LedgerEventRow
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Indexed]
    public Guid LearnerId { get; set; }

    public int Type { get; set; }

    public int Amount { get; set; }

    public int Currency { get; set; }

    public string Reason { get; set; } = string.Empty;

    public Guid? SourceEventId { get; set; }

    public long OccurredAtTicks { get; set; }

    public int LearningDay { get; set; }

    public bool Synced { get; set; }
}

/// <summary>Rewards state and ledger in SQLite (spec 07 §3.7); the ledger stays append-only and is what the server will reconcile.</summary>
public sealed class SqliteRewardsStore(BurdockDatabase database) : IRewardsStore
{
    public async Task<RewardsState?> GetAsync(LearnerId learnerId, CancellationToken cancellationToken)
    {
        var db = await database.GetOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var learner = learnerId.Value;
        var row = await db.Table<RewardsStateRow>().Where(r => r.LearnerId == learner).FirstOrDefaultAsync().ConfigureAwait(false);
        return row is null ? null : ToState(row);
    }

    public async Task SaveAsync(LearnerId learnerId, RewardsState state, IReadOnlyList<LedgerEvent> newEvents, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(newEvents);
        var db = await database.GetOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var row = ToRow(learnerId, state);
        var events = newEvents.Select(e => new LedgerEventRow
        {
            Id = e.Id,
            LearnerId = learnerId.Value,
            Type = (int)e.Type,
            Amount = e.Amount,
            Currency = (int)e.Currency,
            Reason = e.Reason,
            SourceEventId = e.SourceEventId,
            OccurredAtTicks = Ticks.From(e.OccurredAt),
            LearningDay = e.LearningDay.Date.DayNumber,
        }).ToList();
        await db.RunInTransactionAsync(connection =>
        {
            connection.InsertOrReplace(row);
            foreach (var e in events)
            {
                connection.Insert(e);
            }
        }).ConfigureAwait(false);
    }

    private static RewardsState ToState(RewardsStateRow row)
    {
        var lastDay = row.StreakLastDay > 0 ? new LearningDay(DateOnly.FromDayNumber(row.StreakLastDay)) : (LearningDay?)null;
        var achievements = new HashSet<string>(row.Achievements.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), StringComparer.Ordinal);
        var today = row.TodayDay > 0
            ? new DailyEarn(new LearningDay(DateOnly.FromDayNumber(row.TodayDay)), row.TodayRepeatableThorns, row.TodaySessions, row.TodayWordsLearned, row.TodayLearningDayBonus)
            : null;
        var streak = new StreakState(row.StreakLength, row.StreakLongest, lastDay)
        {
            FreezesBanked = row.FreezesBanked,
            LastFreezeGrantWeek = row.FreezeGrantWeek >= 0 ? row.FreezeGrantWeek : null,
            LastFreezePurchaseDay = row.FreezePurchaseDay > 0 ? new LearningDay(DateOnly.FromDayNumber(row.FreezePurchaseDay)) : null,
        };
        var owned = new HashSet<string>(Split(row.OwnedItems), StringComparer.Ordinal) { Catalog.DefaultThemeKey };
        var inventory = new Inventory(owned, row.EquippedOutfit, row.EquippedFrame, string.IsNullOrEmpty(row.EquippedTheme) ? Catalog.DefaultThemeKey : row.EquippedTheme, row.EquippedScene, row.EquippedRide);
        var calendar = new CalendarState(row.CalendarPosition, row.CalendarLastDay > 0 ? new LearningDay(DateOnly.FromDayNumber(row.CalendarLastDay)) : null, row.CalendarClaimedDays);
        var tasks = new TaskState(
            row.TasksDay > 0 ? new LearningDay(DateOnly.FromDayNumber(row.TasksDay)) : null,
            Split(row.TasksDaily),
            new HashSet<string>(Split(row.TasksDailyDone), StringComparer.Ordinal),
            Split(row.TasksPreviousDaily),
            row.TasksWeek >= 0 ? row.TasksWeek : null,
            string.IsNullOrEmpty(row.TasksWeeklyId) ? null : row.TasksWeeklyId,
            row.TasksWeeklyDone,
            new HashSet<string>(Split(row.TasksOneOffDone), StringComparer.Ordinal));
        var activity = row.ActivityDay > 0
            ? new DayActivity(new LearningDay(DateOnly.FromDayNumber(row.ActivityDay)), row.ActivityGraded, row.ActivityNew, row.ActivityTyped, row.ActivitySessions, row.ActivityWarmups, row.ActivityLearned, row.ActivityAccurate, row.ActivityLongest)
            : null;
        var week = row.WeekNumber >= 0 ? new WeekActivity(row.WeekNumber, row.WeekGraded, row.WeekNew, row.WeekTyped, row.WeekLearned, row.WeekDays) : null;
        return new RewardsState(row.Thorns, row.Xp, streak, achievements, row.SessionsCompleted, today, row.LastSessionId)
        {
            Inventory = inventory,
            LearningDays = row.LearningDays,
            Calendar = calendar,
            Tasks = tasks,
            Activity = activity,
            Week = week,
        };
    }

    private static RewardsStateRow ToRow(LearnerId learnerId, RewardsState state) => new()
    {
        LearnerId = learnerId.Value,
        Thorns = state.Thorns,
        Xp = state.Xp,
        StreakLength = state.Streak.Length,
        StreakLongest = state.Streak.Longest,
        StreakLastDay = state.Streak.LastCountedDay?.Date.DayNumber ?? 0,
        Achievements = string.Join(",", state.Achievements.Order(StringComparer.Ordinal)),
        SessionsCompleted = state.SessionsCompleted,
        TodayDay = state.Today?.Day.Date.DayNumber ?? 0,
        TodayRepeatableThorns = state.Today?.RepeatableThorns ?? 0,
        TodaySessions = state.Today?.Sessions ?? 0,
        TodayWordsLearned = state.Today?.WordsLearned ?? 0,
        TodayLearningDayBonus = state.Today?.LearningDayBonusGiven ?? false,
        LastSessionId = state.LastSessionId,
        OwnedItems = string.Join(",", state.Inventory.Owned.Order(StringComparer.Ordinal)),
        EquippedOutfit = state.Inventory.Outfit,
        EquippedFrame = state.Inventory.Frame,
        EquippedTheme = state.Inventory.Theme,
        EquippedScene = state.Inventory.Scene,
        EquippedRide = state.Inventory.Ride,
        FreezesBanked = state.Streak.FreezesBanked,
        FreezeGrantWeek = state.Streak.LastFreezeGrantWeek ?? -1,
        FreezePurchaseDay = state.Streak.LastFreezePurchaseDay?.Date.DayNumber ?? 0,
        LearningDays = state.LearningDays,
        CalendarPosition = state.Calendar.Position,
        CalendarLastDay = state.Calendar.LastClaimedDay?.Date.DayNumber ?? 0,
        CalendarClaimedDays = state.Calendar.ClaimedDays,
        TasksDay = state.Tasks.Day?.Date.DayNumber ?? 0,
        TasksDaily = string.Join(",", state.Tasks.Daily),
        TasksDailyDone = string.Join(",", state.Tasks.DailyDone.Order(StringComparer.Ordinal)),
        TasksPreviousDaily = string.Join(",", state.Tasks.PreviousDaily),
        TasksWeek = state.Tasks.Week ?? -1,
        TasksWeeklyId = state.Tasks.WeeklyId,
        TasksWeeklyDone = state.Tasks.WeeklyDone,
        TasksOneOffDone = string.Join(",", state.Tasks.OneOffDone.Order(StringComparer.Ordinal)),
        ActivityDay = state.Activity?.Day.Date.DayNumber ?? 0,
        ActivityGraded = state.Activity?.Graded ?? 0,
        ActivityNew = state.Activity?.NewIntroduced ?? 0,
        ActivityTyped = state.Activity?.TypedCorrect ?? 0,
        ActivitySessions = state.Activity?.SessionsCounted ?? 0,
        ActivityWarmups = state.Activity?.Warmups ?? 0,
        ActivityLearned = state.Activity?.WordsLearned ?? 0,
        ActivityAccurate = state.Activity?.AccurateSessions ?? 0,
        ActivityLongest = state.Activity?.LongestSession ?? 0,
        WeekNumber = state.Week?.Week ?? -1,
        WeekGraded = state.Week?.Graded ?? 0,
        WeekNew = state.Week?.NewIntroduced ?? 0,
        WeekTyped = state.Week?.TypedCorrect ?? 0,
        WeekLearned = state.Week?.WordsLearned ?? 0,
        WeekDays = state.Week?.LearningDays ?? 0,
    };

    /// <summary>Columns added by a table migration read as NULL on rows that existed before, so null means «nothing» here.</summary>
    private static string[] Split(string? csv) => (csv ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
