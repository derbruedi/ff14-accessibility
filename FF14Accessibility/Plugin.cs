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
    // Zwei Nutzer, ein Dienst: [Chat-Puffer] LogTabFilterN sagt, welchen Filtersatz
    // ein Chat-Register benutzt; NavigationService liest denselben Dienst fuer den
    // Bewegungsmodus, der entscheidet, ob beim manuellen Laufen die Figur oder die
    // Kamera steuert.
    [PluginService] private IGameConfig             GameConfig      { get; init; } = null!;
    [PluginService] private IGameInventory          GameInventory   { get; init; } = null!;
    [PluginService] private IToastGui               ToastGui        { get; init; } = null!;
    [PluginService] private IGameInteropProvider    Interop         { get; init; } = null!;
    // IGameConfig steht weiter oben - drei Nutzer, eine Deklaration (Chat-Filter,
    // Bewegungsmodus). PR #6 brachte eine zweite mit, die hier entfallen ist.
    // [Tiefes Gewoelbe] Loest die Makros in Sheet-Texten so auf, wie das Spiel es tut. Die
    // Pomander-Beschreibungen brauchen das: dort steckt das Wort fuer eine Ebene in
    // einem Switch-Makro, und ExtractText() allein wirft es weg (siehe DeepDungeonText).
    [PluginService] private ISeStringEvaluator      SeStringEval    { get; init; } = null!;

    private readonly Configuration      _config;
    private readonly TolkService        _tolk;
    private readonly BeaconService      _beacon;
    private readonly EscapeRouteService _escape;
    private readonly CueService         _cue;
    private readonly CooldownService    _cooldown;
    private readonly DutyActionService  _dutyActions;
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
    private readonly HuntingLogService  _huntingLog;
    private readonly DutyEntranceService _dutyEntrances;
    private readonly DungeonRouteService _dungeonRoute;
#if DEBUG
    private readonly LiftProbe _liftProbe;
    private readonly ZoneExitProbe _zoneExitProbe;
    private readonly CollisionProbe _collisionProbe;
#endif
    private readonly ZoneBorderService _zoneBorders;
    private readonly LevequestEnemyService _leveEnemies;
    private readonly DirectorTodoService _directorTodos;
    private readonly RouteService       _routes;
    private readonly ShopNpcService     _shops;
    private readonly ObjectNameService  _objectNames;
    private readonly ObstacleService    _obstacles;
    private readonly ObjectMemoryService _objectMemory;
    private readonly NavigationService  _navigation;
    private readonly AutoWalkService    _autoWalk;
    private readonly MeshBridgeService  _bridges;
    private readonly ZoneTransitionHandler _transitions;
    private readonly TrailService       _trails;
    private readonly CharaMakeReader    _charaMake;
    private readonly UIReaderService    _uiReader;
    private readonly ChatReaderService  _chatReader;
    private readonly MessageHistoryService _history;
    // DIE BEIDEN CHATSYSTEME LAUFEN NEBENEINANDER, und der Schalter im
    // Optionsmenue entscheidet nur, welches spricht und welches die Tasten
    // bekommt (Configuration.UseLegacyChatSystem). Beide Nachlesen werden immer
    // gefuellt, damit ein Umschalten mitten in der Sitzung keine Luecke
    // hinterlaesst - der Spieler soll die zwei ja vergleichen koennen.
    private readonly LegacyChatHistoryService _legacyHistory;
    private readonly LegacyChatReaderService  _legacyChatReader;
    // Gehoert zum alten System: Enter schreibt in den Kanal, dessen Nachlese
    // gerade gelesen wurde (v5.67). PR #5 hatte das mitgeloescht.
    private readonly ChatChannelService _chatChannel;
    // [Chat-Puffer] Die eigenen Filterzeilen und Register des Spiels. Der Chat-Leser
    // fragt sie, welche Register eine eingehende Zeile zeigen wuerden.
    private readonly GameChatFilters    _chatFilters;
    // [Chat-Puffer] Liest, welches Chat-Register das Spiel anzeigt, und schaltet es
    // auf den beiden Registertasten ueber die spieleigene ChangeTab um.
    private readonly ChatTabControl     _chatTabs;
    // [Chat-Puffer] Laedt den vom SPIEL gespeicherten Chatverlauf einmalig und still
    // in die Puffer zurueck, damit ein Plugin-Neustart den Verlauf nicht verliert.
    private readonly ChatBackfill       _chatBackfill;
    // [Chat-Puffer] Puffer, die das aktive Register anbietet - wiederverwendet, damit
    // die Blaettertaste nicht bei jedem Druck eine Liste anlegt.
    private readonly List<int>          _offeredChannels = new();
    // [Chat-Puffer] Zuletzt angesagtes Chat-Register. int.MinValue = in dieser
    // Sitzung noch keines gesehen, der erste Wert wird still uebernommen.
    private int _announcedChatTab = int.MinValue;
    // [Einstellungsmenue] Das gesprochene Menue und seine Tastenabfrage.
    private readonly SpokenMenu         _menu;
    private readonly MenuInput          _menuInput;
    private readonly OptionsMenu        _options;
    private readonly ToastService       _toasts;
    private readonly CombatService      _combat;
    private readonly AoeWarningService  _aoeWarn;
    // [Warnstimme] Zweiter Sprachkanal fuer die vier Kampfwarnungen.
    private readonly WarningVoiceService _warnVoice;
    private readonly VitalsService      _vitals;
    private readonly HeadingService     _heading;
    private readonly EmoteService       _emote;
    private readonly KeybindService     _keybinds;
    private readonly DalamudPluginsService _dalamudPlugins;
    private readonly TooltipService _tooltips;
    private readonly TripleTriadService _tripleTriad;
    // ── [Tiefes Gewoelbe] ──
    private readonly DeepDungeonText    _deepText;
    private readonly DeepDungeonState   _deepState;
    private readonly DeepDungeonRoomMap _deepRoomMap;
    private readonly DeepDungeonFloor   _deepFloor;
    private readonly DeepDungeonNav     _deepNav;
    private readonly DeepDungeonPanel   _deepPanel;

    // Single source of truth for the version: log line AND spoken announcement
    // derive from these (they diverged once - spoken 4.1 vs logged 4.2).
    // 5.86 macht das Jagdtagebuch benutzbar: die Rang-Zeilen sagen endlich, was
    // sie sind, und der Objekt-Browser fuehrt zu den Monstern, die der aktuelle
    // Rang noch verlangt - auch in andere Gebiete.
    private const string PluginVersion    = "5.94";
    private const string PluginVersionTag = "Kategorie Dungeon: die Stationen in Laufreihenfolge; Routen-Vorschau nennt die Höhe";

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
        if (_config.Version < 12)
        {
            // V5.89: die Sonderaktionen lagen einen halben Tag lang auf
            // Strg+Numpad7/9/1. Der User hat gemeldet, dass diese Kombis bei ihm
            // nicht ankommen - waehrend Strg+Numpad3 sehr wohl funktioniert, es
            // also keine allgemeine Strg-Schwaeche ist. Diese Fassung war nie
            // veroeffentlicht, aber eine Sitzung mit Hot-Reload kann die alten
            // Werte schon gespeichert haben. Nur die damaligen Vorgaben werden
            // umgezogen, eine eigene Belegung bleibt stehen.
            if (_config.KeyDutyAction1    == "Strg+Numpad7") _config.KeyDutyAction1    = "Umschalt+F10";
            if (_config.KeyDutyAction2    == "Strg+Numpad9") _config.KeyDutyAction2    = "Umschalt+F11";
            if (_config.KeyDutyActionList == "Strg+Numpad1") _config.KeyDutyActionList = "Strg+Umschalt+F8";
            _config.Version = 12;
            PluginInterface.SavePluginConfig(_config);
        }
        // Language for all mod announcements (Auto = follow Windows). Must be set
        // before the first Speak below.
        Loc.Mode = _config.Language;

        TolkNative.Initialize(PluginInterface.AssemblyLocation.DirectoryName!);
        _tolk       = new TolkService(Log);
        // Cue VOR Beacon: der Peil-Ton braucht ihn fuer den Einrast-Ton, der
        // erklaert, warum er beim richtigen Stand schweigt.
        _cue          = new CueService(_config, Log);
        _beacon       = new BeaconService(_config, _tolk, _cue, Log);
        _gearInfo     = new GearInfoService(DataManager, Log);
        // Wohin man aus einer Gefahrenflaeche heraus laufen muss. Vor der
        // Navigation angelegt, weil die den Peil-Ton fuehrt und die Flucht
        // darauf Vorrang hat; der Kampf fuettert sie mit den Flaechen.
        _escape       = new EscapeRouteService(PluginInterface, Log);
        _keybinds     = new KeybindService(_tolk, Log);
        // Inventory first: the hotbar menu reads the carried items from it.
        _inventoryReader = new InventoryService(GameInventory, DataManager, ClientState, _config, _tolk, Log);
        _hotbar       = new HotbarService(DataManager, ClientState, Framework, _gearInfo, _keybinds, _inventoryReader, _tolk, Log);
        _lootRolls    = new LootRollService(DataManager, ClientState, GameGui, _config, _gearInfo, _tolk, Log);
        _equipment    = new EquipmentService(GameInventory, DataManager, _gearInfo, _tolk, Log);
        _questMarkers = new QuestMarkerService(ClientState, DataManager, Log);
        _places       = new PlacesService(DataManager, ClientState, Log);
        _fishing      = new FishingService(ObjectTable, ClientState, DataManager, _places, _tolk, _config, PluginInterface, Log);
        _fates        = new FateService(ClientState);
        _gathering    = new GatheringService(ObjectTable, ClientState, DataManager, _places, _tolk, Log);
        _bestiary     = new BestiaryService(DataManager, Log);
        _huntingLog   = new HuntingLogService(DataManager, ObjectTable, ClientState, _places, Log);
        // Alle Dungeon-, Pruefungs- und Raid-Eingaenge der WELT mit Stufe und Ort,
        // fuer die Browser-Kategorie "Alle Inhalte" (Spielerwunsch 2026-08-19:
        // *"eine kategorie wo man zu den dungeons laufen kann ... nach stufe
        // sortiert ... map uebergreifend hinlaufen"*). Braucht _places fuer die
        // Zonenrouten und muss deshalb dahinter stehen.
        _dutyEntrances = new DutyEntranceService(DataManager, ClientState, _places, Log);
