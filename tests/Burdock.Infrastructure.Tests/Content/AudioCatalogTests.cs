using Burdock.Contracts.Content;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;
using Burdock.Infrastructure.Content;

namespace Burdock.Infrastructure.Tests.Content;

/// <summary>audio-index.json → voice catalogue and clip lookups (ADR 0019).</summary>
public sealed class AudioCatalogTests
{
    private static readonly Guid Hello = Guid.Parse("0199a500-0000-7000-8000-000000000001");

    [Fact]
    public void Maps_voices_in_order_and_prefixes_clip_paths_with_the_asset_root()
    {
        var catalog = AudioCatalog.FromDocument(Document(), "content/audio/").Value;

        catalog.Voices.Select(v => v.Id.Value).ShouldBe(["af_heart", "bm_george"]);
        catalog.Voices[0].ShouldBe(new PronunciationVoice(VoiceId.From("af_heart"), "Heart", PronunciationAccent.American, VoiceGender.Female));
        catalog.Voices[1].ShouldBe(new PronunciationVoice(VoiceId.From("bm_george"), "George", PronunciationAccent.British, VoiceGender.Male));
        catalog.FindWord(LexemeId.From(Hello), VoiceId.From("af_heart"))!.AssetPath.ShouldBe("content/audio/af_heart/0199a500-0000-7000-8000-000000000001.m4a");
        catalog.FindSample(VoiceId.From("bm_george"))!.AssetPath.ShouldBe("content/audio/bm_george/sample.m4a");
    }

    [Fact]
    public void Unknown_words_and_voices_answer_null()
    {
        var catalog = AudioCatalog.FromDocument(Document(), string.Empty).Value;

        catalog.FindWord(LexemeId.From(Guid.NewGuid()), VoiceId.From("af_heart")).ShouldBeNull();
        catalog.FindWord(LexemeId.From(Hello), VoiceId.From("bf_emma")).ShouldBeNull();
        catalog.FindSample(VoiceId.From("bf_emma")).ShouldBeNull();
        AudioCatalog.Empty.Voices.ShouldBeEmpty();
    }

    [Fact]
    public void A_newer_schema_or_a_bad_voice_id_is_rejected()
    {
        AudioCatalog.FromDocument(Document() with { SchemaVersion = 99 }, string.Empty).Error!.Code.ShouldBe("audio.schemaTooNew");

        var bad = Document() with { Voices = [Document().Voices[0] with { Id = "Heart Voice" }] };
        AudioCatalog.FromDocument(bad, string.Empty).Error!.Code.ShouldBe("audio.voiceId");
    }

    private static AudioIndexDocument Document() => new(
        1, "uk-en.free-pool", "uk-en", 1, new DateTimeOffset(2026, 9, 30, 10, 0, 0, TimeSpan.Zero),
        new AudioEngineDocument("kokoro-onnx", "0.6.1", "kokoro-v1.0.onnx", "aa", "bb"),
        [
            new AudioVoiceDocument("af_heart", "Heart", "GA", "female", new AudioFileDocument("af_heart/sample.m4a", "s1", 100),
                new Dictionary<string, AudioFileDocument> { [Hello.ToString("D")] = new("af_heart/0199a500-0000-7000-8000-000000000001.m4a", "f1", 5000) }),
            new AudioVoiceDocument("bm_george", "George", "RP", "male", new AudioFileDocument("bm_george/sample.m4a", "s2", 100),
                new Dictionary<string, AudioFileDocument> { [Hello.ToString("D")] = new("bm_george/0199a500-0000-7000-8000-000000000001.m4a", "f2", 5000), ["not-a-guid"] = new("x", "y", 1) }),
        ]);
}
