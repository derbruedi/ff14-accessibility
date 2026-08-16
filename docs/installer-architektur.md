# Architecture concept: graphical installer for FF14Accessibility

Status: concept, no implementation. Basis for deciding what gets built next.

## 0. Starting point (current state verified)

The existing `Installer/` (`FF14AccessibilityInstaller.csproj`, `Program.cs`)
is a **pure console application** (.NET 8, self-contained single-file win-x64
EXE, about 68 MB including the runtime). No WPF/WinForms code present.

What it can already do today (reusable):
- Detects whether `%AppData%\XIVLauncher` exists; otherwise offers to download
  the official XIVLauncher setup (`Process.Start`, `UseShellExecute`).
- Copies bundled plugin files from a `plugin` subfolder next to the EXE into
  `%AppData%\XIVLauncher\devPlugins\FF14Accessibility`.
- Offers a vnavmesh download (currently **hard-pinned to version 1.2.3.8**,
  URL `https://puni.sh/api/plugins/download/48/vnavmesh/versions/1.2.3.8/...`)
  and unpacks it into `devPlugins\vnavmesh`.
- Patches `dalamudConfig.json` directly: enters DLL paths into
  `DevPluginLoadLocations` and sets `IsEnabled=true` in the
  `DefaultProfile.Plugins` entries. Makes a `.bak-installer` backup
  beforehand, writes without a BOM (important, see below).
- Uses `Newtonsoft.Json` with `JObject`, because Dalamud itself uses
  `TypeNameHandling.All` (`$type` fields) — that has to survive the round trip.
- No update check against GitHub releases built in (plugin files must already
  be in the `plugin` folder next to the EXE — the installer itself currently
  downloads **nothing** from its own repo).
- No self-update.

That is a solid functional base (devPlugins copy + config patch + XIVLauncher
detection demonstrably work). What is missing: a real GUI, an update check
against GitHub releases for **both** plugins, and a more robust vnavmesh
connection.

Verified: `https://puni.sh/api/repository/veyn` currently returns a valid JSON
array with vnavmesh in it (`InternalName: "vnavmesh"`,
`DownloadLinkInstall`/`DownloadLinkUpdate` point at
`puni.sh/api/plugins/download/48/vnavmesh/versions/<version>/...`). That is the
official veyn/xan_0 distribution, not a fake.

---

## 1. Framework choice: WinForms instead of WPF

**Recommendation: WinForms**, self-contained single-file .NET 8 EXE.

Reasoning:
- WinForms standard controls (`Label`, `Button`, `ProgressBar`, `ListBox`)
  automatically set the classic Win32/UIA properties (Name, Role, LabelledBy).
  NVDA/JAWS read them correctly with no extra effort — that has been mature for
  decades.
- WPF does have UIA support too, but considerably more places where you can
  break it (custom templates, styles without `AutomationProperties.Name`,
  custom controls without an `AutomationPeer`). For a small, functional tool the
  extra effort is not justified.
- Tab order (`TabIndex`), focus handling and standard dialogs (`MessageBox`)
  are trivial to get right in WinForms.
- Team context: the existing project is already .NET/C#, no prior UI knowledge
  of WPF/XAML needed — WinForms code-behind is closer to the existing console
  style (imperative, little boilerplate).

WPF as an alternative: only worthwhile if a more extensive/visually demanding
UI is wanted later. For "one window with a progress display, a log list and a
few buttons" it is overkill and increases the risk of screen-reader-unfriendly
spots.

**Self-contained single-file EXE:** yes, keep it as in the existing installer.
The target audience has no technical knowledge — "download the EXE,
double-click" has to work without installing a .NET runtime. The size
(~65-70 MB) is uncritical; this is a one-off download for an accessibility
tool, not a repeated process. `PublishTrimmed` would cut the size, but trimming
is risky with libraries that use reflection (Newtonsoft.Json) — not recommended
without extensive testing.

### Screen reader requirements in concrete terms (WinForms)

