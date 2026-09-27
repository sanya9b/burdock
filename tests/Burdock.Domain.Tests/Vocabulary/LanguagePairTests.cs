using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;

namespace Burdock.Domain.Tests.Vocabulary;

public sealed class LanguagePairTests
{
    [Theory]
    [InlineData("uk-en", "uk", "en")]
    [InlineData(" UK - EN ", "uk", "en")]
    [InlineData("pl-de", "pl", "de")]
    public void Parse_accepts_canonical_and_normalises_case_and_spaces(string input, string native, string target)
    {
        var result = LanguagePair.Parse(input);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Native.Value.ShouldBe(native);
        result.Value.Target.Value.ShouldBe(target);
        result.Value.ToString().ShouldBe($"{native}-{target}");
    }

    [Theory]
    [InlineData(null, "languagePair.empty")]
    [InlineData("", "languagePair.empty")]
    [InlineData("uk", "languagePair.invalid")]
    [InlineData("uk-en-de", "language.invalid")]
    [InlineData("u1-en", "language.invalid")]
    [InlineData("uk-uk", "languagePair.same")]
    public void Parse_rejects_malformed_input_with_a_validation_error(string? input, string expectedCode)
    {
        var result = LanguagePair.Parse(input);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Kind.ShouldBe(ErrorKind.Validation);
        result.Error.Code.ShouldBe(expectedCode);
    }

    [Fact]
    public void The_v1_pair_is_ukrainian_to_english()
    {
        LanguagePair.UkrainianToEnglish.ToString().ShouldBe("uk-en");
        LanguagePair.Parse("uk-en").Value.ShouldBe(LanguagePair.UkrainianToEnglish);
    }
}
