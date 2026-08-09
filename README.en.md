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
  turned off individually (say, shout, party, alliance, tell, free
  company, system).
- **Writing**: opening the chat input announces the active channel ("Chat
  input, Say"), and so does switching channels while typing. Every typed
  character is spoken, as are deletions — because a screen reader cannot
  read the game's own input field.
- **Catching up**: a history browser with eight separate categories
  (dialogues, say, shout, party, alliance, tell, free company, system),
  with no message limit — the whole session is kept. This lets you review
  what you missed at your own pace without disturbing the live chat.

### Navigation and walking

- **Object browser**: cycle through nearby objects with a single key
  (NPCs, enemies, players, gathering points, aetherytes, quest
  objectives, map waypoints such as zone exits). Announces name, kind,
  distance and direction; the object is targeted at the same time.
- **Audio beacon**: a stereo tone indicates the direction to the target
  (panning and pitch), the volume follows the distance.
- **Walk guide**: guided manual walking along the navigation mesh, around
  obstacles — with waypoint tones, direction announcements relative to
  where you are facing, and an arrival tone.
- **Auto-walk**: walk to the target automatically (requires the
  third-party plugin vnavmesh), with route preview, progress
  announcements and an honest message when no path exists. When the
  route stops just short of the destination, the last few metres are
  walked as well, provided there is solid ground all the way.
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

- Combat status on demand: your own HP and MP as a percentage — exactly what
  a sighted player reads off the bar.
- Target HP in steps, a short tone when you target an enemy.
- **Cast warning**: when an enemy casts a spell **at you** it is announced
  — from any nearby enemy, not just the one you have targeted. If it is
  someone other than your target, their name is included. Spells cast at
  other players stay silent.
- **Danger tone for area attacks** (AoE): a pulsing tone for as long as you
  stand inside a telegraphed area; it stops the moment you step out. The
  shape (circle, cone, line) comes from the spell's own data. Off by
  default, see the key overview.
- **Ability ready**: a tone plus the name as soon as an ability comes off
  cooldown (`/acc cd`).
- HP and MP also as stereo tones (at every 10-percent step the stereo
  position reflects how full the bar is).
- Experience gains and loot are announced and archived in the review log.
- Gathering points (GP) for gatherers on demand.

### Inventory and gear

- Item slots in the bag, character window and armoury chest are announced
  with name, level and wearability ("Bronze gladius, level 5, equippable"
  / "not equippable, requires level 26"); empty slots say "Empty".
- Shops: level and wearability are appended to every listed item.
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

### Miscellaneous

- Emote browser: cycle through emotes and perform them.
- Read the hunting log aloud, including each monster's habitat.
- **Fishing**: find fishing spots in the zone and walk to them.
- **Gathering**: find mining and botany nodes; the gathering window is
  read aloud.
- **Mounts**, **grand company shops** and the **character configuration**
  are operable.
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
to the game's keybind table; three deliberately sit on purely visual camera
functions (see below). Ctrl+F1 announces the current help at any time.
Every key can be changed in the settings.

Note: keys are shown with Ctrl/Shift. "Page Up/Down" are the dedicated
navigation keys above the arrow block.

### Finding objects

- **Page Down** — announce and target the next nearby object
- **Page Up** — previous object
- **Ctrl+Page Down** — next object category (NPCs, merchants, enemies,
  quest objectives, levequests, FATEs, gathering points, aetherytes,
  waypoints)
- **Ctrl+Page Up** — previous object category

### Walking and guidance

- **Numpad 3** — auto-walk to the selected target on/off (needs vnavmesh)
- **Ctrl+Numpad 3** — walk guide on/off (audio guidance while walking
  manually, follows the navigation mesh around obstacles)
- **+** — follow your current target continuously, on/off (needs vnavmesh).
  This is the regular plus key, **not** the one on the numpad. On keyboard
  layouts where plus requires Shift, rebind it in the settings
- **Ctrl+Numpad 5** — route preview: hear the path without walking
- **Ctrl+Shift+F1** — walk to coordinates from the clipboard (e.g. copy
  "24.1 21.0", then press the key)
- **Ctrl+Shift+F2** — copy your own map coordinates to the clipboard
- **N** — toggle the facing-direction announcement while turning
- **F** — face the target (game key), **W** — walk (game key)

### Reading and information

- **Ctrl+F1** — help (keys and commands)
- **Ctrl+F2** — announce the active window
- **Ctrl+F10** — read the current menu; with the journal open: read the
  quest
- **Ctrl+F11** — stop speech immediately
- **Ctrl+Delete** — combat status: your HP and MP
- **Ctrl+End** — gathering points (GP) for gatherers
- **Ctrl+L** — level and missing experience
- **Ctrl+F3** — read the inventory (bag and key items)
- **Shift+F3** — gil
- **Ctrl+F4** — read the hunting log
- **Ctrl+F12** — accept an open notification/invitation

### Combat

- **Ctrl+Shift+F3** — danger tone for area attacks on/off. **Off by
  default**, because the shape detection has not been fully confirmed
  in-game yet — a wrong warning during combat would be worse than none

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

Three plugin keys sit on functions the game also binds. This is
intentional, and it is reported as "3 key conflicts" when you log in:

- **Page Up / Page Down** are also camera zoom
- **Ctrl+End** is also "save camera preset"

All three are purely visual and therefore have no consequence for blind
play. If the plugin reports a number **other** than three, check
`FFXIV_Keybinds.txt` on your desktop: in that case a plugin key overlaps
with a real game function.

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

## Notes

- This plugin runs on **Dalamud/XIVLauncher**, which is outside Square
  Enix's official terms of service. Use at your own risk.
- **vnavmesh** is an independent third-party plugin
  ([github.com/awgil/ffxiv_navmesh](https://github.com/awgil/ffxiv_navmesh))
  and is only linked/downloaded here, not redistributed.

## For developers

- Plugin source code: `FF14Accessibility/`
- Installer source code: `Installer/`
- Custom plugin repository (optional path for sighted helpers): `repo.json`
- Project status and test log: `STATUS.md`
- Verified game internals: `docs/game-api.md`
