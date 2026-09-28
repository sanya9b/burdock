using System.Globalization;

namespace Burdock.Presentation.Converters;

/// <summary>
/// Maps a topic key ("travel", "food", ...) to its tonal container colour (color.semantic.*.topic) for the current theme.
/// ConverterParameter "on" returns the matching text colour. Unknown topics fall back to the brand container.
/// </summary>
public sealed class TopicColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var on = string.Equals(parameter as string, "on", StringComparison.OrdinalIgnoreCase);
        var app = Microsoft.Maui.Controls.Application.Current;
        if (app is null)
        {
            return Colors.Transparent;
        }

        var theme = app.RequestedTheme == AppTheme.Dark ? "Dark" : "Light";
        if (value is string topic && topic.Length > 0)
        {
            var key = (on ? "ColorTopicOn" : "ColorTopic") + char.ToUpperInvariant(topic[0]) + topic[1..] + theme;
            if (app.Resources.TryGetValue(key, out var found) && found is Color color)
            {
                return color;
            }
        }

        var fallback = (on ? "ColorBrandOnPrimaryContainer" : "ColorBrandPrimaryContainer") + theme;
        return app.Resources.TryGetValue(fallback, out var brand) && brand is Color brandColor ? brandColor : Colors.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
