using Burdock.Application.Ports;
using Burdock.Domain.Exposure;
using Burdock.Domain.Learning;
using Burdock.Domain.Rewards;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;

namespace Burdock.TestKit.Fakes;

public sealed class InMemoryContentRepository : IContentRepository
{
    private readonly Dictionary<LexemeId, WordEntry> _entries = [];
    private readonly Dictionary<string, int> _packs = new(StringComparer.Ordinal);

    /// <summary>How many candidate scans ran; the day plan must scan once per day, not on every open.</summary>
    public int ListCalls { get; private set; }

    public Task<WordEntry?> GetAsync(LexemeId id, CancellationToken cancellationToken) => Task.FromResult(_entries.GetValueOrDefault(id));

    public Task<IReadOnlyList<WordEntry>> ListByIdsAsync(IReadOnlyCollection<LexemeId> ids, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<WordEntry>>(ids.Select(id => _entries.GetValueOrDefault(id)).Where(e => e is not null).Select(e => e!).ToArray());

    public Task<IReadOnlyList<WordEntry>> ListAsync(LanguagePair pair, IReadOnlyCollection<CefrLevel> levels, IReadOnlyCollection<TopicId> topics, CancellationToken cancellationToken)
    {
        ListCalls++;
        IReadOnlyList<WordEntry> list = _entries.Values
            .Where(e => e.Lexeme.Pair == pair && levels.Contains(e.Lexeme.Level) && (topics.Count == 0 || topics.Any(e.Lexeme.BelongsTo)))
            .OrderBy(e => e.Lexeme.Lemma, StringComparer.Ordinal)
            .ToArray();
        return Task.FromResult(list);
    }

    public Task<int> CountAsync(LanguagePair pair, CancellationToken cancellationToken) => Task.FromResult(_entries.Values.Count(e => e.Lexeme.Pair == pair));

    public Task<int?> GetImportedPackVersionAsync(string packId, CancellationToken cancellationToken)
        => Task.FromResult(_packs.TryGetValue(packId, out var v) ? v : (int?)null);

    public Task<IReadOnlyDictionary<string, int>> GetImportedPackVersionsAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyDictionary<string, int>>(new Dictionary<string, int>(_packs, StringComparer.Ordinal));

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
    /// <summary>How many pack bodies were requested; the bootstrap must not read packs that are already current.</summary>
    public int LoadCalls { get; private set; }

    public Task<IReadOnlyList<BundledPackDescriptor>> ListBundledPacksAsync(LanguagePair pair, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<BundledPackDescriptor>>(packs.Where(p => p.Pair == pair).Select(p => new BundledPackDescriptor(p.PackId, p.Pair, p.Version, p.PackId)).ToArray());

    public Task<ImportedContentPack?> LoadBundledPackAsync(BundledPackDescriptor descriptor, CancellationToken cancellationToken)
    {
        LoadCalls++;
        return Task.FromResult(packs.FirstOrDefault(p => p.PackId == descriptor.PackId));
    }
}

public sealed class InMemoryDailyPlanCache : IDailyPlanCache
{
    private readonly Dictionary<LearnerId, DailyPlanSnapshot> _snapshots = [];

    public Task<DailyPlanSnapshot?> GetAsync(LearnerId learnerId, CancellationToken cancellationToken) => Task.FromResult(_snapshots.GetValueOrDefault(learnerId));

    public Task SaveAsync(DailyPlanSnapshot snapshot, CancellationToken cancellationToken)
    {
        _snapshots[snapshot.LearnerId] = snapshot;
        return Task.CompletedTask;
    }
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
    public Task<int> CountAsync(LearnerId learnerId, ExerciseMode? mode, bool onlyCorrect, CancellationToken cancellationToken)
        => Task.FromResult(Entries.Count(e => e.LearnerId == learnerId && (mode is null || e.Mode == mode) && (!onlyCorrect || e.Rating != Rating.Again)));

    public List<ReviewLogEntry> Entries { get; } = [];

    public Task AppendAsync(ReviewLogEntry entry, CancellationToken cancellationToken)
    {
        Entries.Add(entry);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Entries.Any(e => e.Id == id));

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

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Records.Any(r => r.Id == id));

    public Task<ExposureCounts> CountAsync(LearnerId learnerId, LexemeId lexemeId, CancellationToken cancellationToken)
    {
        var mine = Records.Where(r => r.LearnerId == learnerId && r.LexemeId == lexemeId).ToArray();
        return Task.FromResult(new ExposureCounts(mine.Count(r => r.Kind == ExposureKind.Scheduled), mine.Count(r => r.Kind == ExposureKind.Confirmed)));
    }
}

public sealed class FakeLearnerContext(LearnerId learnerId, LearnerSettings settings) : ILearnerContext
{
    public LearnerSettings Settings { get; private set; } = settings;

    public LearnerProfile? Profile { get; private set; }

    public Task<LearnerId> GetLearnerIdAsync(CancellationToken cancellationToken) => Task.FromResult(learnerId);

    public Task<LearnerSettings> GetSettingsAsync(CancellationToken cancellationToken) => Task.FromResult(Settings);

    public Task SaveSettingsAsync(LearnerSettings settings, CancellationToken cancellationToken)
    {
        Settings = settings;
        return Task.CompletedTask;
    }

    public Task<LearnerProfile?> GetProfileAsync(CancellationToken cancellationToken) => Task.FromResult(Profile);

    public Task SaveProfileAsync(LearnerProfile profile, CancellationToken cancellationToken)
    {
        Profile = profile;
        return Task.CompletedTask;
    }
}

public sealed class FakeOnboardingState : IOnboardingState
{
    public bool Completed { get; private set; }

    public Task<bool> IsCompletedAsync(CancellationToken cancellationToken) => Task.FromResult(Completed);

    public Task MarkCompletedAsync(CancellationToken cancellationToken)
    {
        Completed = true;
        return Task.CompletedTask;
    }
}

public sealed class FakePrivacyConsent : IPrivacyConsent
{
    public bool Analytics { get; private set; }

    public Task<bool> GetAnalyticsConsentAsync(CancellationToken cancellationToken) => Task.FromResult(Analytics);

    public Task SetAnalyticsConsentAsync(bool granted, CancellationToken cancellationToken)
    {
        Analytics = granted;
        return Task.CompletedTask;
    }
}

/// <summary>Returns the given values in order (a value beyond the list repeats the last one).</summary>
public sealed class FakeRandom(params int[] values) : IRandom
{
    private int _index;

    public int Next(int minInclusive, int maxExclusive)
    {
        var value = values[Math.Min(_index, values.Length - 1)];
        _index++;
        return Math.Clamp(value, minInclusive, maxExclusive - 1);
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

public sealed class InMemoryRewardsStore : IRewardsStore
{
    public RewardsState? State { get; private set; }

    public List<LedgerEvent> Events { get; } = [];

    public Task<RewardsState?> GetAsync(LearnerId learnerId, CancellationToken cancellationToken) => Task.FromResult(State);

    public Task SaveAsync(LearnerId learnerId, RewardsState state, IReadOnlyList<LedgerEvent> newEvents, CancellationToken cancellationToken)
    {
        State = state;
        Events.AddRange(newEvents);
        return Task.CompletedTask;
    }
}
