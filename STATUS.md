# FF14 Accessibility Plugin - Projektstatus

## Ziel
Dalamud-Plugin für FF14 das blinden Spielern via NVDA/TOLK ermöglicht das Spiel vollständig per Tastatur zu spielen.

## STAND JETZT (2026-07-16 abends, V4.72 gebaut + deployed)

### Neuinstallation: Installer aktivierte Plugins nicht (16.07. spaet abends, GEFIXT)
User hat alles neu installiert; Plugins (FF14Accessibility + vnavmesh)
lagen in devPlugins, luden aber nicht. ROOT CAUSE (dekompiliert
verifiziert, Dalamud 15.0.2.2 PluginManager Zeile "if (configuration.
DevMode == true)"): Dalamud scannt DevPluginLoadLocations NUR bei
DevMode=true - der Installer setzte das nie. Dadurch entstanden auch
nie Profil-Eintraege, und der alte Installer-Pfad "Spiel einmal
starten, dann erneut ausfuehren" konnte prinzipiell nie greifen.

Sofort-Fix in dalamudConfig.json (Backup: .bak-vor-plugin-enable):
DevMode=true; pro Plugin DevPluginSettings-Eintrag (Schluessel =
DLL-Pfad, StartOnBoot=true, feste WorkingPluginId-GUID) + DefaultProfile-
Eintrag mit DERSELBEN GUID und IsEnabled=true. Mechanik verifiziert:
LocalDevPlugin uebernimmt eine vorhandene WorkingPluginId aus
DevPluginSettings unveraendert; Profile.WantsPlugin matcht per GUID;
Laden beim Boot nur bei IsEnabled=true UND StartOnBoot=true. BOM-frei
geschrieben. AutomaticReloading bewusst false (kein unangekuendigter
Plugin-Reload mitten im Spiel).

Installer dauerhaft gefixt (InstallerService.cs): PatchDalamudConfig
setzt jetzt DevMode=true und saet DevPluginSettings + Profil-Eintraege
selbst (neue Methode EnableDevPlugin, ersetzt EnableProfilePlugins).
Loc-Strings "PluginNotEnabledYet*" raus, "ProfileStructureUnexpected*"
rein. Neue EXE gebaut + nach dist/ kopiert. NICHT released/committet -
beim naechsten Release mitnehmen.

LADEN BESTAETIGT (Log 22:16): Beide Plugins luden ("Loading dev
plugin", Profil-Eintraege state true, V4.73-Startzeile im Log).

### V4.74 (gebaut + deployed, ungetestet): NVDA stumm nach Neuinstallation
User-Meldung: Plugin laeuft, aber Screenreader spricht nicht. Log
22:16: "Tolk geladen. Screenreader: Keiner erkannt" - NVDA lief
(PID 5940). ROOT CAUSE: Tolk.dll laedt nvdaControllerClient64.dll
NATIV per LoadLibrary(Basisname) - der SetDllImportResolver greift
nur fuer managed P/Invoke. Nativer Loader sucht Spielverzeichnis/
System/PATH, NICHT den Plugin-Ordner. Vorher lagen die DLLs im alten
Spielverzeichnis; neues Steam-Verzeichnis (K:\SteamLibrary\...) hat
sie nicht. FIX V4.74: TolkNative.Initialize laedt
nvdaControllerClient64.dll VORAB mit vollem Pfad aus dem Plugin-
Ordner (NativeLibrary.TryLoad) - Windows-Loader gibt bei spaeterem
LoadLibrary mit gleichem Basisnamen das geladene Modul zurueck.
Plugin damit unabhaengig vom Spielverzeichnis, kein Game-Dir-Kopieren
mehr noetig. Log zeigt jetzt "NVDA-Client vorab geladen: True/False"
+ Warnung bei keinem erkannten Screenreader.

NEBENBEI GEFIXT (csproj): DeployToDevFolder lief VOR DalamudPackager
(beide AfterTargets="Build", NuGet-Import registriert spaeter) ->
devPlugins-Manifest hing immer einen Build hinterher (4.74-Build
deployte 4.73-Manifest). Jetzt AfterTargets="DefaultDalamudPackagerDebug".

AutomaticReloading=true gesetzt (User-Wunsch): in dalamudConfig.json
fuer beide Plugins UND als Installer-Standard - neue Deploys werden
ohne Spiel-Neustart uebernommen. Installer-EXE erneut publiziert
(dist/). Weiterhin NICHT committet/released.

V4.74 BESTAETIGT (User 16.07. spaet abends): "ok funktioniert" -
Sprachausgabe ist nach Neuinstallation wieder da. Der komplette
Block Neuinstallation (Plugin-Aktivierung + NVDA-Preload) ist damit
verifiziert.

RELEASE v4.74 VEROEFFENTLICHT (16.07. ~22:45): Commits c864ae9
(Code) + 1ddd395 (repo.json) gepusht; GitHub-Release v4.74 mit
latest.zip / FF14Accessibility-v4.74.0.zip / neuer Installer-EXE
(mit Aktivierungs-Fix). latest-Link verifiziert (HTTP 200, 508508
Bytes = 4.74-Zip). uia_test.ps1 (UIA-Testskript, Repo-Wurzel)
absichtlich uncommittet gelassen.

### Neu in V4.72: Laden-Fix (User-Test 18:21 zeigte das Problem)
V4.71-Testauswertung aus dem Log:
- Item-Slot-Ansage BESTAETIGT: Charakterfenster 18:14:27 sprach
  "Leder-Grimoire, Stufe 5, tragbar" (Job 26 -> Spalte ACN korrekt).
- Laden: Addon heisst wirklich "Shop" (Erkennung lief, Namens-Cache
  wurde gebaut), aber KEIN Treffer beim Namens-Match. ROOT CAUSE:
  Shop-Zeilen-Fokus-Text = '226, <0x02-Payload>Laien-Hanfbundhaube
  <0x03>' - der Item-Name steckt in SeString-Payload-Huellen, der
  Match lief auf dem ROHEN Text. FIX: AppendShopGearInfo sanitized
  jede Zeilen-Teil vor dem Match (TolkService.Sanitize jetzt public).

### Strg+F7 BESTAETIGT (User + Log 18:21:58, noch in der V4.71-Session)
Setup Job 26 -> EquipRecommendedGear -> "4 Plaetze geaendert". Der
spieleigene Optimierer laeuft sauber ueber unsere Taste.

### Gehhilfe BESTAETIGT (User 16.07. abends)
Manuelles Laufen mit Wegpunkt-Routing (Strg+Numpad3, V4.63-4.65)
funktioniert in der Praxis. OFFEN (User-Wunsch, bei Gelegenheit):
Sounds austauschen - aktuell Sinus-Piepser (Beacon 880Hz-Familie,
Wegpunkt-Cue 1568Hz, Ankunft 1320->990Hz). Vor Umsetzung fragen,
welche Sounds stoeren und was stattdessen gewuenscht ist (andere
Toene oder echte Sound-Dateien).

### V4.72 Laden BESTAETIGT (User + Log 18:46)
Beide Faelle sauber: "Laien-Hanfbundhaube: Stufe 10, tragbar" /
"Messingbrille: Stufe 13, nicht tragbar, ab Stufe 13". Der
Payload-Sanitize-Fix war der fehlende Baustein. Ausruestungs-Block
(User-Auftrag Stufe+Tragbarkeit + Bestes anlegen) damit KOMPLETT
bestaetigt bis auf:

### Neu in V4.73 (gebaut + deployed, ungetestet): Braillezeile
User-Wunsch: alles Gesprochene auch auf der Braillezeile. Fix:
TolkService ruft jetzt Tolk_Output (Sprache UND Braille, laut
Tolk-API die empfohlene Ausgabefunktion) statt Tolk_Speak (nur
Sprache). Tolk_Output war in TolkNative schon deklariert, nur
ungenutzt. Bei NVDA geht Braille ueber nvdaController_brailleMessage
(macht Tolk intern).

### RELEASE v4.73 veroeffentlicht (16.07. abends)
Commits 37d51da + 49acfb7 gepusht, GitHub-Release v4.73 mit
latest.zip / FF14Accessibility-v4.73.0.zip / Installer-EXE.
latest-Download-Link verifiziert (HTTP 200). repo.json auf 4.73.

### Noch offen zu testen
1. Braillezeile (V4.73): zeigt jede Ansage an? Startansage
   "Version 4 Punkt 73 bereit" muesste schon auf der Zeile stehen.
2. Strg+F6: jedes getragene Teil mit "Stufe X"?
3. Einstellungen (V4.70/71-Fix): Reiter + Enter -> "Ueberschrift,
   Tab X von 8"? (Rest der Optionen-Baustelle ist eh zurueckgestellt)

---

## STAND V4.71 (2026-07-16, Item-Slots BESTAETIGT, Laden-Match kaputt - Fix in V4.72)

### Neu in V4.71: Ausruestung - Stufe + Tragbarkeit (User-Auftrag)
User will: im Laden UND am Koerper hoeren, welche Stufe ein
Ausruestungsteil hat und ob er es tragen kann; plus "Bestes anlegen".
Letzteres EXISTIERT schon: Strg+F7 (V4.66, noch nie getestet) ruft die
spieleigene "Empfohlene Ausruestung" auf.

Neu: GearInfoService.cs - liest NUR Spiel-Datenblaetter (ilspycmd-
verifiziert): Item.LevelEquip (noetige Stufe), Item.ClassJobCategory
(bool-Spalte je Job, Spaltenwahl ueber ENGLISCHE ClassJob-Abkuerzung,
kein Spalten-Reihenfolge-Raten), Item.EquipRestriction (Volk/Geschlecht),
Spielerseite aus PlayerState (CurrentLevel/CurrentClassJobId/Race/Sex).
Unbekannte Spalte/Werte -> nur "Stufe X", NIE geratenes Urteil (Log
[Gear] zeigt die Luecke). Die native InventoryManager.CanEquip existiert,
braucht aber einen rohen itemRow-Zeiger -> Crash-Risiko, nicht benutzt.

Eingebaut an 3 Stellen:
1. Item-Slot-Navigation (Inventar/Charakterfenster/Arsenal): Ansage
   jetzt "Bronzegladius, Stufe 5, tragbar" bzw. "..., nicht tragbar,
   ab Stufe 26" / "nur fuer Gladiator" / "nicht fuer dein Volk".
   Icon-Aufloesung kennt jetzt auch getragene + Arsenal-Items
   (EquippedItems + Armory*-Container in der Icon-Map).
2. Strg+F6 (Ausruestung vorlesen): pro Teil ", Stufe X" angehaengt;
   "tragbar" wird dort nur bei PROBLEM gesprochen (nicht 12x "tragbar").
3. Laden-Listen: waehrend ein Shop-Fenster offen ist, wird an gesprochene
   Zeilen die Gear-Info angehaengt (Namens-Match gegen Ausruestungs-
   Namen). ACHTUNG: Shop-Addon-Namen (Shop, ShopExchangeItem,
   ShopExchangeCurrency, InclusionShop) sind UNVERIFIZIERT (Community-
   Wissen) - wenn im Laden nichts angehaengt wird, nennt die
   "[Accessibility] Addon:"-Logzeile den echten Fensternamen.

Nebenbei-Fund (CS0649-Warnung): V4.70 hat _csExpectedTabIdx/
_csTabActivatedAt deklariert, aber NIE gesetzt -> "Tab X von 8"-Ansage
nach Enter und der 1,5s-Fallback ("Reiter gedrueckt, aber kein
Seitenwechsel") waren tote Pfade. Jetzt verdrahtet: Enter-Dispatch merkt
sich den gedrueckten Reiter (NodeId -> _csTabs-Index).

### Beim naechsten Start testen (V4.71)
1. "Version 4 Punkt 71 bereit".
2. Charakterfenster/Inventar: Slot mit Ruestung fokussieren ->
   "Name, Stufe X, tragbar"? Leere Slots weiter "Leer"?
3. Strg+F6: jedes Teil mit "Stufe X"?
4. Laden (Haendler) oeffnen, durch Waren gehen: haengt "Stufe X,
   tragbar/nicht tragbar" hinten dran? Falls stumm: Log an Claude
   (Addon-Name pruefen).
5. Strg+F7 (aeltester offener Test): legt empfohlene Ausruestung an,
   Ansage "X Teile gewechselt"?
6. Einstellungen (V4.70-Fix): Reiter fokussieren, Enter -> jetzt
   "Ueberschrift, Tab X von 8"? Bei totem Reiter nach 1,5 s ehrliche
   Meldung?
7. Log-Kontrolle danach: [Gear]-Zeilen (Job-Spalten-Zuordnung,
   Laden-Treffer).

---

## STAND V4.70 (2026-07-16 gebaut, Reiter-Merken war UNVERDRAHTET - in V4.71 gefixt)

### Neu in V4.70: Enter aktiviert Einstellungs-Reiter
V4.69 BESTÄTIGT (Log 16:25): Slider ("Regler ... von X bis Y"),
Auswahllisten ("Bildschirmmodus, Auswahlliste, NVIDIA...") und Reiter
("Reiter 1-8 von 8") werden beim Fokussieren angesagt - der
Enthaltensein-Ansatz (FindTopLevelOwner) war richtig.
USER-MELDUNG: Reiter fokussierbar, aber "wenn ich drücke passiert
nichts" - der Seitenwechsel fand nie statt (keine Tab-Wechsel-Ansage im
Log). Fokus allein aktiviert die DragDrop-Reiter nicht.
FIX: TryActivateFocusedConfigTab in HandleConfirmKey (Enter): dispatcht
das registrierte Klick-Event des fokussierten Reiters an seinen Listener
- gleicher Mechanismus wie das bewährte PressFocusedOk (Enter=Ok der
Charaktererstellung). Kandidaten-Reihenfolge DragDropClick(58) >
MouseClick(9) > ButtonClick(25); ALLE registrierten Event-Typen des
Reiter-Nodes werden geloggt ([CS] Reiter-Aktivierung), damit ein
falscher Kandidat sofort im Log erkennbar ist. Nach dem Wechsel sagt der
vorhandene Tab-Wechsel-Detektor die neue Seiten-Überschrift an.

### Beim nächsten Start testen (V4.70)
1. "Version 4 Punkt 70 bereit".
2. Systemkonfiguration: Reiter fokussieren ("Reiter 3 von 8"), ENTER:
   wechselt die Seite ("<Überschrift>, Tab 3 von 8" wird angesagt)?
3. Falls nicht: Log-Zeile "[CS] Reiter-Aktivierung: ... Events=[...]"
   an Claude - sie zeigt, welche Events der Reiter wirklich registriert.
4. Rest von V4.69: Slider-Werte beim Schieben, Auswahllisten.

---

## STAND V4.69 (2026-07-16 gebaut, BESTÄTIGT)

### Neu in V4.69: Einstellungen-Fix Nummer 2 (V4.68 blieb stumm)
V4.68-Test (Log 16:14): RadioButtons/Buttons sprachen (generischer Leser),
Slider/Auswahllisten weiter stumm, KEINE "[CS] Fokus (global)"-Zeile, keine
Exception. BEFUND: Der V4.68-Ansatz kletterte per ParentNode vom Fokus-Node
zur Fenster-Wurzel - die Eltern-Kette Komponenten-INTERNER Nodes erreicht
die Wurzel aber nicht zuverlässig (Handler stieg still am Wurzel-Check aus).
FIX: Zuordnung umgedreht - FindTopLevelOwner durchsucht die Top-Level-
Komponenten des Fensters danach, WELCHE den Fokus-Node ENTHÄLT (rekursiv
bis Tiefe 3, Dropdown-Fokus sitzt in der eingebetteten Checkbox-Komponente).
Owner-Komponente wird pro Fokus-Wechsel EINMAL gesucht und gecacht
(Wert-Verfolgung nutzt den Cache). Zusätzlich Diagnose-Zeile wenn der
Fokus-Node keinem Top-Level-Control zugeordnet werden kann.

### Beim nächsten Start testen (V4.69) - wie V4.68-Plan
1. "Version 4 Punkt 69 bereit".
2. Systemkonfiguration, Pfeiltasten: Slider "Label, Regler, Wert, von X
   bis Y"? Auswahllisten "Label, Auswahlliste, Eintrag"?
3. Regler links/rechts: neuer Wert gesprochen?
4. Reiter fokussieren: "Reiter X von 8"?
5. Falls wieder stumm: Log hat jetzt die Zeile "[CS] Fokus (global): Node
   ... gehört keinem Top-Level-Control" - dann weiß ich, wo es klemmt.

---

## STAND V4.68 (2026-07-16 gebaut, WIDERLEGT - siehe V4.69)

### Neu in V4.68: Einstellungen - Slider/Auswahllisten/Reiter sprechen
User-Meldung + Log 15:52 + frischer ConfigSystem-Dump: Pfeiltasten-Fokus
wanderte zwischen zwei Slidern ("Transparenz"/"Größe", Seite
Farbwahrnehmung), beide OHNE Text -> Stille. ROOT CAUSE doppelt:
(1) Slider/DropDownList/Reiter tragen keinen Text-Node (Probe [CS-OPT]
    zeigt ""), das Label steht als EIGENER Top-Level-Text DIREKT VOR dem
    Control in der Node-Liste (Dump: "Transparenz" vor Slider id=570);
(2) FindFocusedText sucht das Fokus-BIT an den Nodes, die Tastatur bewegt
    aber den globalen AtkInputManager.FocusedNode (V4.35-Erkenntnis).
FIX (UIReaderService, AnnounceConfigGlobalFocus, laeuft im ConfigSystem-
PostUpdate): globalen FocusedNode zum Top-Level-Ancestor klettern;
Slider -> "{Label}, Regler, {Wert}, von {Min} bis {Max}" (Felder
Value/MinValue/MaxValue ilspycmd-verifiziert); DropDownList ->
"{Label}, Auswahlliste, {gewaehlter Eintrag}" (List->SelectedItemIndex);
Kategorie-Reiter (DragDrop id 7-14) -> "Reiter X von 8". Bleibt der Fokus
auf dem Control, werden nur WERT-Aenderungen gesprochen (Slider-Schieben,
Dropdown-Auswahl). Label = naechster sichtbarer Top-Level-Text VOR dem
Control (volatile Texte wie fps uebersprungen). Controls MIT Text
(CheckBox/RadioButton/Buttons) sagt weiterhin der generische Fokus-Leser
an - keine Doppel-Ansage.
OFFEN: Charakterkonfiguration (ConfigCharacter) vermutlich gleiches
Layout, aber eigener Addon-Name - nach CS-Test pruefen/nachziehen.

### Beim naechsten Start testen (V4.68)
1. "Version 4 Punkt 68 bereit".
2. Systemkonfiguration oeffnen, mit Pfeiltasten durch die Controls:
   Slider sagen "Label, Regler, Wert"? Auswahllisten "Label, Auswahlliste,
   Eintrag"? Checkboxen weiter wie bisher?
3. Auf einem Regler links/rechts druecken: wird der neue Wert gesprochen?
4. Reiter fokussieren: "Reiter X von 8"? Reiter aktivieren: Seiten-
   Ueberschrift wie gehabt?
5. V4.67-Punkte falls offen: "Leer" in der Tasche, Arsenal-Kategorien,
   Strg+F6/F7.

---

## STAND V4.67 (2026-07-16 gebaut)

### Neu in V4.67: Inventar/Arsenal Stufe 2 (aus den User-Dumps vom 16.07.)
DUMP-AUSWERTUNG (InventoryGrid 38 Nodes, Currency 111, ArmouryBoard 125;
Bäume stehen komplett im dalamud.log, die Desktop-Datei wird pro Strg+F5
ÜBERSCHRIEBEN - nur ArmouryBoard liegt noch auf Platte):
- Item-Slots = DragDrop(17) mit Icon-Kind; GEFÜLLTE Slots wurden schon
  gesprochen (Log 14:28: "7 mal Heiltrank", "Hanfbundhaube des Eifers" -
  der generische Item-Slot-Leser im Fokus-Pfad greift).
- LÜCKE 1: LEERE Slots (IconId=0) blieben stumm - Cursor-Bewegung nicht
  von Stillstand unterscheidbar. FIX: Icon-/DragDrop-Komponente mit
  IconId=0 sagt jetzt "Leer" (nur echte Slot-Typen; Icon-dekorierte
  Controls wie ConfigSystem-Tabs haben echte IconIds bzw. matchen nur
  über den Wrapper-Zweig und bleiben stumm).
- LÜCKE 2: Arsenal-Kategorie-Reiter sind reine Icons ohne Text. FIX:
  neuer OnArmouryBoardUpdate-Handler liest den Kategorie-Titel (Text-Node
  id=121, Dump-verifiziert) - beim Öffnen angehängt ("Kategorie Kopf"),
  bei Reiter-Wechsel per Interrupt.
- Charakterfenster-Dump fehlt noch (User erwischte stattdessen Currency);
  Ausrüstungs-Slots dort vermutlich gleiche Icon-Slot-Struktur - der
  "Leer"-Fix gilt generisch, Test zeigt ob mehr nötig ist.
V4.66-BEFUND aus demselben Log: Spiel hat ein eigenes Fenster "AUSRÜSTUNG
OPTIMIEREN" (Addon RecommendEquip, Knöpfe Anlegen/Abbrechen wurden vom
Fokus-Leser gesprochen) - User hat den Spielweg benutzt; Strg+F6/F7
(EquipmentService) noch UNGETESTET.

### Beim nächsten Start testen (V4.67)
1. "Version 4 Punkt 67 bereit".
2. Tasche öffnen, mit Numpad-Pfeilen über LEERE Felder: sagt er "Leer"?
3. Arsenal öffnen: "Kategorie Kopf" (o.ä.) nach dem Fensternamen? Reiter
   wechseln: neue Kategorie sofort angesagt?
4. Strg+F6 (Ausrüstung vorlesen) und Strg+F7 (empfohlene anlegen) - beides
   noch ungetestet aus V4.66.
5. Charakterfenster öffnen + Strg+F5 (Dump fehlt noch; Fenster heißt im
   Hauptmenü "Charakter").

---

## STAND V4.66 (2026-07-16 gebaut)

### Neu in V4.66: Ausrüstung (Stufe 1 - datenbasiert, ohne UI)
User-Wunsch: Inventar-/Rüstungs-/Arsenal-MENÜS barrierefrei + "optimale
Rüstung anziehen". Stufe 1 jetzt, komplett ohne UI-Scraping:
(1) NEU EquipmentService.cs. Strg+F6 = angelegte Ausrüstung vorlesen
    ("Waffe: Bronzegladius. Kopf: ... X Plätze frei."). Quelle:
    IGameInventory EquippedItems-Container; Slot-NAMEN kommen aus der
    EquipSlotCategory-Zeile des jeweiligen Items (Sheet-Spalten ilspycmd-
    verifiziert) - keine Slot-Index-Raterei.
(2) Strg+F7 = EMPFOHLENE AUSRÜSTUNG ANLEGEN. Nutzt den SPIEL-EIGENEN
    Optimierer (gleicher Code wie der Knopf im Charakterfenster):
    UIModule.GetRecommendEquipModule -> SetupForClassJob(CurrentClassJobId)
    -> IsUpdating abwarten (Timeout 3 s) -> EquipRecommendedGear. Alles
    ilspycmd-verifiziert (game-api.md-würdig). Ergebnis-Ansage über
    Vorher/Nachher-Vergleich der EquippedItems: "X Teile gewechselt" bzw.
    "unverändert (schon optimal oder gerade nicht möglich)" - ehrliches
    Feedback statt blindem Erfolgs-Claim (Kampf-Sperre etc.).
