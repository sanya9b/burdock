using System.Text;
using Burdock.Domain.Time;

namespace Burdock.Domain.Rewards;

public enum TaskKind
{
    Daily,
    Weekly,
    OneOff,
}

/// <summary>Selection groups of spec 07 §3.5.2: one daily task is picked from each.</summary>
public enum TaskGroup
{
    Reviews,
    Growth,
    Modes,
    Weekly,
    OneOff,
}

/// <summary>What a task counts. Day and week metrics read the activity counters of the window; total metrics read lifetime counters.</summary>
public enum TaskMetric
{
    Graded,
    NewIntroduced,
    TypedCorrect,
    SessionsCounted,
    Warmups,
    WordsLearned,
    AccurateSessions,
    LongestSession,
    LearningDays,
    SessionsTotal,
    TypedTotal,
    LearnedTotal,
}

public sealed record TaskDefinition(string Id, TaskKind Kind, TaskGroup Group, TaskMetric Metric, int Target, int Thorns, int Xp);

/// <summary>
/// The micro-task library (spec 07 §3.5.1, FR-07-14, FR-07-15), limited to tasks whose predicates the session report can evaluate today.
/// Tasks about examples, pronunciation, topics, relearning, the due queue, the widget, settings and Live Activity wait for their events.
/// Rewards follow §3.2.2 (daily 10, weekly 40, one-off per table) and §3.2.1 XP (10 per task, 30 weekly).
/// </summary>
public static class TaskCatalog
{
    public static IReadOnlyList<TaskDefinition> All { get; } =
    [
        new("TASK_D_REVIEW_10", TaskKind.Daily, TaskGroup.Reviews, TaskMetric.Graded, 10, 10, 10),
        new("TASK_D_ACCURACY", TaskKind.Daily, TaskGroup.Reviews, TaskMetric.AccurateSessions, 1, 10, 10),
        new("TASK_D_NEW_5", TaskKind.Daily, TaskGroup.Growth, TaskMetric.NewIntroduced, 5, 10, 10),
        new("TASK_D_LEARNED_1", TaskKind.Daily, TaskGroup.Growth, TaskMetric.WordsLearned, 1, 10, 10),
        new("TASK_D_TYPED_5", TaskKind.Daily, TaskGroup.Modes, TaskMetric.TypedCorrect, 5, 10, 10),
        new("TASK_D_WARMUP", TaskKind.Daily, TaskGroup.Modes, TaskMetric.Warmups, 1, 10, 10),
        new("TASK_D_SESSION_15", TaskKind.Daily, TaskGroup.Modes, TaskMetric.LongestSession, 15, 10, 10),
        new("TASK_D_TWO_SESSIONS", TaskKind.Daily, TaskGroup.Modes, TaskMetric.SessionsCounted, 2, 10, 10),
        new("TASK_W_DAYS", TaskKind.Weekly, TaskGroup.Weekly, TaskMetric.LearningDays, 5, 40, 30),
        new("TASK_W_LEARNED_10", TaskKind.Weekly, TaskGroup.Weekly, TaskMetric.WordsLearned, 10, 40, 30),
        new("TASK_W_TYPED_30", TaskKind.Weekly, TaskGroup.Weekly, TaskMetric.TypedCorrect, 30, 40, 30),
        new("TASK_W_REVIEW_100", TaskKind.Weekly, TaskGroup.Weekly, TaskMetric.Graded, 100, 40, 30),
        new("TASK_W_NEW_30", TaskKind.Weekly, TaskGroup.Weekly, TaskMetric.NewIntroduced, 30, 40, 30),
        new("TASK_ONB_FIRST_SESSION", TaskKind.OneOff, TaskGroup.OneOff, TaskMetric.SessionsTotal, 1, 20, 10),
        new("TASK_ONB_TYPED", TaskKind.OneOff, TaskGroup.OneOff, TaskMetric.TypedTotal, 1, 10, 10),
        new("TASK_ONB_LEARNED", TaskKind.OneOff, TaskGroup.OneOff, TaskMetric.LearnedTotal, 1, 30, 10),
    ];

