# Konzept: Manuelle Navigation entlang des vnavmesh-Pfads

Stand: Recherche 2026-07-15, Bezug FF14Accessibility V4.61. Reine
Machbarkeitsanalyse, keine Code-Änderung.

## Kurzfassung / Machbarkeits-Aussage

Geht, mit Einschränkungen. vnavmesh liefert bereits heute genau die
Wegpunktliste, die für eine manuell geführte Navigation gebraucht wird
(`Nav.Pathfind`), OHNE dass eine Bewegung ausgelöst wird. Das Plugin hat
mit BeaconService, CueService und der bestehenden Gehhilfe (NavigationService)
schon alle Audio-Bausteine, die für die Wegpunkt-Führung nötig sind. Die
Umsetzung ist im Kern eine Erweiterung der bestehenden Gehhilfe um eine
Wegpunktliste statt eines einzelnen Zielvektors - kein Neubau.

Einschränkungen: Dynamische Hindernisse (andere Spieler, Monster) kennt das
Navmesh nicht, nur die feste Geländeform. Echte Sprünge über Lücken
(Jump-Puzzles) bildet vnavmesh in der Regel nicht ab. Beides wird unten
genauer bewertet.

## 1. vnavmesh-IPC: Pfadberechnung ohne Auto-Bewegung

### Was das Projekt schon nutzt

`AutoWalkService.cs` nutzt aktuell nur bewegungsauslösende und
Status-Endpunkte:

```73:110:H:\ff14\FF14Accessibility\Services\AutoWalkService.cs
        _navIsReady         = pluginInterface.GetIpcSubscriber<bool>("vnavmesh.Nav.IsReady");
        _navBuildProgress   = pluginInterface.GetIpcSubscriber<float>("vnavmesh.Nav.BuildProgress");
        _moveCloseTo        = pluginInterface.GetIpcSubscriber<Vector3, bool, float, bool>("vnavmesh.SimpleMove.PathfindAndMoveCloseTo");
        _pathStop           = pluginInterface.GetIpcSubscriber<object>("vnavmesh.Path.Stop");
        _pathIsRunning      = pluginInterface.GetIpcSubscriber<bool>("vnavmesh.Path.IsRunning");
        _pathfindInProgress = pluginInterface.GetIpcSubscriber<bool>("vnavmesh.SimpleMove.PathfindInProgress");
```

`SimpleMove.PathfindAndMoveCloseTo` berechnet den Pfad UND lässt vnavmesh
den Charakter automatisch laufen (`OverrideMovement`/`OverrideCamera` intern,
siehe `FollowPath.cs`). Das ist für unseren Zweck der falsche Endpunkt - er
nimmt dem Spieler die Tasten aus der Hand.

`Path.ListWaypoints` wird bereits abonniert, aber nur diagnostisch (Logging),
nicht für eine eigene Führung:

```41:45:H:\ff14\FF14Accessibility\Services\AutoWalkService.cs
    // DIAGNOSTIC (temporary): the waypoints of the path vnavmesh is actually
    // following. Lets us tell whether the destination is reachable ...
    private readonly ICallGateSubscriber<List<Vector3>> _pathListWaypoints;
```

### Der entscheidende Fund: `Nav.Pathfind`

Quellcode-Abgleich gegen `awgil/ffxiv_navmesh` (IPCProvider.cs, aktueller
`master`-Stand) zeigt einen reinen Query-Endpunkt, der KEINE Bewegung
auslöst:

```
RegisterFunc("Nav.Pathfind", (Vector3 from, Vector3 to, bool fly)
    => navmeshManager.QueryPathBasic(from, to, fly));
RegisterFunc("Nav.PathfindWithTolerance", (Vector3 from, Vector3 to, bool fly, float range)
    => navmeshManager.QueryPathBasic(from, to, fly, range));
RegisterFunc("Nav.PathfindInProgress", () => navmeshManager.PathfindInProgress);
RegisterFunc("Nav.PathfindNumQueued", () => navmeshManager.NumQueuedPathfindRequests);
RegisterAction("Nav.PathfindCancelAll", () => navmeshManager.Reload(true));
```

