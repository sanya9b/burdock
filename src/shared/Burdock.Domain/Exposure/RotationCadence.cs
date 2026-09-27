using Burdock.Domain.SharedKernel;

namespace Burdock.Domain.Exposure;

/// <summary>
/// How often the displayed word changes on time-driven surfaces. Only fixed presets are allowed
/// (decision Q36 as amended: 15/30/60/120/240 minutes) so widget timelines stay within the OS reload budget.
/// </summary>
public readonly record struct RotationCadence
{
    private RotationCadence(int minutes) => Minutes = minutes;

    public int Minutes { get; }

    public TimeSpan Interval => TimeSpan.FromMinutes(Minutes);

    /// <summary>Allowed presets in ascending order (config key learning.cadenceMinutes.presets).</summary>
    public static IReadOnlyList<int> PresetMinutes { get; } = [15, 30, 60, 120, 240];

    /// <summary>Moderate default from onboarding (decision Q30).</summary>
    public static RotationCadence Default { get; } = new(60);

    /// <summary>Cadence fixed for the free tier, which cannot change pace (decision Q14/Q32).</summary>
    public static RotationCadence FreeTier { get; } = Default;

    public static Result<RotationCadence> Create(int minutes)
        => PresetMinutes.Contains(minutes)
            ? new RotationCadence(minutes)
            : DomainError.Validation("cadence.notPreset", "Errors_Cadence_NotPreset",
                new Dictionary<string, string> { ["minutes"] = minutes.ToString(System.Globalization.CultureInfo.InvariantCulture) });

    public override string ToString() => $"{Minutes} min";
}
