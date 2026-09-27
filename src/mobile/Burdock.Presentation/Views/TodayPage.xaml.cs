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
        _loadCts = new CancellationTokenSource();
        _ = _viewModel.LoadAsync(_loadCts.Token);
    }

    protected override void OnDisappearing()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = null;
        base.OnDisappearing();
    }
}
