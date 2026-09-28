namespace Burdock.Presentation.Services;

/// <summary>Haptic feedback of the signature animations (spec 02 section 3.8): success on correct answers and rewards, error on misses, light on presses. Follows the system setting; own toggle arrives with settings v2.</summary>
public interface IHaptics
{
    void Success();

    void Error();

    void Light();
}

public sealed class MauiHaptics : IHaptics
{
    public void Success() => Perform(HapticFeedbackType.LongPress);

    public void Error() => Perform(HapticFeedbackType.Click);

    public void Light() => Perform(HapticFeedbackType.Click);

    private static void Perform(HapticFeedbackType type)
    {
        try
        {
            if (HapticFeedback.Default.IsSupported)
            {
                HapticFeedback.Default.Perform(type);
            }
        }
        catch (FeatureNotSupportedException)
        {
            // No vibrator: silent.
        }
    }
}
