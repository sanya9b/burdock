using Burdock.Domain.Rewards;

namespace Burdock.Presentation.Controls;

/// <summary>Outfits of the catalogue (spec 07 §3.2.3) the procedural mascot can wear; drawn as layers over any mood and stage (spec 02 §3.10).</summary>
public enum MascotOutfit
{
    None,
    Scarf,
    Glasses,
    StrawHat,
    Backpack,
    Raincoat,
    Wreath,
    Sweater,
    Bandana,
    Vyshyvanka,
    SpaceHelmet,
    Crown,
}

/// <summary>Avatar frames of the catalogue: a ring around the mascot on the profile and in the shop preview.</summary>
public enum MascotFrame
{
    None,
    Leaf,
    Lime,
    Violet,
    Coral,
    Sunny,
    Sky,
    Gold,
}

/// <summary>Maps catalogue keys to the drawable cosmetics; unknown or absent keys mean «nothing worn».</summary>
public static class MascotCosmetics
{
    public static MascotOutfit OutfitFor(string? itemKey) => Slug(itemKey, Catalog.OutfitPrefix) switch
    {
        "scarf" => MascotOutfit.Scarf,
        "glasses" => MascotOutfit.Glasses,
        "strawhat" => MascotOutfit.StrawHat,
        "backpack" => MascotOutfit.Backpack,
        "raincoat" => MascotOutfit.Raincoat,
        "wreath" => MascotOutfit.Wreath,
        "sweater" => MascotOutfit.Sweater,
        "bandana" => MascotOutfit.Bandana,
        "vyshyvanka" => MascotOutfit.Vyshyvanka,
        "spacehelmet" => MascotOutfit.SpaceHelmet,
        "crown" => MascotOutfit.Crown,
        _ => MascotOutfit.None,
    };

    public static MascotFrame FrameFor(string? itemKey) => Slug(itemKey, Catalog.FramePrefix) switch
    {
        "leaf" => MascotFrame.Leaf,
        "lime" => MascotFrame.Lime,
        "violet" => MascotFrame.Violet,
        "coral" => MascotFrame.Coral,
        "sunny" => MascotFrame.Sunny,
        "sky" => MascotFrame.Sky,
        "gold" => MascotFrame.Gold,
        _ => MascotFrame.None,
    };

    private static string? Slug(string? key, string prefix)
        => key is not null && key.StartsWith(prefix, StringComparison.Ordinal) ? key[prefix.Length..] : null;
}
