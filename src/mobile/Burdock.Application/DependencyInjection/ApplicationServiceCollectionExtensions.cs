using Burdock.Application.UseCases.Content;
using Burdock.Application.UseCases.LearningDays;
using Burdock.Application.UseCases.Planning;
using Burdock.Application.UseCases.Sessions;
using Burdock.Application.UseCases.Settings;
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
        return services;
    }
}
