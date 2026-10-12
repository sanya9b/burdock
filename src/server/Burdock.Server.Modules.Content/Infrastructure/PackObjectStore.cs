using Burdock.Server.Modules.Content.Application;
using Burdock.Server.Shared.Storage;

namespace Burdock.Server.Modules.Content.Infrastructure;

/// <summary>
/// The pack store over the shared object store (spec 09 §3.4). Packs always have a public address: the configured CDN
/// base in production, the API's own store route in development (<c>Content:ServeStoreLocally</c>).
/// </summary>
public sealed class PackObjectStore(IObjectStore objects, Uri publicBaseUrl) : IPackObjectStore, IDisposable
{
    /// <summary>Where the development API serves its own store from when no CDN base is configured.</summary>
    public static readonly Uri DevelopmentBaseUrl = new("http://localhost:5210/v1/content/store/");

    public Task PutAsync(string key, ReadOnlyMemory<byte> content, string contentType, CancellationToken cancellationToken)
        => objects.PutAsync(key, content, contentType, cancellationToken);

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken) => objects.OpenReadAsync(key, cancellationToken);

    public Uri PublicUrl(string key) => objects.PublicUrl(key) ?? new Uri(publicBaseUrl, key);

    public void Dispose() => (objects as IDisposable)?.Dispose();
}
