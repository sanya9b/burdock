using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Burdock.Application.Ports;
using Burdock.Application.UseCases.Content;
using Burdock.Application.UseCases.LearningDays;
using Burdock.Application.UseCases.Planning;
using Burdock.Domain.Learning;
using Burdock.Presentation.Navigation;
using Burdock.Presentation.Resources.Strings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Burdock.Presentation.ViewModels;

/// <summary>One tile in the «Слова дня» strip.</summary>
public sealed record WordTile(Guid LexemeId, string Lemma, string Translation, string Reason, string ReasonKey);

/// <summary>S-10 «Сьогодні»: greeting, learning day, today's words, session CTA (spec 03).</summary>
public sealed partial class TodayViewModel : ObservableObject
{
    private static readonly CultureInfo Ukrainian = CultureInfo.GetCultureInfo("uk-UA");
    private static readonly CompositeFormat LearningDayFormat = CompositeFormat.Parse(AppResources.Today_LearningDay_Label);
    private static readonly CompositeFormat PlanSummaryFormat = CompositeFormat.Parse(AppResources.Today_Plan_Summary);

    private readonly LearningDayResolver _learningDays;
    private readonly IClock _clock;
    private readonly BootstrapContentUseCase _bootstrap;
    private readonly GetDailyPlanUseCase _dailyPlan;
    private readonly INavigationService _navigation;
    private readonly ILogger<TodayViewModel> _logger;

    [ObservableProperty]
    private ViewState _state = ViewState.Loading;

    [ObservableProperty]
    private string _greeting = string.Empty;

    [ObservableProperty]
    private string _learningDayText = string.Empty;

    [ObservableProperty]
    private string _planSummary = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartSessionCommand))]
    private bool _hasWords;

    public TodayViewModel(LearningDayResolver learningDays, IClock clock, BootstrapContentUseCase bootstrap, GetDailyPlanUseCase dailyPlan, INavigationService navigation, ILogger<TodayViewModel> logger)
    {
        _learningDays = learningDays ?? throw new ArgumentNullException(nameof(learningDays));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _bootstrap = bootstrap ?? throw new ArgumentNullException(nameof(bootstrap));
        _dailyPlan = dailyPlan ?? throw new ArgumentNullException(nameof(dailyPlan));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        RefreshHeader();
    }

    public ObservableCollection<WordTile> Words { get; } = [];

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

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        RefreshHeader();
        if (Words.Count == 0)
        {
            State = ViewState.Loading;
        }

        try
        {
            await _bootstrap.ExecuteAsync(cancellationToken).ConfigureAwait(true);
            var plan = await _dailyPlan.ExecuteAsync(cancellationToken).ConfigureAwait(true);

            Words.Clear();
            foreach (var word in plan.Words)
            {
                Words.Add(new WordTile(word.Entry.Id.Value, word.Entry.Lexeme.Lemma, word.Entry.Translation, ReasonLabel(word.Reason), word.Reason.ToString()));
            }

            PlanSummary = string.Format(Ukrainian, PlanSummaryFormat, plan.NewCount, plan.DueCount);
            HasWords = Words.Count > 0;
            State = HasWords ? ViewState.Ready : ViewState.Empty;
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
    }

    [RelayCommand]
    private Task OpenWordAsync(WordTile tile)
        => _navigation.GoToAsync(Routes.Today.Word, new Dictionary<string, object>(StringComparer.Ordinal) { ["id"] = tile.LexemeId.ToString("D") });

    [RelayCommand(CanExecute = nameof(HasWords))]
    private Task StartSessionAsync()
        => _navigation.GoToAsync(Routes.Learn.Session, new Dictionary<string, object>(StringComparer.Ordinal) { ["size"] = "10" });

    private static string ReasonLabel(PlanReason reason) => reason switch
    {
        PlanReason.Due => AppResources.Today_Reason_Due,
        PlanReason.Learning => AppResources.Today_Reason_Learning,
        _ => AppResources.Today_Reason_New,
    };

    [LoggerMessage(Level = LogLevel.Error, Message = "Today screen failed to load")]
    private partial void LogLoadFailed(Exception exception);
}
