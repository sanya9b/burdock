using Burdock.Domain.Vocabulary;

namespace Burdock.Domain.Learning;

/// <summary>One answer of the placement mini-test: the level of the word shown and whether the learner claims to know it.</summary>
public readonly record struct PlacementAnswer(CefrLevel Level, bool Known);

/// <summary>
/// Placement mini-test (spec 03 S-03a, Onboarding.QuizQuestions = 8): picks words spread over the levels a pool offers and
/// turns «знаю / не знаю» answers into a suggested CEFR level. Deterministic for a seed, so a restarted test looks the same.
/// </summary>
public static class LevelPlacement
{
    /// <summary>Onboarding.QuizQuestions</summary>
    public const int QuestionCount = 8;

    /// <summary>A level counts as reached when the learner knows at least this share of its words.</summary>
    public const double PassShare = 0.5;

    /// <summary>
    /// Chooses up to <paramref name="count"/> items: slots go round-robin to the pool's levels from the lowest up, words are
    /// drawn at random (seeded) within a level, and the result is ordered easy → hard.
    /// </summary>
    public static IReadOnlyList<T> Pick<T>(IReadOnlyList<T> pool, Func<T, CefrLevel> levelOf, int seed, int count = QuestionCount)
    {
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentNullException.ThrowIfNull(levelOf);
        if (count <= 0 || pool.Count == 0)
        {
            return [];
        }

        var random = new Random(seed);
        var byLevel = pool.GroupBy(levelOf).OrderBy(g => g.Key).Select(g => Shuffle(g.ToList(), random)).ToList();
        var quota = new int[byLevel.Count];
        var remaining = Math.Min(count, pool.Count);
        while (remaining > 0)
        {
            var progressed = false;
            for (var i = 0; i < byLevel.Count && remaining > 0; i++)
            {
                if (quota[i] < byLevel[i].Count)
                {
                    quota[i]++;
                    remaining--;
                    progressed = true;
                }
            }

            if (!progressed)
            {
                break;
            }
        }

        var picked = new List<T>(count);
        for (var i = 0; i < byLevel.Count; i++)
        {
            picked.AddRange(byLevel[i].Take(quota[i]));
        }

        return picked;
    }

    /// <summary>
    /// Walks the quizzed levels from the lowest: a level is reached when at least <see cref="PassShare"/> of its words are known,
    /// and the walk stops at the first level that is not. Knowing every word of the top quizzed level suggests one level above it
    /// (the test had nothing harder to offer), capped at C2. No answers or nothing reached → A1.
    /// </summary>
    public static CefrLevel Estimate(IReadOnlyList<PlacementAnswer> answers)
    {
        ArgumentNullException.ThrowIfNull(answers);
        var levels = answers
            .GroupBy(a => a.Level)
            .OrderBy(g => g.Key)
            .Select(g => (Level: g.Key, Known: g.Count(a => a.Known), Total: g.Count()))
            .ToList();
        if (levels.Count == 0)
        {
            return CefrLevel.A1;
        }

        CefrLevel? reached = null;
        foreach (var (level, known, total) in levels)
        {
            if (known < total * PassShare)
            {
                break;
            }

            reached = level;
        }

        if (reached is null)
        {
            return CefrLevel.A1;
        }

        var top = levels[^1];
        if (reached == top.Level && top.Known == top.Total && top.Level < CefrLevel.C2)
        {
            return (CefrLevel)((int)top.Level + 1);
        }

        return reached.Value;
    }

    private static List<T> Shuffle<T>(List<T> items, Random random)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }

        return items;
    }
}
