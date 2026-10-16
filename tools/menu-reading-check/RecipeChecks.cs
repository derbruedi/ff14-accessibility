using System.Runtime.InteropServices;
using System.Text;
using FF14Accessibility;
using FF14Accessibility.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;

internal static unsafe class RecipeChecks
{
    internal sealed record Report(int Recipes, int Assertions, int Fallbacks, Dictionary<string, int> FallbackReasons,
        List<string> FallbackExamples);
    internal static Report Run(GameDataReader data, GameDescriptionService descriptions)
    {
        Loc.Mode = LanguageMode.English;
        data.Language = Dalamud.Game.ClientLanguage.English;
        var checkedRecipes = 0; var assertions = 0; var fallbacks = 0;
        var reasons = new Dictionary<string, int>();
        var examples = new List<string>();
        foreach (var row in data.GetExcelSheet<Recipe>())
        {
            if (row.RowId == 0 || row.ItemResult.RowId == 0) continue;
            var definition = RecipeMaterialReader.FromSheet(data, row.RowId);
            if (definition == null)
            {
                fallbacks++;
                var reason = Enumerable.Range(0, row.Ingredient.Count).Any(i =>
                    (row.Ingredient[i].RowId == 0) != (row.AmountIngredient[i] == 0)) ? "UnpairedSlots"
                    : Enumerable.Range(6, 2).Any(i => row.Ingredient[i].RowId > 19) ? "NonCrystalSlots"
                    : "EmptyOrUnknownLayout";
                reasons[reason] = reasons.GetValueOrDefault(reason) + 1;
                if (examples.Count < 5) examples.Add($"{row.RowId}: {row.ItemResult.Value.Name}: "
                    + string.Join(",", Enumerable.Range(0, row.Ingredient.Count)
                        .Select(i => $"{row.Ingredient[i].RowId}/{row.AmountIngredient[i]}")));
                continue;
            }
            RecipeNote.RecipeEntry entry = default;
            entry.ItemId = row.ItemResult.RowId;
            entry.RecipeId = checked((ushort)row.RowId);
            for (var i = 0; i < 6; i++)
            {
                entry.Ingredients[i].ItemId = row.Ingredient[i].RowId;
                entry.Ingredients[i].Amount = checked((byte)row.AmountIngredient[i]);
            }
            for (var i = 0; i < 2; i++) entry.Crystals[i].Amount = checked((byte)row.AmountIngredient[6 + i]);
            Check(RecipeMaterialReader.IsComplete(&entry, definition), "complete recipe");
            for (var i = 0; i < 6; i++)
            {
                var saved = entry.Ingredients[i];
                if (saved.ItemId == 0) continue;
                entry.Ingredients[i] = default;
                Check(!RecipeMaterialReader.IsComplete(&entry, definition), "zero/zero material slot");
                entry.Ingredients[i] = saved;
            }
            for (var i = 0; i < 2; i++)
            {
                var saved = entry.Crystals[i].Amount;
                if (saved == 0) continue;
                entry.Crystals[i].Amount = 0;
                Check(!RecipeMaterialReader.IsComplete(&entry, definition), "missing crystal");
                entry.Crystals[i].Amount = saved;
            }
            entry.ItemId++;
            Check(!RecipeMaterialReader.IsComplete(&entry, definition), "different result item");
            entry.ItemId--;
            if (checkedRecipes < 20)
            {
                // Exercise the real reader, with deliberately stale UI crystal stock.
                var label = descriptions.ItemName(entry.ItemId);
                var bytes = Encoding.UTF8.GetBytes(label + "\0");
                var memory = Marshal.AllocHGlobal(bytes.Length);
                try
                {
                    Marshal.Copy(bytes, 0, memory, bytes.Length);
                    AtkTextNode name = default;
                    name.NodeText.StringPtr = (byte*)memory;
                    name.NodeText.BufUsed = bytes.Length;
                    name.NodeText.BufSize = bytes.Length;
                    entry.ItemName = name.NodeText;
                    AddonRecipeNote addon = default;
                    addon.SelectedRecipeName = &name;
                    addon.Crystals[0].QuantityInInventory = &name; // Not a number; must never be read.
                    var counts = new List<uint>();
                    var lines = RecipeMaterialReader.Read(&addon, &entry, descriptions.ItemName, s => s,
                        (id, _) => { counts.Add(id); return 1234; }, definition);
                    Check(lines.Count == definition.Materials.Length + definition.Crystals.Length, "all material/crystal lines");
                    foreach (var crystal in definition.Crystals)
                    {
                        Check(counts.Contains(crystal.ItemId), "crystal inventory identity");
                        Check(lines.Any(line => line == AccessibilityStrings.RecipeCrystal(descriptions.ItemName(crystal.ItemId),
                            crystal.Amount.ToString(), "1234")), "named crystal stock");
                    }
                }
                finally { Marshal.FreeHGlobal(memory); }
            }
            checkedRecipes++;
            void Check(bool passed, string condition)
            {
                if (!passed) throw new Exception($"Recipe {row.RowId}: {condition}");
                assertions++;
            }
        }
        if (checkedRecipes == 0) throw new Exception("No recipes were checked.");
        return new(checkedRecipes, assertions, fallbacks, reasons, examples);
    }
}
