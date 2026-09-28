using Android.App;
using Android.Content.PM;
using Android.Content.Res;
using Android.OS;
using Android.Runtime;
using Android.Window;
using AndroidX.Core.View;

namespace Burdock.Presentation;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(31) && SplashScreen is { } splash)
        {
            // Let the animated mark (drawable-v31/burdock_splash_anim) finish before the system splash leaves; the bootstrap page underneath shows the same mark.
            splash.SetOnExitAnimationListener(new SplashExitListener());
        }
        else
        {
            Services.SystemSplash.MarkDismissed();
        }

        base.OnCreate(savedInstanceState);
        ApplySystemBarAppearance();
    }

    public override void OnConfigurationChanged(Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);
        ApplySystemBarAppearance();
    }

    /// <summary>
    /// Edge-to-edge (API 35+) draws the system bars over our surface colour, so icon contrast is decided here per theme:
    /// dark icons on the light surface, light icons on the dark one (spec 02 section 3.11, GitHub issue #1).
    /// </summary>
    private void ApplySystemBarAppearance()
    {
        if (Window is not { } window || window.DecorView is not { } decor)
        {
            return;
        }

        var uiMode = Resources?.Configuration?.UiMode ?? UiMode.NightUndefined;
        var isDark = (uiMode & UiMode.NightMask) == UiMode.NightYes;
        if (WindowCompat.GetInsetsController(window, decor) is not { } controller)
        {
            return;
        }

        controller.AppearanceLightStatusBars = !isDark;
        controller.AppearanceLightNavigationBars = !isDark;
    }

    /// <summary>Keeps the Android 12+ splash until its icon animation has played (max 1000 ms), then fades it over the bootstrap page.</summary>
    [System.Runtime.Versioning.SupportedOSPlatform("android31.0")]
    private sealed class SplashExitListener : Java.Lang.Object, ISplashScreenOnExitAnimationListener
    {
        private const long FadeMilliseconds = 220;

        public void OnSplashScreenExit(SplashScreenView view)
        {
            ArgumentNullException.ThrowIfNull(view);
            // Wait only for the part of the icon animation that has not played yet; no animation info means no wait.
            var remaining = view.IconAnimationStart is { } startedAt && view.IconAnimationDuration is { } duration
                ? Math.Max(0, startedAt.ToEpochMilli() + duration.ToMillis() - Java.Lang.JavaSystem.CurrentTimeMillis())
                : 0;
            view.PostDelayed(
                () => view.Animate()?.Alpha(0f).SetDuration(FadeMilliseconds).WithEndAction(new Java.Lang.Runnable(() =>
                {
                    view.Remove();
                    Services.SystemSplash.MarkDismissed();
                })).Start(),
                remaining);
        }
    }
}
