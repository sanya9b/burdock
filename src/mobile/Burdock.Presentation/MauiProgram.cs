using Burdock.Application.DependencyInjection;
using Burdock.Infrastructure.DependencyInjection;
using Burdock.Presentation.DependencyInjection;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
#if IOS
using Microsoft.Maui.LifecycleEvents;
#endif

namespace Burdock.Presentation;

/// <summary>Composition root of the mobile app (spec 01 section 3.9). Only registration lives here.</summary>
public static class MauiProgram
{
    // Only the platform heads create the app; the neutral net10.0 head exists for ViewModel tests and never calls this.
    [System.Runtime.Versioning.SupportedOSPlatform("android21.0")]
    [System.Runtime.Versioning.SupportedOSPlatform("ios15.0")]
    [System.Runtime.Versioning.SupportedOSPlatform("maccatalyst15.0")]
    [System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                // Type system (ADR 0018): Rubik for text, Unbounded for display; static instances of the OFL variable fonts,
                // full Ukrainian Cyrillic incl. ґ є ї and U+2019. Aliases are derived by design/tokens/generate.py (family + weight suffix).
                fonts.AddFont("Rubik-Regular.ttf", "Rubik");
                fonts.AddFont("Rubik-Medium.ttf", "RubikMedium");
                fonts.AddFont("Rubik-SemiBold.ttf", "RubikSemiBold");
                fonts.AddFont("Rubik-Bold.ttf", "RubikBold");
                fonts.AddFont("Unbounded-SemiBold.ttf", "UnboundedSemiBold");
                fonts.AddFont("Unbounded-Bold.ttf", "UnboundedBold");
                fonts.AddFont("Unbounded-ExtraBold.ttf", "UnboundedExtraBold");
                // Lucide icon font (ISC, lucide-static 1.48.0): glyph constants live in Resources/Icons/Icon.cs (spec 02 section 3.9).
                fonts.AddFont("Lucide.ttf", "Lucide");
            });

        builder.Services
            .AddBurdockApplication()
            .AddBurdockInfrastructure()
            .AddBurdockPresentation();

#if IOS
        if (OperatingSystem.IsIOSVersionAtLeast(15) || OperatingSystem.IsMacCatalystVersionAtLeast(15))
        {
            ConfigureDeepLinks(builder);
        }
#endif

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

#if IOS
    // burdock:// links from widgets and notifications (spec 04 §3.4): the router keeps the link until the Shell can navigate.
    // A separate method so the lambdas inherit iOS-only platform attributes (CA1416) instead of the Android ones above.
    [System.Runtime.Versioning.SupportedOSPlatform("ios15.0")]
    [System.Runtime.Versioning.SupportedOSPlatform("maccatalyst15.0")]
    private static void ConfigureDeepLinks(MauiAppBuilder builder)
    {
        builder.ConfigureLifecycleEvents(events => events.AddiOS(ios =>
        {
            ios.FinishedLaunching((_, options) =>
            {
                if (options?.ObjectForKey(UIKit.UIApplication.LaunchOptionsUrlKey) is Foundation.NSUrl url)
                {
                    AcceptDeepLink(url);
                }

                return true;
            });
            ios.OpenUrl((_, url, _) =>
            {
                AcceptDeepLink(url);
                return true;
            });
        }));
    }

    [System.Runtime.Versioning.SupportedOSPlatform("ios15.0")]
    [System.Runtime.Versioning.SupportedOSPlatform("maccatalyst15.0")]
    private static void AcceptDeepLink(Foundation.NSUrl url)
    {
        var router = IPlatformApplication.Current?.Services.GetService<Navigation.IDeepLinkRouter>();
        router?.Accept(Uri.TryCreate(url.AbsoluteString, UriKind.Absolute, out var uri) ? uri : null);
    }
#endif
}
