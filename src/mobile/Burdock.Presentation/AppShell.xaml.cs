using Burdock.Presentation.Views;

namespace Burdock.Presentation;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Detail routes pushed on top of a tab (Navigation/Routes.cs is the only other place that knows these strings).
        Routing.RegisterRoute("word", typeof(WordCardPage));
        Routing.RegisterRoute("session", typeof(SessionPage));
        Routing.RegisterRoute("summary", typeof(SessionSummaryPage));
    }
}
