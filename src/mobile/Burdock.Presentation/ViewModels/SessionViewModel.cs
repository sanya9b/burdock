using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Burdock.Application.Ports;
using Burdock.Application.UseCases.Sessions;
using Burdock.Domain.Learning;
using Burdock.Presentation.Navigation;
using Burdock.Presentation.Resources.Icons;
using Burdock.Presentation.Resources.Strings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Burdock.Presentation.ViewModels;

/// <summary>Visual state of one answer option (QuizOption component, spec 02 §3.11).</summary>
public enum OptionVisual
{
    Default,
    Selected,
    Correct,
    Incorrect,
    Disabled,
}

/// <summary>An option in a choice or matching exercise.</summary>
public sealed partial class OptionViewModel(string text, int index, bool isWord) : ObservableObject
{
    public string Text { get; } = text;

    public int Index { get; } = index;

    public bool IsWord { get; } = isWord;

    [ObservableProperty]
    private OptionVisual _visual = OptionVisual.Default;
}

/// <summary>S-12 «Сесія»: drives the three exercise kinds, grading, feedback and the counters the economy needs (spec 03, spec 06 §3.6-3.7, spec 07).</summary>
public sealed partial class SessionViewModel : ObservableObject, IQueryAttributable
{
    private const int MismatchResetMilliseconds = 400;

    private static readonly CultureInfo Ukrainian = CultureInfo.GetCultureInfo("uk-UA");
    private static readonly CompositeFormat ProgressFormat = CompositeFormat.Parse(AppResources.Session_Progress_Label);
    private static readonly CompositeFormat TypoFormat = CompositeFormat.Parse(AppResources.Session_Feedback_Typo);
    private static readonly CompositeFormat WrongFormat = CompositeFormat.Parse(AppResources.Session_Feedback_Wrong);
    private static readonly CompositeFormat AnswerDetailFormat = CompositeFormat.Parse(AppResources.Session_Feedback_Answer_Detail);
    private static readonly CompositeFormat ComboFormat = CompositeFormat.Parse(AppResources.Session_Combo);
    private static readonly CompositeFormat ComboA11yFormat = CompositeFormat.Parse(AppResources.Session_Combo_A11y);

    private readonly BuildSessionUseCase _buildSession;
    private readonly GradeAnswerUseCase _gradeAnswer;
    private readonly INavigationService _navigation;
    private readonly IIdGenerator _ids;
    private readonly IClock _clock;
    private readonly ISpeechService _speech;
    private readonly ILogger<SessionViewModel> _logger;
    private readonly Stopwatch _stopwatch = new();
    private readonly List<SessionItem> _items = [];
    private Guid _sessionId;
    private int _index = -1;
    private int _size = BuildSessionUseCase.DefaultSize;
    private int _graded;
    private int _correct;
    private int _againCount;
    private int _typedCorrect;
    private int _wordsLearned;
    private bool _warmupCompleted;
    private int _combo;
    private DateTimeOffset _startedAt;
    private OptionViewModel? _pendingWord;
    private int _matchedPairs;

    [ObservableProperty]
    private ViewState _state = ViewState.Loading;

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private int _itemCount = 1;

    [ObservableProperty]
    private int _completedCount;

    [ObservableProperty]
    private string _prompt = string.Empty;

    [ObservableProperty]
    private string _stimulus = string.Empty;

    [ObservableProperty]
    private string _stimulusSecondary = string.Empty;

    [ObservableProperty]
    private bool _isChoice;

    [ObservableProperty]
    private bool _isTyped;

    [ObservableProperty]
    private bool _isMatching;

    [ObservableProperty]
    private string _typedAnswer = string.Empty;

    /// <summary>Full sentence for screen readers («Ще ні. Правильно: …»).</summary>
    [ObservableProperty]
    private string _feedback = string.Empty;

    [ObservableProperty]
    private string _feedbackTitle = string.Empty;

    [ObservableProperty]
    private string _feedbackDetail = string.Empty;

    [ObservableProperty]
    private bool _hasFeedbackDetail;

    [ObservableProperty]
    private string _feedbackIcon = Icon.CircleCheck;

    [ObservableProperty]
    private bool _feedbackIsPositive;

    [ObservableProperty]
    private bool _answered;

    [ObservableProperty]
    private bool _isLast;

    /// <summary>Consecutive correct graded answers; the combo chip shows from two (spec 07 §3.8: joy, never pressure).</summary>
    [ObservableProperty]
    private int _comboCount;

    [ObservableProperty]
    private string _comboText = string.Empty;

    [ObservableProperty]
    private string _comboAccessibility = string.Empty;

    [ObservableProperty]
    private bool _showCombo;

    /// <summary>Increments on every graded answer so the page can play the matching animation and haptic.</summary>
    [ObservableProperty]
    private int _answerTick;

    [ObservableProperty]
    private bool _lastAnswerCorrect;

