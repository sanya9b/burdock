using System.Globalization;

namespace Burdock.Presentation.Converters;

/// <summary>Colour of the status dot in the library list (spec 03 S-14) for a <c>LibraryStatus</c> name; the status text next to it carries the meaning.</summary>
public sealed class StatusColorConverter : IValueConverter
{
    private static readonly IReadOnlyDictionary<string, string> Keys = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["New"] = "ColorBorderStrong",
        ["Introduced"] = "ColorFeedbackInfo",
        ["Learning"] = "ColorBrandPrimaryVivid",
        ["Review"] = "ColorStreakActive",
        ["Learned"] = "ColorFeedbackSuccess",
        ["Suspended"] = "ColorTextDisabled",
    };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var app = Microsoft.Maui.Controls.Application.Current;
        if (app is null)
        {
            return Colors.Transparent;
        }

        var theme = app.RequestedTheme == AppTheme.Dark ? "Dark" : "Light";
        var key = Keys.GetValueOrDefault(value?.ToString() ?? string.Empty, "ColorBorderStrong") + theme;
        return app.Resources.TryGetValue(key, out var found) && found is Color color ? color : Colors.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
