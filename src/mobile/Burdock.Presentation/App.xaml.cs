namespace Burdock.Presentation;

// Fully qualified: the Burdock.Application namespace shadows Microsoft.Maui.Controls.Application inside Burdock.Presentation.
public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        InitializeComponent();
    }

    // S-01: the window opens on the animated bootstrap page, which hands over to the Shell once the content is ready.
    // Resolving pages here (after InitializeComponent) matters: their XAML uses StaticResource from the merged token dictionaries.
    protected override Window CreateWindow(IActivationState? activationState)
    {
#if !ANDROID
        Services.SystemSplash.MarkDismissed();
#endif
        var theme = _services.GetRequiredService<Services.IAppThemeService>();
        theme.Apply(theme.Mode);
        var window = new Window(_services.GetRequiredService<Views.BootstrapPage>());
        var sync = _services.GetRequiredService<Services.ISyncCoordinator>();
        var widgets = _services.GetRequiredService<Services.IWidgetCoordinator>();
        window.Resumed += (_, _) =>
        {
            _ = widgets.TriggerAsync("resume");
            _ = sync.TriggerAsync("resume");
        };
        return window;
    }
}
