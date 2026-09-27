using Burdock.Application.Ports;
using Burdock.Application.UseCases.LearningDays;
using Burdock.Domain.Learning;
using Burdock.Domain.SharedKernel;

namespace Burdock.Application.UseCases.Sessions;

/// <summary>Turns an exercise outcome into a grade, schedules the next review and appends the review log (ADR 0008).</summary>
public sealed class GradeAnswerUseCase(
    ILearnerContext learner,
    ILearnerCardRepository cards,
    IReviewLogStore reviewLog,
    IReviewScheduler scheduler,
    IIdGenerator ids,
    IClock clock,
    LearningDayResolver learningDays)
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

        var scheduled = scheduler.Review(card.ToSchedulingInput(), rating.Value, now);
        var applied = card.ApplyReview(rating.Value, item.Mode, scheduled, learningDays.Today(), now);
        if (applied.IsFailure)
        {
            return applied.Error;
        }

        await cards.UpsertAsync(card, cancellationToken).ConfigureAwait(false);
        await reviewLog.AppendAsync(new ReviewLogEntry(ids.NewId(), learnerId, card.Id, card.LexemeId, rating.Value, item.Mode, now, outcome.ResponseTime,
            scheduled.Memory.Stability, scheduled.Memory.Difficulty, scheduled.Due), cancellationToken).ConfigureAwait(false);

        var retrievability = scheduler.Retrievability(card.ToSchedulingInput(), now);
        var correctAnswer = item switch
        {
            ChoiceItem choice => choice.Options[choice.CorrectIndex],
            TypedItem typed => typed.AcceptedForms[0],
            _ => item.Entry.Lexeme.Lemma,
        };
        return new GradeResult(rating.Value, card.State, scheduled.Due, retrievability, correctAnswer);
    }
}
