namespace Burdock.Presentation.Controls.Mascot;

/// <summary>
/// Everything the drawing reads for one frame. Produced by <see cref="MascotAnimator"/>, consumed by the view; nothing else is animated.
/// </summary>
/// <param name="ScaleX">Horizontal squash about the ground contact point (1 at rest).</param>
/// <param name="ScaleY">Vertical stretch about the ground contact point (1 at rest).</param>
/// <param name="OffsetY">Hop in body radii, negative = up.</param>
/// <param name="Tilt">Body tilt in degrees, positive = clockwise.</param>
/// <param name="HookBristle">Added to the hook length multiplier (0 at rest; +0.04 = hooks 4 % longer).</param>
/// <param name="HookWavePhase">Phase in radians of the wave travelling around the hook ring.</param>
/// <param name="HookWaveAngle">Extra rotation in degrees of the raised «hand» hook (the cheer wave).</param>
/// <param name="LeafSway">Degrees the leaf pairs lean, alternating by pair.</param>
/// <param name="Growth">Shown growth scalar 0..1 (spec 02 §3.10).</param>
/// <param name="Face">Continuous face channels.</param>
/// <param name="MouthFrom">Mouth being faded out while a mood changes (equals <see cref="MouthTo"/> at rest).</param>
/// <param name="MouthTo">Mouth of the current mood.</param>
/// <param name="MouthBlend">0..1 crossfade from <see cref="MouthFrom"/> to <see cref="MouthTo"/>.</param>
/// <param name="MouthOpen">0..1 «nom» overlay: the mouth rounds open as a seed arrives.</param>
/// <param name="MoodFrom">Mood whose extras (hat, zees, dots) fade out during a change.</param>
/// <param name="MoodTo">Current mood.</param>
/// <param name="MoodBlend">0..1 crossfade of the mood extras.</param>
/// <param name="Time">Seconds since the animator started; drives loops the drawing owns (floating zees, pulsing dots).</param>
public readonly record struct MascotPose(
    double ScaleX,
    double ScaleY,
    double OffsetY,
    double Tilt,
    double HookBristle,
    double HookWavePhase,
    double HookWaveAngle,
    double LeafSway,
    double Growth,
    FaceParams Face,
    MouthKind MouthFrom,
    MouthKind MouthTo,
    double MouthBlend,
    double MouthOpen,
    MascotMood MoodFrom,
    MascotMood MoodTo,
    double MoodBlend,
    double Time)
{
    /// <summary>The still frame of a mood (Reduce Motion, or before the first tick).</summary>
    public static MascotPose Rest(MascotMood mood, double growth)
        => new(1, 1, 0, 0, 0, 0, 0, 0, growth, FaceParams.For(mood), FaceParams.MouthFor(mood), FaceParams.MouthFor(mood), 1, 0, mood, mood, 1, 0);
}
