using System.ComponentModel;
using System.Globalization;
using Burdock.Presentation.Services;
using Burdock.Presentation.ViewModels;

namespace Burdock.Presentation.Views;

/// <summary>
/// Plays the L2 celebration on arrival (mascot pop, leaf confetti), counts the thorns up, pulses the flame, then feeds the burr with the XP earned
/// and lets it grow (spec 02 §3.9 «Підживлення»); all static under Reduce Motion.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "The token source lives between OnAppearing and OnDisappearing; Shell owns the page lifetime.")]
public partial class SessionSummaryPage : ContentPage, IQueryAttributable
{
    private readonly SessionSummaryViewModel _viewModel;
    private readonly IMotionPreferences _motion;
    private readonly IHaptics _haptics;
    private CancellationTokenSource? _cts;
    private bool _played;
    private bool _mascotShown;

    public SessionSummaryPage(SessionSummaryViewModel viewModel, IMotionPreferences motion, IHaptics haptics)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _motion = motion ?? throw new ArgumentNullException(nameof(motion));
        _haptics = haptics ?? throw new ArgumentNullException(nameof(haptics));
        InitializeComponent();
        BindingContext = _viewModel;
        Celebration.ReduceMotion = _motion.ReduceMotion;
        Celebration.Haptics = _haptics;
        Mascot.ReduceMotion = _motion.ReduceMotion;
        Mascot.Poked += (_, _) => _haptics.Light();
        _viewModel.DeferCelebrations = !_motion.ReduceMotion;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query) => _viewModel.ApplyQueryAttributes(query);

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _cts = new CancellationTokenSource();
        Mascot.Idle = true;
        PlayArrival();
        if (_viewModel.MascotReady)
        {
            ShowMascot();
        }

        _ = _viewModel.LoadAsync(_cts.Token);
    }

    protected override void OnDisappearing()
    {
        Mascot.Idle = false;
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

        if (_viewModel.Accuracy >= SessionSummaryViewModel.CelebrationThreshold)
        {
            Confetti.Play(800);
        }
    }

    /// <summary>The burr appears once its stage before the session is known: pop-in with overshoot, or a plain appearance under Reduce Motion.</summary>
    private void ShowMascot()
    {
        if (_mascotShown)
        {
            return;
        }

        _mascotShown = true;
        Mascot.SetGrowth(_viewModel.MascotStage, _viewModel.MascotProgress, animate: false);
        if (_motion.ReduceMotion)
        {
            Mascot.Opacity = 1;
            return;
        }

        Mascot.Scale = 0.6;
        Mascot.Opacity = 0;
        _ = Mascot.FadeToAsync(1, Duration("MotionDurationBase", 200), Easing.CubicOut);
        _ = Mascot.ScaleToAsync(1, Duration("MotionDurationSlow", 320), Easing.SpringOut);
    }

    private async Task FeedAsync()
    {
        if (_viewModel.GrowthAfter is not { } after)
        {
            return;
        }

        if (_motion.ReduceMotion)
        {
            Mascot.SetGrowth(after.Stage, after.Progress, animate: false);
            _viewModel.BeginCelebrations();
            return;
        }

        // Let the pop-in settle, then the seeds fly in, the burr gulps and grows; only then may a celebration card cover it.
        var celebrate = Duration("MotionDurationCelebrate", 600);
        await Task.Delay((int)Duration("MotionDurationSlow", 320)).ConfigureAwait(true);
        await Mascot.FeedAsync(_viewModel.FeedSeeds, celebrate).ConfigureAwait(true);
        _haptics.Light();
        Mascot.SetGrowth(after.Stage, after.Progress, animate: true);
        await Task.Delay((int)celebrate).ConfigureAwait(true);
        _viewModel.BeginCelebrations();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SessionSummaryViewModel.MascotReady) when _viewModel.MascotReady:
                ShowMascot();
                return;
            case nameof(SessionSummaryViewModel.GrowthAfter):
                _ = FeedAsync();
                return;
            case nameof(SessionSummaryViewModel.ShowRewards) when _viewModel.ShowRewards && !_motion.ReduceMotion:
                break;
            default:
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
