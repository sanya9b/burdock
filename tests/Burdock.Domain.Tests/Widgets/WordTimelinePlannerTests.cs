using Burdock.Domain.Exposure;
using Burdock.Domain.Learning;
using Burdock.Domain.SharedKernel;
using Burdock.Domain.Time;
using Burdock.Domain.Vocabulary;
using Burdock.Domain.Widgets;
using Burdock.TestKit;

namespace Burdock.Domain.Tests.Widgets;

/// <summary>Widget timeline planning (spec 04 §3.3, FR-04-4, NFR-04-3; spec 06 §3.5.2 FR-06-10).</summary>
public sealed class WordTimelinePlannerTests
{
    private static readonly TimeZoneInfo Kyiv = TestTimeZones.KyivFixed;
    private static readonly DateTimeOffset Now = Local(2026, 10, 27, 9, 17, 42);

    [Fact]
    public void Slots_follow_the_cadence_from_the_local_boundary_before_now_up_to_the_horizon()
    {
        var timeline = WordTimelinePlanner.Plan(Request(Settings(cadence: 60), Candidates(10)));

        timeline.Entries.ShouldNotBeEmpty();
        timeline.Entries[0].FireDate.ShouldBe(Local(2026, 10, 27, 9, 0, 0));
        timeline.Entries[1].FireDate.ShouldBe(Local(2026, 10, 27, 10, 0, 0));
        timeline.Entries[0].PlannedUntil.ShouldBe(timeline.Entries[1].FireDate);
        timeline.PlannedUntil.ShouldBe(Now.AddHours(48));
        timeline.Entries[^1].PlannedUntil.ShouldBe(timeline.PlannedUntil);
        (timeline.GeneratedAt.Ticks % TimeSpan.TicksPerSecond).ShouldBe(0);
        timeline.GeneratedAt.ShouldBe(Now);
        timeline.Entries.Select(e => e.EntryIndex).ShouldBe(Enumerable.Range(0, timeline.Entries.Count));
        timeline.Entries.Zip(timeline.Entries.Skip(1)).ShouldAllBe(p => p.Second.FireDate - p.First.FireDate >= TimeSpan.FromMinutes(WordTimelinePlanner.MinEntrySpacingMinutes));
        timeline.CadenceMinutes.ShouldBe(60);
        timeline.TranslationMode.ShouldBe(TranslationMode.Visible);
        timeline.Fallback.ShouldBe(TimelineFallback.OpenApp);
    }

    [Fact]
    public void Quiet_hours_produce_no_slots_so_the_evening_word_stays_until_the_first_morning_slot()
    {
        var timeline = WordTimelinePlanner.Plan(Request(Settings(cadence: 60), Candidates(10)));

        var local = timeline.Entries.Select(e => TimeZoneInfo.ConvertTime(e.FireDate, Kyiv)).ToArray();
        local.ShouldNotContain(t => t.Hour >= 22 || t.Hour < 8);
        var evening = timeline.Entries.Last(e => TimeZoneInfo.ConvertTime(e.FireDate, Kyiv).Day == 27 && TimeZoneInfo.ConvertTime(e.FireDate, Kyiv).Hour == 21);
        evening.PlannedUntil.ShouldBe(Local(2026, 10, 28, 8, 0, 0));
    }

    [Fact]
    public void A_request_inside_quiet_hours_still_produces_a_current_entry()
    {
        var night = Local(2026, 10, 27, 23, 30, 0);
        var timeline = WordTimelinePlanner.Plan(Request(Settings(cadence: 60), Candidates(5), night));

        timeline.Entries[0].FireDate.ShouldBe(Local(2026, 10, 27, 23, 0, 0));
        timeline.Entries[1].FireDate.ShouldBe(Local(2026, 10, 28, 8, 0, 0));
    }

