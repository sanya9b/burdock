using Microsoft.Extensions.DependencyInjection;

namespace Burdock.Presentation;

// Fully qualified: the Burdock.Application namespace shadows Microsoft.Maui.Controls.Application inside Burdock.Presentation.
public partial class App : Microsoft.Maui.Controls.Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}