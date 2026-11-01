namespace Burdock.Presentation.Controls.Mascot;

/// <summary>
/// The life of Реп’яшок as a pure function of time and events (spec 02 §3.8 «Життя Реп’яшка», §3.10). The view feeds it the frame delta and
/// draws <see cref="Pose"/>; nothing in here touches MAUI, so the choreography is unit-tested on the neutral head. Randomness comes from the
/// injected <see cref="Random"/> (seeded in tests), time only from <see cref="Advance"/>.
/// Layers, in order: mood crossfade → delight overlay (poke, growth) → idle breathing, sway, hook bristle and wave, leaf sway → blinks → gaze
/// saccades → one gesture at a time (peek, hop, wiggle, curious, wave) → feeding (seeds, anticipation, nom, gulp) → squash spring → growth spring.
/// </summary>
public sealed partial class MascotAnimator
{
    private readonly Random _random;
    private double _t;
    private MascotPose _pose;
    private bool _idle;

    private MascotMood _mood;
    private MascotMood _moodFrom;
    private FaceParams _faceTo;
    private FaceParams _faceFrom;
    private double _moodBlend = 1;

    private Spring _growth;
    private Spring _squash;
    private Spring _pop;

    private double _delightStart = double.NegativeInfinity;
    private double _delightDuration;
    private bool _delightOpensEyes;

