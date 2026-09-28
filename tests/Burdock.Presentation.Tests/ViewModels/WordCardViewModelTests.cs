using Burdock.Domain.Learning;
using Burdock.Presentation.Resources.Strings;
using Burdock.Presentation.ViewModels;
using ViewState = Burdock.Presentation.ViewModels.ViewState;

namespace Burdock.Presentation.Tests.ViewModels;

/// <summary>S-11 «Картка слова» (spec 03): guess-first translation, highlighted example, counters, «Знаю» / «Вчити».</summary>
public sealed class WordCardViewModelTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Load_fills_the_card_and_keeps_the_translation_hidden()
    {
        var (_, vm) = await OpenAsync(4);

        vm.State.ShouldBe(ViewState.Ready);
        vm.Lemma.ShouldBe("word4");
        vm.Ipa.ShouldBe("/wɜrd4/");
        vm.PartOfSpeech.ShouldBe("іменник");
        vm.TopicKey.ShouldBe("shopping");
        vm.Translation.ShouldBe("слово4");
        vm.HasAlsoMeans.ShouldBeTrue();
        vm.AlsoMeans.ShouldBe("варіант");
        vm.TranslationRevealed.ShouldBeFalse();
        (vm.ExampleBefore, vm.ExampleMatch, vm.ExampleAfter).ShouldBe(("Example with ", "word4", " inside."));
        vm.ExampleNative.ShouldBe("Приклад 4.");
        vm.HasExample.ShouldBeTrue();
        // Opening the card is a confirmed exposure: the word becomes «introduced» and the counters reflect it (ADR 0015).
        vm.StateLabel.ShouldBe(AppResources.Word_State_Introduced);
        vm.ConfirmedExposures.ShouldBe(1);
        vm.ScheduledExposures.ShouldBe(0);
        vm.HasRetrievability.ShouldBeFalse();
        vm.RetrievabilityPercent.ShouldBeEmpty();
    }

    [Fact]
    public async Task Reveal_shows_the_translation_and_the_query_flag_can_open_it_directly()
    {
        var (_, hidden) = await OpenAsync(3);
        var (_, revealed) = await OpenAsync(3, reveal: true);

        hidden.RevealCommand.Execute(null);

        hidden.TranslationRevealed.ShouldBeTrue();
        revealed.TranslationRevealed.ShouldBeTrue();
    }

    [Fact]
    public async Task A_missing_or_unknown_id_is_the_error_state()
    {
        var harness = await new ViewModelHarness().WithImportedWordsAsync(3);
        var withoutId = harness.Resolve<WordCardViewModel>();
        var unknown = harness.Resolve<WordCardViewModel>();
        unknown.ApplyQueryAttributes(new Dictionary<string, object>(StringComparer.Ordinal) { ["id"] = Guid.NewGuid().ToString("D") });

        await withoutId.LoadAsync(Ct);
        await unknown.LoadAsync(Ct);

        withoutId.State.ShouldBe(ViewState.Error);
        unknown.State.ShouldBe(ViewState.Error);
    }

    [Fact]
    public async Task Know_marks_the_card_known_and_goes_back()
    {
        var (harness, vm) = await OpenAsync(2);

        await vm.KnowCommand.ExecuteAsync(null);

        var card = await harness.Cards.GetByLexemeAsync(ViewModelHarness.Learner, ViewModelHarness.Word(2).Id, Ct);
        card.ShouldNotBeNull();
        card.State.ShouldBe(CardState.Known);
        harness.Navigation.BackCount.ShouldBe(1);
    }

    [Fact]
    public async Task Learn_keeps_the_card_in_the_learning_track_and_goes_back()
    {
        var (harness, vm) = await OpenAsync(2);

        await vm.LearnCommand.ExecuteAsync(null);

        var card = await harness.Cards.GetByLexemeAsync(ViewModelHarness.Learner, ViewModelHarness.Word(2).Id, Ct);
        card.ShouldNotBeNull();
        card.State.ShouldNotBe(CardState.New);
        card.State.ShouldNotBe(CardState.Known);
        harness.Navigation.BackCount.ShouldBe(1);
    }

    [Fact]
    public async Task Speak_reads_the_lemma_in_english()
    {
        var (harness, vm) = await OpenAsync(5);

        await vm.SpeakCommand.ExecuteAsync(null);

        harness.Speech.Spoken.ShouldBe(["word5"]);
    }

    [Theory]
    [InlineData("I bought a ticket yesterday.", "ticket", "", "I bought a ", "ticket", " yesterday.")]
    [InlineData("Two tickets, please.", "ticket", "tickets", "Two ", "tickets", ", please.")]
    [InlineData("Thanks a lot!", "thank you", "thanks", "", "Thanks", " a lot!")]
    [InlineData("Nothing here.", "ticket", "", "Nothing here.", "", "")]
    [InlineData("", "ticket", "", "", "", "")]
    public void Highlight_splits_the_example_around_the_longest_matching_form(string example, string lemma, string acceptedForm, string before, string match, string after)
    {
        var forms = acceptedForm.Length == 0 ? Array.Empty<string>() : [acceptedForm];

        WordCardViewModel.Highlight(example, lemma, forms).ShouldBe((before, match, after));
    }

    private static async Task<(ViewModelHarness Harness, WordCardViewModel Vm)> OpenAsync(int n, bool reveal = false)
    {
        var harness = await new ViewModelHarness().WithImportedWordsAsync(12);
        var vm = harness.Resolve<WordCardViewModel>();
        var query = new Dictionary<string, object>(StringComparer.Ordinal) { ["id"] = ViewModelHarness.Word(n).Id.Value.ToString("D") };
        if (reveal)
        {
            query["reveal"] = "1";
        }

        vm.ApplyQueryAttributes(query);
        await vm.LoadAsync(Ct);
        return (harness, vm);
    }
}
