using Burdock.Domain.Exposure;
using Burdock.Domain.SharedKernel;

namespace Burdock.Application.Ports;

/// <summary>Append-only storage of exposures with the two counters the UI shows (ADR 0015).</summary>
public interface IExposureStore
{
    Task AppendAsync(ExposureRecord record, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);

    Task<ExposureCounts> CountAsync(LearnerId learnerId, LexemeId lexemeId, CancellationToken cancellationToken);
}

/// <summary>Scheduled = opportunities, Confirmed = interactions; the UI labels only Confirmed as "переглянуто".</summary>
public readonly record struct ExposureCounts(int Scheduled, int Confirmed);
