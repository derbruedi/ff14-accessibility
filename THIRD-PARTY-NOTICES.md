# Third-party notices

FF14Accessibility is distributed under the GNU Affero General Public License,
version 3 (see `LICENSE`). It ships with, and builds against, software written
by other people. This file lists that software, its licence, and where to get
its source code.

If you redistribute FF14Accessibility, this file must travel with it.

---

## Shipped inside the plugin archive

### XIV Rus game descriptions

- Embedded resource: `FF14Accessibility.Resources.RussianDescriptions.json.gz`
- Source: <https://github.com/xivrus/xiv_ru_weblate>, revision
  `6a2733af6df0f86b133bcbf61c553dfacf80ec2d`.
- Contains FINAL FANTASY XIV text and community Russian translations.
  (C) SQUARE ENIX CO., LTD. All Rights Reserved. The source project identifies
  this material as used under the FINAL FANTASY XIV Material Usage License;
  it is not relicensed as AGPL by inclusion in this plugin.
- Local translation additions and corrections are documented in
  `tools/russian-descriptions/local-game-overrides.json` and `overrides.json`.
  These additions have not been represented as approved by the XIV Rus team.
- Generation instructions and source hashes:
  `tools/russian-descriptions/README.md` and
  `docs/maintenance/russian-descriptions-coverage.json`.

- `RussianActionNames.json.gz` also contains source-matched names of items,
  actions, traits, status effects, mounts, companions and supported interface
  tables, plus status and deep-dungeon descriptions. Its XIV Rus contributions
  use the same pinned revision. Local translations are kept in
  `tools/russian-localization`; their coverage and provenance are recorded in
  `docs/maintenance/russian-game-text-coverage.json`.
- `RussianDescriptionTerms.json.gz` contains the accepted name glossary used
  inside translated descriptions after the game's conditional text is evaluated.
- `RussianCharacterText.json.gz` contains local translations of this project's
  authored appearance descriptions. The original icon and shape tables remain
  the source of the descriptions and their associations.
- Translation model weights and unreviewed machine drafts are not included in
  these resources or the plugin archive.

These files are contained in `latest.zip` / `FF14Accessibility-vX.Y.Z.zip` and
are installed next to the plugin.

### Tolk

