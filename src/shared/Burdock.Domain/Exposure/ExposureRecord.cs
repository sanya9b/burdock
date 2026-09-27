using Burdock.Domain.SharedKernel;

namespace Burdock.Domain.Exposure;

/// <summary>Where a word was shown outside or inside the app (spec 06 §3.1).</summary>
public enum ExposureSurface
{
    WidgetHome,
    WidgetLock,
    StandBy,
    Notification,
    LiveActivity,
    InApp,
}

/// <summary>
/// Scheduled = the system had the word on a surface at that time (an opportunity, not proof of viewing).
/// Confirmed = the learner interacted with it (tap, intent, notification action, in-app view). ADR 0015.
/// </summary>
public enum ExposureKind
{
    Scheduled,
    Confirmed,
}

/// <summary>Append-only record of one exposure; never grades the FSRS card (ADR 0008).</summary>
public sealed record ExposureRecord(Guid Id, LearnerId LearnerId, LexemeId LexemeId, ExposureSurface Surface, ExposureKind Kind, DateTimeOffset At)
{
    public static ExposureRecord Scheduled(Guid id, LearnerId learnerId, LexemeId lexemeId, ExposureSurface surface, DateTimeOffset at)
        => new(id, learnerId, lexemeId, surface, ExposureKind.Scheduled, at);

    public static ExposureRecord Confirmed(Guid id, LearnerId learnerId, LexemeId lexemeId, ExposureSurface surface, DateTimeOffset at)
        => new(id, learnerId, lexemeId, surface, ExposureKind.Confirmed, at);
}
