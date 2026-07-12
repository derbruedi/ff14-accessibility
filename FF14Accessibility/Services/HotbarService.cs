using System;
using System.Collections.Generic;
using System.Text;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace FF14Accessibility.Services;

/// <summary>
/// Reads what is bound to the player's action bar so a blind player knows
/// which number key does what. FFXIV has no single "attack" button: you
/// target an enemy and press hotbar keys (1-9, 0 = Hotbar 1 slots) to use
/// actions. Structs ilspycmd-verified, see docs/game-api.md -> "Hotbar".
/// </summary>
public sealed class HotbarService
{
    private readonly IDataManager _data;
    private readonly TolkService _tolk;
    private readonly IPluginLog _log;

    public HotbarService(IDataManager data, TolkService tolk, IPluginLog log)
    {
        _data = data;
        _tolk = tolk;
        _log = log;
    }

    /// <summary>UI "Hotbar 1" is module index 0; its 12 keys are 1-9, 0, 11, 12.</summary>
    private const int MainHotbarIndex = 0;
    private const int SlotsToRead = 12;

    // Slot index -> the key the player presses (HOTBAR_1_1..HOTBAR_1_0 = 1..0,
    // HOTBAR_1_A/B = keys 11/12 per the live keybind dump).
    private static readonly string[] SlotKeyNames =
        { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "11", "12" };

    /// <summary>
    /// Announces the actions on Hotbar 1: "Taste 1, Vollschlag. Taste 2, ..."
    /// Empty slots are skipped; if the whole bar is empty, says so.
    /// </summary>
    public unsafe void ReadHotbar()
    {
        var module = RaptureHotbarModule.Instance();
        if (module == null)
        {
            _tolk.SpeakInterrupt("Aktionsleiste nicht verfügbar.");
            _log.Warning("[Hotbar] RaptureHotbarModule.Instance() ist null.");
            return;
        }

        var parts = new List<string>();
        for (var slot = 0; slot < SlotsToRead; slot++)
        {
            var s = module->GetSlotById(MainHotbarIndex, (uint)slot);
            if (s == null || s->CommandType == RaptureHotbarModule.HotbarSlotType.Empty)
                continue;

            var name = ResolveName(s->CommandType, s->CommandId, s->PopUpHelp.ToString());
            _log.Info($"[Hotbar] Slot {slot} (Taste {SlotKeyNames[slot]}): type={s->CommandType} " +
                      $"id={s->CommandId} name='{name}'");
            parts.Add($"Taste {SlotKeyNames[slot]}, {name}");
        }

        if (parts.Count == 0)
        {
            _tolk.SpeakInterrupt("Aktionsleiste 1 ist leer.");
            return;
        }

        _tolk.SpeakInterrupt("Aktionsleiste 1. " + string.Join(". ", parts) + ".");
    }

    /// <summary>
    /// Human-readable name for a slot. Combat actions resolve through the
    /// Lumina Action sheet (deterministic); everything else falls back to the
    /// game's own display string (PopUpHelp), then to a type+id label.
    /// </summary>
    private string ResolveName(RaptureHotbarModule.HotbarSlotType type, uint id, string popUpHelp)
    {
        if (type == RaptureHotbarModule.HotbarSlotType.Action &&
            _data.GetExcelSheet<LuminaAction>().TryGetRow(id, out var action))
        {
            var actionName = action.Name.ExtractText();
            if (!string.IsNullOrWhiteSpace(actionName))
                return actionName;
        }

        // PopUpHelp is the game's own display text (name plus keybind hint);
        // use it for items, macros, emotes and anything not in the Action sheet.
        var cleaned = CleanUpHelp(popUpHelp);
        if (!string.IsNullOrWhiteSpace(cleaned))
            return cleaned;

        return $"{type}, {id}";
    }

    /// <summary>Trims the keybind hint the game appends to the display name -
    /// either after a newline/tab, or in trailing brackets ("Spielanleitung [9]",
    /// "Teleport [0]"; log 2026-07-12). The bracketed key is noise for TTS.</summary>
    private static string CleanUpHelp(string popUpHelp)
    {
        if (string.IsNullOrEmpty(popUpHelp)) return string.Empty;
        var sb = new StringBuilder(popUpHelp.Length);
        foreach (var c in popUpHelp)
        {
            if (c == '\n' || c == '\r' || c == '\t') break;
            sb.Append(c);
        }

        var result = sb.ToString().Trim();

        // Drop a trailing "[...]" keybind hint (and the space before it).
        if (result.EndsWith("]"))
        {
            var open = result.LastIndexOf('[');
            if (open > 0) result = result[..open].Trim();
        }
        return result;
    }
}