- Every input/button control needs `Text` or `AccessibleName` set.
- `TabIndex` set consistently and logically (0, 1, 2, … in reading order).
- Progress: **no** bare `ProgressBar` percentage update without a text
  counterpart. An accompanying `Label` with `AccessibleRole = StatusText` and a
  live text update ("Downloading plugin... 40 %") — on a text change WinForms
  labels automatically trigger `LiveRegion`-like behaviour through the standard
  automation, if focus is there or you actively update `Label.AccessibleName`.
  Safer: additionally write the same messages into a focusable
  `ListBox`/`TextBox` (read-only, multiline) that the user can step through with
  the arrow keys — that is the most robust solution, because it does not depend
  on live-region timing.
- No owner-draw controls, no bare GDI+ drawing without a text alternative.
- Order: the window opens with focus on the first meaningful element (e.g. the
  "Install" button or the status list), not on the window itself.
- Modal dialogs (`MessageBox.Show`) are already screen-reader-friendly
  (standard Win32) — for yes/no questions (e.g. "set up vnavmesh now?") that is
  enough, no custom dialog needed.

---

## 2. "Everything needed" – XIVLauncher, Dalamud, vnavmesh

### 2.1 XIVLauncher/Dalamud detection

As in the existing installer: check for `%AppData%\XIVLauncher` (folder), or
more precisely for `%AppData%\XIVLauncher\dalamudConfig.json` as proof that
Dalamud has run at least once.

**XIVLauncher missing entirely:** show a note and offer a download link
(`https://github.com/goatcorp/FFXIVQuickLauncher/releases/latest/download/Setup.exe`),
do NOT install it automatically/run it silently. Reasoning:
- The XIVLauncher setup is an interactive wizard (login, game path selection,
  Dalamud opt-in) — that cannot sensibly be run unattended without processing
  the user's login data (a security/trust risk a third-party tool should not
  take on).
- The existing approach (download the setup, launch the GUI setup, ask the user
  to run it again afterwards) is the right, pragmatic middle ground.
- Important for the GUI version: state clearly that after the XIVLauncher setup
  the user has to **log in once, enable Dalamud, and start the game once**
  before the installer can continue (because `dalamudConfig.json` is only
  created then, and the plugin profiles inside it are only laid down then).

**Dalamud never started** (XIVLauncher present, but `dalamudConfig.json`
missing): same problem, same solution — a text note, no automatic
intervention.

### 2.2 vnavmesh distribution and robustness

Verified via `puni.sh/api/repository/veyn`: vnavmesh is officially distributed
through veyn/xan_0's **puni.sh third-party repo**, with versioned download
links (`.../versions/<version>/install/latest.zip`).

**Two ways for the installer to "set up vnavmesh automatically":**

**Route A — devPlugin copy (as in the existing installer today):**
Direct download of the ZIP from puni.sh, unpack into `devPlugins\vnavmesh`,
entry in `DevPluginLoadLocations` + `IsEnabled=true` in the profile.
- Advantage: works just as robustly/directly as for our own plugin, no
  dependency on Dalamud's internal update logic for third-party repos.
- Disadvantage: the installer has to know for itself which vnavmesh version is
  current (currently hard-pinned in code to `1.2.3.8`) — otherwise the user
  never gets vnavmesh updates automatically, unless they run the
  FF14Accessibility installer again AND the installer actively checks puni.sh
  for the latest version while doing so.
- If vnavmesh is never managed through Dalamud's own mechanism, the user also
  does not see it as a "managed" plugin in the normal `/xlplugins` window —
  cosmetic, but worth mentioning.

**Route B — third-party repo entry (`ThirdRepoList` in dalamudConfig.json) +
Dalamud downloads/updates vnavmesh itself:**
Entry `https://puni.sh/api/repository/veyn` in `config.ThirdRepoList` (a list
of objects
`{"$type": "Dalamud.Configuration.ThirdPartyRepoSettings, Dalamud", "Url": "...", "IsEnabled": true}`
— verified against the Dalamud source, where it is
`List<ThirdPartyRepoSettings> ThirdRepoList`). Dalamud then pulls all future
updates for vnavmesh itself.
- Advantage: after that, no extra installer run is needed for vnavmesh updates
  — Dalamud's own auto-update mechanism (`AutoUpdateBehavior`, "notify only" by
  default, can be set to "update all" on the Dalamud side) takes over.
