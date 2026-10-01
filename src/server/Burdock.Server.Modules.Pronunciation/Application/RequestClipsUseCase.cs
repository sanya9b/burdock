using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;
using Burdock.Server.Modules.Pronunciation.Domain;
using Burdock.Server.Shared.Time;

namespace Burdock.Server.Modules.Pronunciation.Application;

public sealed record ClipRequest(LexemeId LexemeId, string? Text, IReadOnlyList<string>? Voices, ClipRequester RequestedBy);

public sealed record ClipsView(LexemeId LexemeId, PronunciationText Text, string EngineVersion, IReadOnlyList<Clip> Clips);

/// <summary>
/// Queues the missing voices of a lexeme for rendering (ADR 0020 §2). Idempotent: a clip already rendered with the current
/// engine version is returned as is, a clip already queued for the same text is not queued twice, a failed clip is retried.
/// </summary>
public sealed class RequestClipsUseCase(IClipRepository clips, IRenderJobs queue, IClock clock, PronunciationOptions options)
{
    public async Task<Result<ClipsView>> ExecuteAsync(ClipRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var text = PronunciationText.Create(request.Text);
        if (text.IsFailure)
        {
            return Result.Failure<ClipsView>(text.Error);
        }

        var voices = ResolveVoices(request.Voices);
        if (voices.IsFailure)
        {
            return Result.Failure<ClipsView>(voices.Error);
        }

        var now = clock.UtcNow;
        var result = new List<Clip>(voices.Value.Count);
        foreach (var voice in voices.Value)
        {
            var key = new ClipKey(request.LexemeId, voice.Id);
            var existing = await clips.GetAsync(key, cancellationToken).ConfigureAwait(false);
            var current = existing is not null && string.Equals(existing.EngineVersion, options.EngineVersion, StringComparison.Ordinal);
            if (current && existing!.Status == ClipStatus.Rendered)
            {
                result.Add(existing);
                continue;
            }

            if (current && existing!.Status == ClipStatus.Queued && existing.Text == text.Value)
            {
                result.Add(existing);
                continue;
            }

            var clip = Clip.Queued(key, text.Value, options.EngineVersion, request.RequestedBy, now);
            await clips.SaveAsync(clip, cancellationToken).ConfigureAwait(false);
            await queue.EnqueueAsync(key, cancellationToken).ConfigureAwait(false);
            result.Add(clip);
        }

        return Result.Success(new ClipsView(request.LexemeId, text.Value, options.EngineVersion, result));
    }

    private static Result<IReadOnlyList<PronunciationVoice>> ResolveVoices(IReadOnlyList<string>? requested)
    {
        if (requested is null || requested.Count == 0)
        {
            return Result.Success(VoiceCatalog.V1);
        }

        var voices = new List<PronunciationVoice>(requested.Count);
        foreach (var id in requested)
        {
            if (!VoiceCatalog.TryGet(id, out var voice))
            {
                return Result.Failure<IReadOnlyList<PronunciationVoice>>(DomainError.Validation(
                    "pronunciation.voice_unknown",
                    "Errors_Pronunciation_VoiceUnknown",
                    new Dictionary<string, string>(StringComparer.Ordinal) { ["voice"] = id ?? string.Empty }));
            }

            if (!voices.Contains(voice))
            {
                voices.Add(voice);
            }
        }

        return Result.Success<IReadOnlyList<PronunciationVoice>>(voices);
    }
}
