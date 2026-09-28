using Burdock.Domain.Learning;
using Burdock.Domain.Rewards;
using Burdock.Domain.Time;

namespace Burdock.Domain.Tests;

public sealed class RewardsTests
{
    private static readonly LearningDay Day1 = new(new DateOnly(2026, 10, 1));
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Streak_grows_on_consecutive_days_and_restarts_after_a_gap()
    {
        var s = StreakRules.Count(StreakState.Empty, Day1);
        s.Length.ShouldBe(1);
        s = StreakRules.Count(s, Day1); // same day: unchanged
        s.Length.ShouldBe(1);
        s = StreakRules.Count(s, Day1.AddDays(1));
        s = StreakRules.Count(s, Day1.AddDays(2));
        s.Length.ShouldBe(3);
        s.Longest.ShouldBe(3);
        s.LengthOn(Day1.AddDays(3)).ShouldBe(3); // still alive the next day
        s.LengthOn(Day1.AddDays(4)).ShouldBe(0); // gap of two days: ended
        s = StreakRules.Count(s, Day1.AddDays(5));
        s.Length.ShouldBe(1);
        s.Longest.ShouldBe(3);
    }

    [Fact]
    public void Milestones_follow_the_spec_list()
    {
        new[] { 3, 7, 14, 30, 60, 100, 365, 200, 500 }.ShouldAllBe(n => StreakRules.IsMilestone(n));
        new[] { 1, 2, 4, 15, 99, 150 }.ShouldAllBe(n => !StreakRules.IsMilestone(n));
        StreakRules.MilestoneThorns(7).ShouldBe(30);
        StreakRules.MilestoneThorns(200).ShouldBe(250);
        StreakRules.CelebrationLevel(3).ShouldBe(2);
        StreakRules.CelebrationLevel(30).ShouldBe(3);
        StreakRules.CelebrationLevel(100).ShouldBe(4);
    }

    [Fact]
    public void Levels_use_the_quadratic_threshold()
    {
        XpRules.Threshold(1).ShouldBe(0);
        XpRules.Threshold(2).ShouldBe(100);
        XpRules.Threshold(10).ShouldBe(2700);
        XpRules.Threshold(30).ShouldBe(23200);
        XpRules.LevelFor(0).ShouldBe(1);
        XpRules.LevelFor(99).ShouldBe(1);
        XpRules.LevelFor(100).ShouldBe(2);
        XpRules.LevelFor(2700).ShouldBe(10);
        XpRules.LevelFor(1_000_000).ShouldBe(30);
        XpRules.LevelProgress(150).ShouldBe((50, 150));
    }

    [Fact]
    public void Settlement_pays_xp_thorns_streak_and_the_first_step_once()
    {
        var ids = 0;
        Guid NewId() => Guid.Parse($"00000000-0000-0000-0000-{++ids:d12}");
        var report = new SessionReport(Guid.NewGuid(), Graded: 10, Correct: 8, AgainCount: 2, TypedCorrect: 3, WordsLearned: 1, LearningDayCounted: true, WarmupCompleted: true);

        var result = RewardsEngine.Settle(RewardsState.Empty, report, new ProgressCounters(1, 3, 10), Day1, Now, NewId);

        result.XpGained.ShouldBe(2 * 1 + 8 * 2 + 10 + 5 + 25); // cards, session, warm-up, first-step achievement
        result.ThornsGained.ShouldBe(10 + 5 + 5 + 20); // session, learning day, learned word, achievement
        result.StreakAfter.ShouldBe(1);
        result.NewAchievements.Select(a => a.Id).ShouldBe([AchievementIds.FirstStep]);
        result.State.SessionsCompleted.ShouldBe(1);
        result.Events.Count.ShouldBe(8);

        var again = RewardsEngine.Settle(result.State, report, new ProgressCounters(1, 3, 10), Day1, Now, NewId);
        again.IsEmpty.ShouldBeTrue();
        again.State.ShouldBeSameAs(result.State);
    }

    [Fact]
    public void Daily_caps_limit_sessions_and_learned_words()
    {
        var state = RewardsState.Empty;
        for (var i = 0; i < 5; i++)
        {
            var report = new SessionReport(Guid.NewGuid(), 5, 5, 0, 0, WordsLearned: 4, LearningDayCounted: true, WarmupCompleted: false);
            state = RewardsEngine.Settle(state, report, new ProgressCounters(0, 0, 0), Day1, Now, Guid.NewGuid).State;
        }

        // sessions 3 × 10 + learning day 5 + words capped at 10 × 5 + first-step achievement 20
        state.Thorns.ShouldBe(30 + 5 + 50 + 20);
        state.Today!.Sessions.ShouldBe(5);
        state.Today.WordsLearned.ShouldBe(10);
    }

    [Fact]
    public void Milestone_and_hidden_perfect_session_unlock_together()
    {
        var state = RewardsState.Empty with { Streak = new StreakState(6, 6, Day1.AddDays(-1)), Achievements = new HashSet<string>([AchievementIds.FirstStep], StringComparer.Ordinal) };
        var report = new SessionReport(Guid.NewGuid(), Graded: 20, Correct: 20, AgainCount: 0, TypedCorrect: 0, WordsLearned: 0, LearningDayCounted: true, WarmupCompleted: false);

        var result = RewardsEngine.Settle(state, report, new ProgressCounters(0, 0, 20), Day1, Now, Guid.NewGuid);

        result.StreakAfter.ShouldBe(7);
        result.StreakMilestone.ShouldBe(7);
        result.NewAchievements.Select(a => a.Id).ShouldBe([AchievementIds.Streak7, AchievementIds.Perfect20]);
        result.ThornsGained.ShouldBe(10 + 5 + 30 + 30 + 30); // session, day, milestone, two achievements
    }
}
