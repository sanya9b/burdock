using Burdock.Infrastructure.Widgets;
using Burdock.TestKit;
using Burdock.TestKit.Fakes;

namespace Burdock.Infrastructure.Tests.Widgets;

/// <summary>WidgetKit reload coalescing (spec 04 §3.3.2): one reload per window, and a request inside the window is deferred, never dropped.</summary>
public sealed class ReloadCoalescerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 7, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Requests_inside_the_window_are_deferred_to_its_end_and_merged()
    {
        var clock = new FakeClock(Now, TestTimeZones.KyivFixed);
        var reloads = 0;
        var waits = new List<TimeSpan>();
        var gate = new TaskCompletionSource();
        var coalescer = new ReloadCoalescer(clock, TimeSpan.FromSeconds(300), () => reloads++, wait =>
        {
            waits.Add(wait);
            return gate.Task;
        });

        coalescer.Request().ShouldBeTrue();
        reloads.ShouldBe(1);

        clock.Advance(TimeSpan.FromSeconds(60));
        coalescer.Request().ShouldBeFalse();
        coalescer.Request().ShouldBeFalse();
        reloads.ShouldBe(1);
        waits.ShouldBe([TimeSpan.FromSeconds(240)]);

        clock.Advance(TimeSpan.FromSeconds(240));
        gate.SetResult();
        await Task.Yield();
        reloads.ShouldBe(2);

        clock.Advance(TimeSpan.FromSeconds(300));
        coalescer.Request().ShouldBeTrue();
        reloads.ShouldBe(3);
    }
}
