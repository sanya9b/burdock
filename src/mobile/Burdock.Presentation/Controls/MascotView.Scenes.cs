namespace Burdock.Presentation.Controls;

/// <summary>
/// Scenes of the shop (decision log item 22, spec 02 §3.10 «Фони»): a rounded picture that fills the view behind the burr, drawn first, with its
/// horizon on the ground line so the burr stands on it. Same rendering language as the body: flat shapes, one shade and one light per form, no
/// outlines, colours from the primitive ramps through the style. Everything is still; the living burr moves in front of it.
/// </summary>
public sealed partial class MascotView
{
    public static readonly BindableProperty SceneProperty =
        BindableProperty.Create(nameof(Scene), typeof(MascotScene), typeof(MascotView), MascotScene.None, propertyChanged: (b, _, _) => ((MascotView)b).Invalidate());

    public static readonly BindableProperty RideProperty =
        BindableProperty.Create(nameof(Ride), typeof(MascotRide), typeof(MascotView), MascotRide.None, propertyChanged: (b, _, _) => ((MascotView)b).Invalidate());

    public static readonly BindableProperty SceneCornerRadiusProperty =
        BindableProperty.Create(nameof(SceneCornerRadius), typeof(double), typeof(MascotView), -1d, propertyChanged: (b, _, _) => ((MascotView)b).Invalidate());

    public static readonly BindableProperty SceneSkyColorProperty = Colour(nameof(SceneSkyColor), Colors.LightSkyBlue);
    public static readonly BindableProperty SceneNightColorProperty = Colour(nameof(SceneNightColor), Colors.MidnightBlue);
    public static readonly BindableProperty SceneStoneColorProperty = Colour(nameof(SceneStoneColor), Colors.Gray);

    private static readonly (float X, float Y, float Size)[] StarField =
    [
        (0.08f, 0.12f, 1.0f), (0.2f, 0.3f, 0.6f), (0.31f, 0.09f, 0.8f), (0.42f, 0.22f, 0.5f), (0.55f, 0.08f, 1.2f), (0.63f, 0.3f, 0.6f),
        (0.74f, 0.14f, 0.7f), (0.88f, 0.07f, 0.9f), (0.93f, 0.33f, 0.5f), (0.14f, 0.48f, 0.5f), (0.36f, 0.4f, 0.4f), (0.8f, 0.46f, 0.6f),
        (0.05f, 0.62f, 0.4f), (0.96f, 0.58f, 0.5f), (0.5f, 0.5f, 0.4f), (0.68f, 0.6f, 0.5f),
    ];

    /// <summary>The picture behind the burr; None leaves the view transparent as before.</summary>
    public MascotScene Scene { get => (MascotScene)GetValue(SceneProperty); set => SetValue(SceneProperty, value); }

    /// <summary>The vehicle the burr rides; None draws nothing.</summary>
    public MascotRide Ride { get => (MascotRide)GetValue(RideProperty); set => SetValue(RideProperty, value); }

    /// <summary>Corner radius of the scene picture; negative (default) rounds it like a tile, 0 lets a full-bleed card do the clipping (S-17 growth card, S-16 preview).</summary>
    public double SceneCornerRadius { get => (double)GetValue(SceneCornerRadiusProperty); set => SetValue(SceneCornerRadiusProperty, value); }

    public Color SceneSkyColor { get => (Color)GetValue(SceneSkyColorProperty); set => SetValue(SceneSkyColorProperty, value); }

    public Color SceneNightColor { get => (Color)GetValue(SceneNightColorProperty); set => SetValue(SceneNightColorProperty, value); }

    public Color SceneStoneColor { get => (Color)GetValue(SceneStoneColorProperty); set => SetValue(SceneStoneColorProperty, value); }

