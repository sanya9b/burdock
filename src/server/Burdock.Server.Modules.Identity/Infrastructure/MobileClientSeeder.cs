using Burdock.Contracts.V1;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Burdock.Server.Modules.Identity.Infrastructure;

/// <summary>Registers the single public OpenIddict client the apps use (spec 09 §3.3.1); idempotent, runs after migrations.</summary>
public sealed class MobileClientSeeder(IServiceScopeFactory scopes) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        if (await manager.FindByClientIdAsync(ApiRoutes.Connect.ClientId, cancellationToken).ConfigureAwait(false) is not null)
        {
            return;
        }

        await manager.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = ApiRoutes.Connect.ClientId,
            ClientType = ClientTypes.Public,
            DisplayName = "Burdock mobile",
            Permissions =
            {
                Permissions.Endpoints.Token,
                Permissions.Endpoints.Revocation,
                Permissions.GrantTypes.RefreshToken,
                Permissions.Prefixes.GrantType + ApiRoutes.Connect.InstallationGrantType,
                Permissions.Prefixes.Scope + Scopes.OfflineAccess,
            },
        }, cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
