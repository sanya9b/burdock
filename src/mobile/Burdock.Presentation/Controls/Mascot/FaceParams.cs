namespace Burdock.Presentation.Controls.Mascot;

/// <summary>Discrete mouth shapes of the guide (spec 02 §3.10); the pose crossfades between two of them while a mood changes.</summary>
public enum MouthKind
{
    Smile,
    Grin,
    O,
    Flat,
    Dot,
}

/// <summary>
/// Continuous face channels in guide units u = r/84 (spec 02 §3.10). Every mood is a point in this space, so a mood change is a straight
/// interpolation and blinks, glances and the «nom» of feeding are offsets layered on top instead of separate drawings.
/// </summary>
/// <param name="EyeOpen">0 = lids closed into an arc, 1 = fully open.</param>
/// <param name="ArcShape">Shape of the closed lid: +1 ends up (joyful squint ∪), −1 ends down (content ∩), 0 flat line (a plain blink).</param>
/// <param name="EyeScale">1 at rest, 1.15 for surprise.</param>
/// <param name="LookX">Pupil offset in u, positive to the viewer's right.</param>
/// <param name="LookY">Pupil offset in u, positive down.</param>
/// <param name="BrowLiftL">Left brow lift in u.</param>
/// <param name="BrowLiftR">Right brow lift in u.</param>
/// <param name="BrowTiltL">Left brow tilt in u (positive raises the inner end).</param>
/// <param name="BrowTiltR">Right brow tilt in u (positive raises the inner end).</param>
/// <param name="Blush">0..1 opacity of the cheeks.</param>
/// <param name="HookRaise">0..1 of the hook raised as a waving hand (cheer).</param>
public readonly record struct FaceParams(
    double EyeOpen,
    double ArcShape,
    double EyeScale,
    double LookX,
    double LookY,
    double BrowLiftL,
    double BrowLiftR,
    double BrowTiltL,
    double BrowTiltR,
    double Blush,
    double HookRaise)
{
    /// <summary>The guide face of each mood (spec 02 §3.10 table and the static SVG states).</summary>
    public static FaceParams For(MascotMood mood) => mood switch
    {
        MascotMood.Joy => new(0, +1, 1, 0, 0, 4, 4, 0, 0, 1, 0),
        MascotMood.Cheer => new(1, 0, 1, 2, -2, 6, 8, -2, 2, 0, 1),
        MascotMood.Surprise => new(1, 0, 1.15, 0, 0, 12, 12, 0, 0, 0, 0),
        MascotMood.Sad => new(1, 0, 1, -4, 3, 0, 0, -5, 5, 0, 0),
        MascotMood.Sleep => new(0, -1, 1, 0, 0, 0, 0, 0, 0, 0, 0),
        MascotMood.Party => new(0, +1, 1, 0, 0, 6, 6, 0, 0, 1, 0),
        MascotMood.Think => new(1, 0, 1, 5, -6, 2, 10, 0, 3, 0, 0),
        _ => new(0, -1, 1, 0, 0, 6, 6, 0, 0, 1, 0),
    };

    /// <summary>Mouth of each mood; «Proud» and «Joy» smile, «Party» grins, «Surprise» rounds into an «o».</summary>
    public static MouthKind MouthFor(MascotMood mood) => mood switch
    {
        MascotMood.Party => MouthKind.Grin,
        MascotMood.Surprise => MouthKind.O,
        MascotMood.Sad => MouthKind.Flat,
        MascotMood.Sleep or MascotMood.Think => MouthKind.Dot,
        _ => MouthKind.Smile,
    };

    /// <summary>Whether the guide draws this mood with closed lids (arcs); only these moods «peek» in idle.</summary>
    public static bool HasClosedEyes(MascotMood mood) => For(mood).EyeOpen < 0.5;

    public static FaceParams Lerp(in FaceParams a, in FaceParams b, double t) => new(
        a.EyeOpen + (b.EyeOpen - a.EyeOpen) * t,
        a.ArcShape + (b.ArcShape - a.ArcShape) * t,
        a.EyeScale + (b.EyeScale - a.EyeScale) * t,
        a.LookX + (b.LookX - a.LookX) * t,
        a.LookY + (b.LookY - a.LookY) * t,
        a.BrowLiftL + (b.BrowLiftL - a.BrowLiftL) * t,
        a.BrowLiftR + (b.BrowLiftR - a.BrowLiftR) * t,
        a.BrowTiltL + (b.BrowTiltL - a.BrowTiltL) * t,
        a.BrowTiltR + (b.BrowTiltR - a.BrowTiltR) * t,
        a.Blush + (b.Blush - a.Blush) * t,
        a.HookRaise + (b.HookRaise - a.HookRaise) * t);

    /// <summary>The same face with both brows lifted by <paramref name="lift"/> u (curiosity, delight).</summary>
    public FaceParams WithBrowLift(double lift) => this with { BrowLiftL = BrowLiftL + lift, BrowLiftR = BrowLiftR + lift };
}
