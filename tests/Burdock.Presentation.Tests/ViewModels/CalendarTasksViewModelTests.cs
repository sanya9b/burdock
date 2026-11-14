using Burdock.Domain.Rewards;
using Burdock.Presentation.Localization;
using Burdock.Presentation.Resources.Strings;
using Burdock.Presentation.ViewModels;

namespace Burdock.Presentation.Tests.ViewModels;

/// <summary>Login calendar and micro-tasks on «Сьогодні» and «Нагороди» (spec 07 §3.4, §3.5; spec 03 S-10, S-16).</summary>
public sealed class CalendarTasksViewModelTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Today_claims_the_calendar_once_toasts_and_lists_three_tasks()
    {
        var harness = new ViewModelHarness();
        var vm = harness.Resolve<TodayViewModel>();

        await vm.LoadAsync(Ct);

        var state = harness.Rewards.State!;
        state.Calendar.ClaimedDays.ShouldBe(1);
        state.Calendar.Position.ShouldBe(1);
        state.Thorns.ShouldBe(5);
        harness.Rewards.Events.ShouldContain(e => e.Type == LedgerEventType.CalendarClaim && e.Amount == 5);
        harness.Toast.Messages.ShouldContain(m => m.Contains(RewardsText.ThornsPlus(5), StringComparison.Ordinal) && m.Contains("1 з 7", StringComparison.Ordinal));
        vm.ShowTasks.ShouldBeTrue();
        vm.Tasks.Count.ShouldBe(3);
        vm.Tasks.ShouldAllBe(t => t.Kind == TaskKind.Daily && !t.Done && t.Progress == 0);
        state.Tasks.Daily.Count.ShouldBe(3);

        // A second load the same day: nothing claimed again, the same tasks.
        var toasts = harness.Toast.Messages.Count;
        await vm.LoadAsync(Ct);
        harness.Rewards.State!.Calendar.ClaimedDays.ShouldBe(1);
        harness.Toast.Messages.Count.ShouldBe(toasts);
        vm.Tasks.Select(t => t.Id).ShouldBe(state.Tasks.Daily);

        // Next day: position 2 pays 5 more and the selection changes.
        harness.Clock.Advance(TimeSpan.FromDays(1));
        await vm.LoadAsync(Ct);
        harness.Rewards.State!.Calendar.ClaimedDays.ShouldBe(2);
        harness.Rewards.State.Thorns.ShouldBe(10);
        harness.Rewards.State.Calendar.Position.ShouldBe(2);
        harness.Rewards.State.Tasks.Daily.ShouldNotBe(state.Tasks.Daily);
    }

    [Fact]
    public async Task Without_gamification_the_calendar_still_moves_but_nothing_is_shown()
    {
        var harness = new ViewModelHarness(ViewModelHarness.DefaultSettings with { GamificationEnabled = false });
        var vm = harness.Resolve<TodayViewModel>();

        await vm.LoadAsync(Ct);

        harness.Rewards.State!.Calendar.ClaimedDays.ShouldBe(1);
        harness.Toast.Messages.ShouldBeEmpty();
        vm.ShowTasks.ShouldBeFalse();
    }

    [Fact]
    public async Task Rewards_shows_the_calendar_track_the_weekly_task_and_first_steps()
    {
        var harness = new ViewModelHarness();
        await harness.Rewards.SaveAsync(ViewModelHarness.Learner, RewardsState.Empty with { Calendar = new CalendarState(4, null, 18) }, [], Ct);
        var vm = harness.Resolve<RewardsViewModel>();

        await vm.LoadAsync(Ct);

        vm.Calendar.Count.ShouldBe(7);
        vm.Calendar.Count(t => t.IsDone).ShouldBe(4);
        vm.CalendarCaption.ShouldBe(string.Format(System.Globalization.CultureInfo.GetCultureInfo("uk-UA"), System.Text.CompositeFormat.Parse(AppResources.Calendar_Caption), 5));
        vm.CalendarTrackText.ShouldBe(RewardsText.CalendarTrack(18, CalendarRules.NextTrack(18)));
        vm.CalendarTrackText.ShouldContain("18 / 30");
        vm.DailyTasks.Count.ShouldBe(3);
        vm.WeeklyTask.ShouldNotBeNull();
        vm.WeeklyTask.IsWeekly.ShouldBeTrue();
        vm.HasOneOffs.ShouldBeTrue();
        vm.OneOffTasks.Select(t => t.Id).ShouldBe(["TASK_ONB_FIRST_SESSION", "TASK_ONB_TYPED", "TASK_ONB_LEARNED"]);
        vm.OneOffTasks[0].ShowProgressBar.ShouldBeFalse();
        vm.DailyTasks[0].RewardText.ShouldBe("+10");
    }
}
