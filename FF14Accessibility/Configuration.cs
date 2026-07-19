using Dalamud.Configuration;

namespace FF14Accessibility;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 2;

    // Tastaturbelegung. Standard ab V4.21 kollisionsfrei laut Live-Keybind-Dump
    // (2026-07-10): N ist der einzige freie Buchstabe, Strg+F1..F12 sind frei.
    // Format: "Taste" oder "Strg+Umschalt+Taste" (Modifier: Strg, Umschalt, Alt).
    public string KeyHelp         = "Strg+F1";          // Kontextbezogene Hilfe
    public string KeyNextObject   = "N";                // Objekt-Browser: nächstes Objekt anvisieren
    public string KeyPrevObject   = "Umschalt+N";       // Objekt-Browser: vorheriges Objekt
    public string KeyCategory     = "Strg+N";           // Objekt-Browser: Kategorie vorwärts
    public string KeyCategoryPrev = "Strg+Umschalt+N";  // Objekt-Browser: Kategorie zurück (Umschalt = rückwärts, wie N/Umschalt+N; Strg+Alt+N war NVDA-Start-Hotkey!)
    // WINDOWS-FALLE Umschalt+Nummernblock: bei aktivem NumLock wandelt der
    // Tastaturtreiber Umschalt+Numpad-Ziffer in die NAVIGATIONS-Taste um
    // (Numpad3 -> Bild-ab) und laesst Umschalt dabei kuenstlich los - das
    // Plugin sieht NIE VK Numpad3 (Log 2026-07-16: kein einziger
    // Gehhilfe-Trigger seit dem V4.61-Umzug; Bild-ab ist im Spiel obendrein
    // CAMERA_ZOOMOUT). Nur Strg+Numpad-Kombis sind zuverlaessig.
    public string KeyWalkGuide    = "Strg+Numpad3";     // Gehhilfe an/aus (neben Auto-Lauf Numpad3; Strg+Numpad3 laut Keybind-Dump frei)
    public string KeyAutoWalk     = "Numpad3";          // Auto-Lauf zum Ziel an/aus (braucht vnavmesh)
    public string KeyRoutePreview = "Strg+Numpad5";     // Routen-Vorschau: Weg ansagen ohne zu laufen (Numpad5 hat die tastbare Erhebung; bare Numpad5=CAMERA_FOCUS, Strg+Numpad5 frei)
    public string KeyReadUI       = "Strg+F10";         // Aktuelles Menü vorlesen
    public string KeySilence      = "Strg+F11";         // Sprache stoppen
    public string KeyCombatStatus = "Strg+H";           // HP/MP ansagen (H=Health; bare H ist im Spiel MENU_CRAFT, Modifier+H laut Keybind-Dump frei)
    public string KeyDumpUI       = "Strg+F5";          // Node-Tree des aktuellen Addons auf Desktop speichern
    public string KeyWhereAmI     = "Strg+F2";          // Aktives Fenster ansagen + sichtbare Fenster ins Log
    public string KeyReadHotbar   = "Strg+F9";          // Aktionsleiste 1 vorlesen (was liegt auf Taste 1-0)
    public string KeyReadInventory = "Strg+F3";         // Inventar vorlesen (Tasche + Schlüsselgegenstände)
    public string KeyReadGil       = "Umschalt+F3";     // Nur den Gil-Stand ansagen (Umschalt+F1..F12 laut Keybind-Dump frei)
    public string KeyLevelExp      = "Strg+L";          // Stufe + fehlende EXP ansagen (L=Level; bare L ist im Spiel Linkshell)
    public string KeyEmoteNext     = "Umschalt+F5";     // Emote-Browser: nächstes Emote ansagen
    public string KeyEmotePrev     = "Umschalt+F4";     // Emote-Browser: vorheriges Emote ansagen
    public string KeyEmoteDo       = "Umschalt+F6";     // Gewähltes Emote ausführen
    public string KeyBestiary      = "Strg+F4";         // Bestiarium (Jagdtagebuch) komplett vorlesen (Strg+F4 laut Keybind-Dump frei)
    public string KeyReadEquipment = "Strg+F6";         // Angelegte Ausrüstung vorlesen (Strg+F6 laut Keybind-Dump frei)
    public string KeyEquipBest     = "Strg+F7";         // Empfohlene Ausrüstung anlegen - Spiel-eigener Optimierer (Strg+F7 laut Keybind-Dump frei)
    public string KeyRandomLook    = "Strg+F8";         // Charaktererschaffung: "Zufälliges Aussehen"-Knopf drücken (Strg+F8 laut Keybind-Dump frei)
    // Skill-Browser (V4.75): Aktionsleiste 1 per Tastatur umbelegen. Umschalt+F7-F10
    // laut Keybind-Dump frei (Umschalt+F1-F12 alle frei, F3-F6 nutzt das Plugin schon).
    public string KeySkillPrev     = "Umschalt+F7";     // Skill-Browser: vorheriger gelernter Skill
    public string KeySkillNext     = "Umschalt+F8";     // Skill-Browser: nächster gelernter Skill
    public string KeySkillSlot     = "Umschalt+F9";     // Ziel-Taste weiterschalten (sagt aktuelle Belegung an)
    public string KeySkillAssign   = "Umschalt+F10";    // Gewählten Skill auf die Ziel-Taste legen
    public string KeySkillBar      = "Umschalt+F11";    // Ziel-Leiste weiterschalten (Kommandomenü 1-10; Umschalt+F11 laut Keybind-Dump frei)
    // Nachlese-Browser (V4.90): kategorisierter Chat-/Dialog-/System-Verlauf.
    // Komma und Punkt sind im Spiel NICHT belegt (Keybind-Dump 2026-07-17) und
    // werden bei aktivem Textfeld vom Spiel verbraucht (dann kein Fehlauslösen).
    public string KeyChatCatPrev   = "Strg+,";          // Nachlese: vorherige Kategorie (Kanal/Dialoge/System)
    public string KeyChatCatNext   = "Strg+.";          // Nachlese: nächste Kategorie
    public string KeyChatReadOlder = ",";               // Nachlese: ältere Nachricht in der Kategorie
    public string KeyChatReadNewer = ".";               // Nachlese: neuere Nachricht in der Kategorie
    // Benachrichtigungen (V5.9): Einladungen (Freie Gesellschaft, Gruppe,
    // Freundesliste) erscheinen als Popup, das ein Sehender anklickt. Ohne
    // Tastaturweg lief die Einladung fuer den User schlicht ab (Log 2026-07-18
    // 18:20:48). Strg+F12 laut Keybind-Dump frei.
    public string KeyNotification  = "Strg+F12";        // Offene Benachrichtigung aktivieren (Einladung annehmen)
    // Dalamud-Plugin-Liste (V5.13): Dalamuds eigener Plugin-Installer ist ImGui
    // und damit weder vom Screenreader noch vom UIReader lesbar. Gelesen wird
    // deshalb die Datenquelle dahinter (IDalamudPluginInterface.InstalledPlugins).
    // Umschalt+F1/F2/F12 sind die letzten laut Keybind-Dump freien F-Kombis.
    public string KeyPluginsNext    = "Umschalt+F1";     // Plugin-Liste: naechstes Plugin (1. Druck = Uebersicht)
    public string KeyPluginsPrev    = "Umschalt+F2";     // Plugin-Liste: vorheriges Plugin
    public string KeyPluginsConfig  = "Umschalt+F12";    // Einstellungen des gewaehlten Plugins oeffnen (ImGui, nicht vorlesbar)

    /// <summary>Resets all hotkeys to the current defaults (used by config migration).</summary>
    public void ResetKeysToDefaults()
    {
        var defaults = new Configuration();
        KeyHelp         = defaults.KeyHelp;
        KeyNextObject   = defaults.KeyNextObject;
        KeyPrevObject   = defaults.KeyPrevObject;
        KeyCategory     = defaults.KeyCategory;
        KeyCategoryPrev = defaults.KeyCategoryPrev;
        KeyWalkGuide    = defaults.KeyWalkGuide;
        KeyAutoWalk     = defaults.KeyAutoWalk;
        KeyRoutePreview = defaults.KeyRoutePreview;
        KeyReadUI       = defaults.KeyReadUI;
        KeySilence      = defaults.KeySilence;
        KeyCombatStatus = defaults.KeyCombatStatus;
        KeyDumpUI       = defaults.KeyDumpUI;
        KeyWhereAmI     = defaults.KeyWhereAmI;
        KeyReadHotbar   = defaults.KeyReadHotbar;
        KeyReadInventory = defaults.KeyReadInventory;
        KeyReadGil       = defaults.KeyReadGil;
        KeyLevelExp      = defaults.KeyLevelExp;
        KeyEmoteNext     = defaults.KeyEmoteNext;
        KeyEmotePrev     = defaults.KeyEmotePrev;
        KeyEmoteDo       = defaults.KeyEmoteDo;
        KeyBestiary      = defaults.KeyBestiary;
        KeyReadEquipment = defaults.KeyReadEquipment;
        KeyEquipBest     = defaults.KeyEquipBest;
        KeySkillPrev     = defaults.KeySkillPrev;
        KeySkillNext     = defaults.KeySkillNext;
        KeySkillSlot     = defaults.KeySkillSlot;
        KeySkillAssign   = defaults.KeySkillAssign;
        KeySkillBar      = defaults.KeySkillBar;
        KeyChatCatPrev   = defaults.KeyChatCatPrev;
        KeyChatCatNext   = defaults.KeyChatCatNext;
        KeyChatReadOlder = defaults.KeyChatReadOlder;
        KeyChatReadNewer = defaults.KeyChatReadNewer;
    }

    // Chat
    public bool ReadSayChat        = true;
    public bool ReadShoutChat      = true;
    public bool ReadPartyChat      = true;
    public bool ReadAllianceChat   = true;
    public bool ReadTellChat       = true;
    public bool ReadFCChat         = true;
    public bool ReadSystemMessages = true;
    // (V4.91: ReadCombatMessages entfernt - das Kampflog-Vorlesen aus V4.90 kam
    // in-game nie an und wurde samt Nachlese-Kategorie "Kampf" zurueckgebaut.)
    // Tipp-Echo im Chat-Eingabefeld (Senden): NVDA liest das Spiel-Chatfeld
    // nicht, daher spricht das Plugin die getippten Zeichen (V4.90).
    public bool EchoChatInput      = true;

    // Toasts (V4.80): Bildschirm-Popups des Spiels via IToastGui. Fehler-Toasts
    // ("Das Ziel ist zu weit entfernt.") leben NUR im _TextError-Overlay:
    // PostRefresh feuert dafuer nie (Log 2026-07-17: einziges Lifecycle-Event
    // war das leere PostSetup beim Login) und die meisten werden auch nicht
    // in den Chat gespiegelt - ohne Toast-Events blieben sie komplett stumm
    // (User-Meldung 2026-07-17).
    public bool AnnounceErrorToasts = true;  // Fehler-Popups ("zu weit entfernt", "noch nicht bereit")
    public bool AnnounceInfoToasts  = true;  // normale + Quest-Toasts (Gebiets-/Fortschritts-Meldungen)

    // Navigation
    public float NearbyDistance = 30f;
    public bool AnnounceTargetChanges = true;   // Zielwechsel (Tab/F1-F12) ansagen
    public bool AnnounceMapFlag = true;         // neu gesetzte Karten-Markierung ansagen
    public float BeaconVolume = 0.35f;          // Gehhilfe-Ton: 0 = stumm, 1 = volle Lautstärke

    // Auto-Lauf: "Noch X Meter" erst nach so vielen zurückgelegten Metern
    // wieder ansagen (0 = gar nicht). Früher alle 3 Sekunden - das war auf
    // langen Strecken eine Dauerbeschallung (User 2026-07-18).
    public float AutoWalkProgressStep = 50f;

    // Wegenetz-Aufbau (vnavmesh) in 20-Prozent-Schritten ansagen, plus
    // "fertig geladen" am Ende (User-Wunsch 2026-07-18).
    public bool AnnounceMeshProgress = true;

    // Gehhilfe (V4.63): Beacon und Ansagen folgen der vnavmesh-Wegpunkt-Route
    // (um Hindernisse herum) statt der Luftlinie. false = alte Luftlinien-Führung.
    public bool WalkGuideRouteMode = true;
    public float RouteCueVolume = 0.4f;         // Wegpunkt-/Ankunftston der Gehhilfe: 0 = stumm

    // Auto-Lauf: wie nah vnavmesh vor dem Ziel anhält (Meter)
    public float AutoWalkPlaceStopRange = 1.0f;      // Orte, Wegpunkte, Questziele: dicht dran
    public float AutoWalkTransitionStopRange = 0.5f; // Zonen-Übergänge: fast drauf, damit der Übergang auslöst

    // Kampf
    public bool AnnounceTargetHp = true;        // Ziel-HP in Stufen ansagen (im Kampf)
    public bool AnnounceEnemyCast = true;       // Ansage wenn das Ziel eine Aktion wirkt
    // Ziel-Ton bei anvisiertem Gegner ENTFERNT (2026-07-18, User): das Spiel
    // spielt selbst einen Ton - ein zweiter obendrauf war nur Lärm.

    // Ansage-Spam-Filter (V4.62, STATUS.md V4.60/61 dokumentiert): _StatusCustom0
    // (Buff-Leiste) sagte den Sprint-Countdown im Sekundentakt an ("20s".."1s") -
    // reine Ziffern ohne Statuseffekt-Namen (der Text-Scan liest keine Icon-Namen,
    // nur die Restzeit). _FlyText sagte jede Kampfzahl/jedes Buff-Popup an
    // ("+Sprint", "700", "(+100 %)"). Beides ist reiner Laerm ohne Mehrwert -
    // Default: unterdrueckt. Flag bleibt fuer den seltenen Fall, dass jemand den
    // rohen Text-Scan dieser HUD-Elemente trotzdem hoeren moechte (z.B. Debugging).
    public bool SuppressStatusBarSpam = true;   // _StatusCustom0-Sprint-Countdown stumm
    public bool SuppressFlyTextSpam   = true;   // _FlyText-Kampfzahlen stumm
}
