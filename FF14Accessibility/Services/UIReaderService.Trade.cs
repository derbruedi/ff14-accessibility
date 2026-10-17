using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FF14Accessibility.Services;

public sealed partial class UIReaderService
{
    // Last spoken trade. Cleared when the window hides, so the next trade
    // is read in full even if this addon stays loaded.
    private TradeView? _tradeView;

    /// <summary>
    /// Player trade. The addon text has the partner name, the gil figures and
    /// the buttons. The five item slots are icon-only (desktop dump 2026-10-08,
    /// addon Trade, nodes 8-12 and 19-23). The stacks and the gil live in
    /// <c>InventoryManager.TradeItemsLocal</c> and <c>TradeItemsRemote</c>.
    /// The sixth entry of each array is gil. Those arrays are reused by other
    /// windows, so they are read only while Trade is visible.
    /// </summary>
    private unsafe void OnTradeUpdate(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)(nint)args.Addon;
        if (addon == null || !addon->IsVisible)
        {
            _tradeView = null;
            return;
        }

        var manager = InventoryManager.Instance();
        if (manager == null) return;

        var view = ReadTrade(addon, manager);
        if (!IsTradeReady(view)) return;

        if (_tradeView is null)
            LogTradeGilSlots(manager);

        var speech = TradeOfferSpeech.Next(_tradeView, view, TradeItemName);
        _tradeView = view;
        if (speech == null) return;

        _log.Info($"[Trade] {speech} | local {view.Mine.State} {FormatTradeIds(view.Mine)} | remote {view.Theirs.State} {FormatTradeIds(view.Theirs)}");
        _tolk.SpeakInterrupt(speech);
    }

    private void OnTradeClose(AddonEvent type, AddonArgs args) => _tradeView = null;

    private unsafe TradeView ReadTrade(AtkUnitBase* addon, InventoryManager* manager)
    {
        var partner = manager->TradePartnerNameString?.Trim() ?? string.Empty;
        if (partner.Length == 0)
            partner = ReadAddonNodeText(addon, 17);
        return new TradeView(
            partner,
            ReadTradeSide(manager->TradeItemsLocal, manager->TradeLocalState),
            ReadTradeSide(manager->TradeItemsRemote, manager->TradeRemoteState));
    }

    private static TradeSide ReadTradeSide(Span<InventoryItem> slots, TradeState state)
    {
        var items = new List<TradeGoods>(5);
        var gil = 0;
        var count = Math.Min(slots.Length, 6);
        for (var i = 0; i < count; i++)
        {
            // Ref into the live array: GetItemId is a virtual call on the real slot.
            ref var slot = ref slots[i];
            if (i == 5)
            {
                gil = slot.Quantity;
                continue;
            }

            var raw = slot.GetItemId();
            if (raw == 0 || slot.Quantity <= 0) continue;
            var hq = (slot.Flags & InventoryItem.ItemFlags.HighQuality) != 0;
            items.Add(new TradeGoods(ItemSlotService.GetBaseItemId(raw), slot.Quantity, hq));
        }

        return new TradeSide(items.ToArray(), gil, state);
    }

    private static bool IsTradeReady(TradeView view)
    {
        if (view.Partner.Length > 0) return true;
        if (view.Mine.Items.Length > 0 || view.Theirs.Items.Length > 0) return true;
        if (view.Mine.Gil != 0 || view.Theirs.Gil != 0) return true;
        return !IsTradeIdle(view.Mine.State) || !IsTradeIdle(view.Theirs.State);
    }

    private static bool IsTradeIdle(TradeState state)
        => state == TradeState.NotTrading || (int)state == 0;

    private string TradeItemName(uint itemId) => _descriptions.ItemName(itemId);

    private unsafe void LogTradeGilSlots(InventoryManager* manager)
    {
        var local = manager->TradeItemsLocal;
        var remote = manager->TradeItemsRemote;
        _log.Info($"[Trade] slots local={local.Length} remote={remote.Length} partner='{manager->TradePartnerNameString}'");
        if (local.Length > 5)
            _log.Info($"[Trade] gil local ItemId={local[5].ItemId} GetItemId={local[5].GetItemId()} Quantity={local[5].Quantity}");
        if (remote.Length > 5)
            _log.Info($"[Trade] gil remote ItemId={remote[5].ItemId} GetItemId={remote[5].GetItemId()} Quantity={remote[5].Quantity}");
    }

    private static string FormatTradeIds(TradeSide side)
    {
        if (side.Items.Length == 0) return $"gil={side.Gil}";
        var ids = string.Join(",", side.Items.Select(i => $"{i.ItemId}x{i.Quantity}{(i.HighQuality ? "HQ" : "")}"));
        return $"{ids} gil={side.Gil}";
    }
}
