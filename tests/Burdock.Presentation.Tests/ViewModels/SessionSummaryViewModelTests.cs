using System.Globalization;
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
        good.Mascot.ShouldBe("mascot_party.png");

        weak.LearningDayCounted.ShouldBeFalse();
        weak.LearningDayText.ShouldContain("2"); // two more graded cards to reach five
        weak.Mascot.ShouldBe("mascot_cheer.png");
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
        vm.Celebrations.Count.ShouldBeGreaterThanOrEqualTo(1);
        vm.CurrentCelebration.ShouldNotBeNull();

        var events = harness.Rewards.Events.Count;
        await vm.LoadAsync(Ct);
        harness.Rewards.Events.Count.ShouldBe(events);

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
