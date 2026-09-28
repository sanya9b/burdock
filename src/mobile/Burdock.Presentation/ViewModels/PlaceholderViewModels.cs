using Burdock.Presentation.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Burdock.Presentation.ViewModels;

/// <summary>S-13 «Вчити»: session entry points; the focus session comes with the iOS surfaces phase.</summary>
public sealed partial class LearnViewModel(INavigationService navigation) : ObservableObject
{
    [ObservableProperty]
    private ViewState _state = ViewState.Ready;

    [RelayCommand]
    private Task StartQuickAsync(CancellationToken cancellationToken)
        => navigation.GoToAsync(Routes.Learn.Session, new Dictionary<string, object>(StringComparer.Ordinal) { ["size"] = "5" }, cancellationToken);

    [RelayCommand]
    private Task StartFullAsync(CancellationToken cancellationToken)
        => navigation.GoToAsync(Routes.Learn.Session, new Dictionary<string, object>(StringComparer.Ordinal) { ["size"] = "10" }, cancellationToken);
}
