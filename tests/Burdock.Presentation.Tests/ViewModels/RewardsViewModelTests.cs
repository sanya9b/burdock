using Burdock.Domain.Rewards;
using Burdock.Presentation.Controls;
using Burdock.Presentation.Localization;
using Burdock.Presentation.Resources.Strings;
using Burdock.Presentation.ViewModels;
using ViewState = Burdock.Presentation.ViewModels.ViewState;

namespace Burdock.Presentation.Tests.ViewModels;

/// <summary>S-16 «Нагороди» (spec 03, spec 07 §3.2.3): sections, a preview that tries items on, one explicit action per item, the freeze bank.</summary>
public sealed class RewardsViewModelTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Load_builds_the_sections_and_previews_the_first_outfit()
    {
        var vm = new ViewModelHarness().Resolve<RewardsViewModel>();

        await vm.LoadAsync(Ct);

        vm.State.ShouldBe(ViewState.Ready);
        vm.ShowShop.ShouldBeTrue();
        vm.ThornsText.ShouldBe(RewardsText.Thorns(0));
        vm.Sections.Select(s => s.Category).ShouldBe([CatalogCategory.Outfit, CatalogCategory.WidgetTheme, CatalogCategory.Frame]);
        vm.Sections[0].Tiles.Count.ShouldBe(11);
        vm.Sections[1].Tiles.Count.ShouldBe(4);
        vm.Sections[2].Tiles.Count.ShouldBe(7);
        vm.Selected.ShouldNotBeNull();
        vm.Selected.Key.ShouldBe("catalog.outfit.scarf");
        vm.Selected.IsSelected.ShouldBeTrue();
        vm.PreviewOutfit.ShouldBe(MascotOutfit.Scarf);
        vm.PreviewName.ShouldBe(RewardsText.ItemName(Catalog.Find("catalog.outfit.scarf")!));
        vm.CanAct.ShouldBeFalse(); // 0 thorns: the button shows the price but stays disabled
        vm.PreviewStatus.ShouldContain(AppResources.Shop_Earn_Hint);
        vm.FreezeBankedText.ShouldBe(RewardsText.FreezeBanked(0, 2));
        vm.CanBuyFreeze.ShouldBeFalse();

        var bloom = vm.Tiles.Single(t => t.Key == Catalog.DefaultThemeKey);
        bloom.Owned.ShouldBeTrue();
        bloom.Equipped.ShouldBeTrue();
        bloom.StatusText.ShouldBe(AppResources.Shop_Chosen);

        var crown = vm.Tiles.Single(t => t.Key == "catalog.outfit.crown");
        crown.Locked.ShouldBeTrue();
        crown.StatusText.ShouldBe(RewardsText.UnlockShort(UnlockCondition.Level(20)));
    }

    [Fact]
    public async Task Buying_wears_the_item_updates_the_balance_and_toasts()
    {
        var harness = new ViewModelHarness();
        await harness.Rewards.SaveAsync(ViewModelHarness.Learner, RewardsState.Empty with { Thorns = 400 }, [], Ct);
        var vm = harness.Resolve<RewardsViewModel>();
        await vm.LoadAsync(Ct);

        vm.SelectCommand.Execute(vm.Tiles.Single(t => t.Key == "catalog.outfit.glasses"));
        vm.ActionIsPurchase.ShouldBeTrue();
        vm.CanAct.ShouldBeTrue();
        vm.ActionText.ShouldBe(RewardsText.BuyButton(150));
        vm.PreviewOutfit.ShouldBe(MascotOutfit.Glasses);

        await vm.ActCommand.ExecuteAsync(null);

        harness.Rewards.State!.Thorns.ShouldBe(250);
        harness.Rewards.State.Inventory.Outfit.ShouldBe("catalog.outfit.glasses");
        harness.Rewards.Events.ShouldContain(e => e.Type == LedgerEventType.Spend && e.Amount == -150);
        harness.Toast.Messages.ShouldContain(m => m.Contains(RewardsText.ItemName(Catalog.Find("catalog.outfit.glasses")!), StringComparison.Ordinal));
        vm.ThornsText.ShouldBe(RewardsText.Thorns(250));
        vm.Selected!.Key.ShouldBe("catalog.outfit.glasses");
        vm.Selected.Equipped.ShouldBeTrue();
        vm.ActionText.ShouldBe(AppResources.Shop_TakeOff_Button);
        vm.ActionIsPurchase.ShouldBeFalse();

        await vm.ActCommand.ExecuteAsync(null); // take it off
        harness.Rewards.State.Inventory.Outfit.ShouldBeNull();
        vm.PreviewOutfit.ShouldBe(MascotOutfit.Glasses); // still previewed while selected
        vm.ActionText.ShouldBe(AppResources.Shop_Wear_Button);
    }

    [Fact]
    public async Task Choosing_a_widget_theme_replans_the_widget_and_the_default_cannot_be_taken_off()
    {
        var harness = new ViewModelHarness();
        await harness.Rewards.SaveAsync(ViewModelHarness.Learner, RewardsState.Empty with { Thorns = 1_000 }, [], Ct);
        var vm = harness.Resolve<RewardsViewModel>();
        await vm.LoadAsync(Ct);

        vm.SelectCommand.Execute(vm.Tiles.Single(t => t.Key == "catalog.theme.night"));
        await vm.ActCommand.ExecuteAsync(null);

        harness.Rewards.State!.Inventory.Theme.ShouldBe("catalog.theme.night");
        harness.Widgets.Reasons.ShouldContain("theme");
        vm.ActionText.ShouldBe(AppResources.Shop_Default_Button);

        vm.SelectCommand.Execute(vm.Tiles.Single(t => t.Key == Catalog.DefaultThemeKey));
        vm.ActionText.ShouldBe(AppResources.Shop_Choose_Button);
        await vm.ActCommand.ExecuteAsync(null);
        harness.Rewards.State.Inventory.Theme.ShouldBe(Catalog.DefaultThemeKey);
        vm.CanAct.ShouldBeFalse();
    }

    [Fact]
    public async Task A_freeze_is_bought_from_the_bank_card_and_the_bank_text_follows()
    {
        var harness = new ViewModelHarness();
        await harness.Rewards.SaveAsync(ViewModelHarness.Learner, RewardsState.Empty with { Thorns = 250 }, [], Ct);
        var vm = harness.Resolve<RewardsViewModel>();
        await vm.LoadAsync(Ct);

        vm.CanBuyFreeze.ShouldBeTrue();
        await vm.BuyFreezeCommand.ExecuteAsync(null);

        harness.Rewards.State!.Streak.FreezesBanked.ShouldBe(1);
        vm.FreezeBankedText.ShouldBe(RewardsText.FreezeBanked(1, 2));
        vm.CanBuyFreeze.ShouldBeFalse();
        vm.FreezeHint.ShouldBe(AppResources.Shop_Freeze_Cooldown);
        harness.Toast.Messages.ShouldContain(AppResources.Shop_Freeze_Toast);
    }

    [Fact]
    public async Task Without_gamification_the_shop_is_off()
    {
        var vm = new ViewModelHarness(ViewModelHarness.DefaultSettings with { GamificationEnabled = false }).Resolve<RewardsViewModel>();

        await vm.LoadAsync(Ct);

        vm.ShowShop.ShouldBeFalse();
        vm.State.ShouldBe(ViewState.Ready);
    }

    [Fact]
    public async Task The_profile_wears_what_the_inventory_says()
    {
        var harness = new ViewModelHarness();
        var state = RewardsState.Empty with { Inventory = Inventory.Empty with { Owned = new HashSet<string>(StringComparer.Ordinal) { "catalog.outfit.crown", "catalog.frame.gold" }, Outfit = "catalog.outfit.crown", Frame = "catalog.frame.gold" } };
        await harness.Rewards.SaveAsync(ViewModelHarness.Learner, state, [], Ct);
        var vm = harness.Resolve<MeViewModel>();

        await vm.LoadAsync(Ct);

        vm.Outfit.ShouldBe(MascotOutfit.Crown);
        vm.Frame.ShouldBe(MascotFrame.Gold);
    }
}
