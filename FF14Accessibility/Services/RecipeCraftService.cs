using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

/// <summary>
/// Answers "what can I make from my bag right now?" for the recipes of the open
/// crafting log (/acc craftable) - the counterpart of the game's own
/// "herstellbar" line, which it computes for the SELECTED recipe only.
///
/// The list cannot be read off the window: the rows carry no mark for it (dump
/// 2026-09-11 - the only row-to-row difference is the crafted mark), and the
/// game has no filter for it either. So it is assembled from the game's own
/// data: <c>RecipeNote.RecipeList</c> holds every recipe of the open notebook
/// division with its ingredients, and <c>InventoryManager.GetInventoryItemCount</c>
/// says how much of an item the player carries (the call InventoryService
/// already uses - both qualities, since crafting accepts either).
///
/// Where the required items come from:
///   - the six material slots, from the RUNTIME entry (<see cref="RecipeNote.RecipeEntry.Ingredients"/>),
///     which is the same list the window shows;
///   - the two crystal slots, from the Recipe SHEET row that
///     <see cref="RecipeNote.RecipeEntry.RecipeId"/> points at. That join is the game's own,
///     not a guess: the ClientStructs doc of QuestManager.IsRecipeComplete names
///     the parameter "The RowId of the Recipe Sheet". The runtime struct is NOT
///     used for the crystals - it carries only an undocumented Id next to the
///     amount, and what that Id counts is not established (docs/game-api.md).
///
/// Every uncertainty counts as "not makeable": an item id that does not appear
/// among the sheet's material slots, a crystal slot outside the crystal item
/// range, an inventory that cannot be read. A false "you can make this" would
/// send a blind player to the crafting window for nothing, and they have no way
/// to see that the mod was wrong.
/// </summary>
public sealed class RecipeCraftService
{
    /// <summary>How many recipe names are spoken before the rest is summed up.
    /// A list of thirty names is unusable as speech.</summary>
    private const int MaxSpokenNames = 8;

    /// <summary>The crystal items of the game (shard/crystal/cluster of the six
    /// elements) are the first items after Gil. A requirement id above this is
    /// not a crystal - then the slot layout assumption does not hold for that
    /// recipe and the recipe stays unmentioned.</summary>
    private const uint HighestCrystalItemId = 19;

    private readonly InventoryService _inventory;
    private readonly IDataManager _data;
    private readonly IGameGui _gameGui;
    private readonly IPluginLog _log;

    public RecipeCraftService(InventoryService inventory, IDataManager data, IGameGui gameGui, IPluginLog log)
    {
        _inventory = inventory;
        _data = data;
        _gameGui = gameGui;
        _log = log;
    }

    /// <summary>
    /// True while the crafting log is open and carries a recipe list. The KEY-
    /// bound announce hangs on this: outside the notebook the same key keeps its
    /// other job.
    /// </summary>
    public unsafe bool IsCraftingLogOpen()
    {
        var handle = _gameGui.GetAddonByName("RecipeNote");
        if (handle.IsNull) return false;
        var addon = (AddonRecipeNote*)(nint)handle;
        if (!addon->AtkUnitBase.IsVisible) return false;

        var note = RecipeNote.Instance();
        return note != null && note->IsRecipeListReady
               && note->RecipeList != null && note->RecipeList->RecipeCount > 0;
    }

    /// <summary>
    /// The announcement: how many recipes of the open log are makeable from the
    /// bag, then their names, first-craft bonus named where it still applies.
    /// Returns the text - the caller speaks it. When the notebook is not open
    /// there is nothing to read and the text says so.
    /// </summary>
    public unsafe string DescribeCraftableFromBag(string hqKey)
    {
        if (!IsCraftingLogOpen()) return AccessibilityStrings.RecipeBagNoLog;

        var data = RecipeNote.Instance()->RecipeList;
        var total = data->RecipeCount;
        if (data->Recipes == null) return AccessibilityStrings.RecipeBagNoLog;

        var agent = AgentRecipeNote.Instance();
        var craftType = agent != null ? agent->SelectedCraftType : -1;

        // The notebook shows one class at a time, but which recipes the array
        // holds is the game's business, not ours. Narrow to the class the window
        // is on when the entries really do carry other classes too; when NO entry
        // matches the agent's class, the two do not line up and the whole list is
        // read instead of claiming "nothing" for a class the game never filtered.
        var narrowed = false;
        for (var i = 0; i < total; i++)
        {
            if (data->Recipes[i].CraftType == craftType) { narrowed = true; break; }
        }
        if (!narrowed)
            _log.Info($"[Rezepte] CraftType {craftType} passt zu keiner der {total} Zeilen - liste alle auf.");

        var sheet = _data.GetExcelSheet<Recipe>();
        var held = new Dictionary<uint, int>();   // NQ+HQ, one snapshot for the whole run
        var heldNq = new Dictionary<uint, int>(); // NQ only, same snapshot
        var names = new List<string>();
        var makeable = 0;
        var listed = 0;
        var needHq = 0;

        for (var i = 0; i < total; i++)
        {
            ref var entry = ref data->Recipes[i];
            if (narrowed && entry.CraftType != craftType) continue;
            listed++;

            if (!HasAllMaterials(ref entry, sheet, held, heldNq, out var hqOnly)) continue;

            makeable++;
            if (hqOnly) needHq++;
            if (names.Count < MaxSpokenNames)
                names.Add(AccessibilityStrings.RecipeBagEntry(entry.ItemName.ToString(),
                                                              IsFirstCraft(entry.RecipeId), hqOnly));
        }

        if (listed == 0) return AccessibilityStrings.RecipeBagNoLog;
        if (makeable == 0) return AccessibilityStrings.RecipeBagNone(listed);

        var parts = new List<string> { AccessibilityStrings.RecipeBagHeader(makeable, listed) };
        parts.AddRange(names);
        if (makeable > names.Count)
            parts.Add(AccessibilityStrings.RecipeBagMore(makeable - names.Count));
        // Only when one of the NAMED recipes is affected: a hint about recipes
        // that were never spoken would be noise.
        if (needHq > 0 && names.Count > 0)
            parts.Add(AccessibilityStrings.RecipeBagHqHint(hqKey));
        return string.Join(". ", parts);
    }

