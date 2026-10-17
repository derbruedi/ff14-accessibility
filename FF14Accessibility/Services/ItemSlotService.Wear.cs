using System;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace FF14Accessibility.Services;

public sealed unsafe partial class ItemSlotService
{
    private readonly IPluginLog _log = null!;
    private Hook<OnItemHoveredDelegate>? _hoverHook;
    private (InventoryType Container, short Slot, uint ItemId) _lastHovered;

    private delegate bool OnItemHoveredDelegate(AgentItemDetail* agent, InventoryItem** outItem,
                                                InventoryType* container, short* slot,
                                                uint itemId, uint rowIdOrIndex, InventoryItem* item);

    /// <summary>
    /// The item instance the game last resolved on hover, or null when it is not
    /// <paramref name="baseItemId"/>. A stale answer about another item is never
    /// handed out. Looked up again from the container rather than kept as a pointer.
    /// </summary>
    public InventoryItem* TryGetHoveredInstance(uint baseItemId)
    {
        var (type, slot, itemId) = _lastHovered;
        if (baseItemId == 0 || GetBaseItemId(itemId) != baseItemId || slot < 0) return null;

        var inventory = InventoryManager.Instance();
        var container = inventory == null ? null : inventory->GetInventoryContainer(type);
        if (container == null || container->Items == null || slot >= container->Size) return null;

        var item = container->Items + slot;
        return GetBaseItemId(item->GetItemId()) == baseItemId ? item : null;
    }

    /// <summary>The live instance behind an owned slot the sort map already resolved.</summary>
    internal static InventoryItem* InstanceAt(OwnedSlot owned)
    {
        if (owned.Status != OwnedSlotStatus.Ready || owned.PhysicalSlot < 0 || owned.ItemId == 0)
            return null;

        var inventory = InventoryManager.Instance();
        var container = inventory == null ? null : inventory->GetInventoryContainer(owned.Container);
        if (container == null || container->Items == null || owned.PhysicalSlot >= container->Size)
            return null;

        var item = container->Items + owned.PhysicalSlot;
        return GetBaseItemId(item->GetItemId()) == owned.ItemId ? item : null;
    }

    private void EnableHoverHook(IGameInteropProvider interop)
    {
        // Hooking reaches into game memory: a signature that stops matching after
        // a patch must disable this lookup only, never the plugin (TooltipService).
        try
        {
            _hoverHook = interop.HookFromAddress<OnItemHoveredDelegate>(
                AgentItemDetail.Addresses.OnItemHovered.Value, OnItemHovered);
            _hoverHook.Enable();
            _log.Info("[ItemSlot] Hook OnItemHovered aktiv.");
        }
        catch (Exception ex)
        {
            _log.Error($"[ItemSlot] Hook OnItemHovered fehlgeschlagen: {ex}");
        }
    }

    private bool OnItemHovered(AgentItemDetail* agent, InventoryItem** outItem, InventoryType* container,
                               short* slot, uint itemId, uint rowIdOrIndex, InventoryItem* item)
    {
        var result = _hoverHook!.Original(agent, outItem, container, slot, itemId, rowIdOrIndex, item);

        // Only a successful resolve replaces the last answer. TryGetHoveredInstance
        // drops it when the item no longer matches.
        var found = outItem == null ? null : *outItem;
        if (result && found != null && container != null && slot != null)
            _lastHovered = (*container, *slot, found->GetItemId());
        return result;
    }

    private void DisposeHoverHook() => _hoverHook?.Dispose();
}
