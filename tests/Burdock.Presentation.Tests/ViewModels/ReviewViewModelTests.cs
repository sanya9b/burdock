using Burdock.Domain.Learning;
using Burdock.Domain.SharedKernel;
using Burdock.Presentation.Navigation;
using Burdock.Presentation.ViewModels;
using ViewState = Burdock.Presentation.ViewModels.ViewState;

namespace Burdock.Presentation.Tests.ViewModels;

/// <summary>S-10a «Повторимо» (owner decision 2026-11-12, log item 24): packs of five pairs, first outcome grades the word, mistakes demote it, the round settles XP.</summary>
public sealed class ReviewViewModelTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Load_builds_packs_of_five_from_due_learned_words()
    {
        var harness = await HarnessWithDueWordsAsync(7);
        var vm = harness.Resolve<ReviewViewModel>();
        vm.PackDwell = TimeSpan.Zero;

        await vm.LoadAsync(Ct);

        vm.State.ShouldBe(ViewState.Ready);
        vm.IsPlaying.ShouldBeTrue();
        vm.PackCount.ShouldBe(1);
        vm.PacksDone.ShouldBe(0);
        vm.MatchWords.Count.ShouldBe(5);
        vm.MatchMeanings.Count.ShouldBe(5);
        vm.ProgressText.ShouldContain("1");
    }

    [Fact]
    public async Task Without_a_full_pack_the_round_is_empty()
    {
        var harness = await HarnessWithDueWordsAsync(4);
        var vm = harness.Resolve<ReviewViewModel>();

        await vm.LoadAsync(Ct);

        vm.State.ShouldBe(ViewState.Empty);
        vm.IsPlaying.ShouldBeFalse();
    }

    [Fact]
    public async Task A_correct_pair_grades_good_and_keeps_the_word_learned()
    {
        var harness = await HarnessWithDueWordsAsync(5);
        var vm = harness.Resolve<ReviewViewModel>();
        vm.PackDwell = TimeSpan.Zero;
        await vm.LoadAsync(Ct);
        var word = vm.MatchWords[0];
        var meaning = vm.MatchMeanings.Single(m => m.Index == word.Index);

        await vm.PickMatchCommand.ExecuteAsync(word);
        word.Visual.ShouldBe(OptionVisual.Selected);
        await vm.PickMatchCommand.ExecuteAsync(meaning);

        word.Visual.ShouldBe(OptionVisual.Correct);
        meaning.Visual.ShouldBe(OptionVisual.Correct);
        vm.Graded.ShouldBe(1);
        vm.Correct.ShouldBe(1);
        vm.Again.ShouldBe(0);
        vm.Demoted.ShouldBeEmpty();
        var card = (await harness.Cards.GetByLexemeAsync(ViewModelHarness.Learner, LexemeOf(vm, word), Ct))!;
        card.State.ShouldBe(CardState.Learned);
        card.Due!.Value.ShouldBeGreaterThan(harness.Clock.UtcNow);
        harness.Reviews.Entries.Single().Mode.ShouldBe(ExerciseMode.MatchingReview);
        harness.Haptics.Pulses.ShouldBe(["Light"]);
    }

    [Fact]
    public async Task A_mistake_demotes_only_the_word_tapped_first_and_is_graded_once()
    {
        var harness = await HarnessWithDueWordsAsync(5);
        var vm = harness.Resolve<ReviewViewModel>();
        vm.PackDwell = TimeSpan.Zero;
        await vm.LoadAsync(Ct);
        var word = vm.MatchWords[0];
        var wrongMeaning = vm.MatchMeanings.First(m => m.Index != word.Index);
        var rightMeaning = vm.MatchMeanings.Single(m => m.Index == word.Index);

        await vm.PickMatchCommand.ExecuteAsync(word);
        await vm.PickMatchCommand.ExecuteAsync(wrongMeaning);

        vm.Graded.ShouldBe(1);
        vm.Again.ShouldBe(1);
        vm.MistakeTick.ShouldBe(1);
        vm.Demoted.ShouldBe([word.Text]);
        vm.HasDemoted.ShouldBeTrue();
        harness.Haptics.Pulses.ShouldBe(["Error"]);
        word.Visual.ShouldBe(OptionVisual.Default);
        wrongMeaning.Visual.ShouldBe(OptionVisual.Incorrect);
        var demoted = (await harness.Cards.GetByLexemeAsync(ViewModelHarness.Learner, LexemeOf(vm, word), Ct))!;
        demoted.State.ShouldNotBe(CardState.Learned);
        demoted.MasteryProgress.ShouldBe(0);
        var other = (await harness.Cards.GetByLexemeAsync(ViewModelHarness.Learner, LexemeOf(vm, wrongMeaning), Ct))!;
        other.State.ShouldBe(CardState.Learned, "the meaning tapped by error is not punished");
        harness.Reviews.Entries.Count.ShouldBe(1);

        // Matching the same word correctly afterwards completes the pair but does not grade it again.
        await vm.PickMatchCommand.ExecuteAsync(word);
        await vm.PickMatchCommand.ExecuteAsync(rightMeaning);

        word.Visual.ShouldBe(OptionVisual.Correct);
        vm.Graded.ShouldBe(1);
        vm.Correct.ShouldBe(0);
        harness.Reviews.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Finishing_every_pack_shows_the_summary_settles_rewards_and_returns_home()
    {
        var harness = await HarnessWithDueWordsAsync(10);
        var vm = harness.Resolve<ReviewViewModel>();
        vm.PackDwell = TimeSpan.Zero;
        await vm.LoadAsync(Ct);
        vm.PackCount.ShouldBe(2);

        await SolvePackAsync(vm);
        vm.PacksDone.ShouldBe(1);
        vm.IsSummary.ShouldBeFalse();
        vm.ProgressText.ShouldContain("2");
        await SolvePackAsync(vm);

        vm.PacksDone.ShouldBe(2);
        vm.IsSummary.ShouldBeTrue();
        vm.IsPlaying.ShouldBeFalse();
        vm.Graded.ShouldBe(10);
        vm.Correct.ShouldBe(10);
        vm.SummaryBody.ShouldContain("10");
        vm.HasDemoted.ShouldBeFalse();
        vm.ShowXp.ShouldBeTrue();
        harness.Rewards.State.ShouldNotBeNull();
        harness.Rewards.State.Xp.ShouldBeGreaterThan(0);
        harness.Sync.Reasons.ShouldContain("review");
        harness.Widgets.Reasons.ShouldContain("review");

        await vm.DoneCommand.ExecuteAsync(null);
        harness.Navigation.Last.Route.ShouldBe(Routes.Today.Root);
    }

    [Fact]
    public async Task Closing_halfway_keeps_the_graded_pairs_and_settles_once()
    {
        var harness = await HarnessWithDueWordsAsync(5);
        var vm = harness.Resolve<ReviewViewModel>();
        vm.PackDwell = TimeSpan.Zero;
        await vm.LoadAsync(Ct);
        var word = vm.MatchWords[0];
        await vm.PickMatchCommand.ExecuteAsync(word);
        await vm.PickMatchCommand.ExecuteAsync(vm.MatchMeanings.Single(m => m.Index == word.Index));

        await vm.CloseCommand.ExecuteAsync(null);

        harness.Navigation.BackCount.ShouldBe(1);
        harness.Reviews.Entries.Count.ShouldBe(1);
        harness.Rewards.State.ShouldNotBeNull();
        harness.Rewards.State.Xp.ShouldBeGreaterThan(0);
    }

    private static async Task SolvePackAsync(ReviewViewModel vm)
    {
        foreach (var word in vm.MatchWords.ToArray())
        {
            var meaning = vm.MatchMeanings.Single(m => m.Index == word.Index);
            await vm.PickMatchCommand.ExecuteAsync(word);
            await vm.PickMatchCommand.ExecuteAsync(meaning);
        }
    }

    private static LexemeId LexemeOf(ReviewViewModel vm, OptionViewModel option)
    {
        var lemma = option.IsWord ? option.Text : vm.MatchWords.Single(w => w.Index == option.Index).Text;
        var n = int.Parse(lemma["word".Length..], System.Globalization.CultureInfo.InvariantCulture);
        return ViewModelHarness.Word(n).Id;
    }

    /// <summary>Imports <paramref name="count"/> words and marks every one of them learned and due since yesterday.</summary>
    private static async Task<ViewModelHarness> HarnessWithDueWordsAsync(int count)
    {
        var harness = await new ViewModelHarness().WithImportedWordsAsync(count);
        var now = harness.Clock.UtcNow;
        for (var n = 1; n <= count; n++)
        {
            var card = LearnerCard.Restore(CardId.From(Guid.NewGuid()), ViewModelHarness.Learner, ViewModelHarness.Word(n).Id, now.AddDays(-10), CardState.Learned,
                MemoryState.Create(2 + n, 5), null, now.AddDays(-1), now.AddDays(-3), now.AddDays(-10), now.AddDays(-3), 6, 0, 0, 1, 1, [], [], LearningPolicy.MasteryStreak);
            await harness.Cards.UpsertAsync(card, Ct);
        }

        return harness;
    }
}
