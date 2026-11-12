using System.ComponentModel;
using Burdock.Presentation.Services;
using Burdock.Presentation.ViewModels;

namespace Burdock.Presentation.Views;

/// <summary>S-10a «Повторимо»: the view model owns the round; the page only starts it and shakes the pairs on a mistake (static under Reduce Motion).</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "The token source lives between OnAppearing and OnDisappearing; Shell owns the page lifetime.")]
public partial class ReviewPage : ContentPage
{
    private const double ShakeOffset = 6;
    private const uint ShakeStepMilliseconds = 40;

    private readonly ReviewViewModel _viewModel;
    private readonly IMotionPreferences _motion;
    private CancellationTokenSource? _loadCts;

    public ReviewPage(ReviewViewModel viewModel, IMotionPreferences motion)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _motion = motion ?? throw new ArgumentNullException(nameof(motion));
        InitializeComponent();
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _loadCts = new CancellationTokenSource();
        _ = _viewModel.LoadAsync(_loadCts.Token);
    }

    protected override void OnDisappearing()
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = null;
        base.OnDisappearing();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ReviewViewModel.MistakeTick))
        {
            _ = ShakeAsync();
        }
    }

    private async Task ShakeAsync()
    {
        if (_motion.ReduceMotion)
        {
            return;
        }

        await PairsHost.TranslateToAsync(-ShakeOffset, 0, ShakeStepMilliseconds);
        await PairsHost.TranslateToAsync(ShakeOffset, 0, ShakeStepMilliseconds);
        await PairsHost.TranslateToAsync(-ShakeOffset, 0, ShakeStepMilliseconds);
        await PairsHost.TranslateToAsync(0, 0, ShakeStepMilliseconds);
    }
}
