using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Inventory;
using Dalamud.Plugin.Services;
using LuminaEventItem = Lumina.Excel.Sheets.EventItem;
using LuminaItem = Lumina.Excel.Sheets.Item;

namespace FF14Accessibility.Services;

/// <summary>
/// Reads the player's inventory aloud so a blind player can find the item a
/// quest asks for. Item data comes straight from Dalamud's IGameInventory
/// (no UI scraping, so it works even while the bag window is closed); names
/// resolve through the Lumina Item sheet, key items through EventItem.
/// Verified (ilspycmd): IGameInventory.GetInventoryItems(GameInventoryType)
/// returns ReadOnlySpan&lt;GameInventoryItem&gt; with ItemId/BaseItemId/
/// Quantity/IsHq/IsEmpty; key items live in the KeyItems container and index
/// the EventItem sheet.
/// </summary>
public sealed class InventoryService
{
    private readonly IGameInventory _inventory;
    private readonly IDataManager _data;
    private readonly TolkService _tolk;
    private readonly IPluginLog _log;

    // The four 35-slot pages that make up the normal carried inventory.
    private static readonly GameInventoryType[] BagPages =
    {
        GameInventoryType.Inventory1, GameInventoryType.Inventory2,
        GameInventoryType.Inventory3, GameInventoryType.Inventory4,
    };

    public InventoryService(IGameInventory inventory, IDataManager data, TolkService tolk, IPluginLog log)
    {
        _inventory = inventory;
        _data = data;
        _tolk = tolk;
        _log = log;
    }

