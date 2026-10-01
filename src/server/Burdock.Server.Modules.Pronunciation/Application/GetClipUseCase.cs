using Burdock.Domain.SharedKernel;
using Burdock.Server.Modules.Pronunciation.Domain;

namespace Burdock.Server.Modules.Pronunciation.Application;

/// <summary>A clip with the public address it can be fetched from, when the store has one.</summary>
public sealed record ClipLookup(Clip Clip, Uri? Url);

/// <summary>Resolves one lexeme + voice to its clip state (ADR 0020 §2); the endpoint turns the state into 302 / 200 / 202 / 404.</summary>
public sealed class GetClipUseCase(IClipRepository clips, IClipStore store)
{
    public async Task<Result<ClipLookup>> ExecuteAsync(Guid lexemeId, string? voiceId, CancellationToken cancellationToken)
    {
        if (lexemeId == Guid.Empty)
        {
            return Result.Failure<ClipLookup>(DomainError.Validation("pronunciation.lexeme_invalid", "Errors_Pronunciation_LexemeInvalid"));
        }

        if (!VoiceCatalog.TryGet(voiceId, out var voice))
        {
            return Result.Failure<ClipLookup>(DomainError.Validation(
                "pronunciation.voice_unknown",
                "Errors_Pronunciation_VoiceUnknown",
                new Dictionary<string, string>(StringComparer.Ordinal) { ["voice"] = voiceId ?? string.Empty }));
        }

        var clip = await clips.GetAsync(new ClipKey(Burdock.Domain.SharedKernel.LexemeId.From(lexemeId), voice.Id), cancellationToken).ConfigureAwait(false);
        if (clip is null)
        {
            return Result.Failure<ClipLookup>(DomainError.NotFound("pronunciation.clip_not_found", "Errors_Pronunciation_ClipNotFound"));
        }

        return Result.Success(new ClipLookup(clip, clip.Path is null ? null : store.PublicUrl(clip.Path)));
    }
}
