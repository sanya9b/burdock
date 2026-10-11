using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Burdock.Application.Ports;
using Burdock.Contracts.Content;
using Burdock.Contracts.V1;
using Burdock.Contracts.V1.Identity;
using Burdock.Contracts.V1.Progress;
using Burdock.Contracts.V1.Settings;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Sync;
using Burdock.Domain.Vocabulary;
using Burdock.Infrastructure.Content;
using Microsoft.Extensions.Logging;

namespace Burdock.Infrastructure.Sync;

/// <summary>
/// The server over HTTP (spec 09 sections 3.3-3.6). Owns the token session: refresh token in SecureStorage, access token
/// in memory, the installation grant as fallback and registration when the server does not know the installation.
/// Transport trouble becomes <c>sync.offline</c>; the server's problem codes come back as <c>sync.&lt;code&gt;</c>.
/// </summary>
public sealed partial class HttpSyncGateway(HttpClient http, SyncOptions options, IDeviceCredentials credentials, IClock clock, ILogger<HttpSyncGateway> logger) : ISyncGateway, IDisposable
{
    public const string OfflineCode = "sync.offline";
    public const string UnauthorizedCode = "sync.unauthorized";
    private const string InstallationConflictCode = "sync.installation_conflict";
    private static readonly TimeSpan ExpiryMargin = TimeSpan.FromSeconds(30);

    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _accessTokenExpiresAt;
    private LearnerId? _account;

