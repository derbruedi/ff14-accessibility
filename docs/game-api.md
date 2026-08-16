# Game API findings (FF14 / Dalamud / FFXIVClientStructs)

Central, VERIFIED facts about game structures. The source is given in each case
(ilspycmd against FFXIVClientStructs.dll, or a live log). Nothing here is guessed.

Note on quoted strings: texts in „German quotes" are verbatim observations from a
German game client and are deliberately left unchanged, because they are the raw
data the findings rest on.

## Verified structs (ilspycmd, FFXIVClientStructs.dll in the Dalamud dev folder)

- `RaptureAtkUnitManager.Instance()`, `FocusedUnitsList`/`AllLoadedUnitsList`
  (`AtkUnitList`: Entries[256] + Count), `AtkUnitBase.NameString`/`IsVisible`
- `AtkComponentCheckBox.IsChecked`
- `GameObject` (Client.Game.Object): `DrawObject*` @ offset 256,
  `VisibilityFlags RenderFlags` @ 280 (enum: None=0, Model=2, Nameplate=0x800)
- `DrawObject` (Client.Graphics.Scene): has `bool IsVisible` (bit field)
- `CustomizeData` (Client.Game.Character): Race@0, Sex@1, Tribe@4 (bytes).
  BUT: no clean live pointer to the running character creation
  (no AgentCharaMake in this version; AgentLobby has no CustomizeData field)
- `Framework` (Client.System.Framework): `bool WindowInactive` @ offset 6104
  — does the GAME WINDOW have focus? (true = in the background). Used by
  VitalsService to mute the HP/MP tones while you are in another program.
  Preferable to the Windows API `GetForegroundWindow`: the game maintains the
  flag anyway, and a second source of truth could drift from it. Next to it lie
  `CallerWindow` (nint) and `GameWindow*`. NOT YET ESTABLISHED: whether the flag
  also covers minimising/overlays or only a plain focus change — VitalsService
  logs every edge change, which will settle it in practice

## Character creation (CharaMake)

### Addon list (live log 2026-07-10, all open at the same time)
CharaMake, _CharaMakeInfo, _CharaMakeNotice, _CharaMakeShadow, _CharaMakeTitle,
_CharaMakePose, _CharaMakeProgress, _CharaMakeReturn, _CharaMakeHelp,
_CharaMakeRaceGender, _CharaMakeTribe, _CharaMakeFeature, _CharaMakeGuardian,
_CharaMakeCity, _CharaMakeClassSelector, _CharaMakeWorldServer,
_CharaMakeBirthDay, _CharaMakeBgSelector, _CharaMakeCharaName,
CMFIconFaceType, CMFIconHair, CMFIconFeature, CMFIconTatoo, CMFIconFacePaint,
CMFSlider (2x), CMFColorL, CharaMakeSelectYesNo, CharaMakeDCWorldMap(Bg)

### Preview models (live log 2026-07-10, V4.15 probe)
- 32 Pc objects SIMULTANEOUSLY in the ObjectTable: indices 200-231, without
  names, Sex alternating 0/1 = 8 races × 2 tribes × 2 genders
- Exactly ONE visible (`DrawObject.IsVisible=true`, RenderFlags=0x0); the 31
  hidden ones carry RenderFlags=0x40 (a value not named in the enum)
- The visible model = the displayed one → its Sex byte is ground truth for the
  chosen gender (0=male, 1=female, FFXIV convention)

### _CharaMakeRaceGender (dumps 2026-07-09)
- 8 race rows as Comp(1003) [CT=Base], each with two gender checkboxes:
  child id=4 (glyph broken as ® U+00AE) and id=3 (© U+00A9), race name in id=2
- The glyph→gender mapping is UNRESOLVED; one indication (1 data point, log
  2026-07-10 10:19): id=3 (©) checked while the visible model had Sex=0 → © would
  be male, which makes the original assumption id=4=male probably WRONG. The
  announcement therefore uses the visible model, with the checkbox only as a
  change detector + fallback label
- MouseOver announcement via the event target (`AtkEvent->Node`), CleanRaceName
  strips the glyphs off

### _CharaMakeTribe (dump 2026-07-10 10:20)
- Tribe options = top-level checkbox components (node id=7 Comp(1006), id=6
  Comp(1006)), name in the text child id=2 („Hochländer", „Wiesländer")
- ALSO contains 8 Comp(1003) rows [CT=Base] with ®/© checkboxes (like
  RaceGender, text children empty) and back/OK buttons (id=19/18)
- Header row: „Volksstamm", help text „Wähle einen Volksstamm aus."

### _CharaMakeProgress (dump 2026-07-10 10:20) — progress menu on the left
- Comp(1002) buttons per step, label in the text child id=3, current value in
  id=5: „Volk & Geschlecht" (value e.g. „Hyuran ©"), „Volksstamm" (value
  „? ? ?" when still open), „Aussehen", „Namenstag", „Schutzgottheit", „Klasse",
  „Stammwelt", „Name"; OK button = Comp(1001)
- The ©/® in the value of „Volk & Geschlecht" is the chosen gender glyph

### _CharaMakeFeature (dump 2026-07-17 16:35, the „Aussehen" step)
- Category buttons = Comp(1004) [CT=Button], label in the text child id=2
  („Körpergröße", „Körperbau", „Gesicht", … „Stimme"); invisible buttons
  (F=0x2023 without V) are categories not available for the chosen race
- Description as top-level text id=6 („Bestimme das Aussehen deines
  Charakters."), window title id=3 („Aussehen"), „Zufälliges Aussehen" =
  Comp(1003) button, top-level **id=4** (V4.86: Ctrl+F8 presses it by
  ButtonClick dispatch, matched by node ID — language-independent), back/OK =
  id=38/37
- MouseOver/ButtonClick deliver the category in the event param (node id)

### CMFIcon* (dump 2026-07-17 16:35: CMFIconFeature „Gesichtsmerkmale")
- The selection is a List(9) component, entries are ListItemRenderer(14) with
  EXCLUSIVELY image children — NO text per entry. Reading out is only possible
  as "entry X of Y" (ListLen/Sel in the list layout); the icons are mute.
- Window title as top-level text id=3, OK button id=7
- Known picker windows (live log 2026-07-17): CMFIconFaceType, CMFIconHair (52),
  CMFIconFeature, CMFIconTatoo (27?), CMFIconFacePaint (27), CMFColorL (192),
  CMFColorHair (192), CMFColorFacePaint (96); further CMFColor* variants are
  likely (eye/lip/skin colour not yet seen in the log) → the announcement paths
  match on the prefix "CMF"
- `AtkComponentListItemRenderer.ListItemIndex` (offset 388, ilspycmd 2026-07-17)
  = the renderer's DATA row — correct even when the list scrolls under a fixed
  focus node (the renderer slot index would be wrong)
- Reading out: V4.85, two paths ("12 of 52"): the TrackListIndices fallback +
  TryReadCharaMakeIconFocusRow in the global focus path. Live log 17:24: BOTH
  fire (a mouse hover moved Hov2 → list navigation announcement, the focus row
  delivered the same text, and the debounce caught the echo)
- **The GAME ignores the arrow keys in these grids** (log 17:24:47: all four
  arrows, no index/focus movement whatsoever — a pure mouse UI). V4.87: the
  plugin navigates by itself — `AtkComponentList.SelectItem(idx, dispatchEvent)`
  + `ScrollToItem(short)` + `GetItemCount()` (all ilspycmd-verified; also
  present: `DispatchItemEvent(idx, AtkEventType)` as an alternative should
  SelectItem not update the preview — the runtime effect of dispatchEvent is
  still unverified)
- Inactive pickers stay loaded with 0 entries; only the active one has
  ListLength > 0 (log 17:23:52) → detect the "active picker" via Count > 0

### _CharaMakeCharaName (name entry, dump 2026-07-17 17:57)
- Window title „Name des Charakters", help text id=13 („Vor- und Nachname können
  je zwischen 2 und 15 Zeichen…"), instruction id=5 („Gib deinem Charakter einen
  Namen."), total counter id=12 „0/20"
