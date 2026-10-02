using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Burdock.Contracts.V1;
using Burdock.Contracts.V1.Config;
using Burdock.Server.Shared.Security;
using Microsoft.Net.Http.Headers;

namespace Burdock.Server.Api;

/// <summary>GET /v1/config (spec 09 §3.2, FR-09-22): minimum app versions, feature flags and public policy constants, anonymous, ETag + 5 min cache.</summary>
public static class ConfigEndpoints
{
    public const string SectionName = "RemoteConfig";
    private const int CacheSeconds = 300;

    public static void Map(IEndpointRouteBuilder endpoints, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(SectionName);
        var dto = new RemoteConfigDto(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ios"] = section["MinAppVersion:Ios"] ?? "1.0.0",
                ["android"] = section["MinAppVersion:Android"] ?? "1.0.0",
            },
            section.GetSection("Features").GetChildren().ToDictionary(c => c.Key, c => string.Equals(c.Value, "true", StringComparison.OrdinalIgnoreCase), StringComparer.Ordinal),
            section["RulesVersion"] ?? "economy-v1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["privacy"] = section["Urls:Privacy"] ?? "https://burdock.org/privacy",
                ["terms"] = section["Urls:Terms"] ?? "https://burdock.org/terms",
                ["support"] = section["Urls:Support"] ?? "https://burdock.org/support",
            });
        var json = JsonSerializer.SerializeToUtf8Bytes(dto);
        var etag = new EntityTagHeaderValue(string.Create(CultureInfo.InvariantCulture, $"\"{Convert.ToHexStringLower(SHA256.HashData(json))[..16]}\""), isWeak: true);

        endpoints.MapGet(ApiRoutes.Config, (HttpContext context) =>
            {
                context.Response.Headers.ETag = etag.ToString();
                context.Response.Headers.CacheControl = string.Create(CultureInfo.InvariantCulture, $"public, max-age={CacheSeconds}");
                if (context.Request.GetTypedHeaders().IfNoneMatch.Any(tag => tag.Compare(etag, useStrongComparison: false)))
                {
                    return Results.StatusCode(StatusCodes.Status304NotModified);
                }

                return Results.Bytes(json, "application/json; charset=utf-8");
            })
            .AllowAnonymous()
            .RequireRateLimiting(Policies.RateLimits.AnonIp)
            .WithTags("Config")
            .WithName("GetRemoteConfig")
            .WithSummary("Feature flags, minimum app versions and public policy constants; supports If-None-Match.");
    }
}
