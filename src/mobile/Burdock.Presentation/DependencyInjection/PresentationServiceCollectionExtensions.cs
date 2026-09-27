using Burdock.Presentation.Navigation;
using Burdock.Presentation.Services;
using Burdock.Presentation.ViewModels;
using Burdock.Presentation.Views;

namespace Burdock.Presentation.DependencyInjection;

/// <summary>Pages and ViewModels are transient (one per navigation), Shell and navigation are singletons (spec 01 section 3.13).</summary>
public static class PresentationServiceCollectionExtensions
{
    public static IServiceCollection AddBurdockPresentation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<AppShell>();
        services.AddSingleton<INavigationService, ShellNavigationService>();
        services.AddSingleton<ISpeechService, MauiSpeechService>();

        services.AddTransient<TodayViewModel>();
        services.AddTransient<TodayPage>();
        services.AddTransient<LearnViewModel>();
        services.AddTransient<LearnPage>();
        services.AddTransient<WordsViewModel>();
        services.AddTransient<WordsPage>();
        services.AddTransient<MeViewModel>();
        services.AddTransient<MePage>();
        services.AddTransient<WordCardViewModel>();
        services.AddTransient<WordCardPage>();
        services.AddTransient<SessionViewModel>();
        services.AddTransient<SessionPage>();
        services.AddTransient<SessionSummaryViewModel>();
        services.AddTransient<SessionSummaryPage>();

        return services;
    }
}
