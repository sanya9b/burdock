using Burdock.Contracts.V1;
using Burdock.Server.Shared.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Burdock.Server.Modules.LearnerProgress;

/// <summary>Append-only progress events with a per-user server_seq cursor (spec 09 §3.6, T14).</summary>
public sealed class LearnerProgressModule : IModule
{
    public string Name => "progress";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapGroup(ApiRoutes.Progress.Group).WithTags("Progress");
    }
}
