using System.Globalization;
using Burdock.Application.Ports;
using Burdock.Application.UseCases.LearningDays;
using Burdock.Application.UseCases.Planning;
using Burdock.Application.UseCases.Sync;
using Burdock.Domain.Exposure;
using Burdock.Domain.Learning;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Time;
using Burdock.Domain.Widgets;
using Microsoft.Extensions.Logging;

namespace Burdock.Application.UseCases.Widgets;

public sealed record WidgetPlanReport(bool Supported, bool Written, int Entries, int ScheduledSettled, int ConfirmedSettled);

/// <summary>
/// Keeps the widget timeline current (spec 04 FR-04-3, FR-04-15; spec 06 §3.5.2). One pass: settle what the previous
/// timeline showed (scheduled exposures for entries whose time came while a widget was installed, a confirmed exposure
/// for the word the learner advanced past with «Далі»), plan 48 hours from today's words, and write plus reload only
/// when the next entries actually changed or the remaining horizon runs short.
/// </summary>
public sealed partial class PlanWidgetTimelineUseCase(
    IWidgetBridge widgets,
    ILearnerContext learner,
    GetDailyPlanUseCase dailyPlan,
    ILearnerCardRepository cards,
    IExposureStore exposures,
    IProgressOutbox outbox,
    ISyncState state,
    IIdGenerator ids,
    IClock clock,
    LearningDayResolver learningDays,
    ILogger<PlanWidgetTimelineUseCase> logger)
{
    public const string SettledUntilKey = "widget.settledUntil";

    /// <summary>Rewrite even an unchanged timeline once less than this remains, so the fallback never shows while the app is in use.</summary>
    public static readonly TimeSpan ReplanWhenRemainingBelow = TimeSpan.FromHours(24);

    public async Task<WidgetPlanReport> ExecuteAsync(string reason, CancellationToken cancellationToken)
    {
        if (!widgets.IsSupported)
        {
            return new WidgetPlanReport(false, false, 0, 0, 0);
        }

        var learnerId = await learner.GetLearnerIdAsync(cancellationToken).ConfigureAwait(false);
        var settings = await learner.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        var now = clock.UtcNow;
        var previous = await widgets.ReadTimelineAsync(cancellationToken).ConfigureAwait(false);
        var (scheduled, confirmed) = await SettleAsync(previous, learnerId, now, cancellationToken).ConfigureAwait(false);

        var plan = await dailyPlan.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        var counts = await exposures.CountSinceAsync(learnerId, learningDays.StartOfToday(), cancellationToken).ConfigureAwait(false);
        var candidates = plan.Words
            .Select((word, index) => new TimelineCandidate(word.Entry, word.Reason == PlanReason.New, word.Reason == PlanReason.New ? index : word.Retrievability, counts.GetValueOrDefault(word.Entry.Id)))
            .ToArray();
        var timeline = WordTimelinePlanner.Plan(new TimelineRequest(now, clock.LocalTimeZone, settings, candidates));

        var unchanged = previous is not null
            && previous.PlannedUntil - now >= ReplanWhenRemainingBelow
            && string.Equals(previous.LeadingHash(), timeline.LeadingHash(), StringComparison.Ordinal);
        if (unchanged)
        {
            return new WidgetPlanReport(true, false, previous!.Entries.Count, scheduled, confirmed);
        }

        await widgets.WriteTimelineAsync(timeline, cancellationToken).ConfigureAwait(false);
        await widgets.RequestReloadAsync(reason, cancellationToken).ConfigureAwait(false);
        LogPlanned(reason, timeline.Entries.Count, timeline.CadenceMinutes, scheduled, confirmed);
        return new WidgetPlanReport(true, true, timeline.Entries.Count, scheduled, confirmed);
    }

    /// <summary>Turns the previous timeline into exposure records once, up to <paramref name="now"/> (spec 04 §3.5).</summary>
    private async Task<(int Scheduled, int Confirmed)> SettleAsync(WordTimeline? previous, LearnerId learnerId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (previous is null || previous.Entries.Count == 0)
        {
            return (0, 0);
        }

        var stored = await state.GetAsync(SettledUntilKey, cancellationToken).ConfigureAwait(false);
        var settledUntil = DateTimeOffset.TryParse(stored, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : previous.Entries[0].FireDate.AddSeconds(-1);
        var marker = await widgets.ReadMarkerAsync(cancellationToken).ConfigureAwait(false);
        var cursor = await widgets.ReadCursorAsync(cancellationToken).ConfigureAwait(false);
        var surface = SurfaceOf(marker);
        var scheduled = 0;
        var confirmed = 0;

        // Without a timeline request after generation no widget was on screen, so nothing was shown (spec 04 §3.5).
        if (marker is not null && marker.RequestedAt >= previous.GeneratedAt)
        {
            foreach (var entry in previous.Entries.Where(e => e.FireDate > settledUntil && e.FireDate <= now))
            {
                await RecordAsync(learnerId, entry.LexemeId, surface, ExposureKind.Scheduled, entry.FireDate, cancellationToken).ConfigureAwait(false);
                scheduled++;
            }
        }

        // «Далі» confirms the word the learner dismissed and schedules the one that appeared early (spec 06 §3.5.2).
        if (cursor is not null && cursor.AdvancedAt > settledUntil && cursor.AdvancedAt <= now)
        {
            var dismissed = previous.Entries.FirstOrDefault(e => e.EntryIndex == cursor.CursorIndex - 1);
            var shownEarly = previous.Entries.FirstOrDefault(e => e.EntryIndex == cursor.CursorIndex);
            if (dismissed is not null)
            {
                await RecordAsync(learnerId, dismissed.LexemeId, surface, ExposureKind.Confirmed, cursor.AdvancedAt, cancellationToken).ConfigureAwait(false);
                confirmed++;
            }

            if (shownEarly is not null && shownEarly.FireDate > cursor.AdvancedAt)
            {
                await RecordAsync(learnerId, shownEarly.LexemeId, surface, ExposureKind.Scheduled, cursor.AdvancedAt, cancellationToken).ConfigureAwait(false);
                scheduled++;
            }
        }

        await state.SetAsync(SettledUntilKey, now.ToString("O", CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
        return (scheduled, confirmed);
    }

    private async Task RecordAsync(LearnerId learnerId, LexemeId lexemeId, ExposureSurface surface, ExposureKind kind, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var card = await cards.GetByLexemeAsync(learnerId, lexemeId, cancellationToken).ConfigureAwait(false)
                   ?? LearnerCard.CreateNew(CardId.From(ids.NewId()), learnerId, lexemeId, at);
        card.RecordExposure(kind, LearningDay.From(at, clock.LocalTimeZone), at);
        var record = new ExposureRecord(ids.NewId(), learnerId, lexemeId, surface, kind, at);
        await exposures.AppendAsync(record, cancellationToken).ConfigureAwait(false);
        await cards.UpsertAsync(card, cancellationToken).ConfigureAwait(false);
        if (kind == ExposureKind.Confirmed)
        {
            await outbox.AppendAsync(ProgressEventFactory.ExposureConfirmed(record, clock.LocalTimeZone), cancellationToken).ConfigureAwait(false);
        }
    }

    private static ExposureSurface SurfaceOf(WidgetMarker? marker)
    {
        if (marker is null || marker.Families.Count == 0)
        {
            return ExposureSurface.WidgetHome;
        }

        var lockOnly = marker.Families.All(f => f.StartsWith("accessory", StringComparison.OrdinalIgnoreCase));
        return lockOnly ? ExposureSurface.WidgetLock : ExposureSurface.WidgetHome;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Widget timeline written ({Reason}): {Entries} entries at {Cadence} min; settled {Scheduled} scheduled, {Confirmed} confirmed exposures")]
    private partial void LogPlanned(string reason, int entries, int cadence, int scheduled, int confirmed);
}
