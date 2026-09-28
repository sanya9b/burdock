using Burdock.Application.Ports;
using Burdock.Domain.Learning;
using Burdock.Domain.SharedKernel;

namespace Burdock.Application.UseCases.Settings;

/// <summary>Current learner settings for S-18 and onboarding (decision Q30 defaults when nothing was saved yet).</summary>
public sealed class GetLearnerSettingsUseCase(ILearnerContext learner)
{
    public Task<LearnerSettings> ExecuteAsync(CancellationToken cancellationToken) => learner.GetSettingsAsync(cancellationToken);
}

/// <summary>Validates and persists settings; the widget timeline re-plan (flow 3.5.8) hooks in here once surfaces exist.</summary>
public sealed class UpdateLearnerSettingsUseCase(ILearnerContext learner)
{
    public async Task<Result<LearnerSettings>> ExecuteAsync(LearnerSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var validation = settings.Validate();
        if (validation.IsFailure)
        {
            return validation;
        }

        await learner.SaveSettingsAsync(settings, cancellationToken).ConfigureAwait(false);
        return settings;
    }
}
