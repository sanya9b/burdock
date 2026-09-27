namespace Burdock.Domain.SharedKernel;

/// <summary>Learner identity (server-side account or anonymous installation, see ADR 0006).</summary>
public readonly record struct LearnerId(Guid Value)
{
    public static LearnerId From(Guid value) => value == Guid.Empty
        ? throw new ArgumentException("LearnerId cannot be empty.", nameof(value))
        : new LearnerId(value);

    public override string ToString() => Value.ToString("D");
}

/// <summary>Identity of a lexeme in the content database (spec 08 §3.3).</summary>
public readonly record struct LexemeId(Guid Value)
{
    public static LexemeId From(Guid value) => value == Guid.Empty
        ? throw new ArgumentException("LexemeId cannot be empty.", nameof(value))
        : new LexemeId(value);

    public override string ToString() => Value.ToString("D");
}

/// <summary>Identity of a learner's card for one lexeme (spec 06 §3.1).</summary>
public readonly record struct CardId(Guid Value)
{
    public static CardId From(Guid value) => value == Guid.Empty
        ? throw new ArgumentException("CardId cannot be empty.", nameof(value))
        : new CardId(value);

    public override string ToString() => Value.ToString("D");
}

/// <summary>Identity of a topic (category) such as "travel" or "it" (decision Q18).</summary>
public readonly record struct TopicId(string Slug)
{
    public static TopicId From(string slug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        return new TopicId(slug.Trim().ToLowerInvariant());
    }

    public override string ToString() => Slug;
}
