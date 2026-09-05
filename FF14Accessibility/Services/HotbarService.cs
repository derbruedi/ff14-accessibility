using System;
using System.Collections.Generic;
using System.Text;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using LuminaAction = Lumina.Excel.Sheets.Action;
using LuminaEventItem = Lumina.Excel.Sheets.EventItem;
using LuminaGeneralAction = Lumina.Excel.Sheets.GeneralAction;
using LuminaMount = Lumina.Excel.Sheets.Mount;

namespace FF14Accessibility.Services;

/// <summary>
/// Reads what is bound to the player's action bar so a blind player knows
/// which number key does what. FFXIV has no single "attack" button: you
/// target an enemy and press hotbar keys (1-9, 0 = Hotbar 1 slots) to use
/// actions. Also lets the player REBIND those keys: a modal menu browses the
/// assignable keys first, and confirming one opens the lists of things that
/// can go on it (skills, items, quest items, general actions, mounts). The
/// write goes through HotbarSlot.Set + WriteSavedSlot (see PlaceOnSlot), so
/// the change persists like a manual drag-and-drop one.
/// Structs ilspycmd-verified, see docs/game-api.md -> "Hotbar".
/// </summary>
public sealed class HotbarService
{
    private readonly IDataManager _data;
    private readonly IClientState _clientState;
    private readonly IFramework _framework;
    private readonly GearInfoService _gearInfo;
    private readonly KeybindService _keybinds;
    private readonly InventoryService _inventory;
    private readonly TolkService _tolk;
    private readonly IPluginLog _log;

    public HotbarService(IDataManager data, IClientState clientState, IFramework framework,
                         GearInfoService gearInfo, KeybindService keybinds, InventoryService inventory,
                         TolkService tolk, IPluginLog log)
    {
        _data = data;
        _clientState = clientState;
        _framework = framework;
        _gearInfo = gearInfo;
        _keybinds = keybinds;
        _inventory = inventory;
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

    // Which bar ReadHotbar reads out (module index; UI name = index + 1).
    // Fixed to the main bar - the skill menu picks its own target per slot.
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
            return AccessibilityStrings.SlotMainKey(BoundKeyFor(bar, slot) ?? SlotKeyNames[slot]);
        var key = BoundKeyFor(bar, slot);
        return key != null
            ? AccessibilityStrings.SlotBarKey(bar + 1, key)
            : AccessibilityStrings.SlotBarSlot(bar + 1, slot + 1);
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
            _tolk.SpeakInterrupt(AccessibilityStrings.HotbarUnavailable);
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
                ? AccessibilityStrings.SlotMainKey(SlotKeyNames[slot])
                : (BoundKeyFor(bar, slot) is { } key ? AccessibilityStrings.SlotMainKey(key) : AccessibilityStrings.SlotNumberWord(slot + 1));
            _log.Info($"[Hotbar] Leiste {bar + 1} Slot {slot} ({keyLabel}): type={s->CommandType} " +
                      $"id={s->CommandId} name='{name}'");
            parts.Add($"{keyLabel}, {name}");
        }