    public Task<Result<LearnerId>> EnsureAccountAsync(LearnerId proposedLearnerId, CancellationToken cancellationToken) => GuardAsync(async () =>
    {
        if (_account is { } known)
        {
            return Result.Success(known);
        }

        var token = await AcquireTokenAsync(forceRefresh: false, cancellationToken).ConfigureAwait(false);
        if (token.Error is not null)
        {
            return Result.Failure<LearnerId>(token.Error);
        }

        if (token.AccessToken is null)
        {
            var registered = await RegisterAsync(proposedLearnerId, cancellationToken).ConfigureAwait(false);
            if (registered.IsFailure)
            {
                return registered;
            }

            token = await AcquireTokenAsync(forceRefresh: true, cancellationToken).ConfigureAwait(false);
            if (token.AccessToken is null)
            {
                return Result.Failure<LearnerId>(token.Error ?? Unauthorized());
            }

            _account = registered.Value;
            return registered;
        }

        var (response, error) = await SendAuthorizedAsync(() => new HttpRequestMessage(HttpMethod.Get, Relative(ApiRoutes.Identity.Me)), cancellationToken).ConfigureAwait(false);
        if (response is null)
        {
            return Result.Failure<LearnerId>(error!);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                return Result.Failure<LearnerId>(await ErrorFromAsync(response, cancellationToken).ConfigureAwait(false));
            }

            var me = await response.Content.ReadFromJsonAsync(SyncJsonContext.Default.AccountDto, cancellationToken).ConfigureAwait(false) ?? throw new JsonException("Empty account document.");
            _account = LearnerId.From(me.UserId);
            return Result.Success(_account.Value);
        }
    }, cancellationToken);

    public Task<Result<PushOutcome>> PushEventsAsync(IReadOnlyList<SyncEvent> events, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(events);
        return GuardAsync(async () =>
        {
            var body = new PostEventsDto(events.Select(e => new ProgressEventDto(e.Id, e.Type, 1, e.OccurredAt, e.LocalDate, e.InstallationId, ParseJson(e.PayloadJson))).ToArray());
            var (response, error) = await SendAuthorizedAsync(() => new HttpRequestMessage(HttpMethod.Post, Relative(ApiRoutes.Progress.Events)) { Content = JsonContent.Create(body, SyncJsonContext.Default.PostEventsDto) }, cancellationToken).ConfigureAwait(false);
            if (response is null)
            {
                return Result.Failure<PushOutcome>(error!);
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    return Result.Failure<PushOutcome>(await ErrorFromAsync(response, cancellationToken).ConfigureAwait(false));
                }

                var result = await response.Content.ReadFromJsonAsync(SyncJsonContext.Default.PostEventsResultDto, cancellationToken).ConfigureAwait(false) ?? throw new JsonException("Empty batch result.");
                return Result.Success(new PushOutcome(result.Accepted, result.Duplicates, result.Rejected.Select(r => new RejectedSyncEvent(r.Id, r.Code)).ToArray(), result.ServerSeq));
            }
        }, cancellationToken);
    }

    public Task<Result<PullPage>> PullEventsAsync(long since, Guid? excludeInstallation, int limit, CancellationToken cancellationToken) => GuardAsync(async () =>
    {
        var path = string.Create(CultureInfo.InvariantCulture, $"{ApiRoutes.Progress.Events}?since={since}&limit={limit}");
        if (excludeInstallation is { } installation)
        {
            path += string.Create(CultureInfo.InvariantCulture, $"&excludeInstallation={installation:D}");
        }

        var (response, error) = await SendAuthorizedAsync(() => new HttpRequestMessage(HttpMethod.Get, Relative(path)), cancellationToken).ConfigureAwait(false);
        if (response is null)
        {
            return Result.Failure<PullPage>(error!);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                return Result.Failure<PullPage>(await ErrorFromAsync(response, cancellationToken).ConfigureAwait(false));
            }

            var page = await response.Content.ReadFromJsonAsync(SyncJsonContext.Default.EventsPageDto, cancellationToken).ConfigureAwait(false) ?? throw new JsonException("Empty events page.");
            var events = page.Events.Select(s => new SyncedEvent(s.ServerSeq, new SyncEvent(s.Event.Id, s.Event.Type, s.Event.OccurredAt, s.Event.LocalDate, s.Event.InstallationId, s.Event.Payload.GetRawText()))).ToArray();
            return Result.Success(new PullPage(events, page.NextSince, page.HasMore));
        }
    }, cancellationToken);

    public Task<Result<RemoteSettings?>> GetSettingsAsync(string? etag, CancellationToken cancellationToken) => GuardAsync(async () =>
    {
        var (response, error) = await SendAuthorizedAsync(() =>
        {
            var request = new HttpRequestMessage(HttpMethod.Get, Relative(ApiRoutes.Settings.Document));
            if (!string.IsNullOrEmpty(etag))
            {
                request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(Quote(etag)));
            }

            return request;
        }, cancellationToken).ConfigureAwait(false);
        if (response is null)
        {
            return Result.Failure<RemoteSettings?>(error!);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                return Result.Success<RemoteSettings?>(null);
            }

            if (!response.IsSuccessStatusCode)
            {
                return Result.Failure<RemoteSettings?>(await ErrorFromAsync(response, cancellationToken).ConfigureAwait(false));
            }

            var document = await response.Content.ReadFromJsonAsync(SyncJsonContext.Default.SettingsDocumentDto, cancellationToken).ConfigureAwait(false) ?? throw new JsonException("Empty settings document.");
            return Result.Success<RemoteSettings?>(ToRemote(document));
        }
    }, cancellationToken);

    public Task<Result<RemoteSettings>> PutSettingsAsync(IReadOnlyList<SettingsEntry> entries, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return GuardAsync(async () =>
        {
            var body = new PutSettingsDto(SettingsDocumentRules.CurrentSchemaVersion, entries.ToDictionary(e => e.Key, e => new SettingsEntryDto(ParseJson(e.ValueJson), e.UpdatedAt, e.ClientId), StringComparer.Ordinal));
            var (response, error) = await SendAuthorizedAsync(() => new HttpRequestMessage(HttpMethod.Put, Relative(ApiRoutes.Settings.Document)) { Content = JsonContent.Create(body, SyncJsonContext.Default.PutSettingsDto) }, cancellationToken).ConfigureAwait(false);
            if (response is null)
            {
                return Result.Failure<RemoteSettings>(error!);
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    return Result.Failure<RemoteSettings>(await ErrorFromAsync(response, cancellationToken).ConfigureAwait(false));
                }

                var document = await response.Content.ReadFromJsonAsync(SyncJsonContext.Default.SettingsDocumentDto, cancellationToken).ConfigureAwait(false) ?? throw new JsonException("Empty settings document.");
                return Result.Success(ToRemote(document));
            }
        }, cancellationToken);
    }

    public Task<Result<RemoteManifest?>> GetManifestAsync(LanguagePair pair, string? etag, CancellationToken cancellationToken) => GuardAsync(async () =>
    {
        var path = $"{ApiRoutes.Content.Manifest}?pair={Uri.EscapeDataString(pair.ToString())}";
        var (response, error) = await SendAuthorizedAsync(() =>
        {
            var request = new HttpRequestMessage(HttpMethod.Get, Relative(path));
            if (!string.IsNullOrEmpty(etag))
            {
                request.Headers.TryAddWithoutValidation("If-None-Match", etag);
            }

            return request;
        }, cancellationToken).ConfigureAwait(false);
        if (response is null)
        {
            return Result.Failure<RemoteManifest?>(error!);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                return Result.Success<RemoteManifest?>(null);
            }

            if (!response.IsSuccessStatusCode)
            {
                return Result.Failure<RemoteManifest?>(await ErrorFromAsync(response, cancellationToken).ConfigureAwait(false));
            }

            var manifest = await response.Content.ReadFromJsonAsync(SyncJsonContext.Default.ContentManifestDto, cancellationToken).ConfigureAwait(false) ?? throw new JsonException("Empty manifest.");
            var packs = manifest.Packs.Select(p => new RemotePack(p.Id, p.Level, p.Topic, p.Version, p.Sha256, p.SizeBytes, p.Url, p.Required)).ToArray();
            var fingerprint = response.Headers.ETag?.ToString() ?? manifest.ManifestVersion.ToString(CultureInfo.InvariantCulture);
            return Result.Success<RemoteManifest?>(new RemoteManifest(fingerprint, packs));
        }
    }, cancellationToken);

    public Task<Result<ImportedContentPack>> DownloadPackAsync(RemotePack pack, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pack);
        return GuardAsync(async () =>
        {
            // Pack objects are public, immutable and addressed by an unlisted path: no token, integrity by hash (spec 09 section 3.4).
            var url = pack.Url.IsAbsoluteUri ? pack.Url : new Uri(options.BaseUrl, pack.Url);
            using var response = await http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Result.Failure<ImportedContentPack>(await ErrorFromAsync(response, cancellationToken).ConfigureAwait(false));
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
            if (!string.Equals(sha256, pack.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                LogPackCorrupt(pack.Id, pack.Version);
                return Result.Failure<ImportedContentPack>(DomainError.Validation("sync.pack_corrupt", "Errors_Sync_PackCorrupt"));
            }

            using var compressed = new MemoryStream(bytes, writable: false);
            using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
            var document = await JsonSerializer.DeserializeAsync(gzip, ContentPackJsonContext.Default.ContentPackDocument, cancellationToken).ConfigureAwait(false);
            if (document is null)
            {
                return Result.Failure<ImportedContentPack>(DomainError.Validation("sync.pack_empty", "Errors_Sync_PackEmpty"));
            }

            var mapped = ContentPackMapper.ToDomain(document);
            if (mapped.IsFailure)
            {
                return mapped;
            }

            return string.Equals(mapped.Value.PackId, pack.Id, StringComparison.Ordinal)
                ? mapped
                : Result.Failure<ImportedContentPack>(DomainError.Validation("sync.pack_mismatch", "Errors_Sync_PackMismatch"));
        }, cancellationToken);
    }

    public void Dispose() => _tokenLock.Dispose();

    /// <summary>POST /v1/identity/anonymous with the local learner id as the proposed account id; an installation conflict rotates the credential once.</summary>
    private async Task<Result<LearnerId>> RegisterAsync(LearnerId proposedLearnerId, CancellationToken cancellationToken)
    {
        var credential = await credentials.GetOrCreateInstallationAsync(cancellationToken).ConfigureAwait(false);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var body = new RegisterInstallationDto(credential.InstallationId, credential.Secret, options.Platform, options.AppVersion, options.Locale, proposedLearnerId.Value);
            using var response = await http.PostAsJsonAsync(Relative(ApiRoutes.Identity.Anonymous), body, SyncJsonContext.Default.RegisterInstallationDto, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                var account = await response.Content.ReadFromJsonAsync(SyncJsonContext.Default.AccountDto, cancellationToken).ConfigureAwait(false) ?? throw new JsonException("Empty account document.");
                LogRegistered(response.StatusCode == HttpStatusCode.Created);
                return Result.Success(LearnerId.From(account.UserId));
            }

            var error = await ErrorFromAsync(response, cancellationToken).ConfigureAwait(false);
            if (attempt == 0 && string.Equals(error.Code, InstallationConflictCode, StringComparison.Ordinal))
            {
                credential = await credentials.ResetInstallationAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            return Result.Failure<LearnerId>(error);
        }

        return Result.Failure<LearnerId>(DomainError.Conflict(InstallationConflictCode, "Errors_Sync_InstallationConflict"));
    }

    /// <summary>Sends with a bearer token; one retry with a refreshed token after 401.</summary>
    private async Task<(HttpResponseMessage? Response, DomainError? Error)> SendAuthorizedAsync(Func<HttpRequestMessage> makeRequest, CancellationToken cancellationToken)
    {
        var token = await AcquireTokenAsync(forceRefresh: false, cancellationToken).ConfigureAwait(false);
        if (token.AccessToken is null)
        {
            return (null, token.Error ?? Unauthorized());
        }

        var response = await SendAsync(makeRequest, token.AccessToken, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return (response, null);
        }

        response.Dispose();
        token = await AcquireTokenAsync(forceRefresh: true, cancellationToken).ConfigureAwait(false);
        if (token.AccessToken is null)
        {
            return (null, token.Error ?? Unauthorized());
        }

        return (await SendAsync(makeRequest, token.AccessToken, cancellationToken).ConfigureAwait(false), null);
    }

    private async Task<HttpResponseMessage> SendAsync(Func<HttpRequestMessage> makeRequest, string accessToken, CancellationToken cancellationToken)
    {
        using var request = makeRequest();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await http.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Cached access token, else the refresh grant, else the installation grant. No token and no error means the server
    /// does not know this installation yet and the caller should register it.
    /// </summary>
    private async Task<TokenAttempt> AcquireTokenAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        if (!forceRefresh && HasFreshAccessToken())
        {
            return new TokenAttempt(_accessToken, null);
        }

        await _tokenLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!forceRefresh && HasFreshAccessToken())
            {
                return new TokenAttempt(_accessToken, null);
            }

            var refreshToken = await credentials.GetRefreshTokenAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(refreshToken))
            {
                var refreshed = await ExchangeAsync(new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["grant_type"] = "refresh_token",
                    ["client_id"] = ApiRoutes.Connect.ClientId,
                    ["refresh_token"] = refreshToken,
                }, cancellationToken).ConfigureAwait(false);
                if (refreshed.Token is not null)
                {
                    return new TokenAttempt(await StoreAsync(refreshed.Token, cancellationToken).ConfigureAwait(false), null);
                }

                if (refreshed.Error is not null)
                {
                    return new TokenAttempt(null, refreshed.Error);
                }

                // invalid_grant: the refresh token was revoked or rotated away; fall back to the installation itself.
                await credentials.SetRefreshTokenAsync(null, cancellationToken).ConfigureAwait(false);
            }

            var credential = await credentials.GetOrCreateInstallationAsync(cancellationToken).ConfigureAwait(false);
            var exchanged = await ExchangeAsync(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["grant_type"] = ApiRoutes.Connect.InstallationGrantType,
                ["client_id"] = ApiRoutes.Connect.ClientId,
                ["installation_id"] = credential.InstallationId.ToString("D"),
                ["installation_secret"] = credential.Secret,
                ["app_version"] = options.AppVersion,
                ["scope"] = "offline_access",
            }, cancellationToken).ConfigureAwait(false);
            if (exchanged.Token is not null)
            {
                return new TokenAttempt(await StoreAsync(exchanged.Token, cancellationToken).ConfigureAwait(false), null);
            }

            return new TokenAttempt(null, exchanged.Error);
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private bool HasFreshAccessToken() => _accessToken is not null && clock.UtcNow < _accessTokenExpiresAt - ExpiryMargin;

    private async Task<TokenExchange> ExchangeAsync(Dictionary<string, string> form, CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(form);
        using var response = await http.PostAsync(Relative(ApiRoutes.Connect.Token), content, cancellationToken).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            var token = await response.Content.ReadFromJsonAsync(SyncJsonContext.Default.TokenResponseDto, cancellationToken).ConfigureAwait(false);
            return token?.AccessToken is null ? new TokenExchange(null, Unexpected("sync.token_malformed")) : new TokenExchange(token, null);
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var problem = await ReadQuietlyAsync(response, SyncJsonContext.Default.TokenErrorDto, cancellationToken).ConfigureAwait(false);
            if (string.Equals(problem?.Error, "invalid_grant", StringComparison.Ordinal))
            {
                LogGrantRefused(problem?.ErrorDescription ?? "invalid_grant");
                return new TokenExchange(null, null);
            }

            return new TokenExchange(null, DomainError.Validation("sync.token_rejected", "Errors_Sync_TokenRejected", new Dictionary<string, string>(StringComparer.Ordinal) { ["error"] = problem?.Error ?? string.Empty }));
        }

        return new TokenExchange(null, await ErrorFromAsync(response, cancellationToken).ConfigureAwait(false));
    }

    private async Task<string> StoreAsync(TokenResponseDto token, CancellationToken cancellationToken)
    {
        _accessToken = token.AccessToken;
        _accessTokenExpiresAt = clock.UtcNow.AddSeconds(token.ExpiresIn ?? 0);
        if (!string.IsNullOrEmpty(token.RefreshToken))
        {
            await credentials.SetRefreshTokenAsync(token.RefreshToken, cancellationToken).ConfigureAwait(false);
        }

        return token.AccessToken!;
    }

    private async Task<DomainError> ErrorFromAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return DomainError.Unavailable("sync.rate_limited", "Errors_Sync_RateLimited");
        }

        if (status >= 500)
        {
            LogServerError(status);
            return DomainError.Unavailable("sync.server_error", "Errors_Sync_ServerError");
        }

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return Unauthorized();
        }

        var problem = await ReadQuietlyAsync(response, SyncJsonContext.Default.ProblemDocument, cancellationToken).ConfigureAwait(false);
        var code = problem?.Code ?? problem?.Title;
        var data = new Dictionary<string, string>(StringComparer.Ordinal) { ["status"] = status.ToString(CultureInfo.InvariantCulture) };
        if (string.IsNullOrWhiteSpace(code))
        {
            return DomainError.Validation("sync.rejected", "Errors_Sync_Rejected", data);
        }

        LogRejected(status, code);
        return response.StatusCode == HttpStatusCode.Conflict
            ? DomainError.Conflict("sync." + code, "Errors_Sync_Rejected", data)
            : DomainError.Validation("sync." + code, "Errors_Sync_Rejected", data);
    }

    private static async Task<T?> ReadQuietlyAsync<T>(HttpResponseMessage response, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            return await response.Content.ReadFromJsonAsync(typeInfo, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private async Task<Result<T>> GuardAsync<T>(Func<Task<Result<T>>> action, CancellationToken cancellationToken)
    {
        try
        {
            return await action().ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            LogOffline(ex, "request");
            return Result.Failure<T>(Offline());
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            LogOffline(ex, "timeout");
            return Result.Failure<T>(Offline());
        }
        catch (IOException ex)
        {
            LogOffline(ex, "io");
            return Result.Failure<T>(Offline());
        }
        catch (JsonException ex)
        {
            LogProtocol(ex);
            return Result.Failure<T>(Unexpected("sync.protocol"));
        }
    }

    private static RemoteSettings ToRemote(SettingsDocumentDto document)
    {
        var entries = document.Entries.ToDictionary(kv => kv.Key, kv => new SettingsEntry(kv.Key, kv.Value.Value.GetRawText(), kv.Value.UpdatedAt, kv.Value.ClientId), StringComparer.Ordinal);
        return new RemoteSettings(entries, document.Etag);
    }

    private static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static Uri Relative(string path) => new(path, UriKind.Relative);

    private static string Quote(string etag) => etag.StartsWith('"') ? etag : "\"" + etag + "\"";

    private static DomainError Offline() => DomainError.Unavailable(OfflineCode, "Errors_Sync_Offline");

    private static DomainError Unauthorized() => DomainError.Forbidden(UnauthorizedCode, "Errors_Sync_Unauthorized");

    private static DomainError Unexpected(string code) => DomainError.Unexpected(code, "Errors_Sync_Unexpected");

    [LoggerMessage(Level = LogLevel.Information, Message = "Sync transport unavailable ({Detail})")]
    private partial void LogOffline(Exception exception, string detail);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Server answer could not be parsed")]
    private partial void LogProtocol(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Server answered {Status} with code {Code}")]
    private partial void LogRejected(int status, string code);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Server error {Status}")]
    private partial void LogServerError(int status);

    [LoggerMessage(Level = LogLevel.Information, Message = "Token grant refused: {Description}")]
    private partial void LogGrantRefused(string description);

    [LoggerMessage(Level = LogLevel.Information, Message = "Installation registered (created: {Created})")]
    private partial void LogRegistered(bool created);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Pack {PackId} v{Version} failed its SHA-256 check")]
    private partial void LogPackCorrupt(string packId, int version);

    private readonly record struct TokenAttempt(string? AccessToken, DomainError? Error);

    private readonly record struct TokenExchange(TokenResponseDto? Token, DomainError? Error);
}
