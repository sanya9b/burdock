namespace Burdock.Server.Modules.Content.Application;

/// <summary>«Content» section (spec 09 §3.4, §3.15).</summary>
public sealed class ContentOptions
{
    public const string SectionName = "Content";
    public const string FileSystemStore = "FileSystem";
    public const string S3Store = "S3";

    /// <summary><see cref="FileSystemStore"/> (development, tests) or <see cref="S3Store"/> (Hetzner Object Storage).</summary>
    public string ObjectStore { get; set; } = FileSystemStore;

    /// <summary>Folder of the file store; relative paths resolve against the content root.</summary>
    public string StorePath { get; set; } = "data/packs";

    /// <summary>Base of the URLs written into the manifest (https://cdn.burdock.org/). Unset in development points at the API's own store route.</summary>
    public Uri? PublicBaseUrl { get; set; }

    /// <summary>Exposes GET /v1/content/store/{key} so a developer machine can download what it published; never on in production.</summary>
    public bool ServeStoreLocally { get; set; }

    /// <summary>Content:DeprecatedRetentionDays (spec 09 §3.15): how long a superseded version stays downloadable.</summary>
    public int DeprecatedRetentionDays { get; set; } = 180;

    public S3Options S3 { get; set; } = new();

    public sealed class S3Options
    {
        public Uri? ServiceUrl { get; set; }

        public string Bucket { get; set; } = "burdock-content";

        public string Region { get; set; } = "eu-central";

        public string? AccessKeyId { get; set; }

        public string? SecretAccessKey { get; set; }
    }
}
