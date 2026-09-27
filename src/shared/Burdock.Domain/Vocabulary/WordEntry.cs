using Burdock.Domain.SharedKernel;

namespace Burdock.Domain.Vocabulary;

/// <summary>
/// The learner-facing projection of a lexeme: what the widget, WordCard and exercises need (spec 08 §3.3 flattened
/// to the primary sense). Text fields are already in display form (apostrophe U+2019 in Ukrainian).
/// </summary>
public sealed class WordEntry
{
    public WordEntry(
        Lexeme lexeme,
        string translation,
        IReadOnlyList<string> alternativeTranslations,
        IReadOnlyList<string> acceptedForms,
        string? ipaGeneralAmerican,
        string? ipaReceivedPronunciation,
        string? exampleTarget,
        string? exampleNative)
    {
        ArgumentNullException.ThrowIfNull(lexeme);
        ArgumentException.ThrowIfNullOrWhiteSpace(translation);
        ArgumentNullException.ThrowIfNull(alternativeTranslations);
        ArgumentNullException.ThrowIfNull(acceptedForms);

        Lexeme = lexeme;
        Translation = translation.Trim();
        AlternativeTranslations = alternativeTranslations;
        AcceptedForms = acceptedForms.Count > 0 ? acceptedForms : [lexeme.Lemma];
        IpaGeneralAmerican = ipaGeneralAmerican;
        IpaReceivedPronunciation = ipaReceivedPronunciation;
        ExampleTarget = exampleTarget;
        ExampleNative = exampleNative;
    }

    public Lexeme Lexeme { get; }

    public LexemeId Id => Lexeme.Id;

    /// <summary>Primary Ukrainian meaning shown on surfaces.</summary>
    public string Translation { get; }

    /// <summary>Further meanings shown on the WordCard only.</summary>
    public IReadOnlyList<string> AlternativeTranslations { get; }

    /// <summary>Spellings accepted in typed recall (the lemma plus variants such as "colour").</summary>
    public IReadOnlyList<string> AcceptedForms { get; }

    public string? IpaGeneralAmerican { get; }

    public string? IpaReceivedPronunciation { get; }

    public string? ExampleTarget { get; }

    public string? ExampleNative { get; }
}
