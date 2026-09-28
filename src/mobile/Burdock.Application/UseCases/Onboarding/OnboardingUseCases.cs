using Burdock.Application.Ports;
using Burdock.Domain.Learning;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Vocabulary;

namespace Burdock.Application.UseCases.Onboarding;

/// <summary>Everything the onboarding collects (spec 03 S-02…S-08). Notifications are a wish here; the platform permission is asked by the app shell.</summary>
public sealed record OnboardingChoices(
    string Nickname,
    CefrLevel Level,
    IReadOnlyList<TopicId> Topics,
    PacePreset Pace,
    bool NotificationsEnabled,
    bool AnalyticsConsent);

/// <summary>Tells the bootstrap flow whether to open the Shell or the onboarding.</summary>
public sealed class IsOnboardingCompletedUseCase(IOnboardingState state)
{
    public Task<bool> ExecuteAsync(CancellationToken cancellationToken) => state.IsCompletedAsync(cancellationToken);
}

/// <summary>Suggests «Хоробрий Реп’ях #4821»-style nicknames (decision T10); the word lists come from the UI language resources.</summary>
public sealed class SuggestNicknameUseCase(IRandom random)
{
    public string Execute(IReadOnlyList<string> adjectives, string noun)
    {
        ArgumentNullException.ThrowIfNull(adjectives);
        ArgumentException.ThrowIfNullOrWhiteSpace(noun);
        var adjective = adjectives.Count > 0 ? adjectives[random.Next(0, adjectives.Count)] : string.Empty;
        var number = random.Next(1000, 10000);
        return string.IsNullOrEmpty(adjective) ? $"{noun} #{number}" : $"{adjective} {noun} #{number}";
    }
}

/// <summary>Live counter of S-04: how many words the chosen level (with neighbours) and categories cover in the local content.</summary>
public sealed class EstimateWordCountUseCase(ILearnerContext learner, IContentRepository content)
{
    public async Task<int> ExecuteAsync(CefrLevel level, IReadOnlyCollection<TopicId> topics, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(topics);
        if (topics.Count == 0)
        {
            return 0;
        }

        var settings = await learner.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        var entries = await content.ListAsync(settings.Pair, level.WithNeighbours(), topics, cancellationToken).ConfigureAwait(false);
        return entries.Count;
    }
}

/// <summary>Applies the onboarding choices: profile, settings from the pace preset, consent, completion flag (flow 3.5.1).</summary>
public sealed class CompleteOnboardingUseCase(ILearnerContext learner, IPrivacyConsent consent, IOnboardingState state)
{
    public async Task<Result<LearnerSettings>> ExecuteAsync(OnboardingChoices choices, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(choices);
        var profile = LearnerProfile.Create(choices.Nickname);
        if (profile.IsFailure)
        {
            return Result.Failure<LearnerSettings>(profile.Error!);
        }

        var current = await learner.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        var settings = choices.Pace.ApplyTo(current) with
        {
            Level = choices.Level,
            Topics = choices.Topics.Count > 0 ? choices.Topics : current.Topics,
            NotificationsPerDay = choices.NotificationsEnabled ? choices.Pace.NotificationsPerDay() : 0,
        };
        var validation = settings.Validate();
        if (validation.IsFailure)
        {
            return validation;
        }

        await learner.SaveProfileAsync(profile.Value, cancellationToken).ConfigureAwait(false);
        await learner.SaveSettingsAsync(settings, cancellationToken).ConfigureAwait(false);
        await consent.SetAnalyticsConsentAsync(choices.AnalyticsConsent, cancellationToken).ConfigureAwait(false);
        await state.MarkCompletedAsync(cancellationToken).ConfigureAwait(false);
        return settings;
    }
}
