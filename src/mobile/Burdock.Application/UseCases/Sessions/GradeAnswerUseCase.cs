using Burdock.Application.Ports;
using Burdock.Application.UseCases.LearningDays;
using Burdock.Application.UseCases.Sync;
using Burdock.Domain.Learning;
using Burdock.Domain.SharedKernel;

namespace Burdock.Application.UseCases.Sessions;

/// <summary>Turns an exercise outcome into a grade, schedules the next review, appends the review log and queues the ReviewLog event for sync (ADR 0008, spec 09 section 3.5).</summary>
public sealed class GradeAnswerUseCase(
    ILearnerContext learner,
    ILearnerCardRepository cards,
    IReviewLogStore reviewLog,
    IReviewScheduler scheduler,
    IIdGenerator ids,
    IClock clock,
    LearningDayResolver learningDays,
    IProgressOutbox outbox)
{
    public async Task<Result<GradeResult>> ExecuteAsync(SessionItem item, ExerciseOutcome outcome, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        var rating = GradingPolicy.Grade(outcome);
        if (rating is null)
        {
            return DomainError.Validation("session.ungraded", "Errors_Session_Ungraded");
        }

        var learnerId = await learner.GetLearnerIdAsync(cancellationToken).ConfigureAwait(false);
        var now = clock.UtcNow;
        var card = await cards.GetByLexemeAsync(learnerId, item.Entry.Id, cancellationToken).ConfigureAwait(false)
                   ?? LearnerCard.CreateNew(CardId.From(ids.NewId()), learnerId, item.Entry.Id, now);
        card.Introduce(now);
        var wasLearned = card.State is CardState.Learned or CardState.Known;

        var scheduled = scheduler.Review(card.ToSchedulingInput(), rating.Value, now);
        var applied = card.ApplyReview(rating.Value, item.Mode, scheduled, learningDays.Today(), now);
        if (applied.IsFailure)
        {
            return applied.Error;
        }

        await cards.UpsertAsync(card, cancellationToken).ConfigureAwait(false);
        var entry = new ReviewLogEntry(ids.NewId(), learnerId, card.Id, card.LexemeId, rating.Value, item.Mode, now, outcome.ResponseTime,
            scheduled.Memory.Stability, scheduled.Memory.Difficulty, scheduled.Due);
        await reviewLog.AppendAsync(entry, cancellationToken).ConfigureAwait(false);
        await outbox.AppendAsync(ProgressEventFactory.ReviewLog(entry, clock.LocalTimeZone), cancellationToken).ConfigureAwait(false);

        var retrievability = scheduler.Retrievability(card.ToSchedulingInput(), now);
        var correctAnswer = item switch
        {
            ChoiceItem choice => choice.Options[choice.CorrectIndex],
            TypedItem typed => typed.AcceptedForms[0],
            _ => item.Entry.Lexeme.Lemma,
        };
        var becameLearned = !wasLearned && card.State is CardState.Learned or CardState.Known;
        return new GradeResult(rating.Value, card.State, scheduled.Due, retrievability, correctAnswer, becameLearned);
    }
}
