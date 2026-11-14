using Burdock.Domain.Rewards;
using Burdock.Domain.Time;

namespace Burdock.Domain.Tests;

/// <summary>Login calendar (spec 07 §3.4, FR-07-8, FR-07-9) and micro-tasks (§3.5, FR-07-14, FR-07-15).</summary>
public sealed class CalendarAndTasksTests
{
    private static readonly LearningDay Monday = new(new DateOnly(2026, 11, 9)); // a Monday
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid Learner = Guid.Parse("0199a4c0-0000-7000-8000-00000000aaaa");
    private static int _ids;

    private static Guid NewId() => Guid.Parse($"0199a4c0-0000-7000-8000-{++_ids:d12}");

    [Fact]
    public void Calendar_pays_the_cycle_once_per_day_and_a_missed_day_keeps_the_position()
    {
        var state = RewardsState.Empty;
        var first = RewardsEngine.ClaimCalendar(state, Monday, Now, NewId);
        first.Claimed.ShouldBeTrue();
        first.Claim!.Reward.ShouldBe(5);
        first.Claim.Position.ShouldBe(1);
        first.State.Thorns.ShouldBe(5);
        first.Events.Single().Type.ShouldBe(LedgerEventType.CalendarClaim);
        first.Events.Single().Reason.ShouldBe("economy.calendar.cycle:1");

        RewardsEngine.ClaimCalendar(first.State, Monday, Now, NewId).Claimed.ShouldBeFalse();

        // Skip Tuesday: Wednesday still pays position 2, nothing is lost or reset.
        var third = RewardsEngine.ClaimCalendar(first.State, Monday.AddDays(2), Now, NewId);
        third.Claim!.Reward.ShouldBe(5);
        third.Claim.Position.ShouldBe(2);
        third.State.Calendar.ClaimedDays.ShouldBe(2);

        var state7 = third.State;
        for (var i = 3; i <= 7; i++)
        {
            state7 = RewardsEngine.ClaimCalendar(state7, Monday.AddDays(i), Now, NewId).State;
        }

        state7.Calendar.ClaimedDays.ShouldBe(7);
        state7.Calendar.Position.ShouldBe(0); // wrapped
        state7.Thorns.ShouldBe(5 + 5 + 10 + 10 + 15 + 15 + 30);
        RewardsEngine.ClaimCalendar(state7, Monday.AddDays(8), Now, NewId).Claim!.Reward.ShouldBe(5);
    }

    [Fact]
    public void Calendar_track_unlocks_the_theme_gifts_the_frame_and_pays_bonuses()
    {
        CalendarRules.TrackRewardAt(29).ShouldBeNull();
        CalendarRules.TrackRewardAt(30)!.Kind.ShouldBe(CalendarTrackKind.ThemeMeadow);
        CalendarRules.TrackRewardAt(60)!.Kind.ShouldBe(CalendarTrackKind.FrameSky);
        CalendarRules.TrackRewardAt(90).ShouldBe(new CalendarTrackReward(CalendarTrackKind.Thorns, 100, 90));
        CalendarRules.TrackRewardAt(120).ShouldBe(new CalendarTrackReward(CalendarTrackKind.Thorns, 50, 120));
        CalendarRules.TrackRewardAt(135).ShouldBeNull();
        CalendarRules.NextTrack(18).Day.ShouldBe(30);
        CalendarRules.NextTrack(61).Day.ShouldBe(90);
        CalendarRules.NextTrack(120).Day.ShouldBe(150);

        var state = RewardsState.Empty with { Calendar = new CalendarState(3, null, 59) };
        var sixty = RewardsEngine.ClaimCalendar(state, Monday, Now, NewId);
        sixty.Claim!.Track!.Kind.ShouldBe(CalendarTrackKind.FrameSky);
        sixty.State.Inventory.Owns(Catalog.CalendarFrameKey).ShouldBeTrue();
        sixty.State.Inventory.Frame.ShouldBe(Catalog.CalendarFrameKey);
        sixty.Events.Count.ShouldBe(2);

        var ninety = RewardsEngine.ClaimCalendar(RewardsState.Empty with { Calendar = new CalendarState(0, null, 89) }, Monday, Now, NewId);
        ninety.State.Thorns.ShouldBe(5 + 100);
        ninety.Events.ShouldContain(e => e.Type == LedgerEventType.CalendarTrack && e.Amount == 100);

        // The meadow theme opens on 30 claimed days, not on counted learning days.
        var meadow = Catalog.Find("catalog.theme.meadow")!;
        ShopRules.Status(RewardsState.Empty with { LearningDays = 40 }, meadow, Monday, 0).Availability.ShouldBe(ItemAvailability.Locked);
        ShopRules.Status(RewardsState.Empty with { Calendar = new CalendarState(2, null, 30) }, meadow, Monday, 0).Availability.ShouldBe(ItemAvailability.Available);
    }

