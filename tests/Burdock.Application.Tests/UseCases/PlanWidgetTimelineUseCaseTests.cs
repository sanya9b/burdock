using Burdock.Application.Ports;
using Burdock.Application.UseCases.LearningDays;
using Burdock.Application.UseCases.Planning;
using Burdock.Application.UseCases.Widgets;
using Burdock.Domain.Exposure;
using Burdock.Domain.Learning;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Sync;
using Burdock.Domain.Vocabulary;
using Burdock.TestKit;
using Burdock.TestKit.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Burdock.Application.Tests.UseCases;

/// <summary>Widget timeline planning and exposure settlement (spec 04 FR-04-3, FR-04-15; spec 06 §3.5.2).</summary>
public sealed class PlanWidgetTimelineUseCaseTests
{
    private static readonly LearnerId Learner = LearnerId.From(Guid.Parse("0199a4c0-0000-7000-8000-00000000dddd"));
    private static readonly DateTimeOffset T0 = new(2026, 10, 27, 7, 17, 0, TimeSpan.Zero);

    private readonly FakeClock _clock = new(T0, TestTimeZones.KyivFixed);
    private readonly FakeWidgetBridge _bridge = new();
    private readonly InMemoryContentRepository _content = new();
    private readonly InMemoryCardRepository _cards = new();
    private readonly InMemoryExposureStore _exposures = new();
    private readonly InMemoryProgressOutbox _outbox = new();
    private readonly InMemorySyncState _state = new();
    private readonly FakeLearnerContext _learner = new(Learner, LearnerSettings.Moderate(LanguagePair.UkrainianToEnglish, CefrLevel.A1, []) with { NewWordsPerDay = 5 });
    private readonly FakeScheduler _scheduler = new();
    private readonly RandomIds _ids = new();

