namespace Burdock.Domain.Rewards;

/// <summary>Sections of the thorn shop (spec 07 §3.2.3, Q34; scenes and rides per decision log item 22): cosmetics and the streak freeze, never anything with a learning advantage.</summary>
public enum CatalogCategory
{
    WidgetTheme,
    Outfit,
    Frame,
    Scene,
    Ride,
    Freeze,
}

/// <summary>What has to be true before an item can be bought; shown openly («Відкриється на рівні 8»), never with a deadline.</summary>
public enum UnlockKind
{
    None,
    Level,
    Streak,
    WordsLearned,
    LearningDays,
    Sessions,
}

public readonly record struct UnlockCondition(UnlockKind Kind, int Threshold)
{
    public static UnlockCondition None { get; } = new(UnlockKind.None, 0);

    public static UnlockCondition Level(int level) => new(UnlockKind.Level, level);

    public static UnlockCondition Streak(int days) => new(UnlockKind.Streak, days);

    public static UnlockCondition WordsLearned(int words) => new(UnlockKind.WordsLearned, words);

    public static UnlockCondition LearningDays(int days) => new(UnlockKind.LearningDays, days);

    public static UnlockCondition Sessions(int sessions) => new(UnlockKind.Sessions, sessions);

    public bool IsMet(UnlockContext context) => Kind switch
    {
        UnlockKind.None => true,
        UnlockKind.Level => context.Level >= Threshold,
        UnlockKind.Streak => context.LongestStreak >= Threshold,
        UnlockKind.WordsLearned => context.LearnedWords >= Threshold,
        UnlockKind.LearningDays => context.LearningDays >= Threshold,
        UnlockKind.Sessions => context.SessionsCompleted >= Threshold,
        _ => false,
    };
}

/// <summary>The learner's progress the unlock conditions look at.</summary>
public sealed record UnlockContext(int Level, int LongestStreak, int LearnedWords, int LearningDays, int SessionsCompleted);

/// <summary>One purchasable item. <see cref="Key"/> is the stable id of spec 07 §3.2.3 and the ledger reason of its purchase.</summary>
public sealed record CatalogItem(string Key, CatalogCategory Category, int Price, UnlockCondition Unlock)
{
    /// <summary>Last segment of the key («scarf», «night», «gold»); the UI maps it to a name, a drawing or a colour.</summary>
    public string Slug => Key[(Key.LastIndexOf('.') + 1)..];
}

/// <summary>The catalogue (spec 07 §3.2.3). Prices in thorns; the cosmetics sum (≈ 12 750) is the inflation guard of §3.2.4.</summary>
public static class Catalog
{
    public const string ThemePrefix = "catalog.theme.";
    public const string OutfitPrefix = "catalog.outfit.";
    public const string FramePrefix = "catalog.frame.";
    public const string ScenePrefix = "catalog.scene.";
    public const string RidePrefix = "catalog.ride.";
    public const string FreezeKey = "catalog.freeze";
    public const string DefaultThemeKey = "catalog.theme.bloom";

