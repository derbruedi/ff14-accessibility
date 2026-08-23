# Game-API-Erkenntnisse (FF14 / Dalamud / FFXIVClientStructs)

Zentrale, VERIFIZIERTE Fakten über Spiel-Strukturen. Quelle jeweils angegeben
(ilspycmd gegen FFXIVClientStructs.dll oder Live-Log). Nichts hier ist geraten.

## Verifizierte Structs (ilspycmd, FFXIVClientStructs.dll im Dalamud-dev-Ordner)

- `RaptureAtkUnitManager.Instance()`, `FocusedUnitsList`/`AllLoadedUnitsList`
  (`AtkUnitList`: Entries[256] + Count), `AtkUnitBase.NameString`/`IsVisible`
- `AtkComponentCheckBox.IsChecked`
- `GameObject` (Client.Game.Object): `DrawObject*` @ Offset 256,
  `VisibilityFlags RenderFlags` @ 280 (Enum: None=0, Model=2, Nameplate=0x800)
- `DrawObject` (Client.Graphics.Scene): hat `bool IsVisible` (BitField)
- `CustomizeData` (Client.Game.Character): Race@0, Sex@1, Tribe@4 (Bytes).
  ABER: kein sauberer Live-Zeiger auf die laufende Charaktererstellung
  (kein AgentCharaMake in dieser Version; AgentLobby ohne CustomizeData-Feld)
- `Framework` (Client.System.Framework): `bool WindowInactive` @ Offset 6104
  — hat das SPIELFENSTER den Fokus? (true = im Hintergrund). Genutzt vom
  VitalsService, um die HP/MP-Töne stumm zu schalten, solange man in einem
  anderen Programm ist. Vorzuziehen gegenüber der Windows-API
  `GetForegroundWindow`: das Spiel führt das Flag ohnehin, eine zweite
  Wahrheitsquelle könnte davon abdriften. Daneben liegen `CallerWindow`
  (nint) und `GameWindow*`. NOCH NICHT BELEGT: ob das Flag auch
  Minimieren/Overlays abdeckt oder nur den reinen Fokuswechsel — der
  VitalsService loggt jeden Flankenwechsel, das klärt es im Betrieb

### Knoten-Geometrie: `AtkResNode.ScreenX/ScreenY` (ilspycmd 2026-08-18)

- `AtkResNode`: `X`/`Y` @68/72 sind ELTERN-relativ, `ScreenX` @112 und
  `ScreenY` @116 sind die vom Spiel gerechneten Bildschirmkoordinaten — damit
  muss keine Elternkette aufaddiert werden. `Width`/`Height` @160/162 sind
  dagegen lokale Einheiten; fuer den Vergleich mit Bildschirmabstaenden mit
  `AtkUnitBase.Scale` multiplizieren.
- **Die Knotenreihenfolge sagt NICHTS ueber das Layout.** `UldManager.NodeList`
  ist nach absteigender NodeId sortiert, und ob die Beschriftung eines
  Bedienelements davor oder dahinter steht, ist je Panel verschieden. In
  ConfigSystem, Reiter Grafik, steht sie DAHINTER (Aufklappfeld id 374 zwischen
  "Schattenkaskadierung" id 377 und der richtigen Beschriftung
  "Schattenauflösung" id 373), im Reiter Barrierefreiheit DAVOR (Text id 581
  "Stärke", Regler id 580). Wer die Beschriftung ueber die Liste sucht, liegt
  also in der Haelfte aller Faelle eine Einstellung daneben. Richtig ist die
  Geometrie: gleicher Zeilenbereich, Text links vom Bedienelement
  (`ConfigLabelByGeometry`).
- **Sichtbarkeit immer effektiv pruefen.** Eine versteckte Konfigurationsseite
  loescht das Visible-Flag nur an ihrem Container; ihre Textknoten melden
  weiterhin `IsVisible()==true` (Dump 2026-08-18: Text id 505 der
  Farbschema-Seite ist V, waehrend Grafik offen ist). Dafuer gibt es
  `IsEffectivelyVisible` (Knoten + gesamte Elternkette).
- Der Node-Dump (F5) schreibt seit 2026-08-18 `@ScreenX,ScreenY BreitexHoehe`
  in jede Zeile — Layoutfragen sind damit offline am Dump zu klaeren.

## Charaktererstellung (CharaMake)

### Addon-Liste (Live-Log 2026-07-10, alle öffnen gleichzeitig)
CharaMake, _CharaMakeInfo, _CharaMakeNotice, _CharaMakeShadow, _CharaMakeTitle,
_CharaMakePose, _CharaMakeProgress, _CharaMakeReturn, _CharaMakeHelp,
_CharaMakeRaceGender, _CharaMakeTribe, _CharaMakeFeature, _CharaMakeGuardian,
_CharaMakeCity, _CharaMakeClassSelector, _CharaMakeWorldServer,
_CharaMakeBirthDay, _CharaMakeBgSelector, _CharaMakeCharaName,
CMFIconFaceType, CMFIconHair, CMFIconFeature, CMFIconTatoo, CMFIconFacePaint,
CMFSlider (2x), CMFColorL, CharaMakeSelectYesNo, CharaMakeDCWorldMap(Bg)

### Vorschau-Modelle (Live-Log 2026-07-10, V4.15-Probe)
- 32 Pc-Objekte GLEICHZEITIG in der ObjectTable: Indizes 200-231, ohne Namen,
  Sex abwechselnd 0/1 = 8 Völker × 2 Stämme × 2 Geschlechter
- Genau EINES sichtbar (`DrawObject.IsVisible=true`, RenderFlags=0x0);
  die 31 versteckten tragen RenderFlags=0x40 (Wert nicht im Enum benannt)
- Das sichtbare Modell = das angezeigte → sein Sex-Byte ist Ground Truth
  fürs gewählte Geschlecht (0=männlich, 1=weiblich, FFXIV-Konvention)

### _CharaMakeRaceGender (Dumps 2026-07-09)
- 8 Volk-Zeilen als Comp(1003) [CT=Base], je zwei Geschlechts-Checkboxen:
  Kind id=4 (Symbol kaputt als ® U+00AE) und id=3 (© U+00A9), Volksname in id=2
- Symbol→Geschlecht-Zuordnung UNGEKLÄRT; Indiz (1 Datenpunkt, Log 2026-07-10
  10:19): id=3 (©) checked bei sichtbarem Modell Sex=0 → © wäre männlich,
  ursprüngliche Annahme id=4=männlich damit wohl FALSCH. Ansage nutzt daher
  das sichtbare Modell, Checkbox nur als Änderungs-Detektor + Fallback-Label
- MouseOver-Ansage per Event-Target (`AtkEvent->Node`), CleanRaceName
  schneidet Glyphen ab

### _CharaMakeTribe (Dump 2026-07-10 10:20)
- Stamm-Optionen = Top-Level-CheckBox-Komponenten (Node id=7 Comp(1006),
  id=6 Comp(1006)), Name im Text-Kind id=2 („Hochländer", „Wiesländer")
- Enthält AUSSERDEM 8 Comp(1003)-Zeilen [CT=Base] mit ®/©-Checkboxen
  (wie RaceGender, Textkinder leer) und Zurück/Ok-Buttons (id=19/18)
- Kopfzeile: „Volksstamm", Hilfetext „Wähle einen Volksstamm aus."

### _CharaMakeProgress (Dump 2026-07-10 10:20) — Fortschrittsmenü links
- Comp(1002)-Buttons je Schritt, Label in Text-Kind id=3, aktueller Wert in
  id=5: „Volk & Geschlecht" (Wert z.B. „Hyuran ©"), „Volksstamm" (Wert
  „? ? ?" wenn offen), „Aussehen", „Namenstag", „Schutzgottheit", „Klasse",
  „Stammwelt", „Name"; Ok-Button = Comp(1001)
- Das ©/® im Wert von „Volk & Geschlecht" ist das gewählte Geschlechts-Symbol

### _CharaMakeFeature (Dump 2026-07-17 16:35, Schritt „Aussehen")
- Kategorie-Buttons = Comp(1004) [CT=Button], Label im Text-Kind id=2
  („Körpergröße", „Körperbau", „Gesicht", … „Stimme"); unsichtbare Buttons
  (F=0x2023 ohne V) sind für das gewählte Volk nicht verfügbare Kategorien
- Beschreibung als Top-Level-Text id=6 („Bestimme das Aussehen deines
  Charakters."), Fenster-Titel id=3 („Aussehen"),
  „Zufälliges Aussehen" = Comp(1003)-Button, Top-Level-**id=4**
  (V4.86: Strg+F8 drückt ihn per ButtonClick-Dispatch, Match per
  Node-ID — sprachunabhängig), Zurück/Ok = id=38/37
- MouseOver/ButtonClick liefern die Kategorie im Event-Param (node id)

### CMFIcon* (Dump 2026-07-17 16:35: CMFIconFeature „Gesichtsmerkmale")
- Auswahl = List(9)-Komponente, Einträge ListItemRenderer(14) mit
  AUSSCHLIESSLICH Image-Kindern — KEIN Text pro Eintrag. Vorlesen nur als
  „Eintrag X von Y" (ListLen/Sel im List-Layout) möglich, Icons sind stumm.
- Fenster-Titel als Top-Level-Text id=3, Ok-Button id=7
- Bekannte Picker-Fenster (Live-Log 2026-07-17): CMFIconFaceType,
  CMFIconHair (52), CMFIconFeature, CMFIconTatoo (27?), CMFIconFacePaint
  (27), CMFColorL (192), CMFColorHair (192), CMFColorFacePaint (96);
  weitere CMFColor*-Varianten wahrscheinlich (Augen-/Lippen-/Hautfarbe
  noch nicht im Log gesehen) → Ansage-Pfade matchen per Präfix „CMF"
- `AtkComponentListItemRenderer.ListItemIndex` (Offset 388, ilspycmd
  2026-07-17) = DATEN-Zeile des Renderers — korrekt auch wenn die Liste
  unter einem festen Fokus-Node scrollt (Renderer-Slot-Index wäre falsch)
