using Burdock.Presentation.Services;
using Burdock.Presentation.ViewModels;

namespace Burdock.Presentation.Views;

/// <summary>S-16 «Нагороди»: the shop with a live preview of the burr; the preview is the one mascot of the screen.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "The token source lives between OnAppearing and OnDisappearing; Shell owns the page lifetime.")]
public partial class RewardsPage : ContentPage
{
    private readonly RewardsViewModel _viewModel;
    private CancellationTokenSource? _loadCts;

    public RewardsPage(RewardsViewModel viewModel, IMotionPreferences motion, IHaptics haptics)
    {
        ArgumentNullException.ThrowIfNull(motion);
        ArgumentNullException.ThrowIfNull(haptics);
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        BindingContext = _viewModel;
        Mascot.ReduceMotion = motion.ReduceMotion;
        Mascot.Poked += (_, _) => haptics.Light();
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
