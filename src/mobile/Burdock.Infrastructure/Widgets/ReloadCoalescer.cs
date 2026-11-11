using Burdock.Application.Ports;

namespace Burdock.Infrastructure.Widgets;

/// <summary>
/// Coalesces WidgetKit reload requests into at most one call per <see cref="Window"/> (spec 04 §3.3.2, Widget.ReloadCoalesceSeconds)
/// without losing any: the first request reloads at once, later ones inside the window are deferred to the end of it, so a
/// timeline rewritten shortly after a reload still reaches the widget instead of waiting for the next trigger.
/// </summary>
public sealed class ReloadCoalescer(IClock clock, TimeSpan window, Action reload, Func<TimeSpan, Task>? delay = null)
{
    private readonly Func<TimeSpan, Task> _delay = delay ?? (d => Task.Delay(d));
    private readonly Lock _gate = new();
    private DateTimeOffset _lastReload = DateTimeOffset.MinValue;
    private bool _deferred;

    public TimeSpan Window { get; } = window;

    /// <summary>Reloads now when the window has passed; otherwise schedules one reload for the end of the window. Returns true when the reload ran immediately.</summary>
    public bool Request()
    {
        TimeSpan wait;
        lock (_gate)
        {
            var now = clock.UtcNow;
            var elapsed = now - _lastReload;
            if (elapsed >= Window)
            {
                _lastReload = now;
                _deferred = false;
            }
            else
            {
                if (_deferred)
                {
                    return false;
                }

                _deferred = true;
                wait = Window - elapsed;
                _ = FlushLaterAsync(wait);
                return false;
            }
        }

        reload();
        return true;
    }

    private async Task FlushLaterAsync(TimeSpan wait)
    {
        await _delay(wait).ConfigureAwait(false);
        lock (_gate)
        {
            if (!_deferred)
            {
                return;
            }

            _deferred = false;
            _lastReload = clock.UtcNow;
        }

        reload();
    }
}
