using Burdock.Presentation.ViewModels;
using Microsoft.Maui.Storage;

namespace Burdock.Presentation.Services;

/// <summary>
/// English pronunciation through the platform speech engine (decision T7, ADR 0019 keeps it as the fallback behind
/// pre-rendered pack audio). Unlike the default MAUI wrapper it picks the best installed voice of the chosen accent
/// (the engine default is usually the lowest-quality «compact» voice), slows single words down a little, and remembers
/// the accent choice (spec 12: en-US default, en-GB by choice) on the device.
/// Platform halves live in Platforms/Android and Platforms/iOS; the neutral head is silent.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Registered as a singleton; the engine lives as long as the app.")]
public sealed partial class SpeechService(IPreferences preferences) : ISpeechService
{
    private const string AccentKey = "speech.accent";

    /// <summary>Single words read clearer a little slower than running speech; sentences keep a natural pace.</summary>
    private const float WordRate = 0.85f;
    private const float SentenceRate = 0.95f;

    private string? _voiceResolvedFor;

    public PronunciationAccent Accent
    {
        get => Enum.TryParse<PronunciationAccent>(preferences.Get(AccentKey, nameof(PronunciationAccent.American)), out var accent) ? accent : PronunciationAccent.American;
        set
        {
            preferences.Set(AccentKey, value.ToString());
            if (_voiceResolvedFor is not null)
            {
                _voiceResolvedFor = null; // the platform head re-picks the voice for the new accent
            }
        }
    }

    public bool CanOpenSystemVoiceSettings => PlatformSupportsVoiceSettings();

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

    public Task OpenSystemVoiceSettingsAsync() => PlatformOpenVoiceSettingsAsync();

    private partial Task PlatformSpeakAsync(string text, string country, float rate, CancellationToken cancellationToken);

    private static partial bool PlatformSupportsVoiceSettings();

    private static partial Task PlatformOpenVoiceSettingsAsync();
}
