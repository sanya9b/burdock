namespace Burdock.Domain.Time;

/// <summary>
/// The learner's calendar day used for streaks, daily plans and login rewards. It rolls over at
/// <see cref="LearningDayPolicy.DefaultRollover"/> local time (04:00), not at midnight (spec 06 §3.9, T11).
/// </summary>
public readonly record struct LearningDay(DateOnly Date) : IComparable<LearningDay>
{
    /// <summary>Resolves the learning day for an instant in the learner's time zone.</summary>
    public static LearningDay From(DateTimeOffset instant, TimeZoneInfo timeZone, TimeSpan? rollover = null)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        var shift = rollover ?? LearningDayPolicy.DefaultRollover;
        if (shift < TimeSpan.Zero || shift >= TimeSpan.FromDays(1))
        {
            throw new ArgumentOutOfRangeException(nameof(rollover), rollover, "Rollover must be within a single day.");
        }

        var local = TimeZoneInfo.ConvertTime(instant, timeZone);
        return new LearningDay(DateOnly.FromDateTime(local.DateTime - shift));
    }

    public LearningDay AddDays(int days) => new(Date.AddDays(days));

    public LearningDay Next() => AddDays(1);

    public LearningDay Previous() => AddDays(-1);

    /// <summary>Number of learning days from <paramref name="other"/> to this day (positive when this is later).</summary>
    public int DaysSince(LearningDay other) => Date.DayNumber - other.Date.DayNumber;

    public int CompareTo(LearningDay other) => Date.CompareTo(other.Date);

    public static bool operator <(LearningDay left, LearningDay right) => left.CompareTo(right) < 0;

    public static bool operator >(LearningDay left, LearningDay right) => left.CompareTo(right) > 0;

    public static bool operator <=(LearningDay left, LearningDay right) => left.CompareTo(right) <= 0;

    public static bool operator >=(LearningDay left, LearningDay right) => left.CompareTo(right) >= 0;

    public override string ToString() => Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>Policy constants for learning-day computation (config key learning.dayRolloverHour, spec 06 §3.13).</summary>
public static class LearningDayPolicy
{
    /// <summary>04:00 local time: sessions after midnight still count for "yesterday".</summary>
    public static readonly TimeSpan DefaultRollover = TimeSpan.FromHours(4);
}
