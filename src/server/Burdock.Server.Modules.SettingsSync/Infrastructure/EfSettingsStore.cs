using Burdock.Domain.SharedKernel;
using Burdock.Server.Modules.SettingsSync.Application;
using Burdock.Domain.Sync;
using Microsoft.EntityFrameworkCore;

namespace Burdock.Server.Modules.SettingsSync.Infrastructure;

/// <summary>EF Core store over the scoped <see cref="SettingsDbContext"/>; a save upserts the header and the changed entries in one SaveChanges.</summary>
public sealed class EfSettingsStore(SettingsDbContext db) : ISettingsStore
{
    public async Task<SettingsDocument?> GetAsync(LearnerId learnerId, CancellationToken cancellationToken)
    {
        var userId = learnerId.Value;
        var header = await db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.UserId == userId, cancellationToken).ConfigureAwait(false);
        if (header is null)
        {
            return null;
        }

        var rows = await db.Entries.AsNoTracking().Where(e => e.UserId == userId).ToListAsync(cancellationToken).ConfigureAwait(false);
        var entries = rows.ToDictionary(r => r.Key, r => new SettingsEntry(r.Key, r.Value, r.UpdatedAt, r.ClientId), StringComparer.Ordinal);
        return new SettingsDocument(learnerId, header.SchemaVersion, entries, header.Etag, header.UpdatedAt);
    }

    public async Task SaveAsync(SettingsDocument document, IReadOnlyCollection<SettingsEntry> changed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(changed);
        var userId = document.LearnerId.Value;
        var header = await db.Documents.FindAsync([userId], cancellationToken).ConfigureAwait(false);
        if (header is null)
        {
            db.Documents.Add(new SettingsDocumentRow { UserId = userId, SchemaVersion = document.SchemaVersion, Etag = document.Etag, UpdatedAt = document.UpdatedAt });
        }
        else
        {
            header.SchemaVersion = document.SchemaVersion;
            header.Etag = document.Etag;
            header.UpdatedAt = document.UpdatedAt;
        }

        foreach (var entry in changed)
        {
            var row = await db.Entries.FindAsync([userId, entry.Key], cancellationToken).ConfigureAwait(false);
            if (row is null)
            {
                db.Entries.Add(new SettingsEntryRow { UserId = userId, Key = entry.Key, Value = entry.ValueJson, UpdatedAt = entry.UpdatedAt, ClientId = entry.ClientId });
            }
            else
            {
                row.Value = entry.ValueJson;
                row.UpdatedAt = entry.UpdatedAt;
                row.ClientId = entry.ClientId;
            }
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
