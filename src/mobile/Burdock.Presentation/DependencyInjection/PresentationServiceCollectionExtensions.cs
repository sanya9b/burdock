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
        services.AddSingleton<ISpeechService, SpeechService>();
        services.AddSingleton<IMotionPreferences, MotionPreferences>();
        services.AddSingleton<ISystemSplash, SystemSplash>();
        services.AddSingleton<IChoiceSheet, ShellChoiceSheet>();
        services.AddSingleton<IToastService, CommunityToast>();
        services.AddSingleton<IAppThemeService, AppThemeService>();
        services.AddSingleton<INotificationPrompt, NotificationPrompt>();
        services.AddSingleton<IHaptics, MauiHaptics>();

        services.AddTransient<BootstrapViewModel>();
        services.AddTransient<BootstrapPage>();
        services.AddTransient<OnboardingViewModel>();
        services.AddTransient<OnboardingPage>();

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
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<SettingsPage>();

        return services;
    }
}
