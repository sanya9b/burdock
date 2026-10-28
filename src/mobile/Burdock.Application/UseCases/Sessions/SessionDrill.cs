using Burdock.Domain.Learning;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;

namespace Burdock.Application.UseCases.Sessions;

/// <summary>
/// Drives the mastery rule inside one session (spec 06 §3.7, decision of 2026-10-28): the ViewModel records every graded answer
/// and, once the first pass over the plan is done, keeps dequeuing follow-up items until every word has
/// <see cref="MasteryDrill.Target"/> correct answers in a row.
/// </summary>
public sealed class SessionDrill
{
    private readonly MasteryDrill _drill;
    private readonly IReadOnlyDictionary<LexemeId, WordEntry> _entries;
    private readonly SessionItemFactory _factory;

    public SessionDrill(IReadOnlyList<WordEntry> words, SessionItemFactory factory, int target = LearningPolicy.MasteryStreak)
    {
        ArgumentNullException.ThrowIfNull(words);
        _entries = words.ToDictionary(w => w.Id);
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _drill = new MasteryDrill(_entries.Keys, target);
    }

    public int Target => _drill.Target;

    public bool Completed => _drill.Completed;

    public int PendingCount => _drill.PendingCount;

    public int AnswersRemaining => _drill.AnswersRemaining;

    public int Streak(LexemeId word) => _drill.Streak(word);

    /// <summary>Records a graded answer; ungraded warm-ups are ignored.</summary>
    public MasteryStep? Record(SessionItem item, bool correct)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Mode.IsUngraded() || !_entries.ContainsKey(item.Entry.Id))
        {
            return null;
        }

        return _drill.Record(item.Entry.Id, correct);
    }

    /// <summary>The next follow-up exercise, or null when no word is waiting.</summary>
    public SessionItem? Dequeue()
    {
        var word = _drill.Dequeue();
        if (word is not { } id || !_entries.TryGetValue(id, out var entry))
        {
            return null;
        }

        var mode = _drill.ModeFor(id, typedAvailable: entry.AcceptedForms.Count > 0);
        return _factory.Create(entry, mode);
    }
}

/// <summary>Builds exercises for a word from the session's distractor pool with the session's deterministic random source (spec 06 §3.6.2).</summary>
public sealed class SessionItemFactory(IReadOnlyList<WordEntry> pool, Random random)
{
    /// <summary>learning.session.optionsPerChoice</summary>
    public const int OptionsPerChoice = 4;

    public SessionItem Create(WordEntry entry, ExerciseMode mode)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (mode == ExerciseMode.TypedRecall && entry.AcceptedForms.Count > 0)
        {
            return new TypedItem(entry, entry.Translation, entry.AcceptedForms);
        }

        return BuildChoice(entry, mode == ExerciseMode.TypedRecall ? ExerciseMode.ChoiceNativeToTarget : mode);
    }

    private ChoiceItem BuildChoice(WordEntry entry, ExerciseMode mode)
    {
        var toNative = mode == ExerciseMode.ChoiceTargetToNative;
        string Text(WordEntry e) => toNative ? e.Translation : e.Lexeme.Lemma;
        var correct = Text(entry);
        var distractors = pool
            .Where(e => e.Id != entry.Id && !string.Equals(Text(e), correct, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => DistractorRank(entry, e))
            .ThenBy(_ => random.Next())
            .Select(Text)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(OptionsPerChoice - 1)
            .ToList();

        var options = distractors.Append(correct).OrderBy(_ => random.Next()).ToArray();
        return new ChoiceItem(entry, mode, toNative ? entry.Lexeme.Lemma : entry.Translation, options, Array.IndexOf(options, correct));
    }

    /// <summary>Lower is better: same level and part of speech first, then neighbouring levels (spec 06 §3.6 distractor rules).</summary>
    private static int DistractorRank(WordEntry target, WordEntry candidate)
    {
        var levelDistance = Math.Abs((int)target.Lexeme.Level - (int)candidate.Lexeme.Level);
        var posPenalty = target.Lexeme.PartOfSpeech == candidate.Lexeme.PartOfSpeech ? 0 : 1;
        var topicBonus = target.Lexeme.Topics.Any(candidate.Lexeme.BelongsTo) ? 0 : 1;
        return levelDistance * 4 + posPenalty * 2 + topicBonus;
    }
}
