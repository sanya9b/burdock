namespace Burdock.Presentation.Controls;

/// <summary>
/// A miniature widget card of a widget theme for the shop tiles (spec 02 §3.12 themes «Цвіт», «Луг», «Ніч», «Сонце»): the widget background
/// gradient fills a rounded square, a bold bar stands for the word, a thinner one for the translation, an accent pill for the button. Colours come
/// from the generated ColorTheme{Theme}Widget* keys and follow the app theme; the semantic widget colours are the fallback.
/// </summary>
public sealed class ThemeSwatchView : GraphicsView, IDrawable
{
    public static readonly BindableProperty ThemeProperty =
        BindableProperty.Create(nameof(Theme), typeof(string), typeof(ThemeSwatchView), string.Empty, propertyChanged: (b, _, _) => ((ThemeSwatchView)b).Invalidate());

    public ThemeSwatchView()
    {
        Drawable = this;
        InputTransparent = true;
    }

    /// <summary>Generated theme name: Tsvit, Luh, Nich or Sontse.</summary>
    public string Theme { get => (string)GetValue(ThemeProperty); set => SetValue(ThemeProperty, value); }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        var side = Math.Min(dirtyRect.Width, dirtyRect.Height);
        if (side <= 0 || string.IsNullOrEmpty(Theme))
        {
            return;
        }

        var dark = Microsoft.Maui.Controls.Application.Current?.RequestedTheme == AppTheme.Dark;
        var background = Colour("Background", dark);
        var backgroundEnd = Colour("BackgroundEnd", dark);
        var accent = Colour("Accent", dark);
        var word = Colour("Word", dark);
        if (background is null || accent is null || word is null)
        {
            return;
        }

        var translation = Colour("Translation", dark) ?? word;
        canvas.Antialias = true;
        var card = new RectF(dirtyRect.Center.X - side / 2, dirtyRect.Center.Y - side / 2, side, side);
        // Explicit stops: the parameterless LinearGradientPaint starts with white-to-black stops that AddOffset would only join, not replace.
        var paint = new LinearGradientPaint([new PaintGradientStop(0f, background), new PaintGradientStop(1f, backgroundEnd ?? background)], new Point(0, 0), new Point(1, 1));
        canvas.SaveState();
        canvas.SetFillPaint(paint, card);
        canvas.FillRoundedRectangle(card, side * 0.22f);
        canvas.RestoreState();

        var pad = side * 0.16f;
        var line = side * 0.13f;
        canvas.FillColor = word;
        canvas.FillRoundedRectangle(card.Left + pad, card.Top + pad, side * 0.52f, line, line / 2);
        canvas.SaveState();
        canvas.Alpha = 0.85f;
        canvas.FillColor = translation;
        canvas.FillRoundedRectangle(card.Left + pad, card.Top + pad + line * 1.75f, side * 0.36f, line * 0.65f, line * 0.325f);
        canvas.RestoreState();
        var pill = line * 1.35f;
        canvas.FillColor = accent;
        canvas.FillRoundedRectangle(card.Left + pad, card.Bottom - pad - pill, side * 0.4f, pill, pill / 2);
    }

    private Color? Colour(string name, bool dark)
    {
        var mode = dark ? "Dark" : "Light";
        return Lookup($"ColorTheme{Theme}Widget{name}{mode}") ?? Lookup($"ColorWidget{name}{mode}");
    }

    private static Color? Lookup(string key)
    {
        return Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color ? color : null;
    }
}
