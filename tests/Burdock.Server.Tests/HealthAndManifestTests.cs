using System.Net;
using System.Net.Http.Json;
using Burdock.Contracts.V1;
using Burdock.Contracts.V1.Content;

namespace Burdock.Server.Tests;

[Collection(PostgresBackedApi.Name)]
public sealed class HealthAndManifestTests : IDisposable
{
    private readonly BurdockApiFactory _factory;
    private readonly HttpClient _client;

    public HealthAndManifestTests(PostgresFixture postgres)
    {
        ArgumentNullException.ThrowIfNull(postgres);
        _factory = new BurdockApiFactory(postgres.ConnectionString);
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task<HttpResponseMessage> GetManifestAsync(string? pair = null)
    {
        var session = await BurdockApiFactory.SignUpAsync(_client);
        using var request = BurdockApiFactory.Authorized(HttpMethod.Get, pair is null ? ApiRoutes.Content.Manifest : ApiRoutes.Content.Manifest + "?pair=" + pair, session.Tokens.AccessToken);
        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_endpoints_answer_200(string path)
    {
        using var response = await _client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Manifest_returns_an_empty_pack_list_with_a_weak_etag()
    {
        using var response = await GetManifestAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull();
        response.Headers.ETag.IsWeak.ShouldBeTrue();
        response.Headers.CacheControl!.Private.ShouldBeTrue();

        var manifest = await response.Content.ReadFromJsonAsync<ContentManifestDto>(TestContext.Current.CancellationToken);
        manifest.ShouldNotBeNull();
        manifest.Pair.ShouldBe("uk-en");
        manifest.SchemaVersion.ShouldBe(1);
        manifest.Packs.ShouldAllBe(p => p.Id.StartsWith("uk-en.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Manifest_honours_if_none_match()
    {
        var session = await BurdockApiFactory.SignUpAsync(_client);
        using var firstRequest = BurdockApiFactory.Authorized(HttpMethod.Get, ApiRoutes.Content.Manifest, session.Tokens.AccessToken);
        using var first = await _client.SendAsync(firstRequest, TestContext.Current.CancellationToken);
        var etag = first.Headers.ETag!;

        using var request = BurdockApiFactory.Authorized(HttpMethod.Get, ApiRoutes.Content.Manifest, session.Tokens.AccessToken);
        request.Headers.IfNoneMatch.Add(etag);
        using var second = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        second.StatusCode.ShouldBe(HttpStatusCode.NotModified);
    }

    [Fact]
    public async Task Manifest_requires_a_token()
    {
        using var response = await _client.GetAsync(new Uri(ApiRoutes.Content.Manifest, UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Manifest_rejects_a_malformed_pair_as_a_problem()
    {
        using var response = await GetManifestAsync("uk-uk");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("languagePair.same");
    }
}
