using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;
using Burdock.Server.Modules.Pronunciation.Domain;

namespace Burdock.Server.Tests;

/// <summary>Domain rules of the text the engine may be asked to pronounce (ADR 0020 §3).</summary>
public sealed class PronunciationTextTests
{
    [Theory]
    [InlineData("ticket", "ticket")]
    [InlineData("  a.m. ", "a.m.")]
    [InlineData("call\tinto   question", "call into question")]
    [InlineData("rock ’n’ roll", "rock 'n' roll")]
    [InlineData("Ms. O'Neill", "Ms. O'Neill")]
    [InlineData("café", "café")]
    public void Valid_text_is_normalised(string raw, string expected)
    {
        var result = PronunciationText.Create(raw);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null, "pronunciation.text_empty")]
    [InlineData("", "pronunciation.text_empty")]
    [InlineData("  ", "pronunciation.text_empty")]
    [InlineData("слово", "pronunciation.text_invalid")]
    [InlineData("1234", "pronunciation.text_invalid")]
    [InlineData("https://burdock.org", "pronunciation.text_invalid")]
    [InlineData("word\u0007", "pronunciation.text_invalid")]
    public void Invalid_text_is_rejected(string? raw, string code)
    {
        var result = PronunciationText.Create(raw);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(code);
        result.Error.Kind.ShouldBe(ErrorKind.Validation);
    }

    [Fact]
    public void Text_longer_than_the_limit_is_rejected()
    {
        var result = PronunciationText.Create(new string('a', PronunciationText.MaxLength + 1));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("pronunciation.text_too_long");
    }

    [Fact]
    public void Storage_path_is_versioned_per_engine_and_voice()
    {
        var key = new ClipKey(LexemeId.From(Guid.Parse("0199a500-0000-7000-8000-000000000044")), VoiceId.From("bf_emma"));

        Clip.StoragePath("kokoro-82m-v1.0", key).ShouldBe("pronunciation/kokoro-82m-v1.0/bf_emma/0199a500-0000-7000-8000-000000000044.m4a");
    }

    [Fact]
    public void A_clip_fails_for_good_after_three_attempts()
    {
        var text = PronunciationText.Create("ticket").Value;
        var clip = Clip.Queued(new ClipKey(LexemeId.From(Guid.NewGuid()), VoiceId.From("af_heart")), text, "v1", ClipRequester.Admin, DateTimeOffset.UnixEpoch);

        var second = clip.FailedAttempt("HttpRequestException").FailedAttempt("HttpRequestException");
        second.Status.ShouldBe(ClipStatus.Queued);
        second.Attempts.ShouldBe(2);

        var third = second.FailedAttempt("TaskCanceledException");
        third.Status.ShouldBe(ClipStatus.Failed);
        third.LastError.ShouldBe("TaskCanceledException");
    }
}
