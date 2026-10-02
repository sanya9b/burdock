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
/// Listing comes from the index alone (a few KB); a pack body (up to hundreds of KB) is read only when the bootstrap
/// decides that pack must be imported (issue #4). Downloaded packs from the manifest replace this source in phase 2 (T8, T25).
/// </summary>
public sealed partial class BundledContentPackSource(ILogger<BundledContentPackSource> logger) : IContentPackSource
{
    private const string PacksIndexAsset = "content/packs/index.json";
    private const string PacksRoot = "content/packs/";

    /// <summary>Fallback for builds without generated packs; the Presentation project links content/seed/*.json into the app package.</summary>
    private static readonly string[] SeedAssets = ["content/uk-en.free-pool.seed.json"];

    public async Task<IReadOnlyList<BundledPackDescriptor>> ListBundledPacksAsync(LanguagePair pair, CancellationToken cancellationToken)
    {
        if (!await FileSystem.Current.AppPackageFileExistsAsync(PacksIndexAsset).ConfigureAwait(false))
        {
            return await SeedDescriptorsAsync(pair, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await using var stream = await FileSystem.Current.OpenAppPackageFileAsync(PacksIndexAsset).ConfigureAwait(false);
            var index = await JsonSerializer.DeserializeAsync(stream, ContentPackJsonContext.Default.ContentPackIndexDocument, cancellationToken).ConfigureAwait(false);
            if (index is null || index.SchemaVersion > ContentPackIndexDocument.CurrentSchemaVersion)
            {
                LogInvalid(PacksIndexAsset, index is null ? "empty" : "content.schemaTooNew");
                return await SeedDescriptorsAsync(pair, cancellationToken).ConfigureAwait(false);
            }

            var listed = string.Equals(index.Pair, pair.ToString(), StringComparison.Ordinal)
                ? index.Packs.Select(p => new BundledPackDescriptor(p.PackId, pair, p.Version, PacksRoot + p.File)).ToList()
                : [];
            LogIndex(listed.Count);
            return listed.Count > 0 ? listed : await SeedDescriptorsAsync(pair, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            LogInvalid(PacksIndexAsset, ex.GetType().Name);
            return await SeedDescriptorsAsync(pair, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<ImportedContentPack?> LoadBundledPackAsync(BundledPackDescriptor descriptor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var pack = await ReadPackAsync(descriptor.Asset, cancellationToken).ConfigureAwait(false);
        return pack is not null && pack.Pair == descriptor.Pair ? pack : null;
    }

    /// <summary>The seed pack holds a few dozen entries, so reading it to learn its id and version is as cheap as an index.</summary>
    private async Task<IReadOnlyList<BundledPackDescriptor>> SeedDescriptorsAsync(LanguagePair pair, CancellationToken cancellationToken)
    {
        var descriptors = new List<BundledPackDescriptor>(SeedAssets.Length);
        foreach (var asset in SeedAssets)
        {
            var pack = await ReadPackAsync(asset, cancellationToken).ConfigureAwait(false);
            if (pack is not null && pack.Pair == pair)
            {
                descriptors.Add(new BundledPackDescriptor(pack.PackId, pack.Pair, pack.Version, asset));
            }
        }

        return descriptors;
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
