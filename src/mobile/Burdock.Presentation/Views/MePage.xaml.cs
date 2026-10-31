using Burdock.Presentation.Services;
using Burdock.Presentation.ViewModels;

namespace Burdock.Presentation.Views;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "The token source lives between OnAppearing and OnDisappearing; Shell owns the page lifetime.")]
public partial class MePage : ContentPage
{
    private readonly MeViewModel _viewModel;
    private CancellationTokenSource? _loadCts;

    public MePage(MeViewModel viewModel, IMotionPreferences motion)
    {
        ArgumentNullException.ThrowIfNull(motion);
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        BindingContext = _viewModel;
        Mascot.ReduceMotion = motion.ReduceMotion;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _loadCts = new CancellationTokenSource();
        Mascot.Idle = true;
        _ = _viewModel.LoadAsync(_loadCts.Token);
    }

    protected override void OnDisappearing()
    {
        Mascot.Idle = false;
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = null;
        base.OnDisappearing();
    }
}
