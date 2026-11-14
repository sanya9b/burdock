using System.Globalization;
using System.Text;
using Burdock.Application.UseCases.Rewards;
using Burdock.Domain.Rewards;
using Burdock.Presentation.Localization;
using Burdock.Presentation.Resources.Icons;
using Burdock.Presentation.Resources.Strings;

namespace Burdock.Presentation.ViewModels;

/// <summary>One micro-task row (spec 02 `TaskRow`, spec 07 §3.5): icon, name, «7 / 10», a thin progress bar, the reward on the right.</summary>
public sealed record TaskRowItem(string Id, string Title, string Body, int Progress, int Target, bool Done, int Thorns, string Glyph, TaskKind Kind)
{
    private static readonly CultureInfo Ukrainian = CultureInfo.GetCultureInfo("uk-UA");
    private static readonly CompositeFormat ProgressFormat = CompositeFormat.Parse(AppResources.Task_Progress);
    private static readonly CompositeFormat RewardFormat = CompositeFormat.Parse(AppResources.Task_Reward);
    private static readonly CompositeFormat A11yFormat = CompositeFormat.Parse(AppResources.Task_Row_A11y);

    public static TaskRowItem From(TaskView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var task = view.Task;
        return new TaskRowItem(task.Id, RewardsText.TaskTitle(task.Id), RewardsText.TaskBody(task.Id), view.Progress, task.Target, view.Done, task.Thorns, IconFor(task.Metric), task.Kind);
    }

    public string ProgressText => string.Format(Ukrainian, ProgressFormat, Progress, Target);

    public double Fraction => Target == 0 ? 1 : Math.Clamp((double)Progress / Target, 0, 1);

    public string RewardText => string.Format(Ukrainian, RewardFormat, Thorns);

    public bool IsWeekly => Kind == TaskKind.Weekly;

    public bool ShowProgressBar => Target > 1;

    /// <summary>Segment gap of the thin bar: visible segments for small targets, a near-continuous bar for 30 or 100.</summary>
    public double BarGap => Target > 15 ? 1 : 3;

    public string Accessibility => string.Format(Ukrainian, A11yFormat, Title, Done ? AppResources.Task_Done_A11y : ProgressText, RewardsText.Thorns(Thorns));

    private static string IconFor(TaskMetric metric) => metric switch
    {
        TaskMetric.Graded => Icon.Repeat,
        TaskMetric.NewIntroduced => Icon.Sparkles,
        TaskMetric.TypedCorrect or TaskMetric.TypedTotal => Icon.Keyboard,
        TaskMetric.SessionsCounted => Icon.Layers,
        TaskMetric.Warmups => Icon.Dumbbell,
        TaskMetric.WordsLearned or TaskMetric.LearnedTotal => Icon.Award,
        TaskMetric.AccurateSessions => Icon.Target,
        TaskMetric.LongestSession => Icon.Clock,
        TaskMetric.LearningDays => Icon.CalendarDays,
        TaskMetric.SessionsTotal => Icon.Flag,
        _ => Icon.Star,
    };
}

public enum CalendarTileState
{
    Done,
    Today,
    Future,
}

/// <summary>One of the seven calendar tiles (spec 02 `DailyCalendarTile`): position, reward, state.</summary>
public sealed record CalendarTileItem(int Position, int Reward, CalendarTileState State)
{
    private static readonly CultureInfo Ukrainian = CultureInfo.GetCultureInfo("uk-UA");
    private static readonly CompositeFormat RewardFormat = CompositeFormat.Parse(AppResources.Task_Reward);
    private static readonly CompositeFormat A11yFormat = CompositeFormat.Parse(AppResources.Calendar_Tile_A11y);

    public string PositionText => Position.ToString(Ukrainian);

    public string RewardText => string.Format(Ukrainian, RewardFormat, Reward);

    public bool IsDone => State == CalendarTileState.Done;

    public bool IsToday => State == CalendarTileState.Today;

    public string Accessibility => string.Format(Ukrainian, A11yFormat, Position, RewardsText.Thorns(Reward), State switch
    {
        CalendarTileState.Done => AppResources.Calendar_State_Done,
        CalendarTileState.Today => AppResources.Calendar_State_Today,
        _ => AppResources.Calendar_State_Future,
    });

    /// <summary>The seven tiles for a calendar view: positions before the next one are done this cycle, the one claimed today is «today».</summary>
    public static IReadOnlyList<CalendarTileItem> Build(CalendarView calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        var cycle = CalendarRules.Cycle;
        // Positions already paid in the current cycle: those before the next position (all seven when today closed the cycle).
        var doneCount = calendar.ClaimedPosition == cycle.Count ? cycle.Count : calendar.NextPosition;
        var tiles = new List<CalendarTileItem>(cycle.Count);
        for (var i = 0; i < cycle.Count; i++)
        {
            var position = i + 1;
            var state = position == calendar.ClaimedPosition ? CalendarTileState.Today : position <= doneCount ? CalendarTileState.Done : CalendarTileState.Future;
            tiles.Add(new CalendarTileItem(position, cycle[i], state));
        }

        return tiles;
    }
}
