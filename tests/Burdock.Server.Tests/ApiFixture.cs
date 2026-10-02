using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Burdock.Contracts.V1;
using Burdock.Contracts.V1.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace Burdock.Server.Tests;

/// <summary>One PostgreSQL 18 container for the whole test run (spec 09 §3.14: the same major as production); every module schema is migrated by the host on startup.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18")
        .WithDatabase("burdock_test")
        .WithUsername("burdock")
        .WithPassword("burdock-test")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync() => await _container.StartAsync().ConfigureAwait(false);

    public async ValueTask DisposeAsync() => await _container.DisposeAsync().ConfigureAwait(false);
}

[CollectionDefinition(Name)]
public sealed class PostgresBackedApi : ICollectionFixture<PostgresFixture>
{
    public const string Name = "api";
}

/// <summary>Host wired to the test database with relaxed rate limits and plain HTTP for the token endpoint.</summary>
public class BurdockApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseSetting("ConnectionStrings:Burdock", connectionString);
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Auth:AllowInsecureHttp", "true");
        builder.UseSetting("Auth:RefreshTokenReuseLeeway", "00:00:00");
        builder.UseSetting("RateLimits:IdentityCreate", "1000/00:01:00");
        builder.UseSetting("RateLimits:Token", "1000/00:01:00");
        builder.UseSetting("RateLimits:AnonIp", "1000/00:01:00");
        builder.UseSetting("RateLimits:User", "1000/00:01:00");
        builder.UseSetting("RateLimits:UserWrite", "1000/00:01:00");
        Configure(builder);
    }

    protected virtual void Configure(IWebHostBuilder builder)
    {
    }

    /// <summary>Registers a fresh installation and exchanges it for tokens: the two calls every client makes on first launch (spec 09 §3.3.1).</summary>
    public static async Task<TestSession> SignUpAsync(HttpClient client, string platform = "ios")
    {
        ArgumentNullException.ThrowIfNull(client);
        var installationId = Guid.NewGuid();
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        using var registration = await client.PostAsJsonAsync(new Uri(ApiRoutes.Identity.Anonymous, UriKind.Relative), new RegisterInstallationDto(installationId, secret, platform, "1.0.0", "uk"), TestContext.Current.CancellationToken);
        registration.EnsureSuccessStatusCode();
        var account = (await registration.Content.ReadFromJsonAsync<AccountDto>(TestContext.Current.CancellationToken))!;
        var tokens = await ExchangeAsync(client, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = ApiRoutes.Connect.InstallationGrantType,
            ["client_id"] = ApiRoutes.Connect.ClientId,
            ["installation_id"] = installationId.ToString("D"),
            ["installation_secret"] = secret,
            ["app_version"] = "1.0.0",
            ["scope"] = "offline_access",
        });
        return new TestSession(installationId, secret, account, tokens);
    }

    public static async Task<TokenResponse> ExchangeAsync(HttpClient client, IReadOnlyDictionary<string, string> form)
    {
        ArgumentNullException.ThrowIfNull(client);
        using var response = await client.PostAsync(new Uri(ApiRoutes.Connect.Token, UriKind.Relative), new FormUrlEncodedContent(form), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.IsSuccessStatusCode.ShouldBeTrue($"token endpoint answered {(int)response.StatusCode}: {body}");
        return System.Text.Json.JsonSerializer.Deserialize<TokenResponse>(body)!;
    }

    public static HttpRequestMessage Authorized(HttpMethod method, string path, string accessToken)
        => new(method, new Uri(path, UriKind.Relative)) { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) } };
}

public sealed record TokenResponse(
    [property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string AccessToken,
    [property: System.Text.Json.Serialization.JsonPropertyName("refresh_token")] string? RefreshToken,
    [property: System.Text.Json.Serialization.JsonPropertyName("token_type")] string TokenType,
    [property: System.Text.Json.Serialization.JsonPropertyName("expires_in")] int ExpiresIn);

public sealed record TestSession(Guid InstallationId, string Secret, AccountDto Account, TokenResponse Tokens);
