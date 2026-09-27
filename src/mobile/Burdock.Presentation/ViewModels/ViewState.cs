namespace Burdock.Presentation.ViewModels;

/// <summary>Explicit screen state so empty and error states are designed, not accidental (spec 01 section 3.5, spec 03).</summary>
public enum ViewState
{
    Loading,
    Ready,
    Empty,
    Error,
}
