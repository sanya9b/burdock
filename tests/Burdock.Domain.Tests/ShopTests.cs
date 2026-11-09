using Burdock.Domain.Learning;
using Burdock.Domain.Rewards;
using Burdock.Domain.Time;

namespace Burdock.Domain.Tests;

/// <summary>Thorn shop (spec 07 §3.2.3, FR-07-12, FR-07-13) and streak freezes (FR-07-3): fixed prices, open unlocks, no purchase below balance, freeze limits.</summary>
public sealed class ShopTests
{
    private static readonly LearningDay Day1 = new(new DateOnly(2026, 11, 2)); // a Monday
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);
    private static int _ids;

    private static Guid NewId() => Guid.Parse($"0199a4c0-0000-7000-8000-{++_ids:d12}");

    [Fact]
    public void Catalogue_matches_the_spec_table()
    {
        Catalog.All.Select(i => i.Key).Distinct(StringComparer.Ordinal).Count().ShouldBe(Catalog.All.Count);
        Catalog.InCategory(CatalogCategory.Outfit).Count().ShouldBe(11);
        Catalog.InCategory(CatalogCategory.WidgetTheme).Count().ShouldBe(4);
        Catalog.InCategory(CatalogCategory.Frame).Count().ShouldBe(7);
        Catalog.InCategory(CatalogCategory.Scene).Count().ShouldBe(6);
        Catalog.InCategory(CatalogCategory.Ride).Count().ShouldBe(8);
        Catalog.All.Where(i => i.Category != CatalogCategory.Freeze).Sum(i => i.Price).ShouldBe(12_750); // cosmetics of §3.2.3 without emotion packs (log item 22 added scenes 2 000 and rides 4 300)
        Catalog.Find("catalog.ride.saucer")!.Unlock.ShouldBe(UnlockCondition.Level(25));
        Catalog.Find("catalog.scene.space")!.Unlock.ShouldBe(UnlockCondition.Level(12));
        Catalog.Find("catalog.outfit.crown")!.Price.ShouldBe(800);
        Catalog.Find("catalog.outfit.crown")!.Unlock.ShouldBe(UnlockCondition.Level(20));
        Catalog.Find("catalog.theme.sun")!.Slug.ShouldBe("sun");
        Catalog.WidgetThemeId("catalog.theme.night").ShouldBe("nich");
        Catalog.WidgetThemeId("catalog.theme.bloom").ShouldBe("burdock.default");
        Catalog.WidgetThemeId("nope").ShouldBe("burdock.default");
        Catalog.Find("catalog.emotion.base").ShouldBeNull(); // emotion packs wait for animation presets
    }

    [Fact]
    public void Unlock_conditions_read_level_streak_words_days_and_sessions()
    {
        var context = new UnlockContext(Level: 9, LongestStreak: 7, LearnedWords: 50, LearningDays: 29, SessionsCompleted: 1);
        UnlockCondition.None.IsMet(context).ShouldBeTrue();
        UnlockCondition.Level(8).IsMet(context).ShouldBeTrue();
        UnlockCondition.Level(10).IsMet(context).ShouldBeFalse();
        UnlockCondition.Streak(7).IsMet(context).ShouldBeTrue();
        UnlockCondition.Streak(14).IsMet(context).ShouldBeFalse();
        UnlockCondition.WordsLearned(50).IsMet(context).ShouldBeTrue();
        UnlockCondition.LearningDays(30).IsMet(context).ShouldBeFalse();
        UnlockCondition.Sessions(1).IsMet(context).ShouldBeTrue();
    }

    [Fact]
    public void Status_reports_owned_locked_insufficient_and_available()
    {
        var scarf = Catalog.Find("catalog.outfit.scarf")!;
        var crown = Catalog.Find("catalog.outfit.crown")!;
        var state = RewardsState.Empty with { Thorns = 100 };

        ShopRules.Status(state, scarf, Day1, 0).Availability.ShouldBe(ItemAvailability.Insufficient);
        ShopRules.Status(state, scarf, Day1, 0).Missing.ShouldBe(50);
        ShopRules.Status(state with { Thorns = 150 }, scarf, Day1, 0).Availability.ShouldBe(ItemAvailability.Available);
        ShopRules.Status(state with { Thorns = 10_000 }, crown, Day1, 0).Availability.ShouldBe(ItemAvailability.Locked);
        ShopRules.Status(state with { Thorns = 10_000, Xp = XpRules.Threshold(20) }, crown, Day1, 0).Availability.ShouldBe(ItemAvailability.Available);

        var bloom = Catalog.Find(Catalog.DefaultThemeKey)!;
        var status = ShopRules.Status(state, bloom, Day1, 0);
        status.Owned.ShouldBeTrue();
        status.Equipped.ShouldBeTrue();
        status.Availability.ShouldBe(ItemAvailability.Owned);
    }

    [Fact]
    public void Purchase_debits_thorns_records_a_spend_and_wears_the_item()
    {
        var scarf = Catalog.Find("catalog.outfit.scarf")!;
        var state = RewardsState.Empty with { Thorns = 200 };

        var result = ShopRules.Purchase(state, scarf, Day1, 0, Now, NewId);

        result.Success.ShouldBeTrue();
        result.State.Thorns.ShouldBe(50);
        result.State.Inventory.Owns(scarf.Key).ShouldBeTrue();
        result.State.Inventory.Outfit.ShouldBe(scarf.Key);
        result.State.Inventory.OutfitsOwned.ShouldBe(1);
        result.Events.Count.ShouldBe(1);
        result.Events[0].Type.ShouldBe(LedgerEventType.Spend);
        result.Events[0].Amount.ShouldBe(-150);
        result.Events[0].Currency.ShouldBe(Currency.Thorns);
        result.Events[0].Reason.ShouldBe(scarf.Key);

        ShopRules.Purchase(result.State, scarf, Day1, 0, Now, NewId).Reason.ShouldBe(ItemAvailability.Owned);
        ShopRules.Purchase(RewardsState.Empty, scarf, Day1, 0, Now, NewId).Reason.ShouldBe(ItemAvailability.Insufficient);
        ShopRules.Purchase(RewardsState.Empty with { Thorns = 1_000 }, Catalog.Find("catalog.outfit.wreath")!, Day1, 0, Now, NewId).Reason.ShouldBe(ItemAvailability.Locked);
    }

    [Fact]
    public void Free_items_cost_nothing_and_leave_no_spend_event()
    {
        var meadow = Catalog.Find("catalog.theme.meadow")!;
        var state = RewardsState.Empty with { LearningDays = 30 };

        var result = ShopRules.Purchase(state, meadow, Day1, 0, Now, NewId);

        result.Success.ShouldBeTrue();
        result.Events.ShouldBeEmpty();
        result.State.Inventory.Theme.ShouldBe(meadow.Key);
        ShopRules.Purchase(RewardsState.Empty, meadow, Day1, 0, Now, NewId).Reason.ShouldBe(ItemAvailability.Locked);
    }

    [Fact]
    public void Equip_wears_only_owned_cosmetics_and_taking_off_a_theme_returns_to_the_default()
    {
        var night = Catalog.Find("catalog.theme.night")!;
        var glasses = Catalog.Find("catalog.outfit.glasses")!;
        var state = ShopRules.Purchase(RewardsState.Empty with { Thorns = 1_000 }, night, Day1, 0, Now, NewId).State;
        state = ShopRules.Purchase(state, glasses, Day1, 0, Now, NewId).State;
        state.Inventory.Theme.ShouldBe(night.Key);
        state.Inventory.Outfit.ShouldBe(glasses.Key);

        state = ShopRules.Equip(state, glasses, wear: false);
        state.Inventory.Outfit.ShouldBeNull();
        state = ShopRules.Equip(state, night, wear: false);
        state.Inventory.Theme.ShouldBe(Catalog.DefaultThemeKey);
        state = ShopRules.Equip(state, night, wear: true);
        state.Inventory.Theme.ShouldBe(night.Key);

        var unowned = ShopRules.Equip(state, Catalog.Find("catalog.outfit.crown")!, wear: true);
        unowned.Inventory.Outfit.ShouldBeNull();
    }

    [Fact]
    public void Scenes_and_rides_have_their_own_slots_and_never_touch_the_outfit()
    {
        var meadow = Catalog.Find("catalog.scene.meadow")!;
        var scooter = Catalog.Find("catalog.ride.scooter")!;
        var scarf = Catalog.Find("catalog.outfit.scarf")!;
        var state = ShopRules.Purchase(RewardsState.Empty with { Thorns = 1_000 }, scarf, Day1, 0, Now, NewId).State;

        state = ShopRules.Purchase(state, meadow, Day1, 0, Now, NewId).State;
        state.Inventory.Scene.ShouldBe(meadow.Key);
        state.Inventory.IsEquipped(meadow).ShouldBeTrue();
        state = ShopRules.Purchase(state, scooter, Day1, 0, Now, NewId).State;
        state.Inventory.Ride.ShouldBe(scooter.Key);
        state.Inventory.Outfit.ShouldBe(scarf.Key);
        state.Thorns.ShouldBe(1_000 - 150 - 200 - 250);

        state = ShopRules.Equip(state, meadow, wear: false);
        state.Inventory.Scene.ShouldBeNull();
        state.Inventory.Ride.ShouldBe(scooter.Key);
        state = ShopRules.Equip(state, scooter, wear: false);
        state.Inventory.Ride.ShouldBeNull();
        state = ShopRules.Equip(state, meadow, wear: true);
        state.Inventory.Scene.ShouldBe(meadow.Key);

        ShopRules.Equip(state, Catalog.Find("catalog.ride.saucer")!, wear: true).Inventory.Ride.ShouldBeNull();
        ShopRules.Purchase(RewardsState.Empty with { Thorns = 5_000 }, Catalog.Find("catalog.ride.saucer")!, Day1, 0, Now, NewId).Reason.ShouldBe(ItemAvailability.Locked);
    }

    [Fact]
    public void A_freeze_can_be_bought_once_a_week_while_the_bank_is_below_two()
    {
        var freeze = Catalog.Find(Catalog.FreezeKey)!;
        var state = RewardsState.Empty with { Thorns = 1_000 };

        var first = ShopRules.Purchase(state, freeze, Day1, 0, Now, NewId);
        first.Success.ShouldBeTrue();
        first.State.Thorns.ShouldBe(800);
        first.State.Streak.FreezesBanked.ShouldBe(1);
        first.State.Streak.LastFreezePurchaseDay.ShouldBe(Day1);
        first.Events.Select(e => e.Type).ShouldBe([LedgerEventType.Spend, LedgerEventType.FreezeGrant]);

        ShopRules.Status(first.State, freeze, Day1.AddDays(3), 0).Availability.ShouldBe(ItemAvailability.FreezeCooldown);
        var second = ShopRules.Purchase(first.State, freeze, Day1.AddDays(7), 0, Now, NewId);
        second.Success.ShouldBeTrue();
        second.State.Streak.FreezesBanked.ShouldBe(2);
        ShopRules.Status(second.State, freeze, Day1.AddDays(14), 0).Availability.ShouldBe(ItemAvailability.FreezeBankFull);
    }

    [Fact]
    public void A_banked_freeze_covers_one_missed_day_and_the_streak_stays_alive_meanwhile()
    {
        var streak = StreakRules.Count(StreakState.Empty, Day1) with { FreezesBanked = 1 };
        streak.LengthOn(Day1.AddDays(2)).ShouldBe(1); // one missed day, covered by the bank
        streak.LengthOn(Day1.AddDays(3)).ShouldBe(0); // two missed days, bank too small

        var counted = StreakRules.CountWithFreezes(streak, Day1.AddDays(2));
        counted.FreezesConsumed.ShouldBe(1);
        counted.State.Length.ShouldBe(2);
        counted.State.FreezesBanked.ShouldBe(0);

        var reset = StreakRules.CountWithFreezes(streak, Day1.AddDays(3));
        reset.FreezesConsumed.ShouldBe(0);
        reset.State.Length.ShouldBe(1);
        reset.State.FreezesBanked.ShouldBe(1); // never lost
    }

    [Fact]
    public void The_weekly_freeze_is_granted_once_per_week_up_to_the_cap()
    {
        var granted = StreakRules.GrantWeeklyFreeze(StreakState.Empty, Day1);
        granted.ShouldNotBeNull();
        granted.FreezesBanked.ShouldBe(1);
        StreakRules.GrantWeeklyFreeze(granted, Day1.AddDays(3)).ShouldBeNull(); // same week
        var nextWeek = StreakRules.GrantWeeklyFreeze(granted, Day1.AddDays(7));
        nextWeek.ShouldNotBeNull();
        nextWeek.FreezesBanked.ShouldBe(2);
        StreakRules.GrantWeeklyFreeze(nextWeek, Day1.AddDays(14)).ShouldBeNull(); // bank full
        StreakRules.WeekOf(Day1).ShouldBe(StreakRules.WeekOf(Day1.AddDays(6)));
        StreakRules.WeekOf(Day1).ShouldNotBe(StreakRules.WeekOf(Day1.AddDays(7)));
    }

    [Fact]
    public void Settlement_banks_the_weekly_freeze_counts_learning_days_and_keeps_the_inventory()
    {
        var report = new SessionReport(Guid.NewGuid(), 10, 10, 0, 0, 0, LearningDayCounted: true, WarmupCompleted: false);
        var scarf = Catalog.Find("catalog.outfit.scarf")!;
        var start = ShopRules.Purchase(RewardsState.Empty with { Thorns = 150 }, scarf, Day1, 0, Now, NewId).State;

        var first = RewardsEngine.Settle(start, report, new ProgressCounters(0, 0, 10), Day1, Now, NewId);
        first.FreezeGranted.ShouldBeTrue();
        first.State.Streak.FreezesBanked.ShouldBe(1);
        first.State.LearningDays.ShouldBe(1);
        first.State.Inventory.Outfit.ShouldBe(scarf.Key);
        first.Events.ShouldContain(e => e.Type == LedgerEventType.FreezeGrant);

        // Skip Day1 + 1; the freeze covers it on Day1 + 2 and the streak reaches 2.
        var later = RewardsEngine.Settle(first.State, report with { SessionId = Guid.NewGuid() }, new ProgressCounters(0, 0, 20), Day1.AddDays(2), Now.AddDays(2), NewId);
        later.FreezesConsumed.ShouldBe(1);
        later.StreakAfter.ShouldBe(2);
        later.State.Streak.FreezesBanked.ShouldBe(0);
        later.State.LearningDays.ShouldBe(2);
        later.Events.ShouldContain(e => e.Type == LedgerEventType.FreezeConsume);

        var sameDay = RewardsEngine.Settle(later.State, report with { SessionId = Guid.NewGuid() }, new ProgressCounters(0, 0, 30), Day1.AddDays(2), Now.AddDays(2), NewId);
        sameDay.State.LearningDays.ShouldBe(2);
    }
}
