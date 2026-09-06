using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

/// <summary>One craftable recipe the active Handwerker can start right now.</summary>
/// <param name="RecipeId">Lumina <see cref="Recipe"/> RowId / game RecipeId.</param>
/// <param name="Name">Result item name (spoken).</param>
/// <param name="Level">Required crafting level from <see cref="RecipeLevelTable"/>.</param>
/// <param name="CraftType">CraftType row id (0–7 = CRP…CUL).</param>
public sealed record CraftRecipeInfo(ushort RecipeId, string Name, int Level, byte CraftType);

/// <summary>
/// Handwerk: welche freigeschalteten Rezepte der aktive Crafter herstellen
/// kann, weil die Materialien (inkl. Kristalle) im Inventar reichen.
///
/// Quelle (alles Spiel-Wahrheit, kein nachgebautes Freischalt-Bit):
/// - Katalog: Lumina <see cref="Recipe"/> + <see cref="RecipeLevelTable"/>
/// - Verfuegbar: CraftType + Stufe ≤ <c>GetCraftTypeLevel</c>; Secret/Meister
///   zusaetzlich <c>IsRecipeUnlocked</c> (reine IsRecipeUnlocked-Filterung war
///   falsch: Weber-Notizbuch 21 Zeilen, Kategorie 0 — Log 2026-09-06)
/// - Materialien: <see cref="InventoryService.CountOfNqAndHq"/>
/// - Start: <c>AgentRecipeNote.OpenRecipeByRecipeId</c>, dann Klick auf
///   <c>AddonRecipeNote.SynthesizeButton</c> (ButtonClick wie PressOk)
///
/// CraftType 0–7 entspricht ClassJob 8–15 (AgentRecipeNote-Kommentare /
/// Sheet-Reihenfolge CRP…CUL). Kategorie nur sichtbar auf diesen Jobs.
/// </summary>
public sealed class CraftingService
{
    /// <summary>ClassJob row ids for the eight Disciple of the Hand classes.</summary>
    public const uint JobCarpenter      = 8;
    public const uint JobBlacksmith     = 9;
    public const uint JobArmorer        = 10;
    public const uint JobGoldsmith      = 11;
    public const uint JobLeatherworker  = 12;
    public const uint JobWeaver         = 13;
    public const uint JobAlchemist      = 14;
    public const uint JobCulinarian     = 15;

    private readonly IObjectTable      _objectTable;
    private readonly IDataManager      _data;
    private readonly IGameGui          _gameGui;
    private readonly IFramework        _framework;
    private readonly InventoryService  _inventory;
    private readonly TolkService       _tolk;
    private readonly IPluginLog        _log;

    // Pending synthesize after OpenRecipeByRecipeId - the notebook needs a few
    // frames to select the row before the Synthesize button is live.
    private ushort _pendingRecipeId;
    private int    _pendingFramesLeft;
    private const int PendingMaxFrames = 45;

    public CraftingService(
        IObjectTable objectTable,
        IDataManager data,
        IGameGui gameGui,
        IFramework framework,
        InventoryService inventory,
        TolkService tolk,
        IPluginLog log)
    {
        _objectTable = objectTable;
        _data        = data;
        _gameGui     = gameGui;
        _framework   = framework;
        _inventory   = inventory;
        _tolk        = tolk;
        _log         = log;
        _framework.Update += OnFrameworkUpdate;
    }

    /// <summary>Stop listening when the plugin unloads.</summary>
    public void Dispose() => _framework.Update -= OnFrameworkUpdate;

    /// <summary>True while the player is on a Disciple of the Hand class.</summary>
    public bool IsCrafterClassActive()
    {
        var player = _objectTable.LocalPlayer;
        if (player == null) return false;
        return ClassJobToCraftType(player.ClassJob.RowId) is not null;
    }

