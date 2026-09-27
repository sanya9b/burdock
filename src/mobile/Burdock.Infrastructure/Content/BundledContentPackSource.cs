using System.Text.Json;
using Burdock.Application.Ports;
using Burdock.Contracts.Content;
using Burdock.Domain.Vocabulary;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;

namespace Burdock.Infrastructure.Content;

/// <summary>Reads packs shipped as MauiAssets under content/ (the free-pool seed today; spec 08 §3.10).</summary>
public sealed partial class BundledContentPackSource(ILogger<BundledContentPackSource> logger) : IContentPackSource
{
    /// <summary>Logical asset names; the Presentation project links content/seed/*.json into the app package.</summary>
    private static readonly string[] BundledAssets = ["content/uk-en.free-pool.seed.json"];

    public async Task<IReadOnlyList<ImportedContentPack>> LoadBundledPacksAsync(LanguagePair pair, CancellationToken cancellationToken)
    {
        var packs = new List<ImportedContentPack>();
        foreach (var asset in BundledAssets)
        {
            await using var stream = await FileSystem.Current.OpenAppPackageFileAsync(asset).ConfigureAwait(false);
            var document = await JsonSerializer.DeserializeAsync(stream, ContentPackJsonContext.Default.ContentPackDocument, cancellationToken).ConfigureAwait(false);
            if (document is null)
            {
                LogUnreadable(asset);
                continue;
            }

            var mapped = ContentPackMapper.ToDomain(document);
            if (mapped.IsFailure)
            {
                LogInvalid(asset, mapped.Error.Code);
                continue;
            }

            if (mapped.Value.Pair == pair)
            {
                packs.Add(mapped.Value);
            }
        }

        return packs;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Bundled content pack {Asset} could not be read")]
    private partial void LogUnreadable(string asset);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Bundled content pack {Asset} is invalid: {Code}")]
    private partial void LogInvalid(string asset, string code);
}
