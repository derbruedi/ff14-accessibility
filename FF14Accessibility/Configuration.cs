using System.Collections.Generic;
using Dalamud.Configuration;
using FF14Accessibility.Services;

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
    public string KeyFaceWaypoint = "Numpad5";          // Einmal zur Wegrichtung der Gehhilfe drehen. Bare Numpad5 ist im Keybind-Dump CAMERA_FOCUS - vom User bewusst geopfert (rein visuell), das Plugin SCHLUCKT die Taste, damit die Kamera nicht zusaetzlich springt.
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
    public string KeyRestedStatus  = "Umschalt+L";      // Ruhebereich + Erholungsbonus ansagen (neben der Stufe auf L; Umschalt+L steht nicht in der Belegt-Liste des Keybind-Dumps)
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
    // [Chat-Puffer] An den Anfang / ans Ende des aktuellen Puffers springen. Der
    // Verlauf hebt eine ganze Sitzung je Puffer auf, und ein Kampf-Register allein
    // laeuft in die Tausende Zeilen - ein Ende durch wiederholtes Blaettern zu
    // erreichen ist damit keine echte Moeglichkeit. Laut Live-Keybind-Dump
    // (2026-08-11, 679 Eintraege) sind beide Kombis frei: das Spiel belegt HOME und
    // END bare (CAMERA_MODE, CAMERA_LOAD), mit Strg (MENU_SCALE, CAMERA_SAVE) und
    // Strg+Umschalt (CAMERA_RESET), aber nicht mit Umschalt allein.
    public string KeyChatReadOldest = "Umschalt+Pos1";         // Nachlese: an den Anfang des Puffers springen
    public string KeyChatReadNewest = "Umschalt+Ende";         // Nachlese: ans Ende des Puffers springen
    // [Chat-Puffer] Chat-Registerkarte des SPIELS umschalten. Das Spiel hat dafuer
    // ueberhaupt keine Taste: die vollstaendige InputId-Tabelle wurde nach LOG, TAB
    // und CHAT durchsucht (640 Mitglieder, gegen den 679-Eintraege-Livedump
    // geprueft) - TAB_NEXT/TAB_PREV sind der allgemeine UI-Cursor und erreichen den
    // Chatlog nicht, CHATLOG_VIEWERMODE ist unbelegt und schaltet keine Register,
    // ein LOG_TAB_NEXT o.ae. existiert nicht, und einen Slash-Befehl gibt es auch
    // nicht. Ein Sehender KLICKT das Register an.
    // Alt+Pos1/Alt+Ende, weil das Spiel Alt ausschliesslich mit BUCHSTABEN belegt
    // (Chat-Befehle: Alt+R/S/P/L/H/Y/F/A/N/M/T/C) - dieselbe Begruendung, aus der
    // KeyChatCatPrev/-Next oben schon auf Alt+Bild liegen.
    public string KeyChatTabPrev   = "Alt+Pos1";               // Vorherige Chat-Registerkarte (schaltet das Spiel um)
    public string KeyChatTabNext   = "Alt+Ende";               // Nächste Chat-Registerkarte
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
    public string KeyReadLootRolls  = "Umschalt+F7";      // Offene Gruppen-Verlosungen vorlesen (Umschalt+F1..F12 laut Keybind-Dump spielfrei; F7 dort noch frei)
    public string KeyFocusLootRolls = "Umschalt+F8";      // In das Verlosungs-Fenster springen, um dort per Nummernblock Bedarf/Gier/Passen zu waehlen. BEWUSST eine Taste und nicht automatisch: ein Fenster, das sich mitten im Kampf den Fokus greift, schluckt den Nummernblock, waehrend man noch laufen muss.
    public string KeyReadBoard      = "Strg+Umschalt+F4"; // Kartenspiel: das 3x3-Brett vorlesen
    public string KeyReadHand       = "Strg+Umschalt+F5"; // Kartenspiel: die eigene Hand vorlesen
    public string KeyRecordTrail    = "Strg+Umschalt+F6"; // Spur aufzeichnen an/aus: eine Stelle, die das Wegenetz nicht kennt, einmal selbst ablaufen. Strg+Umschalt+F6 ist frei (F1-F5 dieses Clusters sind Goto/Copy-Coords, AoE-Toggle und Kartenspiel).
    // Sonderaktionen eines Auftrags ("Duty Actions") - fangen, betaeuben, ausloesen.
    //
    // ERST AUF Strg+Numpad7/9/1 GELEGT, VOM USER AM 2026-08-19 VERWORFEN: "das mit
    // strg+1 und 7 wird nicht funktionieren er nimmt die strg taste nicht". Auf
    // Nachfrage bestaetigt: Strg+Numpad3 (Gehhilfe) FUNKTIONIERT bei ihm. Es ist
    // also kein allgemeines Strg-Problem, sondern betrifft genau die Ziffern 1/7/9.
    // Damit ist die Zeile "Strg+Numpad1/3/5/7/9 frei" in game-api.md fuer 1/7/9
    // widerlegt - der Keybind-Dump hat sie als frei gemeldet, in der Praxis kommen
    // sie nicht an. Ursache ungeklaert und NICHT geraten; dokumentiert.
    //
    // Umschalt+F10/F11 sind die letzten freien Plaetze im Umschalt+F-Cluster
    // (F1/F2/F12 Plugin-Liste, F3 Gil, F4-F6 Emotes, F7/F8 Verlosung, F9
    // Einstellungen). Einhaendig zu treffen - und das ist hier die Anforderung,
    // die Taste wird MITTEN IM KAMPF im richtigen Moment gebraucht. Nur die
    // Ansage, die nicht eilt, liegt im langsameren Dreifachgriff.
    public string KeyDutyAction1    = "Umschalt+F10";      // Sonderaktion auf Platz 1 ausloesen
    public string KeyDutyAction2    = "Umschalt+F11";      // Sonderaktion auf Platz 2 ausloesen
    public string KeyDutyActionList = "Strg+Umschalt+F8";  // Vorhandene Sonderaktionen noch einmal ansagen (eilt nicht)
    public string KeyReadTasks      = "Strg+Umschalt+F7"; // Aufgabenliste des laufenden Inhalts vorlesen (Freibrief, Dungeon, FATE) - die Zeilen, die ein sehender Spieler am Bildschirmrand liest. Strg+Umschalt+F7 ist der naechste freie Platz dieses Clusters (F1-F6 belegt).
    // [Einstellungsmenue] Oeffnet das gesprochene Einstellungsmenue. Umschalt+F9 ist
    // laut Live-Keybind-Dump frei (F9 bare ist TARGET_PET, mit Umschalt unbelegt).
    public string KeyOptionsMenu    = "Umschalt+F9";      // Einstellungen oeffnen

    // [Peil-Ton] An/aus fuer den Ziel-Peilton. Strg+Umschalt+F9 ist frei: von
    // diesem Cluster sind F1/F2 (Koordinaten), F3 (AoE-Warnton), F4/F5 (Triple
    // Triad), F6 (Spuren), F7 (Aufgabenliste) und F8 (Sonderaktionen) belegt,
    // F9-F12 nicht. Strg+F* ist laut Keybind-Dump spielfrei, Strg+Umschalt+F*
    // erst recht.
    public string KeyToggleBeacon   = "Strg+Umschalt+F9";  // Peil-Ton an/aus
    public string KeyDeepFloor      = "Strg+F";           // [Tiefes Gewoelbe] welches Gewoelbe und welche Ebene. Die eine Zahl, in der der ganze Lauf gemessen wird, und die das Spiel nur beilaeufig nennt.

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
        KeyRestedStatus  = defaults.KeyRestedStatus;
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
        KeyChatReadOldest = defaults.KeyChatReadOldest; // [Chat-Puffer]
        KeyChatReadNewest = defaults.KeyChatReadNewest; // [Chat-Puffer]
        KeyChatTabPrev   = defaults.KeyChatTabPrev;     // [Chat-Puffer]
        KeyChatTabNext   = defaults.KeyChatTabNext;     // [Chat-Puffer]
        KeyOptionsMenu   = defaults.KeyOptionsMenu;     // [Einstellungsmenue]
        KeyReadBoard     = defaults.KeyReadBoard;
        KeyReadHand      = defaults.KeyReadHand;
        KeyDutyAction1   = defaults.KeyDutyAction1;
        KeyDutyAction2   = defaults.KeyDutyAction2;
        KeyDutyActionList = defaults.KeyDutyActionList;
    }

    // ── [Chat-Puffer] Sprachschaltungen ───────────────────────────
    //
    // BEIDE FELDER BRAUCHEN KEINE MIGRATION. Ein fehlender Schluessel bedeutet
    // "noch nie angefasst" und erbt die Voreinstellung, die aus dem Filtersatz des
    // Registers abgeleitet wird - eine leere Sammlung aus einer alten Konfiguration
    // verhaelt sich also genau wie eine frische. Deshalb bleibt Version unangetastet.

    /// <summary>
    /// Welche der SPIELEIGENEN Chat-Register laut vorgelesen werden, nach
    /// Registerindex. Fehlender Schluessel = die aus dem Filtersatz dieses Registers
    /// abgeleitete Voreinstellung (siehe <see cref="Services.ChatTabSpeech"/>);
    /// Index -1 ist der Sammelpuffer, der benutzt wird, wenn der Filterzustand des
    /// Spiels nicht lesbar ist.
    ///
    /// Ein ausgeschaltetes Register wird weiterhin vollstaendig archiviert und
    /// bleibt blaetterbar. Die Schaltung unterdrueckt die Echtzeit-Sprachausgabe und
    /// sonst nichts.
    ///
    /// Schluessel als INT, damit im JSON schlichte Zahlen stehen, die auch dann noch
    /// rund laufen, wenn das Spiel spaeter etwas mit seinen Registerplaetzen macht.
    /// </summary>
    public Dictionary<int, bool> ChatTabSpeech = new();

    /// <summary>
    /// Welche TEILE eines Registers laut vorgelesen werden: aeusserer Schluessel der
    /// Registerindex des Spiels, innerer Schluessel eine der spieleigenen
    /// Schaltungen. Je Register, damit ein Kanal im Kampfregister still sein und in
    /// einem eigens dafuer gefuehrten Register hoerbar bleiben kann.
    ///
    /// DER INNERE SCHLUESSEL IST ENTWEDER EIN KANAL ODER EINE ZEILE, und beide
    /// koennen nicht kollidieren: ein KANAL des Kampflogs ist minus seiner Kategorie
    /// (negativ, so wie <see cref="Services.GameChatChannel.Key"/> ihn fuehrt), eine
    /// ZEILE ist ihre <c>LogFilter</c>-Zeilennummer (nie negativ). Bei den Kategorien
    /// 1 und 2 IST der Kanal seine einzige Zeile, dieselbe positive Zahl bedeutet
    /// dort also auf beiden Ebenen dasselbe. Eine Zeilennummer gehoert zu genau einer
    /// Kategorie, ein positiver Schluessel ist also nie mehrdeutig.
    ///
    /// EIN FEHLENDER SCHLUESSEL ERBT DEN HAUPTSCHALTER DES REGISTERS, und das ist
    /// eine Regel, keine Bequemlichkeit: ein Kanal, den niemand angefasst hat, muss
    /// sich wie seine Nachbarn verhalten und nicht verstummen - ein Kanal, den ein
    /// Patch hinzufuegt, faengt in einem sprechenden Register also hoerbar an.
    /// Stille ist nie die Voreinstellung fuer etwas, das der Spieler nie eingestellt
    /// hat.
    ///
    /// NUR SPRACHE. Nichts hier entscheidet, ob eine Zeile existiert, ob sie
    /// archiviert wird oder in welchem Puffer sie landet - alle drei bleiben bei den
    /// Filterzeilen des Spiels (<see cref="Services.GameChatFilters"/>).
    /// </summary>
    public Dictionary<int, Dictionary<int, bool>> ChatTabChannelSpeech = new();

    /// <summary>
    /// Welches der beiden Chatsysteme spricht und die Tasten bekommt: das
    /// gewohnte mit den festen Kategorien (Dialoge, Sagen, Gruppe, ... Beute)
    /// oder das neue aus PR #5, dessen Puffer den Registern und Filtern des
    /// SPIELS folgen. Umschaltbar im Optionsmenue (Umschalt+F9).
    ///
    /// VORBELEGT AUF DAS GEWOHNTE. Der Testzweig soll das Neue hoerbar machen,
    /// nicht heimlich einfuehren: wer nichts umstellt, hoert genau das, was er
    /// aus v5.83 kennt, und kann von dort aus vergleichen.
    ///
    /// Der Schalter entscheidet NUR ueber Sprache und Tasten. Mitgeschrieben
    /// wird immer in BEIDE Nachlesen, damit ein Umschalten keine Luecke
    /// hinterlaesst - siehe LegacyChatReaderService.
    /// </summary>
    public bool UseLegacyChatSystem = true;

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
    // NPC-Dialoge (XivChatType.NPCDialogue 61 und NPCDialogueAnnouncements 68,
    // Werte per ilspycmd aus Dalamud 2026-08-10). Das ist, was Bosse und
    // Quest-NPCs waehrend eines Kampfes sagen - es kam bisher gar nicht an,
    // weil beide Typen in ChatReaderService.ShouldRead fehlten (User-Meldung
    // 2026-08-10: "in diversen Kaempfen npc dialoge die nicht vorgelesen
    // werden"). Das _BattleTalk-FENSTER war schon angebunden, der Chat-Weg nicht.
    public bool ReadNpcDialogue    = true;
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
    public bool AnnounceDeepRoomChange = true;  // [Tiefes Gewoelbe] beim Betreten eines anderen Raumes ansagen, welcher es ist. Ein sehender Spieler liest seine Position fortlaufend von der Gewoelbe-Karte ab; eine Liste, die man abfragen muss, ist nicht dieselbe Information.
    public float BeaconVolume = 0.35f;          // Peil-Ton: 0 = stumm, 1 = volle Lautstärke

    /// <summary>
    /// Peil-Ton auf das getrackte Ziel (Spielerwunsch 2026-08-19). Läuft, sobald
    /// im Objekt-Browser etwas gewählt oder ein Ziel anvisiert ist - nicht erst
    /// mit der Gehhilfe. Klingt je Zielart anders, wird mit der Entfernung leiser
    /// und VERSTUMMT, sobald man richtig ausgerichtet steht (Aufzüge, Plattformen).
    /// Standard AN, abschaltbar mit <see cref="KeyToggleBeacon"/>.
    /// </summary>
    public bool TargetBeaconEnabled = true;

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
    // Feinere Ziel-HP-Stufen (alle 5 Prozent unter 30), solange ein FREIBRIEF laeuft.
    // Fang-Auftraege wollen den Gegner geschwaecht statt tot; mit den groben Stufen
    // 25/10 ist dieses Fenster nicht zu treffen (gemessen 2026-08-19: von 18 auf tot
    // in sechs Sekunden). Abschaltbar, weil es auf einem Toetungs-Freibrief ein paar
    // Ansagen mehr sind.
    public bool FineTargetHpDuringLeve = true;
    public bool AnnounceEnemyCast = true;       // Ansage wenn das Ziel eine Aktion wirkt
    // Sonderaktionsleiste eines Auftrags. STANDARD AN, anders als die Flaechenwarnung:
    // hier wird nichts berechnet und nichts behauptet - die Leiste ist da oder nicht,
    // und ohne Ansage erfaehrt ein blinder Spieler ihr Auftauchen ueberhaupt nicht.
    // Sie erscheint selten, die Ansage kann also nicht zur Dauerbeschallung werden.
    public bool AnnounceDutyActions = true;

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

    // Klang der Warnung (User-Wunsch 2026-08-21: "der ist nervig"). Vier Stimmen
    // zur Wahl, im Einstellungsmenü mit Vorhören - siehe AoeWarnTone.
    // STANDARD IST NICHT MEHR DER ALTE KLANG: der bisherige blanke Sinus auf
    // 660 Hz ist der eine, von dem gemeldet ist, dass er auf Dauer stört, also
    // wäre er ein schlechter Standard für alle anderen auch. Er bleibt als
    // "Hell" wählbar, die Umstellung ist damit umkehrbar. Welche Stimme am Ende
    // die beste ist, entscheidet das Ohr des Spielers, nicht diese Zeile.
    public AoeWarnTone AoeWarnSound = AoeWarnTone.Soft;

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
    // Betreten/Verlassen eines Ruhebereichs ansagen (Gasthaus, Stadt: dort
    // sammelt sich der Erholungsbonus an). Quelle ist der Sichelmond an der
    // EP-Leiste - AddonExp.MoonIconNode, also genau das Zeichen, das ein
    // sehender Spieler sieht. Siehe CombatService.TrackRestedArea.
    public bool AnnounceRestedArea = true;
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
    // Gruppen-Verlosungen ("Bedarf/Gier/Passen") ansagen, sobald sie aufgehen.
    // Quelle ist der Spielzustand Client.Game.UI.Loot, NICHT das NeedGreed-
    // Fenster - dadurch unabhaengig davon, ob das Fenster Fokus hat.
    // Siehe LootRollService.
    public bool AnnounceLootRolls = true;
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

    // Selbst abgelaufene Spuren ueber Stellen, die das Wegenetz nicht kennt
    // (Steilhaenge, Absaetze - siehe TrailService). Bleiben ueber Sitzungen
    // erhalten, weil eine Luecke im Netz auch nach einem Neuaufbau da ist.
    public List<NavTrail> Trails = new();
}

