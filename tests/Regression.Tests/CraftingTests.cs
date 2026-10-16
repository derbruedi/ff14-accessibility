using System.Runtime.InteropServices;
using System.Text;
using FF14Accessibility;
using FF14Accessibility.Services;
using Dalamud.Plugin.Services;
using Dalamud.Game.ClientState.Objects.SubKinds;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Regression.Tests;

[Collection("Language")]
public unsafe sealed class CraftingTests : IDisposable
{
    private readonly LanguageMode _previous = Loc.Mode;
    private readonly List<nint> _allocations = [];

    public CraftingTests() => Loc.Mode = LanguageMode.Russian;

    public void Dispose()
    {
        Loc.Mode = _previous;
        foreach (var ptr in _allocations) Marshal.FreeHGlobal(ptr);
    }

    [Theory]
    [InlineData("Maple Lumber")]
    [InlineData("Кленовый пиломатериал")]
    public void RequiredAmountSurvivesEmptyUiAndRuntimeMaterialNames(string visible)
    {
        AddonRecipeNote addon = default;
        addon.SelectedRecipeName = Text(visible);
        RecipeNote.RecipeEntry recipe = default;
        recipe.ItemName = Text("Maple Lumber")->NodeText;
        recipe.ItemId = 100;
        recipe.Ingredients[0].ItemId = 200;
        recipe.Ingredients[0].Amount = 3;
        // These byte counters are not the full inventory stock.
        recipe.Ingredients[0].NQCount = 1;
        recipe.Ingredients[0].HQCount = 2;
        var requests = new List<(uint, bool)>();
        var result = RecipeMaterialReader.Read(&addon, &recipe,
            id => id == 100 ? "Кленовый пиломатериал" : "Кленовое бревно", s => s,
            (id, hq) => { requests.Add((id, hq)); return hq ? 7 : 1200; },
            new(100, [(200, 3)], []));
        Assert.Equal("Кленовое бревно, нужно 3, есть 1207, из них обычных 1200, высокого качества 7", Assert.Single(result));
        Assert.Equal(new[] { (200u, false), (200u, true) }, requests);
    }

    [Fact]
    public void ChangingRecipeNeverMixesOldRequirementsWithVisibleMaterialsOrCrystals()
    {
        AddonRecipeNote addon = default;
        addon.SelectedRecipeName = Text("New recipe");
        addon.Ingredients[0].Name = Text("New material");
        addon.Ingredients[0].QuantityRequiredForCraft = Text("4");
        addon.Ingredients[0].QuantityInInventoryNq = Text("12");
        addon.Ingredients[0].QuantityInInventoryHq = Text("0");
        addon.Crystals[0].QuantityRequiredForCraft = Text("2");
        addon.Crystals[0].QuantityInInventory = Text("9,999");
        RecipeNote.RecipeEntry recipe = default;
        recipe.ItemName = Text("Old recipe")->NodeText;
        recipe.ItemId = 100;
        recipe.Ingredients[0].ItemId = 200;
        recipe.Ingredients[0].Amount = 99;
        recipe.Crystals[0].Amount = 99;
        var result = RecipeMaterialReader.Read(&addon, &recipe, _ => "Old recipe", s => s,
            (_, _) => throw new InvalidOperationException("Stale recipe stock must not be read"),
            new(100, [(200, 99)], [(2, 99)]));
        Assert.Equal("New material, нужно 4, есть 12, из них обычных 12, высокого качества 0", result[0]);
        Assert.Equal("Кристалл, нужно 2, есть 9999", result[1]);
    }

    [Fact]
    public void MissingStockAndRequiredAmountAreUnknownRatherThanZeroOrBlank()
    {
        AddonRecipeNote addon = default;
        addon.SelectedRecipeName = Text("New recipe");
        addon.Ingredients[0].Name = Text("Material");
        var result = RecipeMaterialReader.Read(&addon, null, _ => "", s => s, (_, _) => -1);
        Assert.Equal("Material, нужно неизвестно, есть неизвестно, из них обычных неизвестно, высокого качества неизвестно", Assert.Single(result));
    }

    [Fact]
    public void CompletelyBlankRuntimeSlotMustNotHideAVisibleRequiredMaterial()
    {
        AddonRecipeNote addon = default;
        addon.SelectedRecipeName = Text("Recipe");
        addon.Ingredients[0].Name = Text("First material");
        addon.Ingredients[0].QuantityRequiredForCraft = Text("3");
        addon.Ingredients[1].Name = Text("Second material");
        addon.Ingredients[1].QuantityRequiredForCraft = Text("4");
        RecipeNote.RecipeEntry recipe = default;
        recipe.ItemName = Text("Recipe")->NodeText;
        recipe.ItemId = 100;
        recipe.Ingredients[0].ItemId = 200;
        recipe.Ingredients[0].Amount = 3;
        // The second slot has not loaded: both ID and amount are zero.
        var result = RecipeMaterialReader.Read(&addon, &recipe, _ => "First material", s => s,
            (_, _) => throw new InvalidOperationException("Incomplete recipe must use the whole UI"),
            new(100, [(200, 3), (201, 4)], []));
        Assert.Equal(2, result.Count);
        Assert.StartsWith("Second material, нужно 4", result[1]);
    }

