using Burdock.Application.Ports;
using Burdock.Application.UseCases.LearningDays;
using Burdock.Domain.Exposure;
using Burdock.Domain.Learning;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;

namespace Burdock.Application.UseCases.Words;

/// <summary>Everything the WordCard screen shows (spec 03 S-11).</summary>
public sealed record WordCardView(WordEntry Entry, CardState State, int ScheduledExposures, int ConfirmedExposures, double Retrievability, DateTimeOffset? Due);

/// <summary>Shared card lookup/creation for word-level use cases.</summary>
internal static class CardAccess
{
    public static async Task<LearnerCard> GetOrCreateAsync(ILearnerCardRepository cards, IIdGenerator ids, LearnerId learnerId, LexemeId lexemeId, DateTimeOffset now, CancellationToken cancellationToken)
        => await cards.GetByLexemeAsync(learnerId, lexemeId, cancellationToken).ConfigureAwait(false)
           ?? LearnerCard.CreateNew(CardId.From(ids.NewId()), learnerId, lexemeId, now);
}

/// <summary>Records a passive or confirmed exposure from any surface (ADR 0015) and applies the introduction rule (T4).</summary>
public sealed class RecordExposureUseCase(ILearnerContext learner, ILearnerCardRepository cards, IExposureStore exposures, IIdGenerator ids, IClock clock, LearningDayResolver learningDays)
{
    public async Task<LearnerCard> ExecuteAsync(LexemeId lexemeId, ExposureSurface surface, ExposureKind kind, CancellationToken cancellationToken)
    {
        var learnerId = await learner.GetLearnerIdAsync(cancellationToken).ConfigureAwait(false);
        var now = clock.UtcNow;
        var card = await CardAccess.GetOrCreateAsync(cards, ids, learnerId, lexemeId, now, cancellationToken).ConfigureAwait(false);
        card.RecordExposure(kind, learningDays.Today(), now);
        await exposures.AppendAsync(new ExposureRecord(ids.NewId(), learnerId, lexemeId, surface, kind, now), cancellationToken).ConfigureAwait(false);
        await cards.UpsertAsync(card, cancellationToken).ConfigureAwait(false);
        return card;
    }
}

/// <summary>Opens a word in the app: a confirmed in-app exposure plus the data for the card (spec 03 S-11).</summary>
public sealed class OpenWordUseCase(IContentRepository content, RecordExposureUseCase recordExposure, IReviewScheduler scheduler, IClock clock)
{
    public async Task<Result<WordCardView>> ExecuteAsync(LexemeId lexemeId, CancellationToken cancellationToken)
    {
        var entry = await content.GetAsync(lexemeId, cancellationToken).ConfigureAwait(false);
        if (entry is null)
        {
            return DomainError.NotFound("lexeme.notFound", "Errors_Lexeme_NotFound");
        }

        var card = await recordExposure.ExecuteAsync(lexemeId, ExposureSurface.InApp, ExposureKind.Confirmed, cancellationToken).ConfigureAwait(false);
        var retrievability = card.Memory is null ? 0 : scheduler.Retrievability(card.ToSchedulingInput(), clock.UtcNow);
        return new WordCardView(entry, card.State, card.ScheduledExposures, card.ConfirmedExposures, retrievability, card.Due);
    }
}

/// <summary>«Вчити»: introduce the word explicitly so it enters the session queue today.</summary>
public sealed class IntroduceWordUseCase(ILearnerContext learner, ILearnerCardRepository cards, IIdGenerator ids, IClock clock)
{
    public async Task ExecuteAsync(LexemeId lexemeId, CancellationToken cancellationToken)
    {
        var learnerId = await learner.GetLearnerIdAsync(cancellationToken).ConfigureAwait(false);
        var card = await CardAccess.GetOrCreateAsync(cards, ids, learnerId, lexemeId, clock.UtcNow, cancellationToken).ConfigureAwait(false);
        card.Introduce(clock.UtcNow);
        await cards.UpsertAsync(card, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>«Знаю»: the learner claims the word; it leaves the plan until typed recall verifies it (spec 03 S-11, assumption).</summary>
public sealed class ClaimKnownUseCase(ILearnerContext learner, ILearnerCardRepository cards, IIdGenerator ids, IClock clock)
{
    public async Task ExecuteAsync(LexemeId lexemeId, CancellationToken cancellationToken)
    {
        var learnerId = await learner.GetLearnerIdAsync(cancellationToken).ConfigureAwait(false);
        var card = await CardAccess.GetOrCreateAsync(cards, ids, learnerId, lexemeId, clock.UtcNow, cancellationToken).ConfigureAwait(false);
        card.ClaimKnown(clock.UtcNow);
        await cards.UpsertAsync(card, cancellationToken).ConfigureAwait(false);
    }
}
