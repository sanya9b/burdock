using System.Text.Json;
using Burdock.Application.Ports;
using Burdock.Domain.Exposure;
using Burdock.Domain.Learning;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Sync;
using Burdock.Domain.Time;
using Microsoft.Extensions.Logging;

namespace Burdock.Application.UseCases.Sync;

public sealed record ProgressSyncReport(int Pushed, int Rejected, int Pulled, int Applied, long Cursor);

/// <summary>
/// Two directions of the progress sync (spec 09 §3.5, T14). Push: the outbox goes up in batches, accepted and duplicate
/// ids are dropped locally, rejected ones are logged and dropped. Pull: events after the cursor come down and are
/// replayed into the local cards, review log and exposures; event ids equal record ids, so a replay is idempotent.
/// A device with an empty cursor backfills everything (a reinstall or a restored credential); afterwards it skips its own
/// installation's events.
/// </summary>
public sealed partial class SyncProgressUseCase(
    ISyncGateway gateway,
    IProgressOutbox outbox,
    ISyncState state,
    IDeviceCredentials credentials,
    ILearnerContext learner,
    ILearnerCardRepository cards,
    IReviewLogStore reviewLog,
    IExposureStore exposures,
    IReviewScheduler scheduler,
    IIdGenerator ids,
    IClock clock,
    ILogger<SyncProgressUseCase> logger)
{
    public const string CursorKey = "progress.cursor";
    public const int PushBatchSize = 200;
    public const int PullPageSize = 500;

    public async Task<Result<ProgressSyncReport>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var installation = await credentials.GetOrCreateInstallationAsync(cancellationToken).ConfigureAwait(false);
        var pushed = 0;
        var rejected = 0;
        while (true)
        {
            var batch = await outbox.PeekAsync(PushBatchSize, cancellationToken).ConfigureAwait(false);
            if (batch.Count == 0)
            {
                break;
            }

            var stamped = batch.Select(e => e with { InstallationId = installation.InstallationId }).ToArray();
            var push = await gateway.PushEventsAsync(stamped, cancellationToken).ConfigureAwait(false);
            if (push.IsFailure)
            {
                return Result.Failure<ProgressSyncReport>(push.Error);
            }

            foreach (var rejection in push.Value.Rejected)
            {
                LogRejected(rejection.Id, rejection.Code);
            }

            await outbox.RemoveAsync([.. push.Value.Accepted, .. push.Value.Duplicates, .. push.Value.Rejected.Select(r => r.Id)], cancellationToken).ConfigureAwait(false);
            pushed += push.Value.Accepted.Count;
            rejected += push.Value.Rejected.Count;
            if (batch.Count < PushBatchSize)
            {
                break;
            }
        }

        var cursor = long.TryParse(await state.GetAsync(CursorKey, cancellationToken).ConfigureAwait(false), System.Globalization.CultureInfo.InvariantCulture, out var stored) ? stored : 0;
        var backfill = cursor == 0;
        var pulled = 0;
        var applied = 0;
        while (true)
        {
            var pull = await gateway.PullEventsAsync(cursor, backfill ? null : installation.InstallationId, PullPageSize, cancellationToken).ConfigureAwait(false);
            if (pull.IsFailure)
            {
                return Result.Failure<ProgressSyncReport>(pull.Error);
            }

            foreach (var synced in pull.Value.Events)
            {
                pulled++;
                if (await ApplyAsync(synced.Event, cancellationToken).ConfigureAwait(false))
                {
                    applied++;
                }
            }

            if (pull.Value.NextSince > cursor)
            {
                cursor = pull.Value.NextSince;
                await state.SetAsync(CursorKey, cursor.ToString(System.Globalization.CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
            }

            if (!pull.Value.HasMore)
            {
                break;
            }
        }

        return Result.Success(new ProgressSyncReport(pushed, rejected, pulled, applied, cursor));
    }

    /// <summary>Replays one remote event; returns false when it was already applied or is not understood.</summary>
    private async Task<bool> ApplyAsync(SyncEvent @event, CancellationToken cancellationToken)
    {
        var learnerId = await learner.GetLearnerIdAsync(cancellationToken).ConfigureAwait(false);
        switch (@event.Type)
        {
            case ProgressEventTypes.ReviewLog:
            {
                if (await reviewLog.ExistsAsync(@event.Id, cancellationToken).ConfigureAwait(false))
                {
                    return false;
                }

                var payload = JsonSerializer.Deserialize(@event.PayloadJson, ProgressPayloadJsonContext.Default.ReviewLogPayload);
                if (payload is null || !Enum.IsDefined((Rating)payload.Rating) || !Enum.TryParse<ExerciseMode>(payload.Mode, out var mode))
                {
                    LogUnreadable(@event.Id, @event.Type);
                    return false;
                }

                var lexemeId = LexemeId.From(payload.LexemeId);
                var card = await cards.GetByLexemeAsync(learnerId, lexemeId, cancellationToken).ConfigureAwait(false)
                           ?? LearnerCard.CreateNew(CardId.From(payload.CardId), learnerId, lexemeId, payload.ReviewedAt);
                card.Introduce(payload.ReviewedAt);
                var rating = (Rating)payload.Rating;
                var scheduled = scheduler.Review(card.ToSchedulingInput(), rating, payload.ReviewedAt);
                var result = card.ApplyReview(rating, mode, scheduled, LearningDay.From(payload.ReviewedAt, clock.LocalTimeZone), payload.ReviewedAt);
                if (result.IsFailure)
                {
                    LogUnreadable(@event.Id, @event.Type);
                    return false;
                }

                await cards.UpsertAsync(card, cancellationToken).ConfigureAwait(false);
                await reviewLog.AppendAsync(new ReviewLogEntry(@event.Id, learnerId, card.Id, lexemeId, rating, mode, payload.ReviewedAt, TimeSpan.FromMilliseconds(payload.ResponseTimeMs), scheduled.Memory.Stability, scheduled.Memory.Difficulty, scheduled.Due), cancellationToken).ConfigureAwait(false);
                return true;
            }

            case ProgressEventTypes.ExposureConfirmed:
            {
                if (await exposures.ExistsAsync(@event.Id, cancellationToken).ConfigureAwait(false))
                {
                    return false;
                }

                var payload = JsonSerializer.Deserialize(@event.PayloadJson, ProgressPayloadJsonContext.Default.ExposureConfirmedPayload);
                if (payload is null || !Enum.TryParse<ExposureSurface>(payload.Surface, out var surface))
                {
                    LogUnreadable(@event.Id, @event.Type);
                    return false;
                }

                var lexemeId = LexemeId.From(payload.LexemeId);
                var card = await cards.GetByLexemeAsync(learnerId, lexemeId, cancellationToken).ConfigureAwait(false)
                           ?? LearnerCard.CreateNew(CardId.From(ids.NewId()), learnerId, lexemeId, payload.At);
                card.RecordExposure(ExposureKind.Confirmed, LearningDay.From(payload.At, clock.LocalTimeZone), payload.At);
                await exposures.AppendAsync(new ExposureRecord(@event.Id, learnerId, lexemeId, surface, ExposureKind.Confirmed, payload.At), cancellationToken).ConfigureAwait(false);
                await cards.UpsertAsync(card, cancellationToken).ConfigureAwait(false);
                return true;
            }

            case ProgressEventTypes.CardIntroduced:
            {
                var payload = JsonSerializer.Deserialize(@event.PayloadJson, ProgressPayloadJsonContext.Default.CardIntroducedPayload);
                if (payload is null)
                {
                    LogUnreadable(@event.Id, @event.Type);
                    return false;
                }

                var lexemeId = LexemeId.From(payload.LexemeId);
                var card = await cards.GetByLexemeAsync(learnerId, lexemeId, cancellationToken).ConfigureAwait(false);
                if (card is not null && card.State != CardState.New)
                {
                    return false;
                }

                card ??= LearnerCard.CreateNew(CardId.From(payload.CardId), learnerId, lexemeId, payload.IntroducedAt);
                card.Introduce(payload.IntroducedAt);
                await cards.UpsertAsync(card, cancellationToken).ConfigureAwait(false);
                return true;
            }

            default:
                return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Server rejected progress event {EventId}: {Code}")]
    private partial void LogRejected(Guid eventId, string code);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Remote progress event {EventId} of type {Type} could not be applied")]
    private partial void LogUnreadable(Guid eventId, string type);
}
