namespace Burdock.Presentation.Services;

public sealed partial class NotificationPrompt
{
    // The UNUserNotificationCenter request arrives with the iOS surfaces phase (spec 04); until then the wish is stored without a system prompt.
    private static partial Task<bool> PlatformRequestAsync(CancellationToken cancellationToken) => Task.FromResult(false);
}
