using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Burdock.Contracts.V1;
using Burdock.Contracts.V1.Settings;
using Burdock.Domain.SharedKernel;
using Burdock.Server.Modules.SettingsSync.Application;
using Burdock.Server.Shared.Errors;
using Burdock.Server.Shared.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;

namespace Burdock.Server.Modules.SettingsSync.Endpoints;

/// <summary>GET and PUT /v1/settings (spec 09 §3.6, FR-09-13).</summary>
public static class SettingsEndpoints
{
    public const long MaxBodyBytes = 256 * 1024;

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var group = endpoints.MapGroup(ApiRoutes.Settings.Group).WithTags("Settings").RequireAuthorization(Policies.User);

        group.MapGet(string.Empty, GetAsync)
            .RequireRateLimiting(Policies.RateLimits.User)
            .WithName("GetSettings")
            .WithSummary("The learner's merged settings document with its ETag; supports If-None-Match.");

        group.MapPut(string.Empty, PutAsync)
            .RequireRateLimiting(Policies.RateLimits.UserWrite)
            .WithMetadata(new RequestSizeLimitAttribute(MaxBodyBytes))
            .WithName("PutSettings")
            .WithSummary("Merges the given entries per key with last-writer-wins and returns the resulting document.");
    }

    private static async Task<IResult> GetAsync(HttpContext context, ClaimsPrincipal principal, GetSettingsUseCase useCase, CancellationToken cancellationToken)
    {
        if (!CurrentUser.TryGetLearnerId(principal, out var learnerId))
        {
            return Results.Unauthorized();
        }

        var document = await useCase.ExecuteAsync(learnerId, cancellationToken).ConfigureAwait(false);
        var etag = new EntityTagHeaderValue(string.Create(CultureInfo.InvariantCulture, $"\"{document.Etag}\""));
        context.Response.Headers.ETag = etag.ToString();
        context.Response.Headers.CacheControl = "private, no-cache";
        if (context.Request.GetTypedHeaders().IfNoneMatch.Any(tag => tag.Compare(etag, useStrongComparison: true)))
        {
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        return Results.Ok(ToDto(document));
    }

    private static async Task<IResult> PutAsync(HttpContext context, ClaimsPrincipal principal, PutSettingsDto body, PutSettingsUseCase useCase, CancellationToken cancellationToken)
    {
        if (!CurrentUser.TryGetLearnerId(principal, out var learnerId))
        {
            return Results.Unauthorized();
        }

        if (body?.Entries is null)
        {
            return DomainError.Validation("settings.body_missing", "Errors_Settings_BodyMissing").ToProblem();
        }

        var incoming = body.Entries.Select(kv => new IncomingSetting(kv.Key, kv.Value.Value.ValueKind == JsonValueKind.Undefined ? null : kv.Value.Value.GetRawText(), kv.Value.UpdatedAt, kv.Value.ClientId)).ToArray();
        var result = await useCase.ExecuteAsync(learnerId, body.SchemaVersion, incoming, cancellationToken).ConfigureAwait(false);
        return result.ToResult(document =>
        {
            context.Response.Headers.ETag = string.Create(CultureInfo.InvariantCulture, $"\"{document.Etag}\"");
            return Results.Ok(ToDto(document));
        });
    }

    private static SettingsDocumentDto ToDto(SettingsDocument document)
    {
        var entries = new Dictionary<string, SettingsEntryDto>(StringComparer.Ordinal);
        foreach (var (key, entry) in document.Entries)
        {
            using var json = JsonDocument.Parse(entry.ValueJson);
            entries[key] = new SettingsEntryDto(json.RootElement.Clone(), entry.UpdatedAt, entry.ClientId);
        }

        return new SettingsDocumentDto(document.SchemaVersion, entries, document.Etag);
    }
}
