using System;
using System.Collections.Generic;
using System.Text;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace FF14Accessibility.Services;

/// <summary>
/// Reads what is bound to the player's action bar so a blind player knows
/// which number key does what. FFXIV has no single "attack" button: you
/// target an enemy and press hotbar keys (1-9, 0 = Hotbar 1 slots) to use
/// actions. Also lets the player REBIND those keys: a skill browser cycles
/// through the learned actions of the current job and places the chosen one
/// on a chosen slot via the game's own RaptureHotbarModule.SetAndSaveSlot -
/// the exact function the drag-and-drop UI uses, so the change persists like
/// a manual one. Structs ilspycmd-verified, see docs/game-api.md -> "Hotbar".
/// </summary>
public sealed class HotbarService
{
    private readonly IDataManager _data;
    private readonly IClientState _clientState;
    private readonly IFramework _framework;
    private readonly GearInfoService _gearInfo;
    private readonly KeybindService _keybinds;
    private readonly TolkService _tolk;
    private readonly IPluginLog _log;

    public HotbarService(IDataManager data, IClientState clientState, IFramework framework,
                         GearInfoService gearInfo, KeybindService keybinds, TolkService tolk, IPluginLog log)
    {
        _data = data;
        _clientState = clientState;
        _framework = framework;
        _gearInfo = gearInfo;
        _keybinds = keybinds;
        _tolk = tolk;
        _log = log;
    }

    /// <summary>UI "Hotbar 1" is module index 0; its 12 keys are 1-9, 0, 11, 12.</summary>
    private const int MainHotbarIndex = 0;
    private const int SlotsToRead = 12;
    // RaptureHotbarModule.StandardHotbars = Hotbars[0..9] (ilspycmd
    // 2026-07-17); indices 10..17 are the gamepad cross bars - not offered.
    private const int StandardBarCount = 10;

    // Slot index -> the key the player presses (HOTBAR_1_1..HOTBAR_1_0 = 1..0,
    // HOTBAR_1_A/B = keys 11/12 per the live keybind dump).
    private static readonly string[] SlotKeyNames =
        { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "11", "12" };

    // Slot index -> InputId name suffix: HOTBAR_{bar}_{suffix}. Live dump
    // 2026-07-17: HOTBAR_2_1..HOTBAR_2_B follow the HOTBAR_1_* block, bar 2
    // is bound to Strg+1..Strg+0 by default.
    private static readonly string[] SlotInputSuffix =
        { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "A", "B" };

    // Target bar of the skill browser (module index; UI name = index + 1).
    private int _targetBar = MainHotbarIndex;

    /// <summary>The key currently bound to a bar/slot ("Strg+3") from the
    /// game's live keybind table, or null when unbound.</summary>
    private string? BoundKeyFor(int bar, int slot)
        => _keybinds.GetBoundKey($"HOTBAR_{bar + 1}_{SlotInputSuffix[slot]}");

    /// <summary>Spoken location of a slot: bar 1 keeps the familiar
    /// "Taste 7"; other bars name the bar plus the live-bound key
    /// ("Leiste 2, Taste Strg+3"), or the slot number when unbound.</summary>
    private string SlotLabel(int bar, int slot)
    {
        if (bar == MainHotbarIndex)
            return $"Taste {BoundKeyFor(bar, slot) ?? SlotKeyNames[slot]}";
        var key = BoundKeyFor(bar, slot);
        return key != null
            ? $"Leiste {bar + 1}, Taste {key}"
            : $"Leiste {bar + 1}, Slot {slot + 1}";
    }

    /// <summary>
    /// Announces the actions on the browser's target bar (default bar 1):
    /// "Aktionsleiste 1. Taste 1, Vollschlag. ..." Other bars use their
    /// live-bound keys or slot numbers. Empty slots are skipped; if the
    /// whole bar is empty, says so.
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

        var bar = _targetBar;
        var parts = new List<string>();
        for (var slot = 0; slot < SlotsToRead; slot++)
        {
            var s = module->GetSlotById((uint)bar, (uint)slot);
            if (s == null || s->CommandType == RaptureHotbarModule.HotbarSlotType.Empty)
                continue;

            var name = ResolveName(s->CommandType, s->CommandId, s->PopUpHelp.ToString());
            var keyLabel = bar == MainHotbarIndex
                ? $"Taste {SlotKeyNames[slot]}"
                : (BoundKeyFor(bar, slot) is { } key ? $"Taste {key}" : $"Slot {slot + 1}");
            _log.Info($"[Hotbar] Leiste {bar + 1} Slot {slot} ({keyLabel}): type={s->CommandType} " +
                      $"id={s->CommandId} name='{name}'");
            parts.Add($"{keyLabel}, {name}");
        }

