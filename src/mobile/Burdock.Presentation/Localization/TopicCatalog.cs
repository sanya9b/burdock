using Burdock.Presentation.Resources.Strings;

namespace Burdock.Presentation.Localization;

/// <summary>The eight launch categories (decision Q18) in display order with their UI labels (glossary «Категорія»).</summary>
internal static class TopicCatalog
{
    public static IReadOnlyList<string> Slugs { get; } = ["communication", "travel", "work", "it", "food", "health", "city", "shopping"];

    public static string Label(string slug) => slug switch
    {
        "communication" => AppResources.Topic_communication,
        "travel" => AppResources.Topic_travel,
        "work" => AppResources.Topic_work,
        "it" => AppResources.Topic_it,
        "food" => AppResources.Topic_food,
        "health" => AppResources.Topic_health,
        "city" => AppResources.Topic_city,
        "shopping" => AppResources.Topic_shopping,
        _ => slug,
    };
}
