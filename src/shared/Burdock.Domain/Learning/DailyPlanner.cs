using System.Security.Cryptography;
using System.Text;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Time;
using Burdock.Domain.Vocabulary;

namespace Burdock.Domain.Learning;

/// <summary>Why a word is in today's plan.</summary>
public enum PlanReason
{
    /// <summary>Card is due for review.</summary>
    Due,

    /// <summary>Already introduced, still learning.</summary>
    Learning,

    /// <summary>Selected as one of today's new words.</summary>
    New,
}

public sealed record PlannedWord(WordEntry Entry, PlanReason Reason, double Retrievability);

/// <summary>Today's word list in display order (spec 06 §3.5).</summary>
public sealed record DailyPlan(LearningDay Day, IReadOnlyList<PlannedWord> Words)
{
    public int NewCount => Words.Count(w => w.Reason == PlanReason.New);

    public int DueCount => Words.Count(w => w.Reason == PlanReason.Due);
}

/// <summary>
/// Pure, deterministic selection of today's words: due cards first (lowest retrievability first), then cards still in
/// learning, then new words chosen by a seed derived from (learner, day) so every surface agrees within the day.
/// </summary>
public static class DailyPlanner
{
    public static DailyPlan Plan(
        LearnerId learnerId,
        LearningDay day,
        DateTimeOffset now,
        TimeZoneInfo timeZone,
        IReadOnlyList<WordEntry> candidates,
        IReadOnlyDictionary<LexemeId, LearnerCard> cards,
        LearnerSettings settings,
        IReviewScheduler scheduler,
        int? newWordsOverride = null)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(scheduler);

        var due = new List<PlannedWord>();
        var learning = new List<PlannedWord>();
        var fresh = new List<WordEntry>();
        var introducedToday = 0;

        foreach (var entry in candidates)
        {
            if (!cards.TryGetValue(entry.Id, out var card) || card.State == CardState.New)
            {
                fresh.Add(entry);
                continue;
            }

            if (card.State is CardState.Suspended or CardState.Known)
            {
                continue;
            }

            if (card.IntroducedAt is { } introducedAt && LearningDay.From(introducedAt, timeZone) == day)
            {
                introducedToday++;
            }

            var retrievability = card.Memory is null ? 0 : scheduler.Retrievability(card.ToSchedulingInput(), now);
            if (card.Due is { } dueAt && dueAt <= now && card.State != CardState.Introduced)
            {
                due.Add(new PlannedWord(entry, PlanReason.Due, retrievability));
            }
            else if (card.State is CardState.Introduced or CardState.Learning or CardState.Relearning)
            {
                learning.Add(new PlannedWord(entry, PlanReason.Learning, retrievability));
            }
        }

        var newBudget = Math.Max(0, (newWordsOverride ?? settings.NewWordsPerDay) - introducedToday);
        var newWords = fresh
            .OrderBy(e => SeededOrder(learnerId, day, e.Id))
            .Take(newBudget)
            .Select(e => new PlannedWord(e, PlanReason.New, 0))
            .ToList();

        var ordered = due.OrderBy(w => w.Retrievability).ThenBy(w => w.Entry.Lexeme.Lemma, StringComparer.Ordinal)
            .Concat(learning.OrderBy(w => w.Retrievability).ThenBy(w => w.Entry.Lexeme.Lemma, StringComparer.Ordinal))
            .Concat(newWords)
            .ToArray();

        return new DailyPlan(day, ordered);
    }

    /// <summary>Stable pseudo-random key: SHA-256 over learner, day and lexeme (spec 06 §3.5 FreeDailySet seed idea).</summary>
    public static ulong SeededOrder(LearnerId learnerId, LearningDay day, LexemeId lexemeId)
    {
        var input = Encoding.UTF8.GetBytes($"{learnerId}|{day}|{lexemeId}|plan-v1");
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(input, hash);
        return BitConverter.ToUInt64(hash[..8]);
    }
}
