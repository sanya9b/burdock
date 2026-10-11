using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Burdock.Contracts.Content;
using Burdock.Contracts.V1;
using Burdock.Contracts.V1.Content;
using Burdock.Contracts.V1.Identity;
using Burdock.Contracts.V1.Progress;
using Burdock.Contracts.V1.Settings;
using Burdock.Domain.Sync;

namespace Burdock.Infrastructure.Tests.Sync;

/// <summary>
/// Scripted stand-in for the API over the real routes and v1 contracts: registration with the proposed learner id,
/// the installation and refresh grants, bearer checks, idempotent progress batches, LWW settings with ETags, a manifest
/// and the pack store. Serialises with ASP.NET Core's web defaults so the wire format matches the host.
/// </summary>
internal sealed class FakeApiHandler : HttpMessageHandler
{
    public static readonly DateTimeOffset T0 = new(2026, 10, 11, 9, 0, 0, TimeSpan.Zero);
    public const string PackId = "uk-en.a2.travel";
    public const string PackUrl = "http://api.test/v1/content/store/packs/v1/uk-en/a2/travel/uk-en.a2.travel-1.json.gz";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly Dictionary<Guid, (string Secret, Guid LearnerId)> _installations = [];
    private readonly Dictionary<string, Guid> _accessTokens = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (Guid Installation, Guid Learner)> _refreshTokens = new(StringComparer.Ordinal);
    private readonly List<(long Seq, Guid Learner, ProgressEventDto Event)> _events = [];
    private Dictionary<string, SettingsEntry> _settings = new(StringComparer.Ordinal);
    private int _tokenCounter;

    public bool Offline { get; set; }

    public HttpStatusCode? FailNextAuthorizedCallWith { get; set; }

    public HashSet<Guid> TakenLearnerIds { get; } = [];

    public int Registrations { get; private set; }

    public int TokenRequests { get; private set; }

    public int Unauthorized { get; private set; }

    public byte[] PackObject { get; set; } = [];

    public string ManifestEtag { get; set; } = "W/\"m1\"";

    public IReadOnlyDictionary<Guid, (string Secret, Guid LearnerId)> Installations => _installations;

    public string PackSha256 => Convert.ToHexStringLower(SHA256.HashData(PackObject));

    public void PreRegister(Guid installationId, string secret, Guid learnerId) => _installations[installationId] = (secret, learnerId);

    public void RevokeAccessTokens() => _accessTokens.Clear();