#if DEBUG
        // Misst am Aufzug, was ein Aufzug ueberhaupt ist - siehe LiftProbe.
        _liftProbe = new LiftProbe(ObjectTable, TargetManager, _tolk, Log);
        // Misst am Zonenwechsel, ob ExitRange.Scale das Halb- oder das Vollmass ist -
        // siehe ZoneExitProbe. Braucht keinen Startbefehl: der Wechsel ist das Ereignis.
        _zoneExitProbe = new ZoneExitProbe(ClientState, ObjectTable, DataManager, Log);
        // Beantwortet, ob die Absperrung am Tor zum Tiefen Wald gerade STEHT -
        // siehe CollisionProbe. Aus dem laufenden Layout, nicht aus der .lgb.
        _collisionProbe = new CollisionProbe(ObjectTable, ClientState, _tolk, Log);
#endif
        // Echte Zonengrenzen statt Kartensymbol - nur als Zielgeber, siehe ZoneBorderService.
        _zoneBorders = new ZoneBorderService(DataManager, ClientState, Log);
        // Gegner des gerade LAUFENDEN Freibriefs, für die Freibrief-Kategorie
        // des Objekt-Browsers (Spielerwunsch 2026-08-18).
        _leveEnemies  = new LevequestEnemyService(DataManager, Log);
        // Die Aufgabenliste, die ein sehender Spieler am Bildschirmrand liest -
        // fuer Freibrief, Dungeon und FATE gleichermassen (Spielerwunsch
        // 2026-08-18, aufgekommen bei der Freibrief-Suche).
        _directorTodos = new DirectorTodoService(Log);
        _routes       = new RouteService(PluginInterface, Log);
        _shops        = new ShopNpcService(DataManager, Log);
        // Shared by browser, target announcement, auto-walk and follow so all
        // four call the same object by the same name (user report 2026-08-08).
        _objectNames  = new ObjectNameService(DataManager);
        // Die Stationen des Wegs DURCH eine Instanz, in Reihenfolge - der
        // Gegenentwurf zu allen anderen Kategorien, die nach Naehe sortieren
        // (Spielerwunsch 2026-08-29: *"eine kategorie die sich dungeon nennt so
        // das man sie nach der reie ablaufen kann"*). Braucht _objectNames, weil
        // eine Station nur ihre DataId mitbringt und der Name aus dem Sheet kommt.
        _dungeonRoute = new DungeonRouteService(PluginInterface, ClientState, _objectNames, Log);
        // Tells apart several objects sharing one name and remembers where the
        // player has been - a dungeon's four "Truhe" (user wish 2026-08-08).
        _objectMemory = new ObjectMemoryService(ObjectTable, ClientState, Log);
        _navigation   = new NavigationService(ClientState, ObjectTable, TargetManager, _tolk, _beacon, _escape, _cue, _questMarkers, _places, _fishing, _fates, _routes, _shops, _huntingLog, _dutyEntrances, _dungeonRoute, _leveEnemies, _objectNames, _objectMemory, _config, DataManager, GameConfig, Log);
        // Selbst abgelaufene Spuren über Lücken im Wegenetz - der Auto-Lauf
        // greift darauf zurück, wo das Netz endet (siehe TrailService).
        _trails     = new TrailService(PluginInterface, ObjectTable, ClientState, _tolk, _config, Log);
        // Gemessene Netzluecken und der Vorwaerts-Impuls in eine Zonengrenze.
        // Beide werden VOR dem Auto-Lauf gebaut, weil er sie benutzt; beide sind
        // dort optional, der Lauf laeuft ohne sie unveraendert weiter.
        _bridges     = new MeshBridgeService(ClientState, Log);
        _autoWalk   = new AutoWalkService(PluginInterface, ObjectTable, TargetManager, ClientState, _tolk, _config, _places, _routes, _trails, _bridges, _objectNames, Log);
        // Erst jetzt: der Handler teilt sich die vnavmesh-Verbindung des Auto-Laufs,
        // statt eine zweite zu oeffnen.
        // Sagt, WAS im Weg steht, wenn ein Lauf sich festfaehrt - Wesen beim Namen,
        // Kulisse als das, was sie ist. Der Unterschied entscheidet, was der
        // Spieler tut: ein Spieler geht gleich weiter, eine Absperrung nie.
        _obstacles   = new ObstacleService(ObjectTable, _objectNames, Log);
        _autoWalk.Obstacles = _obstacles;
        _transitions = new ZoneTransitionHandler(ObjectTable, ClientState, _autoWalk.Navmesh, _tolk, _obstacles, Log);
        _autoWalk.Transitions = _transitions;
        // Der Peil-Ton spielt nur, solange ein Lauf laeuft - dafuer muss der
        // Navigationsdienst den Auto-Lauf fragen koennen. Auch das erst hier: er
        // wird ein paar Zeilen weiter oben gebaut, lange bevor es einen Auto-Lauf
        // gibt (siehe NavigationService.AutoWalk).
        _navigation.AutoWalk = _autoWalk;
        // Static, so it needs its log handed over once. Without it the turn still
        // happens, only the follow-up measurement in FacingService.Tick stays mute.
        FacingService.Configure(Log);
        // Die Konfiguration wegen der eigenen Reihenfolge der Nachlese-Kategorien
        // (User-Wunsch 2026-08-26): welcher Puffer wann drankommt und welcher gar
        // nicht, steht dort - siehe Configuration.ChatBufferOrder.
        _history    = new MessageHistoryService(_tolk, _config);
        // Die alte Nachlese laeuft parallel mit. Sie wird nur von den beiden
        // Chat-Lesern und dem Spiegel unten gefuellt - ausser dem Sprecher braucht
        // sie nur die Konfiguration, aus demselben Grund wie die neue.
        _legacyHistory = new LegacyChatHistoryService(_tolk, _config);
        // WAS DIE ALTE NACHLESE SONST NICHT SEHEN WUERDE: Dialogfenster und
        // System-Meldungen kommen aus dem UIReader, die Erfahrungspunkte aus dem
        // CombatService, und alle drei kennen nur den NEUEN Dienst. Der Spiegel
        // reicht genau diese Zeilen hinueber. Die Chat-Zeilen selbst laufen NICHT
        // hier durch (mirror: false im Chat-Leser) - die archiviert der alte Leser
        // mit seiner eigenen Kategorie-Zuordnung, sonst staende jede Zeile zweimal
        // im alten Verlauf.
        _history.Mirror = (key, text, partner) =>
        {
            var category = key == MessageHistoryService.DialogueKey
                ? LegacyChatHistoryService.Category.Dialogue
                : LegacyChatHistoryService.Category.System;
            _legacyHistory.Add(category, text, partner);
        };
        // Must exist before the UI reader: that one asks it for the labels of
        // icon buttons, which carry no text of their own.
        _tooltips   = new TooltipService(Interop, Log);
        // Charaktererstellung, Schritt Aussehen. Faehrt sich selbst ueber Update()
        // und liefert dem Fokus-Leser an einer Stelle den Satz zur Kategorie bzw.
        // zum Waehler-Eintrag.
        _charaMake  = new CharaMakeReader(ObjectTable, DataManager, GameGui, _tolk, Log, _tooltips);
        _uiReader   = new UIReaderService(AddonLifecycle, GameGui, _tolk, Log, ObjectTable, _inventoryReader, _gearInfo, _bestiary, _history, _config, DataManager, _tooltips, _charaMake, _lootRolls);
        // [Chat-Puffer] Vor dem Chat-Leser gebaut, der sie fragt, welche Register eine
        // eingehende Zeile zeigen wuerden. Die aus den Sheets abgeleiteten Tabellen
        // entstehen hier; der LIVE-Zustand wird erst bei Bedarf gelesen, denn weder das
        // Log-Modul noch die Filterkonfiguration existieren, bevor der Spieler in einer
        // Welt ist.
        _chatFilters = new GameChatFilters(DataManager, GameConfig, Log);
        // [Chat-Puffer] Liest, welches Register das Spiel zeigt, und schaltet es auf den
        // beiden Registertasten ueber ChangeTab um - die spieleigene Funktion, dieselbe,
        // die auch der Registerknopf ausloest. Die Blaetterliste des Plugins folgt genau
        // diesem einen Index, es gibt also genau EINE Vorstellung von "aktuelles
        // Register", und sie gehoert dem Spiel.
        _chatTabs = new ChatTabControl(GameGui, Log);
        // [Chat-Puffer] Die Blaetterliste = die Kanaele, die das AKTIVE Register
        // eingeschaltet hat. Als Praedikat uebergeben und nicht als Liste, damit sie im
        // Moment des Tastendrucks aus den Live-Filterbytes des Spiels beantwortet wird -
        // eine frueher gebaute Liste waere eine zwischengespeicherte Kopie genau des
        // Zustands, dem hier gefolgt werden soll.
        //
        // Die drei Puffer, die keine Kanaele sind (Dialoge, eigene Meldungen, der
        // Sammelpuffer), gehoeren zu keinem Register und werden daher von keinem
        // verborgen.
        _history.BufferOffered = key =>
        {
            // Der "Alles"-Puffer eines Registers gehoert zu genau EINEM Register, wird
            // also von diesem und von keinem anderen angeboten - dafuer ist keine
            // Filterabfrage noetig, der Schluessel traegt die Antwort schon. Zuerst
            // geprueft, weil die beiden Praefixe verschieden sind und nur einer davon
            // die Filterbytes braucht.
            if (key.StartsWith(MessageHistoryService.TabKeyPrefix, StringComparison.Ordinal))
            {
                var active = _chatTabs.ActiveTabIndex;
                if (active < 0) return true;   // Chatlog nicht lesbar: nichts verbergen
                return key == MessageHistoryService.TabKey(active);
            }

            if (!key.StartsWith(MessageHistoryService.ChannelKeyPrefix, StringComparison.Ordinal)) return true;
            var tab = _chatTabs.ActiveTabIndex;
            if (tab < 0) return true;          // Chatlog nicht lesbar: nichts verbergen
            if (_chatFilters.ChannelsInTab(tab, _offeredChannels) != ChatFilterState.Ready)
                return true;
            foreach (var channel in _offeredChannels)
                if (MessageHistoryService.ChannelKey(channel) == key) return true;
            return false;
        };
        _chatReader = new ChatReaderService(ChatGui, _tolk, _config, _history, ObjectTable, Log, _chatFilters,
                                            () => !_config.UseLegacyChatSystem);
        // Der alte Leser haengt an derselben Chat-Quelle. Er archiviert immer und
        // spricht nur, solange das alte System eingeschaltet ist - genau
        // spiegelbildlich zum neuen, so dass zu jedem Zeitpunkt genau EINER von
        // beiden redet.
        _legacyChatReader = new LegacyChatReaderService(ChatGui, _tolk, _config, _legacyHistory, ObjectTable, Log,
                                                        () => _config.UseLegacyChatSystem);
        _chatChannel = new ChatChannelService(_legacyHistory, _tolk, Log);
        // [Chat-Puffer] Direkt nach dem Leser gebaut, dessen Archivweg und dessen Zaehler
        // gelaufener Nachrichten es beide braucht. Es tut nichts, solange das Log-Modul
        // und der Filterzustand nicht lesbar sind - also bis einige Sekunden nach einer
        // Anmeldung, und sofort nach einem Neuladen des Plugins, dem Fall, fuer den es da
        // ist.
        _chatBackfill = new ChatBackfill(_chatReader, _history, _chatFilters, Log);
        _toasts     = new ToastService(ToastGui, TargetManager, _tolk, _config, Log);
        _aoeWarn    = new AoeWarningService(_config, Log);
        _warnVoice  = new WarningVoiceService(_config, Log);
        _combat     = new CombatService(ObjectTable, TargetManager, GameGui, DataManager, _tolk, _config, _history, _aoeWarn, _escape, _warnVoice, _leveEnemies, Log);
        _cooldown   = new CooldownService(ClientState, DataManager, _cue, _tolk, _config, Log);
        _dutyActions = new DutyActionService(DataManager, _tolk, _cue, _config, Log);
        _vitals     = new VitalsService(ObjectTable, _config, Log);
        _heading    = new HeadingService(ObjectTable, _tolk, _config, Log);
        _emote      = new EmoteService(DataManager, ClientState, _tolk, Log);
        _dalamudPlugins = new DalamudPluginsService(PluginInterface, _tolk, Log);
        _tripleTriad = new TripleTriadService(GameGui, _tolk, Log);
        // [Einstellungsmenue] Ein gesprochenes Menue, ueber den Nummernblock bedient.
        // Es speichert ueber dasselbe SavePluginConfig wie die Schaltbefehle, sofort bei
        // jeder Aenderung.
        // _heading: das Umschalten der Himmelsrichtung muss den Dienst neu einnorden,
        // sonst sagt er beim Wiedereinschalten die Richtung an, in die der Spieler
        // ohnehin schon schaut.
        // _chatFilters: der Chat-Abschnitt des Menues IST die Registerliste des Spiels,
        // also fragt er denselben Leser wie der Chat-Router.
        _menu       = new SpokenMenu(_tolk, Log);
        _menuInput  = new MenuInput(KeyState, Log, SpokenMenu.AllKeys());
        _options    = new OptionsMenu(_config, () => PluginInterface.SavePluginConfig(_config),
                                      _tolk, Log, _heading, _chatFilters, _aoeWarn, _warnVoice,
                                      // [Reihenfolge] Die drei Dienste, die die
                                      // sortierbaren Listen fuehren. Alle drei sind
                                      // hier oben schon gebaut.
                                      _navigation, _legacyHistory, _history);

        // ── [Tiefes Gewoelbe] ──────────────────────────────────────────
        // Jede Beschreibung geht durch DeepDungeonText: der Sheet-Text traegt Makros
        // (darunter das Wort, das das jeweilige Gewoelbe fuer eine Ebene benutzt), die
        // ExtractText() wegwirft - der Auswerter des Spiels loest sie auf.
        _deepText    = new DeepDungeonText(SeStringEval, Log);
        // Die ebenenweiten Zustaende. Ein Gewoelbe fuehrt seine Verbote und seine
        // Pomander-Wirkungen auf dem Content-Director, NICHT in der StatusList des
        // Spielers - dafuer braucht es einen eigenen Leser.
        _deepState   = new DeepDungeonState(DataManager, Log, _deepText);
        // Die Ebene selbst - ihre Raeume und ihre Truhen - aus demselben Director.
        _deepRoomMap = new DeepDungeonRoomMap(DataManager, Log);
        _deepFloor   = new DeepDungeonFloor(DataManager, Log, _deepState, _deepRoomMap, ClientState);
        _deepNav     = new DeepDungeonNav(DataManager, Log, _objectNames, _deepFloor, _tolk, _config, ObjectTable);
        // Als Property uebergeben, damit NavigationService seine Signatur behaelt.
        _navigation.DeepDungeon = _deepNav;
        // Der aufgezeichnete Punkt eines Raumes ist der Ursprung seines Moduls in der
        // Layout-Datei und kann in einer Wand liegen; ResolveReachablePoint legt ihn auf
        // eine Stelle, die auch erreichbar ist. Ohne diese Zuweisung wird der Rohpunkt
        // benutzt, und der Lauf endet an der Wand.
        _deepNav.Walk = _autoWalk;
        // Damit ein Raumpunkt auf Netz gelegt werden kann, das der Spieler wirklich
        // erreicht: der Cache-Schluessel von vnavmesh kennt die Ebene nicht, jede Ebene
        // nach der ersten wuerde sonst auf den Waenden der vorigen laufen.
        _deepNav.Mesh = new DeepDungeonMesh(_deepFloor, _autoWalk.Navmesh, Log);
        _deepPanel   = new DeepDungeonPanel(DataManager, Log, _deepState, ObjectTable, _deepText);
        // Der Fokus-Leser benennt die Plaetze, die nur Symbole sind; die Ebene liefert
        // dem Ergebnisschirm seine Gegenprobe. Beide als Property, aus demselben Grund.
        _uiReader.DeepDungeonPanel = _deepPanel;
        _uiReader.DeepDungeonFloor = _deepFloor;

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

        // "trail loeschen <nr>" nimmt eine Nummer - vor dem switch prüfen.
        // Die Nummer ist die aus "/acc trails", also pro Gebiet gezählt.
        if (trimmed.StartsWith("trail ", StringComparison.OrdinalIgnoreCase))
        {
            var arg = trimmed[6..].Trim();
            var parts = arg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 &&
                (parts[0].Equals("del", StringComparison.OrdinalIgnoreCase) ||
                 parts[0].Equals("loesch", StringComparison.OrdinalIgnoreCase) ||
                 parts[0].Equals("löschen", StringComparison.OrdinalIgnoreCase)) &&
                int.TryParse(parts[1], out var number))
            {
                _trails.DeleteTrail(number);
            }
            else
            {
                _tolk.SpeakInterrupt(AccessibilityStrings.TrailCommandHelp);
            }
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
            case "trails":
                _trails.AnnounceTrails();
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
            // Beantwortet "komme ich schon ins Tiefe Gewoelbe?" aus dem Spiel
            // selbst, statt es aus Quest-Wissen zu raten.
            case "deepdungeon":
                ProbeDeepDungeonUnlock();
                break;
            // Aufzug/Plattform: misst, ob man draufsteht und mitfaehrt, statt es
            // aus Geometrie zu raten (Frage des Users 2026-08-19).
            case "lift":
            case "liftprobe":
                _liftProbe.Start();
                break;
            // Steht die Absperrung am Tor gerade? Liest das laufende Layout statt
            // der .lgb, weil QST_-Layer nach Questfortschritt geschaltet werden.
            case "coll":
            case "collprobe":
                _collisionProbe.Dump();
                break;
