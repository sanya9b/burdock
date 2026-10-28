using Burdock.Application.Ports;
using Burdock.Domain.Widgets;

namespace Burdock.TestKit.Fakes;

/// <summary>In-memory App Group: the timeline, cursor and marker files plus every reload request.</summary>
public sealed class FakeWidgetBridge : IWidgetBridge
{
    public bool IsSupported { get; set; } = true;

    public WordTimeline? Timeline { get; set; }

    public WidgetCursor? Cursor { get; set; }

    public WidgetMarker? Marker { get; set; }

    public int Writes { get; private set; }

    public List<string> Reloads { get; } = [];

    public Task<WordTimeline?> ReadTimelineAsync(CancellationToken cancellationToken) => Task.FromResult(Timeline);

    public Task WriteTimelineAsync(WordTimeline timeline, CancellationToken cancellationToken)
    {
        Timeline = timeline;
        Writes++;
        return Task.CompletedTask;
    }

    public Task<WidgetCursor?> ReadCursorAsync(CancellationToken cancellationToken) => Task.FromResult(Cursor);

    public Task<WidgetMarker?> ReadMarkerAsync(CancellationToken cancellationToken) => Task.FromResult(Marker);

    public Task RequestReloadAsync(string reason, CancellationToken cancellationToken)
    {
        Reloads.Add(reason);
        return Task.CompletedTask;
    }
}