    /// <summary>Draws the scene card clipped to a rounded square; `gy` is the ground line of the burr.</summary>
    private void DrawScene(ICanvas canvas, RectF rect, float side, float gy)
    {
        if (Scene == MascotScene.None)
        {
            return;
        }

        // The picture fills the whole canvas (square on tiles, wide on the big surfaces); sizes follow the width so a wide card gets a wider sky.
        var card = rect;
        var w = card.Width;
        canvas.SaveState();
        var clip = new PathF();
        clip.AppendRoundedRectangle(card, SceneCornerRadius < 0 ? side * 0.14f : (float)SceneCornerRadius);
        canvas.ClipPath(clip);
        switch (Scene)
        {
            case MascotScene.Meadow:
                DrawMeadow(canvas, card, w, gy);
                break;
            case MascotScene.Sea:
                DrawSea(canvas, card, w, gy);
                break;
            case MascotScene.City:
                DrawCity(canvas, card, w, gy);
                break;
            case MascotScene.Night:
                DrawNight(canvas, card, w, gy);
                break;
            case MascotScene.Winter:
                DrawWinter(canvas, card, w, gy);
                break;
            case MascotScene.Space:
                DrawSpace(canvas, card, w);
                break;
            default:
                break;
        }

        canvas.RestoreState();
    }

    private static void Gradient(ICanvas canvas, RectF area, Color from, Color to, bool diagonal = false)
    {
        var paint = new LinearGradientPaint([new PaintGradientStop(0f, from), new PaintGradientStop(1f, to)], new Point(0, 0), diagonal ? new Point(1, 1) : new Point(0, 1));
        canvas.SaveState();
        canvas.SetFillPaint(paint, area);
        canvas.FillRectangle(area);
        canvas.RestoreState();
    }

    private void Sun(ICanvas canvas, float x, float y, float radius)
    {
        canvas.SaveState();
        canvas.Alpha = 0.28f;
        canvas.FillColor = AccentColor;
        canvas.FillCircle(x, y, radius * 1.7f);
        canvas.RestoreState();
        canvas.FillColor = AccentColor;
        canvas.FillCircle(x, y, radius);
    }

    private void Cloud(ICanvas canvas, float x, float y, float s, float alpha)
    {
        canvas.SaveState();
        canvas.Alpha = alpha;
        canvas.FillColor = OutfitLightColor;
        canvas.FillCircle(x, y, s);
        canvas.FillCircle(x + s * 1.1f, y - s * 0.35f, s * 1.25f);
        canvas.FillCircle(x + s * 2.3f, y, s * 0.95f);
        canvas.FillRoundedRectangle(x - s * 0.2f, y - s * 0.1f, s * 3.4f, s * 1.1f, s * 0.5f);
        canvas.RestoreState();
    }

    private void Stars(ICanvas canvas, RectF card, float w, float scale, int sparkles)
    {
        canvas.FillColor = OutfitLightColor;
        canvas.StrokeColor = OutfitLightColor;
        canvas.StrokeSize = w * 0.006f;
        canvas.StrokeLineCap = LineCap.Round;
        var i = 0;
        foreach (var (sx, sy, size) in StarField)
        {
            var x = card.Left + sx * w;
            var y = card.Top + sy * w * scale;
            canvas.FillCircle(x, y, w * 0.009f * size);
            if (i++ < sparkles)
            {
                var arm = w * 0.03f * size;
                canvas.DrawLine(x - arm, y, x + arm, y);
                canvas.DrawLine(x, y - arm, x, y + arm);
            }
        }
    }

    private void DrawMeadow(ICanvas canvas, RectF card, float w, float gy)
    {
        Gradient(canvas, card, SceneSkyColor, OutfitCoolColor);
        Sun(canvas, card.Right - 0.24f * w, card.Top + 0.22f * w, 0.09f * w);
        Cloud(canvas, card.Left + 0.16f * w, card.Top + 0.2f * w, 0.055f * w, 0.95f);
        Cloud(canvas, card.Left + 0.6f * w, card.Top + 0.14f * w, 0.035f * w, 0.8f);
        canvas.FillColor = LeafEdgeColor;
        canvas.FillEllipse(card.Left + 0.25f * w, gy - 0.2f * w, 1.1f * w, 0.44f * w);
        canvas.FillColor = LeafColor;
        canvas.FillEllipse(card.Left - 0.42f * w, gy - 0.14f * w, 1.24f * w, 0.4f * w);
        canvas.FillRectangle(card.Left, gy + 0.04f * w, w, card.Bottom - gy);
        foreach (var (fx, fy, warm) in (ReadOnlySpan<(float, float, bool)>)[(0.1f, 0.05f, true), (0.2f, 0.1f, false), (0.84f, 0.06f, false), (0.92f, 0.11f, true), (0.72f, 0.12f, true)])
        {
            var x = card.Left + fx * w;
            var y = gy + fy * w;
            canvas.FillColor = warm ? OutfitWarmColor : OutfitLightColor;
            canvas.FillCircle(x, y, 0.016f * w);
            canvas.FillColor = AccentColor;
            canvas.FillCircle(x, y, 0.007f * w);
        }
    }

