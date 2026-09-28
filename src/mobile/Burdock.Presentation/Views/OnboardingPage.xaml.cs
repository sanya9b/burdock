using System.ComponentModel;
using Burdock.Presentation.Services;
using Burdock.Presentation.ViewModels;

namespace Burdock.Presentation.Views;

/// <summary>Hosts the onboarding steps; slides each new step in and hands the window to the Shell when the learner finishes.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "The token source lives between OnAppearing and OnDisappearing; the window owns the page lifetime.")]
public partial class OnboardingPage : ContentPage
{
    private static readonly Uri PrivacyPolicy = new("https://burdock.org/privacy");

    private readonly OnboardingViewModel _viewModel;
    private readonly IServiceProvider _services;
    private readonly IMotionPreferences _motion;
    private CancellationTokenSource? _cts;
    private bool _handedOver;

    public OnboardingPage(OnboardingViewModel viewModel, IServiceProvider services, IMotionPreferences motion)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _motion = motion ?? throw new ArgumentNullException(nameof(motion));
        InitializeComponent();
        BindingContext = _viewModel;
        _viewModel.Completed += OnCompleted;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _cts = new CancellationTokenSource();
        _ = _viewModel.LoadAsync(_cts.Token);
    }

    protected override void OnDisappearing()
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed()
    {
        if (_viewModel.CanGoBack)
        {
            _viewModel.BackCommand.Execute(null);
            return true;
        }

        return base.OnBackButtonPressed();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(OnboardingViewModel.StepIndex) || _motion.ReduceMotion)
        {
            return;
        }

        StepHost.CancelAnimations();
        StepHost.Opacity = 0;
        StepHost.TranslationX = 24;
        _ = StepHost.FadeToAsync(1, 200, Easing.CubicOut);
        _ = StepHost.TranslateToAsync(0, 0, 200, Easing.CubicOut);
    }

    private async void OnCompleted(object? sender, EventArgs e)
    {
        if (_handedOver || Window is not { } window)
        {
            return;
        }

        _handedOver = true;
        if (!_motion.ReduceMotion)
        {
            await this.FadeToAsync(0, 180, Easing.CubicIn).ConfigureAwait(true);
        }

        window.Page = _services.GetRequiredService<AppShell>();
    }

    private async void OnPrivacyPolicyTapped(object? sender, TappedEventArgs e)
    {
        try
        {
            await Launcher.Default.OpenAsync(PrivacyPolicy).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // No browser available: the link is informational, nothing else to do.
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }
}
