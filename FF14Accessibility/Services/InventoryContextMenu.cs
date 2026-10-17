using System;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FF14Accessibility.Services;

internal enum InventoryContextResult
{
    Opened,
    Pending,
    Changed,
    NoOwner,
    Failed,
}

/// <summary>
/// Picks the addon that owns the item context menu. A visible gil shop wins,
/// so the game can offer Sell. Otherwise the bag window owns Use, Equip and
/// the other commands the game puts on that item.
/// </summary>
internal static class InventoryContextOwner
{
    internal static uint Choose(uint shopAddonId, uint bagAddonId)
        => shopAddonId != 0 ? shopAddonId : bagAddonId;

    /// <summary>
    /// A hidden inventory addon is not an owner. The game draws the context
    /// menu only for a window that is on screen. Zero means the caller asks
    /// the game to open the inventory.
    /// </summary>
    internal static bool NeedsGameWindow(uint shopAddonId, uint visibleBagAddonId)
        => Choose(shopAddonId, visibleBagAddonId) == 0;
}

/// <summary>
/// Opens the game's own item context menu for one physical bag slot.
/// Does not choose a row and does not confirm a sale.
/// </summary>
internal sealed unsafe class InventoryContextMenu
{
    // MainCommand sheet row 10 of this install, dumped 2026-10-06 from sqpack:
    // English "Inventory", German "Inventar", category 1 (Character).
    // Same UIModule path as the companion command. The I key uses this row.
    private const uint InventoryMainCommand = 10;
    private static readonly string[] BagAddons = ["Inventory", "InventoryLarge", "InventoryExpansion"];
    private readonly IGameGui _gui;
    private readonly IPluginLog _log;
    private Pending? _pending;
    private bool _contextRequested;

    private readonly record struct Pending(InventoryType Type, int Slot, uint ItemId, DateTime UntilUtc);

    public InventoryContextMenu(IGameGui gui, IPluginLog log)
    {
        _gui = gui;
        _log = log;
    }

    public InventoryContextResult TryOpen(uint container, int slot, uint itemId)
    {
        if (slot < 0 || itemId == 0) return InventoryContextResult.Failed;
        var manager = InventoryManager.Instance();
        if (manager == null) return InventoryContextResult.Failed;
        var type = (InventoryType)container;
        if (!SlotStillHolds(manager, type, slot, itemId)) return InventoryContextResult.Changed;
        if (Visible("ContextMenu") != null) return InventoryContextResult.Failed;

        var shop = AddonId("Shop", visibleOnly: true);
        var bag = FirstBag(visibleOnly: true);
        if (!InventoryContextOwner.NeedsGameWindow(shop, bag))
        {
            ClearPending();
            return OpenFor(type, slot, itemId, shop, bag);
        }

        // The closed bag still has an addon id, and OpenForItemSlot accepts it,
        // but the game does not draw the menu unless that window is visible
        // (dalamud.log 2026-10-06 23:34, owner 16, bagVisible=False).
        // Ask once. A second press while waiting only retargets the slot:
        // the command toggles, so it must not be sent again.
        if (_pending == null && !RequestInventoryWindow()) return InventoryContextResult.Failed;
        _pending = new Pending(type, slot, itemId, DateTime.UtcNow.AddSeconds(2));
        _contextRequested = false;
        var immediate = StepPending();
        if (immediate == null)
        {
            _log.Info($"[Inventory] Context waiting: {type}/{slot} item={itemId}.");
            return InventoryContextResult.Pending;
        }
        return immediate.Value;
    }

    /// <summary>
    /// Opens the context menu once the game has shown the inventory window.
    /// Returns false while that window is still on its way.
    /// </summary>
    public bool TryFinishPending(out InventoryContextResult result)
    {
        result = InventoryContextResult.Failed;
        if (_pending == null) return false;
        try
        {
            var step = StepPending();
            if (step == null) return false;
            result = step.Value;
            return true;
        }
        catch (Exception ex)
        {
            ClearPending();
            _log.Error($"[Inventory] Context wait failed: {ex.Message}");
            result = InventoryContextResult.Failed;
            return true;
        }
    }

