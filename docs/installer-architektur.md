# Architektur-Konzept: Grafischer Installer für FF14Accessibility

Status: Konzept, keine Implementierung. Grundlage für die Entscheidung, was als
Nächstes gebaut wird.

## 0. Ausgangslage (Ist-Zustand geprüft)

Der bestehende `Installer/` (`FF14AccessibilityInstaller.csproj`, `Program.cs`)
ist eine **reine Konsolenanwendung** (.NET 8, self-contained single-file
win-x64 EXE, ca. 68 MB inkl. Runtime). Kein WPF/WinForms-Code vorhanden.

Was er heute schon kann (wiederverwendbar):
- Erkennt, ob `%AppData%\XIVLauncher` existiert; bietet sonst den Download
  des offiziellen XIVLauncher-Setups an (`Process.Start`, `UseShellExecute`).
- Kopiert mitgelieferte Plugin-Dateien aus einem `plugin`-Unterordner neben
  der EXE nach `%AppData%\XIVLauncher\devPlugins\FF14Accessibility`.
- Bietet vnavmesh-Download an (aktuell **hart auf Version 1.2.3.8 gepinnt**,
  URL `https://puni.sh/api/plugins/download/48/vnavmesh/versions/1.2.3.8/...`)
  und entpackt es nach `devPlugins\vnavmesh`.
- Patcht `dalamudConfig.json` direkt: trägt DLL-Pfade in
  `DevPluginLoadLocations` ein und setzt `IsEnabled=true` in den
  `DefaultProfile.Plugins`-Einträgen. Macht vorher ein `.bak-installer`-Backup,
  schreibt BOM-frei (wichtig, siehe unten).
- Nutzt `Newtonsoft.Json` mit `JObject`, weil Dalamud selbst
  `TypeNameHandling.All` verwendet (`$type`-Felder) — das muss beim
  Round-Trip erhalten bleiben.
- Kein Update-Check gegen GitHub Releases eingebaut (Plugin-Dateien müssen
  bereits im `plugin`-Ordner neben der EXE liegen — der Installer selbst lädt
  aktuell **nichts** vom eigenen Repo).
- Kein Selbst-Update.

Das ist eine solide Funktionsbasis (devPlugins-Kopie + Config-Patch +
XIVLauncher-Erkennung funktionieren erwiesenermaßen). Was fehlt: echte GUI,
Update-Check gegen GitHub Releases für **beide** Plugins, robustere
vnavmesh-Anbindung.

Verifiziert: `https://puni.sh/api/repository/veyn` liefert aktuell ein
gültiges JSON-Array mit vnavmesh drin (`InternalName: "vnavmesh"`,
`DownloadLinkInstall`/`DownloadLinkUpdate` zeigen auf
`puni.sh/api/plugins/download/48/vnavmesh/versions/<version>/...`). Das ist
die offizielle veyn/xan_0-Distribution, kein Fake.

---

## 1. Framework-Wahl: WinForms statt WPF

**Empfehlung: WinForms**, self-contained single-file .NET 8 EXE.

Begründung:
- WinForms-Standardcontrols (`Label`, `Button`, `ProgressBar`, `ListBox`)
  setzen automatisch die klassischen Win32/UIA-Properties (Name, Role,
  LabelledBy). NVDA/JAWS lesen sie ohne Zusatzaufwand korrekt vor — das ist
  seit Jahrzehnten ausgereift.
- WPF hat zwar ebenfalls UIA-Unterstützung, aber deutlich mehr Stellen, an
  denen man es kaputt machen kann (Custom-Templates, Styles ohne
  `AutomationProperties.Name`, eigene Controls ohne `AutomationPeer`). Für ein
  kleines, funktionales Tool ist der Zusatzaufwand nicht gerechtfertigt.
- Tab-Reihenfolge (`TabIndex`), Fokus-Handling und Standard-Dialoge
  (`MessageBox`) sind in WinForms trivial korrekt zu bekommen.
- Team-Kontext: Das bestehende Projekt ist schon .NET/C#, kein UI-Vorwissen
  in WPF/XAML nötig — WinForms-Code-behind ist näher am bisherigen
  Konsolen-Stil (imperativ, wenig Boilerplate).