    public SessionViewModel(BuildSessionUseCase buildSession, GradeAnswerUseCase gradeAnswer, INavigationService navigation, IIdGenerator ids, IClock clock, ISpeechService speech, ILogger<SessionViewModel> logger)
    {
        _buildSession = buildSession ?? throw new ArgumentNullException(nameof(buildSession));
        _gradeAnswer = gradeAnswer ?? throw new ArgumentNullException(nameof(gradeAnswer));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _ids = ids ?? throw new ArgumentNullException(nameof(ids));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _speech = speech ?? throw new ArgumentNullException(nameof(speech));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public ObservableCollection<OptionViewModel> Options { get; } = [];

    public ObservableCollection<OptionViewModel> MatchWords { get; } = [];

    public ObservableCollection<OptionViewModel> MatchMeanings { get; } = [];

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.TryGetValue("size", out var raw) && int.TryParse(raw?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var size))
        {
            _size = Math.Clamp(size, 1, 30);
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (_items.Count > 0)
        {
            return;
        }

        State = ViewState.Loading;
        try
        {
            var plan = await _buildSession.ExecuteAsync(_size, cancellationToken).ConfigureAwait(true);
            _items.AddRange(plan.Items);
            _sessionId = _ids.NewId();
            _startedAt = _clock.UtcNow;
            if (_items.Count == 0)
            {
                State = ViewState.Empty;
                return;
            }

            ItemCount = _items.Count;
            State = ViewState.Ready;
            ShowNext();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogFailed(ex);
            State = ViewState.Error;
        }
    }

    private void ShowNext()
    {
        _index++;
        if (_index >= _items.Count)
        {
            _ = FinishAsync(CancellationToken.None);
            return;
        }

        var item = _items[_index];
        Answered = false;
        Feedback = string.Empty;
        FeedbackTitle = string.Empty;
        FeedbackDetail = string.Empty;
        HasFeedbackDetail = false;
        TypedAnswer = string.Empty;
        IsLast = _index == _items.Count - 1;
        ProgressText = string.Format(Ukrainian, ProgressFormat, _index + 1, _items.Count);
        Progress = (_index + 1) / (double)_items.Count;
        CompletedCount = _index;
        Options.Clear();
        MatchWords.Clear();
        MatchMeanings.Clear();
        _pendingWord = null;
        _matchedPairs = 0;

        IsChoice = item is ChoiceItem;
        IsTyped = item is TypedItem;
        IsMatching = item is MatchingItem;

        switch (item)
        {
            case ChoiceItem choice:
                Prompt = choice.Mode == ExerciseMode.ChoiceTargetToNative ? AppResources.Session_Choice_TargetToNative_Prompt : AppResources.Session_Choice_NativeToTarget_Prompt;
                Stimulus = choice.Prompt;
                StimulusSecondary = choice.Mode == ExerciseMode.ChoiceTargetToNative ? Localization.Pronunciation.PreferredIpa(choice.Entry, _speech.Accent) : string.Empty;
                for (var i = 0; i < choice.Options.Count; i++)
                {
                    Options.Add(new OptionViewModel(choice.Options[i], i, isWord: false));
                }

                break;
            case TypedItem typed:
                Prompt = AppResources.Session_Typed_Prompt;
                Stimulus = typed.Prompt;
                StimulusSecondary = string.Empty;
                break;
            case MatchingItem matching:
                Prompt = AppResources.Session_Matching_Prompt;
                Stimulus = string.Empty;
                StimulusSecondary = string.Empty;
                var seed = matching.Entries.Aggregate(17, (acc, e) => unchecked(acc * 31 + e.Id.Value.GetHashCode()));
                var random = new Random(seed);
                var words = matching.Entries.Select((e, i) => new OptionViewModel(e.Lexeme.Lemma, i, isWord: true)).OrderBy(_ => random.Next()).ToList();
                var meanings = matching.Entries.Select((e, i) => new OptionViewModel(e.Translation, i, isWord: false)).OrderBy(_ => random.Next()).ToList();
                foreach (var w in words)
                {
                    MatchWords.Add(w);
                }

                foreach (var m in meanings)
                {
                    MatchMeanings.Add(m);
                }

                break;
        }

        _stopwatch.Restart();
    }

