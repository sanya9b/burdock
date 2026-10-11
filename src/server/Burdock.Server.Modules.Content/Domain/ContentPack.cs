using System.Globalization;
using Burdock.Domain.Entitlements;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;

namespace Burdock.Server.Modules.Content.Domain;

public enum PackStatus
{
    Published,
    Deprecated,
}

/// <summary>Source attribution carried by a pack into the manifest (spec 09 §3.4, spec 08 §3.1).</summary>
public sealed record PackAttribution(string Source, string License, string? Text, string? Url);

/// <summary>What identifies a published object: ids follow T25 (`uk-en.{level}.{topic}`, `uk-en.free-pool`).</summary>
public sealed record PackIdentity(string PackId, LanguagePair Pair, string Level, string Topic, int Version);

/// <summary>
/// A published, immutable content pack (spec 09 §3.4, T8/T25): one row per (id, version); publishing a newer version
/// deprecates the older ones, which stay downloadable until <see cref="DeprecatedAfter"/>.
/// </summary>
public sealed class ContentPack
{
    public const string FreePoolTopic = "free-pool";
    public const int Sha256HexLength = 64;
    public const int MaxLevelLength = 16;
    public const int MaxTopicLength = 32;
    public const int MaxPackIdLength = 64;

    private ContentPack(PackIdentity identity, string sha256, long sizeBytes, string objectKey, string minAppVersion, PackStatus status, DateTimeOffset publishedAt, DateOnly? deprecatedAfter, IReadOnlyList<PackAttribution> attributions)
    {
        Identity = identity;
        Sha256 = sha256;
        SizeBytes = sizeBytes;
        ObjectKey = objectKey;
        MinAppVersion = minAppVersion;
        Status = status;
        PublishedAt = publishedAt;
        DeprecatedAfter = deprecatedAfter;
        Attributions = attributions;
    }

    public PackIdentity Identity { get; }

    public string PackId => Identity.PackId;

    public LanguagePair Pair => Identity.Pair;

    public string Level => Identity.Level;

    public string Topic => Identity.Topic;

    public int Version => Identity.Version;

    /// <summary>SHA-256 (lower-case hex) of the stored object, which is the gzipped JSON the client downloads and verifies.</summary>
    public string Sha256 { get; }

    public long SizeBytes { get; }

    public string ObjectKey { get; }

    public string MinAppVersion { get; }

    public PackStatus Status { get; private set; }

    public DateTimeOffset PublishedAt { get; }

    public DateOnly? DeprecatedAfter { get; private set; }

    public IReadOnlyList<PackAttribution> Attributions { get; }

    public bool IsFreePool => string.Equals(Topic, FreePoolTopic, StringComparison.Ordinal);

    public static Result<ContentPack> Publish(PackIdentity identity, string sha256, long sizeBytes, string objectKey, string minAppVersion, IReadOnlyList<PackAttribution> attributions, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(attributions);
        var identityCheck = Validate(identity);
        if (identityCheck.IsFailure)
        {
            return Result.Failure<ContentPack>(identityCheck.Error);
        }

        if (sha256 is not { Length: Sha256HexLength } || !sha256.All(char.IsAsciiHexDigitLower))
        {
            return Result.Failure<ContentPack>(DomainError.Validation("content.pack_sha256_invalid", "Errors_Content_PackSha256Invalid"));
        }

        if (sizeBytes <= 0 || string.IsNullOrWhiteSpace(objectKey) || string.IsNullOrWhiteSpace(minAppVersion))
        {
            return Result.Failure<ContentPack>(DomainError.Validation("content.pack_invalid", "Errors_Content_PackInvalid"));
        }

        return Result.Success(new ContentPack(identity, sha256, sizeBytes, objectKey, minAppVersion.Trim(), PackStatus.Published, now, null, attributions));
    }

    public static ContentPack Restore(PackIdentity identity, string sha256, long sizeBytes, string objectKey, string minAppVersion, PackStatus status, DateTimeOffset publishedAt, DateOnly? deprecatedAfter, IReadOnlyList<PackAttribution> attributions)
        => new(identity, sha256, sizeBytes, objectKey, minAppVersion, status, publishedAt, deprecatedAfter, attributions);

    /// <summary>The free pool is visible to everyone; every other pack needs a state that unlocks Pro (spec 09 §3.4).</summary>
    public bool IsVisibleTo(EntitlementState state) => Status == PackStatus.Published && (IsFreePool || state.UnlocksPro());

    public void Deprecate(DateOnly after)
    {
        Status = PackStatus.Deprecated;
        DeprecatedAfter = after;
    }

    /// <summary>Unlisted, versioned object path (spec 09 §3.4): the manifest is the only place these URLs are revealed.</summary>
    public static string ObjectKeyFor(PackIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        return string.Create(CultureInfo.InvariantCulture, $"packs/v1/{identity.Pair}/{identity.Level.ToLowerInvariant()}/{identity.Topic}/{identity.PackId}-{identity.Version}.json.gz");
    }

    private static Result Validate(PackIdentity identity)
    {
        var pairPrefix = identity.Pair.ToString() + ".";
        var idOk = identity.PackId.Length is > 0 and <= MaxPackIdLength
            && identity.PackId.StartsWith(pairPrefix, StringComparison.Ordinal)
            && identity.PackId.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c is '.' or '-');
        var levelOk = identity.Level.Length is > 0 and <= MaxLevelLength && identity.Level.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');
        var topicOk = identity.Topic.Length is > 0 and <= MaxTopicLength && identity.Topic.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-');
        return idOk && levelOk && topicOk && identity.Version >= 1
            ? Result.Success()
            : Result.Failure(DomainError.Validation("content.pack_identity_invalid", "Errors_Content_PackIdentityInvalid", new Dictionary<string, string>(StringComparer.Ordinal) { ["packId"] = identity.PackId }));
    }
}