`QueryPathBasic` (in `NavmeshManager.cs`) läuft asynchron über den
NavMesh-Query-Layer (`Query.PathfindMesh`/`PathfindVolume`,
String-Pulling optional aktiv) und gibt eine reine
`List<Vector3>` zurück - exakt die Wegpunktliste, die für eine
Audio-Führung gebraucht wird. Es wird an keiner Stelle `FollowPath.Move()`
aufgerufen, also bleibt die Bewegung vollständig beim Spieler.

Signatur für den Dalamud-Subscriber (analog zu den bestehenden Mustern in
`AutoWalkService.cs`):

```csharp
ICallGateSubscriber<Vector3, Vector3, bool, List<Vector3>> navPathfind =
    pluginInterface.GetIpcSubscriber<Vector3, Vector3, bool, List<Vector3>>("vnavmesh.Nav.Pathfind");
```

`docs/game-api.md` hat diesen Endpunkt bereits als offenen Punkt notiert
("`Nav.Pathfind(from, to, fly) -> List<Vector3>` (Wegpunktliste!)"), aber
noch nicht produktiv verdrahtet - dieser Auftrag bestätigt ihn per
Quellcode und zeigt den konkreten Weg zur Nutzung.

Wichtig für Re-Routing (Abschnitt 2): `Nav.Pathfind` läuft asynchron
(`PathfindInProgress`/`PathfindNumQueued` zum Abfragen, ob eine neue
Anfrage schon fertig ist) - ein erneuter Aufruf während eine Anfrage noch
läuft muss abgefangen werden, genau wie es `TryStartPath` heute schon für
`SimpleMove` macht.

### Fazit zu Punkt 1

Geht direkt. Kein Umweg über Reflection oder undokumentierte Strukturen
nötig - `Nav.Pathfind` ist ein offiziell registrierter IPC-Gate im
Fremdplugin, mit derselben Subscriber-Technik, die das Projekt bereits für
sechs andere vnavmesh-Endpunkte einsetzt.

## 2. Führungs-Konzept: Wegpunkte statt Luftlinie

### Was heute schon existiert (Gehhilfe / "Walk Guide")

Die bestehende Gehhilfe in `NavigationService.cs` macht bereits fast alles
Nötige - nur mit einem einzigen Zielpunkt statt einer Wegpunktliste:

```504:537:H:\ff14\FF14Accessibility\Services\NavigationService.cs
    private void WalkGuideFrame(IGameObject player)
    {
        var obj = _objectTable.FirstOrDefault(o => o.GameObjectId == _walkTargetId);
        ...
        var distance = Vector3.Distance(player.Position, obj.Position);
        if (distance <= ArrivalDistance)
        {
            StopWalkGuide();
            _tolk.SpeakInterrupt($"Ziel erreicht: {_walkTargetName}.");
            return;
        }

        var relAngle = RelativeAngle(player, obj.Position);
        _beacon.Update(relAngle, distance);
        ...
        _tolk.SpeakInterrupt($"{FormatDistance(distance)}, {DirectionText(relAngle)}.");
    }
```

Das ist heute eine reine Luftlinien-Führung: Der Ton zeigt immer direkt zum
Endziel, auch wenn dazwischen eine Wand, ein Abhang oder ein Gewässer
liegt. Genau das soll die Wegpunkt-Führung beheben.

### Konzept: Wegpunkt-Zustandsmaschine

Kernidee: `WalkGuideFrame` bekommt statt eines einzelnen Zielpunkts eine
Liste `List<Vector3> _pathWaypoints` plus einen Index `_pathIndex`. Ablauf:

1. Beim Start der Gehhilfe (`ToggleWalkGuide`): `Nav.Pathfind(spielerPos,
   zielPos, fly:false)` aufrufen (asynchron, `PathfindInProgress`
   abwarten wie bei `SimpleMove` heute schon üblich).
