namespace Burdock.Presentation.Services;

public sealed partial class NotificationPrompt
{
    // Android 13+ asks for POST_NOTIFICATIONS; older versions grant it implicitly.
    private static async partial Task<bool> PlatformRequestAsync(CancellationToken cancellationToken)
    {
        var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>().ConfigureAwait(true);
        if (status != PermissionStatus.Granted)
        {
            status = await Permissions.RequestAsync<Permissions.PostNotifications>().ConfigureAwait(true);
        }

        return status == PermissionStatus.Granted;
    }
}
