using SQLite;

namespace Burdock.Infrastructure.Persistence;

/// <summary>Where the local database lives; the platform head supplies the path (App Group container on iOS later, spec 04).</summary>
public sealed record DatabaseOptions(string Path)
{
    public static DatabaseOptions InMemory { get; } = new(":memory:");
}

/// <summary>
/// Single async connection in WAL mode (spec 01 §3.4). Tier-0 data only: no personal data, so no encryption in v1 (T15).
/// </summary>
public sealed class BurdockDatabase : IAsyncDisposable
{
    private const SQLiteOpenFlags Flags = SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache;

    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly Lazy<SQLiteAsyncConnection> _connection;
    private bool _initialized;

    public BurdockDatabase(DatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _connection = new Lazy<SQLiteAsyncConnection>(() => new SQLiteAsyncConnection(options.Path, Flags));
    }

    public SQLiteAsyncConnection Connection => _connection.Value;

    /// <summary>Creates tables on first use; safe to call from every repository method.</summary>
    public async Task<SQLiteAsyncConnection> GetOpenConnectionAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return Connection;
        }

        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_initialized)
            {
                var db = Connection;
                await db.EnableWriteAheadLoggingAsync().ConfigureAwait(false);
                await db.CreateTablesAsync<WordEntryRow, ContentPackRow, LearnerCardRow, ReviewLogRow, ExposureRow>().ConfigureAwait(false);
                _initialized = true;
            }
        }
        finally
        {
            _initLock.Release();
        }

        return Connection;
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection.IsValueCreated)
        {
            await _connection.Value.CloseAsync().ConfigureAwait(false);
        }

        _initLock.Dispose();
    }
}

internal static class Ticks
{
    public static long From(DateTimeOffset value) => value.UtcTicks;

    public static long? From(DateTimeOffset? value) => value?.UtcTicks;

    public static DateTimeOffset To(long ticks) => new(ticks, TimeSpan.Zero);

    public static DateTimeOffset? To(long? ticks) => ticks is null ? null : new DateTimeOffset(ticks.Value, TimeSpan.Zero);
}
