using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace FF14Accessibility.Services;

/// <summary>
/// Reads the game's live keybind table and dumps it to a file for analysis.
/// Source of truth: UIInputData.Instance()->InputData.Keybinds (verified via
/// ilspycmd against FFXIVClientStructs.dll, see docs/game-api.md "Keybind-System").
/// The dump includes a conflict check: which game actions share a key with the
/// plugin's own hotkeys (F1-F12 etc.).
/// </summary>
public sealed class KeybindService
{
    private readonly TolkService _tolk;
    private readonly IPluginLog _log;

    public KeybindService(TolkService tolk, IPluginLog log)
    {
        _tolk = tolk;
        _log = log;
    }

    /// <summary>
    /// True once the game's keybind table is loaded and readable.
    /// Used to defer the automatic dump until the data exists.
    /// </summary>
    public unsafe bool IsReady()
    {
        var uiInput = UIInputData.Instance();
        return uiInput != null
            && uiInput->InputData.Keybinds != null
            && uiInput->InputData.NumKeybinds > 0;
    }

    /// <summary>
    /// Dumps all game keybinds to Desktop\FFXIV_Keybinds.txt and reports
    /// conflicts between plugin hotkeys and game bindings (log + file).
    /// A conflict requires the SAME key and the SAME modifiers; game bindings
    /// that only share the physical key are listed as info.
    /// </summary>
    /// <param name="pluginKeys">Plugin hotkeys as (function, key label, VK code, modifiers).</param>
    /// <param name="announce">False for the automatic post-login dump: it runs
    /// silently (log/file only) and only speaks up when a real key conflict
    /// exists - the success announcement was noise at every login (user
    /// 2026-07-13). Manual /acc keys keeps full spoken feedback.</param>
    public unsafe void DumpKeybinds(
        IReadOnlyList<(string Function, string KeyName, int VirtualKey, bool Ctrl, bool Shift, bool Alt)> pluginKeys,
        bool announce = true)
    {
        var uiInput = UIInputData.Instance();
        if (uiInput == null)
        {
            _log.Warning("[Keys] UIInputData.Instance() ist null.");
            if (announce) _tolk.SpeakInterrupt(AccessibilityStrings.KeybindDumpFailed);
            return;
        }

        var numKeybinds = uiInput->InputData.NumKeybinds;
        if (uiInput->InputData.Keybinds == null || numKeybinds <= 0 || numKeybinds > 4096)
        {
            _log.Warning($"[Keys] Keybind-Tabelle unplausibel: Keybinds={(nint)uiInput->InputData.Keybinds:X}, Num={numKeybinds}");
            if (announce) _tolk.SpeakInterrupt(AccessibilityStrings.KeybindDumpFailed);
            return;
        }

        var keybinds = uiInput->InputData.GetKeybindSpan();

        // Reverse map: virtual key -> all game actions bound to it, with modifiers.
        var actionsByVk = new Dictionary<byte, List<(string Desc, KeyModifierFlag Mod)>>();

        var sb = new StringBuilder();
        sb.AppendLine($"FFXIV Tastenbelegung (live aus dem Spiel) — {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Einträge: {numKeybinds}");
        sb.AppendLine();
        sb.AppendLine("== Aktionen mit Tastatur-Belegung ==");

        var boundCount = 0;
        for (var i = 0; i < numKeybinds; i++)
        {
            var actionName = Enum.GetName(typeof(InputId), i) ?? $"UNBENANNT_{i}";
            var slot1 = FormatKeySetting(keybinds[i].KeySettings[0]);
            var slot2 = FormatKeySetting(keybinds[i].KeySettings[1]);

            if (slot1.Length == 0 && slot2.Length == 0)
                continue;

            boundCount++;
            sb.Append($"{actionName} ({i}): {(slot1.Length > 0 ? slot1 : "-")}");
            if (slot2.Length > 0) sb.Append($" ; {slot2}");
            sb.AppendLine();

            foreach (var setting in keybinds[i].KeySettings)
            {
                var vk = (byte)setting.Key;
                if (vk == 0) continue;
                if (!actionsByVk.TryGetValue(vk, out var list))
                    actionsByVk[vk] = list = new List<(string, KeyModifierFlag)>();
                list.Add(($"{actionName} ({FormatKeySetting(setting)})", setting.KeyModifier));
            }
        }

        sb.AppendLine();
        sb.AppendLine("== Konflikt-Check Plugin-Tasten ==");
        var conflictCount = 0;
        foreach (var (function, keyName, vk, ctrl, shift, alt) in pluginKeys)
        {
            var pluginMod = (ctrl  ? KeyModifierFlag.Ctrl  : 0)
                          | (shift ? KeyModifierFlag.Shift : 0)
                          | (alt   ? KeyModifierFlag.Alt   : 0);

            var sameKey = actionsByVk.TryGetValue((byte)vk, out var actions)
                ? actions
                : new List<(string Desc, KeyModifierFlag Mod)>();
            var exact = sameKey.Where(a => a.Mod == pluginMod).Select(a => a.Desc).ToList();
            var shared = sameKey.Where(a => a.Mod != pluginMod).Select(a => a.Desc).ToList();

            if (exact.Count > 0)
            {
                conflictCount++;
                var line = $"KONFLIKT {keyName} ({function}): {string.Join(", ", exact)}";
                sb.AppendLine(line);
                _log.Warning($"[Keys] {line}");
            }
            else
            {
                sb.AppendLine($"frei     {keyName} ({function})" +
                    (shared.Count > 0 ? $" — gleiche Taste, andere Modifier: {string.Join(", ", shared)}" : ""));
            }
        }

        var dumpFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "FFXIV_Keybinds.txt");
        try
        {
            File.WriteAllText(dumpFile, sb.ToString(), Encoding.UTF8);
        }
        catch (Exception ex) // external call: file system
        {
            _log.Error(ex, "[Keys] Konnte Dump-Datei nicht schreiben.");
            if (announce) _tolk.SpeakInterrupt(AccessibilityStrings.KeybindDumpFailed);
            return;
        }