    private static readonly Dictionary<string, TaskDefinition> ById = All.ToDictionary(t => t.Id, StringComparer.Ordinal);

    public static IReadOnlyList<TaskDefinition> Daily { get; } = All.Where(t => t.Kind == TaskKind.Daily).ToArray();

    public static IReadOnlyList<TaskDefinition> Weekly { get; } = All.Where(t => t.Kind == TaskKind.Weekly).ToArray();

    public static IReadOnlyList<TaskDefinition> OneOff { get; } = All.Where(t => t.Kind == TaskKind.OneOff).ToArray();

    public static TaskDefinition? Find(string id) => ById.GetValueOrDefault(id);
}

/// <summary>Uncapped counters of one learning day, folded from session reports; what the daily tasks measure.</summary>
public sealed record DayActivity(LearningDay Day, int Graded, int NewIntroduced, int TypedCorrect, int SessionsCounted, int Warmups, int WordsLearned, int AccurateSessions, int LongestSession)
{
    public const int AccuracyMinGraded = 10;
    public const double AccuracyThreshold = 0.8;

    public static DayActivity For(LearningDay day) => new(day, 0, 0, 0, 0, 0, 0, 0, 0);

    public DayActivity Record(SessionReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var accurate = report.Graded >= AccuracyMinGraded && report.Correct >= report.Graded * AccuracyThreshold;
        return this with
        {
            Graded = Graded + report.Graded,
            NewIntroduced = NewIntroduced + report.NewIntroduced,
            TypedCorrect = TypedCorrect + report.TypedCorrect,
            SessionsCounted = SessionsCounted + (report.LearningDayCounted ? 1 : 0),
            Warmups = Warmups + (report.WarmupCompleted ? 1 : 0),
            WordsLearned = WordsLearned + report.WordsLearned,
            AccurateSessions = AccurateSessions + (accurate ? 1 : 0),
            LongestSession = Math.Max(LongestSession, report.Graded),
        };
    }
}

/// <summary>Counters of one Monday-based week (<see cref="StreakRules.WeekOf"/>); what the weekly task measures.</summary>
public sealed record WeekActivity(int Week, int Graded, int NewIntroduced, int TypedCorrect, int WordsLearned, int LearningDays)
{
    public static WeekActivity For(int week) => new(week, 0, 0, 0, 0, 0);

    public WeekActivity Record(SessionReport report, bool newLearningDay)
    {
        ArgumentNullException.ThrowIfNull(report);
        return this with
        {
            Graded = Graded + report.Graded,
            NewIntroduced = NewIntroduced + report.NewIntroduced,
            TypedCorrect = TypedCorrect + report.TypedCorrect,
            WordsLearned = WordsLearned + report.WordsLearned,
            LearningDays = LearningDays + (newLearningDay ? 1 : 0),
        };
    }
}

/// <summary>Lifetime counters the one-off tasks read.</summary>
public readonly record struct TaskTotals(int SessionsCompleted, int TypedCorrectTotal, int LearnedWords);

/// <summary>Learner facts the relevance filter of §3.5.2 looks at (settings and card counts; the plan of the day is not needed for the v1 library).</summary>
public sealed record TaskContext(int NewWordsPerDay, int CardsInLearning)
{
    public static TaskContext Default { get; } = new(10, 0);
}

