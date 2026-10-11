using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Burdock.Contracts.V1;
using Burdock.Domain.Vocabulary;
using Burdock.Server.Modules.Content.Application;
using Burdock.Server.Shared.Errors;
using Burdock.Server.Shared.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;

namespace Burdock.Server.Modules.Content.Endpoints;

/// <summary>GET /v1/content/manifest with ETag / If-None-Match (spec 09 FR-09-7) and, in development, the local object store.</summary>
public static class ContentEndpoints
{
    private const string DefaultPair = "uk-en";
    private const int CacheSeconds = 300;
    private const string ImmutableCache = "public, max-age=31536000, immutable";

    public static void Map(IEndpointRouteBuilder endpoints, ContentOptions options)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(options);
        var group = endpoints.MapGroup(ApiRoutes.Content.Group).WithTags("Content");

        group.MapGet("/manifest", GetManifestAsync)
            .RequireAuthorization(Policies.User)
            .RequireRateLimiting(Policies.RateLimits.User)
            .WithName("GetContentManifest")
            .WithSummary("Manifest of content packs for a language pair, filtered by entitlement; supports If-None-Match.");

        if (options.ServeStoreLocally)
        {
            group.MapGet("/store/{**key}", ServeStoredObjectAsync)
                .AllowAnonymous()
                .RequireRateLimiting(Policies.RateLimits.AnonIp)
                .WithName("GetStoredContentObject")
                .WithSummary("Development only: serves objects of the local pack store the way the CDN would.")
                .ExcludeFromDescription();
        }
    }

    private static async Task<IResult> GetManifestAsync(HttpContext context, ClaimsPrincipal principal, GetManifestUseCase useCase, string? pair, CancellationToken cancellationToken)
    {
        var parsed = LanguagePair.Parse(pair ?? DefaultPair);
        if (parsed.IsFailure)
        {
            return parsed.Error.ToProblem();
        }

        if (!CurrentUser.TryGetLearnerId(principal, out var learnerId))
        {
            return Results.Unauthorized();
        }

        var manifest = await useCase.ExecuteAsync(parsed.Value, learnerId, cancellationToken).ConfigureAwait(false);
        var fingerprint = new StringBuilder().Append(manifest.Pair).Append('|').Append(manifest.ManifestVersion);
        foreach (var pack in manifest.Packs)
        {
            fingerprint.Append('|').Append(pack.Id).Append(':').Append(pack.Version).Append(':').Append(pack.Sha256);
        }

        var etag = new EntityTagHeaderValue(string.Create(CultureInfo.InvariantCulture, $"\"{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint.ToString())))[..16]}\""), isWeak: true);
        var response = context.Response;
        response.Headers.ETag = etag.ToString();
        response.Headers.CacheControl = string.Create(CultureInfo.InvariantCulture, $"private, max-age={CacheSeconds}");
        if (context.Request.GetTypedHeaders().IfNoneMatch.Any(tag => tag.Compare(etag, useStrongComparison: false)))
        {
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        return Results.Ok(manifest);
    }

    private static async Task<IResult> ServeStoredObjectAsync(string key, IPackObjectStore store, HttpContext context, CancellationToken cancellationToken)
    {
        var stream = await store.OpenReadAsync(key, cancellationToken).ConfigureAwait(false);
        if (stream is null)
        {
            return Results.NotFound();
        }

        context.Response.Headers.CacheControl = ImmutableCache;
        return Results.File(stream, PublishPackUseCase.ContentType, enableRangeProcessing: true);
    }
}
