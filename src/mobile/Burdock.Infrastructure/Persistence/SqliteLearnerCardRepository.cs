using System.Globalization;
using Burdock.Application.Ports;
using Burdock.Domain.Learning;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Time;

namespace Burdock.Infrastructure.Persistence;

public sealed class SqliteLearnerCardRepository(BurdockDatabase database) : ILearnerCardRepository
{
    public async Task<LearnerCard?> GetByLexemeAsync(LearnerId learnerId, LexemeId lexemeId, CancellationToken cancellationToken)
    {
        var db = await database.GetOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var learner = learnerId.Value;
        var lexeme = lexemeId.Value;
        var row = await db.Table<LearnerCardRow>().Where(r => r.LearnerId == learner && r.LexemeId == lexeme).FirstOrDefaultAsync().ConfigureAwait(false);
        return row is null ? null : ToCard(row);
    }

    public async Task<IReadOnlyDictionary<LexemeId, LearnerCard>> ListAsync(LearnerId learnerId, CancellationToken cancellationToken)
    {
        var db = await database.GetOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var learner = learnerId.Value;
        var rows = await db.Table<LearnerCardRow>().Where(r => r.LearnerId == learner).ToListAsync().ConfigureAwait(false);
        return rows.Select(ToCard).ToDictionary(c => c.LexemeId, c => c);
    }

    public async Task UpsertAsync(LearnerCard card, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(card);
        var db = await database.GetOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await db.InsertOrReplaceAsync(ToRow(card)).ConfigureAwait(false);
    }

    private static LearnerCardRow ToRow(LearnerCard card) => new()
    {
        Id = card.Id.Value,
        LearnerId = card.LearnerId.Value,
        LexemeId = card.LexemeId.Value,
        CreatedAtTicks = Ticks.From(card.CreatedAt),
        State = (int)card.State,
        Stability = card.Memory?.Stability,
        Difficulty = card.Memory?.Difficulty,
        Step = card.Step,
        DueTicks = Ticks.From(card.Due),
        LastReviewTicks = Ticks.From(card.LastReview),
        IntroducedAtTicks = Ticks.From(card.IntroducedAt),
        LearnedAtTicks = Ticks.From(card.LearnedAt),
        Reps = card.Reps,
        Lapses = card.Lapses,
        ScheduledExposures = card.ScheduledExposures,
        ConfirmedExposures = card.ConfirmedExposures,
        RecallSuccesses = card.RecallSuccesses,
        ExposureDays = JoinDays(card.ExposureDays),
        RecallSuccessDays = JoinDays(card.RecallSuccessDays),
    };

    private static LearnerCard ToCard(LearnerCardRow row) => LearnerCard.Restore(
        CardId.From(row.Id),
        LearnerId.From(row.LearnerId),
        LexemeId.From(row.LexemeId),
        Ticks.To(row.CreatedAtTicks),
        (CardState)row.State,
        row.Stability is { } s && row.Difficulty is { } d ? MemoryState.Create(s, d) : null,
        row.Step,
        Ticks.To(row.DueTicks),
        Ticks.To(row.LastReviewTicks),
        Ticks.To(row.IntroducedAtTicks),
        Ticks.To(row.LearnedAtTicks),
        row.Reps,
        row.Lapses,
        row.ScheduledExposures,
        row.ConfirmedExposures,
        row.RecallSuccesses,
        SplitDays(row.ExposureDays),
        SplitDays(row.RecallSuccessDays));

    private static string JoinDays(IEnumerable<LearningDay> days)
        => string.Join(",", days.OrderBy(d => d.Date).Select(d => d.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));

    private static IEnumerable<LearningDay> SplitDays(string value)
        => value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => new LearningDay(DateOnly.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture)));
}

public sealed class SqliteReviewLogStore(BurdockDatabase database) : IReviewLogStore
{
    public async Task AppendAsync(ReviewLogEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var db = await database.GetOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await db.InsertAsync(new ReviewLogRow
        {
            Id = entry.Id,
            LearnerId = entry.LearnerId.Value,
            CardId = entry.CardId.Value,
            LexemeId = entry.LexemeId.Value,
            Rating = (int)entry.Rating,
            Mode = (int)entry.Mode,
            ReviewedAtTicks = Ticks.From(entry.ReviewedAt),
            ResponseTimeTicks = entry.ResponseTime.Ticks,
            StabilityAfter = entry.StabilityAfter,
            DifficultyAfter = entry.DifficultyAfter,
            DueAfterTicks = Ticks.From(entry.DueAfter),
        }).ConfigureAwait(false);
    }

    public async Task<int> CountGradedOnDayAsync(LearnerId learnerId, DateTimeOffset dayStartUtc, DateTimeOffset dayEndUtc, CancellationToken cancellationToken)
    {
        var db = await database.GetOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var learner = learnerId.Value;
        var start = dayStartUtc.UtcTicks;
        var end = dayEndUtc.UtcTicks;
        return await db.Table<ReviewLogRow>().Where(r => r.LearnerId == learner && r.ReviewedAtTicks >= start && r.ReviewedAtTicks < end).CountAsync().ConfigureAwait(false);
    }
}

public sealed class SqliteExposureStore(BurdockDatabase database) : IExposureStore
{
    public async Task AppendAsync(Domain.Exposure.ExposureRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        var db = await database.GetOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await db.InsertAsync(new ExposureRow
        {
            Id = record.Id,
            LearnerId = record.LearnerId.Value,
            LexemeId = record.LexemeId.Value,
            Surface = (int)record.Surface,
            Kind = (int)record.Kind,
            AtTicks = Ticks.From(record.At),
        }).ConfigureAwait(false);
    }

    public async Task<ExposureCounts> CountAsync(LearnerId learnerId, LexemeId lexemeId, CancellationToken cancellationToken)
    {
        var db = await database.GetOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var learner = learnerId.Value;
        var lexeme = lexemeId.Value;
        var scheduled = (int)Domain.Exposure.ExposureKind.Scheduled;
        var confirmed = (int)Domain.Exposure.ExposureKind.Confirmed;
        var s = await db.Table<ExposureRow>().Where(r => r.LearnerId == learner && r.LexemeId == lexeme && r.Kind == scheduled).CountAsync().ConfigureAwait(false);
        var c = await db.Table<ExposureRow>().Where(r => r.LearnerId == learner && r.LexemeId == lexeme && r.Kind == confirmed).CountAsync().ConfigureAwait(false);
        return new ExposureCounts(s, c);
    }
}
