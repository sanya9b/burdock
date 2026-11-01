namespace Burdock.Presentation.Controls;

/// <summary>
/// Cosmetics of the shop on the procedural mascot (spec 07 §3.2.3, spec 02 §3.10 «Вбрання як косметика»): outfits are layers drawn inside the
/// body transform (behind the body for the backpack, over the face for everything else), the avatar frame is a ring outside it. At most two
/// colours per accessory, all from the primitive ramps the style binds.
/// </summary>
public sealed partial class MascotView
{
    public static readonly BindableProperty OutfitProperty =
        BindableProperty.Create(nameof(Outfit), typeof(MascotOutfit), typeof(MascotView), MascotOutfit.None, propertyChanged: (b, _, _) => ((MascotView)b).Invalidate());

    public static readonly BindableProperty AvatarFrameProperty =
        BindableProperty.Create(nameof(AvatarFrame), typeof(MascotFrame), typeof(MascotView), MascotFrame.None, propertyChanged: (b, _, _) => ((MascotView)b).Invalidate());

    public static readonly BindableProperty OutfitWarmColorProperty = Colour(nameof(OutfitWarmColor), Colors.Tomato);
    public static readonly BindableProperty OutfitWarmEdgeColorProperty = Colour(nameof(OutfitWarmEdgeColor), Colors.Firebrick);
    public static readonly BindableProperty OutfitCoolColorProperty = Colour(nameof(OutfitCoolColor), Colors.DeepSkyBlue);
    public static readonly BindableProperty OutfitCoolEdgeColorProperty = Colour(nameof(OutfitCoolEdgeColor), Colors.SteelBlue);
    public static readonly BindableProperty OutfitSunColorProperty = Colour(nameof(OutfitSunColor), Colors.Khaki);
    public static readonly BindableProperty OutfitSunEdgeColorProperty = Colour(nameof(OutfitSunEdgeColor), Colors.DarkGoldenrod);
    public static readonly BindableProperty OutfitLightColorProperty = Colour(nameof(OutfitLightColor), Colors.White);
    public static readonly BindableProperty OutfitInkColorProperty = Colour(nameof(OutfitInkColor), Colors.Black);

    /// <summary>What the burr wears (an owned outfit from the shop, or the one being tried on).</summary>
    public MascotOutfit Outfit { get => (MascotOutfit)GetValue(OutfitProperty); set => SetValue(OutfitProperty, value); }

    /// <summary>Avatar frame: a ring around the figure, outside the body transform so it never hops.</summary>
    public MascotFrame AvatarFrame { get => (MascotFrame)GetValue(AvatarFrameProperty); set => SetValue(AvatarFrameProperty, value); }

    public Color OutfitWarmColor { get => (Color)GetValue(OutfitWarmColorProperty); set => SetValue(OutfitWarmColorProperty, value); }

    public Color OutfitWarmEdgeColor { get => (Color)GetValue(OutfitWarmEdgeColorProperty); set => SetValue(OutfitWarmEdgeColorProperty, value); }

    public Color OutfitCoolColor { get => (Color)GetValue(OutfitCoolColorProperty); set => SetValue(OutfitCoolColorProperty, value); }

    public Color OutfitCoolEdgeColor { get => (Color)GetValue(OutfitCoolEdgeColorProperty); set => SetValue(OutfitCoolEdgeColorProperty, value); }

    public Color OutfitSunColor { get => (Color)GetValue(OutfitSunColorProperty); set => SetValue(OutfitSunColorProperty, value); }

    public Color OutfitSunEdgeColor { get => (Color)GetValue(OutfitSunEdgeColorProperty); set => SetValue(OutfitSunEdgeColorProperty, value); }

    public Color OutfitLightColor { get => (Color)GetValue(OutfitLightColorProperty); set => SetValue(OutfitLightColorProperty, value); }

    public Color OutfitInkColor { get => (Color)GetValue(OutfitInkColorProperty); set => SetValue(OutfitInkColorProperty, value); }

