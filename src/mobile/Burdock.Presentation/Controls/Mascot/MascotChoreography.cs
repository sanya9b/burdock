namespace Burdock.Presentation.Controls.Mascot;

/// <summary>
/// Timing and amplitude of the life of Реп’яшок (spec 02 §3.8 «Життя Реп’яшка»). Seconds and multipliers; the view supplies the token
/// durations (base, celebrate) that the page already reads from resources. Limits of the spec: nothing flickers faster than 3 Hz, no autonomous
/// gesture runs longer than 5 s, the rest is breathing.
/// </summary>
public static class MascotChoreography
{
    // Breathing: a vertical stretch about the ground with a smaller horizontal counter-squash, so the volume reads constant.
    public const double BreathPeriod = 3.4;
    public const double BreathAmplitude = 0.022;
    public const double BreathWidthRatio = 0.55;
    public const double SleepBreathPeriod = 4.8;
    public const double SleepBreathAmplitude = 0.032;
    public const double PartyBreathPeriod = 2.6;

    // Secondary motion that follows the breath: a slow lean, a bristle of the hooks, a wave around the ring, leaves in a light breeze.
    public const double SwayPeriod = 5.3;
    public const double SwayAmplitude = 1.6;
    public const double HookBristleAmplitude = 0.04;
    public const double HookBristleLag = 0.6;
    public const double HookWavePeriod = 4.2;
    public const double LeafSwayPeriod = 4.1;
    public const double LeafSwayAmplitude = 3.0;

    // Blinks: close fast, open slower; sometimes twice. Intervals keep well under the 3 Hz flicker limit.
    public const double BlinkIntervalMin = 2.4;
    public const double BlinkIntervalMax = 5.5;
    public const double BlinkClose = 0.06;
    public const double BlinkHold = 0.04;
    public const double BlinkOpen = 0.11;
    public const double DoubleBlinkGap = 0.14;
    public const double DoubleBlinkChance = 0.25;

    // Gaze: saccades to a point in the eye, mostly back to the viewer.
    public const double SaccadeIntervalMin = 1.2;
    public const double SaccadeIntervalMax = 3.5;
    public const double SaccadeDuration = 0.12;
    public const double SaccadeRadius = 6.0;
    public const double LookAtViewerChance = 0.35;

    // Gestures: the first soon after arrival, then a pause long enough that none of them becomes a tic.
    public const double FirstGestureMin = 3.5;
    public const double FirstGestureMax = 6.0;
    public const double GestureIntervalMin = 8.0;
    public const double GestureIntervalMax = 15.0;
    public const double PeekDuration = 1.1;
    public const double HopDuration = 0.78;
    public const double HopHeight = 0.14;
    public const double HopAnticipation = 0.16;
    public const double HopAirborne = 0.36;
    public const double HopLandImpulse = -1.6;
    public const double WiggleDuration = 0.6;
    public const double WiggleAmplitude = 6.0;
    public const double CuriousDuration = 0.7;
    public const double CuriousBrowLift = 6.0;
    public const double WaveDuration = 1.0;
    public const double WaveAmplitude = 14.0;

    // Reactions: the poke delight, the mood crossfade (motion.duration.base), the growth spring (motion.duration.celebrate).
    public const double PokeDelightDuration = 1.0;
    public const double DelightBrowLift = 5.0;
    public const double MoodBlendDuration = 0.2;
    public const double GrowthDampingRatio = 0.5;
    public const double GrowthDelightDuration = 0.8;

    // Feeding (spec 02 §3.8 «Підживлення»): seeds arc in staggered over the first part of the flight, each lands with a small nom, the last with a gulp.
    public const double SeedStagger = 0.5;
    public const double SeedCurve = 0.28;
    public const double NomImpulse = -0.9;
    public const double GulpImpulse = -2.2;
    public const double GulpSettle = 0.42;
    public const double SquashSettle = 0.32;
    public const double SquashDampingRatio = 0.45;
    public const double SquashWidthRatio = 0.7;
    public const double MouthAnticipationStart = 0.45;
    public const double FeedEyesOpenIn = 0.15;
    public const double FeedEyesCloseOut = 0.3;
    public const int MaxSeeds = 12;

    // Metamorphosis, the stage-up ceremony (spec 02 §3.8 «Переродження»): gather light with closed eyes, shiver as the charge builds, flash and
    // burst a ring while the new form pops out with a strong overshoot, a beat of surprise, then delight and a hop.
    public const double EvolveGather = 0.6;
    public const double EvolveCharge = 0.5;
    public const double EvolveReveal = 1.5;
    public const double EvolveSurpriseHold = 0.5;
    public const double EvolveDelight = 0.9;
    public const double EvolveFlashRise = 0.15;
    public const double EvolveFlashFall = 0.35;
    public const double EvolveFlashPeak = 1.0;
    public const double EvolveRingDuration = 0.55;
    public const double EvolveShiverHz = 9;
    public const double EvolveShiverAmplitude = 2.5;
    public const double EvolveDampingRatio = 0.35;
    public const double EvolveSettle = 0.7;
    public const double EvolveStretchImpulse = 1.4;
    public const double PopSettle = 0.6;
    public const double PopDampingRatio = 0.35;
    public const int EvolveSparks = 12;
}
