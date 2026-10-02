using Burdock.Server.Shared.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Burdock.Server.Shared.Health;

/// <summary>Readiness (spec 09 §3.2): every module schema reachable and fully migrated; the host maps it under /health/ready.</summary>
public sealed class ModuleDatabasesHealthCheck(IEnumerable<IModuleDatabase> databases) : IHealthCheck
{
    public const string Name = "databases";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>(StringComparer.Ordinal);
        var failures = new List<string>();
        foreach (var database in databases)
        {
            if (!await database.CanConnectAsync(cancellationToken).ConfigureAwait(false))
            {
                failures.Add($"{database.Schema}: unreachable");
                continue;
            }

            var pending = await database.PendingMigrationsAsync(cancellationToken).ConfigureAwait(false);
            data[database.Schema] = pending.Count == 0 ? "current" : $"{pending.Count} pending";
            if (pending.Count > 0)
            {
                failures.Add($"{database.Schema}: {pending.Count} pending migration(s)");
            }
        }

        return failures.Count == 0
            ? HealthCheckResult.Healthy("all module schemas are current", data)
            : HealthCheckResult.Unhealthy(string.Join("; ", failures), data: data);
    }
}