- Disadvantage: the actual installation of a plugin from a third-party repo
  still has to be clicked once in the Dalamud plugin installer (ImGui window,
  `/xlplugins`) — and there is **no** documented, official way to trigger that
  programmatically from outside (from the installer process). That was exactly
  the original reason the existing project chose the devPlugin route (ImGui is
  not operable for blind users).
- `ThirdRepoSpeedbumpDismissed` (a bool in the config) also has to be set to
  `true`, otherwise Dalamud shows a warning dialog the first time the
  experimental tab is opened (again ImGui, again not screen-reader operable) —
  the installer could set that too, but it does not change the fact that the
  initial installation itself would still be an ImGui click.

**Recommendation: keep route A (devPlugin copy) for vnavmesh, but
version-dynamic instead of hard-pinned.** Reasoning: it fits the core
requirement "no ImGui click needed". For that the installer fetches
`https://puni.sh/api/repository/veyn` (works as verified above), looks for the
entry with `InternalName == "vnavmesh"`, reads out
`DownloadLinkInstall`/`DownloadLinkUpdate` and `AssemblyVersion` and downloads
whichever version is newest through that — the same update logic as for our own
plugin, just a different source. That gets rid of the hard-coded version number.

**Additionally** the installer can optionally (not mandatory, but a sensible
supplement) also set the third-party repo entry in `ThirdRepoList`, so that
sighted helpers/advanced users can see and manage vnavmesh through `/xlplugins`
in the normal way — that is purely a bonus, and does not replace route A as the
primary, accessibly usable path.

---

## 3. Our own plugin: devPlugin copy vs. custom repo

Currently: `repo.json` (in the project root) already exists as a custom repo
manifest, pointing via `DownloadLinkInstall`/`DownloadLinkUpdate` at
`.../releases/latest/download/latest.zip`. On a release build DalamudPackager
already produces a manifest + `latest.zip` in the output itself (verified via
the package README) — that is intended for a PR into an official repo, but is
equally reusable as our own custom repo JSON.

**Analysis:**
- A custom repo (route B, as described above for vnavmesh) would have the same
  showstopper for our **own** plugin: the initial installation of a new plugin
  from a third-party repo requires a click in the ImGui plugin installer. For
  the target audience (blind first-time users with no technical knowledge) that
  is not acceptable — which is exactly why the devPlugin approach already
  exists in the project.
- For **updates** of an already registered custom-repo plugin, on the other
  hand, Dalamud's auto-update is unobtrusive and robust, provided
  `AutoUpdateBehavior` is set accordingly — but the initial installation remains
  the problem.

**Recommendation: the devPlugin copy remains the primary route driven by the
installer — for both first-time AND update installation, consistent with
vnavmesh (route A).** The `repo.json` in the project root is **additionally**
maintained and kept current (it costs nothing, DalamudPackager generates the
manifest automatically anyway) — as an option for sighted helpers/power users
who would rather add it through `/xlplugins` in the normal way, and as a
fallback/documentation. The installer itself does not necessarily need to read
this repo JSON — it uses the GitHub releases API directly instead (see section
4), which is independent of Dalamud's load cycle and works with the same logic
for our own plugin AND vnavmesh (via puni.sh).

---

## 4. The installer's update mechanics

### 4.1 Version check

- **Our own plugin:** GitHub releases API,
  `GET https://api.github.com/repos/derbruedi/ff14-accessibility/releases/latest`
  (verified by a live call: returns `tag_name` "v4.61" and the asset
  `FF14Accessibility-v4.61.0.zip` with a `browser_download_url`). No auth token
  needed for public repos (rate limit without a token: 60 requests/hour per IP —
  enough for a tool you start occasionally).
- **vnavmesh:** `GET https://puni.sh/api/repository/veyn`, filter for the entry
  with `InternalName == "vnavmesh"`, use `AssemblyVersion` +
  `DownloadLinkInstall`/`DownloadLinkUpdate`.

