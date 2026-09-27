using Burdock.Server.Api;
using Burdock.Server.Shared;
using Burdock.Server.Shared.Modules;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddServerShared();
builder.Services.AddModules(builder.Configuration, BurdockModules.All);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Liveness answers as soon as the process serves requests; readiness runs registered checks (DB, migrations, outbox age).
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = static _ => false });
app.MapHealthChecks("/health/ready");

app.MapModules();

await app.RunAsync().ConfigureAwait(false);

/// <summary>Exposed for WebApplicationFactory in integration tests.</summary>
public partial class Program;
