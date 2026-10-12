using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Burdock.Server.Shared.Observability;

/// <summary>
/// OpenTelemetry for the host (spec 09 NFR-09-9, spec 13 FR-13-19), the Aspire ServiceDefaults shape without the Aspire
/// package: traces and metrics for ASP.NET Core, outgoing HTTP, the runtime and Npgsql, logs through the OTel pipeline.
/// Export is OTLP and entirely configuration-driven: nothing leaves the process unless OTEL_EXPORTER_OTLP_ENDPOINT is set
/// (the collector with the redaction processor in production, spec 13 section 3.7). Health probes are not traced.
/// </summary>
public static class ObservabilityExtensions
{
    public const string ServiceName = "burdock-api";
    public const string OtlpEndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";

    private static readonly string ServiceVersion = typeof(ObservabilityExtensions).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

    public static IHostApplicationBuilder AddBurdockObservability(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        var telemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ServiceName, serviceVersion: ServiceVersion, serviceInstanceId: Environment.MachineName))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter("Npgsql")
                .AddMeter("Microsoft.AspNetCore.RateLimiting"))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options => options.Filter = static context => !IsHealthProbe(context.Request.Path))
                .AddHttpClientInstrumentation()
                .AddSource("Npgsql"));

        if (!string.IsNullOrWhiteSpace(builder.Configuration[OtlpEndpointKey]))
        {
            telemetry.UseOtlpExporter();
        }

        return builder;
    }

    public static bool IsHealthProbe(PathString path) => path.StartsWithSegments("/health") || path.StartsWithSegments("/healthz");
}
