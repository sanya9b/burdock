using Burdock.Domain.Learning;
using Burdock.Domain.SharedKernel;

namespace Burdock.Domain.Tests.Learning;

/// <summary>«Повторимо» (owner decision 2026-11-12, log item 24; spec 06 §3.7.1): due learned words in full packs of five, weakest first.</summary>
public sealed class ReviewRoundRulesTests
{
    private static readonly LearnerId Learner = LearnerId.From(Guid.Parse("0199a4c0-0000-7000-8000-00000000aaaa"));
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Only_full_packs_are_asked_and_the_rest_waits()
    {
        var cards = Enumerable.Range(1, 7).Select(n => Learned(n, dueInDays: -1, stability: n)).ToArray();

        var plan = ReviewRoundRules.Plan(cards, c => c.Memory!.Value.Stability / 10.0, Now);

        plan.DueCount.ShouldBe(7);
        plan.Packs.Count.ShouldBe(1);
        plan.WordCount.ShouldBe(5);
        plan.IsAvailable.ShouldBeTrue();
        plan.Packs[0].ShouldBe(cards.Take(5).Select(c => c.LexemeId).ToArray(), "weakest retrievability first");
    }

    [Fact]
    public void A_round_asks_at_most_three_packs()
    {
        var cards = Enumerable.Range(1, 23).Select(n => Learned(n, dueInDays: -1, stability: n)).ToArray();

        var plan = ReviewRoundRules.Plan(cards, _ => 0.5, Now);

        plan.DueCount.ShouldBe(23);
        plan.Packs.Count.ShouldBe(ReviewRoundRules.MaxPacks);
        plan.WordCount.ShouldBe(ReviewRoundRules.MaxWords);
        plan.Packs.SelectMany(p => p).Distinct().Count().ShouldBe(ReviewRoundRules.MaxWords);
    }

    [Fact]
    public void Fewer_than_five_due_words_make_no_round()
    {
        var cards = Enumerable.Range(1, 4).Select(n => Learned(n, dueInDays: -1, stability: 2)).ToArray();

        var plan = ReviewRoundRules.Plan(cards, _ => 0.5, Now);

        plan.DueCount.ShouldBe(4);
        plan.IsAvailable.ShouldBeFalse();
        plan.Packs.ShouldBeEmpty();
    }

    [Fact]
    public void Only_learned_words_whose_time_has_come_are_due()
    {
        var notYet = Learned(1, dueInDays: 1, stability: 2);
        var due = Learned(2, dueInDays: 0, stability: 2);
        var known = LearnerCard.CreateNew(CardId.From(Guid.NewGuid()), Learner, LexemeId.From(Guid.NewGuid()), Now.AddDays(-2));
        known.ClaimKnown(Now.AddDays(-1));
        var learning = LearnerCard.Restore(CardId.From(Guid.NewGuid()), Learner, LexemeId.From(Guid.NewGuid()), Now.AddDays(-2), CardState.Learning, MemoryState.Create(1, 5), 0,
            Now.AddHours(-1), Now.AddHours(-2), Now.AddDays(-2), null, 1, 0, 0, 0, 0, [], [], 2);

        ReviewRoundRules.IsDue(notYet, Now).ShouldBeFalse();
        ReviewRoundRules.IsDue(due, Now).ShouldBeTrue();
        ReviewRoundRules.IsDue(known, Now).ShouldBeFalse();
        ReviewRoundRules.IsDue(learning, Now).ShouldBeFalse();
        ReviewRoundRules.Plan([notYet, due, known, learning], _ => 0.5, Now).DueCount.ShouldBe(1);
    }

    private static LearnerCard Learned(int n, int dueInDays, double stability)
        => LearnerCard.Restore(CardId.From(Guid.NewGuid()), Learner, LexemeId.From(Guid.Parse($"0199a4c0-0000-7000-8000-0000000000{n:x2}")), Now.AddDays(-10), CardState.Learned,
            MemoryState.Create(stability, 5), null, Now.AddDays(dueInDays), Now.AddDays(-3), Now.AddDays(-10), Now.AddDays(-3), 6, 0, 0, 1, 1, [], [], LearningPolicy.MasteryStreak);
}
