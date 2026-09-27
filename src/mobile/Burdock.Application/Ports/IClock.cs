namespace Burdock.Application.Ports;

/// <summary>The only way application code learns the time (CLAUDE.md section 4). Tests inject a fake.</summary>
public interface IClock
{
    /// <summary>Current instant in UTC.</summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>The learner's current time zone, used for the 04:00 learning-day rollover and quiet hours.</summary>
    TimeZoneInfo LocalTimeZone { get; }
}