/// <summary>
/// Eine selbst abgelaufene Verbindung ueber eine Luecke im Wegenetz. vnavmesh
/// berechnet sein Netz mit Recast und kennt daher keine Stelle, die man nur ueber
/// einen Steilhang oder einen Absatz erreicht; eine solche Strecke einmal selbst
/// zu laufen ist die einzige Quelle, die nicht raet. Abgefahren wird sie mit
/// <c>vnavmesh.Path.MoveTo</c>, das eine feste Punktliste ohne jede Wegsuche
/// abarbeitet. Siehe TrailService und docs/game-api.md.
/// </summary>
[Serializable]
public sealed class NavTrail
{
    /// <summary>Gebiet, in dem die Spur gilt (TerritoryType).</summary>
    public ushort Territory;

    /// <summary>Gesprochener Name, z. B. "Verbindung 1".</summary>
    public string Name = string.Empty;

    /// <summary>Aufgezeichnete Punkte in Laufrichtung, je [X, Y, Z]. Als float[]
    /// und nicht als Vector3, damit die Dalamud-Konfiguration sie so verlaesslich
    /// serialisiert wie die Angel-Koordinaten darueber.</summary>
    public List<float[]> Points = new();

    /// <summary>Ob die Spur auch rueckwaerts benutzt werden darf. Nur wahr, wenn
    /// sie beim Aufzeichnen praktisch eben blieb: die Figur laeuft Absaetze
    /// hinunter, aber nicht hinauf, und eine Einbahn-Ueberquerung sperrt den
    /// Spieler auf der anderen Seite ein (2026-08-09 in-game passiert).</summary>
    public bool BothWays;
}
