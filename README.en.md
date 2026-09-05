# FF14 Accessibility

🇩🇪 Deutsche Version: [README.md](README.md)

A Dalamud plugin that makes **FINAL FANTASY XIV accessible to blind
players**: menus, dialogues, quests, navigation, inventory, combat and
hotbars are read aloud via screen reader (NVDA) and supported with audio
cues — including braille display output and automatic walking.

## Features

### Menus and windows

- Title screen, character selection and the complete character creation
  (race, gender, clan, name) are announced.
- Character creation, the **appearance** step: its roughly twenty pickers
  (face, hairstyle, colours, build …) are announced one by one, and Ctrl+F10
  reads the whole appearance back in one go. None of those pickers is text
  in the game — a screen reader finds nothing there otherwise.
  (Contributed by [bladestorm360](https://github.com/bladestorm360), PR #4)
- List navigation with arrow keys: system menu, journal, selection
  dialogues, context menus — every row is spoken as it gains focus.
- Ok/Cancel dialogues: left/right announces the focused button.
- Settings windows: sliders ("Transparency, slider, 50, from 0 to 100"),
  drop-down lists, checkboxes and tabs; Enter activates a tab.
- **Keybind window**: every row is announced with the command **and** its
  bound key ("Move forward, key W").
- The game's error messages and notifications (toasts) are spoken,
  e.g. "Target is too far away."
- NPC dialogues with the speaker's name first ("Miounne: …"); with the
  journal open, Ctrl+F10 reads the quest description and objectives.
- Every announcement is also sent to the **braille display**.

### Chat

- **Receiving**: incoming messages are read aloud, and each channel can be
  turned off individually in the settings menu (Shift+F9, entry "Chat
  channels") — a flat list, one row per channel. In the familiar chat
  system those are dialogue, say, shout, party, alliance, tell, free
  company, system, gathering and loot; in the new one they are the game's
  own channels. A channel switched off merely stops being spoken — it stays
  in the history for you to read back.
- **Writing**: opening the chat input announces the active channel ("Chat
  input, Say"), and so does switching channels while typing. Every typed
  character is spoken, as are deletions — because a screen reader cannot
  read the game's own input field.
- **Catching up**: a history browser with eight separate categories
  (dialogues, say, shout, party, alliance, tell, free company, system),
  with no message limit — the whole session is kept. This lets you review
  what you missed at your own pace without disturbing the live chat. Inside
  a category one key jumps to the start or the end, because a combat log
  runs into thousands of lines.
- **Replying**: Enter in the history browser answers the message you just
  heard in the right channel — a tell you read is answered directly, without
  typing the name.
- **A second chat system to choose from**: besides the familiar history
  there is a second one whose buffers follow the **game's own tabs and
  filters** — that is, whatever a sighted player set up in their chat tabs.
  The tabs can be switched by key (the game itself has none for that).
  Switch systems in the settings menu (Shift+F9); the **familiar one is the
  default**. Both histories always record, so switching mid-session leaves
  no gap. Alongside "Chat channels" this system also offers "Chat tabs",
  which goes finer: tabs, the channels inside them, and the game's own
  filter rows, where damage dealt can be told apart from damage taken.
  (Contributed by
  [bladestorm360](https://github.com/bladestorm360), PR #5)

### Navigation and walking

- **Object browser**: cycle through nearby objects with a single key
  (NPCs, merchants, enemies, **allies**, players, objects, **duties**,
  **all duties**, quest objectives, levequests, FATEs, gathering points,
  fishing spots, aetherytes, map waypoints such as zone exits). Announces
  name, kind, distance and direction; the object is targeted at the same
  time.
- **You decide the order of the categories** (Settings → Order). Move the
  ones you reach for most to the front; switch the ones you never use off
  entirely and they stop appearing as you cycle. The same goes for the chat
  history categories. In the sorting menu, **Numpad0** picks a row up,
  **Numpad 8/2** move it up and down — **Home** and **End** send it all the
  way to either end — and **Numpad0** puts it back down. Every step tells you
  **which two categories you are now between** ("Enemies, now 3 of 21. Between
  merchants and allies."), so you never have to sort by position number. Every
  step is saved immediately.
- On a **taming levequest** ("pacify the target with the *Soothe* emote") the
  announcement adds **"already tamed"** for an enemy you have already dealt
  with, and **"agitated, cannot be tamed"** for one where an attempt failed.
  Both move to the end of the list, the ones still open stay in front. This
  works for **every** taming task regardless of monster or levequest — the game
  keeps just one such state. A tamed enemy does not despawn and looks like
  every other one; without the hint you walk over to it and only find out when
  the game refuses the emote.
- The **allies** category collects everything fighting on your side: trust
  and duty-support NPCs, party and alliance, carbuncle, fairy, companion
  chocobo. **Duties** lists only the doors leading into a dungeon, trial,
  raid or PvP instance — such a door is a destination, not furniture.
  (Contributed by [bladestorm360](https://github.com/bladestorm360), PR #3)
- The **all duties** category goes further: it lists **every dungeon, trial
  and raid entrance in the game**, sorted by level — including those in
  other zones. It announces name, kind, level and, when the content is not
  unlocked yet, a "locked" (the game itself is asked; this is never guessed
  from your level). If the entrance is in your zone you also get distance
  and direction, otherwise the name of its zone and the next transition
  leading there. **Numpad 3 walks there** — across zone borders, one
  transition at a time, exactly like a quest objective in another zone.
- The **dungeon** category appears **only inside an instance** and is the only
  one **not sorted by distance** but **in walking order**: first the terminal,
  then the boss, then the gate behind it. It answers the question that matters
  in a dungeon and that no proximity search can answer — *where does it
  continue*. **Numpad 3 walks to the selected station.** It is a skeleton of
  sixteen stations on average, not a room plan: between two stations the
  auto-walk runs exactly as it does everywhere else.
  - **The installer fetches the required route files** (309 files, about
    750 KB) into
    `%AppData%\XIVLauncher\pluginConfigs\FF14Accessibility\DungeonPaths\`.
    Setting the plugin up without the installer still works: the **plugin
    downloads them itself on first start** while that folder is empty. As long
    as it is empty, the category is **not offered at all**.
  - In the settings menu (**Shift+F9**) the **dungeon routes** entry says how
    many routes are loaded, re-downloads them on request and switches the
    automatic download off.
  - The route files come from the [AutoDuty](https://github.com/erdelf/AutoDuty)
    project and are fetched **from there** — this plugin ships none of them.
- **Housing furnishings** can be found too: chocobo stable, mailbox, garden
  beds. Objects the game labels with an icon instead of a word are given the
  word its own interface uses for them.
- **Target beacon**: as soon as you **acquire a target**, a tone
  points at it — the side through stereo, "behind you" through a darker
  pitch, the distance through volume (closer = louder). **Every kind of
  target sounds different**: enemy, NPC, object, gathering node,
  transition, aetheryte, quest objective and duty entrance each have their
  own base note, with enemies and objectives adding a second beat. The
  closer you aim, the further the beats drift apart — and **once you are
  lined up it goes silent altogether**; a short acknowledging tone on
  locking in tells you the silence is intended, not a failure. That is what
  makes it work for lifts and platforms: turn until it goes quiet. Merely
  selecting something in the object browser does not start a tone — only
  targeting does; for destinations the game will not let you target (quest
  objectives, map markers, duty entrances from the list) the **walk guide**
  carries the tone instead. Toggle: **Ctrl+Shift+F9**, volume in the
  options menu. All tones can be auditioned with `/acc soundtest`.
- **Walk guide**: guided manual walking along the navigation mesh, around
  obstacles — with waypoint tones, direction announcements relative to
  where you are facing, and an arrival tone.
- **Auto-walk**: walk to the target automatically (requires the
  third-party plugin vnavmesh), with route preview, progress
  announcements and an honest message when no path exists.
- **Recorded trails**: places the navigation mesh does not know can be
  walked once by hand and recorded. If auto-walk gets stuck there later, it
  uses your own trail instead of giving up.
- **Housing districts**: there the mesh is often older than the houses,
  because it is built while the game is still loading the plots — auto-walk
  then ran into fences. The mesh is now rebuilt once per visit, as soon as
  the game reports the district fully loaded; the wait is announced.
- **Follow**: trail your current target continuously — stops when the
  target stops, ends on a zone change or when the target disappears
  (also requires vnavmesh).
- **Route preview**: hear the route without walking
  ("Path to aetheryte, 62 meters: 25 meters north, then …").
- **Facing direction**: while you turn, the compass direction you are
  looking at is announced (north, north-east …); can be turned off.
- **Coordinates**: copy your own map coordinates to the clipboard (to share
  them in chat) or walk to coordinates you copied.
- Target-change announcements for the game's own targeting keys
  (Tab, F1–F12).

### Combat

- Combat status on demand: your own HP as a number ("HP 4523 of 5100") —
  the way the game itself shows it in the party list, and the way you can
  tell whether a potion will be enough. MP stays a percentage, because its
  maximum has been 10000 for every job since patch 5.0.
- Target HP in steps (a percentage — the game never shows a number for
  enemies), a short tone when you target an enemy.
- Cycling through enemies now announces their **level and HP** as well; a
  sighted player reads the level off the target bar too. That announcement
  and the HP format come from PR #1 by
  [bladestorm360](https://github.com/bladestorm360).
- **Area of effect in the tooltip**: an action's description now also names
  the **shape** of its effect area (circle, cone, line …) — the game's text
  only gives the range and draws the shape. (Contributed by
  [bladestorm360](https://github.com/bladestorm360), PR #2)
- **Cast warning**: **every** spell your current target casts is announced —
  in a boss fight, that is the boss's whole routine. Plus any spell aimed
  **at you**, even from an enemy standing next to it; then their name is
  included. When the spell is aimed at you, the announcement says so
  explicitly. Spells cast at other players stay silent.
- **Shape and size of the area**: appended to the cast announcement whenever
  the spell puts an area on the ground — "cone, 90 degrees, 6 meters",
  "line, 30 meters", "circle on you, 5 meters". That tells you **which way**
  to dodge and how far. Shapes this project has never measured stay silent
  rather than guess.
- **Danger tone for area attacks** (AoE): a tone that holds for as long as you
  stand inside a telegraphed area; it stops the moment you step out. The
  shape (circle, cone, line) comes from the spell's own data. Off by
  default, see the key overview.
- **Sound and volume of the warning can be set** (Settings → Sounds). Four
  sounds to choose from: *Bright* (the previous one), *Soft*, *Deep hum* and
  *Swelling*. Each one plays a short sample the moment you select it, so you
  decide by ear. All four hold for as long as the danger does — even the
  swelling one never breaks off, so it cannot be mistaken for the target
  beacon's strikes.
- **Separate voice for combat warnings**: the cast warning, "you are in it"
  and the escape direction no longer go through the screen reader but through
  a system voice of their own (SAPI). The reason: a screen reader has exactly
  *one* speech queue — a target change, a chat line or your own stop key wipes
  a warning that is halfway through. On its own channel nothing can cut in.
  Voice, speed and volume live under Settings → Sounds, and every choice plays
  a sample sentence right away. If your system offers no speech, or you switch
  the channel off, the warnings go back through the screen reader — none of
  them is ever lost.
- **"You are in it" pre-warning**: if you are already standing in the area
  when the cast begins, the announcement says so — including how much time
  is left ("You are in it, 3 seconds."). Walk into it while the cast is
  running and you get "Careful, you are in it, 2 seconds." Part of the AoE
  warning, so it is switched on and off together with it.
- **Fine target health during levequests**: while a levequest is running, target
  health is announced every 5 percent below 30 instead of only at 25 and 10.
  Capture leves want the enemy *weakened*, not defeated — that window is hard to
  hit with the coarse steps. Can be switched off in the options menu.
- **Duty actions**: some duties show a small extra bar (capture, stun, trigger
  a device) that only appears when it is needed. The game offers it **by mouse
  click only** — it has no entry at all in the keybind dump. The mod plays a
  tone and announces it the moment it appears, and puts it on Shift+F10 and
  Shift+F11; Ctrl+Shift+F8 announces it again.
- **Ability ready**: a tone plus the name as soon as an ability comes off
  cooldown (`/acc cd`).
- HP and MP also as stereo tones (at every 10-percent step the stereo
  position reflects how full the bar is).
- Experience gains and loot are announced and archived in the review log.
- **Loot rolls**: read out the party's open rolls (including the item's
  stats, so need or greed can actually be decided) and jump into the roll
  window with a key to choose there with the numpad.
- **Rested bonus**: "Rested area. Rested bonus is accumulating." when you
  enter one, and its size as a percentage of a level on demand.
- Gathering points (GP) for gatherers on demand.
- Level and missing experience on demand.

### Inventory and gear

- Item slots in the bag, character window and armoury chest are announced
  with name, level and wearability ("Bronze gladius, level 5, equippable"
  / "not equippable, requires level 26"); empty slots say "Empty". The
  name comes from the game's own detail window rather than from the icon,
  so items that share an icon are not confused with one another; an HQ
  piece adds "high quality".
- Shops: level and wearability are appended to every listed item.
- **Stats, not just names**: item level, defence and attributes are part of
  the announcement, and which jobs a piece is for is spoken with **your
  own** classes ("for your classes Paladin, Gladiator") instead of the
  game's list of abbreviations.
- **Warning before selling**: if a piece belongs to a gear set, the
  announcement adds ", in a gear set" as you cycle past it. The game only
  paints that hint as a symbol onto the icon — a text reader never gets it.
- Read all equipped gear at once; equip recommended gear using the game's
  own optimizer.
- Inventory and gil on demand.

### Hotbars

- Read a hotbar: which key triggers which action.
- **Assignment menu**: cycle through all learned actions of your current
  job and place them on any of the 10 hotbars — entirely without a mouse.
  Announcements use the actually bound key (e.g. "bar 2, key Ctrl+3").
- The same menu also places **items**: potions, elixirs and food from your
  bag, with the stack size in the announcement ("Potion, 12").

### Deep dungeons (Palace of the Dead and friends)

This entire section was contributed by
[bladestorm360](https://github.com/bladestorm360) (PR #6).

A deep dungeon is self-contained, and the object browser adapts to it:
instead of sixteen world categories there are five answers.

- **Categories inside**: enemies (revealed traps included — the game itself
  lists them as enemies), allies, treasure, the two cairns and the rooms.
  Allies are offered even when nobody is around — "is anyone with me?" is a
  real question, and its empty answer is real too.
- **Rooms instead of objects**: the contents of a floor are read from the
  game's content director, not from the object table. So a destination no
  longer disappears once you walk far enough away for the game to unload it —
  and rooms can be walked to, not merely named.
- **Room changes** are announced as you enter; a sighted player reads their
  position off the dungeon map continuously.
- **Which dungeon, which floor** on demand (Ctrl+F) — the one number the
  whole run is measured in, and one the game only mentions in passing.
- **Character info**: the window now names its slots with name, description
  and count. It consists almost entirely of icons without text, which is why
  it used to announce nothing but its own title.
- **Floor-wide effects** (cairn effects, traps, the ring bonus) are tracked.
  They are not statuses on your character but live on the director — the
  existing effect tracker could not see them at all.

> The deep dungeon features have **not been verified in-game** yet. Should
> Ctrl+F also trigger the game's "face target" for you, the key can be
> changed in the settings.

### Miscellaneous

- Emote browser: cycle through emotes and perform them.
- Read the hunting log aloud, including each monster's habitat.
- **Fishing**: find fishing spots in the zone and walk to them.
- **Gathering**: find mining and botany nodes; the gathering window is
  read aloud.
- **Mounts**, **grand company shops** and the **character configuration**
  are operable.
- **Exchange windows** (seals, certificates): every row names the item, its
  price including the currency, how many you hold yourself, and the
  description.
- **Currency window**: every row says which currency it is — "49,457 gil",
  "1,652/10,000 Storm Seals". It used to be bare numbers next to an icon.
- **Achievements**: opening the window announces your points and
  certificates ("350 achievement points, 1 achievement certificate"); the
  same figure is available any time by moving onto the icon in the window.
- **Triple Triad**: read the board and your own hand.
- Logging in stays quiet: while the game builds its windows, the automatic
  announcements hold back so they cannot cut each other off.
- **Notifications**: accept incoming invitations (free company, party,
  friend list) with a key — the pop-up can otherwise only be clicked with
  the mouse.
- **Plugin list**: browse the installed Dalamud plugins by keyboard
  (Dalamud's own window cannot be read by a screen reader).
- After every login the plugin saves the game's keybinds as a text file
  on the desktop and warns about conflicts with plugin keys.

## Requirements

- Windows, FINAL FANTASY XIV and [XIVLauncher](https://goatcorp.github.io/)
  with Dalamud.
- **NVDA** as screen reader (via the Tolk library; the required DLLs ship
  with the plugin).
- Optional: the third-party plugin **vnavmesh** for auto-walk and
  mesh-based guidance — the installer offers to download it.

## Installation for blind users (with a screen reader)

There is a graphical installer with a single button. It sets everything up
and keeps the plugin up to date — **without** you having to operate
Dalamud's plugin window (which a screen reader cannot read).

### Step by step

1. Download `FF14AccessibilityInstaller.exe` from the
   [latest release](https://github.com/derbruedi/ff14-accessibility/releases/latest)
   (section "Assets", the link with this file name).
2. Run the downloaded file (Enter or double-click in your Downloads
   folder).
3. Windows SmartScreen may show a warning because the installer is not
   signed. In that dialogue activate the link or button "More info" and
   then the button "Run anyway". Both can be reached with Tab and
   activated with Enter or Space.
4. In the installer window the focus automatically jumps to the button
   "Install or update" ("Installieren oder Aktualisieren"). If not, press
   Tab until that button is announced, then press Enter.
5. Wait for the messages in the status field. At the end a dialogue box
   appears saying the operation is complete. Confirm it with Enter.
6. Start XIVLauncher and log into the game — the plugin is active and
   greets you at login with a spoken version announcement.

### Update

To update, simply run `FF14AccessibilityInstaller.exe` again and activate
the "Install or update" button once more. It overwrites the plugin files,
and the next game start loads the new version.

**The installer also updates itself** (from installer version 1.1 onwards).
When a newer installer version exists, it asks first:

1. A Yes/No prompt appears, including the download size. "Yes" fetches the
   new version, "No" carries on with the current one.
2. On "Yes" it downloads, closes briefly and reopens automatically — the
   file at your own location is replaced, so there is nothing to download
   by hand.
3. After the restart it announces "The installer was updated to version …"
   and continues the installation on its own. Confirming with Enter is all
   it takes.

If the file cannot be replaced (write protection, for example), it says so
and carries on normally.

### What the installer does

- Checks whether **XIVLauncher** is installed and otherwise offers to
  download and start the official setup.
- Copies the plugin files into Dalamud's `devPlugins` folder and enables
  them directly in `dalamudConfig.json` (with a backup copy).
- Offers to download the **vnavmesh** plugin (for auto-walk) from its
  original source. vnavmesh is made by a different author and is **not**
  redistributed by this project.

## Default hotkeys

This list is checked against the actual key bindings in the code — every
key listed here is live. The keys were chosen to be mostly free according
to the game's keybind table; a few deliberately sit on purely visual camera
functions (see below). Ctrl+F1 announces the current help at any time.
Every key can be changed in the settings.

Note: keys are shown with Ctrl/Shift. "Page Up/Down" are the dedicated
navigation keys above the arrow block.

### Finding objects

- **Page Down** — announce and target the next nearby object
- **Page Up** — previous object
- **Ctrl+Page Down** — next object category (NPCs, merchants, enemies,
  allies, players, objects, duties, all duties, quest objectives,
  levequests, FATEs, gathering points, fishing spots, aetherytes,
  waypoints; inside a deep dungeon: only enemies, allies, treasure, cairns,
  rooms instead)
- **Ctrl+Page Up** — previous object category

### Walking and guidance

- **Numpad 3** — auto-walk to the selected target on/off (needs vnavmesh)
- **Ctrl+Numpad 3** — walk guide on/off (audio guidance while walking
  manually, follows the navigation mesh around obstacles)
- **+** — follow your current target continuously, on/off (needs vnavmesh).
  This is the regular plus key, **not** the one on the numpad. On keyboard
  layouts where plus requires Shift, rebind it in the settings
- **Ctrl+Shift+F9** — target beacon on/off
- **Ctrl+Numpad 5** — route preview: hear the path without walking
- **Ctrl+Shift+F1** — walk to coordinates from the clipboard (e.g. copy
  "24.1 21.0", then press the key)
- **Ctrl+Shift+F2** — copy your own map coordinates to the clipboard
- **Numpad 5** — turn once towards where the walk guide is pointing
- **Ctrl+Shift+F6** — record a trail on/off (walk a place the navigation
  mesh does not know once by hand)
- **N** — toggle the facing-direction announcement while turning
- **F** — face the target (game key), **W** — walk (game key)

### Reading and information

- **Ctrl+F1** — help (keys and commands)
- **Ctrl+F2** — announce the active window
- **Ctrl+F10** — read the current menu; with the journal open: read the
  quest
- **Ctrl+F11** — stop speech immediately
- **Ctrl+Delete** — combat status: your HP and MP
- **Delete** — the current target's HP, as a percentage (the game shows it
  to a sighted player as a bar only, never as a number)
- **Ctrl+End** — gathering points (GP) for gatherers
- **Ctrl+L** — level and missing experience
- **Shift+L** — rested area and rested bonus
- **Ctrl+F** — deep dungeon: which dungeon, which floor
- **Ctrl+Shift+F7** — read the task list of whatever is running (levequest,
  duty, FATE): exactly the lines shown at the edge of the screen, with
  counter or remaining time
- **Shift+F9** — open the settings menu (spoken and keyboard-operable)
- **Ctrl+F3** — read the inventory (bag and key items)
- **Shift+F3** — gil
- **Ctrl+F4** — read the hunting log
- **Ctrl+F12** — accept an open notification/invitation

### Combat

- **Ctrl+Shift+F3** — danger tone for area attacks on/off. **Off by
  default**, because the shape detection has not been fully confirmed
  in-game yet — a wrong warning during combat would be worse than none
- **Shift+F7** — read the open loot rolls
- **Shift+F8** — jump into the roll window (the numpad then picks need,
  greed or pass there). Deliberately a separate key: a window grabbing
  focus mid-fight would swallow the numpad while you still need to move

### Gear

- **Ctrl+F6** — read equipped gear (with item level and stats)
- **Ctrl+F7** — equip recommended gear (the game's own optimizer)
- **Ctrl+F8** — random appearance (character creation only)

### Filling hotbars

- **Ctrl+F9** — read the first hotbar (what sits on keys 1 to 0)
- **Ctrl+Numpad 0** — open or close the assignment menu

While the assignment menu is open the numpad drives it, and those keys are
kept away from the game so your character does not run off:

- **Numpad 8 / 2** — browse the list
- **Numpad 4 / 6** — switch between **actions** and **items** (potions,
  elixirs, food from your bag)
- **Numpad 0** — select; then choose the target key and press Numpad 0
  again to place it
- **Numpad decimal** — one step back, or close the menu

### Reading back chat

- **Alt+Page Up** / **Alt+Page Down** — previous / next category
  (dialogues, say, shout, party, alliance, tell, free company, system,
  loot); announced with the category name and its message count
- **Shift+Page Up** / **Shift+Page Down** — step to the older / newer
  message inside the selected category ("3 of 12: …")
- **Shift+Home** / **Shift+End** — jump to the start / end of the category
- **Alt+Home** / **Alt+End** — switch the game's chat tab (the game has no
  key for this — a sighted player clicks the tab)
- **Enter** — reply to the message you just heard, in its own channel

### Emotes

- **Shift+F4** / **Shift+F5** — previous / next emote
- **Shift+F6** — perform the chosen emote

### Triple Triad

- **Ctrl+Shift+F4** — read the board
- **Ctrl+Shift+F5** — read your own hand

### Plugin list

- **Shift+F1** / **Shift+F2** — announce the next / previous installed
  plugin
- **Shift+F12** — open the settings of the selected plugin

### Diagnostics

- **Ctrl+F5** — save a UI dump of the current window to the desktop
  (helps with bug reports)

### Overlaps with game keys

A few plugin keys sit on functions the game also binds. This is
intentional; the number of overlaps is announced when you log in:

- **Page Up / Page Down** are also camera zoom
- **Ctrl+End** is also "save camera preset"
- **Numpad 5** is also "focus camera on target"; the plugin keeps this key
  away from the game so the camera does not jump as well

Those functions are purely visual and therefore have no consequence for
blind play. If the announced number goes up compared to what you are used
to, check `FFXIV_Keybinds.txt` on your desktop: in that case a plugin key
overlaps with a real game function. The one open question there is
**Ctrl+F** (deep dungeon): the keybind dump lists Ctrl+F as free, but bare
**F** is "face target" — whether the game turns you anyway on Ctrl+F has
not been measured in-game yet.

## Chat commands

Many functions are also available as commands:

- `/acc help` — announce the help
- `/acc nav` — announce direction and distance to the target
- `/acc set` — track the current target
- `/acc clear` — clear the tracked target
- `/acc near` — list nearby objects
- `/acc status` — announce HP and MP
- `/acc ui` — read the current menu
- `/acc win` — announce the active window
- `/acc keys` — save the game's keybinds to the desktop
- `/acc stop` — stop speech
- `/acc fish` — announce fishing spots in this zone
- `/acc fishhere` — remember your current spot as a casting position
- `/acc gather` — announce gathering nodes in this zone
- `/acc gathergo` — walk to the nearest gathering node
- `/acc trails` — list the trails recorded in this zone
- `/acc cd` (or `/acc cooldowns`) — toggle the "ability ready" announcement
- `/acc soundtest` — play the plugin's tones for reference
- `/acc lang de|en|auto` — switch the language of the plugin's announcements
- `/acc dump <window name>` — save a window's structure to the desktop

## Language

The plugin's own announcements are available in **English and German**.
Without a setting the language follows Windows; `/acc lang en`,
`/acc lang de` or `/acc lang auto` switches it at any time. Game texts
(dialogues, menus, item names) are always read in whatever language your
game client uses. Development and testing happen primarily with the German
client.

## Contributors

Six of this plugin's larger features come from
**[bladestorm360](https://github.com/bladestorm360)**:

- **PR #1** — level and HP while cycling through enemies; your own HP back
  as a number instead of a percentage
- **PR #2** — the shape of an action's effect area in its tooltip (circle,
  cone, line …)
- **PR #3** — the object categories **allies** and **duties**
- **PR #4** — the **appearance** step of character creation
- **PR #5** — the second chat system whose buffers follow the game's own
  tabs and filters
- **PR #6** — the **deep dungeons** (rooms, treasure, cairns, character
  info, floor-wide effects)

Thank you.

## Notes

- This plugin runs on **Dalamud/XIVLauncher**, which is outside Square
  Enix's official terms of service. Use at your own risk.
- **vnavmesh** is an independent third-party plugin
  ([github.com/awgil/ffxiv_navmesh](https://github.com/awgil/ffxiv_navmesh))
  and is only linked/downloaded here, not redistributed.

## Licence

This project is released under the **GNU Affero General Public License,
version 3** (`LICENSE`) — the same licence as Dalamud itself and as goatcorp's
official plugin template. You may use, modify and redistribute the plugin;
anyone distributing a modified version, or offering it over a network, must
publish its source code as well.

Third-party software shipped with the plugin is listed in
`THIRD-PARTY-NOTICES.md`: **Tolk** (LGPL-3.0), the **NVDA Controller Client**
(LGPL-2.1) and **NAudio** (MIT). That file is also inside the downloaded
archive and must stay with it on redistribution.

## For developers

- Plugin source code: `FF14Accessibility/`
- Installer source code: `Installer/`
- Custom plugin repository (optional path for sighted helpers): `repo.json`
- Project status and test log: `STATUS.md`
- Verified game internals: `docs/game-api.md`
