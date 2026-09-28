using System.Globalization;

namespace Burdock.Presentation.Converters;

/// <summary>
/// Hero words are set in Unbounded, which is wide: long lemmas step down from word.hero (≤ 9 letters) to headline.lg (≤ 12) and headline.md,
/// so «appointment» or «understanding» fit a 360 pt screen without truncation. Sizes come from the FontSize* token resources.
/// </summary>
public sealed class LemmaFontSizeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var length = (value as string)?.Length ?? 0;
        var key = length switch
        {
            <= 9 => "FontSizeWordHero",
            <= 12 => "FontSizeHeadlineLg",
            _ => "FontSizeHeadlineMd",
        };
        var app = Microsoft.Maui.Controls.Application.Current;
        return app is not null && app.Resources.TryGetValue(key, out var size) && size is double d ? d : 24d;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
