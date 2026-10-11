using System.Text.Json;
using System.Text.Json.Serialization;
using Burdock.Application.Ports;
using Burdock.Domain.Exposure;
using Burdock.Domain.Learning;
using Burdock.Domain.Sync;
using Burdock.Domain.Time;

namespace Burdock.Application.UseCases.Sync;

/// <summary>Payload of a ReviewLog event (spec 06 §3.3, spec 09 §3.5): enough to replay the review on another device.</summary>
public sealed record ReviewLogPayload(Guid CardId, Guid LexemeId, int Rating, string Mode, DateTimeOffset ReviewedAt, int ResponseTimeMs, double? StabilityAfter, double? DifficultyAfter, DateTimeOffset DueAfter);

public sealed record ExposureConfirmedPayload(Guid LexemeId, string Surface, DateTimeOffset At);

public sealed record CardIntroducedPayload(Guid CardId, Guid LexemeId, DateTimeOffset IntroducedAt);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ReviewLogPayload))]
[JsonSerializable(typeof(ExposureConfirmedPayload))]
[JsonSerializable(typeof(CardIntroducedPayload))]
public sealed partial class ProgressPayloadJsonContext : JsonSerializerContext;

/// <summary>Builds the events the learning use cases drop into the outbox; the event id is the id of the local record, so replays are idempotent.</summary>
public static class ProgressEventFactory
{
    public static SyncEvent ReviewLog(ReviewLogEntry entry, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var payload = new ReviewLogPayload(entry.CardId.Value, entry.LexemeId.Value, (int)entry.Rating, entry.Mode.ToString(), entry.ReviewedAt, (int)Math.Min(int.MaxValue, entry.ResponseTime.TotalMilliseconds), entry.StabilityAfter, entry.DifficultyAfter, entry.DueAfter);
        return new SyncEvent(entry.Id, ProgressEventTypes.ReviewLog, entry.ReviewedAt, LearningDay.From(entry.ReviewedAt, timeZone).Date, null, JsonSerializer.Serialize(payload, ProgressPayloadJsonContext.Default.ReviewLogPayload));
    }

    public static SyncEvent ExposureConfirmed(ExposureRecord record, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(record);
        var payload = new ExposureConfirmedPayload(record.LexemeId.Value, record.Surface.ToString(), record.At);
        return new SyncEvent(record.Id, ProgressEventTypes.ExposureConfirmed, record.At, LearningDay.From(record.At, timeZone).Date, null, JsonSerializer.Serialize(payload, ProgressPayloadJsonContext.Default.ExposureConfirmedPayload));
    }

    public static SyncEvent CardIntroduced(Guid eventId, LearnerCard card, DateTimeOffset at, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(card);
        var payload = new CardIntroducedPayload(card.Id.Value, card.LexemeId.Value, at);
        return new SyncEvent(eventId, ProgressEventTypes.CardIntroduced, at, LearningDay.From(at, timeZone).Date, null, JsonSerializer.Serialize(payload, ProgressPayloadJsonContext.Default.CardIntroducedPayload));
    }
}
