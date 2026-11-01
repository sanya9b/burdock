namespace Burdock.Presentation.Controls.Mascot;

/// <summary>
/// Feeding (spec 02 §3.8 «Підживлення»): seeds arc in from the edges, staggered; the burr opens its eyes to watch the nearest one, rounds its
/// mouth as it comes close, takes a small «nom» per seed and a gulp on the last, then <see cref="FeedFinished"/> lets the caller raise the growth.
/// </summary>
public sealed partial class MascotAnimator
{
    private readonly List<SeedState> _seeds = [];
    private bool _feeding;
    private double _feedElapsed;
    private double _feedDuration;
    private int _arrived;
    private double _gulpAt = double.PositiveInfinity;

    /// <summary>Raised once, after the gulp has settled.</summary>
    public event Action? FeedFinished;

    public bool IsFeeding => _feeding;

    /// <summary>Seeds in flight, for the drawing; empty when not feeding.</summary>
    public IReadOnlyList<SeedState> Seeds => _seeds;

    /// <summary>Starts a feed of <paramref name="seeds"/> seeds (3–12 in the spec) whose last one lands at the end of <paramref name="flightSeconds"/>.</summary>
    public void StartFeed(int seeds, double flightSeconds)
    {
        var count = Math.Clamp(seeds, 1, MascotChoreography.MaxSeeds);
        _seeds.Clear();
        for (var i = 0; i < count; i++)
        {
            var angle = -150 + _random.NextDouble() * 120 + (i % 2 == 0 ? 0 : 180);
            var curve = (i % 2 == 0 ? 1 : -1) * (0.6 + _random.NextDouble() * 0.8) * MascotChoreography.SeedCurve;
            var spin = Uniform(360, 720) * (_random.NextDouble() < 0.5 ? -1 : 1);
            _seeds.Add(new SeedState(angle, i / (double)count * MascotChoreography.SeedStagger, 0.028 + _random.NextDouble() * 0.014, curve, spin));
        }

        _feeding = true;
        _feedElapsed = 0;
        _feedDuration = Math.Max(0.2, flightSeconds);
        _arrived = 0;
        _gulpAt = double.PositiveInfinity;
    }

    private void StepFeed(double dt, ref Frame frame)
    {
        if (!_feeding)
        {
            return;
        }

        _feedElapsed += dt;
        var progress = _feedElapsed / _feedDuration;
        var mouthOpen = 0.0;
        SeedState? watched = null;
        foreach (var seed in _seeds)
        {
            var t = Math.Clamp((progress - seed.Delay) / (1 - MascotChoreography.SeedStagger), 0, 1);
            seed.Progress = t;
            if (t >= 1)
            {
                if (!seed.Arrived)
                {
                    seed.Arrived = true;
                    _arrived++;
                    var last = _arrived == _seeds.Count;
                    _squash.Impulse(last ? MascotChoreography.GulpImpulse : MascotChoreography.NomImpulse);
                    if (last)
                    {
                        _gulpAt = _t;
                    }
                }

                continue;
            }

            if (t <= 0)
            {
                continue;
            }

            var start = MascotChoreography.MouthAnticipationStart;
            var open = t < 0.85 ? Smooth((t - start) / (0.85 - start)) : 1 - Smooth((t - 0.85) / 0.15);
            mouthOpen = Math.Max(mouthOpen, open);
            if (watched is null || t > watched.Progress)
            {
                watched = seed;
            }
        }

        frame.MouthOpen = Math.Max(frame.MouthOpen, mouthOpen);
        if (watched is not null)
        {
            var rad = watched.Angle * Math.PI / 180;
            ForceGaze(Math.Cos(rad) * 0.9, Math.Clamp(Math.Sin(rad) * 0.9, -1, 0.6));
        }

        var eyes = _arrived < _seeds.Count
            ? Smooth(_feedElapsed / MascotChoreography.FeedEyesOpenIn)
            : 1 - Smooth((_t - _gulpAt - 0.1) / MascotChoreography.FeedEyesCloseOut);
        frame.EyeOverride = Math.Max(frame.EyeOverride, eyes);

        if (_arrived == _seeds.Count && _t - _gulpAt >= MascotChoreography.GulpSettle)
        {
            _feeding = false;
            _seeds.Clear();
            FeedFinished?.Invoke();
        }
    }

    /// <summary>One seed of a feed: fixed flight parameters plus its progress 0..1 along the arc.</summary>
    /// <param name="Angle">Direction it comes from, degrees, 0 = right, −90 = top.</param>
    /// <param name="Delay">Fraction of the feed before it starts.</param>
    /// <param name="Size">Radius as a fraction of the view side.</param>
    /// <param name="Curve">Sideways bulge of the arc as a fraction of the flight distance, signed.</param>
    /// <param name="Spin">Degrees it turns over the flight.</param>
    public sealed record SeedState(double Angle, double Delay, double Size, double Curve, double Spin)
    {
        public double Progress { get; internal set; }

        public bool Arrived { get; internal set; }
    }
}
