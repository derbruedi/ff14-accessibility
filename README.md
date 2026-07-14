# FF14 Accessibility

Ein Dalamud-Plugin, das **FINAL FANTASY XIV für blinde Spieler** per NVDA und
Tastatur zugänglich macht: Menüs, Dialoge, Quests, Navigation, Inventar, Kampf
und automatisches Laufen werden vorgelesen bzw. per Ton dargestellt.

## Installation für blinde Nutzerinnen und Nutzer (mit Screenreader)

Es gibt jetzt einen grafischen Installer mit einem einzigen Button. Er richtet
alles ein und hält das Plugin aktuell – **ohne** dass du Dalamuds
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
6. Starte XIVLauncher und logge dich ins Spiel ein – das Plugin ist aktiv und
   meldet sich beim Login mit einer gesprochenen Versionsansage.

### Update

Für ein Update reicht es, `FF14AccessibilityInstaller.exe` erneut
auszuführen und wieder den Button „Installieren oder Aktualisieren" zu
aktivieren. Er überschreibt die Plugin-Dateien, und der nächste Spielstart
lädt die neue Version.

### Was der Installer installiert

- Prüft, ob **XIVLauncher** installiert ist, und bietet sonst an, das offizielle
  Setup herunterzuladen und zu starten.
- Kopiert die Plugin-Dateien in Dalamuds `devPlugins`-Ordner und aktiviert sie
  direkt in `dalamudConfig.json` (mit Sicherung, BOM-frei).
- Bietet an, das **vnavmesh**-Plugin (für den Auto-Lauf) vom Original
  herunterzuladen. vnavmesh stammt von einem anderen Autor und wird **nicht** von
  diesem Projekt mitverteilt.

## Tastenübersicht (Standard)

- **N** / **Umschalt+N** – nächstes / vorheriges Objekt ansagen und anvisieren
- **Strg+N** / **Strg+Umschalt+N** – Objekt-Kategorie wechseln / Gehhilfe an-aus
- **Nummernblock 3** – automatisch zum Ziel laufen (braucht vnavmesh)
- **F** – zum Ziel drehen, **W** – laufen
- **Strg+H** – HP und MP ansagen
- **Strg+L** – Stufe und fehlende Erfahrung
- **Strg+F1** – Hilfe, **Strg+F2** – aktives Fenster, **Strg+F3** – Inventar
- **Strg+F9** – Aktionsleiste vorlesen, **Strg+F10** – Menü vorlesen
- **Strg+F11** – Sprache stoppen, **Strg+F5** – UI-Dump (Diagnose)
- **Umschalt+F3** – Gil, **Umschalt+F4/F5/F6** – Emote zurück/vor/ausführen

## Hinweise

- Dieses Plugin läuft über **Dalamud/XIVLauncher**, das außerhalb der offiziellen
  Nutzungsbedingungen von Square Enix liegt. Die Nutzung erfolgt auf eigene
  Verantwortung.
- **vnavmesh** ist ein eigenständiges Fremd-Plugin
  (github.com/awgil/ffxiv_navmesh) und wird hier nur verlinkt/nachgeladen, nicht
  mitverteilt.

## Für Entwickler

- Plugin-Quellcode: `FF14Accessibility/`
- Installer-Quellcode: `Installer/`
- Custom Plugin Repository (für sehende Helfer, optionaler Weg): `repo.json`
