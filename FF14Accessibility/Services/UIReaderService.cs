using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FF14Accessibility.Services;

public sealed class UIReaderService : IDisposable
{
    private enum ScreenContext
    {
        None,
        Title,
        TitleMenu,
        ConfigSystem,
    }

    private readonly IAddonLifecycle _addonLifecycle;
    private readonly IGameGui        _gameGui;
    private readonly TolkService     _tolk;
    private readonly IPluginLog      _log;
    private readonly IObjectTable    _objectTable;
    private readonly InventoryService _inventory;
    private readonly GearInfoService _gearInfo;
    private readonly BestiaryService _bestiary;
    private readonly Configuration   _config;
    private readonly List<string>    _titleMenuItems = [];

    // SelectYesno: labels are read fresh from the dialog buttons on open -
    // the addon is reused with different labels (Ok/Abbrechen, Ja/Nein, ...
    // dump 2026-07-09 21:32 showed Ok/Abbrechen for "Neuen Charakter erschaffen?").
    private string _lastYesNoText  = string.Empty;
    private string _ynConfirmLabel = "Ja";
    private string _ynCancelLabel  = "Nein";

    // Men�-Stack: (AddonName, zuletzt gesehener SelectedItemIndex)
    // Oberstes Element = aktuell aktives Men�
    private readonly Stack<(string Name, int Index)> _menuStack = new();

    // Fallback f�r Nicht-Listen-Addons (ReceiveEvent)
    // Dedup PRO Addon: In der Charaktererstellung sind mehrere Addons gleichzeitig
    // sichtbar (CMFSlider, CMFIcon* usw.). Mit einem globalen Zustand ueberschreiben
    // sie sich gegenseitig und jede Ansage feuert jeden Frame erneut (Log 20:33).
    private readonly Dictionary<string, (uint Key, string Text)> _lastFocusByAddon = [];

    // _TitleMenu: zuletzt angesagter Button-Text
    private string _lastTitleMenuText = string.Empty;
    private int _lastTitleMenuIndex = -1;
    // Gesetzt bei InputReceived � Dump wird im n�chsten PostUpdate ausgef�hrt,
    // NACHDEM das Spiel intern den Fokus verschoben hat (nicht davor).
    private bool _dumpOnNextTitleMenuUpdate;
    private ScreenContext _activeScreenContext = ScreenContext.None;

    // ConfigSystem state
    private bool   _csPendingSave;
    private bool   _dumpOnNextConfigSystemUpdate;
    private int    _csLastTabIndex    = -1;
    private string _csLastTabText     = string.Empty;
    private readonly List<(uint NodeId, string Label)> _csTabs = [];

    // Enter on a tab: remember WHICH tab we clicked so the page-change
    // announcement can state a truthful position ("Tab X von 8") - the
    // child-4 detection reported "Tab 8 von 8" on page 1 (log 2026-07-16
    // 16:32:06), so its index must not be spoken unconfirmed.
    private int  _csExpectedTabIdx = -1;
    private long _csTabActivatedAt;

    // [CS-NUM] probe: last snapshot of ConfigSystemNumberArray (25 ints,
    // only [0]=FPS is mapped upstream) - a changing slot on tab switch
    // would be a clean data source for the active tab index.
    private readonly int[] _csNumSnapshot = new int[25];
    private bool _csNumSnapshotValid;


    // Performance-Cache: Addons ohne AtkComponentList nicht erneut suchen
    private readonly HashSet<string> _noListCache = [];

    // List probe state: which index field actually follows the keyboard?
    // SelectedItemIndex demonstrably did NOT move for Journal/SystemMenu
    // (log 2026-07-11 09:44/09:45: menus opened, zero index changes while
    // the user navigated). AtkComponentList carries several candidates
    // (ilspycmd 2026-07-11): SelectedItemIndex@308, HeldItemIndex@312,
    // HoveredItemIndex@316, HoveredItemIndex2@344, HoveredItemIndex3@352
    // plus ListItem.IsHighlighted per renderer slot. We track them ALL and
    // announce whichever moves - the [ListProbe] log lines pin the real one.
    private readonly Dictionary<string, (int Sel, int Hov, int Hov2, int Hov3, int Held, string Hl)> _listIndexState = [];

    // Last announced list row per addon (dedup for the probe-driven announce)
    private readonly Dictionary<string, string> _lastListAnnounce = [];

    // Debug-Probe: Vorherige Node-Flags pro FocusTrack-Addon (addonName ? nodeKey ? flags)
    private readonly Dictionary<string, Dictionary<uint, ushort>> _focusTrackFlags = [];

    // Addons mit eigenem PostSetup-Handler � Universal-OnOpen �berspringt diese
    private static readonly HashSet<string> SpecialSetupAddons =
    [
        "Talk", "TalkSubtitle", "SelectYesno", "SelectString", "SelectIconString",
        "_TextError", "_WideText", "_BattleTalk", "_LocationTitle",
        "LevelUpAnnouncement", "ContentsTutorial", "_ScreenText",
        "ConfigPadCalibration",
        "Title",
        "_TitleMenu",
        "ConfigSystem",
        "TitleDCWorldMap",    // Datenzentrum-Auswahl: eigener Handler
        "TitleDCWorldMapBg",  // nur Hintergrund, nichts anzusagen
    ];

    // Addons, bei denen Universal-Update/ReceiveEvent nicht l�uft
    private static readonly HashSet<string> SpecialUpdateAddons =
    [
        "Talk", "TalkSubtitle", "SelectYesno",
        "_TextError", "_WideText", "_BattleTalk", "_LocationTitle",
        "LevelUpAnnouncement", "ContentsTutorial", "_ScreenText",
        "ConfigPadCalibration",
        "LobbyScreenText",    // fires TimerTick every frame � no useful navigation
        "ConfigSystem",       // eigene Handler + FocusTrack
        "TitleDCWorldMap",    // TimelineActiveLabelChanged feuert alle 300ms � eigener Handler
        "TitleDCWorldMapBg",  // nur Hintergrund
        // Charaktererstellung Volk/Geschlecht: eigene Handler. Der generische
        // Update-Pfad fand hier per Collision-Heuristik dauernd den "Zurueck"-
        // Button (Key=19004) und ueberdeckte die Volk-Ansage (Log 22:17).
        "_CharaMakeRaceGender",
        // Volksstamm: eigene Handler, gleiche Struktur wie RaceGender.
        "_CharaMakeTribe",
        // Beschreibungs-Pane: der generische Scanner sprach den Text mit
        // SpeakInterrupt und schnitt damit die Volk-Ansage ab (Log 2026-07-17
        // 16:56); OnCharaMakeHelpUpdate liest ihn dediziert NACH dem Namen.
        "_CharaMakeHelp",
        // Kommentar-Textfeld beim Aussehen-Speichern: der Scanner wuerde
        // Zaehler ("1/40") und Inhalt bei JEDEM Tastendruck unterbrechend
        // sprechen - das dedizierte Tipp-Echo (OnCharaMakeInputUpdate)
        // uebernimmt. Oeffnungs-Ansage kommt weiter aus OnAnyAddonOpen.
        "CharaMakeDataInputString",
    ];

    // HUD-Anzeigen, deren Text/Fokus sich im normalen Spiel laufend aendert -
    // weder Scanner-Ansagen noch Fokus-Ansagen, kein PushMenu. Alle Eintraege
    // sind [Scan]-log-bewiesene Spam-Quellen (2026-07-11 10:14 + 10:35-10:40):
    // _NaviMap/_DTR (Koordinaten, Serveruhr, vnavmesh-Status), NamePlate
    // (Fokus wechselte alle 2s zwischen NPCs), _TargetInfo* (doppelt die
    // [Nav]-Zielansage), ChatLog* (jede Chat-Zeile), _MiniTalk (Sprechblasen),
    // _ParameterWidget/_Exp (HP/MP/XP-Ticks), _GetAction (Skill-Popup),
    // JournalDetail (unsichtbare Buttons "Entfernen/Neuer Versuch/Karte";
    // Quest-Text liest Strg+F10 dediziert), _CharaSelectTitle (Hinweistext).
    // Chat-Vorlesen kommt spaeter sauber via IChatGui-Event, nicht UI-Scraping.
    private static readonly HashSet<string> HudNoiseAddons =
    [
        "_NaviMap", "_DTR", "NamePlate", "_MiniTalk",
        "_TargetInfo", "_TargetInfoMainTarget", "_TargetInfoBuffDebuff",
        "ChatLog", "ChatLogPanel_0", "ChatLogPanel_1", "ChatLogPanel_2", "ChatLogPanel_3",
        "_ParameterWidget", "_Exp", "_GetAction", "_CharaSelectTitle",
        // Quest windows: muted in the generic path, read by the dedicated
        // OnQuestWindowUpdate handler instead (canvas text, correct timing).
        "JournalDetail", "JournalAccept", "JournalResult",
        // _CastBar id=7 = eigener Zauber-Countdown ("00.63"..."00.02"), feuerte
        // jeden Frame beim Teleportieren (Log 2026-07-11 19:52). Eigene Casts
        // spaeter sauber via LocalPlayer.IsCasting ansagen, nicht per Text-Scan.
        "_CastBar",
        // _CharaSelectReturn traegt nur den "Beenden"-Knopf; beim Neuaufbau der
        // Lobby-Fenster (Login-Dialog auf/zu) meldete der Fokus-Leser ihn jedes
        // Mal ungefragt (Log 2026-07-13 00:58). Gezielte Navigation dorthin sagt
        // weiterhin der globale FocusedNode-Leser an.
        "_CharaSelectReturn",
    ];

    // Seit V4.60/61 im Log dokumentierte, aber noch nicht gefixte Spam-Quellen
    // (STATUS.md Zeilen 96-100), jetzt per V4.62 ueber Configuration.cs
    // abschaltbar statt hart in HudNoiseAddons: _StatusCustom0 (Buff-Leiste)
    // traegt ausschliesslich den Sprint-Countdown als Text-Node ("20s".."1s"
    // im Sekundentakt) - keine Statuseffekt-NAMEN (die kommen nur per
    // Maus-Tooltip, nicht als Node-Text), Vollsperre verliert also keine
    // Information. _FlyText traegt Kampf-Popups ("+Sprint", "700", "(+100 %)"),
    // die CombatService bereits sinnvoll aufbereitet ansagt (HP-Schwellen,
    // Cast-Ansagen) - reine Duplikate ohne eigenen Wert.
    private static readonly HashSet<string> StatusBarSpamAddons = ["_StatusCustom0"];
    private static readonly HashSet<string> FlyTextSpamAddons   = ["_FlyText"];

    /// <summary>
    /// Wie HudNoiseAddons, aber fuer per Configuration.cs abschaltbare
    /// Spam-Quellen (Default: unterdrueckt, siehe SuppressStatusBarSpam /
    /// SuppressFlyTextSpam).
    /// </summary>
    private bool IsSuppressedAddon(string name) =>
        HudNoiseAddons.Contains(name)
        || (_config.SuppressStatusBarSpam && StatusBarSpamAddons.Contains(name))
        || (_config.SuppressFlyTextSpam && FlyTextSpamAddons.Contains(name));

    // Listenlose Addons, bei denen wir per PostUpdate den Fokus tracken
    private static readonly HashSet<string> FocusTrackAddons =
    [
        "_TitleMenu",
        "ConfigSystem",
    ];

    private static readonly string[] SelectStringAddons = ["SelectString", "SelectIconString"];

    private static readonly string[] NotificationAddons =
    [
        "_TextError", "_WideText", "_BattleTalk", "_LocationTitle",
        "LevelUpAnnouncement", "ContentsTutorial", "_ScreenText",
    ];

    private static readonly HashSet<string> YesNoLabels =
        ["Ja", "Nein", "Yes", "No", "??", "???", "Oui", "Non"];

    // Plugin.cs pr�ft dies, um Navigationstasten nur bei aktivem Men� zu verarbeiten
    public bool HasActiveMenu
    {
        get
        {
            if (_menuStack.Count > 0) return true;
            unsafe
            {
                var t = _gameGui.GetAddonByName("Talk");
                if (!t.IsNull && ((AtkUnitBase*)(nint)t)->IsVisible) return true;
                var y = _gameGui.GetAddonByName("SelectYesno");
                if (!y.IsNull && ((AtkUnitBase*)(nint)y)->IsVisible) return true;
            }
            return false;
        }
    }

    public UIReaderService(IAddonLifecycle addonLifecycle, IGameGui gameGui, TolkService tolk, IPluginLog log, IObjectTable objectTable, InventoryService inventory, GearInfoService gearInfo, BestiaryService bestiary, Configuration config)
    {
        _addonLifecycle = addonLifecycle;
        _gameGui        = gameGui;
        _tolk           = tolk;
        _log            = log;
        _objectTable    = objectTable;
        _inventory      = inventory;
        _gearInfo       = gearInfo;
        _bestiary       = bestiary;
        _config         = config;
        RegisterHooks();
    }

    private void RegisterHooks()
    {
        // -- Universal: alle Addons --------------------------------
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup,        OnAnyAddonOpen);
        _addonLifecycle.RegisterListener(AddonEvent.PostUpdate,       OnAnyAddonUpdate);
        _addonLifecycle.RegisterListener(AddonEvent.PostReceiveEvent, OnAnyAddonReceive);
        _addonLifecycle.RegisterListener(AddonEvent.PreFinalize,      OnAnyAddonClose);

        // -- Talk / TalkSubtitle (NPC-Dialoge, Untertitel) ---------
        _addonLifecycle.RegisterListener(AddonEvent.PostUpdate, "Talk", OnTalkUpdate);
        _addonLifecycle.RegisterListener(AddonEvent.PostUpdate, "TalkSubtitle", OnTalkUpdate);

