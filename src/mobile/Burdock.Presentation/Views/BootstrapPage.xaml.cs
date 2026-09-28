using Burdock.Presentation.Services;
using Burdock.Presentation.ViewModels;

namespace Burdock.Presentation.Views;

/// <summary>Animated loading screen: the brand mark bobs and wobbles (static under Reduce Motion) until the content is ready, then the Shell takes the window.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "The token source lives between OnAppearing and the hand-over; the window owns the page lifetime.")]
public partial class BootstrapPage : ContentPage
{
    /// <summary>The screen stays at least this long so the animation reads as a moment, not a flicker.</summary>
    private const int MinimumVisibleMilliseconds = 900;

    /// <summary>Safety net when the platform never reports the splash dismissal (warm starts, other platforms).</summary>
    private static readonly TimeSpan SystemSplashTimeout = TimeSpan.FromSeconds(2);

    private readonly BootstrapViewModel _viewModel;
    private readonly IServiceProvider _services;
    private readonly IMotionPreferences _motion;
    private readonly ISystemSplash _systemSplash;
    private CancellationTokenSource? _cts;
    private bool _handedOver;

    public BootstrapPage(BootstrapViewModel viewModel, IServiceProvider services, IMotionPreferences motion, ISystemSplash systemSplash)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _motion = motion ?? throw new ArgumentNullException(nameof(motion));
        _systemSplash = systemSplash ?? throw new ArgumentNullException(nameof(systemSplash));
        InitializeComponent();
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _cts = new CancellationTokenSource();
        StartMarkLoop();
        _ = RunAsync(_cts.Token);
    }

    protected override void OnDisappearing()
    {
        Mark.AbortAnimation("bob");
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        base.OnDisappearing();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var completed = new TaskCompletionSource();
        _viewModel.Completed += (_, _) => completed.TrySetResult();
        var work = _viewModel.RunAsync(cancellationToken);

        // The minimum visible time starts once the platform splash (which shows the same mark) has left the screen.
        await _systemSplash.WaitForDismissalAsync(SystemSplashTimeout).ConfigureAwait(true);
        var shownAt = Environment.TickCount64;
        await work.ConfigureAwait(true);
        if (!completed.Task.IsCompleted)
        {
            return; // error state: the retry button re-runs the command, Completed fires later
        }

        var remaining = MinimumVisibleMilliseconds - (int)(Environment.TickCount64 - shownAt);
        if (remaining > 0)
        {
            try
            {
                await Task.Delay(remaining, cancellationToken).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        await HandOverAsync().ConfigureAwait(true);
    }

    private async Task HandOverAsync()
    {
        if (_handedOver || Window is not { } window)
        {
            return;
        }

        _handedOver = true;
        Mark.AbortAnimation("bob");
        if (!_motion.ReduceMotion)
        {
            await this.FadeToAsync(0, 180, Easing.CubicIn).ConfigureAwait(true);
        }

        // First run goes through the onboarding (spec 03 flow 3.5.1); later runs open the Shell directly.
        var completed = await _services.GetRequiredService<Burdock.Application.UseCases.Onboarding.IsOnboardingCompletedUseCase>().ExecuteAsync(CancellationToken.None).ConfigureAwait(true);
        window.Page = completed ? _services.GetRequiredService<AppShell>() : _services.GetRequiredService<OnboardingPage>();
    }

    /// <summary>Gentle bob with a hook wobble of the mark, looping; motion.duration.slow per half-cycle keeps it calm (spec 02 section 3.8).</summary>
    private void StartMarkLoop()
    {
        if (_motion.ReduceMotion)
        {
            return;
        }

        var bob = new Animation();
        bob.Add(0, 0.5, new Animation(v => Mark.TranslationY = v, 0, -14, Easing.SinInOut));
        bob.Add(0.5, 1, new Animation(v => Mark.TranslationY = v, -14, 0, Easing.SinInOut));
        bob.Add(0, 0.25, new Animation(v => Mark.Rotation = v, 0, 5, Easing.SinInOut));
        bob.Add(0.25, 0.75, new Animation(v => Mark.Rotation = v, 5, -5, Easing.SinInOut));
        bob.Add(0.75, 1, new Animation(v => Mark.Rotation = v, -5, 0, Easing.SinInOut));
        bob.Commit(Mark, "bob", 16, 1400, repeat: () => !_handedOver);
    }
}
