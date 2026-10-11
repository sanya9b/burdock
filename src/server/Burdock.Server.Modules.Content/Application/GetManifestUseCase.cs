using Burdock.Contracts.V1.Content;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;
using Burdock.Server.Modules.Content.Domain;
using Burdock.Server.Shared.Contracts;
using Burdock.Server.Shared.Time;

namespace Burdock.Server.Modules.Content.Application;

/// <summary>
/// GET /v1/content/manifest (spec 09 §3.4, FR-09-7): the newest published version of every pack the learner's entitlement
/// allows, with the CDN address of each object. The free pool is always listed and marked required.
/// </summary>
public sealed class GetManifestUseCase(IPackCatalog catalog, IPackObjectStore store, IEntitlementReader entitlements, IClock clock)
{
    public const int SchemaVersion = 1;

    public async Task<ContentManifestDto> ExecuteAsync(LanguagePair pair, LearnerId learnerId, CancellationToken cancellationToken)
    {
        var state = await entitlements.GetStateAsync(learnerId, cancellationToken).ConfigureAwait(false);
        var visible = (await catalog.ListAsync(pair, cancellationToken).ConfigureAwait(false))
            .Where(p => p.IsVisibleTo(state))
            .GroupBy(p => p.PackId, StringComparer.Ordinal)
            .Select(g => g.OrderByDescending(p => p.Version).First())
            .OrderBy(p => p.PackId, StringComparer.Ordinal)
            .ToArray();

        // Changes with every publication: the newest publication instant, so clients can order manifests without parsing dates.
        var manifestVersion = visible.Length == 0 ? 0 : (int)(visible.Max(p => p.PublishedAt).ToUnixTimeSeconds() % int.MaxValue);
        var packs = visible.Select(p => new ContentPackDto(
            p.PackId,
            p.Level,
            p.Topic,
            p.Version,
            p.Sha256,
            p.SizeBytes,
            store.PublicUrl(p.ObjectKey),
            p.MinAppVersion,
            Required: p.IsFreePool,
            p.DeprecatedAfter,
            p.Attributions.Select(a => new AttributionDto(a.Source, a.License, a.Text, a.Url)).ToArray())).ToArray();
        return new ContentManifestDto(SchemaVersion, manifestVersion, clock.UtcNow, pair.ToString(), packs);
    }
}
