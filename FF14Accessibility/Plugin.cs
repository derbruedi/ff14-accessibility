using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.GamePad;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FF14Accessibility.Native;
using FF14Accessibility.Services;
using FFXIVClientStructs.FFXIV.Client.UI;

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
    private readonly CooldownService    _cooldown;
    private readonly HotbarService      _hotbar;
    private readonly InventoryService   _inventoryReader;
    private readonly LootRollService    _lootRolls;
    private readonly EquipmentService   _equipment;
    private readonly GearInfoService    _gearInfo;
    private readonly QuestMarkerService _questMarkers;
    private readonly PlacesService      _places;
    private readonly FishingService     _fishing;
    private readonly FateService        _fates;
    private readonly GatheringService   _gathering;
    private readonly BestiaryService    _bestiary;
    private readonly RouteService       _routes;
    private readonly ShopNpcService     _shops;
    private readonly ObjectNameService  _objectNames;
    private readonly ObjectMemoryService _objectMemory;
    private readonly NavigationService  _navigation;
    private readonly NavmeshCacheService _meshCache;
    private readonly ZoneExitService    _zoneExits;
    private readonly AutoWalkService    _autoWalk;
    private readonly UIReaderService    _uiReader;
    private readonly ChatReaderService  _chatReader;
    private readonly MessageHistoryService _history;
    private readonly ChatChannelService  _chatChannel;
    private readonly ToastService       _toasts;
    private readonly CombatService      _combat;
    private readonly AoeWarningService  _aoeWarn;
    private readonly VitalsService      _vitals;
    private readonly HeadingService     _heading;
    private readonly EmoteService       _emote;
    private readonly KeybindService     _keybinds;
    private readonly DalamudPluginsService _dalamudPlugins;
    private readonly TooltipService _tooltips;
    private readonly TripleTriadService _tripleTriad;

    // Single source of truth for the version: log line AND spoken announcement
    // derive from these (they diverged once - spoken 4.1 vs logged 4.2).
    private const string PluginVersion    = "5.76";
    private const string PluginVersionTag = "Zonenuebergaenge werden durchlaufen, Beute auswuerfeln, Auf- und Absteigen ueber die Leiste, Begleiter-Verzeichnis, tote Sammelpunkte raus";

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
        if (_config.Version < 8)
        {
            // V5.31: N-Familie freigeraeumt (N wird kuenftig anders gebraucht).
            // Objekt-Browser zieht auf die Bild-Tasten: Unterkategorien auf
            // Bild-auf/Bild-ab, Kategorien auf Strg+Bild-auf/-ab. Bare Bild-
            // auf/-ab ueberschneiden sich mit CAMERA_ZOOMIN/ZOOMOUT (Keybind-
            // Dump), der Zoom ist aber rein visuell und fuer blindes Spiel
            // folgenlos (User bestaetigt 2026-07-22); das Plugin verbraucht die
            // Taste nicht. Nur unveraenderte Standardwerte migrieren, damit eine
            // eigene Belegung nie ueberschrieben wird.
            if (_config.KeyNextObject   == "N")               _config.KeyNextObject   = "BildAb";
            if (_config.KeyPrevObject   == "Umschalt+N")      _config.KeyPrevObject   = "BildAuf";
            if (_config.KeyCategory     == "Strg+N")          _config.KeyCategory     = "Strg+BildAb";
            if (_config.KeyCategoryPrev == "Strg+Umschalt+N") _config.KeyCategoryPrev = "Strg+BildAuf";
            _config.Version = 8;
            PluginInterface.SavePluginConfig(_config);
        }
        if (_config.Version < 9)
        {
            // V5.48: bare "." now opens the mount notebook in-game (MENU_MOUNT),
            // colliding with the "read newer message" key. Move the chat-reread
            // pair onto Umschalt+BildAuf/-Ab (older=up, newer=down). Umschalt+Bild
            // is free both in-game (game binds only bare PRIOR/NEXT) and plugin-
            // side. Only migrate untouched defaults so custom bindings survive.
            if (_config.KeyChatReadOlder == ",") _config.KeyChatReadOlder = "Umschalt+BildAuf";
            if (_config.KeyChatReadNewer == ".") _config.KeyChatReadNewer = "Umschalt+BildAb";
            _config.Version = 9;
            PluginInterface.SavePluginConfig(_config);
        }
        if (_config.Version < 10)
        {
            // V5.49: Strg+, / Strg+. still triggered the mount notebook - the game
            // acts on the BASE key "." (MENU_MOUNT) and ignores the Ctrl modifier
            // (same trap as H/MENU_CRAFT in V5.25, user-confirmed in-game). Move the
            // category pair onto Strg+Umschalt+BildAuf/-Ab, keeping the whole
            // Nachlese/nav family on the Bild cluster (bare=objects, Strg=obj-category,
            // Umschalt=reread, Strg+Umschalt=chat-category). Only untouched defaults.
            if (_config.KeyChatCatPrev == "Strg+,") _config.KeyChatCatPrev = "Strg+Umschalt+BildAuf";
            if (_config.KeyChatCatNext == "Strg+.") _config.KeyChatCatNext = "Strg+Umschalt+BildAb";
            _config.Version = 10;
            PluginInterface.SavePluginConfig(_config);
        }
        if (_config.Version < 11)
        {
            // V5.50: user prefers the chat-category pair on Alt+BildAuf/-Ab (frees
            // the Strg+Umschalt chord). Alt+Bild is unbound in-game (Alt binds only
            // with letters for chat commands). Only migrate the V10 default so a
            // custom binding survives.
            if (_config.KeyChatCatPrev == "Strg+Umschalt+BildAuf") _config.KeyChatCatPrev = "Alt+BildAuf";
            if (_config.KeyChatCatNext == "Strg+Umschalt+BildAb")  _config.KeyChatCatNext = "Alt+BildAb";
            _config.Version = 11;
            PluginInterface.SavePluginConfig(_config);
        }
        // Language for all mod announcements (Auto = follow Windows). Must be set
        // before the first Speak below.
        Loc.Mode = _config.Language;

        TolkNative.Initialize(PluginInterface.AssemblyLocation.DirectoryName!);
        _tolk       = new TolkService(Log);
        _beacon       = new BeaconService(_config, _tolk, Log);
        _cue          = new CueService(_config, Log);
        _gearInfo     = new GearInfoService(DataManager, Log);
        _keybinds     = new KeybindService(_tolk, Log);
        // Inventory first: the hotbar menu reads the carried items from it.
        _inventoryReader = new InventoryService(GameInventory, DataManager, ClientState, _config, _tolk, Log);
        _hotbar       = new HotbarService(DataManager, ClientState, Framework, _gearInfo, _keybinds, _inventoryReader, _tolk, Log);
        _lootRolls    = new LootRollService(DataManager, ClientState, GameGui, _config, _tolk, Log);
        _equipment    = new EquipmentService(GameInventory, DataManager, _gearInfo, _tolk, Log);
        _questMarkers = new QuestMarkerService(ClientState, DataManager, Log);
        _places       = new PlacesService(DataManager, ClientState, Log);
        _fishing      = new FishingService(ObjectTable, ClientState, DataManager, _places, _tolk, _config, PluginInterface, Log);
        _fates        = new FateService(ClientState);
        _gathering    = new GatheringService(ObjectTable, ClientState, DataManager, _places, _tolk, Log);
        _bestiary     = new BestiaryService(DataManager, Log);
        _routes       = new RouteService(PluginInterface, Log);
        _shops        = new ShopNpcService(DataManager, Log);
        // Shared by browser, target announcement, auto-walk and follow so all
        // four call the same object by the same name (user report 2026-08-08).
        _objectNames  = new ObjectNameService(DataManager);
        // Tells apart several objects sharing one name and remembers where the
        // player has been - a dungeon's four "Truhe" (user wish 2026-08-08).
        _objectMemory = new ObjectMemoryService(ObjectTable, ClientState, Log);
        _navigation   = new NavigationService(ClientState, ObjectTable, TargetManager, _tolk, _beacon, _cue, _questMarkers, _places, _fishing, _fates, _routes, _shops, _objectNames, _objectMemory, _config, DataManager, Log);
        // Reads the cached navigation mesh directly - the only way to tell
        // whether a destination hangs on a surface of its own (see the class).
        _meshCache  = new NavmeshCacheService(DataManager, Log);
        // Holds the REAL zone borders (layout engine) instead of their map
        // symbols - see the class for why the symbols are not enough.
        _zoneExits  = new ZoneExitService(ObjectTable, ClientState, DataManager, _places, _tolk, Log);
        _autoWalk   = new AutoWalkService(PluginInterface, ObjectTable, TargetManager, ClientState, _tolk, _config, _places, _routes, _objectNames, _meshCache, Log);
        _history    = new MessageHistoryService(_tolk);
        // Must exist before the UI reader: that one asks it for the labels of
        // icon buttons, which carry no text of their own.
        _tooltips   = new TooltipService(Interop, Log);
        _uiReader   = new UIReaderService(AddonLifecycle, GameGui, _tolk, Log, ObjectTable, _inventoryReader, _gearInfo, _bestiary, _history, _config, DataManager, _tooltips);
        _chatReader = new ChatReaderService(ChatGui, _tolk, _config, _history, ObjectTable, Log);
        _chatChannel = new ChatChannelService(_history, _tolk, Log);
        _toasts     = new ToastService(ToastGui, _tolk, _config, Log);
        _aoeWarn    = new AoeWarningService(_config, Log);
        _combat     = new CombatService(ObjectTable, TargetManager, DataManager, _tolk, _config, _history, _aoeWarn, Log);
        _cooldown   = new CooldownService(ClientState, DataManager, _cue, _tolk, _config, Log);
        _vitals     = new VitalsService(ObjectTable, _config, Log);
        _heading    = new HeadingService(ObjectTable, _tolk, _config, Log);
        _emote      = new EmoteService(DataManager, ClientState, _tolk, Log);
        _dalamudPlugins = new DalamudPluginsService(PluginInterface, _tolk, Log);
        _tripleTriad = new TripleTriadService(GameGui, _tolk, Log);

        RegisterCommands();
        Framework.Update += OnFrameworkUpdate;
        ClientState.Login += OnLogin;

        // Already in the world when the plugin loads (hot reload, /xlplugins):
        // the HUD is long built, so no quiet period is needed - but the flag has
        // to be primed the same way for a normal login that follows.
        if (ClientState.IsLoggedIn)
            Log.Info("[Accessibility] Beim Laden bereits eingeloggt - keine Anmelde-Ruhephase noetig.");

        Log.Info($"FF14 Accessibility Plugin V{PluginVersion} [{PluginVersionTag}] geladen.");
        _tolk.Speak(AccessibilityStrings.VersionReady(PluginVersion));
    }

    private void RegisterCommands()
    {
        // /acc nav  â†’ Richtung zum Ziel
        // /acc set  â†’ Aktuelles Spielziel verfolgen
        // /acc near â†’ Objekte in der Nähe
        // /acc stop â†’ Sprache stoppen
        CommandManager.AddHandler("/acc", new CommandInfo(OnCommand)
        {
            HelpMessage = "FF14 Accessibility: nav, set, near, keys, stop, help"
        });
    }

    private void OnCommand(string command, string args)
    {
        var trimmed = args.Trim();

        // "dump" nimmt einen optionalen Addon-Namen â€” muss vor dem switch geprüft werden
        if (trimmed.StartsWith("dump", StringComparison.OrdinalIgnoreCase))
        {
            var dumpArg = trimmed.Length > 4 ? trimmed[4..].Trim() : string.Empty;
            _uiReader.DumpAddon(dumpArg);
            return;
        }

        // "lang" nimmt ein Sprach-Argument (de/en/auto) - vor dem switch prüfen
        if (trimmed.StartsWith("lang", StringComparison.OrdinalIgnoreCase))
        {
            var langArg = trimmed.Length > 4 ? trimmed[4..].Trim() : string.Empty;
            SetLanguage(langArg);
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
            case "fish":
                _fishing.AnnounceSpotsInCurrentZone();
                break;
            case "fishobj":
                _fishing.ProbeNearbyObjects();
                break;
            case "fishhere":
                _fishing.CaptureHere();
                break;
            case "gather":
                _gathering.AnnounceSpotsInCurrentZone();
                break;
            case "gathergo":
                GatherWalkToNearest();
                break;
            case "soundtest":
                SoundTest();
                break;
#if DEBUG
            // Objekt-Sonde per Befehl: auf Strg+F5 kommt sie nur ans Ruder, wenn
            // KEIN Fenster offen ist (der Menü-Dump gewinnt dort) - in der freien
            // Welt mit sichtbaren HUD-Addons war sie praktisch nicht auslösbar.
            case "objprobe":
                _navigation.DumpNearbyObjects();
                break;
            // Misst, warum ein Gegenstand nicht auf der Leiste landet: loggt den
            // Slot-Zustand nach JEDEM Schritt und probiert die Alternativen durch.
            case "hotbarprobe":
                _hotbar.ProbeItemAssignment();
                break;
            // Versuch Astalicia: zur vermessenen Uebergangsstelle laufen und
            // die Luecke ohne Wegsuche ueberqueren (Path.MoveTo).
            case "planke":
            case "plank":
                _autoWalk.CrossPlank();
                break;
            // Stellt den Kollisionsboden des Spiels dem Wegenetz gegenueber:
            // zeigt, ob an einer Stelle Boden FEHLT oder nur vom Netzbau
            // verworfen wird. Entscheidet, ob eine Zonen-Anpassung helfen kann.
            case "boden":
            case "ground":
                _autoWalk.ProbeGround();
                break;
            // Stellt die echten Zonengrenzen (Layout-Engine) den Kartensymbolen
            // gegenueber, auf die der Auto-Lauf heute zielt - und misst, was
            // PlayerRunningDirection bedeutet.
            case "uebergang":
            case "exitprobe":
                _zoneExits.ProbeExitRanges();
                break;
#endif
            // Prueft, ob zum anvisierten Ziel ueberhaupt ein Weg fuehrt, und
            // nennt sonst den naechsten Punkt, an den man herankommt.
            case "zugang":
            case "approach":
                _autoWalk.AnnounceApproachToTarget();
                break;
            case "cooldowns":
            case "cd":
                ToggleSkillReady();
                break;
            case "help":
                AnnounceHelp();
                break;
            default:
                _tolk.SpeakInterrupt(AccessibilityStrings.UnknownCommand);
                break;
        }
    }

    /// <summary>
    /// Handles "/acc lang &lt;de|en|auto&gt;": switches the announcement language,
    /// persists it in the config so it survives a restart, and confirms the new
    /// setting spoken in the language just chosen. An unknown/empty argument
    /// speaks the usage hint and changes nothing.
    /// </summary>
    private void SetLanguage(string arg)
    {
        var mode = Loc.ParseArg(arg);
        if (mode is null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.LanguageUsage);
            return;
        }

        _config.Language = mode.Value;
        Loc.Mode = mode.Value;                 // take effect immediately for the confirmation below
        PluginInterface.SavePluginConfig(_config);

        // Name the resolved language; "auto" also reports which one Windows picked.
        var languageName = Loc.IsGerman ? AccessibilityStrings.LanguageGerman : AccessibilityStrings.LanguageEnglish;
        _tolk.SpeakInterrupt(mode.Value == LanguageMode.Auto
            ? AccessibilityStrings.LanguageAuto(languageName)
            : AccessibilityStrings.LanguageSet(languageName));
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
            ("Nächstes Objekt",   _config.KeyNextObject),
            ("Vorheriges Objekt", _config.KeyPrevObject),
            ("Kategorie",         _config.KeyCategory),
            ("Kategorie zurück",  _config.KeyCategoryPrev),
            ("Gehhilfe",          _config.KeyWalkGuide),
            ("Auto-Lauf",         _config.KeyAutoWalk),
            ("Ziel folgen",       _config.KeyFollowTarget),
            ("Routen-Vorschau",   _config.KeyRoutePreview),
            ("Zu Koordinaten",    _config.KeyGotoCoords),
            ("Koordinaten kopieren", _config.KeyCopyCoords),
            ("Menü vorlesen",  _config.KeyReadUI),
            ("Sprache stopp",  _config.KeySilence),
            ("Kampfstatus",    _config.KeyCombatStatus),
            ("SP-Stand",       _config.KeySpStatus),
            ("Himmelsrichtung an/aus", _config.KeyToggleHeading),
            ("Flächenwarnung an/aus", _config.KeyToggleAoeWarning),
            ("UI-Dump",        _config.KeyDumpUI),
            ("Aktives Fenster", _config.KeyWhereAmI),
            ("Aktionsleiste",  _config.KeyReadHotbar),
            ("Inventar",       _config.KeyReadInventory),
            ("Gil",            _config.KeyReadGil),
            ("Stufe",          _config.KeyLevelExp),
            ("Emote weiter",   _config.KeyEmoteNext),
            ("Emote zurück",   _config.KeyEmotePrev),
            ("Emote ausführen", _config.KeyEmoteDo),
            ("Bestiarium",     _config.KeyBestiary),
            ("Benachrichtigung", _config.KeyNotification),
            ("Ausrüstung",     _config.KeyReadEquipment),
            ("Beste Ausrüstung", _config.KeyEquipBest),
            ("Zufälliges Aussehen", _config.KeyRandomLook),
            ("Skill-Menü",     _config.KeySkillMenu),
            ("Nachlese Kategorie zurück", _config.KeyChatCatPrev),
            ("Nachlese Kategorie vor",    _config.KeyChatCatNext),
            ("Nachlese älter", _config.KeyChatReadOlder),
            ("Nachlese neuer", _config.KeyChatReadNewer),
            ("Plugin-Liste weiter",  _config.KeyPluginsNext),
            ("Plugin-Liste zurück",  _config.KeyPluginsPrev),
            ("Plugin-Einstellungen", _config.KeyPluginsConfig),
            ("Kartenspiel Brett", _config.KeyReadBoard),
            ("Kartenspiel Hand",  _config.KeyReadHand),
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
        // V5.31: Objekt-Browser auf die Bild-Tasten (N-Familie freigeraeumt).
        // BildAuf=VK_PRIOR/0x21, BildAb=VK_NEXT/0x22. Bare = CAMERA_ZOOMIN/OUT
        // im Spiel (nur visuell), Strg+BildAuf/-Ab laut Keybind-Dump frei.
        ["BildAuf"] = 0x21, ["BildAb"] = 0x22,
        // Nummernblock â€” TitleDCWorldMap Navigation (4=links, 6=rechts, 2=runter, 8=hoch)
        ["Numpad2"] = 0x62, ["Numpad4"] = 0x64,
        ["Numpad6"] = 0x66, ["Numpad8"] = 0x68,
        // Skill-Menü (V5.61): Numpad0=VK_NUMPAD0 (0x60), Numpad-Komma/Dezimal=
        // VK_DECIMAL (0x6E). Beide sind im Spiel belegt (OK / CANCEL), werden
        // aber nur solange das modale Menue offen ist per KeyState=false
        // geschluckt. Muessen hier stehen, damit UpdateKeyEdges sie trackt.
        ["Numpad0"] = 0x60, ["NumpadKomma"] = 0x6E,
        // Freie Tasten laut Keybind-Dump 2026-07-10 (N = einziger freier BARE
        // Buchstabe). H und L sind bare belegt (MENU_CRAFT / MENU_LINKSHELL),
        // aber mit Modifier frei - nur so (Strg+H, Strg+L) konfiguriert.
        ["N"] = 0x4E, ["H"] = 0x48, ["L"] = 0x4C, ["Numpad3"] = 0x63, ["Numpad5"] = 0x65,
        // Nachlese-Browser (V4.90): Komma/Punkt sind im Spiel nicht belegt
        // (Keybind-Dump 2026-07-17). VK_OEM_COMMA=0xBC, VK_OEM_PERIOD=0xBE.
        // Gueltigkeit prueft UpdateKeyEdges via IKeyState.IsVirtualKeyValid.
        [","] = 0xBC, ["."] = 0xBE,
        // Ziel folgen (V5.57): die BARE +-Taste (VK_OEM_PLUS=0xBB, NICHT Numpad+)
        // ist im Keybind-Dump 2026-07-26 NIRGENDS belegt. User-Wunsch: kein Numpad.
        ["+"] = 0xBB,
        // V5.25: Entf ist im Keybind-Dump NIRGENDS belegt - anders als H, wo
        // das Spiel trotz Strg-Modifier MENU_CRAFT ausloeste. VK_DELETE=0x2E.
        ["Entf"] = 0x2E,
        // SP-Stand-Ansage (Sammler). Strg+Ende ist im Keybind-Dump CAMERA_SAVE
        // (rein visuell, folgenlos). VK_END=0x23.
        ["Ende"] = 0x23,
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

        // The key name "+" is the same character as the modifier separator, so a
        // plain Split() swallows it: "+" leaves no parts at all and "Strg++"
        // leaves only "Strg". That silently disabled the follow key (V5.57 to
        // V5.73). Peel a trailing "+" off as the key name, split only the rest.
        var spec = keySpec.Trim();
        string keyName;
        string modifierPart;
        if (spec.EndsWith('+'))
        {
            keyName      = "+";
            modifierPart = spec[..^1];
        }
        else
        {
            var cut      = spec.LastIndexOf('+');
            keyName      = cut < 0 ? spec : spec[(cut + 1)..].Trim();
            modifierPart = cut < 0 ? string.Empty : spec[..cut];
        }

        var parts = modifierPart.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var valid = keyName.Length > 0;
        for (var i = 0; valid && i < parts.Length; i++)
        {
            switch (parts[i].ToLowerInvariant())
            {
                case "strg" or "ctrl":      parsed.Ctrl  = true; break;
                case "umschalt" or "shift": parsed.Shift = true; break;
                case "alt":                 parsed.Alt   = true; break;
                default:                    valid        = false; break;
            }
        }
        if (valid && KeyNameToVK.TryGetValue(keyName, out var vk))
            parsed.Vk = vk;
        else
            Log.Warning($"Unbekannte Tastenangabe in der Konfiguration: '{keySpec}'");

        _keySpecCache[keySpec] = parsed;
        return parsed;
    }

    private bool IsJustPressed(string keySpec)
    {
        // While a game text field has focus (chat, search box, name entry, ...)
        // every keystroke belongs to that field. Standing down here suppresses
        // ALL mod hotkeys at once - typing an "n" writes "n" instead of cycling
        // nearby objects, arrow keys move the text cursor, Return sends the
        // message (user 2026-07-25). The game's own IsTextInputActive is the
        // authority on when a field is receiving input. The per-frame Update()
        // calls in OnFrameworkUpdate do NOT go through here, so the walk guide,
        // beacon and focus reader keep working while typing.
        if (_textInputActive) return false;

        var (vk, ctrl, shift, alt) = ParseKeySpec(keySpec);
        if (vk < 0 || !_keyJustPressed[vk]) return false;
        // Exact modifier match: bare "N" must NOT fire while Alt is held,
        // because the game binds Alt+N (Neulingschat) itself.
        return KeyState[Dalamud.Game.ClientState.Keys.VirtualKey.CONTROL] == ctrl
            && KeyState[Dalamud.Game.ClientState.Keys.VirtualKey.SHIFT]   == shift
            && KeyState[Dalamud.Game.ClientState.Keys.VirtualKey.MENU]    == alt;
    }

    // Numpad keys that drive the modal skill menu. All are game-bound
    // (8/2=move, 0=OK, comma=cancel), so they are swallowed while the menu is
    // open. VKs: NUMPAD8=0x68, NUMPAD2=0x62, NUMPAD0=0x60, DECIMAL=0x6E.
    // NUMPAD4=0x64 / NUMPAD6=0x66 switch between the skill and item list; they
    // are game-bound too (turn left/right), so they join the swallow list.
    private static readonly int[] SkillMenuVks = { 0x68, 0x62, 0x60, 0x6E, 0x64, 0x66 };

    /// <summary>
    /// While the modal skill menu is open, the numpad drives it and the game
    /// must not see those keys (they are movement / OK / cancel). Acts on the
    /// fresh "just pressed" edge (computed by UpdateKeyEdges before this runs),
    /// then forces every menu key up in KeyState so a held key never leaks
    /// movement or a confirm to the game between edges. No-op while closed, so
    /// the numpad works normally the rest of the time.
    /// </summary>
    private void HandleSkillMenuKeys()
    {
        if (!_hotbar.IsSkillMenuOpen) return;

        // Bare presses only (IsJustPressed already requires no modifiers here).
        if (IsJustPressed("Numpad8"))          _hotbar.SkillMenuBrowse(-1);
        else if (IsJustPressed("Numpad2"))     _hotbar.SkillMenuBrowse(+1);
        else if (IsJustPressed("Numpad4"))     _hotbar.SkillMenuSwitchSource(-1);
        else if (IsJustPressed("Numpad6"))     _hotbar.SkillMenuSwitchSource(+1);
        else if (IsJustPressed("Numpad0"))     _hotbar.SkillMenuConfirm();
        else if (IsJustPressed("NumpadKomma")) _hotbar.SkillMenuBack();

        // Swallow the keys from the game for as long as the menu is open.
        foreach (var vk in SkillMenuVks)
        {
            var key = (Dalamud.Game.ClientState.Keys.VirtualKey)vk;
            if (KeyState.IsVirtualKeyValid(vk) && KeyState[key])
                KeyState[key] = false;
        }
    }

    /// <summary>
    /// Reads two map coordinates (e.g. "24.1 21.0", "X: 24,1 Y: 21,0",
    /// "24.1, 21.0") from the WINDOWS CLIPBOARD, converts them to a world
    /// position on the current map and walks there via the auto-walk. The
    /// clipboard is used on purpose: NVDA cannot read the game chat or an ImGui
    /// text field, so the user copies the coords from anywhere readable
    /// (a message, a wiki, or Notepad they typed into) and presses one key.
    /// </summary>
    private void GotoClipboardCoords()
    {
        string clip;
        try
        {
            clip = ReadClipboardText();
        }
        catch (System.Exception ex)
        {
            Log.Warning($"[Goto] Zwischenablage nicht lesbar: {ex.Message}");
            _tolk.SpeakInterrupt(AccessibilityStrings.ClipboardUnreadable);
            return;
        }

        var coords = ParseMapCoords(clip);
        if (coords == null)
        {
            Log.Info($"[Goto] Keine Koordinaten in der Zwischenablage: '{clip}'");
            _tolk.SpeakInterrupt(AccessibilityStrings.NoCoordsInClipboard);
            return;
        }

        var (mapX, mapY) = coords.Value;
        var approx = _places.MapCoordToWorld(mapX, mapY);
        if (approx == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.MapUnknownConvert);
            return;
        }

        // Snap the 2D map point onto the walkable mesh (map coords carry no height).
        var floor = _autoWalk.ResolveFloorPoint(approx.Value) ?? approx.Value;
        var name  = AccessibilityStrings.CoordsName(mapX, mapY);
        Log.Info($"[Goto] {name} -> Welt {approx.Value.X:0.0}/{approx.Value.Z:0.0}, Boden {floor.Y:0.0}");
        _tolk.SpeakInterrupt(AccessibilityStrings.WalkingToCoords(mapX, mapY));

        // Fresh start every time: stop a running walk first, then head out.
        if (_autoWalk.IsActive) _autoWalk.StopQuiet();
        _autoWalk.ToggleToPosition(floor, name, 2.5f);
    }

    /// <summary>Walks to the nearest gathering spot the active job can work
    /// (/acc gathergo). The spot list comes from the zone's LGB layout, so it
    /// reaches clusters anywhere on the map, not only loaded ones.</summary>
    private void GatherWalkToNearest()
    {
        var spot = _gathering.GetNearestSpot();
        if (spot == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.NoGatheringSpotsJob);
            return;
        }

        var floor = _autoWalk.ResolveFloorPoint(spot.Position) ?? spot.Position;
        var name  = AccessibilityStrings.GatheringSpotName(spot.Level);
        Log.Info($"[Gather] Laufe zu GP={spot.GatheringPointId} '{spot.TypeName}' " +
                 $"Welt=({spot.Position.X:F1}|{spot.Position.Z:F1}) Boden Y={floor.Y:F1}");
        _tolk.SpeakInterrupt(AccessibilityStrings.WalkingTo(name));

        _navigation.StopWalkGuideQuiet();
        if (_autoWalk.IsActive) _autoWalk.StopQuiet();
        _autoWalk.ToggleToPosition(floor, name, 3f);
    }

    /// <summary>
    /// Extracts the first two decimal numbers from arbitrary text as map
    /// coordinates. Accepts dot or comma decimals ("24.1" / "24,1") and any
    /// separators around them. Returns null if fewer than two numbers are found
    /// or they are outside the plausible map-coordinate range (1..60).
    /// </summary>
    private static (float X, float Y)? ParseMapCoords(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var matches = System.Text.RegularExpressions.Regex.Matches(text, @"\d+(?:[.,]\d+)?");
        if (matches.Count < 2) return null;

        var nums = new List<float>(2);
        foreach (System.Text.RegularExpressions.Match m in matches)
        {
            var normalized = m.Value.Replace(',', '.');
            if (float.TryParse(normalized, System.Globalization.NumberStyles.Float,
                               System.Globalization.CultureInfo.InvariantCulture, out var v))
                nums.Add(v);
            if (nums.Count == 2) break;
        }

        if (nums.Count < 2) return null;
        if (nums[0] is < 1f or > 60f || nums[1] is < 1f or > 60f) return null;
        return (nums[0], nums[1]);
    }

    /// <summary>
    /// Reads the player's current map coordinates (the in-game 1..~42 values)
    /// and puts them on the clipboard as "X, Y". A sighted player reads these
    /// off the minimap to share their location ("I'm at 24.1, 21.0"); the blind
    /// player cannot, so one key copies them ready to paste into a chat message
    /// or a tell. The reverse direction of <see cref="GotoClipboardCoords"/> -
    /// the "X, Y" format it writes is exactly what that method parses back.
    /// </summary>
    private void CopyCurrentCoords()
    {
        var player = ObjectTable.LocalPlayer;
        if (player == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.PositionUnknown);
            return;
        }

        var coords = _places.WorldToMapCoord(player.Position);
        if (coords == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.MapUnknownCoords);
            return;
        }

        var (mapX, mapY) = coords.Value;
        // Clipboard text uses invariant '.' decimals so the values paste cleanly
        // into chat and round-trip through GotoClipboardCoords' parser.
        var inv  = System.Globalization.CultureInfo.InvariantCulture;
        var text = $"{mapX.ToString("0.0", inv)}, {mapY.ToString("0.0", inv)}";

        bool ok;
        try
        {
            ok = WriteClipboardText(text);
        }
        catch (System.Exception ex)
        {
            Log.Warning($"[CopyCoords] Zwischenablage nicht schreibbar: {ex.Message}");
            _tolk.SpeakInterrupt(AccessibilityStrings.ClipboardNotWritable);
            return;
        }

        if (!ok)
        {
            Log.Warning("[CopyCoords] Zwischenablage konnte nicht geoeffnet/beschrieben werden.");
            _tolk.SpeakInterrupt(AccessibilityStrings.ClipboardNotWritable);
            return;
        }

        Log.Info($"[CopyCoords] Koordinaten {text} kopiert (Welt {player.Position.X:0.0}/{player.Position.Z:0.0}).");
        _tolk.SpeakInterrupt(AccessibilityStrings.CoordsCopied(mapX, mapY));
    }

    /// <summary>
    /// Turns the turn-by-turn compass announcement on or off and speaks the new
    /// state. When switching ON, the current facing is spoken once as immediate
    /// confirmation and the service is re-baselined so it does not echo the same
    /// direction again on its next frame.
    /// </summary>
    private void ToggleHeading()
    {
        _config.AnnounceHeading = !_config.AnnounceHeading;
        PluginInterface.SavePluginConfig(_config);
        _heading.ResetBaseline();

        if (_config.AnnounceHeading)
        {
            var dir = _heading.CurrentHeadingWord();
            _tolk.SpeakInterrupt(AccessibilityStrings.HeadingOn(dir));
        }
        else
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.HeadingOff);
        }
    }

    /// <summary>
    /// Toggles the AoE danger tone (a continuous sound while the player stands in an
    /// enemy cast's danger zone). Off by default because the geometry is not yet
    /// in-game confirmed; this key lets the player opt in and test it. Switching off
    /// silences the tone on the next frame (UpdateAoeWarning honours the flag).
    /// </summary>
    private void ToggleAoeWarning()
    {
        _config.AnnounceAoeWarning = !_config.AnnounceAoeWarning;
        PluginInterface.SavePluginConfig(_config);
        _tolk.SpeakInterrupt(_config.AnnounceAoeWarning
            ? AccessibilityStrings.AoeWarningOn
            : AccessibilityStrings.AoeWarningOff);
    }

    private void ToggleSkillReady()
    {
        _config.AnnounceSkillReady = !_config.AnnounceSkillReady;
        PluginInterface.SavePluginConfig(_config);
        _tolk.SpeakInterrupt(_config.AnnounceSkillReady
            ? AccessibilityStrings.SkillReadyAnnounceOn
            : AccessibilityStrings.SkillReadyAnnounceOff);
    }

    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE  = 0x0002;

    [DllImport("user32.dll", SetLastError = true)] private static extern bool OpenClipboard(nint hWndNewOwner);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool CloseClipboard();
    [DllImport("user32.dll", SetLastError = true)] private static extern nint GetClipboardData(uint uFormat);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool EmptyClipboard();
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetClipboardData(uint uFormat, nint hMem);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern nint GlobalAlloc(uint uFlags, nuint dwBytes);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern nint GlobalLock(nint hMem);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GlobalUnlock(nint hMem);

    /// <summary>
    /// Reads Unicode text from the Windows clipboard via Win32 - no WinForms
    /// (needs STA) and no ImGui reference. OpenClipboard can briefly fail while
    /// another process holds the clipboard, so it is retried a few times.
    /// </summary>
    private static string ReadClipboardText()
    {
        var opened = false;
        for (var attempt = 0; attempt < 6 && !opened; attempt++)
            opened = OpenClipboard(nint.Zero);
        if (!opened) return string.Empty;

        try
        {
            var handle = GetClipboardData(CF_UNICODETEXT);
            if (handle == nint.Zero) return string.Empty;

            var ptr = GlobalLock(handle);
            if (ptr == nint.Zero) return string.Empty;
            try { return Marshal.PtrToStringUni(ptr) ?? string.Empty; }
            finally { GlobalUnlock(handle); }
        }
        finally { CloseClipboard(); }
    }

    /// <summary>
    /// Writes Unicode text to the Windows clipboard via Win32 - the write-side
    /// mirror of <see cref="ReadClipboardText"/> (no WinForms/STA, no ImGui).
    /// SetClipboardData takes ownership of the moveable global block on success,
    /// so it is NOT freed here. Returns false if the clipboard stays locked by
    /// another process or the allocation fails.
    /// </summary>
    private static bool WriteClipboardText(string text)
    {
        var opened = false;
        for (var attempt = 0; attempt < 6 && !opened; attempt++)
            opened = OpenClipboard(nint.Zero);
        if (!opened) return false;

        try
        {
            if (!EmptyClipboard()) return false;

            // Global block holds the string plus a trailing null (Unicode = 2 bytes/char).
            var buffer = new char[text.Length + 1]; // last element stays '\0'
            text.CopyTo(0, buffer, 0, text.Length);

            var hMem = GlobalAlloc(GMEM_MOVEABLE, (nuint)(buffer.Length * 2));
            if (hMem == nint.Zero) return false;

            var ptr = GlobalLock(hMem);
            if (ptr == nint.Zero) return false;
            try { Marshal.Copy(buffer, 0, ptr, buffer.Length); }
            finally { GlobalUnlock(hMem); }

            // On success the clipboard owns hMem; on failure we would leak it,
            // but SetClipboardData only fails with the clipboard already closed.
            return SetClipboardData(CF_UNICODETEXT, hMem) != nint.Zero;
        }
        finally { CloseClipboard(); }
    }

    // Keybind dump runs automatically once per session: the user cannot open
    // the chat yet, so /acc keys would be unreachable for them.
    private bool _keybindsDumped;

    // True while a game text field has keyboard focus. Cached once per frame
    // (IsJustPressed is called ~60x per frame - one native call is enough) and
    // read by IsJustPressed to gate every mod hotkey off while the user types.
    private bool _textInputActive;

    /// <summary>
    /// True while a game text field (chat, search, name entry, ...) has keyboard
    /// focus. Reads the game's own <c>RaptureAtkModule.IsTextInputActive</c> -
    /// the native function the game itself uses to route keystrokes to a text
    /// box - so this matches the game exactly instead of guessing.
    /// </summary>
    private unsafe bool IsGameTextInputActive()
    {
        var module = RaptureAtkModule.Instance();
        return module != null && module->IsTextInputActive();
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        UpdateKeyEdges();

        // Sample the text-input state once for this frame. Log only on change so
        // the in-game test can confirm it flips exactly when the chat opens/closes.
        var textInputActive = IsGameTextInputActive();
        if (textInputActive != _textInputActive)
        {
            _textInputActive = textInputActive;
            Log.Info($"[TextInput] active={_textInputActive} - mod hotkeys {(_textInputActive ? "suppressed" : "live")}");
        }

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
            // Walk guide, auto-walk and follow are mutually exclusive - one at a
            // time. (Only the walk guide sounds the beacon; the others are silent.)
            _autoWalk.StopQuiet();
            _autoWalk.StopFollowQuiet();
            if (_navigation.IsWalkGuideActive)
            {
                _navigation.ToggleWalkGuide(); // second press: off
            }
            // No through-point here: the walk guide steers the PLAYER, who walks
            // through the line themselves once they are told they are there.
            else switch (TryResolveMarkerDestination(out var pos, out var name, out var stop, out _, out _))
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
            else switch (TryResolveMarkerDestination(out var pos, out var name, out var stop, out var guessedY, out var through))
            {
                case MarkerResolve.Resolved: _autoWalk.ToggleToPosition(pos, name, stop, guessedY, through); break;
                case MarkerResolve.None:     _autoWalk.Toggle();                          break;
                case MarkerResolve.Failed:   break; // reason already announced
            }
        }
        if (IsJustPressed(_config.KeyFollowTarget))
        {
            // Follow the current game target continuously (own vnavmesh follow -
            // FFXIV has no plugin-callable native follow). A walk guide would fight
            // over movement, so end it first.
            _navigation.StopWalkGuideQuiet();
            _autoWalk.ToggleFollow();
        }
        if (IsJustPressed(_config.KeyRoutePreview))
        {
            // Speak the route (compass segments) without walking - to the
            // selected marker destination, or to the current game target.
            switch (TryResolveMarkerDestination(out var pos, out var name, out _, out _, out _))
            {
                case MarkerResolve.Resolved: _navigation.PreviewRoute(pos, name); break;
                case MarkerResolve.None:     _navigation.PreviewRouteToTarget();  break;
                case MarkerResolve.Failed:   break; // reason already announced
            }
        }
        if (IsJustPressed(_config.KeyGotoCoords))    GotoClipboardCoords();
        if (IsJustPressed(_config.KeyCopyCoords))    CopyCurrentCoords();
        if (IsJustPressed(_config.KeyReadUI))        _uiReader.ReadCurrentFocus();
        if (IsJustPressed(_config.KeySilence))       _tolk.Silence();
        if (IsJustPressed(_config.KeyCombatStatus))  _combat.AnnounceStatus();
        if (IsJustPressed(_config.KeySpStatus))      _combat.AnnounceGatheringPoints();
        if (IsJustPressed(_config.KeyToggleHeading)) ToggleHeading();
        if (IsJustPressed(_config.KeyToggleAoeWarning)) ToggleAoeWarning();
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
        if (IsJustPressed(_config.KeySkillMenu))     _hotbar.ToggleSkillMenu();
        if (IsJustPressed(_config.KeyReadLootRolls)) _lootRolls.AnnounceOpenRolls();
        if (IsJustPressed(_config.KeyFocusLootRolls)) _lootRolls.FocusRollWindow();
        HandleSkillMenuKeys();
        if (IsJustPressed(_config.KeyChatCatPrev))   _history.SwitchCategory(-1);
        if (IsJustPressed(_config.KeyChatCatNext))   _history.SwitchCategory(+1);
        if (IsJustPressed(_config.KeyChatReadOlder)) _history.ReadOlder();
        if (IsJustPressed(_config.KeyChatReadNewer)) _history.ReadNewer();
        if (IsJustPressed(_config.KeyReadBoard))     _tripleTriad.ReadBoard();
        if (IsJustPressed(_config.KeyReadHand))      _tripleTriad.ReadHand();
        if (IsJustPressed("Escape"))                 _uiReader.HandleEscapeKey();
        // F5 â€” UI-Dump des aktuell aktiven Addons auf den Desktop schreiben
        // (kein Chat-Fenster nötig, funktioniert auch auf dem Titelbildschirm)
        if (IsJustPressed(_config.KeyDumpUI))
        {
            // Dump the focused menu/window first. Only when there is NO such
            // window (overworld) fall back to the nearby-object/marker probe -
            // otherwise its "N Objekte im Log" announcement would override the
            // menu-dump confirmation and it looks as if F5 stopped dumping menus.
            if (!_uiReader.DumpFocusedAddon())
                _navigation.DumpNearbyObjects();
        }
        // F2 â€” aktives Fenster ansagen + alle sichtbaren Fenster ins Log ([Win])
        if (IsJustPressed(_config.KeyWhereAmI))      _uiReader.AnnounceActiveWindow();

        _combat.Update();
        _cooldown.Update();
        // HP/MP tones on every 10 % step (pan = fill level). Independent of
        // combat state on purpose: post-fight regeneration is exactly when the
        // bar refilling should be audible.
        _vitals.Update();
        // Speaks the compass direction the player turns to face (settled turns,
        // sector changes only). Toggled by KeyToggleHeading.
        _heading.Update();
        _equipment.Update();
        // Announces newly arrived USABLE quest items (key items that trigger an
        // action) - the loot channel only says they arrived, not that they do
        // something. Throttles itself to once a second.
        _inventoryReader.Update();
        // Announces party loot rolls the moment they open. Reads the game's own
        // Loot state, so it works no matter what the NeedGreed window is doing.
        _lootRolls.Update();
        // Always runs: drives the walk guide too, which must not die when
        // target-change announcements are switched off. During an auto-walk
        // target announcements are muted (soft-target churn while passing NPCs).
        // Before the navigation update: it records what the player is standing
        // next to RIGHT NOW, and the target announcement that follows should be
        // able to say "schon besucht" for the very object just walked up to.
        _objectMemory.Update();
        _navigation.Update(_config.AnnounceTargetChanges && !_autoWalk.IsActive && !_autoWalk.IsFollowing);
        _autoWalk.Update();
        // Speaks "Angelbereit" when the player faces castable water and "Biss"
        // on a bite - the last-mile fishing cues (reads the game's own state).
        _fishing.Update();
        // Global UI focus (AtkInputManager.FocusedNode): announces whatever
        // control the game itself considers keyboard-focused - dialogs,
        // options, everything. See UIReaderService.UpdateGlobalFocus.
        // Held (not just-pressed) state: survives OS key-repeat for the whole
        // time a direction key stays down, so JournalResult can tell deliberate
        // reward browsing from the game's own unprompted focus auto-cycle.
        // User's in-game menu navigation is the NUMPAD (2/4/6/8 - same as the
        // DC-map and skill-menu navigation above), not the arrow keys - checked
        // both here since arrow keys still move focus in some native windows.
        var navKeyHeld = KeyState[Dalamud.Game.ClientState.Keys.VirtualKey.UP]
            || KeyState[Dalamud.Game.ClientState.Keys.VirtualKey.DOWN]
            || KeyState[Dalamud.Game.ClientState.Keys.VirtualKey.LEFT]
            || KeyState[Dalamud.Game.ClientState.Keys.VirtualKey.RIGHT]
            || KeyState[(Dalamud.Game.ClientState.Keys.VirtualKey)0x68]  // Numpad8
            || KeyState[(Dalamud.Game.ClientState.Keys.VirtualKey)0x62]  // Numpad2
            || KeyState[(Dalamud.Game.ClientState.Keys.VirtualKey)0x64]  // Numpad4
            || KeyState[(Dalamud.Game.ClientState.Keys.VirtualKey)0x66]; // Numpad6
        _uiReader.UpdateGlobalFocus(navKeyHeld);