    private void DrawSea(ICanvas canvas, RectF card, float w, float gy)
    {
        Gradient(canvas, card, SceneSkyColor, OutfitLightColor);
        Sun(canvas, card.Left + 0.3f * w, gy - 0.32f * w, 0.1f * w);
        Cloud(canvas, card.Left + 0.62f * w, card.Top + 0.2f * w, 0.045f * w, 0.9f);
        canvas.StrokeColor = OutfitInkColor;
        canvas.StrokeSize = w * 0.008f;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.SaveState();
        canvas.Alpha = 0.55f;
        foreach (var (gx, gyy) in (ReadOnlySpan<(float, float)>)[(0.74f, 0.36f), (0.84f, 0.3f)])
        {
            var x = card.Left + gx * w;
            var y = card.Top + gyy * w;
            canvas.DrawLine(x - 0.035f * w, y, x, y + 0.02f * w);
            canvas.DrawLine(x, y + 0.02f * w, x + 0.035f * w, y);
        }

        canvas.RestoreState();

        var sea = new RectF(card.Left, gy - 0.06f * w, w, card.Bottom - gy + 0.06f * w);
        Gradient(canvas, sea, OutfitCoolColor, OutfitCoolEdgeColor);
        canvas.FillColor = OutfitLightColor;
        var hullY = gy - 0.08f * w;
        var hull = new PathF();
        hull.MoveTo(card.Left + 0.08f * w, hullY);
        hull.LineTo(card.Left + 0.22f * w, hullY);
        hull.LineTo(card.Left + 0.2f * w, hullY + 0.03f * w);
        hull.LineTo(card.Left + 0.1f * w, hullY + 0.03f * w);
        hull.Close();
        canvas.FillColor = OutfitInkColor;
        canvas.FillPath(hull);
        var sail = new PathF();
        sail.MoveTo(card.Left + 0.15f * w, hullY - 0.01f * w);
        sail.LineTo(card.Left + 0.15f * w, hullY - 0.12f * w);
        sail.LineTo(card.Left + 0.21f * w, hullY - 0.01f * w);
        sail.Close();
        canvas.FillColor = OutfitLightColor;
        canvas.FillPath(sail);

        canvas.SaveState();
        canvas.Alpha = 0.65f;
        canvas.StrokeColor = OutfitLightColor;
        canvas.StrokeSize = w * 0.01f;
        foreach (var (row, offset) in (ReadOnlySpan<(float, float)>)[(0.0f, 0f), (0.06f, 0.07f), (0.12f, 0.02f)])
        {
            for (var x = card.Left - 0.1f * w + offset * w; x < card.Right; x += 0.14f * w)
            {
                canvas.DrawArc(x, gy - 0.02f * w + row * w, 0.14f * w, 0.05f * w, 0, 180, clockwise: false, closed: false);
            }
        }

        canvas.RestoreState();
    }

