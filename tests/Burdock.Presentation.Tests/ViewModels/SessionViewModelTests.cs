using Burdock.Application.UseCases.Sessions;
using Burdock.Domain.Learning;
using Burdock.Presentation.Navigation;
using Burdock.Presentation.ViewModels;
using ViewState = Burdock.Presentation.ViewModels.ViewState;

namespace Burdock.Presentation.Tests.ViewModels;

/// <summary>S-12 «Сесія» (spec 03, spec 06 §3.7, spec 07 §3.8): warm-up matching, choices with feedback, combo, hand-over to the summary.</summary>
public sealed class SessionViewModelTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Load_opens_with_the_matching_warmup_and_plans_four_answers_per_word()
    {
        var (_, vm) = await StartAsync();

        vm.State.ShouldBe(ViewState.Ready);
        vm.IsMatching.ShouldBeTrue();
        vm.IsChoice.ShouldBeFalse();
        vm.ItemCount.ShouldBe(21); // the warm-up + five new words × four correct answers in a row (mastery drill)
        vm.MasteryTarget.ShouldBe(LearningPolicy.MasteryStreak);
        vm.MatchWords.Count.ShouldBe(5);
        vm.MatchMeanings.Count.ShouldBe(5);
        vm.ProgressText.ShouldContain("1");
        vm.Answered.ShouldBeFalse();
        vm.ShowCombo.ShouldBeFalse();
    }

    [Fact]
    public async Task An_empty_plan_is_the_empty_state()
    {
        var vm = new ViewModelHarness().Resolve<SessionViewModel>();

        await vm.LoadAsync(Ct);

        vm.State.ShouldBe(ViewState.Empty);
    }

    [Fact]
    public async Task Matching_all_pairs_completes_the_warmup_and_a_mismatch_flashes_the_meaning()
    {
        var (_, vm) = await StartAsync();
        var firstWord = vm.MatchWords[0];
        var wrongMeaning = vm.MatchMeanings.First(m => m.Index != firstWord.Index);

        vm.PickMatchCommand.Execute(firstWord);
        firstWord.Visual.ShouldBe(OptionVisual.Selected);
        vm.PickMatchCommand.Execute(wrongMeaning);

        wrongMeaning.Visual.ShouldBe(OptionVisual.Incorrect);
        firstWord.Visual.ShouldBe(OptionVisual.Default);
        vm.Answered.ShouldBeFalse();

        CompleteWarmup(vm);

        vm.Answered.ShouldBeTrue();
        vm.LastAnswerCorrect.ShouldBeTrue();
        vm.AnswerTick.ShouldBe(1);
        vm.FeedbackIsPositive.ShouldBeTrue();
        vm.MatchWords.ShouldAllBe(w => w.Visual == OptionVisual.Correct);
        vm.CompletedCount.ShouldBe(1);
    }

    [Fact]
    public async Task Correct_answers_build_a_combo_and_a_wrong_one_resets_it()
    {
        var (_, vm) = await StartAsync();
        CompleteWarmup(vm);

        vm.NextCommand.Execute(null);
        vm.IsChoice.ShouldBeTrue();
        vm.Options.Count.ShouldBe(4);
        await vm.ChooseCommand.ExecuteAsync(CorrectOption(vm));
        vm.LastAnswerCorrect.ShouldBeTrue();
        vm.FeedbackIsPositive.ShouldBeTrue();
        vm.ComboCount.ShouldBe(1);
        vm.ShowCombo.ShouldBeFalse();

        vm.NextCommand.Execute(null);
        await vm.ChooseCommand.ExecuteAsync(CorrectOption(vm));
        vm.ComboCount.ShouldBe(2);
        vm.ShowCombo.ShouldBeTrue();
        vm.ComboText.ShouldContain("2");

        vm.NextCommand.Execute(null);
        var correct = CorrectOption(vm);
        var wrong = vm.Options.First(o => !ReferenceEquals(o, correct));
        await vm.ChooseCommand.ExecuteAsync(wrong);

        vm.LastAnswerCorrect.ShouldBeFalse();
        vm.FeedbackIsPositive.ShouldBeFalse();
        vm.HasFeedbackDetail.ShouldBeTrue();
        vm.FeedbackDetail.ShouldContain(correct.Text);
        vm.ComboCount.ShouldBe(0);
        vm.ShowCombo.ShouldBeFalse();
        wrong.Visual.ShouldBe(OptionVisual.Incorrect);
        correct.Visual.ShouldBe(OptionVisual.Correct);
        vm.Options.Where(o => !ReferenceEquals(o, correct) && !ReferenceEquals(o, wrong)).ShouldAllBe(o => o.Visual == OptionVisual.Disabled);
        vm.AnswerTick.ShouldBe(4);

        // A second tap on the same exercise is ignored.
        await vm.ChooseCommand.ExecuteAsync(correct);
        vm.AnswerTick.ShouldBe(4);
    }

    [Fact]
    public async Task Finishing_hands_the_counters_to_the_summary()
    {
        var (harness, vm) = await StartAsync();
        CompleteWarmup(vm);
        var reviewsBefore = harness.Reviews.Entries.Count;

        // Mastery drill: every word is asked until four correct answers in a row, so the session has 5 × 4 graded steps.
        var answered = 0;
        do
        {
            vm.NextCommand.Execute(null);
            await AnswerCorrectlyAsync(vm);
            answered++;
        }
        while (!vm.IsLast && answered < 40);

        answered.ShouldBe(20);
        vm.IsLast.ShouldBeTrue();
        vm.MasteryStreak.ShouldBe(4);
        vm.NextCommand.Execute(null);

        harness.Reviews.Entries.Count.ShouldBe(reviewsBefore + 20);
        var cards = await harness.Cards.ListAsync(ViewModelHarness.Learner, Ct);
        cards.Values.Count(c => c.State == CardState.Learned).ShouldBe(5);
        harness.Navigation.Last.Route.ShouldBe(Routes.Learn.Summary);
        var parameters = harness.Navigation.Last.Parameters!;
        parameters["graded"].ShouldBe("20");
        parameters["correct"].ShouldBe("20");
        parameters["again"].ShouldBe("0");
        parameters["learned"].ShouldBe("5");
        parameters["warmup"].ShouldBe("1");
        parameters["seconds"].ShouldBe("0"); // the clock is frozen
        Guid.TryParse(parameters["session"].ToString(), out _).ShouldBeTrue();
    }

    [Fact]
    public async Task A_wrong_answer_resets_the_word_counter_and_queues_it_again()
    {
        var (_, vm) = await StartAsync();
        CompleteWarmup(vm);
        vm.NextCommand.Execute(null);
        var word = vm.CurrentItem!.Entry.Id;
        var total = vm.ItemCount;

        var correct = CorrectOption(vm);
        await vm.ChooseCommand.ExecuteAsync(vm.Options.First(o => !ReferenceEquals(o, correct)));

        vm.MasteryStreak.ShouldBe(0);
        vm.ItemCount.ShouldBe(total + 1, "a reset costs one more step");
        vm.IsLast.ShouldBeFalse();

        // The word comes back after the other four words of the first pass.
        var seen = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            vm.NextCommand.Execute(null);
            seen.Add(vm.CurrentItem!.Entry.Id.Value);
            await AnswerCorrectlyAsync(vm);
        }

        seen[^1].ShouldBe(word.Value);
        seen.Take(4).ShouldNotContain(word.Value);
        vm.MasteryStreak.ShouldBe(1);
    }

    [Fact]
    public async Task Close_leaves_the_session()
    {
        var (harness, vm) = await StartAsync();

        await vm.CloseCommand.ExecuteAsync(null);

        harness.Navigation.BackCount.ShouldBe(1);
    }

    private static async Task<(ViewModelHarness Harness, SessionViewModel Vm)> StartAsync()
    {
        var harness = await new ViewModelHarness().WithImportedWordsAsync(12);
        var vm = harness.Resolve<SessionViewModel>();
        vm.ApplyQueryAttributes(new Dictionary<string, object>(StringComparer.Ordinal) { ["size"] = "10" });
        await vm.LoadAsync(Ct);
        return (harness, vm);
    }

    private static void CompleteWarmup(SessionViewModel vm)
    {
        foreach (var word in vm.MatchWords.ToList())
        {
            vm.PickMatchCommand.Execute(word);
            vm.PickMatchCommand.Execute(vm.MatchMeanings.First(m => m.Index == word.Index));
        }
    }

    /// <summary>Answers the current exercise correctly whatever its kind (the drill escalates from choices to typed recall).</summary>
    private static async Task AnswerCorrectlyAsync(SessionViewModel vm)
    {
        switch (vm.CurrentItem)
        {
            case ChoiceItem choice:
                await vm.ChooseCommand.ExecuteAsync(vm.Options[choice.CorrectIndex]);
                break;
            case TypedItem typed:
                vm.TypedAnswer = typed.AcceptedForms[0];
                await vm.CheckTypedCommand.ExecuteAsync(null);
                break;
            default:
                throw new InvalidOperationException("No graded exercise is on screen.");
        }
    }

    /// <summary>The stimulus is «wordN» (target → native), so the correct option reads «словоN».</summary>
    private static OptionViewModel CorrectOption(SessionViewModel vm)
    {
        var number = vm.Stimulus["word".Length..];
        return vm.Options.Single(o => string.Equals(o.Text, $"слово{number}", StringComparison.Ordinal));
    }
}
