using Burdock.Presentation.ViewModels;

namespace Burdock.Presentation.Services;

/// <summary>Text-to-speech through the platform engine; picks an English locale when one is installed.</summary>
public sealed class MauiSpeechService : ISpeechService
{
    private Locale? _english;
    private bool _localesResolved;

    public async Task SpeakEnglishAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (!_localesResolved)
        {
            var locales = await TextToSpeech.Default.GetLocalesAsync().ConfigureAwait(true);
            _english = locales.FirstOrDefault(l => string.Equals(l.Language, "en", StringComparison.OrdinalIgnoreCase) && string.Equals(l.Country, "US", StringComparison.OrdinalIgnoreCase))
                       ?? locales.FirstOrDefault(l => string.Equals(l.Language, "en", StringComparison.OrdinalIgnoreCase));
            _localesResolved = true;
        }

        await TextToSpeech.Default.SpeakAsync(text, new SpeechOptions { Locale = _english, Pitch = 1.0f, Volume = 1.0f }, cancellationToken).ConfigureAwait(true);
    }
}
