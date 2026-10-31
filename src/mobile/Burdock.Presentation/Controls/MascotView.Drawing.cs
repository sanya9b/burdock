using Burdock.Domain.Rewards;

namespace Burdock.Presentation.Controls;

/// <summary>Drawing half of <see cref="MascotView"/>: the geometry follows spec 02 §3.10 (body radius = 84 units of the SVG guide).</summary>
public sealed partial class MascotView
{
    /// <summary>20 hook slots at 18°; this order spreads the hooks evenly at every count, so a seed with four nubs is not lopsided.</summary>
    private static readonly int[] HookOrder = [0, 10, 5, 15, 2, 12, 7, 17, 1, 11, 6, 16, 3, 13, 8, 18, 4, 14, 9, 19];

    private static readonly double[] HookJitter = BuildJitter(7, 20, 3.0);
    private static readonly double[] HookLengthJitter = BuildJitter(11, 20, 0.12);
    private static readonly int[] LeafCountByStage = [0, 2, 4, 6, 6, 6, 6, 6];
    private static readonly PartyBit[] PartyBits = BuildPartyBits();

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

        var g = Math.Clamp(_shown, 0, 1);
        var stageF = g * GrowthRules.StageCount;
        var stageIndex = Math.Min((int)Math.Floor(stageF), GrowthRules.StageCount - 1);
        var frac = stageF >= GrowthRules.StageCount ? 1 : stageF - stageIndex;
        var r = (float)(side * (0.18 + 0.13 * g));
        var u = r / 84f;
        var cx = dirtyRect.Center.X;
        var cy = dirtyRect.Center.Y + side * 0.04f;
        var hookLen = (float)(r * (0.20 + 0.08 * g));
        var hookCount = 4 + 16 * g;

        canvas.SaveState();
        var anchorY = cy + r;
        canvas.Translate(cx, anchorY);
        canvas.Scale((float)(_squashX * _breath), (float)(_squashY * _breath));
        canvas.Translate(-cx, -anchorY);

        if (stageIndex == GrowthRules.StageCount - 1)
        {
            DrawAura(canvas, cx, cy, r, u, frac);
        }

        DrawLeaves(canvas, cx, cy, r, u, stageIndex, frac);
        if (Mood == MascotMood.Party)
        {
            DrawPartyBits(canvas, dirtyRect, side);
        }

        DrawHooks(canvas, cx, cy, r, u, hookLen, hookCount);
        DrawBody(canvas, cx, cy, r, u);
        DrawTopper(canvas, cx, cy, r, u, hookLen, stageIndex, frac);
        DrawFace(canvas, cx, cy, u);
        DrawMoodExtras(canvas, cx, cy, r, u);
        canvas.RestoreState();

