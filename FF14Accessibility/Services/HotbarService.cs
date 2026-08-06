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

    // â”€â”€ Skill assignment menu: a modal, keyboard-only way to put a learned
    //    action on a hotbar key. Sighted players drag from "Actions & Traits";
    //    there is no keyboard path in the game, so the plugin walks the player
    //    through it: browse learned skills, pick one, browse the assignable
    //    keys, confirm. The assignment uses the game's own
    //    RaptureHotbarModule.SetAndSaveSlot (the drag-and-drop path), so it
    //    persists like a manual change. Driven from the numpad while open;
    //    Plugin.cs swallows those keys so the character does not move.

    private enum SkillMenuStep { Closed, PickSkill, PickTarget }
    private SkillMenuStep _menuStep = SkillMenuStep.Closed;

    /// <summary>Which list the menu is browsing. Numpad 4/6 toggles it (user
    /// choice 2026-08-06); the target-key step works the same for both.</summary>
    private enum AssignSource { Skills, Items }
    private AssignSource _menuSource = AssignSource.Skills;

    private readonly List<(uint Id, string Name, byte Level)> _skills = new();
    private int _skillIndex = -1;

    // Carried usable items, rebuilt every time the item list is entered - the
    // inventory changes constantly (potions get drunk), and a cached list would
    // offer the player something that is no longer there.
    private readonly List<InventoryService.UsableItem> _items = new();
    private int _itemIndex = -1;
    // The list is rebuilt when job or level changes (level-ups add skills).
    private byte _skillsJobId;
    private uint _skillsLevel;

    // Flat list of assignable target keys (bar/slot pairs), rebuilt per skill.
    private readonly List<(int Bar, int Slot)> _targets = new();
    private int _targetIndex = -1;

    /// <summary>True while the modal skill menu is open, so Plugin.cs routes
    /// the numpad keys here and swallows them from the game.</summary>
    public bool IsSkillMenuOpen => _menuStep != SkillMenuStep.Closed;

    /// <summary>Opens the skill menu, or closes it when already open (the same
    /// key toggles). On open the learned-skill list is (re)built and the first
    /// skill announced.</summary>
    public void ToggleSkillMenu()
    {
        if (_menuStep != SkillMenuStep.Closed) { CloseSkillMenu(); return; }
        _menuSource = AssignSource.Skills;   // always open on the familiar list
        if (!EnsureSkillList()) return;      // speaks the reason on failure
        if (_skillIndex < 0 || _skillIndex >= _skills.Count) _skillIndex = 0;
        _menuStep = SkillMenuStep.PickSkill;
        _tolk.SpeakInterrupt(AccessibilityStrings.SkillMenuOpened(_skills.Count));
        AnnounceSkill();
    }

    /// <summary>
    /// Numpad 4 / 6: switches between the skill list and the carried-item list
    /// while browsing. Only meaningful in the browse step - during target-key
    /// selection the source is already decided, so the keys are ignored there
    /// (the announcement would otherwise contradict what is about to be placed).
    /// </summary>
    public void SkillMenuSwitchSource()
    {
        if (_menuStep != SkillMenuStep.PickSkill) return;

        if (_menuSource == AssignSource.Skills)
        {
            if (!EnsureItemList()) return;   // speaks the reason on failure
            _menuSource = AssignSource.Items;
            if (_itemIndex < 0 || _itemIndex >= _items.Count) _itemIndex = 0;
            _tolk.SpeakInterrupt(AccessibilityStrings.ItemMenuOpened(_items.Count));
            AnnounceItem();
        }
        else
        {
            if (!EnsureSkillList()) return;
            _menuSource = AssignSource.Skills;
            if (_skillIndex < 0 || _skillIndex >= _skills.Count) _skillIndex = 0;
            _tolk.SpeakInterrupt(AccessibilityStrings.SkillMenuOpened(_skills.Count));
            AnnounceSkill();
        }
    }

    /// <summary>
    /// Rebuilds the carried usable items. Always rebuilt (never cached): the bag
    /// changes while playing, and offering a potion that was already drunk would
    /// assign a slot the player cannot use. Announces and returns false when the
    /// player carries nothing usable.
    /// </summary>
    private bool EnsureItemList()
    {
        if (!_clientState.IsLoggedIn)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.NotLoggedIn);
            return false;
        }

        _items.Clear();
        _items.AddRange(_inventory.CollectUsableItems());
        _itemIndex = _items.Count > 0 ? 0 : -1;

        if (_items.Count == 0)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.NoUsableItems);
            return false;
        }
        return true;
    }

    /// <summary>Closes the menu and says so.</summary>
    public void CloseSkillMenu()
    {
        _menuStep = SkillMenuStep.Closed;
        _tolk.SpeakInterrupt(AccessibilityStrings.SkillMenuClosed);
    }

    /// <summary>Numpad 8 / 2: browse the current step's list (wraps).</summary>
    public void SkillMenuBrowse(int direction)
    {
        switch (_menuStep)
        {
            case SkillMenuStep.PickSkill when _menuSource == AssignSource.Items:
                if (_items.Count == 0) return;
                _itemIndex = ((_itemIndex + direction) % _items.Count + _items.Count) % _items.Count;
                AnnounceItem();
                break;
            case SkillMenuStep.PickSkill:
                if (_skills.Count == 0) return;
                _skillIndex = ((_skillIndex + direction) % _skills.Count + _skills.Count) % _skills.Count;
                AnnounceSkill();
                break;
            case SkillMenuStep.PickTarget:
                if (_targets.Count == 0) return;
                _targetIndex = ((_targetIndex + direction) % _targets.Count + _targets.Count) % _targets.Count;
                AnnounceTarget();
                break;
        }
    }

    /// <summary>Numpad 0: confirm the current step. Skill step -> build the
    /// target list and advance; target step -> assign and close.</summary>
    public void SkillMenuConfirm()
    {
        switch (_menuStep)
        {
            case SkillMenuStep.PickSkill:
                var chosen = _menuSource == AssignSource.Items
                    ? (_itemIndex >= 0 && _itemIndex < _items.Count ? _items[_itemIndex].Name : null)
                    : (_skillIndex >= 0 && _skillIndex < _skills.Count ? _skills[_skillIndex].Name : null);
                if (chosen == null) return;

                BuildTargetList();
                if (_targets.Count == 0)
                {
                    _tolk.SpeakInterrupt(AccessibilityStrings.SkillMenuNoTargets);
                    return;
                }
                _targetIndex = 0;
                _menuStep = SkillMenuStep.PickTarget;
                _tolk.SpeakInterrupt(AccessibilityStrings.SkillMenuPickTarget(chosen, _targets.Count));
                AnnounceTarget();
                break;
            case SkillMenuStep.PickTarget:
                if (_targetIndex < 0 || _targetIndex >= _targets.Count) return;
                var (bar, slot) = _targets[_targetIndex];
                _menuStep = SkillMenuStep.Closed;    // leave the menu; assign speaks the result
                if (_menuSource == AssignSource.Items)
                    AssignItemToSlot(_itemIndex, bar, slot);
                else
                    AssignSkillToSlot(_skillIndex, bar, slot);
                break;
        }
    }

    /// <summary>Numpad comma: step back to skill selection, or close from
    /// there.</summary>
    public void SkillMenuBack()
    {
        if (_menuStep == SkillMenuStep.PickTarget)
        {
            _menuStep = SkillMenuStep.PickSkill;
            if (_menuSource == AssignSource.Items)
            {
                _tolk.SpeakInterrupt(AccessibilityStrings.ItemMenuOpened(_items.Count));
                AnnounceItem();
            }
            else
            {
                _tolk.SpeakInterrupt(AccessibilityStrings.SkillMenuOpened(_skills.Count));
                AnnounceSkill();
            }
        }
        else
        {
            CloseSkillMenu();
        }
    }

    /// <summary>Announces the current skill: name, level, where it already sits
    /// (if anywhere) and its position in the list.</summary>
    private void AnnounceSkill()
    {
        var (id, name, level) = _skills[_skillIndex];
        var location = FindSlotLocationFor(RaptureHotbarModule.HotbarSlotType.Action, id);
        _tolk.SpeakInterrupt(AccessibilityStrings.SkillBrowseEntry(name, level, location, _skillIndex + 1, _skills.Count));
    }

    /// <summary>Announces the current item: name, stack size, where it already
    /// sits (if anywhere) and its position in the list. The count matters for
    /// the decision - a potion with one left is a different choice.</summary>
    private void AnnounceItem()
    {
        var item = _items[_itemIndex];
        var location = FindSlotLocationFor(RaptureHotbarModule.HotbarSlotType.Item, item.ItemId);
        _tolk.SpeakInterrupt(AccessibilityStrings.ItemBrowseEntry(
            item.Name, item.Quantity, item.IsHq, location, _itemIndex + 1, _items.Count));
    }

    /// <summary>Announces the current target key: its label, what is on it now,
    /// and its position in the list.</summary>
    private unsafe void AnnounceTarget()
    {
        var (bar, slot) = _targets[_targetIndex];
        var module = RaptureHotbarModule.Instance();
        var s = module == null ? null : module->GetSlotById((uint)bar, (uint)slot);
        var current = s == null || s->CommandType == RaptureHotbarModule.HotbarSlotType.Empty
            ? AccessibilityStrings.InputEmpty
            : ResolveName(s->CommandType, s->CommandId, s->PopUpHelp.ToString());
        _tolk.SpeakInterrupt(AccessibilityStrings.SkillMenuTargetEntry(
            SlotLabel(bar, slot), current, _targetIndex + 1, _targets.Count));
    }

    /// <summary>Builds the flat list of assignable target keys: bar 1's twelve
    /// slots always (keys 1-0, 11, 12), plus any slot on bars 2-10 that has a
    /// key bound - only those can actually fire the skill.</summary>
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
    /// </summary>
    private unsafe void AssignSkillToSlot(int skillIndex, int bar, int slot)
    {
        if (skillIndex < 0 || skillIndex >= _skills.Count)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.NoSkillSelected);
            return;
        }

        var module = RaptureHotbarModule.Instance();
        if (module == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.HotbarUnavailable);
            return;
        }

        var (id, name, _) = _skills[skillIndex];

        _log.Info($"[Hotbar] Belegen: {SlotLabel(bar, slot)} (Leiste {bar + 1} Slot {slot}) <- action {id} '{name}'. " +
                  $"Vorher: {DescribeSlotRaw(module, bar, slot)}, LeisteGeteilt={module->IsHotbarShared((uint)bar)}");

        if (!PlaceOnSlot(module, bar, slot, RaptureHotbarModule.HotbarSlotType.Action, id, name))
            return;

        // Verdict 2 frames later - announce only what the slot then really holds.
        _framework.RunOnTick(() => VerifyAssignment(bar, slot, id, name), delayTicks: 2);
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
    /// </summary>
    private unsafe void AssignItemToSlot(int itemIndex, int bar, int slot)
    {
        if (itemIndex < 0 || itemIndex >= _items.Count)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.NoSkillSelected);
            return;
        }

        var module = RaptureHotbarModule.Instance();
        if (module == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.HotbarUnavailable);
            return;
        }

        var item = _items[itemIndex];
        _log.Info($"[Hotbar] Belegen: {SlotLabel(bar, slot)} (Leiste {bar + 1} Slot {slot}) <- item {item.ItemId} " +
                  $"'{item.Name}' (Basis {item.BaseItemId}, hq={item.IsHq}, Anzahl {item.Quantity}). " +
                  $"Vorher: {DescribeSlotRaw(module, bar, slot)}");

        if (!PlaceOnSlot(module, bar, slot, RaptureHotbarModule.HotbarSlotType.Item, item.ItemId, item.Name))
            return;

        _framework.RunOnTick(
            () => VerifyAssignment(bar, slot, item.ItemId, item.Name, RaptureHotbarModule.HotbarSlotType.Item),
            delayTicks: 2);
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
