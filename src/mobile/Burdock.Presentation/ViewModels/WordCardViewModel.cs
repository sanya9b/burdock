using System.Globalization;
using System.Text;
using Burdock.Application.UseCases.Words;
using Burdock.Domain.Learning;
using Burdock.Domain.SharedKernel;
using Burdock.Presentation.Navigation;
using Burdock.Presentation.Resources.Strings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Burdock.Presentation.ViewModels;

/// <summary>S-11 «Картка слова»: word, IPA, hidden translation, example, exposure counters, «Знаю» / «Вчити» (spec 03).</summary>
public sealed partial class WordCardViewModel : ObservableObject, IQueryAttributable
{
    private static readonly CultureInfo Ukrainian = CultureInfo.GetCultureInfo("uk-UA");
    private static readonly CompositeFormat ExposuresFormat = CompositeFormat.Parse(AppResources.Word_Exposures_Label);
    private static readonly CompositeFormat RetrievabilityFormat = CompositeFormat.Parse(AppResources.Word_Retrievability_Label);

    private readonly OpenWordUseCase _openWord;
    private readonly IntroduceWordUseCase _introduce;
    private readonly ClaimKnownUseCase _claimKnown;
    private readonly INavigationService _navigation;
    private readonly ISpeechService _speech;
    private readonly ILogger<WordCardViewModel> _logger;
    private LexemeId? _lexemeId;

    [ObservableProperty]
    private ViewState _state = ViewState.Loading;

    [ObservableProperty]
    private string _lemma = string.Empty;

    [ObservableProperty]
    private string _ipa = string.Empty;

    [ObservableProperty]
    private string _partOfSpeech = string.Empty;

    [ObservableProperty]
    private string _translation = string.Empty;

    [ObservableProperty]
    private string _alsoMeans = string.Empty;

    [ObservableProperty]
    private string _exampleTarget = string.Empty;

    [ObservableProperty]
    private string _exampleNative = string.Empty;

    [ObservableProperty]
    private bool _translationRevealed;

    [ObservableProperty]
    private string _stateLabel = string.Empty;

    [ObservableProperty]
    private string _exposuresText = string.Empty;

    [ObservableProperty]
    private string _retrievabilityText = string.Empty;

    [ObservableProperty]
    private bool _hasExample;

    [ObservableProperty]
    private bool _hasAlsoMeans;

    public WordCardViewModel(OpenWordUseCase openWord, IntroduceWordUseCase introduce, ClaimKnownUseCase claimKnown, INavigationService navigation, ISpeechService speech, ILogger<WordCardViewModel> logger)
    {
        _openWord = openWord ?? throw new ArgumentNullException(nameof(openWord));
        _introduce = introduce ?? throw new ArgumentNullException(nameof(introduce));
        _claimKnown = claimKnown ?? throw new ArgumentNullException(nameof(claimKnown));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _speech = speech ?? throw new ArgumentNullException(nameof(speech));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.TryGetValue("id", out var raw) && Guid.TryParse(raw?.ToString(), out var id))
        {
            _lexemeId = LexemeId.From(id);
            TranslationRevealed = query.TryGetValue("reveal", out var reveal) && string.Equals(reveal?.ToString(), "1", StringComparison.Ordinal);
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (_lexemeId is not { } id)
        {
            State = ViewState.Error;
            return;
        }

        State = ViewState.Loading;
        try
        {
            var result = await _openWord.ExecuteAsync(id, cancellationToken).ConfigureAwait(true);
            if (result.IsFailure)
            {
                State = ViewState.Error;
                return;
            }

            var view = result.Value;
            Lemma = view.Entry.Lexeme.Lemma;
            Ipa = view.Entry.IpaGeneralAmerican ?? view.Entry.IpaReceivedPronunciation ?? string.Empty;
            PartOfSpeech = PartOfSpeechLabel(view.Entry.Lexeme.PartOfSpeech);
            Translation = view.Entry.Translation;
            AlsoMeans = string.Join(", ", view.Entry.AlternativeTranslations);
            HasAlsoMeans = AlsoMeans.Length > 0;
            ExampleTarget = view.Entry.ExampleTarget ?? string.Empty;
            ExampleNative = view.Entry.ExampleNative ?? string.Empty;
            HasExample = ExampleTarget.Length > 0;
            StateLabel = StateText(view.State);
            ExposuresText = string.Format(Ukrainian, ExposuresFormat, view.ScheduledExposures, view.ConfirmedExposures);
            RetrievabilityText = view.State is CardState.New or CardState.Introduced
                ? string.Empty
                : string.Format(Ukrainian, RetrievabilityFormat, (int)Math.Round(view.Retrievability * 100));
            State = ViewState.Ready;
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

    [RelayCommand]
    private void Reveal() => TranslationRevealed = true;

    [RelayCommand]
    private Task SpeakAsync(CancellationToken cancellationToken) => _speech.SpeakEnglishAsync(Lemma, cancellationToken);

    [RelayCommand]
    private async Task KnowAsync(CancellationToken cancellationToken)
    {
        if (_lexemeId is { } id)
        {
            await _claimKnown.ExecuteAsync(id, cancellationToken).ConfigureAwait(true);
        }

        await _navigation.GoBackAsync(cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task LearnAsync(CancellationToken cancellationToken)
    {
        if (_lexemeId is { } id)
        {
            await _introduce.ExecuteAsync(id, cancellationToken).ConfigureAwait(true);
        }

        await _navigation.GoBackAsync(cancellationToken).ConfigureAwait(true);
    }

    internal static string StateText(CardState state) => state switch
    {
        CardState.Introduced => AppResources.Word_State_Introduced,
        CardState.Learning => AppResources.Word_State_Learning,
        CardState.Review => AppResources.Word_State_Review,
        CardState.Relearning => AppResources.Word_State_Relearning,
        CardState.Learned => AppResources.Word_State_Learned,
        CardState.Known => AppResources.Word_State_Known,
        CardState.Suspended => AppResources.Word_State_Suspended,
        _ => AppResources.Word_State_New,
    };

    private static string PartOfSpeechLabel(Domain.Vocabulary.PartOfSpeech pos) => pos switch
    {
        Domain.Vocabulary.PartOfSpeech.Noun => "іменник",
        Domain.Vocabulary.PartOfSpeech.Verb => "дієслово",
        Domain.Vocabulary.PartOfSpeech.Adjective => "прикметник",
        Domain.Vocabulary.PartOfSpeech.Adverb => "прислівник",
        Domain.Vocabulary.PartOfSpeech.Pronoun => "займенник",
        Domain.Vocabulary.PartOfSpeech.Preposition => "прийменник",
        Domain.Vocabulary.PartOfSpeech.Conjunction => "сполучник",
        Domain.Vocabulary.PartOfSpeech.Determiner => "означник",
        Domain.Vocabulary.PartOfSpeech.Interjection => "вигук",
        Domain.Vocabulary.PartOfSpeech.Numeral => "числівник",
        Domain.Vocabulary.PartOfSpeech.Phrase => "вислів",
        _ => string.Empty,
    };

    [LoggerMessage(Level = LogLevel.Error, Message = "Word card failed to load")]
    private partial void LogLoadFailed(Exception exception);
}

/// <summary>System text-to-speech (T7: on-device only, English voice).</summary>
public interface ISpeechService
{
    Task SpeakEnglishAsync(string text, CancellationToken cancellationToken);
}
