using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Burdock.Contracts.Content;
using Burdock.Contracts.V1;
using Burdock.Contracts.V1.Pronunciation;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;
using Burdock.Server.Api;
using Burdock.Server.Modules.Pronunciation.Application;
using Burdock.Server.Modules.Pronunciation.Domain;
using Burdock.Server.Shared.Time;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Burdock.Server.Tests;

/// <summary>The pre-rendered dictionary enters the module through `api pronunciation publish` (ADR 0020 §6), and queued work survives a restart.</summary>
[Collection(PostgresBackedApi.Name)]
public sealed class PronunciationPublishTests : IDisposable
{
    private static readonly DateTimeOffset RenderedAt = new(2026, 10, 12, 17, 19, 40, TimeSpan.Zero);

    private readonly PostgresFixture _postgres;
    private readonly PronunciationApiFactory _factory;
    private readonly HttpClient _client;
    private readonly string _root = Path.Combine(Path.GetTempPath(), "burdock-audio-" + Guid.NewGuid().ToString("N"));

    public PronunciationPublishTests(PostgresFixture postgres)
    {
        ArgumentNullException.ThrowIfNull(postgres);
        _postgres = postgres;
        _factory = new PronunciationApiFactory(postgres.ConnectionString);
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        Directory.CreateDirectory(Path.Combine(_root, "am_michael"));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Publishing_an_index_registers_clips_once_rejects_corrupt_files_and_spares_the_renderer()
    {
        var ticket = Guid.NewGuid();
        var journey = Guid.NewGuid();
        WriteIndexAndPack([(ticket, "ticket", "clip-ticket"), (journey, "journey", "clip-journey")]);
        var args = new[] { "pronunciation", "publish", "--index", Path.Combine(_root, "audio-index.json"), "--pack", Path.Combine(_root, "pack.json"), "--parallel", "2" };

        (await PronunciationPublishCommand.RunAsync(_factory.Services, args, NullLogger.Instance, Ct)).ShouldBe(0);
        (await PronunciationPublishCommand.RunAsync(_factory.Services, args, NullLogger.Instance, Ct)).ShouldBe(0);

        using var clip = await _client.GetAsync(ClipUri(ticket, "am_michael"), Ct);
        clip.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await clip.Content.ReadAsStringAsync(Ct)).ShouldBe("clip-ticket");
        clip.Headers.ETag!.Tag.Trim('"').ShouldBe(Sha("clip-ticket"));
        File.Exists(Path.Combine(_factory.StorePath, "pronunciation", "kokoro-82m-v1.0", "am_michael", "sample.m4a")).ShouldBeTrue();

        // The clip of the pipeline counts as rendered: a request for the lexeme renders only the voices that are missing.
        var callsBefore = _factory.Renderer.Calls;
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(ApiRoutes.Pronunciation.Clips, UriKind.Relative)) { Content = JsonContent.Create(new RequestClipsDto(ticket, "ticket", ["am_michael", "af_heart"])) };
        request.Headers.Add(ApiRoutes.Pronunciation.AdminKeyHeader, PronunciationApiFactory.AdminKey);
        using var response = await _client.SendAsync(request, Ct);
        var view = await response.Content.ReadFromJsonAsync<ClipsDto>(Ct);
        view!.Clips.Select(c => (c.Voice, c.Status)).ShouldBe([("am_michael", "rendered"), ("af_heart", "queued")]);
        await WaitForAsync(() => _factory.Renderer.Calls - callsBefore >= 1);
        (_factory.Renderer.Calls - callsBefore).ShouldBe(1);

        // A corrupt file is refused and the stored clip stays intact.
        await File.WriteAllTextAsync(Path.Combine(_root, "am_michael", $"{journey:D}.m4a"), "tampered", Ct);
        (await PronunciationPublishCommand.RunAsync(_factory.Services, args, NullLogger.Instance, Ct)).ShouldBe(1);
        using var intact = await _client.GetAsync(ClipUri(journey, "am_michael"), Ct);
        (await intact.Content.ReadAsStringAsync(Ct)).ShouldBe("clip-journey");
    }

    [Fact]
    public async Task Clips_left_queued_are_rendered_after_a_restart()
    {
        var lexemeId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var clips = scope.ServiceProvider.GetRequiredService<IClipRepository>();
            var now = scope.ServiceProvider.GetRequiredService<IClock>().UtcNow;
            var text = PronunciationText.Create("ticket");
            await clips.SaveAsync(Clip.Queued(new ClipKey(LexemeId.From(lexemeId), VoiceId.From("bf_emma")), text.Value, scope.ServiceProvider.GetRequiredService<PronunciationOptions>().EngineVersion, ClipRequester.Admin, now), Ct);
        }

        using var restarted = new PronunciationApiFactory(_postgres.ConnectionString);
        using var client = restarted.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var deadline = DateTime.UtcNow.AddSeconds(10);
        HttpResponseMessage response;
        while (true)
        {
            response = await client.GetAsync(ClipUri(lexemeId, "bf_emma"), Ct);
            if (response.StatusCode != HttpStatusCode.Accepted || DateTime.UtcNow > deadline)
            {
                break;
            }

            response.Dispose();
            await Task.Delay(100, Ct);
        }

        using (response)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync(Ct)).ShouldBe("m4a:ticket:bf_emma");
        }
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private void WriteIndexAndPack(IReadOnlyList<(Guid Id, string Lemma, string Audio)> words)
    {
        var files = new Dictionary<string, AudioFileDocument>(StringComparer.Ordinal);
        foreach (var (id, _, audio) in words)
        {
            var relative = $"am_michael/{id:D}.m4a";
            File.WriteAllText(Path.Combine(_root, relative), audio);
            files[id.ToString("D")] = new AudioFileDocument(relative, Sha(audio), audio.Length);
        }

        File.WriteAllText(Path.Combine(_root, "am_michael", "sample.m4a"), "sample");
        var index = new AudioIndexDocument(
            AudioIndexDocument.CurrentSchemaVersion,
            "uk-en.all",
            "uk-en",
            1,
            RenderedAt,
            new AudioEngineDocument("kokoro-onnx", "0.6.1", "kokoro-v1.0.onnx", new string('a', 64), new string('b', 64)),
            [new AudioVoiceDocument("am_michael", "Michael", "GA", "male", new AudioFileDocument("am_michael/sample.m4a", Sha("sample"), 6), files)]);
        File.WriteAllText(Path.Combine(_root, "audio-index.json"), JsonSerializer.Serialize(index, AudioIndexJsonContext.Default.AudioIndexDocument));

        var pack = new ContentPackDocument(
            ContentPackDocument.CurrentSchemaVersion,
            "uk-en.all",
            "uk-en",
            "all",
            "all",
            1,
            RenderedAt,
            new ContentProvenanceDocument("test", "published", null, null, []),
            words.Select(w => new ContentEntryDocument(w.Id, w.Lemma, "noun", "A2", ["travel"], "переклад", [], [w.Lemma], null, null, null, null, null)).ToArray());
        File.WriteAllText(Path.Combine(_root, "pack.json"), JsonSerializer.Serialize(pack, ContentPackJsonContext.Default.ContentPackDocument));
    }

    private static string Sha(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static Uri ClipUri(Guid lexemeId, string voice) => new($"{ApiRoutes.Pronunciation.Clips}/{lexemeId:D}/{voice}", UriKind.Relative);

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50, Ct);
        }
    }
}
