using Burdock.Domain.SharedKernel;
using Burdock.Server.Modules.LearnerProgress.Domain;
using Burdock.Server.Shared.Time;

namespace Burdock.Server.Modules.LearnerProgress.Application;

/// <summary>«Progress» section (spec 09 §3.15).</summary>
public sealed class ProgressOptions
{
    public const string SectionName = "Progress";

    public int MaxBatchEvents { get; set; } = 500;

    public int PageSize { get; set; } = 1000;

    public TimeSpan FutureSkewTolerance { get; set; } = TimeSpan.FromMinutes(5);
}

/// <summary>Result of appending a batch: which ids were new, which were already stored, and the learner's cursor afterwards.</summary>
public sealed record AppendOutcome(IReadOnlyList<Guid> Accepted, IReadOnlyList<Guid> Duplicates, long ServerSeq);

public sealed record EventsPage(IReadOnlyList<ProgressEvent> Events, long NextSince, bool HasMore);

/// <summary>Append-only storage with a per-learner monotonic sequence (spec 09 §3.5): appends of one learner are serialised.</summary>
public interface IProgressEventStore
{
    Task<AppendOutcome> AppendAsync(LearnerId learnerId, IReadOnlyList<ProgressEvent> events, CancellationToken cancellationToken);

    Task<EventsPage> ReadAsync(LearnerId learnerId, long since, int limit, Guid? excludeInstallation, CancellationToken cancellationToken);

    Task<long> CurrentSequenceAsync(LearnerId learnerId, CancellationToken cancellationToken);
}

public sealed record IncomingEvent(Guid Id, string? Type, int SchemaVersion, DateTimeOffset OccurredAt, DateOnly LocalDate, Guid? InstallationId, string? PayloadJson);

public sealed record RejectedEvent(Guid Id, string Code);

public sealed record BatchOutcome(IReadOnlyList<Guid> Accepted, IReadOnlyList<Guid> Duplicates, IReadOnlyList<RejectedEvent> Rejected, long ServerSeq);

/// <summary>POST /v1/progress/events (FR-09-10): validates every envelope, rejects the bad ones individually and appends the rest in one transaction.</summary>
public sealed class AppendEventsUseCase(IProgressEventStore store, IClock clock, ProgressOptions options)
{
    public async Task<Result<BatchOutcome>> ExecuteAsync(LearnerId learnerId, IReadOnlyList<IncomingEvent> incoming, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        if (incoming.Count == 0)
        {
            return Result.Success(new BatchOutcome([], [], [], await store.CurrentSequenceAsync(learnerId, cancellationToken).ConfigureAwait(false)));
        }

        if (incoming.Count > options.MaxBatchEvents)
        {
            return Result.Failure<BatchOutcome>(DomainError.Validation("progress.batch_too_large", "Errors_Progress_BatchTooLarge", new Dictionary<string, string>(StringComparer.Ordinal) { ["max"] = options.MaxBatchEvents.ToString(System.Globalization.CultureInfo.InvariantCulture) }));
        }

        var now = clock.UtcNow;
        var accepted = new List<ProgressEvent>(incoming.Count);
        var rejected = new List<RejectedEvent>();
        var seen = new HashSet<Guid>();
        foreach (var e in incoming)
        {
            if (!seen.Add(e.Id))
            {
                continue; // the same id twice in one batch: the first occurrence counts
            }

            var result = ProgressEvent.Accept(e.Id, learnerId, e.Type, e.SchemaVersion, e.OccurredAt, e.LocalDate, e.InstallationId, e.PayloadJson, now, options.FutureSkewTolerance);
            if (result.IsSuccess)
            {
                accepted.Add(result.Value);
            }
            else
            {
                rejected.Add(new RejectedEvent(e.Id, result.Error!.Code));
            }
        }

        var outcome = await store.AppendAsync(learnerId, accepted, cancellationToken).ConfigureAwait(false);
        return Result.Success(new BatchOutcome(outcome.Accepted, outcome.Duplicates, rejected, outcome.ServerSeq));
    }
}

/// <summary>GET /v1/progress/events (FR-09-11): a page after the cursor; a device passes its own installation id to skip what it produced.</summary>
public sealed class ReadEventsUseCase(IProgressEventStore store, ProgressOptions options)
{
    public Task<EventsPage> ExecuteAsync(LearnerId learnerId, long since, int? limit, Guid? excludeInstallation, CancellationToken cancellationToken)
    {
        var pageSize = limit is > 0 ? Math.Min(limit.Value, options.PageSize) : options.PageSize;
        return store.ReadAsync(learnerId, Math.Max(0, since), pageSize, excludeInstallation, cancellationToken);
    }
}