        // -- SelectYesno ------------------------------------------
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup,        "SelectYesno", OnYesNoOpen);
        _addonLifecycle.RegisterListener(AddonEvent.PostReceiveEvent, "SelectYesno", OnYesNoReceive);

        // -- Dialog-Button-Fokus (SelectYesno + JournalResult) -----
        _addonLifecycle.RegisterListener(AddonEvent.PostUpdate, "SelectYesno",   OnDialogButtonProbe);
        _addonLifecycle.RegisterListener(AddonEvent.PostUpdate, "JournalResult", OnDialogButtonProbe);

        // -- Quest-Fenster automatisch vorlesen (Beschreibung/Ziel) -----
        // Content is populated a few frames after PostSetup and changes on page
        // turns, so read on PostUpdate with per-addon dedup.
        _addonLifecycle.RegisterListener(AddonEvent.PostUpdate, "JournalDetail", OnQuestWindowUpdate);
        _addonLifecycle.RegisterListener(AddonEvent.PostUpdate, "JournalAccept", OnQuestWindowUpdate);
        _addonLifecycle.RegisterListener(AddonEvent.PostUpdate, "JournalResult", OnQuestWindowUpdate);

        // -- Bestiarium (Jagdtagebuch, "MonsterNote") -------------------
        // TreeList (Comp CT=TreeList) mit gemischten Zeilen: Rang-Ueberschriften,
        // Monster-Zeilen (Name + Fortschritt "0/3") und Verguetungen. Die
        // Listen-Indizes (Hovered/Selected) bewegen sich hier bei Tastatur-
        // Navigation NICHT (Log 2026-07-12: alle -1), aber der globale FocusedNode
        // wandert. Deshalb dedizierter Leser statt des generischen Fokus-Lesers,
        // der nur "0/10, NEU" (Fortschritt ohne Rang-Namen) wiederholte.
        _addonLifecycle.RegisterListener(AddonEvent.PostUpdate, "MonsterNote", OnMonsterNoteUpdate);

        // Armoury chest: the current category ("Kopf", "Waffe" ...) lives in
        // its title text node - announce it on every tab change, because the
        // category tabs themselves are icon-only (dump 2026-07-16: id=121
        // Text, tabs = Base-wrapped icons without text).
        _addonLifecycle.RegisterListener(AddonEvent.PostUpdate, "ArmouryBoard", OnArmouryBoardUpdate);

        // -- SelectString / SelectIconString ----------------------
        foreach (var name in SelectStringAddons)
            _addonLifecycle.RegisterListener(AddonEvent.PostSetup, name, OnSelectStringOpen);

        // -- Request (NPC-Ablieferung "GEGENSTAND ABLIEFERN") ------
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "Request", OnRequestOpen);

        // -- Titelbildschirm (Logo-Screen vor dem Men�) --------------
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "Title", OnTitleScreenOpen);

        // -- _TitleMenu (das eigentliche Men� mit Buttons) ------------
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup,        "_TitleMenu", OnTitleMenuOpen);
        _addonLifecycle.RegisterListener(AddonEvent.PostReceiveEvent, "_TitleMenu", OnTitleMenuReceive);

        // -- Systemkonfiguration ----------------------------------
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup,        "ConfigSystem", OnConfigSystemOpen);
        _addonLifecycle.RegisterListener(AddonEvent.PreFinalize,      "ConfigSystem", OnConfigSystemClose);
        _addonLifecycle.RegisterListener(AddonEvent.PostReceiveEvent, "ConfigSystem", OnConfigSystemReceive);

        // -- Gamepad-Kalibrierung ---------------------------------

        // -- Datenzentrum-Auswahl
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup,        "TitleDCWorldMap", OnDCWorldMapOpen);
        _addonLifecycle.RegisterListener(AddonEvent.PostUpdate,       "TitleDCWorldMap", OnDCWorldMapUpdate);
        _addonLifecycle.RegisterListener(AddonEvent.PostReceiveEvent, "TitleDCWorldMap", OnDCWorldMapReceive);
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "ConfigPadCalibration", OnPadCalibrationOpen);

        // -- Charaktererstellung: Volk & Geschlecht ---------------
        _addonLifecycle.RegisterListener(AddonEvent.PostUpdate,       "_CharaMakeRaceGender", OnRaceGenderUpdate);
        _addonLifecycle.RegisterListener(AddonEvent.PostReceiveEvent, "_CharaMakeRaceGender", OnRaceGenderReceive);

        // Charaktererstellung: Volksstamm - gleiche Struktur wie RaceGender (F5-Dump 2026-07-10)
        _addonLifecycle.RegisterListener(AddonEvent.PostUpdate,       "_CharaMakeTribe", OnTribeUpdate);
        _addonLifecycle.RegisterListener(AddonEvent.PostReceiveEvent, "_CharaMakeTribe", OnTribeReceive);

        // Charaktererstellung: Beschreibungstext (Volk/Volksstamm) - Dumps
        // 2026-07-17 16:31: der Text steht in _CharaMakeHelp, Text-Node id=4,
        // und wird beim Markieren einer Option live umgeschrieben
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup,  "_CharaMakeHelp", OnCharaMakeHelpOpen);
        _addonLifecycle.RegisterListener(AddonEvent.PostUpdate, "_CharaMakeHelp", OnCharaMakeHelpUpdate);

        // Charaktererstellung: Kommentar-Textfeld beim Aussehen-Speichern
        // (Dump 2026-07-17 17:42: TextInput-Komponente, Zaehler "0/40") -
        // dediziertes Tipp-Echo, generischer Scanner ist dafuer stumm
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup,  "CharaMakeDataInputString", OnCharaMakeInputOpen);
        _addonLifecycle.RegisterListener(AddonEvent.PostUpdate, "CharaMakeDataInputString", OnCharaMakeInputUpdate);

        // Charaktererstellung: Namenseingabe (Vorname/Nachname). Dump
        // 2026-07-17 17:57: zwei sichtbare TextInputs (id=9/7) mit eigenem
        // Label-Text daneben (id=8 "Nachname", id=6 "Vorname"). Handler sagt
        // beim Feldwechsel das Label an + Tipp-Echo pro Feld.
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup,  "_CharaMakeCharaName", OnCharaMakeNameOpen);
        _addonLifecycle.RegisterListener(AddonEvent.PostUpdate, "_CharaMakeCharaName", OnCharaMakeNameUpdate);

        // -- Benachrichtigungen -----------------------------------
        foreach (var name in NotificationAddons)
        {
            _addonLifecycle.RegisterListener(AddonEvent.PostSetup,   name, OnNotification);
            _addonLifecycle.RegisterListener(AddonEvent.PostRefresh, name, OnNotification);
        }
    }

    // -- Universal: Addon ge�ffnet -----------------------------------

    private unsafe void OnAnyAddonOpen(AddonEvent type, AddonArgs args)
    {
        var name = args.AddonName;
        _log.Info($"[Accessibility] Addon: {name}");
        if (SpecialSetupAddons.Contains(name)) return;
        if (IsSuppressedAddon(name)) return;

        var addon = (AtkUnitBase*)(nint)args.Addon;
        if (addon == null) return;

        _noListCache.Remove(name);

        // Invisible at PostSetup: the game pre-creates windows on zone-in
        // (Inventory, ContextMenu family, ...). Announcing their titles
        // ("INVENTAR" 3x at login) and pushing them onto the menu stack
        // (stack depth 7 without any open menu) was pure noise - log
        // 2026-07-11. Park them in _noListCache: the PostUpdate late-list
        // path picks them up WITH announcement once they become visible.
        if (!addon->IsVisible)
        {
            _noListCache.Add(name);
            ScanAddonTexts(name, addon, isInit: true);
            return;
        }

        // Window title first: read from the Window component if present
        // (real windows like inventory/settings carry their title there;
        // dialogs without title yield an empty string and stay silent).
        var title = ReadWindowTitle(addon);
        if (!string.IsNullOrWhiteSpace(title))
        {
            _log.Info($"[Accessibility] {name} Fenstertitel: '{title}'");
            _tolk.SpeakInterrupt(title);
        }

        var list = FindListInAddon(addon);
        if (list != null)
        {
            PushMenu(name, list->SelectedItemIndex);

            // Nur Anzahl + aktuell gew�hlten Eintrag ansagen.
            // Alle Eintr�ge zu iterieren kann bei langen Listen zu Crashes f�hren
            // (uninitialisierte Renderer bei virtuell scrollenden Listen).
            var count = GetListEntryCount(list);
            var sel   = ReadListItemText(list, Math.Max(0, list->SelectedItemIndex));
            var msg   = sel.Length > 0
                ? $"{sel}, {count} Einträge"
                : $"Menü, {count} Einträge";
            // Queue behind the window title instead of cutting it off
            if (string.IsNullOrWhiteSpace(title)) _tolk.SpeakInterrupt(msg);
            else                                  _tolk.Speak(msg);
            return;
        }

        // Generic text cache initialisieren � erm�glicht �nderungs-Erkennung in PostUpdate
        _noListCache.Add(name);
        ScanAddonTexts(name, addon, isInit: true);
        var text = ReadAllTexts(addon);
        if (!string.IsNullOrWhiteSpace(text))
        {
            _tolk.Speak(text);
            // SelectOk (z.B. Server-Warteschlange "hoher Andrang"): Der Fokus
            // springt direkt nach dem Oeffnen auf den Standard-Knopf und schnitt
            // die gerade gesprochene Meldung ab (Log 2026-07-13 00:58, nur
            // "Abbrechen" hoerbar). Gleiche Schutzsperre wie SelectYesno.
            if (name == "SelectOk") _dialogOpenedAt = DateTime.UtcNow;
        }
    }

    // -- Universal: Addon geschlossen --------------------------------

    private void OnAnyAddonClose(AddonEvent type, AddonArgs args)
    {
        var name = args.AddonName;
        _genericTextCache.Remove(name);
        _lastFocusByAddon.Remove(name);
        _lastDialogTexts.Remove(name);
        _listIndexState.Remove(name);
        _lastListAnnounce.Remove(name);
        _lastDialogButtonFlags.Remove(name);
        _lastDialogButtonAnnounce.Remove(name);

        // Reset race/gender dedup so the selection is re-announced on reopen
        if (name == "_CharaMakeRaceGender")
        {
            _lastRaceGender = (string.Empty, string.Empty);
            _lastRaceHover = string.Empty;
            _raceGenderSymbolsLogged = false;
            _previewObjectsLogged = false;
        }

        if (name == "_CharaMakeTribe")
        {
            _lastTribe = string.Empty;
            _lastTribeHover = string.Empty;
        }

        if (name == "_TitleMenu")
            ResetTitleMenuState();

        if (name is "Title" or "_TitleMenu")
            _activeScreenContext = GetCurrentScreenContext();

        // DC-Auswahl geschlossen � State zur�cksetzen
        if (name == "TitleDCWorldMap")
        {
            IsDCMapOpen = false;
            _dcTabPanels.Clear();
            _lastDCText = string.Empty;
            _log.Info("[Accessibility] TitleDCWorldMap geschlossen, DC-State zur�ckgesetzt.");
        }

        if (!_menuStack.Any(m => m.Name == name)) return;

        // Alle Eintr�ge bis einschlie�lich dem geschlossenen Addon entfernen
        while (_menuStack.Count > 0)
        {
            var top = _menuStack.Pop();
            if (top.Name == name) break;
        }
        _noListCache.Remove(name);
        _log.Info($"[Accessibility] Men� geschlossen: {name}, Stack-Tiefe: {_menuStack.Count}");
    }

    // -- Universal: Listenfokus per PostUpdate erkennen --------------

    private unsafe void OnAnyAddonUpdate(AddonEvent type, AddonArgs args)
    {
        var name = args.AddonName;
        // SpecialUpdateAddons �berspringen, au�er ConfigSystem und TitleDCWorldMap (die wir jetzt universell behandeln)
        if (SpecialUpdateAddons.Contains(name) && name != "ConfigSystem" && name != "TitleDCWorldMap") return;

        var currentAddon = (AtkUnitBase*)(nint)args.Addon;
        if (currentAddon == null || !currentAddon->IsVisible) return;

        if (name == "_TitleMenu")
        {
            _activeScreenContext = ScreenContext.TitleMenu;
            AnnounceTitleMenuFocusIfChanged(currentAddon);
            return;
        }

        // ConfigSystem: eigene Fokus-Logik, l�uft VOR dem generischen FindFocusedText
        if (name == "ConfigSystem")
        {
            AnnounceConfigSystemFocusIfChanged(currentAddon);
            return;
        }

        // HUD-Rauschen komplett aus dem generischen Pfad (Fokus + Listen +
        // Scanner): NamePlate-"Fokus" pendelte alle 2s zwischen NPCs und
        // wurde jedes Mal gesprochen (Log 2026-07-11 10:35). Gleiches gilt
        // (per Configuration.cs abschaltbar) fuer _StatusCustom0/_FlyText.
        if (IsSuppressedAddon(name)) return;

        // 1. Universeller Fokus-Check (funktioniert f�r Buttons, Tabs, etc.)
        var res = FindFocusedText(currentAddon);
        if (!string.IsNullOrEmpty(res.Text))
        {
            if (!_lastFocusByAddon.TryGetValue(name, out var last)
                || res.Key != last.Key || res.Text != last.Text)
            {
                _lastFocusByAddon[name] = (res.Key, res.Text);
                _log.Info($"[Accessibility] {name} Fokus: {res.Text} (Key={res.Key})");
                
                var announceText = res.Text;

                // Spezialfall Datenzentrum: Wenn wir einen Tab fokussieren, 
                // wollen wir die DCs dazu h�ren (falls vorhanden).
                if (name == "TitleDCWorldMap")
                {
                    var panelMatch = _dcTabPanels.FirstOrDefault(p => p.Region.Contains(announceText, StringComparison.OrdinalIgnoreCase));
                    if (panelMatch.DCs != null && panelMatch.DCs.Count > 0)
                        announceText = $"{announceText}: {string.Join(", ", panelMatch.DCs.Select(d => d.Name))}";
                }

                _tolk.SpeakInterrupt(announceText);
                
                // Wir f�hren KEIN return aus, damit Listen-Navigation oder Text-Scanner
                // f�r den Rest des Fensters noch laufen k�nnen.
            }
        }

        // 2. Spezial-Logik f�r Datenzentrum (Panel-Sichtbarkeit als Fallback)
        if (name == "TitleDCWorldMap")
        {
            AnnounceDCFocus();
            return;
        }

        // 4. Klassische Listen-Navigation: alle Index-Kandidaten beobachten
        // (siehe _listIndexState-Kommentar; SelectedItemIndex allein war tot)
        if (_menuStack.Count > 0 && _menuStack.Peek().Name == name)
        {
            var list = FindListInAddon(currentAddon);
            if (list != null)
                TrackListIndices(name, list);
        }

        // 5. Generischer Text-Scanner (f�r �nderungen im Addon-Inhalt)
        if (_noListCache.Contains(name) && !IsSuppressedAddon(name))
        {
            // Some menus build their list only AFTER PostSetup (SystemMenu:
            // PostSetup found no list at 21:37:59, the dump 5s later shows
            // List(9) with 15 entries - log 2026-07-10). As soon as the list
            // exists, switch this addon over to list navigation.
            var lateList = FindListInAddon(currentAddon);
            if (lateList != null)
            {
                _noListCache.Remove(name);
                PushMenu(name, lateList->SelectedItemIndex);
                var count = GetListEntryCount(lateList);
                var sel   = ReadListItemText(lateList, Math.Max(0, lateList->SelectedItemIndex));
                _log.Info($"[Accessibility] {name}: Liste nach PostSetup aufgebaut ({count} Eintraege)");
                _tolk.Speak(sel.Length > 0 ? $"{sel}, {count} Einträge" : $"Menü, {count} Einträge");
                return;
            }
            ScanAddonTexts(name, currentAddon, isInit: false);
        }
    }

    // -- Universal: Fokus per ReceiveEvent (Fallback f�r Listen-lose Addons) -

    // 64/65/66 = TimerTick/TimerEnd/TimerStart, 74 = TimelineActiveLabelChanged
    // (AtkEventType, ilspycmd): pure animation/timer noise - _LimitBreak alone
    // fired 74 three times per frame in-game and flooded the log (2026-07-10).
    private static readonly HashSet<byte> IgnoredEventTypes = [3, 4, 5, 12, 14, 15, 16, 17, 23, 24, 64, 65, 66, 74];

    private unsafe void OnAnyAddonReceive(AddonEvent type, AddonArgs args)
    {
        var name = args.AddonName;
        if (SpecialUpdateAddons.Contains(name)) return;
        if (IsSuppressedAddon(name)) return;
        if (args is not AddonReceiveEventArgs recv) return;
        if (IgnoredEventTypes.Contains(Convert.ToByte(recv.AtkEventType))) return;

        var addon = (AtkUnitBase*)(nint)args.Addon;
        if (addon == null) return;

        // Listen-Addons werden von OnAnyAddonUpdate abgedeckt
        if (FindListInAddon(addon) != null) return;

        _log.Info($"[Accessibility] {name} ReceiveEvent: type={recv.AtkEventType} param={recv.EventParam}");

        // For real navigation events (MouseOver / ButtonClick) the AtkEvent
        // pointer names the exact hovered/clicked component and takes priority.
        // The flag-based FindFocusedText below can latch onto a stale highlight
        // - e.g. _CharaMakeRaceGender, where every race node keeps a static
        // collision bit, so FindFocusedText always returned the same race and
        // the dedup silenced navigation (log 2026-07-09 21:43-21:45).
        if ((int)recv.AtkEventType is 6 or 25
            && TryAnnounceEventTarget(name, addon, recv.AtkEvent, interrupt: (int)recv.AtkEventType == 6))
            return;

        var (focused, nodeId) = FindFocusedText(addon);
        if (!string.IsNullOrEmpty(focused))
        {
            if (!_lastFocusByAddon.TryGetValue(name, out var last)
                || nodeId != last.Key || focused != last.Text)
            {
                _lastFocusByAddon[name] = (nodeId, focused);
                _tolk.SpeakInterrupt(focused);
            }
        }
    }

    /// <summary>
    /// Universal fallback: maps the AtkEvent pointers (Node AND Target - Node
    /// can be null, proven on TitleDCWorldMap) to a component node inside the
    /// addon and announces that component's first text. Event params are NOT
    /// node ids, so only pointer comparison is reliable.
    /// </summary>
    /// <returns>
    /// True if the event pointer was resolved to a component (announced or
    /// suppressed by dedup). False when no mapping/text was found, so the
    /// caller can fall back to the flag-based FindFocusedText.
    /// </returns>
    private unsafe bool TryAnnounceEventTarget(string addonName, AtkUnitBase* addon, nint atkEventPtr, bool interrupt)
    {
        // Every early exit logs its reason (lesson from the VirtualQuery bug).
        if (atkEventPtr == 0)
        {
            _log.Info($"[Accessibility] {addonName}: AtkEvent pointer is null.");
            return false;
        }
        var evt = (AtkEvent*)atkEventPtr;
        if (!IsReadable(evt))
        {
            _log.Info($"[Accessibility] {addonName}: AtkEvent not readable: {DescribeMemory(evt)}");
            return false;
        }

        var node   = (nint)evt->Node;
        var target = (nint)evt->Target;

        var match = FindComponentForEvent(addon, node, target);
        if (match == 0)
        {
            _log.Info($"[Accessibility] {addonName}: event target not mapped: Node=0x{node:X} Target=0x{target:X}");
            return false;
        }

        var text = ReadFirstTextInComponent((AtkResNode*)match);
        var key  = ((AtkResNode*)match)->NodeId;
        if (string.IsNullOrWhiteSpace(text))
        {
            _log.Info($"[Accessibility] {addonName}: component matched (node id={key}) but has no text.");
            return false;
        }

        if (_lastFocusByAddon.TryGetValue(addonName, out var last) && key == last.Key && text == last.Text)
            return true; // correctly identified, just unchanged - do not fall back
        _lastFocusByAddon[addonName] = (key, text);
        // MouseOver interrupts (user is navigating), ButtonClick queues -
        // a click often opens a dialog whose announcement must not be cut off.
        if (interrupt) _tolk.SpeakInterrupt(text);
        else           _tolk.Speak(text);
        _log.Info($"[Accessibility] {addonName} Fokus via Event-Target: '{text}' (node id={key})");
        return true;
    }

    /// <summary>
    /// Finds the component node the event pointers belong to. Searches depth-
    /// first (deepest match wins): a component's NodeList contains its child
    /// component NODES but not their internals, so the inner component must be
    /// checked before the outer one claims the match.
    /// </summary>
    private unsafe nint FindComponentForEvent(AtkUnitBase* addon, nint node, nint target)
    {
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var match = FindComponentForEventIn(addon->UldManager.NodeList[i], node, target, depth: 0);
            if (match != 0) return match;
        }
        return 0;
    }

    private unsafe nint FindComponentForEventIn(AtkResNode* candidate, nint node, nint target, int depth)
    {
        const int MaxDepth = 4;
        if (candidate == null || (int)candidate->Type < 1000 || depth > MaxDepth) return 0;

        var comp = ((AtkComponentNode*)candidate)->Component;
        if (comp == null || !IsReadable(comp)) return 0;

        // Inner components first - deepest match wins (e.g. list item inside list)
        for (var i = 0; i < comp->UldManager.NodeListCount; i++)
        {
            var inner = FindComponentForEventIn(comp->UldManager.NodeList[i], node, target, depth + 1);
            if (inner != 0) return inner;
        }

        var ptr = (nint)candidate;
        if (node == ptr || target == ptr) return ptr;
        for (var i = 0; i < comp->UldManager.NodeListCount; i++)
        {
            var c = (nint)comp->UldManager.NodeList[i];
            if (c != 0 && (c == node || c == target)) return ptr;
        }
        return 0;
    }

    // -- Talk --------------------------------------------------------

    // Dedup per addon: Talk keeps its window open across dialog PAGES and
    // sets its text only AFTER PostSetup - the old read-once-at-open handler
    // (ReadFirstText, ids 2-12) never caught anything (user 2026-07-11:
    // no NPC dialog was ever spoken). PostUpdate + change detection catches
    // every page; the log line finally makes the speech output diagnosable.
    private readonly Dictionary<string, string> _lastDialogTexts = [];

    private unsafe void OnTalkUpdate(AddonEvent type, AddonArgs args)
    {
        var name = args.AddonName;
        var addon = (AtkUnitBase*)(nint)args.Addon;
        if (addon == null) return;
        if (!addon->IsVisible)
        {
            _lastDialogTexts.Remove(name);
            return;
        }

        // Collect per text NODE instead of one joined string: the speaker
        // name is its own text node and comes LAST in node-list order
        // (log-verified 2026-07-11: all 26 Talk pages ended in ". Miounne."
        // / ". Soldat des Klageregiments."). AddonTalk only has unnamed
        // AtkTextNode fields (ilspycmd), so the node-id probe line below is
        // the ground truth that pins the name node id permanently.
        var segments = new List<(uint Id, string Text)>();
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node == null || node->Type != NodeType.Text) continue;
            var t = ((AtkTextNode*)node)->NodeText.ToString();
            if (!string.IsNullOrWhiteSpace(t) && t.Length > 1)
                segments.Add((node->NodeId, t));
        }
        if (segments.Count == 0) return;

        var dedupKey = string.Join("|", segments.Select(s => s.Text));
        if (_lastDialogTexts.TryGetValue(name, out var last) && last == dedupKey) return;
        _lastDialogTexts[name] = dedupKey;

        // User wish 2026-07-11: speaker name FIRST ("Miounne: text").
        // Probe-verified (log 2026-07-11 10:14, every Talk page): the name
        // is node id=2, the dialog text node id=3. Pinned to id=2 so pages
        // without a speaker and TalkSubtitle are never reordered wrongly.
        string spoken;
        var nameSeg = name == "Talk" ? segments.FirstOrDefault(s => s.Id == 2) : default;
        if (nameSeg.Text != null && segments.Count >= 2)
        {
            var body = string.Join(". ", segments.Where(s => s.Id != 2).Select(s => s.Text));
            spoken = $"{nameSeg.Text}: {body}";
        }
        else
        {
            spoken = string.Join(". ", segments.Select(s => s.Text));
        }

        var probe = string.Join(" ", segments.Select(s => $"[id{s.Id}]='{(s.Text.Length > 40 ? s.Text[..40] + "..." : s.Text)}'"));
        _log.Info($"[Accessibility] {name} Dialog-Nodes: {probe}");
        _tolk.SpeakInterrupt(spoken);
    }

    // -- ArmouryBoard (Arsenalkammer) ---------------------------------

    // Category title of the armoury chest ("Kopf", "Waffe" ...) as last
    // announced, so switching tabs speaks the new category exactly once.
    private string _lastArmouryCategory = string.Empty;

    private unsafe void OnArmouryBoardUpdate(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)(nint)args.Addon;
        if (addon == null) return;
        if (!addon->IsVisible)
        {
            _lastArmouryCategory = string.Empty;
            return;
        }

        // Title text node id=121 (dump 2026-07-16: [5] id=121 Text V "Kopf").
        var node = addon->GetNodeById(121);
        if (node == null || node->Type != NodeType.Text) return;
        var category = ((AtkTextNode*)node)->NodeText.ToString().Trim();
        if (string.IsNullOrEmpty(category) || category == _lastArmouryCategory) return;

        var isSwitch = _lastArmouryCategory.Length > 0;
        _lastArmouryCategory = category;
        _log.Info($"[Accessibility] ArmouryBoard Kategorie: '{category}'");
        // On open the window announcer names the window first - queue the
        // initial category behind it instead of cutting it off; tab switches
        // interrupt as usual.
        if (isSwitch) _tolk.SpeakInterrupt($"Kategorie {category}.");
        else          _tolk.Speak($"Kategorie {category}.");
    }

    // -- SelectYesno -------------------------------------------------

    private unsafe void OnYesNoOpen(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)(nint)args.Addon;
        if (addon == null) return;
        (_ynConfirmLabel, _ynCancelLabel) = ReadYesNoLabels(addon);
        _lastYesNoText = _ynConfirmLabel;
        var question = ReadYesNoQuestion(addon);
        var buttons  = AccessibilityStrings.DialogButtons(_ynConfirmLabel, _ynCancelLabel);
        _log.Info($"[Accessibility] SelectYesno offen: Frage='{question}' Buttons=[{_ynConfirmLabel}|{_ynCancelLabel}]");
        _tolk.SpeakInterrupt(string.IsNullOrWhiteSpace(question) ? buttons : $"{question} {buttons}");
        _dialogOpenedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Reads the two button labels of a SelectYesno dialog fresh from its nodes.
    /// Verified via dump 2026-07-09 21:32: the visible labeled buttons are the
    /// Comp(1005) nodes id=8 ("Ok") and id=11 ("Abbrechen"); the HoldButton
    /// variants (ids 9/12/15) are invisible duplicates. ULD node ids are static,
    /// only the texts change per dialog.
    /// MAPPING ASSUMPTION (test pending, see STATUS.md): id=8 = callback index 0
    /// (confirm side), id=11 = index 1 (cancel side) - consistent with the
    /// previously verified Ja=0/Nein=1 behavior. Falls back to Ja/Nein.
    /// </summary>
    private unsafe (string Confirm, string Cancel) ReadYesNoLabels(AtkUnitBase* addon)
    {
        string confirm = "Ja", cancel = "Nein";
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var n = addon->UldManager.NodeList[i];
            if (n == null || (int)n->Type < 1000) continue;
            var t = ReadFirstTextInComponent(n);
            if (string.IsNullOrWhiteSpace(t)) continue;
            if (n->NodeId == 8)  confirm = t;
            if (n->NodeId == 11) cancel  = t;
        }
        return (confirm, cancel);
    }

    private void OnYesNoReceive(AddonEvent type, AddonArgs args)
    {
        if (args is AddonReceiveEventArgs recv)
            _log.Info($"[Accessibility] SelectYesno Event: type={recv.AtkEventType} param={recv.EventParam}");
    }

    // -- Struktur-Probe für ein einzelnes Addon ------------------------

    /// <summary>
    /// Logs all visible texts of one addon with their node ids ([Probe]
    /// lines). Ground-truth collector: the quest tracker (_ToDoList) shows
    /// the CURRENT OBJECTIVE of every accepted quest - the user asked for
    /// quest descriptions in the browser (2026-07-11) and the tracker is the
    /// game's own always-current source. Structure unknown until this probe
    /// delivers; do NOT build a reader on guesses.
    /// </summary>
    public unsafe void ProbeAddonTexts(string name)
    {
        var ptr = _gameGui.GetAddonByName(name);
        if (ptr.IsNull)
        {
            _log.Info($"[Probe] {name}: Addon existiert nicht.");
            return;
        }
        var addon = (AtkUnitBase*)(nint)ptr;
        if (!addon->IsVisible)
        {
            _log.Info($"[Probe] {name}: unsichtbar.");
            return;
        }
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var n = addon->UldManager.NodeList[i];
            if (n == null) continue;
            if (n->Type == NodeType.Text && n->IsVisible())
            {
                var t = ((AtkTextNode*)n)->NodeText.ToString().Trim();
                if (t.Length > 1) _log.Info($"[Probe] {name} id={n->NodeId}: '{t}'");
                continue;
            }
            if ((int)n->Type < 1000) continue;
            var comp = ((AtkComponentNode*)n)->Component;
            if (comp == null) continue;
            for (var j = 0; j < comp->UldManager.NodeListCount; j++)
            {
                var child = comp->UldManager.NodeList[j];
                if (child == null || child->Type != NodeType.Text || !child->IsVisible()) continue;
                var t = ((AtkTextNode*)child)->NodeText.ToString().Trim();
                if (t.Length > 1) _log.Info($"[Probe] {name} id={n->NodeId}/{child->NodeId} CT={(int)comp->GetComponentType()}: '{t}'");
            }
        }
    }

    // -- Globaler UI-Fokus (AtkInputManager.FocusedNode) --------------

    // The game tracks THE keyboard/gamepad-focused UI node globally:
    // AtkStage.Instance()->AtkInputManager->FocusedNode (@6272, ilspycmd
    // 2026-07-11). Reading it directly covers every keyboard-navigable
    // window (dialog buttons, options controls) without per-addon flag
    // guessing - the user pressed left/right in Ok/Cancel dialogs several
    // times and NEITHER our key handler NOR any node flag registered it.
    private nint   _lastFocusedNodePtr;
    private string _lastFocusedNodeText = string.Empty;
    private string _lastFocusedItemName = string.Empty;

    public unsafe void UpdateGlobalFocus()
    {
        var stage = AtkStage.Instance();
        if (stage == null) return;
        var input = stage->AtkInputManager;
        if (input == null) return;

        var node = input->FocusedNode;
        if (node == null)
        {
            _lastFocusedNodePtr  = 0;
            _lastFocusedNodeText = string.Empty;
            _lastFocusedItemName = string.Empty;
            return;
        }

        // Bestiarium hat einen eigenen Leser (OnMonsterNoteUpdate). Der globale
        // FocusedNode landet dort auf einer Rang-Zusammenfassung ("0/10, NEU")
        // ohne Rang-Namen - unbrauchbar. Solange das Fenster offen ist, schweigt
        // der generische Leser fuer diese Zeilen (State wird zurueckgesetzt, damit
        // nach dem Schliessen wieder frisch angesagt wird).
        if (IsAddonVisible("MonsterNote"))
        {
            _lastFocusedNodePtr  = 0;
            _lastFocusedNodeText = string.Empty;
            _lastFocusedItemName = string.Empty;
            return;
        }

        // Namenseingabe (_CharaMakeCharaName): der dedizierte Handler
        // OnCharaMakeNameUpdate sagt Feld-Label (Vorname/Nachname) + Tipp-Echo
        // an. Der generische Leser wuerde den Zeichenzaehler ("0/15") unter dem
        // Fokus vorlesen - stumm, solange der Fokus IN einem Namensfeld sitzt
        // (Knoepfe wie "Bestaetigen" liegen NICHT in einem TextInput und werden
        // weiter generisch gelesen).
        if (IsFocusInsideNameField(node))
        {
            _lastFocusedNodePtr  = (nint)node;
            _lastFocusedNodeText = string.Empty;
            _lastFocusedItemName = string.Empty;
            return;
        }

        // Item slots (inventory grid, hand-over, quest reward) show only an
        // icon - no name. Their raw text is empty or JUST the stack quantity
        // ("10"), so the icon->name resolution must take PRIORITY over the text
        // (log 2026-07-12: filled slots announced "10" instead of the item).
        // Resolve only when the focused node changes (bag/sheet lookup is not a
        // per-frame operation); cache it so same-node frames reuse the name.
        if ((nint)node != _lastFocusedNodePtr)
            _lastFocusedItemName = ResolveFocusedItemName(node);

        string text;
        if (!string.IsNullOrEmpty(_lastFocusedItemName))
        {
            text = _lastFocusedItemName;
        }
        else if (TryReadConfigKeybindFocusRow(node, out var keybindRow))
        {
            // Tastenbelegung: arrow keys move the GLOBAL focus, the list
            // index fields never move (log 2026-07-17 13:12: only [Focus]
            // lines, a single List-Navigation at open) - the V4.77 fix in
            // the list path therefore never ran. The generic tree reader
            // also drops single-char key labels ("W", "1"): GetTextFromNode-
            // Tree discards texts of length 1, which is exactly why hotbar
            // rows were spoken without their keys.
            text = keybindRow;
        }
        else if (TryReadCharaMakeIconFocusRow(node, out var cmfRow))
        {
            // Aussehen-Picker der Charaktererstellung: Zeilen sind textlose
            // Icon-/Farbfelder, angesagt wird die Position ("12 von 52").
            text = cmfRow;
        }
        else
        {
            // Focus often sits on a child (collision node) of the actual
            // control - climb up a few levels until some text resolves.
            text = GetTextFromNodeTree(node);
            var cur = node;
            for (var up = 0; string.IsNullOrEmpty(text) && up < 3 && cur->ParentNode != null; up++)
            {
                cur  = cur->ParentNode;
                text = GetTextFromNodeTree(cur);
            }
        }

        if ((nint)node == _lastFocusedNodePtr && text == _lastFocusedNodeText) return;
        _lastFocusedNodePtr  = (nint)node;
        _lastFocusedNodeText = text;
        _log.Info($"[Focus] id={node->NodeId} ptr=0x{(nint)node:X} Text='{text}'");
        // Just after a dialog opens the focus settles onto its default button;
        // announcing it here would interrupt the opening question. Stay silent
        // during the guard window (state is already updated, so the user's next
        // navigation is still detected and announced).
        if (InDialogOpenGuard) return;
        if (!string.IsNullOrEmpty(text))
        {
            // The quest-completion reward summary is spoken in full when
            // JournalResult opens (BuildRewardText). Navigating its currency
            // cells afterwards yields only bare numbers ("400"/"103") - skip
            // those while that window is open; buttons ("Abschließen") and item
            // names are non-numeric and still pass through.
            if (IsBareNumber(text) && IsAddonVisible("JournalResult")) return;
            // Zeichen-Zaehler eines Textfelds ("3/40"): der Fokus sitzt auf
            // dem Zaehler-Node und wuerde bei jedem Tastendruck sprechen -
            // das Tipp-Echo (OnCharaMakeInputUpdate) uebernimmt dort.
            if (IsBareNumber(text) && IsAddonVisible("CharaMakeDataInputString")) return;
            // Item-Slot-Texte tragen die Gear-Info schon (ResolveFocusedItemName);
            // rohe Fokus-Texte (Laden-Zeilen) bekommen sie hier angehaengt.
            if (string.IsNullOrEmpty(_lastFocusedItemName)) text = AppendShopGearInfo(text);
            _tolk.SpeakInterrupt(text); // identische Doppel-Ansagen faengt der 0,5s-Debounce ab
        }
    }

    /// <summary>True for text that is only digits and separators (a currency
    /// amount like "400" or "1.234"), i.e. carries no words.</summary>
    private static bool IsBareNumber(string text)
    {
        var hasDigit = false;
        foreach (var c in text)
        {
            if (char.IsDigit(c)) { hasDigit = true; continue; }
            if (c is ' ' or '.' or ',' or '/' or '%') continue;
            return false;
        }
        return hasDigit;
    }

    /// <summary>Whether a named addon currently exists and is visible.</summary>
    private unsafe bool IsAddonVisible(string addonName)
    {
        var ptr = _gameGui.GetAddonByName(addonName);
        if (ptr.IsNull) return false;
        return ((AtkUnitBase*)(nint)ptr)->IsVisible;
    }

    // Shop windows list gear as bare "name, price" rows - no level, no
    // wearability. While one is open, spoken row texts get the gear info
    // appended when a row part matches an equipment name. Addon names are
    // community knowledge, NOT verified against a dump yet - if a shop stays
    // unenriched, the [Accessibility] Addon: log line names the real window.
    private static readonly string[] ShopAddons =
    {
        "Shop", "ShopExchangeItem", "ShopExchangeCurrency", "InclusionShop",
    };

    /// <summary>Appends "Stufe X, tragbar/nicht tragbar" to a spoken text while a
    /// shop is open and one of its comma-parts is an equipment name. The text
    /// itself is never altered, only extended - prices etc. stay audible.</summary>
    private string AppendShopGearInfo(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var shopOpen = false;
        foreach (var name in ShopAddons)
        {
            if (IsAddonVisible(name)) { shopOpen = true; break; }
        }
        if (!shopOpen) return text;

        foreach (var part in text.Split(", "))
        {
            // Shop rows wrap the item name in SeString payload bytes
            // (0x02..0x03, log 2026-07-16 18:21: '226, <payload>Laien-
            // Hanfbundhaube<payload>') - match on the sanitized name, the
            // same form the user hears.
            var info = _gearInfo.DescribeByName(TolkService.Sanitize(part));
            if (info.Length > 0)
            {
                _log.Info($"[Gear] Laden-Zeile '{part}': {info}");
                return $"{text}, {info}";
            }
        }
        return text;
    }

    /// <summary>
    /// Name of the item the focused node belongs to, or "" if it is not an item
    /// slot. Item slots (inventory grid, hand-over Request, quest reward) carry
    /// an icon but no text, so we climb from the focused collision node to its
    /// slot component, read the icon id and resolve it against the item sheets.
    /// The same climb already resolves button labels (log confirms), so reaching
    /// the slot component this way is proven.
    /// </summary>
    private unsafe string ResolveFocusedItemName(AtkResNode* node)
    {
        // Climb to the nearest ancestor-or-self COMPONENT node (the focus sits
        // on the slot's collision child). Evaluate exactly that component: if it
        // is an item slot (has an icon) resolve the name, otherwise stop - do
        // NOT keep climbing into the addon, or a button/window could pick up an
        // unrelated decorative icon and be mis-announced as an item.
        var cur = node;
        for (var up = 0; up < 3 && cur != null; up++)
        {
            if ((int)cur->Type >= 1000)
            {
                var comp = ((AtkComponentNode*)cur)->Component;
                if (comp == null) return string.Empty;

                var icon = FindSlotIcon(comp);
                if (icon == null) return string.Empty; // a real control, but not an item slot

                // Empty slots must SPEAK (user 2026-07-16: silent cursor moves
                // in the bag/armoury are indistinguishable from a stuck cursor).
                // Only genuine slot components (Icon/DragDrop) with icon id 0
                // say "Leer" - that combination is the empty-item-slot
                // signature. Icon-decorated controls carry a REAL icon id
                // (ConfigSystem tabs, DragDrop) or match only via the wrapper
                // branch (armoury category tabs, Base) and stay silent.
                var compType = comp->GetComponentType();
                var isItemSlot = compType is ComponentType.Icon or ComponentType.DragDrop;
                if (icon->IconId == 0)
                    return isItemSlot && ((AtkResNode*)cur)->IsVisible() ? "Leer" : string.Empty;

                var (name, itemId) = _inventory.ResolveIconItem(icon->IconId);
                if (string.IsNullOrEmpty(name)) return string.Empty;

                // Equipment gets level + wearability appended ("Bronzegladius,
                // Stufe 5, tragbar") - the info a blind player needs when
                // browsing gear slots or shop wares. "" for non-equipment.
                var gear = _gearInfo.DescribeGear(itemId);

                // Prepend the stack count so the user hears "10 mal Eichenholz".
                var qty = ReadIconQuantity(icon);
                _log.Info($"[Focus] Item-Slot iconId={icon->IconId} qty='{qty}' name='{name}' gear='{gear}'");
                var spoken = qty.Length > 0 ? $"{qty} mal {name}" : name;
                return gear.Length > 0 ? $"{spoken}, {gear}" : spoken;
            }
            cur = cur->ParentNode;
        }
        return string.Empty;
    }

    /// <summary>The AtkComponentIcon of a slot component: an Icon component itself,
    /// a DragDrop's icon, or the first Icon child of a wrapper (Multipurpose).
    /// null if the component is not an item slot.</summary>
    private static unsafe AtkComponentIcon* FindSlotIcon(AtkComponentBase* comp)
    {
        switch (comp->GetComponentType())
        {
            case ComponentType.Icon:
                return (AtkComponentIcon*)comp;
            case ComponentType.DragDrop:
                return ((AtkComponentDragDrop*)comp)->AtkComponentIcon;
        }

        // Wrapper (Multipurpose etc.): look for a direct Icon child component.
        for (var i = 0; i < comp->UldManager.NodeListCount; i++)
        {
            var n = comp->UldManager.NodeList[i];
            if (n == null || (int)n->Type < 1000) continue;
            var c = ((AtkComponentNode*)n)->Component;
            if (c != null && c->GetComponentType() == ComponentType.Icon)
                return (AtkComponentIcon*)c;
        }
        return null;
    }

    /// <summary>Visible stack count of an item slot ("10"), read straight from the
    /// icon's own QuantityText node (offset 256, ilspycmd-verified) - not via
    /// GetTextFromNodeTree, which drops 1-char strings and would lose single-digit
    /// counts. "" when the quantity node is hidden/empty or shows a single item.</summary>
    private static unsafe string ReadIconQuantity(AtkComponentIcon* icon)
    {
        var qn = icon->QuantityText;
        if (qn == null || !((AtkResNode*)qn)->IsVisible()) return string.Empty;
        var q = qn->NodeText.ToString().Trim();
        if (q.Length == 0 || q == "1" || !q.All(char.IsDigit)) return string.Empty;
        return q;
    }

    // -- Dialog-Button-Fokus-Probe (SelectYesno, JournalResult) ------

    // Which flag marks the keyboard-selected button of a dialog? Left/right
    // switching was inaudible in V4.31 and the generic focus scan logged no
    // focus-bit movement (log 2026-07-11: one focus line per dialog, then
    // silence while the user navigated). This probe logs ALL visible button
    // flags on every change and announces the button carrying the focus bit
    // - reading the game state instead of mirroring it (Navigate keeps its
    // own left=confirm model only as fallback until the probe settles this).
    private readonly Dictionary<string, string> _lastDialogButtonFlags = [];
    private readonly Dictionary<string, string> _lastDialogButtonAnnounce = [];

    // A dialog's opening announcement (question + buttons) must not be cut off
    // by the button-focus announcers (OnDialogButtonProbe and the global focus
    // reader), which both fire a few ms later and would interrupt it - the user
    // then only heard "Ok" and not what they were confirming (report
    // 2026-07-11). For a short window after a dialog opens they keep tracking
    // state but stay silent; navigation after the window announces normally.
    private DateTime _dialogOpenedAt = DateTime.MinValue;
    private bool InDialogOpenGuard => (DateTime.UtcNow - _dialogOpenedAt).TotalMilliseconds < 1000;

    private unsafe void OnDialogButtonProbe(AddonEvent type, AddonArgs args)
    {
        var name  = args.AddonName;
        var addon = (AtkUnitBase*)(nint)args.Addon;
        if (addon == null || !addon->IsVisible) return;

        const ushort HasFocusBit = 0x100;
        var sb = new StringBuilder();
        string? focused = null;
        var focusedCount = 0;

        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node == null || (int)node->Type < 1000 || !node->IsVisible()) continue;
            var comp = ((AtkComponentNode*)node)->Component;
            if (comp == null || comp->GetComponentType() != ComponentType.Button) continue;

            var label    = ReadFirstTextInComponent(node);
            var nf       = (ushort)node->NodeFlags;
            var hasFocus = (nf & HasFocusBit) != 0;

            sb.Append($" id{node->NodeId}'{label}'=0x{nf:X4}");
            for (var j = 0; j < comp->UldManager.NodeListCount; j++)
            {
                var ch = comp->UldManager.NodeList[j];
                if (ch == null) continue;
                var cf = (ushort)ch->NodeFlags;
                sb.Append($" c{ch->NodeId}=0x{cf:X4}{(ch->IsVisible() ? "V" : "")}");
                if ((cf & HasFocusBit) != 0) hasFocus = true;
            }

            if (hasFocus && !string.IsNullOrWhiteSpace(label))
            {
                focused = label;
                focusedCount++;
            }
        }

        var snapshot = sb.ToString();
        if (_lastDialogButtonFlags.TryGetValue(name, out var last) && last == snapshot) return;
        _lastDialogButtonFlags[name] = snapshot;
        _log.Info($"[BtnProbe] {name}:{snapshot}");

        if (focusedCount != 1 || focused == null) return;
        if (_lastDialogButtonAnnounce.TryGetValue(name, out var announced) && announced == focused) return;
        _lastDialogButtonAnnounce[name] = focused;
        // Keep the state above current, but don't cut off the just-spoken question.
        if (InDialogOpenGuard) return;
        _tolk.SpeakInterrupt(focused);
    }

    // -- SelectString / SelectIconString -----------------------------

    private unsafe void OnSelectStringOpen(AddonEvent type, AddonArgs args)
    {
        var name  = args.AddonName;
        var addon = (AtkUnitBase*)(nint)args.Addon;
        if (addon == null) return;

        _noListCache.Remove(name);

        var list   = FindListInAddon(addon);
        var prompt = ReadFirstText(addon);

        if (list != null)
        {
            PushMenu(name, list->SelectedItemIndex);

            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(prompt))
                sb.Append(prompt).Append(". ");

            // Bis zu 8 Eintr�ge vorlesen � SelectString hat selten mehr davon
            var maxItems = Math.Min(list->ListLength, 8);
            for (var i = 0; i < maxItems; i++)
            {
                var item = ReadListItemText(list, i);
                if (!string.IsNullOrWhiteSpace(item))
                    sb.Append($"{i + 1}. {item}. ");
            }
            if (sb.Length > 0)
                _tolk.SpeakInterrupt(sb.ToString().Trim());
            _dialogOpenedAt = DateTime.UtcNow;
        }
        else if (!string.IsNullOrWhiteSpace(prompt))
        {
            _tolk.SpeakInterrupt(prompt);
            _dialogOpenedAt = DateTime.UtcNow;
        }
    }

    // -- Request (NPC-Ablieferung "GEGENSTAND ABLIEFERN") ------------
    // The hand-over window carries NO item names in its nodes (icon-only
    // slots); the eligible items live in the sibling InventoryEventGrid, also
    // icon-only. We resolve each slot's icon id against the player's own items
    // (InventoryService) so a blind player hears what to hand over. Reading is
    // triggered manually (Strg+F3) because the grid fills a few frames after
    // Request's PostSetup.

    private void OnRequestOpen(AddonEvent type, AddonArgs args)
    {
        _tolk.SpeakInterrupt("Gegenstand abliefern. Drücke Strg F3 für die passenden Gegenstände, dann auswählen und Übergeben.");
    }

    /// <summary>
    /// If the hand-over window (Request) is open, announces the eligible items
    /// from the InventoryEventGrid and returns true; otherwise returns false so
    /// the caller can fall back to reading the whole inventory.
    /// </summary>
    public unsafe bool TryAnnounceHandOver()
    {
        var reqPtr = _gameGui.GetAddonByName("Request");
        if (reqPtr.IsNull || !((AtkUnitBase*)(nint)reqPtr)->IsVisible) return false;
        AnnounceHandOver();
        return true;
    }

    /// <summary>Reads the icon-only slots of the hand-over grid and speaks the resolved item names.</summary>
    private unsafe void AnnounceHandOver()
    {
        var items = new List<string>();
        var gridPtr = _gameGui.GetAddonByName("InventoryEventGrid");
        if (!gridPtr.IsNull)
        {
            var grid = (AtkUnitBase*)(nint)gridPtr;
            var iconNames = _inventory.BuildIconNameMap();

            for (var i = 0; i < grid->UldManager.NodeListCount; i++)
            {
                var node = grid->UldManager.NodeList[i];
                if (node == null || (int)node->Type < 1000) continue;

                var comp = ((AtkComponentNode*)node)->Component;
                if (comp == null || comp->GetComponentType() != ComponentType.DragDrop) continue;

                var icon = ((AtkComponentDragDrop*)comp)->AtkComponentIcon;
                if (icon == null) continue;

                var iconId = icon->IconId;
                if (iconId == 0) continue; // empty slot

                var name = iconNames.TryGetValue(iconId, out var n) ? n : $"Unbekannter Gegenstand, Icon {iconId}";
                _log.Info($"[HandIn] Slot node={node->NodeId} iconId={iconId} name='{name}'");
                items.Add(name);
            }
        }

        var msg = items.Count switch
        {
            0 => "Keine passenden Gegenstände im Beutel gefunden.",
            1 => $"Ein passender Gegenstand: {items[0]}. Auswählen und dann Übergeben drücken.",
            _ => $"{items.Count} passende Gegenstände: {string.Join(", ", items)}. Auswählen und dann Übergeben drücken.",
        };
        _tolk.SpeakInterrupt(msg);
    }

    // -- Titelbildschirm ---------------------------------------------

    private void OnTitleScreenOpen(AddonEvent type, AddonArgs args)
    {
        ResetTitleMenuState();
        _activeScreenContext = ScreenContext.Title;
        _tolk.SpeakInterrupt(AccessibilityStrings.TitleScreen);
    }

    // -- _TitleMenu (Hauptmen� mit Buttons) --------------------------

    private unsafe void OnTitleMenuOpen(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)(nint)args.Addon;
        if (addon == null) return;
        _activeScreenContext = ScreenContext.TitleMenu;

        // Flag-Cache initialisieren (Top-Level + Children)
        var cache = new Dictionary<uint, ushort>();
        _focusTrackFlags["_TitleMenu"] = cache;
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var n = addon->UldManager.NodeList[i];
            if (n == null) continue;
            cache[n->NodeId] = (ushort)n->NodeFlags;
            if ((int)n->Type < 1000) continue;
            var comp = ((AtkComponentNode*)n)->Component;
            if (comp == null) continue;
            for (var j = 0; j < comp->UldManager.NodeListCount; j++)
            {
                var child = comp->UldManager.NodeList[j];
                if (child == null) continue;
                cache[n->NodeId * 10000u + child->NodeId] = (ushort)child->NodeFlags;
            }
        }

        // Men�-Eintr�ge lesen und ansagen
        var selection = GetTitleMenuSelection(addon);
        if (selection.Count <= 0 || string.IsNullOrWhiteSpace(selection.Item))
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.MainMenu);
            return;
        }

        RememberTitleMenuSelection(selection.Item, selection.Index);
        _tolk.SpeakInterrupt($"{AccessibilityStrings.MainMenu}. {FormatTitleMenuSelection(selection.Item, selection.Index, selection.Count)}");
    }

    private unsafe void OnTitleMenuReceive(AddonEvent type, AddonArgs args)
    {
        // Fokus-Ank�ndigungen laufen �ber OnAnyAddonUpdate (PostUpdate, jedes Frame).
        // Bei InputReceived: Dump-Flag setzen � DumpTitleMenuButtonFlags l�uft dann im
        // n�chsten PostUpdate, wenn das Spiel den Fokus bereits intern verschoben hat.
        if (args is not AddonReceiveEventArgs recv) return;
        _log.Info($"[DEBUG] _TitleMenu ReceiveEvent: type={recv.AtkEventType} param={recv.EventParam}");

        if ((int)recv.AtkEventType == 12) // InputReceived
            _dumpOnNextTitleMenuUpdate = true;
    }

    // -- Systemkonfiguration (ConfigSystem) -------------------------

    // Text-Cache: nodeKey ? letzter gesehener Inhalt (f�r �nderungs-Erkennung)
    private readonly Dictionary<uint, string> _configSystemLastTexts = [];

    // Generic text cache: addonName ? (nodeKey ? letzter Text) f�r alle nicht-listenbasierten Addons
    private readonly Dictionary<string, Dictionary<uint, string>> _genericTextCache = [];

    // TitleDCWorldMap: zuletzt angesagte Region (Dedup)
    private string _lastDCText = string.Empty;

    // TitleDCWorldMap: Tab/Panel-Paare (bef�llt in OnDCWorldMapOpen).
    // Wir speichern den Pointer zum Panel-Node, um dessen Sichtbarkeit zu pr�fen.
    // Panels[i] entspricht Tab[i] nach NodeList-Reihenfolge.
    private readonly List<(nint PanelPtr, string Region, List<(nint Node, string Name, nint WorldList)> DCs)> _dcTabPanels = [];

    /// <summary>True wenn TitleDCWorldMap gerade offen ist (f�r Plugin.cs-Navigation).</summary>
    public bool IsDCMapOpen { get; private set; }

    private unsafe void OnConfigSystemOpen(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)(nint)args.Addon;
        if (addon == null) return;

        _csPendingSave       = true;  // Default: OK = gespeichert; Escape setzt auf false
        _activeScreenContext = ScreenContext.ConfigSystem;

        // Flag-Cache + Text-Cache neu aufbauen
        var flagCache = new Dictionary<uint, ushort>();
        _focusTrackFlags["ConfigSystem"] = flagCache;
        _configSystemLastTexts.Clear();

        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var n = addon->UldManager.NodeList[i];
            if (n == null) continue;
            flagCache[n->NodeId] = (ushort)n->NodeFlags;

            if (n->Type == NodeType.Text)
            {
                var t = ((AtkTextNode*)n)->NodeText.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(t))
                    _configSystemLastTexts[n->NodeId] = t;
            }

            if ((int)n->Type < 1000) continue;
            var comp = ((AtkComponentNode*)n)->Component;
            if (comp == null) continue;

            // Diagnose: alle ComponentTypes loggen (Tab-Typ identifizieren)
            _log.Info($"[CS-OPEN] Node[{i}] id={n->NodeId} Type={(int)n->Type} CT={(int)comp->GetComponentType()}({comp->GetComponentType()}) Vis={n->IsVisible()}");

            for (var j = 0; j < comp->UldManager.NodeListCount; j++)
            {
                var child = comp->UldManager.NodeList[j];
                if (child == null) continue;
                flagCache[n->NodeId * 10000u + child->NodeId] = (ushort)child->NodeFlags;

                if (child->Type != NodeType.Text) continue;
                var ct = ((AtkTextNode*)child)->NodeText.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(ct))
                    _configSystemLastTexts[n->NodeId * 10000u + child->NodeId] = ct;
            }
        }

        // Tab-Liste aufbauen + fokussierten Tab ermitteln
        _csTabs.Clear();
        _csLastTabIndex = -1;
        _csLastTabText  = string.Empty;
        ReadConfigSystemTabs(addon);
        var (tabIdx, tabLabel) = GetFocusedTabInfo(addon);
        _csLastTabIndex = tabIdx;
        _csLastTabText  = tabLabel;

        // Ansage: "Systemeinstellungen. [Tab X von N.]"
        var sb = new StringBuilder(AccessibilityStrings.ConfigSystem);
        if (!string.IsNullOrEmpty(tabLabel) && _csTabs.Count > 0)
            sb.Append($". {AccessibilityStrings.TabPosition(tabLabel, tabIdx + 1, _csTabs.Count)}");
        sb.Append(".");
        _tolk.SpeakInterrupt(sb.ToString());
    }

    private void OnConfigSystemClose(AddonEvent type, AddonArgs args)
    {
        _focusTrackFlags.Remove("ConfigSystem");
        _csTabs.Clear();
        _csLastTabIndex = -1;
        _csLastTabText  = string.Empty;
        _lastTitleMenuText = string.Empty; // Dedup zur�cksetzen ? TitleMenu-Button wird neu angesagt

        // Gespeichert oder verworfen?
        var msg = _csPendingSave
            ? AccessibilityStrings.ConfigSystemSaved
            : AccessibilityStrings.ConfigSystemDiscarded;
        _tolk.SpeakInterrupt(msg);
        _csPendingSave = false;

        // _configSystemLastTexts NICHT leeren � Wert-Cache bleibt f�r n�chstes �ffnen
        _activeScreenContext = GetCurrentScreenContext();
    }

    private unsafe void OnConfigSystemReceive(AddonEvent type, AddonArgs args)
    {
        if (args is not AddonReceiveEventArgs recv) return;

        // Alle Events loggen (au�er 74 = TimelineActiveLabelChanged) � f�r Diagnose: OK/Cancel-EventType ermitteln
        if ((int)recv.AtkEventType != 74)
            _log.Info($"[CS-EVT] type={(int)recv.AtkEventType}({recv.AtkEventType}) param={recv.EventParam}");

        // CS-DIAG-Dump bei InputReceived (case 12) ENTFERNT: feuerte bei
        // JEDEM Tastendruck einen 1400-Zeilen-Dump + Ansage "ConfigSystem
        // Dump. 593 Nodes." (Log 2026-07-11 10:33, 40k Zeilen Flut).
        // Struktur ist analysiert: docs/game-api.md -> "ConfigSystem".
        // TODO: OK/Abbrechen-ButtonClick hier erkennen (_csPendingSave),
        // sobald die Button-NodeIds aus einem Klick-Log bekannt sind.
    }

    private bool IsVolatileConfigText(string t)
    {
        if (string.IsNullOrWhiteSpace(t)) return true;
        // FPS-Muster: "60 fps", "144 fps", "60 Bilder/Sek."
        if (t.Contains("fps", StringComparison.OrdinalIgnoreCase)) return true;
        if (t.Contains("Bilder/Sek", StringComparison.OrdinalIgnoreCase)) return true;
        // Reiner Zahlenwert (oft FPS oder Latenz in der Ecke)
        if (int.TryParse(t, out _)) return true;
        // Muster wie "60 / 60"
        if (t.Contains('/') && t.Split('/').All(s => int.TryParse(s.Trim(), out _))) return true;
        return false;
    }

    /// <summary>Scannt alle Text-Nodes in ConfigSystem und sagt echte Änderungen an (gefiltert).</summary>
    private unsafe void ScanConfigSystemTexts(AtkUnitBase* addon)
    {
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var n = addon->UldManager.NodeList[i];
            if (n == null) continue;

            if (n->Type == NodeType.Text)
            {
                var t = ((AtkTextNode*)n)->NodeText.ToString().Trim();
                if (string.IsNullOrWhiteSpace(t)) continue;
                var hasKey = _configSystemLastTexts.TryGetValue(n->NodeId, out var prev);
                if (hasKey && prev == t) continue;
                _configSystemLastTexts[n->NodeId] = t;
                if (!hasKey) continue; // Ersterscheinung ? nicht ansagen

                // FPS-Filter und bekannte Ignorier-Nodes
                if (n->NodeId != 169 && !IsVolatileConfigText(t))
                    _tolk.SpeakInterrupt(t);
                continue;
            }

            if ((int)n->Type < 1000) continue;
            var comp = ((AtkComponentNode*)n)->Component;
            if (comp == null) continue;
            for (var j = 0; j < comp->UldManager.NodeListCount; j++)
            {
                var child = comp->UldManager.NodeList[j];
                if (child == null || child->Type != NodeType.Text) continue;
                var t = ((AtkTextNode*)child)->NodeText.ToString().Trim();
                if (string.IsNullOrWhiteSpace(t)) continue;
                var key = n->NodeId * 10000u + child->NodeId;
                var hasKey = _configSystemLastTexts.TryGetValue(key, out var prev);
                if (hasKey && prev == t) continue;
                _configSystemLastTexts[key] = t;
                if (!hasKey) continue;
                if (!IsVolatileConfigText(t))
                    _tolk.SpeakInterrupt(t);
            }
        }
    }

    // -- ConfigSystem: Fokus-Erkennung + Diagnose --------------------

    // Letzte bekannte Flags pro Option-Node (f�r �nderungs-Erkennung im [CS-OPT]-Logger)
    private readonly Dictionary<uint, ushort> _csOptionFlags = [];

    /// <summary>
    /// Wird jeden PostUpdate-Frame f�r ConfigSystem aufgerufen (statt generischem FindFocusedText).
    /// Pr�ft Tab-Fokus, sagt Tab-Wechsel an, scannt Wert-�nderungen.
    /// Parallel: [CS-OPT]-Logger meldet Flag-�nderungen an Options-Nodes (Diag2).
    /// </summary>
    private unsafe void AnnounceConfigSystemFocusIfChanged(AtkUnitBase* addon)
    {
        // Diagnose-Dump im Frame nach InputReceived
        if (_dumpOnNextConfigSystemUpdate)
        {
            _dumpOnNextConfigSystemUpdate = false;
            DumpConfigSystemNodes(addon);
        }

        // [CS-NUM] Probe: sucht den Slot im ConfigSystemNumberArray, der beim
        // Seitenwechsel springt (kuenftige saubere Quelle fuer den Tab-Index).
        ProbeConfigSystemNumbers();

        // Tab-Fokus pr�fen
        var (tabIdx, tabLabel) = GetFocusedTabInfo(addon);
        if (tabIdx != _csLastTabIndex || tabLabel != _csLastTabText)
        {
            _csLastTabIndex = tabIdx;
            _csLastTabText  = tabLabel;

            if (!string.IsNullOrEmpty(tabLabel) && _csTabs.Count > 0)
            {
                // Position nur ansagen, wenn wir sie sicher WISSEN (eigener
                // Enter-Dispatch merkt sich den gedrueckten Reiter). Die
                // child-4-Erkennung meldete "Tab 8 von 8" bei Seite 1
                // (Log 2026-07-16 16:32:06) - unbestaetigt nicht sprechen.
                if (_csExpectedTabIdx >= 0)
                {
                    _log.Info($"[CS] Tab-Wechsel -> '{tabLabel}' [{_csExpectedTabIdx + 1}/{_csTabs.Count}] (per Enter)");
                    _tolk.SpeakInterrupt(AccessibilityStrings.TabPosition(tabLabel, _csExpectedTabIdx + 1, _csTabs.Count));
                    _csLastTabIndex   = _csExpectedTabIdx;
                    _csExpectedTabIdx = -1;
                }
                else
                {
                    _log.Info($"[CS] Tab-Wechsel -> '{tabLabel}' (Index unbestaetigt, child4 lieferte {tabIdx + 1})");
                    _tolk.SpeakInterrupt(tabLabel);
                }
                LogTabMarkerProbe(addon); // [CS-TAB]: welcher Reiter traegt den Aktiv-Marker?
            }

            // Cache zur�cksetzen: neue Texte des Tabs als Ersterscheinung behandeln
            _configSystemLastTexts.Clear();
            _csOptionFlags.Clear();
            ScanConfigSystemTexts(addon); // Cache bef�llen, nichts ansagen
            LogConfigOptionFlags(addon);  // [CS-OPT] Ausgangszustand protokollieren
            return;
        }

        // Enter auf einem Reiter ohne erkennbaren Seitenwechsel: ehrlich melden
        // statt still bleiben (V4.70 blieb stumm, User drueckte doppelt).
        if (_csExpectedTabIdx >= 0 && Environment.TickCount64 - _csTabActivatedAt > 1500)
        {
            _csExpectedTabIdx = -1;
            _log.Info("[CS] Reiter-Aktivierung ohne erkannten Seitenwechsel (>1,5 s)");
            LogTabMarkerProbe(addon);
            _tolk.SpeakInterrupt("Reiter gedrückt, aber kein Seitenwechsel erkannt.");
        }

        // [CS-OPT] Flags-Änderungen an Option-Nodes erkennen (Diag2)
        LogConfigOptionFlagChanges(addon);

        // 1.5: Textlose Controls über den GLOBALEN Fokus ansagen. Slider,
        // DropDownLists und die Reiter tragen KEINEN Text (Probe [CS-OPT]:
        // Slider/DropDown = ""), FindFocusedText unten sucht zudem nur nach
        // dem Fokus-BIT an den Nodes - die Tastatur bewegt aber den
        // AtkInputManager.FocusedNode (V4.35-Erkenntnis). Ergebnis war
        // Stille bei Pfeiltasten (User-Log 2026-07-16 15:52: Fokus wanderte
        // zwischen zwei Slidern, Text=''). Labels stehen als eigener
        // Top-Level-Text DIREKT VOR dem Control in der Node-Liste
        // (Dump: "Transparenz" vor Slider id=570, "Größe" vor id=566).
        AnnounceConfigGlobalFocus(addon);

        // 2. Fokus auf Optionen (CheckBox, Slider, etc.) prüfen
        var (focused, nodeId) = FindFocusedText(addon);
        if (!string.IsNullOrEmpty(focused))
        {
            if (!_lastFocusByAddon.TryGetValue("ConfigSystem", out var last)
                || nodeId != last.Key || focused != last.Text)
            {
                _lastFocusByAddon["ConfigSystem"] = (nodeId, focused);
                _log.Info($"[CS] Fokus-Wechsel: {focused} (Key={nodeId})");
                _tolk.SpeakInterrupt(focused);
            }
        }

        // 3. Wert-Änderungen in Text-Nodes scannen und ansagen
        ScanConfigSystemTexts(addon);
    }

    // Global-Fokus-Zustand für die Config-Fenster: welcher Fokus-Node zuletzt
    // angesagt wurde, sein Top-Level-Control (gecacht, die Suche ist teuer)
    // und der zuletzt gesprochene Wert (Wert-Änderungen am fokussierten
    // Control - Slider links/rechts, Dropdown-Auswahl - sprechen nur den
    // neuen Wert, nicht die ganze Zeile).
    private nint _csFocusPtr;
    private nint _csFocusTop;
    private int _csFocusTopIdx = -1;
    private string _csFocusValue = string.Empty;

    /// <summary>
    /// Announces text-less config controls (sliders, drop-downs, category
    /// tabs) via the global keyboard focus. Speaks "{label}, {type}, {value}"
    /// on focus change and just the new value while the focus stays put.
    /// Controls WITH text (checkboxes, radio buttons, footer buttons) keep
    /// being announced by the generic focus reader - this handler stays
    /// silent for them to avoid double speech.
    /// V4.69: ownership is found by SEARCHING the addon's top-level
    /// components for the focused node - the ParentNode chain of
    /// component-internal nodes does NOT reliably reach the addon root
    /// (V4.68 climbed it and never matched; log 2026-07-16 16:14).
    /// </summary>
    private unsafe void AnnounceConfigGlobalFocus(AtkUnitBase* addon)
    {
        var stage = AtkStage.Instance();
        if (stage == null || stage->AtkInputManager == null) return;
        var focus = stage->AtkInputManager->FocusedNode;
        if (focus == null) return;

        AtkResNode* top;
        if ((nint)focus == _csFocusPtr)
        {
            // Focus unchanged: reuse the cached control, only track its value.
            if (_csFocusTop == 0) return;
            top = (AtkResNode*)_csFocusTop;
            var newValue = ReadConfigControlValue(top);
            if (newValue.Length > 0 && newValue != _csFocusValue)
            {
                _csFocusValue = newValue;
                _log.Info($"[CS] Wert-Änderung: '{newValue}'");
                _tolk.SpeakInterrupt(newValue);
            }
            return;
        }

        // Fresh focus: find the top-level component that CONTAINS the node.
        _csFocusPtr = (nint)focus;
        var owner = FindTopLevelOwner(addon, focus, out var ownerIdx);
        _csFocusTop = (nint)owner;
        _csFocusTopIdx = ownerIdx;
        if (owner == null)
        {
            _log.Info($"[CS] Fokus (global): Node 0x{(nint)focus:X} gehört keinem Top-Level-Control");
            return;
        }
        top = owner;
        if ((int)top->Type < 1000) return; // bare node (no component), nothing to describe

        var comp = ((AtkComponentNode*)top)->Component;
        if (comp == null) return;
        string desc;
        switch (comp->GetComponentType())
        {
            case ComponentType.Slider:
            {
                // AtkComponentSlider.Value/MinValue/MaxValue (ilspycmd-verified 2026-07-16)
                var slider = (AtkComponentSlider*)comp;
                _csFocusValue = slider->Value.ToString();
                desc = $"{NearestPrecedingLabel(addon, _csFocusTopIdx)}, Regler, {_csFocusValue}, " +
                       $"von {slider->MinValue} bis {slider->MaxValue}.";
                break;
            }
            case ComponentType.DropDownList:
            {
                // AtkComponentDropDownList.List (ilspycmd-verified); the list's
                // SelectedItemIndex marks the chosen entry even while closed.
                _csFocusValue = ReadConfigControlValue(top);
                desc = $"{NearestPrecedingLabel(addon, _csFocusTopIdx)}, Auswahlliste, {_csFocusValue}.";
                break;
            }
            case ComponentType.DragDrop when top->NodeId is >= 7 and <= 14 && _csTabs.Count > 0:
            {
                // Category tab focused (icon-only): position readout; the page
                // heading is announced by the tab-CHANGE detector once the tab
                // is activated.
                var idx = _csTabs.FindIndex(t => t.NodeId == top->NodeId);
                _csFocusValue = string.Empty;
                desc = $"Reiter {idx + 1} von {_csTabs.Count}.";
                break;
            }
            default:
                return; // has text (generic reader speaks it) or unknown - stay silent
        }

        _log.Info($"[CS] Fokus (global): {desc}");
        _tolk.SpeakInterrupt(desc);
    }

    /// <summary>Current value of a config control as speech: slider number or
    /// drop-down selection; "" for types without a trackable value.</summary>
    private unsafe string ReadConfigControlValue(AtkResNode* top)
    {
        if ((int)top->Type < 1000) return string.Empty;
        var comp = ((AtkComponentNode*)top)->Component;
        if (comp == null) return string.Empty;
        switch (comp->GetComponentType())
        {
            case ComponentType.Slider:
                return ((AtkComponentSlider*)comp)->Value.ToString();
            case ComponentType.DropDownList:
            {
                var dd = (AtkComponentDropDownList*)comp;
                return dd->List != null
                    ? ReadListItemText(dd->List, Math.Max(0, dd->List->SelectedItemIndex))
                    : string.Empty;
            }
            default:
                return string.Empty;
        }
    }

    /// <summary>
    /// The addon's top-level node that is or contains <paramref name="focus"/>.
    /// Component contents are searched recursively (a drop-down's focus sits
    /// on a collision node inside its embedded checkbox component).
    /// </summary>
    private static unsafe AtkResNode* FindTopLevelOwner(AtkUnitBase* addon, AtkResNode* focus, out int index)
    {
        var count = addon->UldManager.NodeListCount;
        for (var i = 0; i < count; i++)
        {
            var n = addon->UldManager.NodeList[i];
            if (n == null) continue;
            if (n == focus)
            {
                index = i;
                return n;
            }
            if ((int)n->Type < 1000) continue;
            var comp = ((AtkComponentNode*)n)->Component;
            if (comp != null && ComponentContainsNode(comp, focus, 0))
            {
                index = i;
                return n;
            }
        }
        index = -1;
        return null;
    }

    private static unsafe bool ComponentContainsNode(AtkComponentBase* comp, AtkResNode* needle, int depth)
    {
        if (depth > 3) return false;
        for (var j = 0; j < comp->UldManager.NodeListCount; j++)
        {
            var c = comp->UldManager.NodeList[j];
            if (c == null) continue;
            if (c == needle) return true;
            if ((int)c->Type < 1000) continue;
            var inner = ((AtkComponentNode*)c)->Component;
            if (inner != null && ComponentContainsNode(inner, needle, depth + 1)) return true;
        }
        return false;
    }

    /// <summary>
    /// Label of a text-less control: the nearest visible top-level text node
    /// BEFORE the control in the addon's node list (dump-verified layout,
    /// e.g. "Transparenz" directly before its slider). Volatile texts (fps
    /// counter) are skipped.
    /// </summary>
    private unsafe string NearestPrecedingLabel(AtkUnitBase* addon, int topIdx)
    {
        for (var i = topIdx - 1; i >= 0; i--)
        {
            var n = addon->UldManager.NodeList[i];
            if (n == null || !n->IsVisible() || n->Type != NodeType.Text) continue;
            var t = ((AtkTextNode*)n)->NodeText.ToString().Trim();
            if (t.Length > 1 && !IsVolatileConfigText(t)) return t;
        }
        return "Ohne Beschriftung";
    }

    /// <summary>
    /// Loggt alle CheckBox/RadioButton/Slider/DropDown-Nodes einmalig mit ihren Flags.
    /// Aufruf: beim Tab-Wechsel (initialer Snapshot).
    /// </summary>
    private unsafe void LogConfigOptionFlags(AtkUnitBase* addon)
    {
        _csOptionFlags.Clear();
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var n = addon->UldManager.NodeList[i];
            if (n == null || !n->IsVisible() || (int)n->Type < 1000) continue;
            var comp = ((AtkComponentNode*)n)->Component;
            if (comp == null) continue;
            var ct = (int)comp->GetComponentType();
            // Nur Option-Typen: CheckBox(3), RadioButton(4), Slider(6), DropDownList(10), NumericInput(8)
            if (ct != 3 && ct != 4 && ct != 6 && ct != 8 && ct != 10) continue;
            var nf = (ushort)n->NodeFlags;
            _csOptionFlags[n->NodeId] = nf;
            var label = GetTextFromNodeTree(n, 0);
            if (label.Length > 40) label = label[..40] + "�";
            _log.Info($"[CS-OPT] Init id={n->NodeId} CT={comp->GetComponentType()}({ct}) F=0x{nf:X4} \"{label}\"");
        }
    }

    /// <summary>
    /// Vergleicht Flags aller Option-Nodes mit dem letzten Snapshot.
    /// �ndert sich ein Flag, wird der Node mit [CS-OPT]-Pr�fix geloggt.
    /// </summary>
    private unsafe void LogConfigOptionFlagChanges(AtkUnitBase* addon)
    {
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var n = addon->UldManager.NodeList[i];
            if (n == null || !n->IsVisible() || (int)n->Type < 1000) continue;
            var comp = ((AtkComponentNode*)n)->Component;
            if (comp == null) continue;
            var ct = (int)comp->GetComponentType();
            if (ct != 3 && ct != 4 && ct != 6 && ct != 8 && ct != 10) continue;
            var nf = (ushort)n->NodeFlags;
            if (_csOptionFlags.TryGetValue(n->NodeId, out var prev) && prev == nf) continue;
            _csOptionFlags[n->NodeId] = nf;
            var label = GetTextFromNodeTree(n, 0);
            if (label.Length > 40) label = label[..40] + "�";
            _log.Info($"[CS-OPT] Change id={n->NodeId} CT={comp->GetComponentType()}({ct}) F=0x{nf:X4} \"{label}\"");
        }
    }

    /// <summary>Sucht alle DragDrop(17)-Komponenten in ConfigSystem und f�llt _csTabs (r�ckw�rts = visuell links?rechts).</summary>
    private unsafe void ReadConfigSystemTabs(AtkUnitBase* addon)
    {
        for (var i = addon->UldManager.NodeListCount - 1; i >= 0; i--)
        {
            var n = addon->UldManager.NodeList[i];
            if (n == null || !n->IsVisible() || (int)n->Type < 1000) continue;
            var comp = ((AtkComponentNode*)n)->Component;
            if (comp == null) continue;
            // ConfigSystem-Tabs sind CT=DragDrop(17), NICHT CT=Tab(22) � verifiziert via Dump
            if (comp->GetComponentType() != ComponentType.DragDrop) continue;
            // NodeIds 7�14 = Kategorie-Icons; feste NodeId-Range als Sanity-Check
            if (n->NodeId < 7 || n->NodeId > 14) continue;

            _csTabs.Add((n->NodeId, string.Empty)); // Label wird dynamisch aus Abschnitts�berschrift gelesen
        }
        _log.Info($"[CS] Tabs gefunden: {_csTabs.Count} (NodeIds: {string.Join(", ", _csTabs.Select(t => t.NodeId))})");
    }

    /// <summary>
    /// Ermittelt den fokussierten Tab (Index in _csTabs + dynamisches Label).
    /// Tab-Typ ist DragDrop(17), NodeIds 7�14 � verifiziert via Dump.
    /// Label kommt NICHT aus dem DragDrop-Node selbst (Icon, kein Text), sondern
    /// dynamisch aus der ersten sichtbaren Abschnitts�berschrift (T=3) der aktiven Seite.
    ///
    /// Strategie 1: child NodeId==4 (T=8 Collision, F=0x2FB3) IsVisible-Wechsel (wie TitleMenu � bew�hrt).
    /// Strategie 2: HasFocusBit (0x100) auf DragDrop-Node.
    /// Strategie 3 (Diag2-Fallback): Kind mit h�chsten Flags als "aktiv" annehmen + [CS-OPT]-Log.
    /// Fallback: letzter bekannter Wert.
    /// </summary>
    private unsafe (int Index, string Label) GetFocusedTabInfo(AtkUnitBase* addon)
    {
        if (_csTabs.Count == 0) return (_csLastTabIndex, _csLastTabText);

        // Strategie 1: child NodeId==4 IsVisible (DragDrop-Variante, wie TitleMenu-Muster)
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var n = addon->UldManager.NodeList[i];
            if (n == null || !n->IsVisible() || (int)n->Type < 1000) continue;
            var comp = ((AtkComponentNode*)n)->Component;
            if (comp == null || comp->GetComponentType() != ComponentType.DragDrop) continue;
            if (n->NodeId < 7 || n->NodeId > 14) continue;

            for (var j = 0; j < comp->UldManager.NodeListCount; j++)
            {
                var child = comp->UldManager.NodeList[j];
                // EFFEKTIV sichtbar (Eltern-Kette): das eigene Flag allein traf
                // auf alle Reiter zu -> Strategie lieferte immer den LETZTEN
                // ("Tab 8 von 8" bei Seite 1, Log 2026-07-16 16:32:06).
                if (child == null || child->NodeId != 4 || !IsEffectivelyVisible(child)) continue;
                var idx   = _csTabs.FindIndex(t => t.NodeId == n->NodeId);
                var label = GetConfigSectionHeading(addon);
                // Kein Log hier: laeuft jeden Frame (flutete das Log 2026-07-11,
                // tausende identische Zeilen); der Aufrufer loggt Tab-WECHSEL.
                return (idx >= 0 ? idx : 0, label);
            }
        }

        // Strategie 2: HasFocusBit (0x100) auf DragDrop-Node
        const ushort HasFocusBit = 0x100;
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var n = addon->UldManager.NodeList[i];
            if (n == null || !n->IsVisible() || (int)n->Type < 1000) continue;
            var comp = ((AtkComponentNode*)n)->Component;
            if (comp == null || comp->GetComponentType() != ComponentType.DragDrop) continue;
            if (n->NodeId < 7 || n->NodeId > 14) continue;
            if (((ushort)n->NodeFlags & HasFocusBit) == 0) continue;

            var idx   = _csTabs.FindIndex(t => t.NodeId == n->NodeId);
            var label = GetConfigSectionHeading(addon);
            // Kein Log (jeden Frame) - Aufrufer loggt Tab-Wechsel
            return (idx >= 0 ? idx : 0, label);
        }

        // Diag2-Fallback: alle DragDrop-Nodes loggen (kl�rt: welches Flag wechselt bei Navigation)
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var n = addon->UldManager.NodeList[i];
            if (n == null || !n->IsVisible() || (int)n->Type < 1000) continue;
            var comp = ((AtkComponentNode*)n)->Component;
            if (comp == null || comp->GetComponentType() != ComponentType.DragDrop) continue;
            if (n->NodeId < 7 || n->NodeId > 14) continue;
            var nf = (ushort)n->NodeFlags;
            var childInfo = new System.Text.StringBuilder();
            for (var j = 0; j < comp->UldManager.NodeListCount; j++)
            {
                var child = comp->UldManager.NodeList[j];
                if (child == null) continue;
                childInfo.Append($" ch{child->NodeId}(F=0x{(ushort)child->NodeFlags:X4},v={child->IsVisible()})");
            }
            _log.Info($"[CS-OPT] DragDrop NodeId={n->NodeId} F=0x{nf:X4}{childInfo}");
        }

        return (_csLastTabIndex, _csLastTabText);
    }

    /// <summary>
    /// Liest den Namen des aktiven Tabs dynamisch aus der ersten sichtbaren Abschnitts�berschrift.
    /// Kriterien: T=3 (Text), sichtbar (IsVisible), Flags-Bit 0x10 (Collision/Aktiv), L�nge > 3.
    /// Sprachneutral: funktioniert f�r DE, EN und alle anderen Sprachen.
    /// </summary>
    private unsafe string GetConfigSectionHeading(AtkUnitBase* addon)
    {
        for (var i = addon->UldManager.NodeListCount - 1; i >= 0; i--)
        {
            var n = addon->UldManager.NodeList[i];
            if (n == null || n->Type != NodeType.Text) continue;
            // EFFEKTIVE Sichtbarkeit (Eltern-Kette): versteckte Seiten behalten
            // das eigene Visible-Flag ihrer Nodes, nur der Seiten-CONTAINER wird
            // unsichtbar - IsVisible() des Nodes selbst sah deshalb ALLE
            // Ueberschriften als sichtbar und lieferte immer dieselbe (kein
            // einziger Tab-Wechsel im Log 2026-07-16 16:32 trotz Seitenwechsel).
            if (!IsEffectivelyVisible(n)) continue;
            var t = ((AtkTextNode*)n)->NodeText.ToString().Trim();
            // Volatile Anzeigen ueberspringen: der fps-Zaehler (Top-Level id=4)
            // liegt am ENDE der Node-Liste, also VOR der echten Ueberschrift
            // (id=22 "Anzeigeeinstellungen") - er wurde als Tab-Label gewaehlt
            // und jede fps-Aenderung als "Tab-Wechsel" angesagt (Log 2026-07-11
            // 10:16, Dump-verifiziert).
            if (IsVolatileConfigText(t)) continue;
            if (t.Length > 3 && !t.All(char.IsDigit)) // kein reiner Zahlenwert
                return t;
        }
        return string.Empty;
    }

    /// <summary>
    /// True when the node AND every ancestor carry the Visible flag (0x10).
    /// The bit is log-verified: node id=4 F=0x202F -> IsVisible()=false vs.
    /// F=0x203F -> true (dalamud.log 2026-07-16 16:32:04, ButtonFlags).
    /// Needed because hidden config PAGES only clear the flag on their
    /// container while the child nodes keep theirs.
    /// </summary>
    private static unsafe bool IsEffectivelyVisible(AtkResNode* node)
    {
        var guard = 0;
        for (var n = node; n != null && guard++ < 32; n = n->ParentNode)
        {
            if (((ushort)n->NodeFlags & (ushort)NodeFlags.Visible) == 0) return false;
        }
        return true;
    }

    /// <summary>
    /// [CS-TAB] probe: one line per tab with the child-4 marker state (own
    /// flag + effective visibility). Logged on every page change - the next
    /// test log shows WHICH tab carries a marker that tracks the active page,
    /// so the position readout can be trusted to it in a follow-up version.
    /// </summary>
    private unsafe void LogTabMarkerProbe(AtkUnitBase* addon)
    {
        var sb = new StringBuilder("[CS-TAB]");
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var n = addon->UldManager.NodeList[i];
            if (n == null || (int)n->Type < 1000) continue;
            if (n->NodeId is < 7 or > 14) continue;
            var comp = ((AtkComponentNode*)n)->Component;
            if (comp == null || comp->GetComponentType() != ComponentType.DragDrop) continue;
            for (var j = 0; j < comp->UldManager.NodeListCount; j++)
            {
                var child = comp->UldManager.NodeList[j];
                if (child == null || child->NodeId != 4) continue;
                sb.Append($" T{n->NodeId}:F=0x{(ushort)child->NodeFlags:X4},eff={IsEffectivelyVisible(child)}");
            }
        }
        _log.Info(sb.ToString());
    }

    /// <summary>
    /// [CS-NUM] probe: logs every change in ConfigSystemNumberArray (25 ints;
    /// only [0]=FPS is mapped upstream, so [0] is skipped as known noise).
    /// A slot that jumps exactly on tab activation is the game's own active-
    /// tab index - the clean future source for the position readout.
    /// </summary>
    private unsafe void ProbeConfigSystemNumbers()
    {
        var arr = ConfigSystemNumberArray.Instance();
        if (arr == null) return;
        var data = arr->Data;
        if (!_csNumSnapshotValid)
        {
            for (var i = 0; i < _csNumSnapshot.Length && i < data.Length; i++) _csNumSnapshot[i] = data[i];
            _csNumSnapshotValid = true;
            _log.Info($"[CS-NUM] Init: {string.Join(",", _csNumSnapshot)}");
            return;
        }
        for (var i = 1; i < _csNumSnapshot.Length && i < data.Length; i++) // [0]=FPS ueberspringen
        {
            if (data[i] == _csNumSnapshot[i]) continue;
            _log.Info($"[CS-NUM] [{i}] {_csNumSnapshot[i]} -> {data[i]}");
            _csNumSnapshot[i] = data[i];
        }
    }

    /// <summary>
    /// Vollst�ndiger Diagnose-Dump von ConfigSystem: alle Nodes mit ComponentType, Flags, Text.
    /// Automatisch im n�chsten PostUpdate-Frame nach InputReceived ausgel�st.
    /// Ergebnis: Desktop\FFXIV_CS_Dump.txt + Dalamud-Log mit [CS-DIAG]-Pr�fix.
    /// </summary>
    private unsafe void DumpConfigSystemNodes(AtkUnitBase* addon)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== CS-DIAG: ConfigSystem | Vis={addon->IsVisible} | Nodes={addon->UldManager.NodeListCount} | Tabs={_csTabs.Count} ({string.Join(", ", _csTabs.Select(t => t.Label))}) ===");

        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var n = addon->UldManager.NodeList[i];
            if (n == null) continue;
            var typeNum = (int)n->Type;
            var flags   = (ushort)n->NodeFlags;
            var vis     = n->IsVisible() ? "V" : " ";

            if (typeNum < 1000)
            {
                if (typeNum == 3)
                {
                    var rawText = ((AtkTextNode*)n)->NodeText.ToString().Replace("\n", "?");
                    var txt     = rawText.Length > 70 ? rawText[..70] + "�" : rawText;
                    sb.AppendLine($"  [{i}] id={n->NodeId} T={typeNum} F=0x{flags:X4} {vis} \"{txt}\"");
                }
                else
                {
                    sb.AppendLine($"  [{i}] id={n->NodeId} T={typeNum} F=0x{flags:X4} {vis}");
                }
                continue;
            }

            var comp   = ((AtkComponentNode*)n)->Component;
            var ctNum  = comp != null ? (int)comp->GetComponentType() : -1;
            var ctName = comp != null ? comp->GetComponentType().ToString() : "?";
            var label  = GetTextFromNodeTree(n, 0);
            if (label.Length > 60) label = label[..60] + "�";
            sb.AppendLine($"  [{i}] id={n->NodeId} T={typeNum} CT={ctName}({ctNum}) F=0x{flags:X4} {vis} \"{label}\"");

            if (comp == null) continue;
            for (var j = 0; j < comp->UldManager.NodeListCount; j++)
            {
                var child = comp->UldManager.NodeList[j];
                if (child == null) continue;
                var ct2  = (int)child->Type;
                var cf   = (ushort)child->NodeFlags;
                var cv   = child->IsVisible() ? "V" : " ";
                var cext = string.Empty;
                if (ct2 == 3)
                {
                    var s = ((AtkTextNode*)child)->NodeText.ToString().Replace("\n", "?");
                    cext = $" \"{(s.Length > 50 ? s[..50] + "�" : s)}\"";
                }
                else if (ct2 >= 1000)
                {
                    var cc = ((AtkComponentNode*)child)->Component;
                    if (cc != null) cext = $" [CT={cc->GetComponentType()}({(int)cc->GetComponentType()})]";
                }
                sb.AppendLine($"    child[{j}] id={child->NodeId} T={ct2} F=0x{cf:X4} {cv}{cext}");
            }
        }

        var output = sb.ToString();
        foreach (var line in output.Split('\n'))
        {
            var l = line.TrimEnd('\r');
            if (l.Length > 0) _log.Info($"[CS-DIAG] {l}");
        }

        var dumpFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "FFXIV_CS_Dump.txt");
        try
        {
            File.WriteAllText(dumpFile, output, System.Text.Encoding.UTF8);
            _tolk.SpeakInterrupt($"ConfigSystem Dump. {addon->UldManager.NodeListCount} Nodes.");
            _log.Info($"[CS-DIAG] Gespeichert: {dumpFile}");
        }
        catch (Exception ex)
        {
            _log.Warning($"[CS-DIAG] Datei-Fehler: {ex.Message}");
        }
    }

    /// <summary>
    /// Generischer Text-Scanner f�r beliebige Addons.
    /// isInit=true: bef�llt nur den Cache, sagt nichts an (beim �ffnen).
    /// isInit=false: sagt ge�nderte Texte an (in PostUpdate).
    /// </summary>
    private unsafe void ScanAddonTexts(string addonName, AtkUnitBase* addon, bool isInit)
    {
        if (!_genericTextCache.TryGetValue(addonName, out var cache))
        {
            cache = new Dictionary<uint, string>();
            _genericTextCache[addonName] = cache;
        }

        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var n = addon->UldManager.NodeList[i];
            if (n == null) continue;

            if (n->Type == NodeType.Text)
            {
                if (!n->IsVisible()) continue; // versteckte Nodes = alter/bedingter Inhalt
                var t = ((AtkTextNode*)n)->NodeText.ToString().Trim();
                if (string.IsNullOrWhiteSpace(t) || t.Length <= 1) continue;
                var hasKey = cache.TryGetValue(n->NodeId, out var prev);
                if (hasKey && prev == t) continue;
                cache[n->NodeId] = t;
                if (!isInit && hasKey)
                {
                    _log.Info($"[Scan] {addonName} id={n->NodeId}: '{(t.Length > 60 ? t[..60] + "..." : t)}'");
                    _tolk.SpeakInterrupt(t);
                }
                continue;
            }

            if ((int)n->Type < 1000) continue;
            var comp = ((AtkComponentNode*)n)->Component;
            if (comp == null) continue;
            for (var j = 0; j < comp->UldManager.NodeListCount; j++)
            {
                var child = comp->UldManager.NodeList[j];
                if (child == null || child->Type != NodeType.Text || !child->IsVisible()) continue;
                var t = ((AtkTextNode*)child)->NodeText.ToString().Trim();
                if (string.IsNullOrWhiteSpace(t) || t.Length <= 1) continue;
                var key = n->NodeId * 10000u + child->NodeId;
                var hasKey = cache.TryGetValue(key, out var prev);
                if (hasKey && prev == t) continue;
                cache[key] = t;
                if (!isInit && hasKey)
                {
                    _log.Info($"[Scan] {addonName} key={key}: '{(t.Length > 60 ? t[..60] + "..." : t)}'");
                    _tolk.SpeakInterrupt(t);
                }
            }
        }
    }

    // -- Gamepad-Kalibrierung ----------------------------------------


    // -- Datenzentrum-Auswahl (TitleDCWorldMap) -------------------------

    private unsafe void OnDCWorldMapOpen(AddonEvent type, AddonArgs args)
    {
        _lastDCText = string.Empty;
        IsDCMapOpen = true;
        _dcTabPanels.Clear();

        var addon = (AtkUnitBase*)(nint)args.Addon;
        if (addon == null) { _tolk.SpeakInterrupt("Datenzentrum w�hlen."); return; }

        // -- Schritt 1: Alle Region-Panels sammeln.
        // Wir sammeln ALLE Panels, auch die (noch) unsichtbaren, um die Navigation
        // sp�ter �ber deren Sichtbarkeits-Wechsel zu erkennen.
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var n = addon->UldManager.NodeList[i];
            if (n == null) continue;
            
            // Regionen-Panels sind Komponenten, aber nicht vom Typ 1022 (Tabs).
            var typeNum = (int)n->Type;
            if (typeNum < 1000 || typeNum == 1022) continue;

            var (region, dcs) = ReadDCRegionPanel(n);
            if (string.IsNullOrWhiteSpace(region)) continue;

            _dcTabPanels.Add(((nint)n, region, dcs));
            var dcInfo = string.Join(", ", dcs.Select(d => $"{d.Name}@0x{d.Node:X}"));
            _log.Info($"[DC] Panel gefunden: {region} (NodeId={n->NodeId}, Type={typeNum}, DCs: {dcInfo})");
        }

        if (_dcTabPanels.Count == 0)
        {
            _tolk.SpeakInterrupt("Datenzentrum w�hlen.");
            return;
        }

        // Initiale Ansage: Liste der verf�gbaren Regionen
        var sb = new StringBuilder("Datenzentrum w�hlen. Regionen: ");
        sb.Append(string.Join(", ", _dcTabPanels.Select(p => p.Region)));
        _tolk.SpeakInterrupt(sb.ToString());
        
        // Den ersten Fokus direkt ansagen
        AnnounceDCFocus();
    }

    private unsafe void OnDCWorldMapReceive(AddonEvent type, AddonArgs args)
    {
        if (args is not AddonReceiveEventArgs recv) return;

        // Debug: log ALL events except type=74 (TimelineActiveLabelChanged, fires every 300ms).
        if ((int)recv.AtkEventType != 74)
            _log.Info($"[DC] ReceiveEvent type={(int)recv.AtkEventType}({recv.AtkEventType}) param={recv.EventParam}");

        // DC-Button bestaetigt (Enter/Maus): DC-Name + Welten-Liste ansagen.
        if ((int)recv.AtkEventType == 25)
        {
            TryAnnounceDCSelection(recv.AtkEvent);
            return;
        }

        // Fokus-Erkennung �ber MouseOver-Events (Type 6)
        // Die Parameter entsprechen den Node-IDs der Reiter/Panels.
        // 13 = Ozeanien, 7 = Europa, 1 = Japan, 19 = Nordamerika (verifiziert via Log)
        if ((int)recv.AtkEventType == 6)
        {
            // 1) DC-Buttons innerhalb eines Region-Panels: Event-Node per Pointer-Vergleich
            //    zuordnen. Die Event-Parameter der DC-Buttons (z.B. 9/10 in Europa, Log
            //    2026-07-09) entsprechen keinen Node-IDs und sind nicht dokumentiert;
            //    der Node im AtkEvent ist dagegen eindeutig.
            if (TryAnnounceDCButton(recv.AtkEvent)) return;

            // 2) Region-Tabs: enthalten keine Text-Nodes (Node-Dump 2026-05-27), deshalb
            //    Zuordnung per Event-Parameter (verifiziert via Log).
            var nodeId = (uint)recv.EventParam;
            string? regionName = nodeId switch
            {
                13 => "Ozeanien",
                7  => "Europa",
                1  => "Japan",
                19 => "Nordamerika",
                _  => null
            };

            if (regionName != null)
            {
                // Suche das passende Panel in unserer Liste, um auch die DCs zu erhalten
                var panelMatch = _dcTabPanels.FirstOrDefault(p => p.Region.Contains(regionName, StringComparison.OrdinalIgnoreCase));
                var text = (panelMatch.DCs != null && panelMatch.DCs.Count > 0)
                    ? $"{regionName}: {string.Join(", ", panelMatch.DCs.Select(d => d.Name))}"
                    : regionName;

                if (text != _lastDCText)
                {
                    _log.Info($"[DC] Fokus-Wechsel (Event ID {nodeId}) ? '{text}'");
                    _lastDCText = text;
                    _tolk.SpeakInterrupt(text);
                }
                return;
            }
        }
    }

    /// <summary>
    /// Pr�ft jeden Frame den ausgew�hlten Tab und sagt die Region an, wenn sie sich ge�ndert hat.
    /// Dedup in AnnounceDCFocus() verhindert Wiederholungen; kein AtkStage n�tig.
    /// </summary>
    private void OnDCWorldMapUpdate(AddonEvent type, AddonArgs args)
    {
        // Wir verlassen uns jetzt prim�r auf ReceiveEvent (MouseOver), 
        // da die Sichtbarkeits-Flags in diesem Addon nicht zuverl�ssig sind.
    }

    /// <summary>
    /// Pr�ft, welches der gespeicherten Region-Panels gerade sichtbar ist.
    /// (Wird aktuell nicht genutzt, da OnDCWorldMapReceive zuverl�ssiger ist).
    /// </summary>
    private unsafe void AnnounceDCFocus()
    {
        // Vorerst deaktiviert, da ReceiveEvent die Arbeit �bernimmt.
    }

    /// <summary>
    /// L�scht den Dedup-Cache und sagt die aktuell fokussierte Region sofort an.
    /// Wird von Plugin.cs aufgerufen, wenn der Nutzer eine Nummernblock-Taste dr�ckt.
    /// </summary>
    public void ForceDCMapRead()
    {
        _lastDCText = string.Empty; // Dedup zur�cksetzen � User hat explizit navigiert
        AnnounceDCFocus();
    }

    /// <summary>
    /// Maps the node of a MouseOver event to a DC button (type 1015) recorded in
    /// _dcTabPanels and announces its text. Uses pointer comparison instead of the
    /// event parameter, because DC button event params are undocumented (log 2026-07-09:
    /// params 9/10 inside Europa do not match any node id in the 2026-05-27 node dump).
    /// Text is read fresh from the node at announce time (never cached values).
    /// </summary>
    /// <returns>True if the event node belongs to a known DC button.</returns>
    private unsafe bool TryAnnounceDCButton(nint atkEventPtr)
    {
        // Every early exit logs its reason - a silent exit here already cost us
        // one in-game test run (log 2026-07-09 19:02: no probe lines at all).
        if (atkEventPtr == 0)
        {
            _log.Info("[DC] TryAnnounceDCButton: AtkEvent pointer is null.");
            return false;
        }
        var evt = (AtkEvent*)atkEventPtr;
        if (!IsReadable(evt))
        {
            _log.Info($"[DC] TryAnnounceDCButton: AtkEvent not readable: {DescribeMemory(evt)}");
            return false;
        }

        // Node and Target are treated as opaque pointers: matching only compares
        // them against our known DC button nodes - the event pointers are never
        // dereferenced, so a bogus value cannot crash us.
        var node   = (nint)evt->Node;
        var target = (nint)evt->Target;

        foreach (var panel in _dcTabPanels)
        {
            foreach (var (dcNode, dcName, _) in panel.DCs)
            {
                if (!IsEventNodeInComponent(node, dcNode) && !IsEventNodeInComponent(target, dcNode)) continue;

                var text = ReadFirstTextInComponent((AtkResNode*)dcNode);
                if (string.IsNullOrWhiteSpace(text))
                {
                    _log.Info($"[DC] DC button matched ('{dcName}', Region {panel.Region}) but text read came back empty.");
                    return false;
                }

                if (text != _lastDCText)
                {
                    _lastDCText = text;
                    _tolk.SpeakInterrupt(text);
                    _log.Info($"[DC] DC focus via event node: '{text}' (Region {panel.Region})");
                }
                return true;
            }
        }

        // Diagnostic probe: tells us which pointers the event actually carried if mapping fails.
        _log.Info($"[DC] MouseOver not mapped to a DC button: Node=0x{node:X} Target=0x{target:X}");
        return false;
    }

    /// <summary>
    /// Announces DC name plus its world list after the user confirms a DC button
    /// (AtkEventType 25 / ButtonClick). World names are read fresh from the world
    /// list component at announce time - the lists are only populated once the
    /// region is opened (node dump 2026-07-09).
    /// </summary>
    private unsafe void TryAnnounceDCSelection(nint atkEventPtr)
    {
        if (atkEventPtr == 0)
        {
            _log.Info("[DC] TryAnnounceDCSelection: AtkEvent pointer is null.");
            return;
        }
        var evt = (AtkEvent*)atkEventPtr;
        if (!IsReadable(evt))
        {
            _log.Info($"[DC] TryAnnounceDCSelection: AtkEvent not readable: {DescribeMemory(evt)}");
            return;
        }

        var node   = (nint)evt->Node;
        var target = (nint)evt->Target;

        foreach (var panel in _dcTabPanels)
        {
            foreach (var (dcNode, dcName, worldList) in panel.DCs)
            {
                if (!IsEventNodeInComponent(node, dcNode) && !IsEventNodeInComponent(target, dcNode)) continue;

                var worlds = ReadWorldListTexts(worldList);
                var text   = AccessibilityStrings.DCSelected(dcName, worlds);
                _lastDCText = text; // Dedup mitziehen, sonst wiederholt der naechste MouseOver den Text nicht
                _tolk.SpeakInterrupt(text);
                _log.Info($"[DC] DC selected: '{dcName}' (Region {panel.Region}), worlds=[{string.Join(", ", worlds)}]");
                return;
            }
        }

        // Region-Tabs und der Ok-Knopf landen ebenfalls hier - nur protokollieren.
        _log.Info($"[DC] ButtonClick not mapped to a DC button: Node=0x{node:X} Target=0x{target:X}");
    }

    /// <summary>
    /// Reads all non-empty item texts from a world list component (Comp 1019).
    /// Returns an empty list if the pointer is null or the list has no filled rows.
    /// </summary>
    private unsafe List<string> ReadWorldListTexts(nint listNodePtr)
    {
        var result = new List<string>();
        if (listNodePtr == 0) return result;

        var listNode = (AtkResNode*)listNodePtr;
        if ((int)listNode->Type < 1000) return result;
        var comp = ((AtkComponentNode*)listNode)->Component;
        if (comp == null || !IsReadable(comp)) return result;

        for (var i = 0; i < comp->UldManager.NodeListCount; i++)
        {
            var item = comp->UldManager.NodeList[i];
            if (item == null || (int)item->Type < 1000) continue; // list item renderers only
            var t = ReadFirstTextInComponent(item);
            if (!string.IsNullOrWhiteSpace(t)) result.Add(t);
        }
        return result;
    }

    /// <summary>
    /// True if candidate points to the component node itself or to any node in its
    /// UldManager node list (e.g. the collision node of a button component).
    /// Only the KNOWN component node is dereferenced, never the candidate.
    /// </summary>
    /// <summary>
    /// Reads the window title from an addon's Window component (CT=Window(2)).
    /// Structure verified via SelectYesno dump 2026-07-09 21:32: Comp(1004)
    /// CT=Window holds the title as Text children (empty for untitled dialogs).
    /// Returns an empty string when there is no Window component or no title.
    /// </summary>
    private unsafe string ReadWindowTitle(AtkUnitBase* addon)
    {
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var n = addon->UldManager.NodeList[i];
            if (n == null || (int)n->Type < 1000) continue;
            var comp = ((AtkComponentNode*)n)->Component;
            if (comp == null || !IsReadable(comp)) continue;
            if (comp->GetComponentType() != ComponentType.Window) continue;

            for (var j = 0; j < comp->UldManager.NodeListCount; j++)
            {
                var c = comp->UldManager.NodeList[j];
                if (c == null || c->Type != NodeType.Text) continue;
                var t = ((AtkTextNode*)c)->NodeText.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(t)) return t;
            }
            return string.Empty; // Window component found, but it has no title
        }
        return string.Empty;
    }

    private unsafe bool IsEventNodeInComponent(nint candidate, nint componentNodePtr)
    {
        if (candidate == 0) return false;
        if (candidate == componentNodePtr) return true;
        var compNode = (AtkResNode*)componentNodePtr;
        if ((int)compNode->Type < 1000) return false;
        var comp = ((AtkComponentNode*)compNode)->Component;
        if (comp == null || !IsReadable(comp)) return false;
        for (var i = 0; i < comp->UldManager.NodeListCount; i++)
        {
            if ((nint)comp->UldManager.NodeList[i] == candidate) return true;
        }
        return false;
    }

    /// <summary>
    /// Reads the first non-trivial text node from a component node (fresh read, no cache).
    /// Returns an empty string if the node is not a component or has no usable text.
    /// </summary>
    private unsafe string ReadFirstTextInComponent(AtkResNode* compNode)
    {
        if (compNode == null || (int)compNode->Type < 1000) return string.Empty;
        var comp = ((AtkComponentNode*)compNode)->Component;
        if (comp == null || !IsReadable(comp)) return string.Empty;
        for (var k = 0; k < comp->UldManager.NodeListCount; k++)
        {
            var gc = comp->UldManager.NodeList[k];
            if (gc == null || gc->Type != NodeType.Text) continue;
            var t = ((AtkTextNode*)gc)->NodeText.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(t) && t.Length > 1) return t;
        }
        return string.Empty;
    }

    /// <summary>
    /// Liest Region-Name und DC-Namen aus einem Regionen-Panel-Node des TitleDCWorldMap.
    /// Struktur (verifiziert via Node-Dump 2026-05-27):
    ///   comp child type=1015 ? gc[?] type=Text = DC-Name (z.B. "Materia", "Chaos")
    ///   comp child type=1009 ? gc[?] type=Text = Regionsname (z.B. "Ozeanien", "Europa")
    ///   comp child type=1006 = Ok-Button � ignorieren.
    /// Gibt ("", []) zur�ck wenn der Node kein Regionen-Panel ist.
    /// </summary>
    private unsafe (string Region, List<(nint Node, string Name, nint WorldList)> DCs) ReadDCRegionPanel(AtkResNode* node)
    {
        if (node == null || (int)node->Type < 1000) return (string.Empty, []);
        var comp = ((AtkComponentNode*)node)->Component;
        if (comp == null) return (string.Empty, []);

        var region = string.Empty;
        var dcs    = new List<(nint Node, string Name, nint WorldList)>();

        // Welten-Liste (Comp 1019) steht in der NodeList direkt VOR ihrem DC-Button
        // (Comp 1015) - verifiziert im Node-Dump 2026-07-09 (Europa: Liste Alpha..Zodiark
        // vor Button "Light", Liste Cerberus..Spriggan vor Button "Chaos").
        nint pendingWorldList = 0;

        for (var j = 0; j < comp->UldManager.NodeListCount; j++)
        {
            var child = comp->UldManager.NodeList[j];
            if (child == null || (int)child->Type < 1000) continue;
            if ((int)child->Type == 1019) { pendingWorldList = (nint)child; continue; }
            if ((int)child->Type == 1006) continue; // Ok button � skip

            var childComp = ((AtkComponentNode*)child)->Component;
            if (childComp == null) continue;

            // Find the first non-trivial Text grandchild
            for (var k = 0; k < childComp->UldManager.NodeListCount; k++)
            {
                var gc = childComp->UldManager.NodeList[k];
                if (gc == null || gc->Type != NodeType.Text) continue;
                var t = ((AtkTextNode*)gc)->NodeText.ToString().Trim();
                if (string.IsNullOrWhiteSpace(t) || t.Length <= 1) continue;

                if ((int)child->Type == 1009)
                    region = t;     // region name label (Ozeanien, Europa, �)
                else if ((int)child->Type == 1015)
                {
                    dcs.Add(((nint)child, t, pendingWorldList)); // DC name button
                    pendingWorldList = 0;
                }
                break; // first text node per child is sufficient
            }
        }

        return (region, dcs);
    }
    private void OnPadCalibrationOpen(AddonEvent type, AddonArgs args)
    {
        _tolk.SpeakInterrupt("Gamepad-Kalibrierung. Escape zum Schlie�en.");
    }

    // -- Benachrichtigungen ------------------------------------------

    private unsafe void OnNotification(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)(nint)args.Addon;
        if (addon == null || !addon->IsVisible) return;
        var text = ReadFirstText(addon);
        if (string.IsNullOrWhiteSpace(text)) return;
        // Info toasts arrive via IToastGui first (ToastService, V4.80); the
        // _WideText/_ScreenText addon draw follows moments later with the
        // same text - skip the echo.
        if (_tolk.WasRecentlySpoken(text, 4)) return;
        _tolk.SpeakInterrupt(text);
    }

    // -- Charaktererstellung: Volk & Geschlecht ----------------------

    // Last announced (race, gender) so PostUpdate only speaks on change.
    private (string Race, string Gender) _lastRaceGender = (string.Empty, string.Empty);
    // Last race announced on mouse hover (MouseOver), separate dedup.
    private string _lastRaceHover = string.Empty;
    // Log the raw gender-symbol codepoints once, to verify the male/female
    // mapping assumption against a real run (symbols are garbled in dumps).
    private bool _raceGenderSymbolsLogged;
    // Log the object table once per RaceGender session to identify the preview actor.
    private bool _previewObjectsLogged;

    /// <summary>
    /// Announces the hovered race on MouseOver. Uses the event target pointer
    /// (verified DC pattern) and cleans the trailing gender glyphs off the
    /// label ("Miqo'te\t glyphs" -> "Miqo'te"). Runs instead of the generic
    /// receive handler, which is skipped for this addon (SpecialUpdateAddons).
    /// </summary>
    private unsafe void OnRaceGenderReceive(AddonEvent type, AddonArgs args)
    {
        if (args is not AddonReceiveEventArgs recv) return;
        var et = (int)recv.AtkEventType;
        if (et != 6 && et != 25) return; // MouseOver / ButtonClick only

        var addon = (AtkUnitBase*)(nint)args.Addon;
        if (addon == null) return;
        if (recv.AtkEvent == 0) return;
        var evt = (AtkEvent*)recv.AtkEvent;
        if (!IsReadable(evt)) return;

        var match = FindComponentForEvent(addon, (nint)evt->Node, (nint)evt->Target);
        if (match == 0) return;

        var race = CleanRaceName(ReadFirstTextInComponent((AtkResNode*)match));
        if (string.IsNullOrWhiteSpace(race) || race == _lastRaceHover) return;
        _lastRaceHover = race;
        _tolk.SpeakInterrupt(race);
        _log.Info($"[Accessibility] RaceGender Hover: '{race}'");
    }

    /// <summary>
    /// Announces the currently selected race and gender in _CharaMakeRaceGender
    /// whenever it changes (covers left/right = gender switch and race changes).
    /// Reads the real checked state instead of interpreting the garbled gender
    /// glyphs: each race container (CT=Base) holds two gender checkboxes -
    /// node id=4 (male symbol) and id=3 (female symbol) - verified via dump
    /// 2026-07-09 21:45. IsChecked comes from AtkComponentCheckBox (FFXIVClientStructs).
    /// </summary>
    private unsafe void OnRaceGenderUpdate(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)(nint)args.Addon;
        if (addon == null || !addon->IsVisible) return;

        var (race, symbolGender, found) = ReadRaceGenderSelection(addon);
        if (!found) return;

        if (race == _lastRaceGender.Race && symbolGender == _lastRaceGender.Gender) return;
        var genderOnly = race == _lastRaceGender.Race && !string.IsNullOrEmpty(_lastRaceGender.Race);
        _lastRaceGender = (race, symbolGender);

        // Gender label from the visible preview model (ground truth). Probe
        // 2026-07-10 10:19: exactly one of 32 models visible, Sex=0, while
        // checkbox id=3 was checked - contradicts the id=4=male assumption.
        // Checkbox mapping stays as fallback when no single model is visible;
        // disagreements are logged so the next run settles the mapping.
        var sex    = GetVisiblePreviewSex();
        var gender = sex switch
        {
            0 => AccessibilityStrings.GenderMale,
            1 => AccessibilityStrings.GenderFemale,
            _ => symbolGender,
        };
        if (sex >= 0 && gender != symbolGender)
            _log.Info($"[Accessibility] RaceGender: Vorschau-Sex={sex} widerspricht Checkbox-Symbol '{symbolGender}' - Vorschau gilt");

        var msg = genderOnly ? gender : $"{race}, {gender}";
        _tolk.SpeakInterrupt(msg);
        _log.Info($"[Accessibility] RaceGender gewählt: Volk='{race}' Geschlecht='{gender}'");
    }

    /// <summary>
    /// Finds the race container whose male or female checkbox is checked and
    /// returns (race name, gender, true). Returns (_, _, false) when nothing is
    /// selected yet. GENDER MAPPING ASSUMPTION (test pending, see STATUS.md):
    /// checkbox node id=4 = male, id=3 = female (FFXIV convention, male first).
    /// The raw symbol codepoints are logged once so the run can confirm this.
    /// </summary>
    private unsafe (string Race, string Gender, bool Found) ReadRaceGenderSelection(AtkUnitBase* addon)
    {
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node == null || (int)node->Type < 1000) continue;
            var comp = ((AtkComponentNode*)node)->Component;
            if (comp == null || !IsReadable(comp)) continue;
            if (comp->GetComponentType() != ComponentType.Base) continue;

            AtkComponentCheckBox* male = null, female = null;
            AtkResNode* nameNode = null;
            for (var j = 0; j < comp->UldManager.NodeListCount; j++)
            {
                var child = comp->UldManager.NodeList[j];
                if (child == null || (int)child->Type < 1000) continue;
                var cc = ((AtkComponentNode*)child)->Component;
                if (cc == null || cc->GetComponentType() != ComponentType.CheckBox) continue;
                switch (child->NodeId)
                {
                    case 4: male     = (AtkComponentCheckBox*)cc; break;
                    case 3: female   = (AtkComponentCheckBox*)cc; break;
                    case 2: nameNode = child; break;
                }
            }
            if (male == null || female == null) continue;

            if (!_raceGenderSymbolsLogged)
            {
                var mSym = male->AtkComponentButton.ButtonTextNode != null
                    ? male->AtkComponentButton.ButtonTextNode->NodeText.ToString() : "?";
                var fSym = female->AtkComponentButton.ButtonTextNode != null
                    ? female->AtkComponentButton.ButtonTextNode->NodeText.ToString() : "?";
                _log.Info($"[Accessibility] RaceGender-Symbole: id4(als männlich)='{ToHex(mSym)}' id3(als weiblich)='{ToHex(fSym)}'");
                _raceGenderSymbolsLogged = true;
            }

            if (male->IsChecked || female->IsChecked)
            {
                var race = CleanRaceName(nameNode != null ? GetTextFromNodeTree(nameNode) : string.Empty);

                // Checked state as CHANGE detector (verified in V4.13). The
                // returned label is only the fallback mapping - the caller
                // overrides it with the visible preview model's Sex byte.
                var gender = male->IsChecked ? AccessibilityStrings.GenderMale : AccessibilityStrings.GenderFemale;
                return (race, gender, true);
            }
        }
        return (string.Empty, string.Empty, false);
    }

    /// <summary>
    /// Returns the Sex byte (0=male, 1=female) of the single visible
    /// character-creation preview model, or -1 when not exactly one model is
    /// visible. All 32 race/tribe/sex combinations sit in the object table at
    /// once (verified 2026-07-10: indices 200-231); only the shown one has
    /// DrawObject.IsVisible=true, the hidden 31 carry RenderFlags 0x40
    /// (verified same log: "Vorschau sichtbar: [200] Sex=0 (1 von 32 Pc)").
    /// Struct fields verified via ilspycmd: GameObject.DrawObject@256,
    /// RenderFlags@280, DrawObject.IsVisible. Full dump once per screen,
    /// summary on every call (callers only invoke this on selection changes).
    /// </summary>
    private unsafe int GetVisiblePreviewSex()
    {
        var pcCount  = 0;
        var lastSex  = -1;
        var visible  = new List<string>();
        foreach (var obj in _objectTable)
        {
            if (obj.Address == nint.Zero) continue;
            var chara = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)obj.Address;
            if (chara->ObjectKind != FFXIVClientStructs.FFXIV.Client.Game.Object.ObjectKind.Pc) continue;
            pcCount++;

            var go   = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)obj.Address;
            var draw = go->DrawObject;
            var vis  = draw != null && IsReadable(draw) && draw->IsVisible;
            if (!_previewObjectsLogged)
                _log.Info($"[Accessibility] CharaMake-Objekt[{obj.ObjectIndex}]: Sex={chara->Sex} RenderFlags=0x{(ulong)go->RenderFlags:X} Draw={(draw != null ? 1 : 0)} Sichtbar={vis}");
            if (vis)
            {
                visible.Add($"[{obj.ObjectIndex}] Sex={chara->Sex}");
                lastSex = chara->Sex;
            }
        }
        _previewObjectsLogged = true;
        _log.Info($"[Accessibility] Vorschau sichtbar: {(visible.Count == 0 ? "KEINES" : string.Join(", ", visible))} ({visible.Count} von {pcCount} Pc)");
        return visible.Count == 1 ? lastSex : -1;
    }

    /// <summary>Takes the race name from a glyph-laden label ("Viera\t..."): keeps text up to the first tab/control char.</summary>
    private static string CleanRaceName(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;
        var sb = new StringBuilder();
        foreach (var ch in raw)
        {
            if (ch == '\t' || ch == '\n' || ch == '\r') break;
            // Stop at the gender glyphs / symbols that trail the name
            if (!char.IsLetter(ch) && ch != 32 && ch != 39) break;
            sb.Append(ch);
        }
        return sb.ToString().Trim();
    }

    private static string ToHex(string s) =>
        string.Join(" ", System.Text.Encoding.UTF8.GetBytes(s).Select(b => b.ToString("X2")));

    // -- Charaktererstellung: Volksstamm ------------------------------

    // Dedup state for the tribe screen, reset on addon close.
    private string _lastTribe      = string.Empty;
    private string _lastTribeHover = string.Empty;

    /// <summary>
    /// Announces the hovered tribe/button on MouseOver in _CharaMakeTribe.
    /// Same event-target pattern as OnRaceGenderReceive. Structure from the
    /// F5 dump 2026-07-10 10:20: tribe options are top-level CheckBox
    /// components (node ids 7/6) whose child id=2 text carries the name
    /// ("Hochlaender"/"Wieslaender"); the nested gender-glyph checkboxes
    /// clean to empty strings and are skipped.
    /// </summary>
    private unsafe void OnTribeReceive(AddonEvent type, AddonArgs args)
    {
        if (args is not AddonReceiveEventArgs recv) return;
        var et = (int)recv.AtkEventType;
        if (et != 6 && et != 25) return; // MouseOver / ButtonClick only

        var addon = (AtkUnitBase*)(nint)args.Addon;
        if (addon == null) return;
        if (recv.AtkEvent == 0) return;
        var evt = (AtkEvent*)recv.AtkEvent;
        if (!IsReadable(evt)) return;

        var match = FindComponentForEvent(addon, (nint)evt->Node, (nint)evt->Target);
        if (match == 0) return;

        var label = CleanRaceName(ReadFirstTextInComponent((AtkResNode*)match));
        if (string.IsNullOrWhiteSpace(label) || label == _lastTribeHover) return;
        _lastTribeHover = label;
        _tolk.SpeakInterrupt(label);
        _log.Info($"[Accessibility] Tribe Hover: '{label}'");
    }

    /// <summary>
    /// Announces the selected tribe in _CharaMakeTribe when it changes
    /// (up/down navigation). Reads the checked top-level CheckBox component
    /// that carries a real text label.
    /// </summary>
    private unsafe void OnTribeUpdate(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)(nint)args.Addon;
        if (addon == null || !addon->IsVisible) return;

        var tribe = ReadTribeSelection(addon);
        if (string.IsNullOrEmpty(tribe) || tribe == _lastTribe) return;
        _lastTribe = tribe;
        _tolk.SpeakInterrupt(tribe);
        _log.Info($"[Accessibility] Tribe gewaehlt: '{tribe}'");
    }

    /// <summary>
    /// Finds the checked top-level CheckBox component with a readable label
    /// (>= 2 chars after cleaning - filters the single-glyph gender boxes).
    /// Returns empty when nothing is selected yet (progress shows "? ? ?").
    /// </summary>
    private unsafe string ReadTribeSelection(AtkUnitBase* addon)
    {
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node == null || (int)node->Type < 1000) continue;
            var comp = ((AtkComponentNode*)node)->Component;
            if (comp == null || !IsReadable(comp)) continue;
            if (comp->GetComponentType() != ComponentType.CheckBox) continue;
            if (!((AtkComponentCheckBox*)comp)->IsChecked) continue;

            var label = CleanRaceName(ReadFirstTextInComponent((AtkResNode*)node));
            if (label.Length >= 2) return label;
        }
        return string.Empty;
    }

    private string _lastCharaMakeHelpText = string.Empty;

    private void OnCharaMakeHelpOpen(AddonEvent type, AddonArgs args)
    {
        // Fresh pane: forget the old text so an unchanged description is
        // announced again when the creation screen is re-entered.
        _lastCharaMakeHelpText = string.Empty;
    }

    /// <summary>
    /// Announces the race/tribe description whenever _CharaMakeHelp rewrites
    /// its text. The description lives in top-level Text node id=4 (dumps
    /// 2026-07-17 16:31: race "Die Elezen sind stolze Nomaden..." and tribe
    /// "Der Volksstamm der Wieslaender..."). Spoken non-interrupting so it
    /// queues behind the race/gender announcement; the next SpeakInterrupt
    /// (arrowing on) cuts it off.
    /// </summary>
    private unsafe void OnCharaMakeHelpUpdate(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)(nint)args.Addon;
        if (addon == null || !addon->IsVisible) return;

        var node = addon->GetNodeById(4);
        if (node == null || node->Type != NodeType.Text || !node->IsVisible()) return;

        var text = ((AtkTextNode*)node)->NodeText.ToString().Trim();
        if (text == _lastCharaMakeHelpText) return;
        _lastCharaMakeHelpText = text;
        if (string.IsNullOrWhiteSpace(text)) return;

        _tolk.Speak(text);
        _log.Info($"[Accessibility] CharaMake-Beschreibung: '{TolkService.Sanitize(text)}'");
    }

    // -- Textfeld-Echo (Charaktererstellung: Kommentar beim Speichern) --

    private string _lastTextInputEcho = string.Empty;

    private void OnCharaMakeInputOpen(AddonEvent type, AddonArgs args)
        => _lastTextInputEcho = string.Empty;

    /// <summary>
    /// Typing echo for the comment field of "Charakterdaten speichern"
    /// (CharaMakeDataInputString, dump 2026-07-17 17:42). Reads the
    /// TextInput component's EvaluatedString (AtkComponentInputBase @224,
    /// ilspycmd 2026-07-17) each frame and speaks the difference: typed
    /// characters, deleted characters ("X gelöscht"), or the full new text
    /// after a mid-string edit.
    /// </summary>
    private unsafe void OnCharaMakeInputUpdate(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)(nint)args.Addon;
        if (addon == null || !addon->IsVisible) return;

        var input = FindTextInput(addon);
        if (input == null) return;

        var text = input->AtkComponentInputBase.EvaluatedString.ToString();
        if (text == _lastTextInputEcho) return;
        SpeakTextEchoDiff(_lastTextInputEcho, text);
        _lastTextInputEcho = text;
        _log.Info($"[Echo] Textfeld: '{TolkService.Sanitize(text)}'");
    }

    /// <summary>
    /// Speaks the change between two text-field states: the appended
    /// characters when typing, "X gelöscht" when deleting a trailing run,
    /// "leer" when cleared, or the full new text after a mid-string edit.
    /// </summary>
    private void SpeakTextEchoDiff(string old, string @new)
    {
        if (@new.Length == 0)
            _tolk.SpeakInterrupt("leer");
        else if (@new.Length > old.Length && @new.StartsWith(old, StringComparison.Ordinal))
            _tolk.SpeakInterrupt(@new[old.Length..]);
        else if (old.Length > @new.Length && old.StartsWith(@new, StringComparison.Ordinal))
            _tolk.SpeakInterrupt($"{old[@new.Length..]} gelöscht");
        else
            _tolk.SpeakInterrupt(@new);
    }

    /// <summary>First top-level TextInput component of an addon.</summary>
    private static unsafe AtkComponentTextInput* FindTextInput(AtkUnitBase* addon)
    {
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node == null || (int)node->Type < 1000) continue;
            var comp = ((AtkComponentNode*)node)->Component;
            if (comp == null) continue;
            if (comp->GetComponentType() == ComponentType.TextInput)
                return (AtkComponentTextInput*)comp;
        }
        return null;
    }

    // -- Namenseingabe (Vorname/Nachname) ----------------------------

    private nint   _lastNameFieldPtr;
    private string _lastNameFieldEcho = string.Empty;

    private void OnCharaMakeNameOpen(AddonEvent type, AddonArgs args)
    {
        _lastNameFieldPtr  = 0;
        _lastNameFieldEcho = string.Empty;
    }

    /// <summary>
    /// True while the global keyboard focus sits inside a visible name-entry
    /// TextInput of _CharaMakeCharaName. Used to mute the generic focus reader
    /// (it would otherwise announce the character counter under the cursor).
    /// </summary>
    private unsafe bool IsFocusInsideNameField(AtkResNode* focused)
    {
        var ptr = _gameGui.GetAddonByName("_CharaMakeCharaName");
        if (ptr.IsNull) return false;
        var addon = (AtkUnitBase*)(nint)ptr;
        if (!addon->IsVisible) return false;
        return FindFocusedNameField(addon, focused) != null;
    }

    /// <summary>
    /// Announces the focused name field's label (Vorname/Nachname) on field
    /// change and echoes typing within the focused field. Two visible
    /// TextInputs, each with its own top-level label text beside it (dump
    /// 2026-07-17 17:57). The label is paired by physical proximity (nearest
    /// short top-level text), which is robust to node order and language.
    /// </summary>
    private unsafe void OnCharaMakeNameUpdate(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)(nint)args.Addon;
        if (addon == null || !addon->IsVisible) return;

        var stage = AtkStage.Instance();
        var focused = stage != null && stage->AtkInputManager != null
            ? stage->AtkInputManager->FocusedNode : null;
        if (focused == null) { _lastNameFieldPtr = 0; return; }

        var field = FindFocusedNameField(addon, focused);
        if (field == null) { _lastNameFieldPtr = 0; return; }

        var input   = (AtkComponentTextInput*)((AtkComponentNode*)field)->Component;
        var content = input->AtkComponentInputBase.EvaluatedString.ToString();

        if ((nint)field != _lastNameFieldPtr)
        {
            _lastNameFieldPtr  = (nint)field;
            _lastNameFieldEcho = content;
            var label = FindNameFieldLabel(addon, field);
            var msg   = content.Length > 0 ? $"{label}, {content}" : label;
            _tolk.SpeakInterrupt(msg);
            _log.Info($"[Name] Feld id={field->NodeId} Label='{label}' Inhalt='{TolkService.Sanitize(content)}'");
            return;
        }

        if (content != _lastNameFieldEcho)
        {
            SpeakTextEchoDiff(_lastNameFieldEcho, content);
            _lastNameFieldEcho = content;
            _log.Info($"[Name] Echo id={field->NodeId}: '{TolkService.Sanitize(content)}'");
        }
    }

    /// <summary>
    /// The visible top-level TextInput component node of the name-entry addon
    /// that contains (or is) the focused node, or null. The invisible
    /// alternate-input TextInputs are skipped.
    /// </summary>
    private static unsafe AtkResNode* FindFocusedNameField(AtkUnitBase* addon, AtkResNode* focused)
    {
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node == null || (int)node->Type < 1000 || !node->IsVisible()) continue;
            var comp = ((AtkComponentNode*)node)->Component;
            if (comp == null || comp->GetComponentType() != ComponentType.TextInput) continue;
            if ((nint)node == (nint)focused) return node;
            for (var j = 0; j < comp->UldManager.NodeListCount; j++)
            {
                if ((nint)comp->UldManager.NodeList[j] == (nint)focused) return node;
            }
        }
        return null;
    }

    /// <summary>
    /// Label of a name field = the nearest visible short top-level text node
    /// (2..20 chars, no "/" so counters are excluded). Proximity uses the
    /// field's own X/Y; labels and fields share the addon root as parent, so
    /// their coordinates are directly comparable.
    /// </summary>
    private unsafe string FindNameFieldLabel(AtkUnitBase* addon, AtkResNode* field)
    {
        var fx = field->X;
        var fy = field->Y;
        var best = string.Empty;
        var bestDist = float.MaxValue;
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var n = addon->UldManager.NodeList[i];
            if (n == null || n->Type != NodeType.Text || !n->IsVisible()) continue;
            var t = ((AtkTextNode*)n)->NodeText.ToString().Trim();
            if (t.Length is < 2 or > 20 || t.Contains('/')) continue;
            var dx = n->X - fx;
            var dy = n->Y - fy;
            var dist = dx * dx + dy * dy;
            if (dist < bestDist) { bestDist = dist; best = t; }
        }
        return best;
    }

    // -- Tastatur-Navigation -----------------------------------------

    // isHorizontal=true f�r Links/Rechts, false f�r Hoch/Runter
    public void Navigate(int delta, bool isHorizontal)
    {
        // Aussehen-Picker der Charaktererstellung zuerst: das Spiel ignoriert
        // Pfeiltasten in den Icon-/Farb-Rastern komplett (Log 2026-07-17
        // 17:24:47: alle vier Pfeile gedrueckt, kein ListProbe-/Focus-Wechsel)
        // - hier navigiert das Plugin die Liste selbst (alle Pfeile = +-1).
        unsafe
        {
            if (TryNavigateCharaMakePicker(delta)) return;
        }

        // SelectYesno: nur Links/Rechts navigieren Ja?Nein
        // Hoch/Runter passieren durch � das Spiel navigiert nativ
        if (!isHorizontal) return;
        unsafe
        {
            var ynPtr = _gameGui.GetAddonByName("SelectYesno");
            if (!ynPtr.IsNull && ((AtkUnitBase*)(nint)ynPtr)->IsVisible)
            {
                var newText = delta < 0 ? _ynConfirmLabel : _ynCancelLabel;
                // Log BEFORE the dedup: user reported left/right silence in
                // V4.31 and this method logged nothing - undiagnosable.
                _log.Info($"[Accessibility] Navigate SelectYesno: delta={delta} -> '{newText}' (zuletzt '{_lastYesNoText}')");
                if (newText == _lastYesNoText) return;
                _lastYesNoText = newText;
                _tolk.SpeakInterrupt(newText);
            }
        }
    }

    public unsafe void NavigateGamepad(int delta)
    {
        var ynPtr = _gameGui.GetAddonByName("SelectYesno");
        if (ynPtr.IsNull || !((AtkUnitBase*)(nint)ynPtr)->IsVisible) return;
        var newText = delta < 0 ? _ynConfirmLabel : _ynCancelLabel;
        if (newText == _lastYesNoText) return;
        _lastYesNoText = newText;
        _tolk.SpeakInterrupt(newText);
        _log.Info($"[Accessibility] SelectYesno Gamepad -> {newText}");
    }

    /// <summary>
    /// Arrow-key navigation for the character-creation icon/colour pickers
    /// (CMFIcon*/CMFColor*). The game ignores arrow keys in these grids
    /// (log 2026-07-17 17:24: no index/focus movement on any arrow), so the
    /// plugin moves the selection itself: SelectItem(idx, dispatchEvent:true)
    /// is the game's own selection path (ilspycmd 2026-07-17) - the dispatch
    /// lets the addon react like on a mouse click (preview update).
    /// Returns false when no CMF picker with entries is visible - the caller
    /// continues with the other navigation branches.
    /// </summary>
    private unsafe bool TryNavigateCharaMakePicker(int delta)
    {
        if (!IsAddonVisible("_CharaMakeTitle")) return false;

        // Only when the picker itself is the TOPMOST menu: with another
        // window on top (log 17:42: stack [BgSelector, CMFIconHair,
        // CharaMakeDataExport]) arrows must NOT move the hidden picker list.
        if (_menuStack.Count > 0 && !_menuStack.Peek().Name.StartsWith("CMF", StringComparison.Ordinal))
            return false;

        // Active picker = the top-of-stack CMF menu when visible and
        // populated; fallback: scan all loaded units. Inactive pickers stay
        // loaded with 0 entries (log 17:23:52), so the count check
        // disambiguates.
        AtkUnitBase* addon = null;
        AtkComponentList* list = null;
        string pickerName = string.Empty;
        if (_menuStack.Count > 0)
        {
            var topName = _menuStack.Peek().Name;
            var p = _gameGui.GetAddonByName(topName);
            if (!p.IsNull)
            {
                var a = (AtkUnitBase*)(nint)p;
                if (a->IsVisible)
                {
                    var l = FindListInAddon(a);
                    if (l != null && GetListEntryCount(l) > 0)
                    {
                        addon = a; list = l; pickerName = topName;
                    }
                }
            }
        }
        if (list == null)
        {
            var mgr = RaptureAtkUnitManager.Instance();
            if (mgr == null) return false;
            for (var u = 0; u < mgr->AllLoadedUnitsList.Count && u < 256; u++)
            {
                var a = mgr->AllLoadedUnitsList.Entries[u].Value;
                if (a == null || !a->IsVisible) continue;
                var n = a->NameString;
                if (!n.StartsWith("CMF", StringComparison.Ordinal)) continue;
                var l = FindListInAddon(a);
                if (l == null || GetListEntryCount(l) <= 0) continue;
                addon = a; list = l; pickerName = n;
                break;
            }
        }
        if (list == null) return false;

        var count = GetListEntryCount(list);
        var cur   = list->SelectedItemIndex;
        var next  = cur < 0
            ? (delta > 0 ? 0 : count - 1)
            : Math.Clamp(cur + delta, 0, count - 1);

        list->SelectItem(next, true);
        list->ScrollToItem((short)next);

        var text = $"{next + 1} von {count}";
        // Prime the list tracker's dedup so the Sel change is not re-announced.
        _lastListAnnounce[pickerName] = $"{next}|{text}";
        _tolk.SpeakInterrupt(text);
        _log.Info($"[CMF] Picker-Navigation: {pickerName} {cur} -> {next} (SelectItem dispatch=true)");
        return true;
    }

    public unsafe void ConfirmYesNo()
    {
        var ynPtr = _gameGui.GetAddonByName("SelectYesno");
        if (ynPtr.IsNull)
        {
            _log.Info("[Accessibility] ConfirmYesNo: SelectYesno nicht gefunden.");
            return;
        }
        var addon = (AtkUnitBase*)(nint)ynPtr;
        if (!addon->IsVisible)
        {
            _log.Info("[Accessibility] ConfirmYesNo: SelectYesno nicht sichtbar.");
            return;
        }
        var isCancel    = _lastYesNoText == _ynCancelLabel;
        var idx         = isCancel ? 1 : 0;
        var shouldClose = addon->ShouldFireCallbackAndHideOrClose;
        _log.Info($"[Accessibility] ConfirmYesNo: '{_lastYesNoText}' idx={idx} ShouldFireCallbackAndHideOrClose={shouldClose}");

        if (isCancel)
        {
            // WORKAROUND: FireCallback(1, {Int:1}) schlie�t SelectYesno nicht (Log 11:40:10 best�tigt).
            // Nein hat keinen Callback � das Spiel schlie�t das Fenster direkt ohne Callback.
            // Fix: Close(true) schlie�t das Fenster ohne den Ja-Callback auszul�sen.
            _log.Info("[Accessibility] ConfirmYesNo: Nein ? Close(true)");
            addon->Close(true);
        }
        else
        {
            // Ja: FireCallback mit idx=0 + ShouldFireCallbackAndHideOrClose=True (best�tigt funktionst�chtig).
            if (!shouldClose)
                addon->ShouldFireCallbackAndHideOrClose = true;
            var v = stackalloc AtkValue[1]; v[0].SetInt(0);
            addon->FireCallback(1, v);
        }
        _lastYesNoText = string.Empty;
    }

    public void AnnounceContextHelp()
    {
        _activeScreenContext = GetCurrentScreenContext();
        var text = _activeScreenContext switch
        {
            ScreenContext.Title        => AccessibilityStrings.HelpForTitle,
            ScreenContext.TitleMenu    => AccessibilityStrings.HelpForTitleMenu,
            ScreenContext.ConfigSystem => AccessibilityStrings.HelpForConfigSystem,
            _                          => AccessibilityStrings.NoHelpAvailable,
        };
        _tolk.SpeakInterrupt(text);
    }

    public void HandleEscapeKey()
    {
        var ctx = GetCurrentScreenContext();
        if (ctx == ScreenContext.ConfigSystem)
        {
            _csPendingSave = false; // Escape = verwerfen
            _tolk.SpeakInterrupt(AccessibilityStrings.Back);
            return;
        }
        if (ctx != ScreenContext.TitleMenu) return;
        _activeScreenContext = ScreenContext.TitleMenu;
        _tolk.SpeakInterrupt(AccessibilityStrings.Back);
    }

    public unsafe void HandleConfirmKey()
    {
        var titleMenuPtr = _gameGui.GetAddonByName("_TitleMenu");
        if (!titleMenuPtr.IsNull && ((AtkUnitBase*)(nint)titleMenuPtr)->IsVisible)
        {
            var selection = GetTitleMenuSelection((AtkUnitBase*)(nint)titleMenuPtr);
            if (!string.IsNullOrWhiteSpace(selection.Item))
            {
                _activeScreenContext = ScreenContext.TitleMenu;
                RememberTitleMenuSelection(selection.Item, selection.Index);
                _tolk.SpeakInterrupt(AccessibilityStrings.Confirmed(selection.Item));
            }
            return;
        }

        var yesNoPtr = _gameGui.GetAddonByName("SelectYesno");
        if (!yesNoPtr.IsNull && ((AtkUnitBase*)(nint)yesNoPtr)->IsVisible)
        {
            ConfirmYesNo();
            return;
        }

        // Enter on a focused ConfigSystem category tab: activate it (the
        // keyboard focus alone never switches the page; user 2026-07-16).
        if (TryActivateFocusedConfigTab()) return;

        // Ok buttons in lobby / character creation (tribe screen, DC map,
        // name dialog, ...): dispatch the button's real click event.
        PressFocusedOk();
    }

    /// <summary>
    /// Activates the focused ConfigSystem category tab by dispatching the
    /// tab's own registered click event - the same path a real mouse click
    /// takes (pattern proven by TryClickButton). The tabs are DragDrop
    /// components; candidate event types are tried in order DragDropClick,
    /// MouseClick, ButtonClick, and every registered type is logged once so
    /// a wrong guess is diagnosable from the log. Returns false when
    /// ConfigSystem is closed or the focus is not on a tab.
    /// </summary>
    private unsafe bool TryActivateFocusedConfigTab()
    {
        var ptr = _gameGui.GetAddonByName("ConfigSystem");
        if (ptr.IsNull) return false;
        var addon = (AtkUnitBase*)(nint)ptr;
        if (!addon->IsVisible) return false;

        var stage = AtkStage.Instance();
        if (stage == null || stage->AtkInputManager == null) return false;
        var focus = stage->AtkInputManager->FocusedNode;
        if (focus == null) return false;

        var top = FindTopLevelOwner(addon, focus, out _);
        if (top == null || (int)top->Type < 1000) return false;
        if (top->NodeId is < 7 or > 14) return false;
        var comp = ((AtkComponentNode*)top)->Component;
        if (comp == null || comp->GetComponentType() != ComponentType.DragDrop) return false;

        // Collect the registered events of the tab node and its children.
        var registered = new List<string>();
        AtkEvent* candidate = null;
        var candidateRank = int.MaxValue;
        ScanClickCandidates(top, registered, ref candidate, ref candidateRank);
        for (var j = 0; j < comp->UldManager.NodeListCount; j++)
        {
            var child = comp->UldManager.NodeList[j];
            if (child != null) ScanClickCandidates(child, registered, ref candidate, ref candidateRank);
        }

        _log.Info($"[CS] Reiter-Aktivierung: Node id={top->NodeId}, Events=[{string.Join(", ", registered)}], " +
                  $"Kandidat={(candidate != null ? candidate->State.EventType.ToString() : "KEINER")}");
        if (candidate == null || candidate->Listener == null || !IsReadable(candidate->Listener))
        {
            _tolk.SpeakInterrupt("Reiter reagiert nicht.");
            return true; // handled: focus WAS on a tab, do not fall through
        }

        var data = default(AtkEventData); // zeroed, Size=40 (ilspycmd)
        candidate->Listener->ReceiveEvent(candidate->State.EventType, (int)candidate->Param, candidate, &data);
        // Remember WHICH tab was pressed so the page-change announcement can
        // state a truthful position - and so the 1.5 s no-change fallback can
        // report a dead press. (V4.70 declared these fields but never set
        // them, so both paths were dead code - found via CS0649 warning.)
        var pressedNodeId = top->NodeId; // pointer deref must not leak into the lambda
        _csExpectedTabIdx = _csTabs.FindIndex(t => t.NodeId == pressedNodeId);
        _csTabActivatedAt = Environment.TickCount64;
        // No speech here: switching the page changes the heading, which the
        // tab-change detector announces ("..., Tab X von 8").
        return true;
    }

    /// <summary>Ranks a node's registered events for tab activation:
    /// DragDropClick before MouseClick before ButtonClick.</summary>
    private unsafe void ScanClickCandidates(AtkResNode* node, List<string> registered, ref AtkEvent* best, ref int bestRank)
    {
        var evt   = node->AtkEventManager.Event;
        var guard = 0;
        while (evt != null && guard++ < 32)
        {
            if (!IsReadable(evt)) return;
            var type = evt->State.EventType;
            registered.Add(type.ToString());
            var rank = type switch
            {
                AtkEventType.DragDropClick => 0,
                AtkEventType.MouseClick    => 1,
                AtkEventType.ButtonClick   => 2,
                _                          => int.MaxValue,
            };
            if (rank < bestRank)
            {
                bestRank = rank;
                best = evt;
            }
            evt = evt->NextEvent;
        }
    }

    /// <summary>
    /// Presses the "Ok" button of the topmost focused lobby/character-creation
    /// addon by dispatching the button's registered ButtonClick event to its
    /// listener - the same path a real mouse click takes, no per-addon
    /// callback guessing. Verified via ilspycmd: AtkResNode.AtkEventManager
    /// .Event (linked list via NextEvent), AtkEvent fields Node@0/Target@8/
    /// Listener@16/Param@24/NextEvent@32/State@40, AtkEventState.EventType@0,
    /// AtkEventListener.ReceiveEvent(type, param, event, data), AtkEventData
    /// Size=40, AtkEventType.ButtonClick=25. Restricted to _CharaMake*/
    /// CharaMake*/TitleDCWorldMap so Enter keeps its game meaning elsewhere.
    /// </summary>
    // Confirm-button labels in priority order (German client).
    private static readonly string[] ConfirmButtonLabels = ["Ok", "Bestätigen"];

    public unsafe void PressFocusedOk()
    {
        var mgr = RaptureAtkUnitManager.Instance();
        if (mgr == null) return;

        // Walk focused list from the back: the step addon sits after the
        // always-focused _CharaMakeProgress (whose Ok is the FINAL confirm),
        // e.g. [_CharaMakeProgress, _CharaMakeTribe] - Log 2026-07-10 10:20.
        var sawCandidate = false;
        for (var i = mgr->FocusedUnitsList.Count - 1; i >= 0 && i < 256; i--)
        {
            var addon = mgr->FocusedUnitsList.Entries[i].Value;
            if (addon == null || !addon->IsVisible) continue;
            var name = addon->NameString;
            if (!name.StartsWith("_CharaMake") && !name.StartsWith("CharaMake") && name != "TitleDCWorldMap")
                continue;

            sawCandidate = true;
            // Confirm labels vary per dialog: "Ok" (Tribe/RaceGender),
            // "Bestätigen" (_CharaMakeCharaName, Log 2026-07-10 11:15).
            foreach (var label in ConfirmButtonLabels)
            {
                if (!TryClickButton(addon, label)) continue;
                _tolk.SpeakInterrupt(AccessibilityStrings.OkPressed);
                _log.Info($"[Accessibility] PressOk: '{label}' in '{name}' ausgeloest");
                return;
            }
            _log.Info($"[Accessibility] PressOk: kein Ok-Button in '{name}'");
        }
        if (sawCandidate) _tolk.SpeakInterrupt(AccessibilityStrings.NoOkButton);
    }

    /// <summary>
    /// Finds a visible top-level Button component whose cleaned label equals
    /// <paramref name="label"/> and dispatches its ButtonClick event to the
    /// registered listener. Returns false when no such button/event exists.
    /// </summary>
    private unsafe bool TryClickButton(AtkUnitBase* addon, string label)
    {
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node == null || !node->IsVisible() || (int)node->Type < 1000) continue;
            var comp = ((AtkComponentNode*)node)->Component;
            if (comp == null || !IsReadable(comp)) continue;
            if (comp->GetComponentType() != ComponentType.Button) continue;

            var text = CleanRaceName(ReadFirstTextInComponent(node));
            if (!string.Equals(text, label, StringComparison.OrdinalIgnoreCase)) continue;

            // Click event registration usually sits on the collision child,
            // sometimes on the component node itself - search both.
            var evt = FindEventOfType(node, AtkEventType.ButtonClick);
            for (var j = 0; j < comp->UldManager.NodeListCount && evt == null; j++)
            {
                var child = comp->UldManager.NodeList[j];
                if (child == null) continue;
                evt = FindEventOfType(child, AtkEventType.ButtonClick);
            }
            if (evt == null || evt->Listener == null || !IsReadable(evt->Listener))
            {
                _log.Info($"[Accessibility] PressOk: Button '{label}' ohne ButtonClick-Event/Listener");
                return false;
            }

            var data = default(AtkEventData); // zeroed, Size=40 (ilspycmd)
            evt->Listener->ReceiveEvent(evt->State.EventType, (int)evt->Param, evt, &data);
            _log.Info($"[Accessibility] PressOk: ButtonClick param={evt->Param} dispatched");
            return true;
        }
        return false;
    }

    /// <summary>Walks a node's event list (AtkEventManager.Event -> NextEvent) for the given type.</summary>
    private unsafe AtkEvent* FindEventOfType(AtkResNode* node, AtkEventType type)
    {
        var evt   = node->AtkEventManager.Event;
        var guard = 0;
        while (evt != null && guard++ < 32)
        {
            if (!IsReadable(evt)) return null;
            if (evt->State.EventType == type) return evt;
            evt = evt->NextEvent;
        }
        return null;
    }

    /// <summary>
    /// Presses the game's own "Zufälliges Aussehen" button in the character-
    /// creation appearance step (_CharaMakeFeature, top-level Button node
    /// id=4 - dump 2026-07-17 16:35). Matched by NODE ID, not label, so it
    /// works in every client language. Same ButtonClick dispatch as
    /// PressFocusedOk/TryClickButton. Honest announcements: reports the
    /// press, not an unverified "appearance changed".
    /// </summary>
    public unsafe void PressRandomAppearance()
    {
        var ptr = _gameGui.GetAddonByName("_CharaMakeFeature");
        if (ptr.IsNull || !((AtkUnitBase*)(nint)ptr)->IsVisible)
        {
            _tolk.SpeakInterrupt("Kein Aussehen-Fenster offen. Nur im Schritt Aussehen der Charaktererschaffung.");
            return;
        }
        var addon = (AtkUnitBase*)(nint)ptr;
        var node  = addon->GetNodeById(4);
        if (node == null || (int)node->Type < 1000 || !node->IsVisible())
        {
            _tolk.SpeakInterrupt("Knopf Zufälliges Aussehen nicht gefunden.");
            _log.Warning("[Accessibility] RandomLook: _CharaMakeFeature id=4 fehlt oder unsichtbar");
            return;
        }
        var comp = ((AtkComponentNode*)node)->Component;
        if (comp == null || !IsReadable(comp) || comp->GetComponentType() != ComponentType.Button)
        {
            _tolk.SpeakInterrupt("Knopf Zufälliges Aussehen nicht gefunden.");
            _log.Warning("[Accessibility] RandomLook: id=4 ist kein Button");
            return;
        }

        var evt = FindEventOfType(node, AtkEventType.ButtonClick);
        for (var j = 0; j < comp->UldManager.NodeListCount && evt == null; j++)
        {
            var child = comp->UldManager.NodeList[j];
            if (child == null) continue;
            evt = FindEventOfType(child, AtkEventType.ButtonClick);
        }
        if (evt == null || evt->Listener == null || !IsReadable(evt->Listener))
        {
            _tolk.SpeakInterrupt("Knopf Zufälliges Aussehen reagiert nicht.");
            _log.Warning("[Accessibility] RandomLook: kein ButtonClick-Event/Listener an id=4");
            return;
        }

        var data = default(AtkEventData); // zeroed, Size=40 (ilspycmd)
        evt->Listener->ReceiveEvent(evt->State.EventType, (int)evt->Param, evt, &data);
        _log.Info($"[Accessibility] RandomLook: ButtonClick param={evt->Param} dispatched");
        _tolk.SpeakInterrupt("Zufälliges Aussehen gedrückt.");
    }

    public unsafe void ReadCurrentFocus()
    {
        // Quest-Journal offen? Dann will der User die QUEST lesen, nicht die Liste.
        if (TryReadQuestDetail()) return;

        // Aktives Men� aus dem Stack
        if (_menuStack.Count > 0)
        {
            var (addonName, _) = _menuStack.Peek();
            var ptr = _gameGui.GetAddonByName(addonName);
            if (!ptr.IsNull && ((AtkUnitBase*)(nint)ptr)->IsVisible)
            {
                var addon = (AtkUnitBase*)(nint)ptr;
                var list  = FindListInAddon(addon);
                if (list != null)
                {
                    var sb = new StringBuilder();
                    var maxItems = Math.Min(list->ListLength, 20);
                    for (var i = 0; i < maxItems; i++)
                    {
                        var item = ReadListItemText(list, i);
                        if (!string.IsNullOrWhiteSpace(item))
                            sb.Append($"{i + 1}. {item}. ");
                    }
                    _tolk.SpeakInterrupt(sb.Length > 0 ? sb.ToString().Trim() : "Leere Liste.");
                    return;
                }
            }
        }

        var talkPt = _gameGui.GetAddonByName("Talk");
        if (!talkPt.IsNull && ((AtkUnitBase*)(nint)talkPt)->IsVisible)
        {
            var text = ReadFirstText((AtkUnitBase*)(nint)talkPt);
            _tolk.SpeakInterrupt(string.IsNullOrEmpty(text) ? "Dialog." : text);
            return;
        }

        _tolk.SpeakInterrupt("Kein aktives Men�.");
    }

    /// <summary>
    /// Reads the quest detail pane (JournalDetail): title, level, objectives,
    /// description. Node ids from the F5 dump 2026-07-11 (docs/game-api.md ->
    /// "Journal/JournalDetail"): canvas component JournalCanvas(20) with
    /// direct text children id=38 title, id=9 level, id=8 description;
    /// objective rows are Multipurpose(21) children with an id=3 text.
    /// Returns false when the pane is not visible or carries no text.
    /// </summary>
    private unsafe bool TryReadQuestDetail()
    {
        var text = BuildQuestText("JournalDetail");
        if (text.Length == 0) return false;
        _tolk.SpeakInterrupt(text);
        return true;
    }

    // Auto-read state: last spoken text per quest window (dedup), and which
    // windows we have already logged a structure probe for (once each).
    private readonly Dictionary<string, string> _lastQuestText = new();
    private readonly HashSet<string> _questProbed = new();

    // Static tab/panel headers of the quest window (JournalDetail/Accept/Result).
    // In the first frames after the window opens the description is not populated
    // yet, so the generic text fallback read every visible canvas text - which are
    // these headers: "Zusammenfassung. Optionen. Vergütung bei Erfolg..." before
    // the actual description (log 2026-07-12). They are chrome, never quest
    // content, so drop them from the spoken output (still logged for diagnosis).
    private static readonly HashSet<string> QuestPanelHeaders = new(StringComparer.Ordinal)
    {
        "Zusammenfassung", "Optionen", "Vergütung bei Erfolg", "Vergütung",
        "Bedingungen", "Information", "Tipps", "Ziel", "Belohnung",
    };

    /// <summary>
    /// Automatically reads a quest window (JournalDetail / JournalAccept /
    /// JournalResult) when its text appears or changes. The text is populated a
    /// few frames after the window opens and changes on page turns, so this runs
    /// on PostUpdate and only speaks when the built text differs from last time.
    /// </summary>
    private unsafe void OnQuestWindowUpdate(AddonEvent type, AddonArgs args)
    {
        var name  = args.AddonName;
        var addon = (AtkUnitBase*)(nint)args.Addon;
        if (addon == null || !addon->IsVisible)
        {
            _lastQuestText.Remove(name);   // reset so it re-reads on reopen
            _questProbed.Remove(name);     // re-log the node structure on reopen
            return;
        }

        // JournalResult is the completion window: read the actual REWARDS
        // (item names + amounts), not the generic section labels.
        var text = name == "JournalResult" ? BuildRewardText(addon) : string.Empty;
        if (string.IsNullOrWhiteSpace(text)) text = BuildQuestText(name);
        if (string.IsNullOrWhiteSpace(text)) return;
        if (_lastQuestText.TryGetValue(name, out var prev) && prev == text) return;

        _lastQuestText[name] = text;
        _log.Info($"[Quest] {name}: '{text}'");
        _tolk.SpeakInterrupt(text);
    }

    /// <summary>
    /// Builds the spoken text for a quest window. All three quest windows use a
    /// JournalCanvas component. For JournalDetail the node ids are verified
    /// (9 level, 8 description; objective rows Multipurpose -> id=3; id=38 is an
    /// EXP "Bonus." badge, not the title, so it is skipped) and give a structured
    /// readout. For JournalAccept/JournalResult the ids are not verified yet, so
    /// if the known ids yield nothing we fall back to reading every visible text
    /// node in the canvas in order. Each text node is logged ([Quest]) so the
    /// structure can be turned into a precise reader later.
    /// "" when the window is not visible, has no canvas, or carries no text.
    /// </summary>
    private unsafe string BuildQuestText(string addonName)
    {
        var ptr = _gameGui.GetAddonByName(addonName);
        if (ptr.IsNull) return string.Empty;
        var addon = (AtkUnitBase*)(nint)ptr;
        if (!addon->IsVisible) return string.Empty;

        var canvas = FindJournalCanvas(addon);
        if (canvas == null)
        {
            // No canvas: log the top-level component types once so we can build a
            // precise reader for this window, and fall back to top-level texts.
            ProbeQuestStructure(addonName, addon);
            return ReadAllTexts(addon);
        }

        var level = string.Empty;
        var description = string.Empty;
        var objectives = new List<string>();
        var allTexts = new List<string>();

        for (var i = 0; i < canvas->UldManager.NodeListCount; i++)
        {
            var node = canvas->UldManager.NodeList[i];
            if (node == null || !node->IsVisible()) continue;

            if (node->Type == NodeType.Text)
            {
                var text = ((AtkTextNode*)node)->NodeText.ToString();
                if (string.IsNullOrWhiteSpace(text)) continue;
                if (!_questProbed.Contains(addonName))
                    _log.Info($"[Quest] {addonName} canvas textNode id={node->NodeId} '{text}'");
                switch (node->NodeId)
                {
                    // id=38 is the EXP "Bonus." badge, NOT the quest title - it
                    // showed "Bonus." on every quest (log 2026-07-12), so it is
                    // pure noise for TTS. Kept in the diagnostic log above, but
                    // excluded from the spoken text.
                    case 38: break;
                    case 9:  level = text; allTexts.Add(text); break;
                    case 8:  description = text; allTexts.Add(text); break;
                    // Skip the static panel headers so the fallback readout does
                    // not lead with "Zusammenfassung. Optionen. Vergütung...".
                    default: if (!QuestPanelHeaders.Contains(text.Trim())) allTexts.Add(text); break;
                }
            }
            else if ((int)node->Type >= 1000)
            {
                var comp = ((AtkComponentNode*)node)->Component;
                if (comp == null || comp->GetComponentType() != ComponentType.Multipurpose) continue;
                for (var j = 0; j < comp->UldManager.NodeListCount; j++)
                {
                    var child = comp->UldManager.NodeList[j];
                    if (child == null || child->Type != NodeType.Text
                        || child->NodeId != 3 || !child->IsVisible()) continue;
                    var text = ((AtkTextNode*)child)->NodeText.ToString();
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    objectives.Add(text);
                    allTexts.Add(text);
                }
            }
        }
        if (allTexts.Count > 0) _questProbed.Add(addonName); // log ids only once text exists

        // Structured output when the verified JournalDetail ids matched (objective
        // + description); else the generic canvas-text fallback for the not-yet-
        // verified windows. The quest name is not read here - it is announced when
        // the quest is selected in the journal list.
        var sb = new StringBuilder();
        if (description.Length > 0 || objectives.Count > 0)
        {
            if (level.Length > 0) sb.Append(level).Append(". ");
            if (objectives.Count > 0) sb.Append($"Ziel: {string.Join(", ", objectives)}. ");
            if (description.Length > 0) sb.Append($"Beschreibung: {description}");
        }
        else if (allTexts.Count > 0)
        {
            sb.Append(string.Join(". ", allTexts));
        }
        return sb.ToString().Trim();
    }

    /// <summary>
    /// Reads the reward summary of the quest-completion window (JournalResult).
    /// Structure verified from a UI dump: the JournalCanvas holds reward entries
    /// as Multipurpose components - item rewards carry an Icon component (resolved
    /// to a name), currency/EXP rewards carry only a number in a TextNineGrid plus
    /// a type IMAGE (no resolvable icon id). So items read by name, amounts read
    /// by value. "" if the window has no rewards yet.
    /// </summary>
    private unsafe string BuildRewardText(AtkUnitBase* addon)
    {
        var canvas = FindJournalCanvas(addon);
        if (canvas == null) return string.Empty;

        var items   = new List<string>();
        var amounts = new List<string>();

        for (var i = 0; i < canvas->UldManager.NodeListCount; i++)
        {
            var node = canvas->UldManager.NodeList[i];
            if (node == null || !node->IsVisible() || (int)node->Type < 1000) continue;
            var comp = ((AtkComponentNode*)node)->Component;
            if (comp == null || comp->GetComponentType() != ComponentType.Multipurpose) continue;

            var icon = FindSlotIcon(comp);
            if (icon != null && icon->IconId != 0)
            {
                var name = _inventory.ResolveIconName(icon->IconId);
                if (string.IsNullOrEmpty(name)) continue;
                var qty = ReadIconQuantity(icon);
                items.Add(qty.Length > 0 ? $"{qty} mal {name}" : name);
            }
            else
            {
                var amount = FindNumericText(comp, 0);
                if (amount.Length > 0) amounts.Add(amount);
            }
        }

        if (items.Count == 0 && amounts.Count == 0) return string.Empty;

        var parts = new List<string>();
        if (items.Count > 0) parts.Add(string.Join(", ", items));

        // WORKAROUND: the currency TYPE is only shown as a UI image (no resolvable
        // icon id), so we label the amounts by position - JournalResult always
        // lists Erfahrung first, then Gil, then other currencies. Logged so the
        // order can be verified/replaced with a real type mapping later.
        string[] labels = { "Erfahrung", "Gil" };
        for (var k = 0; k < amounts.Count; k++)
            parts.Add($"{(k < labels.Length ? labels[k] : "weitere Vergütung")} {amounts[k]}");

        _log.Info($"[Quest] JournalResult Belohnung: items=[{string.Join(" | ", items)}] amounts=[{string.Join(",", amounts)}]");
        return "Belohnung: " + string.Join(". ", parts);
    }

    /// <summary>First visible, purely numeric text (digits plus . , space) in a
    /// component subtree - the reward amount of a currency entry. "" if none.</summary>
    private unsafe string FindNumericText(AtkComponentBase* comp, int depth)
    {
        if (comp == null || depth > 4) return string.Empty;
        for (var i = 0; i < comp->UldManager.NodeListCount; i++)
        {
            var n = comp->UldManager.NodeList[i];
            if (n == null || !n->IsVisible()) continue;
            if (n->Type == NodeType.Text)
            {
                var t = ((AtkTextNode*)n)->NodeText.ToString().Trim();
                if (t.Length > 0 && t.Any(char.IsDigit)
                    && t.All(c => char.IsDigit(c) || c is '.' or ',' or ' '))
                    return t;
            }
            else if ((int)n->Type >= 1000)
            {
                var c = ((AtkComponentNode*)n)->Component;
                var r = FindNumericText(c, depth + 1);
                if (r.Length > 0) return r;
            }
        }
        return string.Empty;
    }

    /// <summary>First JournalCanvas component directly under an addon, or null.</summary>
    private static unsafe AtkComponentBase* FindJournalCanvas(AtkUnitBase* addon)
    {
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node == null || (int)node->Type < 1000) continue;
            var comp = ((AtkComponentNode*)node)->Component;
            if (comp != null && comp->GetComponentType() == ComponentType.JournalCanvas)
                return comp;
        }
        return null;
    }

    /// <summary>Logs the top-level component types of a quest window once, so an
    /// unverified window (no JournalCanvas) can be turned into a precise reader.</summary>
    private unsafe void ProbeQuestStructure(string addonName, AtkUnitBase* addon)
    {
        if (!_questProbed.Add(addonName)) return;
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node == null || (int)node->Type < 1000) continue;
            var comp = ((AtkComponentNode*)node)->Component;
            if (comp == null) continue;
            _log.Info($"[Quest] {addonName} probe: node id={node->NodeId} comp={comp->GetComponentType()} vis={node->IsVisible()}");
        }
    }

    // -- Hilfsmethoden -----------------------------------------------

    private void PushMenu(string name, int initialIndex)
    {
        if (_menuStack.Any(m => m.Name == name)) return;
        _menuStack.Push((name, initialIndex));
        _log.Info($"[Accessibility] Men� ge�ffnet: {name}, Stack-Tiefe: {_menuStack.Count}");
    }

    private void ResetTitleMenuState()
    {
        _titleMenuItems.Clear();
        _lastTitleMenuText = string.Empty;
        _lastTitleMenuIndex = -1;
    }

    private void RememberTitleMenuSelection(string item, int index)
    {
        _lastTitleMenuText = item;
        _lastTitleMenuIndex = index;
    }

    private string FormatTitleMenuSelection(string item, int index, int count)
    {
        return AccessibilityStrings.MenuPosition(item, index + 1, count);
    }

    private unsafe void AnnounceTitleMenuFocusIfChanged(AtkUnitBase* addon)
    {
        // Dump NACH PostUpdate (Spiel hat Fokus bereits intern verschoben)
        if (_dumpOnNextTitleMenuUpdate)
        {
            _dumpOnNextTitleMenuUpdate = false;
            DumpTitleMenuButtonFlags(addon);
        }

        var selection = GetTitleMenuSelection(addon);
        if (selection.Count <= 0 || string.IsNullOrWhiteSpace(selection.Item)) return;
        if (selection.Index == _lastTitleMenuIndex && selection.Item == _lastTitleMenuText) return;

        RememberTitleMenuSelection(selection.Item, selection.Index);
        _tolk.SpeakInterrupt(FormatTitleMenuSelection(selection.Item, selection.Index, selection.Count));
    }

    private unsafe (string Item, int Index, int Count) GetTitleMenuSelection(AtkUnitBase* addon)
    {
        var items = ReadTitleMenuItems(addon);
        _titleMenuItems.Clear();
        _titleMenuItems.AddRange(items);

        if (_titleMenuItems.Count == 0) return (string.Empty, -1, 0);

        var focused = FindTitleMenuFocused(addon);
        var index = FindTitleMenuItemIndex(focused);

        if (index < 0 && _lastTitleMenuIndex >= 0 && _lastTitleMenuIndex < _titleMenuItems.Count)
            index = _lastTitleMenuIndex;

        if (index < 0)
            index = 0;

        return (_titleMenuItems[index], index, _titleMenuItems.Count);
    }

    private int FindTitleMenuItemIndex(string? focusedItem)
    {
        if (string.IsNullOrWhiteSpace(focusedItem)) return -1;

        for (var i = 0; i < _titleMenuItems.Count; i++)
        {
            if (string.Equals(_titleMenuItems[i], focusedItem, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private ScreenContext GetCurrentScreenContext()
    {
        unsafe
        {
            var csPtr = _gameGui.GetAddonByName("ConfigSystem");
            if (!csPtr.IsNull && ((AtkUnitBase*)(nint)csPtr)->IsVisible)
                return ScreenContext.ConfigSystem;

            var titleMenuPtr = _gameGui.GetAddonByName("_TitleMenu");
            if (!titleMenuPtr.IsNull && ((AtkUnitBase*)(nint)titleMenuPtr)->IsVisible)
                return ScreenContext.TitleMenu;

            var titlePtr = _gameGui.GetAddonByName("Title");
            if (!titlePtr.IsNull && ((AtkUnitBase*)(nint)titlePtr)->IsVisible)
                return ScreenContext.Title;
        }

        return ScreenContext.None;
    }

    private unsafe string ReadYesNoQuestion(AtkUnitBase* addon)
    {
        for (uint id = 2; id <= 20; id++)
        {
            var node = addon->GetNodeById(id);
            if (node == null || node->Type != NodeType.Text) continue;
            var text = ((AtkTextNode*)node)->NodeText.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(text) && text.Length > 2 && !YesNoLabels.Contains(text))
                return text;
        }
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node == null || node->Type != NodeType.Text) continue;
            var text = ((AtkTextNode*)node)->NodeText.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(text) && text.Length > 2 && !YesNoLabels.Contains(text))
                return text;
        }
        return string.Empty;
    }

    // -- VirtualQuery: Pointer-Sicherheitsnetz gegen AccessViolationException --

    // Size MUST be 48 (4 trailing padding bytes for 8-byte alignment). With 44,
    // VirtualQuery always fails with ERROR_BAD_LENGTH and IsReadable() returns
    // false for EVERY pointer - proven by standalone repro on 2026-07-09.
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    private struct MEMORY_BASIC_INFORMATION
    {
        [FieldOffset(0)]  public nuint BaseAddress;
        [FieldOffset(8)]  public nuint AllocationBase;
        [FieldOffset(16)] public uint  AllocationProtect;
        [FieldOffset(20)] public ushort PartitionId;
        [FieldOffset(24)] public nuint RegionSize;
        [FieldOffset(32)] public uint  State;
        [FieldOffset(36)] public uint  Protect;
        [FieldOffset(40)] public uint  Type;
    }

    [DllImport("kernel32.dll")]
    private static extern unsafe nuint VirtualQuery(void* lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, nuint dwLength);

    private static unsafe bool IsReadable(void* ptr)
    {
        if (ptr == null) return false;
        if (VirtualQuery(ptr, out var mbi, (nuint)sizeof(MEMORY_BASIC_INFORMATION)) == 0) return false;
        const uint MEM_COMMIT   = 0x1000;
        const uint PAGE_NOACCESS = 0x01;
        const uint PAGE_GUARD    = 0x100;
        return mbi.State == MEM_COMMIT && (mbi.Protect & (PAGE_NOACCESS | PAGE_GUARD)) == 0;
    }

    /// <summary>
    /// Diagnostic companion to IsReadable: returns pointer value plus the
    /// VirtualQuery verdict (State/Protect) so failed checks are explainable in the log.
    /// </summary>
    private static unsafe string DescribeMemory(void* ptr)
    {
        if (ptr == null) return "null";
        if (VirtualQuery(ptr, out var mbi, (nuint)sizeof(MEMORY_BASIC_INFORMATION)) == 0)
            return $"0x{(nint)ptr:X}: VirtualQuery failed";
        return $"0x{(nint)ptr:X}: State=0x{mbi.State:X} Protect=0x{mbi.Protect:X} RegionSize=0x{(ulong)mbi.RegionSize:X}";
    }

    // -- List-Item-Text lesen (abgesichert gegen ung�ltige Renderer-Pointer) --

    private unsafe string ReadListItemText(AtkComponentList* list, int idx)
    {
        // Bound = renderer slots, NOT ListLength: TreeList (Journal) manages
        // its rows in its own Items vector (AtkComponentTreeList.Items @432,
        // ilspycmd 2026-07-11) and leaves the base ListLength at 0 - the old
        // ListLength guard rejected every row, which kept the Journal silent
        // although HoveredItemIndex2 moved (log 2026-07-11 10:35).
        if (idx < 0 || idx >= list->AllocatedItemRendererListLength) return string.Empty;
        try
        {
            var rendererSlot = &list->ItemRendererList[idx];
            if (!IsReadable(rendererSlot)) return string.Empty;

            var renderer = (*rendererSlot).AtkComponentListItemRenderer;
            if (renderer == null || !IsReadable(renderer)) return string.Empty;

            var nodeCount = renderer->UldManager.NodeListCount;
            if (nodeCount == 0 || nodeCount > 128) return string.Empty;

            var nodeList = renderer->UldManager.NodeList;
            if (nodeList == null || !IsReadable(nodeList)) return string.Empty;

            // All visible texts of the row, not just the first one - Journal
            // rows carry level AND quest name ("St. 1" + "Willkommen in
            // Gridania", dump 2026-07-10); invisible nodes are stale slots.
            List<string>? parts = null;
            for (uint i = 0; i < nodeCount; i++)
            {
                var node = nodeList[i];
                if (node == null || node->Type != NodeType.Text || !node->IsVisible()) continue;
                var textNode = (AtkTextNode*)node;
                if (textNode->NodeText.IsEmpty) continue;
                var text = textNode->NodeText.ToString();
                if (!string.IsNullOrEmpty(text)) (parts ??= []).Add(text);
            }
            if (parts != null) return string.Join(", ", parts);
        }
        catch (Exception ex) { _log.Warning($"[Accessibility] ListItem-Fehler: {ex.Message}"); }
        return string.Empty;
    }

    /// <summary>
    /// ConfigKeybind (Tastenbelegung) row: "command, Taste key" / ", keine
    /// Taste". The bound keys live in button COMPONENTS inside the row
    /// (binding 1 = component id=6, binding 2 = id=5, key label = text id=5
    /// inside each; command label = direct text id=2 - F5 dump 2026-07-17).
    /// The generic reader only sees direct text nodes, so rows were announced
    /// without their keys ("Vorwärts" instead of "Vorwärts, Taste W").
    /// Returns "" when the structure does not match (caller falls back).
    /// </summary>
    private unsafe string ReadConfigKeybindRow(AtkComponentList* list, int idx)
    {
        if (idx < 0 || idx >= list->AllocatedItemRendererListLength) return string.Empty;
        try
        {
            var rendererSlot = &list->ItemRendererList[idx];
            if (!IsReadable(rendererSlot)) return string.Empty;
            var renderer = (*rendererSlot).AtkComponentListItemRenderer;
            if (renderer == null || !IsReadable(renderer)) return string.Empty;
            return ReadConfigKeybindRenderer((AtkComponentBase*)renderer);
        }
        catch (Exception ex) { _log.Warning($"[Accessibility] ConfigKeybind-Zeile: {ex.Message}"); }
        return string.Empty;
    }

    /// <summary>
    /// Reads one Tastenbelegung row from its ListItemRenderer component.
    /// Rows without binding button components (section headers like
    /// "Laufen und Steuern") return just their label - only real command
    /// rows get ", Taste X" / ", keine Taste" appended.
    /// </summary>
    private unsafe string ReadConfigKeybindRenderer(AtkComponentBase* renderer)
    {
        try
        {
            var nodeCount = renderer->UldManager.NodeListCount;
            var nodeList = renderer->UldManager.NodeList;
            if (nodeList == null || nodeCount == 0 || nodeCount > 128 || !IsReadable(nodeList))
                return string.Empty;

            string label = string.Empty, key1 = string.Empty, key2 = string.Empty;
            var sawBindingButton = false;
            for (uint i = 0; i < nodeCount; i++)
            {
                var node = nodeList[i];
                if (node == null) continue;
                if (node->Type == NodeType.Text && node->NodeId == 2 && node->IsVisible())
                {
                    label = ((AtkTextNode*)node)->NodeText.ToString();
                }
                else if ((int)node->Type >= 1000 && node->NodeId is 5 or 6)
                {
                    var comp = ((AtkComponentNode*)node)->Component;
                    if (comp == null) continue;
                    sawBindingButton = true;
                    var keyText = ReadComponentTextById(comp, 5);
                    if (node->NodeId == 6) key1 = keyText;
                    else key2 = keyText;
                }
            }

            if (string.IsNullOrWhiteSpace(label)) return string.Empty;
            if (!sawBindingButton) return label;
            var keys = new List<string>(2);
            if (!string.IsNullOrWhiteSpace(key1)) keys.Add(key1);
            if (!string.IsNullOrWhiteSpace(key2)) keys.Add(key2);
            return keys.Count > 0
                ? $"{label}, Taste {string.Join(", ", keys)}"
                : $"{label}, keine Taste";
        }
        catch (Exception ex) { _log.Warning($"[Accessibility] ConfigKeybind-Zeile: {ex.Message}"); }
        return string.Empty;
    }

    /// <summary>
    /// While Tastenbelegung is open, resolves the globally focused node to
    /// its list row and reads it with the dedicated row reader (command plus
    /// bound key). False when the window is closed or the node belongs to no
    /// row (tabs, close button) - the caller falls back to the generic
    /// reader. Runs per frame on purpose: the list scrolls UNDER a fixed
    /// focus node (same node ptr, new row content, log 2026-07-17 13:12),
    /// so the row text must be re-read even without a focus change.
    /// </summary>
    private unsafe bool TryReadConfigKeybindFocusRow(AtkResNode* node, out string text)
    {
        text = string.Empty;
        if (!IsAddonVisible("ConfigKeybind")) return false;
        var renderer = ClimbToItemRenderer(node);
        if (renderer == null)
        {
            // Log once per focus change (_lastFocusedNodePtr is still the
            // previous node here), not per frame.
            if ((nint)node != _lastFocusedNodePtr)
                _log.Info($"[Focus] ConfigKeybind: Node id={node->NodeId} liegt in keinem ListItemRenderer.");
            return false;
        }
        text = ReadConfigKeybindRenderer((AtkComponentBase*)renderer);
        return !string.IsNullOrEmpty(text);
    }

    /// <summary>
    /// While the character-creation "Aussehen" pickers are open, resolves the
    /// globally focused node to its row in a CMF icon/colour list and returns
    /// "position von count". Those rows are pure image swatches without any
    /// text (dump 2026-07-17 16:35: CMFIconFeature rows = ListItemRenderer
    /// with only Image children), so the position is the only speakable
    /// content. ListItemIndex is the renderer's DATA row (ilspycmd
    /// 2026-07-17, offset 388) - correct even when the list scrolls under a
    /// fixed focus node. False when no CMF list owns the renderer - the
    /// caller falls back to the generic reader.
    /// </summary>
    private unsafe bool TryReadCharaMakeIconFocusRow(AtkResNode* node, out string text)
    {
        text = string.Empty;
        // _CharaMakeTitle is visible during the whole character creation
        // (dumps 2026-07-17 16:31 + 16:35) - cheap gate before any scanning.
        if (!IsAddonVisible("_CharaMakeTitle")) return false;
        var renderer = ClimbToItemRenderer(node);
        if (renderer == null) return false;

        var mgr = RaptureAtkUnitManager.Instance();
        if (mgr == null) return false;
        for (var u = 0; u < mgr->AllLoadedUnitsList.Count && u < 256; u++)
        {
            var a = mgr->AllLoadedUnitsList.Entries[u].Value;
            if (a == null || !a->IsVisible) continue;
            if (!a->NameString.StartsWith("CMF", StringComparison.Ordinal)) continue;
            var list = FindListInAddon(a);
            if (list == null) continue;
            var slots = Math.Min(list->AllocatedItemRendererListLength, 64);
            for (var i = 0; i < slots; i++)
            {
                if (list->ItemRendererList[i].AtkComponentListItemRenderer != renderer) continue;
                var count = GetListEntryCount(list);
                var idx   = renderer->ListItemIndex;
                if (count <= 0 || idx < 0 || idx >= count) return false;
                text = $"{idx + 1} von {count}";
                return true;
            }
        }
        return false;
    }

    /// <summary>First visible, non-empty text node with the given id inside a
    /// component (direct children only).</summary>
    private static unsafe string ReadComponentTextById(AtkComponentBase* comp, uint textNodeId)
    {
        for (var i = 0; i < comp->UldManager.NodeListCount; i++)
        {
            var node = comp->UldManager.NodeList[i];
            if (node == null || node->Type != NodeType.Text || node->NodeId != textNodeId) continue;
            if (!node->IsVisible()) continue;
            var text = ((AtkTextNode*)node)->NodeText.ToString();
            if (!string.IsNullOrEmpty(text)) return text;
        }
        return string.Empty;
    }

    // -- List index probe (which field tracks keyboard navigation?) --

    private static unsafe (int Sel, int Hov, int Hov2, int Hov3, int Held, string Hl) ReadListIndices(AtkComponentList* list)
    {
        // Highlight mask over the renderer slots (ListItem.IsHighlighted,
        // ilspycmd 2026-07-11). AllocatedItemRendererListLength bounds the
        // real allocation - virtual lists allocate fewer slots than ListLength.
        var hl = new StringBuilder();
        var n = Math.Min(list->AllocatedItemRendererListLength, 64);
        for (var i = 0; i < n; i++)
        {
            if (list->ItemRendererList[i].IsHighlighted)
            {
                if (hl.Length > 0) hl.Append(' ');
                hl.Append(i);
            }
        }
        return (list->SelectedItemIndex, list->HoveredItemIndex,
                list->HoveredItemIndex2, list->HoveredItemIndex3,
                list->HeldItemIndex, hl.ToString());
    }

    private unsafe void TrackListIndices(string name, AtkComponentList* list)
    {
        var state = ReadListIndices(list);
        if (!_listIndexState.TryGetValue(name, out var prev))
        {
            _listIndexState[name] = state;
            _log.Info($"[ListProbe] {name} Basis: Sel={state.Sel} Hov={state.Hov} Hov2={state.Hov2} Hov3={state.Hov3} Held={state.Held} HL=[{state.Hl}] Len={list->ListLength}");
            return;
        }
        if (state == prev) return;

        _listIndexState[name] = state;
        _log.Info($"[ListProbe] {name}: Sel={state.Sel} Hov={state.Hov} Hov2={state.Hov2} Hov3={state.Hov3} Held={state.Held} HL=[{state.Hl}]");

        // Announce the row of whichever candidate moved. Priority is only a
        // tie-breaker; the probe log shows which one actually fired so the
        // list can be trimmed to the verified field afterwards.
        var idx = -1;
        if      (state.Sel  != prev.Sel  && state.Sel  >= 0) idx = state.Sel;
        else if (state.Hov  != prev.Hov  && state.Hov  >= 0) idx = state.Hov;
        else if (state.Hov2 != prev.Hov2 && state.Hov2 >= 0) idx = state.Hov2;
        else if (state.Hov3 != prev.Hov3 && state.Hov3 >= 0) idx = state.Hov3;
        else if (state.Hl != prev.Hl && state.Hl.Length > 0)
            idx = int.Parse(state.Hl.Split(' ')[0]);
        if (idx < 0) return;

        // Tastenbelegung: dedicated row reader so the bound key is announced
        // with the command; generic reader as fallback if the structure drifts.
        var text = name == "ConfigKeybind" ? ReadConfigKeybindRow(list, idx) : string.Empty;
        if (string.IsNullOrEmpty(text)) text = ReadListItemText(list, idx);
        // CharaMake-Aussehen-Picker (CMFIcon*/CMFColor*): rows are pure image
        // swatches without text (dump 2026-07-17 16:35) - the position is the
        // only speakable content.
        if (string.IsNullOrEmpty(text) && name.StartsWith("CMF", StringComparison.Ordinal))
            text = $"{idx + 1} von {GetListEntryCount(list)}";
        if (string.IsNullOrEmpty(text)) return;
        var announce = $"{idx}|{text}";
        if (_lastListAnnounce.TryGetValue(name, out var lastA) && lastA == announce) return;
        _lastListAnnounce[name] = announce;
        _log.Info($"[Accessibility] {name} List-Navigation: [{idx}] {text}");
        _tolk.SpeakInterrupt(AppendShopGearInfo(text));
    }

    // Row count for announcements: TreeList keeps the base ListLength at 0
    // and counts its rows in the derived Items vector instead (Journal said
    // "0 Eintraege"; AtkComponentTreeList.Items @432, ilspycmd 2026-07-11).
    private static unsafe int GetListEntryCount(AtkComponentList* list)
    {
        if (list->ListLength > 0) return list->ListLength;
        if (((AtkComponentBase*)list)->GetComponentType() == ComponentType.TreeList)
            return (int)((AtkComponentTreeList*)list)->Items.LongCount;
        return list->ListLength;
    }

    // TreeList (Journal) inherits AtkComponentList at offset 0
    // ([Inherits<AtkComponentList>(0)], ilspycmd 2026-07-10) - the cast below
    // is safe and SelectedItemIndex/ListLength work for both.
    private static bool IsListComponent(ComponentType type) =>
        type is ComponentType.List or ComponentType.TreeList;

    private static unsafe AtkComponentList* FindListInAddon(AtkUnitBase* addon)
    {
        // Component nodes carry RAW type values >= 1000 (NodeType.Component
        // = 10000 is only what GetNodeType() returns, ilspycmd 2026-07-11).
        // The old check `Type != NodeType.Component` was therefore NEVER true
        // for real components - this function returned null since its
        // introduction and the universal list navigation was dead code
        // (Journal/SystemMenu/SelectString all silent, log 2026-07-11).
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node == null || (int)node->Type < 1000) continue;
            var comp = ((AtkComponentNode*)node)->Component;
            if (comp == null) continue;
            if (IsListComponent(comp->GetComponentType()))
                return (AtkComponentList*)comp;
            for (var j = 0; j < comp->UldManager.NodeListCount; j++)
            {
                var inner = comp->UldManager.NodeList[j];
                if (inner == null || (int)inner->Type < 1000) continue;
                var innerComp = ((AtkComponentNode*)inner)->Component;
                if (innerComp == null) continue;
                if (IsListComponent(innerComp->GetComponentType()))
                    return (AtkComponentList*)innerComp;
            }
        }
        return null;
    }

    // -- Bestiarium (Jagdtagebuch / MonsterNote) --------------------
    //
    // Datenmodell (ilspycmd 2026-07-12): AtkComponentTreeList.Items (@432) =
    // logische Zeilen in visueller Reihenfolge; jedes AtkComponentTreeListItem
    // traegt StringValues (@24, die vom Spiel gesetzten Anzeige-Strings) und
    // einen Renderer-Zeiger (@48). Der fokussierte Node liegt in einem
    // ListItemRenderer - wir klettern hoch, ordnen ihn einer Item-Zeile zu und
    // lesen deren Strings (Name + Fortschritt). Die Listen-Indizes bewegen sich
    // hier bei Tastatur-Navigation NICHT (Log 2026-07-12: alle -1).

    private string _lastBestiaryRow = string.Empty;
    private nint   _lastBestiaryRendererPtr;
    private string? _selectedBestiaryMonster;

    /// <summary>
    /// The hunting log monster whose row is currently focused in the open
    /// bestiary, or null (bestiary closed / a rank or reward row selected).
    /// Plugin.cs uses this for the auto-walk key: track the monster nearby
    /// or announce its habitat.
    /// </summary>
    public string? SelectedBestiaryMonster =>
        _selectedBestiaryMonster != null && IsAddonVisible("MonsterNote")
            ? _selectedBestiaryMonster
            : null;

    private unsafe void OnMonsterNoteUpdate(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)(nint)args.Addon;
        if (addon == null || !addon->IsVisible) return;

        var tree = FindTreeList(addon);
        if (tree == null) return;

        var stage = AtkStage.Instance();
        if (stage == null || stage->AtkInputManager == null) return;
        var focus = stage->AtkInputManager->FocusedNode;
        if (focus == null) return;

        var renderer = ClimbToItemRenderer(focus);
        if (renderer == null) return;

        var (row, index, total) = ReadTreeRow(tree, renderer, out var item);
        if ((nint)renderer == _lastBestiaryRendererPtr && row == _lastBestiaryRow) return;

        // Ground-Truth-Probe (einmal pro Zeilenwechsel, kein Frame-Spam): alle
        // Text-Nodes der Zeile (sichtbar UND unsichtbar) + die Roh-Strings.
        // Klaert, wo bei unfertigen Raengen ("0/10, NEU") der Rang-Name steckt.
        ProbeBestiaryRow(renderer, item);

        _lastBestiaryRendererPtr = (nint)renderer;
        _lastBestiaryRow = row;
        if (string.IsNullOrEmpty(row)) return;

        var announce = index >= 0 && total > 0 ? $"{index + 1} von {total}, {row}" : row;

        // Monster rows get their habitat appended (MonsterNoteTarget sheet) so
        // the user hears WHERE to hunt; the name also arms the auto-walk key.
        _selectedBestiaryMonster = null;
        if (TryExtractBestiaryMonster(row, out var monster))
        {
            var habitat = _bestiary.GetHabitat(monster);
            if (habitat != null)
            {
                _selectedBestiaryMonster = monster;
                announce += $". Lebt in {habitat}";
            }
            else
            {
                // Name mismatch UI vs. BNpcName sheet - log for ground truth.
                _log.Info($"[Bestiary] Kein Lebensraum für '{monster}' (kein Sheet-Treffer).");
            }
        }
        _tolk.SpeakInterrupt(announce);
    }

    /// <summary>
    /// Monster rows read "Name, X von Y" after formatting; rank headers and
    /// rewards carry no progress token. Returns the name part of such a row.
    /// </summary>
    private static bool TryExtractBestiaryMonster(string formattedRow, out string monster)
    {
        monster = string.Empty;
        string? name = null;
        var hasProgress = false;
        foreach (var part in formattedRow.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (IsSpokenProgress(part)) hasProgress = true;
            else name ??= part;
        }
        if (!hasProgress || name == null || name.All(char.IsDigit)) return false;
        monster = name;
        return true;
    }

    /// <summary>True for a progress token as spoken by FormatBestiaryRow ("0 von 3").</summary>
    private static bool IsSpokenProgress(string part)
    {
        var pieces = part.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return pieces.Length == 3
            && pieces[0].All(char.IsDigit)
            && pieces[1] == "von"
            && pieces[2].All(char.IsDigit);
    }

    /// <summary>
    /// Reads the whole open bestiary (Hunting Log) TreeList aloud: every logical
    /// row in visual order (rank headers, monsters with progress, rewards). Bound
    /// to a key so the user hears the full list at once instead of arrowing
    /// through it. Reads the game's own display strings - no sheet mapping.
    /// </summary>
    public unsafe void AnnounceBestiaryOverview()
    {
        var ptr = _gameGui.GetAddonByName("MonsterNote");
        if (ptr.IsNull || !((AtkUnitBase*)(nint)ptr)->IsVisible)
        {
            _tolk.SpeakInterrupt("Bestiarium ist nicht geöffnet.");
            return;
        }

        var addon = (AtkUnitBase*)(nint)ptr;
        var tree = FindTreeList(addon);
        if (tree == null)
        {
            _tolk.SpeakInterrupt("Bestiarium-Liste nicht gefunden.");
            return;
        }

        var total = (int)tree->Items.LongCount;
        var rows = new List<string>();
        for (var i = 0; i < total; i++)
        {
            var item = tree->Items[i].Value;
            if (item == null) continue;
            var text = ReadItemStrings(item);
            if (string.IsNullOrEmpty(text) && item->Renderer != null)
                text = ReadRendererTexts(item->Renderer);
            text = FormatBestiaryRow(text);
            if (string.IsNullOrEmpty(text)) continue;
            // Monsters get their habitat so the overview answers "where to go"
            if (TryExtractBestiaryMonster(text, out var monster)
                && _bestiary.GetHabitat(monster) is { } habitat)
                text += $", lebt in {habitat}";
            rows.Add(text);
        }

        _log.Info($"[Bestiary] Uebersicht: {rows.Count} Zeilen von {total} Items");
        if (rows.Count == 0)
        {
            _tolk.SpeakInterrupt("Bestiarium ist leer.");
            return;
        }
        _tolk.SpeakInterrupt($"Bestiarium, {rows.Count} Einträge. " + string.Join(". ", rows));
    }

    private static unsafe AtkComponentTreeList* FindTreeList(AtkUnitBase* addon)
    {
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node == null || (int)node->Type < 1000) continue;
            var comp = ((AtkComponentNode*)node)->Component;
            if (comp != null && comp->GetComponentType() == ComponentType.TreeList)
                return (AtkComponentTreeList*)comp;
        }
        return null;
    }

    private static unsafe AtkComponentListItemRenderer* ClimbToItemRenderer(AtkResNode* node)
    {
        for (var up = 0; up < 8 && node != null; up++)
        {
            if ((int)node->Type >= 1000)
            {
                var comp = ((AtkComponentNode*)node)->Component;
                if (comp != null && comp->GetComponentType() == ComponentType.ListItemRenderer)
                    return (AtkComponentListItemRenderer*)comp;
            }
            node = node->ParentNode;
        }
        return null;
    }

    private unsafe (string Text, int Index, int Total) ReadTreeRow(
        AtkComponentTreeList* tree, AtkComponentListItemRenderer* renderer, out AtkComponentTreeListItem* item)
    {
        var total = (int)tree->Items.LongCount;
        var index = -1;
        item = null;
        for (var i = 0; i < total; i++)
        {
            var it = tree->Items[i].Value;
            if (it != null && it->Renderer == renderer) { index = i; item = it; break; }
        }

        var text = item != null ? ReadItemStrings(item) : string.Empty;
        if (string.IsNullOrEmpty(text)) text = ReadRendererTexts(renderer);
        return (FormatBestiaryRow(text), index, total);
    }

    /// <summary>The game's own display strings for a TreeList row (StringValues).</summary>
    private static unsafe string ReadItemStrings(AtkComponentTreeListItem* item)
    {
        var parts = new List<string>();
        var n = (int)item->StringValues.LongCount;
        for (var i = 0; i < n; i++)
        {
            var s = item->StringValues[i];
            if (!s.HasValue) continue;
            var t = s.ToString().Trim();
            if (!string.IsNullOrEmpty(t)) parts.Add(t);
        }
        return string.Join(", ", parts);
    }

    /// <summary>Fallback: visible text nodes of a row's renderer in node order.</summary>
    private static unsafe string ReadRendererTexts(AtkComponentListItemRenderer* renderer)
    {
        var comp = (AtkComponentBase*)renderer;
        var parts = new List<string>();
        for (var i = 0; i < comp->UldManager.NodeListCount; i++)
        {
            var n = comp->UldManager.NodeList[i];
            if (n == null || n->Type != NodeType.Text || !n->IsVisible()) continue;
            var t = ((AtkTextNode*)n)->NodeText.ToString().Trim();
            if (!string.IsNullOrEmpty(t)) parts.Add(t);
        }
        return string.Join(", ", parts);
    }

    /// <summary>"0/3, Marienkaefer" -> "Marienkaefer, 0 von 3": moves the progress
    /// token to the end and spells it out for speech; order otherwise preserved.</summary>
    private static string FormatBestiaryRow(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var parts = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var names = new List<string>();
        var progress = new List<string>();
        foreach (var p in parts)
        {
            if (TryFormatProgress(p, out var pretty)) progress.Add(pretty);
            else names.Add(p);
        }
        return string.Join(", ", names.Concat(progress));
    }

    private static bool TryFormatProgress(string part, out string result)
    {
        result = part;
        var slash = part.IndexOf('/');
        if (slash <= 0 || slash >= part.Length - 1) return false;
        var left = part[..slash];
        var right = part[(slash + 1)..];
        if (left.All(char.IsDigit) && right.All(char.IsDigit))
        {
            result = $"{left} von {right}";
            return true;
        }
        return false;
    }

    private unsafe void ProbeBestiaryRow(AtkComponentListItemRenderer* renderer, AtkComponentTreeListItem* item)
    {
        var comp = (AtkComponentBase*)renderer;
        var sb = new StringBuilder();
        for (var i = 0; i < comp->UldManager.NodeListCount; i++)
        {
            var n = comp->UldManager.NodeList[i];
            if (n == null || n->Type != NodeType.Text) continue;
            var t = ((AtkTextNode*)n)->NodeText.ToString().Trim();
            sb.Append($" [id={n->NodeId} vis={(n->IsVisible() ? 1 : 0)} '{t}']");
        }
        var raw = item != null ? ReadItemStrings(item) : "(kein Item)";
        var rid = comp->OwnerNode != null ? ((AtkResNode*)comp->OwnerNode)->NodeId : 0;
        _log.Info($"[Bestiary] Probe rendererId={rid} texts:{sb} strings=[{raw}]");
    }

    /// <summary>
    /// Findet den Node mit dem Fokus-Flag (HasFocusBit) oder relevanten Hover-Effekten.
    /// R�ckgabe: (Text, Key)
    /// </summary>
    private unsafe (string? Text, uint Key) FindFocusedText(AtkUnitBase* addon)
    {
        const ushort HasFocusBit = 0x100;
        const ushort HasCollisionBit = 0x10;

        // 1. Durchlauf: Echter Fokus (0x100) auf Top-Level oder in Kindern
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node == null || !node->IsVisible()) continue;

            if (((ushort)node->NodeFlags & HasFocusBit) != 0)
                return (GetTextFromNodeTree(node), node->NodeId);

            if ((int)node->Type >= 1000)
            {
                var comp = ((AtkComponentNode*)node)->Component;
                if (comp != null)
                {
                    for (var j = 0; j < comp->UldManager.NodeListCount; j++)
                    {
                        var child = comp->UldManager.NodeList[j];
                        // Wenn ein Kind-Node den Fokus hat, nehmen wir den Text des ganzen Komponenten-Nodes
                        if (child != null && child->IsVisible() && ((ushort)child->NodeFlags & HasFocusBit) != 0)
                            return (GetTextFromNodeTree(node), node->NodeId * 1000 + child->NodeId);
                    }
                }
            }
        }

        // 2. Durchlauf: Fallback auf Collision (0x10) f�r statische Men�s (z.B. TitleDCWorldMap)
        // Wir suchen hier nur nach dem Glow auf Kind 4, um Fehlalarme zu vermeiden.
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node == null || !node->IsVisible() || (int)node->Type < 1000) continue;

            var comp = ((AtkComponentNode*)node)->Component;
            if (comp == null) continue;

            for (var j = 0; j < comp->UldManager.NodeListCount; j++)
            {
                var child = comp->UldManager.NodeList[j];
                if (child != null && child->IsVisible() && child->NodeId == 4 && ((ushort)child->NodeFlags & HasCollisionBit) != 0)
                {
                    var text = GetTextFromNodeTree(node);
                    if (!string.IsNullOrWhiteSpace(text) && text.Length > 1)
                        return (text, node->NodeId * 1000 + child->NodeId);
                }
            }
        }

        return (null, 0);
    }

    /// <summary>
    /// Rekursive Text-Extraktion aus einem beliebigen Node-Baum.
    /// Erkennt ComponentType und liest entsprechend aus:
    ///   List      ? SelectedItemIndex-Text
    ///   Button    ? ButtonTextNode (direkt, effizient)
    ///   CheckBox, RadioButton, ListItemRenderer, alle anderen
    ///             ? alle Kind-Nodes werden rekursiv durchsucht,
    ///               Texte werden kombiniert (Label + Wert/Status)
    /// Depth-Limit = 6 verhindert Stack-Overflow bei tiefen B�umen.
    /// </summary>
    private unsafe string GetTextFromNodeTree(AtkResNode* node, int depth = 0)
    {
        if (node == null || depth > 6) return string.Empty;

        // Text-Node: Inhalt direkt zur�ckgeben
        if (node->Type == NodeType.Text)
        {
            var t = ((AtkTextNode*)node)->NodeText.ToString().Trim();
            return t.Length > 1 ? t : string.Empty;
        }

        // Kein Komponenten-Node (Image, Collision, Res usw.): ignorieren
        if ((int)node->Type < 1000) return string.Empty;

        var compNode = (AtkComponentNode*)node;
        var comp     = compNode->Component;
        if (comp == null) return string.Empty;

        var compType = comp->GetComponentType();

        // Liste / Dropdown: aktuell gew�hlten Eintrag lesen
        if (compType == ComponentType.List)
        {
            var list = (AtkComponentList*)comp;
            return ReadListItemText(list, Math.Max(0, list->SelectedItemIndex));
        }

        // Button: ButtonTextNode bevorzugen (direkter Zugriff, keine Rekursion n�tig)
        if (compType == ComponentType.Button)
        {
            var btn = (AtkComponentButton*)comp;
            if (btn->ButtonTextNode != null)
            {
                var t = btn->ButtonTextNode->NodeText.ToString().Trim();
                if (t.Length > 1) return t;
            }
        }

        // CheckBox, RadioButton, ListItemRenderer und alle weiteren:
        // Kind-Nodes rekursiv durchsuchen � ergibt automatisch Label + Wert/Status.
        var sb = new StringBuilder();
        for (var j = 0; j < comp->UldManager.NodeListCount; j++)
        {
            var child = comp->UldManager.NodeList[j];
            if (child == null) continue;
            var childText = GetTextFromNodeTree(child, depth + 1);
            if (!string.IsNullOrWhiteSpace(childText))
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(childText);
            }
        }
        return sb.ToString();
    }

    private unsafe string ReadFirstText(AtkUnitBase* addon)
    {
        for (uint id = 2; id <= 12; id++)
        {
            var node = addon->GetNodeById(id);
            if (node == null || node->Type != NodeType.Text) continue;
            var text = ((AtkTextNode*)node)->NodeText.ToString();
            if (!string.IsNullOrWhiteSpace(text) && text.Length > 1) return text;
        }
        return string.Empty;
    }

    private unsafe List<string> ReadTitleMenuItems(AtkUnitBase* addon)
    {
        var items = new List<string>();
        // R�ckw�rts iterieren ? niedrigere Node-IDs zuerst (visuell oben ? unten).
        // AtkComponentButton (NodeType=1001, ComponentType=1) hat ButtonTextNode direkt bei Offset 0xC8.
        for (var i = addon->UldManager.NodeListCount - 1; i >= 0; i--)
        {
            var node = addon->UldManager.NodeList[i];
            if (node == null || (int)node->Type != 1001) continue;
            var comp = ((AtkComponentNode*)node)->Component;
            if (comp == null) continue;
            var btn = (AtkComponentButton*)comp;
            if (btn->ButtonTextNode == null) continue;
            var text = btn->ButtonTextNode->NodeText.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(text) && text.Length > 1)
                items.Add(text);
        }
        return items;
    }

    private unsafe string? FindTitleMenuFocused(AtkUnitBase* addon)
    {
        // Fokus-Indikator: Kind-Node id=4, Typ Res (t=1) des fokussierten Buttons wird SICHTBAR.
        // Best�tigt durch Dump-Analyse vom 30.05.2026:
        //   Fokussierter Button:   id=4 t=1 F=0x203F vis=True
        //   Unfokussierter Button: id=4 t=1 F=0x202F vis=False
        // HasFocusBit (0x100) ist UNBRAUCHBAR: id=6 (t=8, F=0x273F) hat 0x100 DAUERHAFT auf
        // ALLEN Buttons gesetzt und trifft immer den ersten Button im NodeList (btn=9 'Beenden').
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node == null || (int)node->Type != 1001) continue;

            var comp = ((AtkComponentNode*)node)->Component;
            if (comp == null) continue;

            var focused = false;
            for (var j = 0; j < comp->UldManager.NodeListCount; j++)
            {
                var child = comp->UldManager.NodeList[j];
                if (child != null && child->NodeId == 4 && child->Type == NodeType.Res && child->IsVisible())
                {
                    focused = true;
                    break;
                }
            }
            if (!focused) continue;

            var btn = (AtkComponentButton*)comp;
            if (btn->ButtonTextNode == null) continue;
            var text = btn->ButtonTextNode->NodeText.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(text))
                return text;
        }
        return null;
    }

    /// <summary>
    /// Vollst�ndiger Diagnostik-Dump aller _TitleMenu-Buttons inkl. ALLER Kinder-Nodes.
    /// Wird im PostUpdate-Frame nach InputReceived ausgef�hrt, damit der Fokus-Zustand
    /// bereits vom Spiel aktualisiert wurde.
    /// Log-Format: ButtonFlags btn=... | Kinder: (id type F=0x... vis=true/false)
    /// </summary>
    private unsafe void DumpTitleMenuButtonFlags(AtkUnitBase* addon)
    {
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node == null || (int)node->Type != 1001) continue;

            var comp = ((AtkComponentNode*)node)->Component;
            if (comp == null) continue;

            var btn = (AtkComponentButton*)comp;
            var lbl = btn->ButtonTextNode != null ? btn->ButtonTextNode->NodeText.ToString().Trim() : "?";
            var nf  = (ushort)node->NodeFlags;

            var sb = new System.Text.StringBuilder();
            sb.Append($"[DEBUG] ButtonFlags btn=id{node->NodeId} '{lbl}' NodeF=0x{nf:X4} | Kinder:");

            for (var j = 0; j < comp->UldManager.NodeListCount; j++)
            {
                var child = comp->UldManager.NodeList[j];
                if (child == null) continue;
                var cf  = (ushort)child->NodeFlags;
                var vis = child->IsVisible();
                sb.Append($" (id={child->NodeId} t={(int)child->Type} F=0x{cf:X4} vis={vis})");
            }

            _log.Info(sb.ToString());
        }
    }

    private unsafe string ReadAllTexts(AtkUnitBase* addon)
    {
        // Only VISIBLE texts: hidden nodes carry stale/conditional content -
        // JournalDetail spoke "Du kannst den Auftrag nicht annehmen..." from
        // an invisible error label on open (log 2026-07-11 10:35).
        var sb = new StringBuilder();
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node == null || node->Type != NodeType.Text || !node->IsVisible()) continue;
            var text = ((AtkTextNode*)node)->NodeText.ToString();
            if (!string.IsNullOrWhiteSpace(text) && text.Length > 1)
                sb.Append(text).Append(". ");
        }
        return sb.ToString().Trim();
    }

    // -- Window diagnosis (F2 / "/acc win") --------------------------

    /// <summary>
    /// Announces which addon currently has focus and logs all visible addons
    /// with prefix [Win]. Main purpose: identify unknown menus that have no
    /// handler yet, so they can be dumped and made accessible.
    /// Fields verified against FFXIVClientStructs: AtkUnitManager.FocusedUnitsList /
    /// AllLoadedUnitsList (AtkUnitList: Entries + Count), AtkUnitBase.NameString/IsVisible.
    /// </summary>
    public unsafe void AnnounceActiveWindow()
    {
        var mgr = RaptureAtkUnitManager.Instance();
        if (mgr == null)
        {
            _log.Warning("[Win] RaptureAtkUnitManager.Instance() returned null.");
            _tolk.SpeakInterrupt(AccessibilityStrings.UiManagerUnavailable);
            return;
        }

        // All visible addons -> log only (too many to speak)
        var visible = new List<string>();
        for (var i = 0; i < mgr->AllLoadedUnitsList.Count && i < 256; i++)
        {
            var a = mgr->AllLoadedUnitsList.Entries[i].Value;
            if (a == null || !a->IsVisible) continue;
            visible.Add(a->NameString);
        }

        // Focused addons (usually 0-2) -> spoken
        var focused = new List<string>();
        for (var i = 0; i < mgr->FocusedUnitsList.Count && i < 256; i++)
        {
            var a = mgr->FocusedUnitsList.Entries[i].Value;
            if (a == null) continue;
            focused.Add(a->NameString);
        }

        _log.Info($"[Win] Fokussiert ({focused.Count}): {(focused.Count > 0 ? string.Join(", ", focused) : "-")}");
        _log.Info($"[Win] Sichtbar ({visible.Count}): {string.Join(", ", visible)}");

        if (focused.Count > 0)
            _tolk.SpeakInterrupt(AccessibilityStrings.ActiveWindow(string.Join(", ", focused), visible.Count));
        else
            _tolk.SpeakInterrupt(AccessibilityStrings.NoWindowFocused(visible.Count));
    }

    // -- UI-Dump (Diagnose) ------------------------------------------

    /// <summary>
    /// Findet das aktuell sichtbare/aktive Addon und dumpt seinen Node-Tree.
    /// Wird von Plugin.cs �ber F5 aufgerufen (kein Chat-Fenster n�tig).
    /// Sucht in einer festen Priorit�tsliste, die den Titelbildschirm abdeckt.
    /// </summary>
    public unsafe void DumpFocusedAddon()
    {
        // First choice: whatever the game itself reports as focused
        // (FocusedUnitsList). Covers unknown menus like character creation
        // without relying on the hardcoded candidate list below.
        // Dump ALL visible focused addons into one file: the "main" addon can
        // be an empty container (CharaSelect: Vis=True, 0 Nodes - Log 21:18)
        // while the real content lives in a sibling (_CharaSelectListMenu).
        var mgr = RaptureAtkUnitManager.Instance();
        if (mgr != null && mgr->FocusedUnitsList.Count > 0)
        {
            var names = new List<string>();
            for (var i = 0; i < mgr->FocusedUnitsList.Count && i < 256; i++)
            {
                var a = mgr->FocusedUnitsList.Entries[i].Value;
                if (a == null || !a->IsVisible) continue;
                names.Add(a->NameString);
            }
            if (names.Count > 0)
            {
                // Companion panes ("Journal" -> "JournalDetail") are attached
                // children, never focused themselves, but carry the actual
                // content (quest description) - dump them along.
                foreach (var focusedName in names.ToArray())
                {
                    var detailName = focusedName + "Detail";
                    if (names.Contains(detailName)) continue;
                    var detail = _gameGui.GetAddonByName(detailName);
                    if (!detail.IsNull && ((AtkUnitBase*)(nint)detail)->IsVisible)
                        names.Add(detailName);
                }

                // Character creation: the race/step description pane lives in
                // a SIBLING addon that is never focused (_CharaMakeInfo/
                // Notice/Help - which one is unverified, user report
                // 2026-07-17: race description not announced). Dump every
                // visible CharaMake addon along so the text can be located.
                if (names.Any(n => n.Contains("CharaMake", StringComparison.Ordinal)))
                {
                    for (var i = 0; i < mgr->AllLoadedUnitsList.Count && i < 256; i++)
                    {
                        var a = mgr->AllLoadedUnitsList.Entries[i].Value;
                        if (a == null || !a->IsVisible) continue;
                        var n = a->NameString;
                        if (n.Contains("CharaMake", StringComparison.Ordinal) && !names.Contains(n))
                            names.Add(n);
                    }
                }

                _log.Info($"[Dump] Fokussierte Addons (alle im Dump): {string.Join(", ", names)}");
                DumpAddons(names);
                return;
            }
            _log.Info("[Dump] Fokussierte Addons vorhanden, aber keins sichtbar - Fallback auf Kandidatenliste.");
        }

        // Fallback: fixed candidate list (title screen etc.)
        var candidates = new List<string>();

        // Oberstes Men� im Stack zuerst (im Spiel)
        if (_menuStack.Count > 0)
            candidates.Add(_menuStack.Peek().Name);

        candidates.AddRange([
            "TitleDCWorldMap",        // Datenzentrum-Auswahl (Titelbildschirm)
            "_TitleMenu",             // Hauptmen� (Titelbildschirm)
            "Title",                  // Logo-Screen
            "SelectString",           // Auswahldialog
            "SelectIconString",
            "SelectYesno",
            "Talk",
            "ConfigSystem",
            "ConfigPadCalibration",
        ]);

        foreach (var name in candidates)
        {
            var p = _gameGui.GetAddonByName(name);
            if (p.IsNull) continue;
            var a = (AtkUnitBase*)(nint)p;
            if (!a->IsVisible) continue;
            _log.Info($"[Dump] Aktives Addon erkannt: {name}");
            DumpAddon(name);
            return;
        }

        _tolk.SpeakInterrupt("Kein aktives Addon f�r Dump gefunden.");
        _log.Info("[Dump] Kein sichtbares Addon in der Kandidatenliste gefunden.");
    }

    /// <summary>
    /// Dumpt den kompletten Node-Tree des angegebenen Addons in den PluginLog
    /// UND auf den Desktop (FFXIV_UI_Dump.txt).
    /// Aufruf: /acc dump TitleDCWorldMap oder via DumpFocusedAddon()
    /// </summary>
    public unsafe void DumpAddon(string addonName)
    {
        if (string.IsNullOrWhiteSpace(addonName))
        {
            _tolk.SpeakInterrupt("Kein Addon-Name. Beispiel: /acc dump TitleDCWorldMap");
            return;
        }

        DumpAddons([addonName.Trim()]);
    }

    /// <summary>
    /// Dumps several addons into ONE log block and ONE desktop file.
    /// Used by DumpFocusedAddon (all focused addons) and DumpAddon (single name).
    /// </summary>
    private unsafe void DumpAddons(IReadOnlyList<string> addonNames)
    {
        var sb         = new StringBuilder();
        var totalNodes = 0;
        var foundCount = 0;

        foreach (var name in addonNames)
        {
            var ptr = _gameGui.GetAddonByName(name);
            if (ptr.IsNull)
            {
                sb.AppendLine($"=== DUMP: {name} | nicht offen ===");
                continue;
            }

            var addon = (AtkUnitBase*)(nint)ptr;
            foundCount++;
            totalNodes += addon->UldManager.NodeListCount;
            sb.AppendLine($"=== DUMP: {name} | Vis={addon->IsVisible} | Nodes={addon->UldManager.NodeListCount} ===");

            for (var i = 0; i < addon->UldManager.NodeListCount; i++)
            {
                var node = addon->UldManager.NodeList[i];
                if (node == null) continue;
                DumpNode(sb, node, depth: 0, index: i);
            }
        }

        if (foundCount == 0)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.AddonNotOpen(string.Join(", ", addonNames)));
            return;
        }

        var output = sb.ToString();

        // Log in Zeilen aufgeteilt (Dalamud-Log begrenzt Zeilenl�nge)
        foreach (var line in output.Split('\n'))
        {
            var l = line.TrimEnd('\r');
            if (l.Length > 0) _log.Info(l);
        }

        // Datei auf den Desktop schreiben (auch ohne Chat-Fenster erreichbar)
        var dumpFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "FFXIV_UI_Dump.txt");
        try
        {
            File.WriteAllText(dumpFile, output, System.Text.Encoding.UTF8);
            _tolk.SpeakInterrupt(AccessibilityStrings.DumpSaved(foundCount, totalNodes));
            _log.Info($"[Dump] Gespeichert: {dumpFile}");
        }
        catch (Exception ex)
        {
            _log.Warning($"[Dump] Datei-Fehler: {ex.Message}");
            _tolk.SpeakInterrupt("Dump nur im Dalamud-Log. Datei-Fehler.");
        }
    }

    /// <summary>
    /// Rekursive Node-Ausgabe f�r DumpAddon.
    /// Gibt NodeId, Typ, Flags, Sichtbarkeit und Inhalt aus.
    /// Tiefenlimit = 5, verhindert Stack-Overflow bei tiefen B�umen.
    /// </summary>
    private unsafe void DumpNode(StringBuilder sb, AtkResNode* node, int depth, int index)
    {
        const int MaxDepth = 5;
        var indent  = new string(' ', depth * 2);
        var typeNum = (int)node->Type;

        // Lesbare Typbezeichnung
        var typeName = typeNum switch
        {
            1  => "Res",
            2  => "Image",
            3  => "Text",
            4  => "NineGrid",
            5  => "Counter",
            8  => "Collision",
            12 => "Clip",
            _  => typeNum >= 1000 ? $"Comp({typeNum})" : $"T{typeNum}"
        };

        var flags = (ushort)node->NodeFlags;
        var vis   = node->IsVisible() ? "V" : " ";
        var extra = string.Empty;

        if (typeNum == 3) // Text
        {
            var t = ((AtkTextNode*)node)->NodeText.ToString().Replace("\n", "?");
            if (t.Length > 80) t = t[..80] + "�";
            extra = $" \"{t}\"";
        }
        else if (typeNum >= 1000)
        {
            var comp = ((AtkComponentNode*)node)->Component;
            if (comp != null)
            {
                var ct = comp->GetComponentType();
                extra = $" [CT={ct}({(int)ct}) Ch={comp->UldManager.NodeListCount}]";

                // F�r Listen: L�nge und aktuell gew�hlter Index
                if (ct == ComponentType.List)
                {
                    var list = (AtkComponentList*)comp;
                    extra += $" [ListLen={list->ListLength} Sel={list->SelectedItemIndex}]";
                }
            }
        }

        sb.AppendLine($"{indent}[{index}] id={node->NodeId} {typeName} F=0x{flags:X4} {vis}{extra}");

        if (depth >= MaxDepth || typeNum < 1000) return;

        var nodeComp = ((AtkComponentNode*)node)->Component;
        if (nodeComp == null) return;
        for (var j = 0; j < nodeComp->UldManager.NodeListCount; j++)
        {
            var child = nodeComp->UldManager.NodeList[j];
            if (child != null) DumpNode(sb, child, depth + 1, j);
        }
    }

    public void Dispose()
    {
        _addonLifecycle.UnregisterListener(OnAnyAddonOpen);
        _addonLifecycle.UnregisterListener(AddonEvent.PostUpdate,       OnAnyAddonUpdate);
        _addonLifecycle.UnregisterListener(AddonEvent.PostReceiveEvent, OnAnyAddonReceive);
        _addonLifecycle.UnregisterListener(AddonEvent.PreFinalize,      OnAnyAddonClose);
        _addonLifecycle.UnregisterListener(AddonEvent.PostUpdate, "Talk",         OnTalkUpdate);
        _addonLifecycle.UnregisterListener(AddonEvent.PostUpdate, "TalkSubtitle", OnTalkUpdate);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup,        "SelectYesno", OnYesNoOpen);
        _addonLifecycle.UnregisterListener(AddonEvent.PostReceiveEvent, "SelectYesno", OnYesNoReceive);
        _addonLifecycle.UnregisterListener(AddonEvent.PostUpdate, "SelectYesno",   OnDialogButtonProbe);
        _addonLifecycle.UnregisterListener(AddonEvent.PostUpdate, "JournalResult", OnDialogButtonProbe);
        _addonLifecycle.UnregisterListener(AddonEvent.PostUpdate, "ArmouryBoard",  OnArmouryBoardUpdate);
        _addonLifecycle.UnregisterListener(AddonEvent.PostUpdate, "JournalDetail", OnQuestWindowUpdate);
        _addonLifecycle.UnregisterListener(AddonEvent.PostUpdate, "JournalAccept", OnQuestWindowUpdate);
        _addonLifecycle.UnregisterListener(AddonEvent.PostUpdate, "JournalResult", OnQuestWindowUpdate);
        _addonLifecycle.UnregisterListener(OnSelectStringOpen);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "Request", OnRequestOpen);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "Title", OnTitleScreenOpen);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup,        "_TitleMenu", OnTitleMenuOpen);
        _addonLifecycle.UnregisterListener(AddonEvent.PostReceiveEvent, "_TitleMenu", OnTitleMenuReceive);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup,        "ConfigSystem", OnConfigSystemOpen);
        _addonLifecycle.UnregisterListener(AddonEvent.PreFinalize,      "ConfigSystem", OnConfigSystemClose);
        _addonLifecycle.UnregisterListener(AddonEvent.PostReceiveEvent, "ConfigSystem", OnConfigSystemReceive);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "ConfigPadCalibration", OnPadCalibrationOpen);
        _addonLifecycle.UnregisterListener(AddonEvent.PostUpdate,       "_CharaMakeRaceGender", OnRaceGenderUpdate);
        _addonLifecycle.UnregisterListener(AddonEvent.PostReceiveEvent, "_CharaMakeRaceGender", OnRaceGenderReceive);
        _addonLifecycle.UnregisterListener(AddonEvent.PostUpdate,       "_CharaMakeTribe", OnTribeUpdate);
        _addonLifecycle.UnregisterListener(AddonEvent.PostReceiveEvent, "_CharaMakeTribe", OnTribeReceive);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup,        "_CharaMakeHelp", OnCharaMakeHelpOpen);
        _addonLifecycle.UnregisterListener(AddonEvent.PostUpdate,       "_CharaMakeHelp", OnCharaMakeHelpUpdate);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup,        "CharaMakeDataInputString", OnCharaMakeInputOpen);
        _addonLifecycle.UnregisterListener(AddonEvent.PostUpdate,       "CharaMakeDataInputString", OnCharaMakeInputUpdate);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup,        "_CharaMakeCharaName", OnCharaMakeNameOpen);
        _addonLifecycle.UnregisterListener(AddonEvent.PostUpdate,       "_CharaMakeCharaName", OnCharaMakeNameUpdate);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup,        "TitleDCWorldMap", OnDCWorldMapOpen);
        _addonLifecycle.UnregisterListener(AddonEvent.PostUpdate,       "TitleDCWorldMap", OnDCWorldMapUpdate);
        _addonLifecycle.UnregisterListener(AddonEvent.PostReceiveEvent, "TitleDCWorldMap", OnDCWorldMapReceive);
        foreach (var name in NotificationAddons)
        {
            _addonLifecycle.UnregisterListener(AddonEvent.PostSetup,   name, OnNotification);
            _addonLifecycle.UnregisterListener(AddonEvent.PostRefresh, name, OnNotification);
        }
    }
}
