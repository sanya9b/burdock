using Amazon.S3;
using Amazon.S3.Model;
using Burdock.Server.Modules.Content.Application;
using Microsoft.Extensions.Hosting;

namespace Burdock.Server.Modules.Content.Infrastructure;

/// <summary>Development store: objects under <see cref="ContentOptions.StorePath"/>, served by the API's own store route when enabled.</summary>
public sealed class FileSystemPackObjectStore : IPackObjectStore
{
    private readonly string _root;
    private readonly Uri _publicBaseUrl;

    public FileSystemPackObjectStore(ContentOptions options, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);
        _root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, options.StorePath));
        _publicBaseUrl = options.PublicBaseUrl ?? new Uri("http://localhost:5210/v1/content/store/");
    }

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

    public Uri PublicUrl(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return new Uri(_publicBaseUrl, key);
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

/// <summary>Hetzner Object Storage (S3-compatible) as the CDN origin (spec 09 §3.4, spec 14 §3.16): immutable objects with a one-year cache header.</summary>
public sealed class S3PackObjectStore : IPackObjectStore, IDisposable
{
    public const string ImmutableCacheControl = "public, max-age=31536000, immutable";

    private readonly AmazonS3Client _client;
    private readonly string _bucket;
    private readonly Uri _publicBaseUrl;

    public S3PackObjectStore(ContentOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var s3 = options.S3;
        if (s3.ServiceUrl is null || string.IsNullOrWhiteSpace(s3.AccessKeyId) || string.IsNullOrWhiteSpace(s3.SecretAccessKey) || options.PublicBaseUrl is null)
        {
            throw new InvalidOperationException("Content:S3:ServiceUrl, AccessKeyId, SecretAccessKey and Content:PublicBaseUrl are required for the S3 pack store.");
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
        request.Headers.CacheControl = ImmutableCacheControl;
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

    public Uri PublicUrl(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return new Uri(_publicBaseUrl, key);
    }

    public void Dispose() => _client.Dispose();
}
