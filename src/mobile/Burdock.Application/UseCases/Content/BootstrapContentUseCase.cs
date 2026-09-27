using Burdock.Application.Ports;
using Burdock.Domain.Vocabulary;
using Microsoft.Extensions.Logging;

namespace Burdock.Application.UseCases.Content;

/// <summary>Result of the first-launch import: how many packs were imported or already current.</summary>
public readonly record struct BootstrapContentResult(int PacksImported, int PacksUpToDate, int EntriesAvailable);

/// <summary>
/// Makes sure the bundled packs are in the local cache so widgets and the Today screen work before any network call
/// (decision Q13: cache after first launch; the server manifest refresh is a separate use case).
/// </summary>
public sealed partial class BootstrapContentUseCase(IContentPackSource source, IContentRepository content, ILearnerContext learner, ILogger<BootstrapContentUseCase> logger)
{
    public async Task<BootstrapContentResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        var settings = await learner.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        var packs = await source.LoadBundledPacksAsync(settings.Pair, cancellationToken).ConfigureAwait(false);
        var imported = 0;
        var current = 0;
        foreach (var pack in packs)
        {
            var existing = await content.GetImportedPackVersionAsync(pack.PackId, cancellationToken).ConfigureAwait(false);
            if (existing >= pack.Version)
            {
                current++;
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
}
