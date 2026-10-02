using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;

namespace Burdock.Application.Ports;

/// <summary>Locally cached content (imported packs), read by planner, WordCard and exercises (spec 08 §3.10, spec 01 §3.4).</summary>
public interface IContentRepository
{
    Task<WordEntry?> GetAsync(LexemeId id, CancellationToken cancellationToken);

    /// <summary>Entries of a pair filtered by levels and topics; an empty topic list means all topics.</summary>
    Task<IReadOnlyList<WordEntry>> ListAsync(LanguagePair pair, IReadOnlyCollection<CefrLevel> levels, IReadOnlyCollection<TopicId> topics, CancellationToken cancellationToken);

    /// <summary>Entries for known ids (today's plan from its snapshot, the learner's cards); missing ids are skipped.</summary>
    Task<IReadOnlyList<WordEntry>> ListByIdsAsync(IReadOnlyCollection<LexemeId> ids, CancellationToken cancellationToken);

    Task<int> CountAsync(LanguagePair pair, CancellationToken cancellationToken);

    /// <summary>Version of an imported pack, or null when it was never imported.</summary>
    Task<int?> GetImportedPackVersionAsync(string packId, CancellationToken cancellationToken);

    /// <summary>Versions of every imported pack keyed by pack id: one query, so the bootstrap compares the whole bundle without a round-trip per pack.</summary>
    Task<IReadOnlyDictionary<string, int>> GetImportedPackVersionsAsync(CancellationToken cancellationToken);

    /// <summary>Replaces the entries of a pack atomically and records its version.</summary>
    Task ImportPackAsync(ImportedContentPack pack, CancellationToken cancellationToken);
}

/// <summary>A pack after mapping from its transport format into Domain entries.</summary>
public sealed record ImportedContentPack(string PackId, LanguagePair Pair, int Version, IReadOnlyList<WordEntry> Entries);

/// <summary>A pack a source can provide, known before its entries are read: enough to decide whether an import is needed (issue #4).</summary>
public sealed record BundledPackDescriptor(string PackId, LanguagePair Pair, int Version, string Asset);

/// <summary>Where packs come from: the bundled seed today, downloaded CDN packs later (T8, T25).</summary>
public interface IContentPackSource
{
    /// <summary>Cheap listing of the packs shipped for a pair (the bundled index); no pack body is read.</summary>
    Task<IReadOnlyList<BundledPackDescriptor>> ListBundledPacksAsync(LanguagePair pair, CancellationToken cancellationToken);

    /// <summary>Reads and maps one pack; the bootstrap calls it only for packs newer than the imported version. Null when the asset is unreadable.</summary>
    Task<ImportedContentPack?> LoadBundledPackAsync(BundledPackDescriptor descriptor, CancellationToken cancellationToken);
}