        if (parts.Count == 0)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.HotbarEmpty(bar + 1));
            return;
        }

        _tolk.SpeakInterrupt(AccessibilityStrings.HotbarPrefix(bar + 1) + string.Join(". ", parts) + ".");
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

        // Quest items index the EventItem sheet, not Action - resolve them the
        // same deterministic way instead of relying on the display string.
        if (type == RaptureHotbarModule.HotbarSlotType.EventItem &&
            _data.GetExcelSheet<LuminaEventItem>().TryGetRow(id, out var eventItem))
        {
            var eventItemName = eventItem.Name.ExtractText();
            if (!string.IsNullOrWhiteSpace(eventItemName))
                return eventItemName;
        }

        // General actions (Absteigen, Sprint, Teleport ...) and mounts have
        // their own sheets too - same reasoning as above.
        if (type == RaptureHotbarModule.HotbarSlotType.GeneralAction &&
            _data.GetExcelSheet<LuminaGeneralAction>().TryGetRow(id, out var general))
        {
            var generalName = general.Name.ExtractText();
            if (!string.IsNullOrWhiteSpace(generalName))
                return generalName;
        }

        if (type == RaptureHotbarModule.HotbarSlotType.Mount &&
            _data.GetExcelSheet<LuminaMount>().TryGetRow(id, out var mount))
        {
            var mountName = mount.Singular.ExtractText();   // Mount has no Name column
            if (!string.IsNullOrWhiteSpace(mountName))
                return mountName;
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

    // â”€â”€ Slot assignment menu: a modal, keyboard-only way to put a learned
    //    action, item, quest item, general action or mount on a hotbar key.
    //    Sighted players drag from "Actions & Traits"; there is no keyboard
    //    path in the game, so the plugin walks the player through it in the
    //    order a drag has: pick the KEY first (each one announcing what sits
    //    on it now), then pick what goes on it. That is the reverse of the
    //    original skill-first order (user choice 2026-09-05). After a
    //    placement the menu returns to the key list instead of closing, so a
    //    whole bar can be filled in one session. The write itself goes through
    //    PlaceOnSlot. Driven from the numpad while open; Plugin.cs swallows
    //    those keys so the character does not move.

    private enum SkillMenuStep { Closed, PickSlot, PickEntry }
    private SkillMenuStep _menuStep = SkillMenuStep.Closed;

    /// <summary>Which list the menu is browsing once a key has been picked.
    /// Numpad 4/6 steps through the sources (user choice 2026-08-06; quest
    /// items, general actions and mounts added 2026-08-09); the chosen key is
    /// the same target for all of them.</summary>
    private enum AssignSource { Skills, Items, QuestItems, GeneralActions, Mounts }
    private AssignSource _menuSource = AssignSource.Skills;

    /// <summary>The order Numpad 4/6 steps through, and the order the fallback
    /// in <see cref="EnterFirstUsableSource"/> tries. Static so stepping does
    /// not allocate on every keypress.</summary>
    private static readonly AssignSource[] SourceOrder =
    {
        AssignSource.Skills, AssignSource.Items, AssignSource.QuestItems,
        AssignSource.GeneralActions, AssignSource.Mounts,
    };

    private readonly List<(uint Id, string Name, byte Level)> _skills = new();
    private int _skillIndex = -1;

    // Carried usable items, rebuilt every time the item list is entered - the
    // inventory changes constantly (potions get drunk), and a cached list would
    // offer the player something that is no longer there.
    private readonly List<InventoryService.UsableItem> _items = new();
    private int _itemIndex = -1;

    // Usable key items (quest items). Same reasoning as _items, only more so:
    // they appear and vanish with quest progress.
    private readonly List<InventoryService.QuestItem> _questItems = new();
    private int _questItemIndex = -1;

    // General actions (Sprint, Teleport, Rueckfuehrung, Reittier-Roulette,
    // Absteigen ...) and the player's unlocked mounts. Both are plain id+name
    // lists that differ only in the slot type they are written with.
    private readonly List<(uint Id, string Name)> _generalActions = new();
    private int _generalActionIndex = -1;
    private readonly List<(uint Id, string Name)> _mounts = new();
    private int _mountIndex = -1;
    // The list is rebuilt when job or level changes (level-ups add skills).
    private byte _skillsJobId;
    private uint _skillsLevel;

    // Flat list of assignable target keys (bar/slot pairs), built when the
    // menu opens - it is the FIRST step now, so it no longer depends on what
    // is being placed.
    private readonly List<(int Bar, int Slot)> _targets = new();
    private int _targetIndex = -1;

    // The key confirmed in the first step: the target of whatever the second
    // step picks. Held explicitly rather than read back through _targetIndex so
    // browsing cannot move the target out from under a pending assignment.
    private int _chosenBar = -1;
    private int _chosenSlot = -1;

    /// <summary>Speaks a menu line, interrupting or queueing. A step's header
    /// interrupts (it replaces whatever was being said), the entry that follows
    /// it queues - two interrupting calls in a row would cut the header off
    /// before the player heard it.</summary>
    private void Say(string text, bool interrupt)
    {
        if (interrupt) _tolk.SpeakInterrupt(text);
        else _tolk.Speak(text);
    }

    /// <summary>True while the modal skill menu is open, so Plugin.cs routes
    /// the numpad keys here and swallows them from the game.</summary>
    public bool IsSkillMenuOpen => _menuStep != SkillMenuStep.Closed;

    /// <summary>Opens the assignment menu, or closes it when already open (the
    /// same key toggles). On open the list of assignable KEYS is built and the
    /// first one announced; what goes on it is the second step.</summary>
    public unsafe void ToggleSkillMenu()
    {
        if (_menuStep != SkillMenuStep.Closed) { CloseSkillMenu(); return; }

        if (!_clientState.IsLoggedIn)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.NotLoggedIn);
            return;
        }

        // The key list itself needs only the keybind table, but reading what
        // sits on a key and every path out of this menu go through the module -
        // refuse now rather than after the player has browsed and chosen.
        if (RaptureHotbarModule.Instance() == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.HotbarUnavailable);
            _log.Warning("[Hotbar] RaptureHotbarModule.Instance() ist null - Menue nicht geoeffnet.");
            return;
        }

        BuildTargetList();
        if (_targets.Count == 0)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.SkillMenuNoTargets);
            return;
        }

        _targetIndex = 0;
        _chosenBar = _chosenSlot = -1;
        // Reset the source only here, not per key: within one open session the
        // list the player last used stays selected, so filling several keys
        // from the bag does not walk back to the skills every time.
        _menuSource = AssignSource.Skills;
        _menuStep = SkillMenuStep.PickSlot;
        _tolk.SpeakInterrupt(AccessibilityStrings.SkillMenuSlotsOpened(_targets.Count));
        AnnounceTarget(interrupt: false);
    }

    /// <summary>
    /// Numpad 4 / 6: steps through the source lists while choosing WHAT goes on
    /// the already-picked key - skills, carried items, quest items, general
    /// actions, mounts - 6 forwards, 4 backwards. Ignored while the key itself
    /// is still being picked: there is no source to switch yet, and announcing
    /// one would contradict the key the player is standing on.
    /// <para>
    /// A source the player cannot use right now (empty bag, no quest items) is
    /// SKIPPED rather than announced as an error: stepping should always land
    /// somewhere usable. If nothing else has entries, the current list stays.
    /// </para>
    /// </summary>
    public void SkillMenuSwitchSource(int direction)
    {
        if (_menuStep != SkillMenuStep.PickEntry) return;

        var at = Array.IndexOf(SourceOrder, _menuSource);

        // Walk at most one full circle, stopping at the first source with entries.
        for (var step = 1; step < SourceOrder.Length; step++)
        {
            var next = SourceOrder[((at + direction * step) % SourceOrder.Length + SourceOrder.Length) % SourceOrder.Length];
            if (!TryEnterSource(next)) continue;
            return;
        }

        _tolk.SpeakInterrupt(AccessibilityStrings.SkillMenuNoOtherSource);
    }

    /// <summary>Switches to a source if it has anything to offer, and announces
    /// it. False when the list is empty (or cannot be built) - the caller then
    /// tries the next one. Silent on failure by design: a skipped source is not
    /// an error the player needs to hear about.
    /// <para>
    /// <paramref name="interrupt"/> is false when the caller has just spoken the
    /// key being filled: that line must finish before the list header follows.
    /// The entry inside always queues behind the header for the same reason.
    /// </para></summary>
    private bool TryEnterSource(AssignSource source, bool interrupt = true)
    {
        switch (source)
        {
            case AssignSource.Skills:
                if (!BuildSkillList()) return false;
                _menuSource = AssignSource.Skills;
                if (_skillIndex < 0 || _skillIndex >= _skills.Count) _skillIndex = 0;
                Say(AccessibilityStrings.SkillMenuOpened(_skills.Count), interrupt);
                AnnounceSkill(interrupt: false);
                return true;

            case AssignSource.Items:
                if (!BuildItemList()) return false;
                _menuSource = AssignSource.Items;
                if (_itemIndex < 0 || _itemIndex >= _items.Count) _itemIndex = 0;
                Say(AccessibilityStrings.ItemMenuOpened(_items.Count), interrupt);
                AnnounceItem(interrupt: false);
                return true;

            case AssignSource.QuestItems:
                if (!BuildQuestItemList()) return false;
                _menuSource = AssignSource.QuestItems;
                if (_questItemIndex < 0 || _questItemIndex >= _questItems.Count) _questItemIndex = 0;
                Say(AccessibilityStrings.QuestItemMenuOpened(_questItems.Count), interrupt);
                AnnounceQuestItem(interrupt: false);
                return true;

            case AssignSource.GeneralActions:
                if (!BuildGeneralActionList()) return false;
                _menuSource = AssignSource.GeneralActions;
                if (_generalActionIndex < 0 || _generalActionIndex >= _generalActions.Count) _generalActionIndex = 0;
                Say(AccessibilityStrings.GeneralActionMenuOpened(_generalActions.Count), interrupt);
                AnnounceGeneralAction(interrupt: false);
                return true;

            case AssignSource.Mounts:
                if (!BuildMountList()) return false;
                _menuSource = AssignSource.Mounts;
                if (_mountIndex < 0 || _mountIndex >= _mounts.Count) _mountIndex = 0;
                Say(AccessibilityStrings.MountMenuOpened(_mounts.Count), interrupt);
                AnnounceMount(interrupt: false);
                return true;
        }
        return false;
    }

    /// <summary>
    /// Rebuilds the carried usable items. Always rebuilt (never cached): the bag
    /// changes while playing, and offering a potion that was already drunk would
    /// assign a slot the player cannot use. False when the player carries
    /// nothing usable - silent, the caller decides what that means.
    /// </summary>
    private bool BuildItemList()
    {
        if (!_clientState.IsLoggedIn) return false;

        _items.Clear();
        _items.AddRange(_inventory.CollectUsableItems());
        _itemIndex = _items.Count > 0 ? 0 : -1;
        return _items.Count > 0;
    }

    /// <summary>
    /// Rebuilds the carried usable quest items (key items that do something -
    /// see <see cref="InventoryService.CollectQuestItems"/>). Rebuilt per entry
    /// like the bag list: a quest item vanishes the moment the quest step is
    /// done. False when there are none - silent, same reasoning as above.
    /// </summary>
    private bool BuildQuestItemList()
    {
        if (!_clientState.IsLoggedIn) return false;

        _questItems.Clear();
        _questItems.AddRange(_inventory.CollectQuestItems());
        _questItemIndex = _questItems.Count > 0 ? 0 : -1;
        return _questItems.Count > 0;
    }

    /// <summary>Skill list for the source stepper: builds it and reports whether
    /// it has entries. Keeps the spoken diagnosis of <see cref="EnsureSkillList"/>
    /// for the real failure cases (not logged in, player data not ready).</summary>
    private bool BuildSkillList() => EnsureSkillList() && _skills.Count > 0;

    /// <summary>
    /// Rebuilds the general actions - the things the game itself lists under
    /// "Allgemein" in the actions window: Sprint, Teleport, Rueckfuehrung,
    /// Reittier-Roulette and, the reason this list exists, "Absteigen".
    /// <para>
    /// There is NO keyboard binding for mounting or dismounting in the game
    /// (live keybind dump 2026-08-09, 679 entries) - a sighted player drags
    /// these onto a bar, and this list is the keyboard equivalent of that drag.
    /// </para>
    /// <para>
    /// Filter: a named row whose <c>UnlockLink</c> is either 0 (no gate) or
    /// already unlocked, asked of the game via
    /// <c>UIState.IsUnlockLinkUnlockedOrQuestCompleted</c> - the same call the
    /// skill list uses. Deliberately NOT filtered on <c>UIPriority</c>: exactly
    /// the entries the player asked for ("Absteigen" #23, "Flugreittier-
    /// Roulette" #24) carry priority 0, so that column would drop them.
    /// Sorted by name because a browsed list has to be predictable; the sheet's
    /// own order is unusable here for the same priority-0 reason.
    /// </para>
    /// </summary>
    private unsafe bool BuildGeneralActionList()
    {
        if (!_clientState.IsLoggedIn) return false;

        var ui = UIState.Instance();
        if (ui == null) return false;

        _generalActions.Clear();
        foreach (var row in _data.GetExcelSheet<LuminaGeneralAction>())
        {
            if (row.RowId == 0) continue;
            var name = row.Name.ExtractText();
            if (string.IsNullOrWhiteSpace(name)) continue;

            var unlock = row.UnlockLink;
            if (unlock != 0 && !ui->IsUnlockLinkUnlockedOrQuestCompleted(unlock)) continue;

            _generalActions.Add((row.RowId, name));
        }

        _generalActions.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));
        _generalActionIndex = _generalActions.Count > 0 ? 0 : -1;
        _log.Info($"[Hotbar] Allgemeine Aktionen: {_generalActions.Count} " +
                  $"({string.Join(", ", _generalActions.Take(8).Select(a => $"{a.Name}#{a.Id}"))})");
        return _generalActions.Count > 0;
    }

    /// <summary>
    /// Rebuilds the player's mounts: every named Mount row the game reports as
    /// unlocked via <c>PlayerState.IsMountUnlocked</c> (ilspycmd-verified
    /// 2026-08-09). Asking the game keeps the list honest - the Mount sheet has
    /// 366 named rows, and offering one the player does not own would put a dead
    /// entry on a bar. Sorted by name, same reasoning as above.
    /// </summary>
    private unsafe bool BuildMountList()
    {
        if (!_clientState.IsLoggedIn) return false;

        var ps = PlayerState.Instance();
        if (ps == null) return false;

        _mounts.Clear();
        foreach (var row in _data.GetExcelSheet<LuminaMount>())
        {
            if (row.RowId == 0) continue;
            var name = row.Singular.ExtractText();   // Mount has no Name column
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (!ps->IsMountUnlocked(row.RowId)) continue;

            _mounts.Add((row.RowId, name));
        }

        _mounts.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));
        _mountIndex = _mounts.Count > 0 ? 0 : -1;
        _log.Info($"[Hotbar] Freigeschaltete Reittiere: {_mounts.Count} " +
                  $"({string.Join(", ", _mounts.Take(8).Select(m => $"{m.Name}#{m.Id}"))})");
        return _mounts.Count > 0;
    }

    /// <summary>
    /// Opens the list of things that can go on the just-chosen key: the source
    /// last used in this session when it still has entries, otherwise the first
    /// one that does. Announcements queue - the caller has just said which key
    /// is being filled and that line must not be cut off. False when no source
    /// has anything to offer at all, which leaves the player on the key list.
    /// </summary>
    private bool EnterFirstUsableSource()
    {
        if (TryEnterSource(_menuSource, interrupt: false)) return true;
        foreach (var source in SourceOrder)
            if (source != _menuSource && TryEnterSource(source, interrupt: false))
                return true;
        return false;
    }

    /// <summary>Closes the menu and says so.</summary>
    public void CloseSkillMenu()
    {
        _menuStep = SkillMenuStep.Closed;
        _chosenBar = _chosenSlot = -1;
        _tolk.SpeakInterrupt(AccessibilityStrings.SkillMenuClosed);
    }

    /// <summary>Numpad 8 / 2: browse the current step's list (wraps).</summary>
    public void SkillMenuBrowse(int direction)
    {
        switch (_menuStep)
        {
            case SkillMenuStep.PickSlot:
                if (_targets.Count == 0) return;
                _targetIndex = ((_targetIndex + direction) % _targets.Count + _targets.Count) % _targets.Count;
                AnnounceTarget();
                break;
            case SkillMenuStep.PickEntry when _menuSource == AssignSource.QuestItems:
                if (_questItems.Count == 0) return;
                _questItemIndex = ((_questItemIndex + direction) % _questItems.Count + _questItems.Count) % _questItems.Count;
                AnnounceQuestItem();
                break;
            case SkillMenuStep.PickEntry when _menuSource == AssignSource.GeneralActions:
                if (_generalActions.Count == 0) return;
                _generalActionIndex = ((_generalActionIndex + direction) % _generalActions.Count + _generalActions.Count) % _generalActions.Count;
                AnnounceGeneralAction();
                break;
            case SkillMenuStep.PickEntry when _menuSource == AssignSource.Mounts:
                if (_mounts.Count == 0) return;
                _mountIndex = ((_mountIndex + direction) % _mounts.Count + _mounts.Count) % _mounts.Count;
                AnnounceMount();
                break;
            case SkillMenuStep.PickEntry when _menuSource == AssignSource.Items:
                if (_items.Count == 0) return;
                _itemIndex = ((_itemIndex + direction) % _items.Count + _items.Count) % _items.Count;
                AnnounceItem();
                break;
            case SkillMenuStep.PickEntry:
                if (_skills.Count == 0) return;
                _skillIndex = ((_skillIndex + direction) % _skills.Count + _skills.Count) % _skills.Count;
                AnnounceSkill();
                break;
        }
    }

    /// <summary>Numpad 0: confirm the current step. Key step -> remember the key
    /// and open the lists of what can go on it; entry step -> place it and
    /// return to the key list, so the next key can be filled right away (user
    /// choice 2026-09-05).</summary>
    public void SkillMenuConfirm()
    {
        switch (_menuStep)
        {
            case SkillMenuStep.PickSlot:
                if (_targetIndex < 0 || _targetIndex >= _targets.Count) return;
                var (bar, slot) = _targets[_targetIndex];

                // Name the key being filled BEFORE the list header, so the
                // context is never lost between the two steps.
                _tolk.SpeakInterrupt(AccessibilityStrings.SkillMenuPickEntry(
                    SlotLabel(bar, slot), CurrentSlotContent(bar, slot)));

                if (!EnterFirstUsableSource())
                {
                    _tolk.Speak(AccessibilityStrings.SkillMenuNothingToAssign);
                    return;                      // stay on the key list
                }

                _chosenBar = bar;
                _chosenSlot = slot;
                _menuStep = SkillMenuStep.PickEntry;
                break;

            case SkillMenuStep.PickEntry:
                if (_chosenBar < 0 || _chosenSlot < 0) return;
                var placed = _menuSource switch
                {
                    AssignSource.Items      => AssignItemToSlot(_itemIndex, _chosenBar, _chosenSlot),
                    AssignSource.QuestItems => AssignQuestItemToSlot(_questItemIndex, _chosenBar, _chosenSlot),
                    AssignSource.GeneralActions => AssignEntryToSlot(_generalActions, _generalActionIndex,
                        _chosenBar, _chosenSlot, RaptureHotbarModule.HotbarSlotType.GeneralAction),
                    AssignSource.Mounts     => AssignEntryToSlot(_mounts, _mountIndex,
                        _chosenBar, _chosenSlot, RaptureHotbarModule.HotbarSlotType.Mount),
                    _                       => AssignSkillToSlot(_skillIndex, _chosenBar, _chosenSlot),
                };

                // Back to the key list either way - the player keeps filling the
                // bar. On a started placement VerifyAssignment speaks the verdict
                // two frames later and appends the reminder itself; a placement
                // that never started has nothing pending, so it is said here.
                _menuStep = SkillMenuStep.PickSlot;
                _chosenBar = _chosenSlot = -1;
                if (!placed)
                    _tolk.Speak(AccessibilityStrings.SkillMenuBackAtSlots(_targets.Count));
                break;
        }
    }

    /// <summary>Numpad comma: step back from the entry lists to the key list, or
    /// close the menu from there.</summary>
    public void SkillMenuBack()
    {
        if (_menuStep == SkillMenuStep.PickEntry)
        {
            _menuStep = SkillMenuStep.PickSlot;
            _chosenBar = _chosenSlot = -1;
            _tolk.SpeakInterrupt(AccessibilityStrings.SkillMenuBackAtSlots(_targets.Count));
            AnnounceTarget(interrupt: false);
        }
        else
        {
            CloseSkillMenu();
        }
    }

    /// <summary>Announces the current skill: name, level, where it already sits
    /// (if anywhere) and its position in the list.</summary>
    private void AnnounceSkill(bool interrupt = true)
    {
        var (id, name, level) = _skills[_skillIndex];
        var location = FindSlotLocationFor(RaptureHotbarModule.HotbarSlotType.Action, id);
        Say(AccessibilityStrings.SkillBrowseEntry(name, level, location, _skillIndex + 1, _skills.Count), interrupt);
    }

    /// <summary>Announces the current item: name, stack size, where it already
    /// sits (if anywhere) and its position in the list. The count matters for
    /// the decision - a potion with one left is a different choice.</summary>
    private void AnnounceItem(bool interrupt = true)
    {
        var item = _items[_itemIndex];
        var location = FindSlotLocationFor(RaptureHotbarModule.HotbarSlotType.Item, item.ItemId);
        Say(AccessibilityStrings.ItemBrowseEntry(
            item.Name, item.Quantity, item.IsHq, location, _itemIndex + 1, _items.Count), interrupt);
    }

    /// <summary>Announces the current quest item: name, how many are left, its
    /// cast time and where it already sits. The cast time is spoken because in a
    /// fight it decides whether there is room to use the thing at all - a
    /// sighted player reads it off the tooltip.</summary>
    private void AnnounceQuestItem(bool interrupt = true)
    {
        var item = _questItems[_questItemIndex];
        var location = FindSlotLocationFor(RaptureHotbarModule.HotbarSlotType.EventItem, item.ItemId);
        Say(AccessibilityStrings.QuestItemBrowseEntry(
            item.Name, item.Quantity, item.CastTime, location, _questItemIndex + 1, _questItems.Count), interrupt);
    }

    /// <summary>Announces the current general action: name, where it already
    /// sits and its position in the list.</summary>
    private void AnnounceGeneralAction(bool interrupt = true)
    {
        var (id, name) = _generalActions[_generalActionIndex];
        var location = FindSlotLocationFor(RaptureHotbarModule.HotbarSlotType.GeneralAction, id);
        Say(AccessibilityStrings.PlainBrowseEntry(
            name, location, _generalActionIndex + 1, _generalActions.Count), interrupt);
    }

    /// <summary>Announces the current mount: name, where it already sits and its
    /// position in the list.</summary>
    private void AnnounceMount(bool interrupt = true)
    {
        var (id, name) = _mounts[_mountIndex];
        var location = FindSlotLocationFor(RaptureHotbarModule.HotbarSlotType.Mount, id);
        Say(AccessibilityStrings.PlainBrowseEntry(
            name, location, _mountIndex + 1, _mounts.Count), interrupt);
    }

    /// <summary>Announces the current target key: its label, what is on it now,
    /// and its position in the list.</summary>
    private void AnnounceTarget(bool interrupt = true)
    {
        var (bar, slot) = _targets[_targetIndex];
        Say(AccessibilityStrings.SkillMenuTargetEntry(
            SlotLabel(bar, slot), CurrentSlotContent(bar, slot), _targetIndex + 1, _targets.Count), interrupt);
    }

    /// <summary>Spoken name of whatever sits on a slot right now, or "empty".
    /// Read live on every announcement - a cached value would tell the player
    /// what the key held before their last placement.</summary>
    private unsafe string CurrentSlotContent(int bar, int slot)
    {
        var module = RaptureHotbarModule.Instance();
        var s = module == null ? null : module->GetSlotById((uint)bar, (uint)slot);
        return s == null || s->CommandType == RaptureHotbarModule.HotbarSlotType.Empty
            ? AccessibilityStrings.InputEmpty
            : ResolveName(s->CommandType, s->CommandId, s->PopUpHelp.ToString());
    }

    /// <summary>Builds the flat list of assignable target keys: bar 1's twelve
    /// slots always (keys 1-0, 11, 12), plus any slot on bars 2-10 that has a
    /// key bound - only those can actually fire what is placed there. Depends on
    /// nothing but the keybind table, which is why it can run when the menu
    /// opens rather than after something has been chosen.</summary>
    private void BuildTargetList()
    {
        _targets.Clear();
        for (var bar = 0; bar < StandardBarCount; bar++)
        for (var slot = 0; slot < SlotsToRead; slot++)
            if (bar == MainHotbarIndex || BoundKeyFor(bar, slot) != null)
                _targets.Add((bar, slot));
    }

    /// <summary>
    /// Puts the browsed skill on the chosen key: SetAndSaveSlot persists the
    /// change per job (same path as drag-and-drop), LoadSavedHotbar then pulls
    /// the saved state into the LIVE bar - the V4.76 probe proved that
    /// SetAndSaveSlot alone only updates the saved side (the 09:43 assignment
    /// appeared on the bar after relog; log 2026-07-17 11:59). Success is only
    /// announced after a 2-frame read-back confirms the slot really changed.
    /// <para>
    /// Returns whether the read-back was scheduled: false means the placement
    /// never started, so the caller has to speak the menu state itself instead
    /// of waiting for a verdict that will not come.
    /// </para>
    /// </summary>
    private unsafe bool AssignSkillToSlot(int skillIndex, int bar, int slot)
    {
        if (skillIndex < 0 || skillIndex >= _skills.Count)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.NoSkillSelected);
            return false;
        }

        var module = RaptureHotbarModule.Instance();
        if (module == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.HotbarUnavailable);
            return false;
        }

        var (id, name, _) = _skills[skillIndex];

        _log.Info($"[Hotbar] Belegen: {SlotLabel(bar, slot)} (Leiste {bar + 1} Slot {slot}) <- action {id} '{name}'. " +
                  $"Vorher: {DescribeSlotRaw(module, bar, slot)}, LeisteGeteilt={module->IsHotbarShared((uint)bar)}");

        if (!PlaceOnSlot(module, bar, slot, RaptureHotbarModule.HotbarSlotType.Action, id, name))
            return false;

        // Verdict 2 frames later - announce only what the slot then really holds.
        _framework.RunOnTick(() => VerifyAssignment(bar, slot, id, name), delayTicks: 2);
        return true;
    }

    /// <summary>
    /// Writes one entry to a hotbar slot, live bar AND saved state.
    /// <para>
    /// SetAndSaveSlot is NOT used any more. Measured 2026-08-06 (/acc
    /// hotbarprobe, two jobs): with class Thaumaturge (7) it wrote the saved
    /// state and LoadSavedHotbar pulled the entry onto the bar, but with job
    /// Black Mage (25) the very same call left both sides untouched - for
    /// actions just as much as for items, which is why assigning skills had
    /// silently stopped working too. Its default lets the game save into the PvP
    /// set, which jobs have and classes do not; that is the likely reason, but
    /// it is a HYPOTHESIS, not measured.
    /// </para>
    /// <para>
    /// What IS measured: HotbarSlot.Set puts the entry on the live bar, and
    /// WriteSavedSlot - the direct counterpart of LoadSavedHotbar, told
    /// explicitly that this is not a PvP slot - persists it. That pair held in
    /// BOTH jobs, including across a reload (probe steps F1/F2).
    /// </para>
    /// LoadSavedHotbar is still called afterwards on purpose: it pulls the saved
    /// state back over the live bar, so a failed save shows up as a slot that
    /// reverts - and the read-back then reports honest failure instead of a
    /// change that only looks right until the next reload.
    /// </summary>
    private unsafe bool PlaceOnSlot(RaptureHotbarModule* module, int bar, int slot,
        RaptureHotbarModule.HotbarSlotType type, uint id, string name)
    {
        var ps = PlayerState.Instance();
        if (ps == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.PlayerDataNotReady);
            _log.Warning("[Hotbar] PlayerState null - Belegen abgebrochen.");
            return false;
        }

        // try-catch: external game calls that mutate saved hotbar state.
        try
        {
            var live = module->GetSlotById((uint)bar, (uint)slot);
            if (live == null)
            {
                _tolk.SpeakInterrupt(AccessibilityStrings.AssignFailed);
                _log.Warning($"[Hotbar] GetSlotById lieferte null: bar={bar} slot={slot}");
                return false;
            }

            live->Set(type, id);
            module->WriteSavedSlot(ps->CurrentClassJobId, (uint)bar, (uint)slot, live,
                ignoreSharedHotbars: false, isPvpSlot: false);
            module->LoadSavedHotbar(ps->CurrentClassJobId, (uint)bar);
        }
        catch (Exception ex)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.AssignFailed);
            _log.Error(ex, $"[Hotbar] Set/WriteSavedSlot krachte: bar={bar} slot={slot} type={type} id={id} '{name}'");
            return false;
        }

        _log.Info($"[Hotbar] Direkt nach Set+WriteSavedSlot+LoadSavedHotbar: {DescribeSlotRaw(module, bar, slot)}");
        return true;
    }

    /// <summary>
    /// Puts a carried item on the chosen key, through the same measured path as
    /// <see cref="AssignSkillToSlot"/> (see <see cref="PlaceOnSlot"/>). The id
    /// used is the one the GAME carries for that stack (HQ offset already
    /// applied by Dalamud's GameInventoryItem.ItemId), so nothing is recomputed
    /// here. Success is only announced after the read-back.
    /// <para>
    /// Returns whether the read-back was scheduled: false means the placement
    /// never started, so the caller has to speak the menu state itself instead
    /// of waiting for a verdict that will not come.
    /// </para>
    /// </summary>
    private unsafe bool AssignItemToSlot(int itemIndex, int bar, int slot)
    {
        if (itemIndex < 0 || itemIndex >= _items.Count)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.NoSkillSelected);
            return false;
        }

        var module = RaptureHotbarModule.Instance();
        if (module == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.HotbarUnavailable);
            return false;
        }

        var item = _items[itemIndex];
        _log.Info($"[Hotbar] Belegen: {SlotLabel(bar, slot)} (Leiste {bar + 1} Slot {slot}) <- item {item.ItemId} " +
                  $"'{item.Name}' (Basis {item.BaseItemId}, hq={item.IsHq}, Anzahl {item.Quantity}). " +
                  $"Vorher: {DescribeSlotRaw(module, bar, slot)}");

        if (!PlaceOnSlot(module, bar, slot, RaptureHotbarModule.HotbarSlotType.Item, item.ItemId, item.Name))
            return false;

        _framework.RunOnTick(
            () => VerifyAssignment(bar, slot, item.ItemId, item.Name, RaptureHotbarModule.HotbarSlotType.Item),
            delayTicks: 2);
        return true;
    }

    /// <summary>
    /// Puts a usable quest item on the chosen key, through the same measured
    /// path as the other two sources (see <see cref="PlaceOnSlot"/>).
    /// <para>
    /// The slot type is <c>EventItem</c> with the EventItem row id. The game
    /// also knows <c>HotbarSlotType.KeyItem</c>, but its own doc marks that as
    /// the drag-and-drop form whose id is a SLOT INDEX in the key-item
    /// container, resolved to EventItem on write (RaptureHotbarModule,
    /// ilspycmd 2026-08-09) - a slot index would break as soon as the container
    /// reorders, so the stable row id is used.
    /// </para>
    /// <para>
    /// Returns whether the read-back was scheduled: false means the placement
    /// never started, so the caller has to speak the menu state itself instead
    /// of waiting for a verdict that will not come.
    /// </para>
    /// </summary>
    private unsafe bool AssignQuestItemToSlot(int questItemIndex, int bar, int slot)
    {
        if (questItemIndex < 0 || questItemIndex >= _questItems.Count)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.NoSkillSelected);
            return false;
        }

        var module = RaptureHotbarModule.Instance();
        if (module == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.HotbarUnavailable);
            return false;
        }

        var item = _questItems[questItemIndex];
        _log.Info($"[Hotbar] Belegen: {SlotLabel(bar, slot)} (Leiste {bar + 1} Slot {slot}) <- eventitem {item.ItemId} " +
                  $"'{item.Name}' (Anzahl {item.Quantity}, Wirkzeit {item.CastTime}s). " +
                  $"Vorher: {DescribeSlotRaw(module, bar, slot)}");

        if (!PlaceOnSlot(module, bar, slot, RaptureHotbarModule.HotbarSlotType.EventItem, item.ItemId, item.Name))
            return false;

        _framework.RunOnTick(
            () => VerifyAssignment(bar, slot, item.ItemId, item.Name, RaptureHotbarModule.HotbarSlotType.EventItem),
            delayTicks: 2);
        return true;
    }

    /// <summary>
    /// Puts a plain id+name entry (general action, mount) on the chosen key,
    /// through the same measured path as every other source
    /// (see <see cref="PlaceOnSlot"/>). Only the slot type differs, which is why
    /// these two share one method instead of copying the skill/item ones.
    /// <para>
    /// Returns whether the read-back was scheduled: false means the placement
    /// never started, so the caller has to speak the menu state itself instead
    /// of waiting for a verdict that will not come.
    /// </para>
    /// </summary>
    private unsafe bool AssignEntryToSlot(List<(uint Id, string Name)> entries, int index,
        int bar, int slot, RaptureHotbarModule.HotbarSlotType type)
    {
        if (index < 0 || index >= entries.Count)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.NoSkillSelected);
            return false;
        }

        var module = RaptureHotbarModule.Instance();
        if (module == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.HotbarUnavailable);
            return false;
        }

        var (id, name) = entries[index];
        _log.Info($"[Hotbar] Belegen: {SlotLabel(bar, slot)} (Leiste {bar + 1} Slot {slot}) <- {type} {id} " +
                  $"'{name}'. Vorher: {DescribeSlotRaw(module, bar, slot)}");

        if (!PlaceOnSlot(module, bar, slot, type, id, name)) return false;

        _framework.RunOnTick(() => VerifyAssignment(bar, slot, id, name, type), delayTicks: 2);
        return true;
    }

