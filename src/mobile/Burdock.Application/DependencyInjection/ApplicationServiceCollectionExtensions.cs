using Burdock.Application.UseCases.LearningDays;
using Microsoft.Extensions.DependencyInjection;

namespace Burdock.Application.DependencyInjection;

/// <summary>Registers use cases. Ports are registered by Infrastructure and the platform head, never here.</summary>
public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddBurdockApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<LearningDayResolver>();
        return services;
    }
}
