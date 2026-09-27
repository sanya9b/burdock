namespace Burdock.TestKit;

/// <summary>Deterministic time zones that do not depend on the OS tz database.</summary>
public static class TestTimeZones
{
    /// <summary>Fixed UTC+02:00 (Kyiv standard time) without DST, for reproducible learning-day tests.</summary>
    public static TimeZoneInfo KyivFixed { get; } = TimeZoneInfo.CreateCustomTimeZone(
        "Test/Kyiv+2", TimeSpan.FromHours(2), "Kyiv (fixed +02:00)", "Kyiv (fixed +02:00)");

    /// <summary>Fixed UTC-05:00, for travel scenarios.</summary>
    public static TimeZoneInfo NewYorkFixed { get; } = TimeZoneInfo.CreateCustomTimeZone(
        "Test/NewYork-5", TimeSpan.FromHours(-5), "New York (fixed -05:00)", "New York (fixed -05:00)");
}
