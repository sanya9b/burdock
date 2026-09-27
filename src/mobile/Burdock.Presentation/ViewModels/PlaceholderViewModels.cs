using CommunityToolkit.Mvvm.ComponentModel;

namespace Burdock.Presentation.ViewModels;

/// <summary>S-13 «Вчити»: session entry points and the focus session (phase 1).</summary>
public sealed partial class LearnViewModel : ObservableObject
{
    [ObservableProperty]
    private ViewState _state = ViewState.Empty;
}

/// <summary>S-14 «Слова»: the learner's library (phase 1).</summary>
public sealed partial class WordsViewModel : ObservableObject
{
    [ObservableProperty]
    private ViewState _state = ViewState.Empty;
}

/// <summary>S-17 «Я»: profile, Pro status, settings (phases 1 and 5).</summary>
public sealed partial class MeViewModel : ObservableObject
{
    [ObservableProperty]
    private ViewState _state = ViewState.Empty;
}
