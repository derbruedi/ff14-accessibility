using System.Numerics;
using Dalamud.Game.ClientState.GamePad;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FF14Accessibility.Native;
using FF14Accessibility.Services;

namespace FF14Accessibility;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] private IDalamudPluginInterface PluginInterface { get; init; } = null!;
    [PluginService] private ICommandManager         CommandManager  { get; init; } = null!;
    [PluginService] private IClientState            ClientState     { get; init; } = null!;
    [PluginService] private IObjectTable            ObjectTable     { get; init; } = null!;
    [PluginService] private IChatGui                ChatGui         { get; init; } = null!;
    [PluginService] private IGameGui                GameGui         { get; init; } = null!;
    [PluginService] private IAddonLifecycle         AddonLifecycle  { get; init; } = null!;
    [PluginService] private IPluginLog              Log             { get; init; } = null!;
    [PluginService] private IKeyState               KeyState        { get; init; } = null!;
    [PluginService] private IFramework              Framework       { get; init; } = null!;
    [PluginService] private IGamepadState           GamepadState    { get; init; } = null!;
    [PluginService] private ITargetManager          TargetManager   { get; init; } = null!;
    [PluginService] private IDataManager            DataManager     { get; init; } = null!;
    [PluginService] private IGameInventory          GameInventory   { get; init; } = null!;
    [PluginService] private IToastGui               ToastGui        { get; init; } = null!;
    [PluginService] private IGameInteropProvider    Interop         { get; init; } = null!;

    private readonly Configuration      _config;
    private readonly TolkService        _tolk;
    private readonly BeaconService      _beacon;
    private readonly CueService         _cue;
    private readonly HotbarService      _hotbar;
    private readonly InventoryService   _inventoryReader;
    private readonly EquipmentService   _equipment;
    private readonly GearInfoService    _gearInfo;
    private readonly QuestMarkerService _questMarkers;
    private readonly PlacesService      _places;
    private readonly BestiaryService    _bestiary;
    private readonly RouteService       _routes;
    private readonly NavigationService  _navigation;
    private readonly AutoWalkService    _autoWalk;
    private readonly UIReaderService    _uiReader;
    private readonly ChatReaderService  _chatReader;
    private readonly MessageHistoryService _history;
    private readonly ToastService       _toasts;
    private readonly CombatService      _combat;
    private readonly VitalsService      _vitals;
    private readonly EmoteService       _emote;
    private readonly KeybindService     _keybinds;
    private readonly DalamudPluginsService _dalamudPlugins;
    private readonly TooltipService _tooltips;

    // Single source of truth for the version: log line AND spoken announcement
    // derive from these (they diverged once - spoken 4.1 vs logged 4.2).
    private const string PluginVersion    = "5.29";
    private const string PluginVersionTag = "Absturz-Fix Online-Fenster";

    public Plugin()
    {
        _config     = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        if (_config.Version < 2)
        {
            // V4.21: the old defaults F1-F12 all collide with the game's own
            // targeting keys (live keybind dump 2026-07-10) - move to free keys.
            _config.ResetKeysToDefaults();
            _config.Version = 2;
            PluginInterface.SavePluginConfig(_config);
        }
        if (_config.Version < 3)
        {
            // V4.56: move the level readout off Umschalt+F12 onto Strg+L (L=Level).
            // Targeted migration so other key customisations are preserved.
            if (_config.KeyLevelExp == "Umschalt+F12") _config.KeyLevelExp = "Strg+L";
            _config.Version = 3;
            PluginInterface.SavePluginConfig(_config);
        }
        if (_config.Version < 4)
        {
            // V4.58: move the HP/MP readout off Strg+F12 onto Strg+H (H=Health).
            // bare H is MENU_CRAFT in-game, Strg+H is free (live keybind dump).
            if (_config.KeyCombatStatus == "Strg+F12") _config.KeyCombatStatus = "Strg+H";
            _config.Version = 4;
            PluginInterface.SavePluginConfig(_config);
        }
        if (_config.Version < 5)
        {
            // V4.61: Strg+Alt+N is NVDA's own start-NVDA hotkey (user report) and
            // Alt+N is the game's beginner chat - category-back takes over
            // Strg+Umschalt+N (Umschalt = backwards, matching N/Umschalt+N), the
            // walk guide moves next to the auto-walk key (Numpad3 combos are free).
            // Order matters: free up the walk guide key before assigning it.
            if (_config.KeyWalkGuide == "Strg+Umschalt+N") _config.KeyWalkGuide = "Umschalt+Numpad3";
            if (_config.KeyCategoryPrev == "Strg+Alt+N") _config.KeyCategoryPrev = "Strg+Umschalt+N";
            _config.Version = 5;
            PluginInterface.SavePluginConfig(_config);
        }
        if (_config.Version < 6)
        {
            // V4.64: Umschalt+Numpad3 never reached the plugin - with NumLock on,
            // Windows turns Shift+numpad-digit into the NAVIGATION key (Numpad3
            // -> PageDown, shift artificially released), so the walk guide was
            // untriggerable since V4.61 (log 2026-07-16, see Configuration.cs).
            // Only Ctrl+numpad combos arrive reliably. Order matters: free up
            // Strg+Numpad3 (route preview) before handing it to the walk guide.
            if (_config.KeyRoutePreview == "Strg+Numpad3") _config.KeyRoutePreview = "Strg+Numpad5";
            if (_config.KeyWalkGuide == "Umschalt+Numpad3") _config.KeyWalkGuide = "Strg+Numpad3";
            _config.Version = 6;
            PluginInterface.SavePluginConfig(_config);
        }
        if (_config.Version < 7)
        {
            // V5.25: Strg+H opened the crafting log ON TOP of the HP readout.
            // Log-verified 2026-07-19 (19:19:00.837 'HP 100 Prozent' -> .850
            // RecipeNote opens and its announcement cuts the HP one off): the
            // game acts on the BASE key H (MENU_CRAFT) and ignores the Ctrl
            // modifier here. Only a key the game leaves unbound entirely is
            // safe, so the readout moves to Ctrl+Delete.
            if (_config.KeyCombatStatus == "Strg+H") _config.KeyCombatStatus = "Strg+Entf";
            _config.Version = 7;
            PluginInterface.SavePluginConfig(_config);
        }
        TolkNative.Initialize(PluginInterface.AssemblyLocation.DirectoryName!);
        _tolk       = new TolkService(Log);
        _beacon       = new BeaconService(_config, _tolk, Log);
        _cue          = new CueService(_config, Log);
        _gearInfo     = new GearInfoService(DataManager, Log);
        _keybinds     = new KeybindService(_tolk, Log);
        _hotbar       = new HotbarService(DataManager, ClientState, Framework, _gearInfo, _keybinds, _tolk, Log);
        _inventoryReader = new InventoryService(GameInventory, DataManager, _tolk, Log);
        _equipment    = new EquipmentService(GameInventory, DataManager, _gearInfo, _tolk, Log);
        _questMarkers = new QuestMarkerService(ClientState, DataManager, Log);
        _places       = new PlacesService(DataManager, ClientState, Log);
        _bestiary     = new BestiaryService(DataManager, Log);
        _routes       = new RouteService(PluginInterface, Log);
        _navigation   = new NavigationService(ClientState, ObjectTable, TargetManager, _tolk, _beacon, _cue, _questMarkers, _places, _routes, _config, DataManager, Log);
        _autoWalk   = new AutoWalkService(PluginInterface, ObjectTable, TargetManager, ClientState, _tolk, _config, _places, _routes, Log);
        _history    = new MessageHistoryService(_tolk);
        // Must exist before the UI reader: that one asks it for the labels of
        // icon buttons, which carry no text of their own.
        _tooltips   = new TooltipService(Interop, Log);
        _uiReader   = new UIReaderService(AddonLifecycle, GameGui, _tolk, Log, ObjectTable, _inventoryReader, _gearInfo, _bestiary, _history, _config, DataManager, _tooltips);
        _chatReader = new ChatReaderService(ChatGui, _tolk, _config, _history, ObjectTable, Log);
        _toasts     = new ToastService(ToastGui, _tolk, _config, Log);
        _combat     = new CombatService(ObjectTable, TargetManager, DataManager, _tolk, _config, Log);
        _vitals     = new VitalsService(ObjectTable, _config, Log);
        _emote      = new EmoteService(DataManager, ClientState, _tolk, Log);
        _dalamudPlugins = new DalamudPluginsService(PluginInterface, _tolk, Log);

        RegisterCommands();
        Framework.Update += OnFrameworkUpdate;

        Log.Info($"FF14 Accessibility Plugin V{PluginVersion} [{PluginVersionTag}] geladen.");
        _tolk.Speak($"FF14 Accessibility Version {PluginVersion.Replace(".", " Punkt ")} bereit.");
    }

    private void RegisterCommands()
    {
        // /acc nav  â†’ Richtung zum Ziel
        // /acc set  â†’ Aktuelles Spielziel verfolgen
        // /acc near â†’ Objekte in der NÃ¤he
        // /acc stop â†’ Sprache stoppen
        CommandManager.AddHandler("/acc", new CommandInfo(OnCommand)
        {
            HelpMessage = "FF14 Accessibility: nav, set, near, keys, stop, help"
        });
    }

    private void OnCommand(string command, string args)
    {
        var trimmed = args.Trim();

        // "dump" nimmt einen optionalen Addon-Namen â€” muss vor dem switch geprÃ¼ft werden
        if (trimmed.StartsWith("dump", StringComparison.OrdinalIgnoreCase))
        {
            var dumpArg = trimmed.Length > 4 ? trimmed[4..].Trim() : string.Empty;
            _uiReader.DumpAddon(dumpArg);
            return;
        }

        switch (trimmed.ToLower())
        {
            case "nav":
                _navigation.AnnounceDirection();
                break;
            case "set":
                _navigation.SetTargetFromGameTarget();
                break;
            case "clear":
                _navigation.ClearTarget();
                break;
            case "near":
                _navigation.AnnounceNearbyObjects(_config.NearbyDistance);
                break;
            case "stop":
                _tolk.Silence();
                break;
            case "status":
                _combat.AnnounceStatus();
                break;
            case "ui":
                _uiReader.ReadCurrentFocus();
                break;
            case "win":
                _uiReader.AnnounceActiveWindow();
                break;
            case "keys":
                _keybinds.DumpKeybinds(GetPluginKeys());
                break;
            case "help":
                AnnounceHelp();
                break;
            default:
                _tolk.SpeakInterrupt("Unbekannter Befehl. Tippe /acc help fÃ¼r Hilfe.");
                break;
        }
    }

    /// <summary>
    /// All plugin hotkeys from the config as (function, key label, VK code) â€”
    /// input for the keybind conflict check (/acc keys).
    /// </summary>
    private List<(string Function, string KeyName, int VirtualKey, bool Ctrl, bool Shift, bool Alt)> GetPluginKeys()
    {
        var keys = new List<(string, string, int, bool, bool, bool)>();
        foreach (var (function, keyName) in new[]
        {
            ("Hilfe",             _config.KeyHelp),
            ("NÃ¤chstes Objekt",   _config.KeyNextObject),
            ("Vorheriges Objekt", _config.KeyPrevObject),
            ("Kategorie",         _config.KeyCategory),
            ("Kategorie zurÃ¼ck",  _config.KeyCategoryPrev),
            ("Gehhilfe",          _config.KeyWalkGuide),
            ("Auto-Lauf",         _config.KeyAutoWalk),
            ("Routen-Vorschau",   _config.KeyRoutePreview),
            ("MenÃ¼ vorlesen",  _config.KeyReadUI),
            ("Sprache stopp",  _config.KeySilence),
            ("Kampfstatus",    _config.KeyCombatStatus),
            ("UI-Dump",        _config.KeyDumpUI),
            ("Aktives Fenster", _config.KeyWhereAmI),
            ("Aktionsleiste",  _config.KeyReadHotbar),
            ("Inventar",       _config.KeyReadInventory),
            ("Gil",            _config.KeyReadGil),
            ("Stufe",          _config.KeyLevelExp),
            ("Emote weiter",   _config.KeyEmoteNext),
            ("Emote zurÃ¼ck",   _config.KeyEmotePrev),
            ("Emote ausfÃ¼hren", _config.KeyEmoteDo),
            ("Bestiarium",     _config.KeyBestiary),
            ("Benachrichtigung", _config.KeyNotification),
            ("AusrÃ¼stung",     _config.KeyReadEquipment),
            ("Beste AusrÃ¼stung", _config.KeyEquipBest),
            ("ZufÃ¤lliges Aussehen", _config.KeyRandomLook),
            ("Skill zurÃ¼ck",   _config.KeySkillPrev),
            ("Skill weiter",   _config.KeySkillNext),
            ("Skill-Ziel-Taste", _config.KeySkillSlot),
            ("Skill belegen",  _config.KeySkillAssign),
            ("Skill-Ziel-Leiste", _config.KeySkillBar),
            ("Nachlese Kategorie zurÃ¼ck", _config.KeyChatCatPrev),
            ("Nachlese Kategorie vor",    _config.KeyChatCatNext),
            ("Nachlese Ã¤lter", _config.KeyChatReadOlder),
            ("Nachlese neuer", _config.KeyChatReadNewer),
            ("Plugin-Liste weiter",  _config.KeyPluginsNext),
            ("Plugin-Liste zurÃ¼ck",  _config.KeyPluginsPrev),
            ("Plugin-Einstellungen", _config.KeyPluginsConfig),
        })
        {
            var parsed = ParseKeySpec(keyName);
            if (parsed.Vk >= 0)
                keys.Add((function, keyName, parsed.Vk, parsed.Ctrl, parsed.Shift, parsed.Alt));
        }
        return keys;
    }

    private static readonly Dictionary<string, int> KeyNameToVK = new(StringComparer.OrdinalIgnoreCase)
    {
        ["F1"]  = 0x70, ["F2"]  = 0x71, ["F3"]  = 0x72, ["F4"]  = 0x73,
        ["F5"]  = 0x74, ["F6"]  = 0x75, ["F7"]  = 0x76, ["F8"]  = 0x77,
        ["F9"]  = 0x78, ["F10"] = 0x79, ["F11"] = 0x7A, ["F12"] = 0x7B,
        ["Escape"] = 0x1B,
        ["Up"]     = 0x26, ["Down"]   = 0x28,
        ["Left"]   = 0x25, ["Right"]  = 0x27,
        ["Return"] = 0x0D,
        // Nummernblock â€” TitleDCWorldMap Navigation (4=links, 6=rechts, 2=runter, 8=hoch)
        ["Numpad2"] = 0x62, ["Numpad4"] = 0x64,
        ["Numpad6"] = 0x66, ["Numpad8"] = 0x68,
        // Freie Tasten laut Keybind-Dump 2026-07-10 (N = einziger freier BARE
        // Buchstabe). H und L sind bare belegt (MENU_CRAFT / MENU_LINKSHELL),
        // aber mit Modifier frei - nur so (Strg+H, Strg+L) konfiguriert.
        ["N"] = 0x4E, ["H"] = 0x48, ["L"] = 0x4C, ["Numpad3"] = 0x63, ["Numpad5"] = 0x65,
        // Nachlese-Browser (V4.90): Komma/Punkt sind im Spiel nicht belegt
        // (Keybind-Dump 2026-07-17). VK_OEM_COMMA=0xBC, VK_OEM_PERIOD=0xBE.
        // Gueltigkeit prueft UpdateKeyEdges via IKeyState.IsVirtualKeyValid.
        [","] = 0xBC, ["."] = 0xBE,
        // V5.25: Entf ist im Keybind-Dump NIRGENDS belegt - anders als H, wo
        // das Spiel trotz Strg-Modifier MENU_CRAFT ausloeste. VK_DELETE=0x2E.
        ["Entf"] = 0x2E,
    };

    private readonly bool[] _keyWasDown     = new bool[256];
    private readonly bool[] _keyJustPressed = new bool[256];

    // Parsed key specs ("Strg+Umschalt+N" -> VK + modifiers); Vk=-1 caches invalid specs
    // so a broken config entry logs only once instead of every frame.
    private readonly Dictionary<string, (int Vk, bool Ctrl, bool Shift, bool Alt)> _keySpecCache =
        new(StringComparer.OrdinalIgnoreCase);

    // Edge detection once per frame and per VK: multiple bindings can share one
    // physical key (N, Strg+N, ...) and must all see the same "just pressed" edge.
    private readonly HashSet<int> _warnedInvalidVk = new();

    private void UpdateKeyEdges()
    {
        foreach (var vk in KeyNameToVK.Values)
        {
            // Dalamud's IKeyState only tracks keys the game itself indexes;
            // reading an unsupported VK throws. Guard so a key the game does
            // not track (verify comma/period at runtime) never crashes the
            // frame - it just stays unpressed, logged once for diagnosis.
            if (!KeyState.IsVirtualKeyValid(vk))
            {
                if (_warnedInvalidVk.Add(vk))
                    Log.Warning($"Taste VK 0x{vk:X2} wird von Dalamud/dem Spiel nicht getrackt - Belegung bleibt wirkungslos.");
                _keyJustPressed[vk] = false;
                _keyWasDown[vk] = false;
                continue;
            }
            var down = KeyState[(Dalamud.Game.ClientState.Keys.VirtualKey)vk];
            _keyJustPressed[vk] = down && !_keyWasDown[vk];
            _keyWasDown[vk] = down;
        }
    }

    private (int Vk, bool Ctrl, bool Shift, bool Alt) ParseKeySpec(string keySpec)
    {
        if (_keySpecCache.TryGetValue(keySpec, out var cached)) return cached;

        var parsed = (Vk: -1, Ctrl: false, Shift: false, Alt: false);
        var parts = keySpec.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var valid = parts.Length > 0;
        for (var i = 0; valid && i < parts.Length - 1; i++)
        {
            switch (parts[i].ToLowerInvariant())
            {
                case "strg" or "ctrl":      parsed.Ctrl  = true; break;
                case "umschalt" or "shift": parsed.Shift = true; break;
                case "alt":                 parsed.Alt   = true; break;
                default:                    valid        = false; break;
            }
        }
        if (valid && KeyNameToVK.TryGetValue(parts[^1], out var vk))
            parsed.Vk = vk;
        else
            Log.Warning($"Unbekannte Tastenangabe in der Konfiguration: '{keySpec}'");

        _keySpecCache[keySpec] = parsed;
        return parsed;
    }

    private bool IsJustPressed(string keySpec)
    {
        var (vk, ctrl, shift, alt) = ParseKeySpec(keySpec);
        if (vk < 0 || !_keyJustPressed[vk]) return false;
        // Exact modifier match: bare "N" must NOT fire while Alt is held,
        // because the game binds Alt+N (Neulingschat) itself.
        return KeyState[Dalamud.Game.ClientState.Keys.VirtualKey.CONTROL] == ctrl
            && KeyState[Dalamud.Game.ClientState.Keys.VirtualKey.SHIFT]   == shift
            && KeyState[Dalamud.Game.ClientState.Keys.VirtualKey.MENU]    == alt;
    }

    // Keybind dump runs automatically once per session: the user cannot open
    // the chat yet, so /acc keys would be unreachable for them.
    private bool _keybindsDumped;

    private void OnFrameworkUpdate(IFramework framework)
    {
        UpdateKeyEdges();

        if (!_keybindsDumped && ClientState.IsLoggedIn && _keybinds.IsReady())
        {
            _keybindsDumped = true;
            // Silent: the spoken "Tastenbelegung gespeichert" at every login was
            // noise (user 2026-07-13); conflicts are still announced.
            _keybinds.DumpKeybinds(GetPluginKeys(), announce: false);
        }

        if (IsJustPressed(_config.KeyHelp))          _uiReader.AnnounceContextHelp();
        if (IsJustPressed(_config.KeyNextObject))    _navigation.CycleObject(+1);
        if (IsJustPressed(_config.KeyPrevObject))    _navigation.CycleObject(-1);
        if (IsJustPressed(_config.KeyCategory))
        {
            _navigation.NextCategory();
            // Probe: quest-tracker structure for the objective reader
            // (user wants quest DESCRIPTIONS announced; see UIReaderService)
            _uiReader.ProbeAddonTexts("_ToDoList");
        }
        if (IsJustPressed(_config.KeyCategoryPrev)) _navigation.PreviousCategory();
        if (IsJustPressed(_config.KeyWalkGuide))
        {
            // Walk guide and auto-walk are mutually exclusive - only one at a time.
            // (Only the walk guide sounds the beacon; auto-walk is silent.)
            _autoWalk.StopQuiet();
            if (_navigation.IsWalkGuideActive)
            {
                _navigation.ToggleWalkGuide(); // second press: off
            }
            else switch (TryResolveMarkerDestination(out var pos, out var name, out var stop))
            {
                // Marker destinations (quest objectives, map waypoints) work in
                // the walk guide too since V4.63 - manual walking was
                // game-target-only before.
                case MarkerResolve.Resolved: _navigation.StartWalkGuideToPosition(pos, name, stop); break;
                case MarkerResolve.None:     _navigation.ToggleWalkGuide();                         break;
                case MarkerResolve.Failed:   break; // reason already announced
            }
        }
        if (IsJustPressed(_config.KeyAutoWalk))
        {
            _navigation.StopWalkGuideQuiet();
            var bestiaryMonster = _uiReader.SelectedBestiaryMonster;
            if (bestiaryMonster != null)
            {
                // Bestiary open with a monster row focused: track it - walk to
                // the nearest live one, or tell the user where it lives.
                TrackBestiaryMonster(bestiaryMonster);
            }
            else switch (TryResolveMarkerDestination(out var pos, out var name, out var stop))
            {
                case MarkerResolve.Resolved: _autoWalk.ToggleToPosition(pos, name, stop); break;
                case MarkerResolve.None:     _autoWalk.Toggle();                          break;
                case MarkerResolve.Failed:   break; // reason already announced
            }
        }
        if (IsJustPressed(_config.KeyRoutePreview))
        {
            // Speak the route (compass segments) without walking - to the
            // selected marker destination, or to the current game target.
            switch (TryResolveMarkerDestination(out var pos, out var name, out _))
            {
                case MarkerResolve.Resolved: _navigation.PreviewRoute(pos, name); break;
                case MarkerResolve.None:     _navigation.PreviewRouteToTarget();  break;
                case MarkerResolve.Failed:   break; // reason already announced
            }
        }
        if (IsJustPressed(_config.KeyReadUI))        _uiReader.ReadCurrentFocus();
        if (IsJustPressed(_config.KeySilence))       _tolk.Silence();
        if (IsJustPressed(_config.KeyCombatStatus))  _combat.AnnounceStatus();
        if (IsJustPressed(_config.KeyReadHotbar))    _hotbar.ReadHotbar();
        if (IsJustPressed(_config.KeyReadInventory))
        {
            // In a hand-over (Request) window Strg+F3 reads the eligible items
            // from the grid; otherwise it reads the whole carried inventory.
            if (!_uiReader.TryAnnounceHandOver()) _inventoryReader.ReadInventory();
        }
        if (IsJustPressed(_config.KeyReadGil))       _inventoryReader.AnnounceGil();
        if (IsJustPressed(_config.KeyLevelExp))      _combat.AnnounceLevelExp();
        if (IsJustPressed(_config.KeyEmoteNext))     _emote.CycleNext();
        if (IsJustPressed(_config.KeyEmotePrev))     _emote.CyclePrev();
        if (IsJustPressed(_config.KeyEmoteDo))       _emote.ExecuteSelected();
        if (IsJustPressed(_config.KeyBestiary))      _uiReader.AnnounceBestiaryOverview();
        if (IsJustPressed(_config.KeyPluginsNext))   _dalamudPlugins.CycleNext();
        if (IsJustPressed(_config.KeyPluginsPrev))   _dalamudPlugins.CyclePrev();
        if (IsJustPressed(_config.KeyPluginsConfig)) _dalamudPlugins.OpenConfigOfSelected();
        if (IsJustPressed(_config.KeyNotification))  _uiReader.ActivateNotification();
        if (IsJustPressed(_config.KeyReadEquipment)) _equipment.ReadEquipment();
        if (IsJustPressed(_config.KeyEquipBest))     _equipment.EquipRecommended();
        if (IsJustPressed(_config.KeyRandomLook))    _uiReader.PressRandomAppearance();
        if (IsJustPressed(_config.KeySkillPrev))     _hotbar.CycleSkillPrev();
        if (IsJustPressed(_config.KeySkillNext))     _hotbar.CycleSkillNext();
        if (IsJustPressed(_config.KeySkillSlot))     _hotbar.CycleTargetSlot();
        if (IsJustPressed(_config.KeySkillAssign))   _hotbar.AssignSelectedSkill();
        if (IsJustPressed(_config.KeySkillBar))      _hotbar.CycleTargetBar();
        if (IsJustPressed(_config.KeyChatCatPrev))   _history.SwitchCategory(-1);
        if (IsJustPressed(_config.KeyChatCatNext))   _history.SwitchCategory(+1);
        if (IsJustPressed(_config.KeyChatReadOlder)) _history.ReadOlder();
        if (IsJustPressed(_config.KeyChatReadNewer)) _history.ReadNewer();
        if (IsJustPressed("Escape"))                 _uiReader.HandleEscapeKey();
        // F5 â€” UI-Dump des aktuell aktiven Addons auf den Desktop schreiben
        // (kein Chat-Fenster nÃ¶tig, funktioniert auch auf dem Titelbildschirm)
        if (IsJustPressed(_config.KeyDumpUI))        _uiReader.DumpFocusedAddon();
        // F2 â€” aktives Fenster ansagen + alle sichtbaren Fenster ins Log ([Win])
        if (IsJustPressed(_config.KeyWhereAmI))      _uiReader.AnnounceActiveWindow();

        _combat.Update();
        // HP/MP tones on every 10 % step (pan = fill level). Independent of
        // combat state on purpose: post-fight regeneration is exactly when the
        // bar refilling should be audible.
        _vitals.Update();
        _equipment.Update();
        // Always runs: drives the walk guide too, which must not die when
        // target-change announcements are switched off. During an auto-walk
        // target announcements are muted (soft-target churn while passing NPCs).
        _navigation.Update(_config.AnnounceTargetChanges && !_autoWalk.IsActive);
        _autoWalk.Update();
        // Global UI focus (AtkInputManager.FocusedNode): announces whatever
        // control the game itself considers keyboard-focused - dialogs,
        // options, everything. See UIReaderService.UpdateGlobalFocus.
        _uiReader.UpdateGlobalFocus();

        // DC-Auswahl: Nummernblock-Navigation (4=links, 6=rechts, 2=runter, 8=hoch)
        // Nummernblock-Tasten werden vom Spiel intern verarbeitet und feuern keine
        // AddonReceiveEvent-Hooks â€” deshalb hier abfangen und ForceDCMapRead() aufrufen.
        if (_uiReader.IsDCMapOpen)
        {
            var np2 = IsJustPressed("Numpad2");
            var np4 = IsJustPressed("Numpad4");
            var np6 = IsJustPressed("Numpad6");
            var np8 = IsJustPressed("Numpad8");
            if (np2 || np4 || np6 || np8)
                _uiReader.ForceDCMapRead();
        }

        // MenÃ¼-Navigation: nur wenn ein MenÃ¼ aktiv ist
        if (_uiReader.HasActiveMenu)
        {
            var up    = IsJustPressed("Up");
            var down  = IsJustPressed("Down");
            var left  = IsJustPressed("Left");
            var right = IsJustPressed("Right");
            // Probe: user pressed left/right in Ok/Cancel dialogs repeatedly
            // (their report 2026-07-11) and no Navigate line ever appeared -
            // this line settles whether IKeyState even SEES arrow keys while
            // a dialog is open (the game may consume them for UI navigation).
            if (up || down || left || right)
                Log.Info($"[Key] Pfeiltaste erkannt: hoch={up} runter={down} links={left} rechts={right}");
            if (up)    _uiReader.Navigate(-1, false);
            if (down)  _uiReader.Navigate(+1, false);
            if (left)  _uiReader.Navigate(-1, true);
            if (right) _uiReader.Navigate(+1, true);
        }

        if (IsJustPressed("Return")) _uiReader.HandleConfirmKey();

        // Controller D-Pad Links/Rechts: SelectYesno Jaâ†”Nein
        if (GamepadState.Pressed(GamepadButtons.DpadLeft)  > 0) _uiReader.NavigateGamepad(-1);
        if (GamepadState.Pressed(GamepadButtons.DpadRight) > 0) _uiReader.NavigateGamepad(+1);
    }

    private enum MarkerResolve
    {
        /// <summary>No marker destination selected - callers fall back to the game target.</summary>
        None,
        /// <summary>Walkable position resolved (out parameters are valid).</summary>
        Resolved,
        /// <summary>A marker is selected but unusable; the reason was announced.</summary>
        Failed,
    }

    /// <summary>
    /// Resolves the marker destination selected in the object browser (quest
    /// objective or map waypoint) into a walkable world position. Shared by
    /// auto-walk, walk guide and route preview so all three reach the same
    /// spot. Cross-zone quests resolve to the first transition on the route
    /// (fresh zone check at press time - the flag from selection time is stale
    /// after teleports); 2D map markers get their height from the navmesh.
    /// </summary>
    private MarkerResolve TryResolveMarkerDestination(out Vector3 position, out string name, out float stopRange)
    {
        position = default;
        name = string.Empty;
        stopRange = _config.AutoWalkPlaceStopRange;

        var quest = _navigation.SelectedQuestDestination;
        var place = _navigation.SelectedPlaceDestination;

        if (quest != null)
        {
            if (quest.TerritoryTypeId != ClientState.TerritoryType)
            {
                // Quest is in another zone: walk to the transition that leads
                // there (route over the static map graph) instead of refusing.
                var hop = _places.FindFirstHopToMap(quest.MapId, out _);
                if (hop == null)
                {
                    _tolk.SpeakInterrupt($"{quest.QuestName} ist in einem anderen Gebiet und ich finde keinen Ãœbergang dorthin.");
                    return MarkerResolve.Failed;
                }
                var playerY = ObjectTable.LocalPlayer?.Position.Y ?? 0f;
                var floor   = _autoWalk.ResolveFloorPoint(hop.Position with { Y = playerY });
                if (floor == null)
                {
                    _tolk.SpeakInterrupt($"Kein begehbarer Punkt am {hop.Name} gefunden.");
                    return MarkerResolve.Failed;
                }
                position = floor.Value;
                name = hop.Name;
                // Transition: stop almost on the marker so the zone line triggers.
                stopRange = _config.AutoWalkTransitionStopRange;
                return MarkerResolve.Resolved;
            }

            // Snap the marker onto the walkable mesh so the tight stop range
            // can be met (marker centres can sit off the mesh); fall back to
            // the raw position if no floor is found.
            position = _autoWalk.ResolveFloorPoint(quest.Position) ?? quest.Position;
            name = quest.QuestName;
            stopRange = quest.Radius > 0f
                ? MathF.Max(_config.AutoWalkPlaceStopRange, quest.Radius)
                : _config.AutoWalkPlaceStopRange;
            return MarkerResolve.Resolved;
        }

        if (place != null)
        {
            // Map markers are 2D - resolve the walkable height via the
            // navmesh first (player height as search origin).
            var playerY = ObjectTable.LocalPlayer?.Position.Y ?? 0f;
            var floor   = _autoWalk.ResolveFloorPoint(place.Position with { Y = playerY });
            if (floor == null)
            {
                _tolk.SpeakInterrupt($"Kein begehbarer Punkt bei {place.Name} gefunden.");
                return MarkerResolve.Failed;
            }
            position = floor.Value;
            name = place.Name;
            // Transitions get an extra-tight range so the player walks right
            // into the zone line; other places stop on the spot.
            stopRange = place.IsZoneTransition
                ? _config.AutoWalkTransitionStopRange
                : _config.AutoWalkPlaceStopRange;
            return MarkerResolve.Resolved;
        }

        return MarkerResolve.None;
    }

    /// <summary>
    /// Auto-walk key while a bestiary monster row is focused: targets and walks
    /// to the nearest live specimen, or announces its habitat when none is near.
    /// </summary>
    private void TrackBestiaryMonster(string monsterName)
    {
        if (_autoWalk.IsActive)
        {
            _autoWalk.Toggle(); // second press stops, like every other walk
            return;
        }

        var player = ObjectTable.LocalPlayer;
        if (player == null) return;

        IGameObject? nearest = null;
        var nearestDist = float.MaxValue;
        foreach (var obj in ObjectTable)
        {
            if (obj.ObjectKind != ObjectKind.BattleNpc) continue;
            if (!string.Equals(obj.Name.TextValue, monsterName, StringComparison.OrdinalIgnoreCase)) continue;
            if (obj is IBattleChara { CurrentHp: 0 }) continue; // dead ones don't count
            var dist = System.Numerics.Vector3.Distance(player.Position, obj.Position);
            if (dist < nearestDist) { nearest = obj; nearestDist = dist; }
        }

        if (nearest == null)
        {
            var habitat = _bestiary.GetHabitat(monsterName);
            _tolk.SpeakInterrupt(habitat != null
                ? $"Kein {monsterName} in der NÃ¤he. Lebt in {habitat}."
                : $"Kein {monsterName} in der NÃ¤he.");
            return;
        }

        // Target it first (fight follows the walk); the game may reject the
        // set (V4.24), so read back and warn instead of walking untargeted.
        TargetManager.Target = nearest;
        if (TargetManager.Target?.GameObjectId != nearest.GameObjectId)
            _tolk.SpeakInterrupt("Achtung, nicht anvisiert.");
        _autoWalk.Toggle();
    }

    private void AnnounceHelp()
    {
        _tolk.SpeakInterrupt(
            "Tasten: " +
            "N, nÃ¤chstes Objekt ansagen und anvisieren. " +
            "Umschalt+N, vorheriges Objekt. " +
            "Strg+N, Kategorie vorwÃ¤rts. " +
            "Strg+Umschalt+N, Kategorie zurÃ¼ck. " +
            "Strg+Nummernblock 3, Gehhilfe an oder aus, folgt dem Wegenetz um Hindernisse. " +
            "Nummernblock 3, automatisch zum Ziel laufen. " +
            "Strg+Nummernblock 5, Weg zum Ziel ansagen ohne zu laufen. " +
            "F, zum Ziel hindrehen. W, laufen. " +
            "Strg+F1, diese Hilfe. " +
            "Strg+F2, aktives Fenster. " +
            "Strg+F10, MenÃ¼ vorlesen. " +
            "Strg+F11, Sprache stoppen. " +
            "Strg+Entfernen, HP und MP ansagen. " +
            "Strg+F9, gewÃ¤hlte Aktionsleiste vorlesen. " +
            "Strg+F6, angelegte AusrÃ¼stung vorlesen. " +
            "Strg+F7, empfohlene AusrÃ¼stung anlegen. " +
            "Strg+F8, zufÃ¤lliges Aussehen in der Charaktererschaffung. " +
            "Umschalt+F7 und F8, Skill-Browser zurÃ¼ck und vor. " +
            "Umschalt+F11, Ziel-Leiste wechseln, 1 bis 10. " +
            "Umschalt+F9, Ziel-Taste der Leiste wÃ¤hlen. " +
            "Umschalt+F10, gewÃ¤hlten Skill auf die Ziel-Taste legen. " +
            "Befehle: " +
            "/acc nav, Richtung zum Ziel. " +
            "/acc set, Aktuelles Ziel verfolgen. " +
            "/acc clear, Ziel aufheben. " +
            "/acc near, Objekte in der NÃ¤he. " +
            "/acc status, HP und MP ansagen. " +
            "/acc ui, MenÃ¼ vorlesen. " +
            "/acc win, Aktives Fenster ansagen. " +
            "/acc keys, Spiel-Tastenbelegung auf den Desktop speichern. " +
            "/acc stop, Sprache stoppen."
        );
    }

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;
        CommandManager.RemoveHandler("/acc");
        _tooltips.Dispose();
        _toasts.Dispose();
        _chatReader.Dispose();
        _uiReader.Dispose();
        _autoWalk.Dispose();
        _beacon.Dispose();
        _cue.Dispose();
        _vitals.Dispose();
        _tolk.Dispose();
    }
}
