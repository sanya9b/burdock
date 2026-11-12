using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Burdock.Application.Ports;
using Burdock.Application.UseCases.Reviews;
using Burdock.Application.UseCases.Rewards;
using Burdock.Application.UseCases.Sessions;
using Burdock.Domain.Learning;
using Burdock.Domain.Rewards;
using Burdock.Domain.Vocabulary;
using Burdock.Presentation.Localization;
using Burdock.Presentation.Navigation;
using Burdock.Presentation.Resources.Strings;
using Burdock.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Burdock.Presentation.ViewModels;

/// <summary>
/// S-10a «Повторимо» (owner decision 2026-11-12, log item 24): due learned words as packs of five pairs word ↔ meaning. A
/// correct pair grades Good, a mistake grades Again for the word tapped first and sends it back to «Вчу»; each word is
/// graded once per pack, by its first outcome. The round settles XP like a session and ends with a short summary.
/// </summary>
public sealed partial class ReviewViewModel : ObservableObject
{
    private const int MismatchResetMilliseconds = 400;

    private static readonly CultureInfo Ukrainian = CultureInfo.GetCultureInfo("uk-UA");
    private static readonly CompositeFormat ProgressFormat = CompositeFormat.Parse(AppResources.Review_Progress_Label);
    private static readonly CompositeFormat SummaryOne = CompositeFormat.Parse(AppResources.Review_Summary_One);
    private static readonly CompositeFormat SummaryFew = CompositeFormat.Parse(AppResources.Review_Summary_Few);
    private static readonly CompositeFormat SummaryMany = CompositeFormat.Parse(AppResources.Review_Summary_Many);
    private static readonly CompositeFormat DemotedFormat = CompositeFormat.Parse(AppResources.Review_Demoted_Format);
    private static readonly CompositeFormat XpFormat = CompositeFormat.Parse(AppResources.Review_Xp_Format);

    private readonly GetReviewRoundUseCase _round;
    private readonly GradeReviewPairUseCase _grade;
    private readonly SettleSessionRewardsUseCase _settle;
    private readonly INavigationService _navigation;
    private readonly IHaptics _haptics;
    private readonly IIdGenerator _ids;
    private readonly ISyncCoordinator _sync;
    private readonly IWidgetCoordinator _widgets;
    private readonly ILogger<ReviewViewModel> _logger;
    private readonly HashSet<int> _gradedInPack = [];
    private readonly List<string> _demoted = [];
    private readonly Stopwatch _stopwatch = new();

