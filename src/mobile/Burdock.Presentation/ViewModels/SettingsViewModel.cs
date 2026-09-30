using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Burdock.Application.UseCases.Settings;
using Burdock.Domain.Exposure;
using Burdock.Domain.Learning;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;
using Burdock.Presentation.Localization;
using Burdock.Presentation.Resources.Strings;
using Burdock.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Burdock.Presentation.ViewModels;

/// <summary>S-18 «Налаштування»: every change is validated and saved after a short pause, with a «Збережено» toast (spec 03; ranges from decision Q30).</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "The debounce token source is replaced and cancelled per change; the transient view model lives as long as its page.")]
public sealed partial class SettingsViewModel : ObservableObject
{
    private const int SaveDebounceMilliseconds = 400;

    private static readonly CultureInfo Ukrainian = CultureInfo.GetCultureInfo("uk-UA");
    private static readonly CompositeFormat MinutesFormat = CompositeFormat.Parse(AppResources.Settings_Minutes);
    private static readonly CompositeFormat HoursFormat = CompositeFormat.Parse(AppResources.Settings_Hours);

    private readonly GetLearnerSettingsUseCase _getSettings;
    private readonly UpdateLearnerSettingsUseCase _updateSettings;
    private readonly IAppThemeService _theme;
    private readonly IToastService _toast;
    private readonly ISpeechService _speech;
    private readonly ILogger<SettingsViewModel> _logger;
    private LearnerSettings? _current;
    private bool _loading;
    private CancellationTokenSource? _saveCts;

    [ObservableProperty]
    private ViewState _state = ViewState.Loading;

    [ObservableProperty]
    private int _exposuresPerWord = 5;

    [ObservableProperty]
    private bool _translationOnSurfaces = true;

    [ObservableProperty]
    private int _notificationsPerDay = 3;

    [ObservableProperty]
    private TimeSpan? _quietStart = new TimeSpan(22, 0, 0);

    [ObservableProperty]
    private TimeSpan? _quietEnd = new TimeSpan(8, 0, 0);

    [ObservableProperty]
    private int _newWordsPerDay = 10;

    [ObservableProperty]
    private bool _gamificationOff;

    public SettingsViewModel(GetLearnerSettingsUseCase getSettings, UpdateLearnerSettingsUseCase updateSettings, IAppThemeService theme, IToastService toast, ISpeechService speech, ILogger<SettingsViewModel> logger)
    {
        _getSettings = getSettings ?? throw new ArgumentNullException(nameof(getSettings));
        _updateSettings = updateSettings ?? throw new ArgumentNullException(nameof(updateSettings));
        _theme = theme ?? throw new ArgumentNullException(nameof(theme));
        _toast = toast ?? throw new ArgumentNullException(nameof(toast));
        _speech = speech ?? throw new ArgumentNullException(nameof(speech));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        foreach (var minutes in RotationCadence.PresetMinutes)
        {
            CadenceChips.Add(new ChipOption(minutes.ToString(CultureInfo.InvariantCulture), CadenceLabel(minutes), minutes));
        }

        foreach (var level in CefrLevelExtensions.All)
        {
            LevelChips.Add(new ChipOption(level.ToString(), level.ToString(), level));
        }

        foreach (var slug in TopicCatalog.Slugs)
        {
            TopicChips.Add(new ChipOption(slug, TopicCatalog.Label(slug), TopicId.From(slug)));
        }

        RetentionChips.Add(new ChipOption(nameof(RetentionPreset.Easy), AppResources.Settings_Retention_Easy, RetentionPreset.Easy));
        RetentionChips.Add(new ChipOption(nameof(RetentionPreset.Standard), AppResources.Settings_Retention_Standard, RetentionPreset.Standard));
        RetentionChips.Add(new ChipOption(nameof(RetentionPreset.Maximum), AppResources.Settings_Retention_Max, RetentionPreset.Maximum));

        ThemeChips.Add(new ChipOption(nameof(AppThemeMode.System), AppResources.Settings_Theme_System, AppThemeMode.System));
        ThemeChips.Add(new ChipOption(nameof(AppThemeMode.Light), AppResources.Settings_Theme_Light, AppThemeMode.Light));
        ThemeChips.Add(new ChipOption(nameof(AppThemeMode.Dark), AppResources.Settings_Theme_Dark, AppThemeMode.Dark));
    }

