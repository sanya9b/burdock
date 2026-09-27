namespace Burdock.Domain.Learning;

/// <summary>Exercise types. Only the first four ship in v1 (decision Q21); Listening and Cloze are reserved in the model.</summary>
public enum ExerciseMode
{
    /// <summary>English word shown, choose the Ukrainian meaning (recognition, capped at Good).</summary>
    ChoiceTargetToNative,

    /// <summary>Ukrainian meaning shown, choose the English word (capped at Good).</summary>
    ChoiceNativeToTarget,

    /// <summary>Match five words with five meanings; warm-up, never graded.</summary>
    Matching,

    /// <summary>Type the English word from the Ukrainian meaning (active recall, the only mode that can reach Easy).</summary>
    TypedRecall,

    /// <summary>Reserved: hear the word (system TTS) and choose or type it.</summary>
    Listening,

    /// <summary>Reserved: fill the gap in a sentence.</summary>
    Cloze,
}

public static class ExerciseModeExtensions
{
    /// <summary>Modes whose successes count towards «Вивчено» (spec 06 §3.2: recall, not recognition).</summary>
    public static bool IsRecall(this ExerciseMode mode) => mode is ExerciseMode.TypedRecall or ExerciseMode.Cloze;

    /// <summary>Modes that never produce a grade.</summary>
    public static bool IsUngraded(this ExerciseMode mode) => mode is ExerciseMode.Matching;

    public static bool IsAvailableInV1(this ExerciseMode mode)
        => mode is ExerciseMode.ChoiceTargetToNative or ExerciseMode.ChoiceNativeToTarget or ExerciseMode.Matching or ExerciseMode.TypedRecall;
}
