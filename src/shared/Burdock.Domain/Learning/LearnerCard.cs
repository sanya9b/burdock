using Burdock.Domain.Exposure;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Time;

namespace Burdock.Domain.Learning;

/// <summary>
/// One learner's relationship with one lexeme: lifecycle state, FSRS memory, counters (spec 06 §3.1-3.2).
/// Passive exposures never grade the card (ADR 0008); they only move it from New to Introduced.
/// </summary>
public sealed class LearnerCard
{
    private readonly HashSet<LearningDay> _exposureDays = [];
    private readonly HashSet<LearningDay> _recallSuccessDays = [];

    private LearnerCard(CardId id, LearnerId learnerId, LexemeId lexemeId, DateTimeOffset createdAt)
    {
        Id = id;
        LearnerId = learnerId;
        LexemeId = lexemeId;
        CreatedAt = createdAt;
        State = CardState.New;
    }

    public CardId Id { get; }

    public LearnerId LearnerId { get; }

    public LexemeId LexemeId { get; }

    public DateTimeOffset CreatedAt { get; }

    public CardState State { get; private set; }

    public MemoryState? Memory { get; private set; }

    public int? Step { get; private set; }

    public DateTimeOffset? Due { get; private set; }

    public DateTimeOffset? LastReview { get; private set; }

    public DateTimeOffset? IntroducedAt { get; private set; }

    public DateTimeOffset? LearnedAt { get; private set; }

    public int Reps { get; private set; }

    public int Lapses { get; private set; }

    public int ScheduledExposures { get; private set; }

    public int ConfirmedExposures { get; private set; }

    public int RecallSuccesses { get; private set; }

    public IReadOnlyCollection<LearningDay> ExposureDays => _exposureDays;

    public IReadOnlyCollection<LearningDay> RecallSuccessDays => _recallSuccessDays;

    public SchedulingInput ToSchedulingInput() => new(State, Memory, Step, Due, LastReview);

    public static LearnerCard CreateNew(CardId id, LearnerId learnerId, LexemeId lexemeId, DateTimeOffset now) => new(id, learnerId, lexemeId, now);

    /// <summary>Rehydrates a card from storage without re-running business rules.</summary>
    public static LearnerCard Restore(
        CardId id, LearnerId learnerId, LexemeId lexemeId, DateTimeOffset createdAt, CardState state, MemoryState? memory, int? step,
        DateTimeOffset? due, DateTimeOffset? lastReview, DateTimeOffset? introducedAt, DateTimeOffset? learnedAt, int reps, int lapses,
        int scheduledExposures, int confirmedExposures, int recallSuccesses, IEnumerable<LearningDay> exposureDays, IEnumerable<LearningDay> recallSuccessDays)
    {
        ArgumentNullException.ThrowIfNull(exposureDays);
        ArgumentNullException.ThrowIfNull(recallSuccessDays);
        var card = new LearnerCard(id, learnerId, lexemeId, createdAt)
        {
            State = state,
            Memory = memory,
            Step = step,
            Due = due,
            LastReview = lastReview,
            IntroducedAt = introducedAt,
            LearnedAt = learnedAt,
            Reps = reps,
            Lapses = lapses,
            ScheduledExposures = scheduledExposures,
            ConfirmedExposures = confirmedExposures,
            RecallSuccesses = recallSuccesses,
        };
        card._exposureDays.UnionWith(exposureDays);
        card._recallSuccessDays.UnionWith(recallSuccessDays);
        return card;
    }

    /// <summary>
    /// Records a passive or confirmed exposure. Introduction rule (T4): confirmed exposure introduces at once;
    /// scheduled exposures introduce after <see cref="LearningPolicy.IntroductionExposures"/> on
    /// <see cref="LearningPolicy.IntroductionDistinctDays"/> distinct days.
    /// </summary>
    public bool RecordExposure(ExposureKind kind, LearningDay day, DateTimeOffset at)
    {
        if (kind == ExposureKind.Confirmed)
        {
            ConfirmedExposures++;
        }
        else
        {
            ScheduledExposures++;
        }

        _exposureDays.Add(day);
        if (State != CardState.New)
        {
            return false;
        }

        var introduce = kind == ExposureKind.Confirmed
            || (ScheduledExposures >= LearningPolicy.IntroductionExposures && _exposureDays.Count >= LearningPolicy.IntroductionDistinctDays);
        if (!introduce)
        {
            return false;
        }

        State = CardState.Introduced;
        IntroducedAt = at;
        return true;
    }