    public static IReadOnlyList<CatalogItem> All { get; } =
    [
        new(DefaultThemeKey, CatalogCategory.WidgetTheme, 0, UnlockCondition.None),
        new("catalog.theme.meadow", CatalogCategory.WidgetTheme, 0, UnlockCondition.LearningDays(30)),
        new("catalog.theme.night", CatalogCategory.WidgetTheme, 400, UnlockCondition.None),
        new("catalog.theme.sun", CatalogCategory.WidgetTheme, 600, UnlockCondition.Level(8)),
        new("catalog.outfit.scarf", CatalogCategory.Outfit, 150, UnlockCondition.None),
        new("catalog.outfit.glasses", CatalogCategory.Outfit, 150, UnlockCondition.None),
        new("catalog.outfit.strawhat", CatalogCategory.Outfit, 200, UnlockCondition.None),
        new("catalog.outfit.backpack", CatalogCategory.Outfit, 200, UnlockCondition.Sessions(1)),
        new("catalog.outfit.raincoat", CatalogCategory.Outfit, 250, UnlockCondition.None),
        new("catalog.outfit.wreath", CatalogCategory.Outfit, 300, UnlockCondition.Streak(7)),
        new("catalog.outfit.sweater", CatalogCategory.Outfit, 300, UnlockCondition.None),
        new("catalog.outfit.bandana", CatalogCategory.Outfit, 350, UnlockCondition.WordsLearned(50)),
        new("catalog.outfit.vyshyvanka", CatalogCategory.Outfit, 500, UnlockCondition.Level(10)),
        new("catalog.outfit.spacehelmet", CatalogCategory.Outfit, 600, UnlockCondition.WordsLearned(100)),
        new("catalog.outfit.crown", CatalogCategory.Outfit, 800, UnlockCondition.Level(20)),
        new("catalog.frame.leaf", CatalogCategory.Frame, 100, UnlockCondition.None),
        new("catalog.frame.lime", CatalogCategory.Frame, 150, UnlockCondition.None),
        new("catalog.frame.violet", CatalogCategory.Frame, 150, UnlockCondition.None),
        new("catalog.frame.coral", CatalogCategory.Frame, 200, UnlockCondition.WordsLearned(30)),
        new("catalog.frame.sunny", CatalogCategory.Frame, 250, UnlockCondition.Streak(14)),
        new("catalog.frame.sky", CatalogCategory.Frame, 300, UnlockCondition.None),
        new("catalog.frame.gold", CatalogCategory.Frame, 500, UnlockCondition.Level(15)),
        new("catalog.scene.meadow", CatalogCategory.Scene, 200, UnlockCondition.None),
        new("catalog.scene.sea", CatalogCategory.Scene, 250, UnlockCondition.None),
        new("catalog.scene.city", CatalogCategory.Scene, 300, UnlockCondition.None),
        new("catalog.scene.night", CatalogCategory.Scene, 350, UnlockCondition.None),
        new("catalog.scene.winter", CatalogCategory.Scene, 400, UnlockCondition.LearningDays(14)),
        new("catalog.scene.space", CatalogCategory.Scene, 500, UnlockCondition.Level(12)),
        new("catalog.ride.scooter", CatalogCategory.Ride, 250, UnlockCondition.None),
        new("catalog.ride.bicycle", CatalogCategory.Ride, 300, UnlockCondition.None),
        new("catalog.ride.car", CatalogCategory.Ride, 400, UnlockCondition.WordsLearned(40)),
        new("catalog.ride.van", CatalogCategory.Ride, 450, UnlockCondition.None),
        new("catalog.ride.bus", CatalogCategory.Ride, 500, UnlockCondition.LearningDays(30)),
        new("catalog.ride.steamship", CatalogCategory.Ride, 600, UnlockCondition.Streak(14)),
        new("catalog.ride.airplane", CatalogCategory.Ride, 800, UnlockCondition.WordsLearned(200)),
        new("catalog.ride.saucer", CatalogCategory.Ride, 1000, UnlockCondition.Level(25)),
        new(FreezeKey, CatalogCategory.Freeze, 200, UnlockCondition.None),
    ];

    private static readonly Dictionary<string, CatalogItem> ByKey = All.ToDictionary(i => i.Key, StringComparer.Ordinal);

    /// <summary>Widget theme ids of spec 02 (DesignTokens themes) for the theme items; the bloom theme is the planner default.</summary>
    private static readonly Dictionary<string, string> WidgetThemeIds = new(StringComparer.Ordinal)
    {
        [DefaultThemeKey] = "burdock.default",
        ["catalog.theme.meadow"] = "luh",
        ["catalog.theme.night"] = "nich",
        ["catalog.theme.sun"] = "sontse",
    };

    public static CatalogItem? Find(string key) => ByKey.GetValueOrDefault(key);

    public static IEnumerable<CatalogItem> InCategory(CatalogCategory category) => All.Where(i => i.Category == category);

    /// <summary>The timeline theme id for an owned theme item; unknown keys fall back to the default theme.</summary>
    public static string WidgetThemeId(string themeKey) => WidgetThemeIds.GetValueOrDefault(themeKey, "burdock.default");
}

/// <summary>Owned cosmetics and what is worn (spec 07 §3.7.1 `inventory`, `equipped`): outfit, frame, theme and, since log item 22, a scene behind the burr and a ride under it. The default theme is always owned and worn until another is chosen.</summary>
public sealed record Inventory(IReadOnlySet<string> Owned, string? Outfit, string? Frame, string Theme, string? Scene = null, string? Ride = null)
{
    public static Inventory Empty { get; } = new(new HashSet<string>(StringComparer.Ordinal) { Catalog.DefaultThemeKey }, null, null, Catalog.DefaultThemeKey);

    public bool Owns(string key) => key == Catalog.DefaultThemeKey || Owned.Contains(key);

    public bool IsEquipped(CatalogItem item) => item.Category switch
    {
        CatalogCategory.Outfit => Outfit == item.Key,
        CatalogCategory.Frame => Frame == item.Key,
        CatalogCategory.WidgetTheme => Theme == item.Key,
        CatalogCategory.Scene => Scene == item.Key,
        CatalogCategory.Ride => Ride == item.Key,
        _ => false,
    };

    public int OutfitsOwned => Owned.Count(k => k.StartsWith(Catalog.OutfitPrefix, StringComparison.Ordinal));

    public Inventory With(CatalogItem item)
    {
        var owned = new HashSet<string>(Owned, StringComparer.Ordinal) { item.Key };
        return this with { Owned = owned };
    }

    public bool Equals(Inventory? other)
        => other is not null && Outfit == other.Outfit && Frame == other.Frame && Theme == other.Theme && Scene == other.Scene && Ride == other.Ride && Owned.SetEquals(other.Owned);

    public override int GetHashCode() => HashCode.Combine(Owned.Count, Outfit, Frame, Theme, Scene, Ride);
}
