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

    // The Shell is resolved here, after InitializeComponent merged the token dictionaries: AppShell.xaml uses StaticResource for its tab icons.
    protected override Window CreateWindow(IActivationState? activationState) => new(_services.GetRequiredService<AppShell>());
}
