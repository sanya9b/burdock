using Burdock.Domain.Entitlements;
using Burdock.Domain.SharedKernel;
using Microsoft.Extensions.Configuration;

namespace Burdock.Server.Shared.Contracts;

/// <summary>Synchronous cross-module contract (spec 09 §3.1): Content asks Entitlements what a learner may download.</summary>
public interface IEntitlementReader
{
    Task<EntitlementState> GetStateAsync(LearnerId learnerId, CancellationToken cancellationToken);
}

/// <summary>
/// Stand-in until the Entitlements module ships (phase 5): every learner has the configured state, «Entitlements:DefaultState»,
/// Trial by default, because the apps have no paywall yet and the manifest must still describe the full catalogue.
/// </summary>
public sealed class ConfiguredEntitlementReader : IEntitlementReader
{
    public const string ConfigurationKey = "Entitlements:DefaultState";

    private readonly EntitlementState _state;

    public ConfiguredEntitlementReader(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _state = Enum.TryParse<EntitlementState>(configuration[ConfigurationKey], ignoreCase: true, out var parsed) ? parsed : EntitlementState.Trial;
    }

    public Task<EntitlementState> GetStateAsync(LearnerId learnerId, CancellationToken cancellationToken) => Task.FromResult(_state);
}