/// <summary>Selected tasks and what is done (spec 07 §3.7.1 `tasks: TaskProgress`); lists keep selection order.</summary>
public sealed record TaskState(
    LearningDay? Day,
    IReadOnlyList<string> Daily,
    IReadOnlySet<string> DailyDone,
    IReadOnlyList<string> PreviousDaily,
    int? Week,
    string? WeeklyId,
    bool WeeklyDone,
    IReadOnlySet<string> OneOffDone)
{
    public static TaskState Empty { get; } = new(null, [], new HashSet<string>(StringComparer.Ordinal), [], null, null, false, new HashSet<string>(StringComparer.Ordinal));

    public bool IsDone(TaskDefinition task)
    {
        ArgumentNullException.ThrowIfNull(task);
        return task.Kind switch
        {
            TaskKind.Daily => DailyDone.Contains(task.Id),
            TaskKind.Weekly => WeeklyDone && WeeklyId == task.Id,
            _ => OneOffDone.Contains(task.Id),
        };
    }

    public bool Equals(TaskState? other)
        => other is not null && Day == other.Day && Week == other.Week && WeeklyId == other.WeeklyId && WeeklyDone == other.WeeklyDone
           && Daily.SequenceEqual(other.Daily, StringComparer.Ordinal) && PreviousDaily.SequenceEqual(other.PreviousDaily, StringComparer.Ordinal)
           && DailyDone.SetEquals(other.DailyDone) && OneOffDone.SetEquals(other.OneOffDone);

    public override int GetHashCode() => HashCode.Combine(Day, Week, WeeklyId, WeeklyDone, Daily.Count, DailyDone.Count, OneOffDone.Count);
}

/// <summary>Deterministic selection (seeded by learner and day, FR-07-14), progress and completion of micro-tasks.</summary>
public static class TaskRules
{
    public const int DailyCount = 3;
    private const string DailySalt = "tasks-v1";
    private const string WeeklySalt = "tasks-weekly-v1";

    private static readonly TaskGroup[] DailyGroups = [TaskGroup.Reviews, TaskGroup.Growth, TaskGroup.Modes];

