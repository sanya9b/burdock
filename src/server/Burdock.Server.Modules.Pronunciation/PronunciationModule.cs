using Burdock.Server.Modules.Pronunciation.Application;
using Burdock.Server.Modules.Pronunciation.Endpoints;
using Burdock.Server.Modules.Pronunciation.Infrastructure;
using Burdock.Server.Shared.Modules;
using Burdock.Server.Shared.Persistence;
using Burdock.Server.Shared.Storage;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Burdock.Server.Modules.Pronunciation;

/// <summary>
/// Pronunciation clips for words that were not part of the pipeline pre-render (ADR 0020): an admin (later the pipeline and
/// the learner's own words) requests a lexeme, a background worker renders the missing voices once through the Kokoro
/// sidecar, and the clip is served as an immutable asset. Clip rows live in the <c>pronunciation</c> schema, the clips in
/// the shared object store (folder or S3); the pre-rendered dictionary is imported with <c>api pronunciation publish</c>.
/// </summary>
public sealed class PronunciationModule : IModule
{
    public string Name => "pronunciation";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = configuration.GetSection(PronunciationOptions.SectionName).Get<PronunciationOptions>() ?? new PronunciationOptions();
        services.AddSingleton(options);
        services.AddModuleDbContext<PronunciationDbContext>(configuration, PronunciationDbContext.Schema);
        services.AddScoped<IClipRepository, EfClipRepository>();
        services.AddSingleton<IClipStore>(sp => new ObjectClipStore(ObjectStores.Create(options, sp.GetRequiredService<IHostEnvironment>(), PronunciationOptions.SectionName)));
        services.AddSingleton<ChannelRenderJobs>();
        services.AddSingleton<IRenderJobs>(static sp => sp.GetRequiredService<ChannelRenderJobs>());
        services.AddHttpClient<IPronunciationRenderer, KokoroHttpRenderer>(static (sp, client) =>
        {
            var options = sp.GetRequiredService<PronunciationOptions>();
            client.BaseAddress = options.KokoroBaseUrl;
            client.Timeout = TimeSpan.FromSeconds(options.RenderTimeoutSeconds);
        });
        services.AddHostedService<RenderWorker>();

        services.AddScoped<RequestClipsUseCase>();
        services.AddScoped<GetClipUseCase>();
        services.AddScoped<ImportRenderedClipUseCase>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        PronunciationEndpoints.Map(endpoints);
    }
}
