using System.Globalization;
using System.Text;
using Burdock.Domain.Vocabulary;
using Burdock.Presentation.Resources.Strings;

namespace Burdock.Presentation.Localization;

/// <summary>Ukrainian line under a voice name: «Американська · жіночий голос».</summary>
internal static class VoiceText
{
    private static readonly CultureInfo Ukrainian = CultureInfo.GetCultureInfo("uk-UA");
    private static readonly CompositeFormat Line = CompositeFormat.Parse(AppResources.Settings_Voice_Subtitle);

    public static string Describe(PronunciationVoice voice)
    {
        ArgumentNullException.ThrowIfNull(voice);
        var accent = voice.Accent == PronunciationAccent.British ? AppResources.Settings_Voice_Accent_British : AppResources.Settings_Voice_Accent_American;
        var gender = voice.Gender == VoiceGender.Male ? AppResources.Settings_Voice_Gender_Male : AppResources.Settings_Voice_Gender_Female;
        return string.Format(Ukrainian, Line, accent, gender);
    }
}
