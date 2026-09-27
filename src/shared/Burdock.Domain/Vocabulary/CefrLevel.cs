namespace Burdock.Domain.Vocabulary;

/// <summary>CEFR proficiency level of a lexeme or a learner (decision Q1 scope: A1-C2).</summary>
public enum CefrLevel
{
    A1 = 1,
    A2 = 2,
    B1 = 3,
    B2 = 4,
    C1 = 5,
    C2 = 6,
}

public static class CefrLevelExtensions
{
    /// <summary>All levels in ascending order.</summary>
    public static IReadOnlyList<CefrLevel> All { get; } = [CefrLevel.A1, CefrLevel.A2, CefrLevel.B1, CefrLevel.B2, CefrLevel.C1, CefrLevel.C2];

    /// <summary>Levels that make up the free pool: A1-B1 (decision Q14/Q32).</summary>
    public static IReadOnlyList<CefrLevel> FreePool { get; } = [CefrLevel.A1, CefrLevel.A2, CefrLevel.B1];

    /// <summary>Lower-case slug used in pack ids and routes ("a1").</summary>
    public static string ToSlug(this CefrLevel level) => level.ToString().ToLowerInvariant();

    /// <summary>The level itself plus its immediate neighbours, used for spill-over when a level runs dry (spec 06 §3.5).</summary>
    public static IReadOnlyList<CefrLevel> WithNeighbours(this CefrLevel level)
    {
        var index = (int)level - 1;
        var lower = Math.Max(0, index - 1);
        var upper = Math.Min(All.Count - 1, index + 1);
        return All.Skip(lower).Take(upper - lower + 1).ToArray();
    }
}
