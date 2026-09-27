using Burdock.Domain.Time;
using Burdock.TestKit;

namespace Burdock.Domain.Tests.Time;

public sealed class LearningDayTests
{
    [Theory]
    [InlineData("2026-10-05T01:59:00+02:00", "2026-10-04")] // 01:59 local: still yesterday's learning day
    [InlineData("2026-10-05T03:59:59+02:00", "2026-10-04")] // one second before rollover
    [InlineData("2026-10-05T04:00:00+02:00", "2026-10-05")] // rollover instant belongs to the new day
    [InlineData("2026-10-05T23:30:00+02:00", "2026-10-05")]
    public void From_rolls_over_at_04_00_local_time(string instant, string expectedDate)
    {
        var day = LearningDay.From(DateTimeOffset.Parse(instant, System.Globalization.CultureInfo.InvariantCulture), TestTimeZones.KyivFixed);

        day.Date.ShouldBe(DateOnly.Parse(expectedDate, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void From_converts_utc_instant_into_the_learner_time_zone()
    {
        // 01:30 UTC on the 5th is 03:30 in Kyiv (+2): before rollover, so learning day is the 4th.
        var utc = new DateTimeOffset(2026, 10, 5, 1, 30, 0, TimeSpan.Zero);

        LearningDay.From(utc, TestTimeZones.KyivFixed).Date.ShouldBe(new DateOnly(2026, 10, 4));
        // The same instant in New York (-5) is 20:30 on the 4th: also the 4th, but for a different reason.
        LearningDay.From(utc, TestTimeZones.NewYorkFixed).Date.ShouldBe(new DateOnly(2026, 10, 4));
    }

    [Fact]
    public void From_honours_a_custom_rollover()
    {
        var instant = new DateTimeOffset(2026, 10, 5, 0, 30, 0, TimeSpan.FromHours(2));

        LearningDay.From(instant, TestTimeZones.KyivFixed, TimeSpan.Zero).Date.ShouldBe(new DateOnly(2026, 10, 5));
        LearningDay.From(instant, TestTimeZones.KyivFixed, TimeSpan.FromHours(1)).Date.ShouldBe(new DateOnly(2026, 10, 4));
    }

    [Fact]
    public void From_rejects_rollover_outside_a_day()
    {
        var instant = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

        Should.Throw<ArgumentOutOfRangeException>(() => LearningDay.From(instant, TestTimeZones.KyivFixed, TimeSpan.FromHours(24)));
        Should.Throw<ArgumentOutOfRangeException>(() => LearningDay.From(instant, TestTimeZones.KyivFixed, TimeSpan.FromMinutes(-1)));
    }

    [Fact]
    public void Days_since_and_ordering_follow_the_calendar()
    {
        var monday = new LearningDay(new DateOnly(2026, 10, 5));
        var thursday = monday.AddDays(3);

        thursday.DaysSince(monday).ShouldBe(3);
        monday.DaysSince(thursday).ShouldBe(-3);
        (monday < thursday).ShouldBeTrue();
        monday.Next().Previous().ShouldBe(monday);
        monday.ToString().ShouldBe("2026-10-05");
    }
}
