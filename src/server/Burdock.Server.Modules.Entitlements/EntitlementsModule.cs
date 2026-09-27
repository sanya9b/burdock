using Burdock.Contracts.V1;
using Burdock.Server.Shared.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Burdock.Server.Modules.Entitlements;

/// <summary>App Store / Google Play verification and the entitlement state machine (spec 09 §3.9, spec 11, ADR 0011).</summary>
public sealed class EntitlementsModule : IModule
{
    public string Name => "entitlements";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapGroup(ApiRoutes.Entitlements.Group).WithTags("Entitlements");
    }
}
