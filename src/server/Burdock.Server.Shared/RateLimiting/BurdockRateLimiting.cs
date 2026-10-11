using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Burdock.Server.Shared.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Burdock.Server.Shared.RateLimiting;

/// <summary>Limits per spec 09 §3.15; overridable through the «RateLimits» section as «permits/window», for example «5/01:00:00».</summary>
public sealed record RateLimitRule(int Permits, TimeSpan Window)
{
    public static RateLimitRule Parse(string? value, RateLimitRule fallback)
    {
        ArgumentNullException.ThrowIfNull(fallback);
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var parts = value.Split('/', 2, StringSplitOptions.TrimEntries);
        return parts.Length == 2 && int.TryParse(parts[0], System.Globalization.CultureInfo.InvariantCulture, out var permits) && permits > 0 && TimeSpan.TryParse(parts[1], System.Globalization.CultureInfo.InvariantCulture, out var window) && window > TimeSpan.Zero
            ? new RateLimitRule(permits, window)
            : fallback;
    }
}

/// <summary>
/// Built-in rate limiting (spec 09 §3.2): authenticated requests are partitioned by <c>sub</c>, anonymous ones by a
/// hash of the IP prefix (/24 for IPv4, /64 for IPv6) that lives only in memory. Rejections answer 429 with Retry-After.
/// </summary>
public static class BurdockRateLimiting
{
    public const string SectionName = "RateLimits";

    public static IServiceCollection AddBurdockRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(SectionName);
        var identityCreate = RateLimitRule.Parse(section["IdentityCreate"], new RateLimitRule(5, TimeSpan.FromHours(1)));
        var token = RateLimitRule.Parse(section["Token"], new RateLimitRule(20, TimeSpan.FromMinutes(1)));
        var anonIp = RateLimitRule.Parse(section["AnonIp"], new RateLimitRule(60, TimeSpan.FromMinutes(1)));
        var user = RateLimitRule.Parse(section["User"], new RateLimitRule(120, TimeSpan.FromMinutes(1)));
        var userWrite = RateLimitRule.Parse(section["UserWrite"], new RateLimitRule(60, TimeSpan.FromMinutes(1)));
        var progressBatch = RateLimitRule.Parse(section["ProgressBatch"], new RateLimitRule(30, TimeSpan.FromMinutes(1)));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = static (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                return ValueTask.CompletedTask;
            };

            options.AddPolicy(Policies.RateLimits.IdentityCreate, context => Partition(ClientKey(context), identityCreate));
            options.AddPolicy(Policies.RateLimits.Token, context => Partition(ClientKey(context), token));
            options.AddPolicy(Policies.RateLimits.AnonIp, context => Partition(ClientKey(context), anonIp));
            options.AddPolicy(Policies.RateLimits.User, context => Partition(ClientKey(context), user));
            options.AddPolicy(Policies.RateLimits.UserWrite, context => Partition(ClientKey(context), userWrite));
            options.AddPolicy(Policies.RateLimits.ProgressBatch, context => Partition(ClientKey(context), progressBatch));
        });
        return services;
    }

    private static RateLimitPartition<string> Partition(string key, RateLimitRule rule)
        => RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = rule.Permits,
            Window = rule.Window,
            QueueLimit = 0,
            AutoReplenishment = true,
        });

    /// <summary>The learner id when authenticated, otherwise an opaque hash of the network prefix; the raw address is never kept.</summary>
    public static string ClientKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.User.Identity?.IsAuthenticated == true && CurrentUser.TryGetLearnerId(context.User, out var learnerId))
        {
            return "u:" + learnerId.Value.ToString("N");
        }

        var address = context.Connection.RemoteIpAddress ?? IPAddress.Loopback;
        var bytes = address.MapToIPv6().GetAddressBytes();
        var prefix = address.IsIPv4MappedToIPv6 || address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
            ? bytes[..15]
            : bytes[..8];
        return "p:" + Convert.ToHexStringLower(SHA256.HashData(prefix))[..24];
    }
}
