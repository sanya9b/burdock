using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Burdock.Application.Ports;
using Burdock.Application.UseCases.Words;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;
using Burdock.Presentation.Localization;
using Burdock.Presentation.Navigation;
using Burdock.Presentation.Resources.Strings;
using Burdock.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Burdock.Presentation.ViewModels;

/// <summary>One row of the library list; <see cref="StatusKey"/> is the <c>LibraryStatus</c> name used for the dot colour.</summary>
public sealed record WordRow(Guid LexemeId, string Lemma, string Translation, string StatusLabel, string StatusKey, string DueText, string Level, string Accessibility);

/// <summary>S-14 «Слова»: search with a 200 ms debounce, status chips, level and category pickers, the list (spec 03).</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "The debounce token source is replaced and cancelled per change; the transient view model lives as long as its page.")]
public sealed partial class WordsViewModel : ObservableObject
{
    private const int SearchDebounceMilliseconds = 200;

    private static readonly CultureInfo Ukrainian = CultureInfo.GetCultureInfo("uk-UA");
    private static readonly CompositeFormat CountOne = CompositeFormat.Parse(AppResources.Words_Count_One);
    private static readonly CompositeFormat CountFew = CompositeFormat.Parse(AppResources.Words_Count_Few);
    private static readonly CompositeFormat CountMany = CompositeFormat.Parse(AppResources.Words_Count_Many);
    private static readonly CompositeFormat DueDaysOne = CompositeFormat.Parse(AppResources.Words_Due_Days_One);
    private static readonly CompositeFormat DueDaysFew = CompositeFormat.Parse(AppResources.Words_Due_Days_Few);
    private static readonly CompositeFormat DueDaysMany = CompositeFormat.Parse(AppResources.Words_Due_Days_Many);
    private static readonly CompositeFormat RowFormat = CompositeFormat.Parse(AppResources.Words_Row_A11y);

    private readonly BrowseWordsUseCase _browse;
    private readonly INavigationService _navigation;
    private readonly IChoiceSheet _sheet;
    private readonly IClock _clock;
    private readonly ILogger<WordsViewModel> _logger;
    private CancellationTokenSource? _searchCts;
    private LibraryStatus? _status;
    private CefrLevel? _level;
    private TopicId? _topic;

    [ObservableProperty]
    private ViewState _state = ViewState.Loading;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _countText = string.Empty;

    [ObservableProperty]
    private string _levelChipText = AppResources.Words_Level_Any;

    [ObservableProperty]
    private string _topicChipText = AppResources.Words_Topic_Any;

    [ObservableProperty]
    private bool _hasLevelFilter;

    [ObservableProperty]
    private bool _hasTopicFilter;

    [ObservableProperty]
    private bool _isSearchEmpty;

    [ObservableProperty]
    private bool _isFilterEmpty;

    public WordsViewModel(BrowseWordsUseCase browse, INavigationService navigation, IChoiceSheet sheet, IClock clock, ILogger<WordsViewModel> logger)
    {
        _browse = browse ?? throw new ArgumentNullException(nameof(browse));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        StatusChips.Add(new ChipOption("all", AppResources.Words_Filter_All) { IsSelected = true });
        StatusChips.Add(new ChipOption(nameof(LibraryStatus.New), AppResources.Words_Status_New, LibraryStatus.New));
        StatusChips.Add(new ChipOption(nameof(LibraryStatus.Introduced), AppResources.Words_Status_Introduced, LibraryStatus.Introduced));
        StatusChips.Add(new ChipOption(nameof(LibraryStatus.Learning), AppResources.Words_Status_Learning, LibraryStatus.Learning));
        StatusChips.Add(new ChipOption(nameof(LibraryStatus.Review), AppResources.Words_Status_Review, LibraryStatus.Review));
        StatusChips.Add(new ChipOption(nameof(LibraryStatus.Learned), AppResources.Words_Status_Learned, LibraryStatus.Learned));
    }

    public ObservableCollection<ChipOption> StatusChips { get; } = [];

    /// <summary>Replaced as a whole on every refresh: ~10k rows added one by one would raise 10k collection-changed events and freeze the list for seconds.</summary>
    [ObservableProperty]
    private IReadOnlyList<WordRow> _words = [];

    [RelayCommand(AllowConcurrentExecutions = false)]
    public Task LoadAsync(CancellationToken cancellationToken) => RefreshAsync(cancellationToken);

    [RelayCommand]
    private Task SelectStatusAsync(ChipOption option)
    {
        ArgumentNullException.ThrowIfNull(option);
        foreach (var chip in StatusChips)
        {
            chip.IsSelected = ReferenceEquals(chip, option);
        }

        _status = option.Value is LibraryStatus status ? status : null;
        return RefreshAsync(CancellationToken.None);
    }

