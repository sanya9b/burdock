using Burdock.Domain.Rewards;
using Burdock.Presentation.Controls.Mascot;

namespace Burdock.Presentation.Controls;

/// <summary>
/// Drawing half of <see cref="MascotView"/>: the geometry follows spec 02 §3.10 (body radius = 84 units of the SVG guide) and every frame is a pure
/// function of the <see cref="MascotPose"/> it is handed, so the drawing never animates on its own.
/// </summary>
public sealed partial class MascotView
{
    /// <summary>20 hook slots at 18°; this order spreads the hooks evenly at every count, so a seed with four nubs is not lopsided.</summary>
    private static readonly int[] HookOrder = [0, 10, 5, 15, 2, 12, 7, 17, 1, 11, 6, 16, 3, 13, 8, 18, 4, 14, 9, 19];

    private static readonly double[] HookJitter = BuildJitter(7, 20, 3.0);
    private static readonly double[] HookLengthJitter = BuildJitter(11, 20, 0.12);
    private static readonly int[] LeafCountByStage = [0, 2, 4, 6, 6, 6, 6, 6];
    private static readonly PartyBit[] PartyBits = BuildPartyBits();

    /// <summary>Eye openness below which the lid is drawn as an arc instead of a shrinking white (spec 02 §3.10 arcs of joy, pride and sleep).</summary>
    private const double ArcThreshold = 0.15;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        var side = Math.Min(dirtyRect.Width, dirtyRect.Height);
        if (side <= 0)
        {
            return;
        }

        _hasDrawn = true;
        canvas.Antialias = true;

        var pose = CurrentPose;
        var g = Math.Clamp(pose.Growth, 0, 1);
        var stageF = g * GrowthRules.StageCount;
        var stageIndex = Math.Min((int)Math.Floor(stageF), GrowthRules.StageCount - 1);
        var frac = stageF >= GrowthRules.StageCount ? 1 : stageF - stageIndex;
        var r = (float)(side * (0.18 + 0.13 * g));
        var u = r / 84f;
        var cx = dirtyRect.Center.X;
        var cy = dirtyRect.Center.Y + side * 0.04f;
        var hookLen = (float)(r * (0.20 + 0.08 * g) * (1 + pose.HookBristle));
        var hookCount = 4 + 16 * g;
        var anchorY = cy + r;

        // A hop never pushes the crown out of the view: clamp the lift to the headroom above bud, bloom and hooks.
        var crown = cy - r - hookLen - r * 0.6f - 2 * u;
        var headroom = Math.Max(0, crown - dirtyRect.Top - 2 * u);
        var lift = (float)Math.Max(pose.OffsetY * r, -headroom);
        var hopFraction = Math.Clamp(-lift / Math.Max(1e-3f, r * (float)MascotChoreography.HopHeight), 0, 1);

        var groundY = anchorY + hookLen * 0.9f + 2 * u;
        // Vehicles are a quarter larger than the burr and use the width of a wide canvas (240x160 on the big surfaces), capped so they fit.
        var rideScale = Math.Min(1.25f * r, Math.Min(dirtyRect.Width * 0.26f, side * 0.4f));
        DrawScene(canvas, dirtyRect, side, groundY);
        DrawFrame(canvas, dirtyRect, side, u);
        DrawGroundShadow(canvas, cx, groundY, r, u, pose.ScaleX, hopFraction);
        DrawRideBehind(canvas, cx, groundY, r, rideScale);

        canvas.SaveState();
        canvas.Translate(0, lift);
        canvas.Translate(cx, anchorY);
        canvas.Rotate((float)pose.Tilt + RideTilt);
        canvas.Scale((float)pose.ScaleX, (float)pose.ScaleY);
        canvas.Translate(-cx, -anchorY);

        var pop = (float)Math.Max(0, pose.Pop);
        if (pose.Glow > 0.01)
        {
            DrawGlow(canvas, cx, cy, r, (float)pose.Glow);
        }

        if (stageIndex == GrowthRules.StageCount - 1)
        {
            DrawAura(canvas, cx, cy, r, u, side, frac * pop, pose.Time);
        }

