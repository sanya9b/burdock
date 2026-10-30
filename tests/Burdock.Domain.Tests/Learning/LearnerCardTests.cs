using Burdock.Domain.Exposure;
using Burdock.Domain.Learning;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Time;

namespace Burdock.Domain.Tests.Learning;

public sealed class LearnerCardTests
{
    private static readonly LearnerId Learner = LearnerId.From(Guid.Parse("0199a4c0-0000-7000-8000-00000000aaaa"));
    private static readonly LexemeId Lexeme = LexemeId.From(Guid.Parse("0199a4c0-0000-7000-8000-00000000bbbb"));
    private static readonly DateTimeOffset T0 = new(2026, 10, 5, 10, 0, 0, TimeSpan.Zero);

    private static LearnerCard NewCard() => LearnerCard.CreateNew(CardId.From(Guid.NewGuid()), Learner, Lexeme, T0);

    private static LearningDay Day(int offset) => new(new DateOnly(2026, 10, 5).AddDays(offset));

    [Fact]
    public void Scheduled_exposures_introduce_only_after_three_on_two_distinct_days()
    {
        var card = NewCard();

        card.RecordExposure(ExposureKind.Scheduled, Day(0), T0).ShouldBeFalse();
        card.RecordExposure(ExposureKind.Scheduled, Day(0), T0.AddHours(1)).ShouldBeFalse();
        card.RecordExposure(ExposureKind.Scheduled, Day(0), T0.AddHours(2)).ShouldBeFalse(); // three exposures but one day
        card.State.ShouldBe(CardState.New);

        card.RecordExposure(ExposureKind.Scheduled, Day(1), T0.AddDays(1)).ShouldBeTrue();
        card.State.ShouldBe(CardState.Introduced);
        card.ScheduledExposures.ShouldBe(4);
        card.IntroducedAt.ShouldBe(T0.AddDays(1));
    }

    [Fact]
    public void A_confirmed_exposure_introduces_immediately()
    {
        var card = NewCard();

        card.RecordExposure(ExposureKind.Confirmed, Day(0), T0).ShouldBeTrue();

        card.State.ShouldBe(CardState.Introduced);
        card.ConfirmedExposures.ShouldBe(1);
        card.RecordExposure(ExposureKind.Confirmed, Day(0), T0.AddMinutes(5)).ShouldBeFalse(); // already introduced
    }

    [Fact]
    public void Review_updates_memory_and_counts_recall_successes_towards_learned()
    {
        var card = NewCard();
        card.Introduce(T0);
        var outcome = new SchedulingOutcome(CardState.Review, MemoryState.Create(30, 5), null, T0.AddDays(30));

        card.ApplyReview(Rating.Good, ExerciseMode.ChoiceTargetToNative, outcome, Day(0), T0).IsSuccess.ShouldBeTrue();
        card.State.ShouldBe(CardState.Review); // recognition does not count as recall
        card.RecallSuccesses.ShouldBe(0);

        card.ApplyReview(Rating.Good, ExerciseMode.TypedRecall, outcome, Day(0), T0.AddHours(1));
        card.ApplyReview(Rating.Good, ExerciseMode.TypedRecall, outcome, Day(0), T0.AddHours(2));
        card.State.ShouldBe(CardState.Review); // three successes needed on two days
        card.ApplyReview(Rating.Hard, ExerciseMode.TypedRecall, outcome, Day(1), T0.AddDays(1));

        card.State.ShouldBe(CardState.Learned);
        card.LearnedAt.ShouldBe(T0.AddDays(1));
        card.Reps.ShouldBe(4);
        card.Memory.ShouldBe(MemoryState.Create(30, 5));
    }

    [Fact]
    public void Forgetting_a_review_card_counts_a_lapse_and_drops_learned()
    {
        var card = NewCard();
        card.Introduce(T0);
        var strong = new SchedulingOutcome(CardState.Review, MemoryState.Create(40, 4), null, T0.AddDays(40));
        card.ApplyReview(Rating.Good, ExerciseMode.TypedRecall, strong, Day(0), T0);
        card.ApplyReview(Rating.Good, ExerciseMode.TypedRecall, strong, Day(1), T0.AddDays(1));
        card.ApplyReview(Rating.Good, ExerciseMode.TypedRecall, strong, Day(2), T0.AddDays(2));
        card.State.ShouldBe(CardState.Learned);

        var lapse = new SchedulingOutcome(CardState.Relearning, MemoryState.Create(2, 6), 0, T0.AddDays(3).AddMinutes(10));
        card.ApplyReview(Rating.Again, ExerciseMode.TypedRecall, lapse, Day(3), T0.AddDays(3));

        card.Lapses.ShouldBe(1);
        card.State.ShouldBe(CardState.Relearning);
        card.LearnedAt.ShouldBeNull();
    }

    [Fact]
    public void Ungraded_modes_and_suspended_cards_are_rejected()
    {
        var card = NewCard();
        var outcome = new SchedulingOutcome(CardState.Learning, MemoryState.Create(1, 5), 0, T0.AddMinutes(10));

        card.ApplyReview(Rating.Good, ExerciseMode.Matching, outcome, Day(0), T0).Error!.Code.ShouldBe("card.ungradedMode");
        card.Suspend();
        card.ApplyReview(Rating.Good, ExerciseMode.TypedRecall, outcome, Day(0), T0).Error!.Code.ShouldBe("card.suspended");
    }

    [Fact]
    public void Mastery_progress_follows_the_drill_and_fills_on_learned_or_known()
    {
        var card = LearnerCard.CreateNew(CardId.From(Guid.NewGuid()), Learner, Lexeme, T0);

        card.RecordMastery(2);
        card.MasteryProgress.ShouldBe(2);
        card.RecordMastery(0);
        card.MasteryProgress.ShouldBe(0);
        card.RecordMastery(9);
        card.MasteryProgress.ShouldBe(LearningPolicy.MasteryStreak);

        var mastered = LearnerCard.CreateNew(CardId.From(Guid.NewGuid()), Learner, Lexeme, T0);
        mastered.MarkMastered(T0);
        mastered.State.ShouldBe(CardState.Learned);
        mastered.LearnedAt.ShouldBe(T0);
        mastered.MasteryProgress.ShouldBe(LearningPolicy.MasteryStreak);

        var known = LearnerCard.CreateNew(CardId.From(Guid.NewGuid()), Learner, Lexeme, T0);
        known.ClaimKnown(T0);
        known.MasteryProgress.ShouldBe(LearningPolicy.MasteryStreak);
        known.MarkMastered(T0.AddDays(1));
        known.State.ShouldBe(CardState.Known, "a claimed word is not downgraded to learned");
    }

    [Fact]
    public void Restore_round_trips_all_state()
    {
        var restored = LearnerCard.Restore(CardId.From(Guid.NewGuid()), Learner, Lexeme, T0, CardState.Learning, MemoryState.Create(3, 5), 1,
            T0.AddDays(3), T0, T0, null, 2, 0, 5, 1, 1, [Day(0), Day(1)], [Day(1)], masteryProgress: 3);

        restored.MasteryProgress.ShouldBe(3);
        restored.State.ShouldBe(CardState.Learning);
        restored.Step.ShouldBe(1);
        restored.ExposureDays.Count.ShouldBe(2);
        restored.RecallSuccessDays.ShouldContain(Day(1));
        restored.ToSchedulingInput().ShouldBe(new SchedulingInput(CardState.Learning, MemoryState.Create(3, 5), 1, T0.AddDays(3), T0));
    }
}