    [RelayCommand]
    private async Task PickLevelAsync(CancellationToken cancellationToken)
    {
        var options = new List<string> { AppResources.Words_Level_Any };
        options.AddRange(CefrLevelExtensions.All.Select(l => l.ToString()));
        var picked = await _sheet.ShowAsync(AppResources.Words_Pick_Level_Title, AppResources.Common_Cancel, options, cancellationToken).ConfigureAwait(true);
        if (picked is null)
        {
            return;
        }

        _level = Enum.TryParse<CefrLevel>(picked, out var level) ? level : null;
        HasLevelFilter = _level is not null;
        LevelChipText = _level?.ToString() ?? AppResources.Words_Level_Any;
        await RefreshAsync(cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task PickTopicAsync(CancellationToken cancellationToken)
    {
        var options = new List<string> { AppResources.Words_Topic_Any };
        options.AddRange(TopicCatalog.Slugs.Select(TopicCatalog.Label));
        var picked = await _sheet.ShowAsync(AppResources.Words_Pick_Topic_Title, AppResources.Common_Cancel, options, cancellationToken).ConfigureAwait(true);
        if (picked is null)
        {
            return;
        }

        var slug = TopicCatalog.Slugs.FirstOrDefault(s => string.Equals(TopicCatalog.Label(s), picked, StringComparison.Ordinal));
        _topic = slug is null ? null : TopicId.From(slug);
        HasTopicFilter = _topic is not null;
        TopicChipText = slug is null ? AppResources.Words_Topic_Any : TopicCatalog.Label(slug);
        await RefreshAsync(cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private Task OpenAsync(WordRow? row)
        => row is null
            ? Task.CompletedTask
            : _navigation.GoToAsync(Routes.Words.Word, new Dictionary<string, object>(StringComparer.Ordinal) { ["id"] = row.LexemeId.ToString("D") });

    [RelayCommand]
    private void ClearSearch() => SearchText = string.Empty;

    partial void OnSearchTextChanged(string value)
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        var cts = _searchCts = new CancellationTokenSource();
        _ = DebouncedRefreshAsync(cts.Token);
    }

    private async Task DebouncedRefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(SearchDebounceMilliseconds, cancellationToken).ConfigureAwait(true);
            await RefreshAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // A newer keystroke superseded this refresh.
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (Words.Count == 0)
        {
            State = ViewState.Loading;
        }

        try
        {
            var query = new WordsQuery(SearchText, _status, _level, _topic);
            var words = await _browse.ExecuteAsync(query, cancellationToken).ConfigureAwait(true);
            var now = _clock.UtcNow;
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, _clock.LocalTimeZone).DateTime);

            var rows = new List<WordRow>(words.Count);
            foreach (var word in words)
            {
                rows.Add(ToRow(word, now, today));
            }

            Words = rows;
            CountText = Plural.Uk(words.Count, CountOne, CountFew, CountMany);
            IsSearchEmpty = words.Count == 0 && !string.IsNullOrWhiteSpace(SearchText);
            IsFilterEmpty = words.Count == 0 && !IsSearchEmpty;
            State = words.Count > 0 ? ViewState.Ready : ViewState.Empty;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogLoadFailed(ex);
            State = ViewState.Error;
        }
    }

    private WordRow ToRow(LibraryWord word, DateTimeOffset now, DateOnly today)
    {
        var status = StatusLabel(word.Status);
        var due = DueText(word, now, today);
        var lemma = word.Entry.Lexeme.Lemma;
        var translation = word.Entry.Translation;
        return new WordRow(
            word.Entry.Id.Value,
            lemma,
            translation,
            status,
            word.Status.ToString(),
            due,
            word.Entry.Lexeme.Level.ToString(),
            string.Format(Ukrainian, RowFormat, lemma, translation, status, due));
    }

    private string DueText(LibraryWord word, DateTimeOffset now, DateOnly today)
    {
        if (word.Due is not { } due || word.Status is LibraryStatus.New or LibraryStatus.Learned or LibraryStatus.Suspended)
        {
            return string.Empty;
        }

        if (due <= now)
        {
            return AppResources.Words_Due_Now;
        }

        var dueDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(due, _clock.LocalTimeZone).DateTime);
        var days = dueDay.DayNumber - today.DayNumber;
        return days switch
        {
            <= 0 => AppResources.Words_Due_Today,
            1 => AppResources.Words_Due_Tomorrow,
            _ => Plural.Uk(days, DueDaysOne, DueDaysFew, DueDaysMany),
        };
    }

    private static string StatusLabel(LibraryStatus status) => status switch
    {
        LibraryStatus.Introduced => AppResources.Words_Status_Introduced,
        LibraryStatus.Learning => AppResources.Words_Status_Learning,
        LibraryStatus.Review => AppResources.Words_Status_Review,
        LibraryStatus.Learned => AppResources.Words_Status_Learned,
        LibraryStatus.Suspended => AppResources.Words_Status_Suspended,
        _ => AppResources.Words_Status_New,
    };

    [LoggerMessage(Level = LogLevel.Error, Message = "Words library failed to load")]
    private partial void LogLoadFailed(Exception exception);
}
