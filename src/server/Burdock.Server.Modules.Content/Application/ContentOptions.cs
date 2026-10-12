using Burdock.Server.Shared.Storage;

namespace Burdock.Server.Modules.Content.Application;

/// <summary>«Content» section (spec 09 §3.4, §3.15): the pack object store (shared shape) plus manifest settings.</summary>
public sealed class ContentOptions : ObjectStoreOptions
{
    public const string SectionName = "Content";

    public ContentOptions()
    {
        StorePath = "data/packs";
    }

    /// <summary>Exposes GET /v1/content/store/{key} so a developer machine can download what it published; never on in production.</summary>
    public bool ServeStoreLocally { get; set; }

    /// <summary>Content:DeprecatedRetentionDays (spec 09 §3.15): how long a superseded version stays downloadable.</summary>
    public int DeprecatedRetentionDays { get; set; } = 180;
}