        if (parts.Count == 0)
        {
            _tolk.SpeakInterrupt($"Aktionsleiste {bar + 1} ist leer.");
            return;
        }

        _tolk.SpeakInterrupt($"Aktionsleiste {bar + 1}. " + string.Join(". ", parts) + ".");
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

    // ── Skill browser: rebind hotbar keys without the mouse ──
    //
    // Sighted players drag actions from "Actions & Traits" onto the bar; there
    // is no keyboard path in the game, so the plugin provides one: browse the
    // learned actions of the current job, pick a target key, assign.

    private readonly List<(uint Id, string Name, byte Level)> _skills = new();
    private int _skillIndex = -1;
    private int _targetSlot = -1;
    // The list is rebuilt when job or level changes (level-ups add skills).
    private byte _skillsJobId;
    private uint _skillsLevel;

    /// <summary>Announces the next learned skill of the current job.</summary>
    public void CycleSkillNext() => CycleSkill(+1);

    /// <summary>Announces the previous learned skill.</summary>
    public void CycleSkillPrev() => CycleSkill(-1);

    private void CycleSkill(int direction)
    {
        if (!EnsureSkillList()) return;

        _skillIndex = ((_skillIndex + direction) % _skills.Count + _skills.Count) % _skills.Count;
        var (id, name, level) = _skills[_skillIndex];

        var msg = $"{_skillIndex + 1} von {_skills.Count}: {name}, Stufe {level}";
        var location = FindSlotLocationFor(id);
        if (location != null) msg += $", liegt auf {location}";
        _tolk.SpeakInterrupt(msg);
    }

    /// <summary>
    /// Cycles the target bar (Kommandomenü 1-10). The slot choice resets so
    /// an assignment never lands on a slot the user picked while another bar
    /// was being announced. Says how many slots are filled and warns when
    /// the bar has no keys bound (it could hold skills but not fire them).
    /// </summary>
    public unsafe void CycleTargetBar()
    {
        var module = RaptureHotbarModule.Instance();
        if (module == null)
        {
            _tolk.SpeakInterrupt("Aktionsleiste nicht verfügbar.");
            return;
        }

        _targetBar = (_targetBar + 1) % StandardBarCount;
        _targetSlot = -1;

        var filled = 0;
        var anyKey = false;
        for (var slot = 0; slot < SlotsToRead; slot++)
        {
            var s = module->GetSlotById((uint)_targetBar, (uint)slot);
            if (s != null && s->CommandType != RaptureHotbarModule.HotbarSlotType.Empty) filled++;
            if (BoundKeyFor(_targetBar, slot) != null) anyKey = true;
        }

        var msg = $"Ziel-Leiste {_targetBar + 1}, {filled} von {SlotsToRead} belegt";
        if (!anyKey) msg += ", keine Tasten zugewiesen";
        _tolk.SpeakInterrupt(msg + ".");
    }

    /// <summary>
    /// Cycles the target slot on the chosen bar and announces what is
    /// currently on it, so the player knows what an assignment would replace.
    /// </summary>
    public unsafe void CycleTargetSlot()
    {
        var module = RaptureHotbarModule.Instance();
        if (module == null)
        {
            _tolk.SpeakInterrupt("Aktionsleiste nicht verfügbar.");
            return;
        }

        _targetSlot = (_targetSlot + 1) % SlotsToRead;
        var s = module->GetSlotById((uint)_targetBar, (uint)_targetSlot);
        var current = s == null || s->CommandType == RaptureHotbarModule.HotbarSlotType.Empty
            ? "leer"
            : ResolveName(s->CommandType, s->CommandId, s->PopUpHelp.ToString());
        _tolk.SpeakInterrupt($"Ziel-{SlotLabel(_targetBar, _targetSlot)}: {current}");
    }