    /// <summary>
    /// CraftType for the active class, or null when not a Handwerker.
    /// Mapping: ClassJob 8–15 → CraftType 0–7 (CRP…CUL).
    /// </summary>
    public static byte? ClassJobToCraftType(uint classJobId) =>
        classJobId is >= JobCarpenter and <= JobCulinarian
            ? (byte)(classJobId - JobCarpenter)
            : null;

    /// <summary>
    /// Recipes for the active crafter that are available in the notebook sense
    /// and fully covered by inventory materials. Sorted by level, then name.
    ///
    /// Availability (verified against live notebook 2026-09-06: Weber level 1
    /// showed 21 rows including Hanfgarn while IsRecipeUnlocked-only yielded 0):
    /// - CraftType matches active Handwerker
    /// - ClassJobLevel ≤ <c>RecipeNote.GetCraftTypeLevel</c>
    /// - Secret / Meister recipes also need <c>IsRecipeUnlocked</c>
    /// - Materials: NQ+HQ via <see cref="InventoryService.CountOfNqAndHq"/>
    /// </summary>
    public unsafe List<CraftRecipeInfo> GetCraftableRecipes()
    {
        var player = _objectTable.LocalPlayer;
        if (player == null) return [];

        var craftType = ClassJobToCraftType(player.ClassJob.RowId);
        if (craftType is null) return [];

        var note = RecipeNote.Instance();
        if (note == null)
        {
            _log.Info("[Craft] RecipeNote.Instance ist null - Liste leer.");
            return [];
        }

        var sheet = _data.GetExcelSheet<Recipe>();
        if (sheet == null) return [];

        var maxLevel = note->GetCraftTypeLevel(craftType.Value);
        var result = new List<CraftRecipeInfo>();
        var typeMatch = 0;
        var levelBlocked = 0;
        var specialBlocked = 0;
        var matsBlocked = 0;

        foreach (var row in sheet)
        {
            if (row.ItemResult.RowId == 0) continue;
            if (row.CraftType.RowId != craftType.Value) continue;
            typeMatch++;

            var level = 0;
            if (row.RecipeLevelTable.RowId != 0
                && row.RecipeLevelTable.ValueNullable is { } table)
                level = table.ClassJobLevel;

            if (level > maxLevel)
            {
                levelBlocked++;
                continue;
            }

            var recipeId = (ushort)row.RowId;
            // Ordinary level-gated recipes appear in the notebook without a
            // separate unlock bit. IsRecipeUnlocked alone filtered ALL of them
            // out (log 2026-09-06 12:19: Weber notebook 21 rows, category 0).
            var needsUnlockBit = row.SecretRecipeBook.RowId != 0
                                 || row.IsSpecializationRequired;
            if (needsUnlockBit && !note->IsRecipeUnlocked(recipeId))
            {
                specialBlocked++;
                continue;
            }

            if (!HasAllMaterials(row))
            {
                matsBlocked++;
                continue;
            }

            var name = row.ItemResult.ValueNullable?.Name.ExtractText() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name)) continue;

