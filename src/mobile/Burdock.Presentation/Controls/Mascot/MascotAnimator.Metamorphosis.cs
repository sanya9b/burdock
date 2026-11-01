namespace Burdock.Presentation.Controls.Mascot;

/// <summary>
/// The stage-up ceremony (spec 02 §3.8 «Переродження»): the burr closes its eyes and gathers light, shivers as the charge builds, flashes white
/// while a ring of light bursts outward, and the new, bigger form with its new parts pops out of the flash; it looks surprised, then delighted,
/// and hops. The growth scalar holds the old form until the burst, so the change reads as one moment instead of a slow slide.
/// </summary>
public sealed partial class MascotAnimator
{
    private readonly List<Spark> _sparks = [];
    private bool _evolving;
    private double _evolveStart;
    private double _evolveTarget;
    private bool _burstDone;
    private bool _revealDone;

    /// <summary>The instant the new form springs out (the page plays the success haptic here).</summary>
    public event Action? Burst;

    /// <summary>Raised once, when the ceremony is over and the celebration card may cover the burr.</summary>
    public event Action? MetamorphosisFinished;

    public bool IsEvolving => _evolving;

    /// <summary>Light sparks of the ceremony, in body radii around the centre; empty otherwise.</summary>
    public IReadOnlyList<Spark> Sparks => _sparks;

    /// <summary>Starts the ceremony towards <paramref name="targetGrowth"/>; the shown growth stays where it is until the burst.</summary>
    public void StartMetamorphosis(double targetGrowth)
    {
        _evolving = true;
        _evolveStart = _t;
        _evolveTarget = Math.Clamp(targetGrowth, 0, 1);
        _burstDone = false;
        _revealDone = false;
        _gesture = Gesture.None;
        _sparks.Clear();
        for (var i = 0; i < MascotChoreography.EvolveSparks; i++)
        {
            _sparks.Add(new Spark(Uniform(0, 2 * Math.PI), Uniform(0.7, 1.4), Uniform(0.10, 0.16), _random.NextDouble()));
        }
    }

    private void StepMetamorphosis(ref Frame frame)
    {
        if (!_evolving)
        {
            return;
        }

        var u = _t - _evolveStart;
        var gather = MascotChoreography.EvolveGather;
        var burst = gather + MascotChoreography.EvolveCharge;
        var end = burst + MascotChoreography.EvolveReveal;
        if (u < gather)
        {
            var e = Smooth(u / gather);
            frame.EyeClose = Math.Max(frame.EyeClose, e);
            frame.ScaleY *= 1 + 0.06 * e;
            frame.ScaleX *= 1 + 0.03 * e;
            frame.Glow = 0.6 * e;
            frame.HookBristle += 0.10 * e;
            frame.BrowLift -= 2 * e;
            UpdateSparks(u / burst, afterBurst: -1);
            return;
        }

        if (u < burst)
        {
            var p = (u - gather) / (burst - gather);
            frame.EyeClose = 1;
            frame.ScaleY *= 1.06 + 0.04 * p;
            frame.ScaleX *= 1.03 + 0.02 * p;
            frame.Tilt += MascotChoreography.EvolveShiverAmplitude * p * Math.Sin(2 * Math.PI * MascotChoreography.EvolveShiverHz * u);
            frame.Glow = 0.6 + 0.4 * p;
            frame.HookBristle += 0.10 + 0.06 * p;
            frame.BrowLift -= 2;
            var flashStart = burst - MascotChoreography.EvolveFlashRise;
            if (u > flashStart)
            {
                frame.Flash = MascotChoreography.EvolveFlashPeak * Smooth((u - flashStart) / MascotChoreography.EvolveFlashRise);
            }

            UpdateSparks(u / burst, afterBurst: -1);
            return;
        }

        var v = u - burst;
        if (!_burstDone)
        {
            _burstDone = true;
            _growth = new Spring(MascotChoreography.EvolveSettle, MascotChoreography.EvolveDampingRatio, _growth.Value) { Target = _evolveTarget };
            _pop = new Spring(MascotChoreography.PopSettle, MascotChoreography.PopDampingRatio, 0) { Target = 1 };
            _squash.Impulse(MascotChoreography.EvolveStretchImpulse);
            Burst?.Invoke();
        }

        frame.Flash = MascotChoreography.EvolveFlashPeak * (1 - Smooth(v / MascotChoreography.EvolveFlashFall));
        frame.Ring = Math.Min(1, v / MascotChoreography.EvolveRingDuration);
        frame.Glow = Math.Max(0, 1 - v / 0.8);
        if (v < MascotChoreography.EvolveSurpriseHold)
        {
            var s = Smooth(Math.Min(1, v / 0.12));
            var closing = Smooth(Math.Max(0, v - MascotChoreography.EvolveSurpriseHold + 0.15) / 0.15);
            frame.EyeOverride = Math.Max(frame.EyeOverride, s);
            frame.EyeScaleAdd += 0.15 * s;
            frame.BrowLift += 10 * s;
            frame.MouthOpen = Math.Max(frame.MouthOpen, 0.8 * s * (1 - closing));
        }
        else if (!_revealDone)
        {
            _revealDone = true;
            StartDelight(MascotChoreography.EvolveDelight, opensEyes: true);
            StartGesture(Gesture.Hop);
        }

        UpdateSparks(1, v);
        if (u >= end)
        {
            _evolving = false;
            _sparks.Clear();
            MetamorphosisFinished?.Invoke();
        }
    }

    /// <summary>Before the burst the sparks spiral inward to the rim, faster as the charge builds; after it they fly out and fade.</summary>
    private void UpdateSparks(double gatherProgress, double afterBurst)
    {
        foreach (var spark in _sparks)
        {
            if (afterBurst < 0)
            {
                var p = Math.Clamp(gatherProgress, 0, 1);
                var radius = 1.7 - 0.65 * p;
                var angle = spark.Angle + spark.Speed * (2.5 * p + 1.5 * p * p);
                spark.X = Math.Cos(angle) * radius;
                spark.Y = Math.Sin(angle) * radius;
                spark.Alpha = Math.Min(1, p * 3) * (0.6 + 0.4 * Math.Sin(spark.Phase * 2 * Math.PI + p * 20));
            }
            else
            {
                var radius = 1.05 + (1.2 + spark.Speed) * afterBurst * 1.6;
                var angle = spark.Angle + spark.Speed * 4;
                spark.X = Math.Cos(angle) * radius;
                spark.Y = Math.Sin(angle) * radius;
                spark.Alpha = Math.Max(0, 1 - afterBurst / 0.6);
            }
        }
    }

    /// <summary>One spark of the ceremony: fixed orbit parameters plus its position (body radii from the centre) and opacity this frame.</summary>
    /// <param name="Angle">Start angle in radians.</param>
    /// <param name="Speed">Orbit and fly-out speed factor.</param>
    /// <param name="Size">Size as a fraction of the body radius.</param>
    /// <param name="Phase">Twinkle phase 0..1.</param>
    public sealed record Spark(double Angle, double Speed, double Size, double Phase)
    {
        public double X { get; internal set; }

        public double Y { get; internal set; }

        public double Alpha { get; internal set; }
    }
}
