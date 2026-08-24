# navmeshgaps

Findet die Stellen, an denen vnavmeshs Wegenetz auseinanderfällt — **offline, aus
den Cache-Dateien**, ohne dass jemand hinlaufen muss. Genau dafür ist es gebaut:
wer nichts sieht, kann eine Lücke nicht suchen gehen.

```
navmeshgaps [zonen-filter] [--gap <m>] [--drop <m>] [--min <polygone>]
```

- `zonen-filter` — Teilstring des Cache-Dateinamens, z. B. `s1t2` für Limsa
- `--gap` — größte waagerechte Lücke, die gemeldet wird (Vorgabe 1,5 m)
- `--drop` — größter Höhenunterschied (Vorgabe 1,0 m)
- `--min` — Flächen unter dieser Polygonzahl ignorieren (Vorgabe 10)

Am Ende steht eine Rangliste über alle Zonen, sortiert danach, **wie viel Fläche
eine Verbindung erschließen würde** — nicht danach, wie schmal die Lücke ist.

## Warum solche Lücken entstehen

Aus vnavmeshs eigenem Baucode: `NavmeshBuilder.cs:74` rechnet
`walkableRadius = ceil(AgentRadius / CellSize)` = 2 Zellen, und
`RcAreas.ErodeWalkableArea` (Zeile 235) nimmt die von **jeder** Kante weg. Ein
Steg unter einem Meter Breite ist danach verschwunden.

Die beiden eingebauten Reparaturen greifen dort nicht: `EDGE_CLIMB_DOWN` deckt
−3,2 bis −1,5 m ab, `EDGE_JUMP` −500 bis −1,5 m. Beide verlangen mindestens
1,5 m Höhenunterschied, und beide sind standardmäßig aus. Eine Planke mit 0,5 m
Stufe fällt durch jedes Raster.

vnavmesh hat einen Namen für die Abhilfe: `Navmesh.AreaId.Shortcut` ist im
Quellcode dokumentiert als *„walking through a gap that recast thinks is too
narrow"*. Behoben wird das von Hand, als `LinkPoints` in
`Customizations/Z<id><Name>.cs`.

## Die Falle, die das Werkzeug umgeht

**Die Cache-Datei enthält die handgeschriebenen Links nicht.** `NavmeshManager`
schreibt die Datei zuerst und ruft `CustomizeMesh` erst danach (Zeilen 314–316),
und nach dem Laden noch einmal (289–290). Die Links existieren also nur zur
Laufzeit.

Deshalb spielt navmeshgaps die passende Zonen-Anpassung selbst ein, bevor es
zählt. Ohne das würde es die Limsa-Planke vorschlagen, die seit August gelinkt
ist. Gegenprobe: Ohne Anpassung meldet es dort zwei Flächen mit 1468 und 129
Polygonen und schlägt die Verbindung vor; mit Anpassung ist es **eine** Fläche
mit 1601 Polygonen, und der Vorschlag ist weg.

Die Zuordnung Cache-Datei → Territory läuft über das TerritoryType-Sheet
(Bg-Pfad, Schrägstriche durch Unterstriche ersetzt). Fehlt das sqpack, sagt das
Werkzeug ausdrücklich, dass die Anpassungen fehlen — dann sind bereits gelinkte
Stellen in der Ausgabe.

## Es entscheidet nichts

Jede Zeile ist ein **Vorschlag**. Zwei Flächen einen halben Meter auseinander
sind manchmal eine Laufplanke und manchmal ein Geländer, über das niemand
klettern soll. Das kann Geometrie allein nicht unterscheiden — automatisch
erzeugte Verbindungen sind genau das, was den Spieler in V5.78 auf einem Plateau
eingesperrt hat.

Vorgehen deshalb: Kandidat aussuchen → mit `tools/zone-probe` nachsehen, was dort
tatsächlich steht → `LinkPoints` in die Zonen-Anpassung schreiben → deren
`Version` hochzählen, damit vorhandene Caches neu gebaut werden.

## Voraussetzungen

- `DALAMUD_HOME` gesetzt (für Lumina und dessen Abhängigkeiten)
- vnavmesh installiert — die DLL wird **nicht** mitkopiert, sondern zur Laufzeit
  aus `devPlugins\vnavmesh` geladen. Absicht: Es muss die Fassung sein, die diese
  Caches geschrieben hat. Über `VNAV_DIR` umstellbar
- Cache unter `pluginConfigs\vnavmesh\meshcache`, über `VNAV_CACHE` umstellbar
- sqpack-Pfad über `FFXIV_SQPACK`, sonst die Steam-Vorgabe

```
$env:DALAMUD_HOME = "C:\Users\<name>\AppData\Roaming\XIVLauncher\addon\Hooks\dev"
dotnet build tools\navmesh-gaps\navmeshgaps.csproj -c Release
```
