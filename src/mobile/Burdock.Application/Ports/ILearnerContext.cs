using Burdock.Domain.Learning;
using Burdock.Domain.SharedKernel;

namespace Burdock.Application.Ports;

/// <summary>Who is learning on this device and with which settings. Anonymous local identity until the server issues an account (ADR 0006).</summary>
public interface ILearnerContext
{
    Task<LearnerId> GetLearnerIdAsync(CancellationToken cancellationToken);

    Task<LearnerSettings> GetSettingsAsync(CancellationToken cancellationToken);

    Task SaveSettingsAsync(LearnerSettings settings, CancellationToken cancellationToken);

    /// <summary>Nickname etc.; null before the onboarding named the learner (decision T10).</summary>
    Task<LearnerProfile?> GetProfileAsync(CancellationToken cancellationToken);

    Task SaveProfileAsync(LearnerProfile profile, CancellationToken cancellationToken);
}
