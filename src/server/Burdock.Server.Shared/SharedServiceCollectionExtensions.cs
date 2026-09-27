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
        return services;
    }
}
