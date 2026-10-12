using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Hosting;

namespace Burdock.Server.Shared.Storage;

/// <summary>
/// Immutable objects behind the CDN (spec 09 §3.4, spec 14 §3.16): content packs and pronunciation clips. One
/// implementation per environment: a folder in development and tests, Hetzner Object Storage (S3-compatible) in production.
/// Keys are relative paths; objects are never overwritten with different content by the callers.
/// </summary>
public interface IObjectStore
{
    Task PutAsync(string key, ReadOnlyMemory<byte> content, string contentType, CancellationToken cancellationToken);

    Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken);

    /// <summary>Public (CDN) address of an object, or null when no public base is configured and the API has to stream it.</summary>
    Uri? PublicUrl(string key);
}

/// <summary>Configuration shape shared by every module with an object store («Content», «Pronunciation» sections).</summary>
public class ObjectStoreOptions
{
    public const string FileSystemStore = "FileSystem";
    public const string S3Store = "S3";

    /// <summary><see cref="FileSystemStore"/> (development, tests) or <see cref="S3Store"/> (Hetzner Object Storage).</summary>
    public string ObjectStore { get; set; } = FileSystemStore;

    /// <summary>Folder of the file store; relative paths resolve against the content root.</summary>
    public string StorePath { get; set; } = "data/objects";

    /// <summary>Base of the public URLs (CDN); null makes <see cref="IObjectStore.PublicUrl"/> return null.</summary>
    public Uri? PublicBaseUrl { get; set; }

    public S3Options S3 { get; set; } = new();

    public bool UsesS3 => string.Equals(ObjectStore, S3Store, StringComparison.OrdinalIgnoreCase);

    public sealed class S3Options
    {
        public Uri? ServiceUrl { get; set; }

        public string Bucket { get; set; } = "burdock-content";

        public string Region { get; set; } = "eu-central";

        public string? AccessKeyId { get; set; }

        public string? SecretAccessKey { get; set; }
    }
}

public static class ObjectStores
{
    public const string ImmutableCacheControl = "public, max-age=31536000, immutable";

    /// <summary>Builds the store a module configured; <paramref name="section"/> names the configuration section in error messages.</summary>
    public static IObjectStore Create(ObjectStoreOptions options, IHostEnvironment environment, string section)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);
        return options.UsesS3
            ? new S3ObjectStore(options, section)
            : new FileSystemObjectStore(Path.GetFullPath(Path.Combine(environment.ContentRootPath, options.StorePath)), options.PublicBaseUrl);
    }
}

/// <summary>Objects as files under a root folder; writes are atomic (temp file + move) so a reader never sees a partial object.</summary>
public sealed class FileSystemObjectStore(string root, Uri? publicBaseUrl) : IObjectStore
{
    private readonly string _root = Path.GetFullPath(root);

    public async Task PutAsync(string key, ReadOnlyMemory<byte> content, string contentType, CancellationToken cancellationToken)
    {
        var full = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var temp = full + ".tmp";
        await File.WriteAllBytesAsync(temp, content, cancellationToken).ConfigureAwait(false);
        File.Move(temp, full, overwrite: true);
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        var full = Resolve(key);
        return Task.FromResult<Stream?>(File.Exists(full) ? new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.Asynchronous) : null);
    }

    public Uri? PublicUrl(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return publicBaseUrl is null ? null : new Uri(publicBaseUrl, key);
    }

    private string Resolve(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var full = Path.GetFullPath(Path.Combine(_root, key));
        return full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? full
            : throw new ArgumentException("Object key must stay inside the store root.", nameof(key));
    }
}

/// <summary>Hetzner Object Storage (S3-compatible) as the CDN origin: immutable objects with a one-year cache header.</summary>
public sealed class S3ObjectStore : IObjectStore, IDisposable
{
    private readonly AmazonS3Client _client;
    private readonly string _bucket;
    private readonly Uri? _publicBaseUrl;

    public S3ObjectStore(ObjectStoreOptions options, string section)
    {
        ArgumentNullException.ThrowIfNull(options);
        var s3 = options.S3;
        if (s3.ServiceUrl is null || string.IsNullOrWhiteSpace(s3.AccessKeyId) || string.IsNullOrWhiteSpace(s3.SecretAccessKey))
        {
            throw new InvalidOperationException($"{section}:S3:ServiceUrl, AccessKeyId and SecretAccessKey are required for the S3 object store.");
        }

        _client = new AmazonS3Client(s3.AccessKeyId, s3.SecretAccessKey, new AmazonS3Config
        {
            ServiceURL = s3.ServiceUrl.ToString(),
            ForcePathStyle = true,
            AuthenticationRegion = s3.Region,
        });
        _bucket = s3.Bucket;
        _publicBaseUrl = options.PublicBaseUrl;
    }

    public async Task PutAsync(string key, ReadOnlyMemory<byte> content, string contentType, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(content.ToArray(), writable: false);
        var request = new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = stream,
            ContentType = contentType,
            AutoCloseStream = false,
        };
        request.Headers.CacheControl = ObjectStores.ImmutableCacheControl;
        await _client.PutObjectAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _client.GetObjectAsync(_bucket, key, cancellationToken).ConfigureAwait(false);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Uri? PublicUrl(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _publicBaseUrl is null ? null : new Uri(_publicBaseUrl, key);
    }

    public void Dispose() => _client.Dispose();
}
