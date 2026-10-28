using Burdock.Domain.Widgets;

namespace Burdock.Application.Ports;

/// <summary>What the «Далі» intent wrote last (spec 04 §3.2.3): which entry is current now and when the learner advanced.</summary>
public sealed record WidgetCursor(int CursorIndex, DateTimeOffset AdvancedAt, string Source);

/// <summary>What the extension wrote when it last asked for the timeline (spec 04 §3.2.1): proof that a widget is installed and which families are on screen.</summary>
public sealed record WidgetMarker(DateTimeOffset RequestedAt, IReadOnlyList<string> Families);

/// <summary>
/// The app's side of the App Group hand-off to the widget extension (spec 04 §3.2, FR-04-3). The adapter owns the files,
/// atomic writes and WidgetKit reloads; the use case only decides what the timeline is. Unsupported on platforms without
/// widgets (and on iOS until the App Group entitlement is present).
/// </summary>
public interface IWidgetBridge
{
    bool IsSupported { get; }

    Task<WordTimeline?> ReadTimelineAsync(CancellationToken cancellationToken);

    Task WriteTimelineAsync(WordTimeline timeline, CancellationToken cancellationToken);

    Task<WidgetCursor?> ReadCursorAsync(CancellationToken cancellationToken);

    Task<WidgetMarker?> ReadMarkerAsync(CancellationToken cancellationToken);

    /// <summary>Asks WidgetKit to reload; the adapter coalesces calls within <c>Widget.ReloadCoalesceSeconds</c> (NFR-04-2).</summary>
    Task RequestReloadAsync(string reason, CancellationToken cancellationToken);
}
