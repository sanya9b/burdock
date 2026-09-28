using Burdock.Application.Ports;
using Burdock.Domain.Learning;
using Burdock.Infrastructure.Content;
using Burdock.Infrastructure.Identity;
using Burdock.Infrastructure.Learner;
using Burdock.Infrastructure.Persistence;
using Burdock.Infrastructure.Scheduling;
using Burdock.Infrastructure.Time;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Storage;

namespace Burdock.Infrastructure.DependencyInjection;

/// <summary>Registers port implementations shared by every platform. Platform-specific adapters are added in Platforms/ folders.</summary>
public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddBurdockInfrastructure(this IServiceCollection services, DatabaseOptions? database = null, FsrsOptions? fsrs = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IIdGenerator, Uuid7IdGenerator>();

        services.AddSingleton(database ?? new DatabaseOptions(Path.Combine(FileSystem.Current.AppDataDirectory, "burdock.db3")));
        services.AddSingleton<BurdockDatabase>();
        services.AddSingleton<IContentRepository, SqliteContentRepository>();
        services.AddSingleton<ILearnerCardRepository, SqliteLearnerCardRepository>();
        services.AddSingleton<IReviewLogStore, SqliteReviewLogStore>();
        services.AddSingleton<IExposureStore, SqliteExposureStore>();

        services.AddSingleton(fsrs ?? FsrsOptions.Default);
        services.AddSingleton<IReviewScheduler, FsrsSharpReviewScheduler>();

        services.AddSingleton(_ => Preferences.Default);
        services.AddSingleton<ILearnerContext, PreferencesLearnerContext>();
        services.AddSingleton<IOnboardingState, PreferencesOnboardingState>();
        services.AddSingleton<IPrivacyConsent, PreferencesPrivacyConsent>();
        services.AddSingleton<IRandom, SystemRandom>();
        services.AddSingleton<IContentPackSource, BundledContentPackSource>();

        return services;
    }
}
