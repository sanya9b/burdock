namespace Burdock.Domain.Learning;

/// <summary>Scheduler-visible slice of a card before a review.</summary>
public readonly record struct SchedulingInput(CardState State, MemoryState? Memory, int? Step, DateTimeOffset? Due, DateTimeOffset? LastReview);

/// <summary>What the scheduler decided after a graded review.</summary>
public readonly record struct SchedulingOutcome(CardState State, MemoryState Memory, int? Step, DateTimeOffset Due);

/// <summary>
/// Port for the spaced-repetition algorithm (FSRS-6, ADR 0008). Implemented in Infrastructure so the Domain stays
/// dependency-free; the server never references this namespace (T23).
/// </summary>
public interface IReviewScheduler
{
    /// <summary>Applies a grade at <paramref name="reviewedAt"/> and returns the next state and due instant.</summary>
    SchedulingOutcome Review(SchedulingInput input, Rating rating, DateTimeOffset reviewedAt);

    /// <summary>Probability (0-1) that the learner still remembers the card at <paramref name="at"/>.</summary>
    double Retrievability(SchedulingInput input, DateTimeOffset at);
}
