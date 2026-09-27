using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;

namespace Burdock.Application.Ports;

/// <summary>Locally cached content (imported packs), read by planner, WordCard and exercises (spec 08 §3.10, spec 01 §3.4).</summary>
public interface IContentRepository
{
    Task<WordEntry?> GetAsync(LexemeId id, CancellationToken cancellationToken);

    /// <summary>Entries of a pair filtered by levels and topics; an empty topic list means all topics.</summary>
    Task<IReadOnlyList<WordEntry>> ListAsync(LanguagePair pair, IReadOnlyCollection<CefrLevel> levels, IReadOnlyCollection<TopicId> topics, CancellationToken cancellationToken);

    Task<int> CountAsync(LanguagePair pair, CancellationToken cancellationToken);

    /// <summary>Version of an imported pack, or null when it was never imported.</summary>
    Task<int?> GetImportedPackVersionAsync(string packId, CancellationToken cancellationToken);

    /// <summary>Replaces the entries of a pack atomically and records its version.</summary>
    Task ImportPackAsync(ImportedContentPack pack, CancellationToken cancellationToken);
}

/// <summary>A pack after mapping from its transport format into Domain entries.</summary>
public sealed record ImportedContentPack(string PackId, LanguagePair Pair, int Version, IReadOnlyList<WordEntry> Entries);

/// <summary>Where packs come from: the bundled seed today, downloaded CDN packs later (T8, T25).</summary>
public interface IContentPackSource
{
    /// <summary>Packs shipped inside the app package for first launch without a server round-trip.</summary>
    Task<IReadOnlyList<ImportedContentPack>> LoadBundledPacksAsync(LanguagePair pair, CancellationToken cancellationToken);
}