        DrawLeaves(canvas, cx, cy, r, u, stageIndex, pop, pose.LeafSway);
        DrawOutfitBehind(canvas, cx, cy, r, u);
        DrawHooks(canvas, cx, cy, r, u, hookLen, hookCount, pose);
        DrawBody(canvas, cx, cy, r, u);
        DrawTopper(canvas, cx, cy, r, u, hookLen, stageIndex, pop, pose.LeafSway);
        DrawFace(canvas, cx, cy, u, pose);
        DrawExtras(canvas, cx, cy, r, u, pose);
        DrawOutfit(canvas, cx, cy, r, u);
        if (pose.Flash > 0.01)
        {
            // The white-out of the burst: the old form disappears into light and the new one is what the fading flash reveals.
            canvas.SaveState();
            canvas.Alpha = (float)Math.Min(1, pose.Flash);
            canvas.FillColor = EyeColor;
            canvas.FillCircle(cx, cy, r * 1.35f + hookLen);
            canvas.RestoreState();
        }

        canvas.RestoreState();
        DrawRideFront(canvas, cx, groundY, r, rideScale);
        if (Ride != MascotRide.None)
        {
            // The arms follow the body (hop, lean, breathing) but lie over the near layer of the vehicle.
            canvas.SaveState();
            canvas.Translate(0, lift);
            canvas.Translate(cx, anchorY);
            canvas.Rotate((float)pose.Tilt + RideTilt);
            canvas.Scale((float)pose.ScaleX, (float)pose.ScaleY);
            canvas.Translate(-cx, -anchorY);
            DrawRideArms(canvas, cx, cy, groundY, r, rideScale);
            canvas.RestoreState();
        }

        if (pose.Ring > 0.001 && pose.Ring < 1)
        {
            DrawRing(canvas, cx, cy + lift, r, u, side, (float)pose.Ring);
        }

        DrawSparks(canvas, cx, cy + lift, r);

        if (pose.MoodTo == MascotMood.Party || pose.MoodFrom == MascotMood.Party)
        {
            var alpha = pose.MoodTo == MascotMood.Party ? pose.MoodBlend : 1 - pose.MoodBlend;
            DrawPartyBits(canvas, dirtyRect, side, u, (float)alpha, pose.Time);
        }

