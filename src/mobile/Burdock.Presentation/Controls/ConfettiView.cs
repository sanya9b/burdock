namespace Burdock.Presentation.Controls;

/// <summary>
/// Leaf-and-thorn confetti for celebrations (spec 07 §3.8.1 L2–L4): a handful of particles fall and spin for one burst, never over text
/// (place it behind the content), never longer than 2.5 s. Colours come from the style; under Reduce Motion the caller simply does not play it.
/// </summary>
public sealed class ConfettiView : GraphicsView, IDrawable
{
    public static readonly BindableProperty ColorsProperty =
        BindableProperty.Create(nameof(Colors), typeof(IList<Color>), typeof(ConfettiView), null);

    public static readonly BindableProperty ParticleCountProperty =
        BindableProperty.Create(nameof(ParticleCount), typeof(int), typeof(ConfettiView), 14);

    private readonly List<Particle> _particles = [];
    private readonly Random _random = new();
    private double _progress = 1;

    public ConfettiView()
    {
        Drawable = this;
        InputTransparent = true;
        IsVisible = false;
    }

    public IList<Color>? Colors { get => (IList<Color>?)GetValue(ColorsProperty); set => SetValue(ColorsProperty, value); }

    public int ParticleCount { get => (int)GetValue(ParticleCountProperty); set => SetValue(ParticleCountProperty, value); }

    /// <summary>Starts one burst; <paramref name="durationMs"/> should be motion.duration.celebrate or the ladder value (800–2500 ms).</summary>
    public void Play(uint durationMs)
    {
        var palette = Colors is { Count: > 0 } colors ? colors : [Microsoft.Maui.Graphics.Colors.Gold];
        _particles.Clear();
        for (var i = 0; i < ParticleCount; i++)
        {
            _particles.Add(new Particle(
                X: _random.NextDouble(),
                Drift: (_random.NextDouble() - 0.5) * 0.3,
                Delay: _random.NextDouble() * 0.3,
                Size: 6 + _random.NextDouble() * 6,
                Spin: (_random.NextDouble() - 0.5) * 720,
                Color: palette[_random.Next(palette.Count)],
                Leaf: _random.NextDouble() < 0.6));
        }

        IsVisible = true;
        this.AbortAnimation("confetti");
        new Animation(v => { _progress = v; Invalidate(); }, 0, 1).Commit(this, "confetti", 16, durationMs, Easing.Linear, (_, _) => IsVisible = false);
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        if (_progress >= 1 || _particles.Count == 0)
        {
            return;
        }

        canvas.Antialias = true;
        foreach (var p in _particles)
        {
            var t = Math.Clamp((_progress - p.Delay) / (1 - p.Delay), 0, 1);
            if (t <= 0)
            {
                continue;
            }

            var x = (float)((p.X + p.Drift * t) * dirtyRect.Width);
            var y = (float)(dirtyRect.Y - 12 + t * t * (dirtyRect.Height + 24));
            var alpha = (float)(t < 0.8 ? 1 : (1 - t) / 0.2);
            canvas.SaveState();
            canvas.Translate(x, y);
            canvas.Rotate((float)(p.Spin * t));
            canvas.Alpha = alpha;
            canvas.FillColor = p.Color;
            var s = (float)p.Size;
            if (p.Leaf)
            {
                var path = new PathF();
                path.MoveTo(0, -s);
                path.QuadTo(s, -s / 2, 0, s);
                path.QuadTo(-s, -s / 2, 0, -s);
                path.Close();
                canvas.FillPath(path);
            }
            else
            {
                canvas.FillRoundedRectangle(-s / 2, -s / 4, s, s / 2, s / 6);
            }

            canvas.RestoreState();
        }
    }

    private sealed record Particle(double X, double Drift, double Delay, double Size, double Spin, Color Color, bool Leaf);
}
