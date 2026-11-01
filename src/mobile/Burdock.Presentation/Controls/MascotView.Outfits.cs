namespace Burdock.Presentation.Controls;

/// <summary>
/// Cosmetics of the shop on the procedural mascot (spec 07 §3.2.3, spec 02 §3.10 «Вбрання як косметика»). Each outfit is a small set of named
/// parts drawn inside the body transform (the backpack body behind the burr, everything else over the face); the avatar frame is a ring outside
/// it. Rendering language matches the body: flat shapes with soft volume (one darker shade at the underside, one light accent at the upper
/// left), rounded joins, no outlines, two chromatic ramps per accessory plus neutrals. Angles follow MAUI's DrawArc convention
/// (counter-clockwise, 90 = top): bands on the lower body span 200..340, bands on the forehead 20..160, and decorations along a band use
/// y = cy − sin(angle).
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

        // A soft light on the upper left of every ring, like on the body.
        canvas.SaveState();
        canvas.Alpha = 0.45f;
        canvas.StrokeColor = OutfitLightColor;
        canvas.StrokeSize = 2 * u;
        canvas.DrawArc(cx - radius, cy - radius, 2 * radius, 2 * radius, 110, 160, clockwise: false, closed: false);
        canvas.RestoreState();

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
                for (var i = 0; i < 12; i++)
                {
                    var a = i * Math.PI / 6;
                    canvas.SaveState();
                    canvas.Translate(cx + (float)(Math.Cos(a) * radius), cy + (float)(Math.Sin(a) * radius));
                    canvas.Rotate((float)(a * 180 / Math.PI) + 90);
                    Leaf(canvas, 7 * u, 5 * u, LeafColor, LeafEdgeColor, u);
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

        // backpack.body peeks out on both sides of the burr (the straps on the front lead to it), backpack.flap darker on top with a buckle,
        // backpack.bottle in the right side pocket.
        var width = r * 2.2f;
        var height = r * 1.2f;
        var left = cx - width / 2;
        var top = cy - r * 0.78f;
        var corner = r * 0.3f;
        canvas.FillColor = OutfitCoolColor;
        canvas.FillRoundedRectangle(left, top, width, height, corner);
        canvas.FillColor = OutfitCoolEdgeColor;
        canvas.FillRoundedRectangle(left, top, width, height * 0.42f, corner);
        canvas.SaveState();
        canvas.Alpha = 0.35f;
        canvas.FillRoundedRectangle(left, top + height * 0.82f, width, height * 0.18f, corner * 0.6f);
        canvas.RestoreState();
        canvas.FillColor = OutfitLightColor;
        canvas.FillRoundedRectangle(left + 8 * u, top + height * 0.3f, 10 * u, 6 * u, 2 * u);
        canvas.FillRoundedRectangle(left + width - 18 * u, top + height * 0.3f, 10 * u, 6 * u, 2 * u);

        var bx = cx + r * 1.06f;
        var by = cy - r * 0.22f;
        canvas.FillColor = OutfitLightColor;
        canvas.FillRoundedRectangle(bx, by, 12 * u, 36 * u, 4 * u);
        canvas.FillColor = OutfitWarmColor;
        canvas.FillRoundedRectangle(bx + 2.5f * u, by - 5 * u, 7 * u, 7 * u, 2 * u);
        canvas.FillColor = OutfitCoolEdgeColor;
        canvas.FillRoundedRectangle(bx - 2 * u, by + 16 * u, 16 * u, 9 * u, 2 * u);
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

    /// <summary>A band around the body as a thick stroke (DrawArc angles: counter-clockwise, 90 = top).</summary>
    private static void Band(ICanvas canvas, float cx, float cy, float radius, float thickness, float startAngle, float endAngle, Color color)
    {
        canvas.StrokeColor = color;
        canvas.StrokeSize = thickness;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawArc(cx - radius, cy - radius, 2 * radius, 2 * radius, startAngle, endAngle, clockwise: false, closed: false);
    }

    /// <summary>Point on a band in DrawArc's angle convention.</summary>
    private static (float X, float Y) OnBand(float cx, float cy, float radius, double angleDegrees)
    {
        var a = angleDegrees * Math.PI / 180;
        return (cx + (float)(Math.Cos(a) * radius), cy - (float)(Math.Sin(a) * radius));
    }

    /// <summary>A pointed leaf along the local y axis with a midrib.</summary>
    private static void Leaf(ICanvas canvas, float length, float width, Color fill, Color vein, float u)
    {
        var leaf = new PathF();
        leaf.MoveTo(0, -length);
        leaf.QuadTo(width, 0, 0, length);
        leaf.QuadTo(-width, 0, 0, -length);
        leaf.Close();
        canvas.FillColor = fill;
        canvas.FillPath(leaf);
        canvas.StrokeColor = vein;
        canvas.StrokeSize = 1.5f * u;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawLine(0, -length * 0.7f, 0, length * 0.7f);
    }

    private void DrawScarf(ICanvas canvas, float cx, float cy, float r, float u)
    {
        // scarf.band with an underside shade, knit ticks, scarf.fold where it wraps, scarf.tail with fringe.
        Band(canvas, cx, cy, r * 0.9f + 3 * u, 18 * u, 200, 340, OutfitWarmEdgeColor);
        Band(canvas, cx, cy, r * 0.9f - 2 * u, 16 * u, 202, 338, OutfitWarmColor);
        canvas.StrokeColor = OutfitWarmEdgeColor;
        canvas.StrokeSize = 2 * u;
        canvas.StrokeLineCap = LineCap.Round;
        for (var i = 0; i < 6; i++)
        {
            var (x, y) = OnBand(cx, cy, r * 0.9f - 2 * u, 215 + i * 22);
            canvas.DrawLine(x - 3 * u, y - 5 * u, x + 3 * u, y + 5 * u);
        }

        canvas.SaveState();
        canvas.Translate(cx + r * 0.6f, cy + r * 0.6f);
        canvas.Rotate(48);
        canvas.FillColor = OutfitWarmColor;
        canvas.FillRoundedRectangle(-11 * u, -17 * u, 22 * u, 34 * u, 7 * u);
        canvas.StrokeColor = OutfitWarmEdgeColor;
        canvas.StrokeSize = 2 * u;
        canvas.DrawLine(-11 * u, 10 * u, 11 * u, 10 * u);
        canvas.RestoreState();

        canvas.SaveState();
        canvas.Translate(cx + r * 0.5f, cy + r * 0.8f);
        canvas.Rotate(-14);
        canvas.FillColor = OutfitWarmColor;
        canvas.FillRoundedRectangle(-10 * u, 0, 20 * u, r * 0.5f, 7 * u);
        canvas.StrokeColor = OutfitWarmEdgeColor;
        canvas.StrokeSize = 2 * u;
        for (var i = 0; i < 3; i++)
        {
            var y = r * 0.12f + i * r * 0.11f;
            canvas.DrawLine(-6 * u, y, 6 * u, y + 5 * u);
        }

        canvas.StrokeSize = 2.5f * u;
        for (var i = -1; i <= 1; i++)
        {
            canvas.DrawLine(i * 6 * u, r * 0.5f - 2 * u, i * 6 * u, r * 0.5f + 10 * u);
        }

        canvas.RestoreState();
    }

    private void DrawGlasses(ICanvas canvas, float cx, float cy, float u)
    {
        // glasses.lens ×2 with a cool tint and a glint, glasses.bridge, glasses.temples.
        var eyeY = cy - 8 * u;
        var radius = 27 * u;
        foreach (var sign in (ReadOnlySpan<int>)[-1, 1])
        {
            var x = cx + sign * 24 * u;
            canvas.SaveState();
            canvas.Alpha = 0.18f;
            canvas.FillColor = OutfitCoolColor;
            canvas.FillCircle(x, eyeY, radius);
            canvas.RestoreState();
            canvas.StrokeColor = OutfitInkColor;
            canvas.StrokeSize = 4 * u;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.DrawCircle(x, eyeY, radius);
            canvas.SaveState();
            canvas.Alpha = 0.7f;
            canvas.StrokeColor = OutfitLightColor;
            canvas.StrokeSize = 3 * u;
            canvas.DrawArc(x - radius * 0.72f, eyeY - radius * 0.72f, radius * 1.44f, radius * 1.44f, 115, 150, clockwise: false, closed: false);
            canvas.RestoreState();
        }

        canvas.StrokeColor = OutfitInkColor;
        canvas.StrokeSize = 4 * u;
        var bridge = new PathF();
        bridge.MoveTo(cx - 24 * u + radius, eyeY - 2 * u);
        bridge.QuadTo(cx, eyeY - 12 * u, cx + 24 * u - radius, eyeY - 2 * u);
        canvas.DrawPath(bridge);
        canvas.DrawLine(cx - 24 * u - radius, eyeY - 3 * u, cx - 24 * u - radius - 15 * u, eyeY - 9 * u);
        canvas.DrawLine(cx + 24 * u + radius, eyeY - 3 * u, cx + 24 * u + radius + 15 * u, eyeY - 9 * u);
    }

    private void DrawStrawHat(ICanvas canvas, float cx, float cy, float r, float u)
    {
        // strawhat.dome, strawhat.brim with an underside shadow, weave lines, strawhat.ribbon and bow.
        canvas.SaveState();
        canvas.Translate(cx + r * 0.08f, cy - r * 0.86f);
        canvas.Rotate(8);
        canvas.FillColor = OutfitSunColor;
        canvas.FillEllipse(-r * 0.5f, -r * 0.72f, r, r * 1.0f);
        canvas.StrokeColor = OutfitSunEdgeColor;
        canvas.StrokeSize = 1.8f * u;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.SaveState();
        canvas.Alpha = 0.55f;
        for (var i = 0; i < 3; i++)
        {
            var y = -r * 0.55f + i * r * 0.11f;
            var halfWidth = r * 0.5f * (float)Math.Sqrt(Math.Max(0, 1 - Math.Pow((y + r * 0.22f) / (r * 0.5f), 2))) * 0.92f;
            canvas.DrawLine(-halfWidth, y, halfWidth, y);
        }

        canvas.RestoreState();

        canvas.FillColor = OutfitWarmColor;
        canvas.FillRoundedRectangle(-r * 0.49f, -r * 0.14f, r * 0.98f, 10 * u, 4 * u);
        canvas.FillColor = OutfitSunColor;
        canvas.FillEllipse(-r * 1.2f, -r * 0.1f, r * 2.4f, r * 0.32f);
        canvas.SaveState();
        canvas.Alpha = 0.5f;
        canvas.StrokeColor = OutfitSunEdgeColor;
        canvas.StrokeSize = 3 * u;
        canvas.DrawArc(-r * 1.2f, -r * 0.1f, r * 2.4f, r * 0.32f, 190, 350, clockwise: false, closed: false);
        canvas.RestoreState();

        // The bow: two loops and a knot at the right of the ribbon.
        canvas.FillColor = OutfitWarmColor;
        var bowL = new PathF();
        bowL.MoveTo(r * 0.42f, -r * 0.09f);
        bowL.LineTo(r * 0.3f, -r * 0.2f);
        bowL.LineTo(r * 0.3f, r * 0.02f);
        bowL.Close();
        canvas.FillPath(bowL);
        var bowR = new PathF();
        bowR.MoveTo(r * 0.42f, -r * 0.09f);
        bowR.LineTo(r * 0.56f, -r * 0.2f);
        bowR.LineTo(r * 0.56f, r * 0.02f);
        bowR.Close();
        canvas.FillPath(bowR);
        canvas.FillColor = OutfitWarmEdgeColor;
        canvas.FillCircle(r * 0.42f, -r * 0.09f, 3.5f * u);
        canvas.RestoreState();
    }

    private void DrawStraps(ICanvas canvas, float cx, float cy, float r, float u)
    {
        // backpack.straps hug the sides of the body from the shoulders down to the hips (never across the face): they come out from behind the
        // silhouette at the top and follow it at a constant inset; backpack.chestStrap below the mouth joins them with a light clip.
        void Strap()
        {
            var path = new PathF();
            for (var angle = 114.0; angle <= 232.0; angle += 3)
            {
                var blend = Math.Clamp((angle - 114) / 34, 0, 1);
                var ease = blend * blend * (3 - 2 * blend);
                var radius = (float)(r * (1.04 - 0.2 * ease));
                var (x, y) = OnBand(cx, cy, radius, angle);
                if (angle == 114.0)
                {
                    path.MoveTo(x, y);
                }
                else
                {
                    path.LineTo(x, y);
                }
            }

            canvas.StrokeColor = OutfitCoolEdgeColor;
            canvas.StrokeSize = 8 * u;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.StrokeLineJoin = LineJoin.Round;
            canvas.DrawPath(path);
            canvas.SaveState();
            canvas.Alpha = 0.7f;
            canvas.StrokeColor = OutfitCoolColor;
            canvas.StrokeSize = 2.5f * u;
            canvas.DrawPath(path);
            canvas.RestoreState();
        }

        Strap();
        Mirrored(canvas, cx, Strap);

        var (hx, hy) = OnBand(cx, cy, r * 0.84f, 232);
        canvas.StrokeColor = OutfitCoolEdgeColor;
        canvas.StrokeSize = 5 * u;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawLine(hx, hy, 2 * cx - hx, hy);
        canvas.FillColor = OutfitLightColor;
        canvas.FillRoundedRectangle(cx - 7 * u, hy - 4 * u, 14 * u, 8 * u, 2 * u);
        canvas.FillColor = OutfitCoolEdgeColor;
        canvas.FillRectangle(cx - 1 * u, hy - 4 * u, 2 * u, 8 * u);
    }

    private void DrawRaincoat(ICanvas canvas, float cx, float cy, float r, float u)
    {
        // raincoat.shell: one yellow piece around the whole burr with a face opening (hood and cape together), raincoat.rim: the darker seam of
        // the opening, raincoat.highlight on the upper left of the hood, raincoat.hem shade, raincoat.zip down the front with two toggles,
        // raincoat.drawstrings at the chin.
        var outer = r * 1.14f;
        var opening = new RectF(cx - 70 * u, cy - 44 * u, 140 * u, 98 * u);
        var shell = new PathF();
        shell.AppendCircle(cx, cy, outer);
        shell.AppendEllipse(opening);
        canvas.FillColor = AccentColor;
        canvas.FillPath(shell, WindingMode.EvenOdd);

        canvas.SaveState();
        canvas.Alpha = 0.3f;
        Band(canvas, cx, cy, outer - 6 * u, 10 * u, 235, 305, OutfitSunEdgeColor);
        canvas.RestoreState();
        canvas.SaveState();
        canvas.Alpha = 0.45f;
        Band(canvas, cx, cy, outer - 5 * u, 3 * u, 70, 135, OutfitLightColor);
        canvas.RestoreState();

        canvas.StrokeColor = OutfitSunEdgeColor;
        canvas.StrokeSize = 3 * u;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawEllipse(opening.Left - 1.5f * u, opening.Top - 1.5f * u, opening.Width + 3 * u, opening.Height + 3 * u);

        var zipTop = opening.Bottom + 1 * u;
        var zipBottom = cy + outer - 5 * u;
        canvas.StrokeSize = 2.5f * u;
        canvas.DrawLine(cx, zipTop, cx, zipBottom);
        canvas.SaveState();
        canvas.Alpha = 0.5f;
        canvas.StrokeColor = OutfitLightColor;
        canvas.StrokeSize = 1.2f * u;
        canvas.DrawLine(cx + 3 * u, zipTop + 2 * u, cx + 3 * u, zipBottom - 2 * u);
        canvas.RestoreState();

        foreach (var sign in (ReadOnlySpan<int>)[-1, 1])
        {
            var ty = cy + 70 * u;
            canvas.StrokeColor = OutfitSunEdgeColor;
            canvas.StrokeSize = 2 * u;
            canvas.DrawLine(cx, ty, cx + sign * 16 * u, ty);
            canvas.FillColor = OutfitInkColor;
            canvas.FillRoundedRectangle(cx + sign * 16 * u - 3 * u, ty - 6 * u, 6 * u, 12 * u, 3 * u);

            var (dx, dy) = (cx + sign * 36 * u, opening.Bottom - 10 * u);
            canvas.StrokeColor = OutfitSunEdgeColor;
            canvas.StrokeSize = 2 * u;
            canvas.DrawLine(dx, dy, dx + sign * 3 * u, dy + 18 * u);
            canvas.FillColor = OutfitInkColor;
            canvas.FillRoundedRectangle(dx + sign * 3 * u - 2.5f * u, dy + 16 * u, 5 * u, 9 * u, 2.5f * u);
        }
    }

    private void DrawWreath(ICanvas canvas, float cx, float cy, float r, float u)
    {
        // wreath.leaves along the crown, wreath.flowers: a poppy at the top, two cornflowers, two daisies, wreath.berries between them,
        // wreath.ribbons: three colours hanging at each temple.
        var radius = r * 1.0f;
        for (var i = 0; i < 10; i++)
        {
            var angle = 15 + i * 16.7;
            var (x, y) = OnBand(cx, cy, radius, angle);
            canvas.SaveState();
            canvas.Translate(x, y);
            canvas.Rotate((float)(90 - angle) + (i % 2 == 0 ? 38 : -38));
            Leaf(canvas, 12 * u, 6.5f * u, LeafColor, LeafEdgeColor, u);
            canvas.RestoreState();
        }

        void Ribbons()
        {
            var (ax, ay) = OnBand(cx, cy, radius, 14);
            canvas.StrokeSize = 4 * u;
            canvas.StrokeLineCap = LineCap.Round;
            var ribbons = (ReadOnlySpan<(Color Color, float Length, float Sway)>)[(OutfitWarmColor, 54 * u, 16 * u), (OutfitCoolColor, 46 * u, 6 * u), (AccentColor, 38 * u, 24 * u)];
            foreach (var (color, length, sway) in ribbons)
            {
                var path = new PathF();
                path.MoveTo(ax, ay);
                path.CurveTo(ax + sway, ay + length * 0.35f, ax - sway * 0.3f, ay + length * 0.7f, ax + sway * 0.5f, ay + length);
                canvas.StrokeColor = color;
                canvas.DrawPath(path);
            }
        }

        Ribbons();
        Mirrored(canvas, cx, Ribbons);

        foreach (var angle in (ReadOnlySpan<double>)[36, 144])
        {
            var (x, y) = OnBand(cx, cy, radius, angle);
            canvas.FillColor = OutfitLightColor;
            for (var p = 0; p < 7; p++)
            {
                var pa = p * 2 * Math.PI / 7;
                canvas.FillCircle(x + (float)(Math.Cos(pa) * 5.5 * u), y + (float)(Math.Sin(pa) * 5.5 * u), 3.2f * u);
            }

            canvas.FillColor = AccentColor;
            canvas.FillCircle(x, y, 3.6f * u);
        }

        foreach (var angle in (ReadOnlySpan<double>)[62, 118])
        {
            var (x, y) = OnBand(cx, cy, radius + 2 * u, angle);
            canvas.FillColor = OutfitCoolColor;
            for (var p = 0; p < 6; p++)
            {
                var pa = p * Math.PI / 3;
                canvas.SaveState();
                canvas.Translate(x + (float)(Math.Cos(pa) * 5 * u), y + (float)(Math.Sin(pa) * 5 * u));
                canvas.Rotate((float)(pa * 180 / Math.PI));
                canvas.FillEllipse(-3.5f * u, -2.2f * u, 7 * u, 4.4f * u);
                canvas.RestoreState();
            }

            canvas.FillColor = OutfitCoolEdgeColor;
            canvas.FillCircle(x, y, 2.6f * u);
        }

        var (px, py) = OnBand(cx, cy, radius + 3 * u, 90);
        canvas.FillColor = OutfitWarmColor;
        for (var p = 0; p < 5; p++)
        {
            var pa = -Math.PI / 2 + p * 2 * Math.PI / 5;
            canvas.FillCircle(px + (float)(Math.Cos(pa) * 7.5 * u), py + (float)(Math.Sin(pa) * 7.5 * u), 6.5f * u);
        }

        canvas.SaveState();
        canvas.Alpha = 0.35f;
        canvas.FillColor = OutfitWarmEdgeColor;
        canvas.FillCircle(px, py + 2 * u, 7 * u);
        canvas.RestoreState();
        canvas.FillColor = OutfitInkColor;
        canvas.FillCircle(px, py, 3.6f * u);

        var berry = 0;
        foreach (var angle in (ReadOnlySpan<double>)[24, 49, 76, 104, 131, 156])
        {
            var (bx, by) = OnBand(cx, cy, radius + 6 * u, angle);
            canvas.FillColor = berry++ % 2 == 0 ? OutfitWarmColor : AccentColor;
            canvas.FillCircle(bx, by, 2.8f * u);
        }
    }

    private void DrawSweater(ICanvas canvas, float cx, float cy, float r, float u)
    {
        // sweater.body: the lower body in coral under a shallow neckline, sweater.collar: a ribbed band along it, sweater.pattern: a Nordic row
        // of three white deer between a zigzag and a line of dots.
        var radius = r * 1.03f;
        canvas.FillColor = OutfitWarmColor;
        canvas.FillPath(Garment(cx, cy, radius, 30 * u, 52 * u));
        canvas.SaveState();
        canvas.Alpha = 0.35f;
        Band(canvas, cx, cy, radius - 4 * u, 8 * u, 240, 300, OutfitWarmEdgeColor);
        canvas.RestoreState();
        Neckline(canvas, cx, cy, radius, 30 * u, 52 * u, 7 * u, OutfitWarmEdgeColor);
        canvas.SaveState();
        canvas.Alpha = 0.6f;
        Neckline(canvas, cx, cy, radius, 30 * u, 52 * u, 1.5f * u, OutfitWarmColor);
        canvas.RestoreState();

        var zig = new PathF();
        for (var i = 0; i <= 14; i++)
        {
            var x = cx - 49 * u + i * 7 * u;
            var y = cy + 60 * u + (i % 2 == 0 ? -2 * u : 2 * u);
            if (i == 0)
            {
                zig.MoveTo(x, y);
            }
            else
            {
                zig.LineTo(x, y);
            }
        }

        canvas.StrokeColor = OutfitLightColor;
        canvas.StrokeSize = 1.6f * u;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeLineJoin = LineJoin.Round;
        canvas.DrawPath(zig);

        foreach (var dx in (ReadOnlySpan<float>)[-27, 0, 27])
        {
            Deer(canvas, cx + dx * u, cy + 70 * u, u, OutfitLightColor);
        }

        canvas.FillColor = OutfitLightColor;
        for (var i = -2; i <= 2; i++)
        {
            canvas.FillCircle(cx + i * 10 * u, cy + 80 * u, 1.5f * u);
        }
    }

    private void DrawBandana(ICanvas canvas, float cx, float cy, float r, float u)
    {
        // bandana.band across the forehead with a fold line and dots, bandana.knot with two tails at the right temple.
        Band(canvas, cx, cy, r * 0.74f, 15 * u, 25, 155, OutfitWarmColor);
        canvas.SaveState();
        canvas.Alpha = 0.7f;
        Band(canvas, cx, cy, r * 0.74f - 5 * u, 1.8f * u, 30, 150, OutfitWarmEdgeColor);
        canvas.RestoreState();
        canvas.FillColor = OutfitLightColor;
        for (var i = 0; i < 6; i++)
        {
            var (x, y) = OnBand(cx, cy, r * 0.74f + 1 * u, 38 + i * 21);
            canvas.FillCircle(x, y, 2.2f * u);
        }

        var (kx, ky) = OnBand(cx, cy, r * 0.74f, 22);
        canvas.SaveState();
        canvas.Translate(kx, ky);
        canvas.FillColor = OutfitWarmColor;
        canvas.Rotate(15);
        canvas.FillRoundedRectangle(-3.5f * u, 0, 7 * u, 22 * u, 3.5f * u);
        canvas.Rotate(40);
        canvas.FillRoundedRectangle(-3.5f * u, 0, 7 * u, 17 * u, 3.5f * u);
        canvas.RestoreState();
        canvas.FillColor = OutfitWarmEdgeColor;
        canvas.FillCircle(kx, ky, 5 * u);
    }

    private void DrawVyshyvanka(ICanvas canvas, float cx, float cy, float r, float u)
    {
        // vyshyvanka.shirt: the lower body in white under a shallow neckline, vyshyvanka.collar: a thin red band, vyshyvanka.placket: the central
        // strip between two red lines with red diamonds and black cross-stitches, vyshyvanka.sleeves: a red zigzag at each side,
        // vyshyvanka.bow: a small red bow at the collar.
        var radius = r * 1.03f;
        canvas.FillColor = OutfitLightColor;
        canvas.FillPath(Garment(cx, cy, radius, 30 * u, 52 * u));
        canvas.SaveState();
        canvas.Alpha = 0.12f;
        Band(canvas, cx, cy, radius - 4 * u, 8 * u, 240, 300, OutfitInkColor);
        canvas.RestoreState();
        Neckline(canvas, cx, cy, radius, 30 * u, 52 * u, 4 * u, OutfitWarmEdgeColor);

        canvas.StrokeColor = OutfitWarmEdgeColor;
        canvas.StrokeSize = 1.6f * u;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawLine(cx - 11 * u, cy + 57 * u, cx - 11 * u, cy + 84 * u);
        canvas.DrawLine(cx + 11 * u, cy + 57 * u, cx + 11 * u, cy + 84 * u);
        foreach (var (y, diamond) in (ReadOnlySpan<(float Y, bool Diamond)>)[(62, true), (70, false), (78, true)])
        {
            var py = cy + y * u;
            if (diamond)
            {
                canvas.SaveState();
                canvas.Translate(cx, py);
                canvas.Rotate(45);
                canvas.FillColor = OutfitWarmEdgeColor;
                canvas.FillRectangle(-3.2f * u, -3.2f * u, 6.4f * u, 6.4f * u);
                canvas.RestoreState();
                canvas.FillColor = OutfitLightColor;
                canvas.FillCircle(cx, py, 1.1f * u);
            }
            else
            {
                canvas.StrokeColor = OutfitInkColor;
                canvas.StrokeSize = 1.6f * u;
                canvas.DrawLine(cx - 2.8f * u, py - 2.8f * u, cx + 2.8f * u, py + 2.8f * u);
                canvas.DrawLine(cx - 2.8f * u, py + 2.8f * u, cx + 2.8f * u, py - 2.8f * u);
            }
        }

        void Sleeve()
        {
            var zig = new PathF();
            for (var i = 0; i <= 5; i++)
            {
                var x = cx + 45 * u + (i % 2 == 0 ? -2.5f * u : 2.5f * u);
                var y = cy + 50 * u + i * 4.5f * u;
                if (i == 0)
                {
                    zig.MoveTo(x, y);
                }
                else
                {
                    zig.LineTo(x, y);
                }
            }

            canvas.StrokeColor = OutfitWarmEdgeColor;
            canvas.StrokeSize = 1.5f * u;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.StrokeLineJoin = LineJoin.Round;
            canvas.DrawPath(zig);
            canvas.FillColor = OutfitInkColor;
            canvas.FillCircle(cx + 45 * u, cy + 47 * u, 1.3f * u);
            canvas.FillCircle(cx + 45 * u, cy + 75.5f * u, 1.3f * u);
        }

        Sleeve();
        Mirrored(canvas, cx, Sleeve);

        var bow = new PathF();
        bow.MoveTo(cx, cy + 53 * u);
        bow.LineTo(cx - 7 * u, cy + 49.5f * u);
        bow.LineTo(cx - 7 * u, cy + 56.5f * u);
        bow.Close();
        bow.MoveTo(cx, cy + 53 * u);
        bow.LineTo(cx + 7 * u, cy + 49.5f * u);
        bow.LineTo(cx + 7 * u, cy + 56.5f * u);
        bow.Close();
        canvas.FillColor = OutfitWarmColor;
        canvas.FillPath(bow);
        canvas.FillColor = OutfitWarmEdgeColor;
        canvas.FillCircle(cx, cy + 53 * u, 2 * u);
    }

    /// <summary>A garment on the lower body: the circle segment under a shallow U neckline that meets the silhouette at ±edgeY and dips to dipY.</summary>
    private static PathF Garment(float cx, float cy, float radius, float edgeY, float dipY)
    {
        var halfWidth = (float)Math.Sqrt(Math.Max(0, radius * radius - edgeY * edgeY));
        var edgeAngle = Math.Asin(edgeY / radius) * 180 / Math.PI;
        var path = new PathF();
        path.MoveTo(cx - halfWidth, cy + edgeY);
        path.QuadTo(cx, cy + 2 * dipY - edgeY, cx + halfWidth, cy + edgeY);
        for (var angle = -edgeAngle; angle >= -180 + edgeAngle; angle -= 4)
        {
            var (x, y) = OnBand(cx, cy, radius, angle);
            path.LineTo(x, y);
        }

        path.Close();
        return path;
    }

    /// <summary>The neckline curve of <see cref="Garment"/> as a stroke (collar, band or seam).</summary>
    private static void Neckline(ICanvas canvas, float cx, float cy, float radius, float edgeY, float dipY, float thickness, Color color)
    {
        var halfWidth = (float)Math.Sqrt(Math.Max(0, radius * radius - edgeY * edgeY));
        var path = new PathF();
        path.MoveTo(cx - halfWidth, cy + edgeY);
        path.QuadTo(cx, cy + 2 * dipY - edgeY, cx + halfWidth, cy + edgeY);
        canvas.StrokeColor = color;
        canvas.StrokeSize = thickness;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawPath(path);
    }

    /// <summary>A tiny deer silhouette facing right (body, head with antlers, four legs, a tail), centred on the body.</summary>
    private static void Deer(ICanvas canvas, float x, float y, float u, Color color)
    {
        canvas.FillColor = color;
        canvas.StrokeColor = color;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.FillRoundedRectangle(x - 5.5f * u, y - 2.5f * u, 10 * u, 5 * u, 2 * u);
        canvas.FillRoundedRectangle(x + 2.5f * u, y - 7 * u, 4 * u, 6.5f * u, 1.5f * u);
        canvas.FillCircle(x - 5.5f * u, y - 2 * u, 1.1f * u);
        canvas.StrokeSize = 1 * u;
        canvas.DrawLine(x + 3.5f * u, y - 7 * u, x + 1.5f * u, y - 10.5f * u);
        canvas.DrawLine(x + 5.5f * u, y - 7 * u, x + 7.5f * u, y - 10.5f * u);
        canvas.StrokeSize = 1.3f * u;
        foreach (var leg in (ReadOnlySpan<float>)[-4, -2, 1.5f, 3.5f])
        {
            canvas.DrawLine(x + leg * u, y + 2 * u, x + leg * u, y + 6.5f * u);
        }
    }

    /// <summary>Runs a drawing mirrored across the vertical axis through cx, for symmetric parts drawn once.</summary>
    private static void Mirrored(ICanvas canvas, float cx, Action draw)
    {
        canvas.SaveState();
        canvas.Translate(cx, 0);
        canvas.Scale(-1, 1);
        canvas.Translate(-cx, 0);
        draw();
        canvas.RestoreState();
    }

    private void DrawSpaceHelmet(ICanvas canvas, float cx, float cy, float r, float u)
    {
        // helmet.dome (tinted glass with a soft upper-left light), helmet.rim, helmet.glints, helmet.collar, helmet.antenna.
        var radius = r * 1.3f;
        var centerY = cy - r * 0.08f;
        canvas.SaveState();
        canvas.Alpha = 0.2f;
        canvas.FillColor = OutfitCoolColor;
        canvas.FillCircle(cx, centerY, radius);
        canvas.RestoreState();
        canvas.SaveState();
        canvas.Alpha = 0.22f;
        canvas.FillColor = OutfitLightColor;
        canvas.Translate(cx - radius * 0.35f, centerY - radius * 0.4f);
        canvas.Rotate(-35);
        canvas.FillEllipse(-radius * 0.34f, -radius * 0.16f, radius * 0.68f, radius * 0.32f);
        canvas.RestoreState();

        canvas.StrokeColor = OutfitCoolEdgeColor;
        canvas.StrokeSize = 6 * u;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawCircle(cx, centerY, radius);
        canvas.StrokeColor = OutfitLightColor;
        canvas.StrokeSize = 3 * u;
        canvas.DrawCircle(cx, centerY, radius);
        canvas.SaveState();
        canvas.Alpha = 0.8f;
        canvas.StrokeSize = 5 * u;
        canvas.DrawArc(cx - radius * 0.82f, centerY - radius * 0.82f, radius * 1.64f, radius * 1.64f, 112, 150, clockwise: false, closed: false);
        canvas.StrokeSize = 3 * u;
        canvas.DrawArc(cx - radius * 0.82f, centerY - radius * 0.82f, radius * 1.64f, radius * 1.64f, 158, 166, clockwise: false, closed: false);
        canvas.RestoreState();

        var collarTop = centerY + radius - 12 * u;
        canvas.FillColor = OutfitCoolEdgeColor;
        canvas.FillRoundedRectangle(cx - r * 0.74f, collarTop, r * 1.48f, 20 * u, 9 * u);
        canvas.FillColor = OutfitCoolColor;
        canvas.FillRoundedRectangle(cx - r * 0.6f, collarTop + 7 * u, r * 1.2f, 5 * u, 2.5f * u);
        canvas.FillColor = OutfitLightColor;
        canvas.FillCircle(cx + r * 0.62f, collarTop + 10 * u, 2.5f * u);

        var (ax, ay) = OnBand(cx, centerY, radius, 56);
        canvas.StrokeColor = OutfitInkColor;
        canvas.StrokeSize = 2.5f * u;
        canvas.DrawLine(ax, ay, ax + 8 * u, ay - 14 * u);
        canvas.FillColor = OutfitWarmColor;
        canvas.FillCircle(ax + 8 * u, ay - 14 * u, 4 * u);
    }

    private void DrawCrown(ICanvas canvas, float cx, float cy, float r, float u)
    {
        // crown.base band, crown.points with a shaded right side, crown.jewels with glints; tilted on the upper right like the party hat.
        canvas.SaveState();
        canvas.Translate(cx + r * 0.38f, cy - r * 0.98f);
        canvas.Rotate(16);
        canvas.Scale(1.3f, 1.3f);
        var crown = new PathF();
        crown.MoveTo(-30 * u, 8 * u);
        crown.LineTo(-30 * u, -14 * u);
        crown.LineTo(-15 * u, -2 * u);
        crown.LineTo(0, -26 * u);
        crown.LineTo(15 * u, -2 * u);
        crown.LineTo(30 * u, -14 * u);
        crown.LineTo(30 * u, 8 * u);
        crown.Close();
        canvas.FillColor = AccentColor;
        canvas.FillPath(crown);
        var shade = new PathF();
        shade.MoveTo(0, -26 * u);
        shade.LineTo(15 * u, -2 * u);
        shade.LineTo(30 * u, -14 * u);
        shade.LineTo(30 * u, 8 * u);
        shade.LineTo(0, 8 * u);
        shade.Close();
        canvas.SaveState();
        canvas.Alpha = 0.35f;
        canvas.FillColor = OutfitSunEdgeColor;
        canvas.FillPath(shade);
        canvas.RestoreState();
        canvas.FillColor = OutfitSunEdgeColor;
        canvas.FillRoundedRectangle(-30 * u, 4 * u, 60 * u, 6 * u, 2 * u);
        canvas.FillColor = OutfitWarmColor;
        canvas.FillCircle(0, -1 * u, 4.5f * u);
        canvas.FillColor = OutfitCoolColor;
        canvas.FillCircle(-30 * u, -14 * u, 3 * u);
        canvas.FillCircle(30 * u, -14 * u, 3 * u);
        canvas.FillCircle(0, -26 * u, 3 * u);
        canvas.FillColor = OutfitLightColor;
        canvas.FillCircle(-1.5f * u, -2.5f * u, 1.3f * u);
        canvas.FillCircle(-1 * u, -27 * u, 1 * u);
        canvas.RestoreState();
    }
}