#endif
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

#if DEBUG
    /// <summary>
    /// Answers "can I enter a deep dungeon yet?" from the game's own unlock state
    /// (UIState.IsInstanceContentUnlocked, ilspycmd 2026-08-14) instead of guessing
    /// from quest knowledge. The four entry contents and their names come from the
    /// offline sheet dump of the same day (ContentFinderCondition, ContentType
    /// "Tiefe Gewölbe"); only the FIRST tier of each dungeon is asked, since that
    /// is the one an entry requires.
    /// </summary>
    private unsafe void ProbeDeepDungeonUnlock()
    {
        (uint Id, string Name)[] entries =
        {
            (60001, "Palast der Toten"),
            (60021, "Himmelssäule"),
            (60031, "Eureka Orthos"),
            (60041, "Pilgers Pfad"),
        };

        var parts = new List<string>();
        foreach (var (id, name) in entries)
        {
            bool open;
            // External game call resolved by signature: report the failure instead
            // of turning it into a silent "locked".
            try
            {
                open = FFXIVClientStructs.FFXIV.Client.Game.UI.UIState.IsInstanceContentUnlocked(id);
            }
            catch (Exception ex)
            {
                Log.Error($"[DeepDungeon] Abfrage für {name} ({id}) fehlgeschlagen: {ex.Message}");
                continue;
            }

            Log.Info($"[DeepDungeon] {name} (Inhalt {id}): freigeschaltet={open}");
            if (open) parts.Add(name);
        }

        var msg = parts.Count > 0
            ? $"Freigeschaltet: {string.Join(", ", parts)}."
            : "Kein Tiefes Gewölbe freigeschaltet.";
        Log.Info($"[DeepDungeon] {msg}");
        _tolk.SpeakInterrupt(msg);
    }
