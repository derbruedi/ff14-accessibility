using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using LuminaAction = Lumina.Excel.Sheets.Action;
using LuminaActionTransient = Lumina.Excel.Sheets.ActionTransient;
using LuminaBuddyAction = Lumina.Excel.Sheets.BuddyAction;
using LuminaClassJobCategory = Lumina.Excel.Sheets.ClassJobCategory;
using LuminaCraftAction = Lumina.Excel.Sheets.CraftAction;
using Lumina.Excel;
using Lumina.Text.ReadOnly;
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
/// can go on it (skills, items, quest items, general actions, mounts,
/// chocobo companion commands). The
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
    private readonly GameDescriptionService _descriptions;
    // Optional evaluator used by the diagnostic sheet probe.
    private readonly Dalamud.Plugin.Services.ISeStringEvaluator? _eval;
    private readonly CrossHotbarChangeTracker _crossHotbarChanges = new();
    private int? _unexpectedCrossHotbarId;
    private int? _activeCrossBar;

    /// <summary>Observe the game's chosen set; never read or consume controller buttons.</summary>
    public unsafe void UpdateCrossHotbar(IGameGui gameGui, bool announceChanges)
    {
        var ptr = _clientState.IsLoggedIn ? gameGui.GetAddonByName("_ActionCross") : default;
        var addon = (AddonActionCross*)(nint)ptr;
        if (addon == null)
        {
            _activeCrossBar = null;
            if (!_clientState.IsLoggedIn && IsSkillMenuOpen) CloseSkillMenu();
            _crossHotbarChanges.Update(null);
            _unexpectedCrossHotbarId = null;
            return;
        }
        // Verified against the installed ClientStructs: RaptureHotbarId changes
        // when cycling sets and references module Hotbars (cross bars 10..17).
        // WXHB has a separate source; do not infer it from trigger presses.
        var id = (int)addon->RaptureHotbarId;
        _activeCrossBar = CrossHotbarLayout.IsCrossBar(id) && !addon->DisplayPetBarCross ? id : null;
        if (id is < 10 or > 17)
        {
            if (_unexpectedCrossHotbarId != id)
            {
                _log.Warning($"[CrossHotbar] Cannot announce set: module ID={id}, pet={addon->DisplayPetBarCross}");
                _unexpectedCrossHotbarId = id;
                if (announceChanges && addon->IsVisible && !addon->DisplayPetBarCross)
                    _tolk.SpeakInterrupt(AccessibilityStrings.HotbarUnavailable);
            }
        }
        else _unexpectedCrossHotbarId = null;

        var number = _crossHotbarChanges.Update(announceChanges ? _activeCrossBar : null);
        if (number == null) return;
        _log.Info($"[CrossHotbar] Active set changed: module ID={id}, set={number.Value}");
        _tolk.SpeakInterrupt(AccessibilityStrings.CrossBarName(number.Value));
    }

    public HotbarService(IDataManager data, IClientState clientState, IFramework framework,
                         GearInfoService gearInfo, KeybindService keybinds, InventoryService inventory,
                         TolkService tolk, IPluginLog log, GameDescriptionService descriptions,
                         Dalamud.Plugin.Services.ISeStringEvaluator? eval = null)
    {
        _data = data;
        _clientState = clientState;
        _framework = framework;
        _gearInfo = gearInfo;
        _keybinds = keybinds;
        _inventory = inventory;
        _tolk = tolk;
        _log = log;
        _descriptions = descriptions;
        _eval = eval;
    }
    /// <summary>UI "Hotbar 1" is module index 0; its 12 keys are 1-9, 0, 11, 12.</summary>
    private const int MainHotbarIndex = 0;
    private const int SlotsToRead = 12;
    // RaptureHotbarModule.StandardHotbars = Hotbars[0..9] (ilspycmd
    // 2026-07-17); indices 10..17 are offered separately as controller sets.
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
        if (CrossHotbarLayout.IsCrossBar(bar))
            return AccessibilityStrings.CrossSlotLabel(bar - 9, slot);
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

    /// <summary>Read every button, including empty ones, on the normal active cross set.</summary>
    public unsafe void ReadCrossHotbar()
    {
        var module = RaptureHotbarModule.Instance();
        if (!_clientState.IsLoggedIn || module == null || _activeCrossBar is not int bar)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.CrossReadUnavailable);
            return;
        }
        var parts = new List<string> { AccessibilityStrings.CrossBarName(bar - 9),
            AccessibilityStrings.CrossBarState(module->IsHotbarShared((uint)bar)) };
        for (var slot = 0; slot < CrossHotbarLayout.SlotCount; slot++)
            parts.Add($"{SlotLabel(bar, slot)}, {CurrentSlotContent(bar, slot)}");
        _tolk.SpeakInterrupt(string.Join(". ", parts) + ".");
    }

    /// <summary>
    /// Human-readable name for a slot. Combat actions resolve through the
    /// Lumina Action sheet; crafting actions have their own slot type and
    /// CraftAction sheet. Everything else falls back to the game's own
    /// display string (PopUpHelp), then to a type+id label.
    /// </summary>
    private string ResolveName(RaptureHotbarModule.HotbarSlotType type, uint id, string popUpHelp)
    {
        if (type == RaptureHotbarModule.HotbarSlotType.Action &&
            _data.GetExcelSheet<LuminaAction>().TryGetRow(id, out var action))
        {
            var actionName = _descriptions.ActionName(action.RowId);
            if (!string.IsNullOrWhiteSpace(actionName))
                return actionName;
        }

        // Earlier builds wrote craft row ids as combat actions. Keep their name
        // readable while making clear that those old bindings need replacing.
        if (type == RaptureHotbarModule.HotbarSlotType.Action &&
            _data.GetExcelSheet<LuminaCraftAction>().TryGetRow(id, out var oldCraft))
        {
            var oldCraftName = _descriptions.CraftActionName(oldCraft.RowId);
            if (!string.IsNullOrWhiteSpace(oldCraftName))
                return AccessibilityStrings.CraftActionNeedsReassignment(oldCraftName);
        }

        // Craft actions must retain their own slot type so the game executes
        // them as crafting actions, not as combat actions with a craft row id.
        if (type == RaptureHotbarModule.HotbarSlotType.CraftAction &&
            _data.GetExcelSheet<LuminaCraftAction>().TryGetRow(id, out var craft))
        {
            var craftName = _descriptions.CraftActionName(craft.RowId);
            if (!string.IsNullOrWhiteSpace(craftName))
                return craftName;
        }

        // Quest items index the EventItem sheet, not Action - resolve them the
        // same deterministic way instead of relying on the display string.
        if (type == RaptureHotbarModule.HotbarSlotType.EventItem &&
            _data.GetExcelSheet<LuminaEventItem>().TryGetRow(id, out var eventItem))
        {
            var eventItemName = _descriptions.EventItemName(id);
            if (!string.IsNullOrWhiteSpace(eventItemName))
                return eventItemName;
        }

        // General actions (Absteigen, Sprint, Teleport ...) and mounts have
        // their own sheets too - same reasoning as above.
        if (type == RaptureHotbarModule.HotbarSlotType.GeneralAction &&
            _data.GetExcelSheet<LuminaGeneralAction>().TryGetRow(id, out var general))
        {
            var generalName = _descriptions.GeneralActionName(general.RowId);
            if (!string.IsNullOrWhiteSpace(generalName))
                return generalName;
        }

        if (type == RaptureHotbarModule.HotbarSlotType.Mount &&
            _data.GetExcelSheet<LuminaMount>().TryGetRow(id, out var mount))
        {
            var mountName = _descriptions.MountName(id);   // Mount has no Name column
            if (!string.IsNullOrWhiteSpace(mountName))
                return mountName;
        }

        if (type == RaptureHotbarModule.HotbarSlotType.BuddyAction &&
            _data.GetExcelSheet<LuminaBuddyAction>().TryGetRow(id, out var buddy))
        {
            var buddyName = _descriptions.BuddyActionName(buddy.RowId);
            if (!string.IsNullOrWhiteSpace(buddyName))
                return buddyName;
        }

        if (type == RaptureHotbarModule.HotbarSlotType.Item)
        {
            // Resolve the game's HQ/collectible encoding; preserve the command id itself.
            var itemName = _descriptions.ItemName(Dalamud.Utility.ItemUtil.GetBaseId(id).ItemId);
            if (!string.IsNullOrWhiteSpace(itemName)) return itemName;
        }
        if (type == RaptureHotbarModule.HotbarSlotType.Companion)
        {
            var companionName = _descriptions.CompanionName(id);
            if (!string.IsNullOrWhiteSpace(companionName)) return companionName;
        }
        if (type == RaptureHotbarModule.HotbarSlotType.PetAction)
        {
            var petName = _descriptions.PetActionName(id);
            if (!string.IsNullOrWhiteSpace(petName)) return petName;
        }
        if (type == RaptureHotbarModule.HotbarSlotType.Emote)
        {
            var emoteName = _descriptions.EmoteName(id);
            if (!string.IsNullOrWhiteSpace(emoteName)) return emoteName;
        }
        if (type == RaptureHotbarModule.HotbarSlotType.MainCommand &&
            _data.GetExcelSheet<Lumina.Excel.Sheets.MainCommand>().TryGetRow(id, out var command))
            return RussianGameText.Name(_data, command, x => x.Name);
        if (type == RaptureHotbarModule.HotbarSlotType.ClassJob &&
            _data.GetExcelSheet<Lumina.Excel.Sheets.ClassJob>().TryGetRow(id, out var job))
            return RussianGameText.Name(_data, job, x => x.Name);
        if (type == RaptureHotbarModule.HotbarSlotType.Recipe &&
            _data.GetExcelSheet<Lumina.Excel.Sheets.Recipe>().TryGetRow(id, out var recipe))
            return _inventory.ResolveItemName(recipe.ItemResult.RowId);

        // PopUpHelp is the game's own display text (name plus keybind hint);
        // use it for macros and other types without a supported name sheet.
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

    private enum SkillMenuStep { Closed, PickBar, PickSlot, PickEntry }
    private SkillMenuStep _menuStep = SkillMenuStep.Closed;
    private int _barChoice;
    private bool _keyboardDirect;
    private uint _menuJob;

    /// <summary>Which list the menu is browsing once a key has been picked.
    /// Numpad 4/6 steps through the sources (user choice 2026-08-06; quest
    /// items, general actions and mounts added 2026-08-09); the chosen key is
    /// the same target for all of them.</summary>
    private enum AssignSource { Skills, CraftActions, Items, QuestItems, GeneralActions, Mounts, BuddyActions }
    private AssignSource _menuSource = AssignSource.Skills;

    /// <summary>The order Numpad 4/6 steps through, and the order the fallback
    /// in <see cref="EnterFirstUsableSource"/> tries. Static so stepping does
    /// not allocate on every keypress. Craft actions sit directly behind the
    /// battle skills so a crafter reaches them with one keypress (user request
    /// 2026-09-26: her own craft tab, separate from the battle list).</summary>
    private static readonly AssignSource[] SourceOrder =
    {
        AssignSource.Skills, AssignSource.CraftActions, AssignSource.Items, AssignSource.QuestItems,
        AssignSource.GeneralActions, AssignSource.Mounts, AssignSource.BuddyActions,
    };

    private readonly List<(uint Id, string Name, byte Level)> _skills = new();
    private int _skillIndex = -1;

    /// <summary>All assignable actions of the current crafting class. The game
    /// splits them between CraftAction and Action; keep the actual hotbar slot type
    /// with each row so both kinds can be shown in one craft list.</summary>
    private readonly List<(uint Id, string Name, byte Level, RaptureHotbarModule.HotbarSlotType Type)> _craftSkills = new();
    private int _craftIndex = -1;

    // Description dwell for the skill assign list (same idea as ActionMenu in
    // UIReaderService): name+level is spoken interrupting on browse; the long
    // ActionTransient description is queued NON-interrupting after a short
    // dwell so NVDA finishes the name first and quick scanning never drowns
    // in tooltip text.
    private const double SkillDescDwellSeconds = 0.4;
    private uint _skillDescDwellId;
    private RaptureHotbarModule.HotbarSlotType _skillDescDwellType;
    private long _skillDescDwellTick;
    private bool _skillDescSpoken;

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
    // Companion commands (Heilen, Warten, Folgen, Freie Haltung, unlocked
    // Kunststücke …): BuddyAction sheet + HotbarSlotType.BuddyAction — same
    // surface a sighted player gets by dragging from Mitstreiter onto a bar.
    private readonly List<(uint Id, string Name)> _buddyActions = new();
    private int _buddyActionIndex = -1;
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
    public unsafe void ToggleSkillMenu(bool controllerMode = true)
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

        _barChoice = controllerMode
            ? (_activeCrossBar is int active ? active - CrossHotbarLayout.FirstBar : 0)
            : CrossHotbarLayout.SetCount;
        _keyboardDirect = false;
        var player = PlayerState.Instance();
        _menuJob = player == null ? 0u : player->CurrentClassJobId;
        _chosenBar = _chosenSlot = -1;
        // Reset the source only here, not per key: within one open session the
        // list the player last used stays selected, so filling several keys
        // from the bag does not walk back to the skills every time.
        _menuSource = AssignSource.Skills;
        _menuStep = SkillMenuStep.PickBar;
        _tolk.SpeakInterrupt(AccessibilityStrings.BarPickerOpened);
        AnnounceBarChoice(interrupt: false);
    }

    /// <summary>
    /// Opens the keyboard key list directly, without the crossbar picker.
    /// <para>
    /// A target key still has to be chosen before browsing its assignable entries.
    /// /acc crossbar opens the separate controller set picker.
    /// </para>
    /// </summary>
    public unsafe void OpenSkillMenuDirect()
    {
        if (_menuStep != SkillMenuStep.Closed) CloseSkillMenu();
        if (!_clientState.IsLoggedIn)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.NotLoggedIn);
            return;
        }
        if (RaptureHotbarModule.Instance() == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.HotbarUnavailable);
            return;
        }
        var player = PlayerState.Instance();
        if (player == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.PlayerDataNotReady);
            return;
        }

        _barChoice = CrossHotbarLayout.SetCount;
        _menuJob = player->CurrentClassJobId;
        _menuSource = AssignSource.Skills;
        _chosenBar = _chosenSlot = -1;
        BuildTargetList();
        if (_targets.Count == 0)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.SkillMenuNoTargets);
            return;
        }
        _targetIndex = 0;
        _keyboardDirect = true;
        _menuStep = SkillMenuStep.PickSlot;
        _tolk.SpeakInterrupt(AccessibilityStrings.SkillMenuSlotsOpened(_targets.Count));
        AnnounceTarget(interrupt: false);
    }

    private unsafe void AnnounceBarChoice(bool interrupt = true)
    {
        if (_barChoice == CrossHotbarLayout.SetCount)
        {
            Say(AccessibilityStrings.KeyboardBarChoice, interrupt);
            return;
        }
        var module = RaptureHotbarModule.Instance();
        if (module == null) { Say(AccessibilityStrings.HotbarUnavailable, interrupt); return; }
        Say($"{AccessibilityStrings.CrossBarName(_barChoice + 1)}, " +
            AccessibilityStrings.CrossBarState(module->IsHotbarShared((uint)(_barChoice + 10))), interrupt);
    }

    private void OpenChosenBar()
    {
        BuildTargetList();
        if (_targets.Count == 0) { _tolk.SpeakInterrupt(AccessibilityStrings.SkillMenuNoTargets); return; }
        _targetIndex = 0;
        _menuStep = SkillMenuStep.PickSlot;
        ClearSkillDescDwell();
        AnnounceBarChoice();
        _tolk.Speak(AccessibilityStrings.CrossButtonsOpened);
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
        if (_menuStep == SkillMenuStep.PickSlot)
        {
            if (_keyboardDirect) return;
            _barChoice = CrossHotbarLayout.MoveChoice(_barChoice, direction);
            OpenChosenBar();
            return;
        }
        if (_menuStep == SkillMenuStep.PickBar) { SkillMenuBrowse(direction); return; }
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
                // On crafting classes all assignable actions, including the few
                // Action-sheet buffs, live together in the craft source below.
                if (CrafterAbbreviations.Contains(_gearInfo.EnglishAbbreviation((byte)_menuJob),
                                                  StringComparer.OrdinalIgnoreCase)) return false;
                if (!BuildSkillList()) return false;
                _menuSource = AssignSource.Skills;
                if (_skillIndex < 0 || _skillIndex >= _skills.Count) _skillIndex = 0;
                Say(AccessibilityStrings.SkillMenuOpened(_skills.Count), interrupt);
                AnnounceSkill(interrupt: false);
                return true;

            case AssignSource.CraftActions:
                if (!BuildCraftActionList()) return false;
                _menuSource = AssignSource.CraftActions;
                if (_craftIndex < 0 || _craftIndex >= _craftSkills.Count) _craftIndex = 0;
                Say(AccessibilityStrings.CraftActionMenuOpened(_craftSkills.Count), interrupt);
                AnnounceCraftEntry(_craftIndex, interrupt: false);
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

            case AssignSource.BuddyActions:
                if (!BuildBuddyActionList()) return false;
                _menuSource = AssignSource.BuddyActions;
                if (_buddyActionIndex < 0 || _buddyActionIndex >= _buddyActions.Count)
                    _buddyActionIndex = 0;
                Say(AccessibilityStrings.BuddyActionMenuOpened(_buddyActions.Count), interrupt);
                AnnounceBuddyAction(interrupt: false);
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
    /// <summary>How many skills the last built skill list holds.</summary>
    internal int SkillCount => _skills.Count;

    /// <summary>
    /// Rebuilds the skill list for the current job and writes it, with the
    /// filter counters, into the plugin's own log. Returns how many skills
    /// survived, or 0 when the player state is not readable yet.
    /// <para>
    /// Called by <c>/acc diag</c>: the point is to capture the list at the
    /// moment the player asks, not whenever a menu was last opened.
    /// </para>
    /// </summary>
    internal int WriteSkillDiagnostics()
    {
        // Der Handwerks-Probe laeuft hier mit, nicht hinter einem eigenen Befehl:
        // ein Befehl im #if DEBUG-Block hat den Spieler schon einmal ins Leere
        // geschickt (build 6.08.48, "Unbekannter Befehl"). /acc diag ist nicht
        // gegated und schreibt in dieselbe Datei, die der Spieler schon kennt.
        ProbeCraftDescriptions();

        if (!EnsureSkillList()) return _skills.Count;
        return _skills.Count;
    }

    private bool BuildSkillList() => EnsureSkillList() && _skills.Count > 0;

    /// <summary>
    /// Probe, run from <c>/acc diag</c>: records the same complete crafting list
    /// that Ctrl+Numpad0 offers, including each entry's source and description.
    /// <para>
    /// Deliberately NOT behind <c>#if DEBUG</c> and deliberately not a command
    /// of its own: delivered zips are Release builds, so a DEBUG-gated command
    /// does not exist for the player - 6.08.48 shipped one and the player was
    /// told to run it. It is reachable only as part of <c>diag</c>.
    /// </para>
    /// </summary>
    internal unsafe string ProbeCraftDescriptions()
    {
        if (!_clientState.IsLoggedIn) return "Nicht eingeloggt (not logged in)";

        var player = PlayerState.Instance();
        if (player == null) return "Spielerdaten noch nicht lesbar (player state not readable)";

        var abbreviation = _gearInfo.EnglishAbbreviation((byte)player->CurrentClassJobId);
        var previousIndex = _craftIndex;
        var browsingCraft = _menuStep == SkillMenuStep.PickEntry && _menuSource == AssignSource.CraftActions;
        if (!BuildCraftActionList()) return "Keine Handwerksaktionen für den aktuellen Beruf.";
        if (browsingCraft) _craftIndex = Math.Clamp(previousIndex, 0, _craftSkills.Count - 1);

        PluginFileLog.Write($"[CraftProbe] Beruf {abbreviation}, Stufe {player->CurrentLevel}, " +
                            $"Einträge {_craftSkills.Count}, Probe {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        var withText = 0;
        foreach (var (id, name, level, type) in _craftSkills)
        {
            var description = ResolveActionDescription(id, type);
            if (!string.IsNullOrWhiteSpace(description)) withText++;
            var evaluated = type == RaptureHotbarModule.HotbarSlotType.CraftAction &&
                            _data.GetExcelSheet<LuminaCraftAction>().TryGetRow(id, out var craft)
                ? ReadEvaluated(craft.Description)
                : string.Empty;
            PluginFileLog.Write($"[CraftProbe] {id} \"{name}\" Stufe {level}, Typ {type}, " +
                                $"Beschreibung len={description.Length} \"{Shorten(description)}\", " +
                                $"ausgewertet \"{Shorten(evaluated)}\"");
        }

        var summary = $"Fertig: {_craftSkills.Count} Handwerksaktionen geprueft, {withText} mit Text.";
        PluginFileLog.Write($"[CraftProbe] {summary}");
        return summary;
    }

    private string ReadEvaluated(ReadOnlySeString value)
    {
        try { return _eval?.Evaluate(value).ExtractText() ?? value.ExtractText(); }
        catch (Exception ex)
        {
            _log.Warning($"[CraftProbe] SeString-Auswertung fehlgeschlagen: {ex.Message}");
            return value.ExtractText();
        }
    }

    private static string Shorten(string s) =>
        s.Length <= 90 ? s : s[..90] + "...";

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
    /// Rebuilds companion commands and Kunststücke from the BuddyAction sheet —
    /// Heilen, Warten, Folgen, Haltungen, and unlocked active skills. Slot type
    /// is <c>HotbarSlotType.BuddyAction</c> (ClientStructs). Unlock via
    /// <c>UnlockLink</c> like general actions. Sorted by sheet <c>Sort</c> then
    /// name so the browse order matches Mitstreiter more than A–Z alone.
    /// </summary>
    private unsafe bool BuildBuddyActionList()
    {
        if (!_clientState.IsLoggedIn) return false;

        var ui = UIState.Instance();
        if (ui == null) return false;

        _buddyActions.Clear();
        var withSort = new List<(byte Sort, uint Id, string Name)>();
        foreach (var row in _data.GetExcelSheet<LuminaBuddyAction>())
        {
            if (row.RowId == 0) continue;
            var name = row.Name.ExtractText();
            if (string.IsNullOrWhiteSpace(name)) continue;

            var unlock = row.UnlockLink;
            if (unlock != 0 && !ui->IsUnlockLinkUnlockedOrQuestCompleted(unlock)) continue;

            withSort.Add((row.Sort, row.RowId, name));
        }

        withSort.Sort((a, b) =>
        {
            var bySort = a.Sort.CompareTo(b.Sort);
            return bySort != 0
                ? bySort
                : string.Compare(a.Name, b.Name, StringComparison.CurrentCulture);
        });

        foreach (var e in withSort)
            _buddyActions.Add((e.Id, e.Name));

        _buddyActionIndex = _buddyActions.Count > 0 ? 0 : -1;
        _log.Info($"[Hotbar] Chocobo-Kommandos: {_buddyActions.Count} " +
                  $"({string.Join(", ", _buddyActions.Take(10).Select(a => $"{a.Name}#{a.Id}"))})");
        return _buddyActions.Count > 0;
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
        _keyboardDirect = false;
        _chosenBar = _chosenSlot = -1;
        ClearSkillDescDwell();
        _tolk.SpeakInterrupt(AccessibilityStrings.SkillMenuClosed);
    }

    /// <summary>
    /// Queues a skill tooltip description once the browser has dwelled on the
    /// same Action. Used for the skill list (PickEntry) and for the key list
    /// (PickSlot) when the key already holds a combat action — both speak the
    /// name interrupting first; the description follows non-interrupting.
    /// </summary>
    public void UpdateSkillDescDwell()
    {
        if (_menuStep == SkillMenuStep.PickSlot)
        {
            // Key list: dwell id was armed in AnnounceTarget for Action slots.
        }
        else if (_menuStep != SkillMenuStep.PickEntry ||
                 (_menuSource != AssignSource.Skills && _menuSource != AssignSource.CraftActions))
        {
            ClearSkillDescDwell();
            return;
        }

        if (_skillDescDwellId == 0 || _skillDescSpoken) return;

        var elapsed = (double)(Stopwatch.GetTimestamp() - _skillDescDwellTick) / Stopwatch.Frequency;
        if (elapsed < SkillDescDwellSeconds) return;

        _skillDescSpoken = true;
        var desc = ResolveActionDescription(_skillDescDwellId, _skillDescDwellType);
        if (string.IsNullOrEmpty(desc)) return;
        // Non-interrupting: follows "Taste 1, Ruin, 1 von 36" / skill browse line.
        _tolk.Speak(AccessibilityStrings.ItemDescription(desc));
    }

    private void ClearSkillDescDwell()
    {
        _skillDescDwellId = 0;
        _skillDescDwellType = RaptureHotbarModule.HotbarSlotType.Empty;
        _skillDescSpoken = false;
    }

    /// <summary>
    /// Numpad 5 in the entry step: repeat the current entry and append its
    /// description, interrupting.
    /// <para>
    /// Why a key and not just the dwell: the dwell line is spoken
    /// non-interrupting, so any other announcement (a level-up, a chat line,
    /// the player pressing a browse key) drops it silently, and a description
    /// that arrives unasked is easy to miss. The key answers the question
    /// "what does this one do" the moment it is asked, which is what a player
    /// who cannot see the tooltip needs.
    /// </para>
    /// Reads immediately without changing the menu selection or the next confirm.
    /// </summary>
    public void SkillMenuRepeatEntry() => AnnounceEntryWithDescription();

    /// <summary>Speaks the current entry line again, then its description
    /// (interrupting, never the dwell). Says so plainly when the sheet has no
    /// text, so the silence is never mistaken for a stuck menu.</summary>
    private void AnnounceEntryWithDescription()
    {
        if (_menuStep != SkillMenuStep.PickEntry) return;

        var (id, name, level, count, index) = _menuSource switch
        {
            AssignSource.CraftActions => CraftEntryAt(_craftIndex),
            AssignSource.Skills => SkillEntryAt(_skillIndex),
            _ => (0u, (string?)null, (byte)0, 0, 0),
        };
        if (name == null) return;

        var type = SkillSlotType;
        var location = FindSlotLocationFor(type, id);
        var entry = AccessibilityStrings.SkillBrowseEntry(name, level, location, index, count);
        var desc = ResolveActionDescription(id, type);
        var description = string.IsNullOrEmpty(desc)
            ? AccessibilityStrings.SkillDescriptionMissing(name)
            : AccessibilityStrings.ItemDescription(desc);

        // One interrupting message keeps the name and description together.
        // Cancel the pending dwell so it cannot repeat the same tooltip later.
        ClearSkillDescDwell();
        _tolk.SpeakInterrupt($"{entry}. {description}");
    }

    private void ArmSkillDescDwell(uint actionId, RaptureHotbarModule.HotbarSlotType type)
    {
        _skillDescDwellId = actionId;
        _skillDescDwellType = type;
        _skillDescDwellTick = Stopwatch.GetTimestamp();
        _skillDescSpoken = false;
    }

    private RaptureHotbarModule.HotbarSlotType SkillSlotType =>
        _menuSource == AssignSource.CraftActions && _craftIndex >= 0 && _craftIndex < _craftSkills.Count
            ? _craftSkills[_craftIndex].Type
            : RaptureHotbarModule.HotbarSlotType.Action;

    /// <summary>The craft entry at <paramref name="index"/> in the shape the
    /// shared description announcer needs, or all-null when out of range.</summary>
    private (uint Id, string? Name, byte Level, int Count, int Index) CraftEntryAt(int index)
    {
        if (index < 0 || index >= _craftSkills.Count) return (0, null, 0, 0, 0);
        var (id, name, level, type) = _craftSkills[index];
        name = ResolveName(type, id, name);
        return (id, name, level, _craftSkills.Count, index + 1);
    }

    /// <summary>Same for the battle skill list.</summary>
    private (uint Id, string? Name, byte Level, int Count, int Index) SkillEntryAt(int index)
    {
        if (index < 0 || index >= _skills.Count) return (0, null, 0, 0, 0);
        var (id, name, level) = _skills[index];
        name = ResolveName(RaptureHotbarModule.HotbarSlotType.Action, id, name);
        return (id, name, level, _skills.Count, index + 1);
    }

    /// <summary>Flattened tooltip text for an assignable entry, or empty when
    /// the sheet carries none.
    /// <para>
    /// Battle skills and craft actions use different slot types and sheets, so
    /// a craft id must never be looked up in <c>ActionTransient</c>. Craft actions live in
    /// <c>CraftAction</c> only — reading ActionTransient there is why the craft
    /// list stayed silent on every entry (reported 2026-09-26: «у тебя нету
    /// описаний этих умений вообще»). Goldmasters' actions do carry real text in
    /// that sheet (Basic Synthesis "Increases progress. Efficiency: 120% …"), so
    /// the lookup is worth doing rather than special-casing.
    /// </para>
    /// </summary>
    private string ResolveActionDescription(uint actionId, RaptureHotbarModule.HotbarSlotType type)
        => FlattenDescription(type == RaptureHotbarModule.HotbarSlotType.CraftAction
            ? _descriptions.CraftAction(actionId)
            : _descriptions.Action(actionId));

    /// <summary>Collapses line breaks and runs of spaces for spoken output.</summary>
    private static string FlattenDescription(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var flat = s.Replace('\r', ' ').Replace('\n', ' ');
        while (flat.Contains("  ", StringComparison.Ordinal)) flat = flat.Replace("  ", " ", StringComparison.Ordinal);
        return flat.Trim();
    }

    /// <summary>Numpad 8 / 2: browse the current step's list (wraps).</summary>
    public void SkillMenuBrowse(int direction)
    {
        switch (_menuStep)
        {
            case SkillMenuStep.PickBar:
                _barChoice = CrossHotbarLayout.MoveChoice(_barChoice, direction);
                AnnounceBarChoice();
                break;
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
            case SkillMenuStep.PickEntry when _menuSource == AssignSource.BuddyActions:
                if (_buddyActions.Count == 0) return;
                _buddyActionIndex = ((_buddyActionIndex + direction) % _buddyActions.Count
                                     + _buddyActions.Count) % _buddyActions.Count;
                AnnounceBuddyAction();
                break;
            case SkillMenuStep.PickEntry when _menuSource == AssignSource.Items:
                if (_items.Count == 0) return;
                _itemIndex = ((_itemIndex + direction) % _items.Count + _items.Count) % _items.Count;
                AnnounceItem();
                break;
            case SkillMenuStep.PickEntry when _menuSource == AssignSource.CraftActions:
                if (_craftSkills.Count == 0) return;
                _craftIndex = ((_craftIndex + direction) % _craftSkills.Count + _craftSkills.Count) % _craftSkills.Count;
                AnnounceCraftEntry(_craftIndex);
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
    public unsafe void SkillMenuConfirm()
    {
        var player = PlayerState.Instance();
        if (!_clientState.IsLoggedIn || player == null || _menuJob == 0 || player->CurrentClassJobId != _menuJob)
        {
            CloseSkillMenu();
            _tolk.Speak(AccessibilityStrings.CrossJobChanged);
            return;
        }
        switch (_menuStep)
        {
            case SkillMenuStep.PickBar:
                OpenChosenBar();
                break;
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
                    AssignSource.BuddyActions => AssignEntryToSlot(_buddyActions, _buddyActionIndex,
                        _chosenBar, _chosenSlot, RaptureHotbarModule.HotbarSlotType.BuddyAction),
                    AssignSource.CraftActions => AssignCraftActionToSlot(_craftIndex, _chosenBar, _chosenSlot),
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
        else if (_menuStep == SkillMenuStep.PickSlot)
        {
            if (_keyboardDirect) { CloseSkillMenu(); return; }
            ClearSkillDescDwell();
            _menuStep = SkillMenuStep.PickBar;
            _tolk.SpeakInterrupt(AccessibilityStrings.BarPickerOpened);
            AnnounceBarChoice(interrupt: false);
        }
        else
        {
            CloseSkillMenu();
        }
    }

    /// <summary>Announces the current skill: name, level, where it already sits
    /// (if anywhere) and its position in the list. Arms the description dwell so
    /// the tooltip follows non-interrupting after a short pause.</summary>
    private void AnnounceSkill(bool interrupt = true)
    {
        AnnounceSkillEntry(_skills, _skillIndex, interrupt);
    }

    /// <summary>Shared browse announcement for the battle list and the craft
    /// list: both carry (id, name, level), while each uses its own slot type.
    /// The entry line, location lookup and description dwell share one path.</summary>
    private void AnnounceSkillEntry(List<(uint Id, string Name, byte Level)> list, int index, bool interrupt = true)
    {
        if (index < 0 || index >= list.Count) return;
        var (id, name, level) = list[index];
        // Lists retain game names and their original order. Resolve speech now,
        // so changing language while the menu is open cannot leave cached Russian.
        name = ResolveName(SkillSlotType, id, name);
        var location = FindSlotLocationFor(SkillSlotType, id);
        Say(AccessibilityStrings.SkillBrowseEntry(name, level, location, index + 1, list.Count), interrupt);
        ArmSkillDescDwell(id, SkillSlotType);
    }

    private void AnnounceCraftEntry(int index, bool interrupt = true)
    {
        if (index < 0 || index >= _craftSkills.Count) return;
        var (id, name, level, type) = _craftSkills[index];
        name = ResolveName(type, id, name);
        var location = FindSlotLocationFor(type, id);
        Say(AccessibilityStrings.SkillBrowseEntry(name, level, location, index + 1, _craftSkills.Count), interrupt);
        ArmSkillDescDwell(id, type);
    }

    /// <summary>Announces the current item: name, stack size, where it already
    /// sits (if anywhere) and its position in the list. The count matters for
    /// the decision - a potion with one left is a different choice.</summary>
    private void AnnounceItem(bool interrupt = true)
    {
        var item = _items[_itemIndex];
        var location = FindSlotLocationFor(RaptureHotbarModule.HotbarSlotType.Item, item.ItemId);
        Say(AccessibilityStrings.ItemBrowseEntry(
            _descriptions.ItemName(item.BaseItemId), item.Quantity, item.IsHq, location, _itemIndex + 1, _items.Count), interrupt);
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
            _descriptions.EventItemName(item.ItemId), item.Quantity, item.CastTime, location, _questItemIndex + 1, _questItems.Count), interrupt);
    }

    /// <summary>Announces the current general action: name, where it already
    /// sits and its position in the list.</summary>
    private void AnnounceGeneralAction(bool interrupt = true)
    {
        var (id, name) = _generalActions[_generalActionIndex];
        name = ResolveName(RaptureHotbarModule.HotbarSlotType.GeneralAction, id, name);
        var location = FindSlotLocationFor(RaptureHotbarModule.HotbarSlotType.GeneralAction, id);
        Say(AccessibilityStrings.PlainBrowseEntry(
            name, location, _generalActionIndex + 1, _generalActions.Count), interrupt);
    }

    /// <summary>Announces the current mount: name, where it already sits and its
    /// position in the list.</summary>
    private void AnnounceMount(bool interrupt = true)
    {
        var (id, name) = _mounts[_mountIndex];
        name = ResolveName(RaptureHotbarModule.HotbarSlotType.Mount, id, name);
        var location = FindSlotLocationFor(RaptureHotbarModule.HotbarSlotType.Mount, id);
        Say(AccessibilityStrings.PlainBrowseEntry(
            name, location, _mountIndex + 1, _mounts.Count), interrupt);
    }

    /// <summary>Announces the current companion command / Kunststück.</summary>
    private void AnnounceBuddyAction(bool interrupt = true)
    {
        var (id, name) = _buddyActions[_buddyActionIndex];
        name = ResolveName(RaptureHotbarModule.HotbarSlotType.BuddyAction, id, name);
        var location = FindSlotLocationFor(RaptureHotbarModule.HotbarSlotType.BuddyAction, id);
        Say(AccessibilityStrings.PlainBrowseEntry(
            name, location, _buddyActionIndex + 1, _buddyActions.Count), interrupt);
    }

    /// <summary>Announces the current target key: its label, what is on it now,
    /// and its position in the list. Arms the description dwell when the key
    /// holds a combat action (same pattern as the skill list).</summary>
    private unsafe void AnnounceTarget(bool interrupt = true)
    {
        var (bar, slot) = _targets[_targetIndex];
        var content = CurrentSlotContent(bar, slot);
        Say(AccessibilityStrings.SkillMenuTargetEntry(
            SlotLabel(bar, slot), content, _targetIndex + 1, _targets.Count), interrupt);
        if (CrossHotbarLayout.IsCrossBar(bar))
            _log.Info($"[CrossHotbar] Browse set={bar - 9} slot={slot} code={CrossHotbarLayout.SlotCode(slot)} content='{content}'");

        var module = RaptureHotbarModule.Instance();
        var s = module == null ? null : module->GetSlotById((uint)bar, (uint)slot);
        if (s != null && s->CommandType is RaptureHotbarModule.HotbarSlotType.Action or
            RaptureHotbarModule.HotbarSlotType.CraftAction)
            ArmSkillDescDwell(s->CommandId, s->CommandType);
        else
            ClearSkillDescDwell();
    }

    /// <summary>Spoken name of whatever sits on a slot right now, or "empty".
    /// Read live on every announcement - a cached value would tell the player
    /// what the key held before their last placement.</summary>
    private unsafe string CurrentSlotContent(int bar, int slot)
    {
        var module = RaptureHotbarModule.Instance();
        var s = module == null ? null : module->GetSlotById((uint)bar, (uint)slot);
        if (s == null)
        {
            _log.Warning($"[Hotbar] Cannot read slot: bar={bar} slot={slot}");
            return AccessibilityStrings.HotbarUnavailable;
        }
        return s->CommandType == RaptureHotbarModule.HotbarSlotType.Empty
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
        if (_barChoice < CrossHotbarLayout.SetCount)
        {
            for (var slot = 0; slot < CrossHotbarLayout.SlotCount; slot++)
                _targets.Add((_barChoice + CrossHotbarLayout.FirstBar, slot));
            return;
        }
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
        name = ResolveName(RaptureHotbarModule.HotbarSlotType.Action, id, name);

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
        if (module->PvPHotbarsActive)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.CrossAssignPvpBlocked);
            return false;
        }
        if (!(bar is >= 0 and < StandardBarCount && slot is >= 0 and < SlotsToRead)
            && !CrossHotbarLayout.IsValidTarget(bar, slot))
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.AssignFailed);
            _log.Warning($"[Hotbar] Invalid assignment destination: bar={bar} slot={slot}");
            return false;
        }
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

            // Gegenprobe zur gespeicherten Zelle: HotbarSlot.Set fasst nur die
            // lebende Zelle an (ClientStructs-Doku zu Set: "will only affect the
            // current working hotbar slot"), WriteSavedSlot schreibt die
            // gespeicherte. Beide Seiten muessen die neue Belegung tragen - der
            // Spieler hat nur die lebende gesehen (Rueckmeldung "назначено").
            _log.Info($"[Hotbar] VOR Set: lebend {DescribeSlotRaw(module, bar, slot)} | " +
                      $"gespeichert {DescribeSavedSlotRaw(module, ps->CurrentClassJobId, bar, slot)}");

            live->Set(type, id);
            module->WriteSavedSlot(ps->CurrentClassJobId, (uint)bar, (uint)slot, live,
                ignoreSharedHotbars: false, isPvpSlot: false);

            _log.Info($"[Hotbar] NACH WriteSavedSlot: lebend {DescribeSlotRaw(module, bar, slot)} | " +
                      $"gespeichert {DescribeSavedSlotRaw(module, ps->CurrentClassJobId, bar, slot)}");
            // LoadSavedHotbar liest die GESPEICHERTE Seite in die lebende Leiste
            // zurueck. Wird sie direkt nach dem Schreiben aufgerufen, ueberschreibt
            // sie die eben gesetzte Aenderung wieder - gemessen im Probelauf
            // 2026-09-26: direkt nach dem Aufruf steht die Zelle leer/unveraendert,
            // waehrend die lebende Zelle unmittelbar vorher stimmte. Ohne den
            // Aufruf bleibt die Belegung nur bis zum naechsten Betreten des
            // Gebiets/Job-Wechsel sichtbar - die andere Haelfte desselben Problems.
            // Nicht hier geraten: wird zusammen mit der Bar-Nummer entschieden.
            // module->LoadSavedHotbar(ps->CurrentClassJobId, (uint)bar);
        }
        catch (Exception ex)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.AssignFailed);
            _log.Error(ex, $"[Hotbar] Set/WriteSavedSlot krachte: bar={bar} slot={slot} type={type} id={id} '{name}'");
            return false;
        }

        _log.Info($"[Hotbar] Direkt nach Set+WriteSavedSlot: {DescribeSlotRaw(module, bar, slot)}");
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

        if (!PlaceOnSlot(module, bar, slot, RaptureHotbarModule.HotbarSlotType.Item, item.ItemId, _descriptions.ItemName(item.BaseItemId)))
            return false;

        _framework.RunOnTick(
            () => VerifyAssignment(bar, slot, item.ItemId, _descriptions.ItemName(item.BaseItemId), RaptureHotbarModule.HotbarSlotType.Item),
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

        if (!PlaceOnSlot(module, bar, slot, RaptureHotbarModule.HotbarSlotType.EventItem, item.ItemId, _descriptions.EventItemName(item.ItemId)))
            return false;

        _framework.RunOnTick(
            () => VerifyAssignment(bar, slot, item.ItemId, _descriptions.EventItemName(item.ItemId), RaptureHotbarModule.HotbarSlotType.EventItem),
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
        name = ResolveName(type, id, name);
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

    // MERKE (offen, 2026-09-26): Der Verdacht steht, dass die Bar-Nummer hier
    // nicht die Modul-Bar ist. SlotLabel rechnet "Leiste 2" als Modul-Index 1;
    // die Spiel-Tastenbelegung heisst aber HOTBAR_2_* fuer die zweite Leiste.
    // Wenn die Wahl aus dem Menue (SlotKey-Namen "Strg+3" = HOTBAR_2_3) um eins
    // verschoben in GetSlotById landet, schreibt die Belegung in die falsche
    // Leiste - die Anzeige waere dann ein anderer Slot als der beschriebene.
    // Nicht geraten: der naechste Diag-Lauf mit Handwerks-Probe entscheidet es.

    /// <summary>Raw slot state for the probe log: command type/id plus the
    /// apparent (display-adjusted) action id.</summary>
    private unsafe string DescribeSlotRaw(RaptureHotbarModule* module, int bar, int slot)
    {
        var s = module->GetSlotById((uint)bar, (uint)slot);
        return s == null
            ? "Slot null"
            : $"type={s->CommandType} id={s->CommandId} apparent={s->ApparentActionId}";
    }

    /// <summary>
    /// The SAVED side of the same cell, read back through <c>SavedHotbars</c>
    /// directly (no load, no side effects). <c>DescribeSlotRaw</c> only ever
    /// shows the live bar; the player reported the placement as accepted while
    /// the action stayed dead any time the bar is reloaded, so the live bar
    /// alone cannot tell a failed save from a failed read-back (2026-09-26).
    /// <para>
    /// Indexing follows the ClientStructs doc on <c>_savedHotbars</c>: index 0
    /// is the shared PvE hotbar, otherwise the ClassJob id. A group holds ten
    /// normal hotbars of twelve slots - the same 12 the UI draws, which is also
    /// why slot 12 (index 11) is the last one the mod writes.
    /// </para>
    /// </summary>
    private unsafe string DescribeSavedSlotRaw(RaptureHotbarModule* module, uint jobId, int bar, int slot)
    {
        // The previous guard was never set: this probe has no verified saved
        // layout. Keep its honest result without unreachable native indexing.
        return "unbekannt";
    }

    /// <summary>All locations of this action or item on standard and cross bars,
    /// or null when not placed. Cross locations include the trigger and button.</summary>
    private unsafe string? FindSlotLocationFor(RaptureHotbarModule.HotbarSlotType type, uint id)
    {
        var module = RaptureHotbarModule.Instance();
        if (module == null) return null;
        var locations = new List<string>();
        // Cross destinations first when configuring a controller set; report all copies.
        var bars = _barChoice < CrossHotbarLayout.SetCount
            ? Enumerable.Range(10, 8).Concat(Enumerable.Range(0, 10)) : Enumerable.Range(0, 18);
        foreach (var bar in bars)
        for (var slot = 0; slot < (CrossHotbarLayout.IsCrossBar(bar) ? CrossHotbarLayout.SlotCount : SlotsToRead); slot++)
        {
            var s = module->GetSlotById((uint)bar, (uint)slot);
            if (s != null && s->CommandType == type && s->CommandId == id)
                locations.Add(SlotLabel(bar, slot));
        }
        return locations.Count == 0 ? null : string.Join("; ", locations);
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
        var skippedUnknownJob = 0;
        // Why the skill list can come out short is not answerable from the
        // outside: several filters drop rows, and a blind player only hears the
        // resulting count. The per-reason counters are therefore written to the
        // plugin's own log too, so a bug report can carry the reason instead of
        // the symptom (report 2026-09-26: two craft skills visible where the
        // game offers more).
        var skippedLevel = 0;
        var skippedIsPvP = 0;
        var skippedNoName = 0;
        var skippedNoCategory = 0;
        var skippedJobForeign = 0;
        foreach (var row in _data.GetExcelSheet<LuminaAction>())
        {
            if (row.RowId == 0) continue;
            if (row.IsPvP) { skippedIsPvP++; continue; }
            // ClassJobLevel 0 = not a learned-by-level player action (system rows).
            if (row.ClassJobLevel == 0 || row.ClassJobLevel > level) { skippedLevel++; continue; }
            if (row.ClassJobCategory.RowId == 0 || row.ClassJobCategory.ValueNullable is not { } cat) { skippedNoCategory++; continue; }
            // AllowsJob returns null when the boolean column behind the
            // ClassJobCategory row could not be resolved (ResolveJobColumn
            // logs it). For a craft class that null was read as "carries the
            // job" and the skill list silently emptied (report 2026-09-26:
            // no crafter skill assignable). Only a definite mismatch may
            // filter here - and only when the category names a different job.
            var jobAllowed = _gearInfo.AllowsJob(cat, jobId);
            if (jobAllowed == false && _gearInfo.NamesOnlyOtherJobs(cat, jobId)) { skippedJobForeign++; continue; }
            if (jobAllowed == null) skippedUnknownJob++;
            // Without this the list carried internal non-player rows that pass
            // the job filter (five 'Ausweichen' + 'Perfekter Hieb', log
            // 2026-07-17 12:01) - IsPlayerAction marks the real skill entries.
            if (!row.IsPlayerAction) { skippedNotPlayer++; continue; }

            var name = row.Name.ExtractText();
            if (string.IsNullOrWhiteSpace(name)) { skippedNoName++; continue; }

            // UnlockLink 0 = no quest gate; otherwise ask the game.
            var unlock = row.UnlockLink.RowId;
            if (unlock != 0 && !ui->IsUnlockLinkUnlockedOrQuestCompleted(unlock))
            {
                skippedLocked++;
                continue;
            }

            _skills.Add((row.RowId, name, row.ClassJobLevel));
        }

        // Author 6.08.35: some gatherer rows are not marked IsPlayerAction.
        // Only use the fallback for an empty gatherer list; crafting keeps its
        // separate mixed CraftAction/Action tab, including buffs and unlocks.
        if (_skills.Count == 0
            && _data.GetExcelSheet<Lumina.Excel.Sheets.ClassJob>().TryGetRow(jobId, out var jobRow)
            && jobRow.DohDolJobIndex >= 0 && jobRow.ClassJobCategory.RowId == 32)
            AppendGathererActions(jobId, level, ui, ref skippedLocked);

        // Craft actions deliberately NOT added here: they live in their own
        // CraftAction sheet (verified 2026-09-26 - "Basic Synthesis" is 100001,
        // the Action sheet has no such row at all) and the player wants them on
        // a tab of their own rather than mixed into her battle skills
        // (2026-09-26). See BuildCraftActionList.
        _skills.Sort((a, b) => a.Level != b.Level ? a.Level.CompareTo(b.Level) : a.Id.CompareTo(b.Id));
        var report = $"[Hotbar] Skill-Liste gebaut: Job {jobId}, Stufe {level}, {_skills.Count} Skills | " +
                     $"PvP {skippedIsPvP}, Stufe {skippedLevel}, ohne Kategorie {skippedNoCategory}, " +
                     $"unerlaubter Job {skippedJobForeign}, ohne Namen {skippedNoName}, " +
                     $"Nicht-Spieler-Action {skippedNotPlayer}, noch nicht freigeschaltet {skippedLocked}, " +
                     $"durchgelassen ohne aufloesbare Job-Spalte {skippedUnknownJob}.";
        _log.Info(report);
        PluginFileLog.Write(report);
        foreach (var (id, name, lvl) in _skills)
            PluginFileLog.Write($"[Hotbar]   Stufe {lvl}  {name}  (Aktion {id})");

        if (_skills.Count == 0)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.NoSkillsFound);
            return false;
        }
        return true;
    }

    /// <summary>Author gatherer fallback with the usual level/unlock gates.</summary>
    private unsafe void AppendGathererActions(byte jobId, uint level, UIState* ui, ref int skippedLocked)
    {
        foreach (var row in _data.GetExcelSheet<LuminaAction>())
        {
            if (row.RowId == 0 || row.IsPvP || row.ClassJob.RowId != jobId) continue;
            if (row.ClassJobLevel == 0 || row.ClassJobLevel > level) continue;
            var name = row.Name.ExtractText();
            if (string.IsNullOrWhiteSpace(name)) continue;
            var unlock = row.UnlockLink.RowId;
            if (unlock != 0 && !ui->IsUnlockLinkUnlockedOrQuestCompleted(unlock))
            {
                skippedLocked++;
                continue;
            }
            _skills.Add((row.RowId, name, row.ClassJobLevel));
        }
    }

    /// <summary>
    /// The eight crafting classes, in the game's own order. Used as the
    /// per-class filter for the <see cref="LuminaCraftAction"/> sheet, whose rows
    /// carry the class abbreviation as a plain property (unlike
    /// ClassJobCategory, which is a sheet of boolean columns).
    /// </summary>
    private static readonly string[] CrafterAbbreviations = { "CRP", "BSM", "ARM", "GSM", "LTW", "WVR", "ALC", "CUL" };

    /// <summary>
    /// Rebuilds the crafting tab's list from both game sheets up to the player's
    /// level. CraftAction holds most synthesis actions, while Action holds buffs
    /// such as Waste Not, Innovation and Manipulation.
    /// <para>CraftAction rows need CraftAction hotbar slots, while Action rows
    /// need Action slots. A single list preserves that distinction per entry.</para>
    /// <para>
    /// Rebuilt on every entry, like the bag list: the class level can change while
    /// the plugin is loaded, and a cached list would hide a freshly learned action.
    /// False when the player is not on a crafting class or the class has nothing at
    /// this level - the caller then skips the tab.
    /// </para>
    /// </summary>
    private unsafe bool BuildCraftActionList()
    {
        _craftSkills.Clear();
        _craftIndex = -1;
        if (!_clientState.IsLoggedIn) return false;

        var player = PlayerState.Instance();
        var ui = UIState.Instance();
        if (player == null || ui == null) return false;

        var jobId = (byte)player->CurrentClassJobId;
        var level = (uint)player->CurrentLevel;
        var abbreviation = _gearInfo.EnglishAbbreviation(jobId);
        if (!CrafterAbbreviations.Contains(abbreviation, StringComparer.OrdinalIgnoreCase)) return false;

        var sheet = _data.GetExcelSheet<LuminaCraftAction>();
        if (sheet == null)
        {
            _log.Warning("[Hotbar] CraftAction-Sheet nicht ladbar - Handwerks-Aktionen fehlen.");
            return false;
        }

        // Which ClassJobCategory row stands for the current crafter. The sheet
        // names the category through this row, not through the abbreviation -
        // resolving it that way keeps the eight classes honest if the game ever
        // renumbers (same approach as GearInfoService for ClassJobCategory).
        var categoryId = CraftCategoryFor(abbreviation);
        if (categoryId == 0)
        {
            _log.Warning($"[Hotbar] Keine ClassJobCategory-Zeile für {abbreviation} - Handwerksliste leer.");
            return false;
        }

        foreach (var row in sheet)
        {
            if (row.RowId == 0) continue;

            var name = row.Name.ExtractText();
            if (string.IsNullOrWhiteSpace(name)) continue;

            // The condition that decides whether the CURRENT class really
            // learns this action is the category, not the per-class column.
            //
            // Measured on the datamined CSV 2026-09-26: the CRP..CUL columns
            // are not a per-class flag - in the generic block (category 0) every
            // row carries the same quest id for all eight classes, which is what
            // produced 258 entries, four names repeating over and over. The
            // category is the honest filter: category 9 holds exactly the 29
            // carpenter actions (Basic Synthesis 100001 level 1, Basic Touch 5,
            // Master's Mend 7, Observe 13, Standard Touch 18 ...). The old
            // ClassColumn test is gone with it - it accepted the generic block
            // and so contradicted the level gate below.
            if (row.ClassJobCategory.RowId != categoryId) continue;

            if (row.ClassJobLevel == 0 || row.ClassJobLevel > level) continue;
            // Daring Touch is an automatic upgrade of Hasty Touch and the game
            // explicitly marks it as not assignable (ClassJob is zero).
            if (row.ClassJob.RowId != jobId) continue;
            var quest = row.QuestRequirement.RowId;
            if (quest != 0 && !ui->IsUnlockLinkUnlockedOrQuestCompleted(quest)) continue;

            _craftSkills.Add((row.RowId, name, row.ClassJobLevel,
                RaptureHotbarModule.HotbarSlotType.CraftAction));
        }

        foreach (var row in _data.GetExcelSheet<LuminaAction>())
        {
            if (row.ClassJobCategory.RowId != categoryId || !row.IsPlayerAction || row.IsPvP) continue;
            if (row.ClassJobLevel == 0 || row.ClassJobLevel > level) continue;
            var name = row.Name.ExtractText();
            if (string.IsNullOrWhiteSpace(name)) continue;
            var unlock = row.UnlockLink.RowId;
            if (unlock != 0 && !ui->IsUnlockLinkUnlockedOrQuestCompleted(unlock)) continue;

            _craftSkills.Add((row.RowId, name, row.ClassJobLevel,
                RaptureHotbarModule.HotbarSlotType.Action));
        }

        _craftSkills.Sort((a, b) => a.Level != b.Level ? a.Level.CompareTo(b.Level)
                                                       : a.Id.CompareTo(b.Id));
        _craftIndex = _craftSkills.Count > 0 ? 0 : -1;
        _log.Info($"[Hotbar] Handwerksliste für {abbreviation} (Stufe {level}): {_craftSkills.Count} Aktionen.");
        return _craftSkills.Count > 0;
    }

    /// <summary>Places the selected crafting entry with the slot type supplied
    /// by its source sheet. The battle list has its own index and path.</summary>
    private unsafe bool AssignCraftActionToSlot(int index, int bar, int slot)
    {
        if (index < 0 || index >= _craftSkills.Count)
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

        var (id, name, _, type) = _craftSkills[index];
        name = ResolveName(type, id, name);

        _log.Info($"[Hotbar] Belegen (Handwerk): {SlotLabel(bar, slot)} <- action {id} '{name}'. " +
                  $"Vorher: {DescribeSlotRaw(module, bar, slot)}, LeisteGeteilt={module->IsHotbarShared((uint)bar)}");

        if (!PlaceOnSlot(module, bar, slot, type, id, name))
            return false;

        // Same 2-frame read-back verdict as a battle skill: only announce what
        // the slot really holds afterwards.
        _framework.RunOnTick(() => VerifyAssignment(bar, slot, id, name,
            type), delayTicks: 2);
        return true;
    }

    /// <summary>
    /// The ClassJobCategory row id that stands for the crafter
    /// <paramref name="abbreviation"/> (CRP, GSM …), or 0 when it cannot be
    /// resolved. Resolved by NAME through the ClassJob sheet - the same route
    /// <see cref="GearInfoService"/> takes: the category sheet carries the job
    /// abbreviation as its own name ("GSM"), which is what makes this survive a
    /// renumbering of the category rows.
    /// </summary>
    private uint CraftCategoryFor(string abbreviation)
    {
        // EnglishAbbreviation is compared below; category names must use the same language.
        var sheet = _data.GetExcelSheet<LuminaClassJobCategory>(Dalamud.Game.ClientLanguage.English);
        if (sheet == null) return 0;

        foreach (var row in sheet)
        {
            if (row.RowId == 0) continue;
            if (string.Equals(row.Name.ExtractText().Trim(), abbreviation,
                              StringComparison.OrdinalIgnoreCase))
                return row.RowId;
        }

        return 0;
    }
}
