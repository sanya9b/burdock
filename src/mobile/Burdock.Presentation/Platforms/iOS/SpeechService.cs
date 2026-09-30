using AVFoundation;
using Foundation;

namespace Burdock.Presentation.Services;

public sealed partial class SpeechService
{
    private readonly AVSpeechSynthesizer _synthesizer = new();
    private AVSpeechSynthesisVoice? _voice;
    private AVAudioPlayer? _player;

    /// <summary>Pack clips are MauiAssets copied into the bundle under their logical path.</summary>
    private partial Task<bool> PlatformPlayAsync(string assetPath, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(false);
        }

        var path = Path.Combine(NSBundle.MainBundle.BundlePath, assetPath);
        if (!File.Exists(path))
        {
            return Task.FromResult(false);
        }

        if (_synthesizer.Speaking)
        {
            _synthesizer.StopSpeaking(AVSpeechBoundary.Immediate);
        }

        _player?.Stop();
        _player?.Dispose();
        _player = AVAudioPlayer.FromUrl(NSUrl.FromFilename(path), out var error);
        if (_player is null || error is not null)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(_player.Play());
    }

    private partial Task PlatformSpeakAsync(string text, string country, float rate, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }

        var language = $"en-{country}";
        if (!string.Equals(_voiceResolvedFor, country, StringComparison.Ordinal))
        {
            _voice = PickVoice(language);
            _voiceResolvedFor = country;
        }

        if (_synthesizer.Speaking)
        {
            _synthesizer.StopSpeaking(AVSpeechBoundary.Immediate);
        }

        var utterance = new AVSpeechUtterance(text)
        {
            Voice = _voice ?? AVSpeechSynthesisVoice.FromLanguage(language),
            Rate = AVSpeechUtterance.DefaultSpeechRate * rate,
        };
        _synthesizer.SpeakUtterance(utterance);
        return Task.CompletedTask;
    }

    /// <summary>Premium, then Enhanced, then the default voice of the language; Siri voices are not available to apps.</summary>
    private static AVSpeechSynthesisVoice? PickVoice(string language)
        => AVSpeechSynthesisVoice.GetSpeechVoices()
            .Where(v => string.Equals(v.Language, language, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(v => (int)v.Quality)
            .FirstOrDefault();

    // iOS has no deep link into Settings → Accessibility → Spoken Content; the hint text explains the path instead.
    private static partial bool PlatformSupportsVoiceSettings() => false;

    private static partial Task PlatformOpenVoiceSettingsAsync() => Task.CompletedTask;
}
