using Burdock.Domain.Learning;

namespace Burdock.Domain.Rewards;

/// <summary>Experience points (spec 07 §3.2.1): never spent, never reset. Effort counts: an «Again» still earns.</summary>
public static class XpRules
{
    public const int GradedCardAgain = 1;
    public const int GradedCardHardOrBetter = 2;
    public const int SessionWithLearningDay = 10;
    public const int WarmupCompleted = 5;
    public const int Achievement = 25;
    public const int MaxLevel = 30;

    public static int ForGradedCard(Rating rating) => rating == Rating.Again ? GradedCardAgain : GradedCardHardOrBetter;

    /// <summary>XP required to reach a level: 25·(L−1)·(L+2); level 1 = 0, level 2 = 100, level 10 = 2 700, level 30 = 23 200.</summary>
    public static int Threshold(int level)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);
        return 25 * (level - 1) * (level + 2);
    }

    public static int LevelFor(int xp)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(xp);
        var level = 1;
        while (level < MaxLevel && xp >= Threshold(level + 1))
        {
            level++;
        }

        return level;
    }

    /// <summary>Progress inside the current level as (earned, needed); (earned, 0) at the cap.</summary>
    public static (int Earned, int Needed) LevelProgress(int xp)
    {
        var level = LevelFor(xp);
        if (level >= MaxLevel)
        {
            return (xp - Threshold(MaxLevel), 0);
        }

        var floor = Threshold(level);
        return (xp - floor, Threshold(level + 1) - floor);
    }
}
