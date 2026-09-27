using Burdock.Domain.Exposure;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;

namespace Burdock.Domain.Learning;

/// <summary>Quiet hours in local time; may wrap past midnight (22:00-08:00).</summary>
public readonly record struct QuietHours(TimeOnly Start, TimeOnly End)
{
    public static QuietHours Default { get; } = new(new TimeOnly(22, 0), new TimeOnly(8, 0));

    public bool Contains(TimeOnly time) => Start <= End ? time >= Start && time < End : time >= Start || time < End;
}

/// <summary>Desired FSRS retention presets exposed to the learner (spec 06 §3.3).</summary>
public enum RetentionPreset
{
    /// <summary>0.85: fewer reviews.</summary>
    Easy,

    /// <summary>0.90: default.</summary>
    Standard,

    /// <summary>0.94: maximum recall, more reviews.</summary>
    Maximum,
}

public static class RetentionPresetExtensions
{
    public static double ToDesiredRetention(this RetentionPreset preset) => preset switch
    {
        RetentionPreset.Easy => 0.85,
        RetentionPreset.Maximum => 0.94,
        _ => 0.90,
    };
}

/// <summary>Learner-controlled pace and surface settings (decision Q30 defaults and ranges).</summary>
public sealed record LearnerSettings(
    LanguagePair Pair,
    CefrLevel Level,
    IReadOnlyList<TopicId> Topics,
    int NewWordsPerDay,
    int ExposuresPerWordPerDay,
    RotationCadence Cadence,
    int NotificationsPerDay,
    QuietHours QuietHours,
    bool TranslationHiddenOnSurfaces,
    RetentionPreset Retention,
    bool GamificationEnabled)
{
    public const int MinNewWordsPerDay = 3;
    public const int MaxNewWordsPerDay = 30;
    public const int MinExposuresPerWordPerDay = 1;
    public const int MaxExposuresPerWordPerDay = 10;
    public const int MinNotificationsPerDay = 0;
    public const int MaxNotificationsPerDay = 8;

    /// <summary>«Помірний» pace: 10 new words, 60 min cadence, 5 exposures per word, 3 notifications, quiet 22:00-08:00.</summary>
    public static LearnerSettings Moderate(LanguagePair pair, CefrLevel level, IReadOnlyList<TopicId> topics) => new(
        pair, level, topics, NewWordsPerDay: 10, ExposuresPerWordPerDay: 5, RotationCadence.Default, NotificationsPerDay: 3,
        QuietHours.Default, TranslationHiddenOnSurfaces: false, RetentionPreset.Standard, GamificationEnabled: true);

    public Result<LearnerSettings> Validate()
    {
        if (NewWordsPerDay is < MinNewWordsPerDay or > MaxNewWordsPerDay)
        {
            return DomainError.Validation("settings.newWordsPerDay", "Errors_Settings_NewWordsPerDay");
        }

        if (ExposuresPerWordPerDay is < MinExposuresPerWordPerDay or > MaxExposuresPerWordPerDay)
        {
            return DomainError.Validation("settings.exposuresPerWordPerDay", "Errors_Settings_ExposuresPerWordPerDay");
        }

        if (NotificationsPerDay is < MinNotificationsPerDay or > MaxNotificationsPerDay)
        {
            return DomainError.Validation("settings.notificationsPerDay", "Errors_Settings_NotificationsPerDay");
        }

        return this;
    }
}
