using System.ComponentModel;
using System.Globalization;
using Burdock.Presentation.Services;
using Burdock.Presentation.ViewModels;

namespace Burdock.Presentation.Views;

/// <summary>Plays the L2 celebration on arrival (mascot pop, leaf confetti), counts the thorns up, pulses the flame; all static under Reduce Motion.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "The token source lives between OnAppearing and OnDisappearing; Shell owns the page lifetime.")]
public partial class SessionSummaryPage : ContentPage, IQueryAttributable
{
    private readonly SessionSummaryViewModel _viewModel;
    private readonly IMotionPreferences _motion;
    private readonly IHaptics _haptics;
    private CancellationTokenSource? _cts;
    private bool _played;

    public SessionSummaryPage(SessionSummaryViewModel viewModel, IMotionPreferences motion, IHaptics haptics)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _motion = motion ?? throw new ArgumentNullException(nameof(motion));
        _haptics = haptics ?? throw new ArgumentNullException(nameof(haptics));
        InitializeComponent();
        BindingContext = _viewModel;
        Celebration.ReduceMotion = _motion.ReduceMotion;
        Celebration.Haptics = _haptics;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query) => _viewModel.ApplyQueryAttributes(query);

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _cts = new CancellationTokenSource();
        PlayArrival();
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

    private void PlayArrival()
    {
        if (_played)
        {
            return;
        }

        _played = true;
        _haptics.Success();
        if (_motion.ReduceMotion)
        {
            return;
        }

        MascotImage.Scale = 0.6;
        MascotImage.Opacity = 0;
        _ = MascotImage.FadeToAsync(1, Duration("MotionDurationBase", 200), Easing.CubicOut);
        _ = MascotImage.ScaleToAsync(1, Duration("MotionDurationSlow", 320), Easing.SpringOut);
        if (_viewModel.Accuracy >= SessionSummaryViewModel.CelebrationThreshold)
        {
            Confetti.Play(800);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SessionSummaryViewModel.ShowRewards) || !_viewModel.ShowRewards)
        {
            return;
        }

        if (_motion.ReduceMotion)
        {
            return;
        }

        // Thorns count up with tabular digits over motion.duration.celebrate; the flame pulses once (spec 02 section 3.8 «Свято серії»).
        var from = _viewModel.ThornsBefore;
        var to = _viewModel.ThornsTotal;
        ThornsLabel.Text = from.ToString(CultureInfo.CurrentCulture);
        var counter = new Animation(v => ThornsLabel.Text = ((int)Math.Round(v)).ToString(CultureInfo.CurrentCulture), from, to, Easing.CubicOut);
        counter.Commit(ThornsLabel, "thorns", 16, Duration("MotionDurationCelebrate", 600), finished: (_, _) => ThornsLabel.Text = to.ToString(CultureInfo.CurrentCulture));

        RewardsCard.Opacity = 0;
        RewardsCard.TranslationY = 16;
        _ = RewardsCard.FadeToAsync(1, Duration("MotionDurationBase", 200), Easing.CubicOut);
        _ = RewardsCard.TranslateToAsync(0, 0, Duration("MotionDurationBase", 200), Easing.CubicOut);
        var pulse = new Animation();
        pulse.Add(0, 0.5, new Animation(v => FlameIcon.Scale = v, 1, 1.25, Easing.CubicOut));
        pulse.Add(0.5, 1, new Animation(v => FlameIcon.Scale = v, 1.25, 1, Easing.CubicIn));
        pulse.Commit(FlameIcon, "flame", 16, Duration("MotionDurationCelebrate", 600));
    }

    private uint Duration(string key, uint fallback)
        => Resources.TryGetValue(key, out var raw) || Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out raw) == true
            ? Convert.ToUInt32(raw, CultureInfo.InvariantCulture)
            : fallback;
}
