using System.Windows.Input;
using Microsoft.Maui.Controls.Shapes;
using LucideIcon = Burdock.Presentation.Resources.Icons.Icon;

namespace Burdock.Presentation.Controls;

/// <summary>
/// Relief button of the design system (spec 02 section 3.11 «Button», ADR 0017): a coloured face resting on a darker
/// edge; pressing drops the face onto the edge, releasing lifts it back. Colours, radius and fonts come from styles
/// (implicit style = primary; PushTonal, PushAccent, PushNeutral, PushSuccess, PushDanger, PushIcon, QuizOption).
/// </summary>
public sealed class PushButton : ContentView
{
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(PushButton), string.Empty, propertyChanged: OnContentChanged);

    public static readonly BindableProperty IconProperty =
        BindableProperty.Create(nameof(Icon), typeof(string), typeof(PushButton), string.Empty, propertyChanged: OnContentChanged);

    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(PushButton), null, propertyChanged: OnCommandChanged);

    public static readonly BindableProperty CommandParameterProperty =
        BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(PushButton), null, propertyChanged: (b, _, _) => ((PushButton)b).RefreshCanExecute());

    public static readonly BindableProperty FaceColorProperty =
        BindableProperty.Create(nameof(FaceColor), typeof(Color), typeof(PushButton), Colors.Transparent, propertyChanged: OnLookChanged);

    public static readonly BindableProperty EdgeColorProperty =
        BindableProperty.Create(nameof(EdgeColor), typeof(Color), typeof(PushButton), Colors.Transparent, propertyChanged: OnLookChanged);

    public static readonly BindableProperty TextColorProperty =
        BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(PushButton), Colors.Black, propertyChanged: OnLookChanged);

    public static readonly BindableProperty EdgeHeightProperty =
        BindableProperty.Create(nameof(EdgeHeight), typeof(double), typeof(PushButton), 3d, propertyChanged: OnLookChanged);

    public static readonly BindableProperty CornerRadiusProperty =
        BindableProperty.Create(nameof(CornerRadius), typeof(double), typeof(PushButton), 999d, propertyChanged: OnLookChanged);

    public static readonly BindableProperty FontFamilyProperty =
        BindableProperty.Create(nameof(FontFamily), typeof(string), typeof(PushButton), "RubikBold", propertyChanged: OnLookChanged);

    public static readonly BindableProperty FontSizeProperty =
        BindableProperty.Create(nameof(FontSize), typeof(double), typeof(PushButton), 16d, propertyChanged: OnLookChanged);

    public static readonly BindableProperty IconSizeProperty =
        BindableProperty.Create(nameof(IconSize), typeof(double), typeof(PushButton), 20d, propertyChanged: OnLookChanged);

    public static readonly BindableProperty IconSpacingProperty =
        BindableProperty.Create(nameof(IconSpacing), typeof(double), typeof(PushButton), 8d, propertyChanged: OnLookChanged);

    public static readonly BindableProperty ContentPaddingProperty =
        BindableProperty.Create(nameof(ContentPadding), typeof(Thickness), typeof(PushButton), new Thickness(20, 0), propertyChanged: OnLookChanged);

    public static readonly BindableProperty DisabledOpacityProperty =
        BindableProperty.Create(nameof(DisabledOpacity), typeof(double), typeof(PushButton), 0.38d, propertyChanged: OnLookChanged);

    private readonly Border _edge;
    private readonly Border _face;
    private readonly Label _iconLabel;
    private readonly Label _textLabel;
    private readonly HorizontalStackLayout _content;
    private bool _pointerEventsSeen;

    public PushButton()
    {
        _iconLabel = new Label
        {
            FontFamily = LucideIcon.FontFamily,
            FontAutoScalingEnabled = false,
            VerticalTextAlignment = TextAlignment.Center,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalOptions = LayoutOptions.Center,
            IsVisible = false,
        };
        _textLabel = new Label
        {
            VerticalTextAlignment = TextAlignment.Center,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalOptions = LayoutOptions.Center,
            LineBreakMode = LineBreakMode.WordWrap,
            MaxLines = 2,
            IsVisible = false,
        };
        AutomationProperties.SetIsInAccessibleTree(_iconLabel, false);
        AutomationProperties.SetIsInAccessibleTree(_textLabel, false);

        _content = new HorizontalStackLayout
        {
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Children = { _iconLabel, _textLabel },
        };
        _edge = new Border { StrokeThickness = 0 };
        _face = new Border { StrokeThickness = 0, Content = _content };
        Content = new Grid { Children = { _edge, _face } };

        var pointer = new PointerGestureRecognizer();
        pointer.PointerPressed += (_, _) => { _pointerEventsSeen = true; SetPressed(true); };
        pointer.PointerReleased += (_, _) => SetPressed(false);
        pointer.PointerExited += (_, _) => SetPressed(false);
        GestureRecognizers.Add(pointer);

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => _ = HandleTapAsync();
        GestureRecognizers.Add(tap);

        ApplyLook();
        ApplyContent();
    }

    /// <summary>Raised after the command (if any) ran; for code-behind wiring.</summary>
    public event EventHandler? Clicked;

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>Lucide glyph (see <see cref="LucideIcon"/>); with an empty <see cref="Text"/> the button becomes a circle.</summary>
    public string Icon { get => (string)GetValue(IconProperty); set => SetValue(IconProperty, value); }

    public ICommand? Command { get => (ICommand?)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }

    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }

    public Color FaceColor { get => (Color)GetValue(FaceColorProperty); set => SetValue(FaceColorProperty, value); }

    public Color EdgeColor { get => (Color)GetValue(EdgeColorProperty); set => SetValue(EdgeColorProperty, value); }

    public Color TextColor { get => (Color)GetValue(TextColorProperty); set => SetValue(TextColorProperty, value); }

    public double EdgeHeight { get => (double)GetValue(EdgeHeightProperty); set => SetValue(EdgeHeightProperty, value); }

    public double CornerRadius { get => (double)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    public string FontFamily { get => (string)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    public double IconSize { get => (double)GetValue(IconSizeProperty); set => SetValue(IconSizeProperty, value); }

    public double IconSpacing { get => (double)GetValue(IconSpacingProperty); set => SetValue(IconSpacingProperty, value); }

    public Thickness ContentPadding { get => (Thickness)GetValue(ContentPaddingProperty); set => SetValue(ContentPaddingProperty, value); }

    public double DisabledOpacity { get => (double)GetValue(DisabledOpacityProperty); set => SetValue(DisabledOpacityProperty, value); }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == IsEnabledProperty.PropertyName)
        {
            Opacity = IsEnabled ? 1 : DisabledOpacity;
            if (!IsEnabled)
            {
                SetPressed(false);
            }
        }
        else if (propertyName == HeightRequestProperty.PropertyName)
        {
            ApplyContent();
        }
    }

    private static void OnContentChanged(BindableObject bindable, object oldValue, object newValue) => ((PushButton)bindable).ApplyContent();

    private static void OnLookChanged(BindableObject bindable, object oldValue, object newValue) => ((PushButton)bindable).ApplyLook();

    private static void OnCommandChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var self = (PushButton)bindable;
        if (oldValue is ICommand old)
        {
            old.CanExecuteChanged -= self.OnCanExecuteChanged;
        }

        if (newValue is ICommand added)
        {
            added.CanExecuteChanged += self.OnCanExecuteChanged;
        }

        self.RefreshCanExecute();
    }

    private void OnCanExecuteChanged(object? sender, EventArgs e) => RefreshCanExecute();

    private void RefreshCanExecute()
    {
        if (Command is { } command)
        {
            IsEnabled = command.CanExecute(CommandParameter);
        }
    }

    private void ApplyLook()
    {
        if (_face is null)
        {
            // Implicit styles are applied from the base constructor, before our visual tree exists; the constructor calls ApplyLook again.
            return;
        }

        _edge.StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(CornerRadius) };
        _face.StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(CornerRadius) };
        _edge.BackgroundColor = EdgeColor;
        _face.BackgroundColor = FaceColor;
        _edge.Margin = new Thickness(0, EdgeHeight, 0, 0);
        _face.Margin = new Thickness(0, 0, 0, EdgeHeight);
        _textLabel.TextColor = TextColor;
        _textLabel.FontFamily = FontFamily;
        _textLabel.FontSize = FontSize;
        _iconLabel.TextColor = TextColor;
        _iconLabel.FontSize = IconSize;
        _content.Spacing = IconSpacing;
        _content.Padding = string.IsNullOrEmpty(Text) ? Thickness.Zero : ContentPadding;
        Opacity = IsEnabled ? 1 : DisabledOpacity;
    }

    private void ApplyContent()
    {
        if (_textLabel is null)
        {
            return;
        }

        _iconLabel.Text = Icon;
        _iconLabel.IsVisible = !string.IsNullOrEmpty(Icon);
        _textLabel.Text = Text;
        _textLabel.IsVisible = !string.IsNullOrEmpty(Text);
        _content.Padding = string.IsNullOrEmpty(Text) ? Thickness.Zero : ContentPadding;

        // Icon-only buttons are circles: width follows the requested height.
        WidthRequest = string.IsNullOrEmpty(Text) && HeightRequest > 0 ? HeightRequest : -1;

        if (!string.IsNullOrEmpty(Text) && string.IsNullOrEmpty(SemanticProperties.GetDescription(this)))
        {
            SemanticProperties.SetDescription(this, Text);
        }
    }

    private void SetPressed(bool pressed)
    {
        _face.TranslationY = pressed && IsEnabled ? EdgeHeight : 0;
    }

    private async Task HandleTapAsync()
    {
        if (!IsEnabled)
        {
            return;
        }

        if (!_pointerEventsSeen)
        {
            // Platforms without pointer press/release still get the relief feedback.
            _face.TranslationY = EdgeHeight;
            await Task.Delay(70).ConfigureAwait(true);
            _face.TranslationY = 0;
        }

        if (Command is { } command && command.CanExecute(CommandParameter))
        {
            command.Execute(CommandParameter);
        }

        Clicked?.Invoke(this, EventArgs.Empty);
    }
}