#endif

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
            ("Zur Wegrichtung drehen", _config.KeyFaceWaypoint),
            ("Zu Koordinaten",    _config.KeyGotoCoords),
            ("Koordinaten kopieren", _config.KeyCopyCoords),
            ("Menü vorlesen",  _config.KeyReadUI),
            ("Sprache stopp",  _config.KeySilence),
            ("Kampfstatus",    _config.KeyCombatStatus),
            ("SP-Stand",       _config.KeySpStatus),
            ("Himmelsrichtung an/aus", _config.KeyToggleHeading),
            ("Flächenwarnung an/aus", _config.KeyToggleAoeWarning),
            ("Peil-Ton an/aus", _config.KeyToggleBeacon),
            ("Sonderaktion 1", _config.KeyDutyAction1),
            ("Sonderaktion 2", _config.KeyDutyAction2),
            ("Sonderaktionen ansagen", _config.KeyDutyActionList),
            ("UI-Dump",        _config.KeyDumpUI),
            ("Aktives Fenster", _config.KeyWhereAmI),
            ("Aktionsleiste",  _config.KeyReadHotbar),
            ("Inventar",       _config.KeyReadInventory),
            ("Gil",            _config.KeyReadGil),
            ("Stufe",          _config.KeyLevelExp),
            ("Erholungsbonus", _config.KeyRestedStatus),
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
            // [Chat-Puffer] Mit in der Konfliktpruefung, damit ein Patch, der eine
            // dieser Kombis belegt, im Keybind-Dump auffaellt statt im Spiel.
            ("Nachlese Anfang", _config.KeyChatReadOldest),
            ("Nachlese Ende",   _config.KeyChatReadNewest),
            ("Chat-Registerkarte zurück", _config.KeyChatTabPrev),
            ("Chat-Registerkarte vor",    _config.KeyChatTabNext),
            ("Einstellungen",   _config.KeyOptionsMenu), // [Einstellungsmenue]
            ("Plugin-Liste weiter",  _config.KeyPluginsNext),
            ("Plugin-Liste zurück",  _config.KeyPluginsPrev),
            ("Plugin-Einstellungen", _config.KeyPluginsConfig),
            ("Kartenspiel Brett", _config.KeyReadBoard),
            ("Kartenspiel Hand",  _config.KeyReadHand),
            ("Spur aufzeichnen",  _config.KeyRecordTrail),
            ("Aufgabenliste",     _config.KeyReadTasks),
        })
        {
            var parsed = ParseKeySpec(keyName);
            if (parsed.Vk >= 0)
                keys.Add((function, keyName, parsed.Vk, parsed.Ctrl, parsed.Shift, parsed.Alt));
        }
        return keys;
    }

    /// <summary>
    /// Die EINE Tabelle, die einen Konfigurationsnamen ("Pos1") auf seinen
    /// Windows-Tastencode abbildet.
    ///
    /// <para>
    /// Hier stand bis 2026-08-24 eine ZWEITE, handgeschriebene Kopie, und die war
    /// unvollstaendig: sie kannte als Buchstaben nur N, H und L und kein Pos1.
    /// <see cref="ParseKeySpec"/> schlug in dieser Kopie nach, <see cref="KeyNames"/>
    /// wurde nur fuer das Einstellungsmenue benutzt - mit dem Ergebnis, dass
    /// "Strg+F" (Tiefes Gewoelbe), "Umschalt+Pos1" und "Alt+Pos1" beim Laden als
    /// "Unbekannte Tastenangabe" verworfen wurden und im Spiel einfach nichts taten.
    /// Der Kommentar in KeyNames.cs behauptete schon damals, ParseKeySpec lese dort
    /// nach; jetzt stimmt das wieder.
    /// </para>
    ///
    /// <para>
    /// WARUM DIE DOPPELUNG TOEDLICH WAR und nicht bloss unschoen: die Tabelle
    /// entscheidet zweimal. <see cref="UpdateKeyEdges"/> verfolgt NUR die Tasten
    /// darin, und <see cref="ParseKeySpec"/> loest nur Namen daraus auf. Eine Taste,
    /// die fehlt, hat also weder eine Flanke noch einen Code - sie kann gar nicht
    /// ausloesen, und der Spieler hoert kein Wort darueber.
    /// </para>
    ///
    /// <para>
    /// Warum eine bestimmte Taste gewaehlt wurde (welche das Spiel belegt, welche
    /// der Keybind-Dump frei zeigt), steht bei der jeweiligen Belegung in
    /// <see cref="Configuration"/> - nicht hier. Diese Tabelle kennt nur Namen und
    /// Codes und darf ruhig mehr Tasten fuehren, als belegt sind.
    /// </para>
    /// </summary>
    private static Dictionary<string, int> KeyNameToVK => KeyNames.NameToVk;

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
    /// Turns the player towards the walk guide's next waypoint and takes the key
    /// away from the game.
    /// <para>
    /// Bare NUMPAD5 is CAMERA_FOCUS in the keybind dump. The user chose to give
    /// that up (it is purely visual) because NUMPAD5 carries the raised dot and
    /// is found blind. Since the binding stays live in the game, the key is
    /// swallowed like the skill menu does it - otherwise every turn would also
    /// recentre the camera and fight the direction just set.
    /// </para>
    /// </summary>
    private void HandleFaceWaypointKey()
    {
        if (!IsJustPressed(_config.KeyFaceWaypoint)) return;

        _navigation.FaceGuideDirection();

        const int vkNumpad5 = 0x65;
        var key = (Dalamud.Game.ClientState.Keys.VirtualKey)vkNumpad5;
        if (KeyState.IsVirtualKeyValid(vkNumpad5) && KeyState[key]) KeyState[key] = false;
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
    /// [Tiefes Gewoelbe] Sagt, in welchem Gewoelbe der Spieler ist, auf welcher Ebene davon, und was
    /// diese Ebene gerade mit ihm macht.
    ///
    /// DIE EBENE ist die eine Zahl, in der der ganze Lauf gemessen wird, und das Spiel
    /// nennt sie nur beilaeufig. DIE WIRKUNGEN stehen NICHT in der StatusList des
    /// Spielers - ein Gewoelbe fuehrt seine ebenenweiten Zustaende, seine Verbote und
    /// seine Pomander-Wirkungen auf dem Content-Director, und ein Effekt-Leser, der nur
    /// die StatusList kennt, sieht davon nichts (siehe DeepDungeonState fuer die
    /// Messung, die das belegt).
    ///
    /// Beides auf einer Taste, weil es eine Frage ist: "wo stecke ich, und was liegt
    /// gerade auf mir". Laeuft nichts, wird auch nichts angehaengt - die Taste antwortet
    /// dann einfach mit der Ebene, statt "keine Wirkungen" zu sagen.
    ///
    /// Unterbrechend, wie jede andere Antwort auf eine gedrueckte Taste: der Spieler
    /// wartet darauf. Ausserhalb eines Gewoelbes sagt sie das, statt still zu bleiben -
    /// siehe AccessibilityStrings.DeepFloorOutside.
    /// </summary>
    private void AnnounceDeepFloor()
    {
        var line = _deepFloor.DescribeFloor();
        if (line == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.DeepFloorOutside);
            return;
        }

        // Nur die NAMEN, und die sind alle vom Spiel. Die Beschreibung dazu steht im
        // Fenster Charakterinfo an dem Platz, zu dem sie gehoert (Strg+F10 dort).
        var effects = _deepState.CollectEffects();
        if (effects.Count > 0)
        {
            var rows = new List<string>(effects.Count);
            foreach (var effect in effects)
                rows.Add(AccessibilityStrings.DeepEffectRow(effect.Kind, effect.Name));
            line += ". " + string.Join(", ", rows);
        }

        _tolk.SpeakInterrupt(line);
    }

    /// <summary>
    /// Reads out the task list of whatever is running - levequest, duty, FATE.
    /// These are the lines a sighted player has permanently in the corner of the
    /// screen; without them a blind player cannot tell what the content is
    /// asking for, nor how far they have got (user request 2026-08-18).
    ///
    /// On a key and never automatic, on purpose: the list changes on every kill,
    /// and a line spoken at each change would talk over the fight.
    ///
    /// The line TEXT is the game's own wording and is passed through untouched -
    /// only the progress behind it is put into words here.
    /// </summary>
    private void AnnounceActiveTasks()
    {
        var tasks = _directorTodos.GetActiveTasks();
        if (tasks.Count == 0)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.NoActiveTasks);
            return;
        }

        var parts = new List<string>();
        foreach (var task in tasks)
        {
            parts.Add(task.Title.Length > 0
                ? AccessibilityStrings.TasksOf(task.Title)
                : AccessibilityStrings.TasksHeading);

            // The objective only when it adds something the lines do not already
            // say - on many leves it repeats the single task line word for word.
            if (task.Objective.Length > 0 && !task.Lines.Any(l => l.Text == task.Objective))
                parts.Add($"{task.Objective}.");

            foreach (var line in task.Lines)
                parts.Add($"{line.Text}{line.Detail}{(line.Complete ? AccessibilityStrings.TodoDone : string.Empty)}.");
        }

        var text = string.Join(" ", parts);
        Log.Info($"[Aufgaben] {text}");
        _tolk.SpeakInterrupt(text);
    }

    /// <summary>
    /// Turns the turn-by-turn compass announcement on or off and speaks the new
    /// state. When switching ON, the current facing is spoken once as immediate
    /// confirmation and the service is re-baselined so it does not echo the same
    /// direction again on its next frame.
    /// </summary>
    /// <summary>
    /// Peil-Ton an/aus. Er laeuft, sobald ein Ziel gewaehlt ist, und ist damit
    /// die einzige Dauergeraeuschquelle des Mods - eine Taste, die ihn sofort
    /// stumm schaltet, gehoert zwingend dazu.
    /// </summary>
    private void ToggleTargetBeacon()
    {
        _config.TargetBeaconEnabled = !_config.TargetBeaconEnabled;
        PluginInterface.SavePluginConfig(_config);
        // Sofort still, ohne auf den naechsten Frame zu warten: wer ihn
        // abschaltet, will Ruhe jetzt.
        if (!_config.TargetBeaconEnabled) _beacon.Stop();
        _tolk.SpeakInterrupt(_config.TargetBeaconEnabled
            ? AccessibilityStrings.TargetBeaconOn
            : AccessibilityStrings.TargetBeaconOff);
    }

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
    /// silences the tone on the next frame (UpdateEnemyCastWarnings honours the flag).
    /// Since 2026-08-19 this switch also governs the spoken "du stehst drin" warning,
    /// which is the same geometry put into words - the tone alone never said how much
    /// time was left.
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

    /// <summary>
    /// [Chat-Puffer] Setzt das SPIEL auf das vorige/naechste Chat-Register.
    ///
    /// Es laeuft ueber <see cref="GameChatFilters.Tabs"/> und nicht ueber den rohen
    /// Indexbereich, denn diese Liste sind die Register, die es GIBT - ein Register sagt
    /// durch einen leeren Namen, dass es nicht existiert, und ein blosser Indexlauf
    /// bliebe auf den leeren Plaetzen stehen, die das Spiel dazwischen fuehrt (gemessen:
    /// fuenf Plaetze, drei davon benannt).
    ///
    /// Die Ansage macht <see cref="FollowChatTab"/> im naechsten Frame und nicht diese
    /// Methode, damit ein mit der Maus erreichtes Register und ein mit dieser Taste
    /// erreichtes von demselben Code angesagt werden und sich nie widersprechen koennen.
    /// </summary>
    private void SwitchChatTab(int dir)
    {
        var tabs = _chatFilters.Tabs;
        if (tabs.Count == 0)
        {
            // Vor dem Betreten einer Welt normal, danach ein echter Fehler - das Log
            // unterscheidet die beiden, und Stille waere so oder so das eine Ergebnis,
            // das der Spieler nicht melden koennte.
            _tolk.SpeakInterrupt(AccessibilityStrings.ChatTabUnavailable);
            Log.Info("[ChatTab] Noch keine benannten Registerkarten - nicht umgeschaltet.");
            return;
        }

        var current = _chatTabs.ActiveTabIndex;
        var at = 0;
        for (var i = 0; i < tabs.Count; i++)
            if (tabs[i].Index == current) { at = i; break; }

        var next = tabs[(at + dir + tabs.Count) % tabs.Count];
        if (!_chatTabs.SwitchTo(next.Index))
            _tolk.SpeakInterrupt(AccessibilityStrings.ChatTabUnavailable);
    }

    /// <summary>
    /// [Chat-Puffer] Setzt den Blaetter-Cursor auf den naechsten/vorigen Puffer und sagt
    /// der Nachlese dabei, IN WELCHEM REGISTER sie blaettert.
    ///
    /// Das Register wird genauso ermittelt wie in <see cref="FollowChatTab"/> - der
    /// spieleigene <c>TabIndex</c>, benannt aus <see cref="GameChatFilters.Tabs"/> -,
    /// damit die beiden sich nie darueber uneinig sein koennen, in welchem Register der
    /// Spieler steht. Ist eines von beiden nicht lesbar, bekommt die Nachlese -1 und
    /// faellt auf ihre alte Formulierung zurueck: "dieses Register ist leer" waere sonst
    /// eine Behauptung ueber ein Register, das niemand lesen konnte.
    /// </summary>
    private void SwitchChatBuffer(int dir)
    {
        var index = _chatTabs.ActiveTabIndex;
        var name = string.Empty;
        if (index >= 0)
            foreach (var tab in _chatFilters.Tabs)
                if (tab.Index == index) { name = tab.Name; break; }

        _history.SwitchCategory(dir, name.Length > 0 ? index : -1, name);
    }

    /// <summary>
    /// [Chat-Puffer] Sagt das Chat-Register des Spiels an, sobald es sich aendert, und
    /// setzt den Blaetter-Cursor hinein.
    ///
    /// Bewusst vom spieleigenen <c>TabIndex</c> getrieben und nicht vom Tastenhandler des
    /// Plugins: was auch immer das Register bewegt hat - die Taste des Plugins, ein
    /// Mausklick, oder der Gamepad-Registerlauf, falls er den Chatlog erreicht -, der
    /// Spieler hoert denselben Satz und die Pufferliste folgt. Eine Quelle der Wahrheit,
    /// und es ist die des Spiels.
    ///
    /// Der erste Wert einer Sitzung wird STILL uebernommen. Der Chatlog steht erst einige
    /// Sekunden nach der Anmeldung, und bei jedem Charakterlogin ungefragt "Allgemein" zu
    /// sagen waere Laerm, der an nichts haengt, was der Spieler getan hat.
    /// </summary>
    private void FollowChatTab()
    {
        // Die Registeransage gehoert dem neuen System. Im alten wuerde sie ueber
        // Puffer reden, die dort niemand blaettert.
        if (_config.UseLegacyChatSystem) return;

        _chatTabs.Poll();

        var index = _chatTabs.ActiveTabIndex;
        if (index < 0 || index == _announcedChatTab) return;

        var first = _announcedChatTab == int.MinValue;
        _announcedChatTab = index;
        if (first) return;

        var name = string.Empty;
        foreach (var tab in _chatFilters.Tabs)
            if (tab.Index == index) { name = tab.Name; break; }

        // Kein Name heisst, dass die Filterseite dem Addon noch nicht gefolgt ist. Den
        // rohen Index zu sagen waere hier schlechter als zu schweigen: der naechste Frame
        // hat den Namen, und diese Methode laeuft dann erneut.
        if (name.Length == 0) { _announcedChatTab = int.MinValue; return; }

        _history.EnterTab(index, name);
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        UpdateKeyEdges();
#if DEBUG
        // UNGEGATTERT, mit Absicht: die Sonde sagt am Ende der Messung "fertig",
        // und dieser letzte Aufruf faellt genau dann weg, wenn man ihn an
        // IsTracking haengt. Kostet nichts - die Methode steigt in der ersten
        // Zeile wieder aus, solange nichts laeuft.
        _liftProbe.Update();
        // Laeuft immer mit: der Puffer muss GEFUELLT sein, bevor der Wechsel kommt -
        // ein Schalter, den man vorher druecken muesste, waere hier nutzlos.
        _zoneExitProbe.Update();
#endif

        // Charaktererstellung: kostet nichts ausserhalb des Aussehen-Schritts -
        // die Methode steigt sofort wieder aus, wenn dessen Addon nicht sichtbar ist.
        _charaMake.Update();

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

        // [Chat-Puffer] Dem Register des Spiels folgen und, einmal je Sitzung, den vom
        // Spiel gespeicherten Verlauf nachtragen. Beides vor der Tastenauswertung, damit
        // eine Blaettertaste in diesem Frame schon die richtige Liste sieht.
        FollowChatTab();
        _chatBackfill.Update();

        // [Einstellungsmenue] Ein offenes Menue besitzt die Tastatur. Ein Textfeld
        // besitzt sie ebenfalls - dann koennen keine Tasten gelesen werden, das Menue
        // darf also nicht offen auf eine warten, die nie ankommt.
        if (textInputActive)
        {
            if (_menu.IsOpen) _menu.Close();
        }
        else
        {
            _menuInput.Poll();
            if (_menu.HandleKeys(_menuInput)) return;
        }

        if (IsJustPressed(_config.KeyHelp))          _uiReader.AnnounceContextHelp();
        if (IsJustPressed(_config.KeyNextObject))    _navigation.CycleObject(+1);
        if (IsJustPressed(_config.KeyPrevObject))    _navigation.CycleObject(-1);
        if (IsJustPressed(_config.KeyCategory))
        {
            _navigation.NextCategory();
            // ENTFERNT 2026-08-19: hier hing eine Sonde, die bei JEDEM
            // Kategorie-Wechsel den ganzen Quest-Tracker ins Log schrieb (897
            // Zeilen in einer Spielsitzung). Sie sollte den Aufbau des Trackers
            // fuer den Ziel-Leser klaeren - der steht (QuestMarkerService liest
            // _ToDoList, DirectorTodoService die Aufgabenliste), also faellt sie
            // nach der Sonden-Konvention weg. ProbeAddonTexts selbst bleibt: sie
            // ist das Werkzeug fuer die naechste unbekannte Oberflaeche.
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
            // The v5.74 walk takes no height-is-guess hint - that belonged to
            // the reworked routing which has been rolled back.
            else switch (TryResolveMarkerDestination(out var pos, out var name, out var stop, out _, out var isTransition))
            {
                case MarkerResolve.Resolved: _autoWalk.ToggleToPosition(pos, name, stop, isTransition); break;
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
        if (IsJustPressed(_config.KeyReadUI))
        {
            // Im Schritt Aussehen liest diese Taste das GANZE Aussehen zurueck. Das
            // ist genau das, was das Spiel selbst nirgends anbietet: die Werte liegen
            // in zwanzig Waehler-Fenstern und keiner davon ist Text. Keine neue Taste
            // und keine neue Einstellung - "aktuelles Menue vorlesen" heisst hier eben
            // das.
            if (_charaMake.IsActive) _charaMake.ReadSummary();
            else                     _uiReader.ReadCurrentFocus();
        }
        if (IsJustPressed(_config.KeySilence))       _tolk.Silence();
        if (IsJustPressed(_config.KeyCombatStatus))  _combat.AnnounceStatus();
        if (IsJustPressed(_config.KeyDeepFloor))     AnnounceDeepFloor();
        if (IsJustPressed(_config.KeySpStatus))      _combat.AnnounceGatheringPoints();
        if (IsJustPressed(_config.KeyToggleHeading)) ToggleHeading();
        if (IsJustPressed(_config.KeyToggleAoeWarning)) ToggleAoeWarning();
        if (IsJustPressed(_config.KeyToggleBeacon))     ToggleTargetBeacon();
        // Sonderaktionen des Auftrags. Auf dem Nummernblock, weil sie mitten im
        // Kampf im richtigen Moment kommen muessen.
        if (IsJustPressed(_config.KeyDutyAction1))    _dutyActions.Execute(1);
        if (IsJustPressed(_config.KeyDutyAction2))    _dutyActions.Execute(2);
        if (IsJustPressed(_config.KeyDutyActionList)) _dutyActions.Announce();
        if (IsJustPressed(_config.KeyReadHotbar))    _hotbar.ReadHotbar();
        if (IsJustPressed(_config.KeyReadInventory))
        {
            // In a hand-over (Request) window Strg+F3 reads the eligible items
            // from the grid; otherwise it reads the whole carried inventory.
            if (!_uiReader.TryAnnounceHandOver()) _inventoryReader.ReadInventory();
        }
        if (IsJustPressed(_config.KeyReadGil))       _inventoryReader.AnnounceGil();
        if (IsJustPressed(_config.KeyLevelExp))      _combat.AnnounceLevelExp();
        if (IsJustPressed(_config.KeyRestedStatus))  _combat.AnnounceRestedStatus();
        if (IsJustPressed(_config.KeyReadTasks))     AnnounceActiveTasks();
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
        HandleFaceWaypointKey();
        if (IsJustPressed(_config.KeyReadLootRolls)) _lootRolls.AnnounceOpenRolls();
        if (IsJustPressed(_config.KeyFocusLootRolls)) _lootRolls.FocusRollWindow();
        HandleSkillMenuKeys();
        // DIESELBEN TASTEN, ZWEI SYSTEME. Die vier gewohnten Tasten gibt es in
        // beiden - im alten fuehren sie durch die festen Kategorien, im neuen
        // durch die Puffer der Spielregister. Welche Bedeutung gilt, entscheidet
        // der Schalter im Optionsmenue.
        var legacyChat = _config.UseLegacyChatSystem;
        if (IsJustPressed(_config.KeyChatCatPrev))
        {
            if (legacyChat) _legacyHistory.SwitchCategory(-1); else SwitchChatBuffer(-1);
        }
        if (IsJustPressed(_config.KeyChatCatNext))
        {
            if (legacyChat) _legacyHistory.SwitchCategory(+1); else SwitchChatBuffer(+1);
        }
        if (IsJustPressed(_config.KeyChatReadOlder))
        {
            if (legacyChat) _legacyHistory.ReadOlder(); else _history.ReadOlder();
        }
        if (IsJustPressed(_config.KeyChatReadNewer))
        {
            if (legacyChat) _legacyHistory.ReadNewer(); else _history.ReadNewer();
        }
        // DIESE VIER GIBT ES NUR IM NEUEN SYSTEM: Sprung an Anfang/Ende eines
        // Puffers und die Registertasten des Spiels. Im alten sagen sie das
        // laut, statt einfach nichts zu tun - eine Taste, die stumm bleibt,
        // klingt fuer einen blinden Spieler wie eine kaputte Taste.
        if (IsJustPressed(_config.KeyChatReadOldest))
        {
            if (legacyChat) _tolk.SpeakInterrupt(AccessibilityStrings.ChatKeyOnlyInNewSystem);
            else            _history.ReadOldest();
        }
        if (IsJustPressed(_config.KeyChatReadNewest))
        {
            if (legacyChat) _tolk.SpeakInterrupt(AccessibilityStrings.ChatKeyOnlyInNewSystem);
            else            _history.ReadNewest();
        }
        // [Chat-Puffer] Register des Spiels umschalten. Die Ansage macht FollowChatTab
        // im naechsten Frame, nicht diese Zeile - siehe dort.
        if (IsJustPressed(_config.KeyChatTabPrev))
        {
            if (legacyChat) _tolk.SpeakInterrupt(AccessibilityStrings.ChatKeyOnlyInNewSystem);
            else            SwitchChatTab(-1);
        }
        if (IsJustPressed(_config.KeyChatTabNext))
        {
            if (legacyChat) _tolk.SpeakInterrupt(AccessibilityStrings.ChatKeyOnlyInNewSystem);
            else            SwitchChatTab(+1);
        }
        // [Einstellungsmenue] Zweiter Druck schliesst wieder.
        if (IsJustPressed(_config.KeyOptionsMenu))
        {
            if (_menu.IsOpen) _menu.Close();
            else              _menu.Open(_options.Build());
        }
        if (IsJustPressed(_config.KeyReadBoard))     _tripleTriad.ReadBoard();
        if (IsJustPressed(_config.KeyReadHand))      _tripleTriad.ReadHand();
        if (IsJustPressed(_config.KeyRecordTrail))   _trails.ToggleRecording();
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
        // Sonderaktionsleiste eines Auftrags: sagt an, wenn sie auftaucht. Das
        // Spiel bietet sie NUR per Mausklick an, ein blinder Spieler erfaehrt
        // sonst nie, dass sie da ist.
        _dutyActions.Update();
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
        // Laeuft NACH dem Auto-Lauf und unabhaengig von ihm: der Impuls beginnt
        // genau dann, wenn der Lauf zu Ende ist.
        _transitions.Update();
        // Has to run OUTSIDE the walk: the turn happens at the moment the walk
        // ends, so checking whether it stuck belongs to the frames after that.
        FacingService.Tick(ObjectTable.LocalPlayer);
        // Records the player's own line while a trail recording runs (see TrailService).
        _trails.Update();
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
            // [Chat-Puffer] HIER STAND ChatChannelService.TrySwitchToBrowsedChannel -
            // Enter stellte den SENDEKANAL auf den Puffer um, in dem der Spieler gerade
            // las. Das ist mit den neuen Puffern nicht mehr abbildbar und deshalb
            // entfallen: ein Puffer ist jetzt eine LogFilter-Zeile des Spiels, also ein
            // EMPFANGSFILTER, und daraus laesst sich kein Sendekanal ableiten. Die alte
            // Zuordnung ging nur, solange die Puffer eine eigene, feste Aufzaehlung des
            // Plugins waren (MessageHistoryService.Category), und genau die ersetzt
            // dieser Beitrag. Eine erfundene Zuordnung waere schlimmer als keine: sie
            // wuerde Nachrichten in den falschen Kanal schicken.
            //
            // Das Umschalten des Sendekanals hat das Spiel selbst auf belegbaren Tasten:
            // CMD_SAY, CMD_PARTY, CMD_LINKSHELL_1..8, CMD_REPLY, jeweils auch als
            // _ALWAYS-Variante.
            //
            // IM ALTEN CHATSYSTEM GIBT ES DIE ZUORDNUNG ABER WEITERHIN, denn dort
            // sind die Puffer nach wie vor die feste Kategorienliste, aus der die
            // drei gemessenen Sendekanaele (Sagen 1, Gruppe 2, Freie Gesellschaft 6)
            // und das Fluesterziel abgeleitet werden - genau die Grundlage, die der
            // Absatz oben dem neuen System zu Recht abspricht. Deshalb steht die
            // Zeile hier wieder, nur eben an den Schalter gebunden.
            //
            // Nie, waehrend die Eingabezeile schon offen ist: dort SENDET Enter, und
            // den Kanal darunter zu verschieben wuerde die Nachricht fehlleiten.
            if (!_uiReader.IsChatInputActive())
            {
                if (_config.UseLegacyChatSystem)
                {
                    _chatChannel.TrySwitchToBrowsedChannel();
                }
                else
                {
                    // IM NEUEN SYSTEM NUR DAS FLUESTERN, und das aus einem Grund:
                    // das Ziel steht als Nutzlast in der gelesenen Nachricht selbst
                    // (Name + Heimatwelt), es wird also gelesen und nicht aus einem
                    // Empfangsfilter abgeleitet. Fuer die uebrigen Puffer gilt der
                    // Einwand aus PR #5 unveraendert - ein LogFilter-Eintrag sagt
                    // nichts darueber, wohin eine Antwort gehen soll.
                    _chatChannel.TryAnswerBrowsedTell(_history.CurrentTellPartner, _history.LastActivity);
                }
            }
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
    private MarkerResolve TryResolveMarkerDestination(out Vector3 position, out string name, out float stopRange,
                                                      out bool heightIsGuess, out bool isZoneTransition)
    {
        // Vorbelegen: jeder Rueckgabepfad muss den Wert setzen, und nur der
        // Uebergangs-Zweig weiter unten setzt ihn auf true.
        isZoneTransition = false;
        position = default;
        name = string.Empty;
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
            // A LEVE goal is a search AREA, so its middle is the destination, not
            // its rim: the enemies of a "Such am Zielort" leve only appear once
            // the player moves around inside the circle, and the circle is wide
            // (r=50 measured on the La Noscea leves 2026-08-18). Stopping at the
            // rim left the player 50 m short with nothing to steer by, and a
            // second Numpad 3 answered "angekommen" right away (log 22:41:25).
            // Ordinary quest markers keep the rim stop: there the circle means
            // "the objective is somewhere in here", and its middle is often a
            // spot the player has no reason to stand on.
            stopRange = quest.Radius > 0f && quest.Role != QuestMarkerRole.LeveObjective
                ? MathF.Max(_config.AutoWalkPlaceStopRange, quest.Radius)
                : _config.AutoWalkPlaceStopRange;
            return MarkerResolve.Resolved;
        }

        if (place != null)
        {
            // Map markers are 2D - resolve the walkable height via the
            // navmesh first (player height as search origin).
            var playerY = ObjectTable.LocalPlayer?.Position.Y ?? 0f;
            // Fishing spots are water CENTRES: snap to the nearest bank (wide
            // search) so the player lands at the water, not on a floor the
            // generic 10 m snap happens to find. Fall back to the generic
            // resolver if no bank is found (e.g. vnavmesh not ready).
            // Named places are the CENTRE of a map symbol, not a spot to stand on:
            // a room marker sits in the middle of the room, and there stand tables,
            // chairs and pillars. Measured on "Rudererquartier" in Sastasha
            // (log 2026-08-21 22:11): the marker at (-97|64) has background objects
            // 1.1 m away and five ChairMarkers around it - the walk stopped 2.44 m
            // short because the destination itself is not a place one can stand.
            // ResolveReachablePoint asks vnavmesh the stronger question - a point
            // the player can actually GET to - exactly as the deep dungeon already
            // does for its room origins. It falls back to ResolveFloorPoint itself,
            // so a mesh that cannot answer behaves as before.
            // Uebergaenge zielen auf die ECHTE Grenze, nicht auf ihr Kartensymbol.
            // Das Symbol liegt in der Mitte der Grenze, und die Mitte kann weit
            // ausserhalb des begehbaren Netzes liegen: Neu-Gridania -> Tiefer Wald
            // endete 18,6 m davor, waehrend der Rand der Grenze nur 2,0 m entfernt
            // war (Log 2026-08-22, docs/game-api.md). Ohne passende Grenze - Tueren
            // und Instanz-Eingaenge haben keine - bleibt alles wie bisher.
            // Die Grenze ist 30 m breit, und nur ein Teil davon ist ein Durchgang:
            // am Uebergang Neu-Gridania -> Tiefer Wald stehen Faesser und ein
            // Torbauwerk, gemessen mit tools/zone-probe am 2026-08-22. Deshalb
            // bekommt die Grenzsuche vnavmeshs Erreichbarkeitspruefung mit und
            // nimmt den naechsten Punkt, den das Netz auch annimmt.
            var borderPoint = place.IsZoneTransition
                ? _zoneBorders.FindBorderPoint(place.TargetMapId, ObjectTable.LocalPlayer?.Position ?? place.Position,
                                               _autoWalk.ProbeReachable)
                : null;
            var approach = borderPoint ?? place.Position with { Y = playerY };

            var floor   = place.IsWaterSpot
                ? (_autoWalk.ResolveNearestBank(place.Position with { Y = playerY })
                   ?? _autoWalk.ResolveFloorPoint(place.Position with { Y = playerY }))
                : _autoWalk.ResolveReachablePoint(approach);
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
            // Nur echte Zonengrenzen duerfen am Ende angeschoben werden - und nur,
            // wenn wir wirklich die Grenze anzielen. Ohne borderPoint ist das Ziel
            // das Kartensymbol, und das liegt nicht zwingend im Ausloeser.
            isZoneTransition = place.IsZoneTransition && borderPoint != null;
            return MarkerResolve.Resolved;
        }

        // Jagdziel aus dem Browser. Same routing as a quest goal, for the same
        // reason: the monster's home area is a place on the map, and in another
        // zone the only thing worth walking to is the transition that leads
        // there. Zone checked FRESH here - the flag stored at selection time is
        // stale after a teleport.
        var hunt = _navigation.SelectedHuntTarget;
        if (hunt != null)
        {
            // A live specimen in range beats every marker: the habitat marker is
            // the middle of an area, the monster is what the player wants to
            // reach (user request 2026-08-17). Targeting it is part of the
            // answer - a monster is reached in order to be attacked - and once
            // the game holds it as the hard target, MarkerResolve.None hands the
            // walk to the TARGET path, which re-reads the position every frame.
            // That is what makes walking to a patrolling monster work at all;
            // a fixed position would aim at where it stood at key-press time.
            var live = _huntingLog.FindNearestLive(hunt.MonsterName);
            if (live != null)
            {
                var accepted = _navigation.TargetFromBrowser(live);
                Log.Info($"[Jagd] Lebendes '{hunt.MonsterName}' in " +
                         $"{Vector3.Distance(ObjectTable.LocalPlayer?.Position ?? live.Position, live.Position):F1} m, " +
                         $"id={live.GameObjectId:X}, anvisiert={accepted}");
                if (accepted) return MarkerResolve.None;

                // Game refused the target (quest-locked mobs do): walk to the
                // position it was last seen at instead of falling back to the
                // area marker, which would be much further off.
                position = _autoWalk.ResolveFloorPoint(live.Position) ?? live.Position;
                name = hunt.MonsterName;
                stopRange = AutoWalkService.StopRange;
                return MarkerResolve.Resolved;
            }
            _huntingLog.LogNearbyBattleNpcs(hunt.MonsterName);

            if (hunt.MapId != 0 && hunt.MapId != ClientState.MapId)
            {
                var hop = _places.FindFirstHopToMap(hunt.MapId, out _);
                if (hop == null)
                {
                    _tolk.SpeakInterrupt(AccessibilityStrings.HuntingNoRoute(hunt.MonsterName, hunt.ZoneName));
                    return MarkerResolve.Failed;
                }
                var hopY    = ObjectTable.LocalPlayer?.Position.Y ?? 0f;
                var hopWalk = _autoWalk.ResolveFloorPoint(hop.Position with { Y = hopY });
                if (hopWalk == null)
                {
                    _tolk.SpeakInterrupt(AccessibilityStrings.NoWalkablePointAt(hop.Name));
                    return MarkerResolve.Failed;
                }
                position = hopWalk.Value;
                name = hop.Name;
                stopRange = _config.AutoWalkTransitionStopRange;
                heightIsGuess = true;
                return MarkerResolve.Resolved;
            }

            // 40 of the 647 habitats are dungeon areas the map never marks. Say
            // so instead of walking somewhere arbitrary.
            if (hunt.Position is not { } area)
            {
                _tolk.SpeakInterrupt(AccessibilityStrings.HuntingAreaUnknown(hunt.MonsterName, hunt.AreaName));
                return MarkerResolve.Failed;
            }

            var areaY    = ObjectTable.LocalPlayer?.Position.Y ?? 0f;
            var areaWalk = _autoWalk.ResolveFloorPoint(area with { Y = areaY });
            if (areaWalk == null)
            {
                _tolk.SpeakInterrupt(AccessibilityStrings.NoWalkablePointNear(hunt.AreaName));
                return MarkerResolve.Failed;
            }
            position = areaWalk.Value;
            // Both parts are spoken: the monster is what the player picked, the
            // area is where they are actually being taken - the marker is the
            // centre of the area, not the monster.
            name = hunt.AreaName.Length > 0 ? $"{hunt.MonsterName}, {hunt.AreaName}" : hunt.MonsterName;
            heightIsGuess = true;
            stopRange = _config.AutoWalkPlaceStopRange;
            return MarkerResolve.Resolved;
        }

        // Inhalts-Eingang aus der Kategorie "Alle Inhalte". Dieselbe Wegfuehrung
        // wie beim Quest-Ziel und aus demselben Grund: die Tuer ist ein fester Ort
        // auf der Karte, und in einer anderen Zone ist das einzig Sinnvolle der
        // Uebergang, der dorthin fuehrt. Zone FRISCH geprueft - das Merkmal aus
        // dem Moment der Auswahl ist nach einem Teleport veraltet.
        var duty = _navigation.SelectedDutyEntrance;
        if (duty != null)
        {
            if (duty.TerritoryTypeId != ClientState.TerritoryType)
            {
                var hop = _places.FindFirstHopToMap(duty.MapId, out _);
                if (hop == null)
                {
                    _tolk.SpeakInterrupt(AccessibilityStrings.DutyNoRouteTo(duty.Name, duty.ZoneName));
                    return MarkerResolve.Failed;
                }
                var hopY    = ObjectTable.LocalPlayer?.Position.Y ?? 0f;
                var hopWalk = _autoWalk.ResolveFloorPoint(hop.Position with { Y = hopY });
                if (hopWalk == null)
                {
                    _tolk.SpeakInterrupt(AccessibilityStrings.NoWalkablePointAt(hop.Name));
                    return MarkerResolve.Failed;
                }
                position = hopWalk.Value;
                name = hop.Name;
                stopRange = _config.AutoWalkTransitionStopRange;
                heightIsGuess = true;
                return MarkerResolve.Resolved;
            }

            // In dieser Zone: die Tuer steht mit VOLLER Hoehe im Level-Sheet, die
            // Hoehe ist also nicht geraten (anders als bei jedem Kartenmarker).
            // ResolveFloorPoint bleibt trotzdem davor - es setzt den Punkt auf die
            // begehbare Flaeche, falls die Sheet-Stelle knapp daneben liegt.
            position = _autoWalk.ResolveFloorPoint(duty.Position) ?? duty.Position;
            name = duty.Name;
            // Interaktionsreichweite wie bei einem Objekt: der Spieler will die
            // Tuer benutzen, nicht in ihrer Naehe stehenbleiben.
            stopRange = AutoWalkService.StopRange;
            Log.Info($"[Inhalte] Laufe zu '{duty.Name}' in Zone {duty.TerritoryTypeId} auf {position}.");
            return MarkerResolve.Resolved;
        }

        // Station des Dungeon-Wegs. Sie steht IMMER in der aktuellen Zone - ein
        // Weg gilt fuer genau eine Instanz, und wer sie verlaesst, verliert die
        // Kategorie ohnehin. Deshalb keine Zonenlogik wie bei Tuer oder Quest.
        var dungeonStep = _navigation.SelectedDungeonStep;
        if (dungeonStep != null)
        {
            // Die Hoehe stammt aus der Pfaddatei und ist echt gemessen, nicht wie
            // bei Kartenmarkern geraten. ResolveFloorPoint bleibt trotzdem davor,
            // weil ein aufgezeichneter Punkt in der Luft stehen kann, wenn die
            // Aufnahme im Sprung lag - und dann faende der Lauf nichts.
            position = _autoWalk.ResolveFloorPoint(dungeonStep.Position) ?? dungeonStep.Position;
            var kindWord = AccessibilityStrings.DungeonStepKindWord(dungeonStep.Kind);
            name = dungeonStep.Name.Length > 0 ? dungeonStep.Name
                 : kindWord.Length > 0        ? kindWord
                 : AccessibilityStrings.DungeonWaypointWord;

            // Etwas zum Benutzen will in Reichweite erreicht werden, ein
            // Wegpunkt nur ueberhaupt. Dieselbe Unterscheidung wie zwischen
            // Objekt und Kartenmarker.
            stopRange = dungeonStep.Kind == DungeonStepKind.Waypoint
                ? _config.AutoWalkPlaceStopRange
                : AutoWalkService.StopRange;

            Log.Info($"[Dungeon] Laufe zu Station {dungeonStep.Number} " +
                     $"({dungeonStep.Kind}) auf {position}.");
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

        // Same search as the browser's hunting category uses - one place that
        // knows how a log entry is matched to a monster standing in the world.
        var nearest = _huntingLog.FindNearestLive(monsterName);

        if (nearest == null)
        {
            _huntingLog.LogNearbyBattleNpcs(monsterName);
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

        // Erst die STEUERUNG: von "voellig verkehrt herum" schrittweise auf das
        // Ziel zu, damit man hoert, wie die Schlaege auseinandergehen und der Ton
        // beim Einrasten in den kurzen Quittungston uebergeht. Alles mit der
        // Objekt-Stimme, damit sich nur die Steuerung aendert.
        // Alle Schritte der Steuerung tragen DIESELBE Kennung (1): sie zeigen ein
        // und dasselbe Ziel, an das man sich herandreht.
        _beacon.Start();
        _beacon.Update(180, 60f, BeaconKind.Object, targetKey: 1);                                  // hinten, weit = dunkel und leise
        Framework.RunOnTick(() => _beacon.Update(90, 40f, BeaconKind.Object, targetKey: 1),  delayTicks: 60);  // ganz rechts
        Framework.RunOnTick(() => _beacon.Update(30, 20f, BeaconKind.Object, targetKey: 1),  delayTicks: 120); // fast passend, naeher
        Framework.RunOnTick(() => _beacon.Update(12, 8f,  BeaconKind.Object, targetKey: 1),  delayTicks: 180); // knapp daneben, ruhig
        Framework.RunOnTick(() => _beacon.Update(0, 6f,   BeaconKind.Object, targetKey: 1),  delayTicks: 240); // eingerastet -> Quittung, dann Stille

        // Dann die STIMMEN: jede Zielart einmal, jeweils angesagt und leicht
        // seitlich, damit sie sich vergleichen lassen.
        var kinds = new[]
        {
            BeaconKind.Enemy, BeaconKind.DutyEntrance, BeaconKind.Transition, BeaconKind.Npc,
            BeaconKind.Quest, BeaconKind.Object, BeaconKind.Gathering, BeaconKind.Aetheryte,
        };
        var tick = 300;
        // Jede Stimme bekommt eine EIGENE Kennung, damit sie wie ein neues Ziel
        // klingt: sauberer Anschlag statt Stimmenwechsel mitten im Takt.
        ulong demoKey = 2;
        foreach (var kind in kinds)
        {
            var voice = kind;
            var at = tick;
            var key = demoKey++;
            Framework.RunOnTick(() => _tolk.SpeakInterrupt(AccessibilityStrings.BeaconKindName(voice)), delayTicks: at);
            Framework.RunOnTick(() => _beacon.Update(35, 15f, voice, targetKey: key), delayTicks: at + 24);
            tick += 78;
        }
        Framework.RunOnTick(() => _beacon.Stop(),          delayTicks: tick);
        Framework.RunOnTick(() => _cue.PlayWaypointTone(), delayTicks: tick + 12);
        Framework.RunOnTick(() => _cue.PlayArrivalTone(),  delayTicks: tick + 52);
        tick += 90;

        // HP/MP tones: each case is announced, then the tone plays ~0.4 s later so
        // the label does not step on it. ~90 ticks (~1.5 s) between cases. Percent
        // drives the stereo position; the HP-critical case is <25 % so it pulses.
        // Abstaende relativ zu tick, nicht mehr fest: die Peil-Ton-Vorfuehrung
        // oben ist laenger geworden, und feste Zahlen wuerden mitten in sie
        // hineinsprechen.
        VitalsTestStep(delay: tick,       AccessibilityStrings.SoundTestHpHeal,     health: true,  direction: +1, percent: 80);
        VitalsTestStep(delay: tick + 90,  AccessibilityStrings.SoundTestHpDamage,   health: true,  direction: -1, percent: 55);
        VitalsTestStep(delay: tick + 180, AccessibilityStrings.SoundTestHpCritical, health: true,  direction: -1, percent: 15);
        VitalsTestStep(delay: tick + 270, AccessibilityStrings.SoundTestMpGain,     health: false, direction: +1, percent: 80);
        VitalsTestStep(delay: tick + 360, AccessibilityStrings.SoundTestMpSpend,    health: false, direction: -1, percent: 40);
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
        _legacyChatReader.Dispose();
        _uiReader.Dispose();
        _autoWalk.Dispose();
        _beacon.Dispose();
        _aoeWarn.Dispose();
        _warnVoice.Dispose();
        _cue.Dispose();
        _vitals.Dispose();
        _tolk.Dispose();
    }
}

