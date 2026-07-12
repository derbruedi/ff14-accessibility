using Dalamud.Game.ClientState.GamePad;
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

    private readonly Configuration      _config;
    private readonly TolkService        _tolk;
    private readonly BeaconService      _beacon;
    private readonly CueService         _cue;
    private readonly HotbarService      _hotbar;
    private readonly InventoryService   _inventoryReader;
    private readonly QuestMarkerService _questMarkers;
    private readonly PlacesService      _places;
    private readonly NavigationService  _navigation;
    private readonly AutoWalkService    _autoWalk;
    private readonly UIReaderService    _uiReader;
    private readonly ChatReaderService  _chatReader;
    private readonly CombatService      _combat;
    private readonly EmoteService       _emote;
    private readonly KeybindService     _keybinds;

    // Single source of truth for the version: log line AND spoken announcement
    // derive from these (they diverged once - spoken 4.1 vs logged 4.2).
    private const string PluginVersion    = "4.58";
    private const string PluginVersionTag = "Ansagen entrümpelt (SeString-Payloads, Quest-Reiter, Belohnungs-Zahlen, Doppel-Meldungen, Hotbar-Keybinds, Cross-Zone-Quest-Dedup); HP/MP auf Strg+H (+ Strg+L Level repariert); Auto-Lauf ohne Beacon-Piepen";

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
        TolkNative.Initialize(PluginInterface.AssemblyLocation.DirectoryName!);
        _tolk       = new TolkService(Log);
        _beacon       = new BeaconService(_config, _tolk, Log);
        _cue          = new CueService(_config, Log);
        _hotbar       = new HotbarService(DataManager, _tolk, Log);
        _inventoryReader = new InventoryService(GameInventory, DataManager, _tolk, Log);
        _questMarkers = new QuestMarkerService(ClientState, DataManager, Log);
        _places       = new PlacesService(DataManager, ClientState, Log);
        _navigation   = new NavigationService(ClientState, ObjectTable, TargetManager, _tolk, _beacon, _cue, _questMarkers, _places, DataManager, Log);
        _autoWalk   = new AutoWalkService(PluginInterface, ObjectTable, TargetManager, ClientState, _tolk, _config, Log);
        _uiReader   = new UIReaderService(AddonLifecycle, GameGui, _tolk, Log, ObjectTable, _inventoryReader);
        _chatReader = new ChatReaderService(ChatGui, _tolk, _config);
        _combat     = new CombatService(ObjectTable, TargetManager, DataManager, _tolk, _config, Log);
        _emote      = new EmoteService(DataManager, ClientState, _tolk, Log);
        _keybinds   = new KeybindService(_tolk, Log);

        RegisterCommands();
        Framework.Update += OnFrameworkUpdate;

        Log.Info($"FF14 Accessibility Plugin V{PluginVersion} [{PluginVersionTag}] geladen.");
        _tolk.Speak($"FF14 Accessibility Version {PluginVersion.Replace(".", " Punkt ")} bereit.");
    }

    private void RegisterCommands()
    {
        // /acc nav  → Richtung zum Ziel
        // /acc set  → Aktuelles Spielziel verfolgen
        // /acc near → Objekte in der Nähe
        // /acc stop → Sprache stoppen
        CommandManager.AddHandler("/acc", new CommandInfo(OnCommand)
        {
            HelpMessage = "FF14 Accessibility: nav, set, near, keys, stop, help"
        });
    }

    private void OnCommand(string command, string args)
    {
        var trimmed = args.Trim();

        // "dump" nimmt einen optionalen Addon-Namen — muss vor dem switch geprüft werden
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
                _tolk.SpeakInterrupt("Unbekannter Befehl. Tippe /acc help für Hilfe.");
                break;
        }
    }

    /// <summary>
    /// All plugin hotkeys from the config as (function, key label, VK code) —
    /// input for the keybind conflict check (/acc keys).
    /// </summary>
    private List<(string Function, string KeyName, int VirtualKey, bool Ctrl, bool Shift, bool Alt)> GetPluginKeys()
    {
        var keys = new List<(string, string, int, bool, bool, bool)>();
        foreach (var (function, keyName) in new[]
        {
            ("Hilfe",             _config.KeyHelp),
            ("Nächstes Objekt",   _config.KeyNextObject),
            ("Vorheriges Objekt", _config.KeyPrevObject),
            ("Kategorie",         _config.KeyCategory),
            ("Gehhilfe",          _config.KeyWalkGuide),
            ("Auto-Lauf",         _config.KeyAutoWalk),
            ("Menü vorlesen",  _config.KeyReadUI),
            ("Sprache stopp",  _config.KeySilence),
            ("Kampfstatus",    _config.KeyCombatStatus),
            ("UI-Dump",        _config.KeyDumpUI),
            ("Aktives Fenster", _config.KeyWhereAmI),
            ("Aktionsleiste",  _config.KeyReadHotbar),
            ("Inventar",       _config.KeyReadInventory),
            ("Gil",            _config.KeyReadGil),
            ("Stufe",          _config.KeyLevelExp),
            ("Emote weiter",   _config.KeyEmoteNext),
            ("Emote zurück",   _config.KeyEmotePrev),
            ("Emote ausführen", _config.KeyEmoteDo),
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
        // Nummernblock — TitleDCWorldMap Navigation (4=links, 6=rechts, 2=runter, 8=hoch)
        ["Numpad2"] = 0x62, ["Numpad4"] = 0x64,
        ["Numpad6"] = 0x66, ["Numpad8"] = 0x68,
        // Freie Tasten laut Keybind-Dump 2026-07-10 (N = einziger freier BARE
        // Buchstabe). H und L sind bare belegt (MENU_CRAFT / MENU_LINKSHELL),
        // aber mit Modifier frei - nur so (Strg+H, Strg+L) konfiguriert.
        ["N"] = 0x4E, ["H"] = 0x48, ["L"] = 0x4C, ["Numpad3"] = 0x63,
    };

    private readonly bool[] _keyWasDown     = new bool[256];
    private readonly bool[] _keyJustPressed = new bool[256];

    // Parsed key specs ("Strg+Umschalt+N" -> VK + modifiers); Vk=-1 caches invalid specs
    // so a broken config entry logs only once instead of every frame.
    private readonly Dictionary<string, (int Vk, bool Ctrl, bool Shift, bool Alt)> _keySpecCache =
        new(StringComparer.OrdinalIgnoreCase);

    // Edge detection once per frame and per VK: multiple bindings can share one
    // physical key (N, Strg+N, ...) and must all see the same "just pressed" edge.
    private void UpdateKeyEdges()
    {
        foreach (var vk in KeyNameToVK.Values)
        {
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
            _keybinds.DumpKeybinds(GetPluginKeys());
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
        if (IsJustPressed(_config.KeyWalkGuide))
        {
            // Walk guide and auto-walk are mutually exclusive - only one at a time.
            // (Only the walk guide sounds the beacon; auto-walk is silent.)
            _autoWalk.StopQuiet();
            _navigation.ToggleWalkGuide();
        }
        if (IsJustPressed(_config.KeyAutoWalk))
        {
            _navigation.StopWalkGuideQuiet();
            var quest = _navigation.SelectedQuestDestination;
            var place = _navigation.SelectedPlaceDestination;
            if (quest != null)
            {
                if (quest.TerritoryTypeId != ClientState.TerritoryType)
                {
                    // Fresh zone check at press time - the flag from selection
                    // time is stale after teleports/zone changes. Quest is in
                    // another zone: walk to the transition that leads there
                    // (route over the static map graph) instead of refusing.
                    var hop = _places.FindFirstHopToMap(quest.MapId, out _);
                    if (hop == null)
                    {
                        _tolk.SpeakInterrupt($"{quest.QuestName} ist in einem anderen Gebiet und ich finde keinen Übergang dorthin.");
                    }
                    else
                    {
                        var playerY = ObjectTable.LocalPlayer?.Position.Y ?? 0f;
                        var floor   = _autoWalk.ResolveFloorPoint(hop.Position with { Y = playerY });
                        if (floor == null)
                            // Transition: stop almost on the marker so the zone line triggers.
                            _tolk.SpeakInterrupt($"Kein begehbarer Punkt am {hop.Name} gefunden.");
                        else
                            _autoWalk.ToggleToPosition(floor.Value, hop.Name, _config.AutoWalkTransitionStopRange);
                    }
                }
                else
                {
                    // Snap the marker onto the walkable mesh so the tight stop
                    // range can be met (marker centres can sit off the mesh);
                    // fall back to the raw position if no floor is found.
                    var floor = _autoWalk.ResolveFloorPoint(quest.Position) ?? quest.Position;
                    var stop  = quest.Radius > 0f
                        ? MathF.Max(_config.AutoWalkPlaceStopRange, quest.Radius)
                        : _config.AutoWalkPlaceStopRange;
                    _autoWalk.ToggleToPosition(floor, quest.QuestName, stop);
                }
            }
            else if (place != null)
            {
                // Map markers are 2D - resolve the walkable height via the
                // navmesh first (player height as search origin).
                var playerY = ObjectTable.LocalPlayer?.Position.Y ?? 0f;
                var approx  = place.Position with { Y = playerY };
                var floor   = _autoWalk.ResolveFloorPoint(approx);
                if (floor == null)
                {
                    _tolk.SpeakInterrupt($"Kein begehbarer Punkt bei {place.Name} gefunden.");
                }
                else
                {
                    // Transitions get an extra-tight range so the player walks
                    // right into the zone line; other places stop on the spot.
                    var stop = place.IsZoneTransition
                        ? _config.AutoWalkTransitionStopRange
                        : _config.AutoWalkPlaceStopRange;
                    _autoWalk.ToggleToPosition(floor.Value, place.Name, stop);
                }
            }
            else
            {
                _autoWalk.Toggle();
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
        if (IsJustPressed("Escape"))                 _uiReader.HandleEscapeKey();
        // F5 — UI-Dump des aktuell aktiven Addons auf den Desktop schreiben
        // (kein Chat-Fenster nötig, funktioniert auch auf dem Titelbildschirm)
        if (IsJustPressed(_config.KeyDumpUI))        _uiReader.DumpFocusedAddon();
        // F2 — aktives Fenster ansagen + alle sichtbaren Fenster ins Log ([Win])
        if (IsJustPressed(_config.KeyWhereAmI))      _uiReader.AnnounceActiveWindow();

        _combat.Update();
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
        // AddonReceiveEvent-Hooks — deshalb hier abfangen und ForceDCMapRead() aufrufen.
        if (_uiReader.IsDCMapOpen)
        {
            var np2 = IsJustPressed("Numpad2");
            var np4 = IsJustPressed("Numpad4");
            var np6 = IsJustPressed("Numpad6");
            var np8 = IsJustPressed("Numpad8");
            if (np2 || np4 || np6 || np8)
                _uiReader.ForceDCMapRead();
        }

        // Menü-Navigation: nur wenn ein Menü aktiv ist
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

        // Controller D-Pad Links/Rechts: SelectYesno Ja↔Nein
        if (GamepadState.Pressed(GamepadButtons.DpadLeft)  > 0) _uiReader.NavigateGamepad(-1);
        if (GamepadState.Pressed(GamepadButtons.DpadRight) > 0) _uiReader.NavigateGamepad(+1);
    }

    private void AnnounceHelp()
    {
        _tolk.SpeakInterrupt(
            "Tasten: " +
            "N, nächstes Objekt ansagen und anvisieren. " +
            "Umschalt+N, vorheriges Objekt. " +
            "Strg+N, Kategorie wechseln. " +
            "Strg+Umschalt+N, Gehhilfe an oder aus. " +
            "Nummernblock 3, automatisch zum Ziel laufen. " +
            "F, zum Ziel hindrehen. W, laufen. " +
            "Strg+F1, diese Hilfe. " +
            "Strg+F2, aktives Fenster. " +
            "Strg+F10, Menü vorlesen. " +
            "Strg+F11, Sprache stoppen. " +
            "Strg+H, HP und MP ansagen. " +
            "Strg+F9, Aktionsleiste 1 vorlesen. " +
            "Befehle: " +
            "/acc nav, Richtung zum Ziel. " +
            "/acc set, Aktuelles Ziel verfolgen. " +
            "/acc clear, Ziel aufheben. " +
            "/acc near, Objekte in der Nähe. " +
            "/acc status, HP und MP ansagen. " +
            "/acc ui, Menü vorlesen. " +
            "/acc win, Aktives Fenster ansagen. " +
            "/acc keys, Spiel-Tastenbelegung auf den Desktop speichern. " +
            "/acc stop, Sprache stoppen."
        );
    }

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;
        CommandManager.RemoveHandler("/acc");
        _chatReader.Dispose();
        _uiReader.Dispose();
        _autoWalk.Dispose();
        _beacon.Dispose();
        _cue.Dispose();
        _tolk.Dispose();
    }
}
