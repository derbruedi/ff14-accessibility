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
- Chat is read aloud (each channel can be turned off: say, shout, party,
  alliance, tell, free company, system).
- Every announcement is also sent to the **braille display**.

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
  announcements and an honest message when no path exists.
- **Route preview**: hear the route without walking
  ("Path to aetheryte, 62 meters: 25 meters north, then …").
- Target-change announcements for the game's own targeting keys
  (Tab, F1–F12).

### Combat

- Combat status on demand: your own HP and MP.
- Target HP in steps, announcement when the target starts casting,
  a short tone when you target an enemy.

### Inventory and gear

- Item slots in the bag, character window and armoury chest are announced
  with name, level and wearability ("Bronze gladius, level 5, equippable"
  / "not equippable, requires level 26"); empty slots say "Empty".
- Shops: level and wearability are appended to every listed item.
- Read all equipped gear at once; equip recommended gear using the game's
  own optimizer.
- Inventory and gil on demand.

### Hotbars

- Read the selected hotbar: which key triggers which skill.
- **Skill browser**: cycle through all learned skills of your current job
  and place them on any of the 10 hotbars — entirely without a mouse.
  Announcements use the actually bound key (e.g. "bar 2, key Ctrl+3").

### Miscellaneous

- Emote browser: cycle through emotes and perform them.
- Read the hunting log aloud, including each monster's habitat.
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

### What the installer does

- Checks whether **XIVLauncher** is installed and otherwise offers to
  download and start the official setup.
- Copies the plugin files into Dalamud's `devPlugins` folder and enables
  them directly in `dalamudConfig.json` (with a backup copy).
- Offers to download the **vnavmesh** plugin (for auto-walk) from its
  original source. vnavmesh is made by a different author and is **not**
  redistributed by this project.

## Default hotkeys

The keys were chosen to be free according to the game's keybind table.
Ctrl+F1 announces the current help at any time.

Note: "Umschalt" is the German word for Shift, "Strg" for Ctrl — the lists
below use Ctrl/Shift.

### Finding objects

- **N** — announce and target the next nearby object
- **Shift+N** — previous object
- **Ctrl+N** — next object category (e.g. NPCs, enemies, quest
  objectives, waypoints)
- **Ctrl+Shift+N** — previous object category

### Walking and guidance

- **Numpad 3** — auto-walk to the selected target on/off (needs vnavmesh)
- **Ctrl+Numpad 3** — walk guide on/off (audio guidance while walking
  manually, follows the navigation mesh around obstacles)
- **Ctrl+Numpad 5** — route preview: hear the path without walking
- **F** — face the target (game key), **W** — walk (game key)

### Reading and information

- **Ctrl+F1** — help (keys and commands)
- **Ctrl+F2** — announce the active window
- **Ctrl+F10** — read the current menu; with the journal open: read the
  quest
- **Ctrl+F11** — stop speech immediately
- **Ctrl+H** — combat status: your HP and MP
- **Ctrl+L** — level and missing experience
- **Ctrl+F3** — read the inventory (bag and key items)
- **Shift+F3** — gil
- **Ctrl+F4** — read the hunting log

### Gear

- **Ctrl+F6** — read equipped gear (with item level)
- **Ctrl+F7** — equip recommended gear (the game's own optimizer)

### Hotbars and skill browser

- **Ctrl+F9** — read the selected hotbar
- **Shift+F7** / **Shift+F8** — skill browser: previous / next learned
  skill
- **Shift+F11** — cycle the target bar (bar 1 to 10)
- **Shift+F9** — cycle the target slot on the bar (announces what is on it)
- **Shift+F10** — place the chosen skill on the target slot

### Emotes

- **Shift+F4** / **Shift+F5** — previous / next emote
- **Shift+F6** — perform the chosen emote

### Diagnostics

- **Ctrl+F5** — save a UI dump of the current window to the desktop
  (helps with bug reports)

## Chat commands

Most key functions are also available as commands:

- `/acc nav` — announce direction and distance to the target
- `/acc set` — track the current target
- `/acc clear` — clear the tracked target
- `/acc near` — list nearby objects
- `/acc status` — announce HP and MP
- `/acc ui` — read the current menu
- `/acc win` — announce the active window
- `/acc keys` — save the game's keybinds to the desktop
- `/acc stop` — stop speech

## Language

The plugin is developed and tested with the **German game client**; the
plugin's own announcements are currently mostly German (some basic
announcements follow the Windows display language). Game texts (dialogues,
menus) are read in whatever language your game client uses.

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
