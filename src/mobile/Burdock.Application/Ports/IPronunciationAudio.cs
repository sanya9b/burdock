using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;

namespace Burdock.Application.Ports;

/// <summary>A playable clip: a logical asset path inside the app package today, a cached download later (ADR 0019).</summary>
public sealed record PronunciationClip(string AssetPath);

/// <summary>
/// Pre-rendered pronunciation audio and its voice catalogue (ADR 0019). Callers fall back to the system voice when a clip
/// is missing, so every method answers null instead of throwing for unknown words or voices.
/// </summary>
public interface IPronunciationAudio
{
    /// <summary>Voices the content offers, in catalogue order; the first one is the default.</summary>
    Task<IReadOnlyList<PronunciationVoice>> GetVoicesAsync(CancellationToken cancellationToken);

    Task<PronunciationClip?> FindWordAsync(LexemeId lexemeId, VoiceId voice, CancellationToken cancellationToken);

    /// <summary>The short sample phrase of a voice for the picker.</summary>
    Task<PronunciationClip?> FindSampleAsync(VoiceId voice, CancellationToken cancellationToken);
}
