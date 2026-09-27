using Burdock.Application.Ports;

namespace Burdock.Infrastructure.Identity;

/// <summary>Time-ordered UUIDv7 identifiers so append-only journals merge in creation order across devices (T14).</summary>
public sealed class Uuid7IdGenerator : IIdGenerator
{
    public Guid NewId() => Guid.CreateVersion7();
}
