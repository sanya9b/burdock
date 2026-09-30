using System.Text.Json;
using Burdock.Application.Ports;
using Burdock.Contracts.Content;
using Burdock.Domain.Vocabulary;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;

namespace Burdock.Infrastructure.Content;

/// <summary>
/// Reads packs shipped as MauiAssets (spec 08 §3.10). When content/packs/index.json is bundled (built by
/// content/pipeline/packs/build_packs.py) its packs are the content; otherwise the handwritten seed pack under content/ is.
/// Downloaded packs from the manifest replace this source in phase 2 (T8, T25).
/// </summary>
public sealed partial class BundledContentPackSource(ILogger<BundledContentPackSource> logger) : IContentPackSource
{
    private const string PacksIndexAsset = "content/packs/index.json";
    private const string PacksRoot = "content/packs/";

    /// <summary>Fallback for builds without generated packs; the Presentation project links content/seed/*.json into the app package.</summary>
    private static readonly string[] SeedAssets = ["content/uk-en.free-pool.seed.json"];

    public async Task<IReadOnlyList<ImportedContentPack>> LoadBundledPacksAsync(LanguagePair pair, CancellationToken cancellationToken)
    {
        var assets = await ListPackAssetsAsync(pair, cancellationToken).ConfigureAwait(false);
        var packs = new List<ImportedContentPack>(assets.Count);
        foreach (var asset in assets)
        {
            var pack = await ReadPackAsync(asset, cancellationToken).ConfigureAwait(false);
            if (pack is not null && pack.Pair == pair)
            {
                packs.Add(pack);
            }
        }

        return packs;
    }

    private async Task<IReadOnlyList<string>> ListPackAssetsAsync(LanguagePair pair, CancellationToken cancellationToken)
    {
        if (!await FileSystem.Current.AppPackageFileExistsAsync(PacksIndexAsset).ConfigureAwait(false))
        {
            return SeedAssets;
        }

        try
        {
            await using var stream = await FileSystem.Current.OpenAppPackageFileAsync(PacksIndexAsset).ConfigureAwait(false);
            var index = await JsonSerializer.DeserializeAsync(stream, ContentPackJsonContext.Default.ContentPackIndexDocument, cancellationToken).ConfigureAwait(false);
            if (index is null || index.SchemaVersion > ContentPackIndexDocument.CurrentSchemaVersion)
            {
                LogInvalid(PacksIndexAsset, index is null ? "empty" : "content.schemaTooNew");
                return SeedAssets;
            }

            var listed = index.Packs.Where(p => string.Equals(index.Pair, pair.ToString(), StringComparison.Ordinal)).Select(p => PacksRoot + p.File).ToList();
            LogIndex(listed.Count);
            return listed.Count > 0 ? listed : SeedAssets;
        }
        catch (JsonException ex)
        {
            LogInvalid(PacksIndexAsset, ex.GetType().Name);
            return SeedAssets;
        }
    }

    private async Task<ImportedContentPack?> ReadPackAsync(string asset, CancellationToken cancellationToken)
    {
        await using var stream = await FileSystem.Current.OpenAppPackageFileAsync(asset).ConfigureAwait(false);
        var document = await JsonSerializer.DeserializeAsync(stream, ContentPackJsonContext.Default.ContentPackDocument, cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            LogUnreadable(asset);
            return null;
        }

        var mapped = ContentPackMapper.ToDomain(document);
        if (mapped.IsFailure)
        {
            LogInvalid(asset, mapped.Error.Code);
            return null;
        }

        return mapped.Value;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Bundled pack index lists {Count} packs")]
    private partial void LogIndex(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Bundled content pack {Asset} could not be read")]
    private partial void LogUnreadable(string asset);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Bundled content pack {Asset} is invalid: {Code}")]
    private partial void LogInvalid(string asset, string code);
}
