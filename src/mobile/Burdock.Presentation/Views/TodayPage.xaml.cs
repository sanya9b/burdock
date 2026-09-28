using Burdock.Presentation.ViewModels;

namespace Burdock.Presentation.Views;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "The token source lives between OnAppearing and OnDisappearing; Shell owns the page lifetime.")]
public partial class TodayPage : ContentPage
{
    private readonly TodayViewModel _viewModel;
    private CancellationTokenSource? _loadCts;

    public TodayPage(TodayViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (Microsoft.Maui.Controls.Application.Current is { } app)
        {
            app.RequestedThemeChanged += OnThemeChanged;
        }

        _loadCts = new CancellationTokenSource();
        _ = _viewModel.LoadAsync(_loadCts.Token);
    }

    protected override void OnDisappearing()
    {
        if (Microsoft.Maui.Controls.Application.Current is { } app)
        {
            app.RequestedThemeChanged -= OnThemeChanged;
        }

        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = null;
        base.OnDisappearing();
    }

    // Topic colours of the strip are resolved by a converter at bind time, so a theme switch reloads the plan with fresh tiles.
    private void OnThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        if (_loadCts is { IsCancellationRequested: false } cts)
        {
            _ = _viewModel.LoadAsync(cts.Token);
        }
    }
}
