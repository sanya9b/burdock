using Burdock.Application.Ports;
using Burdock.Domain.Exposure;
using Burdock.Domain.Learning;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;

namespace Burdock.Application.Tests.Fakes;

public sealed class InMemoryContentRepository : IContentRepository
{
    private readonly Dictionary<LexemeId, WordEntry> _entries = [];
    private readonly Dictionary<string, int> _packs = new(StringComparer.Ordinal);

    public Task<WordEntry?> GetAsync(LexemeId id, CancellationToken cancellationToken) => Task.FromResult(_entries.GetValueOrDefault(id));

    public Task<IReadOnlyList<WordEntry>> ListAsync(LanguagePair pair, IReadOnlyCollection<CefrLevel> levels, IReadOnlyCollection<TopicId> topics, CancellationToken cancellationToken)
    {
        IReadOnlyList<WordEntry> list = _entries.Values
            .Where(e => e.Lexeme.Pair == pair && levels.Contains(e.Lexeme.Level) && (topics.Count == 0 || topics.Any(e.Lexeme.BelongsTo)))
            .OrderBy(e => e.Lexeme.Lemma, StringComparer.Ordinal)
            .ToArray();
        return Task.FromResult(list);
    }

    public Task<int> CountAsync(LanguagePair pair, CancellationToken cancellationToken) => Task.FromResult(_entries.Values.Count(e => e.Lexeme.Pair == pair));

    public Task<int?> GetImportedPackVersionAsync(string packId, CancellationToken cancellationToken)
        => Task.FromResult(_packs.TryGetValue(packId, out var v) ? v : (int?)null);

    public Task ImportPackAsync(ImportedContentPack pack, CancellationToken cancellationToken)
    {
        foreach (var e in pack.Entries)
        {
            _entries[e.Id] = e;
        }

        _packs[pack.PackId] = pack.Version;
        return Task.CompletedTask;
    }
}

public sealed class InMemoryPackSource(params ImportedContentPack[] packs) : IContentPackSource
{
    public Task<IReadOnlyList<ImportedContentPack>> LoadBundledPacksAsync(LanguagePair pair, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<ImportedContentPack>>(packs.Where(p => p.Pair == pair).ToArray());
}

public sealed class InMemoryCardRepository : ILearnerCardRepository
{
    private readonly Dictionary<(LearnerId, LexemeId), LearnerCard> _cards = [];

    public Task<LearnerCard?> GetByLexemeAsync(LearnerId learnerId, LexemeId lexemeId, CancellationToken cancellationToken)
        => Task.FromResult(_cards.GetValueOrDefault((learnerId, lexemeId)));

    public Task<IReadOnlyDictionary<LexemeId, LearnerCard>> ListAsync(LearnerId learnerId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyDictionary<LexemeId, LearnerCard>>(_cards.Where(kv => kv.Key.Item1 == learnerId).ToDictionary(kv => kv.Key.Item2, kv => kv.Value));

    public Task UpsertAsync(LearnerCard card, CancellationToken cancellationToken)
    {
        _cards[(card.LearnerId, card.LexemeId)] = card;
        return Task.CompletedTask;
    }
}

public sealed class InMemoryReviewLog : IReviewLogStore
{
    public List<ReviewLogEntry> Entries { get; } = [];

    public Task AppendAsync(ReviewLogEntry entry, CancellationToken cancellationToken)
    {
        Entries.Add(entry);
        return Task.CompletedTask;
    }

    public Task<int> CountGradedOnDayAsync(LearnerId learnerId, DateTimeOffset dayStartUtc, DateTimeOffset dayEndUtc, CancellationToken cancellationToken)
        => Task.FromResult(Entries.Count(e => e.LearnerId == learnerId && e.ReviewedAt >= dayStartUtc && e.ReviewedAt < dayEndUtc));
}

public sealed class InMemoryExposureStore : IExposureStore
{
    public List<ExposureRecord> Records { get; } = [];

    public Task AppendAsync(ExposureRecord record, CancellationToken cancellationToken)
    {
        Records.Add(record);
        return Task.CompletedTask;
    }

    public Task<ExposureCounts> CountAsync(LearnerId learnerId, LexemeId lexemeId, CancellationToken cancellationToken)
    {
        var mine = Records.Where(r => r.LearnerId == learnerId && r.LexemeId == lexemeId).ToArray();
        return Task.FromResult(new ExposureCounts(mine.Count(r => r.Kind == ExposureKind.Scheduled), mine.Count(r => r.Kind == ExposureKind.Confirmed)));
    }
}

public sealed class FakeLearnerContext(LearnerId learnerId, LearnerSettings settings) : ILearnerContext
{
    public LearnerSettings Settings { get; private set; } = settings;

    public Task<LearnerId> GetLearnerIdAsync(CancellationToken cancellationToken) => Task.FromResult(learnerId);

    public Task<LearnerSettings> GetSettingsAsync(CancellationToken cancellationToken) => Task.FromResult(Settings);

    public Task SaveSettingsAsync(LearnerSettings settings, CancellationToken cancellationToken)
    {
        Settings = settings;
        return Task.CompletedTask;
    }
}

public sealed class SequentialIdGenerator : IIdGenerator
{
    private int _next;

    public Guid NewId() => Guid.Parse($"0199a4c0-0000-7000-8000-{++_next:d12}");
}

/// <summary>Deterministic scheduler shaped like FSRS: the first pass goes through a 10-minute learning step, then Good doubles stability, Again resets it.</summary>
public sealed class FakeScheduler : IReviewScheduler
{
    public SchedulingOutcome Review(SchedulingInput input, Rating rating, DateTimeOffset reviewedAt)
    {
        if (input.Memory is null && rating != Rating.Easy)
        {
            return new SchedulingOutcome(rating == Rating.Again ? CardState.Learning : CardState.Learning, MemoryState.Create(1, 5), 0, reviewedAt.AddMinutes(10));
        }

        var stability = rating switch
        {
            Rating.Again => 0.5,
            Rating.Hard => Math.Max(1, (input.Memory?.Stability ?? 1) * 1.2),
            Rating.Good => Math.Max(1, (input.Memory?.Stability ?? 1) * 2),
            _ => Math.Max(2, (input.Memory?.Stability ?? 1) * 3),
        };
        var state = rating == Rating.Again ? (input.State == CardState.Review ? CardState.Relearning : CardState.Learning) : stability >= 1 ? CardState.Review : CardState.Learning;
        return new SchedulingOutcome(state, MemoryState.Create(stability, input.Memory?.Difficulty ?? 5), null, reviewedAt.AddDays(stability));
    }

    public double Retrievability(SchedulingInput input, DateTimeOffset at) => input.Memory is { } m ? Math.Max(0, 1 - 0.1 * (at - (input.LastReview ?? at)).TotalDays / m.Stability) : 0;
}