    [Fact]
    public void IncompleteRuntimeListFallsBackAsAWhole()
    {
        AddonRecipeNote addon = default;
        addon.SelectedRecipeName = Text("Recipe");
        addon.Ingredients[0].Name = Text("Visible material");
        addon.Ingredients[0].QuantityRequiredForCraft = Text("4");
        RecipeNote.RecipeEntry recipe = default;
        recipe.ItemName = Text("Recipe")->NodeText;
        recipe.ItemId = 200;
        recipe.Ingredients[0].ItemId = 100;
        recipe.Ingredients[0].Amount = 3;
        recipe.Ingredients[1].Amount = 4; // ID not filled yet
        var result = RecipeMaterialReader.Read(&addon, &recipe, _ => "Runtime material", s => s,
            (_, _) => throw new InvalidOperationException("Partial list must not be used"),
            new(200, [(100, 3), (101, 4)], []));
        Assert.StartsWith("Visible material, нужно 4", Assert.Single(result));
    }

    [Fact]
    public void EnglishModeDoesNotUseRussianNameToAcceptADifferentSelection()
    {
        Loc.Mode = LanguageMode.English;
        AddonRecipeNote addon = default;
        addon.SelectedRecipeName = Text("Русское название");
        RecipeNote.RecipeEntry recipe = default;
        recipe.ItemName = Text("English name")->NodeText;
        recipe.ItemId = 200;
        recipe.Ingredients[0].ItemId = 100;
        recipe.Ingredients[0].Amount = 3;
        Assert.Empty(RecipeMaterialReader.Read(&addon, &recipe, _ => "Русское название", s => s,
            (_, _) => throw new InvalidOperationException("Wrong language match"),
            new(200, [(100, 3)], [])));
    }

    [Fact]
    public void UnreadableInventoryDoesNotBecomeZeroAndCrystalAmountSurvivesBlankUi()
    {
        AddonRecipeNote addon = default;
        addon.SelectedRecipeName = Text("Recipe");
        addon.Crystals[0].QuantityInInventory = Text("300");
        RecipeNote.RecipeEntry recipe = default;
        recipe.ItemName = Text("Recipe")->NodeText;
        recipe.ItemId = 100;
        recipe.Ingredients[0].ItemId = 200;
        recipe.Ingredients[0].Amount = 3;
        recipe.Crystals[0].Amount = 2;
        var result = RecipeMaterialReader.Read(&addon, &recipe, id => id == 2 ? "Огненный осколок" : "Material", s => s,
            (id, _) => id == 2 ? 5000 : -1, new(100, [(200, 3)], [(2, 2)]));
        Assert.Contains("нужно 3, есть неизвестно", result[0]);
        Assert.Equal("Огненный осколок, нужно 2, есть 5000", result[1]);
    }

    [Theory]
    [InlineData(100, 3, 2, true)]
    [InlineData(101, 3, 2, false)] // Same visible name, different result ID.
    [InlineData(100, 4, 2, false)] // Recipe for same item with different amount.
    [InlineData(100, 3, 0, false)] // Crystals are still loading.
    [InlineData(100, 3, 9, false)] // Crystal requirement from previous recipe.
    public void RuntimeRequiresEveryMaterialAndCrystalFromItsOwnRecipe(uint result, byte amount, byte crystals, bool expected)
    {
        RecipeNote.RecipeEntry recipe = default;
        recipe.ItemId = result;
        recipe.Ingredients[0].ItemId = 200;
        recipe.Ingredients[0].Amount = amount;
        recipe.Crystals[0].Amount = crystals;
        Assert.Equal(expected, RecipeMaterialReader.IsComplete(&recipe, new(100, [(200, 3)], [(2, 2)])));
        Assert.False(RecipeMaterialReader.IsComplete(&recipe, null));
    }

    [Fact]
    public void MissingRecipeDefinitionKeepsVisibleValuesWithoutTrustingRuntime()
    {
        AddonRecipeNote addon = default;
        addon.SelectedRecipeName = Text("Recipe");
        addon.Ingredients[0].Name = Text("Visible material");
        addon.Ingredients[0].QuantityRequiredForCraft = Text("4");
        RecipeNote.RecipeEntry recipe = default;
        recipe.ItemId = 100;
        recipe.ItemName = Text("Recipe")->NodeText;
        recipe.Ingredients[0].ItemId = 200;
        recipe.Ingredients[0].Amount = 3;
        Assert.StartsWith("Visible material, нужно 4", Assert.Single(RecipeMaterialReader.Read(&addon, &recipe,
            _ => "Runtime material", s => s, (_, _) => throw new InvalidOperationException("Unverified runtime"))));
    }

