using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Burdock.Server.Shared.Persistence;

/// <summary>
/// One PostgreSQL schema per module (spec 09 §3.1): the host migrates every registered module database before it
/// serves traffic (`api migrate`) and the readiness check refuses traffic while migrations are pending.
/// </summary>
public interface IModuleDatabase
{
    string Schema { get; }

    Task MigrateAsync(CancellationToken cancellationToken);

    Task<bool> CanConnectAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> PendingMigrationsAsync(CancellationToken cancellationToken);
}

public sealed class ModuleDatabase<TContext>(IServiceScopeFactory scopes, string schema) : IModuleDatabase
    where TContext : DbContext
{
    public string Schema { get; } = schema;

    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        return await context.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> PendingMigrationsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        return (await context.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToArray();
    }
}

public static class ModulePersistenceExtensions
{
    public const string ConnectionStringName = "Burdock";
    public const string MigrationsHistoryTable = "__ef_migrations_history";

    /// <summary>Registers a module DbContext on the shared connection with its own schema and migrations history table.</summary>
    public static IServiceCollection AddModuleDbContext<TContext>(this IServiceCollection services, IConfiguration configuration, string schema, Action<DbContextOptionsBuilder>? configure = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);

        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured (ConnectionStrings__{ConnectionStringName}).");

        services.AddDbContext<TContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable(MigrationsHistoryTable, schema);
                npgsql.EnableRetryOnFailure(maxRetryCount: 3);
            });
            configure?.Invoke(options);
        });
        services.AddSingleton<IModuleDatabase>(sp => new ModuleDatabase<TContext>(sp.GetRequiredService<IServiceScopeFactory>(), schema));
        return services;
    }
}
