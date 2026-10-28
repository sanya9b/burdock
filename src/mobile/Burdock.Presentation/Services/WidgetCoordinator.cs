using Burdock.Application.UseCases.Widgets;
using Microsoft.Extensions.Logging;

namespace Burdock.Presentation.Services;

/// <summary>Re-plans the widget timeline at the moments spec 04 §3.3.2 lists (start, resume, settings, after a session) without overlapping passes.</summary>
public interface IWidgetCoordinator
{
    WidgetPlanReport? LastReport { get; }

    /// <summary>Starts a pass, or marks one more pass when another is running. Never throws.</summary>
    Task TriggerAsync(string reason, CancellationToken cancellationToken = default);
}

/// <summary>Presentation-side scheduler of <see cref="PlanWidgetTimelineUseCase"/>; the use case decides what, this class only when.</summary>
public sealed partial class WidgetCoordinator(IServiceProvider services, ILogger<WidgetCoordinator> logger) : IWidgetCoordinator, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _rerunRequested;

    public WidgetPlanReport? LastReport { get; private set; }

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
                var report = await services.GetRequiredService<PlanWidgetTimelineUseCase>().ExecuteAsync(reason, cancellationToken).ConfigureAwait(false);
                LastReport = report;
                if (report.Supported)
                {
                    LogFinished(reason, report.Written, report.Entries, report.ScheduledSettled, report.ConfirmedSettled);
                }
            }
            while (Interlocked.CompareExchange(ref _rerunRequested, 0, 1) == 1);
        }
        catch (OperationCanceledException)
        {
            // The app is going away; the next start plans again.
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Widget pass finished ({Reason}): written={Written} entries={Entries} scheduled={Scheduled} confirmed={Confirmed}")]
    private partial void LogFinished(string reason, bool written, int entries, int scheduled, int confirmed);

    [LoggerMessage(Level = LogLevel.Error, Message = "Widget pass crashed ({Reason})")]
    private partial void LogFailed(Exception exception, string reason);
}
