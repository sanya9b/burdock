using Microsoft.Extensions.Logging;

namespace Burdock.Presentation.Navigation;

/// <summary>Receives platform URLs (widgets, notifications) and turns them into Shell navigation once the Shell exists (spec 04 §3.4).</summary>
public interface IDeepLinkRouter
{
    /// <summary>The last accepted link that has not been dispatched yet.</summary>
    DeepLink? Pending { get; }

    /// <summary>Called by the platform head with the raw URL; unknown URLs are ignored. Never throws.</summary>
    void Accept(Uri? uri);

    /// <summary>Called when the Shell is on screen: dispatches the pending link and makes later links dispatch immediately.</summary>
    Task FlushAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Cold starts deliver the URL before the Shell is created (the bootstrap page or the onboarding is showing), so the link waits
/// until <see cref="FlushAsync"/>; warm starts dispatch at once. The word card records the confirmed exposure itself from the
/// <c>surface</c> parameter, so one tap yields exactly one exposure.
/// </summary>
public sealed partial class DeepLinkRouter(INavigationService navigation, ILogger<DeepLinkRouter> logger) : IDeepLinkRouter
{
    private readonly Lock _gate = new();
    private DeepLink? _pending;
    private bool _ready;

    public DeepLink? Pending
    {
        get
        {
            lock (_gate)
            {
                return _pending;
            }
        }
    }

    public void Accept(Uri? uri)
    {
        if (!DeepLink.TryParse(uri, out var link))
        {
            LogIgnored(uri?.Scheme ?? "null");
            return;
        }

        bool dispatchNow;
        lock (_gate)
        {
            _pending = link;
            dispatchNow = _ready;
        }

        LogAccepted(link.Kind, link.Source);
        if (dispatchNow)
        {
            _ = FlushAsync();
        }
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        DeepLink? link;
        lock (_gate)
        {
            _ready = true;
            link = _pending;
            _pending = null;
        }

        if (link is null)
        {
            return;
        }

        try
        {
            var (route, parameters) = Destination(link);
            await navigation.GoToAsync(route, parameters, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The app is going away; nothing to recover.
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogFailed(ex, link.Kind);
        }
    }

    /// <summary>Where a link goes (spec 03 deep-link table): the word card on «Сьогодні», a 5-word session, or the tab itself.</summary>
    public static (Route Route, IReadOnlyDictionary<string, object>? Parameters) Destination(DeepLink link)
    {
        ArgumentNullException.ThrowIfNull(link);
        return link.Kind switch
        {
            DeepLinkKind.Word => (Routes.Today.Word, new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["id"] = link.LexemeId!.Value.ToString("D"),
                ["surface"] = link.Surface.ToString(),
            }),
            DeepLinkKind.Quiz => (Routes.Learn.Session, new Dictionary<string, object>(StringComparer.Ordinal) { ["size"] = "5" }),
            _ => (Routes.Today.Root, null),
        };
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Deep link ignored (scheme {Scheme})")]
    private partial void LogIgnored(string scheme);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deep link accepted: {Kind} from {Source}")]
    private partial void LogAccepted(DeepLinkKind kind, string source);

    [LoggerMessage(Level = LogLevel.Error, Message = "Deep link {Kind} could not be dispatched")]
    private partial void LogFailed(Exception exception, DeepLinkKind kind);
}
