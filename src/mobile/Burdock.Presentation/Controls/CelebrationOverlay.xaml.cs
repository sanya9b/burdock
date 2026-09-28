using System.Windows.Input;
using Burdock.Presentation.Services;
using Burdock.Presentation.ViewModels;

namespace Burdock.Presentation.Controls;

/// <summary>Shows one <see cref="CelebrationItem"/> at a time with the ladder animation: scrim fade, card spring-in, confetti for L3+. Static under Reduce Motion.</summary>
public partial class CelebrationOverlay : ContentView
{
    public static readonly BindableProperty ItemProperty =
        BindableProperty.Create(nameof(Item), typeof(CelebrationItem), typeof(CelebrationOverlay), null, propertyChanged: (b, _, n) => ((CelebrationOverlay)b).OnItemChanged(n as CelebrationItem));

    public static readonly BindableProperty ReduceMotionProperty =
        BindableProperty.Create(nameof(ReduceMotion), typeof(bool), typeof(CelebrationOverlay), false);

    public static readonly BindableProperty DismissCommandProperty =
        BindableProperty.Create(nameof(DismissCommand), typeof(ICommand), typeof(CelebrationOverlay), null);

    public static readonly BindableProperty HapticsProperty =
        BindableProperty.Create(nameof(Haptics), typeof(IHaptics), typeof(CelebrationOverlay), null);

    public CelebrationOverlay()
    {
        InitializeComponent();
    }

    public CelebrationItem? Item { get => (CelebrationItem?)GetValue(ItemProperty); set => SetValue(ItemProperty, value); }

    public bool ReduceMotion { get => (bool)GetValue(ReduceMotionProperty); set => SetValue(ReduceMotionProperty, value); }

    /// <summary>Invoked by the button and the scrim; the owner advances its queue (sets the next Item or null).</summary>
    public ICommand? DismissCommand { get => (ICommand?)GetValue(DismissCommandProperty); set => SetValue(DismissCommandProperty, value); }

    public IHaptics? Haptics { get => (IHaptics?)GetValue(HapticsProperty); set => SetValue(HapticsProperty, value); }

    private void OnDismissTapped(object? sender, TappedEventArgs e) => DismissCommand?.Execute(null);

    private void OnItemChanged(CelebrationItem? item)
    {
        if (item is null)
        {
            _ = HideAsync();
            return;
        }

        _ = ShowAsync(item);
    }

    private async Task ShowAsync(CelebrationItem item)
    {
        IsVisible = true;
        Card.CancelAnimations();
        Scrim.CancelAnimations();
        SemanticScreenReader.Default.Announce($"{item.Title}. {item.Body} {item.Reward}".Trim());
        Haptics?.Success();
        if (ReduceMotion)
        {
            Scrim.Opacity = 1;
            Card.Scale = 1;
            Card.Opacity = 1;
            return;
        }

        Scrim.Opacity = 0;
        Card.Opacity = 0;
        Card.Scale = 0.85;
        var slow = Duration("MotionDurationSlow", 320);
        _ = Scrim.FadeToAsync(1, Duration("MotionDurationBase", 200), Easing.CubicOut);
        _ = Card.FadeToAsync(1, slow, Easing.CubicOut);
        await Card.ScaleToAsync(1, slow, Easing.SpringOut).ConfigureAwait(true);
        if (item.Level >= 3)
        {
            Confetti.Play(item.Level >= 4 ? 2500u : 1500u);
        }
    }

    private async Task HideAsync()
    {
        if (!IsVisible)
        {
            return;
        }

        if (!ReduceMotion)
        {
            var fast = Duration("MotionDurationFast", 120);
            await Task.WhenAll(Card.FadeToAsync(0, fast, Easing.CubicIn), Scrim.FadeToAsync(0, fast, Easing.CubicIn)).ConfigureAwait(true);
        }

        IsVisible = false;
    }

    private static uint Duration(string key, uint fallback)
        => Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out var raw) == true && raw is int ms ? (uint)ms : fallback;
}
