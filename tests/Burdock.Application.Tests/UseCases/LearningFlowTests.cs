using Burdock.Application.Ports;
using Burdock.Application.Tests.Fakes;
using Burdock.Application.UseCases.Content;
using Burdock.Application.UseCases.LearningDays;
using Burdock.Application.UseCases.Planning;
using Burdock.Application.UseCases.Sessions;
using Burdock.Application.UseCases.Words;
using Burdock.Domain.Exposure;
using Burdock.Domain.Learning;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;
using Burdock.TestKit;
using Microsoft.Extensions.Logging.Abstractions;

namespace Burdock.Application.Tests.UseCases;

/// <summary>End-to-end flow through the use cases with in-memory ports: bootstrap → plan → open → session → grade.</summary>
public sealed class LearningFlowTests
{
    private static readonly LearnerId Learner = LearnerId.From(Guid.Parse("0199a4c0-0000-7000-8000-00000000aaaa"));

    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 10, 5, 9, 0, 0, TimeSpan.Zero), TestTimeZones.KyivFixed);
    private readonly InMemoryContentRepository _content = new();
    private readonly InMemoryCardRepository _cards = new();
    private readonly InMemoryReviewLog _reviews = new();
    private readonly InMemoryExposureStore _exposures = new();
    private readonly SequentialIdGenerator _ids = new();
    private readonly FakeScheduler _scheduler = new();
    private readonly FakeLearnerContext _learner = new(Learner, LearnerSettings.Moderate(LanguagePair.UkrainianToEnglish, CefrLevel.A1, []) with { NewWordsPerDay = 5 });

    private LearningDayResolver Days => new(_clock);

    private static ImportedContentPack SeedPack(int count) => new("uk-en.free-pool", LanguagePair.UkrainianToEnglish, 1,
        Enumerable.Range(1, count).Select(n => new WordEntry(
            LexemeBuilder.ALexeme().WithId(Guid.Parse($"0199a500-0000-7000-8000-0000000000{n:x2}")).WithLemma($"word{n}").AtLevel(n % 3 == 0 ? CefrLevel.A2 : CefrLevel.A1).Build(),
            $"слово{n}", [], [$"word{n}"], null, null, $"Example {n}.", $"Приклад {n}.")).ToArray());

    [Fact]
    public async Task Bootstrap_imports_bundled_packs_once()
    {
        var useCase = new BootstrapContentUseCase(new InMemoryPackSource(SeedPack(12)), _content, _learner, NullLogger<BootstrapContentUseCase>.Instance);

        var first = await useCase.ExecuteAsync(TestContext.Current.CancellationToken);
        var second = await useCase.ExecuteAsync(TestContext.Current.CancellationToken);

        first.ShouldBe(new BootstrapContentResult(1, 0, 12));
        second.ShouldBe(new BootstrapContentResult(0, 1, 12));
    }

    [Fact]
    public async Task Plan_open_and_grade_move_a_word_through_its_lifecycle()
    {
        await _content.ImportPackAsync(SeedPack(12), TestContext.Current.CancellationToken);
        var plan = new GetDailyPlanUseCase(_learner, _content, _cards, _scheduler, _clock, Days);
        var record = new RecordExposureUseCase(_learner, _cards, _exposures, _ids, _clock, Days);
        var open = new OpenWordUseCase(_content, record, _scheduler, _clock);
        var grade = new GradeAnswerUseCase(_learner, _cards, _reviews, _scheduler, _ids, _clock, Days);

        var today = await plan.ExecuteAsync(TestContext.Current.CancellationToken);
        today.NewCount.ShouldBe(5);
        today.Words.ShouldAllBe(w => w.Reason == PlanReason.New);

        var first = today.Words[0].Entry;
        var view = await open.ExecuteAsync(first.Id, TestContext.Current.CancellationToken);
        view.IsSuccess.ShouldBeTrue();
        view.Value.State.ShouldBe(CardState.Introduced);
        view.Value.ConfirmedExposures.ShouldBe(1);
        _exposures.Records.Single().Surface.ShouldBe(ExposureSurface.InApp);

        var choice = new ChoiceItem(first, ExerciseMode.ChoiceTargetToNative, first.Lexeme.Lemma, [first.Translation, "x", "y", "z"], 0);
        var graded = await grade.ExecuteAsync(choice, new ExerciseOutcome(choice.Mode, Correct: true, TimeSpan.FromSeconds(2)), TestContext.Current.CancellationToken);
        graded.IsSuccess.ShouldBeTrue();
        graded.Value.Rating.ShouldBe(Rating.Good);
        graded.Value.CorrectAnswer.ShouldBe(first.Translation);
        _reviews.Entries.Count.ShouldBe(1);

        // Opening the same word again within the day keeps it in the plan as Learning, and the new-word budget shrinks by one.
        var later = await plan.ExecuteAsync(TestContext.Current.CancellationToken);
        later.Words.Count(w => w.Reason == PlanReason.Learning).ShouldBe(1);
        later.NewCount.ShouldBe(4);
    }

    [Fact]
    public async Task Session_builder_produces_a_matching_warmup_and_four_option_choices()
    {
        await _content.ImportPackAsync(SeedPack(24), TestContext.Current.CancellationToken);
        var plan = new GetDailyPlanUseCase(_learner, _content, _cards, _scheduler, _clock, Days);
        var builder = new BuildSessionUseCase(plan, _learner, _content, _cards, Days);

        var session = await builder.ExecuteAsync(5, TestContext.Current.CancellationToken);
        var again = await builder.ExecuteAsync(5, TestContext.Current.CancellationToken);

        session.Items[0].ShouldBeOfType<MatchingItem>().Entries.Count.ShouldBe(5);
        session.GradedCount.ShouldBe(5);
        foreach (var item in session.Items.Skip(1).Cast<ChoiceItem>())
        {
            item.Options.Count.ShouldBe(4);
            item.Options.Distinct().Count().ShouldBe(4);
            item.Options[item.CorrectIndex].ShouldBe(item.Entry.Translation);
        }

        again.Items.Select(i => i.Entry.Id).ShouldBe(session.Items.Select(i => i.Entry.Id));
    }

    [Fact]
    public async Task Matching_answers_are_rejected_by_grading()
    {
        var entry = SeedPack(1).Entries[0];
        var grade = new GradeAnswerUseCase(_learner, _cards, _reviews, _scheduler, _ids, _clock, Days);

        var result = await grade.ExecuteAsync(new MatchingItem([entry]), new ExerciseOutcome(ExerciseMode.Matching, true, TimeSpan.Zero), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("session.ungraded");
        _reviews.Entries.ShouldBeEmpty();
    }
}
