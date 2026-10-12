using Burdock.Server.Modules.Content.Application;
using Burdock.Server.Modules.Content.Endpoints;
using Burdock.Server.Modules.Content.Infrastructure;
using Burdock.Server.Shared.Modules;
using Burdock.Server.Shared.Persistence;
using Burdock.Server.Shared.Storage;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Burdock.Server.Modules.Content;

/// <summary>Content manifest and pack publication (spec 09 §3.4, spec 08 §3.10, T8/T25): rows in <c>content.packs</c>, objects in the pack store.</summary>
public sealed class ContentModule : IModule
{
    public string Name => "content";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = configuration.GetSection(ContentOptions.SectionName).Get<ContentOptions>() ?? new ContentOptions();
        services.AddSingleton(options);
        services.AddModuleDbContext<ContentDbContext>(configuration, ContentDbContext.Schema);
        services.AddScoped<IPackCatalog, EfPackCatalog>();
        services.AddSingleton<IPackObjectStore>(sp => new PackObjectStore(
            ObjectStores.Create(options, sp.GetRequiredService<IHostEnvironment>(), ContentOptions.SectionName),
            options.PublicBaseUrl ?? PackObjectStore.DevelopmentBaseUrl));

        services.AddScoped<PublishPackUseCase>();
        services.AddScoped<GetManifestUseCase>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ContentEndpoints.Map(endpoints, endpoints.ServiceProvider.GetRequiredService<ContentOptions>());
    }
}
