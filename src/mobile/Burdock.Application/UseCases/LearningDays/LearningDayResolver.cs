using Burdock.Application.Ports;
using Burdock.Domain.Time;

namespace Burdock.Application.UseCases.LearningDays;

/// <summary>Answers "which learning day is it now?" for the current learner using the injected clock and rollover policy.</summary>
public sealed class LearningDayResolver(IClock clock)
{
    private readonly IClock _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public LearningDay Today() => LearningDay.From(_clock.UtcNow, _clock.LocalTimeZone);

    /// <summary>Instant when the current learning day started (04:00 local), the lower bound of "today" for daily caps.</summary>
    public DateTimeOffset StartOfToday()
    {
        var start = Today().Date.ToDateTime(TimeOnly.MinValue) + LearningDayPolicy.DefaultRollover;
        var offset = _clock.LocalTimeZone.GetUtcOffset(start);
        return new DateTimeOffset(start, offset);
    }

    /// <summary>Instant when the current learning day ends, used to schedule plan recomputation and streak checks.</summary>
    public DateTimeOffset EndOfToday()
    {
        var next = Today().Next().Date.ToDateTime(TimeOnly.MinValue) + LearningDayPolicy.DefaultRollover;
        var offset = _clock.LocalTimeZone.GetUtcOffset(next);
        return new DateTimeOffset(next, offset);
    }
}