Alternative WPF: nur sinnvoll, wenn später umfangreichere/optisch anspruchsvollere
UI gewünscht ist. Für "ein Fenster mit Fortschrittsanzeige, Log-Liste, ein paar
Buttons" ist das Overkill und erhöht das Risiko screenreader-unfreundlicher
Stellen.

**Self-contained Single-File EXE:** ja, beibehalten wie im bestehenden
Installer. Zielgruppe hat keine Techniknkenntnisse — "lade EXE, doppelklick"
muss ohne .NET-Runtime-Installation funktionieren. Größe (~65-70 MB) ist
unkritisch, das ist ein einmaliger Download für ein Barrierefreiheits-Tool,
kein wiederholter Prozess. `PublishTrimmed` würde die Größe drücken, aber
Trimming ist riskant bei Reflection-nutzenden Bibliotheken (Newtonsoft.Json)
— nicht empfohlen ohne ausgiebige Tests.

### Screenreader-Anforderungen konkret (WinForms)

- Jedes Eingabe-/Button-Control braucht `Text` oder `AccessibleName` gesetzt.
- `TabIndex` durchgängig und logisch (0, 1, 2, … in Lesereihenfolge).
- Fortschritt: **kein** reines `ProgressBar`-Prozent-Update ohne Text-Pendant.
  Ein begleitendes `Label` mit `AccessibleRole = StatusText` und
  Live-Text-Update ("Lade Plugin herunter... 40 %") — WinForms-Labels lösen
  bei Textänderung automatisch `LiveRegion`-ähnliches Verhalten über die
  Standard-Automation aus, wenn der Fokus dort liegt oder man
  `Label.AccessibleName` aktiv nachzieht. Sicherer: zusätzlich dieselben
  Meldungen in eine fokussierbare `ListBox`/`TextBox` (read-only, Mehrzeilig)
  schreiben, die der Nutzer mit Pfeiltasten durchgehen kann — das ist die
  robusteste Lösung, weil sie nicht von Live-Region-Timing abhängt.
- Keine Owner-Draw-Controls, keine reinen GDI+-Zeichnungen ohne
  Text-Alternative.
- Reihenfolge: Fenster öffnet mit Fokus auf dem ersten sinnvollen Element
  (z. B. "Installieren"-Button oder Status-Liste), nicht auf dem Fenster
  selbst.
