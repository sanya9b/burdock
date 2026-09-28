using Burdock.Domain.Rewards;
using Burdock.Domain.Vocabulary;
using Burdock.Presentation.Localization;
using Burdock.Presentation.Navigation;
using Burdock.Presentation.Resources.Strings;
using Burdock.Presentation.ViewModels;
using ViewState = Burdock.Presentation.ViewModels.ViewState;

namespace Burdock.Presentation.Tests.ViewModels;

/// <summary>S-17 «Я» (spec 03, spec 07 §3.6): level card, XP bar, achievements in paired rows, hidden ones as «Секрет».</summary>
public sealed class MeViewModelTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Load_shows_level_xp_and_achievements_in_paired_rows()
    {
        var vm = new ViewModelHarness().Resolve<MeViewModel>();

        await vm.LoadAsync(Ct);

        vm.State.ShouldBe(ViewState.Ready);
        vm.ShowRewards.ShouldBeTrue();
        vm.LevelText.ShouldBe(RewardsText.Level(1));
        vm.XpProgress.ShouldBe(0);
        vm.Achievements.Count.ShouldBe(AchievementCatalog.All.Count);
        vm.AchievementRows.Count.ShouldBe((AchievementCatalog.All.Count + 1) / 2);
        vm.AchievementRows[^1].HasRight.ShouldBe(AchievementCatalog.All.Count % 2 == 0);
        vm.Achievements.ShouldAllBe(a => !a.Unlocked);
        vm.Achievements.Where(a => a.Secret).ShouldAllBe(a => a.Title == AppResources.Me_Achievement_Secret && a.Body.Length == 0);
        vm.Achievements.Count(a => a.Secret).ShouldBe(AchievementCatalog.All.Count(d => d.Hidden));
        vm.AchievementsSummary.ShouldBe($"0 / {AchievementCatalog.All.Count}");
    }

    [Fact]
    public async Task With_gamification_off_the_rewards_block_is_hidden()
    {
        var vm = new ViewModelHarness(ViewModelHarness.DefaultSettings with { GamificationEnabled = false }).Resolve<MeViewModel>();

        await vm.LoadAsync(Ct);

        vm.ShowRewards.ShouldBeFalse();
    }

    [Fact]
    public async Task Settings_open_from_the_profile()
    {
        var harness = new ViewModelHarness();
        var vm = harness.Resolve<MeViewModel>();

        await vm.OpenSettingsCommand.ExecuteAsync(null);

        harness.Navigation.Last.Route.ShouldBe(Routes.Me.Settings);
    }
}

/// <summary>S-01 Bootstrap (spec 03): content import before the Shell, error state with retry.</summary>
public sealed class BootstrapViewModelTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Run_raises_completed_once_the_bundled_content_is_imported()
    {
        var harness = new ViewModelHarness().WithBundledWords(3);
        var vm = harness.Resolve<BootstrapViewModel>();
        var completed = 0;
        vm.Completed += (_, _) => completed++;

        await vm.RunAsync(Ct);

        completed.ShouldBe(1);
        vm.IsBusy.ShouldBeFalse();
        vm.HasError.ShouldBeFalse();
        (await harness.Content.CountAsync(LanguagePair.UkrainianToEnglish, Ct)).ShouldBe(3);
    }

    [Fact]
    public async Task A_failure_shows_the_error_and_retry_recovers()
    {
        var harness = new ViewModelHarness().WithBundledWords(3);
        harness.ContentPort.Failure = new IOException("disk full");
        var vm = harness.Resolve<BootstrapViewModel>();
        var completed = 0;
        vm.Completed += (_, _) => completed++;

        await vm.RunAsync(Ct);

        vm.HasError.ShouldBeTrue();
        vm.IsBusy.ShouldBeFalse();
        vm.StatusText.ShouldBe(AppResources.Bootstrap_Error);
        completed.ShouldBe(0);

        harness.ContentPort.Failure = null;
        await vm.RetryCommand.ExecuteAsync(null);

        vm.HasError.ShouldBeFalse();
        vm.StatusText.ShouldBe(AppResources.Bootstrap_Preparing);
        completed.ShouldBe(1);
    }
}

/// <summary>The category catalog behind onboarding, settings and the library (decision Q18 with the 28.09.2026 addendum).</summary>
public sealed class TopicCatalogTests
{
    [Fact]
    public void Nine_categories_in_display_order_with_ukrainian_labels()
    {
        TopicCatalog.Slugs.Count.ShouldBe(9);
        TopicCatalog.Slugs[^1].ShouldBe("military");
        TopicCatalog.Label("military").ShouldBe("Військова справа");
        TopicCatalog.Slugs.ShouldAllBe(slug => TopicCatalog.Label(slug) != slug && TopicCatalog.Label(slug).Length > 0);
        TopicCatalog.Label("unknown").ShouldBe("unknown");
    }
}
