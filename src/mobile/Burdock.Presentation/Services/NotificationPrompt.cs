namespace Burdock.Presentation.Services;

/// <summary>Asks the platform for permission to show notifications (onboarding S-06). Rewards never depend on the answer (App Store 5.1.2(i)).</summary>
public interface INotificationPrompt
{
    Task<bool> RequestAsync(CancellationToken cancellationToken);
}

public sealed partial class NotificationPrompt : INotificationPrompt
{
    public Task<bool> RequestAsync(CancellationToken cancellationToken) => PlatformRequestAsync(cancellationToken);

    private static partial Task<bool> PlatformRequestAsync(CancellationToken cancellationToken);
}