    /// <summary>
    /// Advances a closed-bag open. Null means the game has not drawn the
    /// menu yet. An agent match alone is not enough: the hidden owner matched
    /// and still drew nothing.
    /// </summary>
    private InventoryContextResult? StepPending()
    {
        var pending = _pending!.Value;
        if (Visible("ContextMenu") != null)
        {
            ClearPending();
            _log.Info("[Inventory] Context menu visible.");
            return InventoryContextResult.Opened;
        }
        var manager = InventoryManager.Instance();
        if (manager == null || !SlotStillHolds(manager, pending.Type, pending.Slot, pending.ItemId))
        {
            ClearPending();
            return manager == null ? InventoryContextResult.Failed : InventoryContextResult.Changed;
        }
        var shop = AddonId("Shop", visibleOnly: true);
        var bag = FirstBag(visibleOnly: true);
        if (!_contextRequested && !InventoryContextOwner.NeedsGameWindow(shop, bag))
        {
            var opened = OpenFor(pending.Type, pending.Slot, pending.ItemId, shop, bag);
            if (opened != InventoryContextResult.Opened)
            {
                ClearPending();
                return opened;
            }
            _contextRequested = true;
            if (Visible("ContextMenu") != null)
            {
                ClearPending();
                return InventoryContextResult.Opened;
            }
        }
        if (DateTime.UtcNow < pending.UntilUtc) return null;
        ClearPending();
        LogMissingWindow();
        return InventoryContextResult.Failed;
    }

    private void ClearPending()
    {
        _pending = null;
        _contextRequested = false;
    }

    private InventoryContextResult OpenFor(
        InventoryType type, int slot, uint itemId, uint shop, uint bag)
    {
        var owner = InventoryContextOwner.Choose(shop, bag);
        if (owner == 0) return InventoryContextResult.NoOwner;
        var agent = AgentInventoryContext.Instance();
        if (agent == null) return InventoryContextResult.Failed;
        agent->OpenForItemSlot(type, slot, 0, owner);
        var matched = agent->TargetInventoryId == type
            && agent->TargetInventorySlotId == slot
            && agent->OwnerAddonId == owner
            && agent->TargetInventorySlot != null
            && agent->TargetInventorySlot->GetItemId() == itemId;
        _log.Info($"[Inventory] Context {(matched ? "opened" : "rejected")}: " +
                  $"{type}/{slot} item={itemId} owner={owner} shop={shop != 0} bagVisible={bag != 0}.");
        if (!matched)
        {
            CloseIfOwned(owner, type, slot);
            return InventoryContextResult.Failed;
        }
        return InventoryContextResult.Opened;
    }

    private bool RequestInventoryWindow()
    {
        var ui = UIModule.Instance();
        if (ui == null)
        {
            _log.Warning("[Inventory] UIModule.Instance() is null");
            return false;
        }
        ui->ExecuteMainCommand(InventoryMainCommand);
        _log.Info("[Inventory] ExecuteMainCommand(10) requested");
        return true;
    }

    private void LogMissingWindow()
    {
        var agent = AgentInventory.Instance();
        if (agent == null)
        {
            _log.Info("[Inventory] Context window did not appear.");
            return;
        }
        _log.Info("[Inventory] Context window did not appear: " +
                  $"shown={agent->IsAddonShown()} openAddon={agent->OpenAddonId} addon={agent->AddonId}.");
    }

    private static bool SlotStillHolds(InventoryManager* manager, InventoryType type, int slot, uint itemId)
    {
        var live = manager->GetInventorySlot(type, slot);
        return live != null && live->GetItemId() == itemId;
    }

    private uint FirstBag(bool visibleOnly)
    {
        foreach (var name in BagAddons)
        {
            var id = AddonId(name, visibleOnly);
            if (id != 0) return id;
        }
        return 0;
    }

    private uint AddonId(string name, bool visibleOnly)
    {
        var addon = visibleOnly ? Visible(name) : Any(name);
        if (addon == null || addon->Id <= 0) return 0;
        return (uint)addon->Id;
    }

    private void CloseIfOwned(uint owner, InventoryType type, int slot)
    {
        var context = Visible("ContextMenu");
        var agent = AgentInventoryContext.Instance();
        if (context == null || agent == null) return;
        if (agent->OwnerAddonId != owner || agent->TargetInventoryId != type
            || agent->TargetInventorySlotId != slot) return;
        context->Close(true);
    }

    private AtkUnitBase* Visible(string name)
    {
        var addon = Any(name);
        return addon != null && addon->IsVisible ? addon : null;
    }

    private AtkUnitBase* Any(string name)
    {
        var ptr = _gui.GetAddonByName(name);
        return ptr.IsNull ? null : (AtkUnitBase*)(nint)ptr;
    }
}
