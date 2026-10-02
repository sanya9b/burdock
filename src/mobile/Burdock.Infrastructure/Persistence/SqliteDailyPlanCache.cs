using System.Globalization;
using System.Text.Json;
using Burdock.Application.Ports;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Time;
using SQLite;

namespace Burdock.Infrastructure.Persistence;

/// <summary>Snapshot of today's new-word pick per learner (issue #4): one row, replaced on every save, survives process restarts.</summary>
public sealed class SqliteDailyPlanCache(BurdockDatabase database) : IDailyPlanCache
{
    public async Task<DailyPlanSnapshot?> GetAsync(LearnerId learnerId, CancellationToken cancellationToken)
    {
        var db = await database.GetOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var row = await db.FindAsync<DailyPlanCacheRow>(learnerId.Value).ConfigureAwait(false);
        if (row is null || !DateOnly.TryParseExact(row.Day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return null;
        }

        var ids = JsonSerializer.Deserialize(row.NewWordIdsJson, GuidListJsonContext.Default.IReadOnlyListGuid) ?? [];
        return new DailyPlanSnapshot(learnerId, new LearningDay(date), row.Fingerprint, ids.Where(g => g != Guid.Empty).Select(LexemeId.From).ToArray());
    }

    public async Task SaveAsync(DailyPlanSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var db = await database.GetOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await db.InsertOrReplaceAsync(new DailyPlanCacheRow
        {
            LearnerId = snapshot.LearnerId.Value,
            Day = snapshot.Day.ToString(),
            Fingerprint = snapshot.Fingerprint,
            NewWordIdsJson = JsonSerializer.Serialize(snapshot.NewWordIds.Select(id => id.Value).ToArray(), GuidListJsonContext.Default.GuidArray),
        }).ConfigureAwait(false);
    }
}

[Table("daily_plan_cache")]
public sealed class DailyPlanCacheRow
{
    [PrimaryKey]
    public Guid LearnerId { get; set; }

    public string Day { get; set; } = string.Empty;

    public string Fingerprint { get; set; } = string.Empty;

    public string NewWordIdsJson { get; set; } = "[]";
}

[System.Text.Json.Serialization.JsonSerializable(typeof(IReadOnlyList<Guid>))]
[System.Text.Json.Serialization.JsonSerializable(typeof(Guid[]))]
internal sealed partial class GuidListJsonContext : System.Text.Json.Serialization.JsonSerializerContext;
