using Burdock.Presentation.Services;
using Burdock.Presentation.Views;

namespace Burdock.Presentation;

public partial class AppShell : Shell
{
    private readonly ISyncCoordinator _sync;
    private bool _syncStarted;

    public AppShell(ISyncCoordinator sync)
    {
        _sync = sync ?? throw new ArgumentNullException(nameof(sync));
        InitializeComponent();

        // Detail routes pushed on top of a tab (Navigation/Routes.cs is the only other place that knows these strings).
        Routing.RegisterRoute("word", typeof(WordCardPage));
        Routing.RegisterRoute("session", typeof(SessionPage));
        Routing.RegisterRoute("summary", typeof(SessionSummaryPage));
        Routing.RegisterRoute("settings", typeof(SettingsPage));
    }

    // First sync once the main UI is on screen: account, progress, settings, content (spec 09 section 3.3); later passes come from resume and sessions.
    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_syncStarted)
        {
            return;
        }

        _syncStarted = true;
        _ = _sync.TriggerAsync("start");
    }
}