    /// <summary>The frame ring, drawn first and outside the body transform.</summary>
    private void DrawFrame(ICanvas canvas, RectF rect, float side, float u)
    {
        if (AvatarFrame == MascotFrame.None)
        {
            return;
        }

        var color = AvatarFrame switch
        {
            MascotFrame.Leaf => LeafColor,
            MascotFrame.Lime => HookColor,
            MascotFrame.Violet => BodyColor,
            MascotFrame.Coral => OutfitWarmColor,
            MascotFrame.Sunny => AccentColor,
            MascotFrame.Sky => OutfitCoolColor,
            _ => AccentColor,
        };
        var cx = rect.Center.X;
        var cy = rect.Center.Y;
        var radius = side * 0.46f;
        canvas.StrokeColor = color;
        canvas.StrokeSize = 5 * u;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawCircle(cx, cy, radius);

        switch (AvatarFrame)
        {
            case MascotFrame.Gold:
                canvas.StrokeColor = OutfitSunEdgeColor;
                canvas.StrokeSize = 1.5f * u;
                canvas.DrawCircle(cx, cy, radius - 5 * u);
                canvas.FillColor = AccentColor;
                for (var i = 0; i < 4; i++)
                {
                    var a = Math.PI / 4 + i * Math.PI / 2;
                    DrawSparkle(canvas, cx + (float)(Math.Cos(a) * radius), cy + (float)(Math.Sin(a) * radius), 6 * u);
                }

                break;
            case MascotFrame.Leaf:
                // Small leaf ticks along the ring, like a laurel.
                canvas.FillColor = LeafColor;
                for (var i = 0; i < 12; i++)
                {
                    var a = i * Math.PI / 6;
                    canvas.SaveState();
                    canvas.Translate(cx + (float)(Math.Cos(a) * radius), cy + (float)(Math.Sin(a) * radius));
                    canvas.Rotate((float)(a * 180 / Math.PI) + 90);
                    var leaf = new PathF();
                    leaf.MoveTo(0, -7 * u);
                    leaf.QuadTo(5 * u, 0, 0, 7 * u);
                    leaf.QuadTo(-5 * u, 0, 0, -7 * u);
                    leaf.Close();
                    canvas.FillPath(leaf);
                    canvas.RestoreState();
                }

                break;
            default:
                break;
        }
    }

    /// <summary>Layers behind the body (the backpack), drawn after the leaves and before the hooks.</summary>
    private void DrawOutfitBehind(ICanvas canvas, float cx, float cy, float r, float u)
    {
        if (Outfit != MascotOutfit.Backpack)
        {
            return;
        }

        var left = cx - r * 1.08f;
        var top = cy - r * 0.38f;
        var width = r * 0.62f;
        var height = r * 0.92f;
        canvas.FillColor = OutfitCoolColor;
        canvas.FillRoundedRectangle(left, top, width, height, 10 * u);
        canvas.FillColor = OutfitCoolEdgeColor;
        canvas.FillRoundedRectangle(left, top, width, height * 0.32f, 10 * u);
        canvas.FillColor = OutfitLightColor;
        canvas.FillCircle(left + width / 2, top + height * 0.32f, 4 * u);
    }

    /// <summary>Layers over the body and face, drawn after the mood extras.</summary>
    private void DrawOutfit(ICanvas canvas, float cx, float cy, float r, float u)
    {
        switch (Outfit)
        {
            case MascotOutfit.Scarf:
                DrawScarf(canvas, cx, cy, r, u);
                break;
            case MascotOutfit.Glasses:
                DrawGlasses(canvas, cx, cy, u);
                break;
            case MascotOutfit.StrawHat:
                DrawStrawHat(canvas, cx, cy, r, u);
                break;
            case MascotOutfit.Backpack:
                DrawStraps(canvas, cx, cy, r, u);
                break;
            case MascotOutfit.Raincoat:
                DrawRaincoat(canvas, cx, cy, r, u);
                break;
            case MascotOutfit.Wreath:
                DrawWreath(canvas, cx, cy, r, u);
                break;
            case MascotOutfit.Sweater:
                DrawSweater(canvas, cx, cy, r, u);
                break;
            case MascotOutfit.Bandana:
                DrawBandana(canvas, cx, cy, r, u);
                break;
            case MascotOutfit.Vyshyvanka:
                DrawVyshyvanka(canvas, cx, cy, r, u);
                break;
            case MascotOutfit.SpaceHelmet:
                DrawSpaceHelmet(canvas, cx, cy, r, u);
                break;
            case MascotOutfit.Crown:
                DrawCrown(canvas, cx, cy, r, u);
                break;
            default:
                break;
        }
    }

