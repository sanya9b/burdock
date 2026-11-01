namespace Burdock.Presentation.Controls.Mascot;

/// <summary>Idle life: blinks, gaze saccades and the occasional gesture. One gesture at a time; a poke may interrupt whatever runs.</summary>
public sealed partial class MascotAnimator
{
    private double _nextBlinkAt;
    private double _blinkStart = double.NegativeInfinity;
    private bool _blinkSecondPending;

    private double _nextSaccadeAt;
    private double _gazeFromX;
    private double _gazeFromY;
    private double _gazeToX;
    private double _gazeToY;
    private double _gazeStart = double.NegativeInfinity;
    private bool _gazeForced;

    private Gesture _gesture;
    private double _gestureStart;
    private double _nextGestureAt;
    private double _peekSide = 1;
    private bool _hopLanded;
    private bool _curiousBlinked;

    private enum Gesture
    {
        None,
        Peek,
        Hop,
        Wiggle,
        Curious,
        Wave,
    }

    /// <summary>The gesture in flight, for tests and diagnostics.</summary>
    internal string CurrentGesture => _gesture.ToString();

    private static double BlinkTotal => MascotChoreography.BlinkClose + MascotChoreography.BlinkHold + MascotChoreography.BlinkOpen;

    private bool IsBlinking => _t - _blinkStart < BlinkTotal;

    private bool IsSaccading => _t - _gazeStart < MascotChoreography.SaccadeDuration;

    private void ScheduleIdle()
    {
        _nextBlinkAt = _t + Uniform(0.8, 2.5);
        _nextSaccadeAt = _t + Uniform(0.5, 1.5);
        _nextGestureAt = _t + Uniform(MascotChoreography.FirstGestureMin, MascotChoreography.FirstGestureMax);
    }

    private double NextGestureDelay() => Uniform(MascotChoreography.GestureIntervalMin, MascotChoreography.GestureIntervalMax);

    private void TriggerBlink(bool twice)
    {
        if (_t - _blinkStart < BlinkTotal)
        {
            return;
        }

        _blinkStart = _t;
        _blinkSecondPending = twice;
    }

    private void StepBlink(ref Frame frame)
    {
        if (_t >= _nextBlinkAt)
        {
            TriggerBlink(_random.NextDouble() < MascotChoreography.DoubleBlinkChance);
            _nextBlinkAt = _t + Uniform(MascotChoreography.BlinkIntervalMin, MascotChoreography.BlinkIntervalMax);
        }

        var u = _t - _blinkStart;
        if (u < 0)
        {
            return;
        }

        if (u >= BlinkTotal)
        {
            if (_blinkSecondPending)
            {
                _blinkSecondPending = false;
                _blinkStart = _t + MascotChoreography.DoubleBlinkGap;
            }

            return;
        }

        frame.BlinkFactor = 1 - ClosedAmount(u);
    }

    private static double ClosedAmount(double u)
    {
        if (u < MascotChoreography.BlinkClose)
        {
            return Smooth(u / MascotChoreography.BlinkClose);
        }

        u -= MascotChoreography.BlinkClose;
        if (u < MascotChoreography.BlinkHold)
        {
            return 1;
        }

        u -= MascotChoreography.BlinkHold;
        return 1 - Smooth(u / MascotChoreography.BlinkOpen);
    }

    private void StepSaccades()
    {
        if (_gazeForced || _t < _nextSaccadeAt)
        {
            return;
        }

        if (_random.NextDouble() < MascotChoreography.LookAtViewerChance)
        {
            LookAt(0, 0);
        }
        else
        {
            var angle = Uniform(0, 2 * Math.PI);
            var radius = Math.Sqrt(_random.NextDouble());
            LookAt(Math.Cos(angle) * radius, Math.Clamp(Math.Sin(angle) * radius, -1, 0.45));
        }

        _nextSaccadeAt = _t + Uniform(MascotChoreography.SaccadeIntervalMin, MascotChoreography.SaccadeIntervalMax);
    }

    /// <summary>Starts a saccade to a point of the unit disc unless the eyes already head there.</summary>
    private void LookAt(double x, double y)
    {
        if (Math.Abs(x - _gazeToX) < 1e-6 && Math.Abs(y - _gazeToY) < 1e-6)
        {
            return;
        }

        var (currentX, currentY) = CurrentGaze();
        _gazeFromX = currentX;
        _gazeFromY = currentY;
        _gazeToX = x;
        _gazeToY = y;
        _gazeStart = _t;
    }

    private void ForceGaze(double x, double y)
    {
        _gazeForced = true;
        LookAt(x, y);
    }

    private (double X, double Y) CurrentGaze()
    {
        var p = Math.Clamp((_t - _gazeStart) / MascotChoreography.SaccadeDuration, 0, 1);
        var e = 1 - (1 - p) * (1 - p) * (1 - p);
        return (_gazeFromX + (_gazeToX - _gazeFromX) * e, _gazeFromY + (_gazeToY - _gazeFromY) * e);
    }