#if DEBUG
        // Debug-only auto-probe: logs focused config-menu elements while a
        // Config* window is open. Compiled out of release builds.
        _uiReader.ConfigProbeTick();
#endif

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

        if (IsJustPressed("Return"))
        {
            _uiReader.HandleConfirmKey();
            // Enter also opens the game's chat line. When the player has just
            // been browsing the message history, the line should send into THAT
            // channel - so the switch happens here, BEFORE the game opens the
            // line, and the existing "Chat-Eingabe, <Kanal>" announcement states
            // the result instead of a second one competing with it.
            //
            // Never while the line is already open: there Enter SENDS, and moving
            // the channel underneath it would misdeliver the message.
            if (!_uiReader.IsChatInputActive()) _chatChannel.TrySwitchToBrowsedChannel();
        }

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
    /// <summary>How close the walk has to get to a zone border to count as
    /// arrived. Deliberately loose: reaching the border is only the first leg,
    /// and a tight range would leave the crossing leg unstarted.</summary>
    private const float ZoneBorderStopRange = 3f;

    /// <summary>How far past a zone border to drive. The borders measured
    /// 2026-08-09 had half-extents of 2,77 to 15,56 m, so this clears most of
    /// them from the centre outwards; the drive ends the moment the zone
    /// changes anyway, which is what makes an overshoot harmless.</summary>
    private const float ZoneBorderPushMetres = 12f;

    /// <param name="throughPoint">Set only for a zone border with a known
    /// crossing direction: the point past it the walk has to carry on to. Null
    /// everywhere else, including transitions that turn out to be doors.</param>
    private MarkerResolve TryResolveMarkerDestination(out Vector3 position, out string name, out float stopRange,
                                                      out bool heightIsGuess, out Vector3? throughPoint)
    {
        position = default;
        name = string.Empty;
        throughPoint = null;
        stopRange = _config.AutoWalkPlaceStopRange;
        // Map data is 2D. Everything resolved from it has a GUESSED height, and
        // the guess uses the player's own - which picks the wrong storey when
        // they stand far away and lower (measured 2026-08-07: aetheryte
        // Herbstkürbis-See, mesh at Y -49 and Y -39 above the same spot, the
        // guess took -49 and only -39 was reachable). The auto-walk needs to
        // know this to tell a wrong storey from a genuinely unreachable target.
        heightIsGuess = false;

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
                    _tolk.SpeakInterrupt(AccessibilityStrings.QuestInAnotherZoneNoHop(quest.QuestName));
                    return MarkerResolve.Failed;
                }
                var playerY = ObjectTable.LocalPlayer?.Position.Y ?? 0f;
                var floor   = _autoWalk.ResolveFloorPoint(hop.Position with { Y = playerY });
                if (floor == null)
                {
                    _tolk.SpeakInterrupt(AccessibilityStrings.NoWalkablePointAt(hop.Name));
                    return MarkerResolve.Failed;
                }
                position = floor.Value;
                name = hop.Name;
                // Transition: stop almost on the marker so the zone line triggers.
                stopRange = _config.AutoWalkTransitionStopRange;
                heightIsGuess = true;
                return MarkerResolve.Resolved;
            }

            // Snap the marker onto the walkable mesh so the tight stop range
            // can be met (marker centres can sit off the mesh); fall back to
            // the raw position if no floor is found.
            position = _autoWalk.ResolveFloorPoint(quest.Position) ?? quest.Position;
            name = quest.QuestName;
            heightIsGuess = true;
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

            // A zone transition: aim at the REAL border, not at its map symbol.
            // The symbol is artwork - measured 2026-08-09 it sat 0,27 to 6,77 m
            // beside the border it belongs to, and at 6,77 m the walk reported
            // "arrived" without anything happening. The layout engine holds the
            // border itself, including the direction one crosses it in.
            if (place.IsZoneTransition
                && _zoneExits.FindExitForMap(place.TargetMapId, place.Position) is { } border)
            {
                // The box CENTRE height is the middle of a volume that reaches
                // well above the floor (measured: centre Y 8,21 where the ground
                // is at 4,05), so the height comes from the mesh as everywhere
                // else - only X/Z are taken from the border.
                var onFloor = _autoWalk.ResolveFloorPoint(border.Position with { Y = playerY });
                if (onFloor != null)
                {
                    position = onFloor.Value;
                    name = place.Name;
                    heightIsGuess = true;
                    // Wide on purpose: the point is to get INTO the border, and
                    // the second leg does the crossing. A tight range would make
                    // the walk count as "not arrived" a metre out and never hand
                    // over.
                    stopRange = ZoneBorderStopRange;
                    throughPoint = ZoneExitService.PointBeyond(border, position.Y, ZoneBorderPushMetres);
                    return MarkerResolve.Resolved;
                }
                Log.Info($"[Uebergang] Kein begehbarer Punkt an der Grenze '{place.Name}' - " +
                         "falle auf das Kartensymbol zurueck.");
            }
            // Fishing spots are water CENTRES: snap to the nearest bank (wide
            // search) so the player lands at the water, not on a floor the
            // generic 10 m snap happens to find. Fall back to the generic
            // resolver if no bank is found (e.g. vnavmesh not ready).
            var floor   = place.IsWaterSpot
                ? (_autoWalk.ResolveNearestBank(place.Position with { Y = playerY })
                   ?? _autoWalk.ResolveFloorPoint(place.Position with { Y = playerY }))
                : _autoWalk.ResolveFloorPoint(place.Position with { Y = playerY });
            if (floor == null)
            {
                _tolk.SpeakInterrupt(AccessibilityStrings.NoWalkablePointNear(place.Name));
                return MarkerResolve.Failed;
            }
            position = floor.Value;
            name = place.Name;
            heightIsGuess = true;
            // Transitions get an extra-tight range so the player walks right
            // into the zone line; other places stop on the spot.
            stopRange = place.IsZoneTransition
                ? _config.AutoWalkTransitionStopRange
                : _config.AutoWalkPlaceStopRange;
            return MarkerResolve.Resolved;
        }

        var obj = _navigation.SelectedObjectDestination;
        if (obj != null)
        {
            // The game took the pick as its hard target: leave it to the target
            // path, which re-reads the position every frame - that is what makes
            // walking to a moving NPC work. Only when the target did NOT stick
            // (quest props are listed but not targetable) do we steer by
            // position, which is the whole point of remembering the object.
            if ((TargetManager.Target?.GameObjectId ?? 0) == obj.ObjectId) return MarkerResolve.None;

            // Fresh position from the object table; the remembered one is the
            // fallback for an object that has since despawned.
            var live = ObjectTable.FirstOrDefault(o => o.GameObjectId == obj.ObjectId);
            var raw  = live?.Position ?? obj.Position;
            position = _autoWalk.ResolveFloorPoint(raw) ?? raw;
            // The browser already stored a RESOLVED name (gathering node type,
            // sheet name, or the honest "Objekt ohne Namen"), so this only has
            // to guard against a pick made before that resolution existed.
            name = ObjectNameService.IsSpeakable(obj.Name)
                ? obj.Name
                : AccessibilityStrings.UnnamedOfKind(live?.ObjectKind ?? ObjectKind.EventObj);
            // Interaction range, same as the auto-walk to a game target: the
            // player has to end up close enough to actually use the object.
            stopRange = AutoWalkService.StopRange;
            Log.Info($"[Nav] Objekt-Auswahl '{name}' (id={obj.ObjectId:X}) nicht anvisiert - " +
                     $"laufe zur Position {position} (Objekt {(live != null ? "da" : "weg")}).");
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
                ? AccessibilityStrings.NoMonsterNearbyHabitat(monsterName, habitat)
                : AccessibilityStrings.NoMonsterNearby(monsterName));
            return;
        }

        // Target it first (fight follows the walk); the game may reject the
        // set (V4.24), so read back and warn instead of walking untargeted.
        TargetManager.Target = nearest;
        if (TargetManager.Target?.GameObjectId != nearest.GameObjectId)
            _tolk.SpeakInterrupt(AccessibilityStrings.NotTargetedWarning);
        _autoWalk.Toggle();
    }

    /// <summary>
    /// Auditions the generated audio cues on demand ("/acc soundtest") so a blind
    /// player can judge and tune the sounds without walking around in-game: the
    /// navigation beacon is swept ahead -> right -> behind (pitch/pan/volume all
    /// move), then the waypoint and arrival cues play. Timed with framework ticks
    /// (~60/s); the beacon strikes a pluck every 0.5 s, so ~0.7 s per angle lets
    /// each be heard clearly.
    /// </summary>
    private void SoundTest()
    {
        _tolk.SpeakInterrupt(AccessibilityStrings.SoundTestRunning);

        _beacon.Start();
        _beacon.Update(0, 6f);                                             // ahead, close = high + loud + centered
        Framework.RunOnTick(() => _beacon.Update(45, 12f),  delayTicks: 42);  // to the right
        Framework.RunOnTick(() => _beacon.Update(120, 25f), delayTicks: 84);  // behind-right, lower
        Framework.RunOnTick(() => _beacon.Update(180, 45f), delayTicks: 126); // directly behind, lowest + quiet
        Framework.RunOnTick(() => _beacon.Stop(),           delayTicks: 168);
        Framework.RunOnTick(() => _cue.PlayWaypointTone(),  delayTicks: 180);
        Framework.RunOnTick(() => _cue.PlayArrivalTone(),   delayTicks: 220);

        // HP/MP tones: each case is announced, then the tone plays ~0.4 s later so
        // the label does not step on it. ~90 ticks (~1.5 s) between cases. Percent
        // drives the stereo position; the HP-critical case is <25 % so it pulses.
        VitalsTestStep(delay: 300, AccessibilityStrings.SoundTestHpHeal,     health: true,  direction: +1, percent: 80);
        VitalsTestStep(delay: 390, AccessibilityStrings.SoundTestHpDamage,   health: true,  direction: -1, percent: 55);
        VitalsTestStep(delay: 480, AccessibilityStrings.SoundTestHpCritical, health: true,  direction: -1, percent: 15);
        VitalsTestStep(delay: 570, AccessibilityStrings.SoundTestMpGain,     health: false, direction: +1, percent: 80);
        VitalsTestStep(delay: 660, AccessibilityStrings.SoundTestMpSpend,    health: false, direction: -1, percent: 40);
    }

    /// <summary>One HP/MP audition step: speak the label, then play the matching
    /// vitals tone a beat later so speech and tone do not overlap.</summary>
    private void VitalsTestStep(int delay, string label, bool health, int direction, int percent)
    {
        Framework.RunOnTick(() => _tolk.SpeakInterrupt(label),                       delayTicks: delay);
        Framework.RunOnTick(() => _vitals.PlayTestTone(health, direction, percent),  delayTicks: delay + 24);
    }

    private void AnnounceHelp()
    {
        _tolk.SpeakInterrupt(AccessibilityStrings.HelpFull);
    }

    /// <summary>
    /// Starts the post-login quiet period: the game builds its entire HUD here
    /// and every window would otherwise be announced (see
    /// <see cref="UIReaderService.BeginLoginQuiet"/>). The keybind dump is also
    /// re-armed, so a character switch re-checks for key conflicts.
    /// </summary>
    private void OnLogin()
    {
        _uiReader.BeginLoginQuiet(_config.LoginQuietSeconds);
        _keybindsDumped = false;
    }

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;
        ClientState.Login -= OnLogin;
        CommandManager.RemoveHandler("/acc");
        _tooltips.Dispose();
        _toasts.Dispose();
        _chatReader.Dispose();
        _uiReader.Dispose();
        _autoWalk.Dispose();
        _meshCache.Dispose();
        _beacon.Dispose();
        _aoeWarn.Dispose();
        _cue.Dispose();
        _vitals.Dispose();
        _tolk.Dispose();
    }
}
