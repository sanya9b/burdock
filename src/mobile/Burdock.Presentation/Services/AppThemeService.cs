using Microsoft.Maui.Storage;

namespace Burdock.Presentation.Services;

/// <summary>Appearance setting of S-18: system, light or dark (spec 02 section 3.14). Persisted on the device only; not part of the synced learner settings.</summary>
public enum AppThemeMode
{
    System,
    Light,
    Dark,
}

public interface IAppThemeService
{
    AppThemeMode Mode { get; }

    void Apply(AppThemeMode mode);
}

public sealed class AppThemeService(IPreferences preferences) : IAppThemeService
{
    private const string Key = "appearance.theme";

    public AppThemeMode Mode => Enum.TryParse<AppThemeMode>(preferences.Get(Key, nameof(AppThemeMode.System)), out var mode) ? mode : AppThemeMode.System;

    public void Apply(AppThemeMode mode)
    {
        preferences.Set(Key, mode.ToString());
        if (Microsoft.Maui.Controls.Application.Current is { } app)
        {
            app.UserAppTheme = mode switch
            {
                AppThemeMode.Light => AppTheme.Light,
                AppThemeMode.Dark => AppTheme.Dark,
                _ => AppTheme.Unspecified,
            };
        }
    }
}
