using Burdock.Application.Ports;

namespace Burdock.Infrastructure.Time;

/// <summary>Production clock: UTC now and the device's current time zone.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public TimeZoneInfo LocalTimeZone => TimeZoneInfo.Local;
}
