using Burdock.Domain.Learning;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;
using Burdock.Presentation.Resources.Strings;
using Burdock.Presentation.Services;
using Burdock.Presentation.Tests.Fakes;
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
    public async Task The_voice_list_comes_from_the_catalogue_with_the_first_voice_preselected()
    {
        var (harness, vm) = await OpenAsync(TwoTopics);

        vm.HasVoices.ShouldBeTrue();
        vm.VoiceOptions.Select(o => o.Title).ShouldBe(["Heart", "George"]);
        vm.VoiceOptions[0].IsSelected.ShouldBeTrue();
        vm.VoiceOptions[1].Subtitle.ShouldBe("Британська · чоловічий голос");
        vm.CanOpenVoiceSettings.ShouldBeTrue();
        vm.VoiceHint.ShouldBe(AppResources.Settings_Voice_Hint);
        harness.Speech.Voice.ShouldBeNull();
    }

    [Fact]
    public async Task Selecting_a_voice_is_a_device_choice_that_plays_its_sample_and_touches_no_learner_settings()
    {
        var (harness, vm) = await OpenAsync(TwoTopics);

        await vm.SelectVoiceCommand.ExecuteAsync(vm.VoiceOptions[1]);
        await vm.PreviewVoiceCommand.ExecuteAsync(vm.VoiceOptions[0]);
        await vm.OpenVoiceSettingsCommand.ExecuteAsync(null);
        await Task.Delay(600, Ct);

        vm.VoiceOptions[1].IsSelected.ShouldBeTrue();
        vm.VoiceOptions[0].IsSelected.ShouldBeFalse();
        harness.Speech.Voice.ShouldBe(FakeSpeechService.George.Id);
        harness.Speech.Accent.ShouldBe(PronunciationAccent.British);
        harness.Speech.Sampled.ShouldBe([FakeSpeechService.George.Id, FakeSpeechService.Heart.Id]);
        harness.Speech.VoiceSettingsOpened.ShouldBe(1);
        harness.Toast.Messages.ShouldBeEmpty();
        harness.LearnerContext.Settings.ShouldBe(TwoTopics);
    }

    [Fact]
    public async Task A_stored_voice_stays_selected_and_without_a_catalogue_only_the_system_test_button_remains()
    {
        var harness = new ViewModelHarness(TwoTopics);
        harness.Speech.SelectVoice(FakeSpeechService.George);
        var vm = harness.Resolve<SettingsViewModel>();
        await vm.LoadAsync(Ct);
        vm.VoiceOptions.Single(o => o.IsSelected).Title.ShouldBe("George");

        var bare = new ViewModelHarness(TwoTopics);
        bare.Speech.Voices.Clear();
        var bareVm = bare.Resolve<SettingsViewModel>();
        await bareVm.LoadAsync(Ct);
        await bareVm.TestVoiceCommand.ExecuteAsync(null);

        bareVm.HasVoices.ShouldBeFalse();
        bare.Speech.Spoken.ShouldBe([AppResources.Settings_Voice_SampleWord]);
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
