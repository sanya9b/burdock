using Burdock.Application.Ports;
using Burdock.Application.UseCases.LearningDays;
using Burdock.Domain.Learning;
using Burdock.Domain.Rewards;

namespace Burdock.Application.UseCases.Rewards;

/// <summary>Result of settling a session for the summary screen; <see cref="GamificationEnabled"/> tells the UI whether to celebrate (spec 07 §3.9: rewards are still recorded silently).</summary>
public sealed record SessionRewardsView(SessionSettlement Settlement, bool GamificationEnabled);

/// <summary>Applies the economy rules to a finished session (spec 07 §3.2, §3.3, §3.6) and persists state + ledger.</summary>
public sealed class SettleSessionRewardsUseCase(
    ILearnerContext learner,
    IRewardsStore rewards,
    IReviewLogStore reviewLog,
    ILearnerCardRepository cards,
    LearningDayResolver learningDays,
    IClock clock,
    IIdGenerator ids)
{
    public async Task<SessionRewardsView> ExecuteAsync(SessionReport report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        var learnerId = await learner.GetLearnerIdAsync(cancellationToken).ConfigureAwait(false);
        var settings = await learner.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        var state = await rewards.GetAsync(learnerId, cancellationToken).ConfigureAwait(false) ?? RewardsState.Empty;

        var learnerCards = await cards.ListAsync(learnerId, cancellationToken).ConfigureAwait(false);
        var learnedWords = learnerCards.Values.Count(c => c.State is CardState.Learned or CardState.Known);
        var typedCorrect = await reviewLog.CountAsync(learnerId, ExerciseMode.TypedRecall, onlyCorrect: true, cancellationToken).ConfigureAwait(false);
        var gradedTotal = await reviewLog.CountAsync(learnerId, null, onlyCorrect: false, cancellationToken).ConfigureAwait(false);

        var settlement = RewardsEngine.Settle(state, report, new ProgressCounters(learnedWords, typedCorrect, gradedTotal), learningDays.Today(), clock.UtcNow, ids.NewId);
        if (!settlement.IsEmpty || settlement.State != state)
        {
            await rewards.SaveAsync(learnerId, settlement.State, settlement.Events, cancellationToken).ConfigureAwait(false);
        }

        return new SessionRewardsView(settlement, settings.GamificationEnabled);
    }
}

/// <summary>What Today and the profile show: thorns, XP with level progress, streak, achievements.</summary>
public sealed record RewardsOverview(
    int Thorns,
    int Xp,
    int Level,
    int XpEarnedInLevel,
    int XpNeededForLevel,
    int StreakLength,
    bool StreakCountedToday,
    IReadOnlySet<string> Achievements,
    bool GamificationEnabled);

public sealed class GetRewardsOverviewUseCase(ILearnerContext learner, IRewardsStore rewards, LearningDayResolver learningDays)
{
    public async Task<RewardsOverview> ExecuteAsync(CancellationToken cancellationToken)
    {
        var learnerId = await learner.GetLearnerIdAsync(cancellationToken).ConfigureAwait(false);
        var settings = await learner.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        var state = await rewards.GetAsync(learnerId, cancellationToken).ConfigureAwait(false) ?? RewardsState.Empty;
        var today = learningDays.Today();
        var (earned, needed) = XpRules.LevelProgress(state.Xp);
        return new RewardsOverview(state.Thorns, state.Xp, state.Level, earned, needed, state.Streak.LengthOn(today), state.Streak.IsCountedOn(today), state.Achievements, settings.GamificationEnabled);
    }
}
