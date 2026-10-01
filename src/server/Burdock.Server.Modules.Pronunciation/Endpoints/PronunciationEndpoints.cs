using System.Globalization;
using Burdock.Contracts.V1;
using Burdock.Contracts.V1.Pronunciation;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;
using Burdock.Server.Modules.Pronunciation.Application;
using Burdock.Server.Modules.Pronunciation.Domain;
using Burdock.Server.Shared.Errors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;

namespace Burdock.Server.Modules.Pronunciation.Endpoints;

/// <summary>GET /v1/pronunciation/voices, GET /v1/pronunciation/clips/{lexemeId}/{voice}, POST /v1/pronunciation/clips (ADR 0020 §2).</summary>
public static class PronunciationEndpoints
{
    private const string ImmutableCache = "public, max-age=31536000, immutable";
    private const string RetryAfterSeconds = "5";

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var group = endpoints.MapGroup(ApiRoutes.Pronunciation.Group).WithTags("Pronunciation");

        group.MapGet("/voices", GetVoices)
            .WithName("GetPronunciationVoices")
            .WithSummary("The voice catalogue the app offers in «Голос».");

        group.MapGet("/clips/{lexemeId:guid}/{voice}", GetClipAsync)
            .WithName("GetPronunciationClip")
            .WithSummary("The clip of a lexeme in one voice: redirect to the CDN or the file itself; 202 while it renders, 404 when never requested.");

        group.MapPost("/clips", RequestClipsAsync)
            .AddEndpointFilter<AdminKeyFilter>()
            .WithName("RequestPronunciationClips")
            .WithSummary("Queues the missing voices of a lexeme for rendering (admin key required); idempotent.");
    }

    private static IResult GetVoices(HttpContext context)
    {
        context.Response.Headers.CacheControl = string.Create(CultureInfo.InvariantCulture, $"public, max-age={TimeSpan.FromDays(1).TotalSeconds:0}");
        return Results.Ok(VoiceCatalog.V1.Select(ToDto).ToArray());
    }

    private static async Task<IResult> GetClipAsync(Guid lexemeId, string voice, GetClipUseCase useCase, IClipStore store, HttpContext context, CancellationToken cancellationToken)
    {
        var lookup = await useCase.ExecuteAsync(lexemeId, voice, cancellationToken).ConfigureAwait(false);
        if (lookup.IsFailure)
        {
            return lookup.Error.ToProblem();
        }

        var clip = lookup.Value.Clip;
        switch (clip.Status)
        {
            case ClipStatus.Queued:
                context.Response.Headers.RetryAfter = RetryAfterSeconds;
                return Results.Accepted(value: ToDto(clip, null));
            case ClipStatus.Failed:
                return DomainError.Unavailable("pronunciation.render_failed", "Errors_Pronunciation_RenderFailed").ToProblem();
            default:
                break;
        }

        if (lookup.Value.Url is { } url)
        {
            context.Response.Headers.CacheControl = ImmutableCache;
            return Results.Redirect(url.ToString(), permanent: false);
        }

        var stream = await store.OpenReadAsync(clip.Path!, cancellationToken).ConfigureAwait(false);
        if (stream is null)
        {
            return DomainError.NotFound("pronunciation.clip_missing", "Errors_Pronunciation_ClipMissing").ToProblem();
        }

        context.Response.Headers.CacheControl = ImmutableCache;
        return Results.File(stream, "audio/mp4", lastModified: clip.RenderedAt, entityTag: new EntityTagHeaderValue($"\"{clip.Sha256}\""), enableRangeProcessing: true);
    }

    private static async Task<IResult> RequestClipsAsync(RequestClipsDto body, RequestClipsUseCase useCase, IClipStore store, CancellationToken cancellationToken)
    {
        if (body is null || body.LexemeId == Guid.Empty)
        {
            return DomainError.Validation("pronunciation.lexeme_invalid", "Errors_Pronunciation_LexemeInvalid").ToProblem();
        }

        var result = await useCase.ExecuteAsync(new ClipRequest(LexemeId.From(body.LexemeId), body.Text, body.Voices, ClipRequester.Admin), cancellationToken).ConfigureAwait(false);
        return result.ToResult(view => Results.Accepted(value: new ClipsDto(
            view.LexemeId.Value,
            view.Text.Value,
            view.EngineVersion,
            view.Clips.Select(c => ToDto(c, c.Path is null ? null : store.PublicUrl(c.Path))).ToArray())));
    }

    private static VoiceDto ToDto(PronunciationVoice voice)
        => new(voice.Id.Value, voice.Name, voice.Accent == PronunciationAccent.British ? "RP" : "GA", voice.Gender == VoiceGender.Female ? "female" : "male");

    private static ClipStateDto ToDto(Clip clip, Uri? url)
        => new(clip.Key.Voice.Value, clip.Status.ToString().ToLowerInvariant(), url, clip.Sha256, clip.Bytes);
}
