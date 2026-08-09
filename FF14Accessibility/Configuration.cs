using System.Collections.Generic;
using Dalamud.Configuration;

namespace FF14Accessibility;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 2;

    // Sprache aller Mod-Ansagen. Auto = folgt der Windows-Sprache (deutsches
    // Windows -> Deutsch, sonst Englisch). Umschaltbar mit "/acc lang de|en|auto".
    // Spielinhalte (Item-/NPC-Namen, Questtexte) bleiben davon unberuehrt - die
    // kommen bereits in der Spielsprache vom Spiel. Siehe Loc / AccessibilityStrings.
    public LanguageMode Language = LanguageMode.Auto;

    // Tastaturbelegung. Standard ab V4.21 kollisionsfrei laut Live-Keybind-Dump
    // (2026-07-10): N ist der einzige freie Buchstabe, Strg+F1..F12 sind frei.
    // Format: "Taste" oder "Strg+Umschalt+Taste" (Modifier: Strg, Umschalt, Alt).
    public string KeyHelp         = "Strg+F1";          // Kontextbezogene Hilfe
    // V5.31: N-Familie freigeräumt (N wird künftig anders gebraucht). Der
    // Objekt-Browser liegt jetzt auf den Bild-Tasten. Bare Bild-auf/Bild-ab
    // sind im Spiel CAMERA_ZOOMIN/ZOOMOUT (Keybind-Dump 2026-07-10) - der Zoom
    // ist rein visuell und für blindes Spiel folgenlos (User bestätigt
    // 2026-07-22); das Plugin verbraucht die Taste ohnehin nicht. Strg+Bild-
    // auf/-ab sind laut Dump völlig frei.
    public string KeyNextObject   = "BildAb";           // Objekt-Browser: nächstes Objekt (Bild-ab = runter/vor)
    public string KeyPrevObject   = "BildAuf";          // Objekt-Browser: vorheriges Objekt (Bild-auf = hoch/zurück)
    public string KeyCategory     = "Strg+BildAb";      // Objekt-Browser: Kategorie vorwärts
    public string KeyCategoryPrev = "Strg+BildAuf";     // Objekt-Browser: Kategorie zurück
    // WINDOWS-FALLE Umschalt+Nummernblock: bei aktivem NumLock wandelt der
    // Tastaturtreiber Umschalt+Numpad-Ziffer in die NAVIGATIONS-Taste um
    // (Numpad3 -> Bild-ab) und laesst Umschalt dabei kuenstlich los - das
    // Plugin sieht NIE VK Numpad3 (Log 2026-07-16: kein einziger
    // Gehhilfe-Trigger seit dem V4.61-Umzug; Bild-ab ist im Spiel obendrein
    // CAMERA_ZOOMOUT). Nur Strg+Numpad-Kombis sind zuverlaessig.
    public string KeyWalkGuide    = "Strg+Numpad3";     // Gehhilfe an/aus (neben Auto-Lauf Numpad3; Strg+Numpad3 laut Keybind-Dump frei)
    public string KeyAutoWalk     = "Numpad3";          // Auto-Lauf zum Ziel an/aus (braucht vnavmesh)
    public string KeyFollowTarget = "+";                // Anvisiertem Ziel fortlaufend folgen an/aus (braucht vnavmesh). BARE + (VK_OEM_PLUS, NICHT Numpad+), im Keybind-Dump 2026-07-26 spielfrei
    public string KeyRoutePreview = "Strg+Numpad5";     // Routen-Vorschau: Weg ansagen ohne zu laufen (Numpad5 hat die tastbare Erhebung; bare Numpad5=CAMERA_FOCUS, Strg+Numpad5 frei)
    public string KeyGotoCoords   = "Strg+Umschalt+F1"; // Zu Koordinaten aus der Zwischenablage laufen (z.B. "24.1 21.0" kopieren, dann Taste). Alle Strg+F/Umschalt+F sind belegt; Strg+F* ist laut Keybind-Dump spielfrei, also ist Strg+Umschalt+F* erst recht frei.
    public string KeyCopyCoords   = "Strg+Umschalt+F2"; // Eigene aktuelle Karten-Koordinaten in die Zwischenablage kopieren (zum Weitergeben im Chat). Gegenstueck zu KeyGotoCoords; Strg+Umschalt+F* laut Keybind-Dump spielfrei.
    public string KeyReadUI       = "Strg+F10";         // Aktuelles Menü vorlesen
    public string KeySilence      = "Strg+F11";         // Sprache stoppen
    public string KeyCombatStatus = "Strg+Entf";        // HP/MP ansagen. NICHT Strg+H: das Spiel oeffnete trotz Strg das Handwerker-Notizbuch (MENU_CRAFT=H), dessen Ansage die HP-Ansage abschnitt (Log 2026-07-19 19:19:00). Entf ist im Keybind-Dump gar nicht belegt
    public string KeySpStatus     = "Strg+Ende";        // SP-Stand (Sammelpunkte, engl. GP) ansagen - der Vorrat, den Sammler fuer Sammel-Fertigkeiten verbrauchen. Strg+Ende ist im Keybind-Dump CAMERA_SAVE (Kamera-Preset speichern) - rein visuell, fuer blindes Spiel folgenlos (wie die akzeptierte Kamera-Zoom-Ueberschneidung der Bild-Tasten). Plugin schluckt die Taste nicht.
    public string KeyToggleHeading = "N";               // Himmelsrichtungs-Ansage beim Drehen an/aus. Bare N ist die einzige freie Buchstaben-Taste im Spiel (in V5.31 fuer neue Features freigeraeumt, Keybind-Dump).
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
    // Skill-Menü (V5.61): modaler Nummernblock-Assistent zum Umbelegen der
    // Leisten (ersetzt die frueheren 5 Umschalt+F7-F11-Einzeltasten). Oeffnen mit
    // Strg+Numpad0 (laut Keybind-Dump frei; Spiel belegt bei Strg+Numpad nur
    // 2/4/6/8), danach navigiert der Nummernblock im Menue (8/2/0/Komma).
    public string KeySkillMenu     = "Strg+Numpad0";
    // Nachlese-Browser (V4.90): kategorisierter Chat-/Dialog-/System-Verlauf.
    // Das ganze Paar liegt auf dem Bild-Tasten-Cluster, der nachweislich
    // modifier-sauber ist (Objekt-Browser auf bare/Strg+Bild funktioniert):
    //   bare Bild = Objekte, Strg+Bild = Objekt-Kategorie,
    //   Umschalt+Bild = Nachricht aelter/neuer, Alt+Bild = Chat-Kategorie.
    // V5.48: bare "." oeffnet in-game das Reittier-Verzeichnis (MENU_MOUNT) -> Lese-
    // Paar auf Umschalt+BildAuf/-Ab. V5.49: Strg+,/Strg+. wurden ebenfalls von
    // MENU_MOUNT geschluckt (Spiel feuert auf Basistaste "." trotz Strg, gleiche
    // Falle wie H/MENU_CRAFT in V5.25, in-game bestaetigt). V5.50: Kategorie-Paar
    // auf Alt+BildAuf/-Ab (User-Wunsch; Alt+Bild ist frei - das Spiel bindet Alt
    // nur mit Buchstaben fuer Chat-Befehle). Bei aktivem Textfeld verbraucht das
    // Spiel die Taste.
    public string KeyChatCatPrev   = "Alt+BildAuf";            // Nachlese: vorherige Kategorie (Kanal/Dialoge/System)
    public string KeyChatCatNext   = "Alt+BildAb";             // Nachlese: nächste Kategorie
    public string KeyChatReadOlder = "Umschalt+BildAuf";       // Nachlese: ältere Nachricht in der Kategorie
    public string KeyChatReadNewer = "Umschalt+BildAb";        // Nachlese: neuere Nachricht in der Kategorie
    // Benachrichtigungen (V5.9): Einladungen (Freie Gesellschaft, Gruppe,
    // Freundesliste) erscheinen als Popup, das ein Sehender anklickt. Ohne
    // Tastaturweg lief die Einladung fuer den User schlicht ab (Log 2026-07-18
    // 18:20:48). Strg+F12 laut Keybind-Dump frei.
    public string KeyNotification  = "Strg+F12";        // Offene Benachrichtigung aktivieren (Einladung annehmen)
    // AoE-Warnton an/aus. Strg+Umschalt+F3 ist frei (nur F1/F2 dieses Clusters sind
    // fuer Goto/Copy-Coords belegt; Strg+F* ist laut Keybind-Dump spielfrei, also
    // Strg+Umschalt+F* erst recht). Kampf-Toggle, wird einmal gesetzt - eine
    // schwerer erreichbare Kombo ist daher unproblematisch.
    public string KeyToggleAoeWarning = "Strg+Umschalt+F3";
    // Dalamud-Plugin-Liste (V5.13): Dalamuds eigener Plugin-Installer ist ImGui
    // und damit weder vom Screenreader noch vom UIReader lesbar. Gelesen wird
    // deshalb die Datenquelle dahinter (IDalamudPluginInterface.InstalledPlugins).
    // Umschalt+F1/F2/F12 sind die letzten laut Keybind-Dump freien F-Kombis.
    public string KeyPluginsNext    = "Umschalt+F1";     // Plugin-Liste: naechstes Plugin (1. Druck = Uebersicht)
    public string KeyPluginsPrev    = "Umschalt+F2";     // Plugin-Liste: vorheriges Plugin
    public string KeyPluginsConfig  = "Umschalt+F12";    // Einstellungen des gewaehlten Plugins oeffnen (ImGui, nicht vorlesbar)
    // Triple Triad (Kartenspiel). Nur im offenen TripleTriad-Fenster nuetzlich, sonst
    // still. Strg+Umschalt+F4/F5 sind frei (F1-F3 dieses Clusters sind Goto/Copy-Coords
    // bzw. AoE-Toggle; Strg+F* ist laut Keybind-Dump spielfrei, Strg+Umschalt+F* erst recht).
    public string KeyReadBoard      = "Strg+Umschalt+F4"; // Kartenspiel: das 3x3-Brett vorlesen
    public string KeyReadHand       = "Strg+Umschalt+F5"; // Kartenspiel: die eigene Hand vorlesen

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
        KeyFollowTarget = defaults.KeyFollowTarget;
        KeyRoutePreview = defaults.KeyRoutePreview;
        KeyGotoCoords   = defaults.KeyGotoCoords;
        KeyCopyCoords   = defaults.KeyCopyCoords;
        KeyReadUI       = defaults.KeyReadUI;
        KeySilence      = defaults.KeySilence;
        KeyCombatStatus = defaults.KeyCombatStatus;
        KeySpStatus     = defaults.KeySpStatus;
        KeyToggleHeading = defaults.KeyToggleHeading;
        KeyToggleAoeWarning = defaults.KeyToggleAoeWarning;
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
        KeySkillMenu     = defaults.KeySkillMenu;
        KeyChatCatPrev   = defaults.KeyChatCatPrev;
        KeyChatCatNext   = defaults.KeyChatCatNext;
        KeyChatReadOlder = defaults.KeyChatReadOlder;
        KeyChatReadNewer = defaults.KeyChatReadNewer;
        KeyReadBoard     = defaults.KeyReadBoard;
        KeyReadHand      = defaults.KeyReadHand;
    }

    // Chat
    public bool ReadSayChat        = true;
    public bool ReadShoutChat      = true;
    public bool ReadPartyChat      = true;
    public bool ReadAllianceChat   = true;
    public bool ReadTellChat       = true;
    public bool ReadFCChat         = true;
    public bool ReadSystemMessages = true;
    // Sammel-Meldungen (XivChatType.Gathering 67): "Du hast X erhalten",
    // "Du beginnst/bist fertig ..." - die Ausbeute-Rueckmeldung beim Abbauen.
    public bool ReadGatheringMessages = true;
    // (V4.91: ReadCombatMessages entfernt - das Kampflog-Vorlesen aus V4.90 kam
    // in-game nie an und wurde samt Nachlese-Kategorie "Kampf" zurueckgebaut.)
    // Tipp-Echo im Chat-Eingabefeld (Senden): NVDA liest das Spiel-Chatfeld
    // nicht, daher spricht das Plugin die getippten Zeichen (V4.90).
    public bool EchoChatInput      = true;
    // Ob die getippten ZEICHEN beim Schreiben (Chat + Eingabefelder) laut
    // vorgelesen werden. User-Wunsch 2026-07-22: aus. Die Kontext-Ansagen
    // (Feldname, "Chat-Eingabe", Kanal) bleiben davon unberuehrt - nur das
    // Zeichen-fuer-Zeichen-Echo (SpeakTextEchoDiff) haengt daran.
    public bool EchoTypedCharacters = false;

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
    public bool AnnounceHeading = true;         // beim Drehen die Himmelsrichtung ansagen, in die man schaut (nur nach Dreh-Ende + Sektorwechsel, siehe HeadingService). Umschaltbar mit KeyToggleHeading
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

    // HP/MP-Töne (V5.28): bei jeder 10-Prozent-Stufe ein kurzer Ton, dessen
    // Stereo-Position den Füllstand abbildet (voll = rechts, leer = links;
    // User-Entscheid 2026-07-20). Gilt auch ausserhalb des Kampfes, weil
    // gerade die Regeneration danach hörbar sein soll. HP tiefer als MP.
    public bool AnnounceVitalCues = true;
    public float VitalCueVolume = 0.4f;         // 0 = stumm, 1 = volle Lautstärke

    // Kampf
    public bool AnnounceTargetHp = true;        // Ziel-HP in Stufen ansagen (im Kampf)
    public bool AnnounceEnemyCast = true;       // Ansage wenn das Ziel eine Aktion wirkt

    // AoE-Ausweich-Warnung (User-Wunsch 2026-07-26): ein Dauerton, solange der
    // Spieler in der Gefahrenflaeche eines gerade laufenden Gegner-Casts steht.
    // Startet mit dem Cast, verstummt beim Verlassen der Flaeche oder Cast-Ende.
    // Geometrie je CastType (Kreis/Kegel/Linie) aus den Telegraph-Daten belegt -
    // siehe CombatService.IsPlayerInAoe. STANDARD AUS: das Feature ist in-game noch
    // nicht bestaetigt, darf blinden Spielern also nicht ungeprueft als Kampf-Hilfe
    // aufgezwungen werden. Opt-in per KeyToggleAoeWarning; spaeterer Release dreht den
    // Standard auf AN, sobald bestaetigt.
    public bool AnnounceAoeWarning = false;
    public float AoeWarnVolume = 0.5f;          // 0 = stumm, 1 = volle Lautstärke

    // Fähigkeit-bereit-Ansage (User-Wunsch 2026-07-30): wenn eine Fähigkeit mit
    // echter Abklingzeit (oGCD) wieder einsatzbereit ist, Ton + Name ansagen.
    // GCD-Angriffsskills ausgeschlossen (CooldownService). STANDARD AN.
    public bool AnnounceSkillReady = true;
    public float SkillReadyCueVolume = 0.5f;    // 0 = stumm, 1 = volle Lautstärke

    // Fortschritt / Beute
    // XP-Gewinn live ansagen. Der Betrag kommt sauber aus PlayerState
    // (GetCurrentClassJobExp, jeden Frame gelesen - dieselbe Quelle wie die
    // Level-Up-Ansage), kein UI-Scraping. Jeder Gewinn wird zusaetzlich in den
    // Nachlese-Kanal "Beute" geschrieben (User-Wunsch 2026-07-25).
    public bool AnnounceXpGain = true;
    // Eingesammelte Gegenstaende/Waehrung (Loot) live ansagen + in den Beute-
    // Kanal schreiben. Kanal = XivChatType.LootNotice (62), verifiziert aus
    // einem Live-[Chat]-Log 2026-07-25 ("Du hast ein Lammfilet erhalten."),
    // deckt Gegner-Drops und alles ins Inventar Wandernde ab. Siehe
    // ChatReaderService.ShouldRead.
    public bool AnnounceLoot = true;
    // Neu erhaltene BENUTZBARE Quest-Gegenstaende melden (Schluesselgegenstaende
    // mit EventItem.Action != 0, z. B. die "Gleissende Lampe" aus Quest 66333).
    // NICHT dasselbe wie AnnounceLoot: der Beute-Kanal sagt nur, DASS etwas
    // ankam - diese Meldung sagt, dass man es benutzen kann und wie man es auf
    // die Leiste bekommt. Siehe InventoryService.Update.
    public bool AnnounceQuestItems = true;
    // Ziel-Ton bei anvisiertem Gegner ENTFERNT (2026-07-18, User): das Spiel
    // spielt selbst einen Ton - ein zweiter obendrauf war nur Lärm.

    // Ansage-Spam-Filter (V4.62, STATUS.md V4.60/61 dokumentiert): _StatusCustom0
    // (Buff-Leiste) sagte den Sprint-Countdown im Sekundentakt an ("20s".."1s") -
    // reine Ziffern ohne Statuseffekt-Namen (der Text-Scan liest keine Icon-Namen,
    // nur die Restzeit). _FlyText sagte jede Kampfzahl/jedes Buff-Popup an
    // ("+Sprint", "700", "(+100 %)"). Beides ist reiner Laerm ohne Mehrwert -
    // Default: unterdrueckt. Flag bleibt fuer den seltenen Fall, dass jemand den
    // rohen Text-Scan dieser HUD-Elemente trotzdem hoeren moechte (z.B. Debugging).
    // Anmelden: solange das Spiel sein HUD aufbaut, schweigen die AUTOMATISCHEN
    // Fenster- und Fokus-Leser. Gemessen 2026-08-06 (Log 17:35:28): ~15 Ansagen
    // in einer Sekunde, die sich gegenseitig abschnitten (User-Meldung). Alles
    // vom Spieler AUSGELOESTE bleibt hoerbar - nur der Selbstaufbau wird
    // verschluckt. 6 s deckt die gemessene Lawine (~2-4 s) mit Reserve ab.
    public float LoginQuietSeconds = 6f;

    public bool SuppressStatusBarSpam = true;   // _StatusCustom0-Sprint-Countdown stumm
    public bool SuppressFlyTextSpam   = true;   // _FlyText-Kampfzahlen stumm

    // Angeln: pro Angelplatz gemerkte Auswurf-Koordinate ("/acc fishhere"). Fuer
    // Stadt-/Hafen-Plaetze, deren Sheet-Koordinate auf trockenem Land liegt, merkt
    // sich der Spieler die echte Stelle, an der er steht. Schluessel = FishingSpot-
    // Zeilen-ID (sprachunabhaengig); Wert = [KarteX, KarteY]. Bleibt ueber Sitzungen
    // erhalten. Siehe FishingService.CaptureHere / GetSpotsInCurrentZone.
    public Dictionary<uint, float[]> FishingSpotOverrides = new();
}
