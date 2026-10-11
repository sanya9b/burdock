namespace Burdock.Domain.Sync;

/// <summary>Event types of the progress log (spec 09 §3.5), shared so the app emits exactly what the server accepts.</summary>
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
