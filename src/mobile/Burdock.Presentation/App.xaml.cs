namespace Burdock.Presentation;

// Fully qualified: the Burdock.Application namespace shadows Microsoft.Maui.Controls.Application inside Burdock.Presentation.
public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly AppShell _shell;

    public App(AppShell shell)
    {
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState) => new(_shell);
}
