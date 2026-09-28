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
        return new RewardsState(row.Thorns, row.Xp, new StreakState(row.StreakLength, row.StreakLongest, lastDay), achievements, row.SessionsCompleted, today, row.LastSessionId);
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
    };
}
