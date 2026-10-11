using Burdock.Server.Modules.LearnerProgress.Application;
using Burdock.Server.Modules.LearnerProgress.Endpoints;
using Burdock.Server.Modules.LearnerProgress.Infrastructure;
using Burdock.Server.Shared.Modules;
using Burdock.Server.Shared.Persistence;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Burdock.Server.Modules.LearnerProgress;

/// <summary>Append-only progress events with a per-learner server_seq cursor (spec 09 §3.5, T14). Projections and stats follow with FR-09-12 work.</summary>
public sealed class LearnerProgressModule : IModule
{
    public string Name => "progress";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddSingleton(configuration.GetSection(ProgressOptions.SectionName).Get<ProgressOptions>() ?? new ProgressOptions());
        services.AddModuleDbContext<ProgressDbContext>(configuration, ProgressDbContext.Schema);
        services.AddScoped<IProgressEventStore, EfProgressEventStore>();
        services.AddScoped<AppendEventsUseCase>();
        services.AddScoped<ReadEventsUseCase>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ProgressEndpoints.Map(endpoints);
    }
}
