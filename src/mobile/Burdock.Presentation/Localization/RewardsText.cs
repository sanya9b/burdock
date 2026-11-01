using System.Globalization;
using System.Text;
using Burdock.Domain.Rewards;
using Burdock.Presentation.Resources.Icons;
using Burdock.Presentation.Resources.Strings;

namespace Burdock.Presentation.Localization;

/// <summary>Ukrainian wording of the economy (spec 07 §3.2.1 titles, §3.3.3 streak copy, §3.6 achievements).</summary>
internal static class RewardsText
{
    private static readonly CultureInfo Ukrainian = CultureInfo.GetCultureInfo("uk-UA");
    private static readonly CompositeFormat DayOne = CompositeFormat.Parse(AppResources.Rewards_Days_One);
    private static readonly CompositeFormat DayFew = CompositeFormat.Parse(AppResources.Rewards_Days_Few);
    private static readonly CompositeFormat DayMany = CompositeFormat.Parse(AppResources.Rewards_Days_Many);
    private static readonly CompositeFormat StreakFormat = CompositeFormat.Parse(AppResources.Rewards_Streak_Format);
    private static readonly CompositeFormat LevelFormat = CompositeFormat.Parse(AppResources.Rewards_Level_Format);
    private static readonly CompositeFormat ThornsOne = CompositeFormat.Parse(AppResources.Rewards_Thorns_One);
    private static readonly CompositeFormat ThornsFew = CompositeFormat.Parse(AppResources.Rewards_Thorns_Few);
    private static readonly CompositeFormat ThornsMany = CompositeFormat.Parse(AppResources.Rewards_Thorns_Many);
    private static readonly CompositeFormat RewardOne = CompositeFormat.Parse(AppResources.Celebration_Reward_One);
    private static readonly CompositeFormat RewardFew = CompositeFormat.Parse(AppResources.Celebration_Reward_Few);
    private static readonly CompositeFormat RewardMany = CompositeFormat.Parse(AppResources.Celebration_Reward_Many);
    private static readonly string[] LevelTitles = AppResources.Rewards_Level_Titles.Split('|', StringSplitOptions.TrimEntries);
    private static readonly CompositeFormat LevelShortFormat = CompositeFormat.Parse(AppResources.Rewards_Level_Short);
    private static readonly CompositeFormat GrowthToNextFormat = CompositeFormat.Parse(AppResources.Growth_ToNext);
    private static readonly CompositeFormat GrowthA11yFormat = CompositeFormat.Parse(AppResources.Growth_A11y);
    private static readonly CompositeFormat ShopBuyFormat = CompositeFormat.Parse(AppResources.Shop_Buy_Button);
    private static readonly CompositeFormat ShopMissingFormat = CompositeFormat.Parse(AppResources.Shop_Missing);
    private static readonly CompositeFormat UnlockLevelFormat = CompositeFormat.Parse(AppResources.Shop_Unlock_Level);
    private static readonly CompositeFormat UnlockStreakFormat = CompositeFormat.Parse(AppResources.Shop_Unlock_Streak);
    private static readonly CompositeFormat UnlockWordsFormat = CompositeFormat.Parse(AppResources.Shop_Unlock_Words);
    private static readonly CompositeFormat UnlockDaysFormat = CompositeFormat.Parse(AppResources.Shop_Unlock_Days);
    private static readonly CompositeFormat WordsOne = CompositeFormat.Parse(AppResources.Shop_Words_One);
    private static readonly CompositeFormat WordsFew = CompositeFormat.Parse(AppResources.Shop_Words_Few);
    private static readonly CompositeFormat WordsMany = CompositeFormat.Parse(AppResources.Shop_Words_Many);
    private static readonly CompositeFormat FreezeBankedFormat = CompositeFormat.Parse(AppResources.Shop_Freeze_Banked);
    private static readonly CompositeFormat LockLevelFormat = CompositeFormat.Parse(AppResources.Shop_Lock_Level);
    private static readonly CompositeFormat LockStreakFormat = CompositeFormat.Parse(AppResources.Shop_Lock_Streak);
    private static readonly CompositeFormat LockWordsFormat = CompositeFormat.Parse(AppResources.Shop_Lock_Words);
    private static readonly CompositeFormat LockDaysFormat = CompositeFormat.Parse(AppResources.Shop_Lock_Days);

    public static string Days(int days) => Plural.Uk(days, DayOne, DayFew, DayMany);

    /// <summary>«Серія: 3 дні» (spec 07 §3.3.3 allowed copy).</summary>
    public static string StreakDays(int days) => string.Format(Ukrainian, StreakFormat, Days(days));

    public static string Thorns(int amount) => Plural.Uk(amount, ThornsOne, ThornsFew, ThornsMany);

    public static string ThornsReward(int amount) => Plural.Uk(amount, RewardOne, RewardFew, RewardMany);

    public static string LevelTitle(int level)
        => level >= 1 && level <= LevelTitles.Length ? LevelTitles[level - 1] : LevelTitles[^1];

    /// <summary>«Рівень 2 · Паросток».</summary>
    public static string Level(int level) => string.Format(Ukrainian, LevelFormat, level, LevelTitle(level));

