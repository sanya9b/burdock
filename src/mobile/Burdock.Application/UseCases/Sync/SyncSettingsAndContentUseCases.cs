using Burdock.Application.Ports;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Sync;
using Microsoft.Extensions.Logging;

namespace Burdock.Application.UseCases.Sync;

public sealed record SettingsSyncReport(int AppliedFromServer, int SentToServer);

/// <summary>
/// Settings sync (spec 09 §3.6): remote entries that supersede local ones are applied locally, then every locally
/// stamped entry is sent so the server can merge the other way; both sides use the same LWW rule from the shared Domain.
/// </summary>
public sealed class SyncSettingsUseCase(ISyncGateway gateway, ISyncableSettings settings, ISyncState state, IDeviceCredentials credentials)
{
    public const string EtagKey = "settings.etag";

    public async Task<Result<SettingsSyncReport>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var installation = await credentials.GetOrCreateInstallationAsync(cancellationToken).ConfigureAwait(false);
        var clientId = installation.InstallationId.ToString("N");
        var local = await settings.GetEntriesAsync(clientId, cancellationToken).ConfigureAwait(false);
        var localMap = local.ToDictionary(e => e.Key, e => e, StringComparer.Ordinal);

        var remote = await gateway.GetSettingsAsync(etag: null, cancellationToken).ConfigureAwait(false);
        if (remote.IsFailure)
        {
            return Result.Failure<SettingsSyncReport>(remote.Error);
        }

        var applied = 0;
        if (remote.Value is { } document)
        {
            var (_, changed) = SettingsDocumentRules.Merge(localMap, document.Entries.Values);
            if (changed.Count > 0)
            {
                await settings.ApplyAsync(changed, cancellationToken).ConfigureAwait(false);
                applied = changed.Count;
            }
        }

        var toSend = local.Where(e => remote.Value is null || !remote.Value.Entries.TryGetValue(e.Key, out var theirs) || e.Supersedes(theirs)).ToArray();
        var etag = remote.Value?.Etag;
        if (toSend.Length > 0)
        {
            var put = await gateway.PutSettingsAsync(toSend, cancellationToken).ConfigureAwait(false);
            if (put.IsFailure)
            {
                return Result.Failure<SettingsSyncReport>(put.Error);
            }

            etag = put.Value.Etag;
        }

        await state.SetAsync(EtagKey, etag, cancellationToken).ConfigureAwait(false);
        return Result.Success(new SettingsSyncReport(applied, toSend.Length));
    }
}

public sealed record ContentRefreshReport(bool ManifestChanged, int PacksImported, IReadOnlyList<string> Failed);

/// <summary>
/// Content refresh (T8, spec 09 §3.4): the manifest is checked with its ETag; packs newer than the imported version are
/// downloaded, verified and imported. Bundled packs stay until the manifest offers a newer version.
/// </summary>
public sealed partial class RefreshContentUseCase(ISyncGateway gateway, IContentRepository content, ILearnerContext learner, ISyncState state, ILogger<RefreshContentUseCase> logger)
{
    public const string EtagKey = "manifest.etag";

    public async Task<Result<ContentRefreshReport>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var settings = await learner.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        var etag = await state.GetAsync(EtagKey, cancellationToken).ConfigureAwait(false);
        var manifest = await gateway.GetManifestAsync(settings.Pair, etag, cancellationToken).ConfigureAwait(false);
        if (manifest.IsFailure)
        {
            return Result.Failure<ContentRefreshReport>(manifest.Error);
        }

        if (manifest.Value is null)
        {
            return Result.Success(new ContentRefreshReport(false, 0, []));
        }

        var imported = await content.GetImportedPackVersionsAsync(cancellationToken).ConfigureAwait(false);
        var count = 0;
        var failed = new List<string>();
        foreach (var pack in manifest.Value.Packs)
        {
            if (imported.TryGetValue(pack.Id, out var version) && version >= pack.Version)
            {
                continue;
            }

            var download = await gateway.DownloadPackAsync(pack, cancellationToken).ConfigureAwait(false);
            if (download.IsFailure)
            {
                LogPackFailed(pack.Id, pack.Version, download.Error!.Code);
                failed.Add(pack.Id);
                continue;
            }

            await content.ImportPackAsync(download.Value, cancellationToken).ConfigureAwait(false);
            count++;
        }

        if (failed.Count == 0)
        {
            await state.SetAsync(EtagKey, manifest.Value.Etag, cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(new ContentRefreshReport(true, count, failed));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Pack {PackId} v{Version} could not be refreshed: {Code}")]
    private partial void LogPackFailed(string packId, int version, string code);
}

public sealed record SyncReport(bool Online, ProgressSyncReport? Progress, SettingsSyncReport? Settings, ContentRefreshReport? Content, string? Error);

/// <summary>One sync pass in the order that matters for the learner: account, progress, settings, content. Failures of one step do not stop the next.</summary>
public sealed partial class SyncUseCase(ISyncGateway gateway, ILearnerContext learner, ISyncState state, SyncProgressUseCase progress, SyncSettingsUseCase settings, RefreshContentUseCase content, IClock clock, ILogger<SyncUseCase> logger)
{
    public const string AccountKey = "account.id";
    public const string LastSyncKey = "sync.lastAt";

    public async Task<SyncReport> ExecuteAsync(CancellationToken cancellationToken)
    {
        var learnerId = await learner.GetLearnerIdAsync(cancellationToken).ConfigureAwait(false);
        var account = await gateway.EnsureAccountAsync(learnerId, cancellationToken).ConfigureAwait(false);
        if (account.IsFailure)
        {
            LogStepFailed("account", account.Error!.Code);
            return new SyncReport(false, null, null, null, account.Error.Code);
        }

        await state.SetAsync(AccountKey, account.Value.Value.ToString("D"), cancellationToken).ConfigureAwait(false);

        var progressResult = await progress.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        if (progressResult.IsFailure)
        {
            LogStepFailed("progress", progressResult.Error!.Code);
        }

        var settingsResult = await settings.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        if (settingsResult.IsFailure)
        {
            LogStepFailed("settings", settingsResult.Error!.Code);
        }

        var contentResult = await content.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        if (contentResult.IsFailure)
        {
            LogStepFailed("content", contentResult.Error!.Code);
        }

        await state.SetAsync(LastSyncKey, clock.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
        var error = progressResult.Error?.Code ?? settingsResult.Error?.Code ?? contentResult.Error?.Code;
        return new SyncReport(true, progressResult.IsSuccess ? progressResult.Value : null, settingsResult.IsSuccess ? settingsResult.Value : null, contentResult.IsSuccess ? contentResult.Value : null, error);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sync step {Step} skipped: {Code}")]
    private partial void LogStepFailed(string step, string code);
}
