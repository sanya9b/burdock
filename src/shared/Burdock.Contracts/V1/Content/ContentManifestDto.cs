namespace Burdock.Contracts.V1.Content;

/// <summary>Manifest of content packs available to the caller, filtered by entitlement (spec 09 §3.4, T25).</summary>
public sealed record ContentManifestDto(
    int SchemaVersion,
    int ManifestVersion,
    DateTimeOffset GeneratedAt,
    string Pair,
    IReadOnlyList<ContentPackDto> Packs);

/// <summary>One immutable pack: JSON.gz per level and topic behind the CDN, addressed by an unlisted versioned path and verified by SHA-256 of the object.</summary>
public sealed record ContentPackDto(
    string Id,
    string Level,
    string Topic,
    int Version,
    string Sha256,
    long SizeBytes,
    Uri Url,
    string MinAppVersion,
    bool Required,
    DateOnly? DeprecatedAfter,
    IReadOnlyList<AttributionDto> Attributions);

/// <summary>Source attribution the app shows on the licences screen (spec 08 §3.1, content/sources/README.md).</summary>
public sealed record AttributionDto(string Source, string License, string? Text, string? Url);
