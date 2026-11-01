using System.Diagnostics;
using Burdock.Domain.Rewards;
using Burdock.Presentation.Controls.Mascot;

namespace Burdock.Presentation.Controls;

/// <summary>
/// Procedural Реп’яшок (spec 02 §3.10 geometry and growth stages, spec 07 §3.2.7): body, hooks, face by <see cref="Mood"/>, leaves, bud and bloom
/// by <see cref="Stage"/>. Growth is a continuous scalar, so the burr grows a little after every session and not only on a level-up.
/// All motion comes from one <see cref="MascotAnimator"/> driven by a single frame timer (spec 02 §3.8 «Життя Реп’яшка»): breathing, blinks,
/// gaze, gestures while <see cref="Idle"/>; mood crossfades, <see cref="FeedAsync"/>, the growth spring and the <see cref="Poke"/> reaction
/// whenever they happen. Everything is a still frame under <see cref="ReduceMotion"/>. Colours come from the style (primitive ramps).
/// </summary>
public sealed partial class MascotView : GraphicsView, IDrawable
{
    public static readonly BindableProperty StageProperty =
        BindableProperty.Create(nameof(Stage), typeof(GrowthStage), typeof(MascotView), GrowthStage.Burr, propertyChanged: (b, _, _) => ((MascotView)b).OnGrowthChanged());

    public static readonly BindableProperty ProgressProperty =
        BindableProperty.Create(nameof(Progress), typeof(double), typeof(MascotView), 0.5, propertyChanged: (b, _, _) => ((MascotView)b).OnGrowthChanged());

    public static readonly BindableProperty MoodProperty =
        BindableProperty.Create(nameof(Mood), typeof(MascotMood), typeof(MascotView), MascotMood.Proud, propertyChanged: (b, _, _) => ((MascotView)b).OnMoodChanged());

    public static readonly BindableProperty IdleProperty =
        BindableProperty.Create(nameof(Idle), typeof(bool), typeof(MascotView), false, propertyChanged: (b, _, _) => ((MascotView)b).OnIdleChanged());

    public static readonly BindableProperty ReduceMotionProperty =
        BindableProperty.Create(nameof(ReduceMotion), typeof(bool), typeof(MascotView), false, propertyChanged: (b, _, _) => ((MascotView)b).OnReduceMotionChanged());

    public static readonly BindableProperty GrowthDurationProperty =
        BindableProperty.Create(nameof(GrowthDuration), typeof(int), typeof(MascotView), 600);

    public static readonly BindableProperty BodyColorProperty = Colour(nameof(BodyColor), Colors.MediumPurple);
    public static readonly BindableProperty BodyLightColorProperty = Colour(nameof(BodyLightColor), Colors.Plum);
    public static readonly BindableProperty BodyShadeColorProperty = Colour(nameof(BodyShadeColor), Colors.Indigo);
    public static readonly BindableProperty HookColorProperty = Colour(nameof(HookColor), Colors.GreenYellow);
    public static readonly BindableProperty HookTipColorProperty = Colour(nameof(HookTipColor), Colors.OliveDrab);
    public static readonly BindableProperty EyeColorProperty = Colour(nameof(EyeColor), Colors.White);
    public static readonly BindableProperty PupilColorProperty = Colour(nameof(PupilColor), Colors.Black);
    public static readonly BindableProperty BlushColorProperty = Colour(nameof(BlushColor), Colors.Pink);
    public static readonly BindableProperty LeafColorProperty = Colour(nameof(LeafColor), Colors.YellowGreen);
    public static readonly BindableProperty LeafEdgeColorProperty = Colour(nameof(LeafEdgeColor), Colors.DarkOliveGreen);
    public static readonly BindableProperty BloomColorProperty = Colour(nameof(BloomColor), Colors.Thistle);
    public static readonly BindableProperty BloomEdgeColorProperty = Colour(nameof(BloomEdgeColor), Colors.LightCoral);
    public static readonly BindableProperty AccentColorProperty = Colour(nameof(AccentColor), Colors.Gold);

    // Quiet idle (breathing, sway, hook wave) moves well under a pixel per frame, so 20 fps is invisible; blinks, glances, gestures, feeding,
    // growth and mood changes switch to 60 fps for their duration. The profile screen may stay open for a while, so the quiet rate is the budget.
    private static readonly TimeSpan QuietFrameInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan BusyFrameInterval = TimeSpan.FromMilliseconds(16);

    // A field initialiser, so the property-changed handlers the implicit style fires from the base constructor already find it.
    private readonly MascotAnimator _animator = new(MascotMood.Proud, GrowthRules.Adult.Scalar, new Random());
    private readonly Stopwatch _clock = new();
    private IDispatcherTimer? _timer;
    private Window? _window;
    private double _lastTick;
    private bool _windowActive = true;
    private bool _hasDrawn;
    private bool _holdGrowth;

