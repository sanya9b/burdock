using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Burdock.Application.UseCases.Rewards;
using Burdock.Domain.Learning;
using Burdock.Domain.Rewards;
using Burdock.Presentation.Localization;
using Burdock.Presentation.Navigation;
using Burdock.Presentation.Resources.Icons;
using Burdock.Presentation.Resources.Strings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Burdock.Presentation.ViewModels;

/// <summary>S-12d: numbers first, then the economy (XP, thorns, streak) and a queue of celebrations (spec 03, spec 07 §3.8). The learning-day rule counts graded cards only (T11).</summary>
public sealed partial class SessionSummaryViewModel : ObservableObject, IQueryAttributable
{
    /// <summary>Below this accuracy the mascot encourages instead of celebrating (spec 03 S-12d).</summary>
    internal const double CelebrationThreshold = 0.6;

    private static readonly CultureInfo Ukrainian = CultureInfo.GetCultureInfo("uk-UA");
    private static readonly CompositeFormat AccuracyFormat = CompositeFormat.Parse(AppResources.Summary_Accuracy_Label);
    private static readonly CompositeFormat NotYetFormat = CompositeFormat.Parse(AppResources.Summary_LearningDay_NotYet);
    private static readonly CompositeFormat GradedOne = CompositeFormat.Parse(AppResources.Summary_Graded_One);
    private static readonly CompositeFormat GradedFew = CompositeFormat.Parse(AppResources.Summary_Graded_Few);
    private static readonly CompositeFormat GradedMany = CompositeFormat.Parse(AppResources.Summary_Graded_Many);
    private static readonly CompositeFormat CorrectOne = CompositeFormat.Parse(AppResources.Summary_Correct_One);
    private static readonly CompositeFormat CorrectFew = CompositeFormat.Parse(AppResources.Summary_Correct_Few);
    private static readonly CompositeFormat CorrectMany = CompositeFormat.Parse(AppResources.Summary_Correct_Many);
    private static readonly CompositeFormat XpGainedFormat = CompositeFormat.Parse(AppResources.Rewards_Xp_Gained);
    private static readonly CompositeFormat ThornsGainedFormat = CompositeFormat.Parse(AppResources.Rewards_Thorns_Gained);
    private static readonly CompositeFormat MilestoneTitleFormat = CompositeFormat.Parse(AppResources.Celebration_Milestone_Title);

    private readonly INavigationService _navigation;
    private readonly SettleSessionRewardsUseCase _settle;
    private readonly ILogger<SessionSummaryViewModel> _logger;
    private readonly Queue<CelebrationItem> _celebrations = new();
    private SessionReport? _report;
    private bool _settled;

    [ObservableProperty]
    private int _graded;

    [ObservableProperty]
    private int _correct;

    [ObservableProperty]
    private double _accuracy;

    [ObservableProperty]
    private string _accuracyPercentText = string.Empty;

    [ObservableProperty]
    private string _accuracyText = string.Empty;

    [ObservableProperty]
    private string _gradedText = string.Empty;

    [ObservableProperty]
    private string _correctText = string.Empty;

    [ObservableProperty]
    private string _learningDayText = string.Empty;

    [ObservableProperty]
    private bool _learningDayCounted;

    [ObservableProperty]
    private string _mascot = "mascot_party.png";

    [ObservableProperty]
    private bool _showRewards;

    [ObservableProperty]
    private string _xpGainedText = string.Empty;

    [ObservableProperty]
    private string _thornsGainedText = string.Empty;

    [ObservableProperty]
    private int _thornsBefore;

    [ObservableProperty]
    private int _thornsTotal;

    [ObservableProperty]
    private int _streakLength;

    [ObservableProperty]
    private string _streakText = string.Empty;

    [ObservableProperty]
    private string _levelText = string.Empty;

    [ObservableProperty]
    private CelebrationItem? _currentCelebration;