#if DEBUG
    /// <summary>
    /// Measures WHY placing an item on a bar does nothing. The first attempt
    /// (SetAndSaveSlot + LoadSavedHotbar with HotbarSlotType.Item) left the slot
    /// Empty already before the read-back, so either SetAndSaveSlot writes
    /// nothing or LoadSavedHotbar wipes it again - the existing log cannot tell
    /// the two apart. This probe logs the slot state after EVERY single step and
    /// tries the alternatives the game itself offers, then restores the slot.
    /// Runs on the LAST slot of the main bar (key 12) and refuses when that slot
    /// is occupied, so nothing of the player's setup is at risk.
    /// </summary>
    public unsafe void ProbeItemAssignment()
    {
        var module = RaptureHotbarModule.Instance();
        var ps = PlayerState.Instance();
        if (module == null || ps == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.HotbarUnavailable);
            return;
        }

        var items = _inventory.CollectUsableItems();
        if (items.Count == 0)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.NoUsableItems);
            return;
        }

        // Measure on the MAIN bar - that is where assignment has to work. Prefer
        // an empty slot; if the bar is full, take the last one and put its exact
        // content back at the end (type + id are all a slot carries).
        const int bar = MainHotbarIndex;
        var slot = SlotsToRead - 1;
        for (var i = 0; i < SlotsToRead; i++)
        {
            var candidate = module->GetSlotById(bar, (uint)i);
            if (candidate != null && candidate->CommandType == RaptureHotbarModule.HotbarSlotType.Empty)
            {
                slot = i;
                break;
            }
        }

        var original = module->GetSlotById(bar, (uint)slot);
        var originalType = original == null ? RaptureHotbarModule.HotbarSlotType.Empty : original->CommandType;
        var originalId = original == null ? 0u : original->CommandId;

        var item = items[0];
        var jobId = ps->CurrentClassJobId;
        // The suspected culprit: the id the module currently keeps its bars
        // under. Class and job share hotbars in FFXIV (Thaumaturge/Black Mage),
        // so this can differ from the player's CurrentClassJobId - and then a
        // save keyed on the wrong id writes into a set nobody reads back.
        var activeJob = module->ActiveHotbarClassJobId;
        _log.Info($"[HotbarProbe] Start. Item {item.ItemId} '{item.Name}' (Basis {item.BaseItemId}, hq={item.IsHq}), " +
                  $"Leiste {bar + 1} Slot {slot + 1}, " +
                  $"CurrentClassJobId={jobId}, ActiveHotbarClassJobId={activeJob} " +
                  $"(gleich={(jobId == activeJob)}), " +
                  $"LeisteGeteilt(1)={module->IsHotbarShared(bar)} LeisteGeteilt(3)={module->IsHotbarShared(2)}, " +
                  $"Ausgang: {DescribeSlotRaw(module, bar, slot)} " +
                  $"(wird am Ende auf type={originalType} id={originalId} zurueckgesetzt)");

        // A: exactly what the feature does today, but with a reading BETWEEN the
        //    two calls - that is the missing measurement.
        module->SetAndSaveSlot(bar, (uint)slot, RaptureHotbarModule.HotbarSlotType.Item, item.ItemId);
        _log.Info($"[HotbarProbe] A1 nach SetAndSaveSlot(Item): {DescribeSlotRaw(module, bar, slot)}");
        module->LoadSavedHotbar(jobId, bar);
        _log.Info($"[HotbarProbe] A2 nach LoadSavedHotbar:      {DescribeSlotRaw(module, bar, slot)}");

        // B: same call, but not routed through the shared-hotbar handling.
        module->SetAndSaveSlot(bar, (uint)slot, RaptureHotbarModule.HotbarSlotType.Item, item.ItemId,
            ignoreSharedHotbars: true);
        _log.Info($"[HotbarProbe] B  SetAndSaveSlot(ignoreShared): {DescribeSlotRaw(module, bar, slot)}");

        // C: the slot's own Set - the live bar only, no saving involved.
        var s = module->GetSlotById(bar, (uint)slot);
        if (s != null)
        {
            s->Set(RaptureHotbarModule.HotbarSlotType.Item, item.ItemId);
            _log.Info($"[HotbarProbe] C  HotbarSlot.Set(Item):       {DescribeSlotRaw(module, bar, slot)}");
        }

        // D: control group - the SAME calls with an action, to show whether the
        //    item type is the problem or the whole assignment path is.
        if (_skills.Count > 0 || EnsureSkillList())
        {
            var actionId = _skills[0].Id;
            module->SetAndSaveSlot(bar, (uint)slot, RaptureHotbarModule.HotbarSlotType.Action, actionId);
            _log.Info($"[HotbarProbe] D1 SetAndSaveSlot(Action {actionId} '{_skills[0].Name}'): {DescribeSlotRaw(module, bar, slot)}");
            module->LoadSavedHotbar(jobId, bar);
            _log.Info($"[HotbarProbe] D2 nach LoadSavedHotbar:      {DescribeSlotRaw(module, bar, slot)}");
        }

        // E: does a DIFFERENT SetAndSave* overload work? If yes, only
        //    SetAndSaveSlot itself is broken, not the whole family.
        var okFirst = module->SetAndSaveFirstAvailableNormalSlot(
            bar, RaptureHotbarModule.HotbarSlotType.Item, item.ItemId);
        _log.Info($"[HotbarProbe] E  SetAndSaveFirstAvailableNormalSlot -> {okFirst}, " +
                  $"Sondenslot: {DescribeSlotRaw(module, bar, slot)}");
        if (okFirst)
        {
            // It picks its own slot - find and log where it landed, then clear it.
            for (var i = 0; i < SlotsToRead; i++)
            {
                var f = module->GetSlotById(bar, (uint)i);
                if (f == null || f->CommandType != RaptureHotbarModule.HotbarSlotType.Item || f->CommandId != item.ItemId)
                    continue;
                _log.Info($"[HotbarProbe] E  gelandet auf Slot {i + 1}");
                if (i != slot)
                {
                    f->Set(RaptureHotbarModule.HotbarSlotType.Empty, 0);
                    module->ClearSavedSlotById(bar, (uint)i);
                }
                break;
            }
        }

        // F: THE CANDIDATE FIX - set the live slot (proven to work in C) and then
        //    write that slot into the saved state, the direct counterpart of
        //    LoadSavedHotbar. Surviving the reload is what makes it a real fix
        //    rather than a display-only change.
        var live = module->GetSlotById(bar, (uint)slot);
        if (live != null)
        {
            live->Set(RaptureHotbarModule.HotbarSlotType.Item, item.ItemId);
            module->WriteSavedSlot(jobId, bar, (uint)slot, live, false, false);
            _log.Info($"[HotbarProbe] F1 Set + WriteSavedSlot:        {DescribeSlotRaw(module, bar, slot)}");
            module->LoadSavedHotbar(jobId, bar);
            _log.Info($"[HotbarProbe] F2 nach LoadSavedHotbar:        {DescribeSlotRaw(module, bar, slot)}" +
                      "   <- bleibt es hier stehen, ist das der Weg");
            _log.Info($"[HotbarProbe] F3 SavePending={module->GetIsSavePending()}");
        }

        // G: same as F, but keyed on the id the MODULE uses rather than the
        //    player's job id. If F fails and G holds, the id was the whole
        //    problem - and the fix is to stop passing CurrentClassJobId.
        var live2 = module->GetSlotById(bar, (uint)slot);
        if (live2 != null && activeJob != jobId)
        {
            live2->Set(RaptureHotbarModule.HotbarSlotType.Item, item.ItemId);
            module->WriteSavedSlot(activeJob, bar, (uint)slot, live2, false, false);
            module->LoadSavedHotbar(activeJob, bar);
            _log.Info($"[HotbarProbe] G  Set + WriteSavedSlot(activeJob {activeJob}) + LoadSavedHotbar: " +
                      $"{DescribeSlotRaw(module, bar, slot)}");
        }
        else
        {
            _log.Info($"[HotbarProbe] G  uebersprungen (CurrentClassJobId und ActiveHotbarClassJobId sind gleich: {jobId})");
        }

        // Leave the player's bar exactly as it was - live slot AND saved state.
        var restore = module->GetSlotById(bar, (uint)slot);
        if (restore != null)
        {
            restore->Set(originalType, originalId);
            // Restore the SAVED state through the same path F uses - SetAndSaveSlot
            // is proven ineffective above and would leave the old value stored.
            module->WriteSavedSlot(jobId, bar, (uint)slot, restore, false, false);
        }
        module->LoadSavedHotbar(jobId, bar);
        _log.Info($"[HotbarProbe] Ende, wiederhergestellt (soll type={originalType} id={originalId}): " +
                  $"{DescribeSlotRaw(module, bar, slot)}");
        _tolk.SpeakInterrupt(AccessibilityStrings.ProbeDone);
    }