2. Jeden Frame: Beacon zeigt auf `_pathWaypoints[_pathIndex]` (nicht mehr
   aufs Endziel), über dieselbe `BeaconService.Update(relAngle, distance)`-
   Methode wie heute.
3. Ist der Spieler näher als ein Ankunftsradius (z. B. 2-3 m, analog zur
   bestehenden `ArrivalDistance = 3f`) am aktuellen Wegpunkt: Index um
   eins erhöhen. Beim letzten Wegpunkt gilt der bestehende
   "Ziel erreicht"-Pfad.
4. Sprachansage weiterhin alle 2 Sekunden, aber jetzt bezogen auf den
   Gesamt-Fortschritt ("noch 3 Abschnitte, 45 Meter gesamt" oder einfacher
   nur Richtung/Distanz zum aktuellen Wegpunkt, siehe Abschnitt 5).

Das ist strukturell sehr nah an dem, was vnavmeshs eigener `FollowPath.cs`
intern tut (Wegpunkt abarbeiten, `Tolerance` für "erreicht", nächster
Wegpunkt) - nur dass statt `OverrideMovement` unser bestehender
Beacon/Tolk-Mechanismus die Wegpunkte an den Menschen weitergibt.

### Abweichung vom Pfad / Re-Routing

Wenn der Spieler seitlich vom vorgegebenen Pfad abkommt (z. B. weil er der
Ansage nicht exakt folgt oder ausweicht), zeigt der reine
"nächster-Wegpunkt-erreicht"-Test irgendwann falsch: Der Spieler kann
neben dem Pfad an einem Wegpunkt "vorbeischrammen", ohne dass eine
Ankunft erkannt wird, oder er entfernt sich strukturell vom berechneten
Weg.

Zwei ergänzende Prüfungen (beide mit vorhandenen Mitteln umsetzbar, ohne
neue IPC-Endpunkte):

- Lot-Abstand zur aktuellen Wegpunkt-Strecke: Analog zu vnavmeshs eigener
  `DistanceToLineSegment`-Prüfung in `FollowPath.cs` kann geprüft werden,
  wie weit der Spieler seitlich von der Linie (vorheriger Wegpunkt →
  aktueller Wegpunkt) abweicht. Überschreitet das einen Schwellwert
  (z. B. 5-8 m), gilt der Pfad als "verlassen".
- Bewegungs-Stillstand wie im bestehenden Auto-Lauf-Wächter
  (`AutoWalkService.Update`, Positions-Delta < 0,5 m für mehrere Sekunden)
  kann eine Verkeilung/Blockade erkennen.

Bei "Pfad verlassen" wird einfach `Nav.Pathfind` erneut von der aktuellen
Spielerposition zum unveränderten Endziel aufgerufen und die
Wegpunktliste ersetzt - kein Sonderfall, dieselbe Funktion wie beim Start.
Das entspricht genau dem, was der User unter "Re-Routing" meint, und ist
mit der bereits vorhandenen Async-Best-Practice (`PathfindInProgress`
prüfen, keine Doppel-Anfrage) machbar.

### Fazit zu Punkt 2

Geht, als Erweiterung der bestehenden Gehhilfe (kein neues Feature von
Grund auf). Größter Umbau: `WalkGuideFrame` von "ein Zielpunkt" auf "Liste
+ Index" umstellen: Das ist überschaubarer Zustandsmaschinen-Code, keine
neue Audio- oder IPC-Technik.

## 3. Hindernisse: Reicht Wegpunkt-Führung aus?

### Statische Hindernisse: ja, das übernimmt das Navmesh schon

Das Navmesh wird pro Zone einmal aus der Level-Geometrie gebaut
(`NavmeshBuilder`, gecacht in `NavmeshManager.BuildNavmesh`) und bildet nur
begehbare Flächen ab. Wände, Klippen, Gebäude, Wasser (je nach
Zonen-Customization) sind darin schon "ausgespart" - ein Pfad von
`Nav.Pathfind` führt niemals gegen eine feste Wand. Für stehende
Umgebungsgeometrie ist die Wegpunkt-Führung daher tatsächlich eine
vollwertige Hindernis-Umgehung, ohne dass das Plugin selbst irgendetwas
über Geometrie wissen muss.

