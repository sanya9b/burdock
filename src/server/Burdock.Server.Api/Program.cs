using System.Diagnostics;
using Burdock.Server.Api;
using Burdock.Server.Shared;
using Burdock.Server.Shared.Health;
using Burdock.Server.Shared.Modules;
using Burdock.Server.Shared.Persistence;
using Burdock.Server.Shared.RateLimiting;
using Burdock.Server.Shared.Security;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = static context =>
{
    // RFC 9457 with a traceId extension (spec 09 FR-09-23); the machine code is added by ErrorResults.
    context.ProblemDetails.Extensions.TryAdd("traceId", Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
});
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddCheck<ModuleDatabasesHealthCheck>(ModuleDatabasesHealthCheck.Name, tags: ["ready"]);
builder.Services.AddServerShared();
builder.Services.AddBurdockRateLimiting(builder.Configuration);
builder.Services.AddAuthorization(options => options.AddPolicy(Policies.User, policy => policy.RequireAuthenticatedUser()));
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    // Caddy terminates TLS in front of the container (spec 14 §3.11); only the compose network is trusted to set forwarded headers.
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    foreach (var cidr in builder.Configuration.GetSection("Proxy:TrustedNetworks").Get<string[]>() ?? [])
    {
        if (System.Net.IPNetwork.TryParse(cidr, out var network))
        {
            options.KnownIPNetworks.Add(network);
        }
    }
});
if (builder.Configuration.GetValue<bool>(MigrateOnStartup.ConfigurationKey))
{
    builder.Services.AddHostedService<MigrateOnStartup>();
}

builder.Services.AddModules(builder.Configuration, BurdockModules.All);

var app = builder.Build();

if (args.Length > 0 && string.Equals(args[0], "migrate", StringComparison.Ordinal))
{
    // `api migrate`: the deploy step before a new container serves traffic (spec 14 §3.12).
    foreach (var database in app.Services.GetServices<IModuleDatabase>())
    {
        await database.MigrateAsync(CancellationToken.None).ConfigureAwait(false);
        StartupLog.SchemaMigrated(app.Logger, database.Schema);
    }

    return;
}

if (args.Length > 1 && string.Equals(args[0], "content", StringComparison.Ordinal) && string.Equals(args[1], "publish", StringComparison.Ordinal))
{
    // `api content publish --index …`: uploads the packs of a build_packs.py index and records them (spec 09 §3.4).
    Environment.ExitCode = await ContentPublishCommand.RunAsync(app.Services, args, app.Logger, CancellationToken.None).ConfigureAwait(false);
    return;
}

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

// Liveness answers as soon as the process serves requests; readiness runs the registered checks (module schemas current).
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = static _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = static check => check.Tags.Contains("ready") });
app.MapHealthChecks("/healthz/live", new HealthCheckOptions { Predicate = static _ => false });
app.MapHealthChecks("/healthz/ready", new HealthCheckOptions { Predicate = static check => check.Tags.Contains("ready") });

ConfigEndpoints.Map(app, builder.Configuration);
app.MapModules();

await app.RunAsync().ConfigureAwait(false);

/// <summary>Exposed for WebApplicationFactory in integration tests.</summary>
public partial class Program;
