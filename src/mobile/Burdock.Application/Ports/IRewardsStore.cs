using Burdock.Domain.Rewards;
using Burdock.Domain.SharedKernel;

namespace Burdock.Application.Ports;

/// <summary>Folded rewards state plus the append-only ledger (spec 07 §3.7); the server reconciles the ledger in phase 2.</summary>
public interface IRewardsStore
{
    Task<RewardsState?> GetAsync(LearnerId learnerId, CancellationToken cancellationToken);

    /// <summary>Persists the new state and appends the events atomically.</summary>
    Task SaveAsync(LearnerId learnerId, RewardsState state, IReadOnlyList<LedgerEvent> newEvents, CancellationToken cancellationToken);
}
