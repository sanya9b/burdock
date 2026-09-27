using Burdock.Application.DependencyInjection;
using Burdock.Infrastructure.DependencyInjection;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;

namespace Burdock.Presentation;

/// <summary>Composition root of the mobile app (spec 01 section 3.9). Only registration lives here.</summary>
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                // Fixel (MacPaw, OFL 1.1): full Ukrainian Cyrillic incl. ґ є ї and the U+2019 apostrophe (T6, spec 02 section 3.4).
                fonts.AddFont("FixelText-Regular.ttf", "FixelText");
                fonts.AddFont("FixelText-Medium.ttf", "FixelTextMedium");
                fonts.AddFont("FixelText-SemiBold.ttf", "FixelTextSemiBold");
                fonts.AddFont("FixelText-Bold.ttf", "FixelTextBold");
                fonts.AddFont("FixelDisplay-Bold.ttf", "FixelDisplay");
                fonts.AddFont("FixelDisplay-ExtraBold.ttf", "FixelDisplayExtraBold");
            });

        builder.Services
            .AddBurdockApplication()
            .AddBurdockInfrastructure();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