### Dynamische Hindernisse: nein, das kennt das Navmesh nicht

Das Mesh wird einmal beim Zonenbetreten gebaut und ändert sich danach
nicht mehr live. Andere Spieler, Monster, NPCs, die gerade im Weg stehen,
sind für `Nav.Pathfind` unsichtbar - der berechnete Pfad kann mitten durch
eine Gruppe Monster oder einen anderen Spieler führen. Das ist keine
Einschränkung, die sich durch bessere Nutzung von vnavmesh beheben lässt;
sie liegt am Funktionsprinzip des Werkzeugs.

Realistische Einordnung: Das ist kein neues Problem gegenüber dem
heutigen Auto-Lauf - der stolpert an genau denselben Stellen (deshalb
existiert der Stillstands-Wächter in `AutoWalkService`). Beim manuellen
Laufen ist die Situation für einen blinden Spieler eher LEICHTER als beim
Auto-Lauf: Er merkt den Bump/Stopp durch die Kollision sofort über die
normale Spielsteuerung (Charakter bleibt stehen, Bewegungstasten wirken
nicht mehr weiter) und kann reagieren - er braucht keine Umweg-Erkennung
per Diagnose-Log wie beim Auto-Lauf.

### Sinnvolle Zusatz-Option (kein Navmesh-Thema, sondern ObjectTable)

Eine echte Nahbereichs-Warnung ("Monster 2 Meter voraus") wäre technisch
NICHT über vnavmesh lösbar, sondern nur über einen zusätzlichen Scan der
`ObjectTable` in Blickrichtung (Distanz + Winkel-Kegel vor dem Spieler,
ähnlich der bereits vorhandenen `CalculateDirection`-Logik). Das ist
machbar, aber ein eigenständiges Feature losgelöst von der
Wegpunkt-Führung - siehe Aufwandsschätzung, Komfort-Stufe. Es deckt sich
mit dem in `docs/verbesserungsvorschlaege.md` bereits skizzierten
"Objekt-Radar"-Gedanken und sollte nicht mit der eigentlichen
Pfad-Wegpunkt-Führung vermischt werden.

### Fazit zu Punkt 3

Wegpunkt-Führung allein reicht für statische Hindernisse voll aus. Für
dynamische Hindernisse ist sie prinzipbedingt blind - das ist eine reale,
aber im Vergleich zum heutigen Auto-Lauf nicht neue Einschränkung, und für
den MVP tragbar.

## 4. Vertikalität: Sprünge, Erhöhungen, enge Passagen

### Granularität der Wegpunkte

`Nav.Pathfind` läuft mit aktiviertem String-Pulling
(`NavmeshManager.UseStringPulling = true`), das heißt: Wegpunkte liegen
NICHT in festen Abständen, sondern nur dort, wo sich die Richtung ändert
(Ecken, Kurven, Treppenabsätze). Eine lange gerade Strecke ergibt einen
einzigen Wegpunkt über zig Meter, eine Treppe oder ein verwinkelter Gang
erzeugt entsprechend mehr Punkte. Das passt gut zur bestehenden
Beacon-Logik: Zwischen zwei Wegpunkten übernimmt der kontinuierliche
Ton/Richtungshinweis die Feinführung, genau wie heute schon bei einem
einzelnen Fernziel.

### Höhenunterschiede

Jeder Wegpunkt ist ein vollständiger `Vector3` (X, Y, Z) - Höhenänderungen
zwischen zwei aufeinanderfolgenden Wegpunkten lassen sich direkt aus der
Differenz der Y-Werte ablesen. Konzept: Überschreitet die Y-Differenz
zwischen aktuellem und nächstem Wegpunkt einen Schwellwert (z. B. ±1,5 m),
zusätzlich zur Richtungsansage ein kurzes "aufwärts" / "abwärts" bzw.
"Stufen" einblenden. Das ist reine Vektor-Arithmetik auf bereits
vorhandenen Daten, kein neuer IPC-Bedarf.

