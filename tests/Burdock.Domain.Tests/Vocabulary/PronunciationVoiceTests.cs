using Burdock.Domain.Vocabulary;

namespace Burdock.Domain.Tests.Vocabulary;

/// <summary>Voice identifiers follow the pipeline naming (ADR 0019) and stay safe to use as asset folder names.</summary>
public sealed class PronunciationVoiceTests
{
    [Theory]
    [InlineData("af_heart")]
    [InlineData("bm_george")]
    [InlineData("voice2")]
    public void Valid_ids_round_trip(string id)
    {
        VoiceId.From(id).Value.ShouldBe(id);
        VoiceId.From(id).ToString().ShouldBe(id);
        VoiceId.From(id).ShouldBe(new VoiceId(id));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("AF_HEART")]
    [InlineData("af heart")]
    [InlineData("../heart")]
    [InlineData("a_very_long_voice_identifier_that_exceeds_the_limit")]
    public void Invalid_ids_are_rejected(string id)
    {
        Should.Throw<ArgumentException>(() => VoiceId.From(id));
    }

    [Fact]
    public void A_voice_carries_accent_and_gender_for_the_picker()
    {
        var voice = new PronunciationVoice(VoiceId.From("bf_emma"), "Emma", PronunciationAccent.British, VoiceGender.Female);

        voice.Accent.ShouldBe(PronunciationAccent.British);
        voice.Gender.ShouldBe(VoiceGender.Female);
        voice.Name.ShouldBe("Emma");
    }
}
