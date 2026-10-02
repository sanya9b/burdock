using Burdock.Domain.SharedKernel;
using Burdock.Domain.Time;

namespace Burdock.Application.Ports;

/// <summary>
/// Today's new-word pick for one learner (issue #4; spec 06 §3.5 keys the plan by learning day). The snapshot holds a
/// prefix of the fresh candidates in plan order, so later opens of the same day rebuild the plan from the learner's
/// cards plus these ids instead of rescanning every candidate. The fingerprint ties it to the settings that shaped it.
/// </summary>
public sealed record DailyPlanSnapshot(LearnerId LearnerId, LearningDay Day, string Fingerprint, IReadOnlyList<LexemeId> NewWordIds);

public interface IDailyPlanCache
{
    Task<DailyPlanSnapshot?> GetAsync(LearnerId learnerId, CancellationToken cancellationToken);

    Task SaveAsync(DailyPlanSnapshot snapshot, CancellationToken cancellationToken);
}
