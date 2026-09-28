namespace Burdock.Presentation.Services;

/// <summary>Lets the bootstrap page wait until the platform splash (Android 12+ animated icon) has left the screen, so its own moment is actually seen.</summary>
public interface ISystemSplash
{
    Task WaitForDismissalAsync(TimeSpan timeout);
}

public sealed class SystemSplash : ISystemSplash
{
    private static readonly TaskCompletionSource Dismissed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Called by the platform once the system splash view is gone (Android exit listener) or when there is none (iOS).</summary>
    public static void MarkDismissed() => Dismissed.TrySetResult();

    public async Task WaitForDismissalAsync(TimeSpan timeout)
    {
        await Task.WhenAny(Dismissed.Task, Task.Delay(timeout)).ConfigureAwait(true);
    }
}