    /// <summary>A band around the body as a thick stroke. DrawArc counts degrees counter-clockwise from 3 o'clock (90 = top), so 200..340 is the lower half and 20..160 the upper; decorations placed along a band use y = cy - sin(angle).</summary>
    private static void Band(ICanvas canvas, float cx, float cy, float radius, float thickness, float startAngle, float endAngle, Color color)
    {
        canvas.StrokeColor = color;
        canvas.StrokeSize = thickness;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawArc(cx - radius, cy - radius, 2 * radius, 2 * radius, startAngle, endAngle, clockwise: false, closed: false);
    }

    private void DrawScarf(ICanvas canvas, float cx, float cy, float r, float u)
    {
        Band(canvas, cx, cy, r * 0.9f, 16 * u, 205, 335, OutfitWarmColor);
        Band(canvas, cx, cy, r * 0.9f, 4 * u, 215, 325, OutfitWarmEdgeColor);
        // The tail hangs from the right side with three fringes.
        canvas.SaveState();
        canvas.Translate(cx + r * 0.42f, cy + r * 0.72f);
        canvas.Rotate(-12);
        canvas.FillColor = OutfitWarmColor;
        canvas.FillRoundedRectangle(-9 * u, 0, 18 * u, r * 0.5f, 6 * u);
        canvas.StrokeColor = OutfitWarmEdgeColor;
        canvas.StrokeSize = 2.5f * u;
        for (var i = -1; i <= 1; i++)
        {
            canvas.DrawLine(i * 5 * u, r * 0.5f, i * 5 * u, r * 0.5f + 9 * u);
        }

        canvas.RestoreState();
    }

    private void DrawGlasses(ICanvas canvas, float cx, float cy, float u)
    {
        var eyeY = cy - 8 * u;
        var radius = 27 * u;
        canvas.StrokeColor = OutfitInkColor;
        canvas.StrokeSize = 4 * u;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawCircle(cx - 24 * u, eyeY, radius);
        canvas.DrawCircle(cx + 24 * u, eyeY, radius);
        canvas.DrawLine(cx - 24 * u + radius, eyeY - 4 * u, cx + 24 * u - radius, eyeY - 4 * u);
        canvas.DrawLine(cx - 24 * u - radius, eyeY - 4 * u, cx - 24 * u - radius - 14 * u, eyeY - 8 * u);
        canvas.DrawLine(cx + 24 * u + radius, eyeY - 4 * u, cx + 24 * u + radius + 14 * u, eyeY - 8 * u);
    }

    private void DrawStrawHat(ICanvas canvas, float cx, float cy, float r, float u)
    {
        canvas.SaveState();
        canvas.Translate(cx + r * 0.1f, cy - r * 0.82f);
        canvas.Rotate(8);
        canvas.FillColor = OutfitSunColor;
        canvas.FillRoundedRectangle(-r * 0.5f, -r * 0.5f, r, r * 0.52f, 14 * u); // crown
        canvas.FillEllipse(-r * 1.15f, -r * 0.12f, r * 2.3f, r * 0.3f); // brim
        canvas.StrokeColor = OutfitSunEdgeColor;
        canvas.StrokeSize = 2.5f * u;
        canvas.DrawEllipse(-r * 1.15f, -r * 0.12f, r * 2.3f, r * 0.3f);
        canvas.FillColor = OutfitWarmColor;
        canvas.FillRoundedRectangle(-r * 0.5f, -r * 0.16f, r, 9 * u, 4 * u); // band
        canvas.RestoreState();
    }

