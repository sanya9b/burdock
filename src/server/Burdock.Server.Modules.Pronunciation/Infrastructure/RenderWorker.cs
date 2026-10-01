using Burdock.Domain.SharedKernel;
using Burdock.Server.Modules.Pronunciation.Application;
using Burdock.Server.Modules.Pronunciation.Domain;
using Burdock.Server.Shared.Time;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Burdock.Server.Modules.Pronunciation.Infrastructure;

/// <summary>
/// Renders queued clips one at a time (the sidecar is CPU-bound, ADR 0020 §4): engine → store → repository. A failed
/// attempt is retried with a short back-off up to <see cref="Clip.MaxAttempts"/>; the queue survives duplicates because the
/// clip state is re-read before every attempt.
/// </summary>
public sealed partial class RenderWorker(
    IRenderJobs queue,
    IClipRepository clips,
    IClipStore store,
    IServiceScopeFactory scopes,
    IClock clock,
    PronunciationOptions options,
    ILogger<RenderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var key in queue.DequeueAllAsync(stoppingToken).ConfigureAwait(false))
        {
            await RenderAsync(key, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task RenderAsync(ClipKey key, CancellationToken cancellationToken)
    {
        var clip = await clips.GetAsync(key, cancellationToken).ConfigureAwait(false);
        if (clip is null || clip.Status != ClipStatus.Queued || !VoiceCatalog.TryGet(key.Voice.Value, out var voice))
        {
            return;
        }

        try
        {
            using var scope = scopes.CreateScope();
            var renderer = scope.ServiceProvider.GetRequiredService<IPronunciationRenderer>();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(options.RenderTimeoutSeconds));
            var audio = await renderer.RenderAsync(clip.Text, voice, timeout.Token).ConfigureAwait(false);
            var stored = await store.PutAsync(Clip.StoragePath(clip.EngineVersion, key), audio.Content, cancellationToken).ConfigureAwait(false);
            await clips.SaveAsync(clip.Rendered(stored.Path, stored.Sha256, stored.Bytes, clock.UtcNow), cancellationToken).ConfigureAwait(false);
            LogRendered(key.LexemeId, key.Voice.Value, stored.Bytes);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            var failed = clip.FailedAttempt(ex.GetType().Name);
            await clips.SaveAsync(failed, cancellationToken).ConfigureAwait(false);
            LogAttemptFailed(ex, key.LexemeId, key.Voice.Value, failed.Attempts, failed.Status);
            if (failed.Status == ClipStatus.Queued)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, failed.Attempts)), cancellationToken).ConfigureAwait(false);
                await queue.EnqueueAsync(key, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Pronunciation clip rendered: lexeme {LexemeId}, voice {Voice}, {Bytes} bytes")]
    private partial void LogRendered(LexemeId lexemeId, string voice, long bytes);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Pronunciation render attempt {Attempt} failed: lexeme {LexemeId}, voice {Voice}, now {Status}")]
    private partial void LogAttemptFailed(Exception exception, LexemeId lexemeId, string voice, int attempt, ClipStatus status);
}
