using Burdock.Domain.SharedKernel;

namespace Burdock.Server.Modules.LearnerProgress.Domain;

/// <summary>Event types the server accepts (spec 09 §3.5); payload schemas live in spec 06 and are not interpreted here.</summary>
public static class ProgressEventTypes
{
    public const string ReviewLog = "ReviewLog";
    public const string ExposureConfirmed = "ExposureConfirmed";
    public const string ExposureDaily = "ExposureDaily";
    public const string CardIntroduced = "CardIntroduced";
    public const string WordListChanged = "WordListChanged";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { ReviewLog, ExposureConfirmed, ExposureDaily, CardIntroduced, WordListChanged };
}

/// <summary>Why an event in a batch was rejected (spec 09 §3.5); accepted and duplicate events carry no code.</summary>
public static class ProgressRejectionCodes
{
    public const string SchemaInvalid = "schema_invalid";
    public const string FutureTimestamp = "future_timestamp";
    public const string LocalDateImplausible = "local_date_implausible";
    public const string UnknownType = "unknown_type";
    public const string PayloadTooLarge = "payload_too_large";
}

/// <summary>
/// One append-only learner event. The envelope is validated here; the payload is opaque JSON owned by spec 06. The
/// server never recomputes <see cref="LocalDate"/>, it only checks it is within a day of <see cref="OccurredAt"/>.
/// </summary>
public sealed class ProgressEvent
{
    public const int MaxPayloadBytes = 16 * 1024;
    public const int MaxTypeLength = 32;
    public const int CurrentSchemaVersion = 1;

    private ProgressEvent(Guid id, LearnerId learnerId, string type, int schemaVersion, DateTimeOffset occurredAt, DateOnly localDate, Guid? installationId, string payloadJson, long? serverSeq, DateTimeOffset? receivedAt)
    {
        Id = id;
        LearnerId = learnerId;
        Type = type;
        SchemaVersion = schemaVersion;
        OccurredAt = occurredAt;
        LocalDate = localDate;
        InstallationId = installationId;
        PayloadJson = payloadJson;
        ServerSeq = serverSeq;
        ReceivedAt = receivedAt;
    }

    public Guid Id { get; }

    public LearnerId LearnerId { get; }

    public string Type { get; }

    public int SchemaVersion { get; }

    public DateTimeOffset OccurredAt { get; }

    public DateOnly LocalDate { get; }

    public Guid? InstallationId { get; }

    public string PayloadJson { get; }

    /// <summary>Assigned by the store on append; null until then.</summary>
    public long? ServerSeq { get; }

    public DateTimeOffset? ReceivedAt { get; }

    /// <summary>Validates an incoming envelope against the batch rules (spec 09 §3.5): schema, type, clock skew and local-date plausibility.</summary>
    public static Result<ProgressEvent> Accept(Guid id, LearnerId learnerId, string? type, int schemaVersion, DateTimeOffset occurredAt, DateOnly localDate, Guid? installationId, string? payloadJson, DateTimeOffset now, TimeSpan futureSkewTolerance)
    {
        if (id == Guid.Empty || schemaVersion is < 1 or > CurrentSchemaVersion || string.IsNullOrWhiteSpace(payloadJson))
        {
            return Reject(ProgressRejectionCodes.SchemaInvalid);
        }

        if (string.IsNullOrWhiteSpace(type) || type.Length > MaxTypeLength || !ProgressEventTypes.All.Contains(type))
        {
            return Reject(ProgressRejectionCodes.UnknownType);
        }

        if (System.Text.Encoding.UTF8.GetByteCount(payloadJson) > MaxPayloadBytes)
        {
            return Reject(ProgressRejectionCodes.PayloadTooLarge);
        }

        if (occurredAt > now + futureSkewTolerance)
        {
            return Reject(ProgressRejectionCodes.FutureTimestamp);
        }

        var utcDate = DateOnly.FromDateTime(occurredAt.UtcDateTime);
        if (Math.Abs(localDate.DayNumber - utcDate.DayNumber) > 1)
        {
            return Reject(ProgressRejectionCodes.LocalDateImplausible);
        }

        return Result.Success(new ProgressEvent(id, learnerId, type, schemaVersion, occurredAt, localDate, installationId, payloadJson, null, null));
    }

    public static ProgressEvent Restore(Guid id, LearnerId learnerId, string type, int schemaVersion, DateTimeOffset occurredAt, DateOnly localDate, Guid? installationId, string payloadJson, long serverSeq, DateTimeOffset receivedAt)
        => new(id, learnerId, type, schemaVersion, occurredAt, localDate, installationId, payloadJson, serverSeq, receivedAt);

    private static Result<ProgressEvent> Reject(string code) => Result.Failure<ProgressEvent>(DomainError.Validation(code, "Errors_Progress_" + code));
}
