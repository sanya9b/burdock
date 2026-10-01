using Burdock.Server.Modules.Analytics;
using Burdock.Server.Modules.Content;
using Burdock.Server.Modules.Entitlements;
using Burdock.Server.Modules.Identity;
using Burdock.Server.Modules.LearnerProgress;
using Burdock.Server.Modules.Notifications;
using Burdock.Server.Modules.Pronunciation;
using Burdock.Server.Modules.Rewards;
using Burdock.Server.Modules.SettingsSync;
using Burdock.Server.Shared.Modules;

namespace Burdock.Server.Api;

/// <summary>The modules of the monolith in registration order (spec 09 §1.1). Social is reserved (Q24).</summary>
public static class BurdockModules
{
    public static IReadOnlyList<IModule> All { get; } =
    [
        new IdentityModule(),
        new ContentModule(),
        new PronunciationModule(),
        new LearnerProgressModule(),
        new SettingsSyncModule(),
        new RewardsModule(),
        new EntitlementsModule(),
        new NotificationsModule(),
        new AnalyticsModule(),
    ];
}
