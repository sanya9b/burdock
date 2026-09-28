using Burdock.Application.Ports;

namespace Burdock.TestKit.Fakes;

/// <summary>Controllable clock for use-case tests.</summary>
public sealed class FakeClock(DateTimeOffset utcNow, TimeZoneInfo localTimeZone) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;

    public TimeZoneInfo LocalTimeZone { get; set; } = localTimeZone;

    public void Advance(TimeSpan by) => UtcNow += by;
}
