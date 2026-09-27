using Burdock.Application.Ports;
using Burdock.Infrastructure.Identity;
using Burdock.Infrastructure.Time;
using Microsoft.Extensions.DependencyInjection;

namespace Burdock.Infrastructure.DependencyInjection;

/// <summary>Registers port implementations shared by every platform. Platform-specific adapters are added in Platforms/ folders.</summary>
public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddBurdockInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IIdGenerator, Uuid7IdGenerator>();
        return services;
    }
}
