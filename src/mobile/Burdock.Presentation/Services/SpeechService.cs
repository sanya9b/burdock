using Burdock.Application.Ports;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;
using Burdock.Presentation.Resources.Strings;
using Burdock.Presentation.ViewModels;
using Microsoft.Maui.Storage;

namespace Burdock.Presentation.Services;

/// <summary>
/// Pronunciation on the device (ADR 0019). Words play from the pack audio of the learner's voice when the content has a clip;
/// everything else goes through the platform speech engine, which picks the best installed voice of the same accent (the
/// engine default is usually the lowest-quality «compact» voice) and slows single words down a little. One voice per device.
/// Platform halves live in Platforms/Android and Platforms/iOS; the neutral head plays nothing.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Registered as a singleton; the engine and the player live as long as the app.")]
public sealed partial class SpeechService(IPreferences preferences, IPronunciationAudio audio) : ISpeechService
{
    private const string VoiceKey = "speech.voice";
    private const string AccentKey = "speech.accent";

    /// <summary>Single words read clearer a little slower than running speech; sentences keep a natural pace.</summary>
    private const float WordRate = 0.85f;
    private const float SentenceRate = 0.95f;

    private IReadOnlyList<PronunciationVoice>? _voices;
    private string? _voiceResolvedFor;

    public VoiceId? Voice
    {
        get
        {
            var stored = preferences.Get(VoiceKey, string.Empty);
            if (stored.Length == 0)
            {
                return null;
            }

            try
            {
                return VoiceId.From(stored);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }

    /// <summary>Accent of the chosen voice; without a choice the default voice (the first of the catalogue) is American.</summary>
    public PronunciationAccent Accent
        => Voice is not null && Enum.TryParse<PronunciationAccent>(preferences.Get(AccentKey, nameof(PronunciationAccent.American)), out var accent) ? accent : PronunciationAccent.American;

    public bool CanOpenSystemVoiceSettings => PlatformSupportsVoiceSettings();

    public async Task<IReadOnlyList<PronunciationVoice>> GetVoicesAsync(CancellationToken cancellationToken)
        => _voices ??= await audio.GetVoicesAsync(cancellationToken).ConfigureAwait(true);

    public void SelectVoice(PronunciationVoice voice)
    {
        ArgumentNullException.ThrowIfNull(voice);
        preferences.Set(VoiceKey, voice.Id.Value);
        preferences.Set(AccentKey, voice.Accent.ToString());
        if (_voiceResolvedFor is not null)
        {
            _voiceResolvedFor = null; // the platform head re-picks the system voice for the new accent
        }
    }

    public async Task SpeakWordAsync(LexemeId lexemeId, string lemma, CancellationToken cancellationToken)
    {
        var voice = await EffectiveVoiceAsync(cancellationToken).ConfigureAwait(true);
        if (voice is { } chosen
            && await audio.FindWordAsync(lexemeId, chosen, cancellationToken).ConfigureAwait(true) is { } clip
            && await PlatformPlayAsync(clip.AssetPath, cancellationToken).ConfigureAwait(true))
        {
            return;
        }

        await SpeakEnglishAsync(lemma, cancellationToken).ConfigureAwait(true);
    }

    public Task SpeakEnglishAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.CompletedTask;
        }

        var trimmed = text.Trim();
        var rate = trimmed.Contains(' ', StringComparison.Ordinal) && trimmed.Length > 16 ? SentenceRate : WordRate;
        return PlatformSpeakAsync(trimmed, Accent == PronunciationAccent.British ? "GB" : "US", rate, cancellationToken);
    }

    public async Task PlaySampleAsync(VoiceId voice, CancellationToken cancellationToken)
    {
        if (await audio.FindSampleAsync(voice, cancellationToken).ConfigureAwait(true) is { } clip
            && await PlatformPlayAsync(clip.AssetPath, cancellationToken).ConfigureAwait(true))
        {
            return;
        }

        await SpeakEnglishAsync(AppResources.Settings_Voice_SampleWord, cancellationToken).ConfigureAwait(true);
    }

    public Task OpenSystemVoiceSettingsAsync() => PlatformOpenVoiceSettingsAsync();

    /// <summary>The stored voice when the catalogue still offers it, otherwise the first voice of the catalogue, or null without content audio.</summary>
    private async Task<VoiceId?> EffectiveVoiceAsync(CancellationToken cancellationToken)
    {
        var voices = await GetVoicesAsync(cancellationToken).ConfigureAwait(true);
        if (voices.Count == 0)
        {
            return null;
        }

        var stored = Voice;
        return stored is { } id && voices.Any(v => v.Id == id) ? id : voices[0].Id;
    }

    private partial Task PlatformSpeakAsync(string text, string country, float rate, CancellationToken cancellationToken);

    /// <summary>Plays a bundled or cached clip; false when the platform could not, so the caller falls back to speech.</summary>
    private partial Task<bool> PlatformPlayAsync(string assetPath, CancellationToken cancellationToken);

    private static partial bool PlatformSupportsVoiceSettings();

    private static partial Task PlatformOpenVoiceSettingsAsync();
}
