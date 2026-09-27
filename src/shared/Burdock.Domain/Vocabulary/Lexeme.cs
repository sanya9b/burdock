using Burdock.Domain.SharedKernel;

namespace Burdock.Domain.Vocabulary;

/// <summary>Part of speech of a lexeme; values follow the content schema (spec 08 §3.3).</summary>
public enum PartOfSpeech
{
    Noun,
    Verb,
    Adjective,
    Adverb,
    Pronoun,
    Preposition,
    Conjunction,
    Determiner,
    Interjection,
    Numeral,
    Phrase,
}

/// <summary>
/// A headword of the target language with its level and topics. Translations, examples and pronunciation
/// are attached by sense in the content model; the learning engine only needs this projection.
/// </summary>
public sealed class Lexeme
{
    public Lexeme(LexemeId id, LanguagePair pair, string lemma, PartOfSpeech partOfSpeech, CefrLevel level, IReadOnlyList<TopicId> topics, int? frequencyBand = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lemma);
        ArgumentNullException.ThrowIfNull(topics);
        if (frequencyBand is < 1 or > 6)
        {
            throw new ArgumentOutOfRangeException(nameof(frequencyBand), frequencyBand, "Frequency band is 1-6 or null (decision log open question 10).");
        }

        Id = id;
        Pair = pair;
        Lemma = lemma.Trim();
        PartOfSpeech = partOfSpeech;
        Level = level;
        Topics = topics;
        FrequencyBand = frequencyBand;
    }

    public LexemeId Id { get; }

    public LanguagePair Pair { get; }

    public string Lemma { get; }

    public PartOfSpeech PartOfSpeech { get; }

    public CefrLevel Level { get; }

    public IReadOnlyList<TopicId> Topics { get; }

    /// <summary>Coarse frequency band 1 (most frequent) to 6, or null until a licensed source is chosen.</summary>
    public int? FrequencyBand { get; }

    public bool BelongsTo(TopicId topic) => Topics.Contains(topic);
}
