namespace Burdock.Presentation.Controls;

/// <summary>Session progress as one rounded segment per exercise (spec 03 S-12 top row); the newest segment fills with a short animation.</summary>
public sealed class SegmentedProgressBar : GraphicsView, IDrawable
{
    public static readonly BindableProperty TotalProperty =
        BindableProperty.Create(nameof(Total), typeof(int), typeof(SegmentedProgressBar), 1, propertyChanged: (b, _, _) => ((SegmentedProgressBar)b).Invalidate());

    public static readonly BindableProperty CompletedProperty =
        BindableProperty.Create(nameof(Completed), typeof(int), typeof(SegmentedProgressBar), 0, propertyChanged: OnCompletedChanged);

    public static readonly BindableProperty FillColorProperty =
        BindableProperty.Create(nameof(FillColor), typeof(Color), typeof(SegmentedProgressBar), Colors.Black, propertyChanged: (b, _, _) => ((SegmentedProgressBar)b).Invalidate());

    public static readonly BindableProperty TrackColorProperty =
        BindableProperty.Create(nameof(TrackColor), typeof(Color), typeof(SegmentedProgressBar), Colors.LightGray, propertyChanged: (b, _, _) => ((SegmentedProgressBar)b).Invalidate());

    public static readonly BindableProperty GapProperty =
        BindableProperty.Create(nameof(Gap), typeof(double), typeof(SegmentedProgressBar), 4d, propertyChanged: (b, _, _) => ((SegmentedProgressBar)b).Invalidate());

    public static readonly BindableProperty DurationProperty =
        BindableProperty.Create(nameof(Duration), typeof(int), typeof(SegmentedProgressBar), 200);

    private double _reveal = 1;

    public SegmentedProgressBar()
    {
        Drawable = this;
    }

    public int Total { get => (int)GetValue(TotalProperty); set => SetValue(TotalProperty, value); }

    public int Completed { get => (int)GetValue(CompletedProperty); set => SetValue(CompletedProperty, value); }

    public Color FillColor { get => (Color)GetValue(FillColorProperty); set => SetValue(FillColorProperty, value); }

    public Color TrackColor { get => (Color)GetValue(TrackColorProperty); set => SetValue(TrackColorProperty, value); }

    public double Gap { get => (double)GetValue(GapProperty); set => SetValue(GapProperty, value); }

    /// <summary>Fill animation length in ms (motion.duration.base).</summary>
    public int Duration { get => (int)GetValue(DurationProperty); set => SetValue(DurationProperty, value); }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        var count = Math.Max(1, Total);
        var gap = (float)Gap;
        var width = (dirtyRect.Width - gap * (count - 1)) / count;
        var height = dirtyRect.Height;
        var radius = height / 2;
        canvas.Antialias = true;

        for (var i = 0; i < count; i++)
        {
            var x = dirtyRect.X + i * (width + gap);
            canvas.FillColor = TrackColor;
            canvas.FillRoundedRectangle(x, dirtyRect.Y, width, height, radius);

            if (i >= Completed)
            {
                continue;
            }

            var fillWidth = i == Completed - 1 ? (float)(width * _reveal) : width;
            if (fillWidth <= 0)
            {
                continue;
            }

            canvas.FillColor = FillColor;
            canvas.FillRoundedRectangle(x, dirtyRect.Y, Math.Max(fillWidth, height), height, radius);
        }
    }

    private static void OnCompletedChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var self = (SegmentedProgressBar)bindable;
        if (newValue is int added && oldValue is int previous && added > previous && self.Duration > 0)
        {
            self.AbortAnimation("reveal");
            new Animation(v => { self._reveal = v; self.Invalidate(); }, 0, 1).Commit(self, "reveal", 16, (uint)self.Duration, Easing.CubicOut);
            return;
        }

        self._reveal = 1;
        self.Invalidate();
    }
}
