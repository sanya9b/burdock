using System.Globalization;
using Burdock.Application.Ports;
using Burdock.Infrastructure.Persistence;
using SQLite;

namespace Burdock.Infrastructure.Sync;

/// <summary>Events waiting for the server, in the order they happened (spec 09 section 3.5); the installation id is stamped at push time.</summary>
public sealed class SqliteProgressOutbox(BurdockDatabase database) : IProgressOutbox
{
    public async Task AppendAsync(SyncEvent @event, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@event);
        var db = await database.GetOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await db.ExecuteAsync(
            "INSERT OR IGNORE INTO sync_outbox (EventId, Type, OccurredAtTicks, LocalDate, PayloadJson) VALUES (?, ?, ?, ?, ?)",
            @event.Id.ToString("D"), @event.Type, @event.OccurredAt.UtcTicks, @event.LocalDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), @event.PayloadJson).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SyncEvent>> PeekAsync(int limit, CancellationToken cancellationToken)
    {
        var db = await database.GetOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await db.Table<SyncOutboxRow>().OrderBy(r => r.Seq).Take(limit).ToListAsync().ConfigureAwait(false);
        return rows.Select(r => new SyncEvent(
            Guid.Parse(r.EventId),
            r.Type,
            new DateTimeOffset(r.OccurredAtTicks, TimeSpan.Zero),
            DateOnly.ParseExact(r.LocalDate, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            null,
            r.PayloadJson)).ToArray();
    }

    public async Task RemoveAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (ids.Count == 0)
        {
            return;
        }

        var db = await database.GetOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var keys = ids.Select(id => id.ToString("D")).ToArray();
        await db.RunInTransactionAsync(connection =>
        {
            foreach (var key in keys)
            {
                connection.Execute("DELETE FROM sync_outbox WHERE EventId = ?", key);
            }
        }).ConfigureAwait(false);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken)
    {
        var db = await database.GetOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await db.Table<SyncOutboxRow>().CountAsync().ConfigureAwait(false);
    }
}

/// <summary>Cursor, ETags, account id and last sync instant as plain key-value rows.</summary>
public sealed class SqliteSyncState(BurdockDatabase database) : ISyncState
{
    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken)
    {
        var db = await database.GetOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var row = await db.FindAsync<SyncStateRow>(key).ConfigureAwait(false);
        return row?.Value;
    }

    public async Task SetAsync(string key, string? value, CancellationToken cancellationToken)
    {
        var db = await database.GetOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        if (value is null)
        {
            await db.DeleteAsync<SyncStateRow>(key).ConfigureAwait(false);
            return;
        }

        await db.InsertOrReplaceAsync(new SyncStateRow { Key = key, Value = value }).ConfigureAwait(false);
    }
}

[Table("sync_outbox")]
public sealed class SyncOutboxRow
{
    [PrimaryKey]
    [AutoIncrement]
    public int Seq { get; set; }

    [Indexed(Unique = true)]
    public string EventId { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public long OccurredAtTicks { get; set; }

    public string LocalDate { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = string.Empty;
}

[Table("sync_state")]
public sealed class SyncStateRow
{
    [PrimaryKey]
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}
