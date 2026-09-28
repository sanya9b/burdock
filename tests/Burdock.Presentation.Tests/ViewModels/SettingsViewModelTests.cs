using Burdock.Domain.Learning;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;
using Burdock.Presentation.Resources.Strings;
using Burdock.Presentation.Services;
using Burdock.Presentation.ViewModels;
using ViewState = Burdock.Presentation.ViewModels.ViewState;

namespace Burdock.Presentation.Tests.ViewModels;

/// <summary>S-18 «Налаштування» (spec 03): the form mirrors the settings, autosaves after a pause with a toast, keeps one category, applies the theme at once.</summary>
public sealed class SettingsViewModelTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static LearnerSettings TwoTopics { get; } = LearnerSettings.Moderate(LanguagePair.UkrainianToEnglish, CefrLevel.A2, [TopicId.From("shopping"), TopicId.From("food")]);

    [Fact]
    public async Task Load_reflects_the_current_settings()
    {
        var (harness, vm) = await OpenAsync(TwoTopics);
        harness.Theme.Mode.ShouldBe(AppThemeMode.System);

        vm.State.ShouldBe(ViewState.Ready);
        vm.NewWordsPerDay.ShouldBe(10);
        vm.ExposuresPerWord.ShouldBe(5);
        vm.NotificationsPerDay.ShouldBe(3);
        vm.TranslationOnSurfaces.ShouldBeTrue();
        vm.GamificationOff.ShouldBeFalse();
        vm.QuietStart.ShouldNotBeNull();
        vm.LevelChips.Single(c => c.IsSelected).Key.ShouldBe("A2");
        vm.TopicChips.Where(c => c.IsSelected).Select(c => c.Key).ShouldBe(["food", "shopping"], ignoreOrder: true);
        vm.TopicChips.Count.ShouldBe(9);
        vm.CadenceChips.Single(c => c.IsSelected).Key.ShouldBe("60");
        vm.RetentionChips.Single(c => c.IsSelected).Key.ShouldBe(nameof(RetentionPreset.Standard));
        vm.ThemeChips.Single(c => c.IsSelected).Key.ShouldBe(nameof(AppThemeMode.System));
    }

    [Fact]
    public async Task A_change_saves_after_a_pause_and_shows_the_saved_toast()
    {
        var (harness, vm) = await OpenAsync(TwoTopics);

        vm.NewWordsPerDay = 12;
        harness.LearnerContext.Settings.NewWordsPerDay.ShouldBe(10); // not yet
        await Task.Delay(800, Ct);

        harness.LearnerContext.Settings.NewWordsPerDay.ShouldBe(12);
        harness.Toast.Messages.ShouldBe([AppResources.Settings_Saved_Toast]);
    }

    [Fact]
    public async Task Rapid_changes_collapse_into_one_save()
    {
        var (harness, vm) = await OpenAsync(TwoTopics);

        vm.NewWordsPerDay = 11;
        vm.NewWordsPerDay = 12;
        vm.SelectLevelCommand.Execute(vm.LevelChips.Single(c => c.Key == "B1"));
        await Task.Delay(800, Ct);

        harness.LearnerContext.Settings.NewWordsPerDay.ShouldBe(12);
        harness.LearnerContext.Settings.Level.ShouldBe(CefrLevel.B1);
        harness.Toast.Messages.Count.ShouldBe(1);
    }

    [Fact]
    public async Task The_last_selected_category_cannot_be_deselected()
    {
        var (harness, vm) = await OpenAsync(LearnerSettings.Moderate(LanguagePair.UkrainianToEnglish, CefrLevel.A1, [TopicId.From("shopping")]));
        var shopping = vm.TopicChips.Single(c => c.Key == "shopping");
        var military = vm.TopicChips.Single(c => c.Key == "military");

        vm.ToggleTopicCommand.Execute(shopping);
        shopping.IsSelected.ShouldBeTrue();

        vm.ToggleTopicCommand.Execute(military);
        vm.ToggleTopicCommand.Execute(shopping);
        await Task.Delay(800, Ct);

        shopping.IsSelected.ShouldBeFalse();
        military.IsSelected.ShouldBeTrue();
        harness.LearnerContext.Settings.Topics.Select(t => t.Slug).ShouldBe(["military"]);
    }

    [Fact]
    public async Task Selecting_a_theme_applies_it_at_once_without_touching_learner_settings()
    {
        var (harness, vm) = await OpenAsync(TwoTopics);

        vm.SelectThemeCommand.Execute(vm.ThemeChips.Single(c => c.Key == nameof(AppThemeMode.Dark)));
        await Task.Delay(700, Ct);

        harness.Theme.Applied.ShouldBe([AppThemeMode.Dark]);
        vm.ThemeChips.Single(c => c.IsSelected).Key.ShouldBe(nameof(AppThemeMode.Dark));
        harness.Toast.Messages.ShouldBeEmpty();
    }

    [Fact]
    public async Task Pronunciation_is_a_device_choice_that_switches_the_accent_and_plays_a_sample()
    {
        var (harness, vm) = await OpenAsync(TwoTopics);
        vm.PronunciationChips.Single(c => c.IsSelected).Key.ShouldBe(nameof(PronunciationAccent.American));
        vm.CanOpenVoiceSettings.ShouldBeTrue();
        vm.VoiceHint.ShouldBe(AppResources.Settings_Voice_Hint);

        await vm.SelectPronunciationCommand.ExecuteAsync(vm.PronunciationChips.Single(c => c.Key == nameof(PronunciationAccent.British)));
        await vm.TestVoiceCommand.ExecuteAsync(null);
        await vm.OpenVoiceSettingsCommand.ExecuteAsync(null);
        await Task.Delay(600, Ct);

        harness.Speech.Accent.ShouldBe(PronunciationAccent.British);
        harness.Speech.Spoken.ShouldBe([AppResources.Settings_Voice_SampleWord, AppResources.Settings_Voice_SampleWord]);
        harness.Speech.VoiceSettingsOpened.ShouldBe(1);
        harness.Toast.Messages.ShouldBeEmpty();
        harness.LearnerContext.Settings.ShouldBe(TwoTopics);
    }

    [Fact]
    public async Task Without_a_system_voice_settings_link_the_hint_spells_out_the_path()
    {
        var harness = new ViewModelHarness(TwoTopics);
        harness.Speech.CanOpenSystemVoiceSettings = false;
        var vm = harness.Resolve<SettingsViewModel>();

        vm.CanOpenVoiceSettings.ShouldBeFalse();
        vm.VoiceHint.ShouldBe(AppResources.Settings_Voice_Hint_NoLink);
    }

    [Fact]
    public async Task An_out_of_range_value_is_rejected_with_an_error_toast()
    {
        var (harness, vm) = await OpenAsync(TwoTopics);

        vm.NewWordsPerDay = 99;
        await Task.Delay(800, Ct);

        harness.LearnerContext.Settings.NewWordsPerDay.ShouldBe(10);
        harness.Toast.Messages.ShouldBe([AppResources.Common_Error_Title]);
    }

    private static async Task<(ViewModelHarness Harness, SettingsViewModel Vm)> OpenAsync(LearnerSettings settings)
    {
        var harness = new ViewModelHarness(settings);
        var vm = harness.Resolve<SettingsViewModel>();
        await vm.LoadAsync(Ct);
        return (harness, vm);
    }
}