    private void DrawStraps(ICanvas canvas, float cx, float cy, float r, float u)
    {
        canvas.StrokeColor = OutfitCoolEdgeColor;
        canvas.StrokeSize = 6 * u;
        canvas.StrokeLineCap = LineCap.Round;
        foreach (var sign in (ReadOnlySpan<int>)[-1, 1])
        {
            var path = new PathF();
            path.MoveTo(cx + sign * 44 * u, cy - r * 0.72f);
            path.QuadTo(cx + sign * 66 * u, cy - r * 0.1f, cx + sign * 52 * u, cy + r * 0.55f);
            canvas.DrawPath(path);
        }
    }

    private void DrawRaincoat(ICanvas canvas, float cx, float cy, float r, float u)
    {
        // Hood around the upper head, over the hooks, with a rim and two toggles at the chin.
        Band(canvas, cx, cy, r * 1.02f, 24 * u, 15, 165, AccentColor);
        Band(canvas, cx, cy, r * 0.9f, 3 * u, 20, 160, OutfitSunEdgeColor);
        Band(canvas, cx, cy, r * 0.92f, 12 * u, 215, 325, AccentColor);
        canvas.FillColor = OutfitInkColor;
        canvas.FillCircle(cx - 10 * u, cy + r * 0.86f, 3.5f * u);
        canvas.FillCircle(cx + 10 * u, cy + r * 0.86f, 3.5f * u);
    }

    private void DrawWreath(ICanvas canvas, float cx, float cy, float r, float u)
    {
        var radius = r * 1.0f;
        for (var i = 0; i < 9; i++)
        {
            var angle = 200 + i * 17.5;
            var a = angle * Math.PI / 180;
            var x = cx + (float)(Math.Cos(a) * radius);
            var y = cy + (float)(Math.Sin(a) * radius);
            if (i % 2 == 0)
            {
                canvas.SaveState();
                canvas.Translate(x, y);
                canvas.Rotate((float)angle + 90);
                var leaf = new PathF();
                leaf.MoveTo(0, -11 * u);
                leaf.QuadTo(7 * u, 0, 0, 11 * u);
                leaf.QuadTo(-7 * u, 0, 0, -11 * u);
                leaf.Close();
                canvas.FillColor = LeafColor;
                canvas.FillPath(leaf);
                canvas.RestoreState();
            }
            else
            {
                canvas.FillColor = i % 4 == 1 ? OutfitWarmColor : AccentColor;
                canvas.FillCircle(x, y, 6 * u);
                canvas.FillColor = OutfitLightColor;
                canvas.FillCircle(x, y, 2 * u);
            }
        }
    }

    private void DrawSweater(ICanvas canvas, float cx, float cy, float r, float u)
    {
        Band(canvas, cx, cy, r * 0.9f, 22 * u, 200, 340, OutfitWarmEdgeColor);
        // Nordic zigzag along the band.
        canvas.StrokeColor = OutfitLightColor;
        canvas.StrokeSize = 2.5f * u;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeLineJoin = LineJoin.Round;
        var path = new PathF();
        var steps = 12;
        for (var i = 0; i <= steps; i++)
        {
            var angle = (210 + i * (120.0 / steps)) * Math.PI / 180;
            var radius = r * 0.9f + (i % 2 == 0 ? 5 * u : -5 * u);
            var x = cx + (float)(Math.Cos(angle) * radius);
            var y = cy - (float)(Math.Sin(angle) * radius);
            if (i == 0)
            {
                path.MoveTo(x, y);
            }
            else
            {
                path.LineTo(x, y);
            }
        }

        canvas.DrawPath(path);
    }

    private void DrawBandana(ICanvas canvas, float cx, float cy, float r, float u)
    {
        Band(canvas, cx, cy, r * 0.74f, 14 * u, 25, 155, OutfitWarmColor);
        canvas.FillColor = OutfitLightColor;
        for (var i = 0; i < 5; i++)
        {
            var angle = (35 + i * 27.5) * Math.PI / 180;
            canvas.FillCircle(cx + (float)(Math.Cos(angle) * r * 0.74f), cy - (float)(Math.Sin(angle) * r * 0.74f), 2.5f * u);
        }

        // The knot at the right temple.
        canvas.SaveState();
        canvas.Translate(cx + r * 0.72f, cy - r * 0.38f);
        canvas.FillColor = OutfitWarmColor;
        var tail = new PathF();
        tail.MoveTo(0, 0);
        tail.LineTo(16 * u, -6 * u);
        tail.LineTo(14 * u, 8 * u);
        tail.Close();
        canvas.FillPath(tail);
        var tail2 = new PathF();
        tail2.MoveTo(0, 0);
        tail2.LineTo(10 * u, 14 * u);
        tail2.LineTo(-2 * u, 12 * u);
        tail2.Close();
        canvas.FillPath(tail2);
        canvas.RestoreState();
    }

