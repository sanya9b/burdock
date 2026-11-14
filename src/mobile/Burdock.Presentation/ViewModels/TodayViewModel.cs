using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Burdock.Application.Ports;
using Burdock.Application.UseCases.Content;
using Burdock.Application.UseCases.LearningDays;
using Burdock.Application.UseCases.Planning;
using Burdock.Application.UseCases.Reviews;
using Burdock.Application.UseCases.Rewards;
using Burdock.Domain.Rewards;
using Burdock.Domain.Learning;
using Burdock.Domain.Vocabulary;
using Burdock.Presentation.Localization;
using Burdock.Presentation.Navigation;
using Burdock.Presentation.Services;
using Burdock.Presentation.Resources.Icons;
using Burdock.Presentation.Resources.Strings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Burdock.Presentation.ViewModels;

/// <summary>One word in the «Сьогодні» plan: the hero card or a compact tile in the strip.</summary>
public sealed record WordTile(Guid LexemeId, string Lemma, string Translation, string Ipa, string TopicKey, string Reason, string ReasonKey, string ReasonIcon);

/// <summary>S-10 «Сьогодні»: greeting, learning day, hero word, the rest of today's words, session CTA (spec 03).</summary>
public sealed partial class TodayViewModel : ObservableObject
{
    private static readonly CultureInfo Ukrainian = CultureInfo.GetCultureInfo("uk-UA");
    private static readonly CompositeFormat CalendarToastFormat = CompositeFormat.Parse(AppResources.Calendar_Toast);
    private static readonly CompositeFormat LearningDayFormat = CompositeFormat.Parse(AppResources.Today_LearningDay_Label);
    private static readonly CompositeFormat PlanSummaryFormat = CompositeFormat.Parse(AppResources.Today_Plan_Summary);
    private static readonly CompositeFormat HeroPositionFormat = CompositeFormat.Parse(AppResources.Today_Hero_Position);
    private static readonly CompositeFormat HeroPositionA11yFormat = CompositeFormat.Parse(AppResources.Today_Hero_Position_A11y);
    private static readonly CompositeFormat ReviewBodyOne = CompositeFormat.Parse(AppResources.Today_Review_Body_One);
    private static readonly CompositeFormat ReviewBodyFew = CompositeFormat.Parse(AppResources.Today_Review_Body_Few);
    private static readonly CompositeFormat ReviewBodyMany = CompositeFormat.Parse(AppResources.Today_Review_Body_Many);