### Echte Sprünge (Lücken, Jump-Puzzles)

Hier liegt die eigentliche Grenze: vnavmesh bildet nur BEGEHBARE Flächen
ab. Eine Lücke, die nur per Sprung überwunden werden kann (z. B. manche
Schatzkarten-Spots, Jump-Puzzles), ist im Mesh in aller Regel NICHT
verbunden - `Nav.Pathfind` findet dafür schlicht keinen Weg (genau das im
Auto-Lauf bekannte "kein Weg gefunden", siehe
`AutoWalkService.BuildNoPathHint`). Eine Sonderbehandlung dafür existiert
bereits intern in vnavmesh nur für automatisch generierte Sprung-Pfade in
Dungeons (`Navmesh.AreaId.ClientPath`/`Warp` in `FollowPath.CheckCondition`,
dort mit automatischer Sprung-Taste für die AUTO-Bewegung) - das gilt
nicht für frei begehbare Open-World-Lücken und ist für die manuelle
Führung ohnehin irrelevant, weil der Mensch selbst springt.

### Fazit zu Punkt 4

Geht mit Einschränkung: Normale Geländestufen, Treppen, Rampen sind über
die Y-Differenz zwischen Wegpunkten gut ansagbar. Echte Sprung-Lücken
bleiben wie beim heutigen Auto-Lauf unerreichbar - das ist eine Grenze von
vnavmesh selbst, keine Lücke in unserer Umsetzung.

## 5. UX-Skizze

### Tastenbelegung: keine neue Taste nötig

Der Umschalter zwischen Auto-Lauf und manuell geführtem Laufen existiert
bereits: `Numpad3` (Auto-Lauf, `AutoWalkService.Toggle`) und
`Umschalt+Numpad3` (Gehhilfe, `NavigationService.ToggleWalkGuide`). Die
Wegpunkt-Führung ist konzeptionell eine Erweiterung der bestehenden
Gehhilfe (nicht ein dritter Modus), daher bleibt die Tastenbelegung aus
`Configuration.cs` unverändert:

- `N` / `Umschalt+N`: Objekt bzw. Wegpunkt/Questziel auswählen (wie
  bisher).
- `Numpad3`: Auto-Lauf (Spiel bewegt sich selbst).
- `Umschalt+Numpad3`: Gehhilfe an/aus - ab jetzt pfadbasiert statt
  Luftlinie.
- `F`: zum aktuellen (Zwischen-)Ziel hindrehen (bestehende Spieltaste,
  unverändert nutzbar).
- `W`/`R`: manuell laufen/rennen (bestehende Spieltasten, unverändert).

Optional könnte man später eine zusätzliche Taste für "harte Luftlinie
statt Pfad erzwingen" anbieten (Debug-/Fallback-Fall), das ist aber kein
MVP-Bestandteil.

### Was der Nutzer Schritt für Schritt hört

1. `N` mehrfach: Ziel wählen (NPC, Wegpunkt, Questziel - wie heute).
2. `Umschalt+Numpad3`: Gehhilfe an. Ansage z. B. "Gehhilfe an: Aetheryt
   Limsa Lominsa, Weg wird berechnet." Kurze Pause während
   `Nav.Pathfind` asynchron rechnet (typischerweise deutlich unter einer
   Sekunde für normale Zonen-Distanzen).
3. Beacon-Ton startet, gerichtet auf den ERSTEN Wegpunkt (nicht das
   Endziel) - Tonhöhe/Pan wie gewohnt aus `BeaconService`.
4. Der Nutzer dreht sich per `F` oder Gehör zum Ton hin, läuft mit `W`.
5. Alle 2 Sekunden Sprachansage zum aktuellen Wegpunkt: "12 Meter,
   geradeaus." (identisch zum heutigen Format, nur bezogen auf den
   Zwischenpunkt statt das Endziel.)