    [RelayCommand]
    private async Task ChooseAsync(OptionViewModel option)
    {
        if (Answered || _items[_index] is not ChoiceItem choice)
        {
            return;
        }

        _stopwatch.Stop();
        var correct = option.Index == choice.CorrectIndex;
        option.Visual = correct ? OptionVisual.Correct : OptionVisual.Incorrect;
        if (!correct)
        {
            Options[choice.CorrectIndex].Visual = OptionVisual.Correct;
        }

        foreach (var other in Options.Where(o => o.Visual == OptionVisual.Default))
        {
            other.Visual = OptionVisual.Disabled;
        }

        await GradeAsync(new ExerciseOutcome(choice.Mode, correct, _stopwatch.Elapsed), choice.Options[choice.CorrectIndex]).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task CheckTypedAsync()
    {
        if (Answered || _items[_index] is not TypedItem typed)
        {
            return;
        }

        _stopwatch.Stop();
        var check = AnswerNormalizer.Check(TypedAnswer, typed.AcceptedForms);
        await GradeAsync(new ExerciseOutcome(ExerciseMode.TypedRecall, check.Correct, _stopwatch.Elapsed, check.NearMiss), typed.AcceptedForms[0]).ConfigureAwait(true);
    }

    [RelayCommand]
    private void PickMatch(OptionViewModel option)
    {
        if (Answered || option.Visual is OptionVisual.Correct)
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

        if (_pendingWord.Index == option.Index)
        {
            _pendingWord.Visual = OptionVisual.Correct;
            option.Visual = OptionVisual.Correct;
            _matchedPairs++;
        }
        else
        {
            _pendingWord.Visual = OptionVisual.Default;
            option.Visual = OptionVisual.Incorrect;
            _ = ResetAfterPauseAsync(option);
        }

        _pendingWord = null;
        if (_matchedPairs == MatchWords.Count)
        {
            _warmupCompleted = true;
            SetFeedback(positive: true, AppResources.Session_Feedback_Matching_Title, string.Empty, AppResources.Session_Feedback_Correct);
            CompletedCount = _index + 1;
            Answered = true;
            LastAnswerCorrect = true;
            AnswerTick++;
        }
    }

    [RelayCommand]
    private void Next() => ShowNext();

    [RelayCommand]
    private Task CloseAsync(CancellationToken cancellationToken) => _navigation.GoBackAsync(cancellationToken);

    [RelayCommand]
    private Task FinishAsync(CancellationToken cancellationToken)
    {
        var duration = _clock.UtcNow - _startedAt;
        var parameters = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["session"] = _sessionId.ToString("D"),
            ["graded"] = _graded.ToString(CultureInfo.InvariantCulture),
            ["correct"] = _correct.ToString(CultureInfo.InvariantCulture),
            ["again"] = _againCount.ToString(CultureInfo.InvariantCulture),
            ["typed"] = _typedCorrect.ToString(CultureInfo.InvariantCulture),
            ["learned"] = _wordsLearned.ToString(CultureInfo.InvariantCulture),
            ["warmup"] = _warmupCompleted ? "1" : "0",
            ["seconds"] = ((int)duration.TotalSeconds).ToString(CultureInfo.InvariantCulture),
        };
        return _navigation.GoToAsync(Routes.Learn.Summary, parameters, cancellationToken);
    }

    private async Task GradeAsync(ExerciseOutcome outcome, string correctAnswer)
    {
        Answered = true;
        CompletedCount = _index + 1;
        var result = await _gradeAnswer.ExecuteAsync(_items[_index], outcome, CancellationToken.None).ConfigureAwait(true);
        if (result.IsFailure)
        {
            SetFeedback(positive: false, AppResources.Common_Error_Title, string.Empty, AppResources.Common_Error_Title);
            return;
        }

        _graded++;
        if (outcome.Correct)
        {
            _correct++;
            _combo++;
            if (outcome.Mode == ExerciseMode.TypedRecall)
            {
                _typedCorrect++;
            }
        }
        else
        {
            _combo = 0;
        }

        if (result.Value.Rating == Rating.Again)
        {
            _againCount++;
        }

        if (result.Value.BecameLearned)
        {
            _wordsLearned++;
        }

        ComboCount = _combo;
        ComboText = string.Format(Ukrainian, ComboFormat, _combo);
        ComboAccessibility = string.Format(Ukrainian, ComboA11yFormat, _combo);
        ShowCombo = _combo >= 2;

        var detail = string.Format(Ukrainian, AnswerDetailFormat, correctAnswer);
        switch (outcome)
        {
            case { Correct: true, NearMiss: true }:
                SetFeedback(positive: true, AppResources.Session_Feedback_Typo_Title, detail, string.Format(Ukrainian, TypoFormat, correctAnswer));
                break;
            case { Correct: true }:
                SetFeedback(positive: true, AppResources.Session_Feedback_Correct_Title, string.Empty, AppResources.Session_Feedback_Correct);
                break;
            default:
                SetFeedback(positive: false, AppResources.Session_Feedback_Wrong_Title, detail, string.Format(Ukrainian, WrongFormat, correctAnswer));
                break;
        }

        LastAnswerCorrect = outcome.Correct;
        AnswerTick++;
    }

    /// <summary>A mismatched meaning flashes red, then returns to neutral (spec 02 section 3.8).</summary>
    private static async Task ResetAfterPauseAsync(OptionViewModel wrong)
    {
        await Task.Delay(MismatchResetMilliseconds).ConfigureAwait(true);
        if (wrong.Visual == OptionVisual.Incorrect)
        {
            wrong.Visual = OptionVisual.Default;
        }
    }

    private void SetFeedback(bool positive, string title, string detail, string spoken)
    {
        FeedbackIsPositive = positive;
        FeedbackIcon = positive ? Icon.CircleCheck : Icon.CircleX;
        FeedbackTitle = title;
        FeedbackDetail = detail;
        HasFeedbackDetail = detail.Length > 0;
        Feedback = spoken;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Session failed")]
    private partial void LogFailed(Exception exception);
}
