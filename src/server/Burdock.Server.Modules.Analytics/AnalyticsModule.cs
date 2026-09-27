using Burdock.Contracts.V1;
using Burdock.Server.Shared.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Burdock.Server.Modules.Analytics;

/// <summary>Opt-in, anonymous, allow-listed events aggregated by the hour (spec 13, ADR 0012, T24, T27).</summary>
public sealed class AnalyticsModule : IModule
{
    public string Name => "analytics";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapGroup(ApiRoutes.Analytics.Group).WithTags("Analytics");
    }
}
