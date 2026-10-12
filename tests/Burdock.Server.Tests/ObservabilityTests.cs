using System.Diagnostics;
using Burdock.Contracts.V1;
using Burdock.Server.Shared.Observability;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Burdock.Server.Tests;

/// <summary>OpenTelemetry wiring (spec 09 NFR-09-9, spec 13 FR-13-19 and the no-PII rule of spec 13 section 1).</summary>
[Collection(PostgresBackedApi.Name)]
public sealed class ObservabilityTests(PostgresFixture postgres)
{
    private static readonly string[] ForbiddenTags = ["client.address", "http.client_ip", "net.peer.ip", "enduser.id", "http.request.header.authorization"];

    [Fact]
    public async Task Requests_are_traced_without_client_addresses_and_health_probes_are_not_traced()
    {
        var exported = new List<Activity>();
        using var factory = new TracedApiFactory(postgres.ConnectionString, exported);
        using var client = factory.CreateClient();

        factory.Services.GetService<TracerProvider>().ShouldNotBeNull();
        factory.Services.GetService<MeterProvider>().ShouldNotBeNull();

        using (var live = await client.GetAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken))
        {
            live.EnsureSuccessStatusCode();
        }

        using (var config = await client.GetAsync(new Uri(ApiRoutes.Config, UriKind.Relative), TestContext.Current.CancellationToken))
        {
            config.EnsureSuccessStatusCode();
        }

        var server = exported.Where(a => a.Kind == ActivityKind.Server).ToArray();
        server.ShouldNotBeEmpty();
        server.ShouldAllBe(a => a.GetTagItem("http.route") as string != "/health/live");
        var configSpan = server.Single(a => string.Equals(a.GetTagItem("http.route") as string, ApiRoutes.Config, StringComparison.Ordinal));
        configSpan.GetTagItem("http.response.status_code").ShouldBe(200);
        foreach (var activity in server)
        {
            foreach (var tag in activity.TagObjects)
            {
                ForbiddenTags.ShouldNotContain(tag.Key, $"span {activity.DisplayName} carries {tag.Key}");
            }
        }
    }

    [Theory]
    [InlineData("/health/live", true)]
    [InlineData("/healthz/ready", true)]
    [InlineData("/v1/config", false)]
    [InlineData("/healthy-words", false)]
    public void Health_probe_paths_are_recognised_exactly(string path, bool probe)
    {
        ObservabilityExtensions.IsHealthProbe(path).ShouldBe(probe);
    }

    /// <summary>The test host with an in-memory exporter attached to the tracer provider the app configured.</summary>
    private sealed class TracedApiFactory(string connectionString, ICollection<Activity> exported) : BurdockApiFactory(connectionString)
    {
        protected override void Configure(IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services => services.ConfigureOpenTelemetryTracerProvider((_, tracing) => tracing.AddInMemoryExporter(exported)));
        }
    }
}