    private readonly LearningDayResolver _learningDays;
    private readonly IClock _clock;
    private readonly BootstrapContentUseCase _bootstrap;
    private readonly GetDailyPlanUseCase _dailyPlan;
    private readonly INavigationService _navigation;
    private readonly ISpeechService _speech;
    private readonly GetRewardsOverviewUseCase _rewards;
    private readonly GetReviewRoundUseCase _reviewRound;
    private readonly ClaimCalendarUseCase _claimCalendar;
    private readonly GetDailyTasksUseCase _tasks;
    private readonly IToastService _toast;
    private readonly ILogger<TodayViewModel> _logger;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSkeleton))]
    private ViewState _state = ViewState.Loading;

    /// <summary>True while the learner pulled to refresh over existing content (the RefreshView spinner, not the skeleton).</summary>
    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private string _greeting = string.Empty;

    [ObservableProperty]
    private string _learningDayText = string.Empty;

    [ObservableProperty]
    private string _planSummary = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartSessionCommand))]
    private bool _hasWords;

    /// <summary>The word in the hero pager; swiping the pager or tapping a strip tile moves it (owner request 13.11.2026).</summary>
    [ObservableProperty]
    private WordTile? _hero;

    /// <summary>More than one word of the day: the pager counter and the strip are shown.</summary>
    [ObservableProperty]
    private bool _hasMore;

    /// <summary>«3 з 20» under the pager.</summary>
    [ObservableProperty]
    private string _heroPositionText = string.Empty;

    [ObservableProperty]
    private string _heroPositionAccessibility = string.Empty;

    [ObservableProperty]
    private bool _showRewards;

    [ObservableProperty]
    private string _streakText = string.Empty;

    [ObservableProperty]
    private string _streakAccessibility = string.Empty;

    [ObservableProperty]
    private bool _streakCountedToday;

    [ObservableProperty]
    private string _thornsText = string.Empty;

    /// <summary>«Повторимо?» (log item 24): at least one full pack of five learned words is due.</summary>
    [ObservableProperty]
    private bool _showReview;

    [ObservableProperty]
    private string _reviewBody = string.Empty;

    /// <summary>Daily tasks are shown once the rewards loaded (gamification on).</summary>
    [ObservableProperty]
    private bool _showTasks;

    public TodayViewModel(LearningDayResolver learningDays, IClock clock, BootstrapContentUseCase bootstrap, GetDailyPlanUseCase dailyPlan, INavigationService navigation, ISpeechService speech, GetRewardsOverviewUseCase rewards, GetReviewRoundUseCase reviewRound, ClaimCalendarUseCase claimCalendar, GetDailyTasksUseCase tasks, IToastService toast, ILogger<TodayViewModel> logger)
    {
        _learningDays = learningDays ?? throw new ArgumentNullException(nameof(learningDays));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _bootstrap = bootstrap ?? throw new ArgumentNullException(nameof(bootstrap));
        _dailyPlan = dailyPlan ?? throw new ArgumentNullException(nameof(dailyPlan));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _speech = speech ?? throw new ArgumentNullException(nameof(speech));
        _rewards = rewards ?? throw new ArgumentNullException(nameof(rewards));
        _reviewRound = reviewRound ?? throw new ArgumentNullException(nameof(reviewRound));
        _claimCalendar = claimCalendar ?? throw new ArgumentNullException(nameof(claimCalendar));
        _tasks = tasks ?? throw new ArgumentNullException(nameof(tasks));
        _toast = toast ?? throw new ArgumentNullException(nameof(toast));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        RefreshHeader();
    }

    /// <summary>All words of the day in plan order: the pages of the hero pager and the tiles of the strip.</summary>
    public ObservableCollection<WordTile> Words { get; } = [];

    /// <summary>The first load has nothing to show yet: skeleton of the hero and the strip (NFR-03-5), never a spinner.</summary>
    public bool ShowSkeleton => State == ViewState.Loading;

    public void RefreshHeader()
    {
        var localHour = TimeZoneInfo.ConvertTime(_clock.UtcNow, _clock.LocalTimeZone).Hour;
        Greeting = localHour switch
        {
            >= 5 and < 12 => AppResources.Today_Greeting_Morning,
            >= 12 and < 18 => AppResources.Today_Greeting_Day,
            >= 18 and < 23 => AppResources.Today_Greeting_Evening,
            _ => AppResources.Today_Greeting_Night,
        };

        var day = _learningDays.Today();
        LearningDayText = string.Format(Ukrainian, LearningDayFormat, day.Date.ToString("d MMMM", Ukrainian));
    }

    [RelayCommand]
    private Task OpenRewardsAsync(CancellationToken cancellationToken) => _navigation.GoToAsync(Routes.Me.Rewards, null, cancellationToken);

    /// <summary>The tasks card leads straight to the «Завдання» section of «Нагороди» (S-16 category bar).</summary>
    [RelayCommand]
    private Task OpenTasksAsync(CancellationToken cancellationToken)
        => _navigation.GoToAsync(Routes.Me.Rewards, new Dictionary<string, object> { ["section"] = RewardsViewModel.TasksSection }, cancellationToken);

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        RefreshHeader();
        if (Words.Count == 0)
        {
            State = ViewState.Loading;
        }
        else
        {
            IsRefreshing = true;
        }

        try
        {
            await _bootstrap.ExecuteAsync(cancellationToken).ConfigureAwait(true);
            var plan = await _dailyPlan.ExecuteAsync(cancellationToken).ConfigureAwait(true);

            var shown = Hero?.LexemeId;
            Words.Clear();
            foreach (var word in plan.Words)
            {
                Words.Add(ToTile(word, _speech.Accent));
            }

            // A reload (back from the card, theme change, pull to refresh) keeps the page the learner was on.
            Hero = Words.FirstOrDefault(w => w.LexemeId == shown) ?? (Words.Count > 0 ? Words[0] : null);

            PlanSummary = string.Format(Ukrainian, PlanSummaryFormat, plan.NewCount, plan.DueCount);

            var review = await _reviewRound.ExecuteAsync(cancellationToken).ConfigureAwait(true);
            ShowReview = review.IsAvailable;
            ReviewBody = Plural.Uk(review.DueCount, ReviewBodyOne, ReviewBodyFew, ReviewBodyMany);

            // The login calendar is claimed on the first open of the learning day (FR-07-9), before the balance is read.
            var claim = await _claimCalendar.ExecuteAsync(cancellationToken).ConfigureAwait(true);
            var overview = await _rewards.ExecuteAsync(cancellationToken).ConfigureAwait(true);
            ShowRewards = overview.GamificationEnabled;
            StreakCountedToday = overview.StreakCountedToday;
            StreakText = overview.StreakLength.ToString(Ukrainian);
            StreakAccessibility = Localization.RewardsText.StreakDays(overview.StreakLength);
            ThornsText = overview.Thorns.ToString(Ukrainian);
            await LoadTasksAsync(claim, cancellationToken).ConfigureAwait(true);
            HasWords = Words.Count > 0;
            HasMore = Words.Count > 1;
            UpdateHeroPosition();
            // goal_done (spec 03 S-10): the day's words were all met and nothing is due; otherwise the morning empty state.
            State = HasWords ? ViewState.Ready : plan.GoalReached ? ViewState.GoalDone : ViewState.Empty;
        }
        catch (OperationCanceledException)
        {
            // Navigation away cancels the load; nothing to show.
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogLoadFailed(ex);
            State = ViewState.Error;
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    public ObservableCollection<TaskRowItem> Tasks { get; } = [];

    /// <summary>The three daily tasks of the card; the calendar tiles stay on «Нагороди» (owner, 14.11.2026), only the claim toast shows here.</summary>
    private async Task LoadTasksAsync(CalendarClaimView claim, CancellationToken cancellationToken)
    {
        var view = await _tasks.ExecuteAsync(cancellationToken).ConfigureAwait(true);
        ShowTasks = view.GamificationEnabled;
        Tasks.Clear();
        foreach (var task in view.Daily)
        {
            Tasks.Add(TaskRowItem.From(task));
        }

        if (claim.Claimed && view.GamificationEnabled)
        {
            await _toast.ShowAsync(string.Format(Ukrainian, CalendarToastFormat, Localization.RewardsText.ThornsPlus(claim.Reward), claim.Position), cancellationToken).ConfigureAwait(true);
            if (Localization.RewardsText.CalendarTrackToast(claim.Track) is { } track)
            {
                await _toast.ShowAsync(track, cancellationToken).ConfigureAwait(true);
            }
        }
    }

    [RelayCommand]
    private Task OpenWordAsync(WordTile? tile)
        => tile is null
            ? Task.CompletedTask
            : _navigation.GoToAsync(Routes.Today.Word, new Dictionary<string, object>(StringComparer.Ordinal) { ["id"] = tile.LexemeId.ToString("D") });

    [RelayCommand]
    private Task SpeakHeroAsync(CancellationToken cancellationToken) => SpeakWordAsync(Hero, cancellationToken);

    [RelayCommand]
    private Task SpeakWordAsync(WordTile? tile, CancellationToken cancellationToken)
        => tile is { } word ? _speech.SpeakWordAsync(Domain.SharedKernel.LexemeId.From(word.LexemeId), word.Lemma, cancellationToken) : Task.CompletedTask;

    partial void OnHeroChanged(WordTile? value) => UpdateHeroPosition();

    private void UpdateHeroPosition()
    {
        var index = Hero is null ? -1 : Words.IndexOf(Hero);
        HeroPositionText = index < 0 ? string.Empty : string.Format(Ukrainian, HeroPositionFormat, index + 1, Words.Count);
        HeroPositionAccessibility = index < 0 ? string.Empty : string.Format(Ukrainian, HeroPositionA11yFormat, index + 1, Words.Count);
    }

    /// <summary>«Повторити»: the pairs round of due learned words (S-10a).</summary>
    [RelayCommand]
    private Task StartReviewAsync(CancellationToken cancellationToken) => _navigation.GoToAsync(Routes.Learn.Review, null, cancellationToken);

    [RelayCommand(CanExecute = nameof(HasWords))]
    private Task StartSessionAsync()
        => _navigation.GoToAsync(Routes.Learn.Session, new Dictionary<string, object>(StringComparer.Ordinal) { ["size"] = "10" });

    /// <summary>«Ще 5 карток» after the daily goal: a short session that may take new words beyond today's pace.</summary>
    [RelayCommand]
    private Task MoreWordsAsync()
        => _navigation.GoToAsync(Routes.Learn.Session, new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["size"] = GetDailyPlanUseCase.ExtraWordsBatch.ToString(CultureInfo.InvariantCulture),
            ["extra"] = GetDailyPlanUseCase.ExtraWordsBatch.ToString(CultureInfo.InvariantCulture),
        });

    private static WordTile ToTile(PlannedWord word, Domain.Vocabulary.PronunciationAccent accent)
    {
        var entry = word.Entry;
        var topic = entry.Lexeme.Topics.Count > 0 ? entry.Lexeme.Topics[0].Slug : string.Empty;
        return new WordTile(
            entry.Id.Value,
            entry.Lexeme.Lemma,
            entry.Translation,
            Localization.Pronunciation.PreferredIpa(entry, accent),
            topic,
            ReasonLabel(word.Reason),
            word.Reason.ToString(),
            ReasonGlyph(word.Reason));
    }

    private static string ReasonLabel(PlanReason reason) => reason switch
    {
        PlanReason.Due => AppResources.Today_Reason_Due,
        PlanReason.Learning => AppResources.Today_Reason_Learning,
        _ => AppResources.Today_Reason_New,
    };

    private static string ReasonGlyph(PlanReason reason) => reason switch
    {
        PlanReason.Due => Icon.RefreshCw,
        PlanReason.Learning => Icon.Sprout,
        _ => Icon.Sparkles,
    };

    [LoggerMessage(Level = LogLevel.Error, Message = "Today screen failed to load")]
    private partial void LogLoadFailed(Exception exception);
}
