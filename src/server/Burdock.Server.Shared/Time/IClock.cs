namespace Burdock.Server.Shared.Time;

/// <summary>Server-side clock port; the only source of time for module code so jobs and reconciliation stay testable.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

/// <summary>Production clock.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
