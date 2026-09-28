#if !ANDROID && !IOS && !MACCATALYST && !WINDOWS
namespace Burdock.Presentation.Services;

// The platform-neutral net10.0 head (ViewModel tests, spec 15 section 3.7) has no platform folder, so the platform halves
// of the partial services live here. Real devices compile Platforms/Android or Platforms/iOS instead.
public sealed partial class MotionPreferences
{
    private static partial bool PlatformReduceMotion() => false;
}

public sealed partial class NotificationPrompt
{
    private static partial Task<bool> PlatformRequestAsync(CancellationToken cancellationToken) => Task.FromResult(false);
}

public sealed partial class SpeechService
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Implements the instance partial method the platform heads rely on.")]
    private partial Task PlatformSpeakAsync(string text, string country, float rate, CancellationToken cancellationToken) => Task.CompletedTask;

    private static partial bool PlatformSupportsVoiceSettings() => false;

    private static partial Task PlatformOpenVoiceSettingsAsync() => Task.CompletedTask;
}
#endif