    public MascotView()
    {
        Drawable = this;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => Poke();
        GestureRecognizers.Add(tap);
        _animator.Burst += () => Burst?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The burst of a stage-up ceremony, when the new form springs out; pages play the success haptic.</summary>
    public event EventHandler? Burst;

    /// <summary>The learner touched the burr (after the reaction started); pages play the haptic.</summary>
    public event EventHandler? Poked;

    public GrowthStage Stage { get => (GrowthStage)GetValue(StageProperty); set => SetValue(StageProperty, value); }

    /// <summary>0..1 inside the stage (<see cref="GrowthSnapshot.Progress"/>).</summary>
    public double Progress { get => (double)GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }

    public MascotMood Mood { get => (MascotMood)GetValue(MoodProperty); set => SetValue(MoodProperty, value); }

    /// <summary>Autonomous life while on screen (breathing, blinks, gaze, gestures); ignored under Reduce Motion.</summary>
    public bool Idle { get => (bool)GetValue(IdleProperty); set => SetValue(IdleProperty, value); }

    public bool ReduceMotion { get => (bool)GetValue(ReduceMotionProperty); set => SetValue(ReduceMotionProperty, value); }

    /// <summary>Growth spring settle time in ms (motion.duration.celebrate).</summary>
    public int GrowthDuration { get => (int)GetValue(GrowthDurationProperty); set => SetValue(GrowthDurationProperty, value); }

    public Color BodyColor { get => (Color)GetValue(BodyColorProperty); set => SetValue(BodyColorProperty, value); }

    public Color BodyLightColor { get => (Color)GetValue(BodyLightColorProperty); set => SetValue(BodyLightColorProperty, value); }

    public Color BodyShadeColor { get => (Color)GetValue(BodyShadeColorProperty); set => SetValue(BodyShadeColorProperty, value); }

    public Color HookColor { get => (Color)GetValue(HookColorProperty); set => SetValue(HookColorProperty, value); }

    public Color HookTipColor { get => (Color)GetValue(HookTipColorProperty); set => SetValue(HookTipColorProperty, value); }

    public Color EyeColor { get => (Color)GetValue(EyeColorProperty); set => SetValue(EyeColorProperty, value); }

    public Color PupilColor { get => (Color)GetValue(PupilColorProperty); set => SetValue(PupilColorProperty, value); }

    public Color BlushColor { get => (Color)GetValue(BlushColorProperty); set => SetValue(BlushColorProperty, value); }

    public Color LeafColor { get => (Color)GetValue(LeafColorProperty); set => SetValue(LeafColorProperty, value); }

    public Color LeafEdgeColor { get => (Color)GetValue(LeafEdgeColorProperty); set => SetValue(LeafEdgeColorProperty, value); }

    public Color BloomColor { get => (Color)GetValue(BloomColorProperty); set => SetValue(BloomColorProperty, value); }

    public Color BloomEdgeColor { get => (Color)GetValue(BloomEdgeColorProperty); set => SetValue(BloomEdgeColorProperty, value); }

    /// <summary>Gold of celebrations: party hat, confetti, the aura of the great burr.</summary>
    public Color AccentColor { get => (Color)GetValue(AccentColorProperty); set => SetValue(AccentColorProperty, value); }

    /// <summary>Sets the stage and progress together; <paramref name="animate"/> springs the growth from the current look, otherwise it snaps.</summary>
    public void SetGrowth(GrowthStage stage, double progress, bool animate)
    {
        var wasDrawn = _hasDrawn;
        _hasDrawn = animate && wasDrawn;
        Stage = stage;
        Progress = progress;
        _hasDrawn = wasDrawn;
        if (!animate)
        {
            _animator.SetGrowth(Target(), animate: false, GrowthSettle());
            Invalidate();
        }
    }

    /// <summary>
    /// Feeding (spec 02 §3.8 «Підживлення»): <paramref name="seeds"/> lime seeds arc in from the edges over <paramref name="durationMs"/>, the burr
    /// watches them, takes a «nom» per seed and a gulp on the last. Completes after the gulp, so the caller can then raise <see cref="Stage"/> /
    /// <see cref="Progress"/> and let the growth spring play. No-op under Reduce Motion.
    /// </summary>
    public Task FeedAsync(int seeds, uint durationMs)
    {
        if (ReduceMotion || seeds <= 0)
        {
            return Task.CompletedTask;
        }

        var done = new TaskCompletionSource();
        void Finished()
        {
            _animator.FeedFinished -= Finished;
            done.TrySetResult();
        }

        _animator.FeedFinished += Finished;
        _animator.StartFeed(seeds, durationMs / 1000.0);
        SyncTicker();
        return done.Task;
    }

    /// <summary>
    /// Stage-up ceremony (spec 02 §3.8 «Переродження»): the burr holds its old form, gathers light with closed eyes, shivers, flashes, and the
    /// new form with its new parts bursts out, surprised then delighted. Completes when the ceremony is over. Under Reduce Motion it snaps.
    /// </summary>
    public Task EvolveAsync(GrowthStage stage, double progress)
    {
        if (ReduceMotion)
        {
            SetGrowth(stage, progress, animate: false);
            return Task.CompletedTask;
        }

        _holdGrowth = true;
        Stage = stage;
        Progress = progress;
        _holdGrowth = false;

        var done = new TaskCompletionSource();
        void Finished()
        {
            _animator.MetamorphosisFinished -= Finished;
            done.TrySetResult();
        }

        _animator.MetamorphosisFinished += Finished;
        _animator.StartMetamorphosis(Target());
        SyncTicker();
        Invalidate();
        return done.Task;
    }

    /// <summary>A touch: a happy hop with lifted brows (a stir when asleep); only the event under Reduce Motion.</summary>
    public void Poke()
    {
        if (!ReduceMotion)
        {
            _animator.Poke();
            SyncTicker();
        }

        Poked?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The frame the drawing uses right now (still under Reduce Motion).</summary>
    internal MascotPose CurrentPose => ReduceMotion ? MascotPose.Rest(Mood, Target()) : _animator.Pose;

    private static BindableProperty Colour(string name, Color fallback)
        => BindableProperty.Create(name, typeof(Color), typeof(MascotView), fallback, propertyChanged: (b, _, _) => ((MascotView)b).Invalidate());

    private double Target() => new GrowthSnapshot(Stage, 0, 0, Math.Clamp(Progress, 0, 1), null, 0).Scalar;

    private double GrowthSettle() => Math.Max(50, GrowthDuration) / 1000.0;

    private void OnGrowthChanged()
    {
        if (_holdGrowth)
        {
            return;
        }

        _animator.SetGrowth(Target(), animate: _hasDrawn && !ReduceMotion, GrowthSettle());
        SyncTicker();
        Invalidate();
    }

    private void OnMoodChanged()
    {
        _animator.SetMood(Mood, animate: _hasDrawn && !ReduceMotion);
        SyncTicker();
        Invalidate();
    }

    private void OnIdleChanged()
    {
        _animator.Idle = Idle && !ReduceMotion;
        SyncTicker();
    }

    private void OnReduceMotionChanged()
    {
        _animator.Idle = Idle && !ReduceMotion;
        if (ReduceMotion)
        {
            _animator.SetMood(Mood, animate: false);
            _animator.SetGrowth(Target(), animate: false, GrowthSettle());
        }

        SyncTicker();
        Invalidate();
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        // Remember the window: by Unloaded the element may already be detached from it.
        _window = Window;
        if (_window is not null)
        {
            _window.Activated += OnWindowActivated;
            _window.Deactivated += OnWindowDeactivated;
        }

        _windowActive = true;
        SyncTicker();
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        if (_window is not null)
        {
            _window.Activated -= OnWindowActivated;
            _window.Deactivated -= OnWindowDeactivated;
            _window = null;
        }

        StopTicker();
    }

    private void OnWindowActivated(object? sender, EventArgs e)
    {
        _windowActive = true;
        SyncTicker();
    }

    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        _windowActive = false;
        StopTicker();
    }

    private void SyncTicker()
    {
        var wanted = IsLoaded && _windowActive && !ReduceMotion && (Idle || _animator.IsBusy);
        if (wanted)
        {
            StartTicker();
        }
        else
        {
            StopTicker();
        }
    }

    private void StartTicker()
    {
        if (_timer is not null)
        {
            return;
        }

        _clock.Restart();
        _lastTick = 0;
        _timer = Dispatcher.CreateTimer();
        _timer.Interval = _animator.IsLively ? BusyFrameInterval : QuietFrameInterval;
        _timer.IsRepeating = true;
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void StopTicker()
    {
        if (_timer is null)
        {
            return;
        }

        _timer.Stop();
        _timer.Tick -= OnTick;
        _timer = null;
        _clock.Stop();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var now = _clock.Elapsed.TotalSeconds;
        _animator.Advance(now - _lastTick);
        _lastTick = now;
        Invalidate();
        if (!Idle && !_animator.IsBusy)
        {
            StopTicker();
            return;
        }

        var interval = _animator.IsLively ? BusyFrameInterval : QuietFrameInterval;
        if (_timer is not null && _timer.Interval != interval)
        {
            _timer.Interval = interval;
        }
    }
}