    public ObservableCollection<ChipOption> CadenceChips { get; } = [];

    public ObservableCollection<ChipOption> LevelChips { get; } = [];

    public ObservableCollection<ChipOption> TopicChips { get; } = [];

    public ObservableCollection<ChipOption> RetentionChips { get; } = [];

    public ObservableCollection<ChipOption> ThemeChips { get; } = [];

    /// <summary>Voices of the content catalogue (ADR 0019); empty until pack audio is available, then the system voice is all there is.</summary>
    public ObservableCollection<VoiceOption> VoiceOptions { get; } = [];

    [ObservableProperty]
    private bool _hasVoices;

    /// <summary>Android opens the system voice settings; iOS has no such link, so the hint spells out the path.</summary>
    public bool CanOpenVoiceSettings => _speech.CanOpenSystemVoiceSettings;

    public string VoiceHint => _speech.CanOpenSystemVoiceSettings ? AppResources.Settings_Voice_Hint : AppResources.Settings_Voice_Hint_NoLink;

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        _loading = true;
        try
        {
            var settings = await _getSettings.ExecuteAsync(cancellationToken).ConfigureAwait(true);
            _current = settings;
            ExposuresPerWord = settings.ExposuresPerWordPerDay;
            TranslationOnSurfaces = !settings.TranslationHiddenOnSurfaces;
            NotificationsPerDay = settings.NotificationsPerDay;
            QuietStart = settings.QuietHours.Start.ToTimeSpan();
            QuietEnd = settings.QuietHours.End.ToTimeSpan();
            NewWordsPerDay = settings.NewWordsPerDay;
            GamificationOff = !settings.GamificationEnabled;
            SelectSingle(CadenceChips, chip => chip.Value is int m && m == settings.Cadence.Minutes);
            SelectSingle(LevelChips, chip => chip.Value is CefrLevel l && l == settings.Level);
            SelectSingle(RetentionChips, chip => chip.Value is RetentionPreset r && r == settings.Retention);
            SelectSingle(ThemeChips, chip => chip.Value is AppThemeMode mode && mode == _theme.Mode);
            await LoadVoicesAsync(cancellationToken).ConfigureAwait(true);
            foreach (var chip in TopicChips)
            {
                chip.IsSelected = chip.Value is TopicId topic && settings.Topics.Contains(topic);
            }

            State = ViewState.Ready;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogFailed(ex);
            State = ViewState.Error;
        }
        finally
        {
            _loading = false;
        }
    }

    [RelayCommand]
    private void SelectCadence(ChipOption option)
    {
        SelectSingle(CadenceChips, chip => ReferenceEquals(chip, option));
        ScheduleSave();
    }

    [RelayCommand]
    private void SelectLevel(ChipOption option)
    {
        SelectSingle(LevelChips, chip => ReferenceEquals(chip, option));
        ScheduleSave();
    }

    [RelayCommand]
    private void ToggleTopic(ChipOption option)
    {
        ArgumentNullException.ThrowIfNull(option);
        if (option.IsSelected && TopicChips.Count(c => c.IsSelected) == 1)
        {
            return; // at least one category stays selected (spec 03 S-18)
        }

        option.IsSelected = !option.IsSelected;
        ScheduleSave();
    }

    [RelayCommand]
    private void SelectRetention(ChipOption option)
    {
        SelectSingle(RetentionChips, chip => ReferenceEquals(chip, option));
        ScheduleSave();
    }

    [RelayCommand]
    private void SelectTheme(ChipOption option)
    {
        ArgumentNullException.ThrowIfNull(option);
        SelectSingle(ThemeChips, chip => ReferenceEquals(chip, option));
        if (option.Value is AppThemeMode mode)
        {
            _theme.Apply(mode);
        }
    }

    /// <summary>The voice is a device preference used everywhere (owner, 30.09.2026), not a synced learner setting; the sample confirms it at once.</summary>
    [RelayCommand]
    private Task SelectVoiceAsync(VoiceOption option)
    {
        ArgumentNullException.ThrowIfNull(option);
        foreach (var candidate in VoiceOptions)
        {
            candidate.IsSelected = ReferenceEquals(candidate, option);
        }

        _speech.SelectVoice(option.Voice);
        return _speech.PlaySampleAsync(option.Voice.Id, CancellationToken.None);
    }

    [RelayCommand]
    private Task PreviewVoiceAsync(VoiceOption option)
    {
        ArgumentNullException.ThrowIfNull(option);
        return _speech.PlaySampleAsync(option.Voice.Id, CancellationToken.None);
    }

    [RelayCommand]
    private Task TestVoiceAsync(CancellationToken cancellationToken) => _speech.SpeakEnglishAsync(AppResources.Settings_Voice_SampleWord, cancellationToken);

    private async Task LoadVoicesAsync(CancellationToken cancellationToken)
    {
        var voices = await _speech.GetVoicesAsync(cancellationToken).ConfigureAwait(true);
        VoiceOptions.Clear();
        var selected = _speech.Voice is { } stored && voices.Any(v => v.Id == stored) ? stored : voices.Count > 0 ? voices[0].Id : (VoiceId?)null;
        foreach (var voice in voices)
        {
            VoiceOptions.Add(new VoiceOption(voice, VoiceText.Describe(voice)) { IsSelected = voice.Id == selected });
        }

        HasVoices = VoiceOptions.Count > 0;
    }

    [RelayCommand]
    private Task OpenVoiceSettingsAsync() => _speech.OpenSystemVoiceSettingsAsync();

    partial void OnExposuresPerWordChanged(int value) => ScheduleSave();

    partial void OnTranslationOnSurfacesChanged(bool value) => ScheduleSave();

    partial void OnNotificationsPerDayChanged(int value) => ScheduleSave();

    partial void OnQuietStartChanged(TimeSpan? value) => ScheduleSave();

    partial void OnQuietEndChanged(TimeSpan? value) => ScheduleSave();

    partial void OnNewWordsPerDayChanged(int value) => ScheduleSave();

    partial void OnGamificationOffChanged(bool value) => ScheduleSave();

    private static void SelectSingle(IEnumerable<ChipOption> chips, Func<ChipOption, bool> predicate)
    {
        foreach (var chip in chips)
        {
            chip.IsSelected = predicate(chip);
        }
    }

    private static string CadenceLabel(int minutes)
        => minutes < 60
            ? string.Format(Ukrainian, MinutesFormat, minutes)
            : string.Format(Ukrainian, HoursFormat, minutes / 60);

    private void ScheduleSave()
    {
        if (_loading || _current is null)
        {
            return;
        }

        _saveCts?.Cancel();
        _saveCts?.Dispose();
        var cts = _saveCts = new CancellationTokenSource();
        _ = SaveAfterPauseAsync(cts.Token);
    }

    private async Task SaveAfterPauseAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(SaveDebounceMilliseconds, cancellationToken).ConfigureAwait(true);
            await SaveAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer change.
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (_current is not { } current)
        {
            return;
        }

        var cadenceMinutes = CadenceChips.FirstOrDefault(c => c.IsSelected)?.Value as int? ?? current.Cadence.Minutes;
        var settings = current with
        {
            Level = LevelChips.FirstOrDefault(c => c.IsSelected)?.Value as CefrLevel? ?? current.Level,
            Topics = TopicChips.Where(c => c.IsSelected).Select(c => (TopicId)c.Value!).ToArray(),
            NewWordsPerDay = NewWordsPerDay,
            ExposuresPerWordPerDay = ExposuresPerWord,
            Cadence = RotationCadence.Create(cadenceMinutes).Match(c => c, _ => current.Cadence),
            NotificationsPerDay = NotificationsPerDay,
            QuietHours = new QuietHours(TimeOnly.FromTimeSpan(QuietStart ?? current.QuietHours.Start.ToTimeSpan()), TimeOnly.FromTimeSpan(QuietEnd ?? current.QuietHours.End.ToTimeSpan())),
            TranslationHiddenOnSurfaces = !TranslationOnSurfaces,
            Retention = RetentionChips.FirstOrDefault(c => c.IsSelected)?.Value as RetentionPreset? ?? current.Retention,
            GamificationEnabled = !GamificationOff,
        };

        try
        {
            var result = await _updateSettings.ExecuteAsync(settings, cancellationToken).ConfigureAwait(true);
            if (result.IsFailure)
            {
                await _toast.ShowAsync(AppResources.Common_Error_Title, cancellationToken).ConfigureAwait(true);
                return;
            }

            _current = result.Value;
            await _toast.ShowAsync(AppResources.Settings_Saved_Toast, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogFailed(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Settings screen failed")]
    private partial void LogFailed(Exception exception);
}
