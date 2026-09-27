namespace Burdock.Domain.SharedKernel;

/// <summary>Something that happened in the domain and that other parts of the system may react to.</summary>
public interface IDomainEvent
{
    /// <summary>Instant of the event in UTC (Domain never reads the clock itself; callers pass it in).</summary>
    DateTimeOffset OccurredAt { get; }
}
