using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;

namespace Burdock.TestKit;

/// <summary>Fluent builder for <see cref="Lexeme"/> with sensible defaults (spec 15 §3.9 test data builders).</summary>
public sealed class LexemeBuilder
{
    private LexemeId _id = LexemeId.From(Guid.Parse("0199a4c0-0000-7000-8000-000000000001"));
    private LanguagePair _pair = LanguagePair.UkrainianToEnglish;
    private string _lemma = "receipt";
    private PartOfSpeech _partOfSpeech = PartOfSpeech.Noun;
    private CefrLevel _level = CefrLevel.A2;
    private readonly List<TopicId> _topics = [TopicId.From("shopping")];
    private int? _frequencyBand;

    public static LexemeBuilder ALexeme() => new();

    public LexemeBuilder WithId(Guid id)
    {
        _id = LexemeId.From(id);
        return this;
    }

    public LexemeBuilder WithLemma(string lemma)
    {
        _lemma = lemma;
        return this;
    }

    public LexemeBuilder WithPartOfSpeech(PartOfSpeech partOfSpeech)
    {
        _partOfSpeech = partOfSpeech;
        return this;
    }

    public LexemeBuilder AtLevel(CefrLevel level)
    {
        _level = level;
        return this;
    }

    public LexemeBuilder InTopics(params string[] slugs)
    {
        _topics.Clear();
        _topics.AddRange(slugs.Select(TopicId.From));
        return this;
    }

    public LexemeBuilder WithFrequencyBand(int? band)
    {
        _frequencyBand = band;
        return this;
    }

    public LexemeBuilder ForPair(LanguagePair pair)
    {
        _pair = pair;
        return this;
    }

    public Lexeme Build() => new(_id, _pair, _lemma, _partOfSpeech, _level, _topics.ToArray(), _frequencyBand);
}