    public SessionSummaryViewModel(INavigationService navigation, SettleSessionRewardsUseCase settle, ILogger<SessionSummaryViewModel> logger)
    {
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _settle = settle ?? throw new ArgumentNullException(nameof(settle));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public ObservableCollection<CelebrationItem> Celebrations { get; } = [];

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        Graded = Read(query, "graded");
        Correct = Read(query, "correct");
        Accuracy = Graded == 0 ? 0 : (double)Correct / Graded;
        var percent = (int)Math.Round(100 * Accuracy);
        AccuracyPercentText = string.Create(Ukrainian, $"{percent}%");
        AccuracyText = string.Format(Ukrainian, AccuracyFormat, percent);
        GradedText = Plural.Uk(Graded, GradedOne, GradedFew, GradedMany);
        CorrectText = Plural.Uk(Correct, CorrectOne, CorrectFew, CorrectMany);
        Mascot = Accuracy >= CelebrationThreshold ? "mascot_party.png" : "mascot_cheer.png";
        LearningDayCounted = Graded >= LearningPolicy.MinGradedCardsForLearningDay;
        LearningDayText = LearningDayCounted
            ? AppResources.Summary_LearningDay_Counted
            : string.Format(Ukrainian, NotYetFormat, LearningPolicy.MinGradedCardsForLearningDay - Graded);

        var sessionId = query.TryGetValue("session", out var raw) && Guid.TryParse(raw?.ToString(), out var id) ? id : Guid.Empty;
        if (sessionId != Guid.Empty)
        {
            _report = new SessionReport(sessionId, Graded, Correct, Read(query, "again"), Read(query, "typed"), Read(query, "learned"), LearningDayCounted, Read(query, "warmup") == 1);
        }
    }

    /// <summary>Settles the economy once per summary; safe to call on every appearance.</summary>
    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (_settled || _report is null)
        {
            return;
        }

        _settled = true;
        try
        {
            var view = await _settle.ExecuteAsync(_report, cancellationToken).ConfigureAwait(true);
            var s = view.Settlement;
            if (!view.GamificationEnabled || s.IsEmpty)
            {
                ShowRewards = false;
                return;
            }

            XpGainedText = string.Format(Ukrainian, XpGainedFormat, s.XpGained);
            ThornsGainedText = string.Format(Ukrainian, ThornsGainedFormat, s.ThornsGained);
            ThornsBefore = s.State.Thorns - s.ThornsGained;
            ThornsTotal = s.State.Thorns;
            StreakLength = s.StreakAfter;
            StreakText = RewardsText.StreakDays(s.StreakAfter);
            LevelText = RewardsText.Level(s.LevelAfter);
            ShowRewards = true;

            if (s.StreakMilestone is { } milestone)
            {
                _celebrations.Enqueue(new CelebrationItem(
                    StreakRules.CelebrationLevel(milestone),
                    AppResources.Celebration_Milestone_Eyebrow,
                    string.Format(Ukrainian, MilestoneTitleFormat, RewardsText.Days(milestone)),
                    AppResources.Celebration_Milestone_Body,
                    RewardsText.ThornsReward(StreakRules.MilestoneThorns(milestone)),
                    Icon.Flame,
                    "mascot_party.png",
                    AppResources.Celebration_Button));
            }

            if (s.LeveledUp)
            {
                _celebrations.Enqueue(new CelebrationItem(3, AppResources.Celebration_LevelUp_Eyebrow, RewardsText.Level(s.LevelAfter), AppResources.Celebration_LevelUp_Body, string.Empty, Icon.Award, "mascot_party.png", AppResources.Celebration_Button));
            }

            foreach (var achievement in s.NewAchievements)
            {
                _celebrations.Enqueue(new CelebrationItem(
                    3,
                    achievement.Hidden ? AppResources.Celebration_Hidden_Eyebrow : AppResources.Celebration_Achievement_Eyebrow,
                    RewardsText.AchievementTitle(achievement.Id),
                    RewardsText.AchievementBody(achievement.Id),
                    RewardsText.ThornsReward(achievement.Thorns),
                    RewardsText.AchievementGlyph(achievement.Icon),
                    achievement.Hidden ? "mascot_surprise.png" : "mascot_party.png",
                    AppResources.Celebration_Button));
            }

            foreach (var item in _celebrations)
            {
                Celebrations.Add(item);
            }

            ShowNextCelebration();
        }
        catch (OperationCanceledException)
        {
            _settled = false;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogFailed(ex);
        }
    }

    [RelayCommand]
    private void DismissCelebration() => ShowNextCelebration();

    [RelayCommand]
    private Task BackToTodayAsync(CancellationToken cancellationToken) => _navigation.GoToAsync(Routes.Today.Root, null, cancellationToken);

    private void ShowNextCelebration()
    {
        if (_celebrations.TryDequeue(out var next))
        {
            CurrentCelebration = _celebrations.Count == 0 ? next with { Button = AppResources.Celebration_Button_Last } : next;
            return;
        }

        CurrentCelebration = null;
    }

    private static int Read(IDictionary<string, object> query, string key)
        => query.TryGetValue(key, out var raw) && int.TryParse(raw?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;

    [LoggerMessage(Level = LogLevel.Error, Message = "Session rewards failed")]
    private partial void LogFailed(Exception exception);
}
