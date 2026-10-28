using System.Globalization;
using System.Text;
using Burdock.Application.UseCases.Words;
using Burdock.Domain.Learning;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;
using Burdock.Presentation.Navigation;
using Burdock.Presentation.Resources.Strings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Burdock.Presentation.ViewModels;

/// <summary>S-11 «Картка слова»: word, IPA, hidden translation, highlighted example, exposure counters, «Знаю» / «Вчити» (spec 03).</summary>
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
    private Domain.Exposure.ExposureSurface _surface = Domain.Exposure.ExposureSurface.InApp;
    private string _exampleText = string.Empty;

    [ObservableProperty]
    private ViewState _state = ViewState.Loading;

    [ObservableProperty]
    private string _lemma = string.Empty;

    [ObservableProperty]
    private string _ipa = string.Empty;

    [ObservableProperty]
    private string _partOfSpeech = string.Empty;

    [ObservableProperty]
    private string _topicKey = string.Empty;

    [ObservableProperty]
    private string _translation = string.Empty;

    [ObservableProperty]
    private string _alsoMeans = string.Empty;

    [ObservableProperty]
    private string _exampleBefore = string.Empty;

    [ObservableProperty]
    private string _exampleMatch = string.Empty;

    [ObservableProperty]
    private string _exampleAfter = string.Empty;

    [ObservableProperty]
    private string _exampleNative = string.Empty;

    [ObservableProperty]
    private bool _translationRevealed;

    [ObservableProperty]
    private string _stateLabel = string.Empty;

    [ObservableProperty]
    private int _scheduledExposures;

    [ObservableProperty]
    private int _confirmedExposures;

    [ObservableProperty]
    private string _retrievabilityPercent = string.Empty;

    [ObservableProperty]
    private bool _hasRetrievability;

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
            // Deep links from widgets and notifications name their surface (spec 04 §3.4); in-app navigation leaves it out.
            _surface = query.TryGetValue("surface", out var surface) && Enum.TryParse<Domain.Exposure.ExposureSurface>(surface?.ToString(), ignoreCase: true, out var parsed)
                ? parsed
                : Domain.Exposure.ExposureSurface.InApp;
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
            var result = await _openWord.ExecuteAsync(id, _surface, cancellationToken).ConfigureAwait(true);
            if (result.IsFailure)
            {
                State = ViewState.Error;
                return;
            }

            var view = result.Value;
            var entry = view.Entry;
            Lemma = entry.Lexeme.Lemma;
            Ipa = Localization.Pronunciation.PreferredIpa(entry, _speech.Accent);
            PartOfSpeech = PartOfSpeechLabel(entry.Lexeme.PartOfSpeech);
            TopicKey = entry.Lexeme.Topics.Count > 0 ? entry.Lexeme.Topics[0].Slug : string.Empty;
            Translation = entry.Translation;
            AlsoMeans = string.Join(", ", entry.AlternativeTranslations);
            HasAlsoMeans = AlsoMeans.Length > 0;
            var example = entry.ExampleTarget ?? string.Empty;
            _exampleText = example;
            (ExampleBefore, ExampleMatch, ExampleAfter) = Highlight(example, entry.Lexeme.Lemma, entry.AcceptedForms);
            ExampleNative = entry.ExampleNative ?? string.Empty;
            HasExample = example.Length > 0;
            StateLabel = StateText(view.State);
            ScheduledExposures = view.ScheduledExposures;
            ConfirmedExposures = view.ConfirmedExposures;
            ExposuresText = string.Format(Ukrainian, ExposuresFormat, view.ScheduledExposures, view.ConfirmedExposures);
            HasRetrievability = view.State is not (CardState.New or CardState.Introduced);
            var percent = (int)Math.Round(view.Retrievability * 100);
            RetrievabilityPercent = HasRetrievability ? string.Create(Ukrainian, $"{percent}%") : string.Empty;
            RetrievabilityText = HasRetrievability ? string.Format(Ukrainian, RetrievabilityFormat, percent) : string.Empty;
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
    private Task SpeakAsync(CancellationToken cancellationToken)
        => _lexemeId is { } id ? _speech.SpeakWordAsync(id, Lemma, cancellationToken) : _speech.SpeakEnglishAsync(Lemma, cancellationToken);

    /// <summary>The example sentence read aloud: a word in context is pronounced far more naturally than in isolation.</summary>
    [RelayCommand]
    private Task SpeakExampleAsync(CancellationToken cancellationToken)
        => HasExample ? _speech.SpeakEnglishAsync(_exampleText, cancellationToken) : Task.CompletedTask;

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

    /// <summary>
    /// Splits the example around the lemma or an accepted form (longest first) so the view can set it in bold.
    /// A whole word wins («a» in «I have a cat»), then the start of a word (ticket → tickets, walk → walked), then any substring.
    /// </summary>
    internal static (string Before, string Match, string After) Highlight(string example, string lemma, IReadOnlyList<string> acceptedForms)
    {
        if (string.IsNullOrEmpty(example))
        {
            return (string.Empty, string.Empty, string.Empty);
        }

        var candidates = acceptedForms.Append(lemma).Where(f => !string.IsNullOrWhiteSpace(f)).Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(f => f.Length).ToArray();
        foreach (var mode in new[] { FormMatch.WholeWord, FormMatch.WordStart, FormMatch.Anywhere })
        {
            foreach (var form in candidates)
            {
                var at = FindForm(example, form, mode);
                if (at >= 0)
                {
                    return (example[..at], example.Substring(at, form.Length), example[(at + form.Length)..]);
                }
            }
        }

        return (example, string.Empty, string.Empty);
    }

    private enum FormMatch
    {
        WholeWord,
        WordStart,
        Anywhere,
    }

    private static int FindForm(string text, string form, FormMatch mode)
    {
        var from = 0;
        while (from <= text.Length - form.Length)
        {
            var at = text.IndexOf(form, from, StringComparison.OrdinalIgnoreCase);
            if (at < 0)
            {
                return -1;
            }

            var end = at + form.Length;
            var startsWord = at == 0 || !char.IsLetterOrDigit(text[at - 1]);
            var endsWord = end == text.Length || !char.IsLetterOrDigit(text[end]);
            if (mode == FormMatch.Anywhere || (startsWord && (mode == FormMatch.WordStart || endsWord)))
            {
                return at;
            }

            from = at + 1;
        }

        return -1;
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

/// <summary>
/// English pronunciation (ADR 0019): pack audio in the learner's voice when the content has it, otherwise the best installed
/// system voice of the same accent. One voice per device, used everywhere; the choice persists in Preferences.
/// </summary>
public interface ISpeechService
{
    /// <summary>The learner's explicit choice; null means «the first voice of the catalogue».</summary>
    VoiceId? Voice { get; }

    /// <summary>Accent of the effective voice; drives the IPA shown next to words.</summary>
    PronunciationAccent Accent { get; }

    /// <summary>True where the app can open the system voice settings (Android); elsewhere the settings screen shows a text hint.</summary>
    bool CanOpenSystemVoiceSettings { get; }

    Task<IReadOnlyList<PronunciationVoice>> GetVoicesAsync(CancellationToken cancellationToken);

    void SelectVoice(PronunciationVoice voice);

    /// <summary>The word in the learner's voice from the pack, or the system voice when there is no clip.</summary>
    Task SpeakWordAsync(LexemeId lexemeId, string lemma, CancellationToken cancellationToken);

    /// <summary>Free text (sentences, samples) through the system voice.</summary>
    Task SpeakEnglishAsync(string text, CancellationToken cancellationToken);

    /// <summary>The sample phrase of a voice for the picker (pack clip, else the system voice).</summary>
    Task PlaySampleAsync(VoiceId voice, CancellationToken cancellationToken);

    Task OpenSystemVoiceSettingsAsync();
}
