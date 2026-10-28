using Burdock.Application.Ports;
using Burdock.Application.UseCases.LearningDays;
using Burdock.Application.UseCases.Planning;
using Burdock.Domain.Learning;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;

namespace Burdock.Application.UseCases.Sessions;

/// <summary>
/// Composes a mixed session from today's plan (spec 06 §3.7): due and learning cards first, then new words;
/// recognition before recall for a word; four-option choices with same-level distractors; a matching warm-up when
/// five or more words are available. Deterministic for (learner, day) so a restarted session looks the same.
/// </summary>
public sealed class BuildSessionUseCase(GetDailyPlanUseCase dailyPlan, ILearnerContext learner, IContentRepository content, ILearnerCardRepository cards, LearningDayResolver learningDays)
{
    /// <summary>learning.session.defaultSize</summary>
    public const int DefaultSize = 10;

    /// <summary>learning.session.optionsPerChoice</summary>
    public const int OptionsPerChoice = 4;

    /// <summary>learning.session.minDistractorPool: below this many candidates the pool widens to all topics, then to all levels.</summary>
    public const int MinDistractorPool = 40;

    public Task<SessionPlan> ExecuteAsync(int size, CancellationToken cancellationToken) => ExecuteAsync(size, 0, cancellationToken);

    /// <summary>A session that may dip into <paramref name="extraNewWords"/> beyond today's pace («Ще 5 карток», spec 03 S-10/S-12d).</summary>
    public async Task<SessionPlan> ExecuteAsync(int size, int extraNewWords, CancellationToken cancellationToken)
    {
        var plan = await dailyPlan.ExecuteAsync(extraNewWords, cancellationToken).ConfigureAwait(false);
        var learnerId = await learner.GetLearnerIdAsync(cancellationToken).ConfigureAwait(false);
        var settings = await learner.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        var learnerCards = await cards.ListAsync(learnerId, cancellationToken).ConfigureAwait(false);
        // Distractors come from the learner's levels and topics (spec 06 §3.6 prefers the same level and semantic field); the
        // whole pair is read only when that pool is too small to offer three alternatives (issue #4: 9.8k rows per session).
        var levels = settings.Level.WithNeighbours();
        var pool = await content.ListAsync(settings.Pair, levels, settings.Topics, cancellationToken).ConfigureAwait(false);
        if (pool.Count < MinDistractorPool)
        {
            pool = await content.ListAsync(settings.Pair, levels, [], cancellationToken).ConfigureAwait(false);
        }

        if (pool.Count < MinDistractorPool)
        {
            pool = await content.ListAsync(settings.Pair, CefrLevelExtensions.All, [], cancellationToken).ConfigureAwait(false);
        }

        var chosen = plan.Words.Take(Math.Max(size, LearningPolicy.MinGradedCardsForLearningDay)).ToList();
        if (chosen.Count == 0)
        {
            return new SessionPlan([]);
        }

        var seed = unchecked((int)DailyPlanner.SeededOrder(learnerId, learningDays.Today(), chosen[0].Entry.Id));
        var random = new Random(seed);
        var items = new List<SessionItem>();

        if (chosen.Count >= 5)
        {
            items.Add(new MatchingItem(chosen.Take(5).Select(w => w.Entry).ToArray()));
        }

        foreach (var word in chosen)
        {
            var mode = PickMode(word, learnerCards);
            items.Add(mode == ExerciseMode.TypedRecall
                ? new TypedItem(word.Entry, word.Entry.Translation, word.Entry.AcceptedForms)
                : BuildChoice(word.Entry, mode, pool, random));
        }

        return new SessionPlan(items);
    }

    private static ExerciseMode PickMode(PlannedWord word, IReadOnlyDictionary<LexemeId, LearnerCard> learnerCards)
    {
        if (!learnerCards.TryGetValue(word.Entry.Id, out var card) || card.Reps == 0)
        {
            return ExerciseMode.ChoiceTargetToNative;
        }

        if (card.Memory is { Stability: >= 3 } && card.Reps >= 2)
        {
            return ExerciseMode.TypedRecall;
        }

        return card.Reps % 2 == 0 ? ExerciseMode.ChoiceTargetToNative : ExerciseMode.ChoiceNativeToTarget;
    }

    private static ChoiceItem BuildChoice(WordEntry entry, ExerciseMode mode, IReadOnlyList<WordEntry> pool, Random random)
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
