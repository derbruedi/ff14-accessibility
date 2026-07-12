using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using LuminaEmote = Lumina.Excel.Sheets.Emote;

namespace FF14Accessibility.Services;

/// <summary>
/// Lets a blind player perform emotes (e.g. the "bow" a quest asks for) without
/// the chat box or the icon-grid gesture menu. Emotes are browsed by name and
/// fired straight through the game's own emote function
/// (AgentEmote.ExecuteEmote, ilspycmd-verified), filtered to the ones the player
/// has unlocked (CanUseEmote). Names and the /text command come from the Lumina
/// Emote sheet, so nothing is guessed or hard-coded.
/// </summary>
public sealed class EmoteService
{
    private readonly IDataManager _data;
    private readonly IClientState _clientState;
    private readonly TolkService  _tolk;
    private readonly IPluginLog   _log;

    private readonly List<(uint Id, string Name, string Command)> _emotes = new();
    private int _index = -1;

    public EmoteService(IDataManager data, IClientState clientState, TolkService tolk, IPluginLog log)
    {
        _data        = data;
        _clientState = clientState;
        _tolk        = tolk;
        _log         = log;
    }

    /// <summary>Announces the next usable emote in the alphabetical list.</summary>
    public void CycleNext() => Cycle(+1);

    /// <summary>Announces the previous usable emote.</summary>
    public void CyclePrev() => Cycle(-1);

    private void Cycle(int direction)
    {
        if (!EnsureList()) return;

        _index = ((_index + direction) % _emotes.Count + _emotes.Count) % _emotes.Count;
        var (_, name, command) = _emotes[_index];
        var msg = $"{_index + 1} von {_emotes.Count}: {name}";
        if (!string.IsNullOrEmpty(command)) msg += $", Befehl {command}";
        _tolk.SpeakInterrupt(msg);
    }

    /// <summary>Performs the currently selected emote via the game's emote function.</summary>
    public unsafe void ExecuteSelected()
    {
        if (!EnsureList()) return;
        if (_index < 0) { _tolk.SpeakInterrupt("Kein Emote gewählt. Erst durchblättern."); return; }

        var (id, name, _) = _emotes[_index];
        var agent = AgentEmote.Instance();
        if (agent == null) { _tolk.SpeakInterrupt("Emote nicht verfügbar."); return; }

        // External game call: guard it (the function pointer can be null if the
        // signature drifts on a patch, and ExecuteEmote touches game state).
        try
        {
            agent->ExecuteEmote((ushort)id);
            _tolk.SpeakInterrupt($"{name}.");
            _log.Info($"[Emote] Ausgeführt: id={id} '{name}'");
        }
        catch (Exception ex)
        {
            _tolk.SpeakInterrupt("Emote fehlgeschlagen.");
            _log.Error(ex, $"[Emote] ExecuteEmote fehlgeschlagen: id={id} '{name}'");
        }
    }

    /// <summary>
    /// Builds the sorted list of usable emotes once. Filtered to unlocked emotes
    /// (CanUseEmote) with a non-empty name, sorted by name so a specific emote is
    /// easy to find. Returns false (and announces) when nothing is available yet
    /// (not logged in, or the emote agent is not ready).
    /// </summary>
    private unsafe bool EnsureList()
    {
        if (_emotes.Count > 0) return true;

        if (!_clientState.IsLoggedIn)
        {
            _tolk.SpeakInterrupt("Nicht eingeloggt.");
            return false;
        }

        var agent = AgentEmote.Instance();
        if (agent == null)
        {
            _tolk.SpeakInterrupt("Emotes noch nicht bereit.");
            return false;
        }

        foreach (var row in _data.GetExcelSheet<LuminaEmote>())
        {
            if (row.RowId == 0) continue;
            var name = row.Name.ExtractText();
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (!agent->CanUseEmote((ushort)row.RowId)) continue;

            var command = row.TextCommand.ValueNullable?.Command.ExtractText() ?? string.Empty;
            _emotes.Add((row.RowId, name, command));
        }

        _emotes.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));
        _log.Info($"[Emote] Liste gebaut: {_emotes.Count} nutzbare Emotes.");

        if (_emotes.Count == 0)
        {
            _tolk.SpeakInterrupt("Keine Emotes verfügbar.");
            return false;
        }
        return true;
    }
}
