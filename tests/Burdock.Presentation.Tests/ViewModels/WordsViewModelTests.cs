using Burdock.Presentation.Navigation;
using Burdock.Presentation.ViewModels;
using ViewState = Burdock.Presentation.ViewModels.ViewState;

namespace Burdock.Presentation.Tests.ViewModels;

/// <summary>S-14 «Слова» (spec 03): debounced search, status chips, level and category pickers, empty states, opening a card.</summary>
public sealed class WordsViewModelTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Load_lists_every_word_with_its_status_and_a_count()
    {
        var (_, vm) = await OpenAsync();

        vm.State.ShouldBe(ViewState.Ready);
        vm.Words.Count.ShouldBe(12);
        vm.CountText.ShouldContain("12");
        vm.Words.ShouldAllBe(w => w.StatusKey == "New");
        vm.Words[0].Lemma.ShouldBe("word1");
        vm.Words[0].Level.ShouldBe("A1");
        vm.Words[0].Accessibility.ShouldContain("слово1");
        vm.StatusChips.Single(c => c.IsSelected).Key.ShouldBe("all");
    }

    [Fact]
    public async Task Search_filters_after_the_debounce_and_has_its_own_empty_state()
    {
        var (_, vm) = await OpenAsync();

        vm.SearchText = "word1";
        await Task.Delay(450, Ct);

        vm.Words.Select(w => w.Lemma).ShouldBe(["word1", "word10", "word11", "word12"]);
        vm.IsSearchEmpty.ShouldBeFalse();

        vm.SearchText = "zzz";
        await Task.Delay(450, Ct);

        vm.Words.ShouldBeEmpty();
        vm.IsSearchEmpty.ShouldBeTrue();
        vm.IsFilterEmpty.ShouldBeFalse();
        vm.State.ShouldBe(ViewState.Empty);

        vm.ClearSearchCommand.Execute(null);
        await Task.Delay(450, Ct);

        vm.Words.Count.ShouldBe(12);
    }

    [Fact]
    public async Task Status_chips_filter_the_list()
    {
        var (_, vm) = await OpenAsync();

        await vm.SelectStatusCommand.ExecuteAsync(vm.StatusChips.Single(c => c.Key == "Learned"));

        vm.Words.ShouldBeEmpty();
        vm.IsFilterEmpty.ShouldBeTrue();
        vm.State.ShouldBe(ViewState.Empty);
        vm.StatusChips.Single(c => c.IsSelected).Key.ShouldBe("Learned");

        await vm.SelectStatusCommand.ExecuteAsync(vm.StatusChips.Single(c => c.Key == "New"));

        vm.Words.Count.ShouldBe(12);
    }

    [Fact]
    public async Task The_category_picker_offers_all_nine_categories_and_filters()
    {
        var (harness, vm) = await OpenAsync();
        harness.Sheet.Answers.Enqueue("Покупки");

        await vm.PickTopicCommand.ExecuteAsync(null);

        harness.Sheet.OfferedOptions[0].Count.ShouldBe(10); // «Усі категорії» + 9
        harness.Sheet.OfferedOptions[0].ShouldContain("Військова справа");
        vm.HasTopicFilter.ShouldBeTrue();
        vm.TopicChipText.ShouldBe("Покупки");
        vm.Words.Count.ShouldBe(6);
        vm.Words.Select(w => int.Parse(w.Lemma.AsSpan("word".Length), System.Globalization.CultureInfo.InvariantCulture)).ShouldAllBe(n => n % 2 == 0);
    }

    [Fact]
    public async Task The_level_picker_filters_and_cancelling_keeps_the_current_filter()
    {
        var (harness, vm) = await OpenAsync();

        harness.Sheet.Answers.Enqueue("A2");
        await vm.PickLevelCommand.ExecuteAsync(null);

        vm.HasLevelFilter.ShouldBeTrue();
        vm.LevelChipText.ShouldBe("A2");
        vm.Words.Select(w => w.Lemma).ShouldBe(["word12", "word3", "word6", "word9"]);

        await vm.PickLevelCommand.ExecuteAsync(null); // no answer queued = cancelled

        vm.HasLevelFilter.ShouldBeTrue();
        vm.Words.Count.ShouldBe(4);
    }

    [Fact]
    public async Task Opening_a_row_navigates_to_the_word_card()
    {
        var (harness, vm) = await OpenAsync();

        await vm.OpenCommand.ExecuteAsync(vm.Words[2]);

        harness.Navigation.Last.Route.ShouldBe(Routes.Words.Word);
        harness.Navigation.Last.Parameters!["id"].ShouldBe(vm.Words[2].LexemeId.ToString("D"));
    }

    private static async Task<(ViewModelHarness Harness, WordsViewModel Vm)> OpenAsync()
    {
        var harness = await new ViewModelHarness().WithImportedWordsAsync(12);
        var vm = harness.Resolve<WordsViewModel>();
        await vm.LoadAsync(Ct);
        return (harness, vm);
    }
}
