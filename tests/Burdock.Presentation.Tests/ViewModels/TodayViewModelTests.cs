using Burdock.Domain.SharedKernel;
using Burdock.Domain.Learning;
using Burdock.Presentation.Navigation;
using Burdock.Presentation.Resources.Strings;
using Burdock.Presentation.ViewModels;
using ViewState = Burdock.Presentation.ViewModels.ViewState;

namespace Burdock.Presentation.Tests.ViewModels;

/// <summary>S-10 «Сьогодні» (spec 03): states loading → ready / empty / error, greeting by local hour, navigation from the hero and the CTA.</summary>
public sealed class TodayViewModelTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Load_shows_the_hero_and_the_strip_when_the_plan_has_words()
    {
        var harness = new ViewModelHarness().WithBundledWords(12);
        var vm = harness.Resolve<TodayViewModel>();
        vm.State.ShouldBe(ViewState.Loading);

        await vm.LoadAsync(Ct);

        vm.State.ShouldBe(ViewState.Ready);
        vm.Words.Count.ShouldBe(5);
        vm.Hero.ShouldNotBeNull();
        vm.Hero.Lemma.ShouldBe(vm.Words[0].Lemma);
        vm.HasMore.ShouldBeTrue();
        vm.HeroPositionText.ShouldBe("1 з 5");
        vm.PlanSummary.ShouldNotBeNullOrWhiteSpace();
        vm.StartSessionCommand.CanExecute(null).ShouldBeTrue();
    }

    [Fact]
    public async Task A_full_pack_of_due_learned_words_shows_the_review_badge_that_opens_the_round()
    {
        var harness = new ViewModelHarness().WithBundledWords(12);
        var now = harness.Clock.UtcNow;
        for (var n = 1; n <= 5; n++)
        {
            var card = LearnerCard.Restore(CardId.From(Guid.NewGuid()), ViewModelHarness.Learner, ViewModelHarness.Word(n).Id, now.AddDays(-10), CardState.Learned,
                MemoryState.Create(3, 5), null, now.AddDays(-1), now.AddDays(-3), now.AddDays(-10), now.AddDays(-3), 6, 0, 0, 1, 1, [], [], LearningPolicy.MasteryStreak);
            await harness.Cards.UpsertAsync(card, Ct);
        }

        var vm = harness.Resolve<TodayViewModel>();
        await vm.LoadAsync(Ct);

        vm.ShowReview.ShouldBeTrue();
        vm.ReviewBody.ShouldContain("5");
        // Learned words are not words of the day any more (log item 24): the plan holds the other five new ones.
        vm.Words.Select(w => w.Lemma).ShouldNotContain("word1");

        await vm.StartReviewCommand.ExecuteAsync(null);
        harness.Navigation.Last.Route.ShouldBe(Routes.Learn.Review);
    }

    [Fact]
    public async Task Fewer_than_five_due_words_show_no_badge()
    {
        var harness = new ViewModelHarness().WithBundledWords(12);
        var now = harness.Clock.UtcNow;
        for (var n = 1; n <= 4; n++)
        {
            var card = LearnerCard.Restore(CardId.From(Guid.NewGuid()), ViewModelHarness.Learner, ViewModelHarness.Word(n).Id, now.AddDays(-10), CardState.Learned,
                MemoryState.Create(3, 5), null, now.AddDays(-1), now.AddDays(-3), now.AddDays(-10), now.AddDays(-3), 6, 0, 0, 1, 1, [], [], LearningPolicy.MasteryStreak);
            await harness.Cards.UpsertAsync(card, Ct);
        }

        var vm = harness.Resolve<TodayViewModel>();
        await vm.LoadAsync(Ct);

        vm.ShowReview.ShouldBeFalse();
    }

    [Fact]
    public async Task The_first_load_shows_the_skeleton_and_a_reload_uses_the_refresh_indicator()
    {
        var harness = new ViewModelHarness().WithBundledWords(12);
        var vm = harness.Resolve<TodayViewModel>();
        vm.ShowSkeleton.ShouldBeTrue();
        vm.IsRefreshing.ShouldBeFalse();

        await vm.LoadAsync(Ct);
        vm.ShowSkeleton.ShouldBeFalse();

        var refreshingSeen = false;
        vm.PropertyChanged += (_, e) => refreshingSeen |= e.PropertyName == nameof(TodayViewModel.IsRefreshing) && vm.IsRefreshing;
        await vm.LoadAsync(Ct);

        refreshingSeen.ShouldBeTrue();
        vm.IsRefreshing.ShouldBeFalse();
        vm.ShowSkeleton.ShouldBeFalse();
        vm.State.ShouldBe(ViewState.Ready);
    }

    [Fact]
    public async Task Load_without_content_is_the_empty_state()
    {
        var vm = new ViewModelHarness().Resolve<TodayViewModel>();

        await vm.LoadAsync(Ct);

        vm.State.ShouldBe(ViewState.Empty);
        vm.HasWords.ShouldBeFalse();
        vm.Hero.ShouldBeNull();
        vm.StartSessionCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public async Task Meeting_the_daily_goal_shows_goal_done_and_offers_five_more_cards()
    {
        // Five words introduced today and already graduated to review (due tomorrow): nothing to show, the goal is met.
        var harness = await new ViewModelHarness().WithImportedWordsAsync(12);
        var now = harness.Clock.UtcNow;
        for (var n = 1; n <= 5; n++)
        {
            var card = LearnerCard.Restore(CardId.From(Guid.NewGuid()), ViewModelHarness.Learner, ViewModelHarness.Word(n).Id, now.AddHours(-2), CardState.Review, null, null,
                now.AddDays(1), now.AddHours(-1), now.AddHours(-2), null, 1, 0, 0, 1, 1, [], []);
            await harness.Cards.UpsertAsync(card, Ct);
        }

        var vm = harness.Resolve<TodayViewModel>();
        await vm.LoadAsync(Ct);
        await vm.MoreWordsCommand.ExecuteAsync(null);

        vm.State.ShouldBe(ViewState.GoalDone);
        vm.HasWords.ShouldBeFalse();
        harness.Navigation.Last.Route.ShouldBe(Routes.Learn.Session);
        harness.Navigation.Last.Parameters!["size"].ShouldBe("5");
        harness.Navigation.Last.Parameters["extra"].ShouldBe("5");

        // The extra session really gets five new words beyond the pace.
        var session = harness.Resolve<SessionViewModel>();
        session.ApplyQueryAttributes(new Dictionary<string, object>(StringComparer.Ordinal) { ["size"] = "5", ["extra"] = "5" });
        await session.LoadAsync(Ct);
        session.State.ShouldBe(ViewState.Ready);
    }

    [Fact]
    public async Task Load_reports_the_error_state_when_a_port_fails()
    {
        var harness = new ViewModelHarness().WithBundledWords(12);
        harness.ContentPort.Failure = new InvalidOperationException("database locked");
        var vm = harness.Resolve<TodayViewModel>();

        await vm.LoadAsync(Ct);

        vm.State.ShouldBe(ViewState.Error);
        vm.HasWords.ShouldBeFalse();
    }

    [Theory]
    [InlineData(9, "Morning")]
    [InlineData(13, "Day")]
    [InlineData(17, "Evening")]
    [InlineData(22, "Night")]
    public void Greeting_follows_the_local_hour(int utcHour, string expectedKey)
    {
        // The harness zone is UTC+2: 9 → 11 (morning), 13 → 15 (day), 17 → 19 (evening), 22 → 00 (night).
        var harness = new ViewModelHarness();
        harness.Clock.UtcNow = new DateTimeOffset(2026, 10, 5, utcHour, 0, 0, TimeSpan.Zero);

        var vm = harness.Resolve<TodayViewModel>();

        var expected = expectedKey switch
        {
            "Morning" => AppResources.Today_Greeting_Morning,
            "Day" => AppResources.Today_Greeting_Day,
            "Evening" => AppResources.Today_Greeting_Evening,
            _ => AppResources.Today_Greeting_Night,
        };
        vm.Greeting.ShouldBe(expected);
        vm.LearningDayText.ShouldContain("жовтня");
    }

    [Fact]
    public async Task Opening_a_word_navigates_to_the_card_with_its_id()
    {
        var harness = new ViewModelHarness().WithBundledWords(12);
        var vm = harness.Resolve<TodayViewModel>();
        await vm.LoadAsync(Ct);

        await vm.OpenWordCommand.ExecuteAsync(vm.Hero);

        harness.Navigation.Last.Route.ShouldBe(Routes.Today.Word);
        harness.Navigation.Last.Parameters!["id"].ShouldBe(vm.Hero!.LexemeId.ToString("D"));
    }

    [Fact]
    public async Task The_primary_action_starts_a_session_of_ten()
    {
        var harness = new ViewModelHarness().WithBundledWords(12);
        var vm = harness.Resolve<TodayViewModel>();
        await vm.LoadAsync(Ct);

        await vm.StartSessionCommand.ExecuteAsync(null);

        harness.Navigation.Last.Route.ShouldBe(Routes.Learn.Session);
        harness.Navigation.Last.Parameters!["size"].ShouldBe("10");
    }

    [Fact]
    public async Task Swiping_the_pager_moves_the_counter_and_the_page_survives_a_reload()
    {
        var harness = new ViewModelHarness().WithBundledWords(12);
        var vm = harness.Resolve<TodayViewModel>();
        await vm.LoadAsync(Ct);

        vm.Hero = vm.Words[2]; // what the CarouselView does on a swipe (the compact strip is gone, owner 14.11.2026)

        vm.HeroPositionText.ShouldBe("3 з 5");
        await vm.SpeakWordCommand.ExecuteAsync(vm.Words[4]);
        harness.Speech.SpokenWords.ShouldBe([(Burdock.Domain.SharedKernel.LexemeId.From(vm.Words[4].LexemeId), vm.Words[4].Lemma)]);

        // Coming back to the screen reloads the plan but keeps the page.
        await vm.LoadAsync(Ct);
        vm.Hero!.LexemeId.ShouldBe(vm.Words[2].LexemeId);
        vm.HeroPositionText.ShouldBe("3 з 5");
    }

    [Fact]
    public async Task Speaking_the_hero_uses_the_speech_service()
    {
        var harness = new ViewModelHarness().WithBundledWords(12);
        var vm = harness.Resolve<TodayViewModel>();
        await vm.LoadAsync(Ct);

        await vm.SpeakHeroCommand.ExecuteAsync(null);

        harness.Speech.SpokenWords.ShouldBe([(Burdock.Domain.SharedKernel.LexemeId.From(vm.Hero!.LexemeId), vm.Hero.Lemma)]);
    }

    [Fact]
    public async Task Reward_badges_follow_the_gamification_setting()
    {
        var on = new ViewModelHarness().WithBundledWords(12).Resolve<TodayViewModel>();
        var off = new ViewModelHarness(ViewModelHarness.DefaultSettings with { GamificationEnabled = false }).WithBundledWords(12).Resolve<TodayViewModel>();

        await on.LoadAsync(Ct);
        await off.LoadAsync(Ct);

        on.ShowRewards.ShouldBeTrue();
        on.StreakText.ShouldBe("0");
        on.ThornsText.ShouldBe("5"); // the first open of the day claims the login calendar (spec 07 §3.4)
        off.ShowRewards.ShouldBeFalse();
    }
}
