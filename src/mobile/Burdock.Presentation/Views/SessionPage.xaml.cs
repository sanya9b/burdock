using System.ComponentModel;
using Burdock.Presentation.ViewModels;

namespace Burdock.Presentation.Views;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "The token source lives between OnAppearing and OnDisappearing; Shell owns the page lifetime.")]
public partial class SessionPage : ContentPage, IQueryAttributable
{
    private readonly SessionViewModel _viewModel;
    private CancellationTokenSource? _loadCts;

    public SessionPage(SessionViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        BindingContext = _viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query) => _viewModel.ApplyQueryAttributes(query);

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

    /// <summary>Feedback panel slides up with motion.duration.base; a static appearance when the user asked to reduce motion.</summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SessionViewModel.Answered) || !_viewModel.Answered)
        {
            return;
        }

        var duration = Resources.TryGetValue("MotionDurationBase", out var raw) || Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue("MotionDurationBase", out raw) == true
            ? Convert.ToUInt32(raw, System.Globalization.CultureInfo.InvariantCulture)
            : 200u;
        var distance = FeedbackPanel.Height > 0 ? Math.Min(FeedbackPanel.Height, 48) : 24;

        FeedbackPanel.CancelAnimations();
        FeedbackPanel.TranslationY = distance;
        FeedbackPanel.Opacity = 0;
        _ = FeedbackPanel.TranslateToAsync(0, 0, duration, Easing.CubicOut);
        _ = FeedbackPanel.FadeToAsync(1, duration, Easing.CubicOut);
    }
}
