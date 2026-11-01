using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Burdock.Application.UseCases.Rewards;
using Burdock.Domain.Rewards;
using Burdock.Presentation.Controls;
using Burdock.Presentation.Localization;
using Burdock.Presentation.Resources.Icons;
using Burdock.Presentation.Resources.Strings;
using Burdock.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Burdock.Presentation.ViewModels;

/// <summary>One shop tile (spec 03 S-16): name, price and an open status («Є», «Вдягнуто», «Відкриється на рівні 8», «Не вистачає 40»).</summary>
public sealed partial class ShopTile(ShopItemView view) : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public ShopItemView View { get; } = view;

    public string Key => View.Item.Key;

    public CatalogCategory Category => View.Item.Category;

    public string Name { get; } = RewardsText.ItemName(view.Item);

    public string PriceText { get; } = RewardsText.Price(view.Item);

    public bool Owned => View.Status.Owned;

    public bool Equipped => View.Status.Equipped;

    public bool Locked => View.Status.Availability == ItemAvailability.Locked;

    public bool ShowPrice => !Owned;

    public bool IsOutfit => Category == CatalogCategory.Outfit;

    public bool IsFrame => Category == CatalogCategory.Frame;

    public bool IsTheme => Category == CatalogCategory.WidgetTheme;

    /// <summary>What the tile's little burr wears: the outfit or the frame of this item.</summary>
    public MascotOutfit ThumbOutfit { get; } = MascotCosmetics.OutfitFor(view.Item.Key);

    public MascotFrame ThumbFrame { get; } = MascotCosmetics.FrameFor(view.Item.Key);

    /// <summary>Generated theme name for the swatch (bloom → Tsvit, meadow → Luh, night → Nich, sun → Sontse).</summary>
    public string ThumbTheme { get; } = view.Item.Slug switch { "meadow" => "Luh", "night" => "Nich", "sun" => "Sontse", _ => "Tsvit" };

    /// <summary>Status under the name: worn / owned / short unlock condition; empty when the item is available or merely unaffordable (the preview explains).</summary>
    public string StatusText { get; } = StatusFor(view.Status);

    public string Accessibility => string.Format(CultureInfo.GetCultureInfo("uk-UA"), CompositeFormat.Parse(AppResources.Shop_Tile_A11y), Name, StatusText.Length > 0 ? StatusText : PriceText);

    private static string StatusFor(ItemStatus status) => status.Availability switch
    {
        ItemAvailability.Owned when status.Equipped => status.Item.Category == CatalogCategory.WidgetTheme ? AppResources.Shop_Chosen : AppResources.Shop_Worn,
        ItemAvailability.Owned => AppResources.Shop_Owned,
        ItemAvailability.Locked => RewardsText.UnlockShort(status.Unlock),
        _ => string.Empty,
    };

}

public sealed record ShopSection(string Title, CatalogCategory Category, IReadOnlyList<ShopTile> Tiles);

/// <summary>
/// S-16 «Нагороди» (spec 03; spec 07 §3.2.3, FR-07-12, FR-07-13): the catalogue by sections, a live preview of the burr wearing the selected item,
/// one explicit action per item (buy, wear, take off, choose theme) and the streak freeze bank. No timers, no sales, no randomness.
/// </summary>
public sealed partial class RewardsViewModel : ObservableObject
{
    private static readonly CultureInfo Ukrainian = CultureInfo.GetCultureInfo("uk-UA");
    private static readonly CompositeFormat PurchasedFormat = CompositeFormat.Parse(AppResources.Shop_Purchased_Toast);
    private static readonly CompositeFormat WornFormat = CompositeFormat.Parse(AppResources.Shop_Worn_Toast);
    private static readonly CompositeFormat ThemeFormat = CompositeFormat.Parse(AppResources.Shop_Theme_Toast);

    private readonly GetShopUseCase _getShop;
    private readonly PurchaseItemUseCase _purchase;
    private readonly EquipItemUseCase _equip;
    private readonly IToastService _toast;
    private readonly IWidgetCoordinator _widgets;
    private readonly ILogger<RewardsViewModel> _logger;
    private ShopView? _shop;
    private string? _selectedKey;

    [ObservableProperty]
    private ViewState _state = ViewState.Loading;

    [ObservableProperty]
    private bool _showShop;

    [ObservableProperty]
    private string _thornsText = string.Empty;

