using Burdock.Application.Tests.Fakes;
using Burdock.Application.UseCases.LearningDays;
using Burdock.TestKit;

namespace Burdock.Application.Tests.UseCases;

public sealed class LearningDayResolverTests
{
    [Fact]
    public void Today_uses_the_clock_and_the_learner_time_zone()
    {
        // 01:00 UTC = 03:00 Kyiv, before the 04:00 rollover.
        var clock = new FakeClock(new DateTimeOffset(2026, 10, 5, 1, 0, 0, TimeSpan.Zero), TestTimeZones.KyivFixed);
        var resolver = new LearningDayResolver(clock);

        resolver.Today().Date.ShouldBe(new DateOnly(2026, 10, 4));

        clock.Advance(TimeSpan.FromHours(1)); // 04:00 Kyiv
        resolver.Today().Date.ShouldBe(new DateOnly(2026, 10, 5));
    }

    [Fact]
    public void End_of_today_is_the_next_rollover_instant()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 10, 5, 10, 0, 0, TimeSpan.Zero), TestTimeZones.KyivFixed);
        var resolver = new LearningDayResolver(clock);

        var end = resolver.EndOfToday();

        end.ShouldBe(new DateTimeOffset(2026, 10, 6, 4, 0, 0, TimeSpan.FromHours(2)));
        (end > clock.UtcNow).ShouldBeTrue();
    }
}