- Modale Dialoge (`MessageBox.Show`) sind bereits gut screenreader-tauglich
  (Standard-Win32) — für Ja/Nein-Abfragen (z. B. "vnavmesh jetzt
  einrichten?") genügt das, kein Custom-Dialog nötig.

---

## 2. "Alles Nötige" – XIVLauncher, Dalamud, vnavmesh

### 2.1 XIVLauncher/Dalamud-Erkennung

Wie im bestehenden Installer: Prüfung auf
`%AppData%\XIVLauncher` (Ordner) bzw. genauer auf
`%AppData%\XIVLauncher\dalamudConfig.json` als Nachweis, dass Dalamud
mindestens einmal gelaufen ist.

**Fehlt XIVLauncher komplett:** Hinweis + Download-Link anbieten
(`https://github.com/goatcorp/FFXIVQuickLauncher/releases/latest/download/Setup.exe`),
NICHT automatisch mitinstallieren/silent ausführen. Begründung:
- XIVLauncher-Setup ist ein interaktiver Wizard (Login, Spielpfad-Wahl,
  Dalamud-Opt-in) — das lässt sich nicht sinnvoll unattended durchführen,
  ohne Login-Daten des Nutzers zu verarbeiten (Sicherheits-/Vertrauensrisiko,
  das ein Drittanbieter-Tool nicht tragen sollte).
- Der bestehende Ansatz (Setup herunterladen, GUI-Setup starten, Nutzer bittet
  danach erneut auszuführen) ist der richtige, pragmatische Mittelweg.
- Wichtig für die GUI-Version: klare Ansage, dass der Nutzer nach dem
  XIVLauncher-Setup **einmal einloggen, Dalamud aktivieren und das Spiel
  einmal starten** muss, bevor der Installer weitermachen kann (weil
  `dalamudConfig.json` erst dann entsteht bzw. die Plugin-Profile darin
  angelegt werden).

**Dalamud noch nie gestartet** (XIVLauncher da, aber `dalamudConfig.json`
fehlt): dasselbe Problem, gleiche Lösung — Text-Hinweis, kein automatisches
Eingreifen.

### 2.2 vnavmesh-Distribution und Robustheit

Verifiziert über `puni.sh/api/repository/veyn`: vnavmesh wird offiziell über
das **puni.sh Third-Party-Repo** von veyn/xan_0 verteilt, mit versionierten
Download-Links (`.../versions/<version>/install/latest.zip`).

**Zwei Wege, wie der Installer vnavmesh "automatisch einrichtet":**

**Weg A — DevPlugin-Kopie (wie jetzt im bestehenden Installer):**
Direkter Download der ZIP von puni.sh, Entpacken nach
`devPlugins\vnavmesh`, Eintrag in `DevPluginLoadLocations` +
`IsEnabled=true` im Profil.
- Vorteil: funktioniert genauso robust/direkt wie beim eigenen Plugin,
  keine Abhängigkeit von Dalamuds interner Update-Logik für Drittrepos.
- Nachteil: Der Installer muss selbst wissen, welche vnavmesh-Version aktuell
  ist (aktuell im Code hart gepinnt auf `1.2.3.8`) — sonst bekommt der Nutzer
  nie automatisch vnavmesh-Updates, außer er startet den FF14Accessibility-
  Installer erneut UND der Installer prüft dabei aktiv gegen puni.sh nach der
  neuesten Version.
- Wird vnavmesh nie über Dalamuds eigenen Mechanismus verwaltet, sieht der
  Nutzer es auch nicht im normalen `/xlplugins`-Fenster als "verwaltetes"
  Plugin — kosmetisch, aber erwähnenswert.

**Weg B — Third-Party-Repo-Eintrag (`ThirdRepoList` in dalamudConfig.json) +
Dalamud lädt/aktualisiert vnavmesh selbst:**
Eintrag `https://puni.sh/api/repository/veyn` in
`config.ThirdRepoList` (Liste von Objekten
`{"$type": "Dalamud.Configuration.ThirdPartyRepoSettings, Dalamud", "Url": "...", "IsEnabled": true}`
— verifiziert per Dalamud-Quellcode, dort `List<ThirdPartyRepoSettings> ThirdRepoList`).
Dalamud zieht darüber künftig alle Updates für vnavmesh selbst.
- Vorteil: Für vnavmesh-Updates ist danach kein extra Installer-Lauf mehr
  nötig — Dalamuds eigener Auto-Update-Mechanismus (`AutoUpdateBehavior`,
  standardmäßig "nur benachrichtigen", kann Dalamud-seitig auf "alle
  aktualisieren" gestellt werden) übernimmt.
- Nachteil: Die eigentliche Installation eines Plugins aus einem Third-Repo
  muss regulär trotzdem einmal im Dalamud-Plugin-Installer (ImGui-Fenster,
  `/xlplugins`) angeklickt werden — dafür gibt es **keinen** dokumentierten,
  offiziellen Weg, das programmatisch von außen (aus dem Installer-Prozess)
  auszulösen. Genau das war der ursprüngliche Grund, warum das bestehende
  Projekt den DevPlugin-Weg gewählt hat (ImGui ist für blinde Nutzer nicht
  bedienbar).
- `ThirdRepoSpeedbumpDismissed` (ein Bool in der Config) muss ebenfalls auf
  `true` gesetzt werden, sonst zeigt Dalamud beim ersten Öffnen der
  Experimental-Tab einen Warndialog (wieder ImGui, wieder nicht
  screenreader-bedienbar) — ließe sich vom Installer mitsetzen, ändert aber
  nichts daran, dass die Erstinstallation selbst weiter ein ImGui-Klick wäre.

**Empfehlung: Weg A (DevPlugin-Kopie) für vnavmesh beibehalten, aber
versionsdynamisch statt hart gepinnt.** Begründung: Es passt zur
Kernanforderung "kein ImGui-Klick nötig". Der Installer ruft dazu
`https://puni.sh/api/repository/veyn` ab (funktioniert wie o.g. verifiziert),
sucht den Eintrag mit `InternalName == "vnavmesh"`, liest
`DownloadLinkInstall`/`DownloadLinkUpdate` und `AssemblyVersion` heraus und
lädt darüber die jeweils neueste Version — dieselbe Update-Logik wie beim
eigenen Plugin, nur eine andere Quelle. Damit verschwindet die
hartkodierte Versionsnummer.

**Zusätzlich** kann der Installer optional (nicht Pflicht, aber sinnvolle
Ergänzung) den Third-Repo-Eintrag in `ThirdRepoList` mitsetzen, damit
sehende Helfer/fortgeschrittene Nutzer vnavmesh auch regulär über
`/xlplugins` sehen und verwalten können — das ist ein reiner Zusatznutzen,
ersetzt aber nicht Weg A als primären, barrierefrei nutzbaren Pfad.

---

## 3. Eigenes Plugin: DevPlugin-Kopie vs. Custom-Repo

Aktuell: `repo.json` (im Projekt-Root) existiert bereits als Custom-Repo-
Manifest, zeigt per `DownloadLinkInstall`/`DownloadLinkUpdate` auf
`.../releases/latest/download/latest.zip`. DalamudPackager erzeugt beim
Release-Build (Verifiziert über README des Pakets) selbst schon einen
Manifest+`latest.zip` im Output — das für ein PR in ein offizielles Repo
gedacht ist, aber genauso als eigenes Custom-Repo-JSON weiterverwendbar ist.

**Analyse:**
- Ein Custom-Repo (Weg B, wie oben für vnavmesh beschrieben) hätte für das
  **eigene** Plugin denselben Showstopper: Die Erstinstallation eines neuen
  Plugins aus einem Third-Repo verlangt einen Klick im ImGui-Plugin-
  Installer. Für die Zielgruppe (blinde Erstnutzer ohne Technikkenntnisse)
  ist das nicht akzeptabel — genau deshalb existiert der DevPlugin-Ansatz
  im Projekt schon.
- Für **Updates** eines bereits registrierten Custom-Repo-Plugins ist
  Dalamuds Auto-Update dagegen unauffällig und robust, sofern
  `AutoUpdateBehavior` entsprechend steht — aber die Erstinstallation bleibt
  das Problem.

**Empfehlung: DevPlugin-Kopie bleibt der primäre, vom Installer gesteuerte
Weg — für Erst- UND Update-Installation, konsistent zu vnavmesh (Weg A).**
Das `repo.json` im Projekt-Root wird **zusätzlich** weiter gepflegt und aktuell
gehalten (es kostet nichts, DalamudPackager erzeugt das Manifest ohnehin
automatisch mit) — als Option für sehende Helfer/Poweruser, die es lieber
regulär über `/xlplugins` einbinden wollen, und als Fallback/Dokumentation.
Der Installer selbst braucht dieses Repo-JSON nicht zwingend zu lesen — er
nutzt stattdessen direkt die GitHub-Releases-API (siehe Abschnitt 4), was
unabhängig von Dalamuds Ladezyklus ist und mit derselben Logik für eigenes
Plugin UND vnavmesh (über puni.sh) funktioniert.

---

## 4. Update-Mechanik des Installers

### 4.1 Versionsprüfung

- **Eigenes Plugin:** GitHub Releases API,
  `GET https://api.github.com/repos/derbruedi/ff14-accessibility/releases/latest`
  (verifiziert per Live-Abruf: liefert `tag_name` "v4.61" und Asset
  `FF14Accessibility-v4.61.0.zip` mit `browser_download_url`). Kein Auth-Token
  nötig für öffentliche Repos (Rate-Limit ohne Token: 60 Requests/Stunde je
  IP — für ein Tool, das man gelegentlich startet, ausreichend).
- **vnavmesh:** `GET https://puni.sh/api/repository/veyn`, Eintrag mit
  `InternalName == "vnavmesh"` filtern, `AssemblyVersion` +
  `DownloadLinkInstall`/`DownloadLinkUpdate` verwenden.

Versionsvergleich: Für das eigene Plugin reicht ein einfacher
String-/`Version`-Vergleich zwischen der lokal installierten
`AssemblyVersion` (aus der bereits kopierten `FF14Accessibility.json` in
devPlugins, falls vorhanden) und dem `tag_name`/Asset-Namen des Releases.
Für vnavmesh entsprechend mit der `AssemblyVersion` aus dem Repository-JSON.
Bei Erstinstallation (keine lokale Version vorhanden) wird ohne Vergleich
direkt die neueste Version installiert — **das deckt automatisch auch
Punkt 4 aus dem Auftrag ab** ("Erstinstallation lädt immer das neueste
Release, ein Codepfad"): Der Ablauf ist in beiden Fällen identisch
("hole neueste Version, vergleiche mit lokal installierter (falls
vorhanden), installiere bei Unterschied oder Fehlen") — es gibt keinen
separaten Erstinstall-Codepfad, nur eine Bedingung "lokale Version
existiert nicht oder ist älter".

### 4.2 Download + Entpacken

Analog zum bestehenden `TryDownload`/`ZipFile.ExtractToDirectory`-Pattern:
ZIP von `browser_download_url` (GitHub) bzw. `DownloadLinkInstall`
(puni.sh) laden, in einen Temp-Ordner entpacken, dann gezielt die relevanten
Dateien (DLL, `.json`-Manifest, `Tolk.dll`, `nvdaControllerClient64.dll`,
`NAudio*.dll`) nach `devPlugins\FF14Accessibility` bzw. `devPlugins\vnavmesh`
kopieren (überschreiben) — nicht den ganzen ZIP-Inhalt blind in devPlugins
entpacken, um keine Altlasten (z. B. entfernte Dateien einer Vorversion)
liegen zu lassen. Vorher bestehenden Zielordner-Inhalt der bekannten
Dateitypen (`*.dll`, `*.json`, `*.pdb`) löschen oder gezielt überschreiben.

### 4.3 Selbst-Update des Installers

**Empfehlung: kein separates Selbst-Update-Feature für die erste Version.**
Begründung:
- Der Installer ändert sich viel seltener als das Plugin (reine
  Infrastruktur: Download+Kopieren+Config-Patch). Ein Update ist nur nötig,
  wenn sich z. B. das Dalamud-Config-Format ändert oder eine neue
  Sicherheitslücke im Installer selbst gefunden wird — beides seltene
  Ereignisse.
- Ein Installer, der sich selbst überschreibt (laufende EXE ersetzen), ist
  auf Windows nicht trivial (Datei ist zur Laufzeit gesperrt) und bräuchte
  einen Neustart-Trick (z. B. Kopie in Temp starten, die Original-EXE
  ersetzt) — deutlich mehr Komplexität für einen seltenen Fall.
- Alternative, einfache Lösung: Der Installer prüft beim Start zusätzlich
  die eigene Version gegen die neueste Installer-Version im Repo (eigenes
  Release-Asset oder Tag-Konvention) und zeigt bei Bedarf nur einen
  Text-Hinweis mit Link ("Eine neuere Installer-Version ist verfügbar:
  <Link>. Bitte lade sie bei Gelegenheit herunter."), OHNE automatisch zu
  aktualisieren. Das deckt den Bedarf ("Nutzer soll es erfahren") ohne die
  Komplexität eines echten Self-Updaters. Kann bei Bedarf später ergänzt
  werden.

---

## 5. Ablauf-Skizze

### 5.1 Erstinstallation (Nutzer hat noch nichts)

1. Installer startet, Fenster öffnet mit Fokus auf Status-Bereich.
   Ansage/Text: "FF14 Accessibility Installer. Prüfe XIVLauncher..."
2. XIVLauncher nicht gefunden → Text + Dialog: "XIVLauncher wurde nicht
   gefunden. Jetzt herunterladen und Setup starten?" (Ja/Nein-Button, klar
   beschriftet, Standard-`MessageBox` oder eigene beschriftete Buttons).
3. Bei "Ja": Download-Fortschritt als Text ("Lade XIVLauncher-Setup...
   40 %"), Setup wird gestartet, Installer zeigt Abschlusstext: "Bitte folge
   dem XIVLauncher-Assistenten, melde dich an, aktiviere Dalamud in den
   Einstellungen und starte das Spiel einmal. Führe diesen Installer danach
   erneut aus." Fenster bleibt offen bis Nutzer schließt (kein
   automatisches Beenden ohne Ansage).
4. **Zweiter Lauf** (nach Spielstart): XIVLauncher + `dalamudConfig.json`
   gefunden. Text: "XIVLauncher gefunden. Prüfe neueste Version von FF14
   Accessibility..."
5. GitHub-API-Abfrage, keine lokale Version vorhanden → direkter Download
   der neuesten Version. Fortschrittstext mit Prozent.
6. Entpacken, Kopieren nach devPlugins. Text: "Plugin installiert (Version
   4.61)."
7. vnavmesh-Abfrage (puni.sh) → keine lokale Version vorhanden →
   Ja/Nein-Dialog "vnavmesh für Auto-Lauf jetzt einrichten?" → bei Ja:
   Download+Kopieren analog, Fortschrittstext.
8. `dalamudConfig.json` patchen: DevPluginLoadLocations-Einträge setzen,
   Backup anlegen. Da beim ersten Mal die Profil-Einträge (`IsEnabled`)
   noch nicht existieren (Dalamud legt sie erst nach dem ersten
   Laden/Erkennen an), zeigt der Installer: "Plugin eingetragen, aber noch
   nicht aktiviert. Starte das Spiel einmal, beende es, und führe den
   Installer noch einmal aus, um es zu aktivieren."
9. **Dritter Lauf:** Alles vorhanden und aktuell → Profil-Einträge jetzt
   vorhanden → `IsEnabled=true` gesetzt. Abschlusstext: "Fertig. Starte
   FINAL FANTASY XIV über XIVLauncher — das Plugin meldet sich beim Login
   mit einer Sprachansage." Fenster hat einen fokussierbaren
   "Schließen"-Button (kein reiner Enter-Zwang wie in der Konsolen-Version).

### 5.2 Update-Lauf (alles schon installiert, Routinefall)

1. Installer startet. Text: "Prüfe auf Updates..."
2. GitHub-API: lokale Version (aus devPlugins-Manifest) mit `latest`
   verglichen.
3. Kein Update nötig → Text: "FF14 Accessibility ist aktuell (Version
   4.61)." — vnavmesh ebenso geprüft, ebenfalls aktuell.
4. Ist ein Update vorhanden → automatischer Download+Installation ohne
   Rückfrage (Plugin-Updates sind risikoarm, entsprechen dem
   Nutzerwunsch "immer aktuell halten"; nur bei vnavmesh, falls es beim
   letzten Mal abgelehnt wurde, weiterhin fragen statt aufzudrängen).
   Fortschrittstext je Schritt.
5. Abschlusstext mit Versionsnummer(n) und Hinweis "Starte das Spiel neu,
   falls es gerade läuft, damit die neue Version geladen wird."

Alle Texte erscheinen zusätzlich in der fokussierbaren Log-Liste (Pfeiltasten-
navigierbar), damit der Nutzer frühere Meldungen erneut nachlesen/anhören
kann, falls der Screenreader eine Zeile verpasst hat.

---

## 6. Offene Punkte / Empfehlungen zusammengefasst

| Thema | Empfehlung |
|---|---|
| UI-Framework | WinForms (nicht WPF) — bester UIA-Support out of the box |
| Deployment | .NET 8 self-contained single-file EXE, wie bisher |
| XIVLauncher fehlt | Hinweis + offizieller Setup-Download, kein Silent-Install |
| vnavmesh-Einrichtung | DevPlugin-Kopie (Weg A), Version dynamisch von puni.sh statt hart gepinnt; optional zusätzlich ThirdRepoList-Eintrag als Komfort für sehende Helfer |
| Eigenes Plugin | DevPlugin-Kopie bleibt primärer Weg (Erst+Update); `repo.json`/Custom-Repo weiter gepflegt als Zusatzoption, nicht als Installer-Quelle |
| Update-Quelle | GitHub Releases API (`/releases/latest`) für eigenes Plugin, puni.sh-Repository-JSON für vnavmesh |
| Erstinstallation vs. Update | ein Codepfad ("neueste Version holen, mit lokaler vergleichen, installieren wenn nötig/fehlend") |
| Selbst-Update des Installers | vorerst nicht automatisiert; nur Versions-Hinweistext mit Link |

## 7. Nicht Teil dieses Konzepts

Keine Implementierung, kein Code, keine Projektdatei-Änderungen. Nächster
Schritt (nach Freigabe dieses Konzepts) wäre die Erstellung eines
WinForms-Projekts, das die bestehende `Program.cs`-Logik in Services
extrahiert (Download/Update-Logik, Dalamud-Config-Patch) und eine dünne
GUI-Schicht darüberlegt.
