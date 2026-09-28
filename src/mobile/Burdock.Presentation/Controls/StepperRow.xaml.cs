using System.Globalization;
using System.Windows.Input;

namespace Burdock.Presentation.Controls;

public partial class StepperRow : ContentView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(StepperRow), string.Empty);

    public static readonly BindableProperty ValueProperty =
        BindableProperty.Create(nameof(Value), typeof(int), typeof(StepperRow), 0, BindingMode.TwoWay, propertyChanged: (b, _, _) => ((StepperRow)b).OnValueChanged());

    public static readonly BindableProperty MinimumProperty =
        BindableProperty.Create(nameof(Minimum), typeof(int), typeof(StepperRow), 0, propertyChanged: (b, _, _) => ((StepperRow)b).OnValueChanged());

    public static readonly BindableProperty MaximumProperty =
        BindableProperty.Create(nameof(Maximum), typeof(int), typeof(StepperRow), 100, propertyChanged: (b, _, _) => ((StepperRow)b).OnValueChanged());

    public static readonly BindableProperty StepProperty =
        BindableProperty.Create(nameof(Step), typeof(int), typeof(StepperRow), 1);

    /// <summary>Optional composite format for the value ("{0} хв"); plain number when empty.</summary>
    public static readonly BindableProperty ValueFormatProperty =
        BindableProperty.Create(nameof(ValueFormat), typeof(string), typeof(StepperRow), string.Empty, propertyChanged: (b, _, _) => ((StepperRow)b).OnValueChanged());

    private readonly Command _increment;
    private readonly Command _decrement;

    public StepperRow()
    {
        _increment = new Command(() => Value = Math.Min(Maximum, Value + Step), () => Value < Maximum);
        _decrement = new Command(() => Value = Math.Max(Minimum, Value - Step), () => Value > Minimum);
        InitializeComponent();
    }

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public int Value { get => (int)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    public int Minimum { get => (int)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }

    public int Maximum { get => (int)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    public int Step { get => (int)GetValue(StepProperty); set => SetValue(StepProperty, value); }

    public string ValueFormat { get => (string)GetValue(ValueFormatProperty); set => SetValue(ValueFormatProperty, value); }

    public ICommand IncrementCommand => _increment;

    public ICommand DecrementCommand => _decrement;

    public string ValueText => string.IsNullOrEmpty(ValueFormat)
        ? Value.ToString(CultureInfo.CurrentCulture)
        : string.Format(CultureInfo.CurrentCulture, ValueFormat, Value);

    private void OnValueChanged()
    {
        OnPropertyChanged(nameof(ValueText));
        _increment.ChangeCanExecute();
        _decrement.ChangeCanExecute();
    }
}
