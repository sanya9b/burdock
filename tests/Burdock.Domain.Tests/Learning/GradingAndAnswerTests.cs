using Burdock.Domain.Learning;

namespace Burdock.Domain.Tests.Learning;

public sealed class GradingAndAnswerTests
{
    [Theory]
    [InlineData(ExerciseMode.ChoiceTargetToNative, true, 2, false, false, Rating.Good)]
    [InlineData(ExerciseMode.ChoiceTargetToNative, true, 9, false, false, Rating.Hard)]
    [InlineData(ExerciseMode.ChoiceNativeToTarget, false, 1, false, false, Rating.Again)]
    [InlineData(ExerciseMode.TypedRecall, true, 20, false, false, Rating.Good)]
    [InlineData(ExerciseMode.TypedRecall, true, 3, true, false, Rating.Hard)]
    [InlineData(ExerciseMode.TypedRecall, true, 3, false, true, Rating.Easy)]
    [InlineData(ExerciseMode.TypedRecall, false, 3, false, true, Rating.Again)]
    public void Grade_follows_the_outcome_table(ExerciseMode mode, bool correct, int seconds, bool nearMiss, bool easy, Rating expected)
    {
        var rating = GradingPolicy.Grade(new ExerciseOutcome(mode, correct, TimeSpan.FromSeconds(seconds), nearMiss, easy));

        rating.ShouldBe(expected);
    }

    [Fact]
    public void Matching_is_never_graded()
    {
        GradingPolicy.Grade(new ExerciseOutcome(ExerciseMode.Matching, true, TimeSpan.FromSeconds(1))).ShouldBeNull();
        ExerciseMode.Matching.IsUngraded().ShouldBeTrue();
        ExerciseMode.TypedRecall.IsRecall().ShouldBeTrue();
        ExerciseMode.ChoiceTargetToNative.IsRecall().ShouldBeFalse();
    }

    [Theory]
    [InlineData("  Receipt ", "receipt")]
    [InlineData("don’t", "don't")]
    [InlineData("donʼt", "don't")]
    [InlineData("ice-cream", "ice cream")]
    [InlineData("Ice   Cream", "ice cream")]
    [InlineData("café", "cafe")]
    public void Normalize_unifies_case_apostrophes_hyphens_and_diacritics(string input, string expected)
    {
        AnswerNormalizer.Normalize(input).ShouldBe(expected);
    }

    [Fact]
    public void Check_accepts_exact_variants_and_forgives_one_typo_for_long_words()
    {
        string[] forms = ["colour", "color"];

        AnswerNormalizer.Check("Color", forms).ShouldBe(AnswerCheck.Exact("color"));
        AnswerNormalizer.Check("colour", forms).ShouldBe(AnswerCheck.Exact("colour"));
        AnswerNormalizer.Check("coluor", forms).ShouldBe(AnswerCheck.Typo("colour")); // transposition
        AnswerNormalizer.Check("colr", forms).ShouldBe(AnswerCheck.Typo("color")); // one edit from the 5-letter variant
        AnswerNormalizer.Check("kolor", forms).ShouldBe(AnswerCheck.Typo("color"));
        AnswerNormalizer.Check("colourz!", forms).Correct.ShouldBeFalse();
    }

    [Fact]
    public void Check_does_not_forgive_typos_in_short_words()
    {
        AnswerNormalizer.Check("cta", ["cat"]).Correct.ShouldBeFalse();
        AnswerNormalizer.Check("", ["cat"]).Correct.ShouldBeFalse();
        AnswerNormalizer.Check("cat", ["cat"]).ShouldBe(AnswerCheck.Exact("cat"));
    }

    [Theory]
    [InlineData("kitten", "sitting", 3)]
    [InlineData("abcd", "acbd", 1)]
    [InlineData("same", "same", 0)]
    [InlineData("", "abc", 3)]
    public void Damerau_levenshtein_counts_edits_and_transpositions(string a, string b, int expected)
    {
        AnswerNormalizer.DamerauLevenshtein(a, b).ShouldBe(expected);
    }
}