    private IReadOnlyList<MatchingItem> _packs = [];
    private int _packIndex;
    private OptionViewModel? _pendingWord;
    private int _matchedPairs;
    private Guid _sessionId;
    private bool _loaded;
    private bool _settled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPlaying))]
    private ViewState _state = ViewState.Loading;

    /// <summary>The round is over (or was closed after at least one pair): the summary replaces the pairs.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPlaying))]
    private bool _isSummary;

    [ObservableProperty]
    private int _packCount;

    [ObservableProperty]
    private int _packsDone;

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    private int _graded;

    [ObservableProperty]
    private int _correct;

    [ObservableProperty]
    private int _again;

    [ObservableProperty]
    private string _summaryTitle = string.Empty;

    [ObservableProperty]
    private string _summaryBody = string.Empty;

    [ObservableProperty]
    private string _summaryMascot = "mascot_party.png";

    [ObservableProperty]
    private string _demotedText = string.Empty;

    [ObservableProperty]
    private bool _hasDemoted;

    [ObservableProperty]
    private string _xpText = string.Empty;

    [ObservableProperty]
    private bool _showXp;

    /// <summary>Bumped on every mistake so the page can shake and buzz.</summary>
    [ObservableProperty]
    private int _mistakeTick;

    public ReviewViewModel(
        GetReviewRoundUseCase round,
        GradeReviewPairUseCase grade,
        SettleSessionRewardsUseCase settle,
        INavigationService navigation,
        IHaptics haptics,
        IIdGenerator ids,
        ISyncCoordinator sync,
        IWidgetCoordinator widgets,
        ILogger<ReviewViewModel> logger)
    {
        _round = round ?? throw new ArgumentNullException(nameof(round));
        _grade = grade ?? throw new ArgumentNullException(nameof(grade));
        _settle = settle ?? throw new ArgumentNullException(nameof(settle));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _haptics = haptics ?? throw new ArgumentNullException(nameof(haptics));
        _ids = ids ?? throw new ArgumentNullException(nameof(ids));
        _sync = sync ?? throw new ArgumentNullException(nameof(sync));
        _widgets = widgets ?? throw new ArgumentNullException(nameof(widgets));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Pause after a completed pack before the next one appears (same dwell as a correct answer in the session); tests set zero.</summary>
    public TimeSpan PackDwell { get; set; } = SessionViewModel.AutoAdvanceAfterCorrect;

    public ObservableCollection<OptionViewModel> MatchWords { get; } = [];

    public ObservableCollection<OptionViewModel> MatchMeanings { get; } = [];

    public bool IsPlaying => State == ViewState.Ready && !IsSummary;

    /// <summary>Lemmas that fell back to «Вчу» in this round, in order.</summary>
    public IReadOnlyList<string> Demoted => _demoted;

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        // Coming back to the page must not rebuild the round halfway through.
        if (_loaded)
        {
            return;
        }

        State = ViewState.Loading;
        try
        {
            var round = await _round.ExecuteAsync(cancellationToken).ConfigureAwait(true);
            _packs = round.Packs;
            _loaded = true;
            if (_packs.Count == 0)
            {
                State = ViewState.Empty;
                return;
            }

            _sessionId = _ids.NewId();
            PackCount = _packs.Count;
            PacksDone = 0;
            ShowPack(0);
            State = ViewState.Ready;
        }
        catch (OperationCanceledException)
        {
            // Navigation away cancels the load.
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogLoadFailed(ex);
            State = ViewState.Error;
        }
    }

    private void ShowPack(int index)
    {
        _packIndex = index;
        var pack = _packs[index];
        MatchWords.Clear();
        MatchMeanings.Clear();
        _pendingWord = null;
        _matchedPairs = 0;
        _gradedInPack.Clear();

        // Deterministic shuffle from the pack's content, as in the session warm-up: the same pack always lays out the same way.
        var seed = pack.Entries.Aggregate(17, (acc, e) => unchecked((acc * 31) + e.Id.Value.GetHashCode()));
        var random = new Random(seed);
        foreach (var word in pack.Entries.Select((e, i) => new OptionViewModel(e.Lexeme.Lemma, i, isWord: true)).OrderBy(_ => random.Next()))
        {
            MatchWords.Add(word);
        }

        foreach (var meaning in pack.Entries.Select((e, i) => new OptionViewModel(e.Translation, i, isWord: false)).OrderBy(_ => random.Next()))
        {
            MatchMeanings.Add(meaning);
        }

        ProgressText = string.Format(Ukrainian, ProgressFormat, index + 1, PackCount);
        _stopwatch.Restart();
    }

    [RelayCommand]
    private async Task PickMatchAsync(OptionViewModel? option)
    {
        if (option is null || !IsPlaying || option.Visual == OptionVisual.Correct)
        {
            return;
        }

        if (option.IsWord)
        {
            if (_pendingWord is not null)
            {
                _pendingWord.Visual = OptionVisual.Default;
            }

            _pendingWord = option;
            option.Visual = OptionVisual.Selected;
            return;
        }

        if (_pendingWord is null)
        {
            return;
        }

        var word = _pendingWord;
        _pendingWord = null;
        var entry = _packs[_packIndex].Entries[word.Index];
        if (word.Index == option.Index)
        {
            word.Visual = OptionVisual.Correct;
            option.Visual = OptionVisual.Correct;
            _matchedPairs++;
            _haptics.Light();
            await GradeOnceAsync(word.Index, entry, correct: true).ConfigureAwait(true);
            if (_matchedPairs == MatchWords.Count)
            {
                await CompletePackAsync().ConfigureAwait(true);
            }

            return;
        }

        // The mistake belongs to the word the learner was looking a pair for; the meaning tapped by error is not punished (log item 24).
        word.Visual = OptionVisual.Default;
        option.Visual = OptionVisual.Incorrect;
        _haptics.Error();
        MistakeTick++;
        _ = ResetAfterPauseAsync(option);
        await GradeOnceAsync(word.Index, entry, correct: false).ConfigureAwait(true);
    }

    private async Task GradeOnceAsync(int index, WordEntry entry, bool correct)
    {
        // The first outcome decides: a word matched correctly after a mistake is not graded again.
        if (!_gradedInPack.Add(index))
        {
            return;
        }

        var result = await _grade.ExecuteAsync(entry.Id, correct, _stopwatch.Elapsed, CancellationToken.None).ConfigureAwait(true);
        if (result.IsFailure)
        {
            LogGradeFailed(result.Error.Code);
            return;
        }

        Graded++;
        if (correct)
        {
            Correct++;
        }
        else
        {
            Again++;
        }

        if (result.Value.LeftLearned)
        {
            _demoted.Add(entry.Lexeme.Lemma);
            HasDemoted = true;
        }
    }

    private async Task CompletePackAsync()
    {
        PacksDone = _packIndex + 1;
        if (_packIndex + 1 < _packs.Count)
        {
            if (PackDwell > TimeSpan.Zero)
            {
                await Task.Delay(PackDwell).ConfigureAwait(true);
            }

            if (IsSummary)
            {
                return;
            }

            ShowPack(_packIndex + 1);
            return;
        }

        await FinishAsync().ConfigureAwait(true);
    }

    private async Task FinishAsync()
    {
        SummaryTitle = AppResources.Review_Summary_Title;
        SummaryBody = Plural.Uk(Graded, SummaryOne, SummaryFew, SummaryMany);
        SummaryMascot = _demoted.Count > 0 ? "mascot_cheer.png" : "mascot_party.png";
        DemotedText = _demoted.Count > 0
            ? string.Format(Ukrainian, DemotedFormat, string.Join(", ", _demoted))
            : AppResources.Review_Summary_AllKept;
        await SettleAsync().ConfigureAwait(true);
        IsSummary = true;
    }

    /// <summary>XP for the graded pairs, once per round (spec 07 §3.2.1: effort counts, an Again still earns); then sync and the widget catch up.</summary>
    private async Task SettleAsync()
    {
        if (_settled || Graded == 0)
        {
            return;
        }

        _settled = true;
        try
        {
            var report = new SessionReport(_sessionId, Graded, Correct, Again, 0, 0, Graded >= LearningPolicy.MinGradedCardsForLearningDay, false);
            var view = await _settle.ExecuteAsync(report, CancellationToken.None).ConfigureAwait(true);
            ShowXp = view.GamificationEnabled && view.Settlement.XpGained > 0;
            XpText = string.Format(Ukrainian, XpFormat, view.Settlement.XpGained);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogSettleFailed(ex);
        }

        _ = _sync.TriggerAsync("review", CancellationToken.None);
        _ = _widgets.TriggerAsync("review", CancellationToken.None);
    }

    private static async Task ResetAfterPauseAsync(OptionViewModel option)
    {
        await Task.Delay(MismatchResetMilliseconds).ConfigureAwait(true);
        if (option.Visual == OptionVisual.Incorrect)
        {
            option.Visual = OptionVisual.Default;
        }
    }

    /// <summary>Leaving halfway keeps every graded pair; the rewards for them are settled quietly.</summary>
    [RelayCommand]
    private async Task CloseAsync(CancellationToken cancellationToken)
    {
        if (!IsSummary)
        {
            await SettleAsync().ConfigureAwait(true);
        }

        await _navigation.GoBackAsync(cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private Task DoneAsync(CancellationToken cancellationToken) => _navigation.GoToAsync(Routes.Today.Root, null, cancellationToken);

    [LoggerMessage(Level = LogLevel.Error, Message = "Review round failed to load")]
    private partial void LogLoadFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Review pair was not graded ({Code})")]
    private partial void LogGradeFailed(string code);

    [LoggerMessage(Level = LogLevel.Error, Message = "Review round rewards were not settled")]
    private partial void LogSettleFailed(Exception exception);
}
