using System.Text.Json;
using System.Text.Json.Serialization;
using Burdock.Domain.Learning;
using Burdock.Infrastructure.Scheduling;
using FsrsSharp.Configuration;

namespace Burdock.Infrastructure.Tests.Scheduling;

/// <summary>
/// FR-15-2, FR-06-3, AC-06-3: the Fsrs.Sharp adapter behind <see cref="IReviewScheduler"/> reproduces py-fsrs 6.3.2 on the
/// golden vectors in tests/fixtures/fsrs/vectors.v1.json (spec 15 section 3.4). Tolerances: 1e-6 on stability and difficulty,
/// the due instant to the second, states and steps exactly.
/// </summary>
public sealed class FsrsGoldenVectorTests
{
    private const double MemoryTolerance = 1e-6;
    private const double DueToleranceDays = 1.0 / 86400 / 10; // a tenth of a second

    private static readonly GoldenDocument Document = GoldenDocument.Load();

    public static TheoryData<string> CaseIds => [.. Document.Cases.Select(c => c.Id)];

    [Theory]
    [MemberData(nameof(CaseIds))]
    public void FR_15_2_replaying_a_golden_case_matches_py_fsrs(string caseId)
    {
        var goldenCase = Document.Cases.Single(c => string.Equals(c.Id, caseId, StringComparison.Ordinal));
        var scheduler = new FsrsSharpReviewScheduler(new FsrsOptions(goldenCase.DesiredRetention ?? Document.DesiredRetention, Document.MaxIntervalDays, EnableFuzzing: false));

        var input = new SchedulingInput(CardState.New, null, null, null, null);
        var now = Document.Start;
        for (var i = 0; i < goldenCase.Reviews.Count; i++)
        {
            var review = goldenCase.Reviews[i];
            var expected = goldenCase.Expected[i];
            now = now.AddSeconds(Math.Round(review.DeltaDays * 86400));

            if (expected.RetrievabilityBefore is { } retrievability)
            {
                scheduler.Retrievability(input, now).ShouldBe(retrievability, MemoryTolerance, $"{caseId} review {i}: retrievability before");
            }

            var outcome = scheduler.Review(input, (Rating)review.Grade, now);

            outcome.Memory.Stability.ShouldBe(expected.Stability, MemoryTolerance, $"{caseId} review {i}: stability");
            outcome.Memory.Difficulty.ShouldBe(expected.Difficulty, MemoryTolerance, $"{caseId} review {i}: difficulty");
            (outcome.Due - now).TotalDays.ShouldBe(expected.DueDays, DueToleranceDays, $"{caseId} review {i}: due");
            outcome.State.ShouldBe(ToCardState(expected.State), $"{caseId} review {i}: state");
            outcome.Step.ShouldBe(expected.Step, $"{caseId} review {i}: step");

            input = new SchedulingInput(outcome.State, outcome.Memory, outcome.Step, outcome.Due, now);
        }
    }

    [Fact]
    public void FR_06_3_default_parameters_are_the_21_py_fsrs_defaults()
    {
        var weights = ((IEnumerable<double>)new FsrsParameters().Weights).ToArray();

        weights.Length.ShouldBe(21);
        Document.Parameters.Count.ShouldBe(21);
        for (var i = 0; i < weights.Length; i++)
        {
            weights[i].ShouldBe(Document.Parameters[i], 1e-9, $"w[{i}]");
        }
    }

    [Fact]
    public void FR_06_3_golden_document_covers_the_spec_sets()
    {
        // (a) new card x 4 grades, (b) 150 random sequences, (c) retention presets, (d) max-interval clamp, (e) relearning, (f) same-day
        Document.Cases.Count(c => c.Id.StartsWith("a-", StringComparison.Ordinal)).ShouldBe(4);
        Document.Cases.Count(c => c.Id.StartsWith("b-", StringComparison.Ordinal)).ShouldBe(150);
        Document.Cases.Where(c => c.Id.StartsWith("c-", StringComparison.Ordinal)).Select(c => c.DesiredRetention ?? Document.DesiredRetention).Distinct().Count().ShouldBe(3);
        Document.Cases.ShouldContain(c => string.Equals(c.Id, "d-max-interval-clamp", StringComparison.Ordinal));
        Document.Cases.Count(c => c.Id.StartsWith("e-", StringComparison.Ordinal)).ShouldBeGreaterThanOrEqualTo(1);
        Document.Cases.Count(c => c.Id.StartsWith("f-", StringComparison.Ordinal)).ShouldBeGreaterThanOrEqualTo(1);
        Document.MaxIntervalDays.ShouldBe(365);
        Document.LearningStepsMinutes.ShouldBe([10]);
        Document.RelearningStepsMinutes.ShouldBe([10]);
    }

