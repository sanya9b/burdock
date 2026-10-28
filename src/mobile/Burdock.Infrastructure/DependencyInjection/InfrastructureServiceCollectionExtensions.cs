using Burdock.Application.Ports;
using Burdock.Domain.Learning;
using Burdock.Infrastructure.Content;
using Burdock.Infrastructure.Identity;
using Burdock.Infrastructure.Learner;
using Burdock.Infrastructure.Persistence;
using Burdock.Infrastructure.Scheduling;
using Burdock.Infrastructure.Sync;
using Burdock.Infrastructure.Time;
using Burdock.Infrastructure.Widgets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;

namespace Burdock.Infrastructure.DependencyInjection;

/// <summary>Registers port implementations shared by every platform. Platform-specific adapters are added in Platforms/ folders.</summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>HTTP calls to the API give up after this long; the sync treats a timeout as being offline.</summary>
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(30);

    public static IServiceCollection AddBurdockInfrastructure(this IServiceCollection services, DatabaseOptions? database = null, FsrsOptions? fsrs = null, SyncOptions? sync = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IIdGenerator, Uuid7IdGenerator>();

        services.AddSingleton(database ?? new DatabaseOptions(Path.Combine(FileSystem.Current.AppDataDirectory, "burdock.db3")));
        services.AddSingleton<BurdockDatabase>();
        services.AddSingleton<IContentRepository, SqliteContentRepository>();
        services.AddSingleton<ILearnerCardRepository, SqliteLearnerCardRepository>();
        services.AddSingleton<IDailyPlanCache, SqliteDailyPlanCache>();
        services.AddSingleton<IReviewLogStore, SqliteReviewLogStore>();
        services.AddSingleton<IExposureStore, SqliteExposureStore>();
        services.AddSingleton<IRewardsStore, SqliteRewardsStore>();

        services.AddSingleton(fsrs ?? FsrsOptions.Default);
        services.AddSingleton<IReviewScheduler, FsrsSharpReviewScheduler>();

        services.AddSingleton(_ => Preferences.Default);
        services.AddSingleton<ILearnerContext, PreferencesLearnerContext>();
        services.AddSingleton<IOnboardingState, PreferencesOnboardingState>();
        services.AddSingleton<IPrivacyConsent, PreferencesPrivacyConsent>();
        services.AddSingleton<IRandom, SystemRandom>();
        services.AddSingleton<IContentPackSource, BundledContentPackSource>();
        services.AddSingleton<IPronunciationAudio, BundledPronunciationAudio>();
        // Widget timeline hand-off (spec 04 §3.2): active only where an App Group container exists (iOS with the entitlement).
        services.AddSingleton<IWidgetBridge, AppGroupWidgetBridge>();

        // Sync (spec 09 sections 3.3-3.6): credentials in the keychain, outbox and cursors in SQLite, one HttpClient for the API.
        services.AddSingleton(_ => SecureStorage.Default);
        services.AddSingleton<IDeviceCredentials, SecureStorageDeviceCredentials>();
        services.AddSingleton<IProgressOutbox, SqliteProgressOutbox>();
        services.AddSingleton<ISyncState, SqliteSyncState>();
        services.AddSingleton<ISyncableSettings, PreferencesSettingsSync>();
        services.AddSingleton(_ => sync ?? SyncOptions.ForThisDevice());
        services.AddSingleton(sp => new HttpClient { BaseAddress = sp.GetRequiredService<SyncOptions>().BaseUrl, Timeout = HttpTimeout });
        services.AddSingleton<ISyncGateway>(sp => new HttpSyncGateway(
            sp.GetRequiredService<HttpClient>(),
            sp.GetRequiredService<SyncOptions>(),
            sp.GetRequiredService<IDeviceCredentials>(),
            sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<ILogger<HttpSyncGateway>>()));

        return services;
    }
}
