using System.Globalization;
using System.Text;
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

    public static string Days(int days) => Plural.Uk(days, DayOne, DayFew, DayMany);

    /// <summary>«Серія: 3 дні» (spec 07 §3.3.3 allowed copy).</summary>
    public static string StreakDays(int days) => string.Format(Ukrainian, StreakFormat, Days(days));

    public static string Thorns(int amount) => Plural.Uk(amount, ThornsOne, ThornsFew, ThornsMany);

    public static string ThornsReward(int amount) => Plural.Uk(amount, RewardOne, RewardFew, RewardMany);

    public static string LevelTitle(int level)
        => level >= 1 && level <= LevelTitles.Length ? LevelTitles[level - 1] : LevelTitles[^1];

    /// <summary>«Рівень 2 · Паросток».</summary>
    public static string Level(int level) => string.Format(Ukrainian, LevelFormat, level, LevelTitle(level));

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