    /// <summary>
    /// True when the bag holds everything this recipe needs. False on every
    /// doubt (see the class comment) - the direction of the error matters.
    /// </summary>
    private unsafe bool HasAllMaterials(ref RecipeNote.RecipeEntry entry, ExcelSheet<Recipe> sheet,
                                        Dictionary<uint, int> held, Dictionary<uint, int> heldNq,
                                        out bool needsHq)
    {
        needsHq = false;
        var materials = RuntimeMaterials(ref entry);
        foreach (var material in materials)
        {
            var have = Held(material.ItemId, held);
            var nq = HeldNq(material.ItemId, heldNq);
            if (have < material.Amount) return false;

            // The bag carries the material, but not as NQ: the log's own count
            // calls the recipe makeable anyway (it counts HQ), while the normal
            // synthesis does not start until HQ material is taken. Marked, not
            // hidden - hiding it would report "nothing makeable" for a recipe
            // the player can in fact make.
            if (nq >= 0 && nq < material.Amount) needsHq = true;
        }

        if (!sheet.TryGetRow(entry.RecipeId, out var row))
        {
            _log.Info($"[Rezepte] Rezept {entry.RecipeId} fehlt im Recipe-Sheet - bleibt ungenannt.");
            return false;
        }

        // Cross-check the join before trusting the sheet's crystal slots: every
        // material the running game lists has to be one of the sheet's first six
        // ingredient slots. If that fails, this row is not the recipe we think it
        // is, and its slots 6/7 would be some other recipe's crystals.
        var sheetMaterials = new HashSet<uint>();
        for (var i = 0; i < 6 && i < row.Ingredient.Count; i++)
        {
            var id = row.Ingredient[i].RowId;
            if (id != 0) sheetMaterials.Add(id);
        }
        foreach (var material in materials)
        {
            if (sheetMaterials.Contains(material.ItemId)) continue;
            _log.Info($"[Rezepte] Rezept {entry.RecipeId}: Material {material.ItemId} steht nicht in den "
                    + "ersten sechs Sheet-Slots - Kristalle ungeprüft, Rezept bleibt ungenannt.");
            return false;
        }

        for (var i = 6; i < row.Ingredient.Count && i < row.AmountIngredient.Count; i++)
        {
            var itemId = row.Ingredient[i].RowId;
            var amount = row.AmountIngredient[i];
            if (itemId == 0 || amount == 0) continue;

            if (itemId > HighestCrystalItemId)
            {
                _log.Info($"[Rezepte] Rezept {entry.RecipeId}: Slot {i} liefert Item {itemId}, "
                        + "keinen Kristall - Rezept bleibt ungenannt.");
                return false;
            }
            var haveCrystal = Held(itemId, held);
            if (haveCrystal < amount) return false;
        }

        return true;
    }

    /// <summary>The filled material slots of the runtime entry. Empty slots stay
    /// in the struct (dump 2026-08-08: the window keeps all six alive and blanks
    /// the unused ones), so both id and amount have to be set.</summary>
    private static List<(uint ItemId, int Amount)> RuntimeMaterials(ref RecipeNote.RecipeEntry entry)
    {
        var result = new List<(uint, int)>();
        for (var i = 0; i < entry.Ingredients.Length; i++)
        {
            var ing = entry.Ingredients[i];
            if (ing.ItemId == 0 || ing.Amount == 0) continue;
            result.Add((ing.ItemId, ing.Amount));
        }
        return result;
    }

    /// <summary>How many of the item the player carries, NQ and HQ together
    /// (crafting accepts either). Cached per command run: the count is one
    /// snapshot, not a value that may drift while the list is being assembled.
    /// -1 = inventory not readable; that counts as "not enough".</summary>
    private int Held(uint itemId, Dictionary<uint, int> held)
    {
        if (held.TryGetValue(itemId, out var known)) return known;
        var count = _inventory.CountOfNqAndHq(itemId);
        held[itemId] = count;
        return count;
    }

    /// <summary>Same snapshot, NQ only (<see cref="InventoryService.CountOf"/> is
    /// documented as the NQ count). Only used to say whether the recipe can be
    /// made from NQ alone, so -1 (unreadable) is passed through as "unknown"
    /// rather than read as a shortage. Cached for the whole run like
    /// <see cref="Held"/>.</summary>
    private int HeldNq(uint itemId, Dictionary<uint, int> heldNq)
    {
        if (heldNq.TryGetValue(itemId, out var known)) return known;
        var count = _inventory.CountOf(itemId);
        heldNq[itemId] = count;
        return count;
    }

    /// <summary>True when the recipe has never been crafted, so the one-off bonus
    /// for the first craft is still open. The game's own answer: ClientStructs doc
    /// of QuestManager.IsRecipeComplete - "Check if a recipe has been crafted
    /// (= completed) before", parameter = RowId of the Recipe sheet.</summary>
    private static bool IsFirstCraft(uint recipeId)
    {
        return !QuestManager.IsRecipeComplete(recipeId);
    }
}