- Files: `Tolk.dll`
- Copyright: (c) 2014-2019, Davy Kager (read from the DLL's version resource)
- Licence: **GNU Lesser General Public License, version 3 (LGPL-3.0)**
- Source: <https://github.com/dkager/tolk>

Tolk is the bridge to the screen reader. It is used unmodified and is loaded
dynamically at runtime, so it can be replaced with a different build of the
library by overwriting `Tolk.dll` in the plugin folder.

### Sku - spoken party numbers (heal monitor)

- Files: `assets/partymonitor/*.mp3` (122 files: the numbers 1-8 at 15 pitch
  levels each, plus `dead.mp3` and `full.mp3`)
- Origin: the World of Warcraft addon **Sku**, an accessibility addon for blind
  players. Taken from `SkuCore/assets/audio/aq/jus/pitch/`, volume level 100.
- Licence: **GNU General Public License, version 3 (GPL-3.0)** - Sku ships this
  licence in its `LICENSE.txt`. GPL-3.0 material may be combined into an
  AGPL-3.0 work, so these files travel under the plugin's own licence.
- Source: <https://github.com/Sku75/Sku-WoW-Addon-TBC>
- Project page: <https://sku75.github.io/Sku-WoW-Addon-TBC/>

Renamed only, never re-encoded: Sku's `jus_<number>_100_<pitch>.mp3` became
`<number>_<pitch>.mp3`, and `jus_dead_100_0` / `jus_full_100_0` became
`dead.mp3` / `full.mp3`. The audio itself is byte-for-byte Sku's.

The heal monitor uses them because Sku's pitch ladder IS the feature it ports:
15 steps five percent apart, the same word at the same length, only the pitch
telling the player how the group member is doing. Re-synthesising the numbers
and pitching them here was tried first and rejected - the phase vocoder smeared
the consonants until the numbers were hard to tell apart.

### NVDA Controller Client

- Files: `nvdaControllerClient64.dll`
- Copyright: NV Access Limited and the NVDA contributors
- Licence: **GNU Lesser General Public License, version 2.1 (LGPL-2.1)**
- Source: <https://github.com/nvaccess/nvda/tree/master/extras/controllerClient>

Required by Tolk in order to speak through NVDA. It is used unmodified, is
loaded dynamically at runtime, and can likewise be replaced by overwriting the
file. The NVDA Controller Client documentation explicitly permits shipping this
DLL with an application.

### NAudio 2.2.1

- Files: `NAudio.dll`, `NAudio.Core.dll`, `NAudio.Wasapi.dll`, `NAudio.WinMM.dll`,
  `NAudio.Asio.dll`, `NAudio.Midi.dll`, `NAudio.WinForms.dll`
- Licence: **MIT**
- Source: <https://github.com/naudio/NAudio>

Used unmodified for the audio beacon (a generated sine tone with panning and
pitch). Full licence text as shipped in the NuGet package:

```
Copyright 2020 Mark Heath

Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
the Software, and to permit persons to whom the Software is furnished to do so,
subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
```

### System.Speech 9.0.0 (Microsoft)

- Files: `System.Speech.dll`
- Licence: **MIT**
- Source: <https://github.com/dotnet/runtime>

Used unmodified for the second speech channel that carries the combat warnings
(`WarningVoiceService`). It talks to the SAPI voices already installed in
Windows; no voice data is shipped here. The file placed next to the plugin is
the Windows implementation from the package's `runtimes/win/lib/` folder — the
platform-neutral one of the same name throws on any other system. Full licence
text as shipped in the NuGet package:

```
The MIT License (MIT)

Copyright (c) .NET Foundation and Contributors

All rights reserved.

Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
the Software, and to permit persons to whom the Software is furnished to do so,
subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
```

---

## Shipped inside the installer

`FF14AccessibilityInstaller.exe` is published self-contained, so the following
are compiled into that single file.

### Newtonsoft.Json 13.0.3

- Licence: **MIT**
- Source: <https://github.com/JamesNK/Newtonsoft.Json>

Used unmodified to round-trip Dalamud's `dalamudConfig.json` without destroying
its `$type` fields. Full licence text:

```
The MIT License (MIT)

Copyright (c) 2007 James Newton-King

Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
the Software, and to permit persons to whom the Software is furnished to do so,
subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
```

### .NET runtime and Windows Forms (Microsoft)

- Licence: **MIT**
- Source: <https://github.com/dotnet/runtime>, <https://github.com/dotnet/winforms>

Included by `PublishSingleFile` + `SelfContained` so that users do not have to
install .NET themselves.

---

## Referenced at build time, not redistributed

The separate Russian installer also ships **Microsoft.Data.Sqlite.Core 9.0.9**
(MIT, <https://github.com/dotnet/efcore>), **SQLitePCLRaw 2.1.11**
(Apache-2.0, <https://github.com/ericsink/SQLitePCL.raw>) and **LiteDB 5.0.21**
(MIT, <https://github.com/litedb-org/LiteDB>) for selective removal of stored
settings. SQLitePCLRaw uses Windows' system `winsqlite3.dll`; these libraries
are not shipped in the game plugin. Their full licence texts are included in
the installer source distribution under `Installer/Russian/licenses`.

The plugin compiles against these assemblies but does **not** ship them. They
come from the user's own XIVLauncher/Dalamud installation, which is installed
separately.

- **Dalamud** — AGPL-3.0 — <https://github.com/goatcorp/Dalamud>
- **FFXIVClientStructs** — <https://github.com/aers/FFXIVClientStructs>
- **Lumina**, **Lumina.Excel** — <https://github.com/NotAdam/Lumina>
- **ImGuiScene**, **InteropGenerator.Runtime** — part of the Dalamud distribution

Optional runtime cooperation with **vnavmesh**
(<https://github.com/awgil/ffxiv_navmesh>) happens exclusively over Dalamud's
IPC. No code of that plugin is linked or redistributed here.

---

## Final Fantasy XIV

FINAL FANTASY XIV © SQUARE ENIX CO., LTD. All rights reserved. FINAL FANTASY is
a registered trademark of Square Enix Holdings Co., Ltd. This project is not
affiliated with, endorsed by, or sponsored by Square Enix. The localization
catalogs contain game text and source strings for version matching as described
above. Game archives, executables, textures and models are not redistributed.
