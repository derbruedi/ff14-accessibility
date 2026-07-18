# FF14 Accessibility

🇬🇧 English version: [README.en.md](README.en.md)

Ein Dalamud-Plugin, das **FINAL FANTASY XIV für blinde Spielerinnen und
Spieler** zugänglich macht: Menüs, Dialoge, Quests, Navigation, Inventar,
Kampf und Aktionsleisten werden per Screenreader (NVDA) vorgelesen und mit
Tönen unterstützt — inklusive Braillezeile und automatischem Laufen.

## Funktionen

### Menüs und Fenster

- Titelbildschirm, Charakterauswahl und komplette Charaktererstellung
  (Volk, Geschlecht, Volksstamm, Name) werden angesagt.
- Listen-Navigation mit Pfeiltasten: Systemmenü, Journal, Auswahl-Dialoge,
  Kontextmenüs — jede Zeile wird beim Fokussieren gesprochen.
- Ok/Abbrechen-Dialoge: Links/Rechts sagt den fokussierten Knopf an.
- Einstellungsfenster: Regler („Transparenz, Regler, 50, von 0 bis 100"),
  Auswahllisten, Kontrollkästchen und Reiter; Enter aktiviert Reiter.
- Fenster **Tastenbelegung**: jede Zeile wird mit Befehl **und** belegter
  Taste angesagt („Vorwärts, Taste W").
- Fehlermeldungen und Hinweise des Spiels (Toasts) werden gesprochen,
  z. B. „Das Ziel ist zu weit entfernt."
- NPC-Dialoge mit Sprechername zuerst („Miounne: …"); bei offenem Journal
  liest Strg+F10 die Quest-Beschreibung und Ziele vor.
- Alle Ansagen erscheinen zusätzlich auf der **Braillezeile**.

### Chat

- **Empfangen**: eingehende Nachrichten werden vorgelesen, je Kanal
  abschaltbar (Sagen, Rufen, Gruppe, Allianz, Flüstern, Freie
  Gesellschaft, System).
- **Schreiben**: beim Öffnen der Chat-Eingabe wird der aktive Kanal
  angesagt („Chat-Eingabe, Sagen"), ein Kanalwechsel während des Tippens
  ebenso. Jedes getippte Zeichen wird gesprochen, ebenso Gelöschtes —
  denn das Eingabefeld des Spiels liest ein Screenreader nicht.
- **Nachlesen**: ein Verlaufs-Browser mit acht getrennten Kategorien
  (Dialoge, Sagen, Rufen, Gruppe, Allianz, Flüstern, Freie Gesellschaft,
  System) — je 50 Nachrichten. So lässt sich Verpasstes in Ruhe
  nachhören, ohne den laufenden Chat zu stören.

### Navigation und Laufen

- **Objekt-Browser**: mit einer Taste durch Objekte in der Nähe blättern
  (NPCs, Gegner, Spieler, Sammelpunkte, Ätheryten, Quest-Ziele,
  Kartenwegpunkte wie Zonen-Ausgänge). Ansage mit Name, Art, Entfernung
  und Richtung; das Objekt wird gleichzeitig anvisiert.
- **Audio-Beacon**: Stereo-Ton zeigt die Richtung zum Ziel (Seite und
  Tonhöhe), die Lautstärke folgt der Entfernung.
- **Gehhilfe**: geführtes manuelles Laufen entlang des Wegenetzes, um
  Hindernisse herum — mit Wegpunkt-Tönen, Richtungsansagen relativ zur
  Blickrichtung und Ankunftston.
- **Auto-Lauf**: automatisch zum Ziel laufen (benötigt das Fremd-Plugin
  vnavmesh), mit Routen-Vorschau, Fortschrittsansagen und ehrlicher
  Meldung, wenn kein Weg gefunden wird.
- **Routen-Vorschau**: den Weg ansagen lassen, ohne zu laufen
  („Weg zu Ätheryt, 62 Meter: 25 Meter nach Norden, dann …").
- Zielwechsel-Ansagen für die Spiel-eigenen Zieltasten (Tab, F1–F12).

### Kampf

- Kampfstatus auf Tastendruck: eigene HP und MP.
- Ziel-HP in Stufen, Ansage wenn das Ziel einen Zauber wirkt,
  kurzer Ton beim Anvisieren eines Gegners.

### Inventar und Ausrüstung

- Item-Slots in Tasche, Charakterfenster und Arsenal werden mit Name,
  Stufe und Tragbarkeit angesagt („Bronzegladius, Stufe 5, tragbar" /
  „nicht tragbar, ab Stufe 26"); leere Felder sagen „Leer".
- Läden: an jede Ware wird Stufe und Tragbarkeit angehängt.
- Angelegte Ausrüstung komplett vorlesen; empfohlene Ausrüstung mit dem
  Spiel-eigenen Optimierer anlegen.
- Inventar und Gil auf Tastendruck.

### Aktionsleisten (Hotbars)

- Gewählte Aktionsleiste vorlesen: welche Taste löst welchen Skill aus.
- **Skill-Browser**: alle gelernten Skills des aktuellen Jobs per Tastatur
  durchblättern und auf eine beliebige der 10 Leisten legen — komplett
  ohne Maus. Ansagen nennen die tatsächlich gebundene Taste
  (z. B. „Leiste 2, Taste Strg+3").

### Sonstiges

- Emote-Browser: Emotes durchblättern und ausführen.
- Bestiarium (Jagdtagebuch) vorlesen, inklusive Lebensraum der Monster.
- Nach jedem Login speichert das Plugin die Spiel-Tastenbelegung als
  Textdatei auf dem Desktop und warnt bei Konflikten mit Plugin-Tasten.

## Voraussetzungen

- Windows, FINAL FANTASY XIV und [XIVLauncher](https://goatcorp.github.io/)
  mit Dalamud.
- **NVDA** als Screenreader (über die Tolk-Bibliothek; die nötigen DLLs
  bringt das Plugin mit).
- Optional: das Fremd-Plugin **vnavmesh** für Auto-Lauf und
  Wegenetz-Führung — der Installer bietet den Download an.

## Installation für blinde Nutzerinnen und Nutzer (mit Screenreader)

Es gibt einen grafischen Installer mit einem einzigen Button. Er richtet
alles ein und hält das Plugin aktuell — **ohne** dass du Dalamuds
Plugin-Fenster (das ein Screenreader nicht vorliest) bedienen musst.

### Schritt für Schritt

1. Lade `FF14AccessibilityInstaller.exe` vom
   [neuesten Release](https://github.com/derbruedi/ff14-accessibility/releases/latest)
   herunter (Abschnitt „Assets", Link mit diesem Dateinamen).
2. Führe die heruntergeladene Datei aus (Enter oder Doppelklick im
   Downloads-Ordner).
3. Windows SmartScreen zeigt möglicherweise eine Warnung, weil der Installer
   nicht signiert ist. Aktiviere in diesem Dialog den Link oder Button
   „Weitere Informationen" und danach den Button „Trotzdem ausführen". Beide
   lassen sich mit Tab erreichen und mit Enter bzw. Leertaste auslösen.
4. Im Installer-Fenster springt der Fokus automatisch auf den Button
   „Installieren oder Aktualisieren". Falls nicht, drücke Tab, bis dieser
   Button angesagt wird, und drücke dann Enter.
5. Warte die Meldungen im Statusfeld ab. Am Ende erscheint eine Dialogbox mit
   der Meldung „Vorgang abgeschlossen". Bestätige sie mit Enter.
6. Starte XIVLauncher und logge dich ins Spiel ein — das Plugin ist aktiv und
   meldet sich beim Login mit einer gesprochenen Versionsansage.

### Update

Für ein Update reicht es, `FF14AccessibilityInstaller.exe` erneut
auszuführen und wieder den Button „Installieren oder Aktualisieren" zu
aktivieren. Er überschreibt die Plugin-Dateien, und der nächste Spielstart
lädt die neue Version.

### Was der Installer macht

- Prüft, ob **XIVLauncher** installiert ist, und bietet sonst an, das
  offizielle Setup herunterzuladen und zu starten.
- Kopiert die Plugin-Dateien in Dalamuds `devPlugins`-Ordner und aktiviert
  sie direkt in `dalamudConfig.json` (mit Sicherungskopie).
- Bietet an, das **vnavmesh**-Plugin (für den Auto-Lauf) vom Original
  herunterzuladen. vnavmesh stammt von einem anderen Autor und wird
  **nicht** von diesem Projekt mitverteilt.

## Tastenübersicht (Standard)

Die Tasten sind so gewählt, dass sie laut Spiel-Tastenbelegung frei sind.
Strg+F1 sagt jederzeit die aktuelle Hilfe an.

### Objekte finden

- **N** — nächstes Objekt ansagen und anvisieren
- **Umschalt+N** — vorheriges Objekt
- **Strg+N** — Objekt-Kategorie vorwärts (z. B. NPCs, Gegner, Quest-Ziele,
  Wegpunkte)
- **Strg+Umschalt+N** — Objekt-Kategorie zurück

### Laufen und Führung

- **Nummernblock 3** — Auto-Lauf zum gewählten Ziel an/aus (braucht vnavmesh)
- **Strg+Nummernblock 3** — Gehhilfe an/aus (Ton-Führung beim manuellen
  Laufen, folgt dem Wegenetz um Hindernisse)
- **Strg+Nummernblock 5** — Routen-Vorschau: Weg ansagen, ohne zu laufen
- **F** — zum Ziel hindrehen (Spiel-Taste), **W** — laufen (Spiel-Taste)

### Vorlesen und Information

- **Strg+F1** — Hilfe (Tasten und Befehle)
- **Strg+F2** — aktives Fenster ansagen
- **Strg+F10** — aktuelles Menü vorlesen; bei offenem Journal: Quest vorlesen
- **Strg+F11** — Sprache sofort stoppen
- **Strg+H** — Kampfstatus: eigene HP und MP
- **Strg+L** — Stufe und fehlende Erfahrung
- **Strg+F3** — Inventar vorlesen (Tasche und Schlüsselgegenstände)
- **Umschalt+F3** — Gil-Stand
- **Strg+F4** — Bestiarium (Jagdtagebuch) vorlesen

### Ausrüstung

- **Strg+F6** — angelegte Ausrüstung vorlesen (mit Stufe)
- **Strg+F7** — empfohlene Ausrüstung anlegen (Spiel-eigener Optimierer)

### Aktionsleisten und Skill-Browser

- **Strg+F9** — gewählte Aktionsleiste vorlesen
- **Umschalt+F7** / **Umschalt+F8** — Skill-Browser: vorheriger / nächster
  gelernter Skill
- **Umschalt+F11** — Ziel-Leiste wechseln (Leiste 1 bis 10)
- **Umschalt+F9** — Ziel-Taste auf der Leiste wählen (sagt an, was dort liegt)
- **Umschalt+F10** — gewählten Skill auf die Ziel-Taste legen

### Chat nachlesen

- **Strg+Punkt** / **Strg+Komma** — Kategorie vor / zurück (Dialoge,
  Sagen, Rufen, Gruppe, Allianz, Flüstern, Freie Gesellschaft, System);
  angesagt wird der Name mit der Anzahl der Nachrichten
- **Komma** / **Punkt** — in der gewählten Kategorie zur älteren /
  neueren Nachricht blättern („3 von 12: …")

### Emotes

- **Umschalt+F4** / **Umschalt+F5** — Emote zurück / vor
- **Umschalt+F6** — gewähltes Emote ausführen

### Diagnose

- **Strg+F5** — UI-Dump des aktuellen Fensters auf den Desktop speichern
  (hilft bei Fehlerberichten)

## Chat-Befehle

Alle Funktionen mit Tasten gibt es teils auch als Befehl:

- `/acc nav` — Richtung und Entfernung zum Ziel ansagen
- `/acc set` — aktuelles Ziel verfolgen
- `/acc clear` — Ziel aufheben
- `/acc near` — Objekte in der Nähe auflisten
- `/acc status` — HP und MP ansagen
- `/acc ui` — aktuelles Menü vorlesen
- `/acc win` — aktives Fenster ansagen
- `/acc keys` — Spiel-Tastenbelegung auf den Desktop speichern
- `/acc stop` — Sprache stoppen

## Sprache

Das Plugin ist für den **deutschen Spiel-Client** entwickelt und getestet;
die eigenen Ansagen des Plugins sind derzeit überwiegend Deutsch (einige
Grundansagen folgen der Windows-Sprache). Spieltexte (Dialoge, Menüs)
werden in der Sprache des Spiel-Clients vorgelesen.

## Hinweise

- Dieses Plugin läuft über **Dalamud/XIVLauncher**, das außerhalb der
  offiziellen Nutzungsbedingungen von Square Enix liegt. Die Nutzung
  erfolgt auf eigene Verantwortung.
- **vnavmesh** ist ein eigenständiges Fremd-Plugin
  ([github.com/awgil/ffxiv_navmesh](https://github.com/awgil/ffxiv_navmesh))
  und wird hier nur verlinkt bzw. nachgeladen, nicht mitverteilt.

## Für Entwickler

- Plugin-Quellcode: `FF14Accessibility/`
- Installer-Quellcode: `Installer/`
- Custom Plugin Repository (für sehende Helfer, optionaler Weg): `repo.json`
- Projektstand und Testprotokoll: `STATUS.md`
- Verifizierte Spiel-Interna: `docs/game-api.md`
