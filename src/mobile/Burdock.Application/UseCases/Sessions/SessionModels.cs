using Burdock.Domain.Learning;
using Burdock.Domain.Vocabulary;

namespace Burdock.Application.UseCases.Sessions;

/// <summary>One step of a session. The UI switches on the concrete type.</summary>
public abstract record SessionItem(WordEntry Entry, ExerciseMode Mode);

/// <summary>Multiple choice: the prompt is the English word (TargetToNative) or the Ukrainian meaning (NativeToTarget).</summary>
public sealed record ChoiceItem(WordEntry Entry, ExerciseMode Mode, string Prompt, IReadOnlyList<string> Options, int CorrectIndex) : SessionItem(Entry, Mode);

/// <summary>Typed recall: Ukrainian meaning shown, English word expected.</summary>
public sealed record TypedItem(WordEntry Entry, string Prompt, IReadOnlyList<string> AcceptedForms) : SessionItem(Entry, ExerciseMode.TypedRecall);

/// <summary>Warm-up: match words with meanings; never graded (spec 06 §3.6).</summary>
public sealed record MatchingItem(IReadOnlyList<WordEntry> Entries) : SessionItem(Entries[0], ExerciseMode.Matching);

/// <summary>A planned session (spec 06 §3.7): mixed modes, at least <see cref="LearningPolicy.MinGradedCardsForLearningDay"/> graded cards when content allows.</summary>
public sealed record SessionPlan(IReadOnlyList<SessionItem> Items, SessionDrill? Drill = null)
{
    public int GradedCount => Items.Count(i => !i.Mode.IsUngraded());
}

/// <summary>What the UI needs after grading one answer.</summary>
public sealed record GradeResult(Rating Rating, CardState State, DateTimeOffset Due, double Retrievability, string CorrectAnswer, bool BecameLearned);

/// <summary>Session summary shown on S-12d.</summary>
public sealed record SessionSummary(int Graded, int Correct, int NewIntroduced, int Learned, TimeSpan Duration, bool CountsAsLearningDay);
