using Burdock.Server.Modules.SettingsSync.Application;
using Burdock.Server.Modules.SettingsSync.Endpoints;
using Burdock.Server.Modules.SettingsSync.Infrastructure;
using Burdock.Server.Shared.Modules;
using Burdock.Server.Shared.Persistence;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Burdock.Server.Modules.SettingsSync;

/// <summary>Learner settings merged per key with last-writer-wins (spec 09 §3.6, T14).</summary>
public sealed class SettingsSyncModule : IModule
{
    public string Name => "settings";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddModuleDbContext<SettingsDbContext>(configuration, SettingsDbContext.Schema);
        services.AddScoped<ISettingsStore, EfSettingsStore>();
        services.AddScoped<GetSettingsUseCase>();
        services.AddScoped<PutSettingsUseCase>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        SettingsEndpoints.Map(endpoints);
    }
}
