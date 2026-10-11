using System.Text.Json;

namespace Burdock.Contracts.V1.Progress;

/// <summary>
/// One append-only progress event (spec 09 §3.5, FR-09-10): the envelope is the same for every type, the payload belongs
/// to spec 06 and is opaque to the server. Ids are client UUIDv7, unique per learner, which makes batches idempotent.
/// </summary>
public sealed record ProgressEventDto(
    Guid Id,
    string Type,
    int SchemaVersion,
    DateTimeOffset OccurredAt,
    DateOnly LocalDate,
    Guid? InstallationId,
    JsonElement Payload);

public sealed record PostEventsDto(IReadOnlyList<ProgressEventDto> Events);

public sealed record RejectedEventDto(Guid Id, string Code);

/// <summary>Outcome of a batch: every event lands in exactly one list; <see cref="ServerSeq"/> is the learner's cursor after the batch.</summary>
public sealed record PostEventsResultDto(IReadOnlyList<Guid> Accepted, IReadOnlyList<Guid> Duplicates, IReadOnlyList<RejectedEventDto> Rejected, long ServerSeq);

/// <summary>GET /v1/progress/events?since=… (FR-09-11): events after the cursor in server_seq order.</summary>
public sealed record EventsPageDto(IReadOnlyList<SyncedEventDto> Events, long NextSince, bool HasMore);

/// <summary>A stored event with the server-assigned sequence number.</summary>
public sealed record SyncedEventDto(long ServerSeq, ProgressEventDto Event);
