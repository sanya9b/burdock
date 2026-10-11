using System.Globalization;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;

namespace Burdock.Infrastructure.Sync;

/// <summary>Where the app talks to (spec 09 section 3.1) and how it introduces itself at registration (platform, version, locale).</summary>
public sealed record SyncOptions(Uri BaseUrl, string Platform, string AppVersion, string Locale)
{
    public static Uri ProductionBaseUrl { get; } = new("https://api.burdock.org/");

    /// <summary>The Android emulator reaches the development machine at 10.0.2.2; the API listens on 5210 (launchSettings.json).</summary>
    public static Uri AndroidEmulatorBaseUrl { get; } = new("http://10.0.2.2:5210/");

    public static Uri LocalBaseUrl { get; } = new("http://localhost:5210/");

    /// <summary>Debug builds target the developer's API; everything else the production host.</summary>
    public static SyncOptions ForThisDevice()
    {
        var platform = DeviceInfo.Current.Platform == DevicePlatform.iOS ? "ios" : "android";
#if DEBUG
        var baseUrl = DeviceInfo.Current.Platform == DevicePlatform.Android ? AndroidEmulatorBaseUrl : LocalBaseUrl;
#else
        var baseUrl = ProductionBaseUrl;
#endif
        return new SyncOptions(baseUrl, platform, AppInfo.Current.VersionString, CultureInfo.CurrentUICulture.Name);
    }
}
