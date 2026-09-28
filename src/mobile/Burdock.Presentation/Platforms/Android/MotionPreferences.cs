using Android.Provider;

namespace Burdock.Presentation.Services;

public sealed partial class MotionPreferences
{
    // Android has no dedicated reduce-motion switch; "Remove animations" sets the animator duration scale to 0.
    private static partial bool PlatformReduceMotion()
    {
        var resolver = Android.App.Application.Context.ContentResolver;
        if (resolver is null)
        {
            return false;
        }

        return Settings.Global.GetFloat(resolver, Settings.Global.AnimatorDurationScale, 1f) == 0f;
    }
}
