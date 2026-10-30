using Burdock.Application.Ports;
using Burdock.Domain.Learning;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;

namespace Burdock.Application.UseCases.Words;

/// <summary>Learner-facing status groups of the library filters (spec 03 S-14); they fold the card states into five chips plus «відкладено».</summary>
public enum LibraryStatus
{
    New,
    Introduced,
    Learning,
    Review,
    Learned,
    Suspended,
}

public static class LibraryStatusExtensions
{
    public static LibraryStatus ToLibraryStatus(this CardState state) => state switch
    {
        CardState.Introduced => LibraryStatus.Introduced,
        CardState.Learning or CardState.Relearning => LibraryStatus.Learning,
        CardState.Review => LibraryStatus.Review,
        CardState.Learned or CardState.Known => LibraryStatus.Learned,
        CardState.Suspended => LibraryStatus.Suspended,
        _ => LibraryStatus.New,
    };
}

/// <summary>One row of the library: the entry joined with the learner's card (or its absence); <c>MasteryProgress</c> is the streak of the last drill, 0..<see cref="LearningPolicy.MasteryStreak"/>, full for learned and known words.</summary>
public sealed record LibraryWord(WordEntry Entry, CardState State, LibraryStatus Status, DateTimeOffset? Due, int ConfirmedExposures, int Reps, int MasteryProgress = 0);

/// <summary>Filters of S-14; null means «any». Search matches the lemma, accepted forms and translations after normalisation.</summary>
public sealed record WordsQuery(string? Search = null, LibraryStatus? Status = null, CefrLevel? Level = null, TopicId? Topic = null)
{
    public static WordsQuery All { get; } = new();

    public bool HasFilters => Status is not null || Level is not null || Topic is not null;
}

/// <summary>S-14 «Слова»: every word of the learner's pair with its status, filtered and sorted for the library list.</summary>
public sealed class BrowseWordsUseCase(ILearnerContext learner, IContentRepository content, ILearnerCardRepository cards)
{
    public async Task<IReadOnlyList<LibraryWord>> ExecuteAsync(WordsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var learnerId = await learner.GetLearnerIdAsync(cancellationToken).ConfigureAwait(false);
        var settings = await learner.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        var entries = await content.ListAsync(settings.Pair, CefrLevelExtensions.All, [], cancellationToken).ConfigureAwait(false);
        var learnerCards = await cards.ListAsync(learnerId, cancellationToken).ConfigureAwait(false);

        var words = entries.Select(entry =>
        {
            learnerCards.TryGetValue(entry.Id, out var card);
            var state = card?.State ?? CardState.New;
            var mastery = card is null ? 0 : state is CardState.Learned or CardState.Known ? LearningPolicy.MasteryStreak : card.MasteryProgress;
            return new LibraryWord(entry, state, state.ToLibraryStatus(), card?.Due, card?.ConfirmedExposures ?? 0, card?.Reps ?? 0, mastery);
        });
        return Filter(words, query).ToArray();
    }

    /// <summary>Pure filter + sort: status, level, topic, then search; alphabetical by lemma.</summary>
    public static IEnumerable<LibraryWord> Filter(IEnumerable<LibraryWord> words, WordsQuery query)
    {
        ArgumentNullException.ThrowIfNull(words);
        ArgumentNullException.ThrowIfNull(query);
        var result = words;
        if (query.Status is { } status)
        {
            result = result.Where(w => w.Status == status);
        }

        if (query.Level is { } level)
        {
            result = result.Where(w => w.Entry.Lexeme.Level == level);
        }

        if (query.Topic is { } topic)
        {
            result = result.Where(w => w.Entry.Lexeme.BelongsTo(topic));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var needle = AnswerNormalizer.Normalize(query.Search);
            if (needle.Length > 0)
            {
                result = result.Where(w => Matches(w.Entry, needle));
            }
        }

        return result.OrderBy(w => w.Entry.Lexeme.Lemma, StringComparer.OrdinalIgnoreCase);
    }

    private static bool Matches(WordEntry entry, string needle)
        => Contains(entry.Lexeme.Lemma, needle)
           || Contains(entry.Translation, needle)
           || entry.AlternativeTranslations.Any(t => Contains(t, needle))
           || entry.AcceptedForms.Any(f => Contains(f, needle));

    private static bool Contains(string haystack, string needle)
        => AnswerNormalizer.Normalize(haystack).Contains(needle, StringComparison.Ordinal);
}
