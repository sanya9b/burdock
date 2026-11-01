using Burdock.Application.Ports;
using Burdock.Application.UseCases.LearningDays;
using Burdock.Domain.Learning;
using Burdock.Domain.Rewards;

namespace Burdock.Application.UseCases.Rewards;

/// <summary>One shop tile: the item with its status for this learner today.</summary>
public sealed record ShopItemView(CatalogItem Item, ItemStatus Status);

/// <summary>Everything S-16 shows (spec 03): balance, the catalogue with statuses, the freeze bank and what is worn.</summary>
public sealed record ShopView(int Thorns, IReadOnlyList<ShopItemView> Items, int FreezesBanked, int MaxFreezes, Inventory Inventory, bool GamificationEnabled);

public enum PurchaseOutcome
{
    Purchased,
    Refused,
    UnknownItem,
}

public sealed record PurchaseView(PurchaseOutcome Outcome, ItemAvailability Reason, RewardsState State, CatalogItem? Item);

public sealed record EquipView(RewardsState State, bool ThemeChanged);

/// <summary>Reads the catalogue with per-item statuses (spec 07 §3.2.3, ShopRules).</summary>
public sealed class GetShopUseCase(ILearnerContext learner, IRewardsStore rewards, ILearnerCardRepository cards, LearningDayResolver learningDays)
{
    public async Task<ShopView> ExecuteAsync(CancellationToken cancellationToken)
    {
        var learnerId = await learner.GetLearnerIdAsync(cancellationToken).ConfigureAwait(false);
        var settings = await learner.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        var state = await rewards.GetAsync(learnerId, cancellationToken).ConfigureAwait(false) ?? RewardsState.Empty;
        var learned = await ShopProgress.LearnedWordsAsync(cards, learnerId, cancellationToken).ConfigureAwait(false);
        var day = learningDays.Today();
        var items = Catalog.All.Select(item => new ShopItemView(item, ShopRules.Status(state, item, day, learned))).ToArray();
        return new ShopView(state.Thorns, items, state.Streak.FreezesBanked, StreakRules.MaxBankedFreezes, state.Inventory, settings.GamificationEnabled);
    }
}

/// <summary>Buys an item (FR-07-13): the rules decide, the store persists state and the spend event atomically.</summary>
public sealed class PurchaseItemUseCase(ILearnerContext learner, IRewardsStore rewards, ILearnerCardRepository cards, LearningDayResolver learningDays, IClock clock, IIdGenerator ids)
{
    public async Task<PurchaseView> ExecuteAsync(string itemKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(itemKey);
        var item = Catalog.Find(itemKey);
        var learnerId = await learner.GetLearnerIdAsync(cancellationToken).ConfigureAwait(false);
        var state = await rewards.GetAsync(learnerId, cancellationToken).ConfigureAwait(false) ?? RewardsState.Empty;
        if (item is null)
        {
            return new PurchaseView(PurchaseOutcome.UnknownItem, ItemAvailability.Locked, state, null);
        }

        var learned = await ShopProgress.LearnedWordsAsync(cards, learnerId, cancellationToken).ConfigureAwait(false);
        var result = ShopRules.Purchase(state, item, learningDays.Today(), learned, clock.UtcNow, ids.NewId);
        if (!result.Success)
        {
            return new PurchaseView(PurchaseOutcome.Refused, result.Reason, state, item);
        }

        await rewards.SaveAsync(learnerId, result.State, result.Events, cancellationToken).ConfigureAwait(false);
        return new PurchaseView(PurchaseOutcome.Purchased, ItemAvailability.Available, result.State, item);
    }
}

/// <summary>Wears or takes off an owned cosmetic; a theme change is reported so the caller can replan the widget timeline.</summary>
public sealed class EquipItemUseCase(ILearnerContext learner, IRewardsStore rewards)
{
    public async Task<EquipView> ExecuteAsync(string itemKey, bool wear, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(itemKey);
        var learnerId = await learner.GetLearnerIdAsync(cancellationToken).ConfigureAwait(false);
        var state = await rewards.GetAsync(learnerId, cancellationToken).ConfigureAwait(false) ?? RewardsState.Empty;
        var item = Catalog.Find(itemKey);
        if (item is null)
        {
            return new EquipView(state, false);
        }

        var next = ShopRules.Equip(state, item, wear);
        if (next == state)
        {
            return new EquipView(state, false);
        }

        await rewards.SaveAsync(learnerId, next, [], cancellationToken).ConfigureAwait(false);
        return new EquipView(next, next.Inventory.Theme != state.Inventory.Theme);
    }
}

internal static class ShopProgress
{
    /// <summary>Words in «Вивчено» or «Знаю», the counter the unlock conditions use (same rule as the session settlement).</summary>
    public static async Task<int> LearnedWordsAsync(ILearnerCardRepository cards, Domain.SharedKernel.LearnerId learnerId, CancellationToken cancellationToken)
    {
        var all = await cards.ListAsync(learnerId, cancellationToken).ConfigureAwait(false);
        return all.Values.Count(c => c.State is CardState.Learned or CardState.Known);
    }
}