    [Fact]
    public void Fuzz_changes_only_the_interval_and_stays_within_the_fuzz_range()
    {
        var exact = new FsrsSharpReviewScheduler(FsrsOptions.Deterministic);
        var fuzzed = new FsrsSharpReviewScheduler(FsrsOptions.Default);
        var now = Document.Start;
        var matureCard = new SchedulingInput(CardState.Review, MemoryState.Create(30, 5), null, now, now.AddDays(-30));

        var exactOutcome = exact.Review(matureCard, Rating.Good, now);
        var exactDays = (exactOutcome.Due - now).TotalDays;

        for (var attempt = 0; attempt < 20; attempt++)
        {
            var fuzzedOutcome = fuzzed.Review(matureCard, Rating.Good, now);
            fuzzedOutcome.Memory.ShouldBe(exactOutcome.Memory, "fuzz must not touch stability or difficulty");
            fuzzedOutcome.State.ShouldBe(exactOutcome.State);
            var fuzzedDays = (fuzzedOutcome.Due - now).TotalDays;
            fuzzedDays.ShouldBeGreaterThanOrEqualTo(1);
            fuzzedDays.ShouldBeInRange(exactDays * 0.85 - 1, exactDays * 1.15 + 1);
        }
    }

    private static CardState ToCardState(int pyFsrsState) => pyFsrsState switch
    {
        1 => CardState.Learning,
        2 => CardState.Review,
        3 => CardState.Relearning,
        _ => throw new ArgumentOutOfRangeException(nameof(pyFsrsState), pyFsrsState, "Unknown py-fsrs state."),
    };

    public sealed record GoldenDocument(
        [property: JsonPropertyName("parameters")] IReadOnlyList<double> Parameters,
        [property: JsonPropertyName("desiredRetention")] double DesiredRetention,
        [property: JsonPropertyName("maxIntervalDays")] int MaxIntervalDays,
        [property: JsonPropertyName("learningStepsMinutes")] IReadOnlyList<int> LearningStepsMinutes,
        [property: JsonPropertyName("relearningStepsMinutes")] IReadOnlyList<int> RelearningStepsMinutes,
        [property: JsonPropertyName("start")] DateTimeOffset Start,
        [property: JsonPropertyName("cases")] IReadOnlyList<GoldenCase> Cases)
    {
        private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

        public static GoldenDocument Load()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "fsrs", "vectors.v1.json");
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<GoldenDocument>(stream, Options) ?? throw new InvalidOperationException("Golden vectors are empty.");
        }
    }

    public sealed record GoldenCase(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("desiredRetention")] double? DesiredRetention,
        [property: JsonPropertyName("reviews")] IReadOnlyList<GoldenReview> Reviews,
        [property: JsonPropertyName("expected")] IReadOnlyList<GoldenExpectation> Expected);

    public sealed record GoldenReview(
        [property: JsonPropertyName("deltaDays")] double DeltaDays,
        [property: JsonPropertyName("grade")] int Grade);

    public sealed record GoldenExpectation(
        [property: JsonPropertyName("stability")] double Stability,
        [property: JsonPropertyName("difficulty")] double Difficulty,
        [property: JsonPropertyName("dueDays")] double DueDays,
        [property: JsonPropertyName("state")] int State,
        [property: JsonPropertyName("step")] int? Step,
        [property: JsonPropertyName("retrievabilityBefore")] double? RetrievabilityBefore);
}