        DrawSeeds(canvas, cx, cy, r, side);
    }

    private void DrawAura(ICanvas canvas, float cx, float cy, float r, float u, double frac)
    {
        canvas.SaveState();
        canvas.Alpha = (float)(0.18 * (0.4 + 0.6 * frac));
        canvas.FillColor = AccentColor;
        canvas.FillCircle(cx, cy, r * 1.7f);
        canvas.RestoreState();

        canvas.FillColor = AccentColor;
        DrawSparkle(canvas, cx - r * 1.55f, cy - r * 0.95f, 10 * u);
        DrawSparkle(canvas, cx + r * 1.6f, cy - r * 0.45f, 8 * u);
        DrawSparkle(canvas, cx + r * 1.15f, cy - r * 1.5f, 7 * u);
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

    private void DrawLeaves(ICanvas canvas, float cx, float cy, float r, float u, int stageIndex, double frac)
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
            var scale = 2 * pair >= previous ? SmoothStep(Math.Min(1, frac * 3)) : 1;
            if (scale <= 0.01)
            {
                continue;
            }

            var len = (float)(r * (1.0 + 0.1 * pair) * scale);
            var width = len * 0.38f;
            var angle = 50 + 22 * pair;
            foreach (var sign in (ReadOnlySpan<int>)[-1, 1])
            {
                canvas.SaveState();
                canvas.Translate(cx, originY);
                canvas.Rotate(sign * angle);
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

    private void DrawPartyBits(ICanvas canvas, RectF rect, float side)
    {
        foreach (var bit in PartyBits)
        {
            canvas.SaveState();
            canvas.Translate(rect.X + (float)bit.X * rect.Width, rect.Y + (float)bit.Y * side);
            canvas.Rotate((float)bit.Rotation);
            canvas.FillColor = bit.Colour switch { 0 => AccentColor, 1 => BloomEdgeColor, _ => HookColor };
            canvas.FillRoundedRectangle(-side * 0.018f, -side * 0.01f, side * 0.036f, side * 0.02f, side * 0.005f);
            canvas.RestoreState();
        }
    }

    private void DrawHooks(ICanvas canvas, float cx, float cy, float r, float u, float hookLen, double hookCount)
    {
        var full = (int)Math.Floor(hookCount);
        var partial = hookCount - full;
        var visible = Math.Min(HookOrder.Length, full + (partial > 0.02 ? 1 : 0));
        var raised = Mood == MascotMood.Cheer ? RaisedSlot(visible) : -1;
        var baseR = r - 4 * u;
        for (var i = 0; i < visible; i++)
        {
            var slot = HookOrder[i];
            var scale = i < full ? 1.0 : partial;
            var angle = -90 + slot * 18 + HookJitter[slot];
            if (Mood == MascotMood.Sad)
            {
                var c = Math.Cos(angle * Math.PI / 180);
                if (Math.Abs(c) > 0.6)
                {
                    angle += 6 * Math.Sign(c);
                }
            }

            var len = (float)(hookLen * (1 + HookLengthJitter[slot]) * scale);
            var w = (float)(13 * u * (0.6 + 0.4 * scale));
            if (slot == raised)
            {
                len = r * 0.42f;
                w = 15 * u;
            }

            var rad = angle * Math.PI / 180;
            var x0 = cx + (float)(Math.Cos(rad) * baseR);
            var y0 = cy + (float)(Math.Sin(rad) * baseR);
            canvas.SaveState();
            canvas.Translate(x0, y0);
            canvas.Rotate((float)angle + 90);
            canvas.FillColor = HookColor;
            canvas.FillRoundedRectangle(-w / 2, -len, w, len + 6 * u, w / 2);
            canvas.RestoreState();

            var tipR = w * 0.36f;
            canvas.FillColor = HookTipColor;
            canvas.FillCircle(x0 + (float)(Math.Cos(rad) * (len - tipR)), y0 + (float)(Math.Sin(rad) * (len - tipR)), tipR);
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
        canvas.SetFillPaint(paint, rect);
        canvas.FillEllipse(rect);

        canvas.SaveState();
        canvas.Alpha = 0.55f;
        canvas.FillColor = BodyLightColor;
        canvas.FillEllipse(cx - 52 * u, cy - 56 * u, 60 * u, 36 * u);
        canvas.RestoreState();
    }

    private void DrawTopper(ICanvas canvas, float cx, float cy, float r, float u, float hookLen, int stageIndex, double frac)
    {
        if (stageIndex < (int)GrowthStage.Bud - 1)
        {
            return;
        }

        var topY = cy - r - hookLen * 0.5f;
        var scaleIn = (float)SmoothStep(Math.Min(1, frac * 4));
        if (scaleIn <= 0.01f)
        {
            return;
        }

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
    }

    private void DrawFace(ICanvas canvas, float cx, float cy, float u)
    {
        var eyeL = cx - 24 * u;
        var eyeR = cx + 24 * u;
        var eyeY = cy - 8 * u;
        switch (Mood)
        {
            case MascotMood.Joy:
                EyeArc(canvas, eyeL, eyeY, u, up: true);
                EyeArc(canvas, eyeR, eyeY, u, up: true);
                Brow(canvas, eyeL, eyeY, u, 0, 4);
                Brow(canvas, eyeR, eyeY, u, 0, 4);
                Blush(canvas, cx, cy, u);
                Mouth(canvas, cx, cy, u, MouthKind.Smile);
                break;
            case MascotMood.Cheer:
                Eye(canvas, eyeL, eyeY, u, 2, -2, 1);
                Eye(canvas, eyeR, eyeY, u, 2, -2, 1);
                Brow(canvas, eyeL, eyeY, u, -2, 6);
                Brow(canvas, eyeR, eyeY, u, 2, 8);
                Mouth(canvas, cx, cy, u, MouthKind.Smile);
                break;
            case MascotMood.Surprise:
                Eye(canvas, eyeL, eyeY, u, 0, 0, 1.15f);
                Eye(canvas, eyeR, eyeY, u, 0, 0, 1.15f);
                Brow(canvas, eyeL, eyeY, u, 0, 12);
                Brow(canvas, eyeR, eyeY, u, 0, 12);
                Mouth(canvas, cx, cy, u, MouthKind.O);
                break;
            case MascotMood.Sad:
                Eye(canvas, eyeL, eyeY, u, -4, 3, 1);
                Eye(canvas, eyeR, eyeY, u, -4, 3, 1);
                Brow(canvas, eyeL, eyeY, u, -5, 0);
                Brow(canvas, eyeR, eyeY, u, 5, 0);
                Mouth(canvas, cx, cy, u, MouthKind.Flat);
                break;
            case MascotMood.Sleep:
                EyeArc(canvas, eyeL, eyeY, u, up: false);
                EyeArc(canvas, eyeR, eyeY, u, up: false);
                Mouth(canvas, cx, cy, u, MouthKind.Dot);
                break;
            case MascotMood.Party:
                EyeArc(canvas, eyeL, eyeY, u, up: true);
                EyeArc(canvas, eyeR, eyeY, u, up: true);
                Brow(canvas, eyeL, eyeY, u, 0, 6);
                Brow(canvas, eyeR, eyeY, u, 0, 6);
                Blush(canvas, cx, cy, u);
                Mouth(canvas, cx, cy, u, MouthKind.Grin);
                break;
            case MascotMood.Think:
                Eye(canvas, eyeL, eyeY, u, 5, -6, 1);
                Eye(canvas, eyeR, eyeY, u, 5, -6, 1);
                Brow(canvas, eyeL, eyeY, u, 0, 2);
                Brow(canvas, eyeR, eyeY, u, 3, 10);
                Mouth(canvas, cx, cy, u, MouthKind.Dot);
                break;
            default:
                EyeArc(canvas, eyeL, eyeY, u, up: false);
                EyeArc(canvas, eyeR, eyeY, u, up: false);
                Brow(canvas, eyeL, eyeY, u, 0, 6);
                Brow(canvas, eyeR, eyeY, u, 0, 6);
                Blush(canvas, cx, cy, u);
                Mouth(canvas, cx, cy, u, MouthKind.Smile);
                break;
        }
    }

    private void Eye(ICanvas canvas, float x, float y, float u, float lookX, float lookY, float scale)
    {
        var rx = 18 * u * scale;
        var ryFull = 23 * u * scale;
        var open = (float)_eyeOpen;
        var ry = ryFull * open;
        canvas.FillColor = EyeColor;
        canvas.FillEllipse(x - rx, y - ry, 2 * rx, 2 * ry);
        if (open < 0.15f)
        {
            return;
        }

        var pr = ryFull * 0.6f;
        var px = x + lookX * u;
        var py = y + lookY * u;
        canvas.FillColor = PupilColor;
        canvas.FillEllipse(px - pr, py - pr * open, 2 * pr, 2 * pr * open);
        var hr = pr * 0.28f;
        canvas.FillColor = EyeColor;
        canvas.FillEllipse(px - pr * 0.35f - hr, py - pr * 0.4f * open - hr * open, 2 * hr, 2 * hr * open);
    }

    private void EyeArc(ICanvas canvas, float x, float y, float u, bool up)
    {
        var d = up ? -1 : 1;
        var path = new PathF();
        path.MoveTo(x - 18 * u, y + 4 * u * d);
        path.QuadTo(x, y - 18 * u * d, x + 18 * u, y + 4 * u * d);
        canvas.StrokeColor = PupilColor;
        canvas.StrokeSize = 7 * u;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawPath(path);
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

    private void Mouth(ICanvas canvas, float cx, float cy, float u, MouthKind kind)
    {
        var y = cy + 34 * u;
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
    }

    private void Blush(ICanvas canvas, float cx, float cy, float u)
    {
        canvas.SaveState();
        canvas.Alpha = 0.9f;
        canvas.FillColor = BlushColor;
        canvas.FillEllipse(cx - 46 * u - 12 * u, cy + 16 * u - 7 * u, 24 * u, 14 * u);
        canvas.FillEllipse(cx + 46 * u - 12 * u, cy + 16 * u - 7 * u, 24 * u, 14 * u);
        canvas.RestoreState();
    }

    private void DrawMoodExtras(ICanvas canvas, float cx, float cy, float r, float u)
    {
        switch (Mood)
        {
            case MascotMood.Sleep:
                Zee(canvas, cx + 72 * u, cy - 104 * u, 18 * u);
                Zee(canvas, cx + 98 * u, cy - 126 * u, 12 * u);
                break;
            case MascotMood.Think:
                canvas.FillColor = BodyShadeColor;
                for (var i = 0; i < 3; i++)
                {
                    canvas.FillCircle(cx + 86 * u + i * 14 * u, cy - 92 * u - i * 12 * u, (4 + i) * u);
                }

                break;
            case MascotMood.Party:
                // The hat sits tilted on the upper right so a bud or bloom on top stays visible.
                canvas.SaveState();
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

    private void DrawSeeds(ICanvas canvas, float cx, float cy, float r, float side)
    {
        if (_feedProgress >= 1 || _seeds.Count == 0)
        {
            return;
        }

        foreach (var seed in _seeds)
        {
            var t = Math.Clamp((_feedProgress - seed.Delay) / 0.55, 0, 1);
            if (t <= 0)
            {
                continue;
            }

            var ease = t * t;
            var distance = side * 0.5 + (r * 0.9 - side * 0.5) * ease;
            var rad = seed.Angle * Math.PI / 180;
            var x = cx + (float)(Math.Cos(rad) * distance);
            var y = cy + (float)(Math.Sin(rad) * distance);
            var size = (float)(seed.Size * side);
            canvas.SaveState();
            canvas.Alpha = (float)(t < 0.85 ? 1 : (1 - t) / 0.15);
            canvas.FillColor = HookColor;
            canvas.FillCircle(x, y, size);
            canvas.FillColor = HookTipColor;
            canvas.FillCircle(x, y, size * 0.4f);
            canvas.RestoreState();
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

    private enum MouthKind
    {
        Smile,
        Grin,
        O,
        Flat,
        Dot,
    }

    private sealed record PartyBit(double X, double Y, double Rotation, int Colour);
}
