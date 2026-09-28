using System.Globalization;
using System.Text;

namespace Burdock.Presentation.Localization;

/// <summary>Ukrainian plural forms (spec 03 section 3.8.5): 1 відповідь · 2-4 відповіді · 5+ відповідей; 11-14 always take the «many» form.</summary>
internal static class Plural
{
    private static readonly CultureInfo Ukrainian = CultureInfo.GetCultureInfo("uk-UA");

    public static string Uk(int count, CompositeFormat one, CompositeFormat few, CompositeFormat many)
    {
        ArgumentNullException.ThrowIfNull(one);
        ArgumentNullException.ThrowIfNull(few);
        ArgumentNullException.ThrowIfNull(many);
        var n = Math.Abs(count);
        var mod10 = n % 10;
        var mod100 = n % 100;
        var format = mod10 == 1 && mod100 != 11
            ? one
            : mod10 is >= 2 and <= 4 && mod100 is < 12 or > 14
                ? few
                : many;
        return string.Format(Ukrainian, format, count);
    }
}
