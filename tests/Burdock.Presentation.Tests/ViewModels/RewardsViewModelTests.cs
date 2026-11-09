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
        vm.Sections.Select(s => s.Category).ShouldBe([CatalogCategory.Outfit, CatalogCategory.Ride, CatalogCategory.Scene, CatalogCategory.WidgetTheme, CatalogCategory.Frame]);
        vm.Sections.Select(s => s.Tiles.Count).ShouldBe([11, 8, 6, 4, 7]);
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
    public async Task A_scene_and_a_ride_are_previewed_bought_and_removed_in_their_own_slots()
    {
        var harness = new ViewModelHarness();
        await harness.Rewards.SaveAsync(ViewModelHarness.Learner, RewardsState.Empty with { Thorns = 600 }, [], Ct);
        var vm = harness.Resolve<RewardsViewModel>();
        await vm.LoadAsync(Ct);

        var meadow = vm.Tiles.Single(t => t.Key == "catalog.scene.meadow");
        meadow.IsScene.ShouldBeTrue();
        meadow.ThumbScene.ShouldBe(MascotScene.Meadow);
        vm.SelectCommand.Execute(meadow);
        vm.PreviewScene.ShouldBe(MascotScene.Meadow);
        vm.PreviewRide.ShouldBe(MascotRide.None);
        await vm.ActCommand.ExecuteAsync(null);
        harness.Rewards.State!.Inventory.Scene.ShouldBe("catalog.scene.meadow");
        vm.ActionText.ShouldBe(AppResources.Shop_Remove_Button);
        vm.Tiles.Single(t => t.Key == "catalog.scene.meadow").StatusText.ShouldBe(AppResources.Shop_Chosen);

        var scooter = vm.Tiles.Single(t => t.Key == "catalog.ride.scooter");
        scooter.ThumbRide.ShouldBe(MascotRide.Scooter);
        vm.SelectCommand.Execute(scooter);
        vm.PreviewScene.ShouldBe(MascotScene.Meadow); // the chosen scene stays behind while a ride is tried on
        vm.PreviewRide.ShouldBe(MascotRide.Scooter);
        await vm.ActCommand.ExecuteAsync(null);
        harness.Rewards.State!.Inventory.Ride.ShouldBe("catalog.ride.scooter");
        harness.Rewards.State.Inventory.Scene.ShouldBe("catalog.scene.meadow");
        harness.Toast.Messages.ShouldContain(m => m.Contains(RewardsText.ItemName(Catalog.Find("catalog.ride.scooter")!), StringComparison.Ordinal));
        vm.ThornsText.ShouldBe(RewardsText.Thorns(150));

        await vm.ActCommand.ExecuteAsync(null); // remove the ride
        harness.Rewards.State!.Inventory.Ride.ShouldBeNull();
        vm.ActionText.ShouldBe(AppResources.Shop_Use_Button);
        harness.Toast.Messages.ShouldContain(AppResources.Shop_Removed_Toast);
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
        var state = RewardsState.Empty with { Inventory = Inventory.Empty with { Owned = new HashSet<string>(StringComparer.Ordinal) { "catalog.outfit.crown", "catalog.frame.gold", "catalog.scene.space", "catalog.ride.saucer" }, Outfit = "catalog.outfit.crown", Frame = "catalog.frame.gold", Scene = "catalog.scene.space", Ride = "catalog.ride.saucer" } };
        await harness.Rewards.SaveAsync(ViewModelHarness.Learner, state, [], Ct);
        var vm = harness.Resolve<MeViewModel>();

        await vm.LoadAsync(Ct);

        vm.Outfit.ShouldBe(MascotOutfit.Crown);
        vm.Frame.ShouldBe(MascotFrame.Gold);
        vm.Scene.ShouldBe(MascotScene.Space);
        vm.Ride.ShouldBe(MascotRide.Saucer);
    }
}