        _log.Info($"[Keys] {boundCount} Aktionen mit Taste, {conflictCount} Plugin-Konflikte. Gespeichert: {dumpFile}");
        // Conflicts are always spoken - the user must know a plugin key is dead.
        if (announce || conflictCount > 0)
            _tolk.SpeakInterrupt(AccessibilityStrings.KeybindDumpSaved(boundCount, conflictCount));
    }

    /// <summary>
    /// The key currently bound to a game action ("Strg+3"), looked up by
    /// InputId name (e.g. "HOTBAR_2_3") in the LIVE keybind table - same
    /// source as DumpKeybinds (Index == InputId, game-api.md "Keybind-
    /// System"). Null when the action is unbound or the table is not ready.
    /// </summary>
    public unsafe string? GetBoundKey(string inputIdName)
    {
        if (!Enum.TryParse(inputIdName, out InputId id)) return null;
        var uiInput = UIInputData.Instance();
        if (uiInput == null || uiInput->InputData.Keybinds == null) return null;
        var index = (int)id;
        if (index < 0 || index >= uiInput->InputData.NumKeybinds) return null;

        foreach (var setting in uiInput->InputData.GetKeybindSpan()[index].KeySettings)
        {
            var formatted = FormatKeySetting(setting);
            if (formatted.Length > 0) return PrettifyKeyName(formatted);
        }
        return null;
    }

    /// <summary>"Strg+KEY_3" -> "Strg+3": the SeVirtualKey enum prefixes
    /// number/letter keys with "KEY_", which is noise for TTS.</summary>
    private static string PrettifyKeyName(string key) => key.Replace("KEY_", "");

    /// <summary>Formats one key setting as e.g. "Strg+Umschalt+F1"; empty string if unbound.</summary>
    private static string FormatKeySetting(KeySetting setting)
    {
        if ((byte)setting.Key == 0) return string.Empty;

        var sb = new StringBuilder();
        if (setting.KeyModifier.HasFlag(KeyModifierFlag.Ctrl))  sb.Append("Strg+");
        if (setting.KeyModifier.HasFlag(KeyModifierFlag.Shift)) sb.Append("Umschalt+");
        if (setting.KeyModifier.HasFlag(KeyModifierFlag.Alt))   sb.Append("Alt+");
        sb.Append(Enum.GetName(typeof(SeVirtualKey), setting.Key) ?? $"VK{(byte)setting.Key}");
        return sb.ToString();
    }
}
