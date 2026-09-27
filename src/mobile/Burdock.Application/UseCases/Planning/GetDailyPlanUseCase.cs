using Burdock.Application.Ports;
using Burdock.Application.UseCases.LearningDays;
using Burdock.Domain.Learning;
using Burdock.Domain.Vocabulary;

namespace Burdock.Application.UseCases.Planning;

/// <summary>Today's words for the Сьогодні screen and the widget timeline (spec 06 §3.5).</summary>
public sealed class GetDailyPlanUseCase(
    ILearnerContext learner,
    IContentRepository content,
    ILearnerCardRepository cards,
    IReviewScheduler scheduler,
    IClock clock,
    LearningDayResolver learningDays)
{
    public async Task<DailyPlan> ExecuteAsync(CancellationToken cancellationToken)
    {
        var learnerId = await learner.GetLearnerIdAsync(cancellationToken).ConfigureAwait(false);
        var settings = await learner.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        var levels = settings.Level.WithNeighbours();
        var candidates = await content.ListAsync(settings.Pair, levels, settings.Topics, cancellationToken).ConfigureAwait(false);
        var learnerCards = await cards.ListAsync(learnerId, cancellationToken).ConfigureAwait(false);

        // Prefer the chosen level: neighbours only fill the plan when the level itself runs dry (spec 06 §3.5 spill-over).
        var ordered = candidates.OrderBy(c => c.Lexeme.Level == settings.Level ? 0 : 1).ToArray();
        return DailyPlanner.Plan(learnerId, learningDays.Today(), clock.UtcNow, clock.LocalTimeZone, ordered, learnerCards, settings, scheduler);
    }
}
