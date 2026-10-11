using System.IO.Compression;
using System.Security.Cryptography;
using Burdock.Domain.SharedKernel;
using Burdock.Server.Modules.Content.Domain;
using Burdock.Server.Shared.Time;

namespace Burdock.Server.Modules.Content.Application;

public sealed record PublishPackCommand(PackIdentity Identity, ReadOnlyMemory<byte> Json, string MinAppVersion, IReadOnlyList<PackAttribution> Attributions);

public sealed record PublishedPack(ContentPack Pack, bool AlreadyPublished);

/// <summary>
/// `api content publish` (spec 09 §3.4, FR-09-8): gzips the pack, stores it under its unlisted versioned key, records the
/// row with the SHA-256 of the object and deprecates older versions of the same pack. Publishing the same version with
/// identical content is a no-op; with different content it is a conflict, because published objects never change.
/// </summary>
public sealed class PublishPackUseCase(IPackCatalog catalog, IPackObjectStore store, IClock clock, ContentOptions options)
{
    public const string ContentType = "application/gzip";

    public async Task<Result<PublishedPack>> ExecuteAsync(PublishPackCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Json.IsEmpty)
        {
            return Result.Failure<PublishedPack>(DomainError.Validation("content.pack_empty", "Errors_Content_PackEmpty"));
        }

        var gzip = Compress(command.Json.Span);
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(gzip));
        var identity = command.Identity;

        var existing = await catalog.GetAsync(identity.PackId, identity.Version, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return string.Equals(existing.Sha256, sha256, StringComparison.Ordinal)
                ? Result.Success(new PublishedPack(existing, AlreadyPublished: true))
                : Result.Failure<PublishedPack>(DomainError.Conflict("content.pack_version_conflict", "Errors_Content_PackVersionConflict", new Dictionary<string, string>(StringComparer.Ordinal) { ["packId"] = identity.PackId, ["version"] = identity.Version.ToString(System.Globalization.CultureInfo.InvariantCulture) }));
        }

        var now = clock.UtcNow;
        var key = ContentPack.ObjectKeyFor(identity);
        var pack = ContentPack.Publish(identity, sha256, gzip.Length, key, command.MinAppVersion, command.Attributions, now);
        if (pack.IsFailure)
        {
            return Result.Failure<PublishedPack>(pack.Error);
        }

        await store.PutAsync(key, gzip, ContentType, cancellationToken).ConfigureAwait(false);
        await catalog.AddAsync(pack.Value, cancellationToken).ConfigureAwait(false);

        var retireAfter = DateOnly.FromDateTime(now.UtcDateTime).AddDays(options.DeprecatedRetentionDays);
        foreach (var older in (await catalog.ListAsync(identity.Pair, cancellationToken).ConfigureAwait(false))
                     .Where(p => string.Equals(p.PackId, identity.PackId, StringComparison.Ordinal) && p.Version < identity.Version && p.Status == PackStatus.Published))
        {
            older.Deprecate(retireAfter);
            await catalog.UpdateAsync(older, cancellationToken).ConfigureAwait(false);
        }

        await catalog.CommitAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new PublishedPack(pack.Value, AlreadyPublished: false));
    }

    /// <summary>Deterministic gzip (no timestamp, no file name) so the same JSON always yields the same object and hash.</summary>
    internal static byte[] Compress(ReadOnlySpan<byte> json)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            gzip.Write(json);
        }

        return output.ToArray();
    }
}
