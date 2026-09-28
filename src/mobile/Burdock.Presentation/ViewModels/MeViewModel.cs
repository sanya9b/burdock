using Burdock.Presentation.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Burdock.Presentation.ViewModels;

/// <summary>S-17 «Я»: anonymous learner card and the entry to settings; linking, Pro and statistics arrive in phases 2 and 5.</summary>
public sealed partial class MeViewModel(INavigationService navigation) : ObservableObject
{
    [ObservableProperty]
    private ViewState _state = ViewState.Ready;

    [RelayCommand]
    private Task OpenSettingsAsync(CancellationToken cancellationToken) => navigation.GoToAsync(Routes.Me.Settings, null, cancellationToken);
}