    private void DrawCity(ICanvas canvas, RectF card, float w, float gy)
    {
        Gradient(canvas, card, OutfitSunColor, OutfitWarmColor);
        Sun(canvas, card.Left + 0.34f * w, gy - 0.1f * w, 0.13f * w);
        canvas.FillColor = BodyShadeColor;
        var blocks = (ReadOnlySpan<(float X, float H)>)[(-0.02f, 0.42f), (0.11f, 0.28f), (0.24f, 0.56f), (0.38f, 0.36f), (0.5f, 0.64f), (0.64f, 0.4f), (0.76f, 0.5f), (0.9f, 0.3f)];
        foreach (var (bx, bh) in blocks)
        {
            canvas.FillColor = BodyShadeColor;
            canvas.FillRectangle(card.Left + bx * w, gy - bh * w, 0.125f * w, bh * w + 0.1f * w);
        }

        canvas.FillColor = OutfitSunColor;
        var k = 0;
        foreach (var (bx, bh) in blocks)
        {
            for (var row = 0; row < (int)(bh / 0.07f); row++)
            {
                for (var col = 0; col < 2; col++)
                {
                    if (++k % 3 == 0)
                    {
                        continue;
                    }

                    canvas.FillRectangle(card.Left + (bx + 0.025f + col * 0.055f) * w, gy - bh * w + (0.03f + row * 0.07f) * w, 0.022f * w, 0.028f * w);
                }
            }
        }

        canvas.FillColor = SceneStoneColor;
        canvas.FillRectangle(card.Left, gy + 0.02f * w, w, card.Bottom - gy);
        canvas.SaveState();
        canvas.Alpha = 0.7f;
        canvas.FillColor = OutfitLightColor;
        for (var x = card.Left + 0.04f * w; x < card.Right; x += 0.16f * w)
        {
            canvas.FillRoundedRectangle(x, gy + 0.085f * w, 0.08f * w, 0.012f * w, 0.006f * w);
        }

        canvas.RestoreState();
    }

    private void DrawNight(ICanvas canvas, RectF card, float w, float gy)
    {
        Gradient(canvas, card, SceneNightColor, OutfitInkColor);
        Stars(canvas, card, w, 0.75f, 2);
        var mx = card.Right - 0.26f * w;
        var my = card.Top + 0.22f * w;
        canvas.FillColor = OutfitLightColor;
        canvas.FillCircle(mx, my, 0.1f * w);
        canvas.FillColor = SceneNightColor;
        canvas.FillCircle(mx + 0.045f * w, my - 0.03f * w, 0.088f * w);
        canvas.SaveState();
        canvas.Alpha = 0.9f;
        canvas.FillColor = BodyShadeColor;
        canvas.FillEllipse(card.Left + 0.3f * w, gy - 0.16f * w, 1.1f * w, 0.4f * w);
        canvas.FillEllipse(card.Left - 0.45f * w, gy - 0.1f * w, 1.2f * w, 0.36f * w);
        canvas.FillRectangle(card.Left, gy + 0.05f * w, w, card.Bottom - gy);
        canvas.RestoreState();
    }

    private void DrawWinter(ICanvas canvas, RectF card, float w, float gy)
    {
        Gradient(canvas, card, SceneSkyColor, OutfitLightColor);
        canvas.FillColor = OutfitLightColor;
        canvas.FillEllipse(card.Left + 0.3f * w, gy - 0.18f * w, 1.1f * w, 0.42f * w);
        canvas.FillEllipse(card.Left - 0.45f * w, gy - 0.12f * w, 1.25f * w, 0.38f * w);
        canvas.FillRectangle(card.Left, gy + 0.04f * w, w, card.Bottom - gy);
        canvas.SaveState();
        canvas.Alpha = 0.45f;
        canvas.FillColor = SceneSkyColor;
        canvas.FillEllipse(card.Left + 0.1f * w, gy + 0.02f * w, 0.9f * w, 0.2f * w);
        canvas.RestoreState();
        foreach (var (tx, scale) in (ReadOnlySpan<(float, float)>)[(0.13f, 1f), (0.86f, 0.8f)])
        {
            Fir(canvas, card.Left + tx * w, gy - 0.02f * w, 0.26f * w * scale);
        }

        canvas.FillColor = OutfitLightColor;
        canvas.StrokeColor = OutfitLightColor;
        canvas.StrokeSize = w * 0.006f;
        var i = 0;
        foreach (var (sx, sy, size) in StarField)
        {
            var x = card.Left + sx * w;
            var y = card.Top + sy * w * 0.8f;
            canvas.FillCircle(x, y, w * 0.008f * size);
            if (i++ % 5 == 0)
            {
                var arm = w * 0.02f;
                canvas.DrawLine(x - arm, y, x + arm, y);
                canvas.DrawLine(x, y - arm, x, y + arm);
                canvas.DrawLine(x - arm * 0.7f, y - arm * 0.7f, x + arm * 0.7f, y + arm * 0.7f);
                canvas.DrawLine(x - arm * 0.7f, y + arm * 0.7f, x + arm * 0.7f, y - arm * 0.7f);
            }
        }
    }