    [Fact]
    public void Daily_exposure_cap_and_distinct_word_limit_hold_per_learning_day()
    {
        var timeline = WordTimelinePlanner.Plan(Request(Settings(cadence: 15, exposures: 2), Candidates(30)));

        var byDay = timeline.Entries.GroupBy(e => LearningDay.From(e.FireDate, Kyiv)).ToArray();
        byDay.Length.ShouldBeGreaterThanOrEqualTo(2);
        foreach (var day in byDay)
        {
            day.GroupBy(e => e.LexemeId).ShouldAllBe(g => g.Count() <= 2);
            day.Select(e => e.LexemeId).Distinct().Count().ShouldBeLessThanOrEqualTo(WordTimelinePlanner.MaxDistinctWordsPerDay);
        }

        timeline.Entries.Count.ShouldBeLessThanOrEqualTo(WordTimelinePlanner.MaxEntries);
    }

    [Fact]
    public void The_same_word_never_sits_in_adjacent_slots_and_repeats_are_spread_out()
    {
        var timeline = WordTimelinePlanner.Plan(Request(Settings(cadence: 60, exposures: 5), Candidates(3)));

        timeline.Entries.Zip(timeline.Entries.Skip(1)).ShouldAllBe(p => p.First.LexemeId != p.Second.LexemeId);
        var gap = WordTimelinePlanner.MinGap(Settings(cadence: 60, exposures: 5), RotationCadence.Create(60).Value, 5);
        gap.ShouldBeGreaterThanOrEqualTo(2);
        timeline.Entries.Select(e => e.LexemeId).Distinct().Count().ShouldBe(3);
    }

    [Fact]
    public void New_words_take_about_forty_percent_of_the_slots_when_both_groups_are_available()
    {
        var candidates = Candidates(6, isNew: false).Concat(Candidates(6, isNew: true, offset: 100)).ToArray();

        var timeline = WordTimelinePlanner.Plan(Request(Settings(cadence: 15, exposures: 10), candidates));

        var newIds = candidates.Where(c => c.IsNew).Select(c => c.Entry.Id).ToHashSet();
        var share = timeline.Entries.Count(e => newIds.Contains(e.LexemeId)) / (double)timeline.Entries.Count;
        share.ShouldBeInRange(0.3, 0.5);
    }

    [Fact]
    public void Exposures_already_made_today_count_against_the_cap_but_not_tomorrow()
    {
        var saturated = new TimelineCandidate(Entry(1), false, 0, ExposuresToday: 5);
        var fresh = new TimelineCandidate(Entry(2), false, 1, 0);

        var timeline = WordTimelinePlanner.Plan(Request(Settings(cadence: 60, exposures: 5), [saturated, fresh]));

        var today = LearningDay.From(Now, Kyiv);
        timeline.Entries.Where(e => LearningDay.From(e.FireDate, Kyiv) == today).ShouldAllBe(e => e.LexemeId == fresh.Entry.Id);
        timeline.Entries.Where(e => LearningDay.From(e.FireDate, Kyiv) == today.Next()).ShouldContain(e => e.LexemeId == saturated.Entry.Id);
    }

    [Fact]
    public void When_every_word_already_hit_todays_cap_the_current_slot_still_shows_one()
    {
        var saturated = Enumerable.Range(1, 3).Select(n => new TimelineCandidate(Entry(n), false, n, ExposuresToday: 5)).ToArray();

        var timeline = WordTimelinePlanner.Plan(Request(Settings(cadence: 60, exposures: 5), saturated));

        var today = LearningDay.From(Now, Kyiv);
        var todays = timeline.Entries.Where(e => LearningDay.From(e.FireDate, Kyiv) == today).ToArray();
        todays.Length.ShouldBe(1);
        todays[0].FireDate.ShouldBeLessThanOrEqualTo(Now);
        timeline.Entries.Where(e => LearningDay.From(e.FireDate, Kyiv) == today.Next()).ShouldNotBeEmpty();
    }

    [Fact]
    public void Free_tier_fixes_the_cadence_and_limits_the_pool()
    {
        var timeline = WordTimelinePlanner.Plan(Request(Settings(cadence: 15), Candidates(20), freeTier: true));

        timeline.CadenceMinutes.ShouldBe(RotationCadence.FreeTier.Minutes);
        timeline.Entries.Select(e => e.LexemeId).Distinct().Count().ShouldBeLessThanOrEqualTo(WordTimelinePlanner.FreeTierWordsPerDay);
        timeline.Entries.ShouldAllBe(e => e.IsFreeTier);
    }