STUFE 2 OFFEN (braucht F5-Dumps vom User): Navigation IN den Fenstern
Inventar/Charakter/Arsenal (Cursor über Item-Slots sprechen - Slots haben
keine Text-Nodes, Zuordnung Fokus-Slot -> Container-Index nötig, vgl.
V4.46 InventoryEventGrid). Dump-Wunschliste: Tasche offen + Strg+F5,
Charakterfenster offen + Strg+F5, Arsenal offen + Strg+F5.

### Beim nächsten Start testen (V4.66)
1. "Version 4 Punkt 66 bereit".
2. Strg+F6: Ausrüstung wird mit Slot-Namen vorgelesen?
3. Strg+F7: "Lege empfohlene Ausrüstung an" -> Ergebnis-Ansage? Bei
   schlechterem Zeug in der Tasche: wird wirklich gewechselt (Strg+F6
   danach zeigt neue Teile)?
4. V4.65-Beacon: Ton leise bei fernem Ziel, lauter Richtung Ankunft?
5. Für Stufe 2: Tasche/Charakter/Arsenal einzeln öffnen, jeweils Strg+F5
   (Dump), Log/Desktop-Dateien an Claude.

---

## STAND V4.65 (2026-07-16 gebaut)

### Neu in V4.65: Beacon-Lautstärke = Ziel-Distanz (User-Feedback aus V4.64-Test)
V4.64 BESTÄTIGT (Log 10:33): Gehhilfe auf Strg+Numpad3 startet ("Gehhilfe an:
Der einsame Leuchtturmwärter", 343 m, 15 Wegpunkte), Vorschau gesprochen,
Wegpunkt-Erreichen + Skip-Ahead funktionieren (Sprung zu 3/15), Richtung ok.
USER-FEEDBACK: Ton soll leiser sein, je weiter Wegpunkt/Ziel weg ist. Befund:
Lautstärke hing am NÄCHSTEN WEGPUNKT (immer nah → dauernd laut, obwohl das
Ziel 280 m entfernt war). FIX: Signale getrennt — Tonhöhe/Stereo steuern
weiter zum Wegpunkt, die LAUTSTÄRKE folgt jetzt der Rest-Distanz zum ZIEL
(leise = weit, schwillt bis zur Ankunft an). Kurve gestreckt: voll ≤5 m,
linear bis 20 % ab 200 m (vorher 80 m — zu kurz für Quest-Distanzen);
20-%-Boden hält den Ton hörbar. BeaconService.Update + Aufrufer, V4.65.

### Beim nächsten Start testen (V4.65)
1. "Version 4 Punkt 65 bereit".
2. Gehhilfe zu fernem Ziel (Strg+Numpad3): Ton startet LEISE und wird auf
   dem Weg zum Ziel stetig lauter? An Ecken ändert sich nur Tonhöhe/Seite,
   nicht die Lautstärke?
3. Restpunkte aus V4.63/64: Ecken-Führung um Hindernis, Wegpunkt-Ton +
   Ankunfts-Doppelton, Questziel-Führung, Kompass-Check, Auto-Lauf-Vorschau.

---

## STAND V4.64 (2026-07-16 gebaut, in-game bestätigt)

### Neu in V4.64: Gehhilfe-Taste repariert (Windows-NumLock-Umschalt-Falle)
User-Test V4.63 (Log 2026-07-16 08:59): Umschalt+Numpad3 löste NICHTS aus —
kein einziger [Nav]-Gehhilfe-Eintrag, während Strg+Numpad3 (Vorschau) sofort
funktionierte ("Weg zu Verwirrter Fuhrmann, 10 Meter: 10 Meter nach Westen").
ROOT CAUSE: Windows-Tastaturtreiber-Eigenheit — bei aktivem NumLock wird
Umschalt+Numpad-Ziffer in die NAVIGATIONS-Taste umgewandelt (Numpad3 →
Bild-ab, Umschalt künstlich losgelassen). Das Plugin sieht nie VK Numpad3;
Bild-ab ist im Spiel obendrein CAMERA_ZOOMOUT. Die Gehhilfe war damit seit
dem V4.61-Tastenumzug NIE auslösbar. Dokumentiert in game-api.md →
"Safe Mod Keys" (Numpad-Ziffern nie mit Umschalt, nur mit Strg).
FIX: Gehhilfe = Strg+Numpad3 (nachweislich ankommend, neben Auto-Lauf
Numpad3), Routen-Vorschau = Strg+Numpad5 (Numpad5 hat die tastbare
Erhebung; bare Numpad5=CAMERA_FOCUS, Strg+Numpad5 laut Dump frei).
Config-Migration V5→6 (Vorschau ZUERST von Strg+Numpad3 wegziehen, dann
Gehhilfe drauf). Hilfe-Text (Strg+F1) aktualisiert.
Version 4.64, Build 0 Fehler/0 Warnungen, Deploy bestätigt.

### Beim nächsten Start testen (V4.64)
1. "Version 4 Punkt 64 bereit".
2. GEHHILFE: Ziel mit N wählen, dann STRG+Numpad3 — kommt jetzt
   "Gehhilfe an: <Name>" + Beacon + Routen-Vorschau?
3. VORSCHAU: STRG+Numpad5 — "Weg zu <Name>, X Meter ..."?
4. Restliche Testpunkte aus V4.63 unten (Ecken-Führung, Questziel-Führung,
   Abkürzen/Re-Route, Kompass-Check, Auto-Lauf-Vorschau).

---

## STAND V4.63 (2026-07-16 gebaut, Wegpunkt-Routing)

### Neu in V4.63: Wegpunkt-Routing (Routen-Vorschau + pfadbasierte Gehhilfe)
Auftrag: Offener Verdent-Wunsch vom 15.07. — "ansagen über welche Wegpunkte
man den gewünschten Punkt erreicht" und "manuell über mehrere Wegpunkte,
um Hindernisse herum". Umsetzung nach dem extern gelieferten Ratgeber
docs-de/ideen/ff14-route-guidance-guide.md (KOTOR-Mod-Team) plus Verdents
docs/manuelle-navigation-konzept.md.

WICHTIGE KORREKTUR am Konzeptdokument: vnavmesh `Nav.Pathfind` gibt
`Task<List<Vector3>>` zurück, NICHT `List<Vector3>` (ilspycmd an der
installierten DLL, 2026-07-16: QueryPathBasic ist async). Der Task wird
pro Frame gepollt, nie blockiert. Dokumentiert in game-api.md.

NEUE TEILE:
(1) RouteService.cs (neu): Nav.Pathfind-IPC (reine Abfrage, KEINE
    Auto-Bewegung) + Segment-Builder für die Sprach-Vorschau: Wegpunkt-
    Hops werden in 8 Kompass-Sektoren gefaltet (gleiche Richtung =
    zusammengelegt, Hops unter 1 m wandern in den nächsten Abschnitt =
    Mesh-Zittern raus), max. 4 gesprochene Segmente, danach "dann weiter".
    Ansage: "Weg zu Ätheryt, 62 Meter: 25 Meter nach Norden, dann 30
    Meter nach Nordosten, dann weiter."
    Kompass-Konvention Norden=−Z/Osten=+X hergeleitet aus der verifizierten
    Pixel→Welt-Formel + genordeter Spielkarte (game-api.md, neue Sektion);
    jede Vorschau loggt Segment 1 samt Rohvektor als Prüfpunkt.
(2) ROUTEN-VORSCHAU auf Strg+Numpad3 (neben Numpad3=Auto-Lauf und
    Umschalt+Numpad3=Gehhilfe; Numpad3-Kombis laut Keybind-Dump frei,
    kein NVDA-Konflikt bei NumLock an): sagt den Weg zum gewählten Ziel
    an OHNE zu laufen — Questmarker/Wegpunkt aus dem Objekt-Browser oder
    aktuelles Spielziel. Bei "kein Weg": Aethernet-Tipp wie beim Auto-Lauf
    (BuildNoPathHint ist dafür von AutoWalkService nach PlacesService
    umgezogen, ein gemeinsamer Code-Pfad).
(3) GEHHILFE PFADBASIERT (Kern-Upgrade): Beacon + Richtungsansagen
    verfolgen den NÄCHSTEN Wegpunkt der vnavmesh-Route statt der Luftlinie
    — um eine Ecke zeigt der Ton auf die Ecke statt in die Wand.
    Beim Start: Kompass-Vorschau der Route. Danach ereignisgesteuert:
    Wegpunkt erreicht (3-m-Radius) → kurzer hoher Ton (CueService, 1568 Hz,
    mittig) + EINE Ansage zum nächsten Abschnitt relativ zur Blickrichtung
    ("15 Meter, leicht links", bei >1,5 m Höhenunterschied "aufwärts"/
    "abwärts"); dazwischen nur alle 5 s eine Wiederhol-Ansage (vorher
    stur alle 2 s dasselbe). Skip-Ahead: wer die Ecke schneidet oder schon
    nahe einem SPÄTEREN Wegpunkt ist, wird nicht zurückgeschickt (Cursor
    springt still weiter). Drift-Re-Route: >10 m neben der Route (oder
    Ziel-NPC >10 m weitergelaufen) → stilles Neu-Berechnen, Ansage nur
    wenn sich die Richtung dadurch ändert ("Neuer Weg: rechts.").
    Ankunft: fallender Doppelton + "Ziel erreicht" wie bisher.
    Ohne vnavmesh/ohne Route: alte Luftlinien-Führung als Fallback
    ("Kein Wegenetz, führe in Luftlinie."), Config-Schalter
    WalkGuideRouteMode=false erzwingt sie. Neue Config RouteCueVolume.
(4) GEHHILFE KANN JETZT AUCH MARKER-ZIELE: Quest-Ziele, Wegpunkte,
    Ätheryten aus dem Objekt-Browser (vorher nur echte Spielobjekte;
    Fremdzonen-Quests führen zum Übergang, wie beim Auto-Lauf). Die
    Ziel-Auflösung (Zonen-Check, Übergangs-Routing, Navmesh-Höhe) ist
    dafür in Plugin.cs zu TryResolveMarkerDestination zusammengezogen —
    Auto-Lauf, Gehhilfe und Vorschau nutzen denselben Code.
(5) AUTO-LAUF sagt beim Start jetzt einmal die Routen-Vorschau an
    (liest die schon vorhandene Path.ListWaypoints-Diagnose); der
    3-s-Fortschritts-Timer startet 5 s später, damit er die Vorschau
    nicht abschneidet.
Version 4.63 (csproj + Plugin.cs synchron), Build 0 Fehler/0 Warnungen,
Deploy nach devPlugins bestätigt. Commit steht noch aus (erst testen).

### Beim nächsten Start testen (V4.63)
1. "Version 4 Punkt 63 bereit".
2. VORSCHAU: Mit N ein Ziel wählen (z.B. NPC ~50 m weg), Strg+Numpad3:
   kommt "Weg zu <Name>, X Meter: ... nach <Himmelsrichtung> ..."?
   KOMPASS-CHECK: bei einem Ziel mit bekannter Richtung prüfen ob die
   Himmelsrichtung stimmt (Log [Route] zeigt Segment 1 + Vektor —
   falls gespiegelt, bitte Log schicken).
3. GEHHILFE UM ECKE: Ziel hinter einer Ecke/Mauer wählen,
   Umschalt+Numpad3: Vorschau kommt? Beacon zeigt zur ECKE (nicht in
   die Wand)? An der Ecke: kurzer hoher Ton + neue Richtungsansage?
   Am Ziel: fallender Ton + "Ziel erreicht"?
4. GEHHILFE MIT QUESTZIEL: Kategorie Quest-Ziele (Strg+N), Ziel wählen,
   Umschalt+Numpad3 — führt die Gehhilfe? (Vorher ging das gar nicht.)
5. ABKÜRZEN: Beim geführten Laufen absichtlich neben dem Weg laufen
   (>10 m): sagt er nach ein paar Sekunden neue Richtung an bzw. führt
   ohne Streit weiter? Kein Ansage-Spam?
6. AUTO-LAUF: Numpad3 wie gewohnt — zusätzlich einmal die Weg-Vorschau
   nach "Laufe zu ..."? Fortschritt "Noch X Meter" kommt weiter?
7. OHNE ROUTE: Ziel auf getrennter Mesh-Insel (z.B. andere Stadt-Ebene):
   Gehhilfe sagt "Kein Weg gefunden, führe in Luftlinie" + Aethernet-Tipp?
8. V4.62-REST (falls noch nicht getestet): Sprint ohne Countdown-Spam,
   kein "+Sprint"/Kampfzahlen-Spam, Login unverändert.

---

## STAND V4.62 (2026-07-15 gebaut)

### Neu in V4.62: Ansage-Spam gefiltert (_StatusCustom0-Sprint-Countdown, _FlyText)
Auftrag: Top-5-Punkt 1 aus docs/verbesserungsvorschlaege.md umsetzen, aber NUR
den _StatusCustom0/_FlyText-Teil - Login-Geplapper (Zeile 101 unten) laut
User-Entscheid bewusst NICHT anfassen ("damit zufrieden").
BEFUND: Beide Quellen liefen NICHT ueber HudNoiseAddons (das ist eine
hartkodierte Konstante ohne Laufzeit-Schalter), sondern waren schlicht noch
gar nicht gefiltert - der generische Text-Scanner (ScanAddonTexts) und der
Fokus-Leser (FindFocusedText/OnAnyAddonReceive) liefen fuer beide Addons ganz
normal mit. _StatusCustom0 traegt laut Aufbau NUR den Sprint-Countdown als
Text-Node (Statuseffekt-NAMEN kommen nicht als Node-Text, nur per Tooltip) -
eine Vollsperre verliert also keine Information. _FlyText-Popups ("+Sprint",
"700", "(+100 %)") dupliziert nur, was CombatService (HP-Schwellen, Cast-
Ansagen) ohnehin schon sauber aufbereitet ansagt.
FIX (UIReaderService + Configuration):
(1) Zwei neue Configuration.cs-Schalter: SuppressStatusBarSpam (Default true)
    und SuppressFlyTextSpam (Default true) - im Zweifel konfigurierbar statt
    hart verdrahtet, wie vom User verlangt.
(2) Neue Helper-Methode IsSuppressedAddon(name), die HudNoiseAddons PLUS die
    beiden neuen Flags kombiniert; ersetzt HudNoiseAddons.Contains(name) an
    allen 3 Stellen (OnAnyAddonOpen, OnAnyAddonUpdate-Scanner, OnAnyAddonUpdate-
    Fokus) SOWIE zusaetzlich in OnAnyAddonReceive (dort lief bislang GAR KEIN
    HudNoiseAddons-Check - _StatusCustom0/_FlyText haetten also auch ueber den
    ReceiveEvent-Fokus-Pfad durchrutschen koennen).
(3) UIReaderService bekommt Configuration als neuen Konstruktor-Parameter
    (Plugin.cs entsprechend angepasst).
Wichtige Statuseffekte (neue Debuffs/Buffs) werden dadurch NICHT stummgeschaltet -
es wird weiterhin nichts an dieser Stelle je nach Statuseffekt-Namen gefiltert,
sondern das GESAMTE Addon (das ohnehin nur den Countdown zeigt); eine gezielte
Buff-/Debuff-Ansage nach Status-ID ist als eigener Vorschlag in
verbesserungsvorschlaege.md vermerkt (separates Vorhaben, nicht Teil dieses
Auftrags). Login-Geplapper (INVENTAR/SEITE AN SEITE/Menü-Text) unveraendert.
Version 4.62, Build 0 Fehler/0 Warnungen, Deploy nach devPlugins bestaetigt.

### Beim naechsten Start testen (V4.62)
1. "Version 4 Punkt 62 bereit".
2. SPRINT: Sprint/Dauerlauf aktivieren - KEIN Countdown-Sekundentakt ("20s"..
   "1s") mehr aus der Buff-Leiste, KEIN "+Sprint"/"700"/"(+100 %)" mehr aus
   FlyText?
3. LOGIN: unveraendert - weiterhin wie in V4.61 (Server-Warteschlange wird
   vorgelesen, kein "Beenden"-Spam, keine "Tastenbelegung gespeichert"-Ansage)?
4. Neue Statuseffekte/Debuffs im Kampf (z.B. Silence, Vulnerability-Stack):
   werden andere HUD-Ansagen (Kampf-HP, Cast-Ansagen) weiterhin normal gehoert?

---

## STAND V4.61 (2026-07-13 gebaut)

### Neu in V4.61: Auto-Lauf-Wächter repariert (kein Fehlabbruch bei Umwegen)
User-Meldung + Log 2026-07-13 01:08: "Komme nicht näher"-Meldung kam kurz nach
dem Start, obwohl der Charakter lief — vnavmesh nimmt Umwege, dabei steigt die
Luftlinie zum Ziel zeitweise. Zweiter Befund aus demselben Log: Der alte
Abbruch setzte nur `_active=false`, stoppte den vnavmesh-Pfad NICHT — deshalb
lief der Charakter nach der Meldung weiter (und kam sogar an).
FIX (AutoWalkService):
(1) Wächter misst jetzt die BEWEGUNG DES CHARAKTERS (Positionswechsel >=0,5 m
    setzt den 5s-Timer zurück), nicht mehr die Zieldistanz. Umwege = Bewegung =
    kein Abbruch. Echtes Verkeilen (Position friert ein, wie 26 m vor dem
    Übergang nach Unteres La Noscea, La Thagran-Grenzposten) wird weiter erkannt.
(2) Bei echtem Feststecken wird jetzt auch der vnavmesh-Pfad gestoppt
    (Stop(announce:false)), Ansage: "Ich stecke fest, noch X Meter."
(3) csproj-Version hing noch auf 4.59 (Plugin.cs sagte 4.60) — beides auf 4.61
    synchronisiert.

### Auch in V4.61: Tasten-Umzug (Strg+Alt+N war NVDA-Hotkey!)
User-Meldung: Strg+Alt+N (Kategorie zurück, V4.59) ist der Windows-Hotkey zum
NVDA-Start — unbenutzbar. Alt+N ist im Spiel CMD_BEGINNER (Neulings-Chat).
User-Entscheid (3 Optionen angeboten): TAUSCH —
- Kategorie zurück = Strg+Umschalt+N (Logik "Umschalt = rückwärts" wie N/Umschalt+N)
- Gehhilfe = Umschalt+Numpad3 (neben Auto-Lauf Numpad3; Numpad3-Kombis laut
  Keybind-Dump frei, Numpad 0-9 sonst alle vom Spiel belegt)
Config-Migration Version 4→5 (gezielt, nur wenn alte Defaults gesetzt; Gehhilfe
ZUERST umziehen, dann Kategorie-zurück — sonst greift der zweite Check auf den
frisch vergebenen Wert). Hilfe-Text (Strg+F1) aktualisiert.

### V4.60-Testergebnis (Log 2026-07-13 01:00) — BESTÄTIGT
- Bestiarium-Zeilen werden angesagt ("1 von 31, Hermetiker 01", "2 von 31,
  Marienkäfer, 0 von 3", "3 von 31, 75, Vergütung") ✓
- Strg+F4-Übersicht liest die Liste ✓ — ABER nur 20 von 31 Items (Rest fehlt,
  vermutlich eingeklappte/virtuelle Zeilen ohne Renderer)
- Rang-Namen-Frage GEKLÄRT: die "0/10, NEU"-Zeilen sind eine ZWEITE Liste
  (Rang-Auswahl 1–5). Textnodes: id=7 '0/10', id=6 Rang-NUMMER '1'..'5',
  id=5 unsichtbar 'NEU'; StringValues leer ("(kein Item)"). Es gibt dort
  KEINEN Klartext-Namen. Ansage derzeit "1, 0 von 10" — Aufwertung zu
  "Rang 1, 0 von 10, neu" möglich (User noch nicht gefragt).

### Auch in V4.61: Ätheryten zonenweit + Kein-Weg-Aethernet-Tipp (User-Wunsch)
User: (1) Ätheryten über 100 m hinaus sehen/hinlaufen; (2) bei "kein Weg
gefunden" über andere Wegpunkte routen. EINORDNUNG zu (2): "Kein Weg" =
getrennte Mesh-Inseln (Stadt-Ebenen, nur per Aufzug/Aethernet verbunden) —
Laufen über Zwischenpunkte kann die Lücke NIE schließen, spielgerechter Weg
ist das Aethernet. Umsetzung:
(1) Kategorie "Ätheryten" im Objekt-Browser jetzt SHEET-basiert (PlacesService/
    MapMarker DataType 3+4 = Ätheryt + Aethernet-Splitter, zonenweit statt
    ObjectTable ≤100m). CyclePlaceDestination(aetherytesOnly), Ansage
    "Kategorie Ätheryten: N im Gebiet", Numpad 3 läuft hin (Places-Pipeline,
    Y via Navmesh). ObjectKind.Aetheryte-Filter entfällt.
(2) AutoWalkService.BuildNoPathHint (PlacesService injiziert): Bei "Kein Weg
    zu X gefunden" wird der zielnächste Ätheryt/Splitter genannt, wenn er ≤100m
    (2D) am Ziel liegt: "Das Ziel liegt nahe Aethernet <Name>. Reise per
    Aethernet dorthin." + [Nav] Kein-Weg-Tipp-Log. Workflow für den User:
    Strg+N → Ätheryten → N → Numpad 3 → am Splitter interagieren (Spiel-Menü
    wird von der Listen-Navigation gelesen) → Ziel-Splitter wählen → weiter.

### Auch in V4.61: Bestiarium-Lebensraum + Monster-Tracking (User-Wunsch)
User: "kann man die Monster auch tracken bzw. ansagen in welche Gegend man muss?"
DATENLAGE (ilspycmd 2026-07-13, Lumina.Excel.dll): MonsterNoteTarget-Sheet hat
pro Monster BNpcName-Ref + PlaceNameZone[3] + PlaceNameLocation[3] (Zone +
Untergebiet, bis zu 3 Fundorte) — exakt die Lebensraum-Info der Sehenden-UI.
MonsterNote-Sheet: Name, Reward, 4× Target-Ref + 4× Count.
NEU: BestiaryService.cs — lazy Dictionary BNpcName.Singular (lowercase) →
Lebensraum-Text ("Zone, Untergebiet, oder Zone2, …").
(1) Bestiarium-Zeilen-Ansage + Strg+F4-Übersicht hängen bei Monster-Zeilen
    ". Lebt in <Lebensraum>" an. Monster-Erkennung: Zeile hat Fortschritts-
    Token ("X von Y") + Name trifft im Sheet (Rang-Zeilen/Vergütungen nicht).
    Sheet-Fehltreffer werden geloggt ("[Bestiary] Kein Lebensraum für …").
(2) TRACKING: UIReaderService.SelectedBestiaryMonster (fokussierte Monster-
    Zeile, nur solange MonsterNote sichtbar). Numpad 3 bei offenem Bestiarium:
    sucht nächsten LEBENDEN BattleNpc gleichen Namens (CurrentHp>0) in der
    ObjectTable → anvisieren (mit Read-back-Warnung) + Auto-Lauf hin;
    keiner in der Nähe → "Kein <Name> in der Nähe. Lebt in <Lebensraum>."
UNVERIFIZIERT (Log klärt es): UI-Monstername == BNpcName.Singular (Groß-/
Kleinschreibung egal, aber Wortlaut muss stimmen).

### Auch in V4.61: Login-/Lobby-Fixes (User-Feedback 2026-07-13)
(1) SelectOk-Dialog (Server-Warteschlange "hoher Andrang"): Text wurde
    gesprochen, aber 14 ms später vom Fokus-Leser ("Abbrechen") ABGESCHNITTEN
    → User hörte nur "Abbrechen". Fix: OnAnyAddonOpen setzt bei SelectOk
    dieselbe 1s-Dialog-Schutzsperre wie SelectYesno (_dialogOpenedAt).
(2) "Beenden"-Spam: _CharaSelectReturn (trägt nur den Beenden-Knopf) meldete
    bei jedem Lobby-Fenster-Neuaufbau ungefragt Fokus → in HudNoiseAddons.
    Gezielte Navigation dorthin sagt weiter der globale FocusedNode-Leser an.
(3) Auto-Keybind-Dump nach Login ist jetzt STUMM (DumpKeybinds announce:false,
    nur Log/Datei); gesprochen wird nur noch bei echtem Tasten-Konflikt oder
    manuellem /acc keys. "Tastenbelegung gespeichert…" bei jedem Login war Lärm.

### Spam-Quellen aus dem Log (Stand V4.61 - _StatusCustom0/_FlyText inzwischen in V4.62 gefixt s.o.)
- _StatusCustom0 (Buff-Leiste): Sprint-Countdown "20s".."1s" im Sekundentakt
  gesprochen → GEFIXT in V4.62 (SuppressStatusBarSpam, Default an)
- _FlyText: "+Sprint", "+Dauerlauf", "-Sprint", "700", "(+100 %)" gesprochen
  → GEFIXT in V4.62 (SuppressFlyTextSpam, Default an)
- Restliches Login-Geplapper (00:59: "INVENTAR", "SEITE AN SEITE", "Menü, 0
  Einträge", ".", "Ziel.") — User: "nicht so schlimm", bewusst zurückgestellt
  (bleibt weiterhin unangetastet, User-Entscheid 2026-07-15 bestätigt)
- V4.59-Test (Quest-Objective) steht WEITER AUS (keine [Quest]
  Objective-Zeilen im Log)

### Beim nächsten Start testen (V4.61)
1. "Version 4 Punkt 61 bereit".
2. AUTO-LAUF mit Umwegen (Numpad 3, längere Strecke): KEIN "Komme nicht
   näher"-Fehlabbruch mehr kurz nach Start?
3. An einer Verkeil-Stelle (z.B. Übergang Unteres La Noscea): kommt "Ich
   stecke fest, noch X Meter" und der Charakter hört auf zu laufen (Pfad
   wird jetzt wirklich gestoppt)?
4. TASTEN-UMZUG: Meldet der Start "Konflikt"-frei? Strg+Umschalt+N blättert
   die Kategorie RÜCKWÄRTS (nicht mehr Gehhilfe!), Umschalt+Numpad3 schaltet
   die Gehhilfe?
5. V4.59 nachholen: Quest-Ziele blättern (Strg+N) → Objective hinter dem
   Namen?
6. LOGIN: Bei der Server-Warteschlange ("hoher Andrang") wird jetzt der
   TEXT gesprochen (nicht mehr nur "Abbrechen")? Kein ungefragtes "Beenden"
   mehr in der Charakterauswahl? Keine "Tastenbelegung gespeichert"-Ansage?
7. BESTIARIUM: Monster-Zeile fokussieren → kommt "… Lebt in <Gebiet>"?
   Numpad 3 bei fokussiertem Monster: läuft hin (wenn eins in der Nähe)
   bzw. sagt "Kein X in der Nähe. Lebt in …"? Falls kein Lebensraum kommt:
   Log mitschicken ([Bestiary] Kein Lebensraum für … = Namens-Mismatch).
8. ÄTHERYTEN: Strg+N bis "Kategorie Ätheryten: N im Gebiet" → zählt sie
   die ganze Zone (auch weiter als 100 m)? N blättert mit Distanz/Richtung,
   Numpad 3 läuft hin?
9. KEIN-WEG-TIPP: An einer bekannten "kein Weg"-Stelle (z.B. anderes
   Stadt-Level in Limsa) → kommt "… Das Ziel liegt nahe Aethernet <Name>"?

---

## STAND V4.60 (2026-07-13 gebaut, BESTÄTIGT s.o.)

### Neu in V4.60: Bestiarium (Jagdtagebuch, "MonsterNote") barrierefrei
User-Wunsch: Bestiarium vorlesbar machen; User wählte "Beides" (UI-Mitlesen +
Übersichts-Taste). Dump 2026-07-12 (dalamud.log ~Zeile 456) analysiert.
STRUKTUR: MonsterNote = ein Addon mit einer TreeList (Comp CT=TreeList). Zeilen
sind ListItemRenderer in drei Templates: Rang-Überschriften (Comp 1015: Name +
Badge "Erledigt!"), Monster-Zeilen (Comp 1017: Fortschritt "0/3" + Name + Icons)
und Vergütungen (Comp 1018: Betrag + "Vergütung"). Datenmodell ilspycmd-verifiziert:
AtkComponentTreeList.Items (@432) = logische Zeilen in visueller Reihenfolge;
AtkComponentTreeListItem trägt StringValues (@24, Spiel-Anzeige-Strings) + Renderer
(@48). MonsterNoteManager (12× MonsterNoteRankInfo, RankData[10], Kill-Counts) +
Lumina MonsterNote-Sheet existieren (für spätere direkte Datenmodell-Lesung notiert).
ROOT CAUSE des Bugs (Log 21:34): Beim Navigieren bewegen sich die Listen-Indizes
(Hovered/Selected) NICHT (alle -1), aber der globale FocusedNode wandert. Der
generische Fokus-Leser las darum immer nur "0/10, NEU" (Fortschritt + Badge einer
Rang-Überschrift OHNE Rang-Namen) — User hörte bestätigt nur das.
FIX (UIReaderService):
(1) Dedizierter Handler OnMonsterNoteUpdate (PostUpdate "MonsterNote"): klettert
    vom FocusedNode hoch zum ListItemRenderer, ordnet ihn per Items→Renderer der
    logischen Zeile zu, liest deren StringValues (Fallback: sichtbare Text-Nodes
    des Renderers). FormatBestiaryRow stellt "0/3, Marienkäfer" → "Marienkäfer,
    0 von 3" um (Fortschritt ans Ende, ausgeschrieben). Dedup pro Renderer+Text.
    Ansage "X von Y, <Zeile>".
(2) UpdateGlobalFocus schweigt jetzt, solange MonsterNote sichtbar ist (kein
    "0/10, NEU"-Doppel mehr).
(3) ÜBERSICHTS-TASTE Strg+F4 (KeyBestiary, Keybind-Dump frei): AnnounceBestiary-
    Overview liest die ganze offene TreeList am Stück (alle Items in visueller
    Reihenfolge). Kein Sheet-Mapping — liest die vom Spiel gesetzten Strings.
(4) GROUND-TRUTH-PROBE [Bestiary]: pro Zeilenwechsel werden ALLE Text-Nodes der
    Zeile (sichtbar UND unsichtbar, mit id/vis) + die Roh-StringValues geloggt.
    Klärt, wo bei unfertigen Rängen ("0/10, NEU") der Rang-NAME steckt (der Dump
    zeigte nur fertige Ränge mit Namen). Danach ggf. Rang-Namen-Ansage nachziehen.

### Beim nächsten Start testen (V4.60)
1. "Version 4 Punkt 60 bereit".
2. BESTIARIUM öffnen, mit Pfeiltasten/Controller durch die Liste navigieren:
   werden jetzt Rang-Überschriften, Monster ("Marienkäfer, 0 von 3") und
   Vergütungen sauber angesagt — nicht mehr nur "0/10, NEU"?
3. Kommt bei den Rang-Überschriften ein NAME (z.B. "Hermetiker 01") oder weiter
   nur "0 von 10, NEU"? Die [Bestiary] Probe-Logzeilen mitschicken (zeigen alle
   Text-Nodes inkl. unsichtbar + Roh-Strings → daraus ziehe ich den Rang-Namen).
4. Strg+F4 → wird die ganze Liste am Stück vorgelesen? Stimmen Namen/Fortschritt?
   [Bestiary] Übersicht-Logzeile mitschicken.

---

## STAND V4.59 (2026-07-12, V4.59 gebaut)

### Neu in V4.59: Quest-Objective + Kategorie-zurück-Taste
User-Wünsche: (1) bei Quest-Zielen hinter dem Namen zeigen, was noch fehlt;
(2) Taste, um Objekt-Kategorien RÜCKWÄRTS zu blättern.
(1) OBJECTIVE: QuestManager liefert nur Sequenz-Zahlen, kein Klartext; der
Objective-Text liegt nur im laufenden Quest-Tracker (_ToDoList). QuestMarkerService.
GetQuestObjectives liest ihn via RaptureAtkUnitManager. Node-ID-Muster (aus Probe
19:59 verifiziert): Header 70000+slot = Quest-Name, Objective 20000+slot*100+idx.
Map Quest-Name → Objective wird in CycleQuestDestination angehängt: "1 von 3:
Story: Fast wie zu Hause, Baderon Bericht erstatten, 55 Meter, links." Nur
GETRACKTE Quests haben ein Objective (Tracker zeigt begrenzt viele); sonst wie
bisher. Jede Zuordnung als [Quest] Objective-Log.
(2) KATEGORIE ZURÜCK: neue Taste Strg+Alt+N (KeyCategoryPrev, laut Keybind-Dump
frei; bare N-Familie war voll). NextCategory/PreviousCategory teilen CycleCategory.

### Beim nächsten Start testen (V4.59)
1. "Version 4 Punkt 59 bereit".
2. OBJECTIVE: Quest-Ziele (Strg+N bis Quest-Ziele) durchblättern → steht jetzt
   hinter dem Quest-Namen das aktuelle Ziel ("… , Aurelias mit Hermetik erlegen
   0/3, …")? Stimmt die Zuordnung Quest→Objective? [Quest] Objective-Logzeilen
   mitschicken (zeigen slot/name/objective — verifiziert das ID-Muster).
3. KATEGORIE ZURÜCK: Strg+Alt+N → blättert die Kategorie rückwärts (Gegenprobe
   zu Strg+N vorwärts)?

---

## STAND V4.58 (2026-07-12, gebaut)

### V4.57 BESTÄTIGT (Log 2026-07-12, komplettes Limsa-Tutorial gespielt)
Story-Kennzeichnung funktioniert ("Story: Fast wie zu Hause", 899 Hauptszenario-
Namen geladen). Das Plugin trägt sauber durchs Tutorial: NPC-Dialoge, Auto-Lauf,
Quest-Annahme/Abschluss, Kräutersammeln. Log-Analyse ergab 5 Ansage-Ärgernisse
→ in V4.58 behoben.

### Neu in V4.58: Ansagen entrümpelt (5 Log-Befunde behoben)
Alle aus der Log-Auswertung 2026-07-12 (komplettes Tutorial), Ursachen am
Quellcode verifiziert.
1. **SeString-Payloads (GENERELL, TolkService.Sanitize):** Roher
   Utf8String.ToString() reicht FFXIV-Payload-Steuerbytes durch (START 0x02 …
   END 0x03). Belohnungs-Gil-Zelle kam als "H%I&GilIH", NPC-Dialoge trugen
   "\x02\x10\x01\x03"-Umbruch-Chunks. Byte-Analyse aus dem Log bestätigt die
   Delimiter. Sanitize verwirft jetzt ganze Payloads + verirrte C0-Steuerzeichen
   (nicht \t\n\r). Wirkt auf ALLE Ansagen.
2. **Quest-Fenster-Reiter (UIReaderService.BuildQuestText):** Beim Öffnen von
   JournalAccept/Result las der Text-Fallback in den ersten Frames alle Canvas-
   Texte = die Reiter "Zusammenfassung. Optionen. Vergütung bei Erfolg …" vor der
   Beschreibung. Statische Header-Blockliste (QuestPanelHeaders) filtert sie raus.
3. **Belohnungs-Zahlen (UIReaderService.UpdateGlobalFocus):** Navigiert man im
   JournalResult die Währungszellen, kamen nackte "400"/"103". Da die
   Zusammenfassung ("Belohnung: Erfahrung 400. Gil 103") schon beim Öffnen kommt,
   werden reine Zahlen bei sichtbarem JournalResult stumm gehalten (IsBareNumber
   + IsAddonVisible). Buttons/Item-Namen (nicht-numerisch) bleiben.
4. **Doppel-Meldungen (Tolk-Verlauf + ChatReaderService):** Toast-Notification
   (_TextError, INT) UND Chat-SystemMessage lasen dieselbe Meldung ~3 s versetzt
   ("Du hast einen Auftrag angenommen!" 2×). TolkService führt jetzt einen 10-s-
   Verlauf (Remember); ChatReaderService überspringt die Chat-Zeile, wenn der
   präfixlose Text in den letzten 6 s schon gesprochen wurde (WasRecentlySpoken).
5. **Hotbar-Keybinds (HotbarService.CleanUpHelp):** "Spielanleitung [9]",
   "Teleport [0]" — der Keybind-Hinweis in eckigen Klammern wird jetzt am Ende
   abgeschnitten.
6. **Cross-Zone-Quest-Dedup (NavigationService.GetQuestDestinations):** Mehrere
   Marker DERSELBEN Quest in einer Fremdzone lösten denselben langen Routing-Satz
   3× aus ("1 von 3 … 2 von 3 …", identisch). Cross-Zone-Marker werden auf einen
   Eintrag je (Quest, Zielkarte) reduziert (nächster überlebt); In-Zone-Marker
   bleiben einzeln.
7. **HP/MP-Abfrage auf Strg+H (User-Wunsch):** Die HP/MP-Ansage (AnnounceStatus:
   "HP X Prozent, MP Y Prozent", im Kampf + Ziel-HP) liegt jetzt auf **Strg+H**
   statt Strg+F12. Verifiziert am Live-Keybind-Dump: bare H = MENU_CRAFT (belegt),
   Modifier+H frei. Config-Migration Version 3→4 stellt gespeichertes "Strg+F12"
   → "Strg+H" um. Format ist weiterhin PROZENT (falls absolute Zahlen gewünscht:
   AnnounceStatus in CombatService anpassen).
8. **BUG-FIX Strg+L (Level) war tot:** KeyNameToVK kannte nur "N" als Buchstabe,
   NICHT "L" — das in V4.56 eingeführte Strg+L konnte nie geparst werden
   (IsJustPressed immer false, nur Log-Warnung). "H"=0x48 und "L"=0x4C zum
   Dictionary hinzugefügt → Strg+H UND Strg+L funktionieren jetzt.
9. **Auto-Lauf ohne Beacon-Piepen (User-Wunsch):** Der Richtungs-Beacon (Piep-Ton)
   lief beim Auto-Lauf (Numpad3) mit und war störend, da das Spiel ohnehin selbst
   steuert. Beacon komplett aus dem AutoWalkService entfernt (Feld/Parameter/alle
   Aufrufe); gesprochener Fortschritt bleibt. Der Beacon bleibt bei der MANUELLEN
   Gehhilfe (Strg+Umschalt+N) erhalten — dort steuert man selbst per Ton. Beide
   schließen sich weiter aus.

### Beim nächsten Start testen (V4.58)
1. "Version 4 Punkt 58 bereit".
2. QUEST ABSCHLIESSEN: Belohnungsfenster → kommt "Belohnung: Erfahrung X. Gil Y"
   OHNE anschließenden "H%I&GilIH"-Müll? Beim Durchnavigieren KEINE nackten
   Zahlen mehr (Buttons/Items schon)?
3. QUEST ANNEHMEN (JournalAccept): kommt gleich "Beschreibung: …" OHNE führendes
   "Zusammenfassung. Optionen. Vergütung …"?
4. NPC-DIALOGE: hört sich der Text sauber an (keine seltsamen Steuerzeichen/
   Zeichenketten mittendrin)?
5. AUFTRAG ANNEHMEN/ABSCHLIESSEN: kommt "Du hast einen Auftrag angenommen!" nur
   noch EINMAL (nicht zusätzlich als "System: …")?
6. HOTBAR Strg+F9: "Taste 9, Spielanleitung" OHNE "[9]" am Ende?
7. QUEST-ZIELE (Strg+N): mehrere Story-Marker in Fremdzone → wird die Quest nur
   noch EINMAL angesagt statt "1 von 3/2 von 3/3 von 3" identisch?
8. HP/MP: **Strg+H** → "HP X Prozent, MP Y Prozent"? (Strg+F12 tut jetzt nichts.)
9. STUFE: **Strg+L** → "Stufe X. Noch N Erfahrungspunkte…"? (War vorher tot,
   sollte jetzt gehen.)
10. AUTO-LAUF (Numpad3): läuft OHNE Piep-Ton, nur mit "Noch X Meter"? Manuelle
    Gehhilfe (Strg+Umschalt+N) piept weiterhin?

---

### Neu in V4.57: Story-Quests gekennzeichnet + NPC-Ansage umgestellt
User-Wünsche: (1) Hauptszenario-Quests als "Story" kennzeichnen; (2) bei NPCs
zuerst Beruf/Quest, dann Name.
(1) STORY: QuestMarkerService erkennt MSQ über Quest-Sheet (JournalGenre →
JournalCategory → JournalSection.RowId==0 = Hauptszenario, ilspycmd-verifiziert),
baut 1× ein Namens-HashSet, matcht Marker-Label. Quest-Ansage: "Story: <Quest>,
…" bei MSQ. IDataManager in QuestMarkerService injiziert. [Quest] Hauptszenario-
Namen-Log zeigt Anzahl.
(2) NPC-ANSAGE: DescribeNpc → NpcPrefix (liefert "Beruf, Quest, " als PRÄFIX).
Neu: "Ziel: Marktverwalter, Quest verfügbar, Miounne, NPC, 12 Meter, geradeaus."
statt Name zuerst. Gilt für Zielwechsel-Ansage UND Objekt-Browser (N).

### Beim nächsten Start testen (V4.57)
1. "Version 4 Punkt 57 bereit".
2. STORY: Quest-Ziele (Strg+N bis Quest-Ziele) durchblättern → werden MSQ-Quests
   mit "Story:" angesagt, Nebenquests ohne? [Quest] Hauptszenario-Namen-Zeile
   mitschicken (zeigt, ob Erkennung greift).
3. NPC: einen NPC anvisieren/mit N durchblättern → kommt Beruf/Quest ZUERST,
   dann der Name?

---

## STAND V4.56 (2026-07-12)

### Neu in V4.56: Level-Ansage auf Strg+L
User-Wunsch: Level-Ansage weg von Umschalt+F12. Bare "L" ist im Spiel belegt
(MENU_LINKSHELL, Keybind-Dump), Modifier+L aber frei → Strg+L (L=Level).
Config-Migration Version 2→3: bestehendes "Umschalt+F12" wird automatisch auf
"Strg+L" umgestellt (gezielt, andere Tastenanpassungen bleiben).
TASTEN-ÜBERSICHT aktuell: N/Umschalt+N/Strg+N/Strg+Umschalt+N = Objekt-Browser,
Numpad3 = Auto-Lauf, Strg+F1..F12 = Hilfe/Fenster/Inventar/Dump/Hotbar/Menü/
Stille/Kampf, Umschalt+F3 = Gil, Umschalt+F4/F5/F6 = Emote, Strg+L = Stufe/EXP.

### Beim nächsten Start testen (V4.56)
1. "Version 4 Punkt 56 bereit".
2. Strg+L → kommt "Stufe X. Noch N Erfahrungspunkte…"? (Umschalt+F12 tut jetzt nichts.)

---

## STAND V4.55 (2026-07-12)

### Neu in V4.55: Quest-Belohnung als verständliche Zusammenfassung
User: "bei der Questbelohnung nur noch Zahlen — ich will wissen WAS der Eintrag
ist." DIAGNOSE (UI-Dump JournalResult + [Focus]-Log): KEIN Bug der Stückzahl-
Funktion (benannte Items lösen weiter auf). Das Belohnungsfenster hat 2 Sorten
Einträge: ITEMs (Comp(1010) mit Icon → Name) und WÄHRUNG/EXP (Comp(1007), Betrag
in TextNineGrid, Typ nur als Bild → Fokus las nur "260"/"127"/"50"/"103").
FIX: UIReaderService.BuildRewardText liest beim Öffnen von JournalResult eine
Zusammenfassung: "Belohnung: <Item mal N>, Erfahrung X, Gil Y". Items per
Icon→Name, Beträge per Wert. WORKAROUND (dokumentiert + geloggt): Währungs-TYP
steht nur als UI-Bild ohne Icon-Id → Label per Position (Erfahrung zuerst, dann
Gil = Standard-FF14-Reihenfolge). [Quest] JournalResult Belohnung-Log zeigt
items/amounts zur Verifikation. Struktur in game-api.md → "JournalResult".
HINWEIS: Die nackten Zahlen bei Fokus-Navigation der Währungszellen bleiben
vorerst (die Zusammenfassung beim Öffnen deckt den Inhalt ab); bei Bedarf
Fokus-Zellen zusätzlich labeln.

### Beim nächsten Start testen (V4.55)
1. "Version 4 Punkt 55 bereit".
2. QUEST ABSCHLIESSEN: Belohnungsfenster öffnet → kommt "Belohnung: <Items>,
   Erfahrung X, Gil Y"? Stimmen Item-Namen + Beträge? Sind Erfahrung/Gil richtig
   herum (nicht vertauscht)? [Quest] JournalResult Belohnung-Logzeile mitschicken.

---

## STAND V4.54 (2026-07-12)

### Neu in V4.54: Emote-Browser (Verbeugen & Co. ohne Chat)
User braucht für eine Quest das Emote "Verbeugen", kann aber Chat nicht tippen
und fand das Gesten-Menü nicht. WICHTIG: "/verbeugen" existiert NICHT als
Textbefehl (User bestätigt) — deutscher /befehl ≠ Anzeigename. Lösung: EmoteService
löst Emotes direkt über die Spielfunktion aus (AgentEmote.ExecuteEmote,
ilspycmd-verifiziert), gefiltert auf freigeschaltete (CanUseEmote). Namen +
echter /befehl kommen aus dem Lumina-Emote-Sheet (nichts geraten).
TASTEN: Umschalt+F5 = nächstes Emote ansagen ("3 von 45: Verbeugen, Befehl /x"),
Umschalt+F4 = vorheriges, Umschalt+F6 = ausführen. Liste alphabetisch, lazy beim
ersten Druck gebaut (braucht eingeloggten Char + AgentEmote bereit).

### Beim nächsten Start testen (V4.54)
1. "Version 4 Punkt 54 bereit".
2. EMOTE: Umschalt+F5 mehrfach → werden Emote-Namen angesagt (inkl. Befehl)?
   Bis "Verbeugen" blättern → Umschalt+F6 → verbeugt sich der Charakter? Zählt
   die Quest es? [Emote]-Logzeilen mitschicken (id/name + echter /befehl).
3. Umschalt+F4 blättert rückwärts?

---

## STAND V4.53 (2026-07-12)

### Neu in V4.53: Stufe + EXP anzeigen + automatische Level-Up-Ansage
User-Wunsch: eigenes Level sehen, wie viel EXP bis Level-Up fehlt, und beim
Leveln die Meldung hören. Alles aus PlayerState (ilspycmd-verifiziert,
game-api.md → "Stufe / Erfahrung"), kein UI-Scraping.
(1) TASTE Umschalt+F12 (CombatService.AnnounceLevelExp): "Stufe X. Noch N
Erfahrungspunkte bis zur nächsten Stufe." (Maximalstufe → "Stufe X,
Maximalstufe erreicht."). CurrentLevel + ps->GetCurrentClassJobExp() /
GetCurrentClassJobNeededExp(); "noch" = needed − current.
(2) AUTOMATISCH: CombatService.TrackLevelUp (jeden Frame) sagt bei Stufenanstieg
"Stufe X erreicht." Nur bei Anstieg für DENSELBEN Job (Jobwechsel ändert
CurrentLevel auch → kein Fehlalarm; Baseline nach Login/Jobwechsel still gesetzt).
Config KeyLevelExp="Umschalt+F12" + Konflikt-Check.

### Beim nächsten Start testen (V4.53)
1. "Version 4 Punkt 53 bereit".
2. STUFE/EXP: Umschalt+F12 → "Stufe X. Noch N Erfahrungspunkte …"? Zahl plausibel?
3. LEVEL-UP: einen Gegner/eine Quest zum Stufenaufstieg → kommt automatisch
   "Stufe X erreicht."? [Level]-Logzeile mitschicken.

---

## STAND V4.52 (2026-07-12)

### V4.51 BESTÄTIGT (User + Log 10:56–11:07): Quest-Vorlesen funktioniert
Journal (JournalDetail) liest Ziel + Beschreibung sauber vor
("Ziel: Mit Chansteloup sprechen. Beschreibung: …"). JournalResult (abschließen)
feuert ebenfalls. ABER Log-Befunde:
- Node id=38 = "Bonus." (EXP-Bonus-Abzeichen), NICHT der Titel — hing als Rauschen
  vor jeder Quest ("Bonus. Ziel: …"). Über mehrere Quests konsistent.
- JournalResult liefert NUR "Bonus." — Belohnungen/Abschlusstext fehlen (Reward-
  Slots sind Icons, Narrativ evtl. in anderen Nodes). Struktur noch unbekannt.
- Mein Diagnose-Logging ([Quest] canvas textNode / probe:) feuerte NICHT im
  getesteten Build → in V4.52 robuster (Reset beim Schließen).
- JournalAccept tauchte im Log NICHT auf (Annehmen evtl. nicht getestet ODER
  anderer Fenstername) → beim nächsten Test [Accessibility] Addon:-Zeile prüfen.

### Neu in V4.52: "Bonus."-Rauschen raus + robustere Diagnose
(1) Node id=38 wird aus der Quest-Ansage entfernt (bleibt im Diagnose-Log
sichtbar). Journal liest jetzt "Ziel: … Beschreibung: …" ohne führendes "Bonus.".
Quest-NAME wird bewusst nicht wiederholt (kommt bei Listen-Auswahl).
(2) _questProbed wird beim Schließen zurückgesetzt → erneutes Öffnen loggt die
Canvas-Node-Struktur frisch. OFFEN: JournalResult (Abschluss) liest nach id=38-
Ausschluss evtl. gar nichts mehr (nur "Bonus." war da) → braucht eigenen Reward-
Reader, dafür 1 frisches [Quest]-Log nötig.

### Beim nächsten Start testen (V4.52)
1. "Version 4 Punkt 52 bereit".
2. JOURNAL (J), Quest auswählen → kommt "Ziel: … Beschreibung: …" OHNE "Bonus."?
3. QUEST ANNEHMEN bei einem NPC → wird die Beschreibung gelesen? Welcher
   Fenstername steht im Log ([Accessibility] Addon: …)?
4. QUEST ABSCHLIESSEN → was wird gelesen? Bitte die [Quest]-Zeilen (canvas
   textNode id=… / probe:) mitschicken — daraus baue ich den Belohnungs-Reader.

---

## STAND V4.51 (2026-07-12)

### Neu in V4.51: Quest-Fenster automatisch vorlesen
User-Wunsch: Questbeschreibung fehlt (alle 4 Situationen), erstmal AUTOMATISCH.
Dedizierter Handler OnQuestWindowUpdate (PostUpdate + Dedup pro Addon) für
JournalDetail (Journal, Taste J), JournalAccept (Quest annehmen), JournalResult
(Quest abschließen). Alle drei aus dem generischen Pfad genommen (HudNoiseAddons),
damit kein Doppel-Lesen. Timing: Text wird erst Frames nach Öffnen gesetzt +
ändert sich bei Seitenwechsel → PostUpdate liest bei jeder Änderung.
BuildQuestText verallgemeinert TryReadQuestDetail: findet die JournalCanvas-
Komponente; für JournalDetail strukturiert (verifizierte Node-IDs 38 Titel/
9 Stufe/8 Beschreibung/Ziel-Zeilen Multipurpose id=3), für Annehmen/Abschließen
(IDs NICHT verifiziert) Fallback = alle sichtbaren Canvas-Texte in Reihenfolge.
Strg+F10 (TryReadQuestDetail) nutzt jetzt dieselbe BuildQuestText-Basis.
DIAGNOSE: Jeder Canvas-Textknoten wird 1× als [Quest]-Zeile geloggt (id + Text);
Fenster OHNE Canvas werden per ProbeQuestStructure einmal strukturell geloggt
([Quest] … probe: node/comp/vis) → daraus baue ich danach präzise Reader.
UNVERIFIZIERT: (1) heißen die Fenster wirklich JournalAccept/JournalResult?
(2) haben Annehmen/Abschließen eine JournalCanvas? Das [Accessibility] Addon:-Log
+ die [Quest]-Zeilen beantworten beides beim nächsten Test.

### Beim nächsten Start testen (V4.51)
1. "Version 4 Punkt 51 bereit".
2. QUEST ANNEHMEN: NPC mit Quest ansprechen → wird die Beschreibung automatisch
   vorgelesen? [Quest]- und [Accessibility] Addon:-Logzeilen mitschicken.
3. JOURNAL (Taste J): Quest auswählen → kommt automatisch Titel/Ziel/Beschreibung?
4. QUEST ABSCHLIESSEN: Abgabe-/Belohnungsfenster → wird der Abschlusstext gelesen?
5. Wenn eine Situation STUMM bleibt: die [Accessibility] Addon:-Zeile aus dem Log
   schicken (zeigt den echten Fenster-Namen) — dann ziehe ich den Handler nach.

---

## STAND V4.50 (2026-07-12)

### Neu in V4.50: Eigene Gil-Taste (Umschalt+F3)
User will NICHT Strg+F3 nutzen (liest alles vor), sondern Gil separat abfragen.
Neue Taste **Umschalt+F3** = reine Gil-Ansage (InventoryService.AnnounceGil,
nur "Gil: N", kein Inventar-Vorlesen). Umschalt+F1..F12 laut Keybind-Dump
(2026-07-10, game-api.md) frei. Config-Feld KeyReadGil + Konflikt-Check.
Antwort auf User-Frage "stehen Gil im Inventar": Ja, das Inventar-FENSTER zeigt
den Gil-Betrag unten an — wir lesen ihn aber direkt über die API (kein Fenster
nötig).

### Beim nächsten Start testen (V4.50)
1. "Version 4 Punkt 50 bereit".
2. GIL-TASTE: Umschalt+F3 → wird NUR der Gil-Stand angesagt (nicht das ganze
   Inventar)? Stimmt die Zahl? [Inventory] Currency-Logzeile mitschicken.
3. STÜCKZAHL (aus V4.49): über gestapelte Items navigieren → "N mal Name"?

### Neu in V4.49: Stückzahl vor Item-Name + Gil in der Inventar-Ansage
User-Feedback zu V4.48: Item-Namen werden beim Navigieren angesagt, aber die
STÜCKZAHL fehlte davor; außerdem 2 Fragen (Kategorien? Gil?).
(1) STÜCKZAHL: Beim Navigieren über Item-Slots wird jetzt "10 mal Eichenholz"
gesagt (Menge vorangestellt). Quelle: AtkComponentIcon.QuantityText (Offset 256,
ilspycmd-verifiziert) — direkt gelesen statt per GetTextFromNodeTree (das verwirft
1-Zeichen-Strings → einstellige Mengen wären verloren). Nur bei sichtbarem
Mengen-Node + rein numerisch + ≠ "1" (Einzel-Items ohne Präfix). FindIconId →
FindSlotIcon refaktoriert (gibt jetzt AtkComponentIcon* zurück). [Focus]-Log zeigt
iconId/qty/name.
(2) GIL: Strg+F3 (Inventar vorlesen) nennt jetzt zuerst den Gil-Stand. Quelle:
IGameInventory Currency-Container (GameInventoryType.Currency=2000, ilspycmd),
Item-ID 1 = Gil; Menge ist int (deckt Gil-Cap 999.999.999). Label über Item-Sheet
Zeile 1 (spielseitig "Gil", nicht hartkodiert). [Inventory] Currency-Logzeile.
KATEGORIEN-FRAGE beantwortet: Die Tasche (Inventory1-4) ist NICHT thematisch
sortiert (nur 4×35 Slots). Getrennt hält das Spiel: Schlüsselgegenstände, Währung
(Gil/…), Kristalle, Rüstkammer (nach Ausrüstungs-Slot). Wir lesen aktuell
Schlüsselgegenstände + Tasche + jetzt Gil; Kristalle/Rüstkammer könnten wir bei
Bedarf ergänzen.

### Beim nächsten Start testen (V4.49)
1. "Version 4 Punkt 49 bereit".
2. INVENTAR (I): über gestapelte Items navigieren → kommt "N mal Name"
   (z.B. "5 mal Antidot")? Einzel-Items ohne Zahl? [Focus]-Logzeilen mitschicken.
3. GIL: Strg+F3 → wird der Gil-Stand zuerst angesagt und stimmt die Zahl?
   [Inventory] Currency-Logzeile mitschicken.

---

## STAND V4.48 (2026-07-12, getestet: Item-Name ✓, Stückzahl fehlte → V4.49)

### Neu in V4.48: Fix Item-Auto-Ansage + NPC-Rolle/Quest-Marker
(1) BUG aus V4.47-Log (00:50): belegte Inventar-Slots sagten die STÜCKZAHL
("10") statt des Namens — GetTextFromNodeTree fand die Mengen-Textzeile des
Slots, und die Icon-Auflösung lief nur bei LEEREM Text. FIX: In
UpdateGlobalFocus hat die Icon->Name-Auflösung jetzt VORRANG vor dem rohen
Text (Item-Name schlägt Menge); Auflösung nur bei Node-Wechsel, gecacht in
_lastFocusedItemName. ResolveFocusedItemName präzisiert: stoppt an der ersten
Steuerungs-Komponente (kein Weiterklettern ins Addon → Buttons werden nicht
fälschlich als Item gelesen). [Focus] Item-Slot iconId/name loggt jeden Treffer.
(2) NPC-INFO (User-Wunsch): NavigationService.DescribeNpc hängt an die
Zielansage + Objekt-Browser-Ansage die ROLLE (ENpcResident.Title, z.B.
"Marktverwalter") und einen QUEST-MARKER an (GameObject.NamePlateIconId ->
DescribeQuestMarker: 71001-06 "Quest verfügbar", 71021-46 "Quest aktiv", sonst
71xxx "Quest"). NamePlateIconId wird geloggt zum Verfeinern der Ranges.
IDataManager in NavigationService injiziert; DataId->BaseId (Dalamud-Umbenennung).
OFFEN (in-game verifizieren): (1) werden Inventar-Items jetzt mit NAMEN
angesagt (nicht "10")? (2) stimmen NPC-Rolle + Quest-Marker? [Nav] NPC-Logzeilen
zeigen die rohen NamePlateIconIds → Ranges ggf. anpassen.

### Beim nächsten Start testen (V4.48)
1. "Version 4 Punkt 48 bereit".
2. INVENTAR (I): mit Tastatur/Controller über Items → wird jetzt der NAME
   angesagt (statt "10")? [Focus] Item-Slot-Logzeilen mitschicken.
3. NPCs anvisieren/mit N durchblättern → kommt Rolle ("Marktverwalter") +
   ggf. "Quest verfügbar"? [Nav] NPC-Logzeilen (NamePlateIconId) mitschicken.

### Neu in V4.47: AUTO-ANSAGE von Item-Slots beim Navigieren (kein Strg+F3 mehr)
User-Wunsch: Items sollen beim Navigieren automatisch vorgelesen werden; bei
Questbelohnungen muss man auswählen. Log 22:56/23:11 BESTÄTIGT: HandIn-Icon->
Name funktioniert ("Hausgemachte Aalpastete", "Proviantpaket", "Metallbeschlag"
inkl. KeyItems). Belohnungsauswahl = Addons `JournalResult` (Abschließen/
Ablehnen) + `JournalRewardItem` ("Wähle die Vergütung.", Icon-Slots Comp(1006)
->Icon(15)). DURCHBRUCH aus [Focus]-Log: der globale FocusedNode WANDERT beim
Navigieren der Item-Slots (Reward: wechselt id=4 'Abschließen' <-> id=3 ''
=Slots; HandIn: id=4 ''=Ablage-Slots) — Text leer, weil Name nicht in der UI.
LÖSUNG: UpdateGlobalFocus ruft bei leerem Text ResolveFocusedItemName() — klettert
vom Fokus-Node bis 4 Eltern hoch zur Slot-Komponente (FindIconId: Icon->IconId,
DragDrop->AtkComponentIcon.IconId, sonst Icon-Kind der Multipurpose-Hülle) und
löst per InventoryService.ResolveIconName auf. Deckt Inventar + Ablieferung +
Belohnung mit EINEM Mechanismus ab, nur bei Node-WECHSEL (nicht pro Frame).
WICHTIG: Belohnungs-Items besitzt man noch nicht → ResolveIconName fällt aufs
volle Item/EventItem-Sheet zurück (Icon->Name-Cache, einmal gebaut), sonst
eigenes Inventar (kollisionsfrei). Buttons (Übergeben/Abschließen) werden weiter
vom Fokus-Text-Pfad angesagt.
OFFEN (in-game verifizieren): (1) klettert der Fokus-Node wirklich zur Slot-
Komponente (wie bei Buttons bewiesen)? (2) stimmen die Reward-Namen aus dem
Sheet-Fallback? [Focus] Item-Slot iconId/name-Logzeilen zeigen es.

### Beim nächsten Start testen (V4.47)
1. "Version 4 Punkt 47 bereit".
2. INVENTAR (I) öffnen, mit Tastatur/Controller über Items navigieren → wird
   jeder Gegenstand automatisch angesagt (ohne Strg+F3)?
3. NPC-ABLIEFERUNG: über die Slots navigieren → Item-Namen automatisch?
4. QUESTBELOHNUNG (JournalRewardItem): über die Belohnungen navigieren → Namen
   angesagt? Eine auswählen + Abschließen.
5. [Focus]-Logzeilen mitschicken (zeigen iconId+name pro Slot).

### Neu in V4.46: NPC-Ablieferung "GEGENSTAND ABLIEFERN" (Request)
User-Ablauf geklärt (Log+Dump 22:40): Parsemontret-Quest → Talk → `Request`-
Fenster ("GEGENSTAND ABLIEFERN", Nodes: Window-Titel id=3, Button id=14
"Übergeben"/id=15 "Abbrechen", DragDrop-Ablage-Slots) + Geschwister-Addon
`InventoryEventGrid` (35 DragDrop-Slots, aufs infrage kommende Item gefiltert
— im Dump nur 1 Slot mit sichtbarem Icon). WICHTIGER BEFUND: Item-NAMEN
stehen NICHT in der UI (Slots haben nur Icon + leere Mengen-Textzeile id=8/id=7
"") → Name nur per Tooltip. LÖSUNG (ilspycmd-verifiziert): pro DragDrop-Slot
AtkComponentIcon.IconId (uint) lesen und gegen eine Icon→Name-Tabelle auflösen,
die InventoryService.BuildIconNameMap() aus dem EIGENEN Inventar baut (Item-
Sheet Icon@136 / EventItem Icon@24). Node-Zugriff wie im Restcode
(Type>=1000 → AtkComponentNode->Component → ComponentType.DragDrop →
AtkComponentDragDrop->AtkComponentIcon->IconId).
- OnRequestOpen sagt beim Öffnen: "Gegenstand abliefern. Drücke Strg F3 …".
- Strg+F3 ist jetzt kontextabhängig: bei offenem Request → UIReaderService.
  TryAnnounceHandOver() liest die passenden Items aus InventoryEventGrid
  ("Ein passender Gegenstand: X. Auswählen und Übergeben."); sonst normales
  Inventar-Vorlesen (V4.45). Manuell getriggert, weil das Grid erst ein paar
  Frames nach Request-PostSetup gefüllt ist.
- Jeder Slot als [HandIn]-Logzeile (node/iconId/name = Ground Truth, falls
  Icon→Name mal danebenliegt).
OFFEN (noch nicht in-game verifiziert): (1) stimmt die Icon→Name-Auflösung?
(2) die eigentliche AUSWAHL des Items im Grid (blind auf den Slot kommen +
platzieren) ist noch NICHT gelöst — evtl. brauchen wir Grid-Slot-Navigation
per FocusedNode. Die Request-Buttons (Übergeben/Abbrechen) werden schon vom
bestehenden Fokus-Leser angesagt (Log 22:40 bestätigt).

### Beim nächsten Start testen (V4.46)
1. "Version 4 Punkt 46 bereit".
2. Parsemontret ansprechen → kommt "Gegenstand abliefern …"?
3. Strg+F3 drücken → wird der/die passende(n) Gegenstand/Gegenstände mit
   Namen angesagt? [HandIn]- und [Inventory]-Logzeilen mitschicken.
4. Item auswählen + Übergeben: schaffst du die Auswahl (Maus/Controller)?
   Falls nicht: sagen wie du navigierst (Pfeiltasten? Controller?), dann baue
   ich die Grid-Slot-Ansage.

### Neu in V4.45: Inventar vorlesen (Strg+F3)
User braucht fürs Quest-Abschließen einen Gegenstand aus dem Inventar. Neuer
InventoryService liest das Inventar via Dalamud IGameInventory (KEIN UI-Scraping,
funktioniert auch bei geschlossenem Beutel). Strg+F3 sagt: erst
Schlüsselgegenstände (KeyItems-Container, quest-relevant), dann Tasche
(Inventory1-4). Namen: normale Items über Lumina Item-Sheet (BaseItemId, HQ/
Collectible-Offset via GameInventoryItem.BaseItemId), Schlüsselgegenstände über
EventItem-Sheet. Jeder Gegenstand als [Inventory]-Logzeile (Ground Truth: id/
qty/name — zeigt v.a. ob KeyItem->EventItem-Mapping stimmt). Alles ilspycmd-
verifiziert: IGameInventory.GetInventoryItems(GameInventoryType) ->
ReadOnlySpan<GameInventoryItem>. Stapel als "Name mal Anzahl".
OFFEN: Falls die Quest verlangt, den Gegenstand IN DER UI auszuwählen/zu
benutzen (Rechtsklick->Benutzen, Übergabe-Fenster), reicht Vorlesen nicht →
dann Inventar-Addon dumpen (Strg+F5 bei offenem Beutel) und Grid-Navigation
bauen. Erst v1 (Vorlesen) testen.

### Beim nächsten Start testen (V4.45)
1. "Version 4 Punkt 45 bereit".
2. Strg+F3: werden Schlüsselgegenstände + Taschen-Inhalt vorgelesen? Ist der
   Quest-Gegenstand dabei? [Inventory]-Logzeilen mitschicken.
3. Falls die Quest den Gegenstand in der UI benutzen will: sagen, was genau
   die Quest verlangt (Beutel öffnen + benutzen? Übergabe an NPC?) → dann
   bauen wir die passende Navigation.

### PARKIERT: Auto-Lauf-Übergang "Tiefer Wald" (V4.44-Diagnose ausgewertet)
[NavDiag]-Log (21:38) ist EINDEUTIG: Unser Zielpunkt ist KORREKT — vnavmesh
findet einen vollständigen Weg, letzter Wegpunkt liegt exakt auf dem Ziel
(letzter->Ziel=0,0 m). ABER der Charakter bewegt sich KEINEN Meter (pos 5 s
lang eingefroren bei 152,5|165,0 trotz running=True); letzter Wegabschnitt ist
ein einzelner ~18-m-Sprung geradeaus. Ursache: vnavmesh-Netz stimmt an DIESEM
Ausgang nicht mit der echten Spiel-Kollision überein, Char verkeilt sich.
NICHT bei uns fixbar (Netz pro Zone im vnavmesh-Code, kein Config-Wert).
User BESTÄTIGT: andere NPCs und Übergänge funktionieren einwandfrei → es ist
EINE einzelne kaputte Stelle (vnavmesh-Netz-Bug an diesem Ausgang), kein
allgemeines Problem. Optionaler künftiger Hebel auf UNSERER Seite: sanfter
Notausstieg (bei Übergangs-Stau auf Gehhilfe/Beacon umschalten) ODER die
Stelle vnavmesh-Autor melden. NavDiag-Logs sind noch drin (bewusst).

### Neu in V4.44: Diagnose des Auto-Lauf-Feststeckens am Zonen-Übergang
Befund V4.43 (Log 21:12): Auto-Lauf zum Übergang "Tiefer Wald" jammt 18 m vor
dem Ziel fest — vnavmesh meldet einen gültigen Pfad + running=True, der
Charakter kriecht aber nur ~1 m und die Stillstand-Erkennung stoppt korrekt
("Komme nicht näher, noch 18 Meter"). Offen: liegt unser Zielpunkt (Karten-
Symbol-Position) HINTER einem Hindernis, oder hat das Netz dort eine LÜCKE?
Geklärt: vnavmesh-Mesh-Settings (AgentRadius usw.) sind pro Zone im
Plugin-Code FEST verdrahtet (NavmeshCustomization), NICHT per Config änderbar
— nur Neubau von vnavmesh würde sie ändern (Fremdcode, nicht gemacht).
Einzige Config-Änderung: vnavmesh.json BuildMaxCores 1->0 (alle CPU-Kerne,
schnellerer Mesh-Bau neuer Zonen; Backup vnavmesh.json.bak-vor-buildcores).
DIAGNOSE-BUILD: AutoWalkService loggt jetzt via IPC Path.ListWaypoints den
tatsächlich verfolgten Pfad ([NavDiag]): einmal die volle Wegpunkt-Route +
Abstand des letzten Wegpunkts zum Ziel; jede Sekunde Live-Position +
Rest-Wegpunkte + Abstand zum nächsten Wegpunkt. Auswertung:
- letzter Wegpunkt NAH am Ziel (< ~2 m) → Ziel erreichbar, Char jammt an
  echter Kollision (Netz sagt begehbar, Spiel blockiert).
- letzter Wegpunkt WEIT vom Ziel → Netz-Route endet vorher (Lücke / falscher
  Zielpunkt) → Fix in UNSEREM Code (Ziel auf letzten erreichbaren Netzpunkt).

### Beim nächsten Start testen (V4.44)
1. "Version 4 Punkt 44 bereit".
2. Denselben Übergang ansteuern: Quest-Ziel/Wegpunkt "Übergang nach Tiefer
   Wald" mit N wählen → Numpad 3. Feststecken abwarten ("Komme nicht näher").
3. Log an Claude schicken — die [NavDiag]-Zeilen beantworten: Hindernis vor
   dem Ziel oder Netz-Lücke?

---

## Historie: V4.43 (2026-07-11)

### Neu in V4.43: Auto-Lauf-Rückmeldung + garantierte Termination
User: „läuft immer noch nicht richtig ran". LOG-BEFUND (21:00): In der GANZEN
Historie feuert NIE eine Ankunft — Lauf lief 48 s (190 m → 18 m), dann
manuell gestoppt. Ursachen: (1) beim Laufen KEINE gesprochene Rückmeldung,
nur Beacon-Ton → User unsicher, ob es arbeitet, bricht ab; (2) bei einem
Übergang meldet vnavmesh nie Ankunft (Ziel liegt jenseits der Zonenlinie).
FIX (AutoWalkService):
- Gesprochener Fortschritt alle 3 s („Noch 120 Meter").
- ZONENWECHSEL = Erfolg: TerritoryType ändert sich → „Angekommen, neues
  Gebiet erreicht" + Stopp (echtes Ankunftssignal beim Übergang).
- STILLSTAND-ABBRUCH: kein Fortschritt (>1 m) seit 5 s → Stopp mit „Komme
  nicht näher, noch X Meter" statt ewig weiterzulaufen.
- Diagnose-Log alle 3 s: dist/running/computing (klärt beim nächsten Test,
  ob es zügig hinläuft oder an der Zonenkante hängt).
IClientState in AutoWalkService injiziert.

### Beim nächsten Start testen (V4.43)
1. „Version 4 Punkt 43 bereit".
2. AUTO-LAUF: N (Ziel/Wegpunkt) → Numpad 3: hörst du jetzt alle 3 s „Noch X
   Meter"? Läuft die Zahl runter? Kommt am Ende „Ziel erreicht"?
3. ÜBERGANG: Quest anderes Gebiet / Übergangs-Wegpunkt → Numpad 3: läuft er
   hin, und kommt beim Zonenwechsel „Angekommen, neues Gebiet erreicht"?
   Falls er hängt: „Komme nicht näher, noch X Meter" (dann sagt mir X — das
   zeigt, ob der Übergangs-Marker falsch positioniert ist).

---

## V4.42 (2026-07-11)

### Neu in V4.42: Dialog liest wieder die FRAGE vor (User-Meldung)
Problem: Bestätigungsfenster (SelectYesno) sagten nur noch „Ok"/„Abbrechen",
nicht mehr die Frage — man wusste nicht, was man bestätigt. ROOT CAUSE (Log
19:52 + 20:05): DREI Ansager, alle SpeakInterrupt: OnYesNoOpen liest Frage+
Buttons, aber OnDialogButtonProbe UND UpdateGlobalFocus ([Focus]) sagen den
fokussierten Button ~6 ms später an → schneiden die Frage ab. [Focus] liest
die echte Spiel-Fokus-Node (brauchen wir für Links/Rechts), darf nicht weg.
FIX: Schutzfenster von 1 s nach Dialog-Öffnung (InDialogOpenGuard) — beide
Button-Ansager führen ihren Status weiter mit, sprechen aber nicht; Navigation
nach dem Fenster wird normal angesagt. Gilt für SelectYesno + SelectString.

### Beim nächsten Start testen (V4.42)
1. „Version 4 Punkt 42 bereit".
2. DIALOG: ein Bestätigungsfenster öffnen (z. B. Ausloggen, Quest annehmen/
   ablehnen): kommt jetzt die ganze FRAGE + „Ok oder Abbrechen"? Dann
   Links/Rechts: sagt er die neue Auswahl an?

---

## V4.41 (2026-07-11)

### Log-Auswertung V4.40 (Log 19:48-19:52) — was funktioniert / Bugs
FUNKTIONIERT: ✓ Hotbar-Vorlesen Strg+F9 (alle Slots sauber: Gewaltiger
Schuss, Direkter Schuss, Teleport, Sprint …); ✓ Ok/Abbrechen-Dialog
Links/Rechts + Ansage („Das Spiel beenden? Ok oder Abbrechen …") — der
alte Dauerbrenner geht endlich!; ✓ neue Übergangs-Stopp-Distanz 0,5 greift.
NICHT getestet: Kampf (User war in der Stadt, kein Gegner anvisiert).
2 BUGS gefunden + in V4.41 gefixt:

### Neu in V4.41: zwei Fixes aus der Log-Auswertung
1. CAST-BALKEN-SPAM: der Text-Scanner las den EIGENEN Zauber-Countdown
   (_CastBar id=7: „00.63"…„00.02") jeden Frame vor (beim Teleportieren).
   Fix: _CastBar in HudNoiseAddons. (Eigene Casts später sauber via
   LocalPlayer.IsCasting, nicht per Text-Scan.)
2. ÜBERGANG LANDETE IM TIEFGESCHOSS: PointOnFloor castet nach UNTEN → vom
   Steg (Y=-12,9) auf den Boden 37 m tiefer (-50,5); 18-m-Übergang wurde
   40-m-Lauf in die falsche Etage (User brach 2× ab). Fix: ResolveFloorPoint
   nutzt jetzt vnavmesh NearestPoint(10,10) (begrenzte Box um die Höhe),
   PointOnFloor nur noch Fallback. (game-api.md → vnavmesh-FALLE.)

### Beim nächsten Start testen (V4.41)
1. Ansage „Version 4 Punkt 41 bereit".
2. TELEPORT/RÜCKFÜHRUNG wirken: KEIN „00.30 00.28…"-Countdown-Spam mehr?
3. ÜBERGANG: Quest in anderem Gebiet / Übergangs-Wegpunkt → Numpad 3:
   läuft er jetzt die ~18 m auf gleicher Höhe zur Zonenlinie (nicht 40 m
   nach unten)? Log: „[Orte] NearestPoint …" statt „-50,5". Wechselt Gebiet?
4. KAMPF (noch offen aus V4.39): Gegner anvisieren → Ziel-Ton + HP? Angriff
   → „Ziel HP …"? „Gegner wirkt …"? Strg+F12 mit Ziel-HP?

### Neu in V4.40: Auto-Lauf kommt dichter ans Ziel + Übergänge auslösen
User: „läuft nicht immer ganz zu gewissen Orten; bei Übergängen soll er
gleich ins Gebiet rein." ROOT CAUSE: vnavmesh `PathfindAndMoveCloseTo`
hält absichtlich `range` Meter VOR dem Ziel; range war überall 2,5 m
(Interaktionsdistanz). Zusätzlich wurden Questziele nicht aufs Netz
eingerastet (nur Wegpunkte) → bei off-mesh-Markern stoppte er weit vorher.
1. Getrennte Stopp-Distanzen (Config): NPCs bleiben 2,5 m; Orte, Wegpunkte
   und Questziele 1 m (AutoWalkPlaceStopRange); Zonen-Übergänge 0,5 m
   (AutoWalkTransitionStopRange), damit man auf die Zonenlinie läuft und
   der Übergang auslöst.
2. Questziele werden jetzt vor dem Laufen per vnavmesh PointOnFloor aufs
   begehbare Netz eingerastet (Fallback: rohe Marker-Position), damit die
   enge Stopp-Distanz überhaupt erreichbar ist.
3. ToggleToPosition nimmt jetzt die fertige Stopp-Distanz statt eines
   Radius; der Aufrufer (Plugin.cs) wählt sie nach Kontext.

### SORTIERUNG: schon vollständig da (User-Frage geklärt)
User bat, „nicht nur Quests, alle Kategorien nach Entfernung zu sortieren".
Prüfung: ALLE Kategorien sortieren bereits nach Entfernung, nächstes zuerst
— Objekt-Kategorien (GetCategoryObjects, OrderBy Distance), Wegpunkte
(OrderBy Distance2D) und Quests (V4.38). Nichts zu ändern. Falls im Spiel
eine Kategorie doch unsortiert wirkt: konkretes Beispiel (welche Kategorie,
gehörte Reihenfolge) → dann echte Ursache suchen.

### Beim nächsten Start testen (V4.40)
1. Ansage „Version 4 Punkt 40 bereit".
2. AUTO-LAUF GENAUIGKEIT: N (Wegpunkt/Questziel) → Numpad 3: kommt er jetzt
   dichter ran (~1 m statt ~2,5 m)? „Ziel erreicht" statt „beendet, noch X"?
3. ÜBERGANG: Quest in anderem Gebiet oder Wegpunkt-Übergang → Numpad 3:
   läuft er auf die Zonenlinie und wechselt das Gebiet von selbst?
4. Sortierung stichprobenartig: NPCs/Objekte mit N durchblättern — nächstes
   zuerst?

### Ältester offener V4.39-Testblock (Kampf) siehe unten — mittesten:

### V4.39: Kampf-Wahrnehmung (Gegner-HP, Cast, Ziel-Ton, Hotbar)
- Gegner anvisieren (Tab/F11): Ziel-Ton + HP-Ansage? Kampf: „Ziel HP …",
  „Gegner wirkt …", Strg+F12 mit Ziel-HP? Strg+F9: Aktionsleiste vorgelesen?

### V4.38 (mit V4.39 ungetestet): Entfernungssortierung + Annehmbare Quests

### Neu in V4.39: Kampf-Wahrnehmung (barrierefrei kämpfen)
User-Wunsch: „bau alles damit Kämpfen barrierefrei wird, plus ein Ton wenn
ein Gegner anvisiert ist." Alle Structs ilspycmd-verifiziert (game-api.md
→ „Kampf"). Umgesetzt in diesem Batch:
1. GEGNER-HP: Beim Zielwechsel wird die HP des Ziels mit angesagt („Ziel:
   Name, Kampf-NPC, 12 Meter, geradeaus, HP 100 Prozent"). Im Kampf sagt
   das Plugin die HP des aktuellen Ziels in Stufen (75/50/25/10 %) an, damit
   man hört, ob der Angriff wirkt. Strg+F12 nennt jetzt auch die Ziel-HP.
   (CombatService + NavigationService.DescribeTargetHp.)
2. GEGNER-CAST: Wirkt das Ziel eine Aktion, kommt „Gegner wirkt <Name>"
   (Aktionsname aus Lumina Action-Sheet, einmal pro Cast). Vorwarnung für
   große Angriffe. (CombatService.UpdateTarget.)
3. ZIEL-TON: Kurzer 2-Ton-Piepser (steigend, 990→1320 Hz), sobald ein
   GEGNER (BattleNpc) anvisiert wird — auch bei Auswahl per N. Neuer
   CueService (eigener NAudio-Einzelton, unabhängig vom Gehhilfe-Beacon).
   Config: EnableTargetTone, TargetToneVolume=0.4.
4. HOTBAR VORLESEN: Strg+F9 liest Aktionsleiste 1 vor („Taste 1, Vollschlag.
   Taste 2, …"). FF14 hat keinen Angriff-Knopf — man zielt und drückt die
   Zahlentasten 1–0 (= Hotbar-1-Slots). Neuer HotbarService liest
   RaptureHotbarModule, Namen via Lumina Action + PopUpHelp-Fallback.
   Jeder Slot wird als [Hotbar]-Zeile geloggt (Ground Truth).

### Beim nächsten Start testen (V4.39)
1. Ansage „Version 4 Punkt 39 bereit".
2. GEGNER ANVISIEREN (Tab oder F11 oder N-Kategorie „Gegner"): kommt der
   kurze Ziel-Ton? Wird die Gegner-HP mit angesagt?
3. KAMPF: einen schwachen Gegner angreifen (Zahlentasten): sinkt-Ansagen
   „Ziel HP 75/50/25/10 Prozent"? Sagt „Gegner wirkt …" wenn er zaubert?
   Strg+F12 → eigene HP + Ziel-HP?
4. HOTBAR: Strg+F9 → werden die Aktionsnamen der Tasten 1–0 vorgelesen?
   (Log-Zeilen [Hotbar] bitte mitschicken, dann sehe ich rohe type/id/name.)
5. Noch aus V4.38 mittesten: Sortierung nach Entfernung, Kategorie
   „Annehmbare Quests" (Strg+N).

### NÄCHSTER BATCH (V4.40, noch NICHT gebaut) — bewusst verschoben
Diese zwei brauchen eigene Verifikation und kommen als Nächstes:
- Cooldown/GCD-Feedback („bereit"): ActionManager (GetRecastTime/
  IsRecastActive) + HotbarSlot.IsSlotUsable — noch nicht verifiziert.
- Aktions-Fehler („außer Reichweite", „nicht genug MP"): _TextError wird
  schon gelesen (nie in-game bestätigt) bzw. UseAction-Hook — in-game prüfen.

---

### V4.38: Entfernungssortierung + Kategorie „Annehmbare Quests"
User-Wünsche: (1) alles nach Entfernung sortiert, damit klar ist was am
nächsten ist; (2) Kategorie für noch nicht angenommene Quests im Gebiet.
1. SORTIERUNG: Quest-Ziele werden jetzt durchgängig nach Lauf-Entfernung
   sortiert. Im Gebiet nach Luftlinie; in Fremdzonen nach Distanz zum
   ersten Übergang (das ist, wohin man tatsächlich läuft) — „am nächsten"
   bleibt so über Zonengrenzen sinnvoll. In-Gebiet-Ziele kommen weiter
   zuerst. (NavigationService.EffectiveWalkDistance.)
2. NEUE KATEGORIE „Annehmbare Quests" (Strg+N): liest
   Map.UnacceptedQuestMarkers (StdList<MarkerInfo>, ilspycmd-verifiziert)
   → annehmbare Quests in der Nähe mit Name, Entfernung, Richtung. Numpad 3
   läuft hin (nutzt dieselbe SelectedQuestDestination-Pipeline inkl.
   Zonen-Routing, kein Plugin.cs-Eingriff nötig).
   QuestMarkerService.GetUnacceptedDestinations + geteilter Marker-Reader
   AddMarkerDestinations.

### Beim nächsten Start testen (V4.38)
1. Ansage „Version 4 Punkt 38 bereit".
2. ANNEHMBARE QUESTS: Strg+N bis „Kategorie Annehmbare Quests: X im
   Gebiet" → N mehrfach: werden annehmbare Quests mit Entfernung/Richtung
   angesagt? → Numpad 3: läuft er zum Quest-Geber? Dort Quest annehmen.
3. SORTIERUNG: Mehrere Quests aktiv → Quest-Ziele durchblättern: kommt das
   nächstgelegene zuerst? Fremdzonen-Quests nach Übergangs-Nähe sortiert?
4. Weiter offen aus V4.37 (unverändert):

### Neu in V4.37: Zonen-Routing für Quests + Beschreibungs-Vorarbeit
User-Fragen: (1) Quest in anderem Gebiet — wie erfährt ein Blinder, WO
er hinlaufen muss? (2) Quests werden angesagt, aber ohne Beschreibung.
1. ZONEN-ROUTING: PlacesService baut aus den MapMarker-Übergängen einen
   statischen Karten-Graphen (FindFirstHopToMap: Breitensuche, gecacht).
   QuestDestination trägt jetzt MapId.
   - Quest-Ansage bei fremdem Gebiet: „…, im Gebiet Alt-Gridania.
     Dorthin über Übergang nach Alt-Gridania, 150 Meter, links[, danach
     noch 2 weitere Übergänge]. Nummernblock 3 läuft zum Übergang."
   - Numpad 3 läuft bei Fremd-Gebiet-Quest jetzt ZUM ÜBERGANG (mit
     PointOnFloor-Höhe) statt „dorthin kann ich nicht laufen".
   - Kein Weg im Graphen (nur Fähre/Teleport erreichbar) → klare Ansage.
2. Quest-Ansage hängt Marker-Tooltip an, wenn er mehr sagt als der Name.
3. [Probe] _ToDoList: bei jedem Kategorie-Wechsel (Strg+N) loggt das
   Plugin die sichtbaren Texte des Quest-TRACKERS (Node-Ids). Der Tracker
   zeigt das AKTUELLE ZIEL jeder Quest — perfekte Quelle für „Beschreibung
   ansagen", aber Struktur unbekannt → erst Probe, dann Reader (V4.38).

### Beim nächsten Start testen (V4.37)
1. Ansage „Version 4 Punkt 37 bereit".
2. QUEST IN ANDEREM GEBIET: Kategorie Quest-Ziele → N: sagt er Gebiet +
   Übergang an? → Numpad 3: läuft er zum Übergang? Durch den Übergang
   gehen (am Ende selbst durchlaufen), drüben neu ansagen lassen.
3. Strg+N einmal durchschalten (erzeugt [Probe]-Zeilen vom Quest-Tracker
   — die brauche ich für die Beschreibungs-Ansage).
4. Weiter offen: Links/Rechts-Dialog ([Focus]/[Key]), Journal-Zeilen,
   Optionen, Ätheryt-Distanz-Vergleich (Formel-Check), unentdeckte Orte?

---

## V4.36 (2026-07-11 Nachmittag — BESTÄTIGT: Wegpunkte lesen ✓)

### Neu in V4.36: Kategorie „Wegpunkte" (User-Wunsch: Ziele außer Reichweite)
Problem: Quests/NPCs außerhalb der 100m-Reichweite bzw. in anderen Zonen —
es fehlten anlaufbare Zwischenziele (Ausgänge, Ätheryten, Orte).
1. PlacesService (neu): liest die STATISCHEN Karten-Symbole der aktuellen
   Karte aus dem Lumina-Sheet „MapMarker" (Zeilen via Map.MapMarkerRange):
   - DataType 1/2 = ZONEN-ÜBERGANG (DataKey = Ziel-Karte → „Übergang
     nach Alt-Gridania")
   - DataType 3 = Ätheryt (Name aus Aetheryte-Zeile), 4 = Aethernet
   - sonst benannte Orte (PlaceNameSubtext: Gilden, Marktbrett …)
   Pixel→Welt-Formel aus Dalamuds MapUtil hergeleitet (dekompiliert,
   Konsistenz geprüft): welt = (pixel−1024)×100÷SizeFactor − Offset.
2. Objekt-Browser: neue Kategorie „Wegpunkte" (Strg+N), N/Umschalt+N
   blättert nach 2D-Distanz: „3 von 12: Übergang nach Alt-Gridania,
   Übergang, 150 Meter, links." ([Orte]-Logzeilen als Probe).
3. Numpad 3 läuft hin: Kartendaten sind 2D (keine Höhe!) → vorher
   vnavmesh-Query `Query.Mesh.PointOnFloor` (IPC dekompiliert-verifiziert,
   vnavmesh nutzt sie selbst für „FlagToPoint") ermittelt den begehbaren
   Punkt; Fehlschlag → klare Ansage „Kein begehbarer Punkt gefunden".
4. csproj referenziert jetzt Lumina + Lumina.Excel (aus DALAMUD_HOME).
OFFENE LAUFZEITFRAGEN (Log zeigt es): (a) stimmt die Pixel→Welt-Formel
in der Praxis? Ätheryt-Wegpunkt vs. Ätheryt-Objekt (Kategorie Ätheryten)
müssen ~gleiche Distanz melden; (b) MapMarkerUnlockedBit ignorieren wir —
evtl. tauchen unentdeckte Orte auf (Fog-of-War-Frage, ggf. filtern).

### Beim nächsten Start testen (V4.36, inkl. V4.34/V4.35-Punkte)
1. Ansage „Version 4 Punkt 36 bereit".
2. WEGPUNKTE: Strg+N bis „Kategorie Wegpunkte: X im Gebiet, davon Y
   Übergänge" → N mehrfach: Übergänge/Ätheryten/Orte mit Distanz?
   Vergleich: Ätheryt in Kategorie Ätheryten vs. Wegpunkte — ähnliche
   Distanz? → Numpad 3: läuft er zum Übergang? Ansage bei Ankunft?
3. Ok/Abbrechen-Dialog: Links/Rechts → Ansage? ([Focus]/[Key]-Probe)
4. Journal (J): Pfeiltasten → Quest-Zeilen?
5. Optionen: bedienbar? Einloggen/Laufen: Ruhe?

---

## V4.35 (2026-07-11 Mittag, im selben Build)

### Neu in V4.35: Globaler Fokus-Melder + Pfeiltasten-Sonde
User-Meldung: Links/Rechts in Ok/Abbrechen-Dialogen wurde SCHON ÖFTER
gedrückt — weder Navigate-Log noch BtnProbe-Flags registrierten es.
Konsequenz: nicht weiter an Addon-Flags raten, sondern die QUELLE lesen:
1. UpdateGlobalFocus (jeden Frame): liest
   AtkStage.Instance()->AtkInputManager->FocusedNode (@6272, ilspycmd) —
   den Node, den DAS SPIEL für tastatur-fokussiert hält. Bei Wechsel:
   [Focus]-Log + Ansage (Text via GetTextFromNodeTree, klettert bis 3
   Eltern hoch, weil Fokus oft auf dem Collision-Kind sitzt).
   Deckt potenziell ALLE Fenster ab (Dialoge, Optionen, …).
   Identische Doppel-Ansagen (z. B. parallel zur Listen-Navigation)
   fängt der 0,5s-Debounce; gemeldete Dopplungen dann gezielt abdrehen.
2. [Key]-Sonde: loggt jede erkannte Pfeiltaste, solange ein Menü/Dialog
   aktiv ist → klärt endgültig, ob IKeyState Pfeiltasten überhaupt
   SIEHT, wenn ein Dialog offen ist (Spiel könnte sie verschlucken).

### Beim nächsten Start testen (V4.35, inkl. V4.34-Punkte)
1. Ansage „Version 4 Punkt 35 bereit".
2. Ok/Abbrechen-Dialog: Links/Rechts → wird der Knopf jetzt angesagt?
   (Danach zeigen [Focus]/[Key]-Zeilen im Log, was das Spiel tut.)
3. JOURNAL (J): Pfeiltasten → Quest-Zeilen angesagt? (V4.34-Fix)
4. Optionen: bedienbar ohne „Dump"-Ansagen? Pfeiltasten → Ansagen?
5. Einloggen/Umherlaufen: Ruhe? (kein „INVENTAR", kein Nameplate-Geplapper)
   Falls dir dabei etwas FEHLT (z. B. Chat-Meldungen), bitte melden.

---

## V4.34 (2026-07-11 Mittag, im selben Build)

### V4.33-Testauswertung (Log 10:33–10:42, dank [Speak]/[Scan] voll attribuierbar)
- ✓ Optionen öffnen sauber: „Systemeinstellungen. Anzeigeeinstellungen,
  Tab 8 von 8" — KEIN fps-Spam mehr.
- ✓ Kein Koordinaten-/Uhr-Spam beim Laufen (_NaviMap/_DTR-Ignore greift).
- ✓ SelectString-Navigation lief („Ja"/„Nein" bei Pfeiltasten).
- ✓ Talk-Sprecher-zuerst weiter bestätigt (id2-Pinning aktiv ab V4.33).
- ✗ Journal: Liste GEFUNDEN, Hov2 BEWEGT sich (Pfeiltasten!), aber
  Ansage blieb stumm. ROOT CAUSE: TreeList lässt Basis-ListLength auf 0
  (eigener Items-Vektor @432, ilspycmd) → ReadListItemText-Guard
  `idx >= ListLength` verwarf jede Zeile; Ansage war „Menü, 0 Einträge".
- ✗ [Scan]-Log überführte NEUE Spam-Quellen (alle wurden gesprochen!):
  NamePlate-„Fokus" (pendelte alle 2s: Bertennant/Ulta), _TargetInfo
  (doppelt die [Nav]-Ansage), ChatLogPanel_0 (jede Chat-Zeile),
  _MiniTalk (Sprechblasen), _ParameterWidget/_Exp (HP/XP-Ticks),
  _GetAction, JournalDetail (unsichtbare Buttons „Entfernen/Neuer
  Versuch/Karte"), _CharaSelectTitle.
- ✗ JournalDetail sprach beim Journal-Öffnen einen UNSICHTBAREN
  Fehlertext („Du kannst den Auftrag nicht annehmen …") — ReadAllTexts
  las auch versteckte Nodes.
- ✗ ConfigSystem: CS-DIAG-Dump feuerte bei JEDEM Tastendruck (1400
  Zeilen + Ansage „ConfigSystem Dump. 593 Nodes." — 40k Zeilen Flut,
  Options-Bedienung dadurch unbenutzbar).
- ✗ Beim Einloggen „INVENTAR" 3× u.ä.: PostSetup-Ansagen für
  UNSICHTBARE vorerzeugte Fenster + Menü-Stack-Müll (Stack-Tiefe 7).
- Links/Rechts in Ok/Abbrechen-Dialogen: wieder nicht gedrückt (keine
  Navigate-Zeilen); BtnProbe zeigt konstant kein Fokus-Bit auf Buttons.

### Neu in V4.34
1. Journal-Fix: ReadListItemText begrenzt auf AllocatedItemRendererList-
   Length statt ListLength; Eintragszahl via TreeList.Items.LongCount
   (GetListEntryCount) → Journal-Zeilen sollten jetzt sprechen.
2. HudNoiseAddons (erweitert, alles log-bewiesen): NamePlate, _TargetInfo*,
   ChatLog*, _MiniTalk, _ParameterWidget, _Exp, _GetAction, JournalDetail,
   _CharaSelectTitle + bisherige _NaviMap/_DTR. Gilt für Scanner UND
   Fokus-Pfad UND OnOpen. (Chat-Vorlesen später sauber via IChatGui.)
3. Sichtbarkeits-Gate in OnAnyAddonOpen: unsichtbare PostSetup-Addons
   (vorerzeugte Fenster beim Zone-in) werden nur geparkt (_noListCache);
   Ansage kommt erst, wenn sie sichtbar werden (Späte-Listen-Pfad).
4. ReadAllTexts + ScanAddonTexts lesen nur noch SICHTBARE Text-Nodes.
5. CS-DIAG-Dump (Options-Flut) entfernt.

### Beim nächsten Start testen (V4.34)
1. Ansage „Version 4 Punkt 34 bereit".
2. Einloggen: deutlich weniger Geplapper (kein „INVENTAR" 3×)?
3. JOURNAL (J): Pfeiltasten → wird jetzt jede Quest-Zeile angesagt?
4. Optionen (Escape → Systemkonfiguration): normal bedienbar, keine
   „Dump"-Ansagen? Was sagen Pfeiltasten/Tab an?
5. Umherlaufen/NPCs anschauen: Ruhe (kein Nameplate-/Chat-Geplapper)?
   Melde, falls dir dabei ETWAS FEHLT, das du vorher nützlich fandest
   (z. B. Chat-Zeilen) — das bauen wir dann als sauberes Feature.
6. Ok/Abbrechen-Dialog: bitte diesmal LINKS/RECHTS drücken (Probe wartet).

### Antwort auf User-Frage: „Orte" als Kategorie (Recherche 2026-07-11)
JA, machbar — Quellen (ilspycmd-verifiziert):
- AgentMap.EventMarkers (StdVector<MapMarkerData> @232) = dynamische
  Karten-Marker; AgentMap hat CurrentTerritoryId/MapId + SizeFactor/Offsets.
- Statische Karten-Symbole (Ätheryten, ZONEN-AUSGÄNGE, Marktbrett, Läden,
  Gilden): Lumina-Sheet „MapMarker" via IDataManager (X/Y pro Karte,
  IconId, PlaceName). ABER: nur 2D — Y-Höhe fehlt → für Numpad-3-Lauf
  vnavmesh-Query (PointOnFloor) nötig. Plan: neue Kategorie „Orte" im
  Objekt-Browser, gleiche Mechanik wie Quest-Ziele (Positions-Lauf).

---

## Historie: V4.33 (2026-07-11 Mittag)

### V4.32-Testauswertung (Log 10:12–10:17): ZWEI DURCHBRÜCHE + neue Bugs
- 🎉 SYSTEMMENU-NAVIGATION FUNKTIONIERT: alle 15 Einträge wurden beim
  Pfeiltasten-Navigieren angesagt (hoch UND runter, mit Umbruch).
  PROBE-ERGEBNIS: `HoveredItemIndex2` (@344) ist DAS Tastatur-Feld
  (ändert sich zuerst, HoveredItemIndex zieht 1 Frame nach; Sel bleibt -1;
  Enter setzt HeldItemIndex). Dokumentiert in docs/game-api.md.
- 🎉 TALK-SPRECHER ZUERST funktioniert („Capucine: He, du! …").
  PROBE: Name = Node id=2, Text = id=3 (jede Seite konsistent).
- ✗ NEU: ConfigSystem (Optionen, User-Dump 593 Nodes): fps-Zähler wurde
  als Tab-Überschrift gewählt → „59 fps, Tab 8 von 8" JEDE SEKUNDE.
  Root Cause: GetConfigSectionHeading sucht rückwärts, fps-Node (id=4)
  liegt am Ende der Node-Liste VOR der echten Überschrift (id=22
  „Anzeigeeinstellungen"); Volatile-Filter fehlte im Tab-Pfad.
  Zudem loggte „Tab fokussiert" jeden Frame (tausende Zeilen).
- ✗ NEU: Beim Auto-Lauf Spam durch generischen Text-Scanner:
  Koordinaten „X:12,4 Y:13,4", Serveruhr, vnavmesh-Status „Mesh: Ready |
  Moving" — Quelle plausibel _NaviMap + _DTR (Scanner loggte Quelle nicht).
- ✗ Kaputte Symbol-Glyphen in Ansagen („H(icon) Dalamud Plugins",
  Uhr-Icon vor Serverzeit) — FFXIV bettet Icons als Private-Use-Zeichen ein.
- Journal + Links/Rechts in Dialogen: diesmal NICHT getestet (fps-Spam
  dominierte). BtnProbe zeigt: im Ruhezustand trägt kein Dialog-Knopf
  das Fokus-Bit 0x100.

### Neu in V4.33
1. ConfigSystem: Volatile-Filter (fps/Zahlen) in GetConfigSectionHeading —
   Überschrift ist jetzt „Anzeigeeinstellungen" statt „59 fps"; kein
   Ansage-/Log-Spam mehr (Frame-Logs entfernt, Aufrufer loggt Wechsel).
2. Text-Scanner: _NaviMap + _DTR ausgenommen (ScanIgnoredAddons);
   JEDE Scanner-Ansage loggt jetzt ihre Quelle ([Scan] Addon id: 'Text')
   → künftiger Spam ist sofort attribuierbar.
3. TolkService filtert Symbol-Glyphen (U+E000–U+F8FF, U+FFFD) aus ALLEN
   Ansagen → „Dalamud Plugins" statt „H-Kauderwelsch-Dalamud Plugins".
4. Talk: Sprecher-Node fest auf id=2 gepinnt (Probe-verifiziert) —
   Seiten ohne Sprecher und TalkSubtitle werden nie falsch umsortiert.

### Beim nächsten Start testen (V4.33)
1. Ansage „Version 4 Punkt 33 bereit".
2. OPTIONEN (Escape → Systemkonfiguration): Wird beim Öffnen
   „Systemeinstellungen. Anzeigeeinstellungen, Tab 8 von 8" gesagt und
   dann RUHE (kein fps-Spam)? Mit Maus/Tastatur durch Optionen: was
   wird angesagt? Tab wechseln (andere Kategorie-Icons): Ansage?
3. Auto-Lauf (N → Numpad 3): unterwegs KEINE Koordinaten-/Uhr-/
   Mesh-Ansagen mehr?
4. Escape → SystemMenu: „Dalamud Plugins" jetzt sauber (ohne
   Kauderwelsch)?
5. Journal (J): Pfeiltasten → Zeilen angesagt? (V4.32-Listen-Fix nie
   im Journal getestet!)
6. Dialog mit Ok/Abbrechen: LINKS/RECHTS drücken → Ansage? (Log
   zeigt danach [BtnProbe]/Navigate-Zeilen — diesmal bitte testen.)

---

## Historie: V4.32 (2026-07-11 Mittag)

### V4.31-Testauswertung (User + Log 09:34–09:45)
- ✓ NPC-DIALOGE FUNKTIONIEREN (User bestätigt, 26 Talk-Ansagen im Log,
  komplette Miounne-Szene). User-Wunsch: Name ZUERST („Miounne: Text"),
  bisher hing er hinten dran („Text. Miounne.") — Name ist eigener
  Text-Node, kommt in Node-Reihenfolge zuletzt (alle 26 Seiten, 2 Sprecher).
- ✗ Journal + SystemMenu weiter stumm bei Pfeiltasten. ABER: V4.30-Fix
  GREIFT — beide Male „Menü geöffnet" (PushMenu = Liste GEFUNDEN).
  Neue Erkenntnis: SelectedItemIndex BEWEGT SICH NICHT bei Tastatur
  (kein einziger Index-Wechsel im Log trotz Navigation).
- ✗ SelectYesno/JournalResult: Links/Rechts-Wechsel stumm. Navigate()
  loggte nichts → nicht diagnostizierbar (dieselbe Falle wie V4.21:
  TolkService loggte Sprachausgaben nicht).
- JournalResult („Ablehnen"/„Abschließen") ist KEIN SelectYesno — hatte
  gar keinen Links/Rechts-Handler; Fokus-Scan fand nur 1× statisch
  „Ablehnen" (Key=38004, Collision-Fallback matcht immer Node 38).

### Neu in V4.32 (Probes + Fixes)
1. SPRACH-LOG: TolkService loggt jetzt JEDE Ansage ([Speak]-Zeilen,
   auch Debounce-Verwerfungen). Log-Stille beweist ab jetzt Stummheit.
2. Talk: Sprecher ZUERST („Miounne: …"). Annahme (log-verifiziert,
   26 Seiten): letzter Text-Node = Name. Probe-Zeile „Dialog-Nodes:
   [id..]='…'" pinnt die Node-Id dauerhaft fest.
3. Listen-Probe (Journal/SystemMenu): AtkComponentList hat 5 Index-
   Kandidaten (ilspycmd: Selected@308, Held@312, Hovered@316,
   Hovered2@344, Hovered3@352) + IsHighlighted pro Zeile. V4.32 trackt
   ALLE, loggt Änderungen ([ListProbe]) und sagt die Zeile des bewegten
   Index an. Das Log zeigt, welches Feld die Tastatur wirklich trackt.
4. Dialog-Button-Fokus-Probe (SelectYesno + JournalResult): loggt alle
   Button-Flags bei Änderung ([BtnProbe]) und sagt den Button mit
   Fokus-Bit (0x100) an. Navigate() loggt jetzt zusätzlich jeden Aufruf.

### Beim nächsten Start testen (V4.32)
1. Ansage „Version 4 Punkt 32 bereit".
2. NPC ansprechen: kommt der Name jetzt ZUERST („Miounne: …")?
3. J → Journal: Pfeiltasten hoch/runter → wird jede Zeile angesagt?
   (Falls ja: fertig. Falls nein: [ListProbe]-Zeilen zeigen mir warum.)
4. Escape → SystemMenu: Pfeiltasten → „Ausloggen" usw. angesagt?
5. Beenden-Dialog (oder Quest-Dialog): Links/Rechts drücken → wird
   „Ok"/„Abbrechen" (bzw. „Ablehnen"/„Abschließen") angesagt?
6. Danach Log an Claude — die Probe-Zeilen ([ListProbe], [BtnProbe],
   [Speak], Dialog-Nodes) beantworten alle offenen Laufzeitfragen.

---

## Historie: V4.30/V4.31 (2026-07-11 Vormittag)

### V4.29-Testauswertung (Log 08:19–09:17): QUEST-NAVI KOMPLETT BESTÄTIGT
- Quest-Kategorie + Probe: Marker „Willkommen in Gridania" terr=183 ==
  aktuell (Zonen-Feld KORREKT), pos-Y=-8.0 lief direkt (Höhe passt aufs
  Mesh) → beide offenen Laufzeitfragen positiv beantwortet.
- Ansage „1 von 1: …, 100 Meter, geradeaus" → Numpad 3 → 99,9m Auto-Lauf
  → „angekommen=True" (08:26:29). Positions-Variante (id=0) funktioniert.
- JournalDetail-Dump kam an (Strg+F5 dumpte Journal + JournalDetail).
- ABER: Journal/SystemMenu/SelectString-Listen WEITER STUMM → Root Cause
  gefunden (s.u.), Timing-Hypothese von V4.28 war falsch.

### Neu in V4.30: Listen-Erkennung repariert + Quest-Text vorlesen
1. ROOT CAUSE (endlich): FindListInAddon prüfte `Type != NodeType.Component`
   — Komponenten tragen aber ROHE Typwerte ≥1000 (Component=10000 gibt nur
   GetNodeType() zurück). Die Bedingung war IMMER falsch → universelle
   Listen-Navigation war seit Einführung TOTER CODE. Fix: ≥1000-Check wie
   im restlichen Code. Repariert Journal, SystemMenu, SelectString (der
   stumme Ja/Nein-Dialog aus dem 08:27-Dump) und jedes andere Listen-Menü.
   Doku: docs/game-api.md → „FALLE NodeType".
2. Strg+F10 im Journal liest jetzt die QUEST vor: Titel, Stufe,
   „Ziel: Mit Miounne sprechen", Beschreibung (JournalCanvas-Struktur
   aus dem Dump, docs/game-api.md → „Journal / JournalDetail").
3. vnavmesh meldet beim Boot FileNotFound für seine Config (vnavmesh.json
   fehlt) — harmlos (Erststart, Defaults), Auto-Lauf lief trotzdem.

### Neu in V4.31: NPC-Dialoge (User: „noch NIE Sprachausgabe gehört")
- Befund: Talk-Handler las nur 1× bei PostSetup via ReadFirstText
  (Node-IDs 2–12) — Talk setzt den Text aber erst NACH PostSetup und
  wechselt Dialogseiten im SELBEN Fenster → praktisch immer stumm.
  Zudem loggte er nichts (unsichtbar in jeder Diagnose).
- Fix: OnTalkUpdate (PostUpdate) liest jeden Frame ReadAllTexts, spricht
  nur bei ÄNDERUNG (Dedup pro Addon, Reset bei Unsichtbar/Close), loggt
  jede Ansage („{name} Dialog: '…'"). Gilt für Talk UND TalkSubtitle
  (Untertitel-Addon, tauchte 08:26:41 auf, hatte NIE einen Handler).
- AddonTalk-Struct hat nur unbenannte TextNode-Felder (AtkTextNode220…)
  → bewusst generisch gelesen statt Offsets zu raten.
- OFFEN (Log zeigt es): Tippt der Talk-Text buchstabenweise ein
  (Typewriter), spammen wachsende Ansagen → dann Stabilitäts-Check
  nachrüsten. Log-Zeilen verraten es sofort.

### Beim nächsten Start testen (V4.31)
1. Ansage „Version 4 Punkt 31 bereit".
2. NPC ansprechen (Numpad 0 auf Miounne/Bertennant): wird der Dialog-
   text VORGELESEN? Weiterklicken → jede neue Seite angesagt?
   Log-Kontrolle: Zeilen „Talk Dialog: '…'".
3. J → Journal: Pfeiltasten → jede Zeile angesagt („St. 1, Willkommen
   in Gridania")? Log: „List-Navigation".
4. Im Journal Strg+F10 → Quest komplett vorgelesen (Titel, Ziel,
   Beschreibung)?
5. Escape → SystemMenu: Pfeiltasten → „Ausloggen" usw. angesagt?
6. NPC-Dialog mit Auswahlliste → Optionen angesagt?
7. Fehler-Popup: weit entfernten NPC anvisieren, Numpad 0 → „zu weit
   entfernt" hörbar?

---

## Historie: V4.27–V4.29 (2026-07-10 spät)

### 🎉 MEILENSTEIN: Auto-Lauf via vnavmesh FUNKTIONIERT (User + Log 21:20/21:21)
- vnavmesh lädt jetzt: Profil-Eintrag IsEnabled musste auf true (Dalamud
  trägt neue Dev-Plugins by design mit false ein; dekompiliert verifiziert:
  PluginManager.LoadPluginAsync — IsEnabled=true + StartOnBoot=true → lädt).
  Config-Edit bei beendetem Spiel, BOM-frei; Backup
  dalamudConfig.json.bak-vor-vnavmesh-enable.
- 3 erfolgreiche Läufe im Log: „Pfad beendet, dist=2,4/2,5, angekommen=True"
  (Honoraint 33,6m, Bertennant 30,2m). Fehlerpfad davor auch sauber
  (21:10:18 „vnavmesh-IPC fehlgeschlagen" bei noch deaktiviertem Plugin).

### Neu in V4.27 (User-Feedback: „nicht immer Koordinaten ansagen")
- Während des Auto-Laufs sind die automatischen Zielwechsel-Ansagen
  STUMM (Plugin.cs: `_navigation.Update(… && !_autoWalk.IsActive)`).
  Ursache des Genervt-Seins: beim automatischen Laufen greift das Spiel
  laufend vorbeiziehende NPCs als Soft-Target → jedes wurde mit „Ziel:
  Name, Art, X Meter, Richtung" angesagt (Log 21:20:44, 21:20:59 …).
  Auto-Lauf-eigene Ansagen (Laufe zu/Ziel erreicht/gestoppt) bleiben.
  Dedup-Id läuft stumm mit → keine nachgeholte Alt-Ansage nach Ankunft.

### V4.27 BESTÄTIGT (User, 21:37): Auto-Lauf ohne Ansage-Spam. ✓

### Neu in V4.28: Journal + SystemMenu (Dump-Analyse 21:37/21:38)
Befund aus den User-Dumps:
- Journal (Taste J, „ARCHIV"): Quest-Liste ist Comp TreeList(12) mit
  ListItemRenderer-Zeilen (id=4 „St. 1", id=3 Quest-Name; Kategorie-
  Zeilen mit Gebiets-/Add-on-Namen). Wurde nie angesagt, weil
  FindListInAddon nur ComponentType.List akzeptierte.
- SystemMenu (Escape): normale List(9) mit 15 Einträgen [ListLen=15],
  aber bei PostSetup existierte die Liste noch NICHT (Fenstertitel kam,
  „Menü geöffnet" fehlt im Log; Dump 5s später zeigt die Liste) →
  Liste wird erst nach PostSetup aufgebaut.
Fixes (alle in UIReaderService):
1. FindListInAddon akzeptiert auch TreeList — Cast sicher, ilspycmd:
   AtkComponentTreeList [Inherits<AtkComponentList>(0)]
2. Späte Listen: OnAnyAddonUpdate prüft _noListCache-Addons jeden Frame
   erneut; sobald Liste da → PushMenu + Ansage „Eintrag, X Einträge"
3. ReadListItemText liest ALLE sichtbaren Texte der Zeile (Journal:
   „St. 1, Willkommen in Gridania" statt nur „St. 1"); NEU: unsichtbare
   Text-Nodes werden übersprungen (vorher zählten sie mit!)
4. Strg+F5 dumpt Begleitfenster mit: fokussiertes Addon + „…Detail"
   (Journal → JournalDetail = Quest-Beschreibung, Struktur noch unbekannt)

### Neu in V4.29: Quest-Ziele im Objekt-Browser (QuestMarkerService.cs neu)
1. Neue Kategorie „Quest-Ziele" (Strg+N bis zur Ansage): N/Umschalt+N
   blättert durch die Marker der ANGENOMMENEN Quests. Im Gebiet:
   „1 von 2: Questname, 150 Meter, links"; fremdes Gebiet: „… in einem
   anderen Gebiet."
2. Numpad 3 läuft zum gewählten Quest-Ziel (Positions-Variante des
   Auto-Laufs — Marker sind keine GameObjects, kein Target-Set).
   Fremdes Gebiet → klare Ansage statt Lauf (Zonen-Check frisch beim
   Tastendruck). Stop-Range = max(2,5m, Marker-Radius) — Questkreis
   betreten reicht (Radius kommt vom Spiel, kein Magic Value).
3. Quellen ilspycmd-verifiziert: Map.Instance()->QuestMarkers (30×
   MarkerInfo: Label, MarkerData-Vector mit Position/Radius/
   TerritoryTypeId@66) — docs/game-api.md → „Quest-Marker".
4. DEBUG-PROBE eingebaut: jeder N-Druck in der Kategorie loggt alle
   Marker ([Quest]-Zeilen: pos/r/terr/map/render) → klärt die zwei
   offenen Laufzeitfragen (Zonen-Feld korrekt? Y-Höhe auf Mesh?).
5. AutoWalkService refaktoriert: TryStartPath/BeginWalk gemeinsam,
   _destPosition (Objekt = jeden Frame nachgeführt, Position = fix),
   Beacon läuft jetzt auch ohne Objekt.

### Beim nächsten Start testen (V4.29)
1. Ansage „Version 4 Punkt 29 bereit".
2. JOURNAL (V4.28): J drücken → „ARCHIV" + Listen-Ansage? Pfeiltasten
   hoch/runter → jede Zeile angesagt? Im Journal Strg+F5 → Dump enthält
   jetzt AUCH JournalDetail (für Quest-Text-Vorlesen).
3. SYSTEMMENU (V4.28): Escape → beim Pfeiltasten-Navigieren werden
   „Ausloggen", „Systemkonfiguration" usw. angesagt?
4. QUEST-NAVI (V4.29): Quest annehmen (falls keine aktiv), Strg+N bis
   „Kategorie Quest-Ziele: …", N → Ansage mit Distanz? Numpad 3 →
   läuft hin + „Ziel erreicht"? Danach [Quest]-Logzeilen an Claude
   (Probe-Auswertung Zone/Höhe).
5. FEHLER-POPUPS (User-Frage): weit entfernten NPC anvisieren (N),
   Interaktion versuchen (Numpad 0) → wird „Das Ziel ist zu weit
   entfernt." gesprochen? (_TextError-Handler existiert seit früh,
   in-game NIE verifiziert!)
6. Falls Journal-Navigation stumm: SelectedItemIndex der TreeList
   trackt Tastatur evtl. nicht → Log an Claude, dann Highlight-
   NineGrid-Weg (Dump: gewählte Zeile hat id=7 NineGrid sichtbar).

### Offene Baustellen
- V4.25-Features ungetestet: Beacon-Lautstärke nach Distanz,
  Ablehnungs-Melder („Achtung, nicht anvisiert")
- Namenseingabe-Echo, Aussehen-Regler, Lumina ID→Name,
  Quest-NPC-Erkennung (Nameplate-Icon)

### vnavmesh-Installations-Krimi (GELÖST nach 3 Fehlversuchen): BOM!
- Dateien liegen korrekt in devPlugins\vnavmesh (v1.2.3.8, ApiLevel 15 ✓)
- Dev-Plugins lädt Dalamud NUR über DevPluginLoadLocations in
  dalamudConfig.json — Ordner allein reicht nicht
- Die per PowerShell geschriebene Config verschwand 3× „von Geisterhand":
  PS 5.1 schreibt UTF-8 MIT BOM → Dalamuds ReliableFileStorage liest rohe
  Bytes (kein BOM-Strip) → JsonReaderException → STILLER Fallback auf
  SQLite-Backup (dalamudVfs.db) → alter Stand überschreibt die Datei.
  Beweis: lokales Repro mit Dalamuds Serializer-Settings (scratchpad)
- Fix: Desktop\vnavmesh_aktivieren.ps1 schreibt jetzt BOM-los
  ([IO.File]::WriteAllText mit UTF8Encoding(false)) + prüft erstes Byte
- Stand JETZT: Eintrag drin, BOM-frei verifiziert (erstes Byte 0x7B),
  kein Spielprozess — Spielstart durch User steht aus
- Backups: dalamudConfig.json.bak-vor-vnavmesh (mehrfach überschrieben,
  enthält Vor-Edit-Stand des letzten Laufs)

### VORZEICHEN BESTÄTIGT (User-Hörtest V4.24): positiv = rechts stimmt,
Beacon-Panning wanderte auf die richtige Seite. Richtungssystem ist damit
KOMPLETT verifiziert (Nullpunkt per F-Snap-Log, Vorzeichen per User-Ohr).

### Neu in V4.26: Auto-Lauf zum Ziel (Numpad 3) via vnavmesh-IPC
1. Numpad 3 = automatisch zum aktuellen Ziel laufen (Toggle). vnavmesh
   findet den Weg übers Navmesh und steuert um Hindernisse herum.
2. Ansagen: „Laufe zu X" / „Ziel erreicht: X" / „Kein Weg zu X gefunden" /
   „Auto-Lauf gestoppt" / bei fehlendem vnavmesh klare Fehlermeldung.
3. Beacon läuft während des Auto-Laufs mit (Richtung+Distanz hörbar).
4. Gehhilfe und Auto-Lauf schließen sich gegenseitig aus (teilen den Ton).
5. Stoppt bis 2,5 m vor dem Ziel (Interaktionsreichweite); Ankunft wird
   per Distanz-Check verifiziert, sonst „Auto-Lauf beendet, noch X Meter".
6. Alle IPC-Signaturen quellcode-verifiziert: docs/game-api.md → vnavmesh-IPC
7. AUSSTEHEND: vnavmesh selbst installieren (Fremd-Plugin!). Download nach
   devPlugins wurde vorbereitet, wartet auf User-Entscheidung. Version
   1.2.3.8, ApiLevel 15, Quelle https://puni.sh/api/repository/veyn

### V4.24-Testergebnis (Log 16:36–16:42): GEHHILFE-VOLLTEST BESTANDEN
- RICHTUNGS-FIX BESTÄTIGT: nach F ist relAngle=0 (vorher 29°) — die
  V4.23-Formel stimmt. Ganzer Ablauf N→F→W→„angekommen, dist=2,9" lief!
- Ankunft + Auto-Aus funktionieren (16:38:16). Offene Frage an User:
  wurde die Ansage („Angekommen bei Bertennant") auch GEHÖRT?
- Vorzeichen (links/rechts) weiter unbestätigt — Frage an User: Drehung
  16:38:05 (vor dem F) — war das A oder D? rot fiel dabei (1,11→0,50)
- NEUER BUG ENTDECKT: 16:39:26–16:39:44 lehnte das Spiel ALLE Target-Sets
  ab (Hard-Target klebte auf Honoraint; jeder N-Druck sprach zusätzlich
  „Ziel: Honoraint" = Spam). Ursache unklar; SetHardTarget gibt bool
  zurück, Dalamud verwirft ihn (docs/game-api.md → „SetHardTarget kann
  ABLEHNEN"). Zeitgleich hing der User vermutlich an einem Hindernis
  (dist fror bei 28,5 m ein)

### Neu in V4.25 (User-Wünsche + Bugfixes)
1. Beacon-Lautstärke nach Distanz: volle Lautstärke ≤5 m, linear leiser
   bis 20% ab 80 m (User-Wunsch: näher = lauter)
2. Ankunfts-Ansage heißt jetzt „Ziel erreicht: Name" (User-Formulierung);
   Gehhilfe + Ton gehen dabei automatisch aus (war schon so)
3. Ablehnungs-Melder: nach jedem Target-Set liest das Plugin das Ziel
   zurück; bei Ablehnung Ansage „Achtung, nicht anvisiert" + Logzeile
   „[Nav] Target-Set ABGELEHNT" (sonst dreht F still zum FALSCHEN Ziel!)
4. Zielwechsel-Ansage feuert nur noch bei ECHTER Änderung des Ziels
   (Spam-Fix); manuelles Gehhilfe-Aus wird geloggt
5. OFFEN/RECHERCHE: Hindernis-Ausweichen gibt es nicht (Luftlinie!) —
   Stufe 2 Auto-Walk via vnavmesh-IPC steht als Recherche an

### Neu in V4.24: Audio-Beacon in der Gehhilfe (User-Wunsch)
1. Gehhilfe (Strg+Umschalt+N) spielt jetzt zusätzlich Piepser (2×/Sekunde,
   NAudio-Sinuston, jedes Frame aktualisiert — nicht nur alle 2s):
   - Ziel geradeaus = hoch (880 Hz) und mittig
   - Ziel seitlich = Ton wandert auf die Zielseite und wird tiefer
     (eine Oktave pro 90 Grad)
   - Ziel hinter dir = ganz tief (220 Hz), wieder mittig
2. Sprachansagen alle 2s bleiben; „Angekommen" prüft jetzt jedes Frame
3. Der Beacon ist gleichzeitig der VORZEICHEN-TEST: Wandert der Ton beim
   Rechtsdrehen (D) nach LINKS, stimmt alles. Wandert er nach RECHTS,
   ist links/rechts gespiegelt → melden, Fix ist eine Zeile
4. Lautstärke: Config BeaconVolume (0..1, Standard 0,35)
5. Kein Audiogerät → Ansage „Ton-Beacon nicht verfügbar", Sprach-Gehhilfe
   läuft trotzdem weiter
6. Technik: NAudio 2.2.1 (NuGet), CopyLocalLockFileAssemblies=true nötig
   (sonst landen NuGet-DLLs nicht im Output); BeaconService.cs neu
7. Nebenbei-Fix: Gehhilfe lief bisher nur, wenn AnnounceTargetChanges an
   war (Update()-Aufruf war daran gekoppelt) — jetzt entkoppelt

### V4.22-Testergebnis (Log 15:17–15:42): Browser ✓, Gehhilfe ✓, Richtung ✗ (Fix in V4.23)
- Objekt-Browser BESTÄTIGT: N/Umschalt+N blättern durch 10–11 NPCs
  (distanzsortiert, Umbruch am Ende), Ziel wird real gesetzt
- Zielwechsel-Ansage (V4.21-Fix) BESTÄTIGT: 15:26:33 echte Spiel-Targeting-
  Ansage („Ziel: Ulta") via ITargetManager — Tab/F12-Pipeline funktioniert
- Gehhilfe BESTÄTIGT: 2s-Ticks laufen; „Angekommen" ungetestet (nie ≤3m)
- ROTATIONS-KONVENTION GEKNACKT (Beweis im Log): F-Snap rastete 2× auf
  rot=-1,83 ein = exakte Zielrichtung atan2(dx,dz)=-105°=-1,83 rad →
  Blickvektor = (sin rot, cos rot). Alte Formel („0=Norden", atan2(dx,-dz))
  war eine SPIEGELUNG: Plugin sagte „leicht rechts" (29°), während der User
  nachweislich EXAKT aufs Ziel blickte. Details: docs/game-api.md →
  „Rotations-Konvention"

### Neu in V4.23: Richtungs-Fix
1. RelativeAngle nutzt jetzt `atan2(dx, dz) - rot` — Nullpunkt („geradeaus")
   ist damit log-verifiziert korrekt
2. OFFEN: Vorzeichen (positiv = rechts?) noch unverifiziert. TESTPLAN:
   a) N drücken, Richtungsansage merken (z. B. „links")
   b) A drücken/halten (links drehen), N erneut: Ansage muss Richtung
      „geradeaus" wandern — wenn sie Richtung „hinter" wandert, ist das
      Vorzeichen falsch (dann einfach melden, Fix ist eine Zeile)
   c) Gehhilfe-Log liefert Zweitbeweis (rot-Verlauf beim D-Halten)
3. Volltest Gehhilfe: N → F (hindrehen, muss jetzt „geradeaus" sagen!) →
   W bis „Angekommen bei …"

### V4.21-Testergebnis (Log 15:03–15:06): Tab/F weiter stumm — ABER unklar warum
- Sprachausgaben werden NICHT geloggt (TolkService loggt Speak nicht) →
  Log-Stille beweist nichts. V4.22 loggt jetzt [Nav]-Ereignisse.
- Verdacht: Tab schaltet nur GEGNER durch (TARGET_NEXT) — um den User
  herum waren nur friedliche NPCs (Bertennant, Ulta) → Tab tat evtl.
  gar nichts; F (FACE) dreht nur, sagt nie etwas. F12 (nächster NPC)
  wäre der richtige Test gewesen. NamePlate-Wechsel 15:05:41 zeigt
  UI-Aktivität, aber ohne [Nav]-Logs nicht zuordenbar.

### USER-WUNSCH (neu): Auswahl-System für Objekte in der Nähe + automatisch
hinlaufen. Stufe 1 (V4.22, gebaut): Browser+Gehhilfe (User läuft selbst mit W).
Stufe 2 (offen): echtes Auto-Laufen — Recherche nötig (vnavmesh-IPC vs.
Input-Injection; Workaround-Disziplin: erst Optionen sammeln, User fragen!)

### Neu in V4.22: Objekt-Browser + Gehhilfe (NavigationService)
1. N = nächstes Objekt der Kategorie (nach Distanz sortiert, ≤100m):
   visiert es WIRKLICH an (ITargetManager.Target = obj) + sagt
   „2 von 5: Name, Art, Distanz, Richtung". Umschalt+N = zurück.
2. Strg+N = Kategorie: Alles/NPCs/Gegner/Spieler/Objekte/Sammelpunkte/
   Ätheryten (Ansage mit Anzahl). „Ausgänge" gibt es in der ObjectTable
   NICHT — Recherche AgentMap/Map-Marker steht aus.
3. Strg+Umschalt+N = Gehhilfe: alle 2s „Distanz, Richtung", bei ≤3m
   „Angekommen" (auto-aus). Workflow: N → F (Spiel dreht hin) → W/R laufen.
4. Debug-Logs [Nav] für Zielwechsel/Auswahl/Gehhilfe-Ticks inkl. relAngle
   + rot → klärt endlich die Rotations-Konvention UND die Tab-Stummheit.
5. Konflikt-Check jetzt Modifier-exakt (Strg+F1 ≠ F1; die 10 „KONFLIKT"-
   Warnungen von 15:04 waren falsch-positiv, echte Konflikte: 0 erwartet)
6. Config-Felder umbenannt (KeyNextObject/KeyPrevObject/KeyCategory/
   KeyWalkGuide) — alte Namen fallen weg, Werte = Defaults

### V4.20-Testergebnis (Log 13:31–13:34): Dump ✓, Ziel-Ansage ✗ → Ursache gefunden
- Auto-Keybind-Dump LIEF: 171 Aktionen, 10 Konflikte, Datei auf Desktop —
  von Claude analysiert, Ergebnisse in docs/game-api.md → „Safe Mod Keys"
- Tab-Targeting gab KEINE Ansage. Ursache (Log: NamePlate-Fokus da, aber
  kein „Ziel:", kein Fehler): `LocalPlayer.TargetObject` trackt UI-Targeting
  nicht. Fix: Dalamud `ITargetManager.Target ?? SoftTarget` (V4.21, auch
  in SetTargetFromGameTarget gefixt)

### Neu in V4.21
1. Ziel-Ansage + Ziel-Verfolgung lesen jetzt ITargetManager (s.o.)
2. KOLLISIONSFREIE TASTEN (Dump-Ground-Truth: alle F1–F12 vom Spiel belegt,
   N = einziger freier Buchstabe). Neue Belegung:
   - N = Objekte in der Nähe; Umschalt+N = Richtung zum Ziel;
     Strg+N = Ziel verfolgen; Strg+Umschalt+N = Verfolgung beenden
   - Strg+F1 Hilfe, Strg+F2 Fenster, Strg+F5 UI-Dump, Strg+F10 Menü
     vorlesen, Strg+F11 Stille, Strg+F12 Kampfstatus
   - ACHTUNG WORKFLOW: Dumps in der Lobby jetzt Strg+F2/Strg+F5 statt F2/F5!
3. IsJustPressed kann Modifier (exakte Übereinstimmung, Edge-Detection
   1× pro Frame und VK — vier Funktionen teilen sich Taste N)
4. Config-Migration V1→V2 setzt alte F-Tasten-Belegung automatisch um
5. Hilfe-Ansagen (Strg+F1, /acc help) nennen die neuen Tasten

### DURCHBRUCH-ERKENNTNIS: Offizielle Tastenliste (User-Link, dokumentiert
in docs/game-api.md → „Offizielle Standard-Tastaturbelegung")
- ALLE F-Tasten F1–F12 sind vom Spiel belegt (F1=selbst, F2–F8=Gruppe,
  F9=Begleiter, F10=Fokus, F11=nächster GEGNER, F12=nächster NPC/OBJEKT)
- → Spiel hat EINGEBAUTE Navigations-Tasten (F11/F12/Tab)! Unser Job ist
  nur die Ansage. Plugin-Tasten müssen umziehen (Kandidaten: N, NUM3,
  Strg/Umschalt+F-Kombis — Dump abwarten, IsJustPressed kann noch keine Modifier)

### Neu in V4.20: Ziel-Ansage bei Zielwechsel (NavigationService.Update)
Bei jedem Zielwechsel automatisch: „Ziel: Name, Art, Entfernung, Richtung."
Damit sind Tab, F1–F12, T sofort blind nutzbar — Navigation über die
spieleigenen Targeting-Tasten statt eigener Parallel-Mechanik.
Art aus Dalamud ObjectKind (Pc/BattleNpc/EventNpc/Treasure/Aetheryte/
GatheringPoint/EventObj/Mount/Companion/Retainer — ilspycmd-verifiziert).
Abschaltbar per Config AnnounceTargetChanges. Richtungsworte weiter
unter Rotations-Vorbehalt (s.u.).

### Neu in V4.19: Keybind-Dump läuft AUTOMATISCH nach dem Login
User kann den Chat noch nicht öffnen (`/acc keys` unerreichbar) → das Plugin
dumpt jetzt einmal pro Sitzung automatisch, sobald eingeloggt und die
Keybind-Tabelle lesbar ist (KeybindService.IsReady, Retry pro Frame bis
bereit). Ansage: „Tastenbelegung gespeichert: … Konflikte …".
Chat-Öffnen-Frage des Users: Standard-Taste ist Enter (CMD_CHAT) —
wird durch den Dump bestätigt. ABER: NVDA liest getippte Zeichen im
Spiel-Chat nicht (gleiche Lücke wie Namensfeld → Textfeld-Echo-Baustelle).

### User-Richtung: Tutorial läuft (wird vorgelesen!), jetzt gewünscht:
1. Alle Spiel-Tasten dokumentieren (Gate Check für neue Mod-Tasten)
2. Navigationssystem: NPCs, Gegenstände, Quest-Ziele finden (Orientierung)

### Neu in V4.18: `/acc keys` — Spiel-Tastenbelegung dumpen (KeybindService)
- Liest die LIVE-Keybind-Tabelle aus dem Spiel:
  `UIInputData.Instance()->InputData.GetKeybindSpan()` (ilspycmd-verifiziert,
  dokumentiert in docs/game-api.md → „Keybind-System").
- Schreibt alle belegten Aktionen nach `Desktop\FFXIV_Keybinds.txt`
  (Format: `AKTION (InputId): Taste1 ; Taste2`, deutsche Modifier Strg/Umschalt/Alt).
- Konflikt-Check: welche Spiel-Aktionen liegen auf unseren Plugin-Tasten
  (F1–F12)? → Zeilen `KONFLIKT F1 (...)` in Datei + Log.
  VERDACHT: F1–F8 = TARGET_P1–P8 (Gruppenmitglieder) → unsere Tasten
  kollidieren in-game. Dump liefert Ground Truth, dann Tasten neu wählen.

### Beim nächsten Start testen (V4.22)
1. Ansage „Version 4 Punkt 22 bereit".
2. N drücken → „1 von X: Name, Art, Distanz, Richtung"? Mehrfach N →
   blättert weiter? Umschalt+N zurück?
3. Strg+N → „Kategorie NPCs: X in der Nähe"?
4. Objekt wählen (N), dann Strg+Umschalt+N → „Gehhilfe an", F drücken
   (dreht hin), W halten → alle 2s Ansage, am Ende „Angekommen"?
5. F12 → „Ziel: …"-Ansage (Spiel-eigenes Targeting)?
6. Danach Log an Claude: [Nav]-Zeilen klären Rotations-Konvention
   (relAngle sollte beim Zulaufen gegen 0 gehen).
3. V4.17-Punkte weiter offen: kein _LimitBreak-Logspam mehr? Enter im
   Namensdialog (falls neuer Charakter)?
4. Navigation antesten (existiert schon als Befehle, nie in-game getestet):
   `/acc near` (Objekte in der Nähe), Ziel anvisieren + `/acc set`, dann
   `/acc nav` (Richtung+Distanz). ACHTUNG: Richtungsansage (links/rechts/
   geradeaus) basiert auf unverifizierter Rotations-Konvention — bitte
   testen: NPC anvisieren, hindrehen, `/acc nav` → sagt er „geradeaus"?

### Danach (Navigations-Ausbau, nach Keybind-Analyse)
- Sichere Mod-Tasten festlegen, F1–F12-Belegung ggf. umziehen
- Kategorien-Navigation: NPCs / Questgeber / Gegenstände / Aetheryten
  getrennt durchblättern (ObjectKind-Filter), Uhrzeiger- statt
  links/rechts-Ansage, Quest-Marker aus AgentMap lesen (recherchieren)
- Alte Baustellen: SelectString/SystemMenu-Dumps (Log 11:42/11:44),
  Namenseingabe-Echo, Aussehen-Regler, Lumina ID→Name

---

## Historie: V4.17 (2026-07-10 Mittag)

### 🎉 MEILENSTEIN: Charakter erstellt, User ist IM SPIEL (Log 11:16–11:44)
Kompletter CharaMake-Durchlauf hat funktioniert; danach In-Game-Fenster
(SelectString 11:42, SystemMenu 11:44 — F5-Dumps davon liegen im Log!).
Keine Fehler/Exceptions in der ganzen Session.
V4.17-TEIL-BESTÄTIGUNG (mündlich, 2026-07-10 Nachmittag): Tutorial läuft,
Anweisungen werden vorgelesen. Logspam/Namensdialog noch nicht rückgemeldet.

### V4.16-Testergebnisse (Log 11:00–11:16) — fast alles bestätigt
- **Geschlecht KORREKT:** „Hyuran, männlich" angesagt; Widerspruchs-Zeile
  bestätigt erneut: Checkbox-Symbol hätte fälschlich „weiblich" gesagt,
  sichtbares Modell (Sex=0) gilt. Ground-Truth-Weg funktioniert.
  (User hat nicht getoggelt — dank Ground Truth aber egal, Label stimmt immer.)
- **Volksstamm-Handler BESTÄTIGT:** „Wiesländer" gewählt angesagt,
  Hover Hochländer/Wiesländer/Ok funktioniert.
- **Enter=Ok BESTÄTIGT** auf _CharaMakeFeature (param=37) und
  _CharaMakeRaceGender (param=28) — Event-Dispatch-Klick funktioniert!
- **Enter=Ok FEHLTE im Namensdialog:** _CharaMakeCharaName hat keinen
  „Ok"-Button — der Knopf heißt „Bestätigen" (node id=16, Zurück=id=3;
  User musste mit Maus klicken, Log 11:15:48).
- **In-Game-Logspam:** _LimitBreak feuert TimelineActiveLabelChanged 3×
  pro Frame, _ScreenInfo* TimerTick → ~98.000 Zeilen in 26 Minuten.

### Neu in V4.17 (beide Punkte gefixt)
1. `ConfirmButtonLabels = ["Ok", "Bestätigen"]` — Enter drückt jetzt auch
   den Bestätigen-Knopf im Namensdialog.
2. `IgnoredEventTypes` + 64/65/66/74 (TimerTick/End/Start,
   TimelineActiveLabelChanged, Werte per ilspycmd) — Log-Spam weg; die
   Events sind reine Animations-/Timer-Ticks, nie Navigation.

### Beim nächsten Start testen (V4.17)
1. Ansage „Version 4 Punkt 17 bereit".
2. Im Spiel: Log darf nicht mehr fluten (kein _LimitBreak-Spam).
3. Falls neuer Charakter-Durchlauf: Enter im Namensdialog → „Ok"-Ansage
   und Dialog bestätigt?
4. In-Game-Menüs erkunden (Escape → SystemMenu, NPC-Dialoge): was wird
   angesagt, was ist stumm? F2/F5 auf stummen Fenstern.

### Nächste Baustellen (In-Game-Phase!)
- SelectString- und SystemMenu-Dumps aus dem Log analysieren (11:42/11:44)
- Namenseingabe-Echo (getippte Zeichen ansagen) — für den nächsten Charakter
- Aussehen-Feinauswahl (CMFSlider/CMFIcon*/CMFColorL — Dumps vorhanden)
- Alte Liste: Cooldown-Ansagen, Audio-Beacon, Marktbrett, Inventar,
  Zielverfolgung per Name

---

## Historie: V4.16 (2026-07-10 Vormittag)

### V4.15-Testergebnisse (Log 10:18–10:22) — Probe erfolgreich, Session sehr ergiebig
- **Sichtbarkeits-Probe funktioniert:** genau 1 von 32 Vorschau-Modellen
  sichtbar (`Vorschau sichtbar: [200] Sex=0`); die 31 versteckten tragen
  RenderFlags=0x40. Damit haben wir Ground Truth fürs angezeigte Modell.
- **Indiz für VERTAUSCHTE Zuordnung:** Beim Öffnen war Checkbox id=3 (©)
  checked → Plugin sagte „weiblich", aber das sichtbare Modell hatte Sex=0
  (=männlich). Nur 1 Datenpunkt (User hat nicht getoggelt, Modell könnte
  beim Öffnen theoretisch nachhinken) → nicht hart geflippt, sondern:
- **User hat F2/F5-Dumps geliefert:** _CharaMakeTribe + _CharaMakeProgress
  (10:20:10), _CharaMakeFeature + CMFSlider (10:20:38), CMFColorL (10:21),
  SelectYesno (10:22) — alle im Log, analysiert: siehe docs/game-api.md
  (NEU angelegt, CharaMake-Sektion).

### Neu in V4.16
1. **Geschlechts-Ansage = Sex-Byte des SICHTBAREN Vorschau-Modells**
   (V4.14-Idee, aber jetzt das richtige Objekt). Checkbox bleibt
   Änderungs-Detektor + Fallback-Label; Widerspruch wird geloggt
   (`RaceGender: Vorschau-Sex=.. widerspricht Checkbox-Symbol ..`).
   Probe läuft nur bei Auswahl-Änderung — kein Spam.
2. **_CharaMakeTribe-Handler** (Volksstamm, der erste bisher stumme Schritt):
   dediziert in SpecialUpdateAddons; Hover per Event-Target
   („Hochländer"/„Wiesländer"/Ok/Zurück), Auswahl-Änderung per checked
   Top-Level-CheckBox (Label ≥2 Zeichen filtert die ®/©-Glyphen-Boxen).
3. **Enter = Ok in Lobby/Charaktererstellung** (User-Wunsch, alter offener
   Punkt „Taste für Ok im DC-Fenster"): `PressFocusedOk` sucht im obersten
   fokussierten _CharaMake*/CharaMake*/TitleDCWorldMap-Addon den sichtbaren
   Button „Ok" und feuert dessen registriertes ButtonClick-Event an den
   Listener — derselbe Weg wie ein echter Mausklick, KEIN Callback-Raten.
   (Alle Structs per ilspycmd verifiziert: AtkEventManager.Event-Kette,
   AtkEvent.State.EventType/Param/Listener, ReceiveEvent, AtkEventData=40B,
   ButtonClick=25.) Reihenfolge wichtig: von HINTEN durch die Fokus-Liste,
   damit der Schritt-Ok und nicht der finale Progress-Ok getroffen wird.
   Ansage „Ok" bei Erfolg, „Kein Ok-Knopf gefunden" sonst. Enter behält
   überall sonst seine Spielfunktion (Whitelist).

### Beim nächsten Start testen (V4.16)
1. Ladeansage „Version 4 Punkt 16 bereit".
2. Volk & Geschlecht: Geschlecht MEHRMALS mit Links/Rechts wechseln —
   wird es angesagt und stimmt männlich/weiblich jetzt? (Im Log muss
   `Vorschau sichtbar` bei jedem Wechsel das Sex-Byte umschalten.)
3. **Enter drücken** bei Volk & Geschlecht: sagt „Ok" und wechselt zum
   Volksstamm? (Falls „Kein Ok-Knopf gefunden" → Log zeigt warum.)
   Dann beim Volksstamm: Hoch/Runter → werden „Hochländer"/
   „Wiesländer" (bzw. Stämme des gewählten Volks) angesagt? Maus-Hover?
   Wieder mit Enter weiter. ACHTUNG bei der Namenseingabe: Enter könnte
   dort doppelt wirken (Spiel-Enter im Textfeld + unser Ok) — Log prüfen.
4. Weiter durchklicken: Aussehen usw. — F2/F5 auf allem was stumm ist
   (Dumps von Feature/Slider/Farbe sind schon da, aber mehr schadet nicht).
5. Log an Claude.

### Danach geplant
- _CharaMakeFeature/CMFSlider/CMFColorL-Handler (Dumps liegen schon im
  Log vom 10.07., Analyse siehe docs/game-api.md)
- _CharaMakeProgress vorlesen (Schritt-Übersicht mit aktuellen Werten)
- Namenseingabe-Echo (_CharaMakeCharaName): getippte Zeichen ansagen —
  AtkComponentTextInput vorher per ilspycmd verifizieren
- Lumina ID→Name (IDataManager, Excel-Sheets)

---

## Historie: V4.15 (2026-07-10)

### V4.14-Testergebnisse (Log 2026-07-10 09:46) — Ansatz widerlegt
- Die Probe zeigte: **32 Pc-Objekte** (Indizes 200–231, Sex abwechselnd 0/1,
  keine Namen) = alle 8 Völker × 2 Stämme × 2 Geschlechter sind GLEICHZEITIG
  in der ObjectTable. Es gibt nicht „den einen" Vorschau-Charakter.
- Folge: Code nahm das erste Pc (Index 200, immer Sex=0) → Geschlecht hing
  auf „männlich" fest, Links/Rechts sagte NICHTS mehr an (Rückschritt zu V4.13).
- Nebenbefund: Kontroll-Logzeile feuerte jeden Frame → 10.792 Spam-Zeilen.

### Neu in V4.15: Checkbox als Quelle zurück + Sichtbarkeits-Probe
- Geschlecht kommt wieder aus den Checkboxen (id=4/id=3) — Wechsel-Erkennung
  in V4.13 nachweislich zuverlässig. Offen bleibt NUR die Label-Zuordnung.
- Neue Probe `LogPreviewActors` (bei jeder Auswahl-Änderung): loggt pro
  Pc-Objekt Sex, RenderFlags und `DrawObject.IsVisible` (Felder verifiziert
  per ilspycmd: GameObject.DrawObject@256, RenderFlags@280, VisibilityFlags
  None=0/Model=2/Nameplate=0x800; DrawObject.IsVisible existiert).
- HYPOTHESE: genau EIN Vorschau-Modell ist sichtbar; sein Sex-Byte beim
  Geschlechtswechsel bestätigt (oder widerlegt) id=4=männlich.
- Log-Spam behoben: Probe läuft nur bei Änderung, Voll-Dump 1× pro Screen.

### Beim nächsten Start testen (V4.15)
1. Ladeansage „Version 4 Punkt 15 bereit".
2. Charaktererstellung → Volk wählen, dann Geschlecht mehrmals mit
   Links/Rechts wechseln (wird wieder angesagt wie in V4.13).
3. Danach Log an Claude: Zeilen `Vorschau sichtbar: [idx] Sex=..` —
   wechselt das Sex-Byte des sichtbaren Modells synchron mit der Ansage?
   - Sichtbar-Sex=0 wenn „männlich" angesagt → Zuordnung KORREKT.
   - Umgekehrt → Zuordnung drehen.
   - „KEINES" oder 32 sichtbar → IsVisible taugt nicht, anderer Weg nötig.
4. DANN WEITERKLICKEN: Mit Ok zum nächsten Bildschirm (Aussehen usw.).
   Auf JEDEM stummen Bildschirm F2 (Fenstername) + F5 (Struktur-Dump)
   drücken — User-Befund 2026-07-10: alles nach Volk/Geschlecht ist noch
   stumm (Geburtsdatum, Schutzgott, Startklasse, Stadt haben keine Handler).
   Dumps aus dem Log → Handler bauen.

### Offene Frage des Users: Namenseingabe
Das Eingabefeld für den Charakternamen kommt am Ende der Charaktererstellung.
Fenstertitel „Name des Charakters" + Ok/Abbrechen werden schon angesagt
(V4.10 bestätigt). FEHLT: Echo der getippten Zeichen (NVDA liest das
Spiel-Textfeld nicht mit). Nächstes Feature nach dem V4.15-Test:
Textfeld-Inhalt bei Änderung ansagen (AtkComponentTextInput o.ä. — vorher
per ilspycmd verifizieren).

---

## Historie: V4.13 (2026-07-09)

### V4.12-Testergebnisse (Log 22:17) — teils Erfolg
- **Geschlechts-Ansage funktioniert:** Links/Rechts sagt männlich/weiblich. ✓
- **Völker WERDEN erkannt** (V4.11 Event-Target): alle 8 kommen als
  „Fokus via Event-Target: 'Miqo'te…', 'Roegadyn…'" — ABER mit kaputten
  Symbolen dran, und…
- **…„Zurück"-Spam:** Nach jeder Volk-Ansage feuerte der generische Update-
  Handler `FindFocusedText` → Collision-Heuristik fand immer den Zurück-Button
  (Key=19004) → sagte „Zurück" und überdeckte das Volk. Deshalb hörte der User
  bei Hoch/Runter nur „Zurück".

### Neu in V4.13: RaceGender komplett dediziert
- `_CharaMakeRaceGender` ist jetzt in SpecialUpdateAddons → beide generischen
  Pfade (Update = Zurück-Spam, ReceiveEvent) sind aus.
- Dedizierter `OnRaceGenderReceive` (MouseOver): sagt das Volk via Event-Target
  an, gesäubert mit CleanRaceName („Miqo'te\t glyphs" → „Miqo'te").
- `OnRaceGenderUpdate` (Geschlecht) bleibt unverändert.
- Erwartung: Hoch/Runter (Volk wählen) → gewählter Zustand ändert sich →
  OnRaceGenderUpdate sagt „Volk, Geschlecht"; Maus-Hover → sauberes Volk;
  Links/Rechts → Geschlecht. Kein „Zurück"-Spam mehr.

### V4.13-Testergebnisse (Log 22:24/22:25) — ERFOLG
- Kein „Zurück"-Spam mehr. ✓
- Völker sauber angesagt: „Hyuran", „Elezen", „Lalafell", „Miqo'te"
  (CleanRaceName inkl. Apostroph korrekt). ✓
- Volk+Geschlecht bei Auswahl: „Lalafell, männlich/weiblich" schaltet um. ✓
- Symbol-Codepoints geloggt: id4(als männlich)=C2 AE (®, U+00AE),
  id3(als weiblich)=C2 A9 (©, U+00A9).

### V4.14: Geschlecht aus Vorschau-Charakter (Daten-Weg statt Symbol)
Statt die kaputten Symbole zu deuten, liest das Plugin jetzt das echte
`Sex`-Byte (0=männlich, 1=weiblich, FFXIV-Standard) des Vorschau-Charakters
aus der Dalamud-Objekttabelle (`Character.Sex`). Symbol-Zuordnung bleibt nur
als Fallback. Cross-Check wird geloggt (Vorschau-Sex vs Symbol).
- Recherche-Fakten: `CustomizeData` hat Sex@1/Race@0/Tribe@4 (0/1 Bytes);
  `Character.Sex` (geerbt von GameObject) direkt lesbar; kein sauberer Live-
  Zeiger auf die CharaMake-Customize (AgentLobby/CharacterManager) → daher
  Vorschau-Charakter über ObjectTable.
- **PROBE im Build:** Beim Öffnen von RaceGender werden ALLE Objekte einmal
  geloggt (`CharaMake-Objekt[idx]: 'Name' Kind=.. Sex=..`), um den Vorschau-
  Charakter zu identifizieren. HYPOTHESE: Vorschau = ObjectKind.Pc.

### Beim nächsten Start testen (V4.14)
1. Ladeansage „Version 4 Punkt 14 bereit".
2. Charaktererstellung → Volk & Geschlecht: Geschlecht mit Links/Rechts
   wechseln → wird männlich/weiblich angesagt und stimmt es jetzt?
3. WICHTIG: Log ansehen — steht dort `CharaMake-Objekt[..] Kind=Pc Sex=..`?
   Falls die Objektliste LEER ist oder kein Pc dabei: Vorschau ist nicht in
   der ObjectTable → anderer Weg nötig (ClientObjectManager direkt).
4. Danach Log an Claude.

### DANACH (zugesagt): Lumina ID→Name einrichten
IDataManager durchreichen, Excel-Sheets nutzen (Race, Item, Action …) um
IDs in saubere Namen zu übersetzen statt kaputten UI-Text zu lesen. Kommt
nach dem Vorschau-Charakter-Test, um nicht 2 ungetestete Brocken zu stapeln.

### (V4.12 war) Geschlechts-Ansage in der Charaktererstellung
- Dedizierter PostUpdate-Handler `OnRaceGenderUpdate` für `_CharaMakeRaceGender`.
- Liest den ECHTEN Auswahlzustand statt die kaputten Symbole zu deuten:
  jedes Volk (Comp CT=Base) hat zwei Geschlechts-Checkboxen — Node id=4
  (männlich-Symbol) und id=3 (weiblich-Symbol). `AtkComponentCheckBox.IsChecked`
  (verifiziert per ilspycmd) verrät das gewählte Geschlecht.
- Ansage bei Änderung: Volkwechsel → „Viera, weiblich"; nur Geschlecht
  (Links/Rechts) → „männlich"/„weiblich". Volksname wird von Symbolen
  gesäubert (CleanRaceName: nur Buchstaben/Leer/Apostroph bis zum Tab).
- **ANNAHME (Test!):** id=4 = männlich, id=3 = weiblich (FFXIV-Konvention
  männlich zuerst). Codepoints beider Symbole werden EINMAL geloggt
  (`RaceGender-Symbole: id4(als männlich)='..' id3(als weiblich)='..'`),
  damit der Testlauf die Zuordnung bestätigt.
- **HYPOTHESE (Test!):** Das gewählte Volk hat eine checked Geschlechts-
  Checkbox. Falls im Log keine `RaceGender gewählt`-Zeile erscheint, stimmt
  das nicht → Zustand liegt woanders (dann neu ansetzen).
- Encoding-Trap behoben: In UIReaderService.cs war in CleanRaceName ein
  U+2000-Space statt 0x20 gerutscht (Edit-Matching scheiterte) — per awk
  byte-sicher ersetzt.

### Beim nächsten Start testen (V4.12)
1. Ladeansage „Version 4 Punkt 12 bereit".
2. Charaktererstellung → Volk & Geschlecht: durch die 8 Völker gehen
   (werden sie angesagt? — V4.11-Fix) und mit Links/Rechts das Geschlecht
   wechseln → wird „männlich"/„weiblich" angesagt?
3. WICHTIG: Einmal auf ein dir bekanntes Geschlecht stellen und sagen, was
   angesagt wurde → so bestätigen wir die männlich/weiblich-Zuordnung.
4. Danach Log an Claude („schau in die log").

## (vorher) Version 4.11

### V4.10-Testergebnisse (Log 21:43–21:45) — vieles bestätigt
- **Fenstertitel funktioniert:** „Name des Charakters" (_CharaMakeCharaName),
  „Weltenauswahl" (_CharaSelectWorldServer) werden angesagt. ✓
- **SelectYesno echte Labels:** „Die Charaktererschaffung abbrechen? … 
  Buttons=[Ok|Abbrechen]". ✓
- **Callback-Zuordnung BESTÄTIGT:** ButtonClick param=1 (Abbrechen) → nichts;
  param=0 (Ok) → zurück zur Charakterauswahl. Also id=8/„Ok" = Index 0 stimmt. ✓
- **ABER Charaktererstellung stumm:** In `_CharaMakeRaceGender` (8 Völker:
  Viera, Hrothgar, Au Ra, Roegadyn, Miqo'te, Lalafell, Elezen, Hyur) feuern
  MouseOver param 1–8, aber NICHTS wird angesagt.

### Root Cause (gefunden) → Fix in V4.11
- `_CharaMakeRaceGender`: jedes Volk ist ein Comp(1003)-Node; dessen Kind
  id=4 trägt ein statisches Collision-Bit (0x10). FindFocusedText Durchlauf 2
  (Collision-Fallback, eig. für DC gedacht) matchte deshalb IMMER dasselbe
  Volk — unabhängig vom MouseOver → Dedup unterdrückte alles.
- **V4.11-Fix:** Bei MouseOver(6)/ButtonClick(25) hat jetzt der Event-Target-
  Pointer VORRANG (TryAnnounceEventTarget gibt bool zurück); nur wenn er nicht
  mappt, greift die Flag-Heuristik FindFocusedText. Das ist das verifizierte
  DC-Muster, jetzt als bevorzugter Pfad für alle Addons.

### Offene Annahme / evtl. nächster Schritt
- Ob der Event-Target bei RaceGender auf die richtige Komponente zeigt und
  „Viera" etc. sauber vorliest, zeigt erst der nächste Log. Der Volkstext
  enthält im Dump kaputte Geschlechtssymbole („Viera\t © ®") — Encoding
  ggf. später säubern. Erst Log ansehen, dann entscheiden.
- CMFSlider/CMFIcon* (Aussehen-Regler) sind der übernächste Brocken.

### Frühere bestätigte Ergebnisse
- V4.6: Welten-Ansage nach DC-Klick; Geplapper in Charaktererstellung weg.
- V4.7: F2 / `/acc win`. V4.8: F5 dumpt alle fokussierten Fenster.
- V4.10: Fenstertitel + echte Dialog-Knöpfe + Klick queued statt unterbricht.
- Noch offen: Taste für den Ok-Knopf im DC-Fenster.
- Hinweis: F2 = Spiel-Standardtaste „Gruppenmitglied 2" (im Kampf ggf. Konflikt).

### Beim nächsten Start testen
1. Ladeansage „Version 4 Punkt 11 bereit".
2. Charaktererstellung öffnen, mit Pfeiltasten/Maus durch die 8 Völker:
   werden sie jetzt einzeln angesagt (Viera, Hrothgar, …)?
3. Falls ja: klingt der Volksname sauber oder mit Zusatz-Zeichen?
4. Weitere Regler/Icons (CMFSlider, CMFIcon*) durchgehen — was kommt?
5. Danach Log an Claude („schau in die log").

### Wichtige Betriebs-Fakten
- Dalamud lädt das Plugin DIREKT aus `H:\ff14\FF14Accessibility\bin\Debug\net10.0-windows\`
  (DevPluginLoadLocations in dalamudConfig.json). Der devPlugins-Ordner ist NICHT
  die Ladequelle. **Nach jedem Build: Spiel komplett neu starten.**
- Build: `$env:DALAMUD_HOME = "C:\...\XIVLauncher\addon\Hooks\dev"; dotnet build H:\ff14\FF14Accessibility\FF14Accessibility.csproj`
- Versionskennung: EINE Konstante in Plugin.cs (`PluginVersion`/`PluginVersionTag`)
  speist Log-Zeile UND Sprachansage. Bei jeder Code-Änderung hochzählen — nur so
  ist zweifelsfrei erkennbar, welcher Build im Spiel läuft.
- **F5** = Node-Dump des aktiven Addons → `FFXIV_UI_Dump.txt` auf dem Desktop.
  Bestes Diagnose-Werkzeug: User drückt F5 im fraglichen UI-Zustand.
- Git: Letzter Commit 2026-05-30, große ungecommittete Änderungen im Arbeitsverzeichnis.
- UIReaderService.cs hat gemischte Zeichenkodierung (alte Umlaute als kaputte
  Bytes). Bei Edits: old_strings ohne Umlaute wählen. Neue Sprach-Strings nur
  über `AccessibilityStrings.cs` (sauberes UTF-8, DE/EN).

## Heute behoben (2026-07-09) — Details

### 1. Debug-Deploy kopierte nie die neue DLL (csproj)
`ResolvedOutDir` wurde aus `$(OutDir)` in einer statischen PropertyGroup berechnet —
bei SDK-Projekten ist `$(OutDir)` dort noch leer. Es wurde nur die Manifest-JSON
kopiert, die Erfolgsmeldung prüfte bloß die Existenz irgendeiner DLL am Ziel.
Fix: Berechnung IM Target; Meldung basiert auf `CopiedFiles`; nichts kopiert = Build-Fehler.
(Anmerkung: Da Dalamud ohnehin aus bin/ lädt, war das nicht die Ursache des
Hauptproblems — aber der Deploy ist jetzt ehrlich.)

### 2. IsReadable()/VirtualQuery war IMMER kaputt (UIReaderService)
MEMORY_BASIC_INFORMATION war mit Size=44 statt 48 deklariert (4 Padding-Bytes
fehlten). Windows lehnt zu kleine Puffer mit ERROR_BAD_LENGTH ab → `IsReadable()`
gab seit Einführung für JEDEN Pointer false zurück. Alle abgesicherten Lesepfade
(ReadListItemText, DC-Zuordnung, …) liefen still auf Leer-Fallbacks.
Bewiesen per Standalone-Repro (dwLength=44 → ret=0/err=24; 48 → ok).
**Lehre: Sicherheitsnetz-Funktionen brauchen einen Positivtest. Und: stille
Frühausstiege sind verboten — jeder Exit loggt seinen Grund (hat hier einen
kompletten Testlauf gekostet).**

### 3. DC-Auswahl innerhalb einer Region — BESTÄTIGT funktionierend (Log 19:44)
Links/Rechts zwischen Light/Chaos wird angesagt. Mechanik: MouseOver-Event
(Typ 6) → `AtkEvent->Target` per Pointer-Vergleich gegen die beim Öffnen
gesammelten DC-Buttons (Comp 1015) matchen, Text frisch vom Node lesen.
WICHTIG: `AtkEvent->Node` ist bei diesem Addon 0x0 — immer BEIDE Pointer
(Node UND Target) prüfen. Event-Parameter sind KEINE Node-IDs (Regionen:
1=Japan, 7=Europa, 13=Ozeanien, 19=Nordamerika als Fallback-Map verifiziert;
DC-Buttons z.B. 9/10 in Europa, undokumentiert → nur Pointer-Match nutzen).

### 4. Gesprochene Version war hartcodiert „4.1"
Zweiter, separater String neben der Log-Zeile. Jetzt eine gemeinsame Konstante.

### 5. Log-Spam entfernt (V4.5) + Fokus-Dedup pro Addon (V4.6)
- Titelmenü loggte 60 Zeilen/s (per-Frame-Debug in AnnounceTitleMenuFocusIfChanged) — entfernt.
- Charaktererstellung: mehrere gleichzeitig sichtbare Addons (CMFSlider, CMFIcon*)
  überschrieben den GLOBALEN Fokus-Dedup-Zustand gegenseitig → jede Ansage feuerte
  jeden Frame (Dauergeplapper). Fix: `_lastFocusByAddon` Dictionary pro Addon-Name,
  Aufräumen bei Addon-Close. TEST AUSSTEHEND.

## Neu in V4.6: Welten-Ansage nach DC-Klick (TEST AUSSTEHEND)

Node-Dump 2026-07-09 (Zustand nach Klick auf Light) hat die Struktur geklärt:
- Im Region-Panel steht in der NodeList direkt VOR jedem DC-Button (Comp 1015)
  dessen Welten-Liste (Comp 1019): vor „Light" Alpha..Zodiark, vor „Chaos"
  Cerberus..Spriggan. Die Listen werden erst beim Öffnen der Region befüllt.
- Im Panel gibt es einen „Ok"-Button (Comp 1006) zum Bestätigen.
Implementierung: ButtonClick (Event-Typ 25) → `TryAnnounceDCSelection` matcht
per Pointer, liest Welten FRISCH aus der 1019-Liste (nie cachen), Ansage via
`AccessibilityStrings.DCSelected`. Region-Klicks/Ok-Klicks landen im Log als
„ButtonClick not mapped" — normal.

## UIReaderService – Architektur

### Menü-Stack
- `_menuStack` (Stack): PostSetup = Push + vorlesen, PreFinalize = Pop,
  PostUpdate nur fürs oberste Element (SelectedItemIndex-Änderung → ansagen),
  PostReceiveEvent = Fallback für Nicht-Listen-Addons.
- Fokus-Dedup: `_lastFocusByAddon` (pro Addon-Name; NIE wieder global).

### Spezial-Addons
- Talk, SelectYesno: eigene Handler, kein Stack-Eintrag.
- SelectString/SelectIconString: eigener PostSetup-Handler (Fragetext + Liste),
  dann Stack + universelles Update.
- TitleDCWorldMap: eigener Handler-Satz (s.o.), `IsDCMapOpen` für Plugin.cs.
- ConfigSystem: Rückwärts-Scanning (`HasFocusBit` bei Child-Nodes) für Slider,
  Dropdowns, Checkboxen; eigener Text-Cache.
- Benachrichtigungen: eigene Handler.

### Navigation (Plugin.cs)
- Pfeiltasten nur bei `HasActiveMenu`; Spiel navigiert nativ, Update-Hook sagt an.
- Enter/Escape: SuppressKey + FireCallback (kein Doppel-Trigger).
- Controller: D-Pad Links/Rechts → NavigateGamepad(±1) für SelectYesno.
- Nummernblock 2/4/6/8: Navigation im DC-Fenster (ForceDCMapRead setzt nur Dedup zurück).

## Werkzeuge & Diagnose
- F5: Node-Dump aktives Addon → Desktop `FFXIV_UI_Dump.txt` (+ ins Log).
- Dalamud-Log: `C:\Users\brued\AppData\Roaming\XIVLauncher\dalamud.log` —
  DC-Zeilen mit Präfix `[DC]`, ConfigSystem `[CS]`.
- `/acc dump <AddonName>`: Dump per Chat-Befehl.
- ilspycmd (global installiert): Dalamud.dll/FFXIVClientStructs.dll dekompilieren.
- VirtualQuery-Testprogramm u.a. im Claude-Scratchpad (Session-spezifisch, ggf. neu anlegen).

## Chat-Befehle
- /acc win — aktives Fenster ansagen (wie F2)
- /acc set — aktuell anvisiertes Ziel verfolgen
- /acc nav — Richtung + Distanz ansagen
- /acc near — Objekte in der Nähe auflisten
- /acc ui — aktuelles Menü vorlesen
- /acc stop — Sprache stoppen
- /acc help — Hilfe

## Tastenbelegung
- F1 — Kontext-Hilfe
- F2 — aktives Fenster ansagen + sichtbare Fenster ins Log (Diagnose)
- F5 — Node-Dump des fokussierten Addons auf Desktop (Diagnose)
- F6 — Richtung + Distanz zum Ziel
- F7 — aktuelles Spielziel verfolgen
- F8 — Zielverfolgung beenden
- F9 — Objekte in der Nähe
- F10 — aktuelles Menü vorlesen
- F11 — Sprache stoppen
- F12 — HP/MP-Status ansagen
- Nummernblock 2/4/6/8 — Navigation in der Datenzentrums-Auswahl

## Umgebung
- .NET 10 SDK; Dalamud ApiLevel 15, DalamudPackager 15.0.0 (csproj)
- `DALAMUD_HOME` = `C:\Users\brued\AppData\Roaming\XIVLauncher\addon\Hooks\dev`
- Tolk.dll + nvdaControllerClient64.dll liegen im Output-Verzeichnis
- NVDA als Screenreader

## Weitere Features (geplant)
- [ ] Charaktererstellung vollständig zugänglich (NÄCHSTER GROSSER BROCKEN)
- [ ] Cooldowns ansagen (ActionManager.GetRecastGroupDetail via FFXIVClientStructs)
- [ ] Audio-Beacon (Stereo-Panning zum Ziel)
- [ ] Auktionshaus/Marktplatz vollständig lesbar machen
- [ ] Alle Untermenüs (Ausrüstung, Inventar) — testen ob universeller Handler reicht
- [ ] Zielverfolgung per Name über Chat-Befehl
