using Burdock.Domain.Learning;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Time;
using Burdock.Domain.Vocabulary;
using Burdock.TestKit;

namespace Burdock.Domain.Tests.Learning;

public sealed class DailyPlannerTests
{
    private static readonly LearnerId Learner = LearnerId.From(Guid.Parse("0199a4c0-0000-7000-8000-00000000aaaa"));
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 10, 0, 0, TimeSpan.Zero);
    private static readonly LearningDay Today = LearningDay.From(Now, TestTimeZones.KyivFixed);

    private static WordEntry Entry(int n) => new(
        LexemeBuilder.ALexeme().WithId(Guid.Parse($"0199a4c0-0000-7000-8000-0000000000{n:x2}")).WithLemma($"word{n}").Build(),
        $"слово{n}", [], [], null, null, null, null);

    private static LearnerSettings Settings(int newPerDay) => LearnerSettings.Moderate(LanguagePair.UkrainianToEnglish, CefrLevel.A1, []) with { NewWordsPerDay = newPerDay };

    [Fact]
    public void New_word_selection_is_deterministic_for_the_same_learner_and_day()
    {
        var candidates = Enumerable.Range(1, 40).Select(Entry).ToArray();
        var cards = new Dictionary<LexemeId, LearnerCard>();

        var first = DailyPlanner.Plan(Learner, Today, Now, TestTimeZones.KyivFixed, candidates, cards, Settings(10), new FixedScheduler(0.5));
        var second = DailyPlanner.Plan(Learner, Today, Now.AddHours(3), TestTimeZones.KyivFixed, candidates, cards, Settings(10), new FixedScheduler(0.5));

        first.NewCount.ShouldBe(10);
        first.Words.Select(w => w.Entry.Id).ShouldBe(second.Words.Select(w => w.Entry.Id));

        var tomorrow = DailyPlanner.Plan(Learner, Today.Next(), Now.AddDays(1), TestTimeZones.KyivFixed, candidates, cards, Settings(10), new FixedScheduler(0.5));
        tomorrow.Words.Select(w => w.Entry.Id).ShouldNotBe(first.Words.Select(w => w.Entry.Id));
    }

    [Fact]
    public void New_words_come_from_the_chosen_level_before_its_neighbours()
    {
        var chosen = Enumerable.Range(1, 6).Select(n => EntryAt(n, CefrLevel.A1)).ToArray();
        var neighbours = Enumerable.Range(7, 6).Select(n => EntryAt(n, CefrLevel.A2)).ToArray();
        var candidates = neighbours.Concat(chosen).ToArray();
        var cards = new Dictionary<LexemeId, LearnerCard>();

        var plan = DailyPlanner.Plan(Learner, Today, Now, TestTimeZones.KyivFixed, candidates, cards, Settings(8), new FixedScheduler(0.5));

        plan.NewCount.ShouldBe(8);
        plan.Words.Take(6).ShouldAllBe(w => w.Entry.Lexeme.Level == CefrLevel.A1);
        plan.Words.Skip(6).ShouldAllBe(w => w.Entry.Lexeme.Level == CefrLevel.A2);

        var order = DailyPlanner.FreshInPlanOrder(Learner, Today, candidates, cards, Settings(8));
        order.Take(6).Select(e => e.Id).ShouldBe(plan.Words.Take(6).Select(w => w.Entry.Id));
        order.Count.ShouldBe(12);
    }

    private static WordEntry EntryAt(int n, CefrLevel level) => new(
        LexemeBuilder.ALexeme().WithId(Guid.Parse($"0199a4c0-0000-7000-8000-0000000000{n:x2}")).WithLemma($"word{n}").AtLevel(level).Build(),
        $"слово{n}", [], [], null, null, null, null);

    [Fact]
    public void Due_cards_come_first_ordered_by_retrievability_then_learning_then_new()
    {
        var candidates = Enumerable.Range(1, 6).Select(Entry).ToArray();
        var cards = new Dictionary<LexemeId, LearnerCard>();
        var scheduler = new FixedScheduler(0.5);

        var dueWeak = LearnerCard.Restore(CardId.From(Guid.NewGuid()), Learner, candidates[0].Id, Now.AddDays(-10), CardState.Review, MemoryState.Create(5, 5), null,
            Now.AddDays(-1), Now.AddDays(-6), Now.AddDays(-10), null, 3, 0, 0, 1, 1, [], []);
        var learning = LearnerCard.Restore(CardId.From(Guid.NewGuid()), Learner, candidates[1].Id, Now.AddDays(-1), CardState.Learning, MemoryState.Create(1, 5), 0,
            Now.AddHours(2), Now.AddHours(-1), Now.AddDays(-1), null, 1, 0, 0, 1, 0, [], []);
        var known = LearnerCard.Restore(CardId.From(Guid.NewGuid()), Learner, candidates[2].Id, Now.AddDays(-1), CardState.Known, null, null,
            null, null, Now.AddDays(-1), null, 0, 0, 0, 1, 0, [], []);
        cards[dueWeak.LexemeId] = dueWeak;
        cards[learning.LexemeId] = learning;
        cards[known.LexemeId] = known;

        var plan = DailyPlanner.Plan(Learner, Today, Now, TestTimeZones.KyivFixed, candidates, cards, Settings(2), scheduler);

        plan.Words[0].Reason.ShouldBe(PlanReason.Due);
        plan.Words[0].Entry.Id.ShouldBe(candidates[0].Id);
        plan.Words[1].Reason.ShouldBe(PlanReason.Learning);
        plan.Words.Skip(2).ShouldAllBe(w => w.Reason == PlanReason.New);
        plan.NewCount.ShouldBe(2);
        plan.Words.ShouldNotContain(w => w.Entry.Id == known.LexemeId);
    }

    [Fact]
    public void Words_introduced_today_reduce_the_new_word_budget()
    {
        var candidates = Enumerable.Range(1, 10).Select(Entry).ToArray();
        var introducedToday = LearnerCard.CreateNew(CardId.From(Guid.NewGuid()), Learner, candidates[0].Id, Now);
        introducedToday.Introduce(Now.AddHours(-1));
        var cards = new Dictionary<LexemeId, LearnerCard> { [introducedToday.LexemeId] = introducedToday };

        var plan = DailyPlanner.Plan(Learner, Today, Now, TestTimeZones.KyivFixed, candidates, cards, Settings(3), new FixedScheduler(0.5));

        plan.NewCount.ShouldBe(2);
        plan.Words.Count(w => w.Reason == PlanReason.Learning).ShouldBe(1);
    }

    private sealed class FixedScheduler(double retrievability) : IReviewScheduler
    {
        public SchedulingOutcome Review(SchedulingInput input, Rating rating, DateTimeOffset reviewedAt)
            => new(CardState.Review, MemoryState.Create(10, 5), null, reviewedAt.AddDays(10));

        public double Retrievability(SchedulingInput input, DateTimeOffset at) => retrievability;
    }
}
