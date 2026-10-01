using System.Security.Cryptography;
using Burdock.Server.Modules.Pronunciation.Application;
using Microsoft.Extensions.Hosting;

namespace Burdock.Server.Modules.Pronunciation.Infrastructure;

/// <summary>Clips as files under <see cref="PronunciationOptions.StorePath"/>; writes are atomic (temp file + move) so a reader never sees a partial clip.</summary>
public sealed class FileSystemClipStore : IClipStore
{
    private readonly string _root;
    private readonly Uri? _publicBaseUrl;

    public FileSystemClipStore(PronunciationOptions options, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);
        _root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, options.StorePath));
        _publicBaseUrl = options.PublicBaseUrl;
    }

    public async Task<StoredClip> PutAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        var full = Resolve(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var temp = full + ".tmp";
        await File.WriteAllBytesAsync(temp, content, cancellationToken).ConfigureAwait(false);
        File.Move(temp, full, overwrite: true);
        return new StoredClip(path, Convert.ToHexStringLower(SHA256.HashData(content.Span)), content.Length);
    }

    public Task<Stream?> OpenReadAsync(string path, CancellationToken cancellationToken)
    {
        var full = Resolve(path);
        return Task.FromResult<Stream?>(File.Exists(full) ? new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.Asynchronous) : null);
    }

    public Uri? PublicUrl(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return _publicBaseUrl is null ? null : new Uri(_publicBaseUrl, path);
    }

    private string Resolve(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var full = Path.GetFullPath(Path.Combine(_root, path));
        return full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? full
            : throw new ArgumentException("Clip path must stay inside the store root.", nameof(path));
    }
}