    public MascotAnimator(MascotMood mood, double growth, Random random, double growthSettleSeconds = 0.6)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _mood = mood;
        _moodFrom = mood;
        _faceTo = FaceParams.For(mood);
        _faceFrom = _faceTo;
        _growth = new Spring(growthSettleSeconds, MascotChoreography.GrowthDampingRatio, Math.Clamp(growth, 0, 1));
        _squash = new Spring(MascotChoreography.SquashSettle, MascotChoreography.SquashDampingRatio, 0);
        _pop = new Spring(MascotChoreography.PopSettle, MascotChoreography.PopDampingRatio, 1);
        _pose = MascotPose.Rest(mood, growth);
        _nextBlinkAt = double.PositiveInfinity;
        _nextSaccadeAt = double.PositiveInfinity;
        _nextGestureAt = double.PositiveInfinity;
    }

    public MascotMood Mood => _mood;

    /// <summary>Growth scalar the spring is heading to (the shown value is in <see cref="Pose"/>).</summary>
    public double Growth => _growth.Target;

    public double Time => _t;

    public MascotPose Pose => _pose;

    /// <summary>Autonomous life (breathing, blinks, gaze, gestures). Transient reactions (mood change, feeding, growth, poke) play regardless.</summary>
    public bool Idle
    {
        get => _idle;
        set
        {
            if (_idle == value)
            {
                return;
            }

            _idle = value;
            if (value)
            {
                ScheduleIdle();
            }
            else
            {
                _nextBlinkAt = double.PositiveInfinity;
                _nextSaccadeAt = double.PositiveInfinity;
                _nextGestureAt = double.PositiveInfinity;
            }
        }
    }

    /// <summary>True while something still moves that is not plain idle life; the view keeps ticking while this or <see cref="Idle"/> holds.</summary>
    public bool IsBusy => _moodBlend < 1 || _growth.IsActive || _squash.IsActive || _pop.IsActive || IsFeeding || IsEvolving || _gesture != Gesture.None || DelightActive;

    /// <summary>True while something fast is on screen (a reaction, a blink, a saccade); the view draws these at 60 fps and quiet breathing at 20.</summary>
    public bool IsLively => IsBusy || IsBlinking || IsSaccading;

    private bool DelightActive => _t - _delightStart < _delightDuration;

    /// <summary>Changes the mood; <paramref name="animate"/> crossfades face and extras over motion.duration.base, otherwise it snaps.</summary>
    public void SetMood(MascotMood mood, bool animate)
    {
        if (mood == _mood)
        {
            return;
        }

        var current = animate ? CurrentFace() : FaceParams.For(mood);
        _moodFrom = animate ? _mood : mood;
        _faceFrom = current;
        _mood = mood;
        _faceTo = FaceParams.For(mood);
        _moodBlend = animate ? 0 : 1;
        if (!animate)
        {
            _pose = _pose with { Face = _faceTo, MouthFrom = FaceParams.MouthFor(mood), MouthTo = FaceParams.MouthFor(mood), MouthBlend = 1, MoodFrom = mood, MoodTo = mood, MoodBlend = 1 };
        }

        if (_gesture != Gesture.None && !GestureFits(_gesture, mood))
        {
            _gesture = Gesture.None;
        }
    }

    /// <summary>Sets the growth target; <paramref name="animate"/> springs there with a visible overshoot and a short delight, otherwise snaps.</summary>
    public void SetGrowth(double target, bool animate, double settleSeconds)
    {
        target = Math.Clamp(target, 0, 1);
        if (_evolving)
        {
            _evolveTarget = target;
            return;
        }

        if (!animate)
        {
            _growth = new Spring(settleSeconds, MascotChoreography.GrowthDampingRatio, target);
            _pose = _pose with { Growth = target };
            return;
        }

        if (Math.Abs(target - _growth.Value) < 1e-6)
        {
            return;
        }

        _growth = new Spring(settleSeconds, MascotChoreography.GrowthDampingRatio, _growth.Value) { Target = target };
        StartDelight(MascotChoreography.GrowthDelightDuration, opensEyes: true);
    }

    /// <summary>The learner touched the burr: a happy hop (a stir when asleep) and lifted brows, never a complaint.</summary>
    public void Poke()
    {
        StartGesture(_mood == MascotMood.Sleep ? Gesture.Wiggle : Gesture.Hop);
        StartDelight(MascotChoreography.PokeDelightDuration, opensEyes: _mood != MascotMood.Sleep);
        if (_idle)
        {
            _nextGestureAt = _t + NextGestureDelay();
        }
    }

    /// <summary>Advances the clock by <paramref name="dt"/> seconds (clamped to 50 ms so a stalled frame cannot teleport anything) and recomputes the pose.</summary>
    public void Advance(double dt)
    {
        dt = Math.Clamp(dt, 0, 0.05);
        _t += dt;
        if (_moodBlend < 1)
        {
            _moodBlend = Math.Min(1, _moodBlend + dt / MascotChoreography.MoodBlendDuration);
        }

        _growth.Step(dt);
        _squash.Step(dt);
        _pop.Step(dt);

        var frame = new Frame { ScaleX = 1, ScaleY = 1, BlinkFactor = 1 };
        if (_idle)
        {
            ApplyBreath(ref frame);
            StepBlink(ref frame);
        }

        StepGesture(ref frame);
        StepFeed(dt, ref frame);
        StepMetamorphosis(ref frame);
        if (_idle)
        {
            StepSaccades();
        }

        ApplyDelight(ref frame);
        ApplyGaze(ref frame);

        var squash = _squash.Value;
        frame.ScaleY *= 1 + squash;
        frame.ScaleX *= 1 - squash * MascotChoreography.SquashWidthRatio;

        var face = CurrentFace();
        var eyeOpen = Math.Max(face.EyeOpen * frame.BlinkFactor, frame.EyeOverride) * (1 - Math.Clamp(frame.EyeClose, 0, 1));
        var gazeWeight = Math.Clamp((eyeOpen - 0.15) / 0.3, 0, 1);
        face = face with
        {
            EyeOpen = Math.Clamp(eyeOpen, 0, 1),
            EyeScale = face.EyeScale + frame.EyeScaleAdd,
            LookX = face.LookX + frame.GazeX * gazeWeight,
            LookY = face.LookY + frame.GazeY * gazeWeight,
            BrowLiftL = face.BrowLiftL + frame.BrowLift,
            BrowLiftR = face.BrowLiftR + frame.BrowLift,
            Blush = Math.Clamp(face.Blush + frame.BlushBoost, 0, 1),
        };

        var blend = EaseStandard(_moodBlend);
        _pose = new MascotPose(
            frame.ScaleX,
            frame.ScaleY,
            frame.OffsetY,
            frame.Tilt,
            frame.HookBristle,
            frame.HookWavePhase,
            frame.HookWaveAngle,
            frame.LeafSway,
            _growth.Value,
            face,
            FaceParams.MouthFor(_moodFrom),
            FaceParams.MouthFor(_mood),
            blend,
            frame.MouthOpen,
            _moodFrom,
            _mood,
            blend,
            _t,
            frame.Glow,
            frame.Flash,
            frame.Ring,
            _pop.Value);
    }

    private FaceParams CurrentFace() => _moodBlend >= 1 ? _faceTo : FaceParams.Lerp(_faceFrom, _faceTo, EaseStandard(_moodBlend));

    private void StartDelight(double duration, bool opensEyes)
    {
        _delightStart = _t;
        _delightDuration = duration;
        _delightOpensEyes = opensEyes;
    }

    private void ApplyDelight(ref Frame frame)
    {
        if (!DelightActive)
        {
            return;
        }

        var p = (_t - _delightStart) / _delightDuration;
        var bump = Math.Sin(Math.PI * p);
        frame.BrowLift += MascotChoreography.DelightBrowLift * bump;
        frame.BlushBoost = Math.Max(frame.BlushBoost, 0.8 * bump);
        if (_delightOpensEyes)
        {
            frame.EyeOverride = Math.Max(frame.EyeOverride, Math.Min(1, bump * 2));
        }
    }

    private void ApplyBreath(ref Frame frame)
    {
        var (period, amplitude) = _mood switch
        {
            MascotMood.Sleep => (MascotChoreography.SleepBreathPeriod, MascotChoreography.SleepBreathAmplitude),
            MascotMood.Party => (MascotChoreography.PartyBreathPeriod, MascotChoreography.BreathAmplitude),
            _ => (MascotChoreography.BreathPeriod, MascotChoreography.BreathAmplitude),
        };
        var phase = 2 * Math.PI * _t / period;
        var s = Math.Sin(phase);
        frame.ScaleY *= 1 + amplitude * s;
        frame.ScaleX *= 1 - amplitude * MascotChoreography.BreathWidthRatio * s;
        frame.HookBristle += MascotChoreography.HookBristleAmplitude * Math.Sin(phase - MascotChoreography.HookBristleLag);
        frame.Tilt += MascotChoreography.SwayAmplitude * Math.Sin(2 * Math.PI * _t / MascotChoreography.SwayPeriod + 1.3);
        frame.HookWavePhase = 2 * Math.PI * _t / MascotChoreography.HookWavePeriod;
        frame.LeafSway = MascotChoreography.LeafSwayAmplitude * Math.Sin(2 * Math.PI * _t / MascotChoreography.LeafSwayPeriod);
    }

    private double Uniform(double min, double max) => min + _random.NextDouble() * (max - min);

    private static double Smooth(double x)
    {
        x = Math.Clamp(x, 0, 1);
        return x * x * (3 - 2 * x);
    }

    private static double EaseStandard(double x)
    {
        x = Math.Clamp(x, 0, 1);
        return 1 - (1 - x) * (1 - x) * (1 - x);
    }

    /// <summary>Per-frame accumulator the layers add to before the pose is assembled.</summary>
    private struct Frame
    {
        public double ScaleX;
        public double ScaleY;
        public double OffsetY;
        public double Tilt;
        public double HookBristle;
        public double HookWavePhase;
        public double HookWaveAngle;
        public double LeafSway;
        public double BrowLift;
        public double BlushBoost;
        public double EyeOverride;
        public double BlinkFactor;
        public double GazeX;
        public double GazeY;
        public double MouthOpen;
        public double EyeClose;
        public double EyeScaleAdd;
        public double Glow;
        public double Flash;
        public double Ring;
    }
}
