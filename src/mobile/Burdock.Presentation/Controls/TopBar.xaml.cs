using System.Windows.Input;

namespace Burdock.Presentation.Controls;

public partial class TopBar : ContentView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(TopBar), string.Empty);

    public static readonly BindableProperty LeadingIconProperty =
        BindableProperty.Create(nameof(LeadingIcon), typeof(string), typeof(TopBar), string.Empty, propertyChanged: (b, _, _) => ((TopBar)b).OnPropertyChanged(nameof(HasLeading)));

    public static readonly BindableProperty LeadingDescriptionProperty =
        BindableProperty.Create(nameof(LeadingDescription), typeof(string), typeof(TopBar), string.Empty);

    public static readonly BindableProperty LeadingCommandProperty =
        BindableProperty.Create(nameof(LeadingCommand), typeof(ICommand), typeof(TopBar), null);

    public static readonly BindableProperty TrailingProperty =
        BindableProperty.Create(nameof(Trailing), typeof(View), typeof(TopBar), null);

    public TopBar()
    {
        // Default leading action: pop the current route. Pages override with their own command when they need confirmation.
        LeadingCommand = new Command(async () => await Shell.Current.GoToAsync("..").ConfigureAwait(true));
        InitializeComponent();
    }

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>Lucide glyph for the leading button; empty hides it.</summary>
    public string LeadingIcon { get => (string)GetValue(LeadingIconProperty); set => SetValue(LeadingIconProperty, value); }

    public string LeadingDescription { get => (string)GetValue(LeadingDescriptionProperty); set => SetValue(LeadingDescriptionProperty, value); }

    public ICommand? LeadingCommand { get => (ICommand?)GetValue(LeadingCommandProperty); set => SetValue(LeadingCommandProperty, value); }

    public View? Trailing { get => (View?)GetValue(TrailingProperty); set => SetValue(TrailingProperty, value); }

    public bool HasLeading => !string.IsNullOrEmpty(LeadingIcon);
}
