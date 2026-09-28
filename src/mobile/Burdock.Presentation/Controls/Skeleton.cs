using Burdock.Presentation.Services;
using Microsoft.Maui.Controls.Shapes;

namespace Burdock.Presentation.Controls;

/// <summary>
/// Skeleton block (spec 02 section 3.11 «Skeleton / Shimmer», NFR-03-5): a surface.sunken rectangle that stays invisible for
/// motion.duration.skeletonDelay so fast loads never flash it, then fades in and breathes. Static under Reduce Motion.
/// Colour and default radius come from the implicit style in Components.xaml.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "The token source lives between Loaded and Unloaded; the page owns the view lifetime.")]
public sealed class Skeleton : Border
{
    public static readonly BindableProperty CornerRadiusProperty =
        BindableProperty.Create(nameof(CornerRadius), typeof(double), typeof(Skeleton), 8d, propertyChanged: (b, _, _) => ((Skeleton)b).ApplyShape());

    private const string PulseHandle = "pulse";
    private const double PulseFloor = 0.55;

    private CancellationTokenSource? _appearance;

    public Skeleton()
    {
        StrokeThickness = 0;
        Opacity = 0;
        InputTransparent = true;
        AutomationProperties.SetIsInAccessibleTree(this, false);
        ApplyShape();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public double CornerRadius { get => (double)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    private void ApplyShape() => StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(CornerRadius) };

    private void OnLoaded(object? sender, EventArgs e) => _ = AppearAsync();

    private void OnUnloaded(object? sender, EventArgs e)
    {
        this.AbortAnimation(PulseHandle);
        _appearance?.Cancel();
        _appearance?.Dispose();
        _appearance = null;
        Opacity = 0;
    }

    private async Task AppearAsync()
    {
        _appearance?.Cancel();
        _appearance?.Dispose();
        var cts = _appearance = new CancellationTokenSource();
        try
        {
            await Task.Delay(Duration("MotionDurationSkeletonDelay", 300), cts.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var reduceMotion = Handler?.MauiContext?.Services.GetService<IMotionPreferences>()?.ReduceMotion ?? false;
        if (reduceMotion)
        {
            Opacity = 1;
            return;
        }

        await this.FadeToAsync(1, (uint)Duration("MotionDurationBase", 200), Easing.CubicOut).ConfigureAwait(true);
        if (cts.IsCancellationRequested)
        {
            return;
        }

        var slow = Duration("MotionDurationSlow", 320);
        var pulse = new Animation();
        pulse.Add(0, 0.5, new Animation(v => Opacity = v, 1, PulseFloor, Easing.SinInOut));
        pulse.Add(0.5, 1, new Animation(v => Opacity = v, PulseFloor, 1, Easing.SinInOut));
        pulse.Commit(this, PulseHandle, 16, (uint)(slow * 4), repeat: () => IsLoaded && !cts.IsCancellationRequested);
    }

    private static int Duration(string key, int fallback)
        => Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out var raw) == true && raw is int ms ? ms : fallback;
}
