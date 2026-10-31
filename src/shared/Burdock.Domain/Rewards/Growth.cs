namespace Burdock.Domain.Rewards;

/// <summary>
/// Visible growth stage of the mascot (spec 07 §3.2.7, decision log item 17). A pure function of the XP level: the burr never shrinks,
/// never wilts and never asks to be fed; it only grows as the learner earns XP through learning actions.
/// </summary>
public enum GrowthStage
{
    Seed = 1,
    Sprout = 2,
    Leaf = 3,
    Bush = 4,
    Bud = 5,
    Burr = 6,
    Bloom = 7,
    GreatBurr = 8,
}

/// <summary>Where the mascot stands on its way from seed to the great burr.</summary>
/// <param name="Stage">Current stage.</param>
/// <param name="Level">XP level the stage was derived from.</param>
/// <param name="Xp">Total XP.</param>
/// <param name="Progress">0..1 inside the current stage (XP between the first level of this stage and the first level of the next; the last stage saturates at level 30).</param>
/// <param name="NextStage">Null at the last stage.</param>
/// <param name="XpToNextStage">XP still missing until <see cref="NextStage"/>; 0 at the last stage.</param>
public sealed record GrowthSnapshot(GrowthStage Stage, int Level, int Xp, double Progress, GrowthStage? NextStage, int XpToNextStage)
{
    public bool IsFinal => NextStage is null;

    /// <summary>0..1 across all stages, for continuous rendering: seed at 0, great burr fully grown at 1.</summary>
    public double Scalar => ((int)Stage - 1 + Progress) / GrowthRules.StageCount;
}

/// <summary>Stage boundaries on the level ladder of spec 07 §3.2.1; the stage names are the titles of their first levels.</summary>
public static class GrowthRules
{
    public const int StageCount = 8;

    /// <summary>First level of each stage, indexed by stage − 1: Насінинка 1, Паросток 2, Листочок 4, Кущик 5, Бутончик 6, Реп’яшок 7, Цвіт реп’яха 10, Великий Реп’ях 20.</summary>
    private static readonly int[] FirstLevels = [1, 2, 4, 5, 6, 7, 10, 20];

    /// <summary>The grown-up form shown when gamification is off (spec 07 §3.9): the burr itself, halfway through its stage.</summary>
    public static GrowthSnapshot Adult { get; } = new(GrowthStage.Burr, FirstLevel(GrowthStage.Burr), XpRules.Threshold(FirstLevel(GrowthStage.Burr)), 0.5, GrowthStage.Bloom, 0);

    public static int FirstLevel(GrowthStage stage)
    {
        var index = (int)stage - 1;
        ArgumentOutOfRangeException.ThrowIfNegative(index, nameof(stage));
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, StageCount, nameof(stage));
        return FirstLevels[index];
    }

    public static GrowthStage StageFor(int level)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);
        var stage = GrowthStage.Seed;
        for (var i = 1; i < StageCount; i++)
        {
            if (level >= FirstLevels[i])
            {
                stage = (GrowthStage)(i + 1);
            }
        }

        return stage;
    }

    public static GrowthStage? Next(GrowthStage stage) => stage == GrowthStage.GreatBurr ? null : stage + 1;

    public static GrowthSnapshot For(int xp)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(xp);
        var level = XpRules.LevelFor(xp);
        var stage = StageFor(level);
        var next = Next(stage);
        var start = XpRules.Threshold(FirstLevel(stage));
        var end = next is { } n ? XpRules.Threshold(FirstLevel(n)) : XpRules.Threshold(XpRules.MaxLevel);
        var progress = end > start ? Math.Clamp((double)(xp - start) / (end - start), 0, 1) : 1;
        var toNext = next is null ? 0 : Math.Max(0, end - xp);
        return new GrowthSnapshot(stage, level, xp, progress, next, toNext);
    }
}