    public static byte[] Gzip(ContentPackDocument document)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(JsonSerializer.SerializeToUtf8Bytes(document, ContentPackJsonContext.Default.ContentPackDocument));
        }

        return output.ToArray();
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (Offline)
        {
            throw new HttpRequestException(HttpRequestError.ConnectionError, "offline");
        }

        var path = request.RequestUri!.AbsolutePath;
        if (request.Method == HttpMethod.Post && path == ApiRoutes.Identity.Anonymous)
        {
            return await RegisterAsync(request, cancellationToken).ConfigureAwait(false);
        }

        if (request.Method == HttpMethod.Post && path == ApiRoutes.Connect.Token)
        {
            return await TokenAsync(request, cancellationToken).ConfigureAwait(false);
        }

        if (request.Method == HttpMethod.Get && path.StartsWith("/v1/content/store/", StringComparison.Ordinal))
        {
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(PackObject) { Headers = { ContentType = new MediaTypeHeaderValue("application/gzip") } } };
        }

        if (request.Headers.Authorization is not { Scheme: "Bearer", Parameter: { } bearer } || !_accessTokens.TryGetValue(bearer, out var learner))
        {
            Unauthorized++;
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }

        if (FailNextAuthorizedCallWith is { } status)
        {
            FailNextAuthorizedCallWith = null;
            return new HttpResponseMessage(status);
        }

        var query = ParseQuery(request.RequestUri.Query);
        return (request.Method.Method, path) switch
        {
            ("GET", ApiRoutes.Identity.Me) => Ok(new AccountDto(learner, "Reppy", "Active", T0)),
            ("POST", ApiRoutes.Progress.Events) => await AppendAsync(request, learner, cancellationToken).ConfigureAwait(false),
            ("GET", ApiRoutes.Progress.Events) => Read(learner, query),
            ("GET", ApiRoutes.Settings.Document) => GetSettings(request),
            ("PUT", ApiRoutes.Settings.Document) => await PutSettingsAsync(request, cancellationToken).ConfigureAwait(false),
            ("GET", ApiRoutes.Content.Manifest) => Manifest(request),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        };
    }

    private async Task<HttpResponseMessage> RegisterAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = (await request.Content!.ReadFromJsonAsync<RegisterInstallationDto>(Json, cancellationToken).ConfigureAwait(false))!;
        if (_installations.TryGetValue(body.InstallationId, out var existing))
        {
            if (!string.Equals(existing.Secret, body.InstallationSecret, StringComparison.Ordinal))
            {
                return Problem(HttpStatusCode.Conflict, "installation_conflict");
            }

            Registrations++;
            return Ok(new AccountDto(existing.LearnerId, "Reppy", "Active", T0));
        }

        var learnerId = body.LearnerId is { } proposed && proposed != Guid.Empty ? proposed : Guid.NewGuid();
        if (TakenLearnerIds.Contains(learnerId) || _installations.Values.Any(i => i.LearnerId == learnerId))
        {
            return Problem(HttpStatusCode.Conflict, "learner_id_taken");
        }

        _installations[body.InstallationId] = (body.InstallationSecret, learnerId);
        Registrations++;
        return Ok(new AccountDto(learnerId, "Reppy", "Active", T0), HttpStatusCode.Created);
    }

    private async Task<HttpResponseMessage> TokenAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        TokenRequests++;
        var form = ParseQuery("?" + await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        if (!string.Equals(form.GetValueOrDefault("client_id"), ApiRoutes.Connect.ClientId, StringComparison.Ordinal))
        {
            return OAuthError("invalid_client", "client_unknown");
        }

        var grant = form.GetValueOrDefault("grant_type");
        if (string.Equals(grant, ApiRoutes.Connect.InstallationGrantType, StringComparison.Ordinal))
        {
            if (!Guid.TryParse(form.GetValueOrDefault("installation_id"), out var installationId)
                || !_installations.TryGetValue(installationId, out var installation)
                || !string.Equals(installation.Secret, form.GetValueOrDefault("installation_secret"), StringComparison.Ordinal))
            {
                return OAuthError("invalid_grant", "installation_unknown");
            }

            return Issue(installationId, installation.LearnerId);
        }

        if (string.Equals(grant, "refresh_token", StringComparison.Ordinal))
        {
            var presented = form.GetValueOrDefault("refresh_token") ?? string.Empty;
            if (!_refreshTokens.Remove(presented, out var owner))
            {
                return OAuthError("invalid_grant", "refresh_token_invalid");
            }

            return Issue(owner.Installation, owner.Learner);
        }

        return OAuthError("unsupported_grant_type", "grant_type_unsupported");
    }

    private HttpResponseMessage Issue(Guid installationId, Guid learnerId)
    {
        _tokenCounter++;
        var access = $"at-{_tokenCounter}";
        var refresh = $"rt-{_tokenCounter}";
        _accessTokens[access] = learnerId;
        _refreshTokens[refresh] = (installationId, learnerId);
        return Ok(new Dictionary<string, object> { ["access_token"] = access, ["refresh_token"] = refresh, ["token_type"] = "Bearer", ["expires_in"] = 600 });
    }

    private async Task<HttpResponseMessage> AppendAsync(HttpRequestMessage request, Guid learner, CancellationToken cancellationToken)
    {
        var body = (await request.Content!.ReadFromJsonAsync<PostEventsDto>(Json, cancellationToken).ConfigureAwait(false))!;
        var accepted = new List<Guid>();
        var duplicates = new List<Guid>();
        foreach (var e in body.Events)
        {
            if (_events.Any(s => s.Learner == learner && s.Event.Id == e.Id))
            {
                duplicates.Add(e.Id);
                continue;
            }

            _events.Add((_events.Count(s => s.Learner == learner) + 1, learner, e));
            accepted.Add(e.Id);
        }

        return Ok(new PostEventsResultDto(accepted, duplicates, [], _events.Count(s => s.Learner == learner)));
    }

    private HttpResponseMessage Read(Guid learner, Dictionary<string, string> query)
    {
        var since = long.Parse(query.GetValueOrDefault("since") ?? "0", System.Globalization.CultureInfo.InvariantCulture);
        var limit = int.Parse(query.GetValueOrDefault("limit") ?? "500", System.Globalization.CultureInfo.InvariantCulture);
        Guid? exclude = Guid.TryParse(query.GetValueOrDefault("excludeInstallation"), out var g) ? g : null;
        var matching = _events.Where(s => s.Learner == learner && s.Seq > since && (exclude is null || s.Event.InstallationId != exclude)).Take(limit + 1).ToList();
        var page = matching.Take(limit).Select(s => new SyncedEventDto(s.Seq, s.Event)).ToArray();
        return Ok(new EventsPageDto(page, page.Length == 0 ? since : page[^1].ServerSeq, matching.Count > limit));
    }

    private HttpResponseMessage GetSettings(HttpRequestMessage request)
    {
        var etag = SettingsDocumentRules.ComputeEtag(_settings.Values);
        if (request.Headers.IfNoneMatch.Any(t => string.Equals(t.Tag.Trim('"'), etag, StringComparison.Ordinal)))
        {
            return new HttpResponseMessage(HttpStatusCode.NotModified);
        }

        var response = Ok(ToDto(etag));
        response.Headers.ETag = new EntityTagHeaderValue("\"" + etag + "\"");
        return response;
    }

    private async Task<HttpResponseMessage> PutSettingsAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = (await request.Content!.ReadFromJsonAsync<PutSettingsDto>(Json, cancellationToken).ConfigureAwait(false))!;
        var incoming = body.Entries.Select(kv => new SettingsEntry(kv.Key, kv.Value.Value.GetRawText(), kv.Value.UpdatedAt, kv.Value.ClientId));
        var (merged, _) = SettingsDocumentRules.Merge(_settings, incoming);
        _settings = new Dictionary<string, SettingsEntry>(merged, StringComparer.Ordinal);
        return Ok(ToDto(SettingsDocumentRules.ComputeEtag(_settings.Values)));
    }

    private HttpResponseMessage Manifest(HttpRequestMessage request)
    {
        if (request.Headers.TryGetValues("If-None-Match", out var tags) && tags.Contains(ManifestEtag, StringComparer.Ordinal))
        {
            return new HttpResponseMessage(HttpStatusCode.NotModified);
        }

        var pack = new ContentPackDto(PackId, "A2", "travel", 1, PackSha256, PackObject.Length, new Uri(PackUrl), "1.0.0", Required: false, null, []);
        var response = Ok(new ContentManifestDto(1, 7, T0, "uk-en", [pack]));
        response.Headers.ETag = EntityTagHeaderValue.Parse(ManifestEtag);
        return response;
    }

    private SettingsDocumentDto ToDto(string etag)
    {
        var entries = new Dictionary<string, SettingsEntryDto>(StringComparer.Ordinal);
        foreach (var (key, entry) in _settings)
        {
            using var document = JsonDocument.Parse(entry.ValueJson);
            entries[key] = new SettingsEntryDto(document.RootElement.Clone(), entry.UpdatedAt, entry.ClientId);
        }

        return new SettingsDocumentDto(1, entries, etag);
    }

    private static HttpResponseMessage Ok<T>(T value, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = JsonContent.Create(value, options: Json) };

    private static HttpResponseMessage Problem(HttpStatusCode status, string code)
    {
        var problem = new Dictionary<string, object> { ["type"] = "https://burdock.org/problems/" + code, ["title"] = code, ["status"] = (int)status, ["code"] = code };
        return new HttpResponseMessage(status) { Content = JsonContent.Create(problem, new MediaTypeHeaderValue("application/problem+json"), Json) };
    }

    private static HttpResponseMessage OAuthError(string error, string description)
        => new(HttpStatusCode.BadRequest) { Content = JsonContent.Create(new Dictionary<string, string> { ["error"] = error, ["error_description"] = description }, options: Json) };

    private static Dictionary<string, string> ParseQuery(string query)
        => query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(p => Uri.UnescapeDataString(p[0]), p => p.Length > 1 ? Uri.UnescapeDataString(p[1].Replace('+', ' ')) : string.Empty, StringComparer.Ordinal);
}