    [Theory]
    [InlineData(LanguageMode.English, "need 3, have 12")]
    [InlineData(LanguageMode.German, "3 benötigt, vorhanden 12")]
    [InlineData(LanguageMode.Russian, "нужно 3, есть 12")]
    public void StockTotalPreservesTheSelectedLanguage(LanguageMode mode, string phrase)
    {
        Loc.Mode = mode;
        Assert.Contains(phrase, AccessibilityStrings.RecipeMaterial("Item", "3", "10", "2"));
    }

    [Theory]
    [InlineData("1,200", "7", "1207")]
    [InlineData("1\u202F200", "0", "1200")]
    [InlineData("0", "0", "0")]
    [InlineData("-1", "0", "неизвестно")]
    public void InventoryFormattingKeepsLargeCountsAndDistinguishesUnknown(string nq, string hq, string total)
        => Assert.Contains($"есть {total},", AccessibilityStrings.RecipeMaterial("Item", "3", nq, hq));

    [Fact]
    public void CpOnlyChangeIsDetectedAndZeroCpIsSpoken()
    {
        var state = State();
        var before = state.Signature;
        state.Cp = 0;
        Assert.NotEqual(before, state.Signature);
        Assert.StartsWith("Очки работы 0 из 180.", state.Short);
        Assert.Contains("прогресс 6 из 36", state.Short);
        Assert.Contains("прочность 50 из 60", state.Short);
        Assert.Contains("Шаг 2", state.Short);
    }

    [Fact]
    public void ManualAndOpeningReadIncludeConditionStepEffectsAndCp()
    {
        var state = State();
        var full = state.Full;
        Assert.Contains("Очки работы 162 из 180", full);
        Assert.Contains("Состояние: Хорошее", full);
        Assert.Contains("шаг 2", full);
        Assert.Contains("Действует: Innovation 4", full);
        Assert.Equal(full, state.Announcement(false, "", ""));
        Assert.Equal(1, full.Split("Состояние:").Length - 1);
    }

    [Fact]
    public void EffectsAreSpokenWhenTheyChangeAndWhenTheyExpire()
    {
        var state = State();
        Assert.Contains("Действует: Innovation 4", state.Announcement(true, state.Condition, ""));
        Assert.DoesNotContain("Действует:", state.Announcement(true, state.Condition, "Innovation 4"));
        state.Effects = [];
        Assert.Contains("Нет активных ремесленных эффектов", state.Announcement(true, state.Condition, "Innovation 4"));
        Assert.Contains("Состояние: Хорошее", state.Announcement(true, "Обычное", ""));
    }

    [Fact]
    public void MissingPlayerDoesNotClaimZeroCp()
    {
        var state = State();
        state.Cp = state.MaxCp = null;
        Assert.StartsWith("Очки работы недоступны.", state.Full);
    }

    [Fact]
    public void NativeWindowAndLiveCharacterSupplyTheActualReadState()
    {
        AddonSynthesis addon = default;
        addon.CurrentProgress = Text("6");
        addon.MaxProgress = Text("36");
        addon.Condition = Text("Хорошее");
        addon.CraftEffect1.Name = Text("Innovation");
        addon.CraftEffect1.StepsRemaining = Text("4");
        var cp = 162u;
        var player = ChatPlayerTests.Proxy.Of<IPlayerCharacter>(m => m.Name switch
        {
            "get_CurrentCp" => cp,
            "get_MaxCp" => 180u,
            _ => throw new InvalidOperationException(m.Name)
        });
        var objects = ChatPlayerTests.Proxy.Of<IObjectTable>(m => m.Name == "get_LocalPlayer"
            ? player : throw new InvalidOperationException(m.Name));
        var service = new SynthesisService(null!, null!, null!, objects);
        var state = service.Read(&addon);
        Assert.True(state.Read);
        Assert.Equal(162u, state.Cp);
        Assert.Equal(180u, state.MaxCp);
        Assert.Equal("6", state.Progress);
        Assert.Contains("Innovation, осталось шагов 4", state.Full);
        cp = 0;
        Assert.Equal(0u, service.Read(&addon).Cp);
        var empty = new AddonSynthesis();
        Assert.False(service.Read(&empty).Read); // CP alone does not invent an open craft.
    }

    private static SynthesisService.CraftState State() => new()
    {
        Read = true, Item = "Hempen Tabard", Condition = "Хорошее", Quality = "35", MaxQuality = "170",
        HqPercent = "5", Progress = "6", MaxProgress = "36", Durability = "50", MaxDurability = "60",
        Step = "2", Effects = ["Innovation 4"], Cp = 162, MaxCp = 180
    };

    private AtkTextNode* Text(string text)
    {
        var node = (AtkTextNode*)Allocate(sizeof(AtkTextNode));
        *node = default;
        var bytes = Encoding.UTF8.GetBytes(text + "\0");
        var data = Allocate(bytes.Length);
        Marshal.Copy(bytes, 0, data, bytes.Length);
        node->NodeText.StringPtr = (byte*)data;
        node->NodeText.BufSize = bytes.Length;
        node->NodeText.BufUsed = bytes.Length;
        return node;
    }

    private nint Allocate(int size)
    {
        var ptr = Marshal.AllocHGlobal(size);
        _allocations.Add(ptr);
        return ptr;
    }
}
