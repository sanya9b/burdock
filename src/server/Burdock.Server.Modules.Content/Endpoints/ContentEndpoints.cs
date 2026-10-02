using System.Globalization;
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

/// <summary>GET /v1/content/manifest with ETag / If-None-Match (spec 09 FR-09-7).</summary>
public static class ContentEndpoints
{
    private const string DefaultPair = "uk-en";
    private const int CacheSeconds = 300;

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var group = endpoints.MapGroup(ApiRoutes.Content.Group).WithTags("Content");

        group.MapGet("/manifest", GetManifestAsync)
            .RequireAuthorization(Policies.User)
            .RequireRateLimiting(Policies.RateLimits.User)
            .WithName("GetContentManifest")
            .WithSummary("Manifest of content packs for a language pair; supports If-None-Match.");
    }

    private static async Task<IResult> GetManifestAsync(HttpContext context, IContentManifestProvider provider, string? pair, CancellationToken cancellationToken)
    {
        var parsed = LanguagePair.Parse(pair ?? DefaultPair);
        if (parsed.IsFailure)
        {
            return parsed.Error.ToProblem();
        }

        var manifest = await provider.GetManifestAsync(parsed.Value, cancellationToken).ConfigureAwait(false);
        var etag = new EntityTagHeaderValue(
            string.Create(CultureInfo.InvariantCulture, $"\"{manifest.Pair}-{manifest.SchemaVersion}-{manifest.ManifestVersion}\""),
            isWeak: true);

        var response = context.Response;
        response.Headers.ETag = etag.ToString();
        response.Headers.CacheControl = string.Create(CultureInfo.InvariantCulture, $"private, max-age={CacheSeconds}");

        var requestHeaders = context.Request.GetTypedHeaders();
        if (requestHeaders.IfNoneMatch.Any(tag => tag.Compare(etag, useStrongComparison: false)))
        {
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        return Results.Ok(manifest);
    }
}
