using Burdock.Server.Modules.Content.Application;
using Burdock.Server.Modules.Content.Endpoints;
using Burdock.Server.Modules.Content.Infrastructure;
using Burdock.Server.Shared.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Burdock.Server.Modules.Content;

/// <summary>Content manifest and pack publication (spec 09 §3.5, spec 08 §3.10, T25).</summary>
public sealed class ContentModule : IModule
{
    public string Name => "content";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IContentManifestProvider, EmptyContentManifestProvider>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ContentEndpoints.Map(endpoints);
    }
}
