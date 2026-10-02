using Burdock.Application.Ports;
using Burdock.Domain.Vocabulary;
using Microsoft.Extensions.Logging;

namespace Burdock.Application.UseCases.Content;

/// <summary>Result of the first-launch import: how many packs were imported or already current.</summary>
public readonly record struct BootstrapContentResult(int PacksImported, int PacksUpToDate, int EntriesAvailable);

/// <summary>
/// Makes sure the bundled packs are in the local cache so widgets and the Today screen work before any network call
/// (decision Q13: cache after first launch; the server manifest refresh is a separate use case). Runs on every Today
/// load, so it compares the bundled index with the imported versions first and reads a pack body only when that pack
/// is missing or outdated (issue #4: reading all 61 packs on each launch cost ~4–5 s on the emulator).
/// </summary>
public sealed partial class BootstrapContentUseCase(IContentPackSource source, IContentRepository content, ILearnerContext learner, ILogger<BootstrapContentUseCase> logger)
{
    public async Task<BootstrapContentResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        var settings = await learner.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        var descriptors = await source.ListBundledPacksAsync(settings.Pair, cancellationToken).ConfigureAwait(false);
        var importedVersions = await content.GetImportedPackVersionsAsync(cancellationToken).ConfigureAwait(false);
        var imported = 0;
        var current = 0;
        foreach (var descriptor in descriptors)
        {
            if (importedVersions.TryGetValue(descriptor.PackId, out var existing) && existing >= descriptor.Version)
            {
                current++;
                continue;
            }

            var pack = await source.LoadBundledPackAsync(descriptor, cancellationToken).ConfigureAwait(false);
            if (pack is null)
            {
                LogUnreadable(descriptor.PackId, descriptor.Asset);
                continue;
            }

            await content.ImportPackAsync(pack, cancellationToken).ConfigureAwait(false);
            imported++;
            LogImported(pack.PackId, pack.Version, pack.Entries.Count);
        }

        var available = await content.CountAsync(settings.Pair, cancellationToken).ConfigureAwait(false);
        return new BootstrapContentResult(imported, current, available);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Imported content pack {PackId} v{Version} with {Entries} entries")]
    private partial void LogImported(string packId, int version, int entries);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Bundled content pack {PackId} could not be read from {Asset}; skipped")]
    private partial void LogUnreadable(string packId, string asset);
}
