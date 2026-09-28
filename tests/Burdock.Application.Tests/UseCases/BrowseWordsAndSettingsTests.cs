using Burdock.Application.Ports;
using Burdock.Application.Tests.Fakes;
using Burdock.Application.UseCases.Settings;
using Burdock.Application.UseCases.Words;
using Burdock.Domain.Learning;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;
using Burdock.TestKit;

namespace Burdock.Application.Tests.UseCases;

public sealed class BrowseWordsAndSettingsTests
{
    private static readonly LearnerId Learner = LearnerId.From(Guid.Parse("00000000-0000-0000-0000-000000000042"));

    [Fact]
    public void Filter_narrows_by_status_level_and_topic()
    {
        var journey = Word("journey", "подорож", CefrLevel.A2, "travel", CardState.Review);
        var hire = Word("hire", "наймати", CefrLevel.B1, "work", CardState.Learning);
        var relearn = Word("deliver", "доставляти", CefrLevel.B1, "work", CardState.Relearning);
        var fresh = Word("apple", "яблуко", CefrLevel.A1, "food");
        var all = new[] { journey, hire, relearn, fresh };

        BrowseWordsUseCase.Filter(all, new WordsQuery(Status: LibraryStatus.Learning)).Select(w => w.Entry.Lexeme.Lemma).ShouldBe(["deliver", "hire"]);
        BrowseWordsUseCase.Filter(all, new WordsQuery(Level: CefrLevel.B1, Topic: TopicId.From("work"))).Count().ShouldBe(2);
        BrowseWordsUseCase.Filter(all, new WordsQuery(Status: LibraryStatus.New)).Single().Entry.Lexeme.Lemma.ShouldBe("apple");
        BrowseWordsUseCase.Filter(all, WordsQuery.All).Select(w => w.Entry.Lexeme.Lemma).ShouldBe(["apple", "deliver", "hire", "journey"]);
    }

    [Fact]
    public void Search_matches_lemma_forms_and_translations_after_normalisation()
    {
        var journey = Word("journey", "подорож", CefrLevel.A2, "travel", alternatives: ["мандрівка"]);
        var apple = Word("apple", "яблуко", CefrLevel.A1, "food");
        var all = new[] { journey, apple };

        BrowseWordsUseCase.Filter(all, new WordsQuery(Search: "  JOUR ")).Single().ShouldBe(journey);
        BrowseWordsUseCase.Filter(all, new WordsQuery(Search: "Подо")).Single().ShouldBe(journey);
        BrowseWordsUseCase.Filter(all, new WordsQuery(Search: "мандр")).Single().ShouldBe(journey);
        BrowseWordsUseCase.Filter(all, new WordsQuery(Search: "zzz")).ShouldBeEmpty();
    }

    [Fact]
    public async Task Execute_joins_content_with_the_learner_cards()
    {
        var content = new InMemoryContentRepository();
        var journey = Entry("journey", "подорож", CefrLevel.A2, "travel");
        var apple = Entry("apple", "яблуко", CefrLevel.A1, "food");
        await content.ImportPackAsync(new ImportedContentPack("test", LanguagePair.UkrainianToEnglish, 1, [journey, apple]), CancellationToken.None);
        var cards = new InMemoryCardRepository();
        var card = LearnerCard.CreateNew(CardId.From(Guid.NewGuid()), Learner, journey.Id, DateTimeOffset.UtcNow);
        card.Introduce(DateTimeOffset.UtcNow);
        await cards.UpsertAsync(card, CancellationToken.None);
        var settings = LearnerSettings.Moderate(LanguagePair.UkrainianToEnglish, CefrLevel.A1, [TopicId.From("food")]);
        var useCase = new BrowseWordsUseCase(new FakeLearnerContext(Learner, settings), content, cards);

        var words = await useCase.ExecuteAsync(WordsQuery.All, CancellationToken.None);

        // The library ignores the learner's level/topic settings: every word of the pair is listed.
        words.Select(w => w.Entry.Lexeme.Lemma).ShouldBe(["apple", "journey"]);
        words.Single(w => w.Entry.Id == journey.Id).Status.ShouldBe(LibraryStatus.Introduced);
        words.Single(w => w.Entry.Id == apple.Id).Status.ShouldBe(LibraryStatus.New);
    }

    [Fact]
    public async Task Update_settings_validates_before_saving()
    {
        var initial = LearnerSettings.Moderate(LanguagePair.UkrainianToEnglish, CefrLevel.A1, [TopicId.From("food")]);
        var context = new FakeLearnerContext(Learner, initial);
        var useCase = new UpdateLearnerSettingsUseCase(context);

        var rejected = await useCase.ExecuteAsync(initial with { NewWordsPerDay = LearnerSettings.MaxNewWordsPerDay + 1 }, CancellationToken.None);
        rejected.IsFailure.ShouldBeTrue();
        context.Settings.NewWordsPerDay.ShouldBe(initial.NewWordsPerDay);

        var accepted = await useCase.ExecuteAsync(initial with { NewWordsPerDay = 12, Level = CefrLevel.B1 }, CancellationToken.None);
        accepted.IsSuccess.ShouldBeTrue();
        context.Settings.NewWordsPerDay.ShouldBe(12);
        context.Settings.Level.ShouldBe(CefrLevel.B1);
    }

    private static WordEntry Entry(string lemma, string translation, CefrLevel level, string topic, string[]? alternatives = null)
        => new(LexemeBuilder.ALexeme().WithLemma(lemma).AtLevel(level).InTopics(topic).WithId(Guid.NewGuid()).Build(), translation, alternatives ?? [], [lemma], null, null, null, null);

    private static LibraryWord Word(string lemma, string translation, CefrLevel level, string topic, CardState state = CardState.New, string[]? alternatives = null)
        => new(Entry(lemma, translation, level, topic, alternatives), state, state.ToLibraryStatus(), null, 0, 0);
}
