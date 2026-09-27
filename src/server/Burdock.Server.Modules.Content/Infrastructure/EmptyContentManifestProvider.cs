using Burdock.Contracts.V1.Content;
using Burdock.Domain.Vocabulary;
using Burdock.Server.Modules.Content.Application;
using Burdock.Server.Shared.Time;

namespace Burdock.Server.Modules.Content.Infrastructure;

/// <summary>Manifest with no packs; replaced by the PostgreSQL-backed provider when the content pipeline publishes its first pack.</summary>
public sealed class EmptyContentManifestProvider(IClock clock) : IContentManifestProvider
{
    public const int SchemaVersion = 1;

    private readonly IClock _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public Task<ContentManifestDto> GetManifestAsync(LanguagePair pair, CancellationToken cancellationToken)
        => Task.FromResult(new ContentManifestDto(SchemaVersion, ManifestVersion: 0, _clock.UtcNow, pair.ToString(), []));
}
