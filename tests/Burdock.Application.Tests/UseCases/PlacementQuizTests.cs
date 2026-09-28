using Burdock.Application.Ports;
using Burdock.Application.UseCases.Onboarding;
using Burdock.Domain.Learning;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;
using Burdock.TestKit;
using Burdock.TestKit.Fakes;

namespace Burdock.Application.Tests.UseCases;

/// <summary>S-03a: the placement mini-test is drawn from the local content across levels, easy first.</summary>
public sealed class PlacementQuizTests
{
    private static readonly LearnerId Learner = LearnerId.From(Guid.Parse("00000000-0000-0000-0000-000000000088"));

    [Fact]
    public async Task Builds_eight_questions_across_the_levels_the_pool_offers()
    {
        var content = new InMemoryContentRepository();
        await content.ImportPackAsync(Pack(12), TestContext.Current.CancellationToken);
        var useCase = new BuildPlacementQuizUseCase(Context(), content, new FakeRandom(7));

        var quiz = await useCase.ExecuteAsync(TestContext.Current.CancellationToken);
        var again = await new BuildPlacementQuizUseCase(Context(), content, new FakeRandom(7)).ExecuteAsync(TestContext.Current.CancellationToken);

        quiz.Count.ShouldBe(LevelPlacement.QuestionCount);
        quiz.Select(e => e.Lexeme.Level).ShouldBe([CefrLevel.A1, CefrLevel.A1, CefrLevel.A1, CefrLevel.A1, CefrLevel.A2, CefrLevel.A2, CefrLevel.A2, CefrLevel.A2]);
        quiz.Select(e => e.Id).Distinct().Count().ShouldBe(8);
        again.Select(e => e.Id).ShouldBe(quiz.Select(e => e.Id));
    }

    [Fact]
    public async Task Returns_nothing_without_content()
    {
        var useCase = new BuildPlacementQuizUseCase(Context(), new InMemoryContentRepository(), new FakeRandom(1));

        (await useCase.ExecuteAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty();
    }

    private static FakeLearnerContext Context() => new(Learner, LearnerSettings.Moderate(LanguagePair.UkrainianToEnglish, CefrLevel.A1, []));

    // Every third word is A2, the rest A1 (like the ViewModel harness).
    private static ImportedContentPack Pack(int count) => new("uk-en.free-pool", LanguagePair.UkrainianToEnglish, 1,
        Enumerable.Range(1, count).Select(n => new WordEntry(
            LexemeBuilder.ALexeme().WithId(Guid.Parse($"0199a500-0000-7000-8000-0000000000{n:x2}")).WithLemma($"word{n}").AtLevel(n % 3 == 0 ? CefrLevel.A2 : CefrLevel.A1).Build(),
            $"слово{n}", [], [$"word{n}"], null, null, $"Example {n}.", $"Приклад {n}.")).ToArray());
}
