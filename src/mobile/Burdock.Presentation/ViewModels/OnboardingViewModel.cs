using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Burdock.Application.UseCases.Onboarding;
using Burdock.Application.UseCases.Planning;
using Burdock.Application.UseCases.Words;
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

/// <summary>Onboarding steps of phase 1 (spec 03 S-02…S-09 without the widget step, which arrives with the surfaces).</summary>
public enum OnboardingStep
{
    Welcome,
    Level,
    Categories,
    Pace,
    Notifications,
    Privacy,
    Done,
}

/// <summary>One pace card of S-05.</summary>
public sealed partial class PaceOption(PacePreset pace, string title, string words, string minutes, string notifications) : ObservableObject, ISelectable
{
    public PacePreset Pace { get; } = pace;

    public string Title { get; } = title;

    public string Words { get; } = words;

    public string Minutes { get; } = minutes;

    public string Notifications { get; } = notifications;

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>First run: nickname, level, categories, pace, notifications wish, privacy consent, done (flow 3.5.1). Choices persist when the «Done» step opens.</summary>
public sealed partial class OnboardingViewModel : ObservableObject
{
    public const int StepCount = 7;

    private static readonly CultureInfo Ukrainian = CultureInfo.GetCultureInfo("uk-UA");
    private static readonly CompositeFormat CounterFormat = CompositeFormat.Parse(AppResources.Onboarding_Categories_Counter);
    private static readonly CompositeFormat CountOne = CompositeFormat.Parse(AppResources.Words_Count_One);
    private static readonly CompositeFormat CountFew = CompositeFormat.Parse(AppResources.Words_Count_Few);
    private static readonly CompositeFormat CountMany = CompositeFormat.Parse(AppResources.Words_Count_Many);
    private static readonly CompositeFormat PaceWordsFormat = CompositeFormat.Parse(AppResources.Onboarding_Pace_Words);
    private static readonly CompositeFormat PaceMinutesFormat = CompositeFormat.Parse(AppResources.Onboarding_Pace_Minutes);
    private static readonly CompositeFormat PaceNotificationsFormat = CompositeFormat.Parse(AppResources.Onboarding_Pace_Notifications);
    private static readonly CompositeFormat NotificationsBodyFormat = CompositeFormat.Parse(AppResources.Onboarding_Notifications_Body);

