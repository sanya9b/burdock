using System.Net;
using System.Net.Http.Json;
using System.Text;
using Burdock.Contracts.V1;
using Burdock.Contracts.V1.Pronunciation;
using Burdock.Domain.Vocabulary;
using Burdock.Server.Modules.Pronunciation.Application;
using Burdock.Server.Modules.Pronunciation.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Burdock.Server.Tests;

/// <summary>Host with the admin key set, a temp file store and a fake engine instead of the Kokoro sidecar (ADR 0020).</summary>
public sealed class PronunciationApiFactory : WebApplicationFactory<Program>
{
    public const string AdminKey = "test-admin-key";

    public string StorePath { get; } = Path.Combine(Path.GetTempPath(), "burdock-pronunciation-" + Guid.NewGuid().ToString("N"));

    public FakeRenderer Renderer { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseSetting("Pronunciation:AdminKey", AdminKey);
        builder.UseSetting("Pronunciation:StorePath", StorePath);
        builder.ConfigureTestServices(services => services.AddSingleton<IPronunciationRenderer>(Renderer));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(StorePath))
        {
            Directory.Delete(StorePath, recursive: true);
        }
    }
}

/// <summary>Deterministic engine: the "audio" is the text and the voice id, so tests can verify what was stored.</summary>
public sealed class FakeRenderer : IPronunciationRenderer
{
    private int _calls;

    public int Calls => _calls;

    public Task<RenderedAudio> RenderAsync(PronunciationText text, PronunciationVoice voice, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(voice);
        Interlocked.Increment(ref _calls);
        return Task.FromResult(new RenderedAudio(Encoding.UTF8.GetBytes($"m4a:{text.Value}:{voice.Id.Value}"), "audio/mp4"));
    }
}

public sealed class PronunciationEndpointsTests : IClassFixture<PronunciationApiFactory>
{
    private readonly PronunciationApiFactory _factory;
    private readonly HttpClient _client;

    public PronunciationEndpointsTests(PronunciationApiFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task Voices_lists_the_v1_catalogue_with_heart_first()
    {
        var voices = await _client.GetFromJsonAsync<VoiceDto[]>(new Uri(ApiRoutes.Pronunciation.Voices, UriKind.Relative), TestContext.Current.CancellationToken);

        voices.ShouldNotBeNull();
        voices.Select(v => v.Id).ShouldBe(["af_heart", "am_michael", "bf_emma", "bm_george"]);
        voices[0].Accent.ShouldBe("GA");
        voices[2].Accent.ShouldBe("RP");
    }

    [Fact]
    public async Task Requesting_clips_without_the_admin_key_is_unauthorized()
    {
        using var response = await _client.PostAsJsonAsync(new Uri(ApiRoutes.Pronunciation.Clips, UriKind.Relative), new RequestClipsDto(Guid.NewGuid(), "ticket", null), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Requesting_clips_with_a_wrong_key_is_unauthorized()
    {
        using var response = await PostAsync(new RequestClipsDto(Guid.NewGuid(), "ticket", null), "wrong-key");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_request_queues_every_voice_and_the_worker_renders_them_once()
    {
        var lexemeId = Guid.NewGuid();
        var callsBefore = _factory.Renderer.Calls;

        using var response = await PostAsync(new RequestClipsDto(lexemeId, "  Call   into  question ", null));
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var queued = await response.Content.ReadFromJsonAsync<ClipsDto>(TestContext.Current.CancellationToken);
        queued.ShouldNotBeNull();
        queued.Text.ShouldBe("Call into question");
        queued.Clips.Select(c => c.Voice).ShouldBe(["af_heart", "am_michael", "bf_emma", "bm_george"]);
        queued.Clips.ShouldAllBe(c => c.Status == "queued");

        using var clip = await WaitForClipAsync(lexemeId, "bf_emma");
        clip.StatusCode.ShouldBe(HttpStatusCode.OK);
        clip.Content.Headers.ContentType!.MediaType.ShouldBe("audio/mp4");
        clip.Headers.CacheControl!.Public.ShouldBeTrue();
        clip.Headers.CacheControl.MaxAge.ShouldBe(TimeSpan.FromDays(365));
        clip.Headers.ETag.ShouldNotBeNull();
        (await clip.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("m4a:Call into question:bf_emma");

        using var again = await PostAsync(new RequestClipsDto(lexemeId, "call into question", ["bf_emma", "af_heart"]));
        var rendered = await again.Content.ReadFromJsonAsync<ClipsDto>(TestContext.Current.CancellationToken);
        rendered.ShouldNotBeNull();
        rendered.Clips.Select(c => (c.Voice, c.Status)).ShouldBe([("bf_emma", "rendered"), ("af_heart", "rendered")]);
        rendered.Clips[0].Sha256.ShouldNotBeNullOrEmpty();
        (_factory.Renderer.Calls - callsBefore).ShouldBe(4);
    }

    [Fact]
    public async Task A_rendered_clip_answers_not_modified_to_its_etag()
    {
        var lexemeId = Guid.NewGuid();
        using var _ = await PostAsync(new RequestClipsDto(lexemeId, "ticket", ["am_michael"]));
        using var first = await WaitForClipAsync(lexemeId, "am_michael");
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var request = new HttpRequestMessage(HttpMethod.Get, ClipUri(lexemeId, "am_michael"));
        request.Headers.IfNoneMatch.Add(first.Headers.ETag!);
        using var second = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        second.StatusCode.ShouldBe(HttpStatusCode.NotModified);
    }

    [Fact]
    public async Task An_unknown_clip_is_not_found_and_an_unknown_voice_is_a_validation_problem()
    {
        using var missing = await _client.GetAsync(ClipUri(Guid.NewGuid(), "af_heart"), TestContext.Current.CancellationToken);
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using var badVoice = await _client.GetAsync(ClipUri(Guid.NewGuid(), "zz_nobody"), TestContext.Current.CancellationToken);
        badVoice.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await badVoice.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("pronunciation.voice_unknown");
    }

    [Theory]
    [InlineData("", "pronunciation.text_empty")]
    [InlineData("   ", "pronunciation.text_empty")]
    [InlineData("слово", "pronunciation.text_invalid")]
    [InlineData("http://example.org", "pronunciation.text_invalid")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "pronunciation.text_too_long")]
    public async Task Invalid_text_is_rejected_with_the_domain_code(string text, string code)
    {
        using var response = await PostAsync(new RequestClipsDto(Guid.NewGuid(), text, null));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain(code);
    }

    [Fact]
    public async Task An_empty_lexeme_id_is_rejected()
    {
        using var response = await PostAsync(new RequestClipsDto(Guid.Empty, "ticket", null));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("pronunciation.lexeme_invalid");
    }

    private static Uri ClipUri(Guid lexemeId, string voice) => new($"{ApiRoutes.Pronunciation.Clips}/{lexemeId:D}/{voice}", UriKind.Relative);

    private async Task<HttpResponseMessage> PostAsync(RequestClipsDto body, string key = PronunciationApiFactory.AdminKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(ApiRoutes.Pronunciation.Clips, UriKind.Relative)) { Content = JsonContent.Create(body) };
        request.Headers.Add(ApiRoutes.Pronunciation.AdminKeyHeader, key);
        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<HttpResponseMessage> WaitForClipAsync(Guid lexemeId, string voice)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (true)
        {
            var response = await _client.GetAsync(ClipUri(lexemeId, voice), TestContext.Current.CancellationToken);
            if (response.StatusCode != HttpStatusCode.Accepted || DateTime.UtcNow > deadline)
            {
                return response;
            }

            response.Dispose();
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
    }
}
