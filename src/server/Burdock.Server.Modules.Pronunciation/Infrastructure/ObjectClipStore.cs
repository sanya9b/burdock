using System.Security.Cryptography;
using Burdock.Server.Modules.Pronunciation.Application;
using Burdock.Server.Shared.Storage;

namespace Burdock.Server.Modules.Pronunciation.Infrastructure;

/// <summary>Clips in the shared object store (ADR 0020 §5): a folder in development, Object Storage behind the CDN in production.</summary>
public sealed class ObjectClipStore(IObjectStore objects) : IClipStore, IDisposable
{
    public const string ContentType = "audio/mp4";

    public async Task<StoredClip> PutAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        await objects.PutAsync(path, content, ContentType, cancellationToken).ConfigureAwait(false);
        return new StoredClip(path, Convert.ToHexStringLower(SHA256.HashData(content.Span)), content.Length);
    }

    public Task<Stream?> OpenReadAsync(string path, CancellationToken cancellationToken) => objects.OpenReadAsync(path, cancellationToken);

    public Uri? PublicUrl(string path) => objects.PublicUrl(path);

    public void Dispose() => (objects as IDisposable)?.Dispose();
}
