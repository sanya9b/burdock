using System.Collections;

namespace Burdock.Presentation.Views;

/// <summary>Seven login-calendar tiles with a caption (spec 02 `DailyCalendarTile`, spec 07 §3.4), shared by «Сьогодні» and «Нагороди».</summary>
public partial class CalendarStripView : ContentView
{
    public static readonly BindableProperty TilesProperty = BindableProperty.Create(nameof(Tiles), typeof(IEnumerable), typeof(CalendarStripView));

    public static readonly BindableProperty CaptionProperty = BindableProperty.Create(nameof(Caption), typeof(string), typeof(CalendarStripView), string.Empty);

    public CalendarStripView()
    {
        InitializeComponent();
    }

    public IEnumerable? Tiles { get => (IEnumerable?)GetValue(TilesProperty); set => SetValue(TilesProperty, value); }

    public string Caption { get => (string)GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }
}
