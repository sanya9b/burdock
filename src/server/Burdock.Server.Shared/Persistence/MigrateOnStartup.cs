using Microsoft.Extensions.Hosting;

namespace Burdock.Server.Shared.Persistence;

/// <summary>
/// Applies every module migration when the host starts (Development and tests: «Database:MigrateOnStartup»). Production
/// runs <c>api migrate</c> as a separate step before the new container starts (spec 14 §3.12), so this stays off there.
/// Registered before the modules so it runs ahead of their hosted services.
/// </summary>
public sealed class MigrateOnStartup(IEnumerable<IModuleDatabase> databases) : IHostedService
{
    public const string ConfigurationKey = "Database:MigrateOnStartup";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var database in databases)
        {
            await database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
