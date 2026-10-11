using System.Security.Cryptography;
using Burdock.Application.Ports;
using Microsoft.Maui.Storage;

namespace Burdock.Infrastructure.Sync;

/// <summary>
/// Installation id, secret and refresh token in the platform keychain (spec 10 section 3.4: tokens only in SecureStorage,
/// never in App Group files or logs). The credential is created on first use and replaced only when the server reports
/// an installation conflict.
/// </summary>
public sealed class SecureStorageDeviceCredentials(ISecureStorage storage) : IDeviceCredentials, IDisposable
{
    public const string InstallationIdKey = "sync.installationId";
    public const string InstallationSecretKey = "sync.installationSecret";
    public const string RefreshTokenKey = "sync.refreshToken";
    private const int SecretBytes = 32;

    private readonly SemaphoreSlim _lock = new(1, 1);
    private InstallationCredential? _cached;

    public async Task<InstallationCredential> GetOrCreateInstallationAsync(CancellationToken cancellationToken)
    {
        if (_cached is { } cached)
        {
            return cached;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cached is not null)
            {
                return _cached;
            }

            var id = await storage.GetAsync(InstallationIdKey).ConfigureAwait(false);
            var secret = await storage.GetAsync(InstallationSecretKey).ConfigureAwait(false);
            if (Guid.TryParse(id, out var parsed) && parsed != Guid.Empty && !string.IsNullOrEmpty(secret))
            {
                _cached = new InstallationCredential(parsed, secret);
                return _cached;
            }

            _cached = await CreateAsync().ConfigureAwait(false);
            return _cached;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<InstallationCredential> ResetInstallationAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _cached = await CreateAsync().ConfigureAwait(false);
            return _cached;
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task<string?> GetRefreshTokenAsync(CancellationToken cancellationToken) => storage.GetAsync(RefreshTokenKey);

    public async Task SetRefreshTokenAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            storage.Remove(RefreshTokenKey);
            return;
        }

        await storage.SetAsync(RefreshTokenKey, refreshToken).ConfigureAwait(false);
    }

    public void Dispose() => _lock.Dispose();

    /// <summary>UUIDv4 id plus a 32-byte random secret (spec 09 section 3.3.1); a new installation never inherits the old refresh token.</summary>
    private async Task<InstallationCredential> CreateAsync()
    {
        var created = new InstallationCredential(Guid.NewGuid(), Convert.ToBase64String(RandomNumberGenerator.GetBytes(SecretBytes)));
        await storage.SetAsync(InstallationIdKey, created.InstallationId.ToString("D")).ConfigureAwait(false);
        await storage.SetAsync(InstallationSecretKey, created.Secret).ConfigureAwait(false);
        storage.Remove(RefreshTokenKey);
        return created;
    }
}
