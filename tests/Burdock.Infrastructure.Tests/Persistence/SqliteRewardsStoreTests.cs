using Burdock.Domain.Rewards;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Time;
using Burdock.Infrastructure.Persistence;

namespace Burdock.Infrastructure.Tests.Persistence;

/// <summary>The folded rewards state in SQLite (spec 07 §3.7.1): calendar, micro-tasks and activity counters survive a round trip and a table migration.</summary>
public sealed class SqliteRewardsStoreTests
{
    private static readonly LearnerId Learner = LearnerId.From(Guid.Parse("0199a4c0-0000-7000-8000-00000000aaaa"));
    private static readonly LearningDay Day = new(new DateOnly(2026, 11, 14));

    [Fact]
    public async Task Calendar_tasks_and_activity_round_trip()
    {
        await using var database = new BurdockDatabase(DatabaseOptions.InMemory);
        var store = new SqliteRewardsStore(database);
        var ct = TestContext.Current.CancellationToken;
        var tasks = new TaskState(
            Day,
            ["TASK_D_TYPED_5", "TASK_D_REVIEW_10", "TASK_D_TWO_SESSIONS"],
            new HashSet<string>(StringComparer.Ordinal) { "TASK_D_TYPED_5" },
            ["TASK_D_WARMUP"],
            StreakRules.WeekOf(Day),
            "TASK_W_DAYS",
            false,
            new HashSet<string>(StringComparer.Ordinal) { "TASK_ONB_FIRST_SESSION", "TASK_ONB_TYPED" });
        var state = RewardsState.Empty with
        {
            Thorns = 42,
            Calendar = new CalendarState(3, Day, 18),
            Tasks = tasks,
            Activity = new DayActivity(Day, 12, 2, 6, 1, 0, 1, 1, 12),
            Week = new WeekActivity(StreakRules.WeekOf(Day), 30, 5, 9, 2, 3),
        };

        await store.SaveAsync(Learner, state, [], ct);
        var loaded = await store.GetAsync(Learner, ct);

        loaded.ShouldNotBeNull();
        loaded.Calendar.ShouldBe(new CalendarState(3, Day, 18));
        loaded.Tasks.ShouldBe(tasks);
        loaded.Activity.ShouldBe(state.Activity);
        loaded.Week.ShouldBe(state.Week);
    }

    [Fact]
    public async Task A_row_from_before_the_calendar_columns_reads_as_empty_calendar_and_tasks()
    {
        await using var database = new BurdockDatabase(DatabaseOptions.InMemory);
        var store = new SqliteRewardsStore(database);
        var ct = TestContext.Current.CancellationToken;
        await store.SaveAsync(Learner, RewardsState.Empty with { Thorns = 7 }, [], ct);

        // Columns added by CreateTable's migration are NULL on rows written by an older build.
        var connection = await database.GetOpenConnectionAsync(ct);
        await connection.ExecuteAsync("UPDATE rewards_state SET TasksDaily = NULL, TasksDailyDone = NULL, TasksPreviousDaily = NULL, TasksOneOffDone = NULL, OwnedItems = NULL, TasksWeeklyId = NULL");

        var loaded = await store.GetAsync(Learner, ct);

        loaded.ShouldNotBeNull();
        loaded.Thorns.ShouldBe(7);
        loaded.Calendar.ShouldBe(CalendarState.Empty);
        loaded.Tasks.ShouldBe(TaskState.Empty);
        loaded.Activity.ShouldBeNull();
        loaded.Week.ShouldBeNull();
        loaded.Inventory.Owns(Catalog.DefaultThemeKey).ShouldBeTrue();
    }
}