- TWO visible TextInput components (CT=7): **id=9 and id=7** (each F=…V), each
  with its own counter child id=17 („0/15") and display text id=16. Plus TWO
  invisible TextInputs id=11 (counter „0/9") + id=10 („0/6") = alternative input
  modes (unused, no V) → only process the visible fields
- Labels as top-level text: **id=8 „Nachname", id=6 „Vorname"**. Node order:
  TextInput id=9 → text id=8 → TextInput id=7 → text id=6. The id-1 pattern fits
  (9→8, 7→6), but V4.89 pairs by PHYSICAL PROXIMITY (X/Y of the field vs. the
  label) — more robust against node order/language
- „Bestätigen" button id=16, „Zurück" button id=3
- Reading out: V4.89 OnCharaMakeNameUpdate — focus node → the containing visible
  TextInput (FindFocusedNameField); on a field change label + content, otherwise
  a typing echo (EvaluatedString diff). The generic focus reader is muted for
  name fields (IsFocusInsideNameField), while buttons stay generically readable
- OPEN: how does the user switch fields (Tab? click?) — the runtime log was
  missing (rotated); the next test will settle it ([Name] lines)

### Saving the appearance (dumps + log 2026-07-17 17:42)
- Route: the „Aussehen" step → OK → SelectYesno „Die Einstellungen speichern?" →
  Yes
- `CharaMakeDataExport` („CHARAKTERDATEN SPEICHERN"): List(9) with 40 slots,
  ListItemRenderer rows WITH text: id=6 tribe/gender („Wiesländer♂"), id=5
  „Speicherslot N", id=4 date. The keyboard moves Hov2 (natively) → the generic
  list announcement takes hold. Column headers + description as top-level texts
  (id=6/5/4/2)
- `CharaMakeDataImportDialog`: overwrite confirmation (OK/Cancel), the question
  is read by OnAnyAddonOpen
- `CharaMakeDataInputString`: comment dialog — a window component, save/cancel
  buttons (id=5/6), **TextInput component (CT=7)** top-level id=4 with counter
  text id=17 („0/40") and display text id=16
- `AtkComponentInputBase` (ilspycmd 2026-07-17): EvaluatedString @224, RawString
  @328, CursorPos @460, SelectionStart/End @452/456 — EvaluatedString = the
  source for the typing echo (V4.88, OnCharaMakeInputUpdate, diff announcement
  per frame)
- WATCH OUT, focus: in the dialog the global focus sits on the COUNTER node
  („0/40") and changes on every key press → IsBareNumber guard
- [Key] probe finding: IsJustPressed only sees the arrow keys when the game does
  not consume them itself (native list navigation consumes them; dead icon grids
  do not) → plugin navigation never collides with native navigation

### Race/tribe description = _CharaMakeHelp (dumps 2026-07-17 16:31)
- The description text sits in `_CharaMakeHelp`, top-level **text node id=4**
  (F=0x2033 V), and is rewritten live when an option is highlighted — verified at
  TWO steps:
  - Race & gender (16:31:39/49): „Die Elezen sind stolze Nomaden, …"
  - Tribe (16:31:57): „Der Volksstamm der Wiesländer macht die große Mehrheit im
    Volk der Hyuran aus. …"
- The remaining _CharaMakeHelp nodes: id=5 TextNineGrid (text empty), id=3 text
  empty, id=7/6/2 images — id=4 is the only content node
- _CharaMakeInfo is NOT the description (both its text nodes empty, even while
  the description was visible)
- At the „Aussehen" step _CharaMakeHelp is invisible (dumps 16:32/16:35)
- Reading out: V4.83 `OnCharaMakeHelpUpdate` (PostUpdate _CharaMakeHelp, change
  detector on the node text, non-interrupting announcement)
- WATCH OUT (V4.84): `_CharaMakeHelp` MUST be in SpecialUpdateAddons — otherwise
  the generic scanner (ScanAddonTexts) speaks the text additionally via
  SpeakInterrupt and cuts off the name announcement (log 2026-07-17 16:56)

### Not yet analysed (dumps are present in the log of 2026-07-10!)
- CMFColorL (colour selection, ~1283-2793)
- CharaMake-SelectYesno (~4555+)
- The dump file on the desktop is OVERWRITTEN on every F5 — the log has them all

## Clicking buttons programmatically (verified via ilspycmd, 2026-07-10)

The clean route without guessing at callbacks: send the button's registered
ButtonClick event to its listener — the same path as a real mouse click.

- `AtkResNode.AtkEventManager.Event` = the head of a linked list
  (`AtkEvent.NextEvent`); click events hang off the collision child or off the
  component node itself
- `AtkEvent`: Node@0, Target@8, Listener@16, Param@24, NextEvent@32, State@40
- `AtkEventState.EventType`@0 — `AtkEventType.ButtonClick = 25`, MouseOver=6,
  MouseClick=9
- `AtkEventListener.ReceiveEvent(AtkEventType, int eventParam, AtkEvent*,
  AtkEventData*)` — AtkEventData is 40 bytes, passed zeroed
- Implemented in `UIReaderService.PressFocusedOk`/`TryClickButton`
- The SelectYesno special case remains: Yes = `FireCallback(1, {Int:0})` +
  `ShouldFireCallbackAndHideOrClose=true`; No = `Close(true)` (No has NO callback
  — confirmed)

## Lobby / title screen

- `CharaSelect` is an EMPTY container (Vis=True, 0 nodes) — the content sits in
  `_CharaSelectListMenu` (MouseOver param 1/2/3, no text handler of its own)
- `SelectYesno` is reused with changing button texts (OK/Cancel): visible buttons
  Comp(1005) id=8 (confirm) / id=11 (cancel); HoldButton duplicates ids 9/12/15
  invisible; the window component (CT=Window(2)) carries the window title as text
  children; id=8/"OK" = callback index 0
- `TitleDCWorldMap`: the event parameters of the MouseOver events are NOT node
  IDs; map them through `AtkEvent->Node` (the first field). Region tabs (Comp
  1022) have no text — the region names are in panels (comp child 1009), the DC
  names in 1015

## Keybind system (verified via ilspycmd, 2026-07-10)

Namespace `FFXIVClientStructs.FFXIV.Client.System.Input` (+ `Client.UI.UIInputData`):

- **Access:** `UIInputData.Instance()` (fetches `UIModule.Instance()`
  internally). `UIInputData` contains `InputData` as a field at offset 0.
- **`InputData`** (size 2512): `NumKeybinds` (offset 2484, int), `Keybinds`
  (offset 2488, `Keybind*`), `GetKeybindSpan()` → `Span<Keybind>`,
  `GetKeybind(InputId)`, `IsInputIdPressed/Down/Held/Released(InputId)`. The
  index in the table == the InputId value.
- **`Keybind`** (size 11): `KeySettings` (2× KeySetting, keyboard slots 1+2),
  `GamepadSettings` (2× KeySetting, controller).
- **`KeySetting`** (size 2): `Key` (SeVirtualKey, byte — the values == Windows VK
  codes, e.g. F1=112, W=87, 0=unbound), `KeyModifier` (KeyModifierFlag: Shift=1,
  Ctrl=2, Alt=4, flags combinable).
- **`InputId`** enum: roughly 450 named actions (with gaps, e.g. 227–236 are
  missing; max 678). Important groups: `MOVE_*` (321–327), `CAMERA_*` (328–343),
  `TARGET_*` (361–429, among them `TARGET_P1`–`TARGET_P8` = 370–377 → party
  members, presumably F1–F8 by default!), `HOTBAR_1_1`–`HOTBAR_EX_B` (57–188),
  `MENU_*` (237–280 + more), `CMD_*` chat (281–320), `JUMP`=348,
  `AUTORUN_KEY`=349, `KEY_SCREENSHOT`=555. Full text: the scratchpad dump or
  ilspycmd -t.
- **Reading it live in the plugin:** `/acc keys` (V4.18, KeybindService) writes
  all bound actions + a conflict check against the plugin keys to
  `Desktop\FFXIV_Keybinds.txt`.

## Official default keyboard layout (source: de.finalfantasyxiv.com/game_manual/operation, 2026-07-10)

A summary of the official manual. CAVEAT: the special-character keys (German
layout) are partly unclear; the ground truth is the auto keybind dump (V4.19).

### Movement
- W/S forward/back, A/D turn, Q/E strafe, space to jump
- R auto-run, Y draw weapon/dismount, V camera flip
- Flying: space up, Ctrl+space down/dive, Z dismount (in the air)

### Camera
- Arrow keys = aim the camera (NOT movement!), Page Up/Down zoom
- Home switch camera, End default position, NUM5 lock onto the target

### Targeting (CORE FOR NAVIGATION — all F keys are bound!)
- Tab / Shift+Tab: cycle enemies (near→far / far→near)
- F: turn towards the target; Shift+F: set/clear the focus target
- F1: yourself; F2–F8: party members; F9: companion; F10: focus target
- F11: next ENEMY; F12: next NPC OR OBJECT (built-in navigation!)
- T: target's target; Shift+T: attacker
- Ctrl+NUM8/NUM2: enemy list up/down

### Chat
- Enter: open chat; X: text command; Alt+S/G/R/…: chat modes

#### Combat log / reading out your own actions (V4.90)
When an action is used the game writes "Du wirkst X." into the combat log; that
arrives through `IChatGui.ChatMessage` as its own `XivChatType`.
- Named base LogKinds (Dalamud `XivChatType`, = the low 7 bits of the value):
  Damage=41, Miss=42, **Action=43** ("uses an action"), Item=44, Healing=45,
  GainBuff=46, GainDebuff=47, LoseBuff=48, LoseDebuff=49.
- Real messages can arrive as COMBINED values (source/target bits in the higher
  byte), so mask `(int)type & 0x7F` down to the base (robust whether flat or
  combined).
- OPEN/PROBE: whether "own" vs. "other" actions can be distinguished through the
  high bits is NOT verified - ChatReaderService.TryHandleCombat logs every action
  line raw ([Combat] Aktion type=0x…) so that the own-code can be filtered out of
  a live log. Until then ALL action lines are read (config ReadCombatMessages).
  Also the review category "combat".

#### SENDING chat (typing echo in the input field) — ilspycmd-verified 2026-07-17
NVDA does not read the game's chat field; the plugin speaks the typed characters
itself (V4.90). Source:
- `AddonChatLog` (addon name "ChatLog", ALWAYS visible).
  - `TextInput` @608 = `AtkComponentTextInput*` (a direct pointer, no node scan
    needed).
  - `TabIndex` @684 / `TabCount` @685 / `TabNames` (FixedSizeArray5) = the chat
    TABS (general/combat/…), NOT the send channel.
- `AtkComponentTextInput`:
  - `IsActive` (bool) = true while the input mode is open (opened with Enter).
    THE gate that keeps the echo from running every frame.
  - `AtkComponentInputBase.EvaluatedString` = the typed text (as in the CharaMake
    field). Alongside it `CursorPos`, `SelectionStart/End` for later polish
    (editing in the middle).
- Active channel (announcement): `AddonChatLog.CurrentChannelTextNode` @335
  (`AtkTextNode*`) carries the channel label as the game renders it - localised
  and always correct (via `->NodeText.ToString()`, then sanitised). THAT is the
  source for the channel announcement - NO int→name guessing needed. (V4.90 uses
  exactly that.)
  - `RaptureShellModule.Instance()->ChatType` @4048 (int) is the channel as a
    number; test values 2026-07-17: 1/2/4 when toggling with Alt. `TempChatType`
    @4284; whisper target `TellName` @4056 / `TellWorld` @4160 / `TellWorldId`
    @4280. The int→name mapping is NOT verified (the agent enum `ChatChannel`
    only has Say=1/Party=2/Alliance=3, possibly a different numbering) - which is
    why the text node is used for the announcement, not the number.
- Sending (Enter) and switching channels (Tab/Alt+key) stay the game's own — the
  plugin only announces. What is sent is echoed back by ChatReaderService (your
  own /say message arrives as XivChatType.Say).

### Menus (selection)
- NUM0 confirm, NUM, (comma) cancel, NUM* subcommand
- NUM8/2/4/6 cursor, NUM9/NUM7 tabs, NUM+ main menu, NUM- system
- C character, I inventory, M map, J journal (quests!), K command list, U
  character config, Ctrl+U system config, P duty finder, O party, L social
  circles, H/G/B/, notebooks, ä emotes, Ö free company
- Esc: close all UI elements; Print/F13 screenshot; F14 UI mode

### Safe mod keys (CONFIRMED by the live dump 2026-07-10, 171 bound actions)
- **N = the only free letter** (only Alt+N=novice chat is bound)
- **NUMPAD3 free** (NUMPAD1=HUD focus, NUMPAD5=camera, the rest = UI cursor)
- **Ctrl+F1…F12 completely free** (only Ctrl+F20 is bound); Shift+F1…F12 are
  likewise free (bound: Shift+Tab/T/F/M/V)
- Limitation: bare SHIFT/CONTROL are octave keys in BARD PERFORMANCE MODE
  (PERFORMANCE_MODE_*) — avoid Ctrl combinations there
- **WINDOWS TRAP, Shift+numpad (discovered 2026-07-16):** with NumLock active the
  Windows keyboard driver converts Shift+numpad DIGIT into the navigation key
  (Numpad3 → Page Down/VK_NEXT) and artificially releases Shift while doing so —
  IKeyState NEVER sees the numpad VK. Evidence: the walk guide on Shift+Numpad3
  (V4.61–V4.63) did not fire a single time according to the log, while
  Ctrl+Numpad3 (route preview) arrived immediately. Page Down is moreover
  CAMERA_ZOOMOUT in the game. ⇒ NEVER combine numpad digits with Shift, only with
  Ctrl. Ctrl+Numpad2/4/6/8 are taken by the game (alliance/enemy list cursor);
  Ctrl+Numpad1/3/5/7/9 are free.
- Plugin keys since V4.21 (config migration V1→V2): N=nearby objects, Shift+N=
  direction, Ctrl+N=track target, Ctrl+Shift+N=tracking off, Ctrl+F1=help,
  Ctrl+F2=window, Ctrl+F5=UI dump, Ctrl+F10=read menu, Ctrl+F11=silence,
  Ctrl+F12=combat status
- Since V4.21 IsJustPressed can handle modifiers ("Ctrl+Shift+N"), with EXACT
  modifier matching (a bare N does not fire on Alt+N)
- The German umlaut keys run through special VKs: VK136≈Ö (FC menu), VK140≈Ä
  (emotes), VK137/139 = hotbar slots 11/12 (presumably ß/´) — the mapping is
  INFERRED from a comparison with the manual, not hard-verified
- Further dump findings: MENU_FISH=F20, MENU_BUDDY=F22, MENU_RETURN=F24
  (pseudo-keys); camera=arrow keys; CMD_CHAT=RETURN confirmed

### Dalamud targeting (verified 2026-07-10, in-game + ilspycmd)
- `IObjectTable.LocalPlayer.TargetObject` does NOT track UI targeting
  (established in-game: a Tab target was set, the property stayed null → no
  announcement)
- Correct: `ITargetManager` (a Dalamud service): `.Target` (hard target),
  `.SoftTarget`, `.FocusTarget`, `.MouseOverTarget`, `.PreviousTarget` — all
  IGameObject?, and settable too (null = clear the target)
- Dalamud `ObjectKind` enum: None, Pc, BattleNpc, EventNpc, Treasure, Aetheryte,
  GatheringPoint, EventObj, Mount, Companion, Retainer, AreaObject,
  HousingEventObject, Cutscene, ReactionEventObject, Ornament, CardStand (NOT
  "Player"/"MountType"!)

### SetHardTarget can REFUSE (ilspycmd + live log 2026-07-10, 16:39)
- `TargetSystem.SetHardTarget(GameObject*, bool ignoreTargetModes, bool a4,
  int a5)` returns a **bool** — the game can refuse the target change. Dalamud's
  `ITargetManager.Target` setter calls it and THROWS the return value AWAY
  (ilspycmd-verified, Dalamud.dll TargetManager).
- Established live: between 16:39:26 and 16:39:44 ALL of the browser's target
  sets were refused (the hard target stayed on Honoraint), while before and after
  (16:41:34+) they worked. The cause is still UNRESOLVED — since V4.25 the plugin
  logs refusals by reading back ("[Nav] Target-Set ABGELEHNT").
- The getter is `GetHardTarget()` (a game function of its own, not merely a field
  read; the `Target` field sits at offset 128). The `ignoreTargetModes` parameter
  is untested — a candidate should refusals become a problem.

### Rotation convention (VERIFIED from the live log 2026-07-10, 15:26–15:27)
- `IGameObject.Rotation` (radians): **the facing vector = (sin(rot), cos(rot)) in
  the XZ plane**, i.e. rot = atan2(dx, dz) of the facing direction. rot=0 faces
  +Z. The relative angle to the target is therefore `atan2(dx, dz) - rot`
  (normalised to ±180°); 0 = straight ahead.
- Proof: the F key (turn towards the target) locked in twice at exactly
  rot=-1.83; the target bearing from stationary walk guide ticks:
  atan2(dx,dz)=-105° = -1.83 rad — the facing vector matched the target direction
  to within less than 0.5°. The old assumption "0 = north" (atan2(dx,-dz)) was a
  MIRRORING, not an offset.
- OPEN: the sign (does positive mean right or left?). Not derivable from the log.
  Test: target announced as "left" → A (turn left) → the announcement must move
  towards "straight ahead"; or hold D and check in the walk guide log whether rot
  rises or falls.

### vnavmesh IPC (source-verified 2026-07-10, github.com/awgil/ffxiv_navmesh)
- A third-party plugin for navmesh pathfinding + auto-movement. Installation:
  repo `https://puni.sh/api/repository/veyn`, ApiLevel 15. On the user's machine
  it sits as a dev plugin under `devPlugins\vnavmesh` — Dalamud does NOT update
  that automatically, so an update means swapping files (2026-08-10 from 1.2.3.10
  to 1.2.3.13; the old version is in `devPlugins\vnavmesh_backup_1.2.3.10`).
  The download address is the `DownloadLinkInstall` in the repo JSON.
  WATCH OUT: a version change can alter the navmesh format version, and then all
  cached meshes are rebuilt on first entry.
- The IPC gates relevant to us (all with the prefix `vnavmesh.`):
  - `Nav.IsReady` → bool (the zone's mesh is loaded)
  - `Nav.BuildProgress` → float (loading progress)
  - `SimpleMove.PathfindAndMoveTo(Vector3 dest, bool fly)` → bool
  - `SimpleMove.PathfindAndMoveCloseTo(Vector3 dest, bool fly, float range)`
    → bool (false ONLY when a pathfind is already outstanding; source:
    AsyncMoveRequest.MoveTo)
  - `SimpleMove.PathfindInProgress` → bool (the pathfind is still computing)
  - `Path.IsRunning` → bool (currently running; Waypoints.Count > 0)
  - `Path.Stop` → Action (subscriber: GetIpcSubscriber<object>, InvokeAction)
  - `Path.SetTolerance(float)`, `Path.MoveTo(List<Vector3>, bool)` and others
- The Dalamud side: `IDalamudPluginInterface.GetIpcSubscriber<T..., TRet>(name)`,
  `InvokeFunc`/`InvokeAction` (ilspycmd-verified). If the plugin is missing, the
  INVOKE throws (IpcNotReadyError) — subscribing is always safe.
- A path destination is a POINT (the position at the start) — moving NPCs walk
  away, so restart if needed.
- IMPORTANT, `Nav.Pathfind(Vector3 from, Vector3 to, bool fly)`: a pure waypoint
  QUERY without auto-movement, but the return type is **`Task<List<Vector3>>`**,
  NOT `List<Vector3>` (ilspycmd 2026-07-16 against the installed vnavmesh.dll:
  the IPC gate wraps `NavmeshManager.QueryPathBasic`, an `async` method).
  Subscriber: `GetIpcSubscriber<Vector3, Vector3, bool, Task<List<Vector3>>>`;
  poll the task per frame (check `IsCompletedSuccessfully` — the task can fault
  when the mesh unloads on a zone change), NEVER block on `.Result` before
  completion. `Nav.PathfindInProgress`/`Nav.PathfindNumQueued` report the queue
  state; multiple requests are worked through one after another internally
  (`ExecuteWhenIdle`). Verdent's concept document
  (manuelle-navigation-konzept.md) wrongly states `List<Vector3>` here.
- QueryPath throws an exception when no mesh is loaded — check `Nav.IsReady`
  before the invoke (RouteService does that).

### Treasure chests: you read the state, you do not recompute it
`FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure` (ilspycmd 2026-08-09,
inherits from GameObject, size 528). The game maintains the state itself:
- `State` (field offset **416**, enum `TreasureState`):
  `Unopened=0, Opening=1, Opened=2, Unk3=3, FadingOut=4, FadedOut=5`.
  Anything other than `Unopened` means: done. The object then stays in the
  ObjectTable for a while longer, only to play its fade-out.
- `Flags` (field offset **508**, enum `TreasureFlags`): `Opened=1, FadedOut=2`.
  WATCH OUT, the struct documentation describes State and Flags as overlapping
  and says of FadedOut explicitly "sometimes set when fading starts, sometimes
  when fading is complete" — which is why `State` is the more reliable source.
- `CofferKind` (field offset 512, enum `TreasureKind`): `Levequest`,
  `DungeonRaid`, `TreasureHunt`, `PersonalLoot`.
- `ItemCount` (496) + `LootableItemIds` (432, 16 × uint) = the item sheet rows of
  the contents, as soon as they appear in the loot window.
- `CountdownTime`/`ClaimTime` (420/428): the seconds the loot window displays.
→ V5.75 hides chests with `State != Unopened` from the object browser list
(NavigationService.IsEmptiedTreasure). ONLY the list — if you target the chest
with the game keys, it is still announced.

#### Targets higher up: what `fly` actually does (ilspycmd 2026-08-08)
The complete IPC list decompiled from `Navmesh.IPCProvider` of the installed DLL.
On the subject of "the object is above me":
- **The `fly` parameter selects TWO DIFFERENT SEARCH SPACES**, it is not a
  convenience switch (`NavmeshManager.QueryPath`, line 189):
  `flying ? Query.PathfindVolume(...) : Query.PathfindMesh(...)`.
  - `false` (what we pass everywhere) = **the walkable surface**. It knows about
    height perfectly well — stairs, ramps and bridges are part of the mesh. What
    it does NOT know is a connection that does not exist on foot.
  - `true` = **the voxel volume** (airspace, `NavVolume`/`VoxelPathfind`).
- **The volume does not always exist.** `NavmeshQuery` only creates `VolumeQuery`
  when `navmesh.Volume != null` (lines 92-94); otherwise `PathfindVolume` answers
  with the log error "Nav volume was not built" and an empty list. Whether it is
  built for interiors/instances: NOT CHECKED.
- **`fly=true` makes the character JUMP.** `FollowPath` line 153: if the next
  waypoint is higher than the player and they are neither `InFlight` (condition
  77) nor `Diving` (81), it calls `ExecuteJump()` — but only when `IgnoreDeltaY`
  is false. And `Path.MoveTo(waypoints, fly)` sets `IgnoreDeltaY = !fly`
  (IPCProvider lines 78-81). With our `fly=false` the character therefore NEVER
  jumps. (Condition names verified against Dalamud.dll.)

#### Unused IPC gates that bear directly on our "no path found"
- `Nav.PathfindWithTolerance(from, to, fly, float range)` → pathfinding with a
  destination tolerance. Exactly the Haukke case (the target lies next to the
  mesh).
- `Query.Mesh.NearestPointReachable(p, halfExtentXZ, halfExtentY)` → the nearest
  **reachable** mesh point (`FindNearestPointOnMesh(..., allowUnreachable:
  false)`). Our home-made ring search in AutoWalkService reproduces that the hard
  way — here vnavmesh offers it ready-made.
- `Query.Mesh.IsPointOnMesh(p, halfExtentY, allowUnreachable)` → a check of
  whether a point lies on the mesh at all.
- `Nav.PathfindCancelable(from, to, fly, CancellationToken)`.

#### How vnavmesh really starts and ends paths (ilspycmd 2026-08-10)
Three properties that any auto-walk logic MUST know about. All three silently
broke the implementation before V5.79 — the evidence for each is in the Dalamud
log of 2026-08-10.

1. **A path request is asynchronous and does NOT stop the running path.**
   `AsyncMoveRequest.MoveTo` only sets `_pendingTask` (returning `false` if one is
   already running). Only `AsyncMoveRequest.Update` passes the result on to
   `FollowPath.Move`. In that window `Path.IsRunning` still describes the
   PREVIOUS path, and `Path.ListWaypoints` returns its waypoints.
   → Log 08:05:05: a request for "Weinhafen", and what was read back was the
   waypoint list of the Sonnenküste run; 52 ms later the plugin reported
   "finished, 499 m to go", while vnavmesh promptly set off for another 50 m.
   → Consequence: call `Path.Stop` before starting our own, and keep watching for
   several seconds after our own end (a task in flight revives the run).

2. **vnavmesh restarts itself.** With `StopOnStuck` + `RetryOnStuck` (both on in
   the user's configuration, `StuckTimeoutMs` 1000, `StuckTolerance` 0.05),
   `FollowPath.Update` calls `Stop()` after a second without movement and fires
   `OnStuck`; `AsyncMoveRequest` then sends the same request again. As a result
   `Path.IsRunning` **flickers to false once a second** without the run being
   over.
   → Log 08:04:24–08:05:55: 91 "Queueing move-to" entries at one-second intervals
   after the plugin had long since disengaged — the character was pushed silently
   against the mesh edge for a minute.
   → Consequence: "path finished" only after debouncing (V5.79: 1.6 s of
   continuous `!IsRunning && !PathfindInProgress`). A single frame lies.

3. **The last waypoint is pure invention.** `NavmeshQuery.PathfindMesh` appends
   the REQUESTED destination to the result unconditionally (`list.Add(new
   Waypoint(rcVec3f...))`), whether it lies on the mesh or not. So if a zone's
   mesh falls apart into unconnected islands, vnavmesh returns a path whose last
   hop goes straight through the rock, and then pushes the character against it
   endlessly.
   → Log 08:04:23: `restWp=1 nextWp=(490,5|19,0|466,6) distNextWp=453,8` — the
   player at height 58.7, the target at height 19.0, Eastern La Noscea.
   → Consequence: if the character stops moving and only ONE waypoint is left, it
   is not the character that is stuck — the walkable mesh ends there. V5.79
   announces it that way instead of claiming "stuck".

#### Why the path mesh does NOT know every route (Navmesh.NavmeshSettings, ilspycmd 2026-08-10)
A common misunderstanding: the mesh is not a route map shipped by Square Enix,
but is computed by vnavmesh itself with **Recast** from the collision geometry -
for an idealised character with fixed limits:
- `AgentMaxSlopeDeg = 55` - anything steeper than 55 degrees is NOT walkable.
- `AgentMaxClimb = 0.5` - ledges over half a metre are insurmountable.
- `AgentHeight = 2`, `AgentRadius = 0.5` - the surface is additionally shrunk
  half a metre away from every wall.
- `GenerateEdgeClimbLinks = false` (the default, not set on the user's machine) -
  so NO "you can jump/climb down here" connections are generated. The associated
  values (`ClimbDownMaxHeight` 3.2 m, `EdgeJumpHeight` 1.8 m) lie idle.
- `RegionMinSize = 8` - small isolated surfaces drop out entirely.

Consequence: every spot in the game reachable only by jumping down, sliding or
crossing a steep slope does not exist in the mesh. That is exactly how Eastern La
Noscea (s1f3) falls apart into two halves - the Wineport plateau (Y approx.
59-76) and the coast/Costa del Sol (Y approx. 17-20). On foot you can get down,
but no Recast polygon leads over a 55-degree edge.
→ SETTLED AND REFUTED (2026-08-10): `/vnav rebuild` done in the zone, the cache
   demonstrably rewritten, vnavmesh on 1.2.3.13 - the separation reproducibly
   persists (the run again ended at 469 m remaining distance).

CORRECTION TO THE EARLIER NOTE (ilspycmd 2026-08-10): `GenerateEdgeClimbLinks`
CANNOT be "switched on in the vnavmesh settings". `NavmeshSettings` is read
exclusively from `NavmeshCustomization.Settings` (`NavmeshBuilder..ctor`:
`Settings = customization.Settings`), and the user `Config` does not contain
these fields at all - it only has AutoLoadNavmesh, EnableDTR,
ShowQueryStatusInDTR, AlignCameraToMovement/-Height, ShowWaypoints,
ForceShowGameCollision, CancelMoveOnUserInput, StopOnStuck, StuckTolerance,
StuckTimeoutMs, RetryOnStuck, RandomnessMultiplier, BuildMaxCores. The
`NavmeshSettings.Draw()` sliders belong to the debug window "NavmeshCustom", i.e.
to manually built test meshes, not to the automatically loaded zone mesh.
→ Changing Recast parameters = forking vnavmesh. There is no route via a file or
  the UI.

#### What DOES work instead, to bridge a mesh gap (IPC, ilspycmd 2026-08-10)
- `Path.MoveTo(List<Vector3> waypoints, bool fly)` walks OUR OWN point list, with
  no path search at all (it goes straight to `FollowPath.Move`). That is the only
  way to send the character over ground the mesh does not know about - confirmed
  in-game on 2026-08-07 (Astalicia) and 2026-08-09 (the outward leg to the
  magnet).
  WATCH OUT, the point list is NOT safe (ilspycmd 2026-08-10): if the character
  stays below `StuckTolerance` for `StuckTimeoutMs` (500 ms), `FollowPath.Update`
  calls its own `Stop()` and fires `OnStuck` with the LAST waypoint.
  `AsyncMoveRequest` hangs off that and, with `RetryOnStuck` (on for the user),
  starts a normal `MoveTo` to that point - our list is gone and the character
  walks over the mesh again, which of course does not know about the gap.
  This can be detected by two things, both of which
  `AutoWalkService.TrailWalkingUpdate` checks: the waypoint count GROWS (a new
  route has more points than our remaining list; ours only shrinks), and
  `PathfindInProgress` becomes true (our leg never computes).
- `NavmeshCustomization.LinkPoints(mesh, start, end)` is vnavmesh's own mechanism
  for hand-made connections, but it is `protected static` in a customization
  class with `[CustomizationTerritory(id)]` - reachable only through a fork, not
  through the IPC. No customization exists for territory 135.
- FLYING: `NavmeshCustomization.IsFlyingSupported` returns true for
  `TerritoryType.TerritoryIntendedUse` 1, 47 and 49; `NavmeshBuilder` then
  additionally builds a `VoxelMap`, and `Nav.Pathfind`/`Path.MoveTo` take a `fly`
  flag. A flight volume does not know about the 55-degree limit - so for height
  breaks in field zones it is the fundamentally clean workaround. BOTH points are
  UNCHECKED: whether territory 135 has one of the three IntendedUse values, and
  whether the character is allowed to fly there (aether currents - pure game
  state, not recorded in the mesh).
- `seeds-local.json` (`FloodFill.AddPoint` + `Serialize`, stored in the vnavmesh
  pluginConfigs folder) only marks which surfaces are reachable from a seed, and
  thereby arms `NavmeshManager.Prune`. That closes NO gap; it only makes
  `Query.Mesh.NearestPointReachable` / `IsPointOnMesh(allowUnreachable: false)`
  effective at all (without seeds they have no effect, measured 2026-08-09).

Further established details:
- `Path.IsRunning` is exactly `FollowPath.Waypoints.Count > 0`, nothing more.
- `Path.Stop` = `Waypoints.Clear()`. It does NOT abort a running pathfind.
- `FollowPath.OnNavmeshChanged` empties the waypoints — so on a zone change or
  `Nav.Reload` a path disappears by itself.
- `Nav.PathfindCancelAll` is misleadingly named: it calls
  `Reload(allowLoadFromCache: true)`, i.e. it reloads the mesh.
- `Query.Mesh.NearestPointReachable` filters through `FloodFillAwareFilter`
  (`NavmeshQuery` line 83) — that is vnavmesh's own reachability check, and a
  home-made surface analysis is not needed for it.
- `CancelMoveOnUserInput`: if the player presses a movement key themselves,
  `FollowPath.Update` calls `Stop()` — the path is then gone without our plugin
  finding out about it.

### "Following" a player — NO native API (verified via ilspycmd, 2026-07-26)
The "Follow" context menu entry exists in the game, but is **not** exposed as a
callable function in FFXIVClientStructs. The complete assembly was decompiled and
searched: the only "Follow" hits are companion/mount
(`CompanionBehaviorState.Follow`, `FollowMountId`), the portrait camera
(`BannerCameraFollowFlags`) and the map checkbox (`FollowPlayerCheckbox`,
`FollowedPlayerMarkerX/Y`). `MoveController` (MoveControl) carries NO follow
field. Triggering it would only be possible fragilely via
`AgentContext.OpenContextMenu` + finding the entry by text/firing `ReceiveEvent`
(language- and target-type dependent) — rejected.
→ V5.57 builds "follow target" (the + key) on vnavmesh itself instead: in
`AutoWalkService.FollowUpdate`, `SimpleMove.PathfindAndMoveCloseTo` is
continuously re-triggered on the CURRENT target position (distance 3 m, re-path
from 1.5 m of drift or when the path ended, throttled to 0.4 s). It stops when
the target is gone or on a zone change.

### Compass convention, world→cardinal direction (derived 2026-07-16)
- North = −Z, east = +X. Derived from verified facts: the pixel→world formula
  (above) maps map pixel X→world X and map pixel Y→world Z in the SAME sense; map
  images have their origin at the top left (pixel Y grows downwards); the game
  map is north-oriented (north at the top). ⇒ the bearing from north =
  `atan2(dx, −dz)` (0°=N, 90°=E).
- Used by RouteService (the route preview "25 metres to the north"). Every
  preview logs segment 1 including the vector ([Route]) — a mirrored axis would
  show up in the log on the first practical test.
- Player facing direction: rot=0 ⇒ facing vector (sin 0, cos 0) = (0,0,1) = +Z =
  SOUTH (follows from the convention above + the verified facing vector).

### Online window / social (ilspycmd-verified 2026-07-19)
The O key = `MENU_PARTY_MEMBER (271)` according to the keybind dump; the addon
name is "Social".
- `FFXIVClientStructs.FFXIV.Client.UI.AddonSocial` (size 816, [Addon("Social")],
  inherits AtkUnitBase) has the four tabs as `AtkComponentRadioButton*`:
  `PartyMembersRadioButton`@680, `FriendListRadioButton`@688,
  `BlacklistRadioButton`@696, `PlayerSearchRadioButton`@704.
- The ACTIVE tab = the one whose `AtkComponentButton.IsChecked` is set. IsChecked
  is bit 18 of `AtkComponentButton.Flags` (decompiled: `BitOps.GetBit(Flags,
  18)`); RadioButton inherits AtkComponentButton@0.
- LABEL: `AtkComponentButton.ButtonTextNode` (AtkTextNode) carries the localised
  tab text — never translate it yourself, read the node.
- Related structs should the content be needed: `AddonFriendList`,
  `InfoProxyFriendList`, `SocialListNumberArray`/`SocialListStringArray`,
  `AgentFriendlist`.
- IMPORTANT — the CONTENT does NOT sit in the Social addon (log 2026-07-18
  17:05): a tab change attaches a separate window (`Social ReceiveEvent:
  type=ChildAddonAttached param=126/127`) and opens `PartyMemberList`,
  `FriendList` or `SocialList` depending on the tab. A list search in the Social
  addon itself therefore finds NOTHING. Observed mapping: tab 1 „Gruppe"→
  PartyMemberList, 2 „Freunde"→FriendList, 3 „Suche"→SocialList (tab 4 not seen
  yet).
  WATCH OUT: tab 3 carried the label „Suche" even though the struct field at slot
  3 is called `BlacklistRadioButton` — the field names of this ClientStructs
  version evidently do not match the UI order here. So always read the label from
  the ButtonTextNode, never infer it from the field name.

### Invitations / notifications (2026-07-18)
- Popup windows (names from the log, not guessed): `_NotificationFcJoin` (free
  company), `_NotificationParty`, `_NotificationFriend`, `_Notification`. They run
  for 300 s, after which the game cancels the invitation ("Die Einladung von ...
  wurde abgebrochen", SystemMessage 57).
- The window contains a seconds counter (for FcJoin, node key=20005) that changes
  every second — generic text scanners have to suppress bare numbers, otherwise
  the screen reader counts along 300 → 0.
- The invitation MESSAGE itself arrives independently of that through chat
  (SystemMessage) AND a toast — do not read it from the popup.
- The game's keybind dump has NO action for notifications; without a mouse click
  an invitation cannot be answered from the keyboard.
- GAME FUNCTION for answering (in reserve, untested):
  `InfoProxyFreeCompanyInvite` (InfoProxyId.FreeCompanyInvite) and the base
  `InfoProxyInvitedList` both have `RespondToInvitation(CStringPointer
  inviterName, bool accept)` in the vtable @104.
  WATCH OUT: it needs the inviter's name; the proxy only has private `UnkString`
  fields for that (@72 / @176) — unverified.

### Addon kinship: child/host windows (ilspycmd 2026-07-18)
`AtkUnitBase` carries three id fields directly one after another (after
`AtkValuesCount`): `Id` (ushort), `ParentId`, `HostId`, plus `BlockedParentId`.
That makes it possible to find an attached child window without a hard-coded
name list: walk `AllLoadedUnitsList` and check
`child->HostId == host->Id || child->ParentId == host->Id`. Which of the two
fields the game sets per window family is NOT documented — so check both and log
the result.
TRAP: `Id == 0` as a search key matches every addon with an unset back reference
— catch that beforehand.
- LIST TIMING: a freshly opened list window often has `Len=0` and is only filled
  a few frames later (FriendList: empty at PostSetup, entries 35 ms later). So
  "0 entries" on opening is usually not an empty list, but a question asked too
  early.

### Quest level (ilspycmd-verified 2026-07-18)
- FIRST CHOICE: `MapMarkerData.RecommendedLevel` (ushort @64) — the marker
  carries the level itself, no name matching needed. Cross-check in the struct:
  `SetData(.., ushort recommendedLevel, sbyte eventState)`.
- FALLBACK: Lumina `Quest.ClassJobLevel` (Collection<ushort>, index 0) — the
  level the journal shows too. Only mappable by quest NAME and therefore
  imprecise: FFXIV assigns names more than once (repeatables). Further fields
  should they ever be needed: `QuestLevelOffset` (byte @2764), `LevelMax` (byte
  @2786), `SortKey` (ushort @2760).
- OPEN (runtime): whether RecommendedLevel is filled in the marker at all.
  QuestMarkerService logs `lvlMarker=` and `lvlSheet=` per marker.

### The icon above the head: `GameObject.NamePlateIconId` (ushort/uint @272)
Exactly the symbol a SIGHTED player sees above the object (the quest exclamation
mark and so on), 0 = none. It is read, never rebuilt from the quest state.
MEASURED so far (all 2026-08-02): **71201** (Buscarron, South Shroud), **71203**
(Baensyng, Limsa Lominsa), **71351** (Thubyrgeim, Limsa Lominsa).
IMPORTANT: all three real measurements lie at 712xx/713xx — the meaning ranges in
`AccessibilityStrings.QuestMarkerHint` (71001–71006 "available", 71021–71046
"active") have **zero** measurements to this day and never take hold; in reality
the catch-all case 71000–71999 "quest" always hits. The object browser logs every
icon other than 0 together with the object name, so that the classification can
be sharpened from real data. Before any claim that "icon X means Y": measure
first.
Cross-check on reliability (log 2026-08-02 20:01): in Limsa Lominsa the icon
source and the marker source independently produced exactly the same two NPCs
(`per Marker 2, per Symbol 2`, 2 hits in total).

### Quest markers with a world position (ilspycmd-verified 2026-07-10)
Source: `FFXIVClientStructs.FFXIV.Client.Game.UI.Map` (singleton,
`Map.Instance()` via StaticAddressPointers).
- `QuestMarkers` → a span with 30× `MarkerInfo` = markers of ACCEPTED quests;
  `UnacceptedQuestMarkers` (StdList<MarkerInfo>) = acceptable quests nearby; and
  among others `ActiveLevequestMarker`, `GuildLeveAssignmentMarkers`,
  `TripleTriadMarkers`.
- `MarkerInfo` (size 144): `ObjectiveId`@4 (uint), `Label`@8 (Utf8String, the
  quest name), `MarkerData`@112 (StdVector<MapMarkerData> — SEVERAL places per
  quest possible!), `RecommendedLevel`@136, `ShouldRender`@139 (bool).
- `MapMarkerData` (size 80, completed by ilspycmd 2026-08-02): `LevelId`@0,
  `ObjectiveId`@4, `TooltipString`@8 (Utf8String*), `IconId`@16, `Position`@28
  (Vector3, WORLD coordinates!), `Radius`@40, `MapId`@48, `PlaceNameZoneId`@52,
  `PlaceNameId`@56, `EndTimestamp`@60 (int), `RecommendedLevel`@64 (ushort),
  `TerritoryTypeId`@66 (ushort), `DataId`@68 (ushort), `MarkerType`@70,
  `EventState`@71, `Flags`@72.

### TRAP: `MapMarkerData.DataId` is NOT an object id (measured 2026-08-02)
The field looks like the record id of the target object, but is not:
- It is a **ushort** — an NPC `BaseId` is 1,000,000+ and does not fit in 16 bits.
- `MapMarkerData.SetData(levelId, tooltip, icon, x, y, z, radius,
  territoryTypeId, mapId, placeNameZoneId, placeNameId, recommendedLevel,
  eventState)` has **no dataId parameter** — the setter never writes the field.
  Measurement: 0 on all quest markers (log 2026-08-02, "0 Ids aus Markern", the
  category stayed empty).

### Quest marker → object in the world (the right way, 2026-08-02)
`MapMarkerData.LevelId`@0 (= the first SetData parameter) is the row number in
the Lumina sheet **`Level`** (ilspycmd Lumina.Excel.Sheets.Level):
- `X`@0/`Y`@4/`Z`@8, `Yaw`@12, `Radius`@16
- `Object` (uint @20) — the record id of the object at this place, typed through
  `Type` (byte @32): **8 = ENpcBase, 9 = BNpcBase, 12 = Aetheryte,
  14 = GatheringPoint, 45 = EObj**
- `EventId` @24 (RowRef to TripleTriad/Adventure/Opening/**Quest**), `Map`
  (ushort @28), `Territory` (ushort @30)

`Level.Object` lies in the same id space as `IGameObject.BaseId` in the object
browser (cross-check: the NPC titles come through
`ENpcResident.TryGetRow(obj.BaseId)` and are correct). That makes "which object
does this marker mean" a pure sheet lookup — no icon table, no distance
heuristic. When reading, check `Level.Territory` against the current zone:
otherwise an id from another zone marks a similar-looking NPC next door. Careful
with `Type=9` (BNpcBase): a base id applies to ALL enemies of the same kind in
the zone — that is right for "kill 3 beetles", but it is a kind, not an
individual enemy.
- OPEN (runtime, to be settled with a debug probe before use): (1) the field for
  the marker's TerritoryType/zone — markers can lie in a DIFFERENT zone (the
  SetData signature has a territoryTypeId parameter, but the field offset in the
  struct has not been identified yet); (2) whether Position.Y works directly as a
  vnavmesh destination (the marker centre can lie next to the walkable mesh —
  PathfindAndMoveCloseTo with the radius as range should cushion that).
- Empty slots: presumably MarkerData.Count==0 or an empty Label — verify with a
  probe, do not guess.

### FATE (ilspycmd-verified 2026-07-31)
Source: `FFXIVClientStructs.FFXIV.Client.Game.Fate.FateManager` (singleton,
`FateManager.Instance()`). It holds ONLY the FATEs of the current zone; FATEs are
NEVER in the quest journal (they are pure world events).
- `FateManager` (size 208): `CurrentFate`@136 (`FateContext*` — the FATE the
  player is currently standing in), `Fates`@144
  (`StdVector<Pointer<FateContext>>` = all active FATEs), `SyncedFateId`@168.
  Methods: `GetCurrentFateId()`, `GetFateById(ushort)`,
  `TryGetFatePosition(ushort, out Vector3)`, `IsInFateRadius(Vector3*)`,
  `LevelSync()`, `IsSyncedToFate(FateContext*)`.
- `FateContext` (size 10704): `FateId`@24 (ushort), `Name`@192 (Utf8String — read
  it with `.ToString()`, NOT ExtractText!), `Description`@296, `Objective`@400,
  `State`@940 (`FateState`), `Progress`@951 (byte, 0–100 %), `Level`@2035 (byte),
  `MaxLevel`@2036 (byte), `IconId`@2004, `Location`@2128 (Vector3, WORLD
  coordinates — usable directly as a nav destination).
- `FateState` (byte): `Preparing`=3 (currently appearing), `Running`=4 (active,
  joinable), `Ending`=5, `Ended`=7, `Failed`=8.
- `StdVector<T>`: `Count` (int) + indexer `[i]` → `ref T`; iterate with a for
  loop. `Pointer<FateContext>.Value` → `FateContext*`.
- Used in FateService (the object browser category "FATEs"): it lists Running +
  Preparing, and Numpad3 walks to the `Location` (as an in-zone QuestDestination).

### Journal / JournalDetail (F5 dumps 2026-07-10/11)
- Journal (the J key, „ARCHIV"): the quest list = a comp CT=TreeList(12), the
  rows are ListItemRenderers with id=4 (level „St. 1") + id=3 (quest name);
  category rows (area/expansion) have id=2. Tabs „Aktiv"/„Abgeschlossen".
- AtkComponentTreeList inherits AtkComponentList at offset 0 (ilspycmd:
  [Inherits<AtkComponentList>(0)]) → SelectedItemIndex/ListLength are usable.

### AtkComponentList: index fields (ilspycmd 2026-07-11)
All the candidates for "which row is selected/highlighted":
- `SelectedItemIndex` @308, `HeldItemIndex` @312, `HoveredItemIndex` @316,
  `HoveredItemIndex2` @344, `HoveredItemIndex3` @352 (all int)
- `ListLength` @288, `FirstVisibleItemIndex` @296
- `ItemRendererList` @240 (ListItem*, 24 bytes per entry):
  `AtkComponentListItemRenderer*` @8, `IsHighlighted` (bool) @20, `IsDisabled`
  @21. `AllocatedItemRendererListLength` @248 bounds the real allocation (virtual
  lists: fewer slots than ListLength!).
- SOLVED (probe log 2026-07-11 10:15, full SystemMenu test): KEYBOARD navigation
  tracks `HoveredItemIndex2` (@344) — it changes in the frame of the key press;
  `HoveredItemIndex` (@316) follows one frame later. `SelectedItemIndex` stays -1
  throughout (mouse/confirmation only). Enter on an entry sets `HeldItemIndex`
  (observed: Held=7 when opening the system configuration). The IsHighlighted
  mask stayed empty.

### Global UI focus: AtkInputManager (ilspycmd 2026-07-11)
- `AtkStage.Instance()->AtkInputManager` (@40): `FocusedNode` @6272
  (AtkResNode*) = THE currently focused UI node (keyboard/gamepad); `FocusList` =
  256× FocusEntry {AtkEventListener* @0 (usually the addon), AtkEventTarget* @8
  (the node), FocusParam @16}; `TextInput` @0.
- The focus often sits on the control's COLLISION child, not on the component
  node itself → for text, climb up through the parents.
- AtkStage additionally: RaptureAtkUnitManager @32, the AtkCursor type via the
  AtkCursor struct (Type/IsVisible, no target node — the target sits in the
  InputManager).
- OPEN RUNTIME QUESTION (V4.35 probe [Focus]): does FocusedNode follow left/right
  in SelectYesno/JournalResult? (The node flags did not.)

### TreeList: an items vector of its own (ilspycmd + log 2026-07-11)
- `AtkComponentTreeList.Items` @432 =
  StdVector<Pointer<AtkComponentTreeListItem>> — the REAL rows (categories +
  entries). The inherited `ListLength` stays 0 (journal: "menu, 0 entries"
  despite a navigable list).
- So check renderer access (ItemRendererList[idx]) against
  `AllocatedItemRendererListLength`, NOT against ListLength.

### Map markers for the "places" category (research 2026-07-11, ilspycmd)
- AgentMap (Client.UI.Agent): `EventMarkers` StdVector<MapMarkerData> @232
  (+ `EventMarkersPtrs` @208), `SymbolMap` StdMap @352, `CurrentTerritoryId`
  @23072, `CurrentMapId` @23076, `CurrentMapSizeFactor(Float)` +
  `CurrentOffsetX/Y` @22892–22906, `MapMarkerCount` (byte) @23291.
- MAP MARKING ("flag", research 2026-07-18, ilspycmd):
  `AgentMap.FlagMarkerCount` (byte @23294) = the number of flags set,
  `AgentMap.FlagMapMarkers` = Span<FlagMapMarker> (1 element, field
  `_flagMapMarkers` FixedSizeArray1). FlagMapMarker (size 72): MapMarkerBase@0,
  `TerritoryId`@56, `MapId`@60, `XFloat`@64, `YFloat`@68.
  IMPORTANT: XFloat/YFloat are WORLD coordinates (X and Z), NOT map pixels — the
  pixel→world formula must NOT be applied here. Proof:
  `AgentMap.SetFlagMapMarker(territoryId, mapId, Vector3 worldPosition)` writes
  worldPosition.X → x and worldPosition.Z → y (rounded to 3 decimal places) and
  passes them through to the member function. There is no height (Y) — as with
  all map data, resolve it via the navmesh.
  Before reading, check `MapId` against the current map: the flag stays put on a
  zone change and then belongs to a different map.
- Map (Client.Game.UI) has ONLY quest-like markers: QuestMarkers[30],
  LevequestMarkers[16], HousingMarkers[62], UnacceptedQuestMarkers,
  GuildLeveAssignment/GuildOrderGuide/TripleTriad/CustomTalk/GemstoneTrader (all
  StdList<MarkerInfo>). NO aetherytes/exits.
- Static symbols (aetherytes, exits, shops): the Lumina sheet "MapMarker" —
  VERIFIED (ilspycmd Lumina.Excel.Sheets.MapMarker, 2026-07-11): a subrow sheet,
  the row = Map.MapMarkerRange. Fields: Icon@0, PlaceNameSubtext@2, DataKey@4,
  X@8/Y@10 (short, MAP PIXELS 0..2048), DataType@15: 1/2=Map (zone transition,
  DataKey=target map), 3=Aetheryte, 4=PlaceName (aethernet). Access:
  IDataManager.GetSubrowExcelSheet.
- PIXEL→WORLD formula (derived from Dalamud MapUtil, decompiled 2026-07-11):
  display = 0.02·offset + 2048/scale + 0.02·world + 1 and
  display = 2·pixel/scale + 1 (check: pixel 0→1.0, 2048→42.0 at SizeFactor 100)
  ⇒ world = (pixel − 1024) · 100/SizeFactor − offset.
  Map sheet: MapMarkerRange@8, SizeFactor@10, OffsetX@20, OffsetY@22.
  PRACTICAL CHECK outstanding: compare the aetheryte waypoint with the aetheryte
  GameObject.
- Y height: the vnavmesh IPC `Query.Mesh.PointOnFloor(Vector3 p, bool
  allowUnlandable, float halfExtentXZ) → Vector3?` (IPCProvider decompiled
  2026-07-11; vnavmesh uses the same route for FlagToPoint). Further queries:
  NearestPoint/NearestPointReachable/IsPointOnMesh;
  `Nav.Pathfind(from, to, fly) → List<Vector3>` (the waypoint list!).
- TRAP: `PointOnFloor(p, allowUnlandable, halfExtentXZ)` casts DOWNWARDS
  (FindPointOnFloor) → on a walkway or a raised path it snaps to the floor FAR
  BELOW (log 2026-07-11 19:52: input Y=-12.9 → result Y=-50.5, 37 m lower; an
  18 m transition became a 40 m run into the basement).
  For targets at player height use `NearestPoint(p, halfExtentXZ, halfExtentY)`
  instead → the nearest mesh point in a BOUNDED box (vertically capped, it does
  not fall through). Signature `<Vector3, float, float, Vector3?>`.
  ResolveFloorPoint now uses NearestPoint(10,10) first, with PointOnFloor only as
  a fallback.

### Zone transitions: the REAL boundaries (ilspycmd-verified 2026-08-09)
- The map symbol of a transition (MapMarker DataType 1/2, see above) is map
  graphics: map pixels, NO extent, NO direction. It is good for naming and
  listing, NOT as a walk destination — you land beside it instead of going
  through (user report 2026-08-09 "I can't get across, maybe I'm standing at an
  angle").
- The real boundary is maintained by the layout engine: `ExitRangeLayoutInstance`
  (`Client.LayoutEngine.Layer`), `InstanceType.ExitRange = 41`.
  Its own fields: `ExitType`@128 (`ExitRangeType`: **ZoneLine = 1**,
  **Invisible = 2**), `ZoneId`(ushort)@132, `TerritoryType`(ushort)@134 = the
  target zone, `Index`(int)@136, `DestInstanceId`@140, `ReturnInstanceId`@144,
  **`PlayerRunningDirection`(float)@148**.
- Inherited from `TriggerBoxLayoutInstance`: `Collider*`@48, **`Transform`@64**
  (`LayoutEngine.Transform`, size 48: `Translation`@0, `Rotation`(Quaternion)@16,
  `Scale`@32 — the centre AND the extent of the trigger box), `Priority`@112,
  `FlagsType`@116, `FlagsActive`@120.
- Access: `LayoutWorld.Instance()` → `ActiveLayout`(`LayoutManager*`)@32 →
  `Layers` (`StdMap<ushort, Pointer<LayerManager>>`@552) →
  `LayerManager.Instances` (`StdMap<uint, Pointer<ILayoutInstance>>`@40),
  filtering on `ILayoutInstance.Id.Type == ExitRange` (`Identifier`@24:
  `Type`(InstanceType)@1, `LayerKey`@2, `InstanceKey`@4).
  `ILayoutInstance.IsActive` is a bit field in `Flags3`@43.
- TRAP when iterating: `StdMap` yields a **`StdPair`** (`Item1`/`Item2`), NOT a
  `KeyValuePair` — and `Item2` is a `Pointer<T>`, so `.Item2.Value`.
- NOT MEASURED, THEREFORE NOT USED: the meaning of `PlayerRunningDirection`
  (unit, frame of reference, which of the two directions) and whether `Scale` is
  the half or the full extent. The probe `/acc uebergang` logs both readings. A
  wrong walking direction would steer the character AWAY from the boundary —
  worse than the current state.

### Talk / TalkSubtitle (log-verified 2026-07-11)
- AddonTalk has only UNNAMED text node fields
  (AtkTextNode220/228/238/240/248, ilspycmd) — no named "Name" field.
- PROBE-VERIFIED (dialogue node lines, sessions 09:36 + 10:14): the Talk speaker
  name = text node id=2, the dialogue text = id=3. The name node comes AFTER the
  text in node list order (last).
- `_BattleTalk` (the combat speech bubble, [ArenaText] log 2026-07-26 16:26):
  NPC/instructor announcements in instances and on the combat practice ground
  ("Erledigt zuerst den Thaumaturgie-Lehrer", "Das ist der falsche Gegner!"). The
  speaker name = text node id=4, the announcement text = id=6. It is read by the
  SAME handler (OnTalkUpdate); the speaker node id is addon-dependent (Talk=2,
  _BattleTalk=4). V5.55.

### ConfigSystem (system configuration, dump 2026-07-11 10:16, 593 nodes)
- Category tabs: 8× CT=DragDrop(17), NodeIds 7–14 (indices [581]–[588], at the
  end of the node list). The active tab: child id=4 is visible.
- The page heading = top-level text id=22 (e.g. „Anzeigeeinstellungen").
- TRAP: top-level text id=4 = the fps counter („59 fps"), which lies BEFORE the
  heading in a backwards search and changes every second → the heading search has
  to skip volatile texts (fps/numbers).
- Controls: CheckBox(3)/RadioButton(4)/Slider(6)/DropDownList(10), the label =
  the component's child text id=2; section headings are top-level texts in their
  own right (id 575 „Farbwahrnehmung" and so on).
- Footer buttons: „Voreinstellung"/„Schließen"/„Anwenden" (Comp 1001? via the
  child id=2 text).
- Volume sliders (the „Sound" tab, V5.58): the row pattern is a top-level text
  label → a mute checkbox Comp(1027) → a slider Comp(1023); the label = the
  nearest preceding top-level text (NearestPrecedingLabel). Sliders run 0..100 →
  announce them as a percentage. `NearestPrecedingLabel` finds e.g.
  „Hauptlautstärke" before slider id=113.
  SHORT FORM for 0..100 sliders: "label, value %" (not "slider, from 0 to 100" —
  the long form got cut off while navigating quickly, user 2026-07-27).
- TRAP, double announcement (V5.58): audio sliders carry the value as the child
  text id=2 („100"); the GENERIC focus reader read that bare number about 14 ms
  after the config announcement and choked off the label. Fix: skip bare numbers
  while ConfigSystem is visible (as with JournalResult).
- Switch state (V5.58): the checkbox announcement is "label, switch, on/off";
  disabled ("greyed out") ones are detected by **`NodeFlags.Enabled` (0x20) being
  cleared** on the component node — ilspycmd-verified against FFXIVClientStructs;
  dump: active F=0x2033 vs. greyed out F=0x2013 (e.g. the background playback
  sub-items when master is OFF, and the „Anwenden" button before a change).
- Accessibility = tab 8 (DragDrop, tooltip „Barrierefreiheit"). The page switches
  while NAVIGATING and is read out (colour perception/visualise sounds/
  transparency etc.). Enter is swallowed by the game in ConfigSystem (IKeyState
  does not see it) → our own tab Enter activation (TryActivateFocusedConfigTab)
  never fires there; but it is not needed for the page change. The heading shows
  „Anzeigeeinstellungen" (an open cosmetic point).
- JournalDetail: a companion addon (never focused, ChildAddonAttached to
  Journal). The content sits in the comp CT=JournalCanvas(20), with direct text
  children: id=38 quest title, id=9 level, id=8 description text, id=7 label
  „Beschreibung", id=11 label „Ziel". Quest objectives = Multipurpose(21)
  components with a non-empty id=3 text („Mit Miounne sprechen"). The labels come
  AFTER their content in node order (Z-order).

### Combat: enemy HP, cast, hotbar (ilspycmd-verified 2026-07-11)
- Enemy/target data through the Dalamud `IBattleChara` (inherits `ICharacter`):
  `CurrentHp`/`MaxHp`/`CurrentMp`/`MaxMp` (uint, from ICharacter); `IsCasting`
  (bool), `IsCastInterruptible` (bool), `CastActionType` (byte), `CastActionId`
  (uint), `CastTargetObjectId` (ulong), `CurrentCastTime`/`TotalCastTime`
  (float). Access: `ITargetManager.Target as IBattleChara` (only character
  objects have HP; NPCs/objects cast null).
- The cast action name: the Lumina sheet `Action` (Lumina.Excel.Sheets.Action),
  `.Name` is a `ReadOnlySeString` → `.ExtractText()`; access
  `IDataManager.GetExcelSheet<Action>().TryGetRow(CastActionId, out row)`.
  Namespace collision with System.Action → `using LuminaAction = ...`.

### AoE shape/radius (the Action sheet, ilspycmd-verified 2026-07-26)
Needed for the AoE dodging feature. Lumina `Action` sheet fields (offsets
decompiled from Lumina.Excel.Sheets.Action):
- `CastType` (byte, @40) — the SHAPE of the action (circle / cone / line /
  donut …).
  ⚠️ The sheet only delivers the number, no meaning. The number→shape mapping is
  community knowledge but is NOT verified against the code → it is established
  empirically with the DEBUG probe `CombatService.AoeCastProbe` before any
  "you-are-standing-in-it" logic is built on it. Do not guess it hard-coded.
- `EffectRange` (byte, @41) — range/radius.
- `XAxisModifier` (byte, @42) — width (for lines/rectangles).
- `Omen` / `OmenAlt` (RowRef<Omen>, @28/@30) — a reference to the telegraph
  graphic (the ground marker). Not yet evaluated.
- CASTTYPE→SHAPE (established from the [AoeProbe] log + OmenPath, combat practice
  ground 2026-07-26):
  - `2` = CIRCLE at the target position (Feura, EffectRange=5, OmenPath
    'general_1b'). The centre = the cast's target object (CastTargetObjectId),
    otherwise the caster. Ground-placed circles without a target object have
    their centre only in the VFX -> still open.
  - `3` = CONE from the caster in the facing direction (Kahlrodung,
    EffectRange=6=length, OmenPath 'gl_fan090' = a full 90 degrees). The half
    angle = the fan number / 2. Other cones: fan060/fan120 -> parse the angle from
    the name.
  - `4` = LINE/RECTANGLE from the caster in the facing direction (Spalten,
    EffectRange=30=length, XAxisModifier=2, OmenPath 'general02'). ASSUMPTION:
    the half width = XAxisModifier (verify in-game). Careful: EffectRange is the
    LENGTH here, NOT a radius -> treating a line as a circle = a huge false zone
    (that was the V1 bug).
  - Unknown types: conservatively a caster circle (better to over-warn than
    under-warn).
  - The geometry is implemented in `CombatService.IsPlayerInAoe` (V5.55).
- MEASUREMENT PROBE (V5.55, #if DEBUG, automatic per frame): `AoeCastProbe`
  iterates the ObjectTable and logs, per casting IBattleChara (deduped by
  casterId, rising edge), `[AoeProbe]` with
  castId/name/CastType/EffectRange/XAxisModifier/Omen + the geometry (casterPos,
  rot, playerPos, dist, relBearing per the verified rotation convention, atMe,
  castTime). Purpose: to map the CastType numbers against what the player really
  sees. Compiled out of the release.

- Hotbar: `RaptureHotbarModule.Instance()` (via UIModule, a direct static
  Instance() is present). `GetSlotById(uint hotbarId, uint slotId)` →
  `HotbarSlot*`. The UI "hotbar 1" = hotbarId 0; 16 slots per bar, the default
  keys 1–9,0 = slots 0–9, slots 10/11 = keys 11/12 (HOTBAR_1_A/B = VK137/139).
- `HotbarSlot`: `CommandType` (enum `HotbarSlotType : byte`, Empty=0, Action=1,
  Item=2, …, GeneralAction, Macro, Emote, Mount …), `CommandId`@184 (uint = the
  ActionId for type Action), `PopUpHelp`@0 (Utf8String = the game's own display
  name including the keybind hint, universal for all types; use it as a fallback
  after Lumina). Further useful members: `IsSlotUsable(type, id)`,
  `IsSlotActionTargetInRange2(type, id)` (for a later cooldown/range
  announcement, still unused).

### Cooldown / recast (ActionManager, ilspycmd-verified 2026-07-30)
- `ActionManager.Instance()` → `ActionManager*`. All cooldown queries take an
  `ActionType` (enum: None=0, **Action=1**, Item=2, GeneralAction=5 …) + an
  actionId.
- INSTANCE methods (`am->…`):
  - `GetRecastTime(ActionType, uint id) → float` = the TOTAL cooldown of the
    action (independent of the current state). GCD skills ~2.5 s; real abilities
    (oGCD) considerably more. `CooldownService` uses a threshold of >3 s to
    exclude the GCD — without having to guess the build-specific GCD recast group
    id.
  - `IsRecastTimerActive(ActionType, uint id) → bool` = is the cooldown still
    running (true = on cooldown). A falling edge true→false = "ready again".
  - `GetRecastTimeElapsed(ActionType, uint id) → float` = how much has elapsed so
    far.
  - `IsActionOffCooldown(ActionType, uint id) → bool`.
  - `GetCurrentCharges(uint id) → uint` (instance).
- STATIC (without a thisPtr): `ActionManager.GetMaxCharges(uint id, uint level) →
  ushort`. maxCharges>1 = a charge ability; the charge count then counts
  (IsRecastTimerActive stays true until FULL, which is why charges are used as
  the signal).
- Also present (still unused): `GetRecastGroup(int type, uint id) → int` (the GCD
  group exists, but its number is build-dependent — do NOT hard-code it),
  `GetActionRange`, `GetActionCost`, `GetAdjustedRecastTime`, `StartCooldown`,
  `GetActionStatus`.
- Used by `CooldownService` (V5.61): walk the standard bars 0..9, dedupe action
  slots, exclude the GCD via >3 s, announce the on→off edge (tone + name).

### REBINDING the hotbar + learned skills (ilspycmd-verified 2026-07-17)
- `RaptureHotbarModule.SetAndSaveSlot(uint hotbarId, uint slotId, HotbarSlotType
  commandType, uint commandId, bool ignoreSharedHotbars=false, bool
  allowSaveToPvP=true)` — writes ONLY the SAVED hotbar state, NOT the live bar!
  PROVEN IN-GAME (2026-07-17): an assignment at 9:43 had no live effect (even two
  frames later, with the bar not shared), but appeared on the bar after the relog
  at 11:57. The GitHub documentation ("sets a hotbar slot and triggers a save") is
  misleading here.
  ⇒ For immediate effect, call `LoadSavedHotbar(classJobId, hotbarId)` afterwards
  ("loads the saved hotbar into the live hotbar, will not reload from disk",
  respects PvP automatically) — V4.78 does that; check success by reading back
  through `GetSlotById` (two frames later).
  Related: `ClearSavedSlotById(hotbarId, slotId)` (empty a slot),
  `ExecuteSlotById(hotbarId, slotId)` (trigger a slot, byte return),
  `IsHotbarShared(hotbarId)` (bool).
- The Lumina `Action` sheet (columns decompiled 2026-07-17): `Name`,
  `ClassJobLevel` (byte; 0 = not a per-level learned player action),
  `ClassJobCategory` (RowRef, a bool column per job as in the item sheet — the
  column is chosen by the English ClassJob abbreviation, see
  GearInfoService.AllowsJob), `ClassJob` (RowRef), `IsPvP`, `IsRoleAction`,
  `IsPlayerAction` (packed bools), `UnlockLink` (an untyped RowRef, uint at
  offset+4; 0 = no quest unlock needed).
- Unlock check: `UIState.Instance()->IsUnlockLinkUnlockedOrQuestCompleted(uint
  unlockLinkOrQuestId, byte minQuestProgression=0, bool a4=true)` — by its
  signature it takes an UnlockLink OR a quest id (covering both cases of the
  UnlockLink column). UIState lives in
  `FFXIVClientStructs.FFXIV.Client.Game.UI`.
- Skill browser filter: RowId!=0, !IsPvP, ClassJobLevel 1..player level,
  ClassJobCategory contains the current job, UnlockLink satisfied, AND
  IsPlayerAction==true — without the last one, internal rows slip through the job
  filter (established in-game 2026-07-17 12:01: five times „Ausweichen" +
  „Perfekter Hieb" on job 26). OPEN: an exact comparison with the "Actions &
  Traits" window (the log `[Hotbar] Skill-Liste gebaut` shows the count).
- NO unlock method in `ActionManager` (reviewed in full 2026-07-17) — action
  unlocking runs solely through UIState/UnlockLink.
- BAR COUNT (ilspycmd 2026-07-17): `RaptureHotbarModule.Hotbars` =
  FixedSizeArray18<Hotbar>; `StandardHotbars` = Hotbars[0..9] (10 of them, the UI
  "hotbar 1–10"), `CrossHotbars` = Hotbars[10..17] (gamepad). Each hotbar has 16
  slots (FixedSizeArray16<HotbarSlot>), and the standard UI uses 12.
  `GetSlotById(uint hotbarId, uint slotId)`, `LoadSavedHotbar(uint classJobId,
  uint hotbarId)` and SetAndSaveSlot all take the bar number — the V4.78 route
  applies to every bar.
- HOTBAR KEYS in the InputId enum (live dump 2026-07-17): `HOTBAR_{bar}_{suffix}`
  with suffix 1..9, 0, A, B = slot 0..11 (HOTBAR_1_1=57, blocks of 12 directly one
  after another). Bar 2 is Ctrl+1..Ctrl+0 by default (+Ctrl+VK137/139 for slots
  11/12); bar 3 onwards is unbound. Live query: KeybindService.GetBoundKey
  (Enum.TryParse<InputId> → GetKeybindSpan()[Index], V4.81).

### ConfigKeybind — the „Tastenbelegung" (key bindings) window (F5 dump 2026-07-17)
- CORRECTION (log 2026-07-17 13:12, refuting the 09:45 finding): the arrow keys
  move the GLOBAL focus (AtkInputManager.FocusedNode), while the list indices
  stand still (Hov2 stayed 0, only ONE list navigation on opening). The list
  scrolls UNDER a fixed focus node (the same node pointer, changing row text) —
  so row announcements have to be re-read per frame, not only on a focus change.
  ListLen changes per category tab (movement 32, hotkeys 134). Since V4.79 the
  announcement runs through UpdateGlobalFocus → ClimbToItemRenderer → a dedicated
  row reader.
- A TRAP along the way: GetTextFromNodeTree discards texts of length 1 —
  single-character key labels („W", „1", „C") were therefore missing from the
  generic focus path, while multi-character ones („Tab", „NUM0") were not.
- A row = a ListItemRenderer(14) with: a direct text id=2 = the command name
  („Kommandomenü 1 - Slot 1"), a button component id=6 = binding 1, button id=5 =
  binding 2; the key text sits IN EACH CASE in a text child id=5 INSIDE the button
  component. The generic ReadListItemText only reads direct text nodes → the keys
  were missing from the announcement (fix V4.77: ReadConfigKeybindRow).
- German: the hotbar is called „Kommandomenü" in the UI, with the tabs as radio
  buttons: Bewegung/Zielen/Schnelltasten/Chat/System/Kommandos/Gamepad; the
  buttons Schließen/Anwenden/Zurücksetzen; the checkbox „Direkt-Chatmodus
  aktivieren".
- IMPORTANT (semantics): this window changes KEY→SLOT bindings ("which key fires
  hotbar 1 - slot 1"), NOT which skill sits in the slot (that is the hotbar itself
  / SetAndSaveSlot).
- OPEN: what does Enter on a row trigger (a capture mode for a new key?) — never
  tested, no handler; the next in-game test.
- StdList (e.g. `Map.UnacceptedQuestMarkers`): implements `IEnumerable<T>`+`Count`;
  `GetEnumerator()` returns a struct enumerator (foreach is allocation-free),
  yielding by value (a read-only copy is safe).

### Toasts / error messages (IToastGui, ilspycmd-verified 2026-07-17)
- Action errors („Das Ziel ist zu weit entfernt.", „Die Aktion ist noch nicht
  bereit.") are ERROR TOASTS in the overlay `_TextError`.
- TRAP: `_TextError` NEVER fires PostRefresh — the log of 2026-07-17 shows only
  the empty PostSetup at login across a whole session. So the lifecycle approach
  (NotificationAddons) fundamentally cannot deliver these messages. Most action
  errors are not mirrored into the chat either.
- The clean route: `Dalamud.Plugin.Services.IToastGui` (ilspycmd against
  Dalamud.dll): the events `ErrorToast(ref SeString, ref bool isHandled)`,
  `Toast(ref SeString, ref ToastOptions, ref bool)`, `QuestToast(ref SeString,
  ref QuestToastOptions, ref bool)` — they fire on the game's show-toast call.
  Since V4.80 ToastService.cs reads them out (errors = interrupt, info/quest =
  queued with WasRecentlySpoken echo protection, because some info toasts are
  drawn in parallel as `_WideText`/`_ScreenText`).

## Tools / traps

- TRAP, NodeType: component nodes carry values >= 1000 in the RAW Type field
  (1003, 1006, 1027, …). `NodeType.Component` is 10000 and is only returned by
  GetNodeType() (ilspycmd 2026-07-11, the doc remark in the enum). A comparison
  `node->Type == NodeType.Component` is therefore ALWAYS false — which is how
  FindListInAddon was dead from the moment it was introduced and the universal
  list navigation never fired (journal, SystemMenu, SelectString all mute
  in-game). Correct: `(int)node->Type >= 1000`.

- TRAP, dalamudConfig.json: Dalamud reads it through ReliableFileStorage (raw
  bytes → UTF8.GetString, NO BOM strip). A file written with a BOM (PowerShell 5.1
  `Set-Content -Encoding utf8`!) throws a JsonReaderException in the parser →
  Dalamud falls back SILENTLY (verbose log only) to its SQLite backup
  `dalamudVfs.db` and overwrites the file on the next save with the old state.
  External edits are lost silently that way. Always write without a BOM:
  `[IO.File]::WriteAllText(path, text, UTF8Encoding($false))`. (Proven 2026-07-10
  by a repro with Dalamud's own serializer settings; it cost three mysterious
  failed attempts.)
- Dalamud loads dev plugins ONLY from `DevPluginLoadLocations` in
  dalamudConfig.json (+ DevMode=true) — the devPlugins folder alone is NOT
  enough. New dev plugins additionally need a DevPluginSettings entry with
  StartOnBoot=true.
- ilspycmd 9.1.0: `--list-types` is broken (1 line) — but `-l c` (classes), `-l s`
  (structs) and `-l e` (enums) work; individual types via `-t`.
- UIReaderService.cs has mixed character encoding — when editing, choose
  old_strings without umlauts; once a U+2000 space was lodged in it (replaced with
  awk).
- MEMORY_BASIC_INFORMATION needs Size=48 (not 44), otherwise VirtualQuery ALWAYS
  fails silently → verify the IsReadable helper with a positive test.
- Dalamud loads the dev plugin DIRECTLY from `bin\Debug\net10.0-windows\` —
  restart the game after every build.

### Level / experience (PlayerState, ilspycmd-verified 2026-07-12)
`PlayerState.Instance()` (FFXIVClientStructs.FFXIV.Client.Game.UI):
- `CurrentLevel` (short) = the level of the ACTIVE job (the real level; alongside
  it `SyncedLevel`/`IsLevelSynced` for level sync in dungeons)
- `CurrentClassJobId` (byte) = the active job (for level-up tracking, so that a
  job change does not count as a "level up")
- `ps->GetCurrentClassJobExp()` (uint) = the current EXP within THIS level
- `ps->GetCurrentClassJobNeededExp()` (uint) = the EXP for the next level; == 0 at
  maximum level
- "Remaining until level up" = NeededExp − CurrentExp
- IMPORTANT: do NOT call the static `delegate* unmanaged<PlayerState*,uint>`
  properties as `PlayerState.GetCurrentClassJobExp(ps)` (the compiler picks the
  zero-argument instance method → CS1501). Use the instance method on the
  pointer: `ps->GetCurrentClassJobExp()`.
- Level-up announcement: read CurrentLevel every frame and announce on an
  increase (same job) — cleanly from PlayerState, no UI scraping.
  (CombatService.TrackLevelUp)
- XP gain announcement (V5.52, user request 2026-07-25): read
  GetCurrentClassJobExp() every frame, announce the delta on an increase (same
  job) ("X experience") and write it into the review channel "loot". The baseline
  per job (a job change alters the value without a real gain) + the level-up
  reset (the value falls towards 0) are only tracked silently; needed==0 (max
  level) => no tracking. Non-interrupting (Speak), so that XP never cuts off an
  HP/cast warning. (CombatService.TrackXpGain)

### The loot channel (items picked up) — VERIFIED (live [Chat] log 2026-07-25)
Loot/currency that goes into the inventory arrives through
**XivChatType.LootNotice (62)** — an empty sender, a full sentence ("Du hast ein
Lammfilet erhalten.", "Du hast 115 Gil erhalten.", "Du hast 17 Legionstaler
erhalten."). It covers enemy drops (sheep -> mutton/ram's horn), gil, GC seals
and gathering crystals. It lies OUTSIDE the combat log range (41-49) and is
therefore NOT discarded by IsCombatLogLine — it arrived cleanly in the [Chat] log
from the very beginning, just unread (ShouldRead defaulted to false). V5.52:
LootNotice -> ReadLoot (config AnnounceLoot), review channel "loot" (shared with
XP), no prefix. Gathering (67) remains the separate harvesting channel.
- OPEN (not verified): instance/dungeon loot via the roll system (need/greed)
  could run through another channel (LootRoll?) — pull that from a dungeon log
  later if needed.

### Executing emotes (ilspycmd-verified 2026-07-12)
- `AgentEmote.Instance()` (FFXIVClientStructs.FFXIV.Client.UI.Agent):
  - `agent->ExecuteEmote((ushort)emoteId, playEmoteOption=null,
    addToHistory=true, liveUpdateHistory=true)` — triggers an emote directly (the
    same function as the emote menu); no chat and no UI needed. An external call →
    try-catch.
  - `agent->CanUseEmote((ushort)emoteId)` — true when it is unlocked.
- The Lumina sheet `Emote`: RowId == the emoteId for ExecuteEmote; `Name` = the
  display name („Verbeugen"); `TextCommand` = RowRef<TextCommand> → `.Command` =
  the real /command (IMPORTANT: the German /command ≠ the display name;
  "/verbeugen" does NOT exist — read the command from the sheet, do not guess it).
  `Order`/`EmoteCategory` for sorting.
- Implemented: EmoteService (browser: Shift+F4/F5 page through the usable emotes
  alphabetically, Shift+F6 executes). Reason: a blind user cannot type in chat and
  cannot navigate the icon emote palette.

### The JournalResult reward window (UI-dump-verified 2026-07-12)
JournalCanvas contains the reward entries as Multipurpose(21) components:
- ITEM reward: Comp(1010) Multipurpose → focus on the id=3 collision, child id=2
  Comp(1003) Icon(15) = AtkComponentIcon (IconId → name via ResolveIconName, the
  quantity in QuantityText/id=7). An empty slot = IconId 0.
- CURRENCY/EXP: Comp(1007) Multipurpose → focus on the id=5 collision, the amount
  in child id=2 Comp(1011) TextNineGrid(19) → id=2 text ("260"/"127"). The TYPE
  (experience/gil) exists ONLY as the id=3 image (no resolvable icon) → currently
  labelled by position (experience first, then gil = the standard FF14 order).
- Buttons: id=38 „Ablehnen", id=37 „Abschließen".
- Implemented: UIReaderService.BuildRewardText reads "reward: <items>, experience
  X, gil Y" on opening. Reason: focus navigation of the currency cells announced
  only bare numbers (user: "I want to know what the entry is").

### Recognising a main scenario quest (MSQ, ilspycmd-verified 2026-07-12)
- Lumina Quest.JournalGenre → JournalGenre.JournalCategory →
  JournalCategory.JournalSection. MSQ = JournalSection.RowId == 0 („Hauptszenario").
- Implemented: QuestMarkerService builds a HashSet of the MSQ quest names from the
  quest sheet once and matches the marker labels against it (MarkerInfo has no
  direct quest pointer, only Label + ObjectiveId). QuestDestination.IsMainStory →
  the announcement "story: <quest>". The [Quest] main scenario name log shows the
  count.
- MarkerInfo fields: ObjectiveId(uint), Label(Utf8String), MarkerData(StdVector),
  RecommendedLevel(ushort), ShouldRender(bool). MapMarkerData: IconId, Position,
  MapId, TerritoryTypeId, ObjectiveId, MarkerType(byte), Flags(byte), DataId.

### Dalamud's own UI is ImGui — not readable (verified 2026-07-19, ilspycmd Dalamud.dll)
- Dalamud's plugin installer, the Dalamud settings and the windows of third-party
  plugins (e.g. vnavmesh) are drawn in **ImGui**: no AtkUnitBase, no nodes, no
  tree. Neither UIReaderService nor NVDA can find anything there. There is NO
  UI-scraping route — do not go looking for one.
- The right route: read the DATA behind the UI.
  `IDalamudPluginInterface.InstalledPlugins` → `IEnumerable<IExposedPlugin>`.
- `IExposedPlugin` (public, `Dalamud.Plugin`): Name, InternalName, Version,
  IsLoaded, IsOutdated, IsTesting, IsOrphaned, IsDecommissioned, IsBanned, IsDev,
  IsThirdParty, Manifest, HasMainUi, HasConfigUi, `OpenMainUi()`,
  `OpenConfigUi()` (which throw an InvalidOperationException when the respective
  HasXUi is false).
- `IDalamudPluginInterface.OpenPluginInstallerTo(kind, searchText)` only opens the
  ImGui window → worthless for blind users. `CheckForUpdateAsync()` applies ONLY
  to our own plugin.
- NOT public: installing/updating/removing. `InstallPluginAsync`,
  `UpdatePluginsAsync`, `UpdateSinglePluginAsync`, `RemovePlugin`,
  `UpdatablePlugins` live in `Dalamud.Plugin.Internal.PluginManager` (internal) —
  reachable only through reflection, and liable to break silently on Dalamud
  updates. Deliberately not used (user decision 2026-07-19); installation/updates
  run through the installer EXE.
- Used in `DalamudPluginsService.cs` (V5.13, Shift+F1/F2/F12).

### GrandCompanyExchange (the seal quartermaster) — F5 dump 2026-07-25 (V5.47)
A shop where you exchange Grand Company seals for items. It is picked up by the
GENERIC list navigation (not suppressed, it has a `List(9)`), but the generic row
read out cryptically as „0, 1.060, Legionaers-Schwert" (column order, without
labels, sometimes doubled on visibility flicker).
- The addon „GrandCompanyExchange", window title `Comp(1007)` child id=3 =
  „STAATSTALER EINTAUSCHEN".
- **Item list**: id=57 `Comp(1014)` `[CT=List(9)]`, `ListLen=21`. As everywhere,
  the keyboard tracks `HoveredItemIndex2` (@344) — the generic `TrackListIndices`
  announces it.
- **Row template** (`ListItemRenderer`/`Comp(1015)`, every row identical):
  - id=10 text = **owned** (how many you already have), e.g. „0".
  - id=7  text = **the price in seals**, e.g. „1.060".
  - id=6  `Comp(1011)` `NumericInput` = **the purchase quantity**, child text id=5
    = „1" (it sits in the NumericInput's OWN ULD, NOT in the renderer NodeList →
    so neither the generic reader nor the dedicated reader picks it up by
    accident).
  - id=5  text (INVISIBLE) = a duplicate of the item name.
  - id=4  text (visible) = **the item name** (an SeString payload, sanitising
    needed).
- **Category tabs** (`RadioButton(4)`/`Comp(1008)`, text child id=2): id=44
  weapons, id=45 armour, id=46 military supplies, id=47 materials, id=48 special
  items. The ACTIVE tab = the radio button with `IsChecked==true`.
  `AtkComponentButton.IsChecked` = `BitOps.GetBit(Flags@232, 18)` (ilspycmd
  2026-07-25; `AtkComponentRadioButton` inherits `AtkComponentButton`). There is
  NO shared title node as in ArmouryBoard (id=121) — so determine the tab through
  the checked state, and take the label from the text child id=2. V5.47:
  `OnGrandCompanyUpdate` announces "category X" on a tab change (the rank icons
  `Comp(1016)` are radio buttons too, but have NO text child id=2 → filtered out
  by their empty label).
- **Rank icons** (`RadioButton(4)`/`Comp(1016)`, id=37–42): WITHOUT text → the
  global focus reader oscillates mutely here (log 2026-07-25, [Focus] STUMM, about
  every 0.3 s).
- Addon root texts: id=6 = your own GC rank („Legionaer 3. Klasse"), id=8 = your
  own seal balance („300"). (NOT yet used — a candidate for an opening
  announcement, PostSetup timing unverified.)
- **Solution V5.47**: a dedicated `ReadGrandCompanyRow` (name/price/owned via
  `ReadComponentTextById` id 4/7/10) → "name, X seals, owned Y"; hooked into the
  `name switch` of `TrackListIndices`. Stable text ⇒ the `idx|text` dedupe kills
  the duplicate.

## Fishing (ilspycmd-verified 2026-07-25, FFXIVClientStructs.dll + Lumina.Excel.dll)

Goal: accessible fishing. The first step, "where can I fish" — the runtime probe
`/acc fishprobe` (FishingService.Probe, read-only) logs (A) all objects within
200 m with their ObjectKind/DataId/position and (B) the zone's FishingSpot
catalogue with raw X/Z + the conversion. NOT YET verified (the probe is
outstanding): whether fishing holes appear in the ObjectTable (and as which
ObjectKind), and the X/Z scaling.

### Runtime state: FishingEventHandler (Client.Game.Event, size 560)
- It inherits `EventHandler` + `AtkModuleInterface.AtkEventInterface`. Access via
  `EventFramework.Instance()->GetEventHandlerById(<fish event ID>)` — the concrete
  ID is NOT verified (CraftEventHandler uses 655361/0xA0001; pin the fishing ID
  down with a probe on the active handler's `GetEventId`).
- `State` @456 = the enum **FishingState** — the ground truth of the fishing
  process: None, CastingOut, PullingPoleIn (no bite / the fish got away / after a
  catch / the rest), Quitting, PoleReady (standby, rod ready), **Bite (A BITE —
  strike now!)**, Hooking (striking + reeling in), ReleasingCatch,
  ConfirmingCollectable, AmbitiousLure/ModestLure (an action animation only),
  Unk11, LineInWater (the line is in the water, waiting for a bite). ⇒ the bite
  announcement = the edge State->Bite.
- `CanFish` @464 (bool) — this covers "standing correctly": whether a cast can be
  made right now. Further flags @465–470: CanMoochPreviousCatch,
  CanMooch2PreviousCatch, CanReleasePreviousCatch, ChangingPosition,
  CanIdenticalCastPreviousCatch, CanSurfaceSlapPreviousCatch.
  `CurrentCastBaitFlags` @472 (FishingBaitFlags).
- The tug strength (light/medium/heavy) is NOT visible as a field in THIS struct —
  settle it with a probe (it may be derivable from the bite subtype/animation).

### FishingModule (Client.UI.Misc, size 192) — NOT runtime
- A pure save file (UserFileEvent): the fishing log. `UnseenFishCount` @188.
  Irrelevant for positioning/bite announcements.

### The FishingSpot sheet (Lumina) — a static catalogue of all fishing spots
- Fields: `TerritoryType` @52 (RowRef, = the zone), `PlaceNameMain` @54,
  `PlaceNameSub` @56, `PlaceName` @60 (RowRef, display name), `Radius` @58
  (ushort), `Order` @62, `X` @64 (short), `Z` @66 (short), `GatheringLevel` @68
  (byte, the fishing level required), `FishingSpotCategory` @69, `Rare` @71. Zone
  filter: `row.TerritoryType.RowId == clientState.TerritoryType`.
- X/Z = MAP PIXELS (0..2048), VERIFIED against real sheet values (Lumina against
  sqpack, 2026-07-25): all 333 rows lie within X 108..1948 / Z 210..1934, i.e. in
  the pixel range; the conversion yields sensible map coordinates (Fallgourd Float
  21.0/24.6; Limsa Lower Decks 7.7/12.2). ⇒ NOT MapCoordToWorld (1..42), but
  `PlacesService.MapPixelToWorld(X, Z)` (which uses the verified PixelToWorld
  formula, as MapMarker does). The radius is NOT in the same world units (city
  values up to 3000) — ignored for guidance, since a generous arrival distance +
  the navmesh are enough.
- The Y height is missing (map data is 2D) → resolve it via the navmesh
  (PointOnFloor / PathfindAndMoveCloseTo), as with all other waypoints. STILL to
  be confirmed LIVE: that the converted point lands on the fishing hole (the
  compass announcement of /acc fish is the check).
- BUILT V5.52 (debug): FishingService.GetSpotsInCurrentZone +
  AnnounceSpotsInCurrentZone, the command **/acc fish** — announces the zone's
  fishing spots (name, level, distance, cardinal direction), nearest first.
- Related types (should they ever be needed): AddonFishingNote, AddonFishGuide2,
  AgentFishGuide, AddonSpearFishing, InstanceContentOceanFishing (ocean fishing),
  Lumina SpearfishingNotebook.

## Triple Triad (the card game) — AddonTripleTriad

Verified via ilspycmd against FFXIVClientStructs.dll (2026-07-26).

- Addon name: `"TripleTriad"` (GetAddonByName). The struct `AddonTripleTriad`
  (size 4056, `[Inherits<AtkUnitBase>]`, **`[GenerateInterop(false)]`** → NO
  generated span accessors!).
- The card lists are `internal FixedSizeArray*<TripleTriadCard>` — NOT directly
  accessible from the plugin. So read them by pointer arithmetic at the verified
  offsets (stride = `sizeof(TripleTriadCard)` = 168):
  - `_blueDeck` @576  — FixedSizeArray5 = your own hand (the player is always
    blue)
  - `_redDeck`  @1416 — FixedSizeArray5 = the opponent's hand
  - `_board`    @2256 — FixedSizeArray9 = the 3x3 board, row-major (fields 1..9)
  - The offsets sit exactly a stride of 168 apart (576+5*168=1416,
    1416+5*168=2256) → the stride is cross-verified.
- `TripleTriadCard` (size 168, a public struct in AddonTripleTriad):
  - `CardRarity`@128 (byte), `CardType`@129 (enum
    None/Primal/Scion/Beastman/Garland),
  - `CardOwner`@130 (enum **Empty=0, Blue=1, Red=2**),
  - `NumSideU`@131, `NumSideD`@132, `NumSideR`@133, `NumSideL`@134 (byte, edge
    values 1..10; the game displays 10 as "A"),
  - `HasCard`@164 (bool — on the board: the field is occupied; in the hand: the
    slot has not been played yet).
- `AddonTripleTriad.TurnState`@568 (enum **Waiting=0, NormalMove=1,
  MaskedMove=2**). HYPOTHESIS (still to be verified in-game): Waiting = not your
  turn, Normal/MaskedMove = it is your turn. The raw value is logged by
  TripleTriadService ([TripleTriad]).
- BUILT (debug, untested): `TripleTriadService.ReadBoard()` (Ctrl+Shift+F4) +
  `ReadHand()` (Ctrl+Shift+F5). Board: the card count on both sides, the turn
  state, then fields 1..9. Hand: your own cards by fixed slot (1..5), with played
  slots skipped. STILL TO BE TESTED IN-GAME.
- An open question for the test: whether the game cursor in the hand skips played
  slots or compacts the cards — that determines whether the fixed slot number
  (current behaviour) or a running number is the right reference.

## Quest items in combat (ilspycmd + sheet dump, 2026-08-09)

The trigger: a player question about "quests where you have to trigger something
with items during a fight". There are TWO separate mechanics — do not mix them up.

### A) The quest's key item (EventItem) — the common case
- The Lumina sheet `EventItem` (rows from 2000000). Fields:
  `Name`/`Singular`/`Plural`, `Quest` (RowRef to the quest that issues the item),
  `Action` (RowRef; for quest items usually `Action#1
  „Schluesselgegenstand"`/"key item"), `Icon`, `StackSize`, `Category`
  (EventItemCategory), `CastTime` (byte, the cast time in seconds),
  `CastTimeline`, `Timeline`.
- The quest → item mapping works in BOTH directions:
  - from the item: `EventItem.Quest.RowId`
  - from the quest: `Quest.QuestParams[]` with `ScriptInstruction` = `ITEM0`,
    `ITEM1`, … and `ScriptArg` = the EventItem RowId. Likewise `ENEMY0`
    (enemies), `ACTOR0` (NPCs), `HOWTO_EITEM` (the instruction id).
- ESTABLISHED EXAMPLE (offline sheet dump against sqpack, DE): quest **66333
  „Ein Licht fuer die Nacht"** (level 28, JournalGenre 113 „Nebenauftraege
  Finsterwald", North Shroud):
  - `ITEM0` = EventItem **2000627 „Bergmannslampe"** (StackSize 1, CastTime 1)
  - `ITEM1` = EventItem **2000628 „Gleissende Lampe"** (StackSize 2, CastTime 3)
  - `ENEMY0` = 2266
- Inventory: key items live in the container `GameInventoryType.KeyItems`; the
  `ItemId` there indexes the EventItem sheet (already used by
  `InventoryService.CollectKeyItems`).
- Placeable on the bar: `RaptureHotbarModule.HotbarSlotType.**EventItem**` (Id =
  the EventItem RowId). There is additionally `HotbarSlotType.KeyItem` — according
  to the struct documentation that is ONLY the DragDrop special case (Id = the
  slot index in the KeyItems container, resolved to `EventItem` when set). So for
  a programmatic assignment, `EventItem` + RowId is the right route.
- Executing it as an action: `ActionType.EventItem` (=3); alongside it there is
  `ActionType.EventAction` (=4).

### B) Special duty actions — the small extra bar
- `FFXIVClientStructs.FFXIV.Client.Game.DutyActionManager` (size 160):
  - `GetInstanceIfReady()` (static) — null as long as there are none
  - `ActionsPresent` @25 (bool), `NumValidSlots` @24 (byte)
  - `ActionId[5]` @32 (uint, the Action sheet), `ActionActive[5]` @26 (bool)
  - `Recast[5]` @52 (RecastDetail), `MaxCharges[2]` @152, `CurCharges[2]` @154
  - `GetDutyActionId(ushort slot)` (static, slot 0 or 1)
- Executing: `RaptureHotbarModule.ExecuteDutyActionSlot(uint index)` → bool; plus
  `GetDutyActionSlot(index)` → `DutyActionSlot` (inherits `HotbarSlot`, with the
  additional `PrimaryCostType`@224, `IsActive`@225).
- IMPORTANT for accessibility: in the live key binding dump (679 entries,
  2026-08-09) there is NO binding for this bar — the game expects a mouse click
  there. Without a mod it cannot be reached from the keyboard.

### What has NO source
When during a fight the item is to be used is in none of the structures above —
that is combat/quest logic. The available channels for it: the system message
(ChatReaderService), the enemy's cast (CombatService), and the quest's to-do list.
