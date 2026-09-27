using Burdock.Contracts.V1;
using Burdock.Server.Shared.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Burdock.Server.Modules.Rewards;

/// <summary>Ledger intake and reconciliation with the shared EconomyRules (spec 09 §3.8, ADR 0016).</summary>
public sealed class RewardsModule : IModule
{
    public string Name => "rewards";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapGroup(ApiRoutes.Rewards.Group).WithTags("Rewards");
    }
}
