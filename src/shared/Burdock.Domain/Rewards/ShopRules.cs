using Burdock.Domain.Time;

namespace Burdock.Domain.Rewards;

/// <summary>Why an item cannot be bought right now; the UI shows the reason openly and never as a loss.</summary>
public enum ItemAvailability
{
    Available,
    Owned,
    Insufficient,
    Locked,
    FreezeBankFull,
    FreezeCooldown,
}

public sealed record ItemStatus(CatalogItem Item, ItemAvailability Availability, bool Owned, bool Equipped, int Missing, UnlockCondition Unlock);

public sealed record PurchaseResult(bool Success, ItemAvailability Reason, RewardsState State, IReadOnlyList<LedgerEvent> Events)
{
    public static PurchaseResult Refused(ItemAvailability reason, RewardsState state) => new(false, reason, state, []);
}

/// <summary>Pure shop rules (spec 07 §3.2.3, FR-07-12, FR-07-13): fixed prices, open unlock conditions, no purchase below balance, freeze limits.</summary>
public static class ShopRules
{
    public const int FreezePurchaseCooldownDays = 7;

    public static UnlockContext ContextFor(RewardsState state, int learnedWords)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new UnlockContext(state.Level, state.Streak.Longest, learnedWords, state.LearningDays, state.SessionsCompleted, state.Calendar.ClaimedDays);
    }

    public static ItemStatus Status(RewardsState state, CatalogItem item, LearningDay day, int learnedWords)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(item);
        var context = ContextFor(state, learnedWords);
        var owned = item.Category != CatalogCategory.Freeze && state.Inventory.Owns(item.Key);
        var equipped = state.Inventory.IsEquipped(item);
        var missing = Math.Max(0, item.Price - state.Thorns);
        var availability = Availability(state, item, day, context, owned, missing);
        return new ItemStatus(item, availability, owned, equipped, availability == ItemAvailability.Insufficient ? missing : 0, item.Unlock);
    }

    /// <summary>Buys an item: debits thorns with a <see cref="LedgerEventType.Spend"/> event, adds it to the inventory and wears it (cosmetics) or banks a freeze.</summary>
    public static PurchaseResult Purchase(RewardsState state, CatalogItem item, LearningDay day, int learnedWords, DateTimeOffset now, Func<Guid> newId)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(newId);
        var status = Status(state, item, day, learnedWords);
        if (status.Availability != ItemAvailability.Available)
        {
            return PurchaseResult.Refused(status.Availability, state);
        }

        var events = new List<LedgerEvent>();
        if (item.Price > 0)
        {
            events.Add(new LedgerEvent(newId(), LedgerEventType.Spend, -item.Price, Currency.Thorns, item.Key, null, now, day));
        }

        RewardsState next;
        if (item.Category == CatalogCategory.Freeze)
        {
            events.Add(new LedgerEvent(newId(), LedgerEventType.FreezeGrant, 0, Currency.None, "streak.freeze.purchase", null, now, day));
            next = state with
            {
                Thorns = state.Thorns - item.Price,
                Streak = state.Streak with { FreezesBanked = state.Streak.FreezesBanked + 1, LastFreezePurchaseDay = day },
            };
        }
        else
        {
            next = state with { Thorns = state.Thorns - item.Price, Inventory = Wear(state.Inventory.With(item), item, wear: true) };
        }

        return new PurchaseResult(true, ItemAvailability.Available, next, events);
    }

    /// <summary>Wears or takes off an owned cosmetic; a theme is always worn by one item, so taking it off returns to the default theme.</summary>
    public static RewardsState Equip(RewardsState state, CatalogItem item, bool wear)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(item);
        if (item.Category == CatalogCategory.Freeze || !state.Inventory.Owns(item.Key))
        {
            return state;
        }

        return state with { Inventory = Wear(state.Inventory, item, wear) };
    }

    private static Inventory Wear(Inventory inventory, CatalogItem item, bool wear) => item.Category switch
    {
        CatalogCategory.Outfit => inventory with { Outfit = wear ? item.Key : (inventory.Outfit == item.Key ? null : inventory.Outfit) },
        CatalogCategory.Frame => inventory with { Frame = wear ? item.Key : (inventory.Frame == item.Key ? null : inventory.Frame) },
        CatalogCategory.WidgetTheme => inventory with { Theme = wear ? item.Key : (inventory.Theme == item.Key ? Catalog.DefaultThemeKey : inventory.Theme) },
        CatalogCategory.Scene => inventory with { Scene = wear ? item.Key : (inventory.Scene == item.Key ? null : inventory.Scene) },
        CatalogCategory.Ride => inventory with { Ride = wear ? item.Key : (inventory.Ride == item.Key ? null : inventory.Ride) },
        _ => inventory,
    };

    private static ItemAvailability Availability(RewardsState state, CatalogItem item, LearningDay day, UnlockContext context, bool owned, int missing)
    {
        if (owned)
        {
            return ItemAvailability.Owned;
        }

        if (!item.Unlock.IsMet(context))
        {
            return ItemAvailability.Locked;
        }

        if (item.Category == CatalogCategory.Freeze)
        {
            if (state.Streak.FreezesBanked >= StreakRules.MaxBankedFreezes)
            {
                return ItemAvailability.FreezeBankFull;
            }

            if (state.Streak.LastFreezePurchaseDay is { } last && day.DaysSince(last) < FreezePurchaseCooldownDays)
            {
                return ItemAvailability.FreezeCooldown;
            }
        }

        return missing > 0 ? ItemAvailability.Insufficient : ItemAvailability.Available;
    }
}