6. Bei größerem Höhenunterschied zum nächsten Wegpunkt zusätzlich:
   "Weg geht jetzt aufwärts."
7. Erreicht der Nutzer den aktuellen Wegpunkt (Ankunftsradius), wechselt
   der Beacon automatisch, leise/ohne Sprachunterbrechung, auf den
   nächsten Wegpunkt - idealerweise mit einem kurzen, unaufdringlichen
   Übergangston aus `CueService` (analog zum bestehenden Ziel-Ton), damit
   der Nutzer den Wechsel bemerkt, ohne dass eine ganze Ansage
   dazwischenfunkt.
8. Weicht der Nutzer merklich vom Pfad ab: kurze Ansage "Weg wird neu
   berechnet" und stiller Wechsel auf die neue Wegpunktliste (Beacon
   zeigt sofort auf den neuen ersten Punkt).
9. Am letzten Wegpunkt: bestehende Ankunfts-Ansage "Ziel erreicht:
   <Name>." (unverändert aus `WalkGuideFrame`).

## 6. Aufwandsschätzung

### MVP (Kernfunktion, spielbar)

- IPC-Subscriber für `Nav.Pathfind` (und `Nav.PathfindInProgress`) analog
  zu den sechs bestehenden vnavmesh-Subscribern ergänzen.
- `NavigationService.WalkGuideFrame` von Einzelziel auf Wegpunktliste +
  Index umbauen (Ankunftsradius pro Wegpunkt, Weiterschalten,
  Endziel-Erkennung wie gehabt).
- Re-Pathfind-Auslöser: Lot-Abstand zur aktuellen Strecke (einfache
  Vektor-Formel, analog zu vnavmeshs eigener `DistanceToLineSegment`) plus
  Wiederverwendung der bestehenden Stillstands-Erkennung.
- Bestehende Sprach-/Ton-Bausteine (`BeaconService`, `TolkService`)
  unverändert weiterverwenden, nur mit neuem Zielpunkt pro Frame gefüttert.
- Test an mehreren bekannten Strecken (gerade, um die Ecke, Treppe,
  bekannte Bridge-Trap-Stelle aus dem Auto-Lauf-Log).

Einschätzung: kleiner bis mittlerer Aufwand - die Async-IPC-Handhabung,
die Beacon-Logik und die Stillstands-Erkennung existieren bereits im
Projekt und müssen "nur" umverdrahtet werden, nicht neu erfunden.

### Komfort (Ausbaustufen danach)

- Höhenansage bei Wegpunkt-Übergängen ("aufwärts"/"abwärts"/"Stufen") aus
  der Y-Differenz zwischen Wegpunkten.
- Kurzer Übergangston beim Wegpunktwechsel über `CueService` statt reiner
  Sprachansage (weniger Sprach-Unterbrechungen unterwegs).
- Fortschrittsansage auf Wunsch ("noch 3 Abschnitte / 80 Meter gesamt").
- Konfigurierbare Ankunftsradien je nach Zielart (analog zu den
  bestehenden `AutoWalkPlaceStopRange`/`AutoWalkTransitionStopRange` in
  `Configuration.cs`).
- Eigenständiges, separates Feature (nicht Teil der Pfad-Führung selbst):
  Nahbereichs-Warnung vor Personen/Monstern in Blickrichtung über
  `ObjectTable`-Scan - mittlerer Aufwand, siehe auch der verwandte
  Vorschlag in `docs/verbesserungsvorschlaege.md`
  ("Mehrere gleichzeitige Signaturtöne").
- Wärmer/Kälter-Zusatzsignal zur Pfadtreue (kontinuierliches Feedback,
  wie nah der Spieler an der berechneten Linie bleibt, nicht nur am
  Wegpunkt) - eher ein Feinschliff als eine Notwendigkeit, da der
  Lot-Abstand-Check aus dem MVP bereits die Grundsicherung übernimmt.