    private void Fir(ICanvas canvas, float x, float baseY, float height)
    {
        canvas.FillColor = OutfitInkColor;
        canvas.FillRectangle(x - height * 0.06f, baseY - height * 0.1f, height * 0.12f, height * 0.12f);
        for (var tier = 0; tier < 3; tier++)
        {
            var top = baseY - height * (0.45f + tier * 0.3f);
            var bottom = baseY - height * (0.08f + tier * 0.3f);
            var half = height * (0.34f - tier * 0.08f);
            var tri = new PathF();
            tri.MoveTo(x, top);
            tri.LineTo(x + half, bottom);
            tri.LineTo(x - half, bottom);
            tri.Close();
            canvas.FillColor = LeafEdgeColor;
            canvas.FillPath(tri);
            var cap = new PathF();
            cap.MoveTo(x, top);
            cap.LineTo(x + half * 0.42f, top + (bottom - top) * 0.42f);
            cap.LineTo(x - half * 0.42f, top + (bottom - top) * 0.42f);
            cap.Close();
            canvas.FillColor = OutfitLightColor;
            canvas.FillPath(cap);
        }
    }

    private void DrawSpace(ICanvas canvas, RectF card, float w)
    {
        Gradient(canvas, card, OutfitInkColor, SceneNightColor, diagonal: true);
        canvas.SaveState();
        canvas.Alpha = 0.16f;
        canvas.FillColor = BodyColor;
        canvas.FillCircle(card.Left + 0.3f * w, card.Top + 0.68f * w, 0.32f * w);
        canvas.FillColor = OutfitCoolColor;
        canvas.FillCircle(card.Left + 0.75f * w, card.Top + 0.8f * w, 0.24f * w);
        canvas.RestoreState();
        Stars(canvas, card, w, 1.0f, 3);

        var px = card.Right - 0.22f * w;
        var py = card.Top + 0.24f * w;
        var pr = 0.11f * w;
        canvas.FillColor = OutfitWarmColor;
        canvas.FillCircle(px, py, pr);
        canvas.SaveState();
        canvas.Alpha = 0.5f;
        canvas.FillColor = OutfitWarmEdgeColor;
        var shade = new PathF();
        shade.AppendCircle(px, py, pr);
        shade.AppendCircle(px - pr * 0.35f, py - pr * 0.3f, pr * 0.95f);
        canvas.FillPath(shade, WindingMode.EvenOdd);
        canvas.RestoreState();
        canvas.SaveState();
        canvas.Translate(px, py);
        canvas.Rotate(-18);
        canvas.StrokeColor = AccentColor;
        canvas.StrokeSize = w * 0.018f;
        canvas.DrawEllipse(-pr * 1.9f, -pr * 0.42f, pr * 3.8f, pr * 0.84f);
        canvas.RestoreState();
        canvas.FillColor = OutfitWarmColor;
        canvas.FillCircle(px, py - pr * 0.1f, pr * 0.72f);

        canvas.FillColor = OutfitCoolColor;
        canvas.FillCircle(card.Left + 0.17f * w, card.Top + 0.2f * w, 0.05f * w);
        canvas.FillColor = OutfitLightColor;
        canvas.FillCircle(card.Left + 0.155f * w, card.Top + 0.185f * w, 0.014f * w);
        canvas.StrokeColor = OutfitLightColor;
        canvas.StrokeSize = w * 0.01f;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.SaveState();
        canvas.Alpha = 0.8f;
        canvas.DrawLine(card.Left + 0.5f * w, card.Top + 0.1f * w, card.Left + 0.64f * w, card.Top + 0.03f * w);
        canvas.RestoreState();
        canvas.FillCircle(card.Left + 0.64f * w, card.Top + 0.03f * w, w * 0.016f);
    }
}
