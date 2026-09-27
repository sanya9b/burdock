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
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
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