    /// <summary>Relevance of §3.5.1, reduced to what the context knows; everything else is always relevant.</summary>
    public static bool Relevant(TaskDefinition task, TaskContext context)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(context);
        return task.Id switch
        {
            "TASK_D_NEW_5" or "TASK_W_NEW_30" => context.NewWordsPerDay >= 5,
            "TASK_D_WARMUP" or "TASK_D_TYPED_5" or "TASK_W_TYPED_30" => context.CardsInLearning >= 5,
            "TASK_D_LEARNED_1" => context.CardsInLearning >= 1,
            "TASK_W_LEARNED_10" => context.CardsInLearning >= 10,
            _ => true,
        };
    }

    /// <summary>Selects today's and this week's tasks when the day or the week changed; otherwise returns the state untouched.</summary>
    public static TaskState EnsureSelection(TaskState state, Guid learnerKey, LearningDay day, TaskContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        var next = state;
        if (state.Day != day)
        {
            var previous = state.Day == day.Previous() ? state.Daily : [];
            next = next with { Day = day, Daily = SelectDaily(learnerKey, day, context, previous), DailyDone = new HashSet<string>(StringComparer.Ordinal), PreviousDaily = previous };
        }

        var week = StreakRules.WeekOf(day);
        if (state.Week != week)
        {
            next = next with { Week = week, WeeklyId = SelectWeekly(learnerKey, week, context), WeeklyDone = false };
        }

        return next;
    }

    /// <summary>One task per group through the seeded generator; yesterday's tasks are avoided while at least three others remain.</summary>
    public static IReadOnlyList<string> SelectDaily(Guid learnerKey, LearningDay day, TaskContext context, IReadOnlyList<string> previous)
    {
        ArgumentNullException.ThrowIfNull(previous);
        var rng = new SplitMix64(Seed(learnerKey, day.Date.DayNumber, DailySalt));
        var relevant = TaskCatalog.Daily.Where(t => Relevant(t, context)).ToList();
        var fresh = relevant.Where(t => !previous.Contains(t.Id, StringComparer.Ordinal)).ToList();
        var candidates = fresh.Count >= DailyCount ? fresh : relevant;
        var picked = new List<string>(DailyCount);
        foreach (var group in DailyGroups)
        {
            var inGroup = candidates.Where(t => t.Group == group && !picked.Contains(t.Id, StringComparer.Ordinal)).ToList();
            if (inGroup.Count > 0)
            {
                picked.Add(inGroup[rng.Next(inGroup.Count)].Id);
            }
        }

        while (picked.Count < DailyCount)
        {
            var rest = candidates.Where(t => !picked.Contains(t.Id, StringComparer.Ordinal)).ToList();
            if (rest.Count == 0)
            {
                break;
            }

            picked.Add(rest[rng.Next(rest.Count)].Id);
        }

        return picked;
    }

    /// <summary>«П'ять днів» every second week; otherwise one relevant weekly task through the seeded generator.</summary>
    public static string SelectWeekly(Guid learnerKey, int week, TaskContext context)
    {
        if (week % 2 == 0)
        {
            return "TASK_W_DAYS";
        }

        var rng = new SplitMix64(Seed(learnerKey, week, WeeklySalt));
        var relevant = TaskCatalog.Weekly.Where(t => Relevant(t, context)).ToList();
        return relevant.Count == 0 ? "TASK_W_DAYS" : relevant[rng.Next(relevant.Count)].Id;
    }

    public static int Progress(TaskDefinition task, DayActivity day, WeekActivity week, TaskTotals totals)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(day);
        ArgumentNullException.ThrowIfNull(week);
        var daily = task.Kind == TaskKind.Daily;
        var value = task.Metric switch
        {
            TaskMetric.Graded => daily ? day.Graded : week.Graded,
            TaskMetric.NewIntroduced => daily ? day.NewIntroduced : week.NewIntroduced,
            TaskMetric.TypedCorrect => daily ? day.TypedCorrect : week.TypedCorrect,
            TaskMetric.WordsLearned => daily ? day.WordsLearned : week.WordsLearned,
            TaskMetric.SessionsCounted => day.SessionsCounted,
            TaskMetric.Warmups => day.Warmups,
            TaskMetric.AccurateSessions => day.AccurateSessions,
            TaskMetric.LongestSession => day.LongestSession,
            TaskMetric.LearningDays => week.LearningDays,
            TaskMetric.SessionsTotal => totals.SessionsCompleted,
            TaskMetric.TypedTotal => totals.TypedCorrectTotal,
            TaskMetric.LearnedTotal => totals.LearnedWords,
            _ => 0,
        };
        return Math.Min(value, task.Target);
    }

    public static bool IsComplete(TaskDefinition task, DayActivity day, WeekActivity week, TaskTotals totals) => Progress(task, day, week, totals) >= task.Target;

    /// <summary>FNV-1a over learner, window and salt: the same on every device of the learner (AC-07-14).</summary>
    public static ulong Seed(Guid learnerKey, int window, string salt)
    {
        ArgumentNullException.ThrowIfNull(salt);
        const ulong offset = 14695981039346656037;
        const ulong prime = 1099511628211;
        var hash = offset;
        foreach (var b in learnerKey.ToByteArray())
        {
            hash = (hash ^ b) * prime;
        }

        foreach (var b in BitConverter.GetBytes(window))
        {
            hash = (hash ^ b) * prime;
        }

        foreach (var b in Encoding.UTF8.GetBytes(salt))
        {
            hash = (hash ^ b) * prime;
        }

        return hash;
    }

    /// <summary>SplitMix64: tiny, well-distributed and identical on every platform; enough for picking tasks.</summary>
    private sealed class SplitMix64(ulong seed)
    {
        private ulong _state = seed;

        public int Next(int maxExclusive)
        {
            _state += 0x9E3779B97F4A7C15;
            var z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EB;
            z ^= z >> 31;
            return (int)(z % (ulong)maxExclusive);
        }
    }
}
