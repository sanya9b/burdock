using System.Security.Cryptography;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;
using Burdock.Server.Modules.Pronunciation.Domain;
using Burdock.Server.Shared.Time;

namespace Burdock.Server.Modules.Pronunciation.Application;

/// <summary>One pre-rendered clip from the pipeline index (ADR 0019): the expected hash comes from audio-index.json.</summary>
public sealed record RenderedClipImport(LexemeId LexemeId, string? VoiceId, string? Text, ReadOnlyMemory<byte> Content, string ExpectedSha256);

public sealed record ImportedClip(Clip Clip, bool AlreadyImported);

/// <summary>
/// Registers clips rendered by the pipeline (ADR 0020 §6): the integrity of the file is checked against the index, the
/// clip is stored under the current engine version and recorded as rendered by the pipeline. Importing the same clip twice
/// is a no-op, so the whole dictionary can be re-published after an interruption.
/// </summary>
public sealed class ImportRenderedClipUseCase(IClipRepository clips, IClipStore store, IClock clock, PronunciationOptions options)
{
    public async Task<Result<ImportedClip>> ExecuteAsync(RenderedClipImport import, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(import);
        var text = PronunciationText.Create(import.Text);
        if (text.IsFailure)
        {
            return Result.Failure<ImportedClip>(text.Error);
        }

        if (!VoiceCatalog.TryGet(import.VoiceId, out var voice))
        {
            return Result.Failure<ImportedClip>(UnknownVoice(import.VoiceId));
        }

        var sha256 = Convert.ToHexStringLower(SHA256.HashData(import.Content.Span));
        if (import.Content.IsEmpty || !string.Equals(sha256, import.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<ImportedClip>(DomainError.Validation("pronunciation.clip_corrupt", "Errors_Pronunciation_ClipCorrupt"));
        }

        var key = new ClipKey(import.LexemeId, voice.Id);
        var existing = await clips.GetAsync(key, cancellationToken).ConfigureAwait(false);
        if (existing is { Status: ClipStatus.Rendered } && string.Equals(existing.EngineVersion, options.EngineVersion, StringComparison.Ordinal) && string.Equals(existing.Sha256, sha256, StringComparison.Ordinal))
        {
            return Result.Success(new ImportedClip(existing, AlreadyImported: true));
        }

        var now = clock.UtcNow;
        var stored = await store.PutAsync(Clip.StoragePath(options.EngineVersion, key), import.Content, cancellationToken).ConfigureAwait(false);
        var clip = Clip.Queued(key, text.Value, options.EngineVersion, ClipRequester.Pipeline, existing?.CreatedAt ?? now).Rendered(stored.Path, stored.Sha256, stored.Bytes, now);
        await clips.SaveAsync(clip, cancellationToken).ConfigureAwait(false);
        return Result.Success(new ImportedClip(clip, AlreadyImported: false));
    }

    /// <summary>Stores the voice's sample phrase next to its clips; returns the storage path.</summary>
    public async Task<Result<string>> ImportSampleAsync(string? voiceId, ReadOnlyMemory<byte> content, string expectedSha256, CancellationToken cancellationToken)
    {
        if (!VoiceCatalog.TryGet(voiceId, out var voice))
        {
            return Result.Failure<string>(UnknownVoice(voiceId));
        }

        var sha256 = Convert.ToHexStringLower(SHA256.HashData(content.Span));
        if (content.IsEmpty || !string.Equals(sha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<string>(DomainError.Validation("pronunciation.clip_corrupt", "Errors_Pronunciation_ClipCorrupt"));
        }

        var stored = await store.PutAsync(Clip.SamplePath(options.EngineVersion, voice.Id), content, cancellationToken).ConfigureAwait(false);
        return Result.Success(stored.Path);
    }

    private static DomainError UnknownVoice(string? voiceId) => DomainError.Validation(
        "pronunciation.voice_unknown",
        "Errors_Pronunciation_VoiceUnknown",
        new Dictionary<string, string>(StringComparer.Ordinal) { ["voice"] = voiceId ?? string.Empty });
}