    private readonly SuggestNicknameUseCase _suggestNickname;
    private readonly EstimateWordCountUseCase _estimate;
    private readonly CompleteOnboardingUseCase _complete;
    private readonly BrowseWordsUseCase _browse;
    private readonly GetDailyPlanUseCase _dailyPlan;
    private readonly INotificationPrompt _notifications;
    private readonly ILogger<OnboardingViewModel> _logger;
    private bool _persisted;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentStep), nameof(CanGoBack), nameof(CanSkip), nameof(ShowPrimary), nameof(PrimaryText))]
    private int _stepIndex;

    [ObservableProperty]
    private string _nickname = string.Empty;

    [ObservableProperty]
    private string _nicknameError = string.Empty;

    [ObservableProperty]
    private string _categoriesCounter = string.Empty;

    [ObservableProperty]
    private bool _canContinueCategories;

    [ObservableProperty]
    private string _notificationsBody = string.Empty;

    [ObservableProperty]
    private string _notificationsHint = string.Empty;

    [ObservableProperty]
    private string _sampleWord = string.Empty;

    [ObservableProperty]
    private string _sampleTranslation = string.Empty;

    [ObservableProperty]
    private bool _analyticsConsent;

    [ObservableProperty]
    private string _firstWord = string.Empty;

    [ObservableProperty]
    private string _firstIpa = string.Empty;

    [ObservableProperty]
    private string _firstTranslation = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public OnboardingViewModel(
        SuggestNicknameUseCase suggestNickname,
        EstimateWordCountUseCase estimate,
        CompleteOnboardingUseCase complete,
        BrowseWordsUseCase browse,
        GetDailyPlanUseCase dailyPlan,
        INotificationPrompt notifications,
        ILogger<OnboardingViewModel> logger)
    {
        _suggestNickname = suggestNickname ?? throw new ArgumentNullException(nameof(suggestNickname));
        _estimate = estimate ?? throw new ArgumentNullException(nameof(estimate));
        _complete = complete ?? throw new ArgumentNullException(nameof(complete));
        _browse = browse ?? throw new ArgumentNullException(nameof(browse));
        _dailyPlan = dailyPlan ?? throw new ArgumentNullException(nameof(dailyPlan));
        _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        foreach (var level in CefrLevelExtensions.All)
        {
            LevelOptions.Add(new ChipOption(level.ToString(), level.ToString(), level, LevelHint(level)) { IsSelected = level == CefrLevel.A2 });
        }

        foreach (var slug in TopicCatalog.Slugs)
        {
            TopicChips.Add(new ChipOption(slug, TopicCatalog.Label(slug), TopicId.From(slug)));
        }

        foreach (var pace in new[] { PacePreset.Light, PacePreset.Moderate, PacePreset.Intensive })
        {
            PaceOptions.Add(new PaceOption(
                pace,
                PaceTitle(pace),
                string.Format(Ukrainian, PaceWordsFormat, pace.NewWordsPerDay()),
                string.Format(Ukrainian, PaceMinutesFormat, pace.EstimatedMinutesPerDay()),
                string.Format(Ukrainian, PaceNotificationsFormat, pace.NotificationsPerDay()))
            { IsSelected = pace == PacePreset.Moderate });
        }

        NotificationsBody = string.Format(Ukrainian, NotificationsBodyFormat, PacePreset.Moderate.NotificationsPerDay());
        CategoriesCounter = string.Format(Ukrainian, CounterFormat, 0, Plural.Uk(0, CountOne, CountFew, CountMany));
    }

    /// <summary>Raised once the learner finishes; the page hands the window to the Shell.</summary>
    public event EventHandler? Completed;

    public ObservableCollection<ChipOption> LevelOptions { get; } = [];

    public ObservableCollection<ChipOption> TopicChips { get; } = [];

    public ObservableCollection<PaceOption> PaceOptions { get; } = [];

    public OnboardingStep CurrentStep => (OnboardingStep)StepIndex;

    public bool CanGoBack => StepIndex > 0 && CurrentStep != OnboardingStep.Done;

    public bool CanSkip => CurrentStep is OnboardingStep.Categories or OnboardingStep.Pace or OnboardingStep.Privacy;

    /// <summary>The notifications step has its own two equal buttons.</summary>
    public bool ShowPrimary => CurrentStep != OnboardingStep.Notifications;

    public string PrimaryText => CurrentStep switch
    {
        OnboardingStep.Welcome => AppResources.Onboarding_Go_Button,
        OnboardingStep.Privacy => AppResources.Onboarding_Continue_Button,
        OnboardingStep.Done => AppResources.Onboarding_Start_Button,
        _ => AppResources.Onboarding_Next_Button,
    };

    public CefrLevel SelectedLevel => LevelOptions.FirstOrDefault(o => o.IsSelected)?.Value as CefrLevel? ?? CefrLevel.A2;

    public IReadOnlyList<TopicId> SelectedTopics => TopicChips.Where(c => c.IsSelected).Select(c => (TopicId)c.Value!).ToArray();

    public PacePreset SelectedPace => PaceOptions.FirstOrDefault(p => p.IsSelected)?.Pace ?? PacePreset.Moderate;

    public bool NotificationsEnabled { get; private set; }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (Nickname.Length == 0)
        {
            var adjectives = AppResources.Onboarding_Nickname_Adjectives.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Nickname = _suggestNickname.Execute(adjectives, AppResources.Onboarding_Nickname_Noun);
        }

        try
        {
            var words = await _browse.ExecuteAsync(WordsQuery.All, cancellationToken).ConfigureAwait(true);
            if (words.Count > 0)
            {
                var sample = words[0];
                SampleWord = sample.Entry.Lexeme.Lemma;
                SampleTranslation = sample.Entry.Translation;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogFailed(ex);
        }
    }

    [RelayCommand]
    private async Task NextAsync(CancellationToken cancellationToken)
    {
        switch (CurrentStep)
        {
            case OnboardingStep.Welcome:
                var profile = LearnerProfile.Create(Nickname);
                if (profile.IsFailure)
                {
                    NicknameError = AppResources.Onboarding_Nickname_Error;
                    return;
                }

                NicknameError = string.Empty;
                break;
            case OnboardingStep.Categories when SelectedTopics.Count == 0:
                return;
            case OnboardingStep.Privacy:
                if (!await PersistAsync(cancellationToken).ConfigureAwait(true))
                {
                    return;
                }

                break;
            case OnboardingStep.Done:
                Completed?.Invoke(this, EventArgs.Empty);
                return;
        }

        StepIndex = Math.Min(StepIndex + 1, StepCount - 1);
    }

    [RelayCommand]
    private void Back()
    {
        if (CanGoBack)
        {
            StepIndex--;
        }
    }

    [RelayCommand]
    private async Task SkipAsync(CancellationToken cancellationToken)
    {
        if (!CanSkip)
        {
            return;
        }

        if (CurrentStep == OnboardingStep.Categories && SelectedTopics.Count == 0)
        {
            // Skipping keeps all launch categories, like the settings default.
            foreach (var chip in TopicChips)
            {
                chip.IsSelected = true;
            }
        }

        if (CurrentStep == OnboardingStep.Privacy && !await PersistAsync(cancellationToken).ConfigureAwait(true))
        {
            return;
        }

        StepIndex = Math.Min(StepIndex + 1, StepCount - 1);
    }

    [RelayCommand]
    private void SelectLevel(ChipOption option)
    {
        foreach (var chip in LevelOptions)
        {
            chip.IsSelected = ReferenceEquals(chip, option);
        }

        _ = RefreshCounterAsync(CancellationToken.None);
    }

    [RelayCommand]
    private void ToggleTopic(ChipOption option)
    {
        ArgumentNullException.ThrowIfNull(option);
        option.IsSelected = !option.IsSelected;
        _ = RefreshCounterAsync(CancellationToken.None);
    }

    [RelayCommand]
    private void SelectPace(PaceOption option)
    {
        foreach (var pace in PaceOptions)
        {
            pace.IsSelected = ReferenceEquals(pace, option);
        }

        NotificationsBody = string.Format(Ukrainian, NotificationsBodyFormat, SelectedPace.NotificationsPerDay());
    }

    [RelayCommand]
    private async Task EnableNotificationsAsync(CancellationToken cancellationToken)
    {
        var granted = false;
        try
        {
            granted = await _notifications.RequestAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not OperationCanceledException)
        {
            LogFailed(ex);
        }

        NotificationsEnabled = granted;
        NotificationsHint = granted ? string.Empty : AppResources.Onboarding_Notifications_Denied;
        StepIndex = Math.Min(StepIndex + 1, StepCount - 1);
    }

    [RelayCommand]
    private void LaterNotifications()
    {
        NotificationsEnabled = false;
        StepIndex = Math.Min(StepIndex + 1, StepCount - 1);
    }

    private async Task RefreshCounterAsync(CancellationToken cancellationToken)
    {
        var topics = SelectedTopics;
        CanContinueCategories = topics.Count > 0;
        try
        {
            var count = await _estimate.ExecuteAsync(SelectedLevel, topics, cancellationToken).ConfigureAwait(true);
            CategoriesCounter = string.Format(Ukrainian, CounterFormat, topics.Count, Plural.Uk(count, CountOne, CountFew, CountMany));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogFailed(ex);
        }
    }

    /// <summary>Saves profile, settings, consent and the completion flag; then prepares the first word for the «Done» step.</summary>
    private async Task<bool> PersistAsync(CancellationToken cancellationToken)
    {
        if (_persisted)
        {
            return true;
        }

        IsBusy = true;
        try
        {
            var choices = new OnboardingChoices(Nickname, SelectedLevel, SelectedTopics, SelectedPace, NotificationsEnabled, AnalyticsConsent);
            var result = await _complete.ExecuteAsync(choices, cancellationToken).ConfigureAwait(true);
            if (result.IsFailure)
            {
                NicknameError = AppResources.Onboarding_Nickname_Error;
                StepIndex = (int)OnboardingStep.Welcome;
                return false;
            }

            _persisted = true;
            var plan = await _dailyPlan.ExecuteAsync(cancellationToken).ConfigureAwait(true);
            if (plan.Words.Count > 0)
            {
                var first = plan.Words[0];
                FirstWord = first.Entry.Lexeme.Lemma;
                FirstIpa = first.Entry.IpaGeneralAmerican ?? first.Entry.IpaReceivedPronunciation ?? string.Empty;
                FirstTranslation = first.Entry.Translation;
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogFailed(ex);
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string LevelHint(CefrLevel level) => level switch
    {
        CefrLevel.A1 => AppResources.Onboarding_Level_A1,
        CefrLevel.A2 => AppResources.Onboarding_Level_A2,
        CefrLevel.B1 => AppResources.Onboarding_Level_B1,
        CefrLevel.B2 => AppResources.Onboarding_Level_B2,
        CefrLevel.C1 => AppResources.Onboarding_Level_C1,
        _ => AppResources.Onboarding_Level_C2,
    };

    private static string PaceTitle(PacePreset pace) => pace switch
    {
        PacePreset.Light => AppResources.Onboarding_Pace_Light,
        PacePreset.Intensive => AppResources.Onboarding_Pace_Intensive,
        _ => AppResources.Onboarding_Pace_Moderate,
    };

    [LoggerMessage(Level = LogLevel.Error, Message = "Onboarding failed")]
    private partial void LogFailed(Exception exception);
}