    [ObservableProperty]
    private ShopTile? _selected;

    [ObservableProperty]
    private MascotOutfit _previewOutfit;

    [ObservableProperty]
    private MascotFrame _previewFrame;

    [ObservableProperty]
    private string _previewName = string.Empty;

    [ObservableProperty]
    private string _previewStatus = string.Empty;

    [ObservableProperty]
    private string _actionText = string.Empty;

    [ObservableProperty]
    private bool _canAct;

    [ObservableProperty]
    private bool _actionIsPurchase;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _freezeBankedText = string.Empty;

    [ObservableProperty]
    private string _freezeActionText = string.Empty;

    [ObservableProperty]
    private string _freezeHint = string.Empty;

    [ObservableProperty]
    private bool _canBuyFreeze;

    public RewardsViewModel(GetShopUseCase getShop, PurchaseItemUseCase purchase, EquipItemUseCase equip, IToastService toast, IWidgetCoordinator widgets, ILogger<RewardsViewModel> logger)
    {
        _getShop = getShop ?? throw new ArgumentNullException(nameof(getShop));
        _purchase = purchase ?? throw new ArgumentNullException(nameof(purchase));
        _equip = equip ?? throw new ArgumentNullException(nameof(equip));
        _toast = toast ?? throw new ArgumentNullException(nameof(toast));
        _widgets = widgets ?? throw new ArgumentNullException(nameof(widgets));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public ObservableCollection<ShopSection> Sections { get; } = [];

    /// <summary>Every tile of every section, for tests and for the selection sweep.</summary>
    public IEnumerable<ShopTile> Tiles => Sections.SelectMany(s => s.Tiles);

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            _shop = await _getShop.ExecuteAsync(cancellationToken).ConfigureAwait(true);
            Render(_shop);
            State = ViewState.Ready;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogFailed(ex);
            State = ViewState.Error;
        }
    }

    [RelayCommand]
    private void Select(ShopTile? tile)
    {
        if (tile is null)
        {
            return;
        }

        _selectedKey = tile.Key;
        foreach (var t in Tiles)
        {
            t.IsSelected = t.Key == tile.Key;
        }

        Selected = tile;
        RenderPreview();
    }

    /// <summary>The one action of the selected item: buy (and wear), wear, take off, or choose the theme.</summary>
    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task ActAsync(CancellationToken cancellationToken)
    {
        if (Selected is not { } tile || !CanAct)
        {
            return;
        }

        IsBusy = true;
        try
        {
            if (tile.View.Status.Availability == ItemAvailability.Available)
            {
                var result = await _purchase.ExecuteAsync(tile.Key, cancellationToken).ConfigureAwait(true);
                if (result.Outcome == PurchaseOutcome.Purchased)
                {
                    await _toast.ShowAsync(string.Format(Ukrainian, PurchasedFormat, tile.Name), cancellationToken).ConfigureAwait(true);
                    if (tile.Category == CatalogCategory.WidgetTheme)
                    {
                        _ = _widgets.TriggerAsync("theme", CancellationToken.None);
                    }
                }
            }
            else if (tile.Owned)
            {
                var wear = !tile.Equipped;
                var result = await _equip.ExecuteAsync(tile.Key, wear, cancellationToken).ConfigureAwait(true);
                if (result.ThemeChanged)
                {
                    _ = _widgets.TriggerAsync("theme", CancellationToken.None);
                }

                var message = tile.Category == CatalogCategory.WidgetTheme
                    ? string.Format(Ukrainian, ThemeFormat, RewardsText.ItemName(Catalog.Find(result.State.Inventory.Theme) ?? tile.View.Item))
                    : wear ? string.Format(Ukrainian, WornFormat, tile.Name) : AppResources.Shop_TakenOff_Toast;
                await _toast.ShowAsync(message, cancellationToken).ConfigureAwait(true);
            }

            await LoadAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogFailed(ex);
            await _toast.ShowAsync(AppResources.Common_Error_Title, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task BuyFreezeAsync(CancellationToken cancellationToken)
    {
        if (!CanBuyFreeze)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _purchase.ExecuteAsync(Catalog.FreezeKey, cancellationToken).ConfigureAwait(true);
            if (result.Outcome == PurchaseOutcome.Purchased)
            {
                await _toast.ShowAsync(AppResources.Shop_Freeze_Toast, cancellationToken).ConfigureAwait(true);
            }

            await LoadAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogFailed(ex);
            await _toast.ShowAsync(AppResources.Common_Error_Title, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Render(ShopView shop)
    {
        ShowShop = shop.GamificationEnabled;
        ThornsText = RewardsText.Thorns(shop.Thorns);
        Sections.Clear();
        Sections.Add(Section(AppResources.Shop_Section_Outfits, CatalogCategory.Outfit, shop));
        Sections.Add(Section(AppResources.Shop_Section_Themes, CatalogCategory.WidgetTheme, shop));
        Sections.Add(Section(AppResources.Shop_Section_Frames, CatalogCategory.Frame, shop));

        var freeze = shop.Items.First(i => i.Item.Category == CatalogCategory.Freeze).Status;
        FreezeBankedText = RewardsText.FreezeBanked(shop.FreezesBanked, shop.MaxFreezes);
        FreezeActionText = RewardsText.BuyButton(freeze.Item.Price);
        CanBuyFreeze = freeze.Availability == ItemAvailability.Available;
        FreezeHint = freeze.Availability switch
        {
            ItemAvailability.FreezeBankFull => AppResources.Shop_Freeze_Full,
            ItemAvailability.FreezeCooldown => AppResources.Shop_Freeze_Cooldown,
            ItemAvailability.Insufficient => RewardsText.Missing(freeze.Missing),
            _ => string.Empty,
        };

        // Keep the learner's selection across reloads; otherwise start from what the burr wears, then the first outfit.
        var selected = Tiles.FirstOrDefault(t => t.Key == _selectedKey)
            ?? Tiles.FirstOrDefault(t => t.Category == CatalogCategory.Outfit && t.Equipped)
            ?? Tiles.FirstOrDefault(t => t.Category == CatalogCategory.Outfit);
        Select(selected);
    }

    private static ShopSection Section(string title, CatalogCategory category, ShopView shop)
        => new(title, category, shop.Items.Where(i => i.Item.Category == category).Select(i => new ShopTile(i)).ToArray());

    private void RenderPreview()
    {
        var inventory = _shop?.Inventory ?? Inventory.Empty;
        var tile = Selected;
        // The preview wears the selected outfit or frame on top of what is already worn, so the learner sees the item before paying.
        PreviewOutfit = MascotCosmetics.OutfitFor(tile?.Category == CatalogCategory.Outfit ? tile.Key : inventory.Outfit);
        PreviewFrame = MascotCosmetics.FrameFor(tile?.Category == CatalogCategory.Frame ? tile.Key : inventory.Frame);
        if (tile is null)
        {
            PreviewName = string.Empty;
            PreviewStatus = AppResources.Shop_Preview_Hint;
            ActionText = string.Empty;
            CanAct = false;
            ActionIsPurchase = false;
            return;
        }

        var status = tile.View.Status;
        PreviewName = tile.Name;
        PreviewStatus = status.Availability switch
        {
            ItemAvailability.Available => tile.PriceText == AppResources.Shop_Gift ? AppResources.Shop_Gift : RewardsText.Thorns(status.Item.Price),
            ItemAvailability.Insufficient => $"{RewardsText.Missing(status.Missing)}. {AppResources.Shop_Earn_Hint}",
            _ => tile.StatusText,
        };
        ActionIsPurchase = status.Availability == ItemAvailability.Available;
        CanAct = status.Availability == ItemAvailability.Available || status.Owned;
        ActionText = status.Availability switch
        {
            ItemAvailability.Available => RewardsText.BuyButton(status.Item.Price),
            ItemAvailability.Owned when tile.Category == CatalogCategory.WidgetTheme => tile.Equipped
                ? (tile.Key == Catalog.DefaultThemeKey ? AppResources.Shop_Chosen : AppResources.Shop_Default_Button)
                : AppResources.Shop_Choose_Button,
            ItemAvailability.Owned => tile.Equipped ? AppResources.Shop_TakeOff_Button : AppResources.Shop_Wear_Button,
            ItemAvailability.Insufficient => RewardsText.BuyButton(status.Item.Price),
            _ => RewardsText.Unlock(status.Unlock),
        };
        if (tile.Key == Catalog.DefaultThemeKey && tile.Equipped)
        {
            CanAct = false;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Shop failed")]
    private partial void LogFailed(Exception exception);
}
