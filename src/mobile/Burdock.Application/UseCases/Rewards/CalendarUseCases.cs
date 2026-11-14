using Burdock.Application.Ports;
using Burdock.Application.UseCases.LearningDays;
using Burdock.Domain.Learning;
using Burdock.Domain.Rewards;

namespace Burdock.Application.UseCases.Rewards;

/// <summary>What the first open of a learning day paid (spec 07 §3.4); <see cref="Claimed"/> is false on later opens of the same day.</summary>
public sealed record CalendarClaimView(bool Claimed, int Reward, int Position, int ClaimedDays, CalendarTrackReward? Track, bool GamificationEnabled);

/// <summary>Claims the login calendar for today; idempotent per learning day, silent without gamification (the state still moves, spec 07 §3.9).</summary>
public sealed class ClaimCalendarUseCase(ILearnerContext learner, IRewardsStore rewards, LearningDayResolver learningDays, IClock clock, IIdGenerator ids)
{
    public async Task<CalendarClaimView> ExecuteAsync(CancellationToken cancellationToken)
    {
        var learnerId = await learner.GetLearnerIdAsync(cancellationToken).ConfigureAwait(false);
        var settings = await learner.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        var state = await rewards.GetAsync(learnerId, cancellationToken).ConfigureAwait(false) ?? RewardsState.Empty;
        var settlement = RewardsEngine.ClaimCalendar(state, learningDays.Today(), clock.UtcNow, ids.NewId);
        if (settlement.Claim is not { } claim)
        {
            return new CalendarClaimView(false, 0, state.Calendar.Position, state.Calendar.ClaimedDays, null, settings.GamificationEnabled);
        }

        await rewards.SaveAsync(learnerId, settlement.State, settlement.Events, cancellationToken).ConfigureAwait(false);
        return new CalendarClaimView(true, claim.Reward, claim.Position, claim.State.ClaimedDays, claim.Track, settings.GamificationEnabled);
    }
}

/// <summary>One task with its progress today (or this week) and whether it is done.</summary>
public sealed record TaskView(TaskDefinition Task, int Progress, bool Done);

/// <summary>The calendar as the UI shows it: the 1-based position claimed today (0 when not yet), days claimed ever, the next track milestone.</summary>
public sealed record CalendarView(int ClaimedPosition, int NextPosition, int ClaimedDays, CalendarTrackReward NextTrack);

public sealed record TasksView(IReadOnlyList<TaskView> Daily, TaskView? Weekly, IReadOnlyList<TaskView> OneOffs, CalendarView Calendar, bool GamificationEnabled);

/// <summary>Reads today's tasks (spec 07 §3.5, FR-07-14), selecting them for the day and the week if nothing selected them yet, plus the calendar.</summary>
public sealed class GetDailyTasksUseCase(ILearnerContext learner, IRewardsStore rewards, ILearnerCardRepository cards, IReviewLogStore reviewLog, LearningDayResolver learningDays)
{
    public async Task<TasksView> ExecuteAsync(CancellationToken cancellationToken)
    {
        var learnerId = await learner.GetLearnerIdAsync(cancellationToken).ConfigureAwait(false);
        var settings = await learner.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        var state = await rewards.GetAsync(learnerId, cancellationToken).ConfigureAwait(false) ?? RewardsState.Empty;
        var day = learningDays.Today();

        var learnerCards = await cards.ListAsync(learnerId, cancellationToken).ConfigureAwait(false);
        var learnedWords = learnerCards.Values.Count(c => c.State is CardState.Learned or CardState.Known);
        var cardsInLearning = learnerCards.Values.Count(c => c.State is not (CardState.New or CardState.Introduced or CardState.Known));
        var typedCorrect = await reviewLog.CountAsync(learnerId, ExerciseMode.TypedRecall, onlyCorrect: true, cancellationToken).ConfigureAwait(false);

        var tasks = TaskRules.EnsureSelection(state.Tasks, learnerId.Value, day, new TaskContext(settings.NewWordsPerDay, cardsInLearning));
        if (tasks != state.Tasks)
        {
            state = state with { Tasks = tasks };
            await rewards.SaveAsync(learnerId, state, [], cancellationToken).ConfigureAwait(false);
        }

        var activity = state.Activity is { } act && act.Day == day ? act : DayActivity.For(day);
        var weekNo = StreakRules.WeekOf(day);
        var week = state.Week is { } wk && wk.Week == weekNo ? wk : WeekActivity.For(weekNo);
        var totals = new TaskTotals(state.SessionsCompleted, typedCorrect, learnedWords);

        TaskView View(TaskDefinition task) => new(task, TaskRules.Progress(task, activity, week, totals), tasks.IsDone(task));

        var daily = tasks.Daily.Select(TaskCatalog.Find).OfType<TaskDefinition>().Select(View).ToArray();
        var weekly = tasks.WeeklyId is { } id && TaskCatalog.Find(id) is { } weeklyTask ? View(weeklyTask) : null;
        var oneOffs = TaskCatalog.OneOff.Where(t => !tasks.OneOffDone.Contains(t.Id)).Select(View).ToArray();
        var calendar = state.Calendar;
        var calendarView = new CalendarView(
            calendar.IsClaimedOn(day) ? (calendar.Position == 0 ? CalendarRules.Cycle.Count : calendar.Position) : 0,
            calendar.Position,
            calendar.ClaimedDays,
            CalendarRules.NextTrack(calendar.ClaimedDays));
        return new TasksView(daily, weekly, oneOffs, calendarView, settings.GamificationEnabled);
    }
}
