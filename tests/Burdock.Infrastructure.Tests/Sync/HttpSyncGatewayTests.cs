using Burdock.Application.Ports;
using Burdock.Contracts.Content;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Sync;
using Burdock.Domain.Vocabulary;
using Burdock.Infrastructure.Sync;
using Burdock.TestKit.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Burdock.Infrastructure.Tests.Sync;

/// <summary>The HTTP gateway against a scripted API (spec 09 sections 3.3-3.6): registration, token session, retries, error mapping and the v1 wire format.</summary>
public sealed class HttpSyncGatewayTests : IDisposable
{
    private static readonly LearnerId Learner = LearnerId.From(Guid.Parse("0199a4c0-0000-7000-8000-00000000cccc"));
    private static readonly Uri BaseUrl = new("http://api.test/");

    private readonly FakeApiHandler _server = new();
    private readonly HttpClient _http;
    private readonly FakeDeviceCredentials _credentials = new(Guid.Parse("0199a4c0-0000-7000-8000-0000000000a1"));
    private readonly FakeClock _clock = new(FakeApiHandler.T0, TimeZoneInfo.Utc);

    public HttpSyncGatewayTests()
    {
        _http = new HttpClient(_server, disposeHandler: false) { BaseAddress = BaseUrl };
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task First_contact_registers_the_installation_under_the_local_learner_id_and_keeps_a_refresh_token()
    {
        using var gateway = Gateway();

        var account = await gateway.EnsureAccountAsync(Learner, Ct);
        var again = await gateway.EnsureAccountAsync(Learner, Ct);

        account.IsSuccess.ShouldBeTrue(account.Error?.Code);
        account.Value.ShouldBe(Learner);
        again.Value.ShouldBe(Learner);
        _server.Registrations.ShouldBe(1);
        _server.Installations[_credentials.Credential.InstallationId].LearnerId.ShouldBe(Learner.Value);
        _server.Installations[_credentials.Credential.InstallationId].Secret.ShouldBe(_credentials.Credential.Secret);
        _credentials.RefreshToken.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task A_new_process_with_a_refresh_token_resumes_the_session_without_registering_again()
    {
        using (var first = Gateway())
        {
            (await first.EnsureAccountAsync(Learner, Ct)).IsSuccess.ShouldBeTrue();
        }

        var refreshBefore = _credentials.RefreshToken;
        using var second = Gateway();

        var account = await second.EnsureAccountAsync(Learner, Ct);

        account.Value.ShouldBe(Learner);
        _server.Registrations.ShouldBe(1);
        // First process: installation grant before registration (refused), registration, installation grant; second process: one refresh grant.
        _server.TokenRequests.ShouldBe(3);
        _credentials.RefreshToken.ShouldNotBe(refreshBefore);
    }

    [Fact]
    public async Task A_rejected_access_token_is_refreshed_once_and_an_expiring_one_proactively()
    {
        using var gateway = Gateway();
        (await gateway.EnsureAccountAsync(Learner, Ct)).IsSuccess.ShouldBeTrue();
        _server.RevokeAccessTokens();

        var push = await gateway.PushEventsAsync([Event("0199a4c0-0000-7000-8000-000000000e01", "{\"n\":1}")], Ct);

        push.IsSuccess.ShouldBeTrue(push.Error?.Code);
        push.Value.Accepted.Count.ShouldBe(1);
        _server.Unauthorized.ShouldBe(1);
        var tokenRequests = _server.TokenRequests;

        _clock.Advance(TimeSpan.FromMinutes(11));
        (await gateway.PullEventsAsync(0, null, 10, Ct)).IsSuccess.ShouldBeTrue();

        _server.Unauthorized.ShouldBe(1);
        _server.TokenRequests.ShouldBe(tokenRequests + 1);
    }

    [Fact]
    public async Task An_installation_conflict_rotates_the_credential_and_registers_the_new_one()
    {
        var before = _credentials.Credential.InstallationId;
        _server.PreRegister(before, "someone-elses-secret", Guid.NewGuid());
        using var gateway = Gateway();

        var account = await gateway.EnsureAccountAsync(Learner, Ct);

        account.IsSuccess.ShouldBeTrue(account.Error?.Code);
        account.Value.ShouldBe(Learner);
        _credentials.Credential.InstallationId.ShouldNotBe(before);
        _server.Installations[_credentials.Credential.InstallationId].LearnerId.ShouldBe(Learner.Value);
    }

    [Fact]
    public async Task A_taken_learner_id_is_a_conflict_the_app_can_show()
    {
        _server.TakenLearnerIds.Add(Learner.Value);
        using var gateway = Gateway();

        var account = await gateway.EnsureAccountAsync(Learner, Ct);

        account.IsFailure.ShouldBeTrue();
        account.Error!.Code.ShouldBe("sync.learner_id_taken");
        account.Error.Kind.ShouldBe(ErrorKind.Conflict);
    }

    [Fact]
    public async Task Transport_failures_rate_limits_and_server_errors_map_to_their_codes()
    {
        using var gateway = Gateway();
        _server.Offline = true;
        var offline = await gateway.EnsureAccountAsync(Learner, Ct);
        _server.Offline = false;
        (await gateway.EnsureAccountAsync(Learner, Ct)).IsSuccess.ShouldBeTrue();

        _server.FailNextAuthorizedCallWith = System.Net.HttpStatusCode.TooManyRequests;
        var limited = await gateway.GetSettingsAsync(null, Ct);
        _server.FailNextAuthorizedCallWith = System.Net.HttpStatusCode.InternalServerError;
        var broken = await gateway.GetSettingsAsync(null, Ct);

        offline.Error!.Code.ShouldBe(HttpSyncGateway.OfflineCode);
        offline.Error.Kind.ShouldBe(ErrorKind.Unavailable);
        limited.Error!.Code.ShouldBe("sync.rate_limited");
        broken.Error!.Code.ShouldBe("sync.server_error");
    }

    [Fact]
    public async Task Progress_settings_manifest_and_packs_travel_in_the_v1_contracts()
    {
        using var gateway = Gateway();
        (await gateway.EnsureAccountAsync(Learner, Ct)).IsSuccess.ShouldBeTrue();
        var mine = _credentials.Credential.InstallationId;
        var first = Event("0199a4c0-0000-7000-8000-000000000e01", "{\"cardId\":\"0199a4c0-0000-7000-8000-0000000000c1\",\"rating\":3}") with { InstallationId = mine };
        var second = Event("0199a4c0-0000-7000-8000-000000000e02", "{\"lexemeId\":\"0199a500-0000-7000-8000-000000000101\"}") with { InstallationId = mine, Type = ProgressEventTypes.CardIntroduced };

        var push = (await gateway.PushEventsAsync([first, second], Ct)).Value;
        var repeat = (await gateway.PushEventsAsync([first], Ct)).Value;
        var own = (await gateway.PullEventsAsync(0, mine, 100, Ct)).Value;
        var page = (await gateway.PullEventsAsync(0, null, 1, Ct)).Value;
        var rest = (await gateway.PullEventsAsync(page.NextSince, null, 100, Ct)).Value;

        push.Accepted.ShouldBe([first.Id, second.Id]);
        push.ServerSeq.ShouldBe(2);
        repeat.Duplicates.ShouldBe([first.Id]);
        own.Events.ShouldBeEmpty();
        own.HasMore.ShouldBeFalse();
        page.Events.Count.ShouldBe(1);
        page.HasMore.ShouldBeTrue();
        page.NextSince.ShouldBe(1);
        page.Events[0].Event.ShouldBe(first);
        rest.Events.Single().Event.ShouldBe(second);
        rest.HasMore.ShouldBeFalse();

        var put = (await gateway.PutSettingsAsync([new SettingsEntry("settings.level", "\"B1\"", FakeApiHandler.T0, "dev")], Ct)).Value;
        var get = (await gateway.GetSettingsAsync(null, Ct)).Value;
        var unchanged = (await gateway.GetSettingsAsync(put.Etag, Ct)).Value;

        put.Entries["settings.level"].ShouldBe(new SettingsEntry("settings.level", "\"B1\"", FakeApiHandler.T0, "dev"));
        put.Etag.ShouldNotBeNullOrEmpty();
        get.ShouldNotBeNull();
        get.Etag.ShouldBe(put.Etag);
        get.Entries["settings.level"].ClientId.ShouldBe("dev");
        unchanged.ShouldBeNull();

        _server.PackObject = FakeApiHandler.Gzip(Document());
        var manifest = (await gateway.GetManifestAsync(LanguagePair.UkrainianToEnglish, null, Ct)).Value;
        var notModified = (await gateway.GetManifestAsync(LanguagePair.UkrainianToEnglish, manifest!.Etag, Ct)).Value;
        var pack = await gateway.DownloadPackAsync(manifest.Packs.Single(), Ct);
        var corrupt = await gateway.DownloadPackAsync(manifest.Packs.Single() with { Sha256 = "00" }, Ct);

        manifest.Etag.ShouldBe(_server.ManifestEtag);
        manifest.Packs.Single().ShouldBe(new RemotePack(FakeApiHandler.PackId, "A2", "travel", 1, _server.PackSha256, _server.PackObject.Length, new Uri(FakeApiHandler.PackUrl), Required: false));
        notModified.ShouldBeNull();
        pack.IsSuccess.ShouldBeTrue(pack.Error?.Code);
        pack.Value.PackId.ShouldBe(FakeApiHandler.PackId);
        pack.Value.Version.ShouldBe(1);
        pack.Value.Entries.Single().Lexeme.Lemma.ShouldBe("journey");
        corrupt.Error!.Code.ShouldBe("sync.pack_corrupt");
    }

    public void Dispose()
    {
        _http.Dispose();
        _server.Dispose();
    }

    private HttpSyncGateway Gateway() => new(_http, new SyncOptions(BaseUrl, "android", "1.0.0", "uk"), _credentials, _clock, NullLogger<HttpSyncGateway>.Instance);

    private static SyncEvent Event(string id, string payload)
        => new(Guid.Parse(id), ProgressEventTypes.ReviewLog, FakeApiHandler.T0, new DateOnly(2026, 10, 11), null, payload);

    private static ContentPackDocument Document() => new(
        ContentPackDocument.CurrentSchemaVersion,
        FakeApiHandler.PackId,
        "uk-en",
        "A2",
        "travel",
        1,
        FakeApiHandler.T0,
        new ContentProvenanceDocument("test", "published", null, null, []),
        [new ContentEntryDocument(Guid.Parse("0199a500-0000-7000-8000-000000000101"), "journey", "noun", "A2", ["travel"], "подорож", [], ["journey"], null, null, "A long journey.", "Довга подорож.", null)]);
}
