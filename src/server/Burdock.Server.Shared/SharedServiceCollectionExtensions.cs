using Burdock.Server.Shared.Contracts;
using Burdock.Server.Shared.Time;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Burdock.Server.Shared;

/// <summary>Kernel services every module may rely on.</summary>
public static class SharedServiceCollectionExtensions
{
    public static IServiceCollection AddServerShared(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IClock, SystemClock>();
        // Replaced by the Entitlements module when it ships; until then every learner has the configured default state.
        services.TryAddSingleton<IEntitlementReader, ConfiguredEntitlementReader>();
        return services;
    }
}
