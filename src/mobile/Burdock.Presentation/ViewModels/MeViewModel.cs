using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Burdock.Application.UseCases.Rewards;
using Burdock.Domain.Rewards;
using Burdock.Presentation.Localization;
using Burdock.Presentation.Navigation;
using Burdock.Presentation.Resources.Strings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Burdock.Presentation.ViewModels;

/// <summary>One achievement tile of the profile (spec 07 §3.6): hidden ones show only «Секрет» until unlocked.</summary>
public sealed record AchievementTile(string Title, string Body, string Glyph, bool Unlocked, bool Secret, string Reward);

/// <summary>S-17 «Я»: anonymous learner, level and XP, thorns, streak, achievements, entry to settings (spec 03; economy per spec 07).</summary>
public sealed partial class MeViewModel : ObservableObject
{
    private static readonly CultureInfo Ukrainian = CultureInfo.GetCultureInfo("uk-UA");
    private static readonly CompositeFormat XpProgressFormat = CompositeFormat.Parse(AppResources.Rewards_Xp_Progress);
    private static readonly CompositeFormat XpMaxFormat = CompositeFormat.Parse(AppResources.Rewards_Xp_Max);

    private readonly INavigationService _navigation;
    private readonly GetRewardsOverviewUseCase _overview;
    private readonly ILogger<MeViewModel> _logger;

    [ObservableProperty]
    private ViewState _state = ViewState.Ready;

    [ObservableProperty]
    private bool _showRewards;

    [ObservableProperty]
    private string _levelText = string.Empty;

    [ObservableProperty]
    private double _xpProgress;

    [ObservableProperty]
    private string _xpText = string.Empty;

    [ObservableProperty]
    private string _thornsText = string.Empty;

    [ObservableProperty]
    private string _streakText = string.Empty;

    [ObservableProperty]
    private string _achievementsSummary = string.Empty;

    public MeViewModel(INavigationService navigation, GetRewardsOverviewUseCase overview, ILogger<MeViewModel> logger)
    {
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _overview = overview ?? throw new ArgumentNullException(nameof(overview));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public ObservableCollection<AchievementTile> Achievements { get; } = [];

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var overview = await _overview.ExecuteAsync(cancellationToken).ConfigureAwait(true);
            ShowRewards = overview.GamificationEnabled;
            LevelText = RewardsText.Level(overview.Level);
            XpProgress = overview.XpNeededForLevel == 0 ? 1 : (double)overview.XpEarnedInLevel / overview.XpNeededForLevel;
            XpText = overview.XpNeededForLevel == 0
                ? string.Format(Ukrainian, XpMaxFormat, overview.Xp)
                : string.Format(Ukrainian, XpProgressFormat, overview.XpEarnedInLevel, overview.XpNeededForLevel);
            ThornsText = RewardsText.Thorns(overview.Thorns);
            StreakText = RewardsText.StreakDays(overview.StreakLength);

            Achievements.Clear();
            var unlockedCount = 0;
            foreach (var definition in AchievementCatalog.All)
            {
                var unlocked = overview.Achievements.Contains(definition.Id);
                unlockedCount += unlocked ? 1 : 0;
                var secret = definition.Hidden && !unlocked;
                Achievements.Add(new AchievementTile(
                    secret ? AppResources.Me_Achievement_Secret : RewardsText.AchievementTitle(definition.Id),
                    secret ? string.Empty : RewardsText.AchievementBody(definition.Id),
                    secret ? Resources.Icons.Icon.CircleHelp : RewardsText.AchievementGlyph(definition.Icon),
                    unlocked,
                    secret,
                    RewardsText.ThornsReward(definition.Thorns)));
            }

            AchievementsSummary = string.Create(Ukrainian, $"{unlockedCount} / {AchievementCatalog.All.Count}");
            State = ViewState.Ready;
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
    private Task OpenSettingsAsync(CancellationToken cancellationToken) => _navigation.GoToAsync(Routes.Me.Settings, null, cancellationToken);

    [LoggerMessage(Level = LogLevel.Error, Message = "Profile failed to load")]
    private partial void LogFailed(Exception exception);
}
