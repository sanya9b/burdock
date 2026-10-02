using System.Globalization;
using Burdock.Application.Ports;
using Burdock.Application.UseCases.LearningDays;
using Burdock.Domain.Learning;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Time;
using Burdock.Domain.Vocabulary;

namespace Burdock.Application.UseCases.Planning;

/// <summary>
/// Today's words for the Сьогодні screen and the widget timeline (spec 06 §3.5). The first open of a learning day scans
/// the candidates of the learner's levels and topics and remembers a prefix of the fresh candidates in plan order; every
/// later open that day rebuilds the plan from the learner's cards plus that prefix (issue #4: the scan cost ~1 s on
/// 10k words). A shortage in the prefix, another day or changed settings fall back to the scan.
/// </summary>
public sealed class GetDailyPlanUseCase(
    ILearnerContext learner,
    IContentRepository content,
    ILearnerCardRepository cards,
    IReviewScheduler scheduler,
    IClock clock,
    LearningDayResolver learningDays,
    IDailyPlanCache planCache)
{
    /// <summary>learning.plan.snapshotFactor: how many days' worth of new words the snapshot keeps as spares for words marked known or suspended.</summary>
    public const int SnapshotFactor = 3;

    public async Task<DailyPlan> ExecuteAsync(CancellationToken cancellationToken)
    {
        var learnerId = await learner.GetLearnerIdAsync(cancellationToken).ConfigureAwait(false);
        var settings = await learner.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        var day = learningDays.Today();
        var now = clock.UtcNow;
        var learnerCards = await cards.ListAsync(learnerId, cancellationToken).ConfigureAwait(false);
        var fingerprint = Fingerprint(settings);

        var levels = settings.Level.WithNeighbours();
        var snapshot = await planCache.GetAsync(learnerId, cancellationToken).ConfigureAwait(false);
        if (snapshot is not null && snapshot.Day == day && string.Equals(snapshot.Fingerprint, fingerprint, StringComparison.Ordinal))
        {
            var ids = learnerCards.Keys.Concat(snapshot.NewWordIds).Distinct().ToArray();
            // The same candidate filter as the scan, so a card started under other settings does not resurface only on this path.
            var known = (await content.ListByIdsAsync(ids, cancellationToken).ConfigureAwait(false))
                .Where(e => levels.Contains(e.Lexeme.Level) && (settings.Topics.Count == 0 || settings.Topics.Any(e.Lexeme.BelongsTo)))
                .ToArray();
            var fromSnapshot = DailyPlanner.Plan(learnerId, day, now, clock.LocalTimeZone, known, learnerCards, settings, scheduler);
            if (!HasShortage(fromSnapshot, learnerCards, settings, day))
            {
                return fromSnapshot;
            }
        }

        var candidates = await content.ListAsync(settings.Pair, levels, settings.Topics, cancellationToken).ConfigureAwait(false);
        var plan = DailyPlanner.Plan(learnerId, day, now, clock.LocalTimeZone, candidates, learnerCards, settings, scheduler);
        var prefix = DailyPlanner.FreshInPlanOrder(learnerId, day, candidates, learnerCards, settings)
            .Take(settings.NewWordsPerDay * SnapshotFactor)
            .Select(e => e.Id)
            .ToArray();
        await planCache.SaveAsync(new DailyPlanSnapshot(learnerId, day, fingerprint, prefix), cancellationToken).ConfigureAwait(false);
        return plan;
    }

    /// <summary>The snapshot ran out of spares: fewer new words than the budget allows while the full candidate set might still have some.</summary>
    private bool HasShortage(DailyPlan plan, IReadOnlyDictionary<LexemeId, LearnerCard> learnerCards, LearnerSettings settings, LearningDay day)
    {
        var introducedToday = learnerCards.Values.Count(c => c.IntroducedAt is { } at && c.State != CardState.New && LearningDay.From(at, clock.LocalTimeZone) == day);
        return plan.NewCount < Math.Max(0, settings.NewWordsPerDay - introducedToday);
    }

    private static string Fingerprint(LearnerSettings settings)
        => string.Create(CultureInfo.InvariantCulture, $"{settings.Pair}|{settings.Level}|{string.Join(',', settings.Topics.Select(t => t.Slug).Order(StringComparer.Ordinal))}|{settings.NewWordsPerDay}|plan-v2");
}
