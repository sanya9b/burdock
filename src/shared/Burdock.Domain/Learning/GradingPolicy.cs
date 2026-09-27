namespace Burdock.Domain.Learning;

/// <summary>What happened in one exercise, before it is turned into a grade.</summary>
public readonly record struct ExerciseOutcome(ExerciseMode Mode, bool Correct, TimeSpan ResponseTime, bool NearMiss = false, bool EasyRequested = false);

/// <summary>Maps exercise outcomes onto FSRS ratings (spec 06 §3.6 table). Pure and deterministic.</summary>
public static class GradingPolicy
{
    /// <summary>learning.grading.fastChoiceSeconds: a correct choice faster than this is Good, slower is Hard.</summary>
    public static readonly TimeSpan FastChoiceThreshold = TimeSpan.FromSeconds(4);

    /// <summary>learning.grading.fastTypedSeconds: informational only; typed recall is graded by exactness, not speed.</summary>
    public static readonly TimeSpan FastTypedThreshold = TimeSpan.FromSeconds(12);

    /// <summary>Returns null for ungraded modes (Matching).</summary>
    public static Rating? Grade(ExerciseOutcome outcome)
    {
        if (outcome.Mode.IsUngraded())
        {
            return null;
        }

        if (!outcome.Correct)
        {
            return Rating.Again;
        }

        return outcome.Mode switch
        {
            ExerciseMode.ChoiceTargetToNative or ExerciseMode.ChoiceNativeToTarget or ExerciseMode.Listening
                => outcome.ResponseTime <= FastChoiceThreshold ? Rating.Good : Rating.Hard,
            ExerciseMode.TypedRecall or ExerciseMode.Cloze when outcome.NearMiss => Rating.Hard,
            ExerciseMode.TypedRecall or ExerciseMode.Cloze when outcome.EasyRequested => Rating.Easy,
            ExerciseMode.TypedRecall or ExerciseMode.Cloze => Rating.Good,
            _ => Rating.Good,
        };
    }
}
