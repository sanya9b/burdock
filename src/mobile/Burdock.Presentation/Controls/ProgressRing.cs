namespace Burdock.Presentation.Controls;

/// <summary>ProgressRing (spec 02 section 3.11): a round track with an animated arc; put the value label on top in a Grid.</summary>
public sealed class ProgressRing : GraphicsView, IDrawable
{
    public static readonly BindableProperty ValueProperty =
        BindableProperty.Create(nameof(Value), typeof(double), typeof(ProgressRing), 0d, propertyChanged: OnValueChanged);

    public static readonly BindableProperty ThicknessProperty =
        BindableProperty.Create(nameof(Thickness), typeof(double), typeof(ProgressRing), 10d, propertyChanged: (b, _, _) => ((ProgressRing)b).Invalidate());

    public static readonly BindableProperty FillColorProperty =
        BindableProperty.Create(nameof(FillColor), typeof(Color), typeof(ProgressRing), Colors.Black, propertyChanged: (b, _, _) => ((ProgressRing)b).Invalidate());

    public static readonly BindableProperty TrackColorProperty =
        BindableProperty.Create(nameof(TrackColor), typeof(Color), typeof(ProgressRing), Colors.LightGray, propertyChanged: (b, _, _) => ((ProgressRing)b).Invalidate());

    public static readonly BindableProperty DurationProperty =
        BindableProperty.Create(nameof(Duration), typeof(int), typeof(ProgressRing), 600);

    public static readonly BindableProperty SegmentsProperty =
        BindableProperty.Create(nameof(Segments), typeof(int), typeof(ProgressRing), 0, propertyChanged: (b, _, _) => ((ProgressRing)b).Invalidate());

    /// <summary>Gap between segments in degrees.</summary>
    private const float SegmentGapDegrees = 14f;

    private double _shown;

    public ProgressRing()
    {
        Drawable = this;
    }

    /// <summary>0..1.</summary>
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    public double Thickness { get => (double)GetValue(ThicknessProperty); set => SetValue(ThicknessProperty, value); }

    public Color FillColor { get => (Color)GetValue(FillColorProperty); set => SetValue(FillColorProperty, value); }

    public Color TrackColor { get => (Color)GetValue(TrackColorProperty); set => SetValue(TrackColorProperty, value); }

    /// <summary>Sweep animation length in ms (motion.duration.celebrate).</summary>
    public int Duration { get => (int)GetValue(DurationProperty); set => SetValue(DurationProperty, value); }

    /// <summary>0 = continuous arc; N &gt; 0 = N separate segments, of which round(Value × N) are filled (mastery ring, S-14).</summary>
    public int Segments { get => (int)GetValue(SegmentsProperty); set => SetValue(SegmentsProperty, value); }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        var stroke = (float)Thickness;
        var side = Math.Min(dirtyRect.Width, dirtyRect.Height) - stroke;
        if (side <= 0)
        {
            return;
        }

        var x = dirtyRect.X + (dirtyRect.Width - side) / 2;
        var y = dirtyRect.Y + (dirtyRect.Height - side) / 2;
        canvas.Antialias = true;
        canvas.StrokeSize = stroke;
        canvas.StrokeLineCap = LineCap.Round;
        if (Segments > 0)
        {
            DrawSegments(canvas, x, y, side);
            return;
        }

        canvas.StrokeColor = TrackColor;
        canvas.DrawEllipse(x, y, side, side);

        var sweep = (float)(360 * Math.Clamp(_shown, 0, 1));
        if (sweep <= 0)
        {
            return;
        }

        canvas.StrokeColor = FillColor;
        canvas.DrawArc(x, y, side, side, 90, 90 - Math.Min(sweep, 359.9f), clockwise: true, closed: false);
    }

    private void DrawSegments(ICanvas canvas, float x, float y, float side)
    {
        var count = Segments;
        var filled = (int)Math.Round(Math.Clamp(_shown, 0, 1) * count, MidpointRounding.AwayFromZero);
        var span = 360f / count;
        var arc = Math.Max(1f, span - SegmentGapDegrees);
        for (var i = 0; i < count; i++)
        {
            // Segments run clockwise from 12 o'clock; Maui angles are counter-clockwise from 3 o'clock.
            var start = 90 - i * span - SegmentGapDegrees / 2;
            canvas.StrokeColor = i < filled ? FillColor : TrackColor;
            canvas.DrawArc(x, y, side, side, start, start - arc, clockwise: true, closed: false);
        }
    }

    private static void OnValueChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var self = (ProgressRing)bindable;
        var target = Math.Clamp((double)newValue, 0, 1);
        self.AbortAnimation("sweep");
        if (self.Duration <= 0)
        {
            self._shown = target;
            self.Invalidate();
            return;
        }

        new Animation(v => { self._shown = v; self.Invalidate(); }, self._shown, target).Commit(self, "sweep", 16, (uint)self.Duration, Easing.CubicOut);
    }
}
