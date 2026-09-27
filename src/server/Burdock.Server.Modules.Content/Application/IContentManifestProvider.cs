using Burdock.Contracts.V1.Content;
using Burdock.Domain.Vocabulary;

namespace Burdock.Server.Modules.Content.Application;

/// <summary>Produces the manifest for a language pair. Entitlement filtering is applied by the caller once the Entitlements module exists.</summary>
public interface IContentManifestProvider
{
    Task<ContentManifestDto> GetManifestAsync(LanguagePair pair, CancellationToken cancellationToken);
}
