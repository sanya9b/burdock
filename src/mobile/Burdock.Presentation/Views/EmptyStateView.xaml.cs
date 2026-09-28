namespace Burdock.Presentation.Views;

public partial class EmptyStateView : ContentView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(EmptyStateView), string.Empty);

    public static readonly BindableProperty BodyProperty =
        BindableProperty.Create(nameof(Body), typeof(string), typeof(EmptyStateView), string.Empty);

    public static readonly BindableProperty MascotProperty =
        BindableProperty.Create(nameof(Mascot), typeof(string), typeof(EmptyStateView), string.Empty, propertyChanged: (b, _, _) => ((EmptyStateView)b).OnPropertyChanged(nameof(HasMascot)));

    public static readonly BindableProperty MascotSizeProperty =
        BindableProperty.Create(nameof(MascotSize), typeof(double), typeof(EmptyStateView), 160d);

    public EmptyStateView()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Body
    {
        get => (string)GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }

    /// <summary>Mascot image name (mascot_cheer.png, mascot_think.png, ...; spec 02 section 3.10). Empty hides the illustration.</summary>
    public string Mascot
    {
        get => (string)GetValue(MascotProperty);
        set => SetValue(MascotProperty, value);
    }

    /// <summary>Rendered size in pt (size.mascot.*; lg = 160 for full-screen states, md = 96 inside lists).</summary>
    public double MascotSize
    {
        get => (double)GetValue(MascotSizeProperty);
        set => SetValue(MascotSizeProperty, value);
    }

    public bool HasMascot => !string.IsNullOrEmpty(Mascot);
}
