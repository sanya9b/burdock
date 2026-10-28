using Burdock.Domain.SharedKernel;

namespace Burdock.Domain.Learning;

/// <summary>Outcome of one recorded answer in a <see cref="MasteryDrill"/>.</summary>
/// <param name="Streak">Correct answers in a row for the word after this answer.</param>
/// <param name="Mastered">The word reached the target with this answer.</param>
/// <param name="Reset">A wrong answer sent the word's counter back to zero.</param>
public readonly record struct MasteryStep(int Streak, bool Mastered, bool Reset);

/// <summary>
/// Session-level mastery rule (owner decision of 2026-10-28, spec 06 §3.7): every word of a session is asked again until it
/// collects <see cref="LearningPolicy.MasteryStreak"/> correct answers in a row within that session; a wrong answer resets the
/// word's counter; reaching the target marks the word learned. FSRS keeps scheduling the reviews underneath; the drill only
/// decides what the session asks next.
/// </summary>
public sealed class MasteryDrill
{
    private readonly Dictionary<LexemeId, int> _streaks = [];
    private readonly Queue<LexemeId> _pending = new();

    public MasteryDrill(IEnumerable<LexemeId> words, int target = LearningPolicy.MasteryStreak)
    {
        ArgumentNullException.ThrowIfNull(words);
        ArgumentOutOfRangeException.ThrowIfLessThan(target, 1);
        Target = target;
        foreach (var word in words)
        {
            _streaks.TryAdd(word, 0);
        }
    }

    public int Target { get; }

    public IReadOnlyCollection<LexemeId> Words => _streaks.Keys;

    public int Streak(LexemeId word) => _streaks.GetValueOrDefault(word);

    public bool IsMastered(LexemeId word) => Streak(word) >= Target;

    public bool Completed => _streaks.Values.All(s => s >= Target);

    /// <summary>Words waiting to be asked again (answered at least once, not mastered yet), in order.</summary>
    public int PendingCount => _pending.Count(w => !IsMastered(w));

    /// <summary>Correct answers still needed across all words, assuming no further mistakes.</summary>
    public int AnswersRemaining => _streaks.Values.Sum(s => Math.Max(0, Target - s));

    /// <summary>Records an answer: a correct one advances the streak, a wrong one resets it; an unmastered word goes to the back of the queue.</summary>
    public MasteryStep Record(LexemeId word, bool correct)
    {
        var before = _streaks.GetValueOrDefault(word);
        var after = correct ? before + 1 : 0;
        _streaks[word] = after;
        var mastered = correct && after >= Target && before < Target;
        if (after < Target)
        {
            _pending.Enqueue(word);
        }

        return new MasteryStep(after, mastered, !correct && before > 0);
    }

    /// <summary>The next word to ask again, or null when nothing is waiting; words mastered since they were queued are skipped.</summary>
    public LexemeId? Dequeue()
    {
        while (_pending.TryDequeue(out var word))
        {
            if (!IsMastered(word))
            {
                return word;
            }
        }

        return null;
    }

    /// <summary>
    /// Exercise for the word's next answer: recognition first, production last (spec 06 §3.6 upper bounds): 0 → word→meaning,
    /// 1 → meaning→word, 2 → word→meaning, 3+ → typed recall when the entry has accepted forms, otherwise meaning→word.
    /// </summary>
    public ExerciseMode ModeFor(LexemeId word, bool typedAvailable)
        => Streak(word) switch
        {
            0 => ExerciseMode.ChoiceTargetToNative,
            1 => ExerciseMode.ChoiceNativeToTarget,
            2 => ExerciseMode.ChoiceTargetToNative,
            _ => typedAvailable ? ExerciseMode.TypedRecall : ExerciseMode.ChoiceNativeToTarget,
        };
}
