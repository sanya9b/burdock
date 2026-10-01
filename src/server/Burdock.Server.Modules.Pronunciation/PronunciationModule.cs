using Burdock.Server.Modules.Pronunciation.Application;
using Burdock.Server.Modules.Pronunciation.Endpoints;
using Burdock.Server.Modules.Pronunciation.Infrastructure;
using Burdock.Server.Shared.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Burdock.Server.Modules.Pronunciation;

/// <summary>
/// Pronunciation clips for words that were not part of the pipeline pre-render (ADR 0020): an admin (later the pipeline and
/// the learner's own words) requests a lexeme, a background worker renders the missing voices once through the Kokoro
/// sidecar, and the clip is served as an immutable asset. The in-memory repository and file store are the v1 adapters;
/// PostgreSQL and Object Storage replace them when the server gets its database (phase 2).
/// </summary>
public sealed class PronunciationModule : IModule
{
    public string Name => "pronunciation";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<PronunciationOptions>(configuration.GetSection(PronunciationOptions.SectionName));
        services.AddSingleton(static sp => sp.GetRequiredService<IOptions<PronunciationOptions>>().Value);

        services.AddSingleton<IClipRepository, InMemoryClipRepository>();
        services.AddSingleton<IClipStore, FileSystemClipStore>();
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
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        PronunciationEndpoints.Map(endpoints);
    }
}