    private void ApplyGaze(ref Frame frame)
    {
        var (x, y) = CurrentGaze();
        frame.GazeX = x * MascotChoreography.SaccadeRadius;
        frame.GazeY = y * MascotChoreography.SaccadeRadius;
        _gazeForced = false;
    }

    private Gesture PickGesture()
    {
        ReadOnlySpan<Gesture> options = _mood switch
        {
            MascotMood.Proud => [Gesture.Peek, Gesture.Peek, Gesture.Hop, Gesture.Wiggle],
            MascotMood.Joy => [Gesture.Peek, Gesture.Hop, Gesture.Wiggle],
            MascotMood.Party => [Gesture.Hop, Gesture.Wiggle, Gesture.Peek],
            MascotMood.Cheer => [Gesture.Wave, Gesture.Wave, Gesture.Hop, Gesture.Curious],
            MascotMood.Think or MascotMood.Surprise => [Gesture.Curious],
            _ => [],
        };
        return options.IsEmpty ? Gesture.None : options[_random.Next(options.Length)];
    }

    private static bool GestureFits(Gesture gesture, MascotMood mood) => gesture switch
    {
        Gesture.Peek => FaceParams.HasClosedEyes(mood),
        Gesture.Wave => mood == MascotMood.Cheer,
        _ => true,
    };

    private void StartGesture(Gesture gesture)
    {
        _gesture = gesture;
        _gestureStart = _t;
        _hopLanded = false;
        _curiousBlinked = false;
        _peekSide = _random.NextDouble() < 0.5 ? -1 : 1;
    }

    private static double GestureDuration(Gesture gesture) => gesture switch
    {
        Gesture.Peek => MascotChoreography.PeekDuration,
        Gesture.Hop => MascotChoreography.HopDuration,
        Gesture.Wiggle => MascotChoreography.WiggleDuration,
        Gesture.Curious => MascotChoreography.CuriousDuration,
        Gesture.Wave => MascotChoreography.WaveDuration,
        _ => 0,
    };

    private void StepGesture(ref Frame frame)
    {
        if (_idle && _gesture == Gesture.None && _t >= _nextGestureAt)
        {
            var next = PickGesture();
            if (next != Gesture.None)
            {
                StartGesture(next);
            }

            _nextGestureAt = _t + NextGestureDelay();
        }

        if (_gesture == Gesture.None)
        {
            return;
        }

        var p = (_t - _gestureStart) / GestureDuration(_gesture);
        if (p >= 1)
        {
            _gesture = Gesture.None;
            return;
        }

        switch (_gesture)
        {
            case Gesture.Peek:
                var open = p < 0.15 ? Smooth(p / 0.15) : p < 0.82 ? 1 : 1 - Smooth((p - 0.82) / 0.18);
                frame.EyeOverride = Math.Max(frame.EyeOverride, open);
                frame.BrowLift += 2 * open;
                if (p < 0.5)
                {
                    ForceGaze(_peekSide * 0.8, -0.3);
                }
                else
                {
                    ForceGaze(0, 0);
                }

                break;
            case Gesture.Hop:
                var anticipation = MascotChoreography.HopAnticipation / MascotChoreography.HopDuration;
                var airborne = MascotChoreography.HopAirborne / MascotChoreography.HopDuration;
                if (p < anticipation)
                {
                    var crouch = Smooth(p / anticipation);
                    frame.ScaleY *= 1 - 0.08 * crouch;
                    frame.ScaleX *= 1 + 0.05 * crouch;
                }
                else if (p < anticipation + airborne)
                {
                    var q = (p - anticipation) / airborne;
                    frame.OffsetY = -MascotChoreography.HopHeight * Math.Sin(Math.PI * q);
                    var stretch = 0.06 * Math.Abs(Math.Cos(Math.PI * q));
                    frame.ScaleY *= 1 + stretch;
                    frame.ScaleX *= 1 - stretch * 0.6;
                    frame.BrowLift += 3 * Math.Sin(Math.PI * q);
                }
                else if (!_hopLanded)
                {
                    _hopLanded = true;
                    _squash.Impulse(MascotChoreography.HopLandImpulse);
                }

                break;
            case Gesture.Wiggle:
                frame.Tilt += MascotChoreography.WiggleAmplitude * Math.Sin(2 * Math.PI * 3 * p) * (1 - p);
                frame.HookBristle += 0.06 * (1 - p);
                break;
            case Gesture.Curious:
                frame.BrowLift += MascotChoreography.CuriousBrowLift * Math.Sin(Math.PI * p);
                if (!_curiousBlinked && p > 0.1)
                {
                    _curiousBlinked = true;
                    TriggerBlink(twice: true);
                }

                if (_mood == MascotMood.Think)
                {
                    ForceGaze(p < 0.5 ? -0.7 : 0.7, -0.8);
                }

                break;
            case Gesture.Wave:
                frame.HookWaveAngle = MascotChoreography.WaveAmplitude * Math.Sin(2 * Math.PI * 2.5 * p) * (1 - p);
                break;
            default:
                break;
        }
    }
}
