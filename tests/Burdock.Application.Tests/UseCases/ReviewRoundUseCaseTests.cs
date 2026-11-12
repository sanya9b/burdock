using Burdock.Application.Ports;
using Burdock.Application.UseCases.LearningDays;
using Burdock.Application.UseCases.Reviews;
using Burdock.Domain.Learning;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Sync;
using Burdock.Domain.Vocabulary;
using Burdock.TestKit;
using Burdock.TestKit.Fakes;

namespace Burdock.Application.Tests.UseCases;

/// <summary>«Повторимо» (owner decision 2026-11-12, log item 24): the round from due learned words and the grading of one pair.</summary>
public sealed class ReviewRoundUseCaseTests
{
    private static readonly LearnerId Learner = LearnerId.From(Guid.Parse("0199a4c0-0000-7000-8000-00000000dddd"));
    private static readonly DateTimeOffset T0 = new(2026, 11, 12, 7, 0, 0, TimeSpan.Zero);

    private readonly FakeClock _clock = new(T0, TestTimeZones.KyivFixed);
    private readonly InMemoryContentRepository _content = new();
    private readonly InMemoryCardRepository _cards = new();
    private readonly InMemoryReviewLog _reviews = new();
    private readonly InMemoryProgressOutbox _outbox = new();
    private readonly FakeLearnerContext _learner = new(Learner, LearnerSettings.Moderate(LanguagePair.UkrainianToEnglish, CefrLevel.A1, []));
    private readonly FakeScheduler _scheduler = new();
    private readonly SequentialIdGenerator _ids = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_round_packs_due_learned_words_by_five_with_their_entries()
    {
        var entries = await SeedAsync(8);
        foreach (var (entry, index) in entries.Select((e, i) => (e, i)))
        {
            await _cards.UpsertAsync(Learned(entry.Id, dueInDays: index < 7 ? -1 : 1, stability: 2 + index), Ct);
        }

        var round = await Round().ExecuteAsync(Ct);

        round.DueCount.ShouldBe(7);
        round.IsAvailable.ShouldBeTrue();
        round.Packs.Count.ShouldBe(1);
        round.WordCount.ShouldBe(5);
        round.Packs[0].Mode.ShouldBe(ExerciseMode.Matching);
        round.Packs[0].Entries.Select(e => e.Id).ShouldBe(entries.Take(5).Select(e => e.Id), "lowest retrievability first");
    }

    [Fact]
    public async Task Fewer_than_five_due_words_give_no_round_but_report_the_count()
    {
        var entries = await SeedAsync(4);
        foreach (var entry in entries)
        {
            await _cards.UpsertAsync(Learned(entry.Id, dueInDays: -1, stability: 2), Ct);
        }

        var round = await Round().ExecuteAsync(Ct);

        round.DueCount.ShouldBe(4);
        round.IsAvailable.ShouldBeFalse();
        round.Packs.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_correct_pair_keeps_the_word_learned_and_moves_its_review_further()
    {
        var entry = (await SeedAsync(1))[0];
        await _cards.UpsertAsync(Learned(entry.Id, dueInDays: -1, stability: 2), Ct);

        var result = await Grade().ExecuteAsync(entry.Id, correct: true, TimeSpan.FromSeconds(2), Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Rating.ShouldBe(Rating.Good);
        result.Value.LeftLearned.ShouldBeFalse();
        var card = (await _cards.GetByLexemeAsync(Learner, entry.Id, Ct))!;
        card.State.ShouldBe(CardState.Learned);
        card.MasteryProgress.ShouldBe(LearningPolicy.MasteryStreak);
        card.Due!.Value.ShouldBeGreaterThan(T0);
        _reviews.Entries.Single().Mode.ShouldBe(ExerciseMode.MatchingReview);
        _outbox.Events.Single().Type.ShouldBe(ProgressEventTypes.ReviewLog);
    }

    [Fact]
    public async Task A_mistake_sends_the_word_back_to_learning_with_an_empty_ring()
    {
        var entry = (await SeedAsync(1))[0];
        await _cards.UpsertAsync(Learned(entry.Id, dueInDays: -1, stability: 2), Ct);

        var result = await Grade().ExecuteAsync(entry.Id, correct: false, TimeSpan.FromSeconds(3), Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Rating.ShouldBe(Rating.Again);
        result.Value.LeftLearned.ShouldBeTrue();
        var card = (await _cards.GetByLexemeAsync(Learner, entry.Id, Ct))!;
        card.State.ShouldNotBe(CardState.Learned);
        card.MasteryProgress.ShouldBe(0);
        card.LearnedAt.ShouldBeNull();
        card.Lapses.ShouldBe(1);
        (await Round().ExecuteAsync(Ct)).DueCount.ShouldBe(0);
    }

    [Fact]
    public async Task Grading_an_unknown_word_fails_without_side_effects()
    {
        var entry = (await SeedAsync(1))[0];

        var result = await Grade().ExecuteAsync(entry.Id, correct: true, TimeSpan.Zero, Ct);

        result.IsFailure.ShouldBeTrue();
        _reviews.Entries.ShouldBeEmpty();
        _outbox.Events.ShouldBeEmpty();
    }

    private GetReviewRoundUseCase Round() => new(_learner, _cards, _content, _scheduler, _clock);

    private GradeReviewPairUseCase Grade() => new(_learner, _cards, _reviews, _scheduler, _ids, _clock, new LearningDayResolver(_clock), _outbox);

    private async Task<IReadOnlyList<WordEntry>> SeedAsync(int count)
    {
        var entries = Enumerable.Range(1, count).Select(n => new WordEntry(
            LexemeBuilder.ALexeme().WithId(Guid.Parse($"0199a4c0-0000-7000-8000-0000000000{n:x2}")).WithLemma($"word{n}").Build(),
            $"слово{n}", [], [$"word{n}"], "/wɜrd/", null, "Example sentence.", "Приклад.")).ToArray();
        await _content.ImportPackAsync(new ImportedContentPack("test", LanguagePair.UkrainianToEnglish, 1, entries), Ct);
        return entries;
    }

    private static LearnerCard Learned(LexemeId lexemeId, int dueInDays, double stability)
        => LearnerCard.Restore(CardId.From(Guid.NewGuid()), Learner, lexemeId, T0.AddDays(-10), CardState.Learned, MemoryState.Create(stability, 5), null,
            T0.AddDays(dueInDays), T0.AddDays(-3), T0.AddDays(-10), T0.AddDays(-3), 6, 0, 0, 1, 1, [], [], LearningPolicy.MasteryStreak);
}