    private void DrawVyshyvanka(ICanvas canvas, float cx, float cy, float r, float u)
    {
        Band(canvas, cx, cy, r * 0.88f, 26 * u, 200, 340, OutfitLightColor);
        // Embroidery: alternating red and black diamonds along the collar, a slit in the middle.
        for (var i = 0; i < 9; i++)
        {
            var angle = (212 + i * 14.5) * Math.PI / 180;
            var x = cx + (float)(Math.Cos(angle) * r * 0.88f);
            var y = cy - (float)(Math.Sin(angle) * r * 0.88f);
            canvas.SaveState();
            canvas.Translate(x, y);
            canvas.Rotate(45);
            canvas.FillColor = i % 2 == 0 ? OutfitWarmEdgeColor : OutfitInkColor;
            var size = (i % 2 == 0 ? 5f : 3.5f) * u;
            canvas.FillRectangle(-size / 2, -size / 2, size, size);
            canvas.RestoreState();
        }

        canvas.StrokeColor = OutfitWarmEdgeColor;
        canvas.StrokeSize = 2 * u;
        canvas.DrawLine(cx, cy + r * 0.78f, cx, cy + r * 1.0f);
    }

    private void DrawSpaceHelmet(ICanvas canvas, float cx, float cy, float r, float u)
    {
        var radius = r * 1.3f;
        var centerY = cy - r * 0.08f;
        canvas.SaveState();
        canvas.Alpha = 0.22f;
        canvas.FillColor = OutfitCoolColor;
        canvas.FillCircle(cx, centerY, radius);
        canvas.RestoreState();
        canvas.StrokeColor = OutfitLightColor;
        canvas.StrokeSize = 4 * u;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawCircle(cx, centerY, radius);
        canvas.SaveState();
        canvas.Alpha = 0.7f;
        canvas.StrokeSize = 5 * u;
        canvas.DrawArc(cx - radius * 0.8f, centerY - radius * 0.8f, radius * 1.6f, radius * 1.6f, 110, 150, clockwise: false, closed: false);
        canvas.RestoreState();
        canvas.FillColor = OutfitCoolEdgeColor;
        canvas.FillRoundedRectangle(cx - r * 0.7f, centerY + radius - 10 * u, r * 1.4f, 18 * u, 8 * u);
    }

    private void DrawCrown(ICanvas canvas, float cx, float cy, float r, float u)
    {
        canvas.SaveState();
        canvas.Translate(cx + r * 0.38f, cy - r * 0.9f);
        canvas.Rotate(16);
        var crown = new PathF();
        crown.MoveTo(-30 * u, 8 * u);
        crown.LineTo(-30 * u, -14 * u);
        crown.LineTo(-15 * u, -2 * u);
        crown.LineTo(0, -24 * u);
        crown.LineTo(15 * u, -2 * u);
        crown.LineTo(30 * u, -14 * u);
        crown.LineTo(30 * u, 8 * u);
        crown.Close();
        canvas.FillColor = AccentColor;
        canvas.FillPath(crown);
        canvas.StrokeColor = OutfitSunEdgeColor;
        canvas.StrokeSize = 2 * u;
        canvas.StrokeLineJoin = LineJoin.Round;
        canvas.DrawPath(crown);
        canvas.FillColor = BloomEdgeColor;
        canvas.FillCircle(0, -24 * u, 4 * u);
        canvas.FillColor = OutfitCoolColor;
        canvas.FillCircle(-30 * u, -14 * u, 3 * u);
        canvas.FillCircle(30 * u, -14 * u, 3 * u);
        canvas.RestoreState();
    }
}
