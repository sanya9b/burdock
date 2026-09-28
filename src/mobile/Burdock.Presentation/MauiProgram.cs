using Burdock.Application.DependencyInjection;
using Burdock.Infrastructure.DependencyInjection;
using Burdock.Presentation.DependencyInjection;
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

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