            result.Add(new CraftRecipeInfo(recipeId, name, level, craftType.Value));
        }

        _log.Info($"[Craft] craftType={craftType} maxLevel={maxLevel} typ={typeMatch} "
                + $"stufeWeg={levelBlocked} spezialWeg={specialBlocked} matsWeg={matsBlocked} "
                + $"herstellbar={result.Count}");

        return result
            .OrderBy(r => r.Level)
            .ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Opens the crafting log on <paramref name="recipe"/> and clicks Synthesize
    /// once the selection sticks. Announces failure when the game refuses.
    /// </summary>
    public unsafe void TryStartCraft(CraftRecipeInfo recipe)
    {
        if (!IsCrafterClassActive())
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.CraftNotCrafter);
            return;
        }

        var agent = AgentRecipeNote.Instance();
        if (agent == null)
        {
            _log.Info("[Craft] AgentRecipeNote.Instance ist null.");
            _tolk.SpeakInterrupt(AccessibilityStrings.CraftStartFailed);
            return;
        }

        // Cancel any previous pending click so a rapid second press does not
        // synthesize the wrong recipe.
        _pendingRecipeId = 0;
        _pendingFramesLeft = 0;

        _log.Info($"[Craft] OpenRecipeByRecipeId id={recipe.RecipeId} '{recipe.Name}'");
        agent->OpenRecipeByRecipeId(recipe.RecipeId);
        _pendingRecipeId = recipe.RecipeId;
        _pendingFramesLeft = PendingMaxFrames;
        _tolk.SpeakInterrupt(AccessibilityStrings.CraftStarting(recipe.Name));
    }

    private bool HasAllMaterials(Recipe row)
    {
        // Sheet: 8 Ingredient / AmountIngredient slots (materials + crystals as
        // Item rows). Game RecipeEntry splits 6+2; the sheet keeps them together.
        for (var i = 0; i < 8; i++)
        {
            var amount = row.AmountIngredient[i];
            if (amount == 0) continue;
            var itemId = row.Ingredient[i].RowId;
            if (itemId == 0) continue;

            var have = _inventory.CountOfNqAndHq(itemId);
            if (have < 0)
            {
                _log.Info($"[Craft] Inventar nicht lesbar fuer Item {itemId}.");
                return false;
            }
            if (have < amount) return false;
        }
        return true;
    }

    private unsafe void OnFrameworkUpdate(IFramework framework)
    {
        if (_pendingRecipeId == 0 || _pendingFramesLeft <= 0) return;
        _pendingFramesLeft--;

        var note = RecipeNote.Instance();
        var selected = note != null && note->RecipeList != null
            ? note->RecipeList->SelectedRecipe
            : null;

        if (selected == null || selected->RecipeId != _pendingRecipeId)
        {
            if (_pendingFramesLeft == 0)
            {
                _log.Info($"[Craft] Auswahl id={_pendingRecipeId} kam nicht rechtzeitig.");
                _pendingRecipeId = 0;
                _tolk.SpeakInterrupt(AccessibilityStrings.CraftStartFailed);
            }
            return;
        }

        var clicked = TryClickSynthesize();
        _log.Info($"[Craft] Synthesize-Klick fuer id={_pendingRecipeId}: {clicked}");
        _pendingRecipeId = 0;
        _pendingFramesLeft = 0;
        if (!clicked)
            _tolk.SpeakInterrupt(AccessibilityStrings.CraftStartFailed);
    }

    /// <summary>
    /// Dispatches ButtonClick on <c>AddonRecipeNote.SynthesizeButton</c> - same
    /// event path as <c>UIReaderService.TryClickButton</c> for Ok.
    /// </summary>
    private unsafe bool TryClickSynthesize()
    {
        var ptr = _gameGui.GetAddonByName("RecipeNote");
        if (ptr.IsNull) return false;
        var addon = (AddonRecipeNote*)(nint)ptr;
        if (!addon->AtkUnitBase.IsVisible) return false;

        var button = addon->SynthesizeButton;
        if (button == null) return false;

        var owner = button->OwnerNode;
        if (owner == null) return false;

        var evt = FindEventOfType((AtkResNode*)owner, AtkEventType.ButtonClick);
        if (evt == null)
        {
            // Collision child often holds the registration (same as PressOk).
            for (var i = 0; i < button->UldManager.NodeListCount && evt == null; i++)
            {
                var child = button->UldManager.NodeList[i];
                if (child == null) continue;
                evt = FindEventOfType(child, AtkEventType.ButtonClick);
            }
        }
        if (evt == null || evt->Listener == null) return false;

        var data = default(AtkEventData);
        evt->Listener->ReceiveEvent(evt->State.EventType, (int)evt->Param, evt, &data);
        return true;
    }

    private static unsafe AtkEvent* FindEventOfType(AtkResNode* node, AtkEventType type)
    {
        var evt = node->AtkEventManager.Event;
        var guard = 0;
        while (evt != null && guard++ < 32)
        {
            if (evt->State.EventType == type) return evt;
            evt = evt->NextEvent;
        }
        return null;
    }
}