    [Fact]
    public void Entries_carry_the_surface_fields_and_hidden_translation_mode()
    {
        var settings = Settings(cadence: 60) with { TranslationHiddenOnSurfaces = true };
        var timeline = WordTimelinePlanner.Plan(Request(settings, Candidates(2)));

        timeline.TranslationMode.ShouldBe(TranslationMode.Hidden);
        var entry = timeline.Entries[0];
        entry.Word.ShouldStartWith("word");
        entry.Translation.ShouldStartWith("слово");
        entry.Pos.ShouldBe("noun");
        entry.Ipa.ShouldBe("/wɜrd/");
        entry.Example.ShouldBe("Example sentence.");
        entry.CategoryId.ShouldBe("shopping");
        entry.ThemeId.ShouldBe(WordTimelinePlanner.DefaultThemeId);
    }

    [Fact]
    public void Planning_is_deterministic_hashes_are_stable_and_the_horizon_does_not_change_the_leading_hash()
    {
        var first = WordTimelinePlanner.Plan(Request(Settings(cadence: 60), Candidates(10)));
        var again = WordTimelinePlanner.Plan(Request(Settings(cadence: 60), Candidates(10)));
        var later = WordTimelinePlanner.Plan(Request(Settings(cadence: 60), Candidates(10), Now.AddMinutes(5)));

        again.ContentHash().ShouldBe(first.ContentHash());
        later.LeadingHash().ShouldBe(first.LeadingHash());
        later.ContentHash().ShouldNotBe(first.ContentHash());
        first.EntryAt(Now)!.EntryIndex.ShouldBe(0);
        first.EntryAt(Local(2026, 10, 27, 10, 30, 0))!.EntryIndex.ShouldBe(1);
        first.EntryAt(first.PlannedUntil).ShouldBeNull();
        first.EntryAt(Local(2026, 10, 27, 8, 0, 0)).ShouldBeNull();
    }

    [Fact]
    public void Fifteen_minute_cadence_without_quiet_hours_stays_within_the_entry_limit()
    {
        var settings = Settings(cadence: 15, exposures: 10) with { QuietHours = new QuietHours(new TimeOnly(3, 0), new TimeOnly(3, 0)) };

        var timeline = WordTimelinePlanner.Plan(Request(settings, Candidates(40)));

        timeline.Entries.Count.ShouldBeLessThanOrEqualTo(WordTimelinePlanner.MaxEntries);
        timeline.Entries.Count.ShouldBeGreaterThan(150);
    }

    [Fact]
    public void No_candidates_yield_an_empty_timeline_that_falls_back_immediately()
    {
        var timeline = WordTimelinePlanner.Plan(Request(Settings(cadence: 60), []));

        timeline.Entries.ShouldBeEmpty();
        timeline.PlannedUntil.ShouldBe(timeline.GeneratedAt);
        timeline.EntryAt(Now).ShouldBeNull();
    }

    private static TimelineRequest Request(LearnerSettings settings, IReadOnlyList<TimelineCandidate> candidates, DateTimeOffset? now = null, bool freeTier = false)
        => new(now ?? Now, Kyiv, settings, candidates, freeTier);

    private static LearnerSettings Settings(int cadence, int exposures = 5)
        => LearnerSettings.Moderate(LanguagePair.UkrainianToEnglish, CefrLevel.A1, []) with { Cadence = RotationCadence.Create(cadence).Value, ExposuresPerWordPerDay = exposures };

    private static TimelineCandidate[] Candidates(int count, bool isNew = false, int offset = 0)
        => Enumerable.Range(1 + offset, count).Select(n => new TimelineCandidate(Entry(n), isNew, n, 0)).ToArray();

    private static WordEntry Entry(int n) => new(
        LexemeBuilder.ALexeme().WithId(Guid.Parse($"0199a4c0-0000-7000-8000-0000000000{n:x2}")).WithLemma($"word{n}").Build(),
        $"слово{n}", [], [$"word{n}"], "/wɜrd/", null, "Example sentence.", "Приклад.");

    private static DateTimeOffset Local(int year, int month, int day, int hour, int minute, int second)
    {
        var local = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, Kyiv.GetUtcOffset(local)).ToUniversalTime();
    }
}
