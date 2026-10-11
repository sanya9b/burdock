using Burdock.Application.Ports;
using Burdock.Domain.Sync;
using Burdock.Infrastructure.Persistence;
using Burdock.Infrastructure.Sync;

namespace Burdock.Infrastructure.Tests.Sync;

/// <summary>Outbox and sync state in SQLite (spec 09 section 3.5): order, idempotent append, removal by id, key-value round trip.</summary>
public sealed class SqliteSyncStoresTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 11, 7, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task Outbox_keeps_insertion_order_ignores_duplicates_and_removes_by_id()
    {
        var path = TempPath();
        var database = new BurdockDatabase(new DatabaseOptions(path));
        try
        {
            var outbox = new SqliteProgressOutbox(database);
            var ct = TestContext.Current.CancellationToken;
            var first = Event("0199a4c0-0000-7000-8000-000000000001", "{\"n\":1}");
            var second = Event("0199a4c0-0000-7000-8000-000000000002", "{\"n\":2}");
            var third = Event("0199a4c0-0000-7000-8000-000000000003", "{\"n\":3}");

            await outbox.AppendAsync(first, ct);
            await outbox.AppendAsync(second, ct);
            await outbox.AppendAsync(third, ct);
            await outbox.AppendAsync(first, ct);

            (await outbox.CountAsync(ct)).ShouldBe(3);
            var peeked = await outbox.PeekAsync(2, ct);
            peeked.Select(e => e.Id).ShouldBe([first.Id, second.Id]);
            peeked[0].ShouldBe(first);

            await outbox.RemoveAsync([first.Id, third.Id], ct);

            (await outbox.PeekAsync(10, ct)).ShouldBe([second]);
            await outbox.RemoveAsync([], ct);
            (await outbox.CountAsync(ct)).ShouldBe(1);
        }
        finally
        {
            await Cleanup(database, path);
        }
    }

    [Fact]
    public async Task State_round_trips_values_and_null_removes_a_key()
    {
        var path = TempPath();
        var database = new BurdockDatabase(new DatabaseOptions(path));
        try
        {
            var state = new SqliteSyncState(database);
            var ct = TestContext.Current.CancellationToken;

            (await state.GetAsync("progress.cursor", ct)).ShouldBeNull();
            await state.SetAsync("progress.cursor", "42", ct);
            await state.SetAsync("progress.cursor", "43", ct);
            (await state.GetAsync("progress.cursor", ct)).ShouldBe("43");
            await state.SetAsync("progress.cursor", null, ct);
            (await state.GetAsync("progress.cursor", ct)).ShouldBeNull();
        }
        finally
        {
            await Cleanup(database, path);
        }
    }

    private static SyncEvent Event(string id, string payload)
        => new(Guid.Parse(id), ProgressEventTypes.ReviewLog, T0, new DateOnly(2026, 10, 11), null, payload);

    private static string TempPath() => Path.Combine(Path.GetTempPath(), $"burdock-sync-{Guid.NewGuid():N}.db3");

    private static async Task Cleanup(BurdockDatabase database, string path)
    {
        await database.DisposeAsync();
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // The WAL may still be held by the connection pool on Windows; the temp folder is cleaned by the OS.
        }
    }
}
