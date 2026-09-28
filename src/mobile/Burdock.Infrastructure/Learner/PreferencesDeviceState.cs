using Burdock.Application.Ports;
using Microsoft.Maui.Storage;

namespace Burdock.Infrastructure.Learner;

/// <summary>Device-local flags in Preferences: onboarding completion and privacy consent (moves to the synced profile in phase 2 where the spec says so).</summary>
public sealed class PreferencesOnboardingState(IPreferences preferences) : IOnboardingState
{
    private const string CompletedKey = "onboarding.completed";

    public Task<bool> IsCompletedAsync(CancellationToken cancellationToken) => Task.FromResult(preferences.Get(CompletedKey, false));

    public Task MarkCompletedAsync(CancellationToken cancellationToken)
    {
        preferences.Set(CompletedKey, true);
        return Task.CompletedTask;
    }
}

public sealed class PreferencesPrivacyConsent(IPreferences preferences) : IPrivacyConsent
{
    private const string AnalyticsKey = "privacy.analyticsConsent";

    public Task<bool> GetAnalyticsConsentAsync(CancellationToken cancellationToken) => Task.FromResult(preferences.Get(AnalyticsKey, false));

    public Task SetAnalyticsConsentAsync(bool granted, CancellationToken cancellationToken)
    {
        preferences.Set(AnalyticsKey, granted);
        return Task.CompletedTask;
    }
}

/// <summary>System randomness for non-learning decisions (nickname suggestions); learning itself stays deterministic.</summary>
public sealed class SystemRandom : IRandom
{
    public int Next(int minInclusive, int maxExclusive) => Random.Shared.Next(minInclusive, maxExclusive);
}