    /// <summary>The learner opened the word in the app or chose «Вчити»: introduced immediately.</summary>
    public void Introduce(DateTimeOffset at)
    {
        if (State != CardState.New)
        {
            return;
        }

        State = CardState.Introduced;
        IntroducedAt = at;
    }

    /// <summary>Applies a graded review (never called for Matching or passive exposures).</summary>
    public Result ApplyReview(Rating rating, ExerciseMode mode, SchedulingOutcome outcome, LearningDay day, DateTimeOffset reviewedAt)
    {
        if (mode.IsUngraded())
        {
            return DomainError.Validation("card.ungradedMode", "Errors_Card_UngradedMode");
        }

        if (State is CardState.Suspended)
        {
            return DomainError.Conflict("card.suspended", "Errors_Card_Suspended");
        }

        var previousState = State;
        Reps++;
        if (rating == Rating.Again && previousState is CardState.Review or CardState.Learned)
        {
            Lapses++;
        }

        Memory = outcome.Memory;
        Step = outcome.Step;
        Due = outcome.Due;
        LastReview = reviewedAt;
        IntroducedAt ??= reviewedAt;

        if (rating != Rating.Again && mode.IsRecall())
        {
            RecallSuccesses++;
            _recallSuccessDays.Add(day);
        }

        State = ResolveState(outcome.State, rating);
        if (State == CardState.Learned && LearnedAt is null)
        {
            LearnedAt = reviewedAt;
        }
        else if (State != CardState.Learned)
        {
            LearnedAt = null;
        }

        return Result.Success();
    }

    /// <summary>«Знаю»: the learner claims to know the word; verified later by typed recall (spec 03 S-11, assumption).</summary>
    public void ClaimKnown(DateTimeOffset at)
    {
        State = CardState.Known;
        IntroducedAt ??= at;
    }

    public void Suspend() => State = CardState.Suspended;

    /// <summary>
    /// Session mastery (decision 2026-10-28, spec 06 §3.7): the word collected <see cref="LearningPolicy.MasteryStreak"/> correct
    /// answers in a row within one session and is learned regardless of FSRS stability; a later <c>Again</c> still regresses it.
    /// </summary>
    public void MarkMastered(DateTimeOffset at)
    {
        if (State is CardState.Known or CardState.Suspended)
        {
            return;
        }

        IntroducedAt ??= at;
        State = CardState.Learned;
        LearnedAt ??= at;
    }

    private CardState ResolveState(CardState schedulerState, Rating rating)
    {
        if (rating == Rating.Again)
        {
            return schedulerState == CardState.Relearning ? CardState.Relearning : CardState.Learning;
        }

        var learned = Memory is { } memory
            && memory.Stability >= LearningPolicy.LearnedMinStabilityDays
            && RecallSuccesses >= LearningPolicy.LearnedMinRecallSuccesses
            && _recallSuccessDays.Count >= LearningPolicy.LearnedMinRecallDays;
        if (learned)
        {
            return CardState.Learned;
        }

        return schedulerState switch
        {
            CardState.Review => CardState.Review,
            CardState.Relearning => CardState.Relearning,
            _ => CardState.Learning,
        };
    }
}

/// <summary>Learning-policy constants (spec 06 §3.13; config keys in comments so they can move to remote config later).</summary>
public static class LearningPolicy
{
    /// <summary>learning.introduction.exposures</summary>
    public const int IntroductionExposures = 3;

    /// <summary>learning.introduction.distinctDays</summary>
    public const int IntroductionDistinctDays = 2;

    /// <summary>learning.learned.minStabilityDays</summary>
    public const double LearnedMinStabilityDays = 21;

    /// <summary>learning.learned.minRecallSuccesses</summary>
    public const int LearnedMinRecallSuccesses = 3;

    /// <summary>learning.learned.minRecallDays</summary>
    public const int LearnedMinRecallDays = 2;

    /// <summary>session.masteryStreak: correct answers in a row within one session that make a word learned (decision 2026-10-28).</summary>
    public const int MasteryStreak = 4;

    /// <summary>learning.session.minGradedCardsForLearningDay (T11)</summary>
    public const int MinGradedCardsForLearningDay = 5;
}
