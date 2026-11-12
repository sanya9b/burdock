using Burdock.Application.Ports;
using Burdock.Application.UseCases.LearningDays;
using Burdock.Application.UseCases.Sessions;
using Burdock.Application.UseCases.Sync;
using Burdock.Domain.Learning;
using Burdock.Domain.SharedKernel;

namespace Burdock.Application.UseCases.Reviews;

/// <summary>«Повторимо» (owner decision 2026-11-12, log item 24): how many learned words are due and the packs of five pairs the round asks now.</summary>
public sealed record ReviewRound(int DueCount, IReadOnlyList<MatchingItem> Packs)
{
    public static ReviewRound Empty { get; } = new(0, []);

    public int WordCount => Packs.Sum(p => p.Entries.Count);

    /// <summary>At least one full pack of five: the badge on «Сьогодні» shows only then.</summary>
    public bool IsAvailable => Packs.Count > 0;
}

/// <summary>Builds the round from the learner's cards: <see cref="ReviewRoundRules"/> picks and orders, content supplies the entries.</summary>
public sealed class GetReviewRoundUseCase(ILearnerContext learner, ILearnerCardRepository cards, IContentRepository content, IReviewScheduler scheduler, IClock clock)
{
    public async Task<ReviewRound> ExecuteAsync(CancellationToken cancellationToken)
    {
        var learnerId = await learner.GetLearnerIdAsync(cancellationToken).ConfigureAwait(false);
        var now = clock.UtcNow;
        var learnerCards = await cards.ListAsync(learnerId, cancellationToken).ConfigureAwait(false);
        var plan = ReviewRoundRules.Plan(
            learnerCards.Values,
            c => c.Memory is null ? 0 : scheduler.Retrievability(c.ToSchedulingInput(), now),
            now);
        if (!plan.IsAvailable)
        {
            return new ReviewRound(plan.DueCount, []);
        }

        var ids = plan.Packs.SelectMany(p => p).ToArray();
        var entries = (await content.ListByIdsAsync(ids, cancellationToken).ConfigureAwait(false)).ToDictionary(e => e.Id);
        var packs = plan.Packs
            .Select(pack => pack.Where(entries.ContainsKey).Select(id => entries[id]).ToArray())
            // A word missing from the content cache (pack not imported yet) would leave a short pack; such a pack is skipped, the words stay due.
            .Where(pack => pack.Length == ReviewRoundRules.PackSize)
            .Select(pack => new MatchingItem(pack))
            .ToArray();
        return new ReviewRound(plan.DueCount, packs);
    }
}

/// <summary>What one graded pair did to the word: <see cref="LeftLearned"/> is true when a mistake sent it back to «Вчу».</summary>
public sealed record ReviewPairResult(Rating Rating, CardState State, bool LeftLearned);

/// <summary>
/// Grades one pair of the round: a correct match is Good (the next review moves further), a mistake is Again, which
/// also empties the knowledge ring so the word reads «Вчу» 0/4 and returns to the session drill. Writes the review log
/// and the sync event like any graded answer (ADR 0008, spec 09 §3.5).
/// </summary>
public sealed class GradeReviewPairUseCase(
    ILearnerContext learner,
    ILearnerCardRepository cards,
    IReviewLogStore reviewLog,
    IReviewScheduler scheduler,
    IIdGenerator ids,
    IClock clock,
    LearningDayResolver learningDays,
    IProgressOutbox outbox)
{
    public async Task<Result<ReviewPairResult>> ExecuteAsync(LexemeId lexemeId, bool correct, TimeSpan responseTime, CancellationToken cancellationToken)
    {
        var learnerId = await learner.GetLearnerIdAsync(cancellationToken).ConfigureAwait(false);
        var card = await cards.GetByLexemeAsync(learnerId, lexemeId, cancellationToken).ConfigureAwait(false);
        if (card is null)
        {
            return DomainError.NotFound("review.unknownCard", "Errors_Review_UnknownCard");
        }

        var now = clock.UtcNow;
        var rating = correct ? Rating.Good : Rating.Again;
        var wasLearned = card.State is CardState.Learned or CardState.Known;
        var scheduled = scheduler.Review(card.ToSchedulingInput(), rating, now);
        var applied = card.ApplyReview(rating, ExerciseMode.MatchingReview, scheduled, learningDays.Today(), now);
        if (applied.IsFailure)
        {
            return applied.Error;
        }

        if (!correct)
        {
            card.RecordMastery(0);
        }

        await cards.UpsertAsync(card, cancellationToken).ConfigureAwait(false);
        var entry = new ReviewLogEntry(ids.NewId(), learnerId, card.Id, card.LexemeId, rating, ExerciseMode.MatchingReview, now, responseTime,
            scheduled.Memory.Stability, scheduled.Memory.Difficulty, scheduled.Due);
        await reviewLog.AppendAsync(entry, cancellationToken).ConfigureAwait(false);
        await outbox.AppendAsync(ProgressEventFactory.ReviewLog(entry, clock.LocalTimeZone), cancellationToken).ConfigureAwait(false);

        var leftLearned = wasLearned && card.State is not (CardState.Learned or CardState.Known);
        return new ReviewPairResult(rating, card.State, leftLearned);
    }
}