    [Fact]
    public void Daily_selection_is_deterministic_one_per_group_and_avoids_yesterday()
    {
        var context = new TaskContext(NewWordsPerDay: 10, CardsInLearning: 20);
        var a = TaskRules.SelectDaily(Learner, Monday, context, []);
        var b = TaskRules.SelectDaily(Learner, Monday, context, []);
        a.ShouldBe(b);
        a.Count.ShouldBe(3);
        a.Select(id => TaskCatalog.Find(id)!.Group).Distinct().Count().ShouldBe(3);

        var tomorrow = TaskRules.SelectDaily(Learner, Monday.AddDays(1), context, a);
        tomorrow.ShouldNotContain(id => a.Contains(id));

        var other = Guid.Parse("0199a4c0-0000-7000-8000-00000000bbbb");
        Enumerable.Range(0, 14).Any(i => !TaskRules.SelectDaily(Learner, Monday.AddDays(i), context, []).SequenceEqual(TaskRules.SelectDaily(other, Monday.AddDays(i), context, []))).ShouldBeTrue();

        // A beginner (no cards yet, pace 3): typed, warm-up, learned and new tasks are irrelevant; the pick still fills three slots.
        var beginner = TaskRules.SelectDaily(Learner, Monday, new TaskContext(3, 0), []);
        beginner.Count.ShouldBe(3);
        beginner.ShouldNotContain("TASK_D_NEW_5");
        beginner.ShouldNotContain("TASK_D_TYPED_5");
        beginner.ShouldNotContain("TASK_D_WARMUP");

        TaskRules.SelectWeekly(Learner, 2970, context).ShouldBe("TASK_W_DAYS"); // even week
        TaskCatalog.Weekly.Select(t => t.Id).ShouldContain(TaskRules.SelectWeekly(Learner, 2971, context));
    }

    [Fact]
    public void Settlement_completes_tasks_pays_them_and_never_twice()
    {
        var context = new TaskContext(10, 20);
        var state = RewardsState.Empty with { Tasks = TaskRules.EnsureSelection(TaskState.Empty, Learner, Monday, context) with { Daily = ["TASK_D_TYPED_5", "TASK_D_REVIEW_10", "TASK_D_TWO_SESSIONS"] } };
        var counters = new ProgressCounters(LearnedWords: 0, TypedCorrectTotal: 6, GradedTotal: 12, CardsInLearning: 20);
        var report = new SessionReport(Guid.NewGuid(), Graded: 12, Correct: 11, AgainCount: 1, TypedCorrect: 6, WordsLearned: 0, LearningDayCounted: true, WarmupCompleted: false, NewIntroduced: 2);

        var s = RewardsEngine.Settle(state, report, counters, Monday, Now, NewId, context, Learner);

        s.TasksCompleted.Select(t => t.Id).ShouldBe(["TASK_D_TYPED_5", "TASK_D_REVIEW_10", "TASK_ONB_FIRST_SESSION", "TASK_ONB_TYPED"]);
        s.State.Tasks.DailyDone.ShouldBe(new HashSet<string> { "TASK_D_TYPED_5", "TASK_D_REVIEW_10" });
        s.State.Tasks.OneOffDone.ShouldContain("TASK_ONB_FIRST_SESSION");
        s.State.Activity!.Graded.ShouldBe(12);
        s.State.Activity.TypedCorrect.ShouldBe(6);
        s.State.Activity.NewIntroduced.ShouldBe(2);
        s.State.Activity.AccurateSessions.ShouldBe(1);
        s.State.Week!.LearningDays.ShouldBe(1);
        s.Events.Count(e => e.Type == LedgerEventType.TaskComplete && e.Currency == Currency.Thorns).ShouldBe(4);
        // session 10 + learning day 5 + two daily tasks 20 + one-offs 20 + 10
        s.ThornsGained.ShouldBe(10 + 5 + 10 + 10 + 20 + 10 + s.NewAchievements.Sum(a => a.Thorns));

        // The second session of the day completes «Двічі за день» only; nothing already done pays again.
        var second = new SessionReport(Guid.NewGuid(), 6, 6, 0, 3, 0, true, false);
        var s2 = RewardsEngine.Settle(s.State, second, counters, Monday, Now, NewId, context, Learner);
        s2.TasksCompleted.Select(t => t.Id).ShouldBe(["TASK_D_TWO_SESSIONS"]);
        s2.State.Tasks.DailyDone.Count.ShouldBe(3);

        // Next day: a fresh selection, yesterday's ids remembered, counters restart.
        var s3 = RewardsEngine.Settle(s2.State, new SessionReport(Guid.NewGuid(), 3, 3, 0, 0, 0, false, false), counters, Monday.AddDays(1), Now, NewId, context, Learner);
        s3.State.Tasks.Day.ShouldBe(Monday.AddDays(1));
        s3.State.Tasks.PreviousDaily.ShouldBe(["TASK_D_TYPED_5", "TASK_D_REVIEW_10", "TASK_D_TWO_SESSIONS"]);
        s3.State.Tasks.DailyDone.ShouldBeEmpty();
        s3.State.Activity!.Graded.ShouldBe(3);
        s3.State.Week!.Graded.ShouldBe(12 + 6 + 3);
    }

    [Fact]
    public void Weekly_task_pays_outside_the_daily_cap_and_the_old_signature_ignores_tasks()
    {
        var context = new TaskContext(10, 20);
        var tasks = TaskRules.EnsureSelection(TaskState.Empty, Learner, Monday, context) with { Daily = [], WeeklyId = "TASK_W_REVIEW_100" };
        var state = RewardsState.Empty with { Tasks = tasks, Today = new DailyEarn(Monday, RewardsEngine.DailyEarnCap, 3, 10, true) };
        var report = new SessionReport(Guid.NewGuid(), 100, 90, 10, 0, 0, true, false);

        var s = RewardsEngine.Settle(state, report, new ProgressCounters(0, 0, 100), Monday, Now, NewId, context, Learner);
        s.TasksCompleted.Select(t => t.Id).ShouldContain("TASK_W_REVIEW_100");
        s.State.Tasks.WeeklyDone.ShouldBeTrue();
        s.Events.ShouldContain(e => e.Reason == "task:TASK_W_REVIEW_100" && e.Amount == 40); // the cap is full, the weekly task still pays

        var plain = RewardsEngine.Settle(state, report, new ProgressCounters(0, 0, 100), Monday, Now, NewId);
        plain.TasksCompleted.ShouldBeEmpty();
        plain.State.Activity!.Graded.ShouldBe(100); // counters are kept regardless
    }
}
