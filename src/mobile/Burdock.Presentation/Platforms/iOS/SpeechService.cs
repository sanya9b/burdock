using AVFoundation;
using Foundation;

namespace Burdock.Presentation.Services;

public sealed partial class SpeechService
{
    private readonly AVSpeechSynthesizer _synthesizer = new();
    private AVSpeechSynthesisVoice? _voice;
    private AVAudioPlayer? _player;
    private bool _sessionConfigured;
    private bool _synthesizerHooked;

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

        _player.FinishedPlaying += (_, _) => ReleaseAudioSession();
        _player.DecoderError += (_, _) => ReleaseAudioSession();

        if (!ActivateAudioSession())
        {
            return Task.FromResult(false);
        }

        var started = _player.Play();
        if (!started)
        {
            ReleaseAudioSession();
        }

        return Task.FromResult(started);
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

        if (_player is { Playing: true })
        {
            _player.Stop();
        }

        HookSynthesizer();
        ActivateAudioSession();

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

    /// <summary>
    /// Without an explicit category iOS runs the app in «solo ambient», which follows the Ring/Silent switch: on a phone set to
    /// silent both the pack clips and the speech engine play nothing. Pronunciation is what the learner tapped for, so the
    /// session is «playback» (audible with the switch on silent, like other language apps), ducking music and pausing
    /// podcasts for the length of a word, and releasing them again when the clip or the utterance ends.
    /// </summary>
    private bool ActivateAudioSession()
    {
        var session = AVAudioSession.SharedInstance();
        if (!_sessionConfigured)
        {
            var categoryError = session.SetCategory(
                AVAudioSessionCategory.Playback,
                AVAudioSessionCategoryOptions.DuckOthers | AVAudioSessionCategoryOptions.InterruptSpokenAudioAndMixWithOthers);
            _sessionConfigured = categoryError is null;
        }

        return session.SetActive(true) is null;
    }

    /// <summary>Deactivates the session once nothing of ours plays, so ducked music comes back to full volume.</summary>
    private void ReleaseAudioSession()
    {
        if (_synthesizer.Speaking || _player is { Playing: true })
        {
            return;
        }

        AVAudioSession.SharedInstance().SetActive(false, AVAudioSessionSetActiveOptions.NotifyOthersOnDeactivation, out _);
    }

    private void HookSynthesizer()
    {
        if (_synthesizerHooked)
        {
            return;
        }

        _synthesizerHooked = true;
        _synthesizer.DidFinishSpeechUtterance += (_, _) => ReleaseAudioSession();
        _synthesizer.DidCancelSpeechUtterance += (_, _) => ReleaseAudioSession();
    }

    // iOS has no deep link into Settings → Accessibility → Spoken Content; the hint text explains the path instead.
    private static partial bool PlatformSupportsVoiceSettings() => false;

    private static partial Task PlatformOpenVoiceSettingsAsync() => Task.CompletedTask;
}
