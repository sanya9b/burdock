using Burdock.Contracts.V1;
using Burdock.Server.Shared.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Burdock.Server.Modules.Notifications;

/// <summary>APNs updates for the user-started Live Activity session in v1; content-update push in v1.1 (spec 09 §3.10, ADR 0005, T8).</summary>
public sealed class NotificationsModule : IModule
{
    public string Name => "notifications";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapGroup(ApiRoutes.LiveActivities.Group).WithTags("LiveActivities");
    }
}