        DrawSeeds(canvas, cx, cy, r, side);
    }

    private void DrawGroundShadow(ICanvas canvas, float cx, float y, float r, float u, double scaleX, double hop)
    {
        var rx = (float)(r * 0.72 * scaleX * (1 - 0.3 * hop));
        var ry = r * 0.10f;
        canvas.SaveState();
        canvas.Alpha = (float)(0.14 * (1 - 0.45 * hop));
        canvas.FillColor = BodyShadeColor;
        canvas.FillEllipse(cx - rx, y - ry + 2 * u, 2 * rx, 2 * ry);
        canvas.RestoreState();
    }

    /// <summary>Light gathered before the burst: a soft gold halo with a brighter lavender core, growing with the charge.</summary>
    private void DrawGlow(ICanvas canvas, float cx, float cy, float r, float glow)
    {
        canvas.SaveState();
        canvas.Alpha = 0.35f * glow;
        canvas.FillColor = AccentColor;
        canvas.FillCircle(cx, cy, r * (1.35f + 0.4f * glow));
        canvas.Alpha = 0.45f * glow;
        canvas.FillColor = BodyLightColor;
        canvas.FillCircle(cx, cy, r * (1.1f + 0.2f * glow));
        canvas.RestoreState();
    }

    /// <summary>The ring of light after the burst: expands from the rim to the edge of the view over its whole life, thinning and fading.</summary>
    private void DrawRing(ICanvas canvas, float cx, float cy, float r, float u, float side, float ring)
    {
        var start = r * 1.05f;
        var end = Math.Max(start + u, Math.Min(r * 3f, side * 0.5f - 2 * u));
        var eased = 1 - (1 - ring) * (1 - ring);
        canvas.SaveState();
        canvas.Alpha = 1 - ring;
        canvas.StrokeColor = AccentColor;
        canvas.StrokeSize = Math.Max(1, 8 * u * (1 - ring));
        canvas.DrawCircle(cx, cy, start + (end - start) * eased);
        canvas.RestoreState();
    }

    private void DrawSparks(ICanvas canvas, float cx, float cy, float r)
    {
        if (ReduceMotion || !_animator.IsEvolving)
        {
            return;
        }

        foreach (var spark in _animator.Sparks)
        {
            if (spark.Alpha <= 0.01)
            {
                continue;
            }

            canvas.SaveState();
            canvas.Alpha = (float)spark.Alpha;
            canvas.FillColor = AccentColor;
            DrawSparkle(canvas, cx + (float)spark.X * r, cy + (float)spark.Y * r, (float)spark.Size * r);
            canvas.RestoreState();
        }
    }

    private void DrawAura(ICanvas canvas, float cx, float cy, float r, float u, float side, double frac, double time)
    {
        var radius = Math.Min(r * 1.7f, side * 0.5f - u);
        canvas.SaveState();
        canvas.Alpha = (float)(0.18 * (0.4 + 0.6 * frac) * (1 + 0.15 * Math.Sin(2 * Math.PI * time / MascotChoreography.BreathPeriod)));
        canvas.FillColor = AccentColor;
        canvas.FillCircle(cx, cy, radius);
        canvas.RestoreState();

        canvas.FillColor = AccentColor;
        var twinkle = 0.85f + 0.15f * (float)Math.Sin(2 * Math.PI * time / 1.7);
        DrawSparkle(canvas, cx - r * 1.55f, cy - r * 0.95f, 10 * u * twinkle);
        DrawSparkle(canvas, cx + r * 1.6f, cy - r * 0.45f, 8 * u * (2 - twinkle));
        DrawSparkle(canvas, cx + r * 1.15f, cy - r * 1.5f, 7 * u * twinkle);
    }

    private static void DrawSparkle(ICanvas canvas, float x, float y, float s)
    {
        var path = new PathF();
        path.MoveTo(x, y - s);
        path.LineTo(x + s * 0.25f, y - s * 0.25f);
        path.LineTo(x + s, y);
        path.LineTo(x + s * 0.25f, y + s * 0.25f);
        path.LineTo(x, y + s);
        path.LineTo(x - s * 0.25f, y + s * 0.25f);
        path.LineTo(x - s, y);
        path.LineTo(x - s * 0.25f, y - s * 0.25f);
        path.Close();
        canvas.FillPath(path);
    }

    /// <summary>Leaves by stage; the pairs new to the current stage scale by <paramref name="pop"/> (they burst out at the stage-up ceremony).</summary>
    private void DrawLeaves(ICanvas canvas, float cx, float cy, float r, float u, int stageIndex, float pop, double sway)
    {
        var count = LeafCountByStage[stageIndex];
        if (count == 0)
        {
            return;
        }

        var previous = stageIndex > 0 ? LeafCountByStage[stageIndex - 1] : 0;
        var originY = cy + r * 0.6f;
        for (var pair = 0; pair < count / 2; pair++)
        {
            var scale = 2 * pair >= previous ? pop : 1;
            if (scale <= 0.01)
            {
                continue;
            }

            var len = (float)(r * (1.0 + 0.1 * pair) * scale);
            var width = len * 0.38f;
            var angle = 50 + 22 * pair;
            var lean = (float)(sway * (pair % 2 == 0 ? 1 : -0.7));
            foreach (var sign in (ReadOnlySpan<int>)[-1, 1])
            {
                canvas.SaveState();
                canvas.Translate(cx, originY);
                canvas.Rotate(sign * angle + lean);
                var leaf = new PathF();
                leaf.MoveTo(0, 0);
                leaf.QuadTo(width, len * 0.5f, 0, len);
                leaf.QuadTo(-width, len * 0.5f, 0, 0);
                leaf.Close();
                canvas.FillColor = LeafColor;
                canvas.FillPath(leaf);
                canvas.StrokeColor = LeafEdgeColor;
                canvas.StrokeSize = 2.5f * u;
                canvas.StrokeLineCap = LineCap.Round;
                canvas.DrawLine(0, len * 0.08f, 0, len * 0.8f);
                canvas.RestoreState();
            }
        }
    }

    private void DrawPartyBits(ICanvas canvas, RectF rect, float side, float u, float alpha, double time)
    {
        if (alpha <= 0.01f)
        {
            return;
        }

        var i = 0;
        foreach (var bit in PartyBits)
        {
            var drift = (float)(3 * u * Math.Sin(time * 1.3 + i++));
            canvas.SaveState();
            canvas.Alpha = alpha;
            canvas.Translate(rect.X + (float)bit.X * rect.Width, rect.Y + (float)bit.Y * side + drift);
            canvas.Rotate((float)(bit.Rotation + 20 * Math.Sin(time * 0.9 + i)));
            canvas.FillColor = bit.Colour switch { 0 => AccentColor, 1 => BloomEdgeColor, _ => HookColor };
            canvas.FillRoundedRectangle(-side * 0.018f, -side * 0.01f, side * 0.036f, side * 0.02f, side * 0.005f);
            canvas.RestoreState();
        }
    }

    private void DrawHooks(ICanvas canvas, float cx, float cy, float r, float u, float hookLen, double hookCount, in MascotPose pose)
    {
        var full = (int)Math.Floor(hookCount);
        var partial = hookCount - full;
        var visible = Math.Min(HookOrder.Length, full + (partial > 0.02 ? 1 : 0));
        var raise = pose.Face.HookRaise;
        var raised = raise > 0.01 ? RaisedSlot(visible) : -1;
        var droop = (pose.MoodTo == MascotMood.Sad ? pose.MoodBlend : 0) + (pose.MoodFrom == MascotMood.Sad ? 1 - pose.MoodBlend : 0);
        var baseR = r - 4 * u;
        for (var i = 0; i < visible; i++)
        {
            var slot = HookOrder[i];
            var scale = i < full ? 1.0 : partial;
            var angle = -90 + slot * 18 + HookJitter[slot];
            var wave = 2.5 * Math.Sin(pose.HookWavePhase + slot * 18 * 2 * Math.PI / 180);
            if (droop > 0)
            {
                var c = Math.Cos(angle * Math.PI / 180);
                if (Math.Abs(c) > 0.6)
                {
                    angle += 6 * Math.Sign(c) * droop;
                }
            }

            var len = (float)(hookLen * (1 + HookLengthJitter[slot]) * scale);
            var w = (float)(13 * u * (0.6 + 0.4 * scale));
            var direction = angle + wave;
            if (slot == raised)
            {
                len = (float)(len + (r * 0.42f - len) * raise);
                w = (float)(w + (15 * u - w) * raise);
                direction += pose.HookWaveAngle * raise;
            }

            var rad = angle * Math.PI / 180;
            var x0 = cx + (float)(Math.Cos(rad) * baseR);
            var y0 = cy + (float)(Math.Sin(rad) * baseR);
            var dirRad = direction * Math.PI / 180;
            var dx = (float)Math.Cos(dirRad);
            var dy = (float)Math.Sin(dirRad);

            // The capsule of the guide (rounded rect from 6 u inside the rim to the tip) as one round-capped stroke: a single call per hook.
            var inner = w / 2 - 6 * u;
            var outer = Math.Max(inner, len - w / 2);
            canvas.StrokeColor = HookColor;
            canvas.StrokeSize = w;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.DrawLine(x0 + dx * inner, y0 + dy * inner, x0 + dx * outer, y0 + dy * outer);

            var tipR = w * 0.36f;
            canvas.FillColor = HookTipColor;
            canvas.FillCircle(x0 + dx * (len - tipR), y0 + dy * (len - tipR), tipR);
        }
    }

    /// <summary>The «waving hand» of the cheer mood: the visible hook closest to the upper-left (216°), as in the static guide.</summary>
    private static int RaisedSlot(int visible)
    {
        var best = -1;
        var bestDistance = double.MaxValue;
        for (var i = 0; i < visible; i++)
        {
            var slot = HookOrder[i];
            var angle = -90 + slot * 18;
            var distance = Math.Abs(((angle - 216) % 360 + 540) % 360 - 180);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = slot;
            }
        }

        return best;
    }

    private void DrawBody(ICanvas canvas, float cx, float cy, float r, float u)
    {
        var rect = new RectF(cx - r, cy - r, 2 * r, 2 * r);
        var paint = new RadialGradientPaint { Center = new Point(0.38, 0.32), Radius = 0.8 };
        paint.AddOffset(0f, BodyLightColor);
        paint.AddOffset(0.55f, BodyColor);
        paint.AddOffset(1f, BodyShadeColor);
        // The gradient stays inside its own state: on Android a later FillColor does not drop the shader, and every fill after the body
        // (eye whites, pupils, blush, the party hat) would silently take the body gradient and vanish into it.
        canvas.SaveState();
        canvas.SetFillPaint(paint, rect);
        canvas.FillEllipse(rect);
        canvas.RestoreState();

        canvas.SaveState();
        canvas.Alpha = 0.55f;
        canvas.FillColor = BodyLightColor;
        canvas.FillEllipse(cx - 52 * u, cy - 56 * u, 60 * u, 36 * u);
        canvas.RestoreState();
    }

    /// <summary>Bud and blooms; the crown changes at every stage from the bud on, so it scales by <paramref name="pop"/>.</summary>
    private void DrawTopper(ICanvas canvas, float cx, float cy, float r, float u, float hookLen, int stageIndex, float pop, double sway)
    {
        if (stageIndex < (int)GrowthStage.Bud - 1)
        {
            return;
        }

        var topY = cy - r - hookLen * 0.5f;
        var scaleIn = pop;
        if (scaleIn <= 0.01f)
        {
            return;
        }

        canvas.SaveState();
        canvas.Translate(cx, topY);
        canvas.Rotate((float)(sway * 0.5));
        canvas.Translate(-cx, -topY);
        if (stageIndex == (int)GrowthStage.Bud - 1)
        {
            var h = r * 0.55f * scaleIn;
            var w = r * 0.36f * scaleIn;
            var bud = new PathF();
            bud.MoveTo(cx, topY - h);
            bud.QuadTo(cx + w, topY - h * 0.45f, cx, topY + h * 0.05f);
            bud.QuadTo(cx - w, topY - h * 0.45f, cx, topY - h);
            bud.Close();
            canvas.FillColor = BloomColor;
            canvas.FillPath(bud);
            canvas.StrokeColor = LeafEdgeColor;
            canvas.StrokeSize = 3 * u;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.DrawLine(cx - w * 0.3f, topY, cx - w * 0.95f, topY - h * 0.35f);
            canvas.DrawLine(cx + w * 0.3f, topY, cx + w * 0.95f, topY - h * 0.35f);
            canvas.RestoreState();
            return;
        }

        var (petals, spread, length) = stageIndex switch
        {
            (int)GrowthStage.Burr - 1 => (3, 28, 0.40f),
            (int)GrowthStage.Bloom - 1 => (5, 26, 0.47f),
            _ => (7, 20, 0.45f),
        };
        var pw = 7 * u;
        var len = r * length * scaleIn;
        for (var i = 0; i < petals; i++)
        {
            var angle = -spread * (petals - 1) / 2f + spread * i;
            canvas.SaveState();
            canvas.Translate(cx, topY);
            canvas.Rotate(angle);
            canvas.FillColor = BloomColor;
            canvas.FillRoundedRectangle(-pw / 2, -len, pw, len, pw / 2);
            canvas.FillColor = BloomEdgeColor;
            canvas.FillCircle(0, -len + pw * 0.5f, pw * 0.5f);
            canvas.RestoreState();
        }

        if (petals >= 5)
        {
            canvas.FillColor = BloomEdgeColor;
            canvas.FillCircle(cx, topY, 5 * u * scaleIn);
        }

        canvas.RestoreState();
    }

    private void DrawFace(ICanvas canvas, float cx, float cy, float u, in MascotPose pose)
    {
        var face = pose.Face;
        var eyeL = cx - 24 * u;
        var eyeR = cx + 24 * u;
        var eyeY = cy - 8 * u;
        Eye(canvas, eyeL, eyeY, u, face);
        Eye(canvas, eyeR, eyeY, u, face);
        Brow(canvas, eyeL, eyeY, u, (float)face.BrowTiltL, (float)face.BrowLiftL);
        Brow(canvas, eyeR, eyeY, u, (float)face.BrowTiltR, (float)face.BrowLiftR);
        if (face.Blush > 0.01)
        {
            Blush(canvas, cx, cy, u, (float)face.Blush);
        }

        var baseAlpha = (float)(1 - pose.MouthOpen);
        if (pose.MouthBlend < 1)
        {
            Mouth(canvas, cx, cy, u, pose.MouthFrom, baseAlpha * (float)(1 - pose.MouthBlend));
            Mouth(canvas, cx, cy, u, pose.MouthTo, baseAlpha * (float)pose.MouthBlend);
        }
        else
        {
            Mouth(canvas, cx, cy, u, pose.MouthTo, baseAlpha);
        }

        if (pose.MouthOpen > 0.01)
        {
            OpenMouth(canvas, cx, cy, u, (float)pose.MouthOpen);
        }
    }

    /// <summary>
    /// One eye from the continuous channels: an open white with the pupil clipped inside it (so a glance can go far without leaking), a dark lid
    /// line that settles over the top as it closes, and below <see cref="ArcThreshold"/> the lid alone, bending into the arc of the mood.
    /// </summary>
    private void Eye(ICanvas canvas, float x, float y, float u, in FaceParams face)
    {
        var scale = (float)face.EyeScale;
        var rx = 18 * u * scale;
        var ryFull = 23 * u * scale;
        var open = face.EyeOpen;
        if (open <= ArcThreshold)
        {
            var k = 1 - open / ArcThreshold;
            var d = (float)(face.ArcShape * k);
            var path = new PathF();
            path.MoveTo(x - 18 * u, y - 4 * u * d);
            path.QuadTo(x, y + 18 * u * d, x + 18 * u, y - 4 * u * d);
            canvas.StrokeColor = PupilColor;
            canvas.StrokeSize = 7 * u;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.DrawPath(path);
            return;
        }

        var ry = (float)(ryFull * open);
        var white = new PathF();
        white.AppendEllipse(x - rx, y - ry, 2 * rx, 2 * ry);
        canvas.FillColor = EyeColor;
        canvas.FillPath(white);

        canvas.SaveState();
        canvas.ClipPath(white);
        var pr = ryFull * 0.6f;
        var px = x + (float)face.LookX * u;
        var py = y + (float)face.LookY * u;
        canvas.FillColor = PupilColor;
        canvas.FillCircle(px, py, pr);
        var hr = pr * 0.28f;
        canvas.FillColor = EyeColor;
        canvas.FillCircle(px - pr * 0.35f, py - pr * 0.4f, hr);
        canvas.RestoreState();

        var lid = (float)Math.Clamp((0.4 - open) / 0.25, 0, 1);
        if (lid > 0.01f)
        {
            canvas.SaveState();
            canvas.Alpha = lid;
            var path = new PathF();
            path.MoveTo(x - rx, y);
            path.QuadTo(x, y - 2 * ry, x + rx, y);
            canvas.StrokeColor = PupilColor;
            canvas.StrokeSize = 7 * u;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.DrawPath(path);
            canvas.RestoreState();
        }
    }

    private void Brow(ICanvas canvas, float x, float y, float u, float tilt, float lift)
    {
        var yb = y - 34 * u - lift * u;
        var path = new PathF();
        path.MoveTo(x - 14 * u, yb + 2 * u + tilt * u);
        path.QuadTo(x, yb - 6 * u, x + 14 * u, yb + 2 * u - tilt * u);
        canvas.StrokeColor = BodyShadeColor;
        canvas.StrokeSize = 5 * u;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawPath(path);
    }

    private void Mouth(ICanvas canvas, float cx, float cy, float u, MouthKind kind, float alpha)
    {
        if (alpha <= 0.01f)
        {
            return;
        }

        var y = cy + 34 * u;
        canvas.SaveState();
        canvas.Alpha = alpha;
        canvas.StrokeColor = PupilColor;
        canvas.StrokeSize = 5 * u;
        canvas.StrokeLineCap = LineCap.Round;
        switch (kind)
        {
            case MouthKind.Smile:
                var smile = new PathF();
                smile.MoveTo(cx - 14 * u, y);
                smile.QuadTo(cx, y + 14 * u, cx + 14 * u, y);
                canvas.DrawPath(smile);
                break;
            case MouthKind.Grin:
                var grin = new PathF();
                grin.MoveTo(cx - 20 * u, y - 2 * u);
                grin.QuadTo(cx, y + 24 * u, cx + 20 * u, y - 2 * u);
                grin.Close();
                canvas.FillColor = PupilColor;
                canvas.FillPath(grin);
                var tongue = new PathF();
                tongue.MoveTo(cx - 12 * u, y + 6 * u);
                tongue.QuadTo(cx, y + 16 * u, cx + 12 * u, y + 6 * u);
                tongue.Close();
                canvas.FillColor = BloomEdgeColor;
                canvas.FillPath(tongue);
                break;
            case MouthKind.O:
                canvas.FillColor = PupilColor;
                canvas.FillEllipse(cx - 7 * u, y + 4 * u - 9 * u, 14 * u, 18 * u);
                break;
            case MouthKind.Flat:
                canvas.DrawLine(cx - 10 * u, y + 2 * u, cx + 10 * u, y + 2 * u);
                break;
            default:
                canvas.FillColor = PupilColor;
                canvas.FillCircle(cx, y + 2 * u, 3.5f * u);
                break;
        }

        canvas.RestoreState();
    }

    /// <summary>The «nom» of feeding: a rounded mouth that scales with the anticipation and shows a hint of tongue.</summary>
    private void OpenMouth(ICanvas canvas, float cx, float cy, float u, float open)
    {
        var y = cy + 38 * u;
        var w = 7 * u * (0.4f + 0.6f * open);
        var h = 9 * u * (0.3f + 0.7f * open);
        canvas.SaveState();
        canvas.Alpha = Math.Min(1, open * 1.5f);
        canvas.FillColor = PupilColor;
        canvas.FillEllipse(cx - w, y - h, 2 * w, 2 * h);
        canvas.FillColor = BloomEdgeColor;
        canvas.FillEllipse(cx - w * 0.6f, y + h * 0.15f, w * 1.2f, h * 0.7f);
        canvas.RestoreState();
    }

    private void Blush(ICanvas canvas, float cx, float cy, float u, float alpha)
    {
        canvas.SaveState();
        canvas.Alpha = 0.9f * alpha;
        canvas.FillColor = BlushColor;
        canvas.FillEllipse(cx - 46 * u - 12 * u, cy + 16 * u - 7 * u, 24 * u, 14 * u);
        canvas.FillEllipse(cx + 46 * u - 12 * u, cy + 16 * u - 7 * u, 24 * u, 14 * u);
        canvas.RestoreState();
    }

    private void DrawExtras(ICanvas canvas, float cx, float cy, float r, float u, in MascotPose pose)
    {
        if (pose.MoodBlend < 1)
        {
            MoodExtras(canvas, cx, cy, r, u, pose.MoodFrom, (float)(1 - pose.MoodBlend), pose.Time);
        }

        MoodExtras(canvas, cx, cy, r, u, pose.MoodTo, (float)pose.MoodBlend, pose.Time);
    }

    private void MoodExtras(ICanvas canvas, float cx, float cy, float r, float u, MascotMood mood, float alpha, double time)
    {
        if (alpha <= 0.01f)
        {
            return;
        }

        switch (mood)
        {
            case MascotMood.Sleep:
                // Two zees drift up and fade on a 2.4 s loop, offset by a third of the loop.
                for (var i = 0; i < 2; i++)
                {
                    var phase = (time / 2.4 + i * 0.34) % 1;
                    canvas.SaveState();
                    canvas.Alpha = alpha * (float)Math.Sin(Math.PI * phase);
                    var rise = (float)(14 * u * phase);
                    Zee(canvas, cx + 72 * u + i * 26 * u, cy - 104 * u - i * 22 * u - rise, (18 - 6 * i) * u);
                    canvas.RestoreState();
                }

                break;
            case MascotMood.Think:
                canvas.SaveState();
                canvas.Alpha = alpha;
                canvas.FillColor = BodyShadeColor;
                for (var i = 0; i < 3; i++)
                {
                    var pulse = (float)(1 + 0.25 * Math.Sin(2 * Math.PI * time / 1.2 - i * 0.9));
                    canvas.FillCircle(cx + 86 * u + i * 14 * u, cy - 92 * u - i * 12 * u, (4 + i) * u * pulse);
                }

                canvas.RestoreState();
                break;
            case MascotMood.Party:
                // The hat sits tilted on the upper right so a bud or bloom on top stays visible.
                canvas.SaveState();
                canvas.Alpha = alpha;
                canvas.Translate(cx + r * 0.47f, cy - r * 0.88f);
                canvas.Rotate(28);
                var hat = new PathF();
                hat.MoveTo(-26 * u, 10 * u);
                hat.LineTo(0, -44 * u);
                hat.LineTo(26 * u, 10 * u);
                hat.Close();
                canvas.FillColor = AccentColor;
                canvas.FillPath(hat);
                canvas.FillColor = BloomEdgeColor;
                canvas.FillCircle(0, -44 * u, 7 * u);
                canvas.RestoreState();
                break;
            default:
                break;
        }
    }

    private void Zee(ICanvas canvas, float x, float y, float size)
    {
        var path = new PathF();
        path.MoveTo(x, y);
        path.LineTo(x + size, y);
        path.LineTo(x, y + size);
        path.LineTo(x + size, y + size);
        canvas.StrokeColor = BodyShadeColor;
        canvas.StrokeSize = size * 0.28f;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeLineJoin = LineJoin.Round;
        canvas.DrawPath(path);
    }

    /// <summary>Seeds of a feed fly along quadratic arcs, spinning, with a short fading trail; they accelerate into the body.</summary>
    private void DrawSeeds(ICanvas canvas, float cx, float cy, float r, float side)
    {
        if (ReduceMotion || !_animator.IsFeeding)
        {
            return;
        }

        foreach (var seed in _animator.Seeds)
        {
            var t = seed.Progress;
            if (t <= 0 || seed.Arrived)
            {
                continue;
            }

            var rad = seed.Angle * Math.PI / 180;
            var dirX = Math.Cos(rad);
            var dirY = Math.Sin(rad);
            var startD = side * 0.5;
            var endD = r * 0.9;
            var sx = cx + dirX * startD;
            var sy = cy + dirY * startD;
            var ex = cx + dirX * endD;
            var ey = cy + dirY * endD;
            var bulge = seed.Curve * (startD - endD);
            var ctrlX = (sx + ex) / 2 - dirY * bulge;
            var ctrlY = (sy + ey) / 2 + dirX * bulge;
            var size = (float)(seed.Size * side);
            foreach (var (lag, ghost) in (ReadOnlySpan<(double, float)>)[(0.10, 0.18f), (0.05, 0.35f), (0.0, 1f)])
            {
                var p = t - lag;
                if (p <= 0)
                {
                    continue;
                }

                var e = p * p;
                var mx = (1 - e) * (1 - e) * sx + 2 * (1 - e) * e * ctrlX + e * e * ex;
                var my = (1 - e) * (1 - e) * sy + 2 * (1 - e) * e * ctrlY + e * e * ey;
                var fade = t < 0.1 ? t / 0.1 : t > 0.9 ? (1 - t) / 0.1 : 1;
                canvas.SaveState();
                canvas.Alpha = (float)fade * ghost;
                canvas.Translate((float)mx, (float)my);
                canvas.Rotate((float)(seed.Spin * p));
                var s = size * (lag > 0 ? 0.7f : 1f);
                canvas.FillColor = HookColor;
                canvas.FillEllipse(-s * 1.5f, -s, s * 3f, s * 2f);
                canvas.FillColor = HookTipColor;
                canvas.FillCircle(s * 0.9f, 0, s * 0.5f);
                canvas.RestoreState();
            }
        }
    }

    private static double SmoothStep(double x) => x * x * (3 - 2 * x);

    private static double[] BuildJitter(int seed, int count, double amplitude)
    {
        var random = new Random(seed);
        var values = new double[count];
        for (var i = 0; i < count; i++)
        {
            values[i] = (random.NextDouble() * 2 - 1) * amplitude;
        }

        return values;
    }

    private static PartyBit[] BuildPartyBits()
    {
        var random = new Random(21);
        var bits = new PartyBit[14];
        for (var i = 0; i < bits.Length; i++)
        {
            bits[i] = new PartyBit(0.05 + random.NextDouble() * 0.9, 0.03 + random.NextDouble() * 0.35, random.NextDouble() * 120 - 60, random.Next(3));
        }

        return bits;
    }

    private sealed record PartyBit(double X, double Y, double Rotation, int Colour);
}
