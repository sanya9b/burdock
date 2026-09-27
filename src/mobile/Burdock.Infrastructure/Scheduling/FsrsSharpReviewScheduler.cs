using Burdock.Domain.Learning;
using FsrsSharp.Configuration;
using FsrsSharp.Core;
using FsrsSharp.Models;

namespace Burdock.Infrastructure.Scheduling;

/// <summary>Tunables from spec 06 §3.3: desired retention 0.90 (presets 0.85/0.94), max interval 365 days, fuzz on in production.</summary>
public sealed record FsrsOptions(double DesiredRetention = 0.90, int MaximumIntervalDays = 365, bool EnableFuzzing = true)
{
    public static FsrsOptions Default { get; } = new();

    /// <summary>Deterministic variant for tests and golden comparisons.</summary>
    public static FsrsOptions Deterministic { get; } = new(EnableFuzzing: false);
}

/// <summary>Adapter over Fsrs.Sharp 2.0.0 (MIT, parity-tested against py-fsrs 6.3.2) behind the Domain port (ADR 0008, T3).</summary>
public sealed class FsrsSharpReviewScheduler : IReviewScheduler
{
    private readonly Scheduler _scheduler;

    public FsrsSharpReviewScheduler(FsrsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _scheduler = new Scheduler(new FsrsConfig
        {
            DesiredRetention = options.DesiredRetention,
            MaximumInterval = options.MaximumIntervalDays,
            EnableFuzzing = options.EnableFuzzing,
            LearningSteps = [TimeSpan.FromMinutes(10)],
            RelearningSteps = [TimeSpan.FromMinutes(10)],
            Parameters = new FsrsParameters(),
        });
    }

    public SchedulingOutcome Review(SchedulingInput input, Domain.Learning.Rating rating, DateTimeOffset reviewedAt)
    {
        var card = ToCard(input, reviewedAt);
        var result = _scheduler.ReviewCard(card, (FsrsSharp.Models.Rating)(int)rating, reviewedAt, null);
        var reviewed = result.Card;
        var memory = MemoryState.Create(reviewed.Stability ?? 0.1, Math.Clamp(reviewed.Difficulty ?? 5, 1, 10));
        return new SchedulingOutcome(ToDomainState(reviewed.State), memory, reviewed.Step, reviewed.Due);
    }

    public double Retrievability(SchedulingInput input, DateTimeOffset at)
        => input.Memory is null ? 0 : Math.Clamp(_scheduler.GetCardRetrievability(ToCard(input, at), at), 0, 1);

    private static Card ToCard(SchedulingInput input, DateTimeOffset now) => new(
        cardId: null,
        state: ToFsrsState(input.State),
        step: input.Step,
        stability: input.Memory?.Stability,
        difficulty: input.Memory?.Difficulty,
        due: input.Due ?? now,
        lastReview: input.LastReview);

    private static State ToFsrsState(CardState state) => state switch
    {
        CardState.New or CardState.Introduced or CardState.Known or CardState.Suspended => State.New,
        CardState.Learning => State.Learning,
        CardState.Review or CardState.Learned => State.Review,
        CardState.Relearning => State.Relearning,
        _ => State.New,
    };

    private static CardState ToDomainState(State state) => state switch
    {
        State.Learning => CardState.Learning,
        State.Review => CardState.Review,
        State.Relearning => CardState.Relearning,
        _ => CardState.Learning,
    };
}