    /// <summary>«Рівень 5».</summary>
    public static string LevelShort(int level) => string.Format(Ukrainian, LevelShortFormat, level);

    /// <summary>Stage names are the titles of the first level of each stage (spec 07 §3.2.7): «Насінинка», «Паросток», … «Великий Реп’ях».</summary>
    public static string StageName(GrowthStage stage) => LevelTitle(GrowthRules.FirstLevel(stage));

    /// <summary>«Ще 300 XP до стадії «Бутончик»» or «Найвища стадія росту».</summary>
    public static string GrowthCaption(GrowthSnapshot growth)
    {
        ArgumentNullException.ThrowIfNull(growth);
        return growth.NextStage is { } next
            ? string.Format(Ukrainian, GrowthToNextFormat, growth.XpToNextStage.ToString("N0", Ukrainian), StageName(next))
            : AppResources.Growth_Final;
    }

    /// <summary>Screen-reader description of the mascot: «Реп’яшок на стадії «Кущик»».</summary>
    public static string GrowthDescription(GrowthStage stage) => string.Format(Ukrainian, GrowthA11yFormat, StageName(stage));

    /// <summary>Name of a catalogue item from the resx («Шарфик», «Ніч», «Золота»); unknown keys show their slug.</summary>
    public static string ItemName(CatalogItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var category = item.Category switch
        {
            CatalogCategory.WidgetTheme => "theme",
            CatalogCategory.Outfit => "outfit",
            CatalogCategory.Frame => "frame",
            _ => "freeze",
        };
        return AppResources.ResourceManager.GetString($"Catalog_{category}_{item.Slug}", AppResources.Culture) ?? item.Slug;
    }

    /// <summary>«150» for a priced item, «Подарунок» for a free one.</summary>
    public static string Price(CatalogItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Price == 0 ? AppResources.Shop_Gift : item.Price.ToString("N0", Ukrainian);
    }

    /// <summary>«Купити за 150 колючок».</summary>
    public static string BuyButton(int price) => string.Format(Ukrainian, ShopBuyFormat, Thorns(price));

    /// <summary>«Не вистачає 40 колючок».</summary>
    public static string Missing(int missing) => string.Format(Ukrainian, ShopMissingFormat, Thorns(missing));

    /// <summary>Open unlock condition, never with a deadline (spec 07 §3.2.3).</summary>
    public static string Unlock(UnlockCondition condition) => condition.Kind switch
    {
        UnlockKind.Level => string.Format(Ukrainian, UnlockLevelFormat, condition.Threshold),
        UnlockKind.Streak => string.Format(Ukrainian, UnlockStreakFormat, Days(condition.Threshold)),
        UnlockKind.WordsLearned => string.Format(Ukrainian, UnlockWordsFormat, Plural.Uk(condition.Threshold, WordsOne, WordsFew, WordsMany)),
        UnlockKind.LearningDays => string.Format(Ukrainian, UnlockDaysFormat, condition.Threshold),
        UnlockKind.Sessions => AppResources.Shop_Unlock_Sessions,
        _ => string.Empty,
    };

    /// <summary>Short unlock label for a tile: «Рівень 20», «Серія 7 днів», «50 слів у «Вивчено»».</summary>
    public static string UnlockShort(UnlockCondition condition) => condition.Kind switch
    {
        UnlockKind.Level => string.Format(Ukrainian, LockLevelFormat, condition.Threshold),
        UnlockKind.Streak => string.Format(Ukrainian, LockStreakFormat, Days(condition.Threshold)),
        UnlockKind.WordsLearned => string.Format(Ukrainian, LockWordsFormat, Plural.Uk(condition.Threshold, WordsOne, WordsFew, WordsMany)),
        UnlockKind.LearningDays => string.Format(Ukrainian, LockDaysFormat, condition.Threshold),
        UnlockKind.Sessions => AppResources.Shop_Lock_Sessions,
        _ => string.Empty,
    };

    /// <summary>«У запасі 1 з 2».</summary>
    public static string FreezeBanked(int banked, int max) => string.Format(Ukrainian, FreezeBankedFormat, banked, max);

    public static string AchievementTitle(string id) => AppResources.ResourceManager.GetString($"Ach_{id}_Title", AppResources.Culture) ?? id;

    public static string AchievementBody(string id) => AppResources.ResourceManager.GetString($"Ach_{id}_Body", AppResources.Culture) ?? string.Empty;

    /// <summary>Lucide glyph for a catalogue icon name; icons we do not bundle fall back to a related one.</summary>
    public static string AchievementGlyph(string icon) => icon switch
    {
        "flame" => Icon.Flame,
        "calendar-check" => Icon.CalendarCheck,
        "medal" => Icon.Medal,
        "award" => Icon.Award,
        "sprout" => Icon.Sprout,
        "trees" => Icon.Leaf,
        "mountain" => Icon.Flag,
        "keyboard" => Icon.Keyboard,
        "repeat" => Icon.Repeat,
        "flower" => Icon.Sparkles,
        "crown" => Icon.Crown,
        "sparkles" => Icon.Sparkles,
        "footprints" => Icon.Rocket,
        _ => Icon.Award,
    };
}
