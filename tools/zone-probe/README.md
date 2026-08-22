# zoneprobe

Was steht auf diesen Koordinaten? Läuft **offline gegen das installierte
sqpack** — das Spiel muss nicht laufen, und es braucht keine Debug-Sonde.

```
zoneprobe <territoryId> <x> <z> <radius> [y]
```

Beispiel — die Stelle in Neu-Gridania, an der der Auto-Lauf zum Übergang nach
Tiefer Wald stehenblieb:

```
zoneprobe 132 154.5 152.0 10 -12.9
```

Listet jedes Layout-Objekt im Umkreis, nach Entfernung sortiert und nach Typ
gruppiert. Gibt `y` an, steht in jeder Zeile zusätzlich der Höhenunterschied.

## Warum das die richtige Quelle ist

Am dekompilierten Code geprüft (`SceneExtractor.cs:165/177/305`): vnavmesh nimmt
das lebende Layout aus dem Spielspeicher plus die `.pcb`-Kollisionsdateien aus
dem sqpack. Eine Wege-Datenbank gibt es nirgends — Recast voxelisiert diese
Geometrie zur Laufzeit zu einer begehbaren Fläche.

Steht hier also ein Objekt mit Kollision, ist es auch für das Wegenetz ein
Hindernis. **Der Umkehrschluss gilt nicht.** Zwei Grenzen, beide gemessen:

- Welche Ebenen aktiv sind, entscheidet das Spiel zur Laufzeit. `QST_*`-Layer
  hängen am Questfortschritt — die Datei sagt nur, dass es sie gibt.
- Vieles trägt gar keine eigene Kollision im Layout. Gegenprobe an der
  Laufplanke der Astalicia (Limsa, Territory 129, 2026-08-22): 235 Objekte im
  8-m-Umkreis, davon praktisch alle `collision=None`; die Schiffsmodelle
  (`bg_ex_ships`, `bg_ex_ship_blue1`) führen keine `.pcb`. Die begehbare Fläche
  steckt dort in der Modellgeometrie, die aus dem laufenden Prozess kommt
  (`SceneDefinition` kennt nur `FillFromActiveLayout`).

Kurz: zoneprobe beweist Hindernisse, keine Abwesenheit von Hindernissen. Für
freistehendes Mobiliar (Fässer, Kisten, Tore) trägt es, für Schiffs- und
Gebäudegeometrie nicht.

## Worauf es in der Ausgabe ankommt

- `collision=Box` oder `collision=Replace (...pcb)` — das Objekt blockiert.
  `collision=None` ist reine Deko und stoppt niemanden.
- `CollisionBox` mit `pushPlayerOut` — eine unsichtbare Wand, die den Spieler
  aktiv hinausschiebt. Achtung auf den Layer-Namen: Ebenen wie `QST_*` schaltet
  das Spiel je nach Questfortschritt ein und aus, sie sind also nicht immer aktiv.
- `scale` ist bei Trigger-Boxen das **Halbmaß**, nicht das Vollmaß. Am
  2026-08-22 an zwei Zonengrenzen in beide Richtungen gemessen (ZoneExitProbe).

## Voraussetzungen

`DALAMUD_HOME` muss gesetzt sein (für `Lumina.dll`). Der sqpack-Pfad steht als
Vorgabe im Programm und lässt sich über die Umgebungsvariable `FFXIV_SQPACK`
überschreiben.

```
$env:DALAMUD_HOME = "C:\Users\<name>\AppData\Roaming\XIVLauncher\addon\Hooks\dev"
dotnet build tools\zone-probe\zoneprobe.csproj -c Release
```
