namespace Burdock.Application.Ports;

/// <summary>Generates time-ordered identifiers (UUIDv7) for append-only records so they merge deterministically across devices (T14).</summary>
public interface IIdGenerator
{
    Guid NewId();
}
