using Burdock.Presentation.Navigation;
using Burdock.Presentation.Services;
using Burdock.Presentation.Views;

namespace Burdock.Presentation;

public partial class AppShell : Shell
{
    private readonly ISyncCoordinator _sync;
    private readonly IWidgetCoordinator _widgets;
    private readonly IDeepLinkRouter _deepLinks;
    private bool _syncStarted;

    public AppShell(ISyncCoordinator sync, IWidgetCoordinator widgets, IDeepLinkRouter deepLinks)
    {
        _sync = sync ?? throw new ArgumentNullException(nameof(sync));
        _widgets = widgets ?? throw new ArgumentNullException(nameof(widgets));
        _deepLinks = deepLinks ?? throw new ArgumentNullException(nameof(deepLinks));
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
        // A widget or notification tap on a cold start waits here until the Shell can navigate (spec 04 §3.4).
        _ = _deepLinks.FlushAsync();
        if (_syncStarted)
        {
            return;
        }

        _syncStarted = true;
        _ = _widgets.TriggerAsync("start");
        _ = _sync.TriggerAsync("start");
    }
}
