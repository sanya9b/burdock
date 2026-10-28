using System.Diagnostics.CodeAnalysis;
using Burdock.Domain.Exposure;

namespace Burdock.Presentation.Navigation;

public enum DeepLinkKind
{
    /// <summary><c>burdock://word/{lexemeId}</c>: the word card.</summary>
    Word,

    /// <summary><c>burdock://quiz</c>: a short session.</summary>
    Quiz,

    /// <summary><c>burdock://today</c> or anything else under the scheme: the «Сьогодні» tab.</summary>
    Today,
}

/// <summary>
/// A parsed <c>burdock://</c> link (spec 04 §3.4, spec 02 §3.12). <see cref="Source"/> is the raw <c>src</c> value
/// (e.g. <c>widget.small</c>), <see cref="Surface"/> the exposure surface it maps onto for <c>exposureConfirmed</c>.
/// </summary>
public sealed record DeepLink(DeepLinkKind Kind, Guid? LexemeId, string Source, ExposureSurface Surface, bool AdvanceCursor)
{
    public const string Scheme = "burdock";

    public static bool TryParse(Uri? uri, [NotNullWhen(true)] out DeepLink? link)
    {
        link = null;
        if (uri is null || !uri.IsAbsoluteUri || !string.Equals(uri.Scheme, Scheme, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var query = ParseQuery(uri.Query);
        var source = query.GetValueOrDefault("src", string.Empty);
        var surface = SurfaceFor(source);
        var advance = string.Equals(query.GetValueOrDefault("action"), "next", StringComparison.OrdinalIgnoreCase);
        var host = uri.Host;
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (string.Equals(host, "word", StringComparison.OrdinalIgnoreCase))
        {
            if (segments.Length == 0 || !Guid.TryParse(segments[0], out var id))
            {
                return false;
            }

            link = new DeepLink(DeepLinkKind.Word, id, source, surface, advance);
            return true;
        }

        if (string.Equals(host, "quiz", StringComparison.OrdinalIgnoreCase))
        {
            link = new DeepLink(DeepLinkKind.Quiz, null, source, surface, advance);
            return true;
        }

        link = new DeepLink(DeepLinkKind.Today, null, source, surface, advance);
        return true;
    }

    /// <summary>Spec 04 §3.4: <c>widget.accessory*</c> is the lock screen, <c>widget.standby</c> StandBy, every other widget the home screen.</summary>
    public static ExposureSurface SurfaceFor(string source)
    {
        if (source.StartsWith("widget.accessory", StringComparison.OrdinalIgnoreCase) || string.Equals(source, "lockscreen", StringComparison.OrdinalIgnoreCase))
        {
            return ExposureSurface.WidgetLock;
        }

        if (string.Equals(source, "widget.standby", StringComparison.OrdinalIgnoreCase))
        {
            return ExposureSurface.StandBy;
        }

        if (source.StartsWith("widget", StringComparison.OrdinalIgnoreCase))
        {
            return ExposureSurface.WidgetHome;
        }

        if (source.StartsWith("notification", StringComparison.OrdinalIgnoreCase))
        {
            return ExposureSurface.Notification;
        }

        if (source.StartsWith("liveactivity", StringComparison.OrdinalIgnoreCase))
        {
            return ExposureSurface.LiveActivity;
        }

        return ExposureSurface.InApp;
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=', StringComparison.Ordinal);
            var key = Uri.UnescapeDataString(separator < 0 ? pair : pair[..separator]);
            var value = separator < 0 ? string.Empty : Uri.UnescapeDataString(pair[(separator + 1)..]);
            result[key] = value;
        }

        return result;
    }
}
