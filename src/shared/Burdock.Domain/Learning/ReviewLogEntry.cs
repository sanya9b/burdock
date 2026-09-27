using Burdock.Domain.SharedKernel;

namespace Burdock.Domain.Learning;

/// <summary>Append-only record of one graded answer; the source of truth for re-deriving card state and for FSRS re-optimisation (T14, spec 06 §3.1).</summary>
public sealed record ReviewLogEntry(
    Guid Id,
    LearnerId LearnerId,
    CardId CardId,
    LexemeId LexemeId,
    Rating Rating,
    ExerciseMode Mode,
    DateTimeOffset ReviewedAt,
    TimeSpan ResponseTime,
    double? StabilityAfter,
    double? DifficultyAfter,
    DateTimeOffset DueAfter);
