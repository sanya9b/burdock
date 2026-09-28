namespace Burdock.Presentation.Services;

/// <summary>System accessibility preference for reduced motion (spec 02 section 3.8: every animation has a static equivalent).</summary>
public interface IMotionPreferences
{
    bool ReduceMotion { get; }
}

/// <summary>Platform-backed implementation; the platform half lives in Platforms/*/MotionPreferences.cs.</summary>
public sealed partial class MotionPreferences : IMotionPreferences
{
    public bool ReduceMotion => PlatformReduceMotion();

    private static partial bool PlatformReduceMotion();
}
