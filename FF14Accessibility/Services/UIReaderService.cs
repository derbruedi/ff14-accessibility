using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FF14Accessibility.Services;

public sealed class UIReaderService : IDisposable
{
    private readonly IAddonLifecycle _addonLifecycle;
    private readonly IGameGui        _gameGui;
    private readonly TolkService     _tolk;
    private readonly IPluginLog      _log;

    // SelectYesno
    private string _lastYesNoText = string.Empty;

    // Menü-Stack: (AddonName, zuletzt gesehener SelectedItemIndex)
    // Oberstes Element = aktuell aktives Menü
    private readonly Stack<(string Name, int Index)> _menuStack = new();

    // Fallback für Nicht-Listen-Addons (ReceiveEvent)
    private string _lastFocused = "";

    // Letzte fokussierte NodeId (um Fokus-Wechsel bei gleichem Text zu erkennen)
    private uint _lastFocusedNodeId = 0;

    // _TitleMenu: zuletzt angesagter Button-Text
    private string _lastTitleMenuText = string.Empty;


    // Performance-Cache: Addons ohne AtkComponentList nicht erneut suchen
    private readonly HashSet<string> _noListCache = [];

    // Debug-Probe: Vorherige Node-Flags pro FocusTrack-Addon (addonName → nodeKey → flags)
    private readonly Dictionary<string, Dictionary<uint, ushort>> _focusTrackFlags = [];

    // Addons mit eigenem PostSetup-Handler — Universal-OnOpen überspringt diese
    private static readonly HashSet<string> SpecialSetupAddons =
    [
        "Talk", "SelectYesno", "SelectString", "SelectIconString",
        "_TextError", "_WideText", "_BattleTalk", "_LocationTitle",
        "LevelUpAnnouncement", "ContentsTutorial", "_ScreenText",
        "ConfigPadCalibration",
        "Title",
        "_TitleMenu",
        "ConfigSystem",
        "TitleDCWorldMap",    // Datenzentrum-Auswahl: eigener Handler
        "TitleDCWorldMapBg",  // nur Hintergrund, nichts anzusagen
    ];

    // Addons, bei denen Universal-Update/ReceiveEvent nicht läuft
    private static readonly HashSet<string> SpecialUpdateAddons =
    [
        "Talk", "SelectYesno",
        "_TextError", "_WideText", "_BattleTalk", "_LocationTitle",
        "LevelUpAnnouncement", "ContentsTutorial", "_ScreenText",
        "ConfigPadCalibration",
        "LobbyScreenText",    // fires TimerTick every frame — no useful navigation
        "ConfigSystem",       // eigene Handler + FocusTrack
        "TitleDCWorldMap",    // TimelineActiveLabelChanged feuert alle 300ms — eigener Handler
        "TitleDCWorldMapBg",  // nur Hintergrund
    ];

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
        ["Ja", "Nein", "Yes", "No", "はい", "いいえ", "Oui", "Non"];

    // Plugin.cs prüft dies, um Navigationstasten nur bei aktivem Menü zu verarbeiten
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

    public UIReaderService(IAddonLifecycle addonLifecycle, IGameGui gameGui, TolkService tolk, IPluginLog log)
    {
        _addonLifecycle = addonLifecycle;
        _gameGui        = gameGui;
        _tolk           = tolk;
        _log            = log;
        RegisterHooks();
    }

    private void RegisterHooks()
    {
        // ── Universal: alle Addons ────────────────────────────────
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup,        OnAnyAddonOpen);
        _addonLifecycle.RegisterListener(AddonEvent.PostUpdate,       OnAnyAddonUpdate);
        _addonLifecycle.RegisterListener(AddonEvent.PostReceiveEvent, OnAnyAddonReceive);
        _addonLifecycle.RegisterListener(AddonEvent.PreFinalize,      OnAnyAddonClose);

