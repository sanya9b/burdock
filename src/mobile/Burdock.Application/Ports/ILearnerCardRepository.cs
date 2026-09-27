using Burdock.Domain.Learning;
using Burdock.Domain.SharedKernel;

namespace Burdock.Application.Ports;

/// <summary>Learner cards (one per lexeme per learner). Persisted immediately; sync happens from the change journal later (T14).</summary>
public interface ILearnerCardRepository
{
    Task<LearnerCard?> GetByLexemeAsync(LearnerId learnerId, LexemeId lexemeId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<LexemeId, LearnerCard>> ListAsync(LearnerId learnerId, CancellationToken cancellationToken);

    Task UpsertAsync(LearnerCard card, CancellationToken cancellationToken);
}

/// <summary>Append-only review log (ADR 0008, T14).</summary>
public interface IReviewLogStore
{
    Task AppendAsync(ReviewLogEntry entry, CancellationToken cancellationToken);

    Task<int> CountGradedOnDayAsync(LearnerId learnerId, DateTimeOffset dayStartUtc, DateTimeOffset dayEndUtc, CancellationToken cancellationToken);
}
