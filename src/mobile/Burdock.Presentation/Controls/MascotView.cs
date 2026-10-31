using Burdock.Domain.Rewards;

namespace Burdock.Presentation.Controls;

/// <summary>
/// Procedural Реп’яшок (spec 02 §3.10 geometry and growth stages, spec 07 §3.2.7): body, hooks, face by <see cref="Mood"/>, leaves, bud and bloom by
/// <see cref="Stage"/>. Growth is a continuous scalar, so the burr grows a little after every session and not only on a level-up.
/// Animations: growth spring when the stage or progress changes, <see cref="FeedAsync"/> (XP seeds fly in, the burr gulps), idle breathing with
/// blinks. Everything is static under <see cref="ReduceMotion"/>. Colours come from the style (primitive ramps, as for every illustration).
/// </summary>
public sealed partial class MascotView : GraphicsView, IDrawable
{
    public static readonly BindableProperty StageProperty =
        BindableProperty.Create(nameof(Stage), typeof(GrowthStage), typeof(MascotView), GrowthStage.Burr, propertyChanged: (b, _, _) => ((MascotView)b).OnGrowthChanged());

    public static readonly BindableProperty ProgressProperty =
        BindableProperty.Create(nameof(Progress), typeof(double), typeof(MascotView), 0.5, propertyChanged: (b, _, _) => ((MascotView)b).OnGrowthChanged());

    public static readonly BindableProperty MoodProperty =
        BindableProperty.Create(nameof(Mood), typeof(MascotMood), typeof(MascotView), MascotMood.Proud, propertyChanged: (b, _, _) => ((MascotView)b).Invalidate());

    public static readonly BindableProperty IdleProperty =
        BindableProperty.Create(nameof(Idle), typeof(bool), typeof(MascotView), false, propertyChanged: (b, _, _) => ((MascotView)b).SyncIdle());

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

    private const string GrowAnimation = "mascot-grow";
    private const string FeedAnimation = "mascot-feed";
    private const string GulpAnimation = "mascot-gulp";
    private const string IdleAnimation = "mascot-idle";
    private const string BlinkAnimation = "mascot-blink";
    private const int MaxSeeds = 12;

    private readonly List<Seed> _seeds = [];
    private readonly Random _random = new();
    private double _shown = GrowthRules.Adult.Scalar;
    private double _feedProgress = 1;
    private double _squashX = 1;
    private double _squashY = 1;
    private double _breath = 1;
    private double _eyeOpen = 1;
    private bool _hasDrawn;
    private bool _idleRunning;
    private int _idleCycles;

    public MascotView()
    {
        Drawable = this;
        Loaded += (_, _) => SyncIdle();
        Unloaded += (_, _) => StopIdle();
    }

    public GrowthStage Stage { get => (GrowthStage)GetValue(StageProperty); set => SetValue(StageProperty, value); }

    /// <summary>0..1 inside the stage (<see cref="GrowthSnapshot.Progress"/>).</summary>
    public double Progress { get => (double)GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }

    public MascotMood Mood { get => (MascotMood)GetValue(MoodProperty); set => SetValue(MoodProperty, value); }

    /// <summary>Breathing and blinking while on screen; ignored under Reduce Motion.</summary>
    public bool Idle { get => (bool)GetValue(IdleProperty); set => SetValue(IdleProperty, value); }

    public bool ReduceMotion { get => (bool)GetValue(ReduceMotionProperty); set => SetValue(ReduceMotionProperty, value); }

    /// <summary>Growth spring length in ms (motion.duration.celebrate).</summary>
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
        if (!animate)
        {
            _hasDrawn = false;
        }

