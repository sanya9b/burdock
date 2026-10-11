using Burdock.Application.UseCases.Sync;
using Microsoft.Extensions.Logging;

namespace Burdock.Presentation.Services;

/// <summary>Runs the sync at the moments that matter (start, resume, after a session) and never overlaps two passes.</summary>
public interface ISyncCoordinator
{
    /// <summary>Outcome of the last finished pass; null before the first one.</summary>
    SyncReport? LastReport { get; }

    /// <summary>Starts a pass, or marks one more pass when another is already running. Never throws.</summary>
    Task TriggerAsync(string reason, CancellationToken cancellationToken = default);
}

/// <summary>
/// Presentation-side scheduler of <see cref="SyncUseCase"/> (spec 09 section 3.3): the use case decides what to do, this
/// class only decides when. A trigger during a running pass queues exactly one follow-up pass.
/// </summary>
public sealed partial class SyncCoordinator(IServiceProvider services, ILogger<SyncCoordinator> logger) : ISyncCoordinator, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _rerunRequested;

    public SyncReport? LastReport { get; private set; }

    public async Task TriggerAsync(string reason, CancellationToken cancellationToken = default)
    {
        if (!await _gate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            Interlocked.Exchange(ref _rerunRequested, 1);
            return;
        }

        try
        {
            do
            {
                Interlocked.Exchange(ref _rerunRequested, 0);
                LogStarted(reason);
                var report = await services.GetRequiredService<SyncUseCase>().ExecuteAsync(cancellationToken).ConfigureAwait(false);
                LastReport = report;
                LogFinished(reason, report.Online, report.Error ?? "none", report.Progress?.Pushed ?? 0, report.Progress?.Applied ?? 0, report.Content?.PacksImported ?? 0);
            }
            while (Interlocked.CompareExchange(ref _rerunRequested, 0, 1) == 1);
        }
        catch (OperationCanceledException)
        {
            // The app is going away; the next start syncs again.
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogFailed(ex, reason);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Sync pass started ({Reason})")]
    private partial void LogStarted(string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sync pass finished ({Reason}): online={Online} error={Error} pushed={Pushed} applied={Applied} packs={Packs}")]
    private partial void LogFinished(string reason, bool online, string error, int pushed, int applied, int packs);

    [LoggerMessage(Level = LogLevel.Error, Message = "Sync pass crashed ({Reason})")]
    private partial void LogFailed(Exception exception, string reason);
}
