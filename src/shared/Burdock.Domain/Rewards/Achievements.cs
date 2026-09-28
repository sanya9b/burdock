namespace Burdock.Domain.Rewards;

/// <summary>Achievement identifiers of spec 07 §3.6 that phase 1 can evaluate; the rest (topics, comeback, relearn, wardrobe) join with their features.</summary>
public static class AchievementIds
{
    public const string FirstStep = "ACH_FIRST_STEP";
    public const string Streak7 = "ACH_STREAK_7";
    public const string Streak30 = "ACH_STREAK_30";
    public const string Streak100 = "ACH_STREAK_100";
    public const string Streak365 = "ACH_STREAK_365";
    public const string Learned10 = "ACH_LEARNED_10";
    public const string Learned100 = "ACH_LEARNED_100";
    public const string Learned500 = "ACH_LEARNED_500";
    public const string Learned1000 = "ACH_LEARNED_1000";
    public const string Typed100 = "ACH_TYPED_100";
    public const string Reviews1000 = "ACH_REVIEWS_1000";
    public const string Level10 = "ACH_LEVEL_10";
    public const string Level20 = "ACH_LEVEL_20";
    public const string Level30 = "ACH_LEVEL_30";
    public const string Perfect20 = "ACH_PERFECT_20";
}

/// <summary>Catalogue entry: reward and visibility; names and descriptions are UI strings keyed by the id.</summary>
public sealed record AchievementDefinition(string Id, int Thorns, bool Hidden, string Icon);

/// <summary>What the rules look at after a session; the application layer assembles it from the stores.</summary>
public sealed record ProgressSnapshot(
    int SessionsCompleted,
    int LearnedWords,
    int StreakLength,
    int TypedCorrectTotal,
    int GradedTotal,
    int Level,
    int LastSessionGraded,
    bool LastSessionHadAgain);

public static class AchievementCatalog
{
    public static IReadOnlyList<AchievementDefinition> All { get; } =
    [
        new(AchievementIds.FirstStep, 20, false, "footprints"),
        new(AchievementIds.Streak7, 30, false, "flame"),
        new(AchievementIds.Streak30, 50, false, "calendar-check"),
        new(AchievementIds.Streak100, 100, false, "medal"),
        new(AchievementIds.Streak365, 100, false, "award"),
        new(AchievementIds.Learned10, 20, false, "sprout"),
        new(AchievementIds.Learned100, 50, false, "trees"),
        new(AchievementIds.Learned500, 100, false, "mountain"),
        new(AchievementIds.Learned1000, 100, false, "mountain"),
        new(AchievementIds.Typed100, 30, false, "keyboard"),
        new(AchievementIds.Reviews1000, 50, false, "repeat"),
        new(AchievementIds.Level10, 30, false, "flower"),
        new(AchievementIds.Level20, 50, false, "award"),
        new(AchievementIds.Level30, 100, false, "crown"),
        new(AchievementIds.Perfect20, 30, true, "sparkles"),
    ];

    public static AchievementDefinition Get(string id) => All.First(a => string.Equals(a.Id, id, StringComparison.Ordinal));

    /// <summary>Achievements whose condition now holds and which were not unlocked before, in catalogue order (spec 07 FR-07-16: once only).</summary>
    public static IReadOnlyList<AchievementDefinition> NewlyUnlocked(ProgressSnapshot progress, IReadOnlySet<string> unlocked)
    {
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(unlocked);
        return All.Where(a => !unlocked.Contains(a.Id) && Holds(a.Id, progress)).ToArray();
    }

    private static bool Holds(string id, ProgressSnapshot p) => id switch
    {
        AchievementIds.FirstStep => p.SessionsCompleted >= 1,
        AchievementIds.Streak7 => p.StreakLength >= 7,
        AchievementIds.Streak30 => p.StreakLength >= 30,
        AchievementIds.Streak100 => p.StreakLength >= 100,
        AchievementIds.Streak365 => p.StreakLength >= 365,
        AchievementIds.Learned10 => p.LearnedWords >= 10,
        AchievementIds.Learned100 => p.LearnedWords >= 100,
        AchievementIds.Learned500 => p.LearnedWords >= 500,
        AchievementIds.Learned1000 => p.LearnedWords >= 1000,
        AchievementIds.Typed100 => p.TypedCorrectTotal >= 100,
        AchievementIds.Reviews1000 => p.GradedTotal >= 1000,
        AchievementIds.Level10 => p.Level >= 10,
        AchievementIds.Level20 => p.Level >= 20,
        AchievementIds.Level30 => p.Level >= 30,
        AchievementIds.Perfect20 => p.LastSessionGraded >= 20 && !p.LastSessionHadAgain,
        _ => false,
    };
}
