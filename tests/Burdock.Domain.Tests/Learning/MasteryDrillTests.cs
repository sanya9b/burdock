using Burdock.Domain.Learning;
using Burdock.Domain.SharedKernel;

namespace Burdock.Domain.Tests.Learning;

/// <summary>Session mastery rule (owner decision 2026-10-28, spec 06 §3.7): four correct answers in a row per word, a mistake resets.</summary>
public sealed class MasteryDrillTests
{
    private static readonly LexemeId A = LexemeId.From(Guid.Parse("0199a500-0000-7000-8000-0000000000a1"));
    private static readonly LexemeId B = LexemeId.From(Guid.Parse("0199a500-0000-7000-8000-0000000000b1"));

    [Fact]
    public void Four_correct_answers_in_a_row_master_a_word_and_the_default_target_is_four()
    {
        var drill = new MasteryDrill([A, B]);

        drill.Target.ShouldBe(LearningPolicy.MasteryStreak);
        LearningPolicy.MasteryStreak.ShouldBe(4);
        drill.Record(A, true).ShouldBe(new MasteryStep(1, false, false));
        drill.Record(A, true).ShouldBe(new MasteryStep(2, false, false));
        drill.Record(A, true).ShouldBe(new MasteryStep(3, false, false));
        drill.Record(A, true).ShouldBe(new MasteryStep(4, true, false));
        drill.IsMastered(A).ShouldBeTrue();
        drill.Completed.ShouldBeFalse();
        drill.AnswersRemaining.ShouldBe(4);
    }

    [Fact]
    public void A_mistake_resets_the_word_counter_and_only_the_fourth_confirmation_counts()
    {
        var drill = new MasteryDrill([A]);
        drill.Record(A, true);
        drill.Record(A, true);

        var reset = drill.Record(A, false);

        reset.ShouldBe(new MasteryStep(0, false, true));
        drill.Streak(A).ShouldBe(0);
        drill.Record(A, true); drill.Record(A, true); drill.Record(A, true);
        drill.Record(A, true).Mastered.ShouldBeTrue();
        drill.Record(A, true).Mastered.ShouldBeFalse("mastery is reported once");
        drill.Completed.ShouldBeTrue();
    }

    [Fact]
    public void Unmastered_words_queue_up_in_answer_order_and_mastered_ones_leave_the_queue()
    {
        var drill = new MasteryDrill([A, B]);

        drill.Record(A, true);
        drill.Record(B, false);
        drill.Record(A, true);

        drill.PendingCount.ShouldBe(3);
        drill.Dequeue().ShouldBe(A);
        drill.Dequeue().ShouldBe(B);
        drill.Dequeue().ShouldBe(A);
        drill.Dequeue().ShouldBeNull();
        drill.Record(A, true);
        drill.Record(A, true).Mastered.ShouldBeTrue();
        drill.PendingCount.ShouldBe(0);
    }

    [Fact]
    public void The_exercise_escalates_from_recognition_to_production()
    {
        var drill = new MasteryDrill([A]);

        drill.ModeFor(A, typedAvailable: true).ShouldBe(ExerciseMode.ChoiceTargetToNative);
        drill.Record(A, true);
        drill.ModeFor(A, typedAvailable: true).ShouldBe(ExerciseMode.ChoiceNativeToTarget);
        drill.Record(A, true);
        drill.ModeFor(A, typedAvailable: true).ShouldBe(ExerciseMode.ChoiceTargetToNative);
        drill.Record(A, true);
        drill.ModeFor(A, typedAvailable: true).ShouldBe(ExerciseMode.TypedRecall);
        drill.ModeFor(A, typedAvailable: false).ShouldBe(ExerciseMode.ChoiceNativeToTarget);
    }
}
