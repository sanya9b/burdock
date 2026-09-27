using System.Net;
using System.Net.Http.Json;
using Burdock.Contracts.V1;
using Burdock.Contracts.V1.Content;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Burdock.Server.Tests;

public sealed class HealthAndManifestTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HealthAndManifestTests(WebApplicationFactory<Program> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _client = factory.CreateClient();
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
        using var response = await _client.GetAsync(new Uri(ApiRoutes.Content.Manifest, UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull();
        response.Headers.ETag.IsWeak.ShouldBeTrue();
        response.Headers.CacheControl!.Private.ShouldBeTrue();

        var manifest = await response.Content.ReadFromJsonAsync<ContentManifestDto>(TestContext.Current.CancellationToken);
        manifest.ShouldNotBeNull();
        manifest.Pair.ShouldBe("uk-en");
        manifest.SchemaVersion.ShouldBe(1);
        manifest.Packs.ShouldBeEmpty();
    }

    [Fact]
    public async Task Manifest_honours_if_none_match()
    {
        using var first = await _client.GetAsync(new Uri(ApiRoutes.Content.Manifest, UriKind.Relative), TestContext.Current.CancellationToken);
        var etag = first.Headers.ETag!;

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(ApiRoutes.Content.Manifest, UriKind.Relative));
        request.Headers.IfNoneMatch.Add(etag);
        using var second = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        second.StatusCode.ShouldBe(HttpStatusCode.NotModified);
    }

    [Fact]
    public async Task Manifest_rejects_a_malformed_pair_as_a_problem()
    {
        using var response = await _client.GetAsync(new Uri(ApiRoutes.Content.Manifest + "?pair=uk-uk", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("languagePair.same");
    }
}
