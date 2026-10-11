using Burdock.Application.UseCases.Content;
using Burdock.Application.UseCases.LearningDays;
using Burdock.Application.UseCases.Onboarding;
using Burdock.Application.UseCases.Planning;
using Burdock.Application.UseCases.Rewards;
using Burdock.Application.UseCases.Sessions;
using Burdock.Application.UseCases.Settings;
using Burdock.Application.UseCases.Sync;
using Burdock.Application.UseCases.Words;
using Microsoft.Extensions.DependencyInjection;

namespace Burdock.Application.DependencyInjection;

/// <summary>Registers use cases. Ports are registered by Infrastructure and the platform head, never here.</summary>
public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddBurdockApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<LearningDayResolver>();
        services.AddTransient<BootstrapContentUseCase>();
        services.AddTransient<GetDailyPlanUseCase>();
        services.AddTransient<RecordExposureUseCase>();
        services.AddTransient<OpenWordUseCase>();
        services.AddTransient<IntroduceWordUseCase>();
        services.AddTransient<ClaimKnownUseCase>();
        services.AddTransient<BuildSessionUseCase>();
        services.AddTransient<GradeAnswerUseCase>();
        services.AddTransient<BrowseWordsUseCase>();
        services.AddTransient<GetLearnerSettingsUseCase>();
        services.AddTransient<UpdateLearnerSettingsUseCase>();
        services.AddTransient<IsOnboardingCompletedUseCase>();
        services.AddTransient<SuggestNicknameUseCase>();
        services.AddTransient<EstimateWordCountUseCase>();
        services.AddTransient<CompleteOnboardingUseCase>();
        services.AddTransient<BuildPlacementQuizUseCase>();
        services.AddTransient<SettleSessionRewardsUseCase>();
        services.AddTransient<GetRewardsOverviewUseCase>();
        services.AddTransient<SyncProgressUseCase>();
        services.AddTransient<SyncSettingsUseCase>();
        services.AddTransient<RefreshContentUseCase>();
        services.AddTransient<SyncUseCase>();
        return services;
    }
}
