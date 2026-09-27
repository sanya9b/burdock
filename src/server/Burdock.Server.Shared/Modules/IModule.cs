using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Burdock.Server.Shared.Modules;

/// <summary>
/// A bounded context inside the modular monolith (spec 09 §1.1). Modules own their schema, register their own
/// services and map their own endpoint group; they never reference each other.
/// </summary>
public interface IModule
{
    /// <summary>Stable lower-case name used for PostgreSQL schemas, logging scopes and OpenAPI tags.</summary>
    string Name { get; }

    void AddServices(IServiceCollection services, IConfiguration configuration);

    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