    /// <summary>
    /// Puts the browsed skill on the chosen key: SetAndSaveSlot persists the
    /// change per job (same path as drag-and-drop), LoadSavedHotbar then pulls
    /// the saved state into the LIVE bar - the V4.76 probe proved that
    /// SetAndSaveSlot alone only updates the saved side (the 09:43 assignment
    /// appeared on the bar after relog; log 2026-07-17 11:59). Success is only
    /// announced after a 2-frame read-back confirms the slot really changed.
    /// </summary>
    public unsafe void AssignSelectedSkill()
    {
        if (_skillIndex < 0 || _skillIndex >= _skills.Count)
        {
            _tolk.SpeakInterrupt("Kein Skill gewählt. Erst mit dem Skill-Browser blättern.");
            return;
        }
        if (_targetSlot < 0)
        {
            _tolk.SpeakInterrupt("Keine Ziel-Taste gewählt. Erst die Ziel-Taste wählen.");
            return;
        }

        var module = RaptureHotbarModule.Instance();
        if (module == null)
        {
            _tolk.SpeakInterrupt("Aktionsleiste nicht verfügbar.");
            return;
        }

        var (id, name, _) = _skills[_skillIndex];
        var bar = _targetBar;
        var slot = _targetSlot;

        _log.Info($"[Hotbar] Belegen: {SlotLabel(bar, slot)} (Leiste {bar + 1} Slot {slot}) <- action {id} '{name}'. " +
                  $"Vorher: {DescribeSlotRaw(module, bar, slot)}, LeisteGeteilt={module->IsHotbarShared((uint)bar)}");

        // try-catch: external game calls that mutate saved hotbar state.
        try
        {
            // V4.76-Probe bewies: SetAndSaveSlot schreibt nur den GESPEICHERTEN
            // Zustand - die 9:43-Zuweisung erschien erst nach dem Relog auf der
            // Leiste (Log 2026-07-17 11:59), live blieb der Slot unveraendert.
            // LoadSavedHotbar zieht den gespeicherten Stand sofort in die
            // Live-Leiste ("loads the saved hotbar into the live hotbar",
            // FFXIVClientStructs-Doku; respektiert PvP automatisch).
            module->SetAndSaveSlot((uint)bar, (uint)slot,
                RaptureHotbarModule.HotbarSlotType.Action, id);
            var ps = PlayerState.Instance();
            if (ps == null)
            {
                _log.Warning("[Hotbar] PlayerState null - LoadSavedHotbar uebersprungen, Aenderung greift erst beim Relog.");
            }
            else
            {
                module->LoadSavedHotbar(ps->CurrentClassJobId, (uint)bar);
            }
        }
        catch (Exception ex)
        {
            _tolk.SpeakInterrupt("Belegen fehlgeschlagen.");
            _log.Error(ex, $"[Hotbar] SetAndSaveSlot/LoadSavedHotbar krachte: bar={bar} slot={slot} action={id} '{name}'");
            return;
        }

        _log.Info($"[Hotbar] Direkt nach SetAndSaveSlot+LoadSavedHotbar: {DescribeSlotRaw(module, bar, slot)}");

        // Verdict 2 frames later - announce only what the slot then really holds.
        _framework.RunOnTick(() => VerifyAssignment(bar, slot, id, name), delayTicks: 2);
    }

    private unsafe void VerifyAssignment(int bar, int slot, uint actionId, string name)
    {
        var module = RaptureHotbarModule.Instance();
        var s = module == null ? null : module->GetSlotById((uint)bar, (uint)slot);
        if (s != null && s->CommandType == RaptureHotbarModule.HotbarSlotType.Action && s->CommandId == actionId)
        {
            _tolk.SpeakInterrupt($"{name} liegt jetzt auf {SlotLabel(bar, slot)}.");
            _log.Info($"[Hotbar] Belegt (nach 2 Frames): Leiste {bar + 1} Slot {slot} = action {actionId} '{name}'");
        }
        else
        {
            _tolk.SpeakInterrupt("Belegen fehlgeschlagen, die Taste hat sich nicht geändert.");
            _log.Warning($"[Hotbar] SetAndSaveSlot ohne Wirkung (nach 2 Frames): bar={bar} slot={slot} soll action {actionId} " +
                         $"'{name}', ist {(module == null ? "Modul null" : DescribeSlotRaw(module, bar, slot))}");
        }
    }

