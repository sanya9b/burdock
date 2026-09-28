using Burdock.Domain.Vocabulary;
using Burdock.Presentation.ViewModels;

namespace Burdock.Presentation.Localization;

/// <summary>Which transcription to show for the learner's accent (spec 12 §3.9): RP for British, GA otherwise, falling back to whichever exists.</summary>
internal static class Pronunciation
{
    public static string PreferredIpa(WordEntry entry, PronunciationAccent accent)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return accent == PronunciationAccent.British
            ? entry.IpaReceivedPronunciation ?? entry.IpaGeneralAmerican ?? string.Empty
            : entry.IpaGeneralAmerican ?? entry.IpaReceivedPronunciation ?? string.Empty;
    }
}
