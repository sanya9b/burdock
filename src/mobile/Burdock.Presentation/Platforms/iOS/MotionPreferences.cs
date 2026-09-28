using UIKit;

namespace Burdock.Presentation.Services;

public sealed partial class MotionPreferences
{
    private static partial bool PlatformReduceMotion() => UIAccessibility.IsReduceMotionEnabled;
}
