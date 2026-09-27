namespace Burdock.Contracts.V1.Content;

/// <summary>Manifest of content packs available to the caller, filtered by entitlement (spec 09 §3.5, T25).</summary>
public sealed record ContentManifestDto(
    int SchemaVersion,
    int ManifestVersion,
    DateTimeOffset GeneratedAt,
    string Pair,
    IReadOnlyList<ContentPackDto> Packs);

/// <summary>One immutable pack: JSON.gz per level and topic, addressed by content hash.</summary>
public sealed record ContentPackDto(
    string Id,
    string Level,
    string Topic,
    int Version,
    string Sha256,
    long SizeBytes,
    Uri Url,
    string MinAppVersion,
    bool Required);