        // ── Talk ─────────────────────────────────────────────────
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup,        "Talk", OnTalkOpen);
        _addonLifecycle.RegisterListener(AddonEvent.PostReceiveEvent, "Talk", OnTalkReceive);

        // ── SelectYesno ──────────────────────────────────────────
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup,        "SelectYesno", OnYesNoOpen);
        _addonLifecycle.RegisterListener(AddonEvent.PostReceiveEvent, "SelectYesno", OnYesNoReceive);

        // ── SelectString / SelectIconString ──────────────────────
        foreach (var name in SelectStringAddons)
            _addonLifecycle.RegisterListener(AddonEvent.PostSetup, name, OnSelectStringOpen);

        // ── Titelbildschirm (Logo-Screen vor dem Menü) ──────────────
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "Title", OnTitleScreenOpen);

        // ── _TitleMenu (das eigentliche Menü mit Buttons) ────────────
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup,        "_TitleMenu", OnTitleMenuOpen);
        _addonLifecycle.RegisterListener(AddonEvent.PostReceiveEvent, "_TitleMenu", OnTitleMenuReceive);

        // ── Systemkonfiguration ──────────────────────────────────
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup,        "ConfigSystem", OnConfigSystemOpen);
        _addonLifecycle.RegisterListener(AddonEvent.PreFinalize,      "ConfigSystem", OnConfigSystemClose);
        _addonLifecycle.RegisterListener(AddonEvent.PostReceiveEvent, "ConfigSystem", OnConfigSystemReceive);

        // ── Gamepad-Kalibrierung ─────────────────────────────────

        // -- Datenzentrum-Auswahl
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup,        "TitleDCWorldMap", OnDCWorldMapOpen);
        _addonLifecycle.RegisterListener(AddonEvent.PostUpdate,       "TitleDCWorldMap", OnDCWorldMapUpdate);
        _addonLifecycle.RegisterListener(AddonEvent.PostReceiveEvent, "TitleDCWorldMap", OnDCWorldMapReceive);
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "ConfigPadCalibration", OnPadCalibrationOpen);

        // ── Benachrichtigungen ───────────────────────────────────
        foreach (var name in NotificationAddons)
        {
            _addonLifecycle.RegisterListener(AddonEvent.PostSetup,   name, OnNotification);
            _addonLifecycle.RegisterListener(AddonEvent.PostRefresh, name, OnNotification);
        }
    }

    // ── Universal: Addon geöffnet ───────────────────────────────────

    private unsafe void OnAnyAddonOpen(AddonEvent type, AddonArgs args)
    {
        var name = args.AddonName;
        _log.Info($"[Accessibility] Addon: {name}");
        if (SpecialSetupAddons.Contains(name)) return;

        var addon = (AtkUnitBase*)(nint)args.Addon;
        if (addon == null) return;

        _noListCache.Remove(name);

        var list = FindListInAddon(addon);
        if (list != null)
        {
            PushMenu(name, list->SelectedItemIndex);

            // Nur Anzahl + aktuell gewählten Eintrag ansagen.
            // Alle Einträge zu iterieren kann bei langen Listen zu Crashes führen
            // (uninitialisierte Renderer bei virtuell scrollenden Listen).
            var count = list->ListLength;
            var sel   = ReadListItemText(list, Math.Max(0, list->SelectedItemIndex));
            var msg   = sel.Length > 0
                ? $"{sel}, {count} Einträge"
                : $"Menü, {count} Einträge";
            _tolk.SpeakInterrupt(msg);
            return;
        }

        // Generic text cache initialisieren — ermöglicht Änderungs-Erkennung in PostUpdate
        _noListCache.Add(name);
        ScanAddonTexts(name, addon, isInit: true);
        var text = ReadAllTexts(addon);
        if (!string.IsNullOrWhiteSpace(text))
            _tolk.Speak(text);
    }

    // ── Universal: Addon geschlossen ────────────────────────────────

    private void OnAnyAddonClose(AddonEvent type, AddonArgs args)
    {
        var name = args.AddonName;
        _genericTextCache.Remove(name);

        // DC-Auswahl geschlossen — State zurücksetzen
        if (name == "TitleDCWorldMap")
        {
            IsDCMapOpen = false;
            _dcTabPanels.Clear();
            _lastDCText = string.Empty;
            _log.Info("[Accessibility] TitleDCWorldMap geschlossen, DC-State zurückgesetzt.");
        }

        if (!_menuStack.Any(m => m.Name == name)) return;

        // Alle Einträge bis einschließlich dem geschlossenen Addon entfernen
        while (_menuStack.Count > 0)
        {
            var top = _menuStack.Pop();
            if (top.Name == name) break;
        }
        _noListCache.Remove(name);
        _log.Info($"[Accessibility] Menü geschlossen: {name}, Stack-Tiefe: {_menuStack.Count}");
    }

    // ── Universal: Listenfokus per PostUpdate erkennen ──────────────

    private unsafe void OnAnyAddonUpdate(AddonEvent type, AddonArgs args)
    {
        var name = args.AddonName;
        // SpecialUpdateAddons überspringen, außer ConfigSystem und TitleDCWorldMap (die wir jetzt universell behandeln)
        if (SpecialUpdateAddons.Contains(name) && name != "ConfigSystem" && name != "TitleDCWorldMap") return;

        var currentAddon = (AtkUnitBase*)(nint)args.Addon;
        if (currentAddon == null || !currentAddon->IsVisible) return;

        // 1. Universeller Fokus-Check (funktioniert für Buttons, Tabs, etc.)
        var res = FindFocusedText(currentAddon);
        if (!string.IsNullOrEmpty(res.Text))
        {
            if (res.Key != _lastFocusedNodeId || res.Text != _lastFocused)
            {
                _lastFocusedNodeId = res.Key;
                _lastFocused = res.Text;
                _log.Info($"[Accessibility] {name} Fokus: {res.Text} (Key={res.Key})");
                
                var announceText = res.Text;

                // Spezialfall Datenzentrum: Wenn wir einen Tab fokussieren, 
                // wollen wir die DCs dazu hören (falls vorhanden).
                if (name == "TitleDCWorldMap")
                {
                    var panelMatch = _dcTabPanels.FirstOrDefault(p => p.Region.Contains(announceText, StringComparison.OrdinalIgnoreCase));
                    if (panelMatch.DCs != null && panelMatch.DCs.Count > 0)
                        announceText = $"{announceText}: {string.Join(", ", panelMatch.DCs)}";
                }

                _tolk.SpeakInterrupt(announceText);
                
                // Wir führen KEIN return aus, damit Listen-Navigation oder Text-Scanner
                // für den Rest des Fensters noch laufen können.
            }
        }

        // 2. Spezial-Logik für ConfigSystem (Text-Änderungen in Checkboxen/Slidern)
        if (name == "ConfigSystem")
        {
            if (_configSystemLastTexts.Count > 0)
                ScanConfigSystemTexts(currentAddon);
            return;
        }

        // 3. Spezial-Logik für Datenzentrum (Panel-Sichtbarkeit als Fallback)
        if (name == "TitleDCWorldMap")
        {
            AnnounceDCFocus();
            return;
        }

        // 4. Klassische Listen-Navigation (Fallback für Addons, die kein Fokus-Bit setzen)
        if (_menuStack.Count > 0 && _menuStack.Peek().Name == name)
        {
            var list = FindListInAddon(currentAddon);
            if (list != null)
            {
                var idx = list->SelectedItemIndex;
                if (idx != _menuStack.Peek().Index)
                {
                    _menuStack.Pop();
                    _menuStack.Push((name, idx));
                    var text = ReadListItemText(list, idx);
                    if (!string.IsNullOrEmpty(text))
                    {
                        _log.Info($"[Accessibility] {name} List-Navigation: [{idx}] {text}");
                        _tolk.SpeakInterrupt(text);
                    }
                }
            }
        }

        // 5. Generischer Text-Scanner (für Änderungen im Addon-Inhalt)
        if (_noListCache.Contains(name))
        {
            ScanAddonTexts(name, currentAddon, isInit: false);
        }
    }

    // ── Universal: Fokus per ReceiveEvent (Fallback für Listen-lose Addons) ─

    private static readonly HashSet<byte> IgnoredEventTypes = [3, 4, 5, 12, 14, 15, 16, 17, 23, 24];

    private unsafe void OnAnyAddonReceive(AddonEvent type, AddonArgs args)
    {
        var name = args.AddonName;
        if (SpecialUpdateAddons.Contains(name)) return;
        if (args is not AddonReceiveEventArgs recv) return;
        if (IgnoredEventTypes.Contains(Convert.ToByte(recv.AtkEventType))) return;

        var addon = (AtkUnitBase*)(nint)args.Addon;
        if (addon == null) return;

        // Listen-Addons werden von OnAnyAddonUpdate abgedeckt
        if (FindListInAddon(addon) != null) return;

        _log.Info($"[Accessibility] {name} ReceiveEvent: type={recv.AtkEventType} param={recv.EventParam}");

        var (focused, nodeId) = FindFocusedText(addon);
        if (!string.IsNullOrEmpty(focused))
        {
            if (nodeId != _lastFocusedNodeId || focused != _lastFocused)
            {
                _lastFocusedNodeId = nodeId;
                _lastFocused = focused;
                _tolk.SpeakInterrupt(focused);
            }
        }
    }

    // ── Talk ────────────────────────────────────────────────────────

    private unsafe void OnTalkOpen(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)(nint)args.Addon;
        if (addon == null) return;
        var text = ReadFirstText(addon);
        if (!string.IsNullOrWhiteSpace(text)) _tolk.SpeakInterrupt(text);
    }

    private unsafe void OnTalkReceive(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)(nint)args.Addon;
        if (addon == null) return;
        var text = ReadFirstText(addon);
        if (!string.IsNullOrWhiteSpace(text)) _tolk.SpeakInterrupt(text);
    }

    // ── SelectYesno ─────────────────────────────────────────────────

    private unsafe void OnYesNoOpen(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)(nint)args.Addon;
        if (addon == null) return;
        _lastYesNoText = "Ja";
        var question = ReadYesNoQuestion(addon);
        _tolk.SpeakInterrupt(string.IsNullOrWhiteSpace(question) ? "Ja oder Nein?" : $"{question} Ja oder Nein?");
    }

    private void OnYesNoReceive(AddonEvent type, AddonArgs args)
    {
        if (args is AddonReceiveEventArgs recv)
            _log.Info($"[Accessibility] SelectYesno Event: type={recv.AtkEventType} param={recv.EventParam}");
    }

    // ── SelectString / SelectIconString ─────────────────────────────

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

            // Bis zu 8 Einträge vorlesen — SelectString hat selten mehr davon
            var maxItems = Math.Min(list->ListLength, 8);
            for (var i = 0; i < maxItems; i++)
            {
                var item = ReadListItemText(list, i);
                if (!string.IsNullOrWhiteSpace(item))
                    sb.Append($"{i + 1}. {item}. ");
            }
            if (sb.Length > 0)
                _tolk.SpeakInterrupt(sb.ToString().Trim());
        }
        else if (!string.IsNullOrWhiteSpace(prompt))
        {
            _tolk.SpeakInterrupt(prompt);
        }
    }

    // ── Titelbildschirm ─────────────────────────────────────────────

    private void OnTitleScreenOpen(AddonEvent type, AddonArgs args)
    {
        _tolk.SpeakInterrupt("Titelbildschirm. Drücke Enter um das Menü zu öffnen.");
    }

    // ── _TitleMenu (Hauptmenü mit Buttons) ──────────────────────────

    private unsafe void OnTitleMenuOpen(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)(nint)args.Addon;
        if (addon == null) return;

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

        // Menü-Einträge lesen und ansagen
        var items = ReadTitleMenuItems(addon);
        _lastTitleMenuText = string.Empty;

        if (items.Count == 0)
        {
            _tolk.SpeakInterrupt("Hauptmenü.");
            return;
        }
        var sb = new StringBuilder("Hauptmenü: ");
        for (var i = 0; i < items.Count; i++)
            sb.Append($"{i + 1}. {items[i]}. ");
        _tolk.SpeakInterrupt(sb.ToString().TrimEnd());
    }

    private void OnTitleMenuReceive(AddonEvent type, AddonArgs args)
    {
        // Fokus-Ankündigungen laufen über OnAnyAddonUpdate (Flag-Änderung HasCollision).
        // ReceiveEvent wird nur noch für Debug-Logging genutzt.
        if (args is AddonReceiveEventArgs recv)
            _log.Info($"[DEBUG] _TitleMenu ReceiveEvent: type={recv.AtkEventType} param={recv.EventParam}");
    }

    // ── Systemkonfiguration (ConfigSystem) ─────────────────────────

    // Text-Cache: nodeKey → letzter gesehener Inhalt (für Änderungs-Erkennung)
    private readonly Dictionary<uint, string> _configSystemLastTexts = [];

    // Generic text cache: addonName → (nodeKey → letzter Text) für alle nicht-listenbasierten Addons
    private readonly Dictionary<string, Dictionary<uint, string>> _genericTextCache = [];

    // TitleDCWorldMap: zuletzt angesagte Region (Dedup)
    private string _lastDCText = string.Empty;

    // TitleDCWorldMap: Tab/Panel-Paare (befüllt in OnDCWorldMapOpen).
    // Wir speichern den Pointer zum Panel-Node, um dessen Sichtbarkeit zu prüfen.
    // Panels[i] entspricht Tab[i] nach NodeList-Reihenfolge.
    private readonly List<(nint PanelPtr, string Region, List<string> DCs)> _dcTabPanels = [];

    /// <summary>True wenn TitleDCWorldMap gerade offen ist (für Plugin.cs-Navigation).</summary>
    public bool IsDCMapOpen { get; private set; }

    private unsafe void OnConfigSystemOpen(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)(nint)args.Addon;
        if (addon == null) return;

        var isReopen  = _configSystemLastTexts.Count > 0;
        var newTexts  = new Dictionary<uint, string>();
        var flagCache = new Dictionary<uint, ushort>();
        _focusTrackFlags["ConfigSystem"] = flagCache;

        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var n = addon->UldManager.NodeList[i];
            if (n == null) continue;
            flagCache[n->NodeId] = (ushort)n->NodeFlags;

            if (n->Type == NodeType.Text)
            {
                var t = ((AtkTextNode*)n)->NodeText.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(t))
                    newTexts[n->NodeId] = t;
            }

            if ((int)n->Type < 1000) continue;
            var comp = ((AtkComponentNode*)n)->Component;
            if (comp == null) continue;

            for (var j = 0; j < comp->UldManager.NodeListCount; j++)
            {
                var child = comp->UldManager.NodeList[j];
                if (child == null) continue;
                flagCache[n->NodeId * 10000u + child->NodeId] = (ushort)child->NodeFlags;

                if (child->Type != NodeType.Text) continue;
                var ct = ((AtkTextNode*)child)->NodeText.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(ct))
                    newTexts[n->NodeId * 10000u + child->NodeId] = ct;
            }
        }

        if (isReopen)
        {
            // Kategorie-Wechsel: geänderten Text mit 3-50 Zeichen finden → Kategorieüberschrift ansagen
            string? categoryName = null;
            foreach (var (key, newText) in newTexts)
            {
                if (newText.Length < 3 || newText.Length > 50) continue;
                if (!_configSystemLastTexts.TryGetValue(key, out var oldText) || oldText == newText) continue;
                if (categoryName == null || newText.Length < categoryName.Length)
                    categoryName = newText;
            }
            _log.Info($"[Accessibility] ConfigSystem Kategorie: '{categoryName ?? "?"}'");
            _tolk.SpeakInterrupt(categoryName ?? "Systemkonfiguration.");
        }
        else
        {
            _tolk.SpeakInterrupt("Systemkonfiguration.");
        }

        _configSystemLastTexts.Clear();
        foreach (var (k, v) in newTexts)
            _configSystemLastTexts[k] = v;
    }

    private void OnConfigSystemClose(AddonEvent type, AddonArgs args)
    {
        _focusTrackFlags.Remove("ConfigSystem");
        _lastTitleMenuText = string.Empty; // Dedup zurücksetzen → fokussierter Button wird neu angesagt
        // _configSystemLastTexts bewusst NICHT leeren — für Kategorie-Vergleich beim Wiederöffnen
    }

    private unsafe void OnConfigSystemReceive(AddonEvent type, AddonArgs args)
    {
        if (args is not AddonReceiveEventArgs recv) return;
        if ((int)recv.AtkEventType != 12) return; // nur InputReceived

        var addon = (AtkUnitBase*)(nint)args.Addon;
        if (addon == null) return;

        ScanConfigSystemTexts(addon);
    }

    /// <summary>Scannt alle Text-Nodes in ConfigSystem und sagt echte Änderungen an.</summary>
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
                if (!hasKey) continue; // Ersterscheinung — nicht ansagen
                if (n->NodeId != 169) // id=169 = Immerse-Warnung
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
                _tolk.SpeakInterrupt(t);
            }
        }
    }

    /// <summary>
    /// Generischer Text-Scanner für beliebige Addons.
    /// isInit=true: befüllt nur den Cache, sagt nichts an (beim Öffnen).
    /// isInit=false: sagt geänderte Texte an (in PostUpdate).
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
                var t = ((AtkTextNode*)n)->NodeText.ToString().Trim();
                if (string.IsNullOrWhiteSpace(t) || t.Length <= 1) continue;
                var hasKey = cache.TryGetValue(n->NodeId, out var prev);
                if (hasKey && prev == t) continue;
                cache[n->NodeId] = t;
                if (!isInit && hasKey)
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
                if (string.IsNullOrWhiteSpace(t) || t.Length <= 1) continue;
                var key = n->NodeId * 10000u + child->NodeId;
                var hasKey = cache.TryGetValue(key, out var prev);
                if (hasKey && prev == t) continue;
                cache[key] = t;
                if (!isInit && hasKey)
                    _tolk.SpeakInterrupt(t);
            }
        }
    }

    // ── Gamepad-Kalibrierung ────────────────────────────────────────


    // -- Datenzentrum-Auswahl (TitleDCWorldMap) -------------------------

    private unsafe void OnDCWorldMapOpen(AddonEvent type, AddonArgs args)
    {
        _lastDCText = string.Empty;
        IsDCMapOpen = true;
        _dcTabPanels.Clear();

        var addon = (AtkUnitBase*)(nint)args.Addon;
        if (addon == null) { _tolk.SpeakInterrupt("Datenzentrum wählen."); return; }

        // ── Schritt 1: Alle Region-Panels sammeln.
        // Wir sammeln ALLE Panels, auch die (noch) unsichtbaren, um die Navigation
        // später über deren Sichtbarkeits-Wechsel zu erkennen.
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
            _log.Info($"[DC] Panel gefunden: {region} (NodeId={n->NodeId}, Type={typeNum})");
        }

        if (_dcTabPanels.Count == 0)
        {
            _tolk.SpeakInterrupt("Datenzentrum wählen.");
            return;
        }

        // Initiale Ansage: Liste der verfügbaren Regionen
        var sb = new StringBuilder("Datenzentrum wählen. Regionen: ");
        sb.Append(string.Join(", ", _dcTabPanels.Select(p => p.Region)));
        _tolk.SpeakInterrupt(sb.ToString());
        
        // Den ersten Fokus direkt ansagen
        AnnounceDCFocus();
    }

    private unsafe void OnDCWorldMapReceive(AddonEvent type, AddonArgs args)
    {
        if (args is not AddonReceiveEventArgs recv) return;
        // Debug: log ALL events except type=74 (TimelineActiveLabelChanged, fires every 300ms).
        // This lets us see what the numpad keys generate.
        if ((int)recv.AtkEventType != 74)
            _log.Info($"[DC] ReceiveEvent type={(int)recv.AtkEventType}({recv.AtkEventType}) param={recv.EventParam}");
        // Only announce on MouseOver (type=6, verified via log).
        if ((int)recv.AtkEventType != 6) return;

        var addon = (AtkUnitBase*)(nint)args.Addon;
        if (addon == null) return;

        // EventParam = node ID of the hovered region panel.
        var nodeId = (uint)recv.EventParam;
        var node   = addon->GetNodeById(nodeId);
        if (node == null) return;

        var (region, dcs) = ReadDCRegionPanel(node);
        if (string.IsNullOrWhiteSpace(region)) return;

        var text = dcs.Count > 0 ? $"{region}: {string.Join(", ", dcs)}" : region;
        if (text == _lastDCText) return;
        _log.Info($"[Accessibility] TitleDCWorldMap: '{text}' (node={nodeId})");
        _lastDCText = text;
        _tolk.SpeakInterrupt(text);
    }

    /// <summary>
    /// Prüft jeden Frame den ausgewählten Tab und sagt die Region an, wenn sie sich geändert hat.
    /// Dedup in AnnounceDCFocus() verhindert Wiederholungen; kein AtkStage nötig.
    /// </summary>
    private void OnDCWorldMapUpdate(AddonEvent type, AddonArgs args)
    {
        if (!IsDCMapOpen || _dcTabPanels.Count == 0) return;
        AnnounceDCFocus();
    }

    /// <summary>
    /// Prüft, welches der gespeicherten Region-Panels gerade sichtbar ist.
    /// Dies ist der zuverlässigste Indikator für den Fokus in diesem Menü.
    /// </summary>
    private unsafe void AnnounceDCFocus()
    {
        if (_dcTabPanels.Count == 0) return;

        foreach (var (panelPtr, region, dcs) in _dcTabPanels)
        {
            if (panelPtr == nint.Zero) continue;
            var panel = (AtkResNode*)panelPtr;

            // In FF14-Tabs ist meist nur das aktive Panel sichtbar.
            if (!panel->IsVisible()) continue;

            var text = dcs.Count > 0 ? $"{region}: {string.Join(", ", dcs)}" : region;
            if (text == _lastDCText) return; 
            
            _log.Info($"[DC] Fokus-Wechsel erkannt (Panel sichtbar) → '{text}'");
            _lastDCText = text;
            _tolk.SpeakInterrupt(text);
            return;
        }
    }

    /// <summary>
    /// Löscht den Dedup-Cache und sagt die aktuell fokussierte Region sofort an.
    /// Wird von Plugin.cs aufgerufen, wenn der Nutzer eine Nummernblock-Taste drückt.
    /// </summary>
    public void ForceDCMapRead()
    {
        _lastDCText = string.Empty; // Dedup zurücksetzen — User hat explizit navigiert
        AnnounceDCFocus();
    }

    /// <summary>
    /// Liest Region-Name und DC-Namen aus einem Regionen-Panel-Node des TitleDCWorldMap.
    /// Struktur (verifiziert via Node-Dump 2026-05-27):
    ///   comp child type=1015 → gc[?] type=Text = DC-Name (z.B. "Materia", "Chaos")
    ///   comp child type=1009 → gc[?] type=Text = Regionsname (z.B. "Ozeanien", "Europa")
    ///   comp child type=1006 = Ok-Button — ignorieren.
    /// Gibt ("", []) zurück wenn der Node kein Regionen-Panel ist.
    /// </summary>
    private unsafe (string Region, List<string> DCs) ReadDCRegionPanel(AtkResNode* node)
    {
        if (node == null || (int)node->Type < 1000) return (string.Empty, []);
        var comp = ((AtkComponentNode*)node)->Component;
        if (comp == null) return (string.Empty, []);

        var region = string.Empty;
        var dcs    = new List<string>();

        for (var j = 0; j < comp->UldManager.NodeListCount; j++)
        {
            var child = comp->UldManager.NodeList[j];
            if (child == null || (int)child->Type < 1000) continue;
            if ((int)child->Type == 1006) continue; // Ok button — skip

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
                    region = t;     // region name label (Ozeanien, Europa, …)
                else if ((int)child->Type == 1015)
                    dcs.Add(t);     // DC name button (Materia, Chaos, …)
                break; // first text node per child is sufficient
            }
        }

        return (region, dcs);
    }
    private void OnPadCalibrationOpen(AddonEvent type, AddonArgs args)
    {
        _tolk.SpeakInterrupt("Gamepad-Kalibrierung. Escape zum Schließen.");
    }

    // ── Benachrichtigungen ──────────────────────────────────────────

    private unsafe void OnNotification(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)(nint)args.Addon;
        if (addon == null || !addon->IsVisible) return;
        var text = ReadFirstText(addon);
        if (!string.IsNullOrWhiteSpace(text)) _tolk.SpeakInterrupt(text);
    }

    // ── Tastatur-Navigation ─────────────────────────────────────────

    // isHorizontal=true für Links/Rechts, false für Hoch/Runter
    public void Navigate(int delta, bool isHorizontal)
    {
        // SelectYesno: nur Links/Rechts navigieren Ja↔Nein
        // Hoch/Runter passieren durch — das Spiel navigiert nativ
        if (!isHorizontal) return;
        unsafe
        {
            var ynPtr = _gameGui.GetAddonByName("SelectYesno");
            if (!ynPtr.IsNull && ((AtkUnitBase*)(nint)ynPtr)->IsVisible)
            {
                var newText = delta < 0 ? "Ja" : "Nein";
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
        var newText = delta < 0 ? "Ja" : "Nein";
        if (newText == _lastYesNoText) return;
        _lastYesNoText = newText;
        _tolk.SpeakInterrupt(newText);
        _log.Info($"[Accessibility] SelectYesno Gamepad → {newText}");
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
        var idx         = _lastYesNoText == "Nein" ? 1 : 0;
        var shouldClose = addon->ShouldFireCallbackAndHideOrClose;
        _log.Info($"[Accessibility] ConfirmYesNo: idx={idx} ShouldFireCallbackAndHideOrClose={shouldClose}");

        if (_lastYesNoText == "Nein")
        {
            // WORKAROUND: FireCallback(1, {Int:1}) schließt SelectYesno nicht (Log 11:40:10 bestätigt).
            // Nein hat keinen Callback — das Spiel schließt das Fenster direkt ohne Callback.
            // Fix: Close(true) schließt das Fenster ohne den Ja-Callback auszulösen.
            _log.Info("[Accessibility] ConfirmYesNo: Nein → Close(true)");
            addon->Close(true);
        }
        else
        {
            // Ja: FireCallback mit idx=0 + ShouldFireCallbackAndHideOrClose=True (bestätigt funktionstüchtig).
            if (!shouldClose)
                addon->ShouldFireCallbackAndHideOrClose = true;
            var v = stackalloc AtkValue[1]; v[0].SetInt(0);
            addon->FireCallback(1, v);
        }
        _lastYesNoText = string.Empty;
    }

    public unsafe void ReadCurrentFocus()
    {
        // Aktives Menü aus dem Stack
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

        _tolk.SpeakInterrupt("Kein aktives Menü.");
    }

    // ── Hilfsmethoden ───────────────────────────────────────────────

    private void PushMenu(string name, int initialIndex)
    {
        if (_menuStack.Any(m => m.Name == name)) return;
        _menuStack.Push((name, initialIndex));
        _log.Info($"[Accessibility] Menü geöffnet: {name}, Stack-Tiefe: {_menuStack.Count}");
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

    // ── VirtualQuery: Pointer-Sicherheitsnetz gegen AccessViolationException ──

    [StructLayout(LayoutKind.Explicit, Size = 44)]
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

    // ── List-Item-Text lesen (abgesichert gegen ungültige Renderer-Pointer) ──

    private unsafe string ReadListItemText(AtkComponentList* list, int idx)
    {
        if (idx < 0 || idx >= list->ListLength) return string.Empty;
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

            for (uint i = 0; i < nodeCount; i++)
            {
                var node = nodeList[i];
                if (node == null || node->Type != NodeType.Text) continue;
                var textNode = (AtkTextNode*)node;
                if (textNode->NodeText.IsEmpty) continue;
                var text = textNode->NodeText.ToString();
                if (!string.IsNullOrEmpty(text)) return text;
            }
        }
        catch (Exception ex) { _log.Warning($"[Accessibility] ListItem-Fehler: {ex.Message}"); }
        return string.Empty;
    }

    private static unsafe AtkComponentList* FindListInAddon(AtkUnitBase* addon)
    {
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node == null || node->Type != NodeType.Component) continue;
            var comp = ((AtkComponentNode*)node)->Component;
            if (comp == null) continue;
            if (comp->GetComponentType() == ComponentType.List)
                return (AtkComponentList*)comp;
            for (var j = 0; j < comp->UldManager.NodeListCount; j++)
            {
                var inner = comp->UldManager.NodeList[j];
                if (inner == null || inner->Type != NodeType.Component) continue;
                var innerComp = ((AtkComponentNode*)inner)->Component;
                if (innerComp == null) continue;
                if (innerComp->GetComponentType() == ComponentType.List)
                    return (AtkComponentList*)innerComp;
            }
        }
        return null;
    }

    /// <summary>
    /// Findet den Node mit dem Fokus-Flag (HasFocusBit) oder relevanten Hover-Effekten.
    /// Rückgabe: (Text, Key)
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
                        if (child != null && child->IsVisible() && ((ushort)child->NodeFlags & HasFocusBit) != 0)
                            return (GetTextFromNodeTree(child), node->NodeId * 1000 + child->NodeId);
                    }
                }
            }
        }

        // 2. Durchlauf: Fallback auf Collision (0x10) für statische Menüs (z.B. TitleDCWorldMap)
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
    ///   List      → SelectedItemIndex-Text
    ///   Button    → ButtonTextNode (direkt, effizient)
    ///   CheckBox, RadioButton, ListItemRenderer, alle anderen
    ///             → alle Kind-Nodes werden rekursiv durchsucht,
    ///               Texte werden kombiniert (Label + Wert/Status)
    /// Depth-Limit = 6 verhindert Stack-Overflow bei tiefen Bäumen.
    /// </summary>
    private unsafe string GetTextFromNodeTree(AtkResNode* node, int depth = 0)
    {
        if (node == null || depth > 6) return string.Empty;

        // Text-Node: Inhalt direkt zurückgeben
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

        // Liste / Dropdown: aktuell gewählten Eintrag lesen
        if (compType == ComponentType.List)
        {
            var list = (AtkComponentList*)comp;
            return ReadListItemText(list, Math.Max(0, list->SelectedItemIndex));
        }

        // Button: ButtonTextNode bevorzugen (direkter Zugriff, keine Rekursion nötig)
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
        // Kind-Nodes rekursiv durchsuchen — ergibt automatisch Label + Wert/Status.
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
        // Rückwärts iterieren → niedrigere Node-IDs zuerst (visuell oben → unten).
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
        // AtkComponentButton (NodeType=1001) hat ButtonTextNode direkt bei Offset 0xC8.
        // Fokus-Indikator: Child-Node id=4 (type=Res) des fokussierten Buttons hat HasCollision (0x10).
        // Unfokussierte Buttons: 0x202F, fokussierter Button: 0x203F.
        const ushort HasCollisionBit = 0x10;
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node == null || (int)node->Type != 1001) continue;

            var comp = ((AtkComponentNode*)node)->Component;
            if (comp == null) continue;

            var hasFocus = false;
            for (var j = 0; j < comp->UldManager.NodeListCount; j++)
            {
                var child = comp->UldManager.NodeList[j];
                // Nur Res-Knoten prüfen — das ist der Fokus-Indikator (child=4 type=Res)
                // Andere Kinder haben HasCollision dauerhaft gesetzt und würden false positives erzeugen
                if (child != null && child->Type == NodeType.Res && ((ushort)child->NodeFlags & HasCollisionBit) != 0)
                {
                    hasFocus = true;
                    break;
                }
            }
            if (!hasFocus) continue;

            var btn = (AtkComponentButton*)comp;
            if (btn->ButtonTextNode == null) continue;
            var text = btn->ButtonTextNode->NodeText.ToString().Trim();
            _log.Info($"[DEBUG] TitleMenu btn={node->NodeId} ButtonTextNode='{text}'");
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        return null;
    }

    private unsafe string ReadAllTexts(AtkUnitBase* addon)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node == null || node->Type != NodeType.Text) continue;
            var text = ((AtkTextNode*)node)->NodeText.ToString();
            if (!string.IsNullOrWhiteSpace(text) && text.Length > 1)
                sb.Append(text).Append(". ");
        }
        return sb.ToString().Trim();
    }

    // ── UI-Dump (Diagnose) ──────────────────────────────────────────

    /// <summary>
    /// Findet das aktuell sichtbare/aktive Addon und dumpt seinen Node-Tree.
    /// Wird von Plugin.cs über F5 aufgerufen (kein Chat-Fenster nötig).
    /// Sucht in einer festen Prioritätsliste, die den Titelbildschirm abdeckt.
    /// </summary>
    public unsafe void DumpFocusedAddon()
    {
        // Prioritätsliste: Addons, die auf dem Titelbildschirm und im Spiel relevant sind
        var candidates = new List<string>();

        // Oberstes Menü im Stack zuerst (im Spiel)
        if (_menuStack.Count > 0)
            candidates.Add(_menuStack.Peek().Name);

        candidates.AddRange([
            "TitleDCWorldMap",        // Datenzentrum-Auswahl (Titelbildschirm)
            "_TitleMenu",             // Hauptmenü (Titelbildschirm)
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

        _tolk.SpeakInterrupt("Kein aktives Addon für Dump gefunden.");
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

        var ptr = _gameGui.GetAddonByName(addonName.Trim());
        if (ptr.IsNull)
        {
            _tolk.SpeakInterrupt($"Addon {addonName} nicht offen.");
            return;
        }

        var addon = (AtkUnitBase*)(nint)ptr;
        var sb    = new StringBuilder();
        sb.AppendLine($"=== DUMP: {addonName} | Vis={addon->IsVisible} | Nodes={addon->UldManager.NodeListCount} ===");

        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node == null) continue;
            DumpNode(sb, node, depth: 0, index: i);
        }

        var output = sb.ToString();

        // Log in Zeilen aufgeteilt (Dalamud-Log begrenzt Zeilenlänge)
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
            _tolk.SpeakInterrupt($"UI Dump auf Desktop gespeichert. {addon->UldManager.NodeListCount} Nodes.");
            _log.Info($"[Dump] Gespeichert: {dumpFile}");
        }
        catch (Exception ex)
        {
            _log.Warning($"[Dump] Datei-Fehler: {ex.Message}");
            _tolk.SpeakInterrupt("Dump nur im Dalamud-Log. Datei-Fehler.");
        }
    }

    /// <summary>
    /// Rekursive Node-Ausgabe für DumpAddon.
    /// Gibt NodeId, Typ, Flags, Sichtbarkeit und Inhalt aus.
    /// Tiefenlimit = 5, verhindert Stack-Overflow bei tiefen Bäumen.
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
            var t = ((AtkTextNode*)node)->NodeText.ToString().Replace("\n", "↵");
            if (t.Length > 80) t = t[..80] + "…";
            extra = $" \"{t}\"";
        }
        else if (typeNum >= 1000)
        {
            var comp = ((AtkComponentNode*)node)->Component;
            if (comp != null)
            {
                var ct = comp->GetComponentType();
                extra = $" [CT={ct}({(int)ct}) Ch={comp->UldManager.NodeListCount}]";

                // Für Listen: Länge und aktuell gewählter Index
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
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup,        "Talk",        OnTalkOpen);
        _addonLifecycle.UnregisterListener(AddonEvent.PostReceiveEvent, "Talk",        OnTalkReceive);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup,        "SelectYesno", OnYesNoOpen);
        _addonLifecycle.UnregisterListener(AddonEvent.PostReceiveEvent, "SelectYesno", OnYesNoReceive);
        _addonLifecycle.UnregisterListener(OnSelectStringOpen);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "Title", OnTitleScreenOpen);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup,        "_TitleMenu", OnTitleMenuOpen);
        _addonLifecycle.UnregisterListener(AddonEvent.PostReceiveEvent, "_TitleMenu", OnTitleMenuReceive);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup,        "ConfigSystem", OnConfigSystemOpen);
        _addonLifecycle.UnregisterListener(AddonEvent.PreFinalize,      "ConfigSystem", OnConfigSystemClose);
        _addonLifecycle.UnregisterListener(AddonEvent.PostReceiveEvent, "ConfigSystem", OnConfigSystemReceive);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "ConfigPadCalibration", OnPadCalibrationOpen);
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
