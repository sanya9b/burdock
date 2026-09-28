using Burdock.Domain.Learning;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;
using Burdock.Presentation.Resources.Strings;
using Burdock.Presentation.ViewModels;

namespace Burdock.Presentation.Tests.ViewModels;

/// <summary>Onboarding S-02…S-09 (spec 03 flow 3.5.1): nickname validation, step gating, skip semantics, notification answer, persistence on the privacy step.</summary>
public sealed class OnboardingViewModelTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Load_suggests_a_nickname_and_shows_a_sample_word()
    {
        var (_, vm) = await StartAsync();

        vm.Nickname.ShouldContain(AppResources.Onboarding_Nickname_Noun);
        vm.SampleWord.ShouldStartWith("word");
        vm.SampleTranslation.ShouldStartWith("слово");
        vm.CurrentStep.ShouldBe(OnboardingStep.Welcome);
        vm.CanGoBack.ShouldBeFalse();
        vm.PrimaryText.ShouldBe(AppResources.Onboarding_Go_Button);
    }

    [Fact]
    public async Task Nine_categories_are_offered_with_none_preselected()
    {
        var (_, vm) = await StartAsync();

        vm.TopicChips.Count.ShouldBe(9);
        vm.TopicChips.Single(c => c.Key == "military").Label.ShouldBe("Військова справа");
        vm.TopicChips.ShouldAllBe(c => !c.IsSelected);
        vm.CanContinueCategories.ShouldBeFalse();
        vm.LevelOptions.Single(o => o.IsSelected).Key.ShouldBe("A2");
        vm.PaceOptions.Single(p => p.IsSelected).Pace.ShouldBe(PacePreset.Moderate);
    }

    [Fact]
    public async Task An_invalid_nickname_blocks_the_first_step()
    {
        var (_, vm) = await StartAsync();

        vm.Nickname = "x";
        await vm.NextCommand.ExecuteAsync(null);

        vm.NicknameError.ShouldBe(AppResources.Onboarding_Nickname_Error);
        vm.CurrentStep.ShouldBe(OnboardingStep.Welcome);

        vm.Nickname = "Оля";
        await vm.NextCommand.ExecuteAsync(null);

        vm.NicknameError.ShouldBeEmpty();
        vm.CurrentStep.ShouldBe(OnboardingStep.Level);
        vm.CanGoBack.ShouldBeTrue();
    }

    [Fact]
    public async Task Categories_require_at_least_one_choice_and_the_counter_follows_the_selection()
    {
        var (_, vm) = await GoToAsync(OnboardingStep.Categories);

        await vm.NextCommand.ExecuteAsync(null);
        vm.CurrentStep.ShouldBe(OnboardingStep.Categories);

        vm.ToggleTopicCommand.Execute(vm.TopicChips.Single(c => c.Key == "food"));
        await Task.Delay(50, Ct);

        vm.CanContinueCategories.ShouldBeTrue();
        vm.CategoriesCounter.ShouldContain("1");
        await vm.NextCommand.ExecuteAsync(null);
        vm.CurrentStep.ShouldBe(OnboardingStep.Pace);
    }

    [Fact]
    public async Task Skipping_categories_selects_all_of_them()
    {
        var (_, vm) = await GoToAsync(OnboardingStep.Categories);
        vm.CanSkip.ShouldBeTrue();

        await vm.SkipCommand.ExecuteAsync(null);

        vm.TopicChips.ShouldAllBe(c => c.IsSelected);
        vm.SelectedTopics.Count.ShouldBe(9);
        vm.CurrentStep.ShouldBe(OnboardingStep.Pace);
    }

    [Fact]
    public async Task The_notifications_step_records_the_answer_and_moves_on()
    {
        var (harness, vm) = await GoToAsync(OnboardingStep.Notifications);
        vm.ShowPrimary.ShouldBeFalse();
        harness.Notifications.Granted = true;

        await vm.EnableNotificationsCommand.ExecuteAsync(null);

        harness.Notifications.Requests.ShouldBe(1);
        vm.NotificationsEnabled.ShouldBeTrue();
        vm.NotificationsHint.ShouldBeEmpty();
        vm.CurrentStep.ShouldBe(OnboardingStep.Privacy);

        var (_, later) = await GoToAsync(OnboardingStep.Notifications);
        later.LaterNotificationsCommand.Execute(null);
        later.NotificationsEnabled.ShouldBeFalse();
        later.CurrentStep.ShouldBe(OnboardingStep.Privacy);
    }

    [Fact]
    public async Task The_privacy_step_persists_the_choices_and_prepares_the_first_word()
    {
        var (harness, vm) = await StartAsync();
        vm.Nickname = " Оля ";
        await vm.NextCommand.ExecuteAsync(null);
        vm.SelectLevelCommand.Execute(vm.LevelOptions.Single(o => o.Key == "B1"));
        await vm.NextCommand.ExecuteAsync(null);
        vm.ToggleTopicCommand.Execute(vm.TopicChips.Single(c => c.Key == "shopping"));
        vm.ToggleTopicCommand.Execute(vm.TopicChips.Single(c => c.Key == "military"));
        await vm.NextCommand.ExecuteAsync(null);
        vm.SelectPaceCommand.Execute(vm.PaceOptions.Single(p => p.Pace == PacePreset.Intensive));
        await vm.NextCommand.ExecuteAsync(null);
        vm.LaterNotificationsCommand.Execute(null);
        vm.CurrentStep.ShouldBe(OnboardingStep.Privacy);
        vm.AnalyticsConsent = true;

        await vm.NextCommand.ExecuteAsync(null);

        vm.CurrentStep.ShouldBe(OnboardingStep.Done);
        vm.CanGoBack.ShouldBeFalse();
        vm.PrimaryText.ShouldBe(AppResources.Onboarding_Start_Button);
        harness.LearnerContext.Profile.ShouldNotBeNull();
        harness.LearnerContext.Profile.Nickname.ShouldBe("Оля");
        harness.LearnerContext.Settings.Level.ShouldBe(CefrLevel.B1);
        harness.LearnerContext.Settings.Topics.Select(t => t.Slug).ShouldBe(["shopping", "military"]);
        harness.LearnerContext.Settings.NewWordsPerDay.ShouldBe(PacePreset.Intensive.NewWordsPerDay());
        harness.Privacy.Analytics.ShouldBeTrue();
        harness.Onboarding.Completed.ShouldBeTrue();
        vm.FirstWord.ShouldStartWith("word");
        vm.FirstTranslation.ShouldStartWith("слово");

        var completed = false;
        vm.Completed += (_, _) => completed = true;
        await vm.NextCommand.ExecuteAsync(null);
        completed.ShouldBeTrue();
    }

    [Fact]
    public async Task Back_returns_one_step()
    {
        var (_, vm) = await GoToAsync(OnboardingStep.Pace);

        vm.BackCommand.Execute(null);
        vm.CurrentStep.ShouldBe(OnboardingStep.Categories);
        vm.BackCommand.Execute(null);
        vm.BackCommand.Execute(null);
        vm.CurrentStep.ShouldBe(OnboardingStep.Welcome);

        vm.BackCommand.Execute(null);
        vm.CurrentStep.ShouldBe(OnboardingStep.Welcome);
    }

    private static async Task<(ViewModelHarness Harness, OnboardingViewModel Vm)> StartAsync()
    {
        var harness = await new ViewModelHarness().WithImportedWordsAsync(12);
        var vm = harness.Resolve<OnboardingViewModel>();
        await vm.LoadAsync(Ct);
        return (harness, vm);
    }

    /// <summary>Walks forward with valid answers (nickname «Оля», the «food» category, the default pace) up to <paramref name="step"/>.</summary>
    private static async Task<(ViewModelHarness Harness, OnboardingViewModel Vm)> GoToAsync(OnboardingStep step)
    {
        var (harness, vm) = await StartAsync();
        vm.Nickname = "Оля";
        while (vm.CurrentStep < step)
        {
            if (vm.CurrentStep == OnboardingStep.Categories && vm.SelectedTopics.Count == 0)
            {
                vm.ToggleTopicCommand.Execute(vm.TopicChips.Single(c => c.Key == "food"));
            }

            await vm.NextCommand.ExecuteAsync(null);
        }

        vm.CurrentStep.ShouldBe(step);
        return (harness, vm);
    }
}