#endif

    private unsafe void VerifyAssignment(int bar, int slot, uint actionId, string name,
        RaptureHotbarModule.HotbarSlotType type = RaptureHotbarModule.HotbarSlotType.Action)
    {
        var module = RaptureHotbarModule.Instance();
        var s = module == null ? null : module->GetSlotById((uint)bar, (uint)slot);
        if (s != null && s->CommandType == type && s->CommandId == actionId)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.SkillAssigned(name, SlotLabel(bar, slot)));
            _log.Info($"[Hotbar] Belegt (nach 2 Frames): Leiste {bar + 1} Slot {slot} = action {actionId} '{name}'");
        }
        else
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.AssignFailedNoChange);
            _log.Warning($"[Hotbar] Set+WriteSavedSlot ohne Wirkung (nach 2 Frames): bar={bar} slot={slot} soll action {actionId} " +
                         $"'{name}', ist {(module == null ? "Modul null" : DescribeSlotRaw(module, bar, slot))}");
        }

        // The menu stays open on the key list after a placement (user choice
        // 2026-09-05), so the player can fill the next key. Queued behind the
        // verdict - an interrupting line here would cut off the very result the
        // player is waiting for.
        if (_menuStep == SkillMenuStep.PickSlot)
            _tolk.Speak(AccessibilityStrings.SkillMenuBackAtSlots(_targets.Count));
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

    /// <summary>Spoken location of this action or item on any standard bar
    /// ("Taste 7" / "Leiste 2, Taste Strg+3"), or null when not placed.</summary>
    private unsafe string? FindSlotLocationFor(RaptureHotbarModule.HotbarSlotType type, uint id)
    {
        var module = RaptureHotbarModule.Instance();
        if (module == null) return null;
        for (var bar = 0; bar < StandardBarCount; bar++)
        for (var slot = 0; slot < SlotsToRead; slot++)
        {
            var s = module->GetSlotById((uint)bar, (uint)slot);
            if (s != null && s->CommandType == type && s->CommandId == id)
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
            _tolk.SpeakInterrupt(AccessibilityStrings.NotLoggedIn);
            return false;
        }

        var ps = PlayerState.Instance();
        var ui = UIState.Instance();
        if (ps == null || ui == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.PlayerDataNotReady);
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
            _tolk.SpeakInterrupt(AccessibilityStrings.NoSkillsFound);
            return false;
        }
        return true;
    }
}