    public PlanWidgetTimelineUseCaseTests()
    {
        var entries = Enumerable.Range(1, 12).Select(n => new WordEntry(
            LexemeBuilder.ALexeme().WithId(Guid.Parse($"0199a500-0000-7000-8000-0000000000{n:x2}")).WithLemma($"word{n}").AtLevel(CefrLevel.A1).Build(),
            $"слово{n}", [], [$"word{n}"], null, null, $"Example {n}.", null)).ToArray();
        _content.ImportPackAsync(new ImportedContentPack("test", LanguagePair.UkrainianToEnglish, 1, entries), CancellationToken.None).GetAwaiter().GetResult();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_unsupported_bridge_makes_the_pass_a_no_op()
    {
        _bridge.IsSupported = false;

        var report = await UseCase().ExecuteAsync("start", Ct);

        report.Supported.ShouldBeFalse();
        _bridge.Writes.ShouldBe(0);
        _bridge.Reloads.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_first_pass_writes_a_timeline_from_todays_words_and_requests_a_reload()
    {
        var report = await UseCase().ExecuteAsync("start", Ct);

        report.Supported.ShouldBeTrue();
        report.Written.ShouldBeTrue();
        report.Entries.ShouldBeGreaterThan(10);
        _bridge.Timeline.ShouldNotBeNull();
        _bridge.Timeline.CadenceMinutes.ShouldBe(60);
        _bridge.Timeline.Entries[0].FireDate.ShouldBeLessThanOrEqualTo(T0);
        _bridge.Timeline.Entries.Select(e => e.LexemeId).Distinct().Count().ShouldBe(5);
        _bridge.Reloads.ShouldBe(["start"]);
    }

    [Fact]
    public async Task An_empty_daily_plan_falls_back_to_the_learners_known_words_instead_of_sleeping()
    {
        // Every candidate of the level is marked known: the Today screen is empty, but the widget keeps repeating them.
        var entries = await _content.ListAsync(LanguagePair.UkrainianToEnglish, [CefrLevel.A1], [], Ct);
        foreach (var (entry, index) in entries.Select((e, i) => (e, i)))
        {
            var card = LearnerCard.CreateNew(CardId.From(Guid.NewGuid()), Learner, entry.Id, T0.AddDays(-2));
            card.ClaimKnown(T0.AddDays(-1).AddMinutes(index));
            await _cards.UpsertAsync(card, Ct);
        }

        var report = await UseCase().ExecuteAsync("start", Ct);

        report.Written.ShouldBeTrue();
        report.Entries.ShouldBeGreaterThan(0);
        var timeline = _bridge.Timeline.ShouldNotBeNull();
        timeline.PlannedUntil.ShouldBeGreaterThan(T0);
        timeline.Entries.Select(e => e.LexemeId).Distinct().Count().ShouldBeLessThanOrEqualTo(Burdock.Domain.Widgets.WordTimelinePlanner.MaxDistinctWordsPerDay);
        timeline.Entries[0].FireDate.ShouldBeLessThanOrEqualTo(T0);
    }

    [Fact]
    public async Task An_unchanged_leading_timeline_is_not_rewritten_until_the_horizon_runs_short()
    {
        var useCase = UseCase();
        await useCase.ExecuteAsync("start", Ct);

        var again = await useCase.ExecuteAsync("resume", Ct);
        _clock.Advance(TimeSpan.FromHours(25));
        var later = await useCase.ExecuteAsync("resume", Ct);

        again.Written.ShouldBeFalse();
        later.Written.ShouldBeTrue();
        _bridge.Writes.ShouldBe(2);
        _bridge.Reloads.ShouldBe(["start", "resume"]);
    }

    [Fact]
    public async Task Scheduled_exposures_are_settled_only_for_entries_shown_while_a_widget_was_installed()
    {
        var useCase = UseCase();
        await useCase.ExecuteAsync("start", Ct);
        _clock.Advance(TimeSpan.FromHours(3));
        var withoutWidget = await useCase.ExecuteAsync("resume", Ct);

        withoutWidget.ScheduledSettled.ShouldBe(0);
        _exposures.Records.ShouldBeEmpty();

        _bridge.Marker = new WidgetMarker(_clock.UtcNow, ["systemSmall"]);
        _clock.Advance(TimeSpan.FromHours(2));
        var withWidget = await useCase.ExecuteAsync("resume", Ct);

        withWidget.ScheduledSettled.ShouldBe(2);
        _exposures.Records.Count.ShouldBe(2);
        _exposures.Records.ShouldAllBe(r => r.Kind == ExposureKind.Scheduled && r.Surface == ExposureSurface.WidgetHome);
        foreach (var record in _exposures.Records)
        {
            (await _cards.GetByLexemeAsync(Learner, record.LexemeId, Ct))!.ScheduledExposures.ShouldBe(1);
        }

        _outbox.Events.ShouldBeEmpty();
        _state.Values[PlanWidgetTimelineUseCase.SettledUntilKey].ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Advancing_with_the_widget_button_confirms_the_dismissed_word_and_schedules_the_early_one()
    {
        var useCase = UseCase();
        _bridge.Marker = new WidgetMarker(T0, ["accessoryRectangular"]);
        await useCase.ExecuteAsync("start", Ct);
        var first = _bridge.Timeline!.Entries[0];
        var second = _bridge.Timeline.Entries[1];
        _bridge.Cursor = new WidgetCursor(1, T0.AddMinutes(10), "nextWordIntent");
        _clock.Advance(TimeSpan.FromMinutes(20));

        var report = await useCase.ExecuteAsync("resume", Ct);

        report.ConfirmedSettled.ShouldBe(1);
        report.ScheduledSettled.ShouldBe(2);
        _exposures.Records.ShouldContain(r => r.LexemeId == first.LexemeId && r.Kind == ExposureKind.Confirmed && r.Surface == ExposureSurface.WidgetLock && r.At == T0.AddMinutes(10));
        _exposures.Records.ShouldContain(r => r.LexemeId == second.LexemeId && r.Kind == ExposureKind.Scheduled && r.At == T0.AddMinutes(10));
        var card = (await _cards.GetByLexemeAsync(Learner, first.LexemeId, Ct)).ShouldNotBeNull();
        card.ConfirmedExposures.ShouldBe(1);
        card.State.ShouldBe(CardState.Introduced);
        _outbox.Events.ShouldHaveSingleItem().Type.ShouldBe(ProgressEventTypes.ExposureConfirmed);
    }

    private PlanWidgetTimelineUseCase UseCase()
    {
        var days = new LearningDayResolver(_clock);
        var plan = new GetDailyPlanUseCase(_learner, _content, _cards, _scheduler, _clock, days, new InMemoryDailyPlanCache());
        return new PlanWidgetTimelineUseCase(_bridge, _learner, plan, _content, _cards, _exposures, _outbox, _state, _ids, _clock, days, NullLogger<PlanWidgetTimelineUseCase>.Instance);
    }

    private sealed class RandomIds : IIdGenerator
    {
        public Guid NewId() => Guid.NewGuid();
    }
}
