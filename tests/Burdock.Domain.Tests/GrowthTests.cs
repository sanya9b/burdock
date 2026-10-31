using Burdock.Domain.Rewards;
using Burdock.Domain.Time;

namespace Burdock.Domain.Tests;

/// <summary>Mascot growth (spec 07 §3.2.7, FR-07-29..31): a pure function of XP, monotonic, never shrinking, with stage names tied to level titles.</summary>
public sealed class GrowthTests
{
    [Theory]
    [InlineData(1, GrowthStage.Seed)]
    [InlineData(2, GrowthStage.Sprout)]
    [InlineData(3, GrowthStage.Sprout)]
    [InlineData(4, GrowthStage.Leaf)]
    [InlineData(5, GrowthStage.Bush)]
    [InlineData(6, GrowthStage.Bud)]
    [InlineData(7, GrowthStage.Burr)]
    [InlineData(9, GrowthStage.Burr)]
    [InlineData(10, GrowthStage.Bloom)]
    [InlineData(19, GrowthStage.Bloom)]
    [InlineData(20, GrowthStage.GreatBurr)]
    [InlineData(30, GrowthStage.GreatBurr)]
    public void Stages_follow_the_level_bands(int level, GrowthStage expected)
    {
        GrowthRules.StageFor(level).ShouldBe(expected);
        GrowthRules.FirstLevel(expected).ShouldBeLessThanOrEqualTo(level);
    }

    [Fact]
    public void First_levels_are_the_level_table_entries_whose_titles_name_the_stages()
    {
        GrowthRules.FirstLevel(GrowthStage.Seed).ShouldBe(1);
        GrowthRules.FirstLevel(GrowthStage.Sprout).ShouldBe(2);
        GrowthRules.FirstLevel(GrowthStage.Leaf).ShouldBe(4);
        GrowthRules.FirstLevel(GrowthStage.Bush).ShouldBe(5);
        GrowthRules.FirstLevel(GrowthStage.Bud).ShouldBe(6);
        GrowthRules.FirstLevel(GrowthStage.Burr).ShouldBe(7);
        GrowthRules.FirstLevel(GrowthStage.Bloom).ShouldBe(10);
        GrowthRules.FirstLevel(GrowthStage.GreatBurr).ShouldBe(20);
        GrowthRules.Next(GrowthStage.Seed).ShouldBe(GrowthStage.Sprout);
        GrowthRules.Next(GrowthStage.GreatBurr).ShouldBeNull();
    }

    [Fact]
    public void Progress_starts_at_zero_on_the_first_level_of_a_stage_and_counts_xp_to_the_next_stage()
    {
        var seed = GrowthRules.For(0);
        seed.Stage.ShouldBe(GrowthStage.Seed);
        seed.Progress.ShouldBe(0);
        seed.NextStage.ShouldBe(GrowthStage.Sprout);
        seed.XpToNextStage.ShouldBe(100);

        var halfSeed = GrowthRules.For(50);
        halfSeed.Progress.ShouldBe(0.5, 1e-9);
        halfSeed.XpToNextStage.ShouldBe(50);

        var bush = GrowthRules.For(XpRules.Threshold(5));
        bush.Stage.ShouldBe(GrowthStage.Bush);
        bush.Progress.ShouldBe(0);
        bush.XpToNextStage.ShouldBe(XpRules.Threshold(6) - XpRules.Threshold(5));

        var lastXpOfBush = GrowthRules.For(XpRules.Threshold(6) - 1);
        lastXpOfBush.Stage.ShouldBe(GrowthStage.Bush);
        lastXpOfBush.Progress.ShouldBeLessThan(1);
        lastXpOfBush.XpToNextStage.ShouldBe(1);
    }

    [Fact]
    public void The_last_stage_saturates_at_level_30_and_has_no_next_stage()
    {
        var fresh = GrowthRules.For(XpRules.Threshold(20));
        fresh.Stage.ShouldBe(GrowthStage.GreatBurr);
        fresh.Progress.ShouldBe(0);
        fresh.IsFinal.ShouldBeTrue();
        fresh.XpToNextStage.ShouldBe(0);

        var grown = GrowthRules.For(XpRules.Threshold(30));
        grown.Progress.ShouldBe(1);
        grown.Scalar.ShouldBe(1, 1e-9);

        var beyond = GrowthRules.For(1_000_000);
        beyond.Progress.ShouldBe(1);
        beyond.Scalar.ShouldBe(1, 1e-9);
    }

    [Fact]
    public void Growth_never_goes_backwards_as_xp_increases()
    {
        var previous = GrowthRules.For(0);
        for (var xp = 1; xp <= XpRules.Threshold(30) + 500; xp += 7)
        {
            var current = GrowthRules.For(xp);
            ((int)current.Stage).ShouldBeGreaterThanOrEqualTo((int)previous.Stage);
            current.Scalar.ShouldBeGreaterThanOrEqualTo(previous.Scalar - 1e-12);
            current.Scalar.ShouldBeInRange(0, 1);
            previous = current;
        }
    }

    [Fact]
    public void The_adult_form_is_the_burr_halfway_through_its_stage()
    {
        GrowthRules.Adult.Stage.ShouldBe(GrowthStage.Burr);
        GrowthRules.Adult.Progress.ShouldBe(0.5);
    }

    [Fact]
    public void A_settlement_exposes_growth_before_and_after_and_flags_a_stage_change()
    {
        var day = new LearningDay(new DateOnly(2026, 10, 31));
        var now = new DateTimeOffset(2026, 10, 31, 10, 0, 0, TimeSpan.Zero);
        var ids = 0;
        var state = RewardsState.Empty with { Xp = XpRules.Threshold(6) - 5 }; // five XP short of «Бутончик»
        var report = new SessionReport(Guid.NewGuid(), 10, 10, 0, 0, 0, true, false);

        var settlement = RewardsEngine.Settle(state, report, new ProgressCounters(0, 0, 10), day, now, () => Guid.Parse($"0199a4c0-0000-7000-8000-{++ids:d12}"));

        settlement.GrowthBefore.Stage.ShouldBe(GrowthStage.Bush);
        settlement.GrowthAfter.Stage.ShouldBe(GrowthStage.Bud);
        settlement.StageChanged.ShouldBeTrue();
        settlement.GrowthBefore.Xp.ShouldBe(state.Xp);
        settlement.GrowthAfter.Xp.ShouldBe(state.Xp + settlement.XpGained);
    }
}
