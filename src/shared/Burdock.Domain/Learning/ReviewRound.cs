using Burdock.Domain.SharedKernel;

namespace Burdock.Domain.Learning;

/// <summary>A planned «Повторимо» round: how many learned words are due and the packs of five the round asks now.</summary>
public sealed record ReviewRoundPlan(int DueCount, IReadOnlyList<IReadOnlyList<LexemeId>> Packs)
{
    public static ReviewRoundPlan Empty { get; } = new(0, []);

    public int WordCount => Packs.Sum(p => p.Count);

    /// <summary>At least one full pack: the badge on «Сьогодні» appears only then.</summary>
    public bool IsAvailable => Packs.Count > 0;
}

/// <summary>
/// Repetition of learned words (owner decision of 2026-11-12, log item 24; spec 06 §3.7.1). A word is due when FSRS says
/// so; due words are asked as pairs word ↔ meaning in packs of <see cref="PackSize"/>, at most <see cref="MaxPacks"/>
/// packs per round, weakest first; only full packs are asked, the rest waits for the next round. A correct pair keeps the
/// word learned and moves its next review further; a mistake sends it back to «Вчу» with an empty knowledge ring.
/// </summary>
public static class ReviewRoundRules
{
    public const int PackSize = 5;
    public const int MaxPacks = 3;
    public const int MaxWords = PackSize * MaxPacks;

    /// <summary>Learned words whose review time has come; «Знаю» words are never asked, they were vouched for.</summary>
    public static bool IsDue(LearnerCard card, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(card);
        return card.State == CardState.Learned && card.Due is { } due && due <= now;
    }

    public static ReviewRoundPlan Plan(IEnumerable<LearnerCard> cards, Func<LearnerCard, double> retrievability, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(retrievability);
        var due = cards
            .Where(c => IsDue(c, now))
            .Select(c => (Card: c, Retrievability: retrievability(c)))
            .OrderBy(x => x.Retrievability)
            .ThenBy(x => x.Card.Due)
            .ThenBy(x => x.Card.LexemeId.Value)
            .Select(x => x.Card.LexemeId)
            .ToArray();
        var packCount = Math.Min(MaxPacks, due.Length / PackSize);
        var packs = Enumerable.Range(0, packCount)
            .Select(i => (IReadOnlyList<LexemeId>)due.Skip(i * PackSize).Take(PackSize).ToArray())
            .ToArray();
        return new ReviewRoundPlan(due.Length, packs);
    }
}