- Vorlesen: V4.85, zwei Pfade („12 von 52"): TrackListIndices-Fallback
  + TryReadCharaMakeIconFocusRow im globalen Fokus-Pfad. Live-Log
  17:24: BEIDE greifen (Maus-Hover bewegte Hov2 → List-Navigation-
  Ansage, Fokus-Zeile lieferte denselben Text, Debounce fing das Echo)
- **Das SPIEL ignoriert Pfeiltasten in diesen Rastern** (Log 17:24:47:
  alle vier Pfeile, keinerlei Index-/Fokus-Bewegung — reine Maus-UI).
  V4.87: Plugin navigiert selbst — `AtkComponentList.SelectItem(idx,
  dispatchEvent)` + `ScrollToItem(short)` + `GetItemCount()` (alle
  ilspycmd-verifiziert; auch vorhanden: `DispatchItemEvent(idx,
  AtkEventType)` als Alternative, falls SelectItem die Vorschau nicht
  aktualisiert — Laufzeit-Wirkung von dispatchEvent noch unverifiziert)
- Inaktive Picker bleiben geladen mit 0 Einträgen; nur der aktive hat
  ListLength > 0 (Log 17:23:52) → Erkennung „aktiver Picker" über
  Count > 0

### _CharaMakeCharaName (Namenseingabe, Dump 2026-07-17 17:57)
- Fenstertitel „Name des Charakters", Hilfetext id=13 („Vor- und
  Nachname können je zwischen 2 und 15 Zeichen…"), Instruktion id=5
  („Gib deinem Charakter einen Namen."), Gesamt-Zähler id=12 „0/20"
- ZWEI sichtbare TextInput-Komponenten (CT=7): **id=9 und id=7**
  (je F=…V), jede mit eigenem Zähler-Kind id=17 („0/15") und
  Anzeige-Text id=16. Dazu ZWEI unsichtbare TextInputs id=11 (Zähler
  „0/9") + id=10 („0/6") = alternative Eingabemodi (nicht genutzt,
  kein V) → nur sichtbare Felder verarbeiten
- Labels als Top-Level-Text: **id=8 „Nachname", id=6 „Vorname"**.
  Node-Reihenfolge: TextInput id=9 → Text id=8 → TextInput id=7 →
  Text id=6. id-1-Muster passt (9→8, 7→6), aber V4.89 paart per
  PHYSISCHER NÄHE (X/Y des Feldes vs. Label) — robuster gegen Node-
  Ordnung/Sprache
- „Bestätigen"-Button id=16, „Zurück"-Button id=3
- Vorlesen: V4.89 OnCharaMakeNameUpdate — Fokus-Node → enthaltendes
  sichtbares TextInput (FindFocusedNameField), bei Feldwechsel Label +
  Inhalt, sonst Tipp-Echo (EvaluatedString-Diff). Generischer Fokus-
  Leser für Namensfelder stummgeschaltet (IsFocusInsideNameField),
  Knöpfe bleiben generisch lesbar
- OFFEN: wie wechselt der Nutzer die Felder (Tab? Klick?) — Laufzeit-
  Log fehlte (rotiert); nächster Test klärt es ([Name]-Zeilen)

### Aussehen speichern (Dumps + Log 2026-07-17 17:42)
- Weg: Aussehen-Schritt → Ok → SelectYesno „Die Einstellungen
  speichern?" → Ja
- `CharaMakeDataExport` („CHARAKTERDATEN SPEICHERN"): List(9) mit 40
  Slots, ListItemRenderer-Zeilen MIT Text: id=6 Volksstamm/Geschlecht
  („Wiesländer♂"), id=5 „Speicherslot N", id=4 Datum. Tastatur bewegt
  Hov2 (nativ) → generische Listen-Ansage greift. Spalten-Köpfe +
  Beschreibung als Top-Level-Texte (id=6/5/4/2)
- `CharaMakeDataImportDialog`: Überschreiben-Bestätigung (Ok/Abbrechen),
  Frage wird von OnAnyAddonOpen gelesen
- `CharaMakeDataInputString`: Kommentar-Dialog — Window-Komponente,
  Speichern/Abbrechen-Buttons (id=5/6), **TextInput-Komponente (CT=7)**
  top-level id=4 mit Zähler-Text id=17 („0/40") und Anzeige-Text id=16
- `AtkComponentInputBase` (ilspycmd 2026-07-17): EvaluatedString @224,
  RawString @328, CursorPos @460, SelectionStart/End @452/456 —
  EvaluatedString = Quelle fürs Tipp-Echo (V4.88,
  OnCharaMakeInputUpdate, Diff-Ansage pro Frame)
- ACHTUNG Fokus: der globale Fokus sitzt im Dialog auf dem ZÄHLER-Node
  („0/40") und ändert sich pro Tastendruck → IsBareNumber-Guard
- [Key]-Probe-Erkenntnis: IsJustPressed sieht Pfeiltasten NUR, wenn das
  Spiel sie nicht selbst verbraucht (native Listen-Navigation
  verbraucht sie; tote Icon-Raster nicht) → Plugin-Navigation kollidiert
  nie mit nativer Navigation

### Volk-/Volksstamm-Beschreibung = _CharaMakeHelp (Dumps 2026-07-17 16:31)
- Der Beschreibungstext steht in `_CharaMakeHelp`, Top-Level-**Text-Node
  id=4** (F=0x2033 V), und wird beim Markieren einer Option live
  umgeschrieben — verifiziert an ZWEI Schritten:
  - Volk & Geschlecht (16:31:39/49): „Die Elezen sind stolze Nomaden, …"
  - Volksstamm (16:31:57): „Der Volksstamm der Wiesländer macht die
    große Mehrheit im Volk der Hyuran aus. …"
- Übrige _CharaMakeHelp-Nodes: id=5 TextNineGrid (Text leer), id=3 Text
  leer, id=7/6/2 Images — id=4 ist der einzige Inhalts-Node
- _CharaMakeInfo ist NICHT die Beschreibung (beide Text-Nodes leer,
  auch während die Beschreibung sichtbar war)
- Am Schritt „Aussehen" ist _CharaMakeHelp unsichtbar (Dumps 16:32/16:35)
- Vorlesen: V4.83 `OnCharaMakeHelpUpdate` (PostUpdate _CharaMakeHelp,
  Änderungs-Detektor auf dem Node-Text, nicht-unterbrechende Ansage)
- ACHTUNG (V4.84): `_CharaMakeHelp` MUSS in SpecialUpdateAddons stehen —
  sonst spricht der generische Scanner (ScanAddonTexts) den Text
  zusätzlich per SpeakInterrupt und schneidet die Namens-Ansage ab
  (Log 2026-07-17 16:56)

### Noch nicht analysiert (Dumps im Log vom 2026-07-10 vorhanden!)
- CMFColorL (Farbwahl, ~1283-2793)
- CharaMake-SelectYesno (~4555+)
- Dump-Datei auf Desktop wird bei jedem F5 ÜBERSCHRIEBEN — Log hat alle

## Buttons programmatisch klicken (verifiziert per ilspycmd, 2026-07-10)

Sauberer Weg ohne Callback-Raten: das registrierte ButtonClick-Event des
Buttons an seinen Listener schicken — derselbe Pfad wie ein echter Mausklick.

- `AtkResNode.AtkEventManager.Event` = Kopf einer verketteten Liste
  (`AtkEvent.NextEvent`); Klick-Events hängen am Collision-Kind oder am
  Component-Node selbst
- `AtkEvent`: Node@0, Target@8, Listener@16, Param@24, NextEvent@32, State@40
- `AtkEventState.EventType`@0 — `AtkEventType.ButtonClick = 25`, MouseOver=6,
  MouseClick=9
- `AtkEventListener.ReceiveEvent(AtkEventType, int eventParam, AtkEvent*,
  AtkEventData*)` — AtkEventData ist 40 Bytes, genullt übergeben
- Implementiert in `UIReaderService.PressFocusedOk`/`TryClickButton`
- SelectYesno-Sonderfall bleibt: Ja = `FireCallback(1, {Int:0})` +
  `ShouldFireCallbackAndHideOrClose=true`; Nein = `Close(true)` (Nein hat
  KEINEN Callback — bestätigt)

## Lobby / Titelbildschirm

- `CharaSelect` ist LEERER Container (Vis=True, 0 Nodes) — Inhalt liegt in
  `_CharaSelectListMenu` (MouseOver param 1/2/3, kein eigener Text-Handler)
- `SelectYesno` wird mit wechselnden Knopf-Texten wiederverwendet (Ok/Abbrechen):
  sichtbare Knöpfe Comp(1005) id=8 (Bestätigen) / id=11 (Abbrechen);
  HoldButton-Duplikate ids 9/12/15 unsichtbar; Window-Komponente (CT=Window(2))
  trägt Fenstertitel als Text-Kinder; id=8/„Ok" = Callback-Index 0
- `TitleDCWorldMap`: Event-Parameter der MouseOver-Events sind KEINE Node-IDs;
  Zuordnung über `AtkEvent->Node` (erstes Feld). Region-Tabs (Comp 1022) ohne
  Text — Regionsnamen in Panels (Comp-Child 1009), DC-Namen in 1015

## Keybind-System (verifiziert per ilspycmd, 2026-07-10)

Namespace `FFXIVClientStructs.FFXIV.Client.System.Input` (+ `Client.UI.UIInputData`):

- **Zugriff:** `UIInputData.Instance()` (holt sich `UIModule.Instance()` intern).
  `UIInputData` enthält `InputData` als Feld an Offset 0.
- **`InputData`** (Size 2512): `NumKeybinds` (Offset 2484, int),
  `Keybinds` (Offset 2488, `Keybind*`), `GetKeybindSpan()` → `Span<Keybind>`,
  `GetKeybind(InputId)`, `IsInputIdPressed/Down/Held/Released(InputId)`.
  Index in der Tabelle == InputId-Wert.
- **`Keybind`** (Size 11): `KeySettings` (2× KeySetting, Tastatur-Slot 1+2),
  `GamepadSettings` (2× KeySetting, Controller).
- **`KeySetting`** (Size 2): `Key` (SeVirtualKey, byte — Werte == Windows-VK-Codes,
  z.B. F1=112, W=87, 0=unbelegt), `KeyModifier` (KeyModifierFlag:
  Shift=1, Ctrl=2, Alt=4, Flags kombinierbar).
- **`InputId`**-Enum: ~450 benannte Aktionen (mit Lücken, z.B. 227–236 fehlen;
  max 678). Wichtige Gruppen: `MOVE_*` (321–327), `CAMERA_*` (328–343),
  `TARGET_*` (361–429, u.a. `TARGET_P1`–`TARGET_P8` = 370–377 →
  Gruppenmitglieder, Standard vermutl. F1–F8!), `HOTBAR_1_1`–`HOTBAR_EX_B`
  (57–188), `MENU_*` (237–280 + weitere), `CMD_*` Chat (281–320),
  `JUMP`=348, `AUTORUN_KEY`=349, `KEY_SCREENSHOT`=555.
  Volltext: Scratchpad-Dump oder ilspycmd -t.
- **Live-Auslesen im Plugin:** `/acc keys` (V4.18, KeybindService) schreibt
  alle belegten Aktionen + Konflikt-Check gegen Plugin-Tasten nach
  `Desktop\FFXIV_Keybinds.txt`.

## Gamepad-Eingabe (verifiziert per ilspycmd, 2026-08-17 — Machbarkeitsstudie, NICHTS davon gebaut)

**Dalamud-Seite** (`Dalamud.Game.ClientState.GamePad`, Interface
`Dalamud.Plugin.Services.IGamepadState`):

- `Pressed(GamepadButtons)` → nur im ERSTEN Frame des Drucks; `Repeat(...)` in
  Intervallen bei Halten; `Released(...)` im Frame nach dem Loslassen;
  `Raw(...)` roher Zustand. Alle geben `float` (1 oder 0) zurück.
  Damit ist die Flankenerkennung, die das Plugin für die Tastatur selbst baut
  (`_keyWasDown`/`_keyJustPressed` in Plugin.cs), fertig vorhanden.
- `LeftStick` / `RightStick` als `Vector2`.
- `GamepadInputAddress` (nint) — Zeiger auf die GamepadInput-Struct.
- **`GamepadButtons`** (Flags, ushort): DpadUp/Down/Left/Right, North/South/
  West/East, L1/L2/L3, R1/R2/R3, Start, Select. **16 Stück, mehr gibt es nicht.**

**Schlucken einzelner Knöpfe: NICHT über die öffentliche API möglich.**
Für die Tastatur genügt `KeyState[key] = false`. Ein Gegenstück fehlt:

- Das Interface ist rein lesend (nur Getter + die vier Abfragemethoden).
- Dalamud-intern gibt es nur ALLES-ODER-NICHTS: ist
  `ImGuiConfigFlags.NavEnableGamepad` gesetzt, setzt `GamepadState` beim
  Framework-Update `gamepadInput->GamepadInputData.ButtonsPressed = None` und
  blockt damit die gesamte Gamepad-Eingabe des Spiels.
- Ansatzpunkte für selektives Filtern gäbe es: über `GamepadInputAddress`
  direkt in die Struct schreiben (Dalamud macht genau das), oder die
  spieleigene `UIInputData.FilterGamepadInputs(UIInputData*)`. Dazu kommen die
  Felder `UIInputData.GamepadInputs` und `GamepadInputs2` (je `GamepadInputData`).
- **OFFEN, MUSS GEMESSEN WERDEN:** ob ein so gefilterter Knopf die Spiellogik
  wirklich nicht mehr erreicht — das hängt an der Reihenfolge von Dalamud-Hook
  und Spiel-Frame und steht in keiner Quelle. Ohne belastbare Messung ist jede
  Controller-Bedienung ein Blindflug: ein nicht geschluckter Knopf löst
  zusätzlich seine Spielfunktion aus.

**Welche Knöpfe belegt sind:** steht in derselben Keybind-Tabelle wie die
Tastatur — `Keybind.GamepadSettings` (2 Slots pro Aktion, siehe Abschnitt
darüber). `KeybindService` liest bisher nur `KeySettings`; für einen
Gamepad-Dump muss dort `GamepadSettings` ergänzt werden. Ob die `Key`-Werte
darin dieselben Windows-VK-Codes sind wie bei der Tastatur oder eine eigene
Knopf-Nummerierung, ist NICHT geprüft.

## Offizielle Standard-Tastaturbelegung (Quelle: de.finalfantasyxiv.com/game_manual/operation, 2026-07-10)

Zusammenfassung des offiziellen Handbuchs. VORBEHALT: Sonderzeichen-Tasten
(deutsches Layout) teils unklar; Ground Truth ist der Auto-Keybind-Dump (V4.19).

### Bewegung
- W/S vor/zurück, A/D drehen, Q/E Seitschritt, Leertaste springen
- R Auto-Rennen, Y Waffe ziehen/Absteigen, V Kamera-Flip
- Fliegen: Leertaste hoch, Strg+Leertaste runter/tauchen, Z absteigen (Luft)

### Kamera
- Pfeiltasten = Kamera richten (NICHT Bewegung!), Bild↑/↓ Zoom
- Pos1 Kamera wechseln, Ende Standardposition, NUM5 auf Ziel einrasten

### Zielauswahl (KERN FÜR NAVIGATION — alle F-Tasten belegt!)
- Tab / Umschalt+Tab: Gegner durchschalten (nah→fern / fern→nah)
- F: zum Ziel hinwenden; Umschalt+F: Fokusziel setzen/löschen
- F1: sich selbst; F2–F8: Gruppenmitglieder; F9: Begleiter; F10: Fokusziel
- F11: nächster GEGNER; F12: nächster NPC ODER OBJEKT (eingebaute Navigation!)
- T: Ziel des Ziels; Umschalt+T: Angreifer
- Strg+NUM8/NUM2: Feindliste hoch/runter

### Chat
- Enter: Chat öffnen; X: Textkommando; Alt+S/G/R/…: Chatmodi

#### Kampflog / eigene Aktionen vorlesen (V4.90)
Beim Einsetzen einer Aktion schreibt das Spiel "Du wirkst X." ins Kampflog;
das kommt über `IChatGui.ChatMessage` als eigener `XivChatType`.
- Benannte Basis-LogKinds (Dalamud `XivChatType`, = Low-7-Bits des Wertes):
  Damage=41, Miss=42, **Action=43** ("setzt Aktion ein"), Item=44,
  Healing=45, GainBuff=46, GainDebuff=47, LoseBuff=48, LoseDebuff=49.
- Reale Nachrichten können als KOMBINIERTE Werte ankommen (Quell-/Ziel-Bits
  im höheren Byte), darum `(int)type & 0x7F` auf die Basis maskieren
  (robust, egal ob flach oder kombiniert).
- OFFEN/PROBE: ob "eigene" vs. "fremde" Aktion über die hohen Bits
  unterscheidbar ist, ist NICHT verifiziert - ChatReaderService.TryHandleCombat
  loggt jede Aktions-Zeile roh ([Combat] Aktion type=0x…), damit der
  Eigen-Code aus einem Live-Log gefiltert werden kann. Bis dahin werden ALLE
  Aktions-Zeilen gelesen (Config ReadCombatMessages). Auch Nachlese-Kategorie
  "Kampf".

#### Chat SENDEN (Tipp-Echo im Eingabefeld) — ilspycmd-verifiziert 2026-07-17
NVDA liest das Spiel-Chatfeld nicht; das Plugin spricht die getippten
Zeichen selbst (V4.90). Quelle:
- `AddonChatLog` (Addon-Name „ChatLog", IMMER sichtbar).
  - `TextInput` @608 = `AtkComponentTextInput*` (Direktzeiger, kein
    Node-Scan nötig).
  - `TabIndex` @684 / `TabCount` @685 / `TabNames` (FixedSizeArray5) =
    die Chat-REITER (Allgemein/Kampf/…), NICHT der Sende-Kanal.
- `AtkComponentTextInput`:
  - `IsActive` (bool) = true, solange der Eingabemodus offen ist
    (Enter geöffnet). DAS Gate, damit das Echo nicht jeden Frame läuft.
  - `AtkComponentInputBase.EvaluatedString` = getippter Text (wie beim
    CharaMake-Feld). Dazu `CursorPos`, `SelectionStart/End` für späteren
    Feinschliff (Editieren mittendrin).
- Aktiver Kanal (Ansage): `AddonChatLog.CurrentChannelTextNode` @335
  (`AtkTextNode*`) trägt das Kanal-Label, wie das Spiel es rendert -
  lokalisiert und immer korrekt (via `->NodeText.ToString()`, dann
  sanitizen). DAS ist die Quelle für die Kanal-Ansage - KEIN int→Name-Raten
  nötig. (V4.90 nutzt genau das.)
  - `RaptureShellModule.Instance()->ChatType` @4048 (int) ist der Kanal als
    Zahl; Testwerte 2026-07-17: 1/2/4 bei Alt-Umschaltung. `TempChatType`
    @4284; Flüster-Ziel `TellName` @4056 / `TellWorld` @4160 / `TellWorldId`
    @4280. Die int→Name-Zuordnung ist NICHT verifiziert (Agent-Enum
    `ChatChannel` nur Say=1/Party=2/Alliance=3, evtl. andere Nummerierung) -
    darum wird für die Ansage der Textnode genutzt, nicht die Zahl.
- Senden (Enter) und Kanalwechsel (Tab/Alt+Taste) bleiben spieleigen —
  das Plugin sagt nur an. Gesendetes echot der ChatReaderService zurück
  (eigene /say-Nachricht kommt als XivChatType.Say).

### Menüs (Auswahl)
- NUM0 bestätigen, NUM, (Komma) abbrechen, NUM* Unterkommando
- NUM8/2/4/6 Cursor, NUM9/NUM7 Reiter, NUM+ Hauptmenü, NUM- System
- C Charakter, I Inventar, M Karte, J Archiv (Quests!), K Kommandoliste,
  U Charakterkonfig, Strg+U Systemkonfig, P Inhaltssuche, O Gruppe,
  L Kontaktkreise, H/G/B/, Notizbücher, ä Emotes, Ö Freie Gesellschaft
- Esc: alle UI-Elemente schließen; Druck/F13 Screenshot; F14 UI-Modus

### Safe Mod Keys (BESTÄTIGT durch Live-Dump 2026-07-10, 171 belegte Aktionen)
- **N = einziger freier Buchstabe** (nur Alt+N=Neulingschat belegt)
- **NUMPAD3 frei** (NUMPAD1=HUD-Fokus, NUMPAD5=Kamera, Rest = UI-Cursor)
- **Strg+F1…F12 komplett frei** (nur Strg+F20 belegt);
  Umschalt+F1…F12 ebenfalls frei (belegt: Umschalt+Tab/T/F/M/V)
- Einschränkung: bare SHIFT/CONTROL sind im BARDEN-MUSIKMODUS Oktav-Tasten
  (PERFORMANCE_MODE_*) — Strg-Kombis dort vermeiden
- **WINDOWS-FALLE Umschalt+Nummernblock (entdeckt 2026-07-16):** bei aktivem
  NumLock wandelt der Windows-Tastaturtreiber Umschalt+Numpad-ZIFFER in die
  Navigations-Taste um (Numpad3 → Bild-ab/VK_NEXT) und lässt Umschalt dabei
  künstlich los — IKeyState sieht NIE die Numpad-VK. Beleg: Gehhilfe auf
  Umschalt+Numpad3 (V4.61–V4.63) hat laut Log kein einziges Mal gefeuert,
  während Strg+Numpad3 (Routen-Vorschau) sofort ankam. Bild-ab ist im Spiel
  obendrein CAMERA_ZOOMOUT. ⇒ Numpad-Ziffern NIE mit Umschalt kombinieren,
  nur mit Strg. Strg+Numpad2/4/6/8 sind vom Spiel belegt (Allianz-/
  Gegnerlisten-Cursor); Strg+Numpad1/3/5/7/9 frei.
- **⚠️ KORREKTUR 2026-08-19 zu der Zeile darüber: Strg+Numpad1/7/9 kommen in der
  PRAXIS NICHT AN.** Der User hat die drei beim Test der Sonderaktionen gemeldet
  („er nimmt die strg taste nicht"). Auf Nachfrage bestätigt: **Strg+Numpad3
  funktioniert bei ihm**, es ist also keine allgemeine Strg-Schwäche, sondern
  betrifft genau die Ziffern 1/7/9. Der Keybind-Dump meldet sie als frei — er
  sagt aber nur, dass das SPIEL sie nicht belegt, nicht dass sie beim Plugin
  ankommen. Ursache ungeklärt und bewusst NICHT geraten (Verdacht Screenreader
  oder Tastaturtreiber, unbelegt). ⇒ Für neue Tasten nur Strg+Numpad0/3/5
  verwenden, das sind die drei nachweislich funktionierenden.
- Plugin-Tasten seit V4.21 (Config-Migration V1→V2): N=Objekte nah,
  Umschalt+N=Richtung, Strg+N=Ziel verfolgen, Strg+Umschalt+N=Verfolgung aus,
  Strg+F1=Hilfe, Strg+F2=Fenster, Strg+F5=UI-Dump, Strg+F10=Menü vorlesen,
  Strg+F11=Stille, Strg+F12=Kampfstatus
- IsJustPressed kann seit V4.21 Modifier („Strg+Umschalt+N"), EXAKTE
  Modifier-Übereinstimmung (bare N feuert nicht bei Alt+N)
- Deutsche Umlaut-Tasten laufen über Sonder-VKs: VK136≈Ö (FC-Menü),
  VK140≈Ä (Emotes), VK137/139 = Hotbar-Slots 11/12 (vermutl. ß/´) —
  Zuordnung aus Manual-Abgleich GEFOLGERT, nicht hart verifiziert
- Weitere Dump-Erkenntnisse: MENU_FISH=F20, MENU_BUDDY=F22, MENU_RETURN=F24
  (Pseudo-Tasten); Kamera=Pfeiltasten; CMD_CHAT=RETURN bestätigt

### Dalamud-Targeting (verifiziert 2026-07-10, in-game + ilspycmd)
- `IObjectTable.LocalPlayer.TargetObject` trackt UI-Targeting NICHT
  (in-game belegt: Tab-Ziel gesetzt, Property blieb null → keine Ansage)
- Richtig: `ITargetManager` (Dalamud-Service): `.Target` (hartes Ziel),
  `.SoftTarget`, `.FocusTarget`, `.MouseOverTarget`, `.PreviousTarget` —
  alle IGameObject?, auch setzbar (null = Ziel löschen)
- Dalamud `ObjectKind`-Enum: None, Pc, BattleNpc, EventNpc, Treasure,
  Aetheryte, GatheringPoint, EventObj, Mount, Companion, Retainer,
  AreaObject, HousingEventObject, Cutscene, ReactionEventObject, Ornament,
  CardStand (NICHT „Player"/„MountType"!)

### SetHardTarget kann ABLEHNEN (ilspycmd + Live-Log 2026-07-10, 16:39)
- `TargetSystem.SetHardTarget(GameObject*, bool ignoreTargetModes, bool a4,
  int a5)` gibt **bool** zurück — das Spiel kann die Zieländerung verweigern.
  Dalamuds `ITargetManager.Target`-Setter ruft das auf und WIRFT den
  Rückgabewert WEG (ilspycmd-verifiziert, Dalamud.dll TargetManager).
- Live belegt: 16:39:26–16:39:44 wurden ALLE Target-Sets des Browsers
  abgelehnt (Hard-Target blieb auf Honoraint), davor und danach (16:41:34+)
  funktionierten sie. Ursache noch UNGEKLÄRT — Plugin loggt Ablehnungen
  seit V4.25 per Read-back („[Nav] Target-Set ABGELEHNT").
- Getter = `GetHardTarget()` (eigene Spielfunktion, nicht bloß Feld-Read;
  Feld `Target` liegt bei Offset 128). `ignoreTargetModes`-Parameter
  ungetestet — Kandidat, falls Ablehnungen zum Problem werden.

### Rotations-Konvention (VERIFIZIERT aus Live-Log 2026-07-10, 15:26–15:27)
- `IGameObject.Rotation` (Radiant): **Blickvektor = (sin(rot), cos(rot))
  in der XZ-Ebene**, d. h. rot = atan2(dx, dz) der Blickrichtung.
  rot=0 blickt nach +Z. Relativwinkel zum Ziel:
  **`rot - atan2(dx, dz)`** (normalisiert auf ±180°); 0 = geradeaus,
  positiv = rechts, negativ = links.
- Beweis: F-Taste (zum Ziel drehen) rastete zweimal auf exakt rot=-1,83
  ein; Ziel-Peilung aus stationären Gehhilfe-Ticks: atan2(dx,dz)=-105° =
  -1,83 rad — Blickvektor traf Zielrichtung auf <0,5° genau. Die alte
  Annahme „0 = Norden" (atan2(dx,-dz)) war eine SPIEGELUNG, kein Offset.

#### Vorzeichen: GEKLÄRT 2026-08-23 — die Differenz stand jahrelang falsch herum
- Bis dahin rechnete der Mod `atan2(dx, dz) - rot`, also andersherum. Damit
  kamen **links und rechts vertauscht** heraus — in den Ansagen, in der
  Gehhilfe und im Peil-Ton (dessen Stereoseite `sin(relAngle)` ist). Spieler
  meldeten Sprache und Ton gemeinsam als falsch; es war nie ein Doppelfehler,
  sondern dieser eine.
- Die Herleitung braucht kein neues Log, nur zwei Angaben aus diesem
  Dokument zusammengelegt:
  - Blickvektor = (sin(rot), cos(rot)), also blickt rot=0 nach +Z.
  - Norden ist −Z, Osten +X (`RouteService.SectorOf` = `atan2(dx, −dz)`,
    0 = Norden — dieselbe Rechnung, die die Himmelsrichtungsansagen speist).
  - Zusammen: **rot=0 blickt nach SÜDEN**, denn
    `HeadingSector(0) = SectorOf(0, 1) = atan2(0, −1) = 180°`.
  - Ein Ziel im Osten (dx>0) ergab mit der alten Formel ein Plus, also
    „rechts". Wer nach Süden blickt, hat Osten aber **links**.
- Bestätigt in-game vom User 2026-08-23: „wenn ich nach links laufe wird
  weniger und nach rechts mehr, links und rechts ist vertauscht".
- **Warnung an künftige Leser:** der Code trug an dieser Stelle den Vermerk
  „Vorzeichen per Beacon-Hörtest bestätigt (2026-07-10)", während HIER
  „OFFEN" stand. Der Hörtest kann die Seite nur bestätigt haben, wenn dabei
  nach Norden geblickt wurde — nur dann fallen beide Vorzeichen zusammen.
  Ein „bestätigt" im Code, dem im Referenzdokument ein „offen" gegenübersteht,
  ist kein Beweis, sondern ein Widerspruch, der aufzulösen ist.
- Betroffene Stellen, beide korrigiert: `NavigationService.RelativeAngle`
  und `CombatService.RelBearingDeg`. **Nicht** betroffen: die AoE-Geometrie
  (`EscapeRouteService` rechnet mit `MathF.Abs` des Winkels) und
  `FacingService`/`FaceGuideDirection` (dort ist `atan2(dx, dz)` die zu
  setzende Ziel-Rotation, kein Relativwinkel — dort wäre die Umkehr falsch).

### vnavmesh-IPC (Quellcode-verifiziert 2026-07-10, github.com/awgil/ffxiv_navmesh)
- Fremd-Plugin für Navmesh-Wegfindung + Auto-Bewegung. Installation:
  Repo `https://puni.sh/api/repository/veyn`, ApiLevel 15. Beim User liegt es
  als Dev-Plugin unter `devPlugins\vnavmesh` — Dalamud aktualisiert das NICHT
  automatisch, ein Update heisst Dateien austauschen (2026-08-10 von 1.2.3.10
  auf 1.2.3.13; alte Fassung liegt in `devPlugins\vnavmesh_backup_1.2.3.10`).
  Die Downloadadresse steht als `DownloadLinkInstall` in der Repo-JSON.
  ACHTUNG: Ein Versionswechsel kann die Navmesh-Formatversion aendern, dann
  werden alle gecachten Netze beim ersten Betreten neu gebaut.
- Für uns relevante IPC-Gates (alle mit Präfix `vnavmesh.`):
  - `Nav.IsReady` → bool (Mesh der Zone geladen)
  - `Nav.BuildProgress` → float (Ladefortschritt)
  - `SimpleMove.PathfindAndMoveTo(Vector3 dest, bool fly)` → bool
  - `SimpleMove.PathfindAndMoveCloseTo(Vector3 dest, bool fly, float range)`
    → bool (false NUR wenn schon eine Wegfindung aussteht; Quelle:
    AsyncMoveRequest.MoveTo)
  - `SimpleMove.PathfindInProgress` → bool (Wegfindung rechnet noch)
  - `Path.IsRunning` → bool (läuft gerade; Waypoints.Count > 0)
  - `Path.Stop` → Action (Subscriber: GetIpcSubscriber<object>, InvokeAction)
  - `Path.SetTolerance(float)`, `Path.MoveTo(List<Vector3>, bool)` u. a.
- Dalamud-Seite: `IDalamudPluginInterface.GetIpcSubscriber<T..., TRet>(name)`,
  `InvokeFunc`/`InvokeAction` (ilspycmd-verifiziert). Fehlt das Plugin,
  wirft der INVOKE (IpcNotReadyError) — Subscriben ist immer gefahrlos.
- Pfadziel ist ein PUNKT (Position beim Start) — bewegte NPCs laufen weg,
  ggf. neu starten.
- WICHTIG `Nav.Pathfind(Vector3 from, Vector3 to, bool fly)`: reine
  Wegpunkt-ABFRAGE ohne Auto-Bewegung, aber der Rückgabetyp ist
  **`Task<List<Vector3>>`**, NICHT `List<Vector3>` (ilspycmd 2026-07-16
  an der installierten vnavmesh.dll: das IPC-Gate wrappt
  `NavmeshManager.QueryPathBasic`, eine `async`-Methode). Subscriber:
  `GetIpcSubscriber<Vector3, Vector3, bool, Task<List<Vector3>>>`;
  Task pro Frame pollen (`IsCompletedSuccessfully` prüfen — Task kann
  faulten wenn das Mesh beim Zonenwechsel entlädt), NIE blockierend
  `.Result` vor Abschluss. `Nav.PathfindInProgress`/`Nav.PathfindNumQueued`
  melden den Queue-Zustand; mehrere Anfragen werden intern nacheinander
  abgearbeitet (`ExecuteWhenIdle`). Verdents Konzeptdokument
  (manuelle-navigation-konzept.md) gibt hier fälschlich `List<Vector3>` an.
- QueryPath wirft eine Exception, wenn kein Mesh geladen ist — vor dem
  Invoke `Nav.IsReady` prüfen (RouteService macht das).

### Schatztruhen: Zustand liest man, man rechnet ihn nicht nach
`FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure` (ilspycmd 2026-08-09,
erbt von GameObject, Size 528). Das Spiel fuehrt den Zustand selbst:
- `State` (FieldOffset **416**, enum `TreasureState`):
  `Unopened=0, Opening=1, Opened=2, Unk3=3, FadingOut=4, FadedOut=5`.
  Alles ausser `Unopened` heisst: erledigt. Das Objekt bleibt danach noch eine
  Weile in der ObjectTable, nur um sein Ausblenden zu spielen.
- `Flags` (FieldOffset **508**, enum `TreasureFlags`): `Opened=1, FadedOut=2`.
  ACHTUNG, die Struct-Doku nennt State und Flags ueberlappend und sagt zu
  FadedOut ausdruecklich „sometimes set when fading starts, sometimes when
  fading is complete" — deshalb ist `State` die verlaesslichere Quelle.
- `CofferKind` (FieldOffset 512, enum `TreasureKind`): `Levequest`,
  `DungeonRaid`, `TreasureHunt`, `PersonalLoot`.
- `ItemCount` (496) + `LootableItemIds` (432, 16 × uint) = Item-Sheet-Zeilen des
  Inhalts, sobald er im Beutefenster steht.
- `CountdownTime`/`ClaimTime` (420/428): die Sekunden, die das Beutefenster
  anzeigt.
→ V5.75 blendet Truhen mit `State != Unopened` aus der Objekt-Browser-Liste aus
(NavigationService.IsEmptiedTreasure). NUR die Liste — anvisiert man die Truhe
mit den Spieltasten, wird sie weiterhin angesagt.

#### Höher gelegene Ziele: was `fly` wirklich tut (ilspycmd 2026-08-08)
Vollständige IPC-Liste aus `Navmesh.IPCProvider` der installierten DLL
dekompiliert. Zum Thema „Objekt liegt über mir":
- **Der `fly`-Parameter wählt ZWEI VERSCHIEDENE SUCHRÄUME**, er ist kein
  Komfort-Schalter (`NavmeshManager.QueryPath`, Zeile 189):
  `flying ? Query.PathfindVolume(...) : Query.PathfindMesh(...)`.
  - `false` (was wir überall übergeben) = **Gehfläche**. Kennt Höhe sehr wohl —
    Treppen, Rampen, Brücken sind Teil des Netzes. Was sie NICHT kennt, ist
    eine Verbindung, die es begehbar nicht gibt.
  - `true` = **Voxel-Volumen** (Luftraum, `NavVolume`/`VoxelPathfind`).
- **Das Volumen gibt es nicht immer.** `NavmeshQuery` legt `VolumeQuery` nur an,
  wenn `navmesh.Volume != null` (Zeile 92-94); sonst antwortet `PathfindVolume`
  mit dem Log-Fehler „Nav volume was not built" und einer leeren Liste.
  Ob es für Innenräume/Instanzen gebaut wird: NICHT GEPRÜFT.
- **`fly=true` lässt die Figur SPRINGEN.** `FollowPath` Zeile 153: liegt der
  nächste Wegpunkt höher als der Spieler und ist er weder `InFlight` (Condition
  77) noch `Diving` (81), ruft es `ExecuteJump()` — aber nur, wenn
  `IgnoreDeltaY` false ist. Und `Path.MoveTo(waypoints, fly)` setzt
  `IgnoreDeltaY = !fly` (IPCProvider Zeile 78-81). Mit unserem `fly=false`
  springt die Figur also NIE. (Condition-Namen aus Dalamud.dll verifiziert.)

#### Ungenutzte IPC-Gates, die unser „kein Weg gefunden" direkt betreffen
- `Nav.PathfindWithTolerance(from, to, fly, float range)` → Wegfindung mit
  Zieltoleranz. Genau der Haukke-Fall (Ziel liegt neben dem Netz).
- `Query.Mesh.NearestPointReachable(p, halfExtentXZ, halfExtentY)` → nächster
  **erreichbarer** Netzpunkt (`FindNearestPointOnMesh(..., allowUnreachable:
  false)`). Unsere selbstgebaute Ringsuche in AutoWalkService macht das zu Fuß
  nach — hier bietet vnavmesh es fertig an.
- `Query.Mesh.IsPointOnMesh(p, halfExtentY, allowUnreachable)` → Prüfung, ob ein
  Punkt überhaupt auf dem Netz liegt.
- `Nav.PathfindCancelable(from, to, fly, CancellationToken)`.

#### Wie vnavmesh Pfade wirklich startet und beendet (ilspycmd 2026-08-10)
Drei Eigenschaften, die jede Auto-Lauf-Logik kennen MUSS. Alle drei haben die
Implementierung vor V5.79 stillschweigend kaputtgemacht — Beleg jeweils im
Dalamud-Log vom 2026-08-10.

1. **Ein Pfadauftrag ist asynchron und stoppt den laufenden Pfad NICHT.**
   `AsyncMoveRequest.MoveTo` setzt nur `_pendingTask` (gibt `false` zurück,
   wenn schon einer läuft). Erst `AsyncMoveRequest.Update` reicht das Ergebnis
   an `FollowPath.Move` weiter. In diesem Fenster beschreibt `Path.IsRunning`
   noch den VORHERIGEN Pfad, und `Path.ListWaypoints` liefert dessen Wegpunkte.
   → Log 08:05:05: Auftrag „Weinhafen", zurückgelesen wurde die Wegpunktliste
   des Sonnenküste-Laufs; 52 ms später meldete das Plugin „beendet, noch 499 m",
   während vnavmesh gleich darauf 50 m weit lossteuerte.
   → Konsequenz: vor dem eigenen Start `Path.Stop` rufen, und nach dem eigenen
   Ende noch einige Sekunden nachwachen (ein Task in flight belebt den Lauf neu).

2. **vnavmesh startet sich selbst neu.** Mit `StopOnStuck` + `RetryOnStuck`
   (beide in der Nutzerkonfiguration an, `StuckTimeoutMs` 1000,
   `StuckTolerance` 0,05) ruft `FollowPath.Update` nach einer Sekunde ohne
   Bewegung `Stop()` und feuert `OnStuck`; `AsyncMoveRequest` schickt daraufhin
   denselben Auftrag erneut. `Path.IsRunning` blinkt dadurch **im Sekundentakt
   auf false**, ohne dass der Lauf zu Ende wäre.
   → Log 08:04:24–08:05:55: 91 „Queueing move-to" im Sekundentakt, nachdem das
   Plugin sich längst ausgeklinkt hatte — die Figur wurde eine Minute lang
   lautlos gegen die Netzkante geschoben.
   → Konsequenz: „Pfad zu Ende" nur nach Entprellung (V5.79: 1,6 s durchgehend
   `!IsRunning && !PathfindInProgress`). Ein einzelnes Frame lügt.

3. **Der letzte Wegpunkt ist frei erfunden.** `NavmeshQuery.PathfindMesh` hängt
   das ANGEFRAGTE Ziel unbedingt an das Ergebnis an (`list.Add(new Waypoint(
   rcVec3f...))`), ob es auf dem Netz liegt oder nicht. Zerfällt das Netz einer
   Zone in unverbundene Inseln, liefert vnavmesh also einen Pfad, dessen letzter
   Sprung quer durch den Fels geht, und drückt die Figur dann endlos dagegen.
   → Log 08:04:23: `restWp=1 nextWp=(490,5|19,0|466,6) distNextWp=453,8` —
   Spieler auf Höhe 58,7, Ziel auf Höhe 19,0, Östliches La Noscea.
   → Konsequenz: bleibt die Figur stehen und ist nur noch EIN Wegpunkt übrig,
   ist nicht sie festgesteckt, sondern das begehbare Netz endet dort. V5.79 sagt
   das so an, statt „festgesteckt" zu behaupten.

#### Warum das Wegenetz NICHT alle Wege kennt (Navmesh.NavmeshSettings, ilspycmd 2026-08-10)
Haeufiges Missverstaendnis: das Netz ist keine von Square Enix mitgelieferte
Wegkarte, sondern wird von vnavmesh selbst mit **Recast** aus der
Kollisionsgeometrie berechnet - fuer eine idealisierte Figur mit festen Grenzen:
- `AgentMaxSlopeDeg = 55` - alles steiler als 55 Grad ist NICHT begehbar.
- `AgentMaxClimb = 0,5` - Absaetze ueber einen halben Meter sind unueberwindbar.
- `AgentHeight = 2`, `AgentRadius = 0,5` - die Flaeche wird zusaetzlich um einen
  halben Meter von jeder Wand weg geschrumpft.
- `GenerateEdgeClimbLinks = false` (Standard, beim User nicht gesetzt) - es
  werden also KEINE "hier kann man runterspringen/-klettern"-Verbindungen
  erzeugt. Die zugehoerigen Werte (`ClimbDownMaxHeight` 3,2 m, `EdgeJumpHeight`
  1,8 m) liegen brach.
- `RegionMinSize = 8` - kleine isolierte Flaechen fallen ganz raus.

Folge: Jede Stelle, die man im Spiel nur durch Herunterspringen, Rutschen oder
ueber einen steilen Hang erreicht, existiert im Netz nicht. Genau so zerfaellt
Oestliches La Noscea (s1f3) in zwei Haelften - Weinhafen-Plateau (Y ca. 59-76)
und Kueste/Costa del Sol (Y ca. 17-20). Zu Fuss kommt man hinunter, ueber eine
55-Grad-Kante fuehrt aber kein Recast-Polygon.
→ ERLEDIGT UND WIDERLEGT (2026-08-10): `/vnav rebuild` in der Zone gemacht, Cache
   nachweislich neu geschrieben, vnavmesh auf 1.2.3.13 - die Trennung besteht
   reproduzierbar weiter (Lauf endete wieder bei 469 m Restentfernung).

KORREKTUR ZUR FRUEHEREN NOTIZ (ilspycmd 2026-08-10): `GenerateEdgeClimbLinks`
laesst sich NICHT "in den vnavmesh-Einstellungen einschalten". `NavmeshSettings`
wird ausschliesslich aus `NavmeshCustomization.Settings` gelesen
(`NavmeshBuilder..ctor`: `Settings = customization.Settings`), und die
Nutzer-`Config` enthaelt diese Felder gar nicht - sie hat nur AutoLoadNavmesh,
EnableDTR, ShowQueryStatusInDTR, AlignCameraToMovement/-Height, ShowWaypoints,
ForceShowGameCollision, CancelMoveOnUserInput, StopOnStuck, StuckTolerance,
StuckTimeoutMs, RetryOnStuck, RandomnessMultiplier, BuildMaxCores. Die
`NavmeshSettings.Draw()`-Regler gehoeren zum Debug-Fenster "NavmeshCustom", also
zu manuell gebauten Testnetzen, nicht zum automatisch geladenen Zonennetz.
→ Recast-Parameter aendern = vnavmesh forken. Kein Weg ueber eine Datei oder UI.

#### Was stattdessen geht, um eine Netzluecke zu ueberbruecken (IPC, ilspycmd 2026-08-10)
- `Path.MoveTo(List<Vector3> waypoints, bool fly)` faehrt eine EIGENE Punktliste
  ab, ganz ohne Wegsuche (geht direkt an `FollowPath.Move`). Das ist der einzige
  Weg, die Figur ueber Boden zu schicken, den das Netz nicht kennt - im Spiel
  bestaetigt 2026-08-07 (Astalicia) und 2026-08-09 (Hinweg zum Magneten).
  ACHTUNG, die Punktliste ist NICHT sicher (ilspycmd 2026-08-10): Bleibt die
  Figur `StuckTimeoutMs` (500 ms) unter `StuckTolerance` stehen, ruft
  `FollowPath.Update` sein eigenes `Stop()` und feuert `OnStuck` mit dem LETZTEN
  Wegpunkt. Daran haengt `AsyncMoveRequest`, das bei `RetryOnStuck` (beim User
  an) ein normales `MoveTo` auf diesen Punkt startet - unsere Liste ist weg und
  die Figur laeuft wieder ueber das Netz, das die Luecke ja nicht kennt.
  Erkennen laesst sich das an zwei Dingen, die `AutoWalkService.TrailWalkingUpdate`
  beide prueft: die Wegpunktzahl WAECHST (eine neue Route hat mehr Punkte als
  unsere Restliste; unsere schrumpft nur), und `PathfindInProgress` wird wahr
  (unsere Etappe rechnet nie).
- `NavmeshCustomization.LinkPoints(mesh, start, end)` ist vnavmeshs eigener
  Mechanismus fuer handgemachte Verbindungen, aber `protected static` in einer
  Customization-Klasse mit `[CustomizationTerritory(id)]` - nur per Fork
  erreichbar, nicht ueber die IPC. Fuer Gebiet 135 existiert keine Customization.
- FLIEGEN: `NavmeshCustomization.IsFlyingSupported` gibt true fuer
  `TerritoryType.TerritoryIntendedUse` 1, 47 und 49; dann baut `NavmeshBuilder`
  zusaetzlich eine `VoxelMap`, und `Nav.Pathfind`/`Path.MoveTo` nehmen ein
  `fly`-Flag. Ein Flugvolumen kennt die 55-Grad-Grenze nicht - fuer Hoehenbrueche
  in Feldzonen also die grundsaetzlich saubere Umgehung. UNGEPRUEFT ist beides:
  ob Gebiet 135 einen der drei IntendedUse-Werte hat, und ob der Charakter dort
  fliegen darf (Aetherstroeme - reiner Spielzustand, steht nicht im Netz).
- `seeds-local.json` (`FloodFill.AddPoint` + `Serialize`, Ablage im
  vnavmesh-pluginConfigs-Ordner) markiert nur, welche Flaechen von einem Seed aus
  erreichbar sind, und schaltet damit `NavmeshManager.Prune` scharf. Das schliesst
  KEINE Luecke; es macht nur `Query.Mesh.NearestPointReachable` /
  `IsPointOnMesh(allowUnreachable: false)` ueberhaupt wirksam (die ohne Seeds
  wirkungslos sind, gemessen 2026-08-09).

Weitere belegte Kleinigkeiten:
- `Path.IsRunning` ist exakt `FollowPath.Waypoints.Count > 0`, nichts weiter.
- `Path.Stop` = `Waypoints.Clear()`. Es bricht KEINE laufende Wegfindung ab.
- `FollowPath.OnNavmeshChanged` leert die Wegpunkte — beim Zonenwechsel oder
  `Nav.Reload` verschwindet ein Pfad also von selbst.
- `Nav.PathfindCancelAll` ist irreführend benannt: es ruft `Reload(allowLoadFromCache: true)`,
  lädt das Netz also neu.
- `Query.Mesh.NearestPointReachable` filtert über `FloodFillAwareFilter`
  (`NavmeshQuery` Zeile 83) — das ist vnavmeshs eigene Erreichbarkeitsprüfung,
  eine selbstgebaute Flächenanalyse ist dafür nicht nötig.
- `CancelMoveOnUserInput`: drückt der Spieler selbst eine Bewegungstaste, ruft
  `FollowPath.Update` `Stop()` — der Pfad ist dann weg, ohne dass unser Plugin
  etwas davon erfährt.

### Spieler „folgen" — KEIN natives API (verifiziert per ilspycmd, 2026-07-26)
Das Kontextmenü „Folgen" existiert im Spiel, ist aber in FFXIVClientStructs
**nicht** als aufrufbare Funktion freigelegt. Vollständige Assembly dekompiliert
und durchsucht: die einzigen „Follow"-Treffer sind Begleiter/Mount
(`CompanionBehaviorState.Follow`, `FollowMountId`), Porträt-Kamera
(`BannerCameraFollowFlags`) und das Karten-Häkchen (`FollowPlayerCheckbox`,
`FollowedPlayerMarkerX/Y`). `MoveController` (MoveControl) trägt KEIN Follow-Feld.
Ein Auslösen ginge nur fragil über `AgentContext.OpenContextMenu` + Eintrag per
Text finden/`ReceiveEvent` feuern (sprach-/zieltyp-abhängig) — verworfen.
→ V5.57 baut „Ziel folgen" (Taste +) stattdessen selbst auf vnavmesh: in
`AutoWalkService.FollowUpdate` wird `SimpleMove.PathfindAndMoveCloseTo` fortlaufend
auf die AKTUELLE Zielposition neu ausgelöst (Abstand 3 m, Re-Path ab 1,5 m Drift
oder wenn der Pfad endete, throttled 0,4 s). Stoppt bei Ziel-weg/Zonenwechsel.

### Kompass-Konvention Welt→Himmelsrichtung (hergeleitet 2026-07-16)
- Norden = −Z, Osten = +X. Herleitung aus verifizierten Fakten: die
  Pixel→Welt-Formel (oben) bildet Karten-Pixel-X→Welt-X und
  Karten-Pixel-Y→Welt-Z GLEICHSINNIG ab; Kartenbilder haben den Ursprung
  oben links (Pixel-Y wächst nach unten); die Spielkarte ist genordet
  (Norden oben). ⇒ Peilung ab Nord = `atan2(dx, −dz)` (0°=N, 90°=O).
- Genutzt von RouteService (Routen-Vorschau „25 Meter nach Norden").
  Jede Vorschau loggt Segment 1 samt Vektor ([Route]) — eine gespiegelte
  Achse würde beim ersten Praxistest im Log sichtbar.
- Spieler-Blickrichtung: rot=0 ⇒ Blickvektor (sin 0, cos 0) = (0,0,1) =
  +Z = SÜDEN (folgt aus obiger Konvention + verifiziertem Blickvektor).

### Online-Fenster / Social (ilspycmd-verifiziert 2026-07-19)
Taste O = `MENU_PARTY_MEMBER (271)` laut Keybind-Dump; Addon-Name „Social".
- `FFXIVClientStructs.FFXIV.Client.UI.AddonSocial` (Size 816,
  [Addon("Social")], erbt AtkUnitBase) hat die vier Registerkarten als
  `AtkComponentRadioButton*`: `PartyMembersRadioButton`@680,
  `FriendListRadioButton`@688, `BlacklistRadioButton`@696,
  `PlayerSearchRadioButton`@704.
- AKTIVE Karte = die, deren `AtkComponentButton.IsChecked` gesetzt ist.
  IsChecked ist Bit 18 von `AtkComponentButton.Flags` (dekompiliert:
  `BitOps.GetBit(Flags, 18)`); RadioButton erbt AtkComponentButton@0.
- LABEL: `AtkComponentButton.ButtonTextNode` (AtkTextNode) trägt den
  lokalisierten Karten-Text — nie selbst übersetzen, den Node lesen.
- Verwandte Structs falls der Inhalt gebraucht wird: `AddonFriendList`,
  `InfoProxyFriendList`, `SocialListNumberArray`/`SocialListStringArray`,
  `AgentFriendlist`.
- WICHTIG — der INHALT liegt NICHT im Social-Addon (Log 2026-07-18 17:05):
  ein Tab-Wechsel hängt ein eigenes Fenster an (`Social ReceiveEvent:
  type=ChildAddonAttached param=126/127`) und öffnet je nach Karte
  `PartyMemberList`, `FriendList` oder `SocialList`. Eine Listen-Suche im
  Social-Addon selbst findet daher NICHTS. Beobachtete Zuordnung:
  Karte 1 „Gruppe"→PartyMemberList, 2 „Freunde"→FriendList,
  3 „Suche"→SocialList (Karte 4 noch nicht gesehen).
  ACHTUNG: Karte 3 trug das Label „Suche", obwohl das Struct-Feld an
  Slot 3 `BlacklistRadioButton` heißt — die Feldnamen der ClientStructs-
  Version stimmen hier offenbar nicht mit der UI-Reihenfolge überein.
  Deshalb Label immer aus dem ButtonTextNode lesen, nie aus dem Feldnamen
  ableiten.

### Einladungen / Benachrichtigungen (2026-07-18)
- Popup-Fenster (Namen aus dem Log, nicht geraten): `_NotificationFcJoin`
  (Freie Gesellschaft), `_NotificationParty`, `_NotificationFriend`,
  `_Notification`. Laufzeit 300 s, dann bricht das Spiel die Einladung ab
  („Die Einladung von ... wurde abgebrochen", SystemMessage 57).
- Das Fenster enthält einen Sekunden-Zähler (bei FcJoin Node key=20005),
  der sich jede Sekunde ändert — generische Text-Scanner müssen nackte
  Zahlen unterdrücken, sonst zählt der Screenreader 300 → 0 mit.
- Die Einladungs-MELDUNG selbst kommt unabhängig davon über Chat
  (SystemMessage) UND Toast — nicht aus dem Popup lesen.
- Im Keybind-Dump des Spiels gibt es KEINE Aktion für Benachrichtigungen;
  ohne Mausklick ist eine Einladung per Tastatur nicht beantwortbar.
- SPIELFUNKTION zum Antworten (Reserve, ungetestet):
  `InfoProxyFreeCompanyInvite` (InfoProxyId.FreeCompanyInvite) und die
  Basis `InfoProxyInvitedList` haben beide in der vtable @104
  `RespondToInvitation(CStringPointer inviterName, bool accept)`.
  ACHTUNG: braucht den Namen des Einladenden; im Proxy stehen dafür nur
  private `UnkString`-Felder (@72 / @176) — unverifiziert.

### Addon-Verwandtschaft: Kind-/Host-Fenster (ilspycmd 2026-07-18)
`AtkUnitBase` trägt drei Id-Felder direkt hintereinander (nach
`AtkValuesCount`): `Id` (ushort), `ParentId`, `HostId`, dazu
`BlockedParentId`. Damit lässt sich ein angehängtes Kind-Fenster ohne
hartcodierte Namensliste finden: `AllLoadedUnitsList` durchlaufen und
`child->HostId == host->Id || child->ParentId == host->Id` prüfen.
Welches der beiden Felder das Spiel je Fensterfamilie setzt, ist NICHT
dokumentiert — deshalb beide prüfen und das Ergebnis loggen.
FALLE: `Id == 0` als Suchschlüssel matcht jedes Addon mit ungesetztem
Rückverweis — vorher abfangen.
- LISTEN-TIMING: ein frisch geöffnetes Listenfenster hat oft `Len=0` und
  wird erst ein paar Frames später gefüllt (FriendList: leer bei
  PostSetup, Einträge 35 ms später). „0 Einträge" beim Öffnen ist also
  in der Regel keine leere Liste, sondern eine zu früh gestellte Frage.

### Quest-Stufe (ilspycmd-verifiziert 2026-07-18)
- ERSTE WAHL: `MapMarkerData.RecommendedLevel` (ushort @64) — der Marker
  trägt die Stufe selbst, kein Namensabgleich nötig. Gegenprobe im Struct:
  `SetData(.., ushort recommendedLevel, sbyte eventState)`.
- FALLBACK: Lumina `Quest.ClassJobLevel` (Collection<ushort>, Index 0) —
  die Stufe, die auch das Journal zeigt. Nur per Quest-NAMEN zuordenbar
  und damit unpräzise: FFXIV vergibt Namen mehrfach (Wiederholbare).
  Weitere Felder falls je gebraucht: `QuestLevelOffset` (byte @2764),
  `LevelMax` (byte @2786), `SortKey` (ushort @2760).
- OFFEN (Laufzeit): ob RecommendedLevel im Marker überhaupt gefüllt ist.
  QuestMarkerService loggt pro Marker `lvlMarker=` und `lvlSheet=`.

### Symbol über dem Kopf: `GameObject.NamePlateIconId` (ushort/uint @272)
Genau das Zeichen, das ein SEHENDER Spieler über dem Objekt sieht (Quest-
Ausrufezeichen usw.), 0 = keins. Wird gelesen, nie aus dem Quest-Zustand
nachgebaut. GEMESSEN bisher (alle 2026-08-02): **71201** (Buscarron,
Süd-Schwarzhölzer), **71203** (Baensyng, Limsa Lominsa), **71351**
(Thubyrgeim, Limsa Lominsa).
WICHTIG: Alle drei echten Messwerte liegen bei 712xx/713xx — die
Bedeutungs-Bereiche in `AccessibilityStrings.QuestMarkerHint`
(71001–71006 „verfügbar", 71021–71046 „aktiv") haben bis heute **null**
Messwerte und greifen nie; real trifft immer der Sammel-Fall
71000–71999 „Quest". Der Objekt-Browser loggt jedes Symbol ungleich 0 mit
Objektnamen, damit die Einteilung aus echten Daten geschärft werden kann.
Vor einer Aussage „Icon X heißt Y": erst messen.
Gegenprobe zur Verlässlichkeit (Log 2026-08-02 20:01): in Limsa Lominsa
lieferten Symbol-Quelle und Marker-Quelle unabhängig voneinander exakt
dieselben zwei NPCs (`per Marker 2, per Symbol 2`, 2 Treffer gesamt).

### Quest-Marker mit Welt-Position (ilspycmd-verifiziert 2026-07-10)
Quelle: `FFXIVClientStructs.FFXIV.Client.Game.UI.Map` (Singleton,
`Map.Instance()` via StaticAddressPointers).
- `QuestMarkers` → Span mit 30× `MarkerInfo` = Marker der ANGENOMMENEN
  Quests; `UnacceptedQuestMarkers` (StdList<MarkerInfo>) = annehmbare
  Quests in der Nähe; außerdem u. a. `ActiveLevequestMarker`,
  `GuildLeveAssignmentMarkers`, `TripleTriadMarkers`.
- `MarkerInfo` (Size 144): `ObjectiveId`@4 (uint), `Label`@8 (Utf8String,
  Quest-Name), `MarkerData`@112 (StdVector<MapMarkerData> — MEHRERE Orte
  pro Quest möglich!), `RecommendedLevel`@136, `ShouldRender`@139 (bool).
- `MapMarkerData` (Size 80, vollständig ilspycmd 2026-08-02): `LevelId`@0,
  `ObjectiveId`@4, `TooltipString`@8 (Utf8String*), `IconId`@16,
  `Position`@28 (Vector3, WELT-Koordinaten!), `Radius`@40, `MapId`@48,
  `PlaceNameZoneId`@52, `PlaceNameId`@56, `EndTimestamp`@60 (int),
  `RecommendedLevel`@64 (ushort), `TerritoryTypeId`@66 (ushort),
  `DataId`@68 (ushort), `MarkerType`@70, `EventState`@71, `Flags`@72.

### FALLE: `MapMarkerData.DataId` ist KEINE Objekt-Id (gemessen 2026-08-02)
Das Feld sieht aus wie die Datensatz-Id des Ziel-Objekts, ist es aber nicht:
- Es ist ein **ushort** — eine NPC-`BaseId` liegt bei 1.000.000+ und passt
  nicht in 16 Bit.
- `MapMarkerData.SetData(levelId, tooltip, icon, x, y, z, radius,
  territoryTypeId, mapId, placeNameZoneId, placeNameId, recommendedLevel,
  eventState)` hat **keinen dataId-Parameter** — der Setzer schreibt das Feld
  nie. Messung: bei allen Quest-Markern 0 (Log 2026-08-02, „0 Ids aus
  Markern", Kategorie blieb leer).

### Quest-Marker → Objekt in der Welt (der richtige Weg, 2026-08-02)
`MapMarkerData.LevelId`@0 (= erster SetData-Parameter) ist die Zeilennummer
im Lumina-Sheet **`Level`** (ilspycmd Lumina.Excel.Sheets.Level):
- `X`@0/`Y`@4/`Z`@8, `Yaw`@12, `Radius`@16
- `Object` (uint @20) — die Datensatz-Id des Objekts an diesem Ort, typisiert
  über `Type` (byte @32): **8 = ENpcBase, 9 = BNpcBase, 12 = Aetheryte,
  14 = GatheringPoint, 45 = EObj**
- `EventId` @24 (RowRef auf TripleTriad/Adventure/Opening/**Quest**),
  `Map` (ushort @28), `Territory` (ushort @30)

`Level.Object` liegt im selben Id-Raum wie `IGameObject.BaseId` im Objekt-
Browser (Gegenprobe: die NPC-Titel kommen über `ENpcResident.TryGetRow(
obj.BaseId)` und stimmen). Damit ist „welches Objekt meint dieser Marker"
eine reine Sheet-Abfrage — keine Icon-Tabelle, keine Abstands-Heuristik.
Beim Lesen `Level.Territory` gegen die aktuelle Zone prüfen: sonst markiert
eine Id aus einer anderen Zone einen gleich aussehenden NPC nebenan.
Achtung bei `Type=9` (BNpcBase): eine Base-Id gilt für ALLE Gegner derselben
Art in der Zone — das ist für „töte 3 Käfer" richtig, aber es ist eben eine
Art, kein Einzelgegner.
- OFFEN (Laufzeit, vor Nutzung per Debug-Probe klären): (1) Feld für
  TerritoryType/Zone des Markers — Marker können in ANDERER Zone liegen
  (SetData-Signatur hat territoryTypeId-Parameter, Feld-Offset im Struct
  noch nicht identifiziert); (2) taugt Position.Y direkt als
  vnavmesh-Ziel (Marker-Zentrum kann neben dem begehbaren Mesh liegen —
  PathfindAndMoveCloseTo mit Radius als range dürfte das abfedern).
- Leere Slots: vermutlich MarkerData.Count==0 bzw. Label leer — per
  Probe verifizieren, nicht raten.

### Laufender Freibrief + seine Gegner (ilspycmd + Sheet-Dump, 2026-08-18)
Frage: „welche Gegner verlangt der Freibrief, der GERADE läuft?" Marker
(`Map.LevequestMarkers`) zeigen nur die FLÄCHE, nicht die Monster.

**Laufenden Freibrief finden — nur Felder der Director-BASIS lesen:**
- `EventFramework.Instance()->DirectorModule` (@192) → `DirectorList` @64
  (`StdVector<Pointer<Director>>`) = alle aktiven Directors.
- `Director` erbt `EventHandler`: `Info`@32 (`EventHandlerInfo`) →
  `EventId`@0 → `EntryId`@0 (ushort) + `ContentId`@2 (`EventHandlerContent`).
- **`EventId.ContentId` nennt die DIRECTOR-ART, nicht das Event.** Die
  Director-Werte liegen ab 0x8000: `BattleLeveDirector = 32769`,
  `GatheringLeveDirector = 32770`, `CompanyLeveDirector = 32775`,
  `FateDirector = 32794`. `GuildLeveAssignment = 6` ist der Geber-NPC-
  Handler und kommt hier NIE vor (Fehlannahme 2026-08-18, im Log gefallen).
- **Die Freibrief-Nummer steht in `Director.ContentId`@736 (uint)**, nicht in
  `EventId.EntryId`. Gemessen 2026-08-18: `Content=BattleLeveDirector(32769)
  Entry=537 DirectorContentId=528 Titel='Lästige Nager'` — und Leve 528 heißt
  im Sheet „Lästige Nager". `EntryId` war bei zwei Läufen desselben
  Freibriefs 537 bzw. 542, ist also eine Instanz-Nummer.
- Weitere sichere Basis-Felder: `Sequence`@744, `Title`@760 (Utf8String,
  = Freibrief-Name), `Objective`@864, `DirectorTodos`@1088
  (`StdVector<DirectorTodo>` — `Text`, `Complete`, `CurrentCount`@120).
- **FALLE:** `Director` ist exakt **1120 Byte** groß, `LeveDirector.LeveId`
  liegt bei **@1120** — also direkt DAHINTER. `LeveDirector` = 1200,
  `BattleLeveDirector` = 1680 (mit `LeveData`@1232). Es gibt KEIN Feld, an
  dem man die Unterklasse eines `Director*` erkennt. Deshalb: alles ab 1120
  ist ein Überlesen, solange nicht anderweitig feststeht, dass der Zeiger ein
  Leve-Director ist. Die Freibrief-Nummer kommt daher aus
  `Info.EventId.EntryId`, nicht aus `LeveDirector.LeveId`.
- Gegenprobe gegen `QuestManager.Instance()->LeveQuests` (`_leveQuests`@3624,
  16× `LeveWork`: `LeveId`@8, `Sequence`@10, `Flags`@12): nur eine Nummer,
  die auch angenommen ist, wird verwendet. Gemessen: 528 stand in der Liste.
- Der Director erscheint erst, wenn der Freibrief GESTARTET ist (Leve-Kugel
  angesprochen), nicht schon beim Annehmen — im Log 21:38:51 stand nur noch
  der FateDirector, 21:41:11 wieder der BattleLeveDirector.

**Gegner des Freibriefs — reine Sheet-Abfrage (offline gemessen 2026-08-18):**
- `Leve.DataId` ist ein Mehrfach-`RowRef` auf CraftLeve / CompanyLeve /
  GatheringLeve / BattleLeve. Die vier Sheets benutzen **disjunkte
  Zeilennummern** (Battle 65563–65764, Gathering 131080–131339,
  Company 196619–196684, Craft 917505–918744), deshalb löst
  `RowRef.Is<T>()` / `TryGetValue<T>()` hier eindeutig auf.
- `LeveAssignmentType`: 1 = „Söldner" → BattleLeve (191 Leves);
  2/3 = Minenarbeiter/Gärtner → GatheringLeve; 4–12 = Handwerk → CraftLeve;
  13/14/15 = die drei Staatlichen Gesellschaften → CompanyLeve.
- `BattleLeve.LeveData` (8 Slots): `BNpcName` (RowRef auf BNpcName),
  `ToDoNumberInvolved` (Stückzahl), `EnemyLevel`, `NumOfAppearance`.
  `CompanyLeve.CompanyLeveStruct` hat dieselben Felder ohne Stückzahl.
- Beispiel: Leve 501 „Durstige Bestien" → BattleLeve 65602 →
  BNpcName 198 „Nussknackerhörnchen" ×4, 199 „Stolper-Fungus" ×4.
- **`ToDoNumberInvolved` taugt NICHT als „so viele musst du töten".** Ein
  Monster füllt oft MEHRERE Slots: Leve 530 „Pralle Reben" listet Bienenwolke
  viermal mit je 2, Leve 527 Wander-Mandragora mit 5 und mit 0; Leve 528
  „Lästige Nager" hat bei allen drei Arten 0. Die Staffelung steckt in
  `ToDoSequence`/`NumOfAppearance` und ist ungemessen — die Zahl nur ansagen,
  wenn eine Art genau einen Slot belegt.
- Namen IMMER über `MonsterNameText.Resolve` — die deutschen Sheet-Namen
  enthalten Platzhalter (`wuchernd[a] Efeuranke`).

**FALLE: der Freibrief spawnt KEINE eigene Art (gemessen 2026-08-18).**
Der `BNpcName` im Slot ist ein Journal-Name, nicht der Name des Objekts:
- Leve 528 Slot 0 nennt BNpcName **1096 „streunend[a] Dodo"**. Das Monster,
  das 2,9 m vor dem Spieler erschien, hatte aber **NameId 393 „Dodo"** —
  dieselbe Art wie die wilden Dodos 40 m weiter. Zuordnung über BNpcName oder
  über den angezeigten Namen findet deshalb **nichts**.
  **Nachtrag 2026-08-18:** der echte Leve-Spawn hieß in der späteren Messung
  sehr wohl `'Streunender Dodo'` (mit Leve-EventId). Das Objekt von 2,9 m war
  also vermutlich gar nicht der Leve-Gegner, sondern ein wildes Dodo —
  ein weiterer Grund, die Zuordnung nie am Namen festzumachen.
- Was zusammenpasst, ist `LeveDataStruct.BaseID` (BNpcBase **339**) mit
  `IGameObject.BaseId` (**339**) des gespawnten Objekts. Die wilden Dodos
  tragen 339 allerdings ebenso — die Art allein trennt also nicht.
- **Auch tot (gemessen 2026-08-18):** `EventHandler.EventObjects`@8 am Director
  ist bei laufendem Freibrief **leer** („0 Director-Objekte").
- **Tot, und zwar gemessen (2026-08-18, zweite Runde):**
  `GameObject.GetEventHandlersImpl` (virtual function 30, Puffer für 32 Zeiger)
  meldet für JEDES Monster denselben zonenweiten Handler — `25770280390` für
  den Leve-Träger-Marienkäfer genauso wie für die wilden Marienkäfer daneben —
  **nie** die Director-Adresse. Die Kategorie fand darüber 0 Gegner, obwohl der
  Freibrief lief und der Gegner 26 m entfernt stand. Nicht noch einmal versuchen.

**SO GEHT ES: `GameObject.EventId` (gemessen 2026-08-18).**
Das gespawnte Monster trägt die **EventId seines Freibrief-Directors**:
- `'Streunender Dodo'` → `EventId=2147549737` (Content `BattleLeveDirector`
  32769, Entry **553**), während Leve 528 lief.
- `'Träger-Marienkäfer'` → `EventId=2147549739` (…, Entry **555**), während
  Leve 512 lief.
- Jedes normale Zonentier daneben liest `EventId=0` — auch die Dodos
  **derselben Art**. Das ist die einzige gemessene Trennung von Leve-Spawn und
  Wildwuchs.
- `EventId` (ilspycmd, `FFXIVClientStructs...Game.Event.EventId`, Size 4,
  `LayoutKind.Explicit`): `Id`@0 (uint, das Ganze), `EntryId`@0 (ushort),
  `ContentId`@2 (`EventHandlerContent`). Es gibt `==`/`Equals` über `Id`.
- Verglichen wird die **volle Id** gegen `Director.Info.EventId.Id`, nicht nur
  die Director-Art: der Eintrag trennt den eigenen Freibrief von dem des
  Spielers, der danebensteht. Offen (im Log sichtbar gemacht): ob Director und
  Monster IMMER dieselbe Entry-Nummer führen — beide Messungen liegen nicht
  zeitgleich vor.
- **`NamePlateIconId` ist NICHT 0** (die frühere Notiz war falsch): beide
  gemessenen Leve-Spawns trugen **71244**, das allgemeine Aufgabenziel-Symbol.
  Es sagt aber nicht, zu WELCHER Aufgabe — als Freibrief-Bindung untauglich.
  Es ist der Grund, warum diese Gegner ohnehin schon in der Kategorie
  „Quest-Gegner" auftauchen (Filterbereich 71000–71999).
- `ICharacter.NameId` **ist** die BNpcName-Zeile — bestätigt am selben Dump:
  393 = „Dodo", 405 = „winzig[a] Mandragora", 115 = „Wind-Exergon", jeweils
  identisch mit dem Sheet.

### Fang-Freibrief: „dieser Gegner ist schon gezähmt" (Log + Sheet-Dump, 2026-08-21)
Frage: ein Fang-Freibrief lässt mehr Gegner stehen, als er verlangt („Im Namen
des Fortschritts": elf Pyrit-Kobalos für vier Fänge). Ein gezähmter
verschwindet nicht, verliert keine HP und heißt weiter genauso — woran erkennt
man ihn, ohne das Emote zu verschwenden?

**Antwort: `Status 213` in der `IBattleChara.StatusList` des Gegners.**
- Gemessen (dalamud.log 2026-08-21): 09:35:51 weist das Spiel den Versuch mit
  „Der Pyrit-Kobalos ist bereits zahm." ab; 09:36:01 liest die Fang-Sonde an
  demselben Ziel `Status: 213:'Besänftigung'`.
- Sheet-Beleg (Lumina, installierte Spieldaten): Zeile 213 = DE „Besänftigung"
  / EN „Pacification", Beschreibung **„Zahm und greift nicht mehr an."** /
  „The target is pacified and will no longer attack.", Symbol **216301**,
  `LockActions=True`, `CanDispel=False`, `IsPermanent=False`.
- **Verwechslungsgefahr ausgeschlossen:** drei weitere Zeilen heißen englisch
  ebenfalls „Pacification" (6, 620, 5188), sind aber alle der Spieler-Debuff
  DE „Pacem" („Waffenfertigkeiten können nicht eingesetzt werden") mit Symbol
  215017. 213 ist die **einzige** Zeile des ganzen Sheets mit Symbol 216301.
- Deshalb wird auf die **Id** geprüft, nie auf den Namen: der Name steht in der
  Sprache des Clients, die Id nicht.
- `IsPermanent=False` heißt: der Status hat eine Laufzeit. Wie lange sie ist,
  ist **nicht gemessen** — ein gezähmter Gegner kann nach unbekannter Zeit
  wieder ohne Status dastehen. Darum wird er im Objekt-Browser nur nach hinten
  sortiert und nicht ausgeblendet.
- **Offen:** ob die Statusliste auch für einen NICHT anvisierten Gegner gefüllt
  ist. Der Log-Trace `[Leve] … davon N schon gezaehmt (Ids), M aufgestachelt`
  beantwortet das beim nächsten Fang-Freibrief.

**Das Gegenstück: `Status 214` „Aufstachelung" / EN „Agitation".**
Liegt im Sheet direkt neben 213 (Symbol 216302 gegen 216301) und beschreibt
sich als „Nach misslungener Besänftigung noch wilder als zuvor." / „Excited by
failed pacification. Attack power and attack magic potency are enhanced."
Ein Gegner mit 214 ist **vorübergehend nicht zähmbar** (das Spiel weist ab mit
„ist in Raserei verfallen und lässt sich nicht beruhigen") und schlägt härter
zu. Für den Spieler ist das dieselbe Art Auskunft wie „schon zahm", nur aus
dem anderen Grund — deshalb wird beides angesagt.

**ES GIBT NUR EINE ZÄHM-MECHANIK IM SPIEL.** Nachgesehen im `LogMessage`-Sheet
(2026-08-21) auf die Frage, ob sich das verallgemeinern lässt — ihre Meldungen
stehen als geschlossener Block:
- **1805** „… wurde gezähmt. (n/m)" — Erfolg samt Zähler
- **1806** „… konnte nicht gezähmt werden und verfällt in Raserei."
- **1807** „… ist bereits zahm."
- **1808** „… ist in Raserei verfallen und lässt sich nicht beruhigen."
- **1809** „… ist nicht mehr zahm" — **der Zustand kann enden**, unabhängige
  Bestätigung für `IsPermanent=False`. Ein gezähmter Gegner bleibt es nicht
  ewig; deshalb wird er sortiert, aber nie ausgeblendet.

Dazu die beiden **einzigen** Anleitungen im selben Sheet:
- **1837** „Besänftige rasende Ziele, indem du das Emote „Beruhigen" (/ruhig)
  auf sie anwendest."
- **1838** „Besänftige rasende Ziele, indem du den richtigen
  Schlüsselgegenstand auf sie anwendest."

Zwei Wege hinein, eine Mechanik dahinter. **Unbelegt bleibt genau ein Glied:**
dass auch der Schlüsselgegenstand-Weg (1838) Status 213 setzt — gemessen ist
nur der Emote-Weg. Da es keinen zweiten „zahm"-Status im Sheet gibt, wäre ein
eigener Zustand dafür ein Sonderfall ohne Zeile.

Emote 35: DE „Beruhigen" (`/ruhig`, `/soothe`), EN „Soothe".

### FATE (ilspycmd-verifiziert 2026-07-31)
Quelle: `FFXIVClientStructs.FFXIV.Client.Game.Fate.FateManager` (Singleton,
`FateManager.Instance()`). Hält NUR die FATEs der aktuellen Zone; FATEs
stehen NIE im Quest-Journal (reine Welt-Ereignisse).
- `FateManager` (Size 208): `CurrentFate`@136 (`FateContext*` — das FATE, in
  dem der Spieler gerade steht), `Fates`@144 (`StdVector<Pointer<FateContext>>`
  = alle aktiven FATEs), `SyncedFateId`@168.
  Methoden: `GetCurrentFateId()`, `GetFateById(ushort)`,
  `TryGetFatePosition(ushort, out Vector3)`, `IsInFateRadius(Vector3*)`,
  `LevelSync()`, `IsSyncedToFate(FateContext*)`.
- `FateContext` (Size 10704): `FateId`@24 (ushort), `Name`@192 (Utf8String —
  mit `.ToString()` lesen, NICHT ExtractText!), `Description`@296,
  `Objective`@400, `State`@940 (`FateState`), `Progress`@951 (byte, 0–100 %),
  `Level`@2035 (byte), `MaxLevel`@2036 (byte), `IconId`@2004,
  `Location`@2128 (Vector3, WELT-Koordinaten — taugt direkt als Nav-Ziel).
- `FateState` (byte): `Preparing`=3 (erscheint gerade), `Running`=4 (aktiv,
  beitretbar), `Ending`=5, `Ended`=7, `Failed`=8.
- `StdVector<T>`: `Count` (int) + Indexer `[i]` → `ref T`; iterieren per
  for-Schleife. `Pointer<FateContext>.Value` → `FateContext*`.
- Genutzt in FateService (Objekt-Browser-Kategorie „FATEs"): listet Running +
  Preparing, Numpad3 läuft zur `Location` (als in-Zone-QuestDestination).

### Journal / JournalDetail (F5-Dumps 2026-07-10/11)
- Journal (Taste J, „ARCHIV"): Quest-Liste = Comp CT=TreeList(12), Zeilen
  sind ListItemRenderer mit id=4 (Stufe „St. 1") + id=3 (Quest-Name);
  Kategorie-Zeilen (Gebiet/Add-on) haben id=2. Tabs „Aktiv"/„Abgeschlossen".
- AtkComponentTreeList erbt AtkComponentList an Offset 0 (ilspycmd:
  [Inherits<AtkComponentList>(0)]) → SelectedItemIndex/ListLength nutzbar.

### AtkComponentList: Index-Felder (ilspycmd 2026-07-11)
Alle Kandidaten für „welche Zeile ist gewählt/markiert":
- `SelectedItemIndex` @308, `HeldItemIndex` @312, `HoveredItemIndex` @316,
  `HoveredItemIndex2` @344, `HoveredItemIndex3` @352 (alle int)
- `ListLength` @288, `FirstVisibleItemIndex` @296
- `ItemRendererList` @240 (ListItem*, 24 Bytes/Eintrag):
  `AtkComponentListItemRenderer*` @8, `IsHighlighted` (bool) @20,
  `IsDisabled` @21. `AllocatedItemRendererListLength` @248 begrenzt die
  echte Allokation (virtuelle Listen: weniger Slots als ListLength!).
- GELÖST (Probe-Log 2026-07-11 10:15, SystemMenu-Volltest): die
  TASTATUR-Navigation trackt `HoveredItemIndex2` (@344) — es ändert sich
  im Frame des Tastendrucks; `HoveredItemIndex` (@316) zieht 1 Frame
  später nach. `SelectedItemIndex` bleibt dabei -1 (nur Maus/Bestätigung).
  Enter auf einem Eintrag setzt `HeldItemIndex` (beobachtet: Held=7 beim
  Öffnen der Systemkonfiguration). IsHighlighted-Maske blieb leer.

### Globaler UI-Fokus: AtkInputManager (ilspycmd 2026-07-11)
- `AtkStage.Instance()->AtkInputManager` (@40): `FocusedNode` @6272
  (AtkResNode*) = DER aktuell fokussierte UI-Node (Tastatur/Gamepad);
  `FocusList` = 256× FocusEntry {AtkEventListener* @0 (i.d.R. das Addon),
  AtkEventTarget* @8 (der Node), FocusParam @16}; `TextInput` @0.
- Fokus sitzt oft auf dem COLLISION-Kind des Controls, nicht auf dem
  Komponenten-Node selbst → für Text Eltern hochklettern.
- AtkStage außerdem: RaptureAtkUnitManager @32, AtkCursor-Typ via
  AtkCursor-Struct (Type/IsVisible, kein Ziel-Node — Ziel steckt im
  InputManager).
- OFFENE LAUFZEITFRAGE (V4.35-Probe [Focus]): folgt FocusedNode dem
  Links/Rechts in SelectYesno/JournalResult? (Node-Flags taten es nicht.)

### TreeList: eigener Items-Vektor (ilspycmd + Log 2026-07-11)
- `AtkComponentTreeList.Items` @432 = StdVector<Pointer<AtkComponentTreeListItem>>
  — die ECHTEN Zeilen (Kategorien + Einträge). Das geerbte `ListLength`
  bleibt 0 (Journal: „Menü, 0 Einträge" trotz navigierbarer Liste).
- Renderer-Zugriff (ItemRendererList[idx]) daher gegen
  `AllocatedItemRendererListLength` prüfen, NICHT gegen ListLength.

### Karten-Marker für „Orte"-Kategorie (Recherche 2026-07-11, ilspycmd)
- AgentMap (Client.UI.Agent): `EventMarkers` StdVector<MapMarkerData> @232
  (+ `EventMarkersPtrs` @208), `SymbolMap` StdMap @352,
  `CurrentTerritoryId` @23072, `CurrentMapId` @23076,
  `CurrentMapSizeFactor(Float)` + `CurrentOffsetX/Y` @22892–22906,
  `MapMarkerCount` (byte) @23291.
- KARTEN-MARKIERUNG („Flagge", Recherche 2026-07-18, ilspycmd):
  `AgentMap.FlagMarkerCount` (byte @23294) = Anzahl gesetzter Flaggen,
  `AgentMap.FlagMapMarkers` = Span<FlagMapMarker> (1 Element, Feld
  `_flagMapMarkers` FixedSizeArray1). FlagMapMarker (Size 72):
  MapMarkerBase@0, `TerritoryId`@56, `MapId`@60, `XFloat`@64, `YFloat`@68.
  WICHTIG: XFloat/YFloat sind WELT-Koordinaten (X und Z), KEINE Karten-
  Pixel — die Pixel→Welt-Formel darf hier NICHT angewandt werden. Beweis:
  `AgentMap.SetFlagMapMarker(territoryId, mapId, Vector3 worldPosition)`
  schreibt worldPosition.X → x und worldPosition.Z → y (auf 3 Nachkomma-
  stellen gerundet) und reicht sie an die Member-Funktion durch. Höhe (Y)
  gibt es nicht — wie bei allen Kartendaten via Navmesh auflösen.
  Vor dem Lesen `MapId` gegen die aktuelle Karte prüfen: die Flagge bleibt
  beim Zonenwechsel stehen und gehört dann zu einer anderen Karte.
- Map (Client.Game.UI) hat NUR Quest-artige Marker: QuestMarkers[30],
  LevequestMarkers[16], HousingMarkers[62], UnacceptedQuestMarkers,
  GuildLeveAssignment/GuildOrderGuide/TripleTriad/CustomTalk/
  GemstoneTrader (alle StdList<MarkerInfo>). KEINE Ätheryten/Ausgänge.
- Statische Symbole (Ätheryten, Ausgänge, Läden): Lumina-Sheet „MapMarker"
  — VERIFIZIERT (ilspycmd Lumina.Excel.Sheets.MapMarker, 2026-07-11):
  Subrow-Sheet, Zeile = Map.MapMarkerRange. Felder: Icon@0,
  PlaceNameSubtext@2, DataKey@4, X@8/Y@10 (short, KARTEN-PIXEL 0..2048),
  DataType@15: 1/2=Map (Zonen-Übergang, DataKey=Ziel-Map), 3=Aetheryte,
  4=PlaceName (Aethernet). Zugriff: IDataManager.GetSubrowExcelSheet.
- PIXEL→WELT-Formel (hergeleitet aus Dalamud MapUtil, dekompiliert
  2026-07-11): display = 0.02·offset + 2048/scale + 0.02·welt + 1 und
  display = 2·pixel/scale + 1 (Check: pixel 0→1.0, 2048→42.0 bei
  SizeFactor 100) ⇒ welt = (pixel − 1024) · 100/SizeFactor − Offset.
  Map-Sheet: MapMarkerRange@8, SizeFactor@10, OffsetX@20, OffsetY@22.
  PRAXIS-CHECK offen: Ätheryt-Wegpunkt vs. Ätheryt-GameObject vergleichen.
- Y-Höhe: vnavmesh-IPC `Query.Mesh.PointOnFloor(Vector3 p, bool
  allowUnlandable, float halfExtentXZ) → Vector3?` (IPCProvider
  dekompiliert 2026-07-11; vnavmesh nutzt denselben Weg für FlagToPoint).
  Weitere Queries: NearestPoint/NearestPointReachable/IsPointOnMesh;
  `Nav.Pathfind(from, to, fly) → List<Vector3>` (Wegpunktliste!).
- FALLE `PointOnFloor(p, allowUnlandable, halfExtentXZ)` castet nach UNTEN
  (FindPointOnFloor) → auf einem Steg/erhöhten Weg schnappt es auf den
  Boden WEIT DARUNTER (Log 2026-07-11 19:52: Eingabe Y=-12,9 → Ergebnis
  Y=-50,5, 37 m tiefer; 18-m-Übergang wurde zum 40-m-Lauf ins Tiefgeschoss).
  Für Ziele auf Spielerhöhe stattdessen `NearestPoint(p, halfExtentXZ,
  halfExtentY)` → nächster Netz-Punkt in einer BEGRENZTEN Box (vertikal
  gedeckelt, fällt nicht durch). Signatur `<Vector3, float, float, Vector3?>`.
  ResolveFloorPoint nutzt jetzt NearestPoint(10,10) zuerst, PointOnFloor nur
  als Fallback.

### Zonenübergänge: die ECHTEN Grenzen (ilspycmd-verifiziert 2026-08-09)
- Das Kartensymbol eines Übergangs (MapMarker DataType 1/2, siehe oben) ist
  Kartengrafik: Karten-Pixel, KEINE Ausdehnung, KEINE Richtung. Es taugt zum
  Benennen und Auflisten, NICHT als Laufziel — man landet daneben statt
  hindurch (User-Meldung 2026-08-09 "ich komme nicht rüber, stehe evtl schief").
- Die echte Grenze führt die Layout-Engine: `ExitRangeLayoutInstance`
  (`Client.LayoutEngine.Layer`), `InstanceType.ExitRange = 41`.
  Eigene Felder: `ExitType`@128 (`ExitRangeType`: **ZoneLine = 1**,
  **Invisible = 2**), `ZoneId`(ushort)@132, `TerritoryType`(ushort)@134
  = Zielzone, `Index`(int)@136, `DestInstanceId`@140, `ReturnInstanceId`@144,
  **`PlayerRunningDirection`(float)@148**.
- Geerbt von `TriggerBoxLayoutInstance`: `Collider*`@48, **`Transform`@64**
  (`LayoutEngine.Transform`, Size 48: `Translation`@0, `Rotation`(Quaternion)@16,
  `Scale`@32 — Mitte UND Ausdehnung der Trigger-Box), `Priority`@112,
  `FlagsType`@116, `FlagsActive`@120.
- Zugriff: `LayoutWorld.Instance()` → `ActiveLayout`(`LayoutManager*`)@32 →
  `Layers` (`StdMap<ushort, Pointer<LayerManager>>`@552) → `LayerManager.Instances`
  (`StdMap<uint, Pointer<ILayoutInstance>>`@40), filtern auf
  `ILayoutInstance.Id.Type == ExitRange` (`Identifier`@24: `Type`(InstanceType)@1,
  `LayerKey`@2, `InstanceKey`@4). `ILayoutInstance.IsActive` ist ein Bitfeld
  in `Flags3`@43.
- FALLE beim Iterieren: `StdMap` liefert **`StdPair`** (`Item1`/`Item2`), NICHT
  `KeyValuePair` — und `Item2` ist ein `Pointer<T>`, also `.Item2.Value`.
- **`PlayerRunningDirection` ist jetzt GEMESSEN (offline, 2026-08-22).** Grundlage:
  alle `ExitRange`-Instanzen aus `planmap.lgb` — **978 Übergänge in 267 Zonen**,
  über Lumina (`LayerCommon.ExitRangeInstanceObject`) direkt aus dem sqpack, ohne
  laufendes Spiel. Die Übergänge stehen NUR in `planmap.lgb`, nicht in `bg.lgb`.
  - **Einheit: Radiant.** Der größte Betrag über alle 978 Werte ist 6,283 = 2π.
    (Ein 5-Grad-Raster gibt es NICHT durchgängig — nur 60,4 % der Werte liegen
    darauf. Die frühere Behauptung „alle zehn Werte" beruhte auf einer
    Stichprobe von zehn und trägt nicht als allgemeine Aussage.)
  - **Bezugssystem: `(sin θ, 0, cos θ)`** — dieselbe Konvention wie
    `Math.Atan2(dx, dz)` im Plugin. Belegt durch Gegenprobe: mit dieser Lesart
    zeigen 91,3 % der Richtungen aus der Zone hinaus, mit der vertauschten
    Lesart `(cos, sin)` nur 54,0 % — also Zufallsniveau. Der Test trennt.
  - **Sie zeigt IN DIE NEUE ZONE** (aus der alten hinaus): 702 von 769 Fällen in
    Zonen mit mindestens drei Ausgängen. Näherung für die Zonenmitte war der
    Schwerpunkt der Ausgänge; die restlichen 8,7 % erklären sich plausibel aus
    dieser groben Näherung, sind aber NICHT einzeln geprüft.
  - NICHT belegt: dass Partner-Grenzen 180° gegeneinander stehen. Ein Test über
    82 eindeutige Paare ergab das NICHT (Median 150° Abweichung von 180°).
    Der Test taugt allerdings wenig — Rückwege liegen oft an anderer Stelle als
    der Hinweg. Aus ihm folgt weder Bestätigung noch Widerlegung.
  - **`Scale` ist das HALBMASS - im Spiel gemessen 2026-08-22** (Sonde
    `ZoneExitProbe`, zwei Durchgaenge, Neu-Gridania <-> Tiefer Wald in beide
    Richtungen). Beweisfuehrung:
    - Hinweg (132 -> 148): Box Mitte (170,1|-10,6|159,0), Scale (15,6|3,8|15,0).
      Die Figur war nach der HALB-Lesart ab 0,8 s vor dem Wechsel in der Box,
      nach der VOLL-Lesart **nie**.
    - Rueckweg (148 -> 132): Box Mitte (129,0|63,4|-330,9), Scale
      (36,5|40,4|15,3). HALB ab 1,0 s vor dem Wechsel drin, VOLL **nie**.
    - Damit ist VOLL widerlegt: der Uebergang loeste beide Male aus, obwohl die
      Figur nach dieser Lesart nie in der Box war.
    - Der Abstand zwischen "Box betreten" und "Zone gewechselt" betrug 0,8 bzw.
      1,0 s. Diese Groesse ist in beiden Richtungen fast gleich - das ist die
      Uebergangs-/Ladeverzoegerung, waehrend der die Figur weiterlaeuft, und
      NICHT ein Hinweis auf eine kleinere Box.
  - **Die Laufrichtung ist damit auch in-game bestaetigt**, nicht nur statistisch
    offline: Hinweg 85 Grad -> Vektor (sin, cos) = fast reines +X, und der
    Spieler lief von X 153 auf 159. Rueckweg 180 Grad -> -Z, gelaufen von
    Z -298 auf -321. Beide Male zeigt sie in die neue Zone.
  - **`TerritoryType` der ExitRange stimmte beide Male** mit der tatsaechlich
    geladenen Zone ueberein (148 bzw. 132). Die Layout-Daten sind als Zielangabe
    verlaesslich.

**Praktische Folge fuer den Auto-Lauf:** Die Box des Uebergangs nach Tiefer Wald
reicht von X = 154,5 bis X = 185,7. Der Auto-Lauf endete am Netzrand bei
X = 152,5 - also **2,0 m** vor dem Boxrand statt der 18,6 m bis zur Boxmitte.
Ein Ziel am naechstgelegenen BOXPUNKT statt in der Boxmitte verkuerzt die Luecke
hier um den Faktor neun.

### Inhalts-Eingänge der ganzen Welt (offline-Sheet-Dump 2026-08-19)
- Frage: „wo sind ALLE Dungeon-/Prüfungs-/Raid-Türen, mit Stufe und Ort?" —
  ohne dass das Objekt geladen sein muss. Grundlage der Browser-Kategorie
  „Alle Inhalte" (`DutyEntranceService`).
- **Tür → Inhalt** liefert `DungeonSide` (schon vorhanden, dort dokumentiert):
  `EObj.Data` → Block 0x001D `InstanceContentGuide` bzw. 0x000D
  `ArrayEventHandler` → `InstanceContent` → `ContentFinderCondition`
  (Name, `ContentType`, `ClassJobLevelRequired`). 182 EObj im ganzen Spiel,
  davon 155 mit ContentType 2 (Dungeons), 4 (Prüfungen) oder 5 (Raids).
- **Tür → Ort** steht im `Level`-Sheet, NICHT in einem Marker-Sheet:
  `Level.Object` = die EObj-Zeilennummer, `Territory`, `Map`, `X`/`Y`/`Z`.
  Die Höhe ist ECHT (3D), anders als bei `MapMarker` — ein Kartenmarker ist
  2D und braucht die Boden-Schätzung, diese Position nicht.
- **`Level.Type` = 45 ist der EObj-Verweis**, gemessen über das ganze Sheet
  (61.346 Zeilen): Typ 45 hat einen eigenen `Object`-Id-Bereich
  2000002..2015509, der sich mit keinem anderen Typ überschneidet
  (8 = 12275..1059814 ENpc, 9 = 1..20058, 14 = 30006..34748,
  49 = konstant 5000000). Alle 175 auflösbaren Eingänge tragen Typ 45 —
  der Join `Level.Object == EObj.RowId` ist damit eindeutig, keine
  Zahlenkollision wie beim Low-Word-Weg in `DungeonSide`.
- **3 der 155 haben KEINE Level-Zeile** („Sägerschrei", zwei Eingänge zu
  „Verschlungene Schatten 3 - 1") — die werden weggelassen, nicht geraten.
  7 Inhalte haben MEHRERE Eingänge, teils in verschiedenen Zonen
  (z. B. „Götterdämmerung - Ravana": Dravanisches Vorland + Opferkammer).
- **Freigeschaltet?** fragt `UIState.IsInstanceContentUnlocked(instanceContentId)`
  (statisch, ohne this-Zeiger; gegen die installierte FFXIVClientStructs.dll
  geprüft). Daneben gibt es `IsInstanceContentCompleted` und die
  Public-Content-Paare. Der Schlüssel ist die `InstanceContent`-Zeile
  (`ContentFinderCondition.Content.RowId`), NICHT die CFC-Zeile.
- Erwartungswerte für den Test (offline vorausberechnet, deutscher Client):
  152 Eingänge mit Ort, nach Entdopplung pro Inhalt **145 Listeneinträge**;
  erster Eintrag Stufe 15 „Sastasha" (Westliches La Noscea), 7 Einträge
  bis Stufe 30.

### Talk / TalkSubtitle (Log-verifiziert 2026-07-11)
- AddonTalk hat nur UNBENANNTE Text-Node-Felder (AtkTextNode220/228/238/
  240/248, ilspycmd) — kein benanntes „Name"-Feld.
- PROBE-VERIFIZIERT (Dialog-Nodes-Zeilen, Sessions 09:36 + 10:14):
  Talk-Sprechername = Text-Node id=2, Dialogtext = id=3. Der Name-Node
  kommt in Node-Listen-Reihenfolge NACH dem Text (zuletzt).
- `_BattleTalk` (Kampf-Sprechblase, [ArenaText]-Log 2026-07-26 16:26): NPC-/
  Lehrer-Ansagen in Instanzen und im Kampfuebungsplatz ("Erledigt zuerst den
  Thaumaturgie-Lehrer", "Das ist der falsche Gegner!"). Sprechername = Text-Node
  id=4, Ansagetext = id=6. Wird vom SELBEN Handler (OnTalkUpdate) gelesen; die
  Sprecher-Node-Id ist addon-abhaengig (Talk=2, _BattleTalk=4). V5.55.

### ConfigSystem (Systemkonfiguration, Dump 2026-07-11 10:16, 593 Nodes)
- Kategorie-Tabs: 8× CT=DragDrop(17), NodeIds 7–14 (Indizes [581]–[588],
  am Ende der Node-Liste). Aktiver Tab: Kind id=4 sichtbar.
- Seiten-Überschrift = Top-Level-Text id=22 (z. B. „Anzeigeeinstellungen").
- FALLE: Top-Level-Text id=4 = fps-Zähler („59 fps"), liegt bei
  Rückwärtssuche VOR der Überschrift und ändert sich sekündlich →
  Überschriften-Suche muss volatile Texte (fps/Zahlen) überspringen.
- Controls: CheckBox(3)/RadioButton(4)/Slider(6)/DropDownList(10),
  Label = Kind-Text id=2 der Komponente; Abschnitts-Überschriften sind
  eigenständige Top-Level-Texte (id 575 „Farbwahrnehmung" usw.).
- Fußnoten-Buttons: „Voreinstellung"/„Schließen"/„Anwenden" (Comp 1001?
  via Kind id=2-Text).
- Lautstärke-Regler (Reiter „Sound", V5.58): Zeilen-Muster Top-Level-Text-Label
  → Stumm-CheckBox Comp(1027) → Slider Comp(1023); Label = nächster vorangehender
  Top-Level-Text (NearestPrecedingLabel). Slider laufen 0..100 → als Prozent
  ansagen. `NearestPrecedingLabel` findet z. B. „Hauptlautstärke" vor Slider id=113.
  KURZFORM für 0..100-Slider: „Label, Wert %" (kein „Regler, von 0 bis 100" —
  die Langform wurde beim schnellen Navigieren abgeschnitten, User 2026-07-27).
- FALLE Doppel-Ansage (V5.58): Audio-Slider tragen den Wert als Text-Kind id=2
  („100"); der GENERISCHE Fokus-Leser las diese nackte Zahl ~14 ms nach der
  Config-Ansage und würgte das Label ab. Fix: nackte Zahlen überspringen, solange
  ConfigSystem sichtbar ist (wie bei JournalResult).
- Schalter-Zustand (V5.58): CheckBox-Ansage „Label, Schalter, an/aus"; deaktivierte
  („ausgegraut") erkannt an **`NodeFlags.Enabled` (0x20) gelöscht** am Komponenten-
  Node — ilspycmd-verifiziert gegen FFXIVClientStructs; Dump: aktiv F=0x2033 vs.
  ausgegraut F=0x2013 (z. B. Hintergrund-Wiedergabe-Unterpunkte bei Master AUS,
  und der „Anwenden"-Button vor einer Änderung).
- Barrierefreiheit = Reiter 8 (DragDrop, Tooltip „Barrierefreiheit"). Seite schaltet
  beim NAVIGIEREN um und wird gelesen (Farbwahrnehmung/Töne visualisieren/Transparenz
  etc.). Enter wird in ConfigSystem vom Spiel geschluckt (IKeyState sieht es nicht) →
  die eigene Reiter-Enter-Aktivierung (TryActivateFocusedConfigTab) feuert dort nie;
  für den Seitenwechsel aber nicht nötig. Überschrift zeigt „Anzeigeeinstellungen"
  (offener kosmetischer Punkt).
- JournalDetail: Begleit-Addon (nie fokussiert, ChildAddonAttached an
  Journal). Inhalt liegt im Comp CT=JournalCanvas(20), direkte Text-Kinder:
  id=38 Quest-Titel, id=9 Stufe, id=8 Beschreibungstext, id=7 Label
  „Beschreibung", id=11 Label „Ziel". Quest-Ziele = Multipurpose(21)-
  Komponenten mit nicht-leerem id=3-Text („Mit Miounne sprechen").
  Labels stehen in Node-Reihenfolge NACH ihrem Inhalt (Z-Order).

### Kampf: Gegner-HP, Cast, Hotbar (ilspycmd-verifiziert 2026-07-11)
- Gegner-/Ziel-Daten über Dalamud `IBattleChara` (erbt `ICharacter`):
  `CurrentHp`/`MaxHp`/`CurrentMp`/`MaxMp` (uint, aus ICharacter);
  `IsCasting` (bool), `IsCastInterruptible` (bool), `CastActionType`
  (byte), `CastActionId` (uint), `CastTargetObjectId` (ulong),
  `CurrentCastTime`/`TotalCastTime` (float). Zugriff: `ITargetManager.Target
  as IBattleChara` (nur Character-Objekte haben HP; NPCs/Objekte casten null).
- Cast-Aktionsname: Lumina-Sheet `Action` (Lumina.Excel.Sheets.Action),
  `.Name` ist `ReadOnlySeString` → `.ExtractText()`; Zugriff
  `IDataManager.GetExcelSheet<Action>().TryGetRow(CastActionId, out row)`.
  Namespace-Kollision mit System.Action → `using LuminaAction = ...`.

### AoE-Form/Radius (Action-Sheet, ilspycmd-verifiziert 2026-07-26)
Für das AoE-Ausweich-Feature nötig. Lumina `Action`-Sheet-Felder (Offsets aus
Lumina.Excel.Sheets.Action dekompiliert):
- `CastType` (byte, @40) — die FORM der Aktion (Kreis / Kegel / Linie / Donut …).
  ⚠️ Das Sheet liefert NUR die Zahl, keine Bedeutung. Die Zuordnung
  Zahl→Form ist Community-Wissen, aber NICHT am Code verifiziert → wird per
  DEBUG-Sonde `CombatService.AoeCastProbe` empirisch belegt, bevor darauf
  „stehst-drin"-Logik gebaut wird. NICHT hartcodiert raten.
- `EffectRange` (byte, @41) — Reichweite/Radius.
- `XAxisModifier` (byte, @42) — Breite (für Linien/Rechtecke).
- `Omen` / `OmenAlt` (RowRef<Omen>, @28/@30) — Verweis auf die Telegraph-Grafik
  (Boden-Markierung). Noch nicht ausgewertet.
- CASTTYPE->FORM (aus [AoeProbe]-Log + OmenPath belegt, Kampfuebungsplatz 2026-07-26):
  - `2` = KREIS an der Ziel-Position (Feura, EffectRange=5, OmenPath 'general_1b').
    Zentrum = Ziel-Objekt des Casts (CastTargetObjectId), sonst Caster. Boden-
    platzierte Kreise ohne Ziel-Objekt haben ihr Zentrum nur in der VFX -> noch offen.
  - `3` = KEGEL vom Caster in Blickrichtung (Kahlrodung, EffectRange=6=Laenge,
    OmenPath 'gl_fan090' = 90 Grad voll). Halbwinkel = fan-Zahl/2. Andere Kegel:
    fan060/fan120 -> Winkel aus dem Namen parsen.
  - `4` = LINIE/RECHTECK vom Caster in Blickrichtung (Spalten, EffectRange=30=Laenge,
    XAxisModifier=2, OmenPath 'general02'). ANNAHME: Halbbreite = XAxisModifier
    (in-game verifizieren). Achtung: EffectRange ist hier die LAENGE, NICHT ein
    Radius -> Linie als Kreis behandeln = riesige Falsch-Zone (war der V1-Bug).
  - Unbekannte Typen: konservativ Caster-Kreis (lieber ueber- als unterwarnen).
  - Geometrie umgesetzt in `CombatService.IsPlayerInAoe` (V5.55).
- MESS-SONDE (V5.55, #if DEBUG, auto pro Frame): `AoeCastProbe` iteriert die
  ObjectTable, loggt je castenden IBattleChara (dedupe per casterId, rising edge)
  `[AoeProbe]` mit castId/Name/CastType/EffectRange/XAxisModifier/Omen + Geometrie
  (casterPos, rot, playerPos, dist, relBearing per verifizierter Rotations-
  Konvention, atMe, castTime). Zweck: CastType-Zahlen gegen das mappen, was der
  Spieler wirklich sieht. Aus Release rauskompiliert.

- Hotbar: `RaptureHotbarModule.Instance()` (via UIModule, direkte statische
  Instance() vorhanden). `GetSlotById(uint hotbarId, uint slotId)` →
  `HotbarSlot*`. UI-„Aktionsleiste 1" = hotbarId 0; 16 Slots/Leiste,
  Standard-Tasten 1–9,0 = Slots 0–9, Slots 10/11 = Tasten 11/12
  (HOTBAR_1_A/B = VK137/139).
- `HotbarSlot`: `CommandType` (Enum `HotbarSlotType : byte`, Empty=0,
  Action=1, Item=2, …, GeneralAction, Macro, Emote, Mount …),
  `CommandId`@184 (uint = ActionId bei Type Action), `PopUpHelp`@0
  (Utf8String = spiel-eigener Anzeigename inkl. Keybind-Hinweis, universell
  für alle Typen; als Fallback nach Lumina). Weitere nützliche Member:
  `IsSlotUsable(type, id)`, `IsSlotActionTargetInRange2(type, id)` (für
  spätere Cooldown-/Reichweiten-Ansage, noch ungenutzt).

### Cooldown / Recast (ActionManager, ilspycmd-verifiziert 2026-07-30)
- `ActionManager.Instance()` → `ActionManager*`. Alle Cooldown-Abfragen nehmen
  `ActionType` (Enum: None=0, **Action=1**, Item=2, GeneralAction=5 …) + actionId.
- INSTANZ-Methoden (`am->…`):
  - `GetRecastTime(ActionType, uint id) → float` = GESAMT-Abklingzeit der Action
    (unabhaengig vom aktuellen Stand). GCD-Skills ~2,5 s; echte Fähigkeiten (oGCD)
    deutlich mehr. `CooldownService` nutzt Schwelle >3 s, um GCD auszuschliessen —
    ohne die build-spezifische GCD-Recast-Gruppen-Id raten zu muessen.
  - `IsRecastTimerActive(ActionType, uint id) → bool` = laeuft die Abklingzeit noch
    (true = auf Cooldown). Fallende Kante true→false = „wieder bereit".
  - `GetRecastTimeElapsed(ActionType, uint id) → float` = bisher verstrichen.
  - `IsActionOffCooldown(ActionType, uint id) → bool`.
  - `GetCurrentCharges(uint id) → uint` (Instanz).
- STATISCH (ohne thisPtr): `ActionManager.GetMaxCharges(uint id, uint level) → ushort`.
  maxCharges>1 = Ladungs-Fähigkeit; dann zaehlt die Ladungs-Anzahl (IsRecast-
  TimerActive bleibt bis VOLL true, deshalb Ladungen als Signal nutzen).
- Weiter vorhanden (noch ungenutzt): `GetRecastGroup(int type, uint id) → int`
  (GCD-Gruppe existiert, Nr. build-abhaengig — NICHT hartkodiert), `GetActionRange`,
  `GetActionCost`, `GetAdjustedRecastTime`, `StartCooldown`, `GetActionStatus`.
- Genutzt von `CooldownService` (V5.61): Standard-Leisten 0..9 durchgehen, Action-
  Slots dedupen, GCD per >3 s ausschliessen, Kante on→off ansagen (Ton+Name).

### Hotbar UMBELEGEN + gelernte Skills (ilspycmd-verifiziert 2026-07-17)
- `RaptureHotbarModule.SetAndSaveSlot(uint hotbarId, uint slotId,
  HotbarSlotType commandType, uint commandId, bool ignoreSharedHotbars=false,
  bool allowSaveToPvP=true)` — schreibt NUR den GESPEICHERTEN Hotbar-
  Zustand, NICHT die Live-Leiste! IN-GAME BEWIESEN (2026-07-17): Zuweisung
  9:43 blieb live wirkungslos (auch 2 Frames später, Leiste nicht geteilt),
  erschien aber nach dem Relog um 11:57 auf der Leiste. Die GitHub-Doku
  („sets a hotbar slot and triggers a save") ist hier irreführend.
  ⇒ Für sofortige Wirkung danach `LoadSavedHotbar(classJobId, hotbarId)`
  aufrufen („loads the saved hotbar into the live hotbar, will not reload
  from disk", respektiert PvP automatisch) — V4.78 macht das; Erfolg per
  Read-back über `GetSlotById` prüfen (2 Frames später).
  Verwandt: `ClearSavedSlotById(hotbarId, slotId)` (Slot leeren),
  `ExecuteSlotById(hotbarId, slotId)` (Slot auslösen, byte-Rückgabe),
  `IsHotbarShared(hotbarId)` (bool).
- Lumina `Action`-Sheet (Spalten dekompiliert 2026-07-17): `Name`,
  `ClassJobLevel` (byte; 0 = keine per-Stufe-gelernte Spieler-Action),
  `ClassJobCategory` (RowRef, bool-Spalte je Job wie beim Item-Sheet —
  Spaltenwahl über engl. ClassJob-Abkürzung, s. GearInfoService.AllowsJob),
  `ClassJob` (RowRef), `IsPvP`, `IsRoleAction`, `IsPlayerAction` (packed
  bools), `UnlockLink` (untypisierte RowRef, uint bei Offset+4; 0 = keine
  Quest-Freischaltung nötig).
- Freischaltungs-Check: `UIState.Instance()->
  IsUnlockLinkUnlockedOrQuestCompleted(uint unlockLinkOrQuestId, byte
  minQuestProgression=0, bool a4=true)` — nimmt laut Signatur UnlockLink-
  ODER Quest-Id (deckt beide Fälle der UnlockLink-Spalte ab). UIState liegt
  in `FFXIVClientStructs.FFXIV.Client.Game.UI`.
- Skill-Browser-Filter: RowId!=0, !IsPvP, ClassJobLevel 1..Spielerstufe,
  ClassJobCategory enthält aktuellen Job, UnlockLink erfüllt, UND
  IsPlayerAction==true — OHNE letzteres rutschen interne Zeilen durch den
  Job-Filter (in-game belegt 2026-07-17 12:01: fünfmal „Ausweichen" +
  „Perfekter Hieb" bei Job 26). OFFEN: exakter Abgleich mit dem Fenster
  „Aktionen & Traits" (Log `[Hotbar] Skill-Liste gebaut` zeigt Anzahl).
- KEINE Unlock-Methode in `ActionManager` (komplett durchgesehen 2026-07-17)
  — Action-Freischaltung läuft nur über UIState/UnlockLink.
- LEISTEN-ANZAHL (ilspycmd 2026-07-17): `RaptureHotbarModule.Hotbars` =
  FixedSizeArray18<Hotbar>; `StandardHotbars` = Hotbars[0..9] (10 Stück,
  UI „Kommandomenü 1–10"), `CrossHotbars` = Hotbars[10..17] (Gamepad).
  Jede Hotbar hat 16 Slots (FixedSizeArray16<HotbarSlot>), Standard-UI
  nutzt 12. `GetSlotById(uint hotbarId, uint slotId)`,
  `LoadSavedHotbar(uint classJobId, uint hotbarId)` und SetAndSaveSlot
  nehmen alle die Leisten-Nummer — der V4.78-Pfad gilt für jede Leiste.
- HOTBAR-TASTEN im InputId-Enum (Live-Dump 2026-07-17):
  `HOTBAR_{Leiste}_{Suffix}` mit Suffix 1..9, 0, A, B = Slot 0..11
  (HOTBAR_1_1=57, Blöcke à 12 direkt hintereinander). Leiste 2 ist
  standardmäßig Strg+1..Strg+0 (+Strg+VK137/139 für Slot 11/12);
  Leiste 3+ unbelegt. Live-Abfrage: KeybindService.GetBoundKey
  (Enum.TryParse<InputId> → GetKeybindSpan()[Index], V4.81).

### ConfigKeybind — Fenster „Tastenbelegung" (F5-Dump 2026-07-17)
- KORREKTUR (Log 2026-07-17 13:12, widerlegt den 09:45-Befund):
  Pfeiltasten bewegen den GLOBALEN Fokus (AtkInputManager.FocusedNode),
  die Listen-Indizes stehen still (Hov2 blieb 0, nur EINE
  List-Navigation beim Öffnen). Die Liste scrollt dabei UNTER einem
  festen Fokus-Node (gleicher Node-Ptr, wechselnder Zeilentext) —
  Zeilen-Ansagen müssen deshalb pro Frame neu gelesen werden, nicht
  nur bei Fokus-Wechsel. ListLen wechselt je Kategorie-Reiter
  (Bewegung 32, Schnelltasten 134). Ansage läuft seit V4.79 über
  UpdateGlobalFocus → ClimbToItemRenderer → dedizierter Zeilen-Leser.
- FALLE dabei: GetTextFromNodeTree verwirft Texte der Länge 1 —
  einstellige Tasten-Labels („W", „1", „C") fehlten deshalb im
  generischen Fokus-Pfad, mehrstellige („Tab", „NUM0") nicht.
- Zeile = ListItemRenderer(14) mit: direktem Text id=2 = Befehlsname
  („Kommandomenü 1 - Slot 1"), Button-Komponente id=6 = Belegung 1,
  Button id=5 = Belegung 2; der Tasten-Text steckt JEWEILS in einem
  Text-Kind id=5 IN der Button-Komponente. Der generische
  ReadListItemText liest nur direkte Text-Nodes → Tasten fehlten in
  der Ansage (Fix V4.77: ReadConfigKeybindRow).
- Deutsch: Hotbar heißt in der UI „Kommandomenü", Reiter als
  RadioButtons: Bewegung/Zielen/Schnelltasten/Chat/System/Kommandos/
  Gamepad; Knöpfe Schließen/Anwenden/Zurücksetzen; Checkbox
  „Direkt-Chatmodus aktivieren".
- WICHTIG (Semantik): Dieses Fenster ändert TASTE→SLOT-Bindungen
  („welche Taste feuert Kommandomenü 1 - Slot 1"), NICHT welcher
  Skill im Slot liegt (das ist die Hotbar selbst / SetAndSaveSlot).
- OFFEN: Was löst Enter auf einer Zeile aus (Erfassungsmodus für
  neue Taste?) — nie getestet, kein Handler; nächster In-Game-Test.
- StdList (z. B. `Map.UnacceptedQuestMarkers`): implementiert
  `IEnumerable<T>`+`Count`; `GetEnumerator()` liefert Struct-Enumerator
  (foreach allokationsfrei), yield by value (read-only-Kopie sicher).

### Toasts / Fehlermeldungen (IToastGui, ilspycmd-verifiziert 2026-07-17)
- Aktions-Fehler („Das Ziel ist zu weit entfernt.", „Die Aktion ist
  noch nicht bereit.") sind FEHLER-TOASTS im Overlay `_TextError`.
- FALLE: `_TextError` feuert PostRefresh NIE — Log 2026-07-17 zeigt
  über eine ganze Session nur das leere PostSetup beim Login. Der
  Lifecycle-Ansatz (NotificationAddons) kann diese Meldungen also
  prinzipiell nicht liefern. In den Chat gespiegelt werden die
  meisten Aktions-Fehler ebenfalls nicht.
- Sauberer Weg: `Dalamud.Plugin.Services.IToastGui` (ilspycmd an
  Dalamud.dll): Events `ErrorToast(ref SeString, ref bool isHandled)`,
  `Toast(ref SeString, ref ToastOptions, ref bool)`,
  `QuestToast(ref SeString, ref QuestToastOptions, ref bool)` —
  feuern auf dem Show-Toast-Aufruf des Spiels. Seit V4.80 liest
  ToastService.cs sie vor (Fehler = Interrupt, Info/Quest = Queue
  mit WasRecentlySpoken-Echo-Schutz, da manche Info-Toasts parallel
  als `_WideText`/`_ScreenText` gezeichnet werden).

## Werkzeuge / Traps

- FALLE NodeType: Komponenten-Nodes tragen im ROHEN Type-Feld Werte
  >= 1000 (1003, 1006, 1027, …). `NodeType.Component` ist 10000 und wird
  nur von GetNodeType() zurückgegeben (ilspycmd 2026-07-11, Doku-Remark
  im Enum). Ein Vergleich `node->Type == NodeType.Component` ist deshalb
  IMMER falsch — so war FindListInAddon seit Einführung tot und die
  universelle Listen-Navigation hat nie gefeuert (Journal, SystemMenu,
  SelectString in-game alle stumm). Richtig: `(int)node->Type >= 1000`.

- FALLE dalamudConfig.json: Dalamud liest sie über ReliableFileStorage
  (rohe Bytes → UTF8.GetString, KEIN BOM-Strip). Eine mit BOM geschriebene
  Datei (PowerShell 5.1 `Set-Content -Encoding utf8`!) wirft im Parser
  JsonReaderException → Dalamud fällt STILL (nur Verbose-Log) auf seine
  SQLite-Sicherung `dalamudVfs.db` zurück und überschreibt die Datei beim
  nächsten Speichern mit dem alten Stand. Externe Edits gehen so lautlos
  verloren. Immer BOM-los schreiben: `[IO.File]::WriteAllText(path, text,
  UTF8Encoding($false))`. (Bewiesen 2026-07-10 per Repro mit Dalamuds
  eigenen Serializer-Settings; kostete drei mysteriöse Fehlversuche)
- Dev-Plugins lädt Dalamud NUR aus `DevPluginLoadLocations` in
  dalamudConfig.json (+ DevMode=true) — der devPlugins-Ordner allein
  genügt NICHT. Neue Dev-Plugins brauchen zudem DevPluginSettings-Eintrag
  mit StartOnBoot=true
- ilspycmd 9.1.0: `--list-types` kaputt (1 Zeile) — aber `-l c` (Klassen),
  `-l s` (Structs), `-l e` (Enums) funktionieren; Typen einzeln per `-t`
- UIReaderService.cs hat gemischte Zeichenkodierung — bei Edits old_strings
  ohne Umlaute wählen; einmal steckte ein U+2000-Space drin (per awk ersetzt)
- MEMORY_BASIC_INFORMATION braucht Size=48 (nicht 44), sonst scheitert
  VirtualQuery IMMER still → IsReadable-Helfer mit Positivtest verifizieren
- Dalamud lädt Dev-Plugin DIREKT aus `bin\Debug\net10.0-windows\` —
  nach jedem Build Spiel neu starten

### Stufe / Erfahrung (PlayerState, ilspycmd-verifiziert 2026-07-12)
`PlayerState.Instance()` (FFXIVClientStructs.FFXIV.Client.Game.UI):
- `CurrentLevel` (short) = Stufe des AKTIVEN Jobs (echte Stufe; daneben
  `SyncedLevel`/`IsLevelSynced` bei Level-Sync in Dungeons)
- `CurrentClassJobId` (byte) = aktiver Job (zum Level-Up-Tracking, damit ein
  Jobwechsel nicht als "Level-Up" zählt)
- `ps->GetCurrentClassJobExp()` (uint) = aktuelle EXP in DIESER Stufe
- `ps->GetCurrentClassJobNeededExp()` (uint) = EXP für die nächste Stufe;
  == 0 bei Maximalstufe
- "Noch bis Level-Up" = NeededExp − CurrentExp
- WICHTIG: Die statischen `delegate* unmanaged<PlayerState*,uint>`-Properties
  NICHT als `PlayerState.GetCurrentClassJobExp(ps)` aufrufen (Compiler wählt die
  0-Arg-Instanzmethode → CS1501). Instanzmethode am Pointer nutzen: `ps->GetCurrentClassJobExp()`.
- Level-Up-Ansage: CurrentLevel jeden Frame lesen, bei Anstieg (gleicher Job)
  ansagen — sauber aus PlayerState, kein UI-Scraping. (CombatService.TrackLevelUp)
- XP-Gewinn-Ansage (V5.52, User-Wunsch 2026-07-25): GetCurrentClassJobExp() jeden
  Frame lesen, bei Anstieg (gleicher Job) das Delta ansagen ("X Erfahrung") und in
  den Nachlese-Kanal "Beute" schreiben. Baseline pro Job (Job-Wechsel aendert den
  Wert ohne echten Gewinn) + Level-Up-Ruecksprung (Wert faellt Richtung 0) nur
  stumm nachziehen; needed==0 (Maxstufe) => kein Tracking. Nicht-unterbrechend
  (Speak), damit XP nie eine HP-/Cast-Warnung abschneidet. (CombatService.TrackXpGain)

### Loot-Kanal (eingesammelte Gegenstaende) — VERIFIZIERT (Live-[Chat]-Log 2026-07-25)
Beute/Waehrung, die ins Inventar wandert, kommt ueber **XivChatType.LootNotice
(62)** — leerer Sender, voller Satz ("Du hast ein Lammfilet erhalten.", "Du hast
115 Gil erhalten.", "Du hast 17 Legionstaler erhalten."). Deckt Gegner-Drops
(Schaf -> Lammfilet/Schafsbockhorn), Gil, GC-Taler und Sammel-Kristalle ab. Liegt
AUSSERHALB des Kampflog-Bereichs (41-49), wird also NICHT von IsCombatLogLine
verworfen — kam von Anfang an sauber im [Chat]-Log an, nur ungelesen (ShouldRead
default false). V5.52: LootNotice -> ReadLoot (Config AnnounceLoot), Nachlese-Kanal
"Beute" (gemeinsam mit XP), kein Prefix. Gathering (67) bleibt der separate
Abbau-Kanal.
- OFFEN (nicht verifiziert): Instanz-/Dungeon-Beute per Wuerfelsystem (Bedarf/Gier)
  koennte ueber einen anderen Kanal (LootRoll?) laufen — bei Bedarf spaeter aus
  einem Dungeon-Log nachziehen.

### Emotes ausführen (ilspycmd-verifiziert 2026-07-12)
- `AgentEmote.Instance()` (FFXIVClientStructs.FFXIV.Client.UI.Agent):
  - `agent->ExecuteEmote((ushort)emoteId, playEmoteOption=null, addToHistory=true, liveUpdateHistory=true)`
    — löst ein Emote direkt aus (dieselbe Funktion wie das Gesten-Menü);
    kein Chat/keine UI nötig. Externer Call → try-catch.
  - `agent->CanUseEmote((ushort)emoteId)` — true wenn freigeschaltet.
- Lumina-Sheet `Emote`: RowId == emoteId für ExecuteEmote; `Name` = Anzeigename
  ("Verbeugen"); `TextCommand` = RowRef<TextCommand> → `.Command` = echter
  /befehl (WICHTIG: deutscher /befehl ≠ Anzeigename; "/verbeugen" existiert NICHT
  — Befehl aus dem Sheet lesen, nicht raten). `Order`/`EmoteCategory` für Sortierung.
- Umgesetzt: EmoteService (Browser: Umschalt+F4/F5 blättern nutzbare Emotes
  alphabetisch, Umschalt+F6 führt aus). Grund: blinder User kann Chat nicht
  tippen und Icon-Gesten-Palette nicht navigieren.

### JournalResult Belohnungs-Fenster (UI-Dump-verifiziert 2026-07-12)
JournalCanvas enthält Belohnungs-Einträge als Multipurpose(21)-Komponenten:
- ITEM-Belohnung: Comp(1010) Multipurpose → Fokus auf id=3 Collision, Kind
  id=2 Comp(1003) Icon(15) = AtkComponentIcon (IconId → Name via ResolveIconName,
  Menge in QuantityText/id=7). Leerer Slot = IconId 0.
- WÄHRUNG/EXP: Comp(1007) Multipurpose → Fokus auf id=5 Collision, Betrag in
  Kind id=2 Comp(1011) TextNineGrid(19) → id=2 Text ("260"/"127"). Der TYP
  (Erfahrung/Gil) steht NUR als id=3 Image (kein auflösbares Icon) → aktuell
  per Position gelabelt (Erfahrung zuerst, dann Gil = Standard-FF14-Reihenfolge).
- Buttons: id=38 "Ablehnen", id=37 "Abschließen".
- Umgesetzt: UIReaderService.BuildRewardText liest beim Öffnen "Belohnung: <Items>,
  Erfahrung X, Gil Y". Grund: Fokus-Navigation der Währungszellen sagte nur nackte
  Zahlen (User: "ich will wissen was der Eintrag ist").

### Hauptszenario-Quest erkennen (MSQ, ilspycmd-verifiziert 2026-07-12)
- Lumina Quest.JournalGenre → JournalGenre.JournalCategory → JournalCategory.
  JournalSection. MSQ = JournalSection.RowId == 0 ("Hauptszenario").
- Umgesetzt: QuestMarkerService baut 1× ein HashSet der MSQ-Quest-Namen aus dem
  Quest-Sheet und matcht die Marker-Label dagegen (MarkerInfo hat keinen direkten
  Quest-Zeiger, nur Label + ObjectiveId). QuestDestination.IsMainStory → Ansage
  "Story: <Quest>". [Quest] Hauptszenario-Namen-Log zeigt die Anzahl.
- MarkerInfo-Felder: ObjectiveId(uint), Label(Utf8String), MarkerData(StdVector),
  RecommendedLevel(ushort), ShouldRender(bool). MapMarkerData: IconId, Position,
  MapId, TerritoryTypeId, ObjectiveId, MarkerType(byte), Flags(byte), DataId.

### Dalamud-eigene UI ist ImGui — nicht lesbar (verifiziert 2026-07-19, ilspycmd Dalamud.dll)
- Dalamuds Plugin-Installer, Dalamud-Einstellungen und die Fenster fremder
  Plugins (z.B. vnavmesh) werden in **ImGui** gezeichnet: kein AtkUnitBase,
  keine Nodes, kein Baum. Weder UIReaderService noch NVDA können dort etwas
  finden. Es gibt KEINEN UI-Scraping-Weg — nicht danach suchen.
- Richtiger Weg: die DATEN hinter der UI lesen.
  `IDalamudPluginInterface.InstalledPlugins` → `IEnumerable<IExposedPlugin>`.
- `IExposedPlugin` (öffentlich, `Dalamud.Plugin`): Name, InternalName, Version,
  IsLoaded, IsOutdated, IsTesting, IsOrphaned, IsDecommissioned, IsBanned,
  IsDev, IsThirdParty, Manifest, HasMainUi, HasConfigUi,
  `OpenMainUi()`, `OpenConfigUi()` (werfen InvalidOperationException, wenn das
  jeweilige HasXUi false ist).
- `IDalamudPluginInterface.OpenPluginInstallerTo(kind, searchText)` öffnet nur
  das ImGui-Fenster → für blinde Nutzer wertlos. `CheckForUpdateAsync()` gilt
  NUR fürs eigene Plugin.
- NICHT öffentlich: Installieren/Updaten/Entfernen. `InstallPluginAsync`,
  `UpdatePluginsAsync`, `UpdateSinglePluginAsync`, `RemovePlugin`,
  `UpdatablePlugins` liegen in `Dalamud.Plugin.Internal.PluginManager`
  (internal) — nur per Reflection erreichbar, bricht potenziell still bei
  Dalamud-Updates. Bewusst nicht genutzt (User-Entscheid 2026-07-19);
  Installation/Update laufen über die Installer-EXE.
- Genutzt in `DalamudPluginsService.cs` (V5.13, Umschalt+F1/F2/F12).

### GrandCompanyExchange (Staatstaler-Quartiermeister) — F5-Dump 2026-07-25 (V5.47)
Shop, in dem man Staatstaler (Grand Company Seals) gegen Gegenstaende eintauscht.
Wird von der GENERISCHEN Listen-Navigation erfasst (nicht unterdrueckt, hat eine
`List(9)`), aber die generische Zeile las kryptisch „0, 1.060, Legionaers-Schwert"
(Spaltenreihenfolge, ohne Label, teils doppelt bei Sichtbarkeits-Flackern).
- Addon „GrandCompanyExchange", Fenstertitel `Comp(1007)` Kind id=3 = „STAATSTALER
  EINTAUSCHEN".
- **Item-Liste**: id=57 `Comp(1014)` `[CT=List(9)]`, `ListLen=21`. Tastatur trackt
  wie ueberall `HoveredItemIndex2` (@344) — die generische `TrackListIndices` sagt an.
- **Zeilen-Template** (`ListItemRenderer`/`Comp(1015)`, jede Zeile identisch):
  - id=10 Text = **Besitz** (wie viele man schon hat), z.B. „0".
  - id=7  Text = **Preis in Staatstalern**, z.B. „1.060".
  - id=6  `Comp(1011)` `NumericInput` = **Kaufmenge**, Kind-Text id=5 = „1"
    (liegt in der EIGENEN ULD des NumericInput, NICHT im Renderer-NodeList → wird
    vom generischen Reader und vom dedizierten Reader NICHT versehentlich gelesen).
  - id=5  Text (UNSICHTBAR) = Item-Name-Duplikat.
  - id=4  Text (sichtbar) = **Item-Name** (SeString-Payload, Sanitize noetig).
- **Kategorie-Reiter** (`RadioButton(4)`/`Comp(1008)`, Text-Kind id=2): id=44 Waffen,
  id=45 Ruestung, id=46 Militaerbedarf, id=47 Materialien, id=48 Besondere Artikel.
  Der AKTIVE Reiter = der RadioButton mit `IsChecked==true`. `AtkComponentButton.IsChecked`
  = `BitOps.GetBit(Flags@232, 18)` (ilspycmd 2026-07-25; `AtkComponentRadioButton`
  erbt `AtkComponentButton`). KEIN gemeinsamer Titel-Node wie bei ArmouryBoard (id=121) —
  darum Reiter ueber checked-State ermitteln, Label aus Text-Kind id=2. V5.47:
  `OnGrandCompanyUpdate` sagt bei Reiterwechsel „Kategorie X" (die Rang-Icons `Comp(1016)`
  sind auch RadioButtons, haben aber KEIN Text-Kind id=2 → per leerem Label gefiltert).
- **Rang-Icons** (`RadioButton(4)`/`Comp(1016)`, id=37–42): OHNE Text → der globale
  Fokus-Reader pendelt hier stumm (Log 2026-07-25, [Focus] STUMM, alle ~0,3s).
- Addon-Root-Texte: id=6 = eigener GC-Rang („Legionaer 3. Klasse"), id=8 = eigenes
  Staatstaler-Guthaben („300"). (Noch NICHT genutzt — Kandidat fuer Oeffnungs-Ansage,
  PostSetup-Timing unverifiziert.)
- **Loesung V5.47**: dedizierter `ReadGrandCompanyRow` (Name/Preis/Besitz per
  `ReadComponentTextById` id 4/7/10) → „Name, X Staatstaler, Besitz Y"; eingehaengt im
  `name switch` von `TrackListIndices`. Stabiler Text ⇒ `idx|text`-Dedup killt das Doppel.

## Fischen (ilspycmd-verifiziert 2026-07-25, FFXIVClientStructs.dll + Lumina.Excel.dll)

Ziel: Angeln barrierefrei. Erster Schritt „wo kann ich angeln" — Laufzeit-Sonde
`/acc fishprobe` (FishingService.Probe, read-only) loggt (A) alle Objekte in 200 m
mit ObjectKind/DataId/Position und (B) den FishingSpot-Katalog der Zone mit
Roh-X/Z + Umrechnung. NOCH NICHT verifiziert (Sonde offen): ob Angel-Loecher in
der ObjectTable auftauchen (und als welche ObjectKind), und die X/Z-Skalierung.

### Laufzeit-Zustand: FishingEventHandler (Client.Game.Event, Size 560)
- Erbt `EventHandler` + `AtkModuleInterface.AtkEventInterface`. Zugriff ueber
  `EventFramework.Instance()->GetEventHandlerById(<Fisch-Event-ID>)` — die
  konkrete ID ist NICHT verifiziert (CraftEventHandler nutzt 655361/0xA0001;
  Fishing-ID per Probe `GetEventId` des aktiven Handlers festnageln).
- `State` @456 = enum **FishingState** — die Grundwahrheit des Angelvorgangs:
  None, CastingOut, PullingPoleIn (kein Biss / Fisch entwischt / nach Fang / Rest),
  Quitting, PoleReady (Standby, Rute bereit), **Bite (BISS — jetzt anschlagen!)**,
  Hooking (Anschlagen + Einholen), ReleasingCatch, ConfirmingCollectable,
  AmbitiousLure/ModestLure (nur Aktions-Animation), Unk11, LineInWater (Leine im
  Wasser, warten auf Biss). ⇒ Biss-Ansage = Flanke State->Bite.
- `CanFish` @464 (bool) — betrifft „richtig stehen": ob gerade ausgeworfen werden
  kann. Weitere Flags @465–470: CanMoochPreviousCatch, CanMooch2PreviousCatch,
  CanReleasePreviousCatch, ChangingPosition, CanIdenticalCastPreviousCatch,
  CanSurfaceSlapPreviousCatch. `CurrentCastBaitFlags` @472 (FishingBaitFlags).
- Tug-Staerke (leicht/mittel/schwer) ist in DIESEM Struct NICHT als Feld sichtbar
  — per Probe klaeren (evtl. aus Bite-Untertyp/Animation ableitbar).

### FishingModule (Client.UI.Misc, Size 192) — NICHT Laufzeit
- Reines Save-File (UserFileEvent): Fischtagebuch. `UnseenFishCount` @188. Fuer
  die Positionierung/Biss-Ansage irrelevant.

### FishingSpot-Sheet (Lumina) — statischer Katalog aller Angelplaetze
- Felder: `TerritoryType` @52 (RowRef, = Zone), `PlaceNameMain` @54,
  `PlaceNameSub` @56, `PlaceName` @60 (RowRef, Anzeigename), `Radius` @58 (ushort),
  `Order` @62, `X` @64 (short), `Z` @66 (short), `GatheringLevel` @68 (byte, noetige
  Angelstufe), `FishingSpotCategory` @69, `Rare` @71. Filter Zone:
  `row.TerritoryType.RowId == clientState.TerritoryType`.
- X/Z = KARTEN-PIXEL (0..2048), VERIFIZIERT an echten Sheet-Werten (Lumina gegen
  sqpack, 2026-07-25): alle 333 Zeilen liegen in X 108..1948 / Z 210..1934, also
  im Pixelbereich; Umrechnung ergibt sinnvolle Kartenkoordinaten (Fallgourd Float
  21,0/24,6; Limsa Untere Decks 7,7/12,2). ⇒ NICHT MapCoordToWorld (1..42), sondern
  `PlacesService.MapPixelToWorld(X, Z)` (nutzt die verifizierte PixelToWorld-Formel,
  wie MapMarker). Radius ist NICHT in denselben Welt-Einheiten (Stadtwerte bis 3000)
  — fuer die Fuehrung ignoriert, grosszuegige Ankunftsdistanz + Navmesh reichen.
- Y-Hoehe fehlt (Kartendaten 2D) → via Navmesh (PointOnFloor / PathfindAndMoveCloseTo)
  aufloesen, wie bei allen anderen Wegpunkten. LIVE noch zu bestaetigen: dass der
  umgerechnete Punkt auf dem Angelloch landet (Kompass-Ansage von /acc fish = Check).
- GEBAUT V5.52 (Debug): FishingService.GetSpotsInCurrentZone + AnnounceSpotsInCurrentZone,
  Kommando **/acc fish** — sagt Angelplaetze der Zone (Name, Stufe, Entfernung,
  Himmelsrichtung), naechster zuerst.
- Verwandte Typen (falls je gebraucht): AddonFishingNote, AddonFishGuide2,
  AgentFishGuide, AddonSpearFishing, InstanceContentOceanFishing (Meeresangeln),
  Lumina SpearfishingNotebook.

## Triple Triad (Kartenspiel) — AddonTripleTriad

Verifiziert per ilspycmd gegen FFXIVClientStructs.dll (2026-07-26).

- Addon-Name: `"TripleTriad"` (GetAddonByName). Struct `AddonTripleTriad`
  (Size 4056, `[Inherits<AtkUnitBase>]`, **`[GenerateInterop(false)]`** →
  KEINE generierten Span-Accessoren!).
- Kartenlisten sind `internal FixedSizeArray*<TripleTriadCard>` — aus dem Plugin
  NICHT direkt zugreifbar. Deshalb per Pointer-Arithmetik an den verifizierten
  Offsets lesen (Stride = `sizeof(TripleTriadCard)` = 168):
  - `_blueDeck` @576  — FixedSizeArray5 = eigene Hand (Spieler ist immer Blau)
  - `_redDeck`  @1416 — FixedSizeArray5 = Gegnerhand
  - `_board`    @2256 — FixedSizeArray9 = 3x3-Brett, row-major (Feld 1..9)
  - Die Offsets liegen exakt auf Stride 168 aufeinander (576+5*168=1416,
    1416+5*168=2256) → Stride cross-verifiziert.
- `TripleTriadCard` (Size 168, public struct in AddonTripleTriad):
  - `CardRarity`@128 (byte), `CardType`@129 (enum None/Primal/Scion/Beastman/Garland),
  - `CardOwner`@130 (enum **Empty=0, Blue=1, Red=2**),
  - `NumSideU`@131, `NumSideD`@132, `NumSideR`@133, `NumSideL`@134 (byte, Kantenwerte
    1..10; das Spiel zeigt 10 als "A"),
  - `HasCard`@164 (bool — Brett: Feld belegt; Hand: Slot noch nicht gespielt).
- `AddonTripleTriad.TurnState`@568 (enum **Waiting=0, NormalMove=1, MaskedMove=2**).
  HYPOTHESE (in-game noch zu verifizieren): Waiting = nicht am Zug, Normal/MaskedMove
  = du bist am Zug. Rohwert wird von TripleTriadService geloggt ([TripleTriad]).
- GEBAUT (Debug, ungetestet): `TripleTriadService.ReadBoard()` (Strg+Umschalt+F4) +
  `ReadHand()` (Strg+Umschalt+F5). Brett: Kartenzahl beider Seiten, Zug-Zustand,
  dann Feld 1..9. Hand: eigene Karten per festem Slot (1..5), gespielte Slots
  uebersprungen. NOCH IN-GAME ZU TESTEN.
- Offene Frage fuer den Test: Ob der Spielcursor in der Hand gespielte Slots
  ueberspringt oder die Karten kompaktiert — davon haengt ab, ob die feste
  Slot-Nummer (aktuell) oder eine laufende Nummer die richtige Referenz ist.

## Quest-Gegenstaende im Kampf (ilspycmd + Sheet-Dump, 2026-08-09)

Ausloeser: Spielerfrage „Quests, wo man mit Gegenstaenden im Kampf etwas
ausloesen muss". Es sind ZWEI getrennte Mechaniken — nicht vermischen.

### A) Schluesselgegenstand der Quest (EventItem) — der haeufige Fall
- Lumina-Sheet `EventItem` (Zeilen ab 2000000). Felder: `Name`/`Singular`/`Plural`,
  `Quest` (RowRef auf die Quest, die den Gegenstand ausgibt), `Action` (RowRef;
  bei Quest-Gegenstaenden i. d. R. `Action#1 „Schluesselgegenstand"`), `Icon`,
  `StackSize`, `Category` (EventItemCategory), `CastTime` (byte, Wirkzeit in s),
  `CastTimeline`, `Timeline`.
- Zuordnung Quest → Gegenstand geht in BEIDE Richtungen:
  - vom Gegenstand aus: `EventItem.Quest.RowId`
  - von der Quest aus: `Quest.QuestParams[]` mit `ScriptInstruction` = `ITEM0`,
    `ITEM1`, … und `ScriptArg` = EventItem-RowId. Analog `ENEMY0` (Gegner),
    `ACTOR0` (NPC), `HOWTO_EITEM` (Anleitungs-Id).
- BELEGTES BEISPIEL (offline Sheet-Dump gegen sqpack, DE): Quest **66333
  „Ein Licht fuer die Nacht"** (Stufe 28, JournalGenre 113 „Nebenauftraege
  Finsterwald", Nordwald):
  - `ITEM0` = EventItem **2000627 „Bergmannslampe"** (StackSize 1, CastTime 1)
  - `ITEM1` = EventItem **2000628 „Gleissende Lampe"** (StackSize 2, CastTime 3)
  - `ENEMY0` = 2266
- Inventar: Schluesselgegenstaende liegen im Container
  `GameInventoryType.KeyItems`; die `ItemId` dort indiziert das EventItem-Sheet
  (nutzt `InventoryService.CollectKeyItems` bereits).
- Auf die Leiste legbar: `RaptureHotbarModule.HotbarSlotType.**EventItem**`
  (Id = EventItem-RowId). Es gibt zusaetzlich `HotbarSlotType.KeyItem` — das ist
  laut Struct-Doku NUR der DragDrop-Sonderfall (Id = Slot-Index im
  KeyItems-Container, wird beim Setzen in `EventItem` aufgeloest). Fuer eine
  programmatische Zuweisung ist also `EventItem` + RowId der richtige Weg.
- Ausfuehren als Aktion: `ActionType.EventItem` (=3); daneben existiert
  `ActionType.EventAction` (=4).

### B) Sonderaktionen im Auftrag („Duty Actions") — die kleine Extra-Leiste
- `FFXIVClientStructs.FFXIV.Client.Game.DutyActionManager` (Size 160):
  - `GetInstanceIfReady()` (statisch) — null, solange es keine gibt
  - `ActionsPresent` @25 (bool), `NumValidSlots` @24 (byte)
  - `ActionId[5]` @32 (uint, Action-Sheet), `ActionActive[5]` @26 (bool)
  - `Recast[5]` @52 (RecastDetail), `MaxCharges[2]` @152, `CurCharges[2]` @154
  - `GetDutyActionId(ushort slot)` (statisch, Slot 0 oder 1)
- Ausfuehren: `RaptureHotbarModule.ExecuteDutyActionSlot(uint index)` → bool;
  dazu `GetDutyActionSlot(index)` → `DutyActionSlot` (erbt `HotbarSlot`,
  zusaetzlich `PrimaryCostType`@224, `IsActive`@225).
- WICHTIG fuer Barrierefreiheit: Im Live-Tastenbelegungs-Dump (679 Eintraege,
  2026-08-09) gibt es KEINE Belegung fuer diese Leiste — das Spiel erwartet dort
  einen Mausklick. Ohne Mod ist sie per Tastatur nicht erreichbar.

### Was KEINE Quelle hat
Wann im Kampf der Gegenstand einzusetzen ist, steht in keiner der o. g.
Strukturen — das ist Kampf-/Questlogik. Vorhandene Kanaele dafuer: Systemmeldung
(ChatReaderService), Gegner-Zauber (CombatService), ToDo-Liste der Quest.

## Inhaltssuche und ihre Einstellungen (ilspycmd + UI-Dump, 2026-08-19)

### Zustand der Einstellungen: `Client::Game::UI::ContentsFinder`

Die Duty-Finder-Einstellungen liegen als spieleigene Felder vor — nichts davon
muss aus Symbolen abgeleitet werden. `ContentsFinder.Instance()` (StaticAddress,
Size 176):

- `LootRules@24` — `enum LootRule : byte { Normal, GreedOnly, Lootmaster }`
- `IsUnrestrictedParty@25` — „Keine Beschränkungen"
- `IsMinimalIL@26` — „Anpassung an Mindest-Gegenstandsstufe"
- `IsSilenceEcho@27` — „Teilnahme ohne Kraft des Transzendierens"
- `IsExplorerMode@28` — „Erkundungsmodus"
- `IsLevelSync@29` — „Stufenanpassung"
- `IsLimitedLevelingRoulette@30` — „Einschränkung für Zufallsinhalt: Stufensteigerung"
- `QueueInfo@32` — `ContentsFinderQueueInfo` (Warteschlangenstand, Position,
  `PoppedContent*`-Kopien derselben Schalter für den bereits zugeteilten Inhalt)

NICHT in dieser Struktur, und auch sonst nirgends in FFXIVClientStructs gefunden:
die Zeile **„An laufenden Einsätzen teilnehmen"**. In Dalamuds `UiConfigOption`
gibt es `ContentsFinderSupplyEnable`, das inhaltlich passen könnte — UNGEPRÜFT,
deshalb wird für diese Zeile bewusst kein Zustand angesagt.

Ebenfalls in `UiConfigOption`, für die vier Sprach-Kästchen:
`ContentsFinderUseLangTypeJA` / `…EN` / `…DE` / `…FR` (in dieser Reihenfolge).

**GEMESSEN (2026-08-19, Log 19:29–19:31): diese Felder sind der GESPEICHERTE
Stand, nicht der Stand im offenen Fenster.** Alle sechs blieben auf `False`,
während der Spieler „Keine Beschränkungen" einschaltete; erst beim „Ok" schrieb
das Spiel selbst in den Chat „Teilnahmebedingungen wurden wie folgt festgelegt:
Keine Beschränkungen". Das Fenster arbeitet also auf einer Arbeitskopie.

Für eine Ansage des Zustands im offenen Fenster taugen diese Felder deshalb
NICHT.

### Wo die Arbeitskopie steht (gemessen 2026-08-19, Log 19:36)

Im Bedienelement selbst. Eine Sonde schrieb bei jeder Änderung alle Kandidaten
mit, der Spieler schaltete „Keine Beschränkungen" einmal aus und einmal an, und
die Richtung wurde beide Male gegen die Bestätigung des Spiels im Chat geprüft
(„… festgelegt: -" bzw. „… festgelegt: Keine Beschränkungen"). Es flippen vier
Dinge gemeinsam:

- **AN:** Bild-`PartId` **0**, NineGrid 8 sichtbar / 9 versteckt, Text 6 sichtbar
- **AUS:** Bild-`PartId` **1**, NineGrid 9 sichtbar / 8 versteckt, Text 7 sichtbar

Gelesen wird die `PartId` des Zustandssymbols am **rechten Rand** der Zeile
(über die Geometrie gefunden: das am weitesten rechts liegende Bild der Zeile,
in deren rechter Hälfte). Die Zeile trägt links noch ein zweites Bild — das ist
NICHT das Zustandssymbol.

`AtkComponentButton.IsChecked` (Bit 18 in `Flags`) blieb bei jedem Umschalten
`False` — genau wie FFXIVClientStructs es dokumentiert („used by
AtkComponentCheckBox and AtkComponentRadioButton"). Für diese Zeilen unbrauchbar.
`Flags` selbst blieb konstant `0x20810100`.

Die Sprach-Kästchen sind echte CheckBox-Komponenten, ihr `IsChecked` stimmt
(im Test bestätigt: Japanisch aus, Englisch aus, Deutsch an auf deutschem
Client) — die Zuordnung JA/EN/DE/FR von links ist damit belegt.

### Fensteraufbau `ContentsFinderSetting` (Dump 2026-08-19, 31 Knoten)

- Knoten 4 bis 10 — die sieben Optionszeilen, `Comp(1012)` **Button** (kein
  CheckBox!), Beschriftung im Textknoten 5. Alle bei x=90, Breite 390, y = 189,
  224, 259, 294, 329, 364, 399. Knoten-Reihenfolge läuft der Bildschirmordnung
  ENTGEGEN (id 10 steht unten) — deshalb wird über die Geometrie sortiert.
- Knoten 14 — `Comp(1014)` **DropDownList** (Beuteregeln). Ihr Anzeigefeld ist
  intern selbst eine CheckBox (`Comp(1013)`); ein Aufstieg zur nächstgelegenen
  Komponente sagt sie deshalb fälschlich als Schalter an. `FindTopLevelOwner`
  benutzen.
- Knoten 20 bis 23 — die vier Sprach-Kästchen, `CheckBox`, alle bei y=473,
  x = 342, 376, 410, 444. Kein Text, kein Tooltip.
- Knoten 26 — `Comp(1016)` **Base**, das einzige seiner Art im Fenster: die
  Erklärungs-Tafel rechts, Text im Kindknoten 4 (bis ~500 Zeichen).
- Knoten 25 — Text, wiederholt den Namen des Bedienelements unter dem Fokus.
- Knoten 2 — Unterzeile „Richte Bedingungen für die Teilnahme an Inhalten ein."
  Wichtig, weil Inhaltssuche UND Einstellungen beide „INHALTSSUCHE" heißen.
- Knoten 29/30 — „Ok" und „Schließen", `Comp(1005)`, x=634 bzw. 790, Breite 150.

### Falle: das Kollisionsfeld über dem ganzen Fenster

`Comp(1004)` (Fensterrahmen, Knoten 31) enthält ein Kollisionsfeld (Knoten 13),
das die volle Fensterfläche abdeckt (880x440) und das Fokus-Bit trägt. Der
generische `FindFocusedText` fand dadurch bei JEDEM Fokuswechsel den
Fenstertitel statt des Bedienelements (`Key=31013` in jeder Zeile des Logs vom
2026-08-19). Dasselbe Muster ist bei `ContentsFinder` zu sehen (`Key=76012`).

## Boss-Attacken: NICHT aus den Spieldaten ableitbar (gemessen 2026-08-21)

Frage des Users: *"kriegen wir raus welche boss welche attacken macht?"* — also
im Voraus, statt erst beim Cast. Offline gegen die Sheets gemessen (Lumina,
deutscher Client), Ergebnis negativ. Damit die Frage nicht erneut recherchiert
wird, hier der vollständige Befund.

### Was es gibt: das `Behavior`-Sheet

Ein **Subrow-Sheet**, 6636 Zeilen (lückenlos 30000..36635), 17 Spalten, 1 bis
256 Subrows je Zeile. **Spalte 4 (Int32) ist eine Action-Id** — belegt, nicht
vermutet: von 82.498 Werten ungleich 0 lösen 71.960 zu einem echten
Aktionsnamen im `Action`-Sheet auf ("Einherjar", "Welle der Düsternis",
"Nichts-Feuga", "Steinsprenger", "Windschlag"). Eine Zeile ist damit die
Aktionsliste **eines Verhaltens**, im Median 5 Aktionen, höchstens 81.

Lesen: `GetSubrowSheet<RawSubrow>(null, "Behavior")` — `GetSheet` wirft
`NotSupportedException: Specified sheet variant Subrows is not supported`.

### Warum es trotzdem nicht trägt — zwei unabhängige Gründe

**1. Es deckt nur 1,88 Prozent der Aktionen ab.** Das `Behavior`-Sheet führt
4749 verschiedene Action-Ids, davon 836 benannte. Das `Action`-Sheet hat 44.448
benannte Zeilen. Was drinsteht, sind durchweg niedrige Ids, also
ARR-Feldgegner — es ist das Standardverhalten einfacher Welt-Mobs, kein
Boss-Skript. Stichprobe:

- "Frostatem" (id 16445, die Boss-Aktion aus unserem eigenen Beispiel): **NEIN**
- "Schwanzschlag" (11 Ids, von 935 bis 41579): **keine einzige** enthalten
- "Einherjar" 785: ja — 3144 und 24036 dagegen nicht
- "Steinsprenger" 787: ja — die anderen acht Ids nicht

**2. Es gibt keinen Weg vom Gegner zu seiner Behavior-Zeile.** Geprüft:

- `BNpcBase` (20402 Zeilen, 27 Spalten): KEINE Spalte zeigt in den Bereich
  30000..36635. Der scheinbare Volltreffer beim Test „Feld + 30000" ist
  wertlos — das Sheet ist lückenlos, also trifft dort **jeder** kleine Wert.
- `BNpcName`, `BNpcCustomize`, `BNpcState`, `BNpcParts`, `ModelChara`,
  `NotoriousMonster`: keine Spalte mit auch nur einem auflösenden Treffer.
- `FFXIVClientStructs`: die Zeichenkette `BehaviorId` kommt in der DLL **nicht**
  vor. Kein bekanntes Laufzeitfeld.

**Der Rückwärtsweg trägt ebenfalls nicht.** Idee: eine beobachtete Attacke
identifiziert die Behavior-Zeile, und die kennt dann die übrigen. Gemessen an
den 836 benannten Aktionen: nur 249 stehen in genau einer Zeile, 271 in 6 bis
50, und 84 in mehr als 50. "Einherjar" steht in 158 Zeilen, "Steinsprenger" in
113. Der Median liegt bei 4, der Höchstwert bei 3857.

### Schlussfolgerung

Boss-Mechaniken stehen in keiner Excel-Tabelle. Genau deshalb erzeugt die
gesamte Community ihre Timelines aus **aufgezeichneten Kämpfen** (cactbot,
FFLogs) und nicht aus den Sheets. Der einzige gangbare Weg für dieses Projekt
ist derselbe: selbst mitschreiben, was ein Gegner wirft.

Was das Spiel dagegen **sofort** hergibt, sobald ein Cast beginnt, und was das
Plugin bereits nutzt: Aktionsname, `CastType` (Form) und `EffectRange` aus dem
`Action`-Sheet, dazu `TotalCastTime - CurrentCastTime` als verbleibende Zeit.
Siehe `ActionShapeService` und `CombatService.UpdateEnemyCastWarnings`.

### Nebenbefund: die Ansage ist länger als der Cast (gemessen 2026-08-21)

Bei derselben Untersuchung mitgemessen, weil die eigentliche Frage des Users
„mehr Zeit zum Reagieren" war. **Nicht umgesetzt** — der User hat entschieden,
es vorerst so zu lassen. Die Zahlen stehen hier, damit sie nicht neu erhoben
werden müssen.

**Wirkzeit der Aktionen** (`Action`, Spalte 38 = Zehntelsekunden; abgelesen an
„Frostatem" id 16445 → 30, was der Restzeit-Ansage von 3 s entspricht). Über
27.959 benannte Aktionen mit Wirkzeit > 0:

- Median 4,0 s; 25. Perzentil 3,0 s; 75. Perzentil 5,0 s
- 40 Prozent liegen bei ≤ 3,0 s, 10 Prozent bei ≤ 2,0 s

**Sprechdauer unserer Warnungen** (SAPI, Microsoft Hedda, Tempo 2 = Standard;
lautlos über `SetOutputToAudioStream` gemessen, 16 kHz/16 Bit/Mono):

- „Gegner wirkt Frostatem. Kegel, 90 Grad, 6 Meter. Du stehst drin, 3 Sekunden."
  → **7,95 s**
- nur Cast + Form → 5,33 s
- „Achtung, du stehst drin, 2 Sekunden." → 3,43 s
- „Drin! Rechts raus, 7 Meter." → 3,45 s
- „Raus, rechts." → 1,85 s

Die volle Warnung dauert also rund doppelt so lang wie der mediane Cast, und
sie beginnt mit dem Zaubernamen — der Angabe, die zum Ausweichen am wenigsten
beiträgt. Ein möglicher Umbau wäre, die Ansage am verbleibenden Zeitbudget
auszurichten (`TotalCastTime - CurrentCastTime` liegt bereits vor) und die
Gefahr nach vorn zu ziehen.

## Spiel-Navimesh: liegt auf dem SERVER, nicht im Client (gemessen 2026-08-22)

Frage war, ob wir statt vnavmesh das Navigationsnetz des Spiels selbst nutzen
koennen - fuer immer aktuelle Wege und Treppen. Antwort: die fertigen Netzdaten
sind im Client nicht vorhanden.

**Der Beweis steht in der Zonendatei selbst.** Jede Zone hat unter
`bg/<pfad>/level/<zone>.lvb` eine Level-Datei (Magic `LVB1`, darin `SCN1`), die
ihre Bestandteile als Klartextpfade auffuehrt. Fuer `s1f1` (Unteres La Noscea):

- `bg/ffxiv/sea_s1/fld/s1f1/level/s1f1.svb`
- `bg/ffxiv/sea_s1/fld/s1f1/level/s1f1.lcb`
- `/server/data/bg/ffxiv/sea_s1/fld/s1f1/navimesh/s1f1.nvm`
- `/server/data/bg/ffxiv/sea_s1/fld/s1f1/navimesh/s1f1.nvx`

Die beiden Navimesh-Dateien tragen das Praefix `/server/data/`. Ueber alle Zonen
gemessen (Lumina, TerritoryType.Bg, aktueller Spielstand):

- 873 Zonen mit lesbarer `.lvb`
- **873 von 873** verweisen auf `/server/data/`
- **0** Zonen haben eine `.nvm` oder `.nvx` im Client-sqpack
- im `level/`-Ordner liegen genau drei Dateien: `.lvb`, `.svb`, `.lcb`

Damit ist auch die Nebenfrage beantwortet, warum die Endung `.nvm` weder in der
Spiel-EXE noch in der Pfadliste von FFXIV Explorer (`hashlist.db`, SQLite,
422.035 Dateinamen) vorkommt: die Dateien sind nie ausgeliefert worden.
`NaviMeshResourceHandle` existiert in FFXIVClientStructs und als Zeichenkette in
`ffxiv_dx11.exe`, aber es ist nur die generische Ressourcen-Huelle
(`Data`/`Length`/`FileName`) - eine Wegsuche-API des Clients ist damit NICHT
belegt. Kein `Pathfind`-Symbol, kein RTTI, keine Recast/Detour-Bibliothek in der
EXE (die "recast"-Treffer dort sind Abklingzeiten).

## Sheet `RecastNavimesh`: die Bauparameter des Spiels (gemessen 2026-08-22)

Das Netz fehlt, das REZEPT ist da. Das Sheet existiert im aktuellen Spielstand:
8 Zeilen, 35 Spalten. Zeile 0 heisst `default`, Zeile 11 `navimesh_test`, die
uebrigen tragen Zonenkuerzel (`w1d1`, `l1r2`, `r2d1` - Dungeons).

Zeile 0 (`default`):
`160 | 0.2 | 0.2 | 2 | 0.5 | 0.6 | 56 | True | 8 | 20 | False | 12 | 1.4 | 6 | 6
| 1 | 0.2 | 53 | 2.13 | -30 | 11.6 | 9 | 2.8 | 0.001 | 0.04 | 0.01 | 0.1 | 3 |
0.7 | True | 0.4 | 2 | 0 | False`

Zeile 12 (`w1d1`) weicht ab, u.a. `0.6 -> 1`, `56 -> 60`, `53 -> 58`.

**Bedeutung, und wo die Grenze der Gewissheit liegt:** Der Sheetname und die
Werte passen zur Recast-Konfiguration (`rcConfig`) - Kachelgroesse 160,
Zellgroesse/-hoehe 0.2, maximale Ecken pro Polygon 6 sind dort uebliche Groessen.
Das legt nahe, dass Square Enix seine Netze mit Recast baut, derselben
Bibliothek, die auch vnavmesh benutzt. WELCHE Spalte welches rcConfig-Feld ist,
ist damit NICHT bewiesen - die Zuordnung steht noch aus und darf bis dahin nicht
als Tatsache verwendet werden.

Konsequenz fuer die Planung: ein eigenes Netz muss aus den Kollisionsdaten des
Clients gebaut werden (wie vnavmesh es tut), aber es koennte mit den
Bauparametern DES SPIELS gebaut werden statt mit geratenen. Genau diese
Parameter (Steigungswinkel, Kletterhoehe, Agentenradius) entscheiden darueber,
ob Treppen und schmale Durchgaenge im Netz landen.

## Spaltenzuordnung `RecastNavimesh` und Vergleich mit vnavmesh (gemessen 2026-08-22)

**Die Spaltennamen sind belegt, nicht gedeutet.** Lumina liefert eine generierte
Klasse `Lumina.Excel.Sheets.RecastNavimesh` mit benannten Eigenschaften; sie
stammt aus der Schema-Definition der Community, nicht aus eigener Auslegung:

`TileSize, CellSize, CellHeight, AgentHeight, AgentRadius, AgentMaxClimb,
AgentMaxSlope, RegionMinSize, RegionMergedSize, MaxEdgeLength, MaxEdgeError,
VertsPerPoly, DetailMeshSampleDistance, DetailMeshMaxSampleError` + 20 Unknown.

Das sind eins zu eins die Felder von `rcConfig`. Damit ist belegt, dass Square
Enix seine Navimeshes mit Recast baut.

### Werte des Spiels (Sheet, 8 Zeilen)

- Zeile 0 `default`: TileSize 160, CellSize 0,2, CellHeight 0,2, AgentHeight 2,
  AgentRadius 0,5, **AgentMaxClimb 0,6**, **AgentMaxSlope 56**, RegionMinSize 8,
  RegionMergedSize 20, MaxEdgeLength 12, MaxEdgeError 1,4, VertsPerPoly 6,
  DetailSampleDist 6, DetailMaxSampleError 1
- Zeilen `w1d1`, `l1r2`, `r2d1`, `r1f1`: identisch, ausser
  **AgentMaxClimb 1,0** und **AgentMaxSlope 60**
- Zeilen `d2d4`, `r2d3`, `navimesh_test`: wie default

OFFEN: wie eine Zone ihrer Sheetzeile zugeordnet wird. Die RowIds (0, 11-16,
148) sind nicht die TerritoryType-Ids der genannten Kuerzel - ungeprueft. Nicht
als Zuordnung verwenden, bevor das gemessen ist.

### Werte von vnavmesh (`Navmesh.NavmeshSettings`, per Reflection aus der DLL)

CellSize 0,25 | CellHeight 0,25 | AgentHeight 2 | AgentRadius 0,5 |
**AgentMaxClimb 0,5** | **AgentMaxSlopeDeg 55** | RegionMinSize 8 |
RegionMergeSize 20 | PolyMaxEdgeLen 12 | PolyMaxSimplificationError 1,5 |
PolyMaxVerts 6 | DetailSampleDist 6 | DetailMaxSampleError 1 |
GenerateEdgeClimbLinks False | GenerateEdgeJumpLinks False

### Der Unterschied, auf den es ankommt

`RcConfig.WalkableClimb` ist eine GANZZAHL in Voxeln (daneben steht
`WalkableClimbWorld` in Metern) - die Kletterhoehe wird also in Vielfachen der
Zellhoehe diskretisiert. Daraus folgt die hoechste Stufe, die noch als begehbar
gilt:

- vnavmesh: 0,5 / 0,25 = **2 Voxel, also 0,50 m**
- Spiel default: 0,6 / 0,2 = **3 Voxel, also 0,60 m**
- Spiel in Zonen wie `w1d1`/`r1f1`: 1,0 / 0,2 = **5 Voxel, also 1,00 m**

vnavmesh haelt also nur halb so hohe Stufen fuer begehbar wie das Spiel in
seinen grosszuegigen Zonen. Dazu kommt die groebere Aufloesung (Zellen 0,25
statt 0,2 in Breite UND Hoehe).

HYPOTHESE, noch nicht bewiesen: das erklaert unsere wiederkehrenden Netzschaeden
(Oestliches La Noscea zerfaellt, Astalicia unerreichbar, Wohngebiet-Zaeune,
fehlende Treppen). Beweisen laesst es sich nur mit einem Neubau unter den
Spielwerten und einem Gegentest an einer bekannt kaputten Stelle.

### Ob wir vnavmesh diese Werte geben koennen: nein, nicht von aussen

Die Settings stehen im Code, nicht in einer Konfigurationsdatei.
`vnavmesh.json` enthaelt nur StopOnStuck/RetryOnStuck/BuildMaxCores. Pro Zone
gibt es fest einkompilierte `NavmeshCustomization`-Klassen (z. B.
`vnavmesh.Customizations.Z0613RubySea`) mit eigenem `Settings`-Feld. Aenderbar
waere das nur per Reflection zur Laufzeit oder in einem eigenen Bau.

Bemerkenswert fuer die Eigenbau-Frage: vnavmesh liefert DotRecast (Core,
Recast, Detour, Detour.Extras) als eigene DLLs mit - die Recast-Portierung in
C# ist also fertig verfuegbar und muesste nicht geschrieben werden.

## Fallstudie Neu-Gridania -> Tiefer Wald: das Netz endet VOR der Grenze (2026-08-22)

Gemessener Fehlfall aus dem Log (2026-08-22 10:53), zusammen mit den
Layout-Daten offline aufgeloest. Er zeigt, dass "Kartensymbol ist ungenau" NICHT
die einzige Ursache fuer misslungene Uebergaenge ist - und hier gar nicht die.

- Zone: Neu-Gridania (Territory 132, `ffxiv/fst_f1/twn/f1t1`).
- Kartensymbol "Uebergang nach Tiefer Wald" (MapMarker, DataType 1/2):
  Welt **(170,0|162,0)**.
- Echte Grenze (`ExitRange` aus `planmap.lgb`): **(170,1|-10,6|159,0)**,
  Scale (15,6|3,8|15,0), Laufrichtung 85 Grad.
- **Abstand Symbol <-> echte Grenze: nur 3,0 m.** Das Kartensymbol war hier also
  brauchbar. Die Marker-Ungenauigkeit erklaert diesen Fall NICHT.
- Der Lauf endete bei **(152,5|-13,0|165,0)** - **18,6 m** vor der Grenze und
  2,4 m tiefer. Im Log ist das gut sichtbar: `restWp=1`,
  `nextWp=(170,0|-12,4|162,0)`, `distNextWp=17,8`, ueber Sekunden unveraendert.
  Das ist Fakt 3 aus `AutoWalkService` - vnavmesh haengt das angeforderte Ziel
  unbedingt an die Wegpunktliste, erreichbar oder nicht.
- Was dazwischen liegt: 25 Hintergrundobjekte, fast alle im Layer
  **`f0t0_b1_gate`** (ein Torbauwerk, Teile auf zwei Hoehen: -13,0 und -9,7),
  seitlich ein `bgparts_door_close`. Ab etwa einem Fuenftel der Strecke gibt es
  keine platzierten Objekte mehr - der Boden dort ist Terrain.

**Schlussfolgerung:** Das begehbare Netz von vnavmesh reicht nicht bis zur
Zonengrenze; es endet am Torbauwerk, und hinter dem Tor steigt das Gelaende um
2,4 m an. Ein besserer Zielpunkt (echte Grenze statt Kartensymbol) wuerde daran
NICHTS aendern - der Lauf endet so oder so am Netzrand. Das passt zur
Parameter-Hypothese (vnavmesh erlaubt 0,50 m Stufenhoehe, das Spiel 0,60 bis
1,00 m), ist damit aber NICHT bewiesen: ob dort Stufen dieser Hoehe liegen,
wurde nicht gemessen.

**OFFEN und praktisch wichtig:** ob `Transform.Scale` der ExitRange die HALBE
oder die VOLLE Ausdehnung ist. Bei Halbausdehnung reichte die Trigger-Box bis
X = 154,5 - der Spieler stand bei X = 152,5, also nur **2 m** davor, und ein
Ziel am Boxrand statt in der Boxmitte wuerde den Uebergang ausloesen. Bei
Vollausdehnung waeren es 10 m. Messbar nur im Spiel: Position beim
Zonenwechsel protokollieren.