    /// <summary>Raw slot state for the probe log: command type/id plus the
    /// apparent (display-adjusted) action id.</summary>
    private unsafe string DescribeSlotRaw(RaptureHotbarModule* module, int bar, int slot)
    {
        var s = module->GetSlotById((uint)bar, (uint)slot);
        return s == null
            ? "Slot null"
            : $"type={s->CommandType} id={s->CommandId} apparent={s->ApparentActionId}";
    }

    /// <summary>Spoken location of this action on any standard bar
    /// ("Taste 7" / "Leiste 2, Taste Strg+3"), or null when not placed.</summary>
    private unsafe string? FindSlotLocationFor(uint actionId)
    {
        var module = RaptureHotbarModule.Instance();
        if (module == null) return null;
        for (var bar = 0; bar < StandardBarCount; bar++)
        for (var slot = 0; slot < SlotsToRead; slot++)
        {
            var s = module->GetSlotById((uint)bar, (uint)slot);
            if (s != null && s->CommandType == RaptureHotbarModule.HotbarSlotType.Action && s->CommandId == actionId)
                return SlotLabel(bar, slot);
        }
        return null;
    }

    /// <summary>
    /// Builds the learned-skill list for the current job and level, sorted by
    /// level like the game's Actions window. Filter (all columns ilspycmd-
    /// verified): non-PvP Action rows whose ClassJobCategory includes the
    /// current job, ClassJobLevel 1..current level, and - when UnlockLink is
    /// set - the unlock quest is completed (UIState.
    /// IsUnlockLinkUnlockedOrQuestCompleted, handles both link and quest ids).
    /// Rebuilt on job or level change; announces and returns false when empty.
    /// </summary>
    private unsafe bool EnsureSkillList()
    {
        if (!_clientState.IsLoggedIn)
        {
            _tolk.SpeakInterrupt("Nicht eingeloggt.");
            return false;
        }

        var ps = PlayerState.Instance();
        var ui = UIState.Instance();
        if (ps == null || ui == null)
        {
            _tolk.SpeakInterrupt("Spielerdaten noch nicht bereit.");
            return false;
        }

        var jobId = ps->CurrentClassJobId;
        var level = (uint)ps->CurrentLevel;
        if (_skills.Count > 0 && _skillsJobId == jobId && _skillsLevel == level) return true;

        _skills.Clear();
        _skillIndex = -1;
        _skillsJobId = jobId;
        _skillsLevel = level;

        var skippedLocked = 0;
        var skippedNotPlayer = 0;
        foreach (var row in _data.GetExcelSheet<LuminaAction>())
        {
            if (row.RowId == 0 || row.IsPvP) continue;
            // ClassJobLevel 0 = not a learned-by-level player action (system rows).
            if (row.ClassJobLevel == 0 || row.ClassJobLevel > level) continue;
            if (row.ClassJobCategory.RowId == 0 || row.ClassJobCategory.ValueNullable is not { } cat) continue;
            if (_gearInfo.AllowsJob(cat, jobId) != true) continue;
            // Without this the list carried internal non-player rows that pass
            // the job filter (five 'Ausweichen' + 'Perfekter Hieb', log
            // 2026-07-17 12:01) - IsPlayerAction marks the real skill entries.
            if (!row.IsPlayerAction) { skippedNotPlayer++; continue; }

            var name = row.Name.ExtractText();
            if (string.IsNullOrWhiteSpace(name)) continue;

            // UnlockLink 0 = no quest gate; otherwise ask the game.
            var unlock = row.UnlockLink.RowId;
            if (unlock != 0 && !ui->IsUnlockLinkUnlockedOrQuestCompleted(unlock))
            {
                skippedLocked++;
                continue;
            }

            _skills.Add((row.RowId, name, row.ClassJobLevel));
        }

        _skills.Sort((a, b) => a.Level != b.Level ? a.Level.CompareTo(b.Level) : a.Id.CompareTo(b.Id));
        _log.Info($"[Hotbar] Skill-Liste gebaut: Job {jobId}, Stufe {level}, {_skills.Count} Skills, " +
                  $"{skippedLocked} noch nicht freigeschaltet, {skippedNotPlayer} Nicht-Spieler-Actions gefiltert.");

        if (_skills.Count == 0)
        {
            _tolk.SpeakInterrupt("Keine Skills gefunden.");
            return false;
        }
        return true;
    }
}
