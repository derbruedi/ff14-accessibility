# FF14 Accessibility

Ein Dalamud-Plugin, das **FINAL FANTASY XIV für blinde Spieler** per NVDA und
Tastatur zugänglich macht: Menüs, Dialoge, Quests, Navigation, Inventar, Kampf
und automatisches Laufen werden vorgelesen bzw. per Ton dargestellt.

## Installation und Updates (der einfache Weg)

Das Installer-Programm richtet alles ein und hält das Plugin aktuell – **ohne**
dass du Dalamuds Plugin-Fenster (das ein Screenreader nicht vorliest) bedienen
musst.

1. Lade das aktuelle Installer-Paket aus den Releases herunter und entpacke es.
2. Führe `FF14AccessibilityInstaller.exe` aus. Das Programm ist eine
   Konsolenanwendung – NVDA liest die Ausgaben automatisch mit.
3. Folge den Ansagen. Beim **ersten Mal** wirst du ggf. gebeten, das Spiel einmal
   über XIVLauncher zu starten und den Installer danach erneut auszuführen (damit
   das Plugin scharfgeschaltet wird).
4. Für ein **Update** reicht es, den Installer erneut auszuführen – er überschreibt
   die Plugin-Dateien, und der nächste Spielstart lädt die neue Version.

Beim Login meldet sich das Plugin mit einer gesprochenen Versionsansage.

## Was der Installer macht

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
