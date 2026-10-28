namespace Burdock.Presentation.ViewModels;

/// <summary>Explicit screen state so empty and error states are designed, not accidental (spec 01 section 3.5, spec 03).</summary>
public enum ViewState
{
    Loading,
    Ready,
    Empty,

    /// <summary>S-10 goal_done: the day's words are all met; the primary action becomes «Ще 5 карток».</summary>
    GoalDone,
    Error,
}
