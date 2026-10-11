using Burdock.Application.Ports;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Sync;
using Burdock.Domain.Vocabulary;

namespace Burdock.TestKit.Fakes;

public sealed class InMemoryProgressOutbox : IProgressOutbox
{
    public List<SyncEvent> Events { get; } = [];

    public Task AppendAsync(SyncEvent @event, CancellationToken cancellationToken)
    {
        Events.Add(@event);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SyncEvent>> PeekAsync(int limit, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<SyncEvent>>(Events.Take(limit).ToArray());

    public Task RemoveAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        Events.RemoveAll(e => ids.Contains(e.Id));
        return Task.CompletedTask;
    }

    public Task<int> CountAsync(CancellationToken cancellationToken) => Task.FromResult(Events.Count);
}

public sealed class InMemorySyncState : ISyncState
{
    public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken) => Task.FromResult(Values.GetValueOrDefault(key));

    public Task SetAsync(string key, string? value, CancellationToken cancellationToken)
    {
        if (value is null)
        {
            Values.Remove(key);
        }
        else
        {
            Values[key] = value;
        }

        return Task.CompletedTask;
    }
}

public sealed class FakeDeviceCredentials(Guid installationId) : IDeviceCredentials
{
    public InstallationCredential Credential { get; private set; } = new(installationId, Convert.ToBase64String([.. installationId.ToByteArray(), .. installationId.ToByteArray()]));

    public string? RefreshToken { get; private set; }

    public Task<InstallationCredential> GetOrCreateInstallationAsync(CancellationToken cancellationToken) => Task.FromResult(Credential);

    public Task<InstallationCredential> ResetInstallationAsync(CancellationToken cancellationToken)
    {
        Credential = new InstallationCredential(Guid.NewGuid(), Credential.Secret);
        return Task.FromResult(Credential);
    }

    public Task<string?> GetRefreshTokenAsync(CancellationToken cancellationToken) => Task.FromResult(RefreshToken);

    public Task SetRefreshTokenAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        RefreshToken = refreshToken;
        return Task.CompletedTask;
    }
}

/// <summary>Settings as stamped entries; an empty client id marks a local write whose client is the installation at sync time.</summary>
public sealed class InMemorySyncableSettings : ISyncableSettings
{
    public Dictionary<string, SettingsEntry> Entries { get; } = new(StringComparer.Ordinal);

    public void SetLocal(string key, string valueJson, DateTimeOffset at) => Entries[key] = new SettingsEntry(key, valueJson, at, string.Empty);

