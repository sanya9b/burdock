using System.Globalization;
using Burdock.Domain.Rewards;
using Burdock.Presentation.Controls;
using Burdock.Presentation.Localization;
using Burdock.Presentation.Navigation;
using Burdock.Presentation.Resources.Strings;
using Burdock.Presentation.ViewModels;

namespace Burdock.Presentation.Tests.ViewModels;

/// <summary>S-12d «Підсумок» (spec 03, spec 07 §3.8): numbers first, rewards settled once, celebrations queued in order.</summary>
public sealed class SessionSummaryViewModelTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Numbers_drive_the_accuracy_texts_and_the_learning_day()
    {
        var harness = new ViewModelHarness();

        var good = Open(harness, graded: 10, correct: 8);
        var weak = Open(harness, graded: 3, correct: 1);

        good.Accuracy.ShouldBe(0.8, 1e-9);
        good.AccuracyPercentText.ShouldBe("80%");
        good.GradedText.ShouldContain("10");
        good.CorrectText.ShouldContain("8");
        good.LearningDayCounted.ShouldBeTrue();
        good.LearningDayText.ShouldBe(AppResources.Summary_LearningDay_Counted);
        good.Mood.ShouldBe(MascotMood.Party);

        weak.LearningDayCounted.ShouldBeFalse();
        weak.LearningDayText.ShouldContain("2"); // two more graded cards to reach five
        weak.Mood.ShouldBe(MascotMood.Cheer);
    }

    [Fact]
    public async Task Load_settles_the_rewards_once_and_walks_through_the_celebration_queue()
    {
        var harness = new ViewModelHarness();
        var vm = Open(harness, graded: 10, correct: 10);

        await vm.LoadAsync(Ct);

        vm.ShowRewards.ShouldBeTrue();
        vm.XpGainedText.ShouldNotBeNullOrWhiteSpace();
        vm.ThornsBefore.ShouldBe(0);
        vm.ThornsTotal.ShouldBeGreaterThan(0);
        vm.StreakLength.ShouldBe(1);
        vm.LevelText.ShouldNotBeNullOrWhiteSpace();
        vm.MascotReady.ShouldBeTrue();
        vm.MascotStage.ShouldBe(GrowthStage.Seed);
        vm.MascotProgress.ShouldBe(0);
        vm.MascotDescription.ShouldBe(RewardsText.GrowthDescription(GrowthStage.Seed));
        vm.GrowthAfter.ShouldNotBeNull();
        vm.GrowthAfter.Xp.ShouldBe(harness.Rewards.State!.Xp);
        vm.FeedSeeds.ShouldBeInRange(3, 12);
        vm.Celebrations.Count.ShouldBeGreaterThanOrEqualTo(1);
        vm.CurrentCelebration.ShouldNotBeNull();

        var events = harness.Rewards.Events.Count;
        await vm.LoadAsync(Ct);
        harness.Rewards.Events.Count.ShouldBe(events);
        harness.Sync.Reasons.ShouldBe(["session"]);

        CelebrationItem? last = null;
        var shown = 0;
        while (vm.CurrentCelebration is { } current)
        {
            last = current;
            shown++;
            vm.DismissCelebrationCommand.Execute(null);
        }

        shown.ShouldBe(vm.Celebrations.Count);
        last!.Button.ShouldBe(AppResources.Celebration_Button_Last);
    }

    [Fact]
    public async Task With_gamification_off_the_rewards_stay_hidden_but_are_still_recorded()
    {
        var harness = new ViewModelHarness(ViewModelHarness.DefaultSettings with { GamificationEnabled = false });
        var vm = Open(harness, graded: 10, correct: 10);

        await vm.LoadAsync(Ct);

        vm.ShowRewards.ShouldBeFalse();
        vm.CurrentCelebration.ShouldBeNull();
        harness.Rewards.State.ShouldNotBeNull();
        vm.MascotReady.ShouldBeTrue();
        vm.MascotStage.ShouldBe(GrowthRules.Adult.Stage);
        vm.GrowthAfter.ShouldBeNull();
        vm.MascotDescription.ShouldBe(AppResources.Growth_A11y_Plain);
    }

    [Fact]
    public async Task Crossing_a_growth_stage_turns_the_level_up_card_into_a_growth_celebration()
    {
        var harness = new ViewModelHarness();
        await harness.Rewards.SaveAsync(ViewModelHarness.Learner, RewardsState.Empty with { Xp = 95 }, [], Ct); // five XP short of «Паросток»
        var vm = Open(harness, graded: 10, correct: 10);

        await vm.LoadAsync(Ct);

        vm.MascotStage.ShouldBe(GrowthStage.Seed);
        vm.GrowthAfter!.Stage.ShouldBe(GrowthStage.Sprout);
        vm.Celebrations.ShouldContain(c => c.Eyebrow == AppResources.Celebration_Growth_Eyebrow && c.Title == RewardsText.StageName(GrowthStage.Sprout));
        vm.Celebrations.ShouldNotContain(c => c.Eyebrow == AppResources.Celebration_LevelUp_Eyebrow);
    }

    [Fact]
    public async Task Deferred_celebrations_wait_for_the_page_to_finish_feeding_the_burr()
    {
        var harness = new ViewModelHarness();
        var vm = Open(harness, graded: 10, correct: 10);
        vm.DeferCelebrations = true;

        await vm.LoadAsync(Ct);

        vm.ShowRewards.ShouldBeTrue();
        vm.Celebrations.Count.ShouldBeGreaterThanOrEqualTo(1);
        vm.CurrentCelebration.ShouldBeNull();

        vm.BeginCelebrations();

        vm.CurrentCelebration.ShouldNotBeNull();
        var first = vm.CurrentCelebration;
        vm.BeginCelebrations(); // idempotent: a second call does not skip the card being shown
        vm.CurrentCelebration.ShouldBe(first);
    }

    [Fact]
    public async Task Without_a_session_the_mascot_is_still_shown()
    {
        var harness = new ViewModelHarness();
        var vm = Open(harness, graded: 3, correct: 3, withSession: false);

        await vm.LoadAsync(Ct);

        vm.MascotReady.ShouldBeTrue();
        vm.GrowthAfter.ShouldBeNull();
    }

    [Fact]
    public async Task Without_a_session_id_nothing_is_settled()
    {
        var harness = new ViewModelHarness();
        var vm = Open(harness, graded: 10, correct: 10, withSession: false);

        await vm.LoadAsync(Ct);

        vm.ShowRewards.ShouldBeFalse();
        harness.Rewards.State.ShouldBeNull();
    }

    [Fact]
    public async Task Back_to_today_navigates_to_the_root()
    {
        var harness = new ViewModelHarness();
        var vm = Open(harness, graded: 5, correct: 5);

        await vm.BackToTodayCommand.ExecuteAsync(null);

        harness.Navigation.Last.Route.ShouldBe(Routes.Today.Root);
    }

    private static SessionSummaryViewModel Open(ViewModelHarness harness, int graded, int correct, bool withSession = true)
    {
        var vm = harness.Resolve<SessionSummaryViewModel>();
        var query = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["graded"] = graded.ToString(CultureInfo.InvariantCulture),
            ["correct"] = correct.ToString(CultureInfo.InvariantCulture),
            ["again"] = "0",
            ["typed"] = "0",
            ["learned"] = "0",
            ["warmup"] = "1",
            ["seconds"] = "120",
        };
        if (withSession)
        {
            query["session"] = Guid.NewGuid().ToString("D");
        }

        vm.ApplyQueryAttributes(query);
        return vm;
    }
}
