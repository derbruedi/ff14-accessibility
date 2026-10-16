using System;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

/// <summary>Reads one selected recipe; callbacks keep live inventory counts
/// separate from the byte-sized NQ/HQ fields in RecipeIngredient.</summary>
internal static unsafe class RecipeMaterialReader
{
    internal sealed record Definition(uint ResultItemId, (uint ItemId, int Amount)[] Materials,
        (uint ItemId, int Amount)[] Crystals);

    internal static Definition? FromSheet(IDataManager data, uint recipeId)
    {
        if (recipeId == 0 || !data.GetExcelSheet<Recipe>().TryGetRow(recipeId, out var row)
            || row.ItemResult.RowId == 0 || row.Ingredient.Count != 8 || row.AmountIngredient.Count != 8)
            return null;
        var materials = new List<(uint, int)>();
        var crystals = new List<(uint, int)>();
        for (var i = 0; i < 8; i++)
        {
            var id = row.Ingredient[i].RowId;
            var amount = row.AmountIngredient[i];
            // Empty crystal RowRefs use -1 (uint.MaxValue) in real Recipe rows.
            if (i >= 6 && amount == 0 && id == uint.MaxValue) continue;
            if ((id == 0) != (amount == 0)) return null;
            if (id == 0) continue;
            if (i < 6) materials.Add((id, amount));
            else
            {
                if (id is < 2 or > 19) return null;
                crystals.Add((id, amount));
            }
        }
        return materials.Count == 0 ? null : new(row.ItemResult.RowId, materials.ToArray(), crystals.ToArray());
    }

    internal static bool IsComplete(RecipeNote.RecipeEntry* recipe, Definition? definition)
    {
        if (recipe == null || definition == null || recipe->ItemId != definition.ResultItemId) return false;
        var materials = new List<(uint ItemId, int Amount)>();
        foreach (var ing in recipe->Ingredients)
        {
            if ((ing.ItemId == 0) != (ing.Amount == 0)) return false;
            if (ing.ItemId != 0) materials.Add((ing.ItemId, ing.Amount));
        }
        // A zero/zero slot can be unused OR still loading. Only the full recipe
        // sheet can distinguish those cases, including recipes for the same item.
        if (!materials.OrderBy(x => x.ItemId).ThenBy(x => x.Amount).SequenceEqual(
            definition.Materials.OrderBy(x => x.ItemId).ThenBy(x => x.Amount))) return false;
        var amounts = new List<int>();
        foreach (var crystal in recipe->Crystals)
            if (crystal.Amount > 0) amounts.Add(crystal.Amount);
        // RecipeCrystal.Id is undocumented; obtain identities from Recipe,
        // never infer an item ID from this field or a stale UI icon.
        return amounts.Order().SequenceEqual(definition.Crystals.Select(x => x.Amount).Order());
    }

    internal static bool MatchesSelection(string visible, string original, string localized) =>
        !string.IsNullOrWhiteSpace(visible) &&
        (string.Equals(visible.Trim(), original.Trim(), StringComparison.OrdinalIgnoreCase) ||
         string.Equals(visible.Trim(), localized.Trim(), StringComparison.OrdinalIgnoreCase));

    internal static List<string> Read(AddonRecipeNote* addon, RecipeNote.RecipeEntry* recipe,
        Func<uint, string> itemName, Func<string, string> translateLabel,
        Func<uint, bool, int> inventoryCount, Definition? definition = null)
    {
        var lines = new List<string>();
        var useRuntime = IsComplete(recipe, definition) && MatchesSelection(
            AtkText.ReadClean(addon->SelectedRecipeName), AtkText.ReadClean(&recipe->ItemName),
            Loc.IsRussian ? itemName(recipe->ItemId) : string.Empty);

        if (useRuntime)
        {
            foreach (var ing in recipe->Ingredients)
            {
                if (ing.ItemId == 0 || ing.Amount == 0) continue;
                // Names can still be loading in RecipeNote; the confirmed item
                // ID is sufficient to read the name and the full bag stock.
                lines.Add(AccessibilityStrings.RecipeMaterial(itemName(ing.ItemId),
                    ing.Amount.ToString(), Count(ing.ItemId, false), Count(ing.ItemId, true)));
            }
        }

        if (lines.Count == 0)
        {
            foreach (var ing in addon->Ingredients)
            {
                var name = AtkText.ReadClean(ing.Name).Trim();
                if (name.Length == 0) continue;
                lines.Add(AccessibilityStrings.RecipeMaterial(translateLabel(name),
                    AtkText.ReadClean(ing.QuantityRequiredForCraft),
                    AtkText.ReadClean(ing.QuantityInInventoryNq),
                    AtkText.ReadClean(ing.QuantityInInventoryHq)));
            }
        }

        if (useRuntime)
        {
            foreach (var crystal in definition!.Crystals)
                lines.Add(AccessibilityStrings.RecipeCrystal(itemName(crystal.ItemId),
                    crystal.Amount.ToString(), Count(crystal.ItemId, false)));
            return lines;
        }

        for (var i = 0; i < addon->Crystals.Length; i++)
        {
            var crystal = addon->Crystals[i];
            var needed = AtkText.ReadClean(crystal.QuantityRequiredForCraft).Trim();
            if (needed.Length == 0 || needed == "0") continue;
            lines.Add(AccessibilityStrings.RecipeCrystal(needed,
                AtkText.ReadClean(crystal.QuantityInInventory)));
        }
        return lines;

        string Count(uint itemId, bool hq)
        {
            var value = inventoryCount(itemId, hq);
            return value >= 0 ? value.ToString() : string.Empty;
        }
    }
}
