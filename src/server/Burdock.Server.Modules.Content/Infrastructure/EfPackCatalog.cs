using System.Text.Json;
using Burdock.Domain.Vocabulary;
using Burdock.Server.Modules.Content.Application;
using Burdock.Server.Modules.Content.Domain;
using Microsoft.EntityFrameworkCore;

namespace Burdock.Server.Modules.Content.Infrastructure;

/// <summary>EF Core catalogue over the scoped <see cref="ContentDbContext"/>; <see cref="CommitAsync"/> is the unit of work.</summary>
public sealed class EfPackCatalog(ContentDbContext db) : IPackCatalog
{
    private static readonly JsonSerializerOptions AttributionJson = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<ContentPack>> ListAsync(LanguagePair pair, CancellationToken cancellationToken)
    {
        var key = pair.ToString();
        var rows = await db.Packs.Where(p => p.Pair == key).OrderBy(p => p.PackId).ThenBy(p => p.Version).ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.Select(ToDomain).ToArray();
    }

    public async Task<ContentPack?> GetAsync(string packId, int version, CancellationToken cancellationToken)
    {
        var row = await db.Packs.FindAsync([packId, version], cancellationToken).ConfigureAwait(false);
        return row is null ? null : ToDomain(row);
    }

    public Task AddAsync(ContentPack pack, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pack);
        db.Packs.Add(new PackRow
        {
            PackId = pack.PackId,
            Version = pack.Version,
            Pair = pack.Pair.ToString(),
            Level = pack.Level,
            Topic = pack.Topic,
            Sha256 = pack.Sha256,
            SizeBytes = pack.SizeBytes,
            ObjectKey = pack.ObjectKey,
            MinAppVersion = pack.MinAppVersion,
            Status = pack.Status.ToString(),
            PublishedAt = pack.PublishedAt,
            DeprecatedAfter = pack.DeprecatedAfter,
            AttributionsJson = JsonSerializer.Serialize(pack.Attributions, AttributionJson),
        });
        return Task.CompletedTask;
    }

    public async Task UpdateAsync(ContentPack pack, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pack);
        var row = await db.Packs.FindAsync([pack.PackId, pack.Version], cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Pack {pack.PackId} v{pack.Version} is not stored.");
        row.Status = pack.Status.ToString();
        row.DeprecatedAfter = pack.DeprecatedAfter;
    }

    public Task CommitAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    private static ContentPack ToDomain(PackRow row)
    {
        var attributions = JsonSerializer.Deserialize<List<PackAttribution>>(row.AttributionsJson, AttributionJson) ?? [];
        return ContentPack.Restore(
            new PackIdentity(row.PackId, LanguagePair.Parse(row.Pair).Value, row.Level, row.Topic, row.Version),
            row.Sha256,
            row.SizeBytes,
            row.ObjectKey,
            row.MinAppVersion,
            Enum.Parse<PackStatus>(row.Status),
            row.PublishedAt,
            row.DeprecatedAfter,
            attributions);
    }
}