    /// <summary>
    /// Announces the whole inventory: key items first (quests usually need
    /// those), then the bag contents. Stacks read as "name mal count".
    /// </summary>
    public void ReadInventory()
    {
        var gil      = GetGil();
        var keyItems = CollectKeyItems();
        var bagItems = CollectBagItems();

        if (gil < 0 && keyItems.Count == 0 && bagItems.Count == 0)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.InventoryEmpty);
            return;
        }

        var parts = new List<string>();
        if (gil >= 0)
            parts.Add($"{ResolveItemName(1)}: {gil}");
        if (keyItems.Count > 0)
            parts.Add(AccessibilityStrings.KeyItemsLabel(string.Join(", ", keyItems)));
        if (bagItems.Count > 0)
            parts.Add(AccessibilityStrings.BagLabel(bagItems.Count, string.Join(", ", bagItems)));

        _tolk.SpeakInterrupt(string.Join(". ", parts) + ".");
    }

    /// <summary>
    /// Announces only the current gil - a quick check without reading the whole
    /// inventory (bound to its own key so the user does not have to sit through
    /// the full Strg+F3 readout).
    /// </summary>
    public void AnnounceGil()
    {
        var gil = GetGil();
        _tolk.SpeakInterrupt(gil >= 0
            ? $"{ResolveItemName(1)}: {gil}"
            : AccessibilityStrings.GilUnavailable);
    }

    /// <summary>
    /// Current gil: the currency item with id 1 in the Currency container.
    /// Quantity is int (max ~2.1e9), covering gil's 999,999,999 cap. -1 if the
    /// entry is missing (e.g. read before the inventory is loaded). The label is
    /// pulled from the Item sheet (row 1 = "Gil"), so the announced word is
    /// game-sourced rather than hard-coded.
    /// </summary>
    private int GetGil()
    {
        foreach (var item in _inventory.GetInventoryItems(GameInventoryType.Currency))
        {
            if (item.ItemId != 1) continue;
            _log.Info($"[Inventory] Currency Gil id={item.ItemId} qty={item.Quantity}");
            return item.Quantity;
        }
        return -1;
    }

    /// <summary>Non-empty stacks in the four bag pages, resolved via the Item sheet.</summary>
    private List<string> CollectBagItems()
    {
        var result = new List<string>();
        foreach (var page in BagPages)
        {
            foreach (var item in _inventory.GetInventoryItems(page))
            {
                if (item.IsEmpty || item.ItemId == 0) continue;

                var name = ResolveItemName(item.BaseItemId);
                var hq = item.IsHq ? AccessibilityStrings.HighQuality : string.Empty;
                _log.Info($"[Inventory] {page} slot={item.InventorySlot} id={item.ItemId} " +
                          $"qty={item.Quantity} hq={item.IsHq} name='{name}'");
                result.Add(item.Quantity > 1 ? AccessibilityStrings.ItemStack(name, item.Quantity, hq) : $"{name}{hq}");
            }
        }
        return result;
    }

    /// <summary>
    /// One carried item that can be placed on a hotbar slot.
    /// <paramref name="ItemId"/> is the id the GAME uses, HQ offset already
    /// applied - that is the value a hotbar slot must hold, so nothing is
    /// recomputed here. <paramref name="BaseItemId"/> only serves sheet lookups.
    /// </summary>
    public readonly record struct UsableItem(
        uint ItemId, uint BaseItemId, string Name, int Quantity, bool IsHq);

    /// <summary>
    /// The carried items that can actually be put on a hotbar: bag contents
    /// whose Item sheet row has an ItemAction. That column is the game's own
    /// mark for "this item does something when used" - it covers medicines,
    /// food, orchestrion rolls and minion whistles without the plugin keeping a
    /// hand-written category list (offline sheet dump 2026-08-06: 4987 of 50773
    /// named items, led by Arznei/Gericht/Verschiedenes).
    /// Identical stacks across bag pages are merged, HQ kept apart from NQ
    /// because they are different ids and the player may own both.
    /// </summary>
    public List<UsableItem> CollectUsableItems()
    {
        var merged = new Dictionary<uint, UsableItem>();
        foreach (var page in BagPages)
        {
            foreach (var item in _inventory.GetInventoryItems(page))
            {
                if (item.IsEmpty || item.ItemId == 0) continue;
                if (!_data.GetExcelSheet<LuminaItem>().TryGetRow(item.BaseItemId, out var row)) continue;
                if (row.ItemAction.RowId == 0) continue;   // not usable

                var name = row.Name.ExtractText();
                if (string.IsNullOrWhiteSpace(name)) continue;

                if (merged.TryGetValue(item.ItemId, out var seen))
                {
                    merged[item.ItemId] = seen with { Quantity = seen.Quantity + item.Quantity };
                }
                else
                {
                    merged[item.ItemId] = new UsableItem(
                        item.ItemId, item.BaseItemId, name, item.Quantity, item.IsHq);
                }
            }
        }

        var result = merged.Values.OrderBy(i => i.Name).ThenBy(i => i.IsHq).ToList();
        _log.Info($"[Inventory] Belegbare Gegenstaende: {result.Count} " +
                  $"({string.Join(", ", result.Take(5).Select(i => $"{i.Name}{(i.IsHq ? " HQ" : string.Empty)} x{i.Quantity} id={i.ItemId}"))})");
        return result;
    }

    /// <summary>Non-empty key items, resolved via the EventItem sheet.</summary>
    private List<string> CollectKeyItems()
    {
        var result = new List<string>();
        foreach (var item in _inventory.GetInventoryItems(GameInventoryType.KeyItems))
        {
            if (item.IsEmpty || item.ItemId == 0) continue;

            var name = ResolveKeyItemName(item.ItemId);
            _log.Info($"[Inventory] KeyItems slot={item.InventorySlot} id={item.ItemId} " +
                      $"qty={item.Quantity} name='{name}'");
            result.Add(item.Quantity > 1 ? AccessibilityStrings.ItemStack(name, item.Quantity, string.Empty) : name);
        }
        return result;
    }

    // Everything the player owns that has an icon slot in the UI: bags, key
    // items, worn gear and the armoury chest (Dalamud GameInventoryType values
    // ilspycmd-verified 2026-07-16) - so character-window and armoury slots
    // resolve against the player's own items instead of the sheet fallback.
    private static readonly GameInventoryType[] GearContainers =
    {
        GameInventoryType.EquippedItems,
        GameInventoryType.ArmoryMainHand, GameInventoryType.ArmoryOffHand,
        GameInventoryType.ArmoryHead,     GameInventoryType.ArmoryBody,
        GameInventoryType.ArmoryHands,    GameInventoryType.ArmoryWaist,
        GameInventoryType.ArmoryLegs,     GameInventoryType.ArmoryFeets,
        GameInventoryType.ArmoryEar,      GameInventoryType.ArmoryNeck,
        GameInventoryType.ArmoryWrist,    GameInventoryType.ArmoryRings,
        GameInventoryType.ArmorySoulCrystal,
    };

    /// <summary>
    /// Maps item icon ids to names for everything the player currently owns.
    /// Hand-over grids (Request/InventoryEventGrid) show icon-only slots with
    /// NO text in the UI, so the icon id is the only link to the item - we
    /// resolve it against the player's own items (collisions are practically
    /// impossible within a single bag). Rebuilt per call so it reflects the
    /// current inventory.
    /// </summary>
    public Dictionary<uint, string> BuildIconNameMap()
    {
        var map = new Dictionary<uint, string>();
        foreach (var (icon, entry) in BuildOwnedIconMap())
            map[icon] = entry.Name;
        return map;
    }

    /// <summary>Icon id -> (name, item id) for all owned items. Key items carry
    /// ItemId 0 - they index the EventItem sheet, not Item, and have no gear data.</summary>
    private Dictionary<uint, (string Name, uint ItemId)> BuildOwnedIconMap()
    {
        var map = new Dictionary<uint, (string, uint)>();

        foreach (var page in BagPages)
            AddIconEntries(map, page);
        foreach (var container in GearContainers)
            AddIconEntries(map, container);

        foreach (var item in _inventory.GetInventoryItems(GameInventoryType.KeyItems))
        {
            if (item.IsEmpty || item.ItemId == 0) continue;
            if (_data.GetExcelSheet<LuminaEventItem>().TryGetRow(item.ItemId, out var row))
            {
                var name = row.Name.ExtractText();
                if (!string.IsNullOrWhiteSpace(name)) map[row.Icon] = (name, 0);
            }
        }

        return map;
    }

    private void AddIconEntries(Dictionary<uint, (string, uint)> map, GameInventoryType container)
    {
        foreach (var item in _inventory.GetInventoryItems(container))
        {
            if (item.IsEmpty || item.ItemId == 0) continue;
            if (_data.GetExcelSheet<LuminaItem>().TryGetRow(item.BaseItemId, out var row))
            {
                var name = row.Name.ExtractText();
                if (!string.IsNullOrWhiteSpace(name)) map[row.Icon] = (name, item.BaseItemId);
            }
        }
    }

    private Dictionary<uint, (string Name, uint ItemId)>? _iconSheetCache;

    /// <summary>
    /// Resolves an item icon id to a name for the focus auto-announce. Prefers
    /// the player's own items (no icon collisions within one bag); falls back to
    /// a full Item/EventItem sheet reverse lookup (built once, cached) so quest
    /// REWARD items - which are not in the bag yet - resolve too. "" if unknown.
    /// </summary>
    public string ResolveIconName(uint iconId) => ResolveIconItem(iconId).Name;

    /// <summary>Like ResolveIconName, but also returns the Item sheet row id so
    /// callers can announce gear data. ItemId 0 = no Item row (unknown/key item).</summary>
    public (string Name, uint ItemId) ResolveIconItem(uint iconId)
    {
        if (iconId == 0) return (string.Empty, 0);

        if (BuildOwnedIconMap().TryGetValue(iconId, out var owned)) return owned;

        _iconSheetCache ??= BuildIconSheetCache();
        return _iconSheetCache.TryGetValue(iconId, out var sheet) ? sheet : (string.Empty, 0);
    }

    /// <summary>The tooltip description of an Item sheet row, or "" when there is
    /// none (itemId 0, key items, or a row without a description). Raw sheet text -
    /// the caller flattens line breaks for speech.</summary>
    public string ResolveItemDescription(uint itemId)
    {
        if (itemId == 0) return string.Empty;
        if (_data.GetExcelSheet<LuminaItem>().TryGetRow(itemId, out var row))
        {
            var desc = row.Description.ExtractText();
            if (!string.IsNullOrWhiteSpace(desc)) return desc;
        }
        return string.Empty;
    }

    private Dictionary<uint, (string Name, uint ItemId)> BuildIconSheetCache()
    {
        var map = new Dictionary<uint, (string, uint)>();
        foreach (var row in _data.GetExcelSheet<LuminaItem>())
        {
            if (row.Icon == 0) continue;
            var name = row.Name.ExtractText();
            if (!string.IsNullOrWhiteSpace(name)) map[row.Icon] = (name, row.RowId);
        }
        foreach (var row in _data.GetExcelSheet<LuminaEventItem>())
        {
            if (row.Icon == 0) continue;
            var name = row.Name.ExtractText();
            if (!string.IsNullOrWhiteSpace(name)) map.TryAdd(row.Icon, (name, 0));
        }
        _log.Info($"[Inventory] Icon-Sheet-Cache gebaut: {map.Count} Einträge.");
        return map;
    }

    private string ResolveItemName(uint baseItemId)
    {
        if (_data.GetExcelSheet<LuminaItem>().TryGetRow(baseItemId, out var row))
        {
            var name = row.Name.ExtractText();
            if (!string.IsNullOrWhiteSpace(name)) return name;
        }
        return AccessibilityStrings.ItemFallback(baseItemId);
    }

    private string ResolveKeyItemName(uint id)
    {
        if (_data.GetExcelSheet<LuminaEventItem>().TryGetRow(id, out var row))
        {
            var name = row.Name.ExtractText();
            if (!string.IsNullOrWhiteSpace(name)) return name;
        }
        return AccessibilityStrings.KeyItemFallback(id);
    }
}
