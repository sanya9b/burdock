using Burdock.Domain.Vocabulary;
using Burdock.Server.Modules.Content.Domain;

namespace Burdock.Server.Modules.Content.Application;

/// <summary>Rows of <c>content.packs</c>: every version of every pack of a pair, published and deprecated.</summary>
public interface IPackCatalog
{
    Task<IReadOnlyList<ContentPack>> ListAsync(LanguagePair pair, CancellationToken cancellationToken);

    Task<ContentPack?> GetAsync(string packId, int version, CancellationToken cancellationToken);

    Task AddAsync(ContentPack pack, CancellationToken cancellationToken);

    Task UpdateAsync(ContentPack pack, CancellationToken cancellationToken);

    Task CommitAsync(CancellationToken cancellationToken);
}

/// <summary>Where pack objects live (spec 09 §3.4): Hetzner Object Storage behind Bunny in production, a folder in development.</summary>
public interface IPackObjectStore
{
    Task PutAsync(string key, ReadOnlyMemory<byte> content, string contentType, CancellationToken cancellationToken);

    Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken);

    /// <summary>Address the manifest hands to the client; the CDN origin, or the API's own store route in development.</summary>
    Uri PublicUrl(string key);
}
