using System.ComponentModel;
using System.Globalization;
using Burdock.Presentation.Services;
using Burdock.Presentation.ViewModels;

namespace Burdock.Presentation.Views;

/// <summary>Signature answer animations (spec 02 section 3.8): correct = feedback slides up and the combo chip bounces; wrong = two ±6 pt shakes. Haptics follow. Static under Reduce Motion.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "The token source lives between OnAppearing and OnDisappearing; Shell owns the page lifetime.")]
public partial class SessionPage : ContentPage, IQueryAttributable
{
    private readonly SessionViewModel _viewModel;
    private readonly IMotionPreferences _motion;
    private readonly IHaptics _haptics;
    private CancellationTokenSource? _loadCts;

    public SessionPage(SessionViewModel viewModel, IMotionPreferences motion, IHaptics haptics)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _motion = motion ?? throw new ArgumentNullException(nameof(motion));
        _haptics = haptics ?? throw new ArgumentNullException(nameof(haptics));
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

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SessionViewModel.Answered) when _viewModel.Answered:
                SlideFeedbackIn();
                break;
            case nameof(SessionViewModel.AnswerTick):
                PlayAnswer(_viewModel.LastAnswerCorrect);
                break;
            case nameof(SessionViewModel.ShowCombo) when _viewModel.ShowCombo:
            case nameof(SessionViewModel.ComboCount) when _viewModel.ShowCombo:
                BounceCombo();
                break;
        }
    }

    private void SlideFeedbackIn()
    {
        if (_motion.ReduceMotion)
        {
            return;
        }

        var duration = Duration("MotionDurationBase", 200);
        var distance = FeedbackPanel.Height > 0 ? Math.Min(FeedbackPanel.Height, 48) : 24;
        FeedbackPanel.CancelAnimations();
        FeedbackPanel.TranslationY = distance;
        FeedbackPanel.Opacity = 0;
        _ = FeedbackPanel.TranslateToAsync(0, 0, duration, Easing.CubicOut);
        _ = FeedbackPanel.FadeToAsync(1, duration, Easing.CubicOut);
    }

    private void PlayAnswer(bool correct)
    {
        if (correct)
        {
            _haptics.Success();
            return;
        }

        _haptics.Error();
        if (_motion.ReduceMotion)
        {
            return;
        }

        var fast = Duration("MotionDurationFast", 120);
        var shake = new Animation();
        shake.Add(0, 0.25, new Animation(v => ExerciseHost.TranslationX = v, 0, -6, Easing.SinOut));
        shake.Add(0.25, 0.5, new Animation(v => ExerciseHost.TranslationX = v, -6, 6, Easing.SinInOut));
        shake.Add(0.5, 0.75, new Animation(v => ExerciseHost.TranslationX = v, 6, -6, Easing.SinInOut));
        shake.Add(0.75, 1, new Animation(v => ExerciseHost.TranslationX = v, -6, 0, Easing.SinIn));
        shake.Commit(ExerciseHost, "shake", 16, fast * 2, finished: (_, _) => ExerciseHost.TranslationX = 0);
    }

    private void BounceCombo()
    {
        if (_motion.ReduceMotion)
        {
            return;
        }

        ComboChip.CancelAnimations();
        ComboChip.Scale = 0.6;
        _ = ComboChip.ScaleToAsync(1, Duration("MotionDurationSlow", 320), Easing.SpringOut);
    }

    private uint Duration(string key, uint fallback)
        => Resources.TryGetValue(key, out var raw) || Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out raw) == true
            ? Convert.ToUInt32(raw, CultureInfo.InvariantCulture)
            : fallback;
}
