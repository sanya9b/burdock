using Burdock.Application.Ports;
using Burdock.Domain.Learning;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;

namespace Burdock.Application.UseCases.Words;

/// <summary>
/// Learner-facing status groups of the library filters (spec 03 S-14). They follow the knowledge ring, not the scheduler
/// state (owner decision of 2026-11-12, log item 23): «Нові» never graded, «Знайомство» seen but not graded, «Вчу» graded
/// with 0–3 of 4 answers in a row, «Вивчено» 4 of 4 or «Знаю», «Повторюю» a learned word whose review is due now.
/// </summary>
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
    /// <summary>Status of the learner's card (or its absence) at <paramref name="now"/>.</summary>
    public static LibraryStatus ToLibraryStatus(this LearnerCard? card, DateTimeOffset now)
    {
        if (card is null)
        {
            return LibraryStatus.New;
        }

        return card.State switch
        {
            CardState.New => LibraryStatus.New,
            CardState.Introduced => LibraryStatus.Introduced,
            CardState.Suspended => LibraryStatus.Suspended,
            CardState.Known => LibraryStatus.Learned,
            CardState.Learned => card.Due is { } due && due <= now ? LibraryStatus.Review : LibraryStatus.Learned,
            _ => card.MasteryProgress >= LearningPolicy.MasteryStreak ? LibraryStatus.Learned : LibraryStatus.Learning,
        };
    }
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
public sealed class BrowseWordsUseCase(ILearnerContext learner, IContentRepository content, ILearnerCardRepository cards, IClock clock)
{
    public async Task<IReadOnlyList<LibraryWord>> ExecuteAsync(WordsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var learnerId = await learner.GetLearnerIdAsync(cancellationToken).ConfigureAwait(false);
        var settings = await learner.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        var entries = await content.ListAsync(settings.Pair, CefrLevelExtensions.All, [], cancellationToken).ConfigureAwait(false);
        var learnerCards = await cards.ListAsync(learnerId, cancellationToken).ConfigureAwait(false);
        var now = clock.UtcNow;

        var words = entries.Select(entry =>
        {
            learnerCards.TryGetValue(entry.Id, out var card);
            var state = card?.State ?? CardState.New;
            var mastery = card is null ? 0 : state is CardState.Learned or CardState.Known ? LearningPolicy.MasteryStreak : card.MasteryProgress;
            return new LibraryWord(entry, state, card.ToLibraryStatus(now), card?.Due, card?.ConfirmedExposures ?? 0, card?.Reps ?? 0, mastery);
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
