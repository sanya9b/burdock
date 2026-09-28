namespace Burdock.Domain.Learning;

/// <summary>The three onboarding paces (spec 03 S-05, decision Q30): new words per day with a matching notification count; the widget cadence stays at the default.</summary>
public enum PacePreset
{
    Light,
    Moderate,
    Intensive,
}

public static class PacePresetExtensions
{
    public static int NewWordsPerDay(this PacePreset pace) => pace switch
    {
        PacePreset.Light => 5,
        PacePreset.Intensive => 20,
        _ => 10,
    };

    public static int NotificationsPerDay(this PacePreset pace) => pace switch
    {
        PacePreset.Light => 2,
        PacePreset.Intensive => 4,
        _ => 3,
    };

    /// <summary>Rough daily effort shown on the pace cards: about half a minute per new word.</summary>
    public static int EstimatedMinutesPerDay(this PacePreset pace) => pace switch
    {
        PacePreset.Light => 3,
        PacePreset.Intensive => 10,
        _ => 5,
    };

    public static LearnerSettings ApplyTo(this PacePreset pace, LearnerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings with { NewWordsPerDay = pace.NewWordsPerDay(), NotificationsPerDay = pace.NotificationsPerDay() };
    }

    /// <summary>The preset whose new-word count matches the settings, if any (used to pre-select a card).</summary>
    public static PacePreset? FromSettings(LearnerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        foreach (var pace in new[] { PacePreset.Light, PacePreset.Moderate, PacePreset.Intensive })
        {
            if (pace.NewWordsPerDay() == settings.NewWordsPerDay)
            {
                return pace;
            }
        }

        return null;
    }
}