        Stage = stage;
        Progress = progress;
        _hasDrawn = wasDrawn;
        if (!animate)
        {
            this.AbortAnimation(GrowAnimation);
            _shown = Target();
            Invalidate();
        }
    }

    /// <summary>
    /// Feeding (spec 02 §3.9 «Підживлення»): <paramref name="seeds"/> lime seeds fly in from the edges and the burr gulps once. Completes when the
    /// gulp ends, so the caller can then raise <see cref="Stage"/>/<see cref="Progress"/> and let the growth spring play. No-op under Reduce Motion.
    /// </summary>
    public Task FeedAsync(int seeds, uint durationMs)
    {
        if (ReduceMotion || seeds <= 0)
        {
            return Task.CompletedTask;
        }

        this.AbortAnimation(FeedAnimation);
        this.AbortAnimation(GulpAnimation);
        _seeds.Clear();
        var count = Math.Min(seeds, MaxSeeds);
        for (var i = 0; i < count; i++)
        {
            _seeds.Add(new Seed(
                Angle: -150 + _random.NextDouble() * 120 + (i % 2 == 0 ? 0 : 180),
                Delay: i / (double)count * 0.45,
                Size: 0.028 + _random.NextDouble() * 0.014));
        }

        var done = new TaskCompletionSource();
        new Animation(v => { _feedProgress = v; Invalidate(); }, 0, 1)
            .Commit(this, FeedAnimation, 16, durationMs, Easing.Linear, (_, _) =>
            {
                _seeds.Clear();
                _feedProgress = 1;
                var gulp = new Animation(v =>
                {
                    var s = Math.Sin(Math.PI * v) * 0.08;
                    _squashX = 1 + s;
                    _squashY = 1 - s;
                    Invalidate();
                }, 0, 1);
                gulp.Commit(this, GulpAnimation, 16, 260, Easing.SinInOut, (_, _) =>
                {
                    _squashX = _squashY = 1;
                    Invalidate();
                    done.TrySetResult();
                });
            });
        return done.Task;
    }

    private static BindableProperty Colour(string name, Color fallback)
        => BindableProperty.Create(name, typeof(Color), typeof(MascotView), fallback, propertyChanged: (b, _, _) => ((MascotView)b).Invalidate());

    private double Target() => new GrowthSnapshot(Stage, 0, 0, Math.Clamp(Progress, 0, 1), null, 0).Scalar;

    private void OnGrowthChanged()
    {
        var target = Target();
        if (!_hasDrawn || ReduceMotion || Math.Abs(target - _shown) < 1e-6)
        {
            _shown = target;
            Invalidate();
            return;
        }

        this.AbortAnimation(GrowAnimation);
        new Animation(v => { _shown = v; Invalidate(); }, _shown, target).Commit(this, GrowAnimation, 16, (uint)Math.Max(1, GrowthDuration), Easing.SpringOut);
    }

    private void OnReduceMotionChanged()
    {
        if (ReduceMotion)
        {
            StopIdle();
            this.AbortAnimation(GrowAnimation);
            _shown = Target();
            Invalidate();
            return;
        }

        SyncIdle();
    }

    private void SyncIdle()
    {
        if (Idle && !ReduceMotion && IsLoaded)
        {
            StartIdle();
        }
        else
        {
            StopIdle();
        }
    }

    private void StartIdle()
    {
        if (_idleRunning)
        {
            return;
        }

        _idleRunning = true;
        _idleCycles = 0;
        var breath = new Animation(v =>
        {
            _breath = 1 + 0.015 * (0.5 - 0.5 * Math.Cos(2 * Math.PI * v));
            if (_idleCycles % 2 == 1 && v >= 0.3 && v < 0.34 && !this.AnimationIsRunning(BlinkAnimation))
            {
                Blink();
            }

            Invalidate();
        }, 0, 1);
        breath.Commit(this, IdleAnimation, 33, 2600, Easing.Linear, (_, _) => _idleCycles++, () => _idleRunning);
    }

    private void StopIdle()
    {
        if (!_idleRunning)
        {
            return;
        }

        _idleRunning = false;
        this.AbortAnimation(IdleAnimation);
        this.AbortAnimation(BlinkAnimation);
        _breath = 1;
        _eyeOpen = 1;
        Invalidate();
    }

    private void Blink()
    {
        var blink = new Animation(v => { _eyeOpen = 1 - 0.9 * Math.Sin(Math.PI * v); Invalidate(); }, 0, 1);
        blink.Commit(this, BlinkAnimation, 16, 160, Easing.Linear, (_, _) => _eyeOpen = 1);
    }

    private sealed record Seed(double Angle, double Delay, double Size);
}
