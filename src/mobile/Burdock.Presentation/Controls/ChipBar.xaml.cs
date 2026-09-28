using System.Collections;
using System.Windows.Input;

namespace Burdock.Presentation.Controls;

public partial class ChipBar : ContentView
{
    public static readonly BindableProperty ItemsProperty =
        BindableProperty.Create(nameof(Items), typeof(IEnumerable), typeof(ChipBar), null);

    public static readonly BindableProperty SelectCommandProperty =
        BindableProperty.Create(nameof(SelectCommand), typeof(ICommand), typeof(ChipBar), null);

    public static readonly BindableProperty WrapProperty =
        BindableProperty.Create(nameof(Wrap), typeof(bool), typeof(ChipBar), false, propertyChanged: (b, _, _) => ((ChipBar)b).OnPropertyChanged(nameof(Scrolls)));

    public static readonly BindableProperty EdgeInsetProperty =
        BindableProperty.Create(nameof(EdgeInset), typeof(Thickness), typeof(ChipBar), Thickness.Zero);

    public ChipBar()
    {
        InitializeComponent();
    }

    /// <summary>Sequence of <see cref="ViewModels.ChipOption"/>.</summary>
    public IEnumerable? Items { get => (IEnumerable?)GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }

    /// <summary>Receives the tapped <see cref="ViewModels.ChipOption"/>; the owner toggles selection (single or multi).</summary>
    public ICommand? SelectCommand { get => (ICommand?)GetValue(SelectCommandProperty); set => SetValue(SelectCommandProperty, value); }

    /// <summary>Wrap onto several lines (category pickers) instead of scrolling horizontally (filters).</summary>
    public bool Wrap { get => (bool)GetValue(WrapProperty); set => SetValue(WrapProperty, value); }

    /// <summary>Padding of the scrolling row, so chips can bleed to the screen edge and still start at the page inset.</summary>
    public Thickness EdgeInset { get => (Thickness)GetValue(EdgeInsetProperty); set => SetValue(EdgeInsetProperty, value); }

    public bool Scrolls => !Wrap;
}
