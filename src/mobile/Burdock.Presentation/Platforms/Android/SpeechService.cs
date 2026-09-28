using Android.Content;
using Android.Speech.Tts;
using AndroidTts = Android.Speech.Tts.TextToSpeech;

namespace Burdock.Presentation.Services;

public sealed partial class SpeechService
{
    private AndroidTts? _engine;
    private Task<bool>? _engineReady;

    private async partial Task PlatformSpeakAsync(string text, string country, float rate, CancellationToken cancellationToken)
    {
        var engine = await EnsureEngineOrNullAsync().ConfigureAwait(true);
        if (engine is null || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (!string.Equals(_voiceResolvedFor, country, StringComparison.Ordinal))
        {
            var voice = PickVoice(engine, country);
            if (voice is not null)
            {
                engine.SetVoice(voice);
            }
            else
            {
                engine.SetLanguage(Java.Util.Locale.ForLanguageTag($"en-{country}"));
            }

            _voiceResolvedFor = country;
        }

        engine.SetSpeechRate(rate);
        engine.Speak(text, QueueMode.Flush, null, Guid.NewGuid().ToString("N"));
    }

    /// <summary>
    /// Best installed voice for en-{country}: highest quality first, offline before network at equal quality, lower latency
    /// last; voices that still need a download are skipped and network voices are considered only while online.
    /// </summary>
    private static Voice? PickVoice(AndroidTts engine, string country)
    {
        ICollection<Voice>? voices;
        try
        {
            voices = engine.Voices;
        }
        catch (Java.Lang.Exception)
        {
            return null;
        }

        if (voices is null)
        {
            return null;
        }

        var online = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
        return voices
            .Where(v => v.Locale is { } locale
                        && string.Equals(locale.Language, "en", StringComparison.OrdinalIgnoreCase)
                        && string.Equals(locale.Country, country, StringComparison.OrdinalIgnoreCase))
            .Where(v => v.Features?.Contains(AndroidTts.Engine.KeyFeatureNotInstalled) != true)
            .Where(v => !v.IsNetworkConnectionRequired || online)
            .OrderByDescending(v => (int)v.Quality)
            .ThenBy(v => v.IsNetworkConnectionRequired)
            .ThenBy(v => (int)v.Latency)
            .FirstOrDefault();
    }

    private Task<bool> EnsureEngineAsync()
    {
        if (_engineReady is not null)
        {
            return _engineReady;
        }

        var ready = new TaskCompletionSource<bool>();
        _engine = new AndroidTts(Android.App.Application.Context, new InitListener(ready));
        _engineReady = ready.Task;
        return _engineReady;
    }

    private async Task<AndroidTts?> EnsureEngineOrNullAsync()
        => await EnsureEngineAsync().ConfigureAwait(true) ? _engine : null;

    private static partial bool PlatformSupportsVoiceSettings() => true;

    /// <summary>Opens the system text-to-speech settings where better voices can be downloaded.</summary>
    private static partial Task PlatformOpenVoiceSettingsAsync()
    {
        try
        {
            var intent = new Intent("com.android.settings.TTS_SETTINGS").AddFlags(ActivityFlags.NewTask);
            Android.App.Application.Context.StartActivity(intent);
        }
        catch (ActivityNotFoundException)
        {
            // No settings screen on this device; the hint text still explains what to do.
        }

        return Task.CompletedTask;
    }

    private sealed class InitListener(TaskCompletionSource<bool> ready) : Java.Lang.Object, AndroidTts.IOnInitListener
    {
        public void OnInit(OperationResult status) => ready.TrySetResult(status == OperationResult.Success);
    }
}