Version comparison: for our own plugin a simple string/`Version` comparison
between the locally installed `AssemblyVersion` (from the already copied
`FF14Accessibility.json` in devPlugins, if present) and the release's
`tag_name`/asset name is enough. For vnavmesh, correspondingly with the
`AssemblyVersion` from the repository JSON. On a first installation (no local
version present) the newest version is installed directly without a comparison
— **that automatically also covers point 4 of the brief** ("a first
installation always downloads the newest release, one code path"): the sequence
is identical in both cases ("fetch newest version, compare with the locally
installed one (if present), install on a difference or if missing") — there is
no separate first-install code path, only a condition "local version does not
exist or is older".

### 4.2 Download + unpack

Analogous to the existing `TryDownload`/`ZipFile.ExtractToDirectory` pattern:
download the ZIP from `browser_download_url` (GitHub) or `DownloadLinkInstall`
(puni.sh), unpack into a temp folder, then copy (overwrite) the relevant files
specifically (DLL, `.json` manifest, `Tolk.dll`, `nvdaControllerClient64.dll`,
`NAudio*.dll`) into `devPlugins\FF14Accessibility` or `devPlugins\vnavmesh` —
do not blindly unpack the whole ZIP content into devPlugins, so that no legacy
cruft (e.g. files removed in a previous version) is left lying around. First
delete or specifically overwrite the existing target folder content of the known
file types (`*.dll`, `*.json`, `*.pdb`).

### 4.3 Self-update of the installer

> **ADDENDUM 2026-07-18 (installer 1.1.0): implemented.** The user wanted to
> get rid of the manual step "download the new EXE yourself". The note text
> recommended below was also dead code: `CheckInstallerUpdateHint` read the
> version out of the asset NAME by regex, and the asset is called
> `FF14AccessibilityInstaller.exe` with no version in it — the regex never
> matched.
>
> Implemented sequence (`InstallerService.TrySelfUpdateAsync` +
> `SelfUpdate.cs`):
> 1. On startup, read the release asset `installer.json`
>    (`{ InstallerVersion, AssetName, Sha256 }`). If it is missing (older
>    releases), the check is skipped silently. The version source is
>    deliberately this manifest and NOT the file name, so that the download link
>    and the instructions in the README stay stable.
> 2. If the version is higher: ask via MessageBox, including the download size.
>    On "No" everything continues as normal.
> 3. Download into `%TEMP%`, SHA256 comparison against the manifest (if the hash
>    is missing, it is only logged), start the new EXE with
>    `--apply-update "<target path>" <PID>`, then the old instance exits.
> 4. Phase 2 (the new EXE from `%TEMP%`): waits for the old PID to end, copies
>    itself over the original EXE (20 attempts at 500 ms each, because Windows
>    keeps the file locked briefly) and starts it with `--updated`.
> 5. The newly started original EXE skips the language dialog, reports the
>    update via a dialog and runs the installation automatically. On startup it
>    also clears out old `FF14AccInstaller_*.exe` files from `%TEMP%` (about
>    160 MB each).
>
> If the replacement fails (write protection, missing rights), phase 2 reports
> that honestly and carries on from `%TEMP%` — the installation then still
> succeeds, only the user's file stays old.
>
> A pitfall, guarded against in `ParseVersionLoose`: `Version` treats unset
> components as -1, so "1.1.0" counts as SMALLER than "1.1.0.0". A three-part
> value in `installer.json` would have silently prevented the update from ever
> triggering; that is why the parser now always pads to four components.
>
> Verified end to end on 2026-07-18 with an artificial 1.0.0 build against the
> real release v4.91: detection, dialog, download (~20 s), hash check,
> replacement of the original file, restart, auto-installation, and "the
> installer is up to date" on the following run (no endless loop).
>
> The original section stays below as decision history:

**Recommendation (superseded): no separate self-update feature for the first
version.** Reasoning:
- The installer changes far less often than the plugin (pure infrastructure:
  download + copy + config patch). An update is only needed if, say, the Dalamud
  config format changes or a new security hole is found in the installer itself
  — both rare events.
- An installer that overwrites itself (replacing the running EXE) is not
  trivial on Windows (the file is locked at runtime) and would need a restart
  trick (e.g. start a copy in temp that replaces the original EXE) —
  considerably more complexity for a rare case.
- A simple alternative: on startup the installer additionally checks its own
  version against the newest installer version in the repo (a dedicated release
  asset or a tag convention) and, if needed, only shows a text note with a link
  ("A newer installer version is available: <link>. Please download it when you
  get a chance."), WITHOUT updating automatically. That covers the need ("the
  user should find out") without the complexity of a real self-updater. Can be
  added later if needed.

---

## 5. Flow sketch

### 5.1 First installation (the user has nothing yet)

1. The installer starts, the window opens with focus on the status area.
   Announcement/text: "FF14 Accessibility Installer. Checking XIVLauncher..."
2. XIVLauncher not found → text + dialog: "XIVLauncher was not found. Download
   it and start the setup now?" (yes/no buttons, clearly labelled, a standard
   `MessageBox` or custom labelled buttons).
3. On "Yes": download progress as text ("Downloading XIVLauncher setup...
   40 %"), the setup is started, the installer shows a closing text: "Please
   follow the XIVLauncher wizard, log in, enable Dalamud in the settings and
   start the game once. Then run this installer again." The window stays open
   until the user closes it (no automatic exit without an announcement).
4. **Second run** (after starting the game): XIVLauncher +
   `dalamudConfig.json` found. Text: "XIVLauncher found. Checking the newest
   version of FF14 Accessibility..."
5. GitHub API query, no local version present → direct download of the newest
   version. Progress text with a percentage.
6. Unpack, copy into devPlugins. Text: "Plugin installed (version 4.61)."
7. vnavmesh query (puni.sh) → no local version present → yes/no dialog "Set up
   vnavmesh for auto-walk now?" → on yes: download + copy in the same way,
   progress text.
8. Patch `dalamudConfig.json` (one run is enough, verified by decompilation
   against Dalamud 15.0.2.2): `DevMode=true` (without it Dalamud does not scan
   the DevPluginLoadLocations at all), DevPluginLoadLocations entries, one
   `DevPluginSettings` entry per plugin (key = DLL path, `StartOnBoot=true`,
   fixed `WorkingPluginId`), plus a DefaultProfile entry with the same
   `WorkingPluginId` and `IsEnabled=true`. Dalamud only generates a GUID of its
   own when the `DevPluginSettings` entry is missing — pre-filled entries are
   taken over unchanged. A backup is created. Closing text: "Done. Start FINAL
   FANTASY XIV through XIVLauncher — the plugin announces itself at login with a
   spoken message." The window has a focusable "Close" button (not the bare
   press-Enter requirement of the console version).
   (History: until July 2026 the installer only set existing profile entries to
   `IsEnabled=true` and required a second run after a game start for that. That
   could never work, because without `DevMode=true` no profile entries were ever
   created.)

### 5.2 Update run (everything already installed, the routine case)

1. The installer starts. Text: "Checking for updates..."
2. GitHub API: the local version (from the devPlugins manifest) compared with
   `latest`.
3. No update needed → text: "FF14 Accessibility is up to date (version 4.61)."
   — vnavmesh checked likewise, also up to date.
4. If an update is available → automatic download + installation without asking
   (plugin updates are low-risk and match the user's wish to "always keep it
   current"; only for vnavmesh, if it was declined last time, keep asking rather
   than forcing it). Progress text per step.
5. Closing text with the version number(s) and the note "Restart the game if it
   is currently running, so that the new version gets loaded."

All texts additionally appear in the focusable log list (navigable with the
arrow keys), so the user can read/hear earlier messages again if the screen
reader missed a line.

---

## 6. Open points / recommendations summarised

| Topic | Recommendation |
|---|---|
| UI framework | WinForms (not WPF) — best UIA support out of the box |
| Deployment | .NET 8 self-contained single-file EXE, as before |
| XIVLauncher missing | Note + official setup download, no silent install |
| vnavmesh setup | devPlugin copy (route A), version pulled dynamically from puni.sh instead of hard-pinned; optionally also a ThirdRepoList entry as a convenience for sighted helpers |
| Our own plugin | devPlugin copy remains the primary route (first install + update); `repo.json`/custom repo still maintained as an extra option, not as an installer source |
| Update source | GitHub releases API (`/releases/latest`) for our own plugin, the puni.sh repository JSON for vnavmesh |
| First install vs. update | one code path ("fetch the newest version, compare with the local one, install if needed/missing") |
| Self-update of the installer | not automated for now; only a version note with a link |

## 7. Not part of this concept

No implementation, no code, no project file changes. The next step (after this
concept is approved) would be creating a WinForms project that extracts the
existing `Program.cs` logic into services (download/update logic, Dalamud config
patch) and lays a thin GUI layer over it.
