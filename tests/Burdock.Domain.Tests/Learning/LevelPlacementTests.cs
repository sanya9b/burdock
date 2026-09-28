using Burdock.Domain.Learning;
using Burdock.Domain.Vocabulary;

namespace Burdock.Domain.Tests.Learning;

/// <summary>S-03a placement mini-test rules (spec 03, Onboarding.QuizQuestions = 8).</summary>
public sealed class LevelPlacementTests
{
    [Fact]
    public void Pick_spreads_the_slots_over_the_levels_from_the_lowest_and_orders_easy_to_hard()
    {
        var pool = Pool((CefrLevel.A1, 5), (CefrLevel.A2, 5), (CefrLevel.B1, 5));

        var picked = LevelPlacement.Pick(pool, w => w.Level, seed: 42);

        picked.Count.ShouldBe(8);
        picked.Select(w => w.Level).ShouldBe([CefrLevel.A1, CefrLevel.A1, CefrLevel.A1, CefrLevel.A2, CefrLevel.A2, CefrLevel.A2, CefrLevel.B1, CefrLevel.B1]);
        picked.Distinct().Count().ShouldBe(8);
    }

    [Fact]
    public void Pick_with_all_six_levels_gives_two_to_the_lowest_two_and_one_to_the_rest()
    {
        var pool = Pool((CefrLevel.A1, 3), (CefrLevel.A2, 3), (CefrLevel.B1, 3), (CefrLevel.B2, 3), (CefrLevel.C1, 3), (CefrLevel.C2, 3));

        var picked = LevelPlacement.Pick(pool, w => w.Level, seed: 1);

        picked.Select(w => w.Level).ShouldBe([CefrLevel.A1, CefrLevel.A1, CefrLevel.A2, CefrLevel.A2, CefrLevel.B1, CefrLevel.B2, CefrLevel.C1, CefrLevel.C2]);
    }

    [Fact]
    public void Pick_is_deterministic_for_a_seed_and_shuffles_within_a_level()
    {
        var pool = Pool((CefrLevel.A1, 40));

        var first = LevelPlacement.Pick(pool, w => w.Level, seed: 7);
        var again = LevelPlacement.Pick(pool, w => w.Level, seed: 7);
        var other = LevelPlacement.Pick(pool, w => w.Level, seed: 8);

        first.ShouldBe(again);
        first.ShouldNotBe(pool.Take(8).ToList());
        other.ShouldNotBe(first);
    }

    [Fact]
    public void Pick_returns_what_a_small_pool_has_and_nothing_for_an_empty_one()
    {
        LevelPlacement.Pick(Pool((CefrLevel.A1, 2), (CefrLevel.B2, 1)), w => w.Level, seed: 3).Select(w => w.Level).ShouldBe([CefrLevel.A1, CefrLevel.A1, CefrLevel.B2]);
        LevelPlacement.Pick(Pool(), w => w.Level, seed: 3).ShouldBeEmpty();
        LevelPlacement.Pick(Pool((CefrLevel.A1, 5)), w => w.Level, seed: 3, count: 0).ShouldBeEmpty();
    }

    [Fact]
    public void Estimate_walks_up_while_at_least_half_of_a_level_is_known()
    {
        // A1 2/2, A2 1/2 (half counts), B1 0/2 → B1 stops the walk
        Estimate("A1+ A1+ A2+ A2- B1- B1-").ShouldBe(CefrLevel.A2);
        // A1 2/2, A2 0/2, B1 2/2 → the gap at A2 stops the walk even though B1 was known
        Estimate("A1+ A1+ A2- A2- B1+ B1+").ShouldBe(CefrLevel.A1);
        // A1 2/3 known
        Estimate("A1+ A1+ A1- A2- A2- A2-").ShouldBe(CefrLevel.A1);
    }

    [Fact]
    public void Estimate_suggests_one_level_above_the_top_when_everything_up_there_was_known()
    {
        Estimate("A1+ A1+ A2+ A2+ B1+ B1+").ShouldBe(CefrLevel.B2);
        Estimate("A1+ A2+ B1+ B2+ C1+ C2+").ShouldBe(CefrLevel.C2);
        Estimate("A1+ A1+ A2+ A2+ B1+ B1-").ShouldBe(CefrLevel.B1);
    }

    [Fact]
    public void Estimate_falls_back_to_A1_without_answers_or_without_a_reached_level()
    {
        LevelPlacement.Estimate([]).ShouldBe(CefrLevel.A1);
        Estimate("A1- A1- A2- A2-").ShouldBe(CefrLevel.A1);
        Estimate("A1- A2+ A2+").ShouldBe(CefrLevel.A1);
    }

    private static CefrLevel Estimate(string script)
        => LevelPlacement.Estimate(script.Split(' ').Select(t => new PlacementAnswer(Enum.Parse<CefrLevel>(t[..2]), t[2] == '+')).ToList());

    private static List<Word> Pool(params (CefrLevel Level, int Count)[] levels)
        => levels.SelectMany(l => Enumerable.Range(1, l.Count).Select(n => new Word($"{l.Level}-{n}", l.Level))).ToList();

    private sealed record Word(string Lemma, CefrLevel Level);
}
