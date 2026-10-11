using Burdock.Domain.SharedKernel;
using Burdock.Domain.Sync;
using Burdock.Domain.Vocabulary;

namespace Burdock.Application.Ports;

/// <summary>The installation credential of this device (spec 09 §3.3.1): id + 32-byte secret kept in SecureStorage, never in files.</summary>
public sealed record InstallationCredential(Guid InstallationId, string Secret);

public interface IDeviceCredentials
{
    Task<InstallationCredential> GetOrCreateInstallationAsync(CancellationToken cancellationToken);

    /// <summary>Replaces the credential after the server reported an installation conflict (spec 09 section 3.3.1).</summary>
    Task<InstallationCredential> ResetInstallationAsync(CancellationToken cancellationToken);

    Task<string?> GetRefreshTokenAsync(CancellationToken cancellationToken);

    Task SetRefreshTokenAsync(string? refreshToken, CancellationToken cancellationToken);
}

/// <summary>One progress event as the app produces and consumes it (spec 09 §3.5); the payload is JSON built by <c>ProgressEventFactory</c>.</summary>
public sealed record SyncEvent(Guid Id, string Type, DateTimeOffset OccurredAt, DateOnly LocalDate, Guid? InstallationId, string PayloadJson);

public sealed record SyncedEvent(long ServerSeq, SyncEvent Event);

public sealed record PushOutcome(IReadOnlyList<Guid> Accepted, IReadOnlyList<Guid> Duplicates, IReadOnlyList<RejectedSyncEvent> Rejected, long ServerSeq);

public sealed record RejectedSyncEvent(Guid Id, string Code);

public sealed record PullPage(IReadOnlyList<SyncedEvent> Events, long NextSince, bool HasMore);

public sealed record RemoteSettings(IReadOnlyDictionary<string, SettingsEntry> Entries, string Etag);

public sealed record RemotePack(string Id, string Level, string Topic, int Version, string Sha256, long SizeBytes, Uri Url, bool Required);

public sealed record RemoteManifest(string Etag, IReadOnlyList<RemotePack> Packs);

/// <summary>
/// The server as the app sees it (ADR 0007: server mandatory, cache first). The adapter owns registration, tokens and
/// transport; use cases only see results. Network trouble surfaces as <c>sync.offline</c> and never as an exception.
/// </summary>
public interface ISyncGateway
{
    /// <summary>Registers this installation (proposing the local learner id for a fresh account) and makes sure tokens are available.</summary>
    Task<Result<LearnerId>> EnsureAccountAsync(LearnerId proposedLearnerId, CancellationToken cancellationToken);

    Task<Result<PushOutcome>> PushEventsAsync(IReadOnlyList<SyncEvent> events, CancellationToken cancellationToken);

    Task<Result<PullPage>> PullEventsAsync(long since, Guid? excludeInstallation, int limit, CancellationToken cancellationToken);

    /// <summary>Null when the server answered 304 for the given ETag.</summary>
    Task<Result<RemoteSettings?>> GetSettingsAsync(string? etag, CancellationToken cancellationToken);

    Task<Result<RemoteSettings>> PutSettingsAsync(IReadOnlyList<SettingsEntry> entries, CancellationToken cancellationToken);

    /// <summary>Null when the server answered 304 for the given ETag.</summary>
    Task<Result<RemoteManifest?>> GetManifestAsync(LanguagePair pair, string? etag, CancellationToken cancellationToken);

    /// <summary>Downloads, verifies (SHA-256 of the object) and maps one pack.</summary>
    Task<Result<ImportedContentPack>> DownloadPackAsync(RemotePack pack, CancellationToken cancellationToken);
}

/// <summary>Events waiting to be pushed; appended by the learning use cases, drained by the sync.</summary>
public interface IProgressOutbox
{
    Task AppendAsync(SyncEvent @event, CancellationToken cancellationToken);

    Task<IReadOnlyList<SyncEvent>> PeekAsync(int limit, CancellationToken cancellationToken);

    Task RemoveAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    Task<int> CountAsync(CancellationToken cancellationToken);
}

/// <summary>Small key-value state of the sync: cursor, ETags, account id, last sync time.</summary>
public interface ISyncState
{
    Task<string?> GetAsync(string key, CancellationToken cancellationToken);

    Task SetAsync(string key, string? value, CancellationToken cancellationToken);
}

/// <summary>The learner settings as synchronisable entries with per-key stamps (spec 09 §3.6).</summary>
public interface ISyncableSettings
{
    /// <summary>Entries the user has changed on this device (keys without a stamp are defaults and are not sent).</summary>
    Task<IReadOnlyList<SettingsEntry>> GetEntriesAsync(string clientId, CancellationToken cancellationToken);

    /// <summary>Writes entries that won the merge into the local settings together with their stamps.</summary>
    Task ApplyAsync(IReadOnlyList<SettingsEntry> entries, CancellationToken cancellationToken);
}
