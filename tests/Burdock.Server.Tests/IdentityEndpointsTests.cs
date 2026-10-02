using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Burdock.Contracts.V1;
using Burdock.Contracts.V1.Identity;

namespace Burdock.Server.Tests;

/// <summary>Anonymous accounts and tokens (spec 09 §3.3.1, FR-09-1, FR-09-2) against a real PostgreSQL schema.</summary>
[Collection(PostgresBackedApi.Name)]
public sealed partial class IdentityEndpointsTests : IDisposable
{
    private readonly BurdockApiFactory _factory;
    private readonly HttpClient _client;

    public IdentityEndpointsTests(PostgresFixture postgres)
    {
        ArgumentNullException.ThrowIfNull(postgres);
        _factory = new BurdockApiFactory(postgres.ConnectionString);
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Registering_a_new_installation_creates_an_account_with_a_generated_nickname()
    {
        var installationId = Guid.NewGuid();
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        using var response = await _client.PostAsJsonAsync(new Uri(ApiRoutes.Identity.Anonymous, UriKind.Relative), new RegisterInstallationDto(installationId, secret, "android", "1.0.0", "uk"), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var account = await response.Content.ReadFromJsonAsync<AccountDto>(TestContext.Current.CancellationToken);
        account.ShouldNotBeNull();
        account.UserId.ShouldNotBe(Guid.Empty);
        account.Status.ShouldBe("Active");
        NicknamePattern().IsMatch(account.Nickname).ShouldBeTrue(account.Nickname);
    }

    [Fact]
    public async Task Repeating_the_registration_is_idempotent_and_a_wrong_secret_is_a_conflict()
    {
        var installationId = Guid.NewGuid();
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var body = new RegisterInstallationDto(installationId, secret, "ios", "1.0.0", null);

        using var first = await _client.PostAsJsonAsync(new Uri(ApiRoutes.Identity.Anonymous, UriKind.Relative), body, TestContext.Current.CancellationToken);
        using var second = await _client.PostAsJsonAsync(new Uri(ApiRoutes.Identity.Anonymous, UriKind.Relative), body with { AppVersion = "1.0.1" }, TestContext.Current.CancellationToken);
        using var wrong = await _client.PostAsJsonAsync(new Uri(ApiRoutes.Identity.Anonymous, UriKind.Relative), body with { InstallationSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) }, TestContext.Current.CancellationToken);

        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        var created = await first.Content.ReadFromJsonAsync<AccountDto>(TestContext.Current.CancellationToken);
        var repeated = await second.Content.ReadFromJsonAsync<AccountDto>(TestContext.Current.CancellationToken);
        repeated!.UserId.ShouldBe(created!.UserId);
        repeated.Nickname.ShouldBe(created.Nickname);
        wrong.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await wrong.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("installation_conflict");
    }

    [Theory]
    [InlineData("windows", "1.0.0", "identity.platform_invalid")]
    [InlineData("ios", "", "identity.app_version_invalid")]
    public async Task Invalid_registrations_are_validation_problems(string platform, string appVersion, string code)
    {
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        using var response = await _client.PostAsJsonAsync(new Uri(ApiRoutes.Identity.Anonymous, UriKind.Relative), new RegisterInstallationDto(Guid.NewGuid(), secret, platform, appVersion, null), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain(code);
    }

    [Fact]
    public async Task A_short_secret_is_rejected()
    {
        using var response = await _client.PostAsJsonAsync(new Uri(ApiRoutes.Identity.Anonymous, UriKind.Relative), new RegisterInstallationDto(Guid.NewGuid(), Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)), "ios", "1.0.0", null), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("identity.secret_invalid");
    }

    [Fact]
    public async Task The_installation_grant_issues_tokens_that_open_the_account()
    {
        var session = await BurdockApiFactory.SignUpAsync(_client);

        session.Tokens.TokenType.ShouldBe("Bearer");
        session.Tokens.AccessToken.ShouldNotBeNullOrEmpty();
        session.Tokens.RefreshToken.ShouldNotBeNullOrEmpty();
        session.Tokens.ExpiresIn.ShouldBeInRange(1, 600);

        using var request = BurdockApiFactory.Authorized(HttpMethod.Get, ApiRoutes.Identity.Me, session.Tokens.AccessToken);
        using var me = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        me.StatusCode.ShouldBe(HttpStatusCode.OK);
        var account = await me.Content.ReadFromJsonAsync<AccountDto>(TestContext.Current.CancellationToken);
        account!.UserId.ShouldBe(session.Account.UserId);
        account.Nickname.ShouldBe(session.Account.Nickname);
    }

    [Fact]
    public async Task Me_requires_a_token()
    {
        using var response = await _client.GetAsync(new Uri(ApiRoutes.Identity.Me, UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_unknown_installation_cannot_get_tokens()
    {
        using var response = await _client.PostAsync(new Uri(ApiRoutes.Connect.Token, UriKind.Relative), new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = ApiRoutes.Connect.InstallationGrantType,
            ["client_id"] = ApiRoutes.Connect.ClientId,
            ["installation_id"] = Guid.NewGuid().ToString("D"),
            ["installation_secret"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            ["app_version"] = "1.0.0",
        }), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("invalid_grant");
        body.ShouldContain("installation_unknown");
    }

    [Fact]
    public async Task Refresh_tokens_rotate_and_a_reused_one_is_rejected()
    {
        var session = await BurdockApiFactory.SignUpAsync(_client);
        var refresh = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = ApiRoutes.Connect.ClientId,
            ["refresh_token"] = session.Tokens.RefreshToken!,
        };

        var rotated = await BurdockApiFactory.ExchangeAsync(_client, refresh);
        rotated.AccessToken.ShouldNotBe(session.Tokens.AccessToken);
        rotated.RefreshToken.ShouldNotBeNullOrEmpty();
        rotated.RefreshToken.ShouldNotBe(session.Tokens.RefreshToken);

        using var reuse = await _client.PostAsync(new Uri(ApiRoutes.Connect.Token, UriKind.Relative), new FormUrlEncodedContent(refresh), TestContext.Current.CancellationToken);
        reuse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await reuse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("invalid_grant");

        using var request = BurdockApiFactory.Authorized(HttpMethod.Get, ApiRoutes.Identity.Me, rotated.AccessToken);
        using var me = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        me.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_nickname_can_be_changed_within_the_shared_rules()
    {
        var session = await BurdockApiFactory.SignUpAsync(_client);

        using var ok = BurdockApiFactory.Authorized(HttpMethod.Patch, ApiRoutes.Identity.Me, session.Tokens.AccessToken);
        ok.Content = JsonContent.Create(new RenameAccountDto("  Реп’яшок Олесь  "));
        using var renamed = await _client.SendAsync(ok, TestContext.Current.CancellationToken);
        renamed.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await renamed.Content.ReadFromJsonAsync<AccountDto>(TestContext.Current.CancellationToken))!.Nickname.ShouldBe("Реп’яшок Олесь");

        using var bad = BurdockApiFactory.Authorized(HttpMethod.Patch, ApiRoutes.Identity.Me, session.Tokens.AccessToken);
        bad.Content = JsonContent.Create(new RenameAccountDto("x"));
        using var rejected = await _client.SendAsync(bad, TestContext.Current.CancellationToken);
        rejected.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await rejected.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("profile.nickname.length");
    }

    [Fact]
    public async Task Readiness_reports_the_module_schemas_as_current()
    {
        using var response = await _client.GetAsync(new Uri("/health/ready", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("Healthy");
    }

    [Fact]
    public async Task Remote_config_is_anonymous_cached_and_honours_if_none_match()
    {
        using var first = await _client.GetAsync(new Uri(ApiRoutes.Config, UriKind.Relative), TestContext.Current.CancellationToken);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        first.Headers.ETag.ShouldNotBeNull();
        first.Headers.CacheControl!.Public.ShouldBeTrue();
        (await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("rulesVersion");

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(ApiRoutes.Config, UriKind.Relative));
        request.Headers.IfNoneMatch.Add(first.Headers.ETag!);
        using var second = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        second.StatusCode.ShouldBe(HttpStatusCode.NotModified);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [GeneratedRegex(@"^\p{Lu}\p{Ll}+ Реп’ях #\d{4}$")]
    private static partial Regex NicknamePattern();
}
