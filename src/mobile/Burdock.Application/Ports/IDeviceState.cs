namespace Burdock.Application.Ports;

/// <summary>Whether the first-run onboarding (spec 03 S-02…S-09) was finished on this device.</summary>
public interface IOnboardingState
{
    Task<bool> IsCompletedAsync(CancellationToken cancellationToken);

    Task MarkCompletedAsync(CancellationToken cancellationToken);
}

/// <summary>Privacy choices of the learner (spec 10, S-08): opt-in analytics; crash reports follow decision-log item 8 once it is decided.</summary>
public interface IPrivacyConsent
{
    Task<bool> GetAnalyticsConsentAsync(CancellationToken cancellationToken);

    Task SetAnalyticsConsentAsync(bool granted, CancellationToken cancellationToken);
}

/// <summary>Randomness behind a port so use cases stay deterministic in tests (CLAUDE.md section 4).</summary>
public interface IRandom
{
    /// <summary>Uniform integer in [minInclusive, maxExclusive).</summary>
    int Next(int minInclusive, int maxExclusive);
}
