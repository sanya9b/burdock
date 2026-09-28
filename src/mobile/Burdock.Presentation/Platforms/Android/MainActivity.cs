using Android.App;
using Android.Content.PM;
using Android.Content.Res;
using Android.OS;
using AndroidX.Core.View;

namespace Burdock.Presentation;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
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
}
