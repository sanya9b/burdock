using System.Text.Json;
using Burdock.Application.Ports;
using Burdock.Domain.Widgets;
using Microsoft.Extensions.Logging;

namespace Burdock.Infrastructure.Widgets;

/// <summary>
/// The App Group hand-off to the widget extension (spec 04 §3.2, NFR-04-2, NFR-04-4). Files are written atomically
/// (temp file + replace), protected until first unlock and excluded from backup; WidgetKit reloads are coalesced. On iOS
/// without the App Group entitlement, and on every other platform, the container is absent and the bridge reports
/// <see cref="IsSupported"/> = false, so the app keeps working without widgets.
/// </summary>
public sealed partial class AppGroupWidgetBridge(IClock clock, ILogger<AppGroupWidgetBridge> logger) : IWidgetBridge
{
    public const string AppGroupId = "group.org.burdock.app";
    public const string TimelineFile = "Timeline/word-timeline.v1.json";
    public const string CursorFile = "Timeline/word-cursor.v1.json";
    public const string MarkerFile = "Timeline/extension-marker.v1.json";

    /// <summary>Widget.ReloadCoalesceSeconds (spec 04 §3.9).</summary>
    public static readonly TimeSpan ReloadCoalesce = TimeSpan.FromSeconds(300);

    private readonly Lazy<string?> _root = new(PlatformContainerRoot);
    private readonly Lock _reloadGate = new();
    private DateTimeOffset _lastReload = DateTimeOffset.MinValue;

    public bool IsSupported => _root.Value is not null;

    public async Task<WordTimeline?> ReadTimelineAsync(CancellationToken cancellationToken)
    {
        var document = await ReadAsync(TimelineFile, WidgetJsonContext.Default.WidgetTimelineDocument, cancellationToken).ConfigureAwait(false);
        return document is null ? null : WidgetTimelineMapper.ToDomain(document);
    }

    public async Task WriteTimelineAsync(WordTimeline timeline, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        var root = _root.Value ?? throw new InvalidOperationException("The App Group container is not available on this platform.");
        var path = Path.Combine(root, TimelineFile);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(WidgetTimelineMapper.ToDocument(timeline, clock.LocalTimeZone), WidgetJsonContext.Default.WidgetTimelineDocument);
        var temp = path + ".tmp";
        await File.WriteAllBytesAsync(temp, bytes, cancellationToken).ConfigureAwait(false);
        File.Move(temp, path, overwrite: true);
        PlatformProtect(path);
    }

    public async Task<WidgetCursor?> ReadCursorAsync(CancellationToken cancellationToken)
    {
        var document = await ReadAsync(CursorFile, WidgetJsonContext.Default.WidgetCursorDocument, cancellationToken).ConfigureAwait(false);
        return document is null ? null : WidgetTimelineMapper.ToDomain(document);
    }

    public async Task<WidgetMarker?> ReadMarkerAsync(CancellationToken cancellationToken)
    {
        var document = await ReadAsync(MarkerFile, WidgetJsonContext.Default.WidgetMarkerDocument, cancellationToken).ConfigureAwait(false);
        return document is null ? null : WidgetTimelineMapper.ToDomain(document);
    }

    public Task RequestReloadAsync(string reason, CancellationToken cancellationToken)
    {
        if (!IsSupported)
        {
            return Task.CompletedTask;
        }

        lock (_reloadGate)
        {
            var now = clock.UtcNow;
            if (now - _lastReload < ReloadCoalesce)
            {
                LogReloadCoalesced(reason);
                return Task.CompletedTask;
            }

            _lastReload = now;
        }

        PlatformReloadTimelines();
        LogReloadRequested(reason);
        return Task.CompletedTask;
    }

    private async Task<T?> ReadAsync<T>(string relativePath, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
        where T : class
    {
        var root = _root.Value;
        if (root is null)
        {
            return null;
        }

        var path = Path.Combine(root, relativePath);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize(bytes, typeInfo);
        }
        catch (JsonException ex)
        {
            LogUnreadable(ex, relativePath);
            return null;
        }
        catch (IOException ex)
        {
            LogUnreadable(ex, relativePath);
            return null;
        }
    }

    private static partial string? PlatformContainerRoot();

    private static partial void PlatformProtect(string path);

    private static partial void PlatformReloadTimelines();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Widget reload requested ({Reason})")]
    private partial void LogReloadRequested(string reason);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Widget reload coalesced ({Reason})")]
    private partial void LogReloadCoalesced(string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "App Group file {File} could not be read")]
    private partial void LogUnreadable(Exception exception, string file);
}

#if IOS
public sealed partial class AppGroupWidgetBridge
{
    private static partial string? PlatformContainerRoot()
        => Foundation.NSFileManager.DefaultManager.GetContainerUrl(AppGroupId)?.Path;

    private static partial void PlatformProtect(string path)
    {
        // NFR-04-4: readable by the extension after a reboot before the first unlock, never in iCloud backups (T12).
        var attributes = new Foundation.NSFileAttributes { ProtectionKey = Foundation.NSFileProtection.CompleteUntilFirstUserAuthentication };
        Foundation.NSFileManager.DefaultManager.SetAttributes(attributes, path, out _);
        using var url = Foundation.NSUrl.FromFilename(path);
        url.SetResource(Foundation.NSUrl.IsExcludedFromBackupKey, Foundation.NSNumber.FromBoolean(true), out _);
    }

    private static partial void PlatformReloadTimelines() => new WidgetKit.WidgetCenterProxy().ReloadAllTimeLines();
}
#else
public sealed partial class AppGroupWidgetBridge
{
    private static partial string? PlatformContainerRoot() => null;

    private static partial void PlatformProtect(string path)
    {
    }

    private static partial void PlatformReloadTimelines()
    {
    }
}
#endif