    public Task<IReadOnlyList<SettingsEntry>> GetEntriesAsync(string clientId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<SettingsEntry>>(Entries.Values.Select(e => e.ClientId.Length == 0 ? e with { ClientId = clientId } : e).ToArray());

    public Task ApplyAsync(IReadOnlyList<SettingsEntry> entries, CancellationToken cancellationToken)
    {
        foreach (var entry in entries)
        {
            Entries[entry.Key] = entry;
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// In-memory stand-in for the server with the rules of the real modules: idempotent append by id with a per-account
/// sequence, pull after a cursor minus one installation, LWW settings through the shared Domain rules, a manifest with an ETag.
/// </summary>
public sealed class FakeSyncGateway : ISyncGateway
{
    private readonly List<SyncedEvent> _events = [];
    private readonly Dictionary<string, SettingsEntry> _settings = new(StringComparer.Ordinal);
    private readonly List<(RemotePack Pack, ImportedContentPack Content)> _packs = [];

    public bool Offline { get; set; }

    public HashSet<Guid> TakenLearnerIds { get; } = [];

    public LearnerId? AccountId { get; private set; }

    public IReadOnlyList<SyncedEvent> Events => _events;

    public IReadOnlyDictionary<string, SettingsEntry> Settings => _settings;

    public string ManifestEtag { get; set; } = "manifest-1";

    public int PushCalls { get; private set; }

    public int PullCalls { get; private set; }

    public int ManifestCalls { get; private set; }

    public int DownloadCalls { get; private set; }

    public void Publish(RemotePack pack, ImportedContentPack content)
    {
        ArgumentNullException.ThrowIfNull(pack);
        _packs.RemoveAll(p => string.Equals(p.Pack.Id, pack.Id, StringComparison.Ordinal));
        _packs.Add((pack, content));
    }

    public Task<Result<LearnerId>> EnsureAccountAsync(LearnerId proposedLearnerId, CancellationToken cancellationToken)
    {
        if (Offline)
        {
            return Task.FromResult(Result.Failure<LearnerId>(OfflineError));
        }

        if (AccountId is null)
        {
            if (TakenLearnerIds.Contains(proposedLearnerId.Value))
            {
                return Task.FromResult(Result.Failure<LearnerId>(DomainError.Conflict("sync.learner_id_taken", "Errors_Sync_LearnerIdTaken")));
            }

            AccountId = proposedLearnerId;
        }

        return Task.FromResult(Result.Success(AccountId.Value));
    }

    public Task<Result<PushOutcome>> PushEventsAsync(IReadOnlyList<SyncEvent> events, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (Offline)
        {
            return Task.FromResult(Result.Failure<PushOutcome>(OfflineError));
        }

        PushCalls++;
        var accepted = new List<Guid>();
        var duplicates = new List<Guid>();
        foreach (var @event in events)
        {
            if (_events.Any(s => s.Event.Id == @event.Id))
            {
                duplicates.Add(@event.Id);
                continue;
            }

            _events.Add(new SyncedEvent(_events.Count + 1, @event));
            accepted.Add(@event.Id);
        }

        return Task.FromResult(Result.Success(new PushOutcome(accepted, duplicates, [], _events.Count)));
    }

    public Task<Result<PullPage>> PullEventsAsync(long since, Guid? excludeInstallation, int limit, CancellationToken cancellationToken)
    {
        if (Offline)
        {
            return Task.FromResult(Result.Failure<PullPage>(OfflineError));
        }

        PullCalls++;
        var matching = _events.Where(s => s.ServerSeq > since && (excludeInstallation is null || s.Event.InstallationId != excludeInstallation)).Take(limit + 1).ToList();
        var hasMore = matching.Count > limit;
        var page = matching.Take(limit).ToArray();
        var next = page.Length == 0 ? since : page[^1].ServerSeq;
        return Task.FromResult(Result.Success(new PullPage(page, next, hasMore)));
    }

    public Task<Result<RemoteSettings?>> GetSettingsAsync(string? etag, CancellationToken cancellationToken)
    {
        if (Offline)
        {
            return Task.FromResult(Result.Failure<RemoteSettings?>(OfflineError));
        }

        var current = SettingsDocumentRules.ComputeEtag(_settings.Values);
        if (string.Equals(etag, current, StringComparison.Ordinal))
        {
            return Task.FromResult(Result.Success<RemoteSettings?>(null));
        }

        return Task.FromResult(Result.Success<RemoteSettings?>(new RemoteSettings(new Dictionary<string, SettingsEntry>(_settings, StringComparer.Ordinal), current)));
    }

    public Task<Result<RemoteSettings>> PutSettingsAsync(IReadOnlyList<SettingsEntry> entries, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (Offline)
        {
            return Task.FromResult(Result.Failure<RemoteSettings>(OfflineError));
        }

        var (merged, _) = SettingsDocumentRules.Merge(_settings, entries);
        _settings.Clear();
        foreach (var (key, entry) in merged)
        {
            _settings[key] = entry;
        }

        return Task.FromResult(Result.Success(new RemoteSettings(new Dictionary<string, SettingsEntry>(_settings, StringComparer.Ordinal), SettingsDocumentRules.ComputeEtag(_settings.Values))));
    }

    public Task<Result<RemoteManifest?>> GetManifestAsync(LanguagePair pair, string? etag, CancellationToken cancellationToken)
    {
        if (Offline)
        {
            return Task.FromResult(Result.Failure<RemoteManifest?>(OfflineError));
        }

        ManifestCalls++;
        if (string.Equals(etag, ManifestEtag, StringComparison.Ordinal))
        {
            return Task.FromResult(Result.Success<RemoteManifest?>(null));
        }

        return Task.FromResult(Result.Success<RemoteManifest?>(new RemoteManifest(ManifestEtag, _packs.Select(p => p.Pack).ToArray())));
    }

    public Task<Result<ImportedContentPack>> DownloadPackAsync(RemotePack pack, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pack);
        if (Offline)
        {
            return Task.FromResult(Result.Failure<ImportedContentPack>(OfflineError));
        }

        DownloadCalls++;
        var found = _packs.FirstOrDefault(p => string.Equals(p.Pack.Id, pack.Id, StringComparison.Ordinal) && p.Pack.Version == pack.Version);
        return Task.FromResult(found.Content is null
            ? Result.Failure<ImportedContentPack>(DomainError.NotFound("sync.pack_missing", "Errors_Sync_PackMissing"))
            : Result.Success(found.Content));
    }

    private static DomainError OfflineError => DomainError.Unavailable("sync.offline", "Errors_Sync_Offline");
}
