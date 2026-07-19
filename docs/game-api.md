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
  rot=0 blickt nach +Z. Relativwinkel zum Ziel daher:
  `atan2(dx, dz) - rot` (normalisiert auf ±180°); 0 = geradeaus.
- Beweis: F-Taste (zum Ziel drehen) rastete zweimal auf exakt rot=-1,83
  ein; Ziel-Peilung aus stationären Gehhilfe-Ticks: atan2(dx,dz)=-105° =
  -1,83 rad — Blickvektor traf Zielrichtung auf <0,5° genau. Die alte
  Annahme „0 = Norden" (atan2(dx,-dz)) war eine SPIEGELUNG, kein Offset.
- OFFEN: Vorzeichen (positiv = rechts oder links?). Aus dem Log nicht
  ableitbar. Test: Ziel „links" angesagt → A (links drehen) → Ansage muss
  Richtung „geradeaus" wandern; bzw. D halten und im Gehhilfe-Log prüfen,
  ob rot dabei steigt oder fällt.

### vnavmesh-IPC (Quellcode-verifiziert 2026-07-10, github.com/awgil/ffxiv_navmesh)
- Fremd-Plugin für Navmesh-Wegfindung + Auto-Bewegung. Installation:
  Repo `https://puni.sh/api/repository/veyn`; aktuell 1.2.3.8, ApiLevel 15
  (kompatibel). Alternativ als Dev-Plugin nach devPlugins entpackbar.
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
- `MapMarkerData` (Size 80): `LevelId`@0, `ObjectiveId`@4,
  `TooltipString`@8 (Utf8String*), `IconId`@16, `Position`@28 (Vector3,
  WELT-Koordinaten!), `Radius`@40, `MapId`@48, `RecommendedLevel`@64.
- OFFEN (Laufzeit, vor Nutzung per Debug-Probe klären): (1) Feld für
  TerritoryType/Zone des Markers — Marker können in ANDERER Zone liegen
  (SetData-Signatur hat territoryTypeId-Parameter, Feld-Offset im Struct
  noch nicht identifiziert); (2) taugt Position.Y direkt als
  vnavmesh-Ziel (Marker-Zentrum kann neben dem begehbaren Mesh liegen —
  PathfindAndMoveCloseTo mit Radius als range dürfte das abfedern).
- Leere Slots: vermutlich MarkerData.Count==0 bzw. Label leer — per
  Probe verifizieren, nicht raten.

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

### Talk / TalkSubtitle (Log-verifiziert 2026-07-11)
- AddonTalk hat nur UNBENANNTE Text-Node-Felder (AtkTextNode220/228/238/
  240/248, ilspycmd) — kein benanntes „Name"-Feld.
- PROBE-VERIFIZIERT (Dialog-Nodes-Zeilen, Sessions 09:36 + 10:14):
  Talk-Sprechername = Text-Node id=2, Dialogtext = id=3. Der Name-Node
  kommt in Node-Listen-Reihenfolge NACH dem Text (zuletzt).

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
