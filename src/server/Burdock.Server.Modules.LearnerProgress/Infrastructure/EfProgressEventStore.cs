using Burdock.Domain.SharedKernel;
using Burdock.Server.Modules.LearnerProgress.Application;
using Burdock.Server.Modules.LearnerProgress.Domain;
using Burdock.Server.Shared.Time;
using Microsoft.EntityFrameworkCore;

namespace Burdock.Server.Modules.LearnerProgress.Infrastructure;

/// <summary>
/// Appends run in one transaction per batch: the learner's <c>user_seq</c> row is locked (FOR UPDATE), duplicates are
/// filtered by (user_id, id), new rows get consecutive sequence numbers and the cursor is advanced (spec 09 §3.5).
/// Locking one row per learner gives monotonic sequences without a global sequence or serialisable isolation.
/// </summary>
public sealed class EfProgressEventStore(ProgressDbContext db, IClock clock) : IProgressEventStore
{
    public async Task<AppendOutcome> AppendAsync(LearnerId learnerId, IReadOnlyList<ProgressEvent> events, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(events);
        var userId = learnerId.Value;
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await db.Database.ExecuteSqlAsync($"INSERT INTO progress.user_seq (user_id, last_seq) VALUES ({userId}, 0) ON CONFLICT (user_id) DO NOTHING", cancellationToken).ConfigureAwait(false);
            var lastSeq = await db.Database.SqlQuery<long>($"SELECT last_seq AS \"Value\" FROM progress.user_seq WHERE user_id = {userId} FOR UPDATE").FirstAsync(cancellationToken).ConfigureAwait(false);

            var ids = events.Select(e => e.Id).ToArray();
            var existing = ids.Length == 0
                ? []
                : await db.Events.Where(e => e.UserId == userId && ids.Contains(e.Id)).Select(e => e.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
            var existingSet = existing.ToHashSet();

            var accepted = new List<Guid>();
            var duplicates = new List<Guid>();
            var now = clock.UtcNow;
            foreach (var e in events)
            {
                if (existingSet.Contains(e.Id))
                {
                    duplicates.Add(e.Id);
                    continue;
                }

                lastSeq++;
                db.Events.Add(new EventRow
                {
                    UserId = userId,
                    Id = e.Id,
                    ServerSeq = lastSeq,
                    Type = e.Type,
                    SchemaVersion = e.SchemaVersion,
                    OccurredAt = e.OccurredAt,
                    LocalDate = e.LocalDate,
                    InstallationId = e.InstallationId,
                    Payload = e.PayloadJson,
                    ReceivedAt = now,
                });
                accepted.Add(e.Id);
            }

            if (accepted.Count > 0)
            {
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await db.Database.ExecuteSqlAsync($"UPDATE progress.user_seq SET last_seq = {lastSeq} WHERE user_id = {userId}", cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            db.ChangeTracker.Clear();
            return new AppendOutcome(accepted, duplicates, lastSeq);
        }).ConfigureAwait(false);
    }

    public async Task<EventsPage> ReadAsync(LearnerId learnerId, long since, int limit, Guid? excludeInstallation, CancellationToken cancellationToken)
    {
        var userId = learnerId.Value;
        var query = db.Events.AsNoTracking().Where(e => e.UserId == userId && e.ServerSeq > since);
        if (excludeInstallation is { } installation)
        {
            query = query.Where(e => e.InstallationId != installation);
        }

        var rows = await query.OrderBy(e => e.ServerSeq).Take(limit + 1).ToListAsync(cancellationToken).ConfigureAwait(false);
        var hasMore = rows.Count > limit;
        var page = rows.Take(limit).Select(r => ProgressEvent.Restore(r.Id, LearnerId.From(r.UserId), r.Type, r.SchemaVersion, r.OccurredAt, r.LocalDate, r.InstallationId, r.Payload, r.ServerSeq, r.ReceivedAt)).ToArray();
        var nextSince = page.Length == 0 ? since : page[^1].ServerSeq!.Value;
        return new EventsPage(page, nextSince, hasMore);
    }

    public async Task<long> CurrentSequenceAsync(LearnerId learnerId, CancellationToken cancellationToken)
    {
        var row = await db.Sequences.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == learnerId.Value, cancellationToken).ConfigureAwait(false);
        return row?.LastSeq ?? 0;
    }
}
