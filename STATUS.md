# FF14 Accessibility Plugin - Projektstatus

## Ziel
Dalamud-Plugin für FF14 das blinden Spielern via NVDA/TOLK ermöglicht das Spiel vollständig per Tastatur zu spielen.

## STAND JETZT (2026-08-23, "ZIEL UNTER EINEM VORSPRUNG - GEBAUT, UNGETESTET")

>>> NEU UND NOCH NICHT IM SPIEL GEPRUEFT: Der Auto-Lauf setzte den Spieler aufs
    Bruecken-DECK, wenn das Ziel darunter stand. Aufgefallen an der Quest
    "Auf der Flucht" (Stufe 44, Coerthas, Territory 155): der NPC Wedge steht
    bei (-397,5|229,15|448,3) unter der Bruecke r1f1_s1_brdg1.

    GEMESSEN, nicht vermutet (Werkzeuge: tools/navmesh-gaps --at, plus ein neues
    heightprobe im Scratchpad, das einen senkrechten Strahl durch die
    Kollisionsgeometrie schiesst):
    - Durchgangshoehe an Wedges Standort 1,25 m. vnavmesh verlangt AgentHeight
      2,0 m und wirft alles darunter weg (NavmeshBuilder.cs:73/221).
    - Der Verlauf: Z 446 noch 2,33 m, Z 447 nur noch 1,80 m - und genau dort
      endet auch das Netz. Zwei unabhaengige Messungen, dieselbe Grenze.
    - Das naechste Netz an Wedges Koordinaten ist das Deck 2,85 m ueber ihm.

    GEBAUT: `TryStepOutFromUnderCeiling` in AutoWalkService. Liegt das Netz am
    Ziel mindestens 1,5 m hoeher, sucht es in Ringen (2,5 m Schritte, 8 Speichen,
    bis 15 m) einen Boden auf Zielhoehe und laeuft dorthin. Ansage beim Start und
    bei der Ankunft nennt Vorsprung, Restentfernung und Richtung.

    FALLE, DIE DABEI AUFFLOG: Ein enges Hoehen-Halbmass filtert NICHT. Die
    Abfrage mit 15 m / 1,5 m lieferte wieder das Deck 2,65 m hoeher, weil
    Detours FindNearestPoly die Kandidaten ueber Polygon-Bounding-Boxen sucht
    und ein geneigtes Deck-Polygon in die Scheibe hineinragt. Verlaesslich ist
    nur die Pruefung am ERGEBNIS - daher die Ringsuche.

    BEIDE WEGE, nicht nur einer: Der Auto-Lauf (AutoWalkService.Begin) und die
    Gehhilfe (NavigationService.StartWalkGuide) stellen jetzt dieselbe Frage, die
    Methode ist dafuer public. Sonst haette der Peil-Ton weiter aufs Deck gezeigt,
    waehrend der Auto-Lauf unten ankommt. In beiden Faellen wird die Ziel-Id auf 0
    gesetzt (sonst zoege der naechste Frame die Objektposition zurueck) - ein
    Ziel unter einem Vorsprung wird also nicht mehr verfolgt, wenn es laeuft.

    ERWARTUNG FUER DEN TEST (offline gegen den Netz-Cache vorausberechnet):
    Ziel wird (-399,8|228,8|445,0), rund 4,0 m von Wedge, Richtung Suedost.
    Log-Zeile beginnt mit `[Vorsprung]`.

    BESTAETIGT IST BEREITS: Der Punkt unter der Bruecke ist begehbar und Wedge
    von dort ansprechbar - der User ist mit `/vnav moveto -399 228.8 444.6`
    hingelaufen und hat mit ihm geredet.

## RELEASE v5.90 (2026-08-23, "ZWOELF TESTS BESTAETIGT, NAVIGATION IST DRAUSSEN")

>>> DER USER HAT DIE OFFENE-PUNKTE-LISTE DURCHGEARBEITET. Neu angelegt:
    `OFFENE-PUNKTE.txt` in der Repo-Wurzel - alle offenen Punkte aus diesem
    Dokument, den Memory-Notizen und dem Git-Stand, in sechs Teilen und
    durchnummeriert. Er hat Teil 1 mit `#` abgehakt.

    ALS ERLEDIGT BESTAETIGT (2026-08-23): Peil-Ton nur beim Laufen (das war die
    Spielerbeschwerde), HP/MP-Ton-Gegentest, Zonenuebergang mit
    Grenz-Kandidaten, Drehung im Spielgefuehl, AoE-Warnton-Auswahl, Tiefes
    Gewoelbe aus PR 6, Chatlog-Einstellungen, Systemkonfiguration Grafik,
    Chat-Kanaele im Optionsmenue, Jagdziel-Kategorie, Aufgabenliste auf
    Strg+Umschalt+F7, Freibrief-Gegner in der Kategorie.

    NOCH OFFEN in Teil 1: fehlt "geradeaus" in der Sprache (3), Hindernis-Ansage
    (4), Ankunft und Nachfassen (6), Fluchtrichtung bei AoE (10), "schon
    gezaehmt" (11). Punkt 8 betrifft ein privates Fremd-Plugin und steht in
    `PRIVAT.txt`.

>>> NEUE GRUNDREGEL DES USERS: die Fremd-Plugins, die er persoenlich benutzt,
    kommen NIE ins Release - nicht ins Paket, nicht in den Installer, nicht in
    die Doku. Ein frueherer Wunsch, eines davon beim Updaten mitzuinstallieren,
    ist damit ZURUECKGEZOGEN.

    GEPRUEFT UND BEREINIGT: im Repo steckte nichts davon - bis auf zwei
    unbenutzte Namenskonstanten in `Installer/InstallerService.cs`. Sie wurden
    nie aufgerufen, sahen aber aus wie eine halbfertige Vorbereitung. Entfernt,
    und an der neu gebauten EXE mit `strings` gegengeprueft: null Treffer.

    AUSGELAGERT: alles, was die private Einrichtung dieser Plugins beschreibt,
    steht jetzt in `PRIVAT.txt` (in `.gitignore`, wird nie committet). Aus
    diesem Dokument sind die betreffenden fuenf Abschnitte herausgenommen, aus
    `OFFENE-PUNKTE.txt` der zugehoerige Testpunkt. Was hier bleibt, sind
    Erkenntnisse ueber UNSEREN Code - etwa dass unsere Boss-Cast-Ansage hinter
    einem fremden Optionsgatter tot lag. Die gehoeren ins Projekt.

>>> RELEASE v5.90. Versionen synchron gesetzt: csproj 5.90.0(.0), Plugin.cs
    "5.90", repo.json 5.90.0.0, im gebauten Manifest steht AssemblyVersion
    5.90.0.0. Drin ist alles seit v5.89, also die komplette Navigationsarbeit
    vom 22.08. (Bruecken ueber Netzluecken, Zonenuebergaenge, Hindernis-Ansage,
    Ankunft/Nachfassen, Kamera-Versatz) und der 23.08. (Vorzeichenfix,
    Himmelsrichtungen statt links/rechts, Peil-Ton nur beim Laufen und nie
    still, Lautstaerke am Peilpunkt, HP/MP-Ton-Schalter im Menue).

    INSTALLER auf 1.2.1.0 gehoben, obwohl die Aenderung nur die zwei toten
    Konstanten betrifft. BEGRUENDUNG, weil die Regel eigentlich "unveraendert =
    nicht bumpen" lautet: die EXE ist jetzt eine ANDERE Datei als die von
    v5.89. Zwei verschiedene Binaries unter derselben Versionsnummer sind genau
    die Drift, die uns beim 5.51-Fall Nutzer auf einer alten Fassung
    festhaengen liess. Lieber ein Update-Angebot zu viel als eine
    Versionsnummer, die nicht mehr eindeutig ist.

>>> NICHT ANGEFASST, BEWUSST: System.Speech.dll liegt weiter DOPPELT im ZIP
    (oben und unter runtimes/win/lib/net9.0/, je 685 KB, daher 1,53 MB
    Gesamtgroesse). Der Fix waere ein Ausschluss im csproj - das aendert aber
    den Ladepfad einer DLL, an der die SAPI-Warnstimme haengt, und das gehoert
    nicht ungetestet in einen Release. Bleibt Punkt 29 in OFFENE-PUNKTE.txt.

## FRUEHERER STAND (2026-08-23, "PEIL-TON-REGRESSION IN V5.89 GEFUNDEN UND ZURUECKGENOMMEN")

>>> DIE BESCHWERDE AUS DER SPIELERSCHAFT (2026-08-23), woertlich:
    *"this latest update broke the game ... when going fighting the target is
    changing if I press any key during the fight, while the beeping keeps going
    on and on. the way it was before if you wanted to trigger the beacon was
    fine, now its all on every time and you hear it over everything."*

>>> BEFUND 1, DAUERPIEPEN - BELEGT, WAR EINE ECHTE REGRESSION IN V5.89.
    Zwei Dinge griffen ineinander:
      - `Configuration.TargetBeaconEnabled = true`, also Standard AN.
      - `NavigationService` kannte KEINE Kampfabfrage (Suche nach `InCombat`
        und `ConditionFlag` in der Datei: null Treffer).
    `UpdateTargetBeacon` spielte, sobald irgendetwas anvisiert war - und im
    Kampf ist immer etwas anvisiert. Eingefuehrt in 9ec2f24 (2026-08-19), und
    das liegt VOR dem v5.89-Release, war also bei allen Spielern draussen.

>>> DER VORHER-ZUSTAND IST NACHGESEHEN, NICHT ERINNERT. In `9ec2f24^` steht
    `if (_walkGuideActive) WalkGuideFrame(player);` OHNE else-Zweig - vor dem
    19.08. speiste ausschliesslich die Gehhilfe den Ton. Auch der Auto-Lauf
    hatte damals keinen.

>>> ENTSCHEIDUNG DES USERS (2026-08-23): Ton nur noch waehrend eines Laufs.
    Damit ist die eigene Vorgabe vom 2026-08-19 ("die toene fuer getrackte
    sachen sollen auch kommen wenn die gehhilfe aus ist aber dann nur wenn was
    anvisiert ist") ausdruecklich GEKIPPT. Wer das spaeter liest: es war kein
    Versehen, es war eine bewusste Ruecknahme nach Spielerrueckmeldung.

>>> GEBAUT (Debug, 0 Fehler / 0 Warnungen, deployt nach devPlugins):
      - `AutoWalkService.IsWalking` - `IsActive || IsFollowing`, eine Auskunft
        fuer "bewegen sich die Fuesse".
      - `NavigationService.AutoWalk` - Property, nachtraeglich gesetzt, weil
        Plugin.cs den Navigationsdienst frueher baut (wie `DeepDungeon`).
      - Gatter in `UpdateTargetBeacon`: `if (AutoWalk is not { IsWalking: true })`
        -> `_beacon.Idle()`. Idle statt Stop, damit der naechste Lauf ohne
        Geraete-Aussetzer einsetzt.
      - Beschriftung `OptTargetBeacon`: "Peil-Ton auf das Ziel" -> "Peil-Ton
        beim Laufen" / "Navigation beacon". Sonst sucht der Spieler den Fehler
        beim Ton statt beim Lauf.
    NICHT angefasst: der Fluchtton bei AoE (`TryDriveEscapeBeacon`) laeuft
    weiter unabhaengig vom Schalter - das war schon vorher so und ist gewollt.

>>> BEFUND 2, ZIELWECHSEL BEI TASTENDRUCK - BELEGT, ABER AUF WUNSCH DES USERS
    BEWUSST NICHT GEAENDERT. `CycleObject` visiert beim Blaettern das Spielziel
    an (`NavigationService.cs:862`, `_targetManager.Target = obj`), und das
    haengt auf Bild-ab/Bild-auf (`Configuration.cs:28-29`). Wer sich im Kampf
    mit dem Objekt-Browser orientiert, verliert dabei sein Kampfziel. Der User
    hat am 2026-08-23 entschieden: so lassen.

>>> BEFUND 3, UNBESTAETIGT - NICHT ANFASSEN OHNE MESSUNG. Die Zielwechsel-ANSAGE
    liest `_targetManager.Target ?? _targetManager.SoftTarget`
    (`NavigationService.cs:256`), folgt also auch dem Soft-Target. Der TON tut
    das bewusst nicht (`Zeile 399`: *"das wandert beim Vorbeilaufen von NPC zu
    NPC"*). Ob daraus im Kampf zusaetzliche "Ziel: ..."-Ansagen entstehen, ist
    NICHT gemessen. Vor einer Aenderung gehoert da eine Debug-Sonde hin.

>>> ZU TESTEN, in dieser Reihenfolge:
      1. Im Kampf ein Ziel anvisieren, ohne zu laufen. Der Ton muss SCHWEIGEN.
      2. Gehhilfe einschalten. Ton muss kommen wie frueher.
      3. Auto-Lauf (Numpad3) auf ein anvisiertes Ziel. Ton muss kommen und am
         Ende des Laufs aufhoeren.
      4. Optionsmenue: der Schalter heisst jetzt "Peil-Ton beim Laufen".

>>> BEFUND 4 IST GELOEST - ES WAR EIN VORZEICHENDREHER, NICHT DIE KAMERA.
    Der User hat es in-game gemessen (2026-08-23): *"wenn ich nach links laufe
    wird weniger und nach rechts mehr, links und rechts ist vertauscht"*. Das
    Wort "vertauscht" war der Hinweis: ein Kamera-Versatz waere WECHSELHAFT,
    ein Vorzeichenfehler ist KONSTANT.

    DIE URSACHE, hergeleitet aus zwei Angaben, die beide schon im Projekt
    standen - es brauchte kein neues Log:
      - Blickvektor = (sin(rot), cos(rot)), also blickt rot=0 nach +Z.
      - Norden ist -Z, Osten +X (`RouteService.SectorOf` = atan2(dx, -dz),
        0 = Norden; dieselbe Rechnung speist die Himmelsrichtungsansagen).
      - Zusammen: rot=0 blickt nach SUEDEN, denn
        HeadingSector(0) = SectorOf(0, 1) = atan2(0, -1) = 180 Grad.
      - Ein Ziel im Osten (dx>0) ergab mit `atan2 - rot` ein PLUS, also
        "rechts". Wer nach Sueden blickt, hat Osten aber LINKS.

    DAS ERKLAERT AUCH, WARUM SPRACHE UND TON GEMEINSAM FALSCH WAREN: es war nie
    ein Doppelfehler. Der Peil-Ton nimmt `sin(relAngle)` als Stereoseite, haengt
    also an derselben Zahl wie das gesprochene Wort.

    WIE ES SO LANGE UEBERLEBT HAT - der Punkt, der wehtut: im Code stand
    "Vorzeichen per Beacon-Hoertest bestaetigt (2026-07-10)", waehrend in
    `docs/game-api.md` an derselben Sache "OFFEN: Vorzeichen (positiv = rechts
    oder links?)" stand. Der Hoertest kann die Seite nur bestaetigt haben, wenn
    dabei nach NORDEN geblickt wurde - nur dann fallen beide Vorzeichen
    zusammen. Ein "bestaetigt" im Code, dem im Referenzdokument ein "offen"
    gegenuebersteht, ist kein Beweis, sondern ein Widerspruch.

    GEAENDERT (Debug + Release, je 0 Fehler / 0 Warnungen):
      - `NavigationService.RelativeAngle`: jetzt `rot - atan2(dx, dz)`.
      - `CombatService.RelBearingDeg`: dieselbe Kopie, derselbe Dreher, mit.
      - `docs/game-api.md`: Vorzeichen von "OFFEN" auf geklaert, mit Herleitung
        und einer Warnung an kuenftige Leser.
    NICHT geaendert, bewusst: die AoE-Geometrie (`EscapeRouteService` rechnet
    mit `MathF.Abs`, Vorzeichen egal - falsch war nur, WOHIN wir geschickt
    haben, nicht OB gewarnt wurde) und `FacingService`/`FaceGuideDirection`
    (dort ist `atan2(dx, dz)` die zu setzende Ziel-Rotation, kein
    Relativwinkel - eine Umkehr waere dort ein neuer Fehler).

    GEGENGERECHNET an fuenf Lagen: Blick Sued/Ziel Ost -> links, Blick
    Sued/Ziel West -> rechts, Ziel voraus -> geradeaus, Ziel hinten -> hinten,
    Blick Ost/Ziel Sued -> rechts.

    ZU TESTEN: sagt der Mod jetzt die Seite, auf der das Ziel wirklich liegt -
    und zieht der Peil-Ton auf dieselbe Seite? Gegenprobe: einmal nach Norden
    und einmal nach Sueden blickend, denn nach Norden war es auch vorher
    richtig.

>>> BEFUND 5, DIE FOLGE-ENTSCHEIDUNG: ALLES GESPROCHENE LAEUFT JETZT AUF
    HIMMELSRICHTUNG. Vorschlag und Entscheidung des Users 2026-08-23, direkt
    nach dem Vorzeichenfund.

    DER GRUND IST NICHT GESCHMACK, SONDERN DIE FEHLERKLASSE. `CompassAdjective`
    faellt allein aus der Positionsdifferenz und fasst `player.Rotation` gar
    nicht an. Eine Himmelsrichtung KANN darum nicht auf die falsche Seite
    zeigen - weder durch einen Vorzeichendreher noch durch eine verdrehte
    Kamera. Der Fehler von heute war nur moeglich, WEIL links/rechts an der
    Blickrichtung haengt.

    ARBEITSTEILUNG SEITHER, das ist der Kern: die SPRACHE sagt absolut, WO
    etwas liegt ("30 Meter, oestlich"), der PEIL-TON fuehrt relativ die
    Ausrichtung (Stereo, verstummt beim Einrasten). Die beiden ergaenzen sich,
    statt dasselbe doppelt zu sagen. Der Ton MUSS relativ bleiben - Stereo ist
    von Natur aus links/rechts.

    GEAENDERT (Debug + Release, je 0 Fehler / 0 Warnungen):
      - `RouteService.CompassAdjective(from, to)` neu - Adjektivform
        ("oestlich") neben dem vorhandenen `CompassWord` ("Osten").
      - `NavigationService.CalculateDirection` liefert jetzt den Kompass. Die
        SIGNATUR blieb gleich, darum sind alle 17 Aufrufstellen (Objekt-
        Browser, Questziele, Zielansage, FATEs, Haendler, Jagd, Dungeontueren,
        Sammel- und Angelpunkte) auf einen Schlag mit umgestellt.
      - Gehhilfe: laufende Ansage und Wegpunkt-Ansage auf Kompass.
      - Reroute-Vergleich ("hat sich die Richtung geaendert?") auf Kompass -
        dort sogar der bessere Massstab, weil die relative Angabe schon
        wechselte, wenn der Spieler sich nur drehte.
      - Fluchtansage im Kampf auf Kompass.

    GRAMMATIKFALLE, gefunden und behoben: `EscapeDirection` lautet "Raus nach
    {richtung}" und verlangt das SUBSTANTIV - "Raus nach oestlich" waere kein
    Deutsch. Dort steht `CompassWord`, ueberall sonst (Richtung hinter einem
    Komma: "30 Meter, oestlich") das Adjektiv. `RouteViaHop` und `NewRoute`
    wurden gegengeprueft, beide tragen das Adjektiv.

    WAS VERLOREN GEHT, bewusst: "geradeaus" gibt es im Kompass nicht, also
    fehlt die gesprochene Bestaetigung "du laeufst richtig". Die traegt jetzt
    allein der Peil-Ton, der genau dann verstummt. WENN SICH DAS IM SPIEL ALS
    LUECKE ANFUEHLT, ist das die erste Stelle zum Nachbessern.

    `AccessibilityStrings.RelativeDirection` bleibt bestehen, hat aber keinen
    Sprech-Aufrufer mehr: sie speist nur noch die Sonde (die die TON-Seite
    misst) und ist der Rueckweg, falls der Kompass sich im Spiel als schlechter
    erweist. Im Code ist genau das vermerkt, damit sie nicht als tote
    Beschriftung durchrutscht.

    ZU TESTEN: klingen die Ansagen brauchbar ("Ziel: Wolf, 30 Meter, oestlich,
    80 Prozent Leben.")? Und traegt der Peil-Ton die Feinausrichtung allein,
    jetzt wo "geradeaus" nicht mehr gesagt wird?

>>> BEFUND 9, FOLGEFEHLER AUS BEFUND 8 - SELBST EINGEBAUT, GLEICH BEHOBEN.
    Der User: *"wenn ich mich mit Numpad5 automatisch ausrichte geht der Ton
    nicht mit"*. Ursache: als der Ton am 2026-08-23 auf das Segment-Ende
    umgestellt wurde, blieb `CurrentGuidePoint` - die Quelle der Ausricht-Taste -
    auf dem naechsten ROHEN Wegpunkt stehen. Numpad5 drehte den Spieler also
    woandershin, als der Ton zeigte; dass der Ton danach nicht still wurde, war
    voellig richtig, denn der Spieler stand nach der Drehung tatsaechlich falsch.
    Angeglichen: `CurrentGuidePoint` UND die beiden Reroute-Ansagen liefern jetzt
    ebenfalls das Segment-Ende. Die Cursor-Logik (Wegpunkt erreicht, Abweichung,
    Ecken abschneiden) bleibt bewusst auf den Rohpunkten - die muss die Route
    Punkt fuer Punkt abgehen.

    MERKSATZ, teurer als er aussieht: Ton, gesprochene Richtung und
    Ausricht-Taste beantworten dieselbe Frage ("wo geht es lang") und muessen aus
    DERSELBEN Quelle kommen. Drei Antworten auf eine Frage sind fuer einen
    blinden Spieler nicht auseinanderzuhalten - er hoert nur, dass etwas nicht
    stimmt, und kann nicht sagen, welche der drei luegt.

>>> BEFUND 10, LAUTSTAERKE HING AM FALSCHEN PUNKT (Vorschlag des Users, gebaut).
    Log 15:27: `dist=10,0 zielDist=701,0` - der Peilpunkt lag 10 m entfernt, die
    Lautstaerke rechnete mit 701 m und landete damit auf der Untergrenze von
    15 %. Auf langen Routen war der Ton praktisch stumm. Jetzt bekommt
    `_beacon.Update` `guideDist` statt `distance`: Richtung UND Lautstaerke
    meinen denselben Punkt. `arrived` bleibt an der Reststrecke, denn es
    beantwortet "bin ich da" und nicht "wo ist der naechste Knick".
    Das alte Gegenargument ("Wegpunkte sind immer nah, der Ton waere dauernd
    laut") traegt nicht: der Ton ist ohnehin nur hoerbar, wenn die Ausrichtung
    NICHT stimmt - laut ist er also genau dann, wenn eine Korrektur faellig ist.

>>> IN-GAME BESTAETIGT (User, 2026-08-23): *"das mit den himmelsrichtungen
    geht"* und *"das mit den tönen funktioniert"*. Damit sind bestaetigt:
      - Himmelsrichtungen statt links/rechts (Befund 5)
      - der Vorzeichenfix (Befund 4) - der Ton haengt an `RelativeAngle`, also
        beweist ein richtig ziehender Ton die Rechnung mit
      - Segment-Peilung + Numpad5-Angleich (Befunde 8A, 9)
      - Geraet schliessen gegen das Rauschen (Befund 8B)
      - Lautstaerke am Peilpunkt (Befund 10) und der durchlaufende Ton (11)
    NICHT bestaetigt, weil nie eingetreten: der Kamera-Verdacht (`kameraAb` stand
    in jeder Messung auf 0,0). Bleibt offen, ist aber kein bekannter Fehler.

>>> BEFUND 11, ENTSCHIEDEN UND GEBAUT: DER TON IST NIE MEHR STILL.
    User woertlich (2026-08-23): *"es soll nicht still sein der ton soll immer
    sein egal wie lang wenn man näher kommt soll der ton lauter werden nie
    stille"*. Damit ist die Umkehrung vom 2026-08-19 ("geradeaus = Stille")
    ZURUECKGENOMMEN - das dritte Mal, dass dieses Design gedreht wird.

    WAS SICH AENDERT: `alignedNow` schaltet nicht mehr auf `Silent`. Der Ton
    laeuft durch, solange gefuehrt wird. "Richtig" klingt jetzt mittig, hell und
    ruhig statt gar nicht - und das kommt ohne Sonderfall zustande, die
    vorhandene Rechnung liefert es von selbst: Pan gegen die Mitte (sin(0)=0),
    Tonhoehe auf dem unverbogenen Grundton, Takt auf dem langsamsten Wert
    (0,95 s). Der Einrast-Quittungston bleibt, markiert aber jetzt den Moment
    statt eine Stille zu erklaeren.

    STILLE GIBT ES WEITER, aber nur wo sie etwas anderes heisst: kein Ziel,
    keine Fuehrung, kein Lauf (Idle/Stop an den Aufrufstellen).

    MITGEZOGEN, weil sonst Doku und Ansage etwas versprechen, das es nicht mehr
    gibt - das schickt den Spieler auf die Suche nach einem Fehler, der keiner
    ist:
      - GESPROCHEN: "Peil-Ton an. Er verstummt, sobald du richtig stehst." ->
        "...wird mittig und ruhig, wenn du richtig stehst." (auch EN)
      - Klassendoku `BeaconService`, `Configuration.TargetBeaconEnabled`,
        `RouteService.CompassAdjective`.

    ZU BEOBACHTEN: ein Dauerton ueber 700 m Wegstrecke kann auf Dauer muede
    machen - das war 2026-08-19 der Grund fuer die Umkehrung. Wenn es soweit
    kommt, ist der Hebel NICHT die Stille zurueckzuholen, sondern der Takt bei
    stimmender Ausrichtung (heute 0,95 s) und die Lautstaerke.

>>> BEFUND 8, BEIDE TONFEHLER GEMESSEN UND ERKLAERT (Log 13:18, 2026-08-23).
    Die HP/MP-Toene sind ENTLASTET: null `[Vitals]`-Zeilen im ganzen Zeitraum,
    obwohl sie jetzt sichtbar loggen. Es waren zwei andere Sachen.

    A) "BEIM AUSRICHTEN SPINNT DER TON" - der Peilpunkt springt.
         13:18:15.190  still=True  eingerastet=True   rot=-1,797
         13:18:15.355  still=False eingerastet=False  rot=-1,797
         13:18:15.419  "Wegpunkt erreicht, weiter zu 4/5"
       `rot` UNVERAENDERT - der Spieler hat sich nicht gedreht. Geaendert hat
       sich das Ziel des Tons: er peilt den naechsten WEGPUNKT an, und beim
       Passieren springt der auf den uebernaechsten, der woanders liegt. Der Ton
       rastet aus, man richtet sich neu aus, naechster Wegpunkt, wieder aus.
       Auf dieser Strecke lagen 5 Wegpunkte auf 72 m.
       BEMERKENSWERT: die SPRACHE fasst dieselbe Route zu 2 Segmenten zusammen
       ("5 Wegpunkte, 2 Segmente"), der Ton nimmt alle 5 roh. Der Fix liegt
       damit nahe - den Ton auf das Ende des SEGMENTS zeigen lassen statt auf
       den naechsten Rohpunkt. GEBAUT 2026-08-23 auf Entscheidung des Users,
       mit seiner Auflage "der Ton darf nicht abreissen":
         - `RouteService.SegmentEndIndex(from, waypoints, cursor)` neu - dieselbe
           Regel wie `BuildSegments`, inklusive der Sub-Meter-Ausnahme (ein
           Zentimeter-Huepfer an einer Tuerschwelle hat keine verlaessliche
           Richtung und darf kein Segment zerschneiden).
         - `WalkGuideFrame` peilt damit das Segment-Ende an. Dieselbe Route hat
           2 Peilpunkte statt 5.
         - DIE AUFLAGE IST KONSTRUKTIV ERFUELLT, nicht nur gehofft: `targetKey`
           bleibt ueber die ganze Gehhilfe `_walkBeaconKey`. NUR ein Wechsel
           dieses Schluessels laesst `BeaconService.Update` den Takt abbrechen
           und neu ansetzen (`provider.Restart`). Ein wandernder Peilpunkt unter
           demselben Schluessel aendert nur Winkel und Lautstaerke - fortlaufend,
           ohne Luecke. Auch das Geraet bleibt waehrend des Laufs offen; die
           Stop-Aenderung aus B greift nur, wenn KEIN Lauf laeuft.
         - SPRACHE MITGEZOGEN, sonst nennt sie eine Richtung, die der Ton nicht
           zeigt: die Wegpunkt-Ansage meint jetzt auch das Segment-Ende, mit
           Merker `_lastSpokenLeg` gegen Wiederholung. Im Log vom 13:18 kamen
           "4 Meter, suedwestlich" und "3 Meter, suedwestlich" 150 ms
           hintereinander - zwei Rohpunkte, eine Richtung.
       ZU TESTEN: reisst der Ton beim Uebergang zwischen zwei Segmenten ab?
       ACHTUNG BEIM BEURTEILEN: dass er beim Einrasten VERSTUMMT und beim
       Abbiegen wieder einsetzt, ist keine Luecke, sondern das Signal.

    B) "VERSCHWINDET NICHT BEIM AUSMACHEN" - das Geraet blieb offen. GEFIXT.
       Die Sonde stand nach dem Ende der Gehhilfe eine Minute lang auf
       `still=True offen=True` - stumm, aber der Ausgabestrom lief weiter und
       schob Stille durch die Soundkarte, was je nach Geraet hoerbar rauscht.
       `Idle()` schaltet nur stumm, `Stop()` schliesst. Das war richtig, solange
       der Ton bei jedem Ziel lief (Aufmachen kostet einen Aussetzer) - seit er
       an einen LAUF gebunden ist, vergehen bis zum naechsten Ton Minuten.
       Ich hatte das heute Vormittag mit dem Gatter sogar noch verfestigt.
       Geaendert auf `Stop()` an drei Stellen: `UpdateTargetBeacon` (Gatter),
       `StopWalkGuide` (damit "aus" im selben Moment aus ist) und beim
       Spielerverlust (Ladebildschirm).

    LEHRE: `still=True` heisst NICHT "nichts zu hoeren". Bei einer Tonquelle
    gehoert die Frage "ist das Geraet ueberhaupt zu?" mit in die Sonde - genau
    diese Spalte hat den Fall geloest.

>>> BEFUND 7, DER WAHRSCHEINLICHE TAETER: DIE HP/MP-TOENE. Verdacht vom
    2026-08-23, noch NICHT bewiesen - aber der Gegentest dauert 30 Sekunden.

    AUSGANGSPUNKT: der User meldete nach dem Peil-Ton-Umbau *"hast du das
    plugin neugestartet, fuer mich hat sich nichts geaendert"*. Geprueft: die
    DLL wurde 12:52:49 deployt und 12:52:53 vom Spiel geladen, alle Aenderungen
    waren also drin. Damit ist "nichts geaendert" selbst der Befund - wir haben
    am falschen Ton gearbeitet. Das Log zeigt den Peil-Ton nachweislich korrekt:
    er schweigt beim Ausschalten der Gehhilfe, rastet beim Ausrichten ein, und
    seine Richtung stimmt (nachgerechnet: Blick Osten, Ziel Nordwesten ->
    "hinter links").

    WARUM DIE VITALWERT-TOENE PASSEN (Configuration.cs, HP/MP-Toene seit V5.28):
      - Ein Ton bei JEDER 10-Prozent-Stufe von HP UND MP.
      - Die Stereoseite bildet den FUELLSTAND ab, nicht eine Richtung
        (voll = rechts, leer = links) -> *"er ist nicht da wo er sein sollte"*.
      - Sie laufen ausdruecklich AUCH AUSSERHALB DES KAMPFES, damit die
        Regeneration hoerbar ist -> *"geht nicht aus wenn man das manuelle
        Laufen aus macht"*. Sie haben mit der Gehhilfe nichts zu tun.
      - Der User spielt Schwarzmagier (Log 13:02:44). Mana bewegt sich dort
        staendig, also folgen die Stufen dicht aufeinander -> *"wird ganz oft
        gespammt ganz schnell"*.

    WARUM WIR SIE EINEN HALBEN TAG LANG NICHT GESEHEN HABEN - die uebertragbare
    Lehre: `VitalsService` loggte mit `_log.Debug`, und DALAMUD SCHREIBT KEIN
    DEBUG in die dalamud.log. Nachgezaehlt am 2026-08-23: 3598 INF, 45 WRN,
    NULL DBG. Eine Tonquelle ohne Logspur kann man nicht ausschliessen, und
    genau deshalb blieb sie ausserhalb des Verdachts, waehrend drei Sonden den
    unschuldigen Peil-Ton vermassen. MERKSATZ: bevor man eine Quelle
    ausschliesst, pruefen, ob sie ueberhaupt sichtbar WAERE.

    GEBAUT (Debug + Release, je 0 Fehler / 0 Warnungen):
      - `VitalsService.LogVital` - im Debug-Build auf INFO, damit die Toene im
        Log auftauchen.
      - MENUEEINTRAEGE NACHGETRAGEN: "Töne für Leben und Mana" (Schalter) und
        "Lautstärke Leben und Mana". Die liefen seit V5.28 voellig ohne
        Schaltung - ein Spieler konnte diese Tonquelle nicht einmal
        versuchsweise ausschliessen. Das ist derselbe Mangel wie damals beim
        AoE-Lautstaerkeregler.

    GEGENTEST (der User, 30 Sekunden): Optionsmenue -> "Töne für Leben und
    Mana" AUS. Ist die Stoerung damit weg, ist der Fall geklaert. Bleibt sie,
    sind die HP/MP-Toene entlastet und der naechste Kandidat ist ein FREMDES
    Plugin - dann pruefen, ob die Stoerung auch bei
    abgeschaltetem FF14Accessibility auftritt.

>>> BEFUND 6, ZWEI TONFEHLER - GEMELDET 2026-08-23, SONDE GEBAUT, UNGEMESSEN.
    Der User nach dem Kompass-Test: *"die navigationstoene sind nicht korrekt,
    der ton stoppt nicht wenn man die gehhilfe aus macht und wenn man sich
    ausrichtet passt der ton sich nicht an"*.

    ZUM TONERZEUGER SELBST: der ist geprueft und in Ordnung.
    `BeaconSampleProvider.Read` gibt bei `Silent` zuverlaessig Stille aus und
    setzt den Takt zurueck. Der Fehler sitzt also davor - bei der Frage, WER den
    Ton fuettert.

    SYMPTOM 2 IST DER SCHLUESSEL, und der Verdacht ist derselbe wie bei Befund 4,
    diesmal aber als HAUPTsymptom: der Ton rechnet gegen `player.Rotation`. Bei
    `MoveMode` 0 dreht sich die FIGUR nicht, wenn der Spieler sich umsieht - nur
    die KAMERA. Dann bleibt `rot` konstant, und der Ton KANN sich nicht
    anpassen. Das trifft genau den Zweck des Tons ("dreh dich, bis es still
    wird"), und es erklaert, warum es beim Kompass-Umbau nicht auffiel: die
    Sprache haengt seither an gar keiner Blickrichtung mehr.
    NOCH NICHT GEMESSEN. Der Fix waere, den Ton bei `MoveMode` 0 gegen
    `FacingService.CameraFacing()` zu rechnen - erst messen.

    SYMPTOM 1 IST UNGEKLAERT UND KOENNTE SCHON BEHOBEN SEIN. Durchgespielt:
    Gehhilfe aus -> `StopWalkGuide` -> `_beacon.Idle()` -> naechster Frame
    `UpdateTargetBeacon` -> das heute gebaute Gatter -> `Idle()`. Das ist still.
    Zwei Erklaerungen bleiben, und die Sonde trennt sie: entweder lief ein
    Auto-Lauf oder ein Ziel-Folgen (dann haelt `IsWalking` den Ton absichtlich
    am Leben), oder getestet wurde mit einer DLL von vor dem Gatter. ZUERST
    PLUGIN NEU LADEN, dann messen.

    GEBAUT: `[BeaconProbe]` in `NavigationService.Update`, plus drei
    Debug-Leser in `BeaconService` (`DebugSilent`, `DebugAligned`,
    `DebugTargetKey`). Loggt quelle / still / eingerastet / offen / schluessel /
    gehhilfe / laeuft / rot / dirH / kameraAb. Entdoppelt: nur bei Aenderung,
    sonst hoechstens 1x pro Sekunde.

    SO WIRD ES GELESEN:
      - `still=False` bei `quelle=Ziel` und `laeuft=False` -> ein `Idle()`, das
        nicht angekommen ist. Das ist Symptom 1.
      - `rot` bleibt konstant, waehrend sich der Spieler dreht, und `kameraAb`
        waechst -> Figur steht, Kamera dreht. Das ist Symptom 2, bewiesen.
      - `laeuft=True` obwohl der Spieler nur die Gehhilfe aus hat -> es lief ein
        Auto-Lauf/Folgen, dann ist Symptom 1 gar kein Fehler.

>>> DER KAMERA-VERDACHT VON VORHIN BLEIBT OFFEN, ist aber NICHT die Ursache
    dieses Fehlers. `MoveMode` = 0 heisst weiterhin kamerarelative Bewegung,
    waehrend wir gegen die Figur rechnen - das kann bei verdrehter Kamera
    zusaetzlich danebenliegen. Die Sonde dafuer steht (`[NavDirProbe]`, Marker
    `ABWEICHUNG`), `FacingService.CameraFacing()` liest die Kamerarichtung.
    Erst nach dem Vorzeichen-Test messen, sonst vermischen sich zwei Effekte.

>>> URSPRUENGLICHER VERDACHT (ueberholt, nur noch als Historie lesen):
    Zweite Spielerbeschwerde 2026-08-23: *"it says go left, so go left well it
    should say right instead ... I've tried all sorts of changing control
    settings from the camera to everything. even the beeping locator is
    incorrect."* Dass Sprache UND Ton gemeinsam falsch sind, spricht fuer EINE
    Ursache, nicht fuer zwei Fehler - beide haengen an `RelativeAngle`.

    DIE ABLEITUNG, aus zwei bereits gemessenen Fakten:
      1. `UiControl.MoveMode` = 0 -> kamerarelative Bewegung. Vorwaerts laeuft
         die Figur dorthin, wo die KAMERA schaut. Gemessen 2026-08-22, steht in
         `FacingService` und im Memory `camera_dirh_half_turn_offset`.
      2. `RelativeAngle` (`NavigationService.cs`) rechnet gegen
         `player.Rotation`, also gegen die FIGUR.
    Solange die Kamera hinter der Figur steht, sind beide identisch - und GENAU
    dieser Fall wurde am 2026-07-10 per Hoertest bestaetigt ("Vorzeichen vom
    User per Beacon-Hoertest bestaetigt"). Steht die Kamera woanders, zeigen
    Ansage und Steuerung auseinander. Fuer einen blinden Spieler ist eine
    verdrehte Kamera unsichtbar - er kann den Zustand nicht bemerken.

    NICHT UMGEBAUT, weil ungemessen. Stattdessen zwei Bausteine gebaut:
      - `FacingService.CameraFacing()` - die Richtung, in die die Kamera SCHAUT
        (`DirH + Pi`, der halbe-Drehung-Versatz ist hier eingerechnet, damit ihn
        kein Aufrufer vergessen kann). Den braucht ein spaeterer Fix ohnehin.
      - `[NavDirProbe]` erweitert: loggt jetzt Figur-Wort UND Kamera-Wort
        nebeneinander, dazu `kameraAb` (wie weit beide auseinanderstehen),
        `moveMode` und das Wort `ABWEICHUNG`, wo die beiden Woerter
        auseinanderfallen.

    SO WIRD ES ENTSCHIEDEN (Debug-Build noetig, die Sonde ist `#if DEBUG`):
      1. Einmal normal spielen und dabei eine Richtungsansage ausloesen.
      2. Im Log nach `ABWEICHUNG` suchen. Kommt das Wort vor, ist die Ursache
         bewiesen und der Fix ist: `RelativeAngle` bei `moveMode=0` gegen
         `CameraFacing()` rechnen statt gegen `player.Rotation`.
      3. Kommt es NIE vor, ist die Kamera nicht die Ursache und der Fehler
         liegt woanders - dann NICHT an der Rechnung drehen, sondern weiter
         messen.

>>> NEBENBEFUND, nicht repariert: `Plugin.cs` enthaelt 6 kaputte Gedankenstriche
    (`â€"` statt `-`) in Kommentarzeilen 534, 738, 824, 1745, 1756, 1827 - Reste
    frueherer PowerShell-Umschreibungen. Rein kosmetisch, kein Codepfad betroffen.

>>> IM LOG VOM 2026-08-22 (21:21-22:56) STEHT KEIN PLUGIN-FEHLER. Die einzige
    Exception ist vnavmesh: `FileNotFoundException` auf
    `pluginConfigs\vnavmesh\seeds-local.json` - der bekannte Saatpunkt-Fehler.

## FRUEHERER STAND (2026-08-22, "WAS STEHT MIR IM WEG")

>>> WUNSCH DES USERS: "es ist gut zu wissen, wo man gegen laeuft". Gebaut als
    ObstacleService, Debug 0/0 und Release 0/0, liegt in devPlugins.
    Der Nutzen steckt nicht darin, DASS etwas blockiert, sondern WAS: ein
    Spieler geht von allein weiter, eine Absperrung nie.

>>> ZWEI QUELLEN, UND SIE GEBEN UNTERSCHIEDLICH VIEL HER:
    1. WESEN (Spieler, NPCs, Begleiter, Vermieter, Wohnraum-Objekte) tragen
       Namen, Position und HitboxRadius. "Wer steht im Weg" ist damit eine reine
       Rechnung: Objekt liegt in Laufrichtung, Abstand zur Laufachse kleiner als
       beide Hitboxen plus etwas Spiel, Hoehenunterschied unter 2 m. Der Name
       kommt aus dem vorhandenen ObjectNameService. Das ist auch der haeufige
       Fall - in einer Stadt steht staendig jemand im Tuerrahmen.
    2. KULISSE TRAEGT KEINEN NAMEN. Mit zone-probe gemessen: Hintergrundteile
       weisen sich nur ueber Modell- und Kollisionsdatei aus
       (`f1t0_a0_taru1.mdl`, `f1t0_b0_gatdr.pcb`) plus Layout-Typ. Fuer ein Fass
       oder einen Zaun fuehrt das Spiel nirgends einen sprechbaren Namen, anders
       als bei NPCs oder Truhen.
       DESHALB WIRD KEINER ERFUNDEN. Eine Uebersetzungstabelle fuer fremde
       Modellkuerzel waere genau die Art Vermutung, die die Regeln hier
       verbieten. Ehrlich sagbar ist die Unterscheidung, auf die es ankommt:
         - "eine unsichtbare Absperrung" (CollisionBox mit pushPlayerOut)
         - "ein festes Hindernis" (BgPart mit Kollision)
       Das Modellkuerzel geht ins LOG, nicht ins Ohr - damit sich spaeter
       beantworten laesst, was es war, ohne es jetzt zu raten.

>>> ANGEBUNDEN AN DIE ZWEI STELLEN, DIE HEUTE SCHON "DA STEHT ETWAS IM WEG"
    SAGEN - keine neue Taste noetig (Tastenknappheit):
      - ZoneTransitionHandler.Stop: "Ich komme nicht in X hinein, <Y> steht im Weg."
      - AutoWalkService, Zweig "festgesteckt": "Ich komme nicht weiter, <Y> steht
        im Weg. Noch N Meter." Der Netzende-Zweig behaelt seinen eigenen Wortlaut:
        dort steht NICHTS im Weg, dort hoert der Boden auf. Zwei verschiedene
        Lagen, zwei verschiedene Ansagen.
    Beide Ansagen bilingual in AccessibilityStrings. Findet der Dienst nichts,
    bleibt es beim bisherigen, vagen aber ehrlichen Satz.

>>> GELESEN WIRD DAS LAUFENDE LAYOUT, dieselbe Kette wie beim Netzbau von
    vnavmesh (`SceneDefinition.FillFromLayout`), inklusive desselben Aktiv-Bits
    `Flags3 & 0x10`. Was hier als Hindernis zaehlt, zaehlt also auch fuers
    Wegenetz als eines. Nur Lesezugriffe, kein Hook.

>>> ZU TESTEN: irgendwo gegen einen Spieler oder NPC laufen lassen (Auto-Lauf zu
    einem Ziel, hinter dem jemand steht) - kommt der Name? Und an einer Stelle
    mit Kulisse: kommt "ein festes Hindernis", und steht im Log die Zeile
    `[Hindernis] Kulisse blockiert, Kollisionskennung ...`?

>>> NOCH NICHT GEBAUT: die Ansage beim MANUELLEN Laufen. Dafuer fehlt das Signal
    "der Spieler will gerade laufen" - ohne das ist Stillstand nicht von
    Stehenbleiben zu unterscheiden. vnavmesh liest das per Signatur-Hook auf
    RMIWalk. Ob `Character.MoveController` / `Character.MovementState` ein
    lesbares Feld bieten, ist UNGEKLAERT - die Felder gibt es, ihr Aufbau steht
    nicht in der mitgelieferten Doku. Muesste ausgemessen werden.

## FRUEHERER STAND (2026-08-22, "DER UEBERGANG IST DURCHLAUFEN - IN BEIDE RICHTUNGEN")

>>> ES FUNKTIONIERT. Im Log vom Test 18:41 laeuft die ganze Kette durch, ohne
    eine einzige Stockung:
      18:41:10.966  `[Bruecke] Weg endet 2,7 m vor ... - nicht erreichbar`
      18:41:10.968  `[Bruecke] 'Tor zum Tiefen Wald' passt`
      18:41:10.969  `umgelenkt - erst zur Bruecke`
      18:41:12.337  `fahre Bruecke 'Tor zum Tiefen Wald'`
      18:41:12.360  `Spur zu Ende, ich laufe normal weiter`
      18:41:13.262  `beendet (angekommen, dist=0,7)`
      18:41:13.863  `[ExitProbe] === Zonenwechsel 132 nach 148 ===`
      18:41:14.429  Toast: 'Jadeweiher-Ufer'
    KEIN ANSCHIEBEN NOETIG - die Figur ist einfach durchgelaufen.

>>> GEGENPROBE RUECKWEG, ebenfalls bestanden (18:41:22-25): dort passt KEINE
    gemessene Luecke, und das Log sagt das auch (`Keine gemessene Luecke passt -
    laufe den Teilweg trotzdem`). Der Teilweg allein hat gereicht: `angekommen,
    dist=0,8`, Zonenwechsel 148 nach 132. Genau das war der Sinn des Zweigs -
    Stillstand ist die schlechteste Auskunft.

>>> DIE UNSICHTBARE WAND IST NICHT AKTIV - GEMESSEN, NICHT VERMUTET. Die
    CollisionBox aus `QST_OP_ENPC_001` deckt X 154,0..156,0 ueber Z 146,4..168,4
    ab. Der Positionsverlauf der ExitProbe zeigt die Figur quer hindurch:
    (154,7|158,1) -> (155,2|158,2) -> (155,8|158,2) -> (156,4|158,3) ->
    (158,4|158,5), ohne jeden Widerstand. Damit ist die Sperren-Deutung von
    vorhin WIDERLEGT, und die Bruecke ist kein Cheat: der Weg ist offen.

>>> WAS DIE FEHLVERSUCHE WIRKLICH UNTERSCHIED: der gewaehlte Grenz-Kandidat.
      - 18:41 (klappt):    Kandidat bei (156,4|-10,6|**158,0**) - noerdlich.
      - 18:19/18:20 (nein): Kandidat bei (156,3|-10,6|**149,2**) bzw. Z 150,5 -
        suedlich, hinter den Toren und Faessern.
    Der ZoneBorderService nimmt den Punkt der Trigger-Box, der dem SPIELER am
    naechsten liegt. Steht der Spieler suedlich, waehlt er einen suedlichen
    Randpunkt - und dort ist der Durchgang zu. Ablehnen kann er nichts, weil
    NearestPointReachable in Zone 132 nichts filtert (keine Saatpunkte).
    DAS IST DER LETZTE OFFENE PUNKT an dieser Baustelle.

>>> DIE SONDE WURDE NIE AUSGELOEST - der User hat nichts eingegeben, daher keine
    `[CollProbe]`-Zeile. Kein Fehler. Sie ist gebaut und liegt bereit
    (`/acc coll`), fuer die Wand-Frage aber nicht mehr noetig: die ist oben aus
    dem Positionsverlauf beantwortet. Sie bleibt vorerst liegen, weil sie der
    erste Baustein fuer die gewuenschte Hindernis-Ansage ist ("wogegen laufe ich
    gerade?"). Wird sie dafuer nicht gebraucht, nach Sonden-Konvention loeschen.

## FRUEHERER STAND (2026-08-22, "DIE LUECKE WAR EIN SYMPTOM, DIE SPERRE IST DIE URSACHE")

>>> BEIDE FIXES VON HEUTE WIRKEN, IM LOG BESTAETIGT (Test 18:19-18:20):
      - Fix 1: beim Anschub um 18:20:00.845 steht KEIN "nachlaufender Pfad
        abgeraeumt" mehr dazwischen. Der Waechter laesst ihn in Ruhe.
      - Fix 2: `Weg endet 2,7 m vor ...` -> `'Tor zum Tiefen Wald' passt` ->
        `umgelenkt` -> einmal sogar `fahre Bruecke`. Die Kette laeuft.
      - GEGENTEST BESTANDEN, und er hat die unsichere Zahl geklaert: bei einem
        Weg, der wirklich ankommt, meldet die Sonde `letzter echter Wegpunkt
        0,0 m davor` - dreimal, zu Miounnes Hexenstuebchen. 0,0 gegen 2,7-3,8:
        die Trennung ist scharf, PathShortfallSlack = 2 m ist bestaetigt.

>>> UND TROTZDEM KOMMT DIE FIGUR NICHT DURCH - WEIL DORT ETWAS STEHT.
    tools/zone-probe an genau den beiden Stellen, an denen sie haengenblieb:
      - CollisionBox aus Layer `QST_OP_ENPC_001/planner.lgb`, Mitte
        (155,0|-8,7|157,4), Halbmass (1,0|5,8|11,0), `pushPlayerOut=31`.
        Das ist eine unsichtbare Wand: X 154,0..156,0, Z 146,4..168,4, und in
        der Hoehe umschliesst sie die Figur.
      - Zwei weitere solche Boxen desselben Layers liegen auf der Linie, auf der
        der Anschub in Fall B scheiterte.
      - Dazu Tore mit echter Kollision (`f1t0_b0_gatdr.pcb`, `f1t0_b0_gat00.pcb`)
        direkt noerdlich. Der Toast im Log nennt den Ort: Blaudachs-Pforte.
    UNSERE BRUECKE ENDET MITTEN IN DIESER WAND: (155,0|-12,8|161,0) liegt in
    X 154..156. Die Figur wird also nicht von Faessern gestoppt, sie wird
    hinausgeschoben.

>>> DAMIT KIPPT DIE DEUTUNG VON GESTERN. Die 36-Polygon-Insel ist KEINE
    Baufehler-Luecke im Netz. Recast hat den Bereich hinter der Absperrung voellig
    korrekt als unerreichbar abgetrennt. Die Luecke war das Symptom, die Sperre
    ist die Ursache. Was daraus folgt, haengt an einer Frage, die die Datei nicht
    beantwortet - `QST_*`-Layer schaltet das Spiel nach Questfortschritt:
      - Wand AKTIV -> das Spiel sperrt den Weg mit Absicht. Dann darf der Mod das
        nicht unterlaufen (Playability-Regel), und dorthin gehoert eine ehrliche
        Ansage statt einer Bruecke.
      - Wand ABGESCHALTET -> etwas anderes blockiert, und die Brueckenpunkte
        muessen neu vermessen werden, ausserhalb X 154..156.

>>> GEBAUT: CollisionProbe (`/acc coll`), Debug 0/0, liegt in devPlugins.
    Liest das LAUFENDE Layout statt der .lgb - nur dort steht, was das Spiel
    gerade eingeschaltet hat. Dieselbe Kette, die vnavmesh fuer den Netzbau
    benutzt (`SceneDefinition.FillFromLayout`): ueber LayoutWorld auf
    `InstancesByType`, je Instanz das Aktiv-Bit `Flags3 & 0x10`. Nur Lesezugriffe,
    kein Hook. Listet CollisionBoxen (mit AKTIV, Ausdehnung und "SPIELER STEHT
    DRIN") und BgParts mit Kollision.
    Beantwortet nebenbei die zweite Frage des Users ("kann man ansagen, wogegen
    man laeuft?"): Position, Ausdehnung und Modellkennung stehen da. Sprechbare
    Namen fuehrt das Spiel nicht, aber die Kuerzel sind lesbar - `taru1` ist ein
    Fass, `gatdr` die Tortuer.

>>> FRAGE DES USERS: "es gibt zwei uebergaenge nach tiefer wald, warum?" -
    stimmt, und die Antwort ist wichtig. zoneprobe ueber die ganze Zone:
      - A (170,1|-10,6|159,0), Halbmass (15,6|3,8|15,0), Laufrichtung 85 Grad -
        der oestliche, Blaudachs-Pforte. Dorthin laeuft das Plugin, weil naeher.
      - B (-122,2|-3,7|105,6), Halbmass (1,0|1,0|1,0), Laufrichtung 0 Grad -
        ganz im Westen, rund 290 m weg, auffaellig winzig.
    (Dazu drei Uebergaenge nach 133, Alt-Gridania.)
    B TAUGT NICHT ALS AUSWEICHWEG: navmeshgaps sagt fuer B "mesh here, but no
    route from FROM" in ALLEN VIER Netzvarianten.

>>> UND DABEI KAM DER EIGENTLICHE FUND: NEU-GRIDANIA HAT VIER NETZVARIANTEN.
    Im Cache des Users liegen vier Dateien fuer dieselbe Zone, und sie
    unterscheiden sich nicht kosmetisch:
      - `...f1t1__10942__27.AE__0`  1466 Polygone, Uebergang A ABGELEHNT
      - `...f1t1__10942__27__0`     1466 Polygone, Uebergang A ABGELEHNT
      - `...f1t1__10942____0`       1464 Polygone, Uebergang A ABGELEHNT
      - `...f1t1__125B9____0`       1643 Polygone, Uebergang A **ANGENOMMEN**
    vnavmesh baut den Namen als `{zone}__{filterKey}__{festivals}__{zoneSGs}`
    (NavmeshManager.GetCacheKey). Die vier unterscheiden sich AUSSCHLIESSLICH im
    filterKey: dreimal 10942, die begehbare einmal 125B9. Der Filter entscheidet,
    welche Layer an sind.
    ES GIBT ALSO EINEN ZUSTAND, IN DEM MAN DORT DURCHLAEUFT - und der Zustand des
    Users beim Test war keiner davon. Das stuetzt die Sperren-Deutung stark,
    beweist sie aber noch nicht.

>>> ZU TESTEN: an die Stelle laufen, wo der Auto-Lauf haengenbleibt, dann
    `/acc coll`. Zwei Zeilen zaehlen:
      1. Die CollisionBox mit Mitte (155,0|-8,7|157,4): steht dort `AKTIV=True`
         oder `AKTIV=False`? Das ist die Kernfrage.
      2. `layerFilterKey=...` bzw. die Filter-Zeilen: steht dort 10942 (die
         gesperrte Fassung) oder 125B9 (die offene)?
    Zusammen sagen die beiden, ob die Sperre steht und in welcher Fassung der
    Zone der Spieler sich befindet.

## FRUEHERER STAND (2026-08-22, "BEIDE NEUBAUTEN WAREN TOT, AUS ZWEI GRUENDEN")

>>> DER USER HAT GETESTET UND ICH HABE DAS LOG GELESEN. Beide gestern gebauten
    Teile sind gelaufen und haben nicht getragen - jeder aus einem eigenen,
    inzwischen bewiesenen Grund. Nichts davon war eine Vermutung, beides steht
    im Log bzw. im vnavmesh-Quelltext.

    BEFUND 1: DER AUTO-LAUF LOESCHT SEINEN EIGENEN ANSCHUB, NACH 11 ms.
      Log 17:50:00 -> .541 "beendet (Uebergang: schiebe die letzten 3,6 m)",
      .550 "[Uebergang] Schiebe in ... hinein", .561 "nachlaufender Pfad nach
      dem Ende abgeraeumt", 01.766 "Anschieben bringt nichts - keine Bewegung".
      Das ist GuardUpdate (AutoWalkService). Der Waechter laeuft nach jedem
      Lauf-Ende weiter, sieht Path.IsRunning und ruft Path.Stop. Er kann einen
      fremden Nachlaeufer nicht von unserem eigenen Anschub unterscheiden - und
      TryNudgeIntoTransition ruft Finish() bewusst ZUERST. Vier Versuche im Log,
      viermal dasselbe Muster. Die Meldung "da steht etwas im Weg" war falsch.

    BEFUND 2: DIE ERREICHBARKEITSPRUEFUNG PRUEFT IN NEU-GRIDANIA NICHTS.
      Im ganzen Log steht keine einzige "[Bruecke]"-Zeile - der Zweig wurde nie
      betreten. Er haengt an ProbeReachable == null, und das wird nie null:
      17:50:12.217 "[Orte] NearestPointReachable (156,4|-10,6|155,1) ->
      (156,4|-12,8|155,1)", also "erreichbar" fuer genau den Punkt, den wir
      offline als unerreichbar vermessen haben. Im vnavmesh-Quelltext gelesen:
        - NearestPointReachable filtert nur gegen ein Bit, FLAG_UNREACHABLE
          (NavmeshQuery.cs:59-64).
        - Gesetzt wird das Bit ausschliesslich von Prune
          (NavmeshManager.cs:396-402).
        - Prune laeuft nur fuer Territorien mit Flood-Fill-Saatpunkten
          (NavmeshManager.cs:112-114).
        - Die Saatliste (seeds.json, 2026-08-22 abgerufen) fuehrt 54 Zonen.
          132 ist NICHT dabei - 128, 129, 134, 135 schon, 132 nicht. 148 haette
          einen.
      Ohne Saat traegt in Neu-Gridania kein Polygon das Bit, also ist
      NearestPointReachable bitgenau NearestPoint. Das trifft nicht nur die
      Bruecke, sondern auch die Kandidatenauswahl: "Kandidat 1/9, 0 abgelehnt"
      lehnt nichts ab, weil sie nichts ablehnen KANN.

>>> BEIDES REPARIERT (Debug 0/0, Release 0/0, liegt in devPlugins):

    1. GuardUpdate laesst einen laufenden Anschub in Ruhe (Transitions.IsActive)
       und schiebt sein eigenes Fenster so lange vor sich her. Unbeaufsichtigt
       ist dabei nichts: der ZoneTransitionHandler begrenzt sich selbst auf 3 s,
       bricht nach 1,2 s Stillstand ab und stoppt vnavmesh am Ende immer - und
       danach laeuft der volle Waechter noch einmal.

    2. TryBridgePartialPath liest die Antwort aus dem WEG statt aus der Abfrage.
       Recast liefert fuer ein unerreichbares Ziel einen TEILWEG, der am Rand der
       eigenen Flaeche endet. Achtung beim Auswerten: vnavmesh haengt das
       Wunschziel unkonditioniert hinten an (res.Add(endPos) in BEIDEN Zweigen
       von PathfindMesh, das Clamping darueber ist auskommentiert) - der LETZTE
       Wegpunkt ist also erfunden, der VORLETZTE sagt die Wahrheit. Derselbe
       erfundene Endpunkt, der in V5.78 die falschen Ankunftsmeldungen erzeugt
       hat. Endet der Weg zu weit vorm Ziel, sucht der Lauf eine gemessene
       Bruecke und lenkt auf deren diesseitiges Ende um; passt keine, laeuft er
       den Teilweg trotzdem und sagt am Ende die ehrliche Restentfernung.
       Der Vorab-Check bleibt drin - wo es Saatpunkte GIBT, antwortet er richtig
       und spart einen Fehlversuch.

>>> EINE ZAHL IST NOCH UNSICHER: PathShortfallSlack = 2 m. Ein Weg gilt als
    abgeschnitten, wenn sein letzter echter Wegpunkt weiter als stopRange + 2 m
    vom Ziel liegt. Der gemessene kaputte Fall ist 3,6 m bei stopRange 0,5 m,
    also trennt 2 m die Faelle mit Luft nach beiden Seiten. Was NICHT gemessen
    ist: wie weit dieser Punkt bei einem Weg liegt, der wirklich ankommt (string
    pulling setzt ihn auf eine Polygonkante). Zu klein gewaehlt wuerde ein guter
    Weg als kaputt gelten. Genau das prueft Gegentest 3.

>>> ZU TESTEN (in-game, das kann nur der User):
      1. In Neu-Gridania zum "Uebergang nach Tiefer Wald" laufen. Erwartete
         Log-Kette: `[Bruecke] Weg endet 3,x m vor ...` -> `[Bruecke] 'Tor zum
         Tiefen Wald' passt` -> `umgelenkt - erst zur Bruecke` -> `fahre Bruecke`
         -> normaler Lauf -> ggf. `[Uebergang] Schiebe in ... hinein`.
         Kommt die Figur in den Tiefen Wald?
      2. Wenn der Anschub laeuft: kommt jetzt KEIN "nachlaufender Pfad nach dem
         Ende abgeraeumt" mehr dazwischen? Das war Befund 1.
      3. GEGENTEST, wichtig fuer die unsichere Zahl oben: irgendwohin laufen, wo
         der Weg normal durchgeht. Dort MUSS `[Bruecke] Weg reicht bis zum Ziel`
         im Log stehen, mit der gemessenen Entfernung. Steht dort statt dessen
         "Weg endet ... m vor", ist PathShortfallSlack zu klein und die Zahl aus
         dem Log sagt, auf welchen Wert.

## FRUEHERER STAND (2026-08-22, "DER UEBERGANG LAG HINTER EINER INSEL")

>>> DIE URSACHE IST GEMESSEN, UND SIE WAR NICHT DIE, DIE ICH ZULETZT ANNAHM.
    Mit dem neuen tools/navmesh-gaps (Punkt-Modus) gegen den echten Cache des
    Users geprueft, mit genau den Werten, die das Plugin uebergibt:
      - Von der Spielerposition (152,2|-13,0|151,5) aus umfasst die
        zusammenhaengende Flaeche 1466 Polygone.
      - ALLE NEUN Grenz-Kandidaten und auch die Boxmitte antworten
        "mesh here, but no route from FROM" - dort ist Boden, aber kein Weg.
      - Der ganze Uebergangsbereich haengt an einer INSEL von 36 Polygonen.
        Von der Insel aus sind alle Kandidaten UND die Boxmitte erreichbar.
      - Zwischen Spielerflaeche und Insel: 1,46 m Luecke, 0,00 m Hoehenunterschied,
        bei (153,75|-12,75|160,25) <-> (155,00|-12,75|161,00).
    DAMIT WAERE DER KANDIDATEN-FIX VON HEUTE FRUEH ALLEIN WIRKUNGSLOS geblieben:
    er haette brav alle neun durchprobiert, alle verworfen und waere beim
    naechsten Punkt gelandet - vor den Faessern. Der Fix ist trotzdem richtig,
    er repariert nur eine andere Ebene.

>>> ZUM VERGLEICH: Neu-Gridania zerfaellt insgesamt in 718 Flaechen, Limsa in
    352. Zerfallene Netze sind der Normalzustand, nicht die Ausnahme.

>>> IDEE DES USERS (ausgearbeiteter Vorschlag: Rotation + Vorwaerts-Impuls +
    Targeting-Fallback). Bewertung gegen die Messungen:
      1. Rotation: WAR SCHON GEBAUT und ist seit heute frueh repariert
         (FacingService, Atan2 + DirH-Halbdrehung). Nachgemessen: Abweichung
         0,000 nach 0,2 s und 1,0 s.
      2. Vorwaerts-Impuls: richtige Idee, aber fuer DIESEN Fall zu kurz gegriffen
         - zwischen Lauf-Ende und Grenze liegen Faesser und eine Netzinsel, kein
         freier Meter. Fuer den Fall "Lauf endet knapp vor dem Ausloeser" ist er
         genau richtig, und den gibt es.
      3. Targeting/Interact: trifft hier nicht zu. Zonengrenzen im Freien sind
         ExitRange-Triggerboxen aus planmap.lgb, es gibt nichts zum Anvisieren.
         Fuer Instanz-Eingaenge und Tueren (EventObject) gilt das anders, und den
         Weg benutzt das Plugin dort bereits.

>>> GEBAUT, BEIDES AUF WUNSCH DES USERS ("bau beides, plugin-variante"):

    1. MeshBridgeService - gemessene Netzluecken als Tabelle im Plugin.
       - Fuer den blinden Spieler ist "lauf die Luecke einmal ab" (aufgezeichnete
         Spur) untauglich: *"ich kann das nicht selber ablaufen weil ich den weg
         nicht weiss"*. Also kommen die Punkte aus der Offline-Messung.
       - Erster Eintrag: Territory 132, "Tor zum Tiefen Wald", mit der
         Herkunftsangabe direkt im Code.
       - Der Auto-Lauf fragt VOR dem Start, ob das Ziel erreichbar ist. Ist es das
         nicht und passt eine Bruecke, laeuft er zuerst ganz normal zum
         diesseitigen Ende (das liegt auf der erreichbaren Seite), faehrt die
         Luecke dann mit Path.MoveTo ab und nimmt danach das eigentliche Ziel
         wieder auf. Die Spur-Etappe wird dafuer komplett wiederverwendet -
         gleiches Problem, gleiche Fahrweise, nur andere Herkunft der Punkte.
       - Ein Eintrag gilt nur fuer Ziele innerhalb seiner gemessenen Reichweite
         (40 m), damit eine Luecke nicht fuer fremde Wege herhalten muss.

    2. ZoneTransitionHandler - der Vorwaerts-Impuls.
       - Laeuft NUR bei echten Zonengrenzen und nur, wenn die Grenze hoechstens
         6 m entfernt ist. Weiter weg heisst: da steht etwas im Weg, und blindes
         Schieben mahlt die Figur hinein.
       - Faehrt mit Path.MoveTo statt simulierter Tasten - dieselbe Mechanik wie
         die Spuren, steuert ohne Wegfindung, und wir koennen sie stoppen.
       - Bricht ab bei Zonenwechsel (Ziel erreicht), nach 3 s, oder sobald sich
         1,2 s lang nichts bewegt. Dann sagt er, dass etwas im Weg steht.
       - Richtet vorher ueber FacingService aus, also Figur UND Kamera.

    Neue Ansagen bilingual in AccessibilityStrings (BridgeCrossing,
    TransitionNudgeFailed). Debug 0/0 und Release 0/0, liegt in devPlugins.

>>> ZU TESTEN (in-game, das kann nur der User):
      1. In Neu-Gridania zum "Uebergang nach Tiefer Wald" laufen. Erwartete
         Log-Kette: `[Bruecke] 'Tor zum Tiefen Wald' passt` -> `erst zur Bruecke`
         -> `fahre Bruecke` -> danach normaler Lauf -> ggf. `[Uebergang] Schiebe
         in ... hinein`. Kommt die Figur in den Tiefen Wald?
      2. Wenn nicht: welcher Schritt fehlt im Log? Jeder hat seinen eigenen
         Marker, die Kette zeigt genau, wo sie abreisst.
      3. Gegenprobe an einer Grenze OHNE Luecke: dort darf keine Bruecke
         auftauchen, der Lauf muss unveraendert sein.

## FRUEHERER STAND (2026-08-22, "V5.89 IST DRAUSSEN - DIE NAVIGATION BLEIBT DRIN")

>>> RELEASE v5.89 VEROEFFENTLICHT UND VERIFIZIERT. Der User wollte "von allem was
    getestet ist ein release" - eine Trennung nach Teststand war nicht moeglich,
    weil Bestaetigtes und Ungetestetes in denselben Commits liegen. Gewaehlte
    Trennlinie nach Rueckfrage: alles COMMITTETE seit v5.88 (14 Commits), die
    heutige Navigationsarbeit bleibt draussen.
      - Drin: Warnstimme SAPI (bestaetigt), Einstellungen der Inhaltssuche
        (bestaetigt), Fluchtrichtung, Warnton-Auswahl, Fang-Zustand, Peil-Ton,
        Dungeonliste, Sonderaktionen, Tiefes Gewoelbe, Jagdziel-Fixes.
      - Draussen: Drehung, Ankunft/Nachfassen, Grenz-Kandidaten, zone-probe.
    Versionen synchron: csproj 5.89.0(.0), Plugin.cs "5.89", repo.json
    5.89.0.0. Sechs Assets haengen am Release. Gegengeprueft durch Download von
    releases/latest: latest.zip meldet AssemblyVersion 5.89.0.0.

>>> INSTALLER MUSSTE MIT: sein Quellcode wurde seit v5.88 geaendert (zwei
    Namenskonstanten fuer fremde Plugins, Commit 9ec2f24). Deshalb 1.1.0.0
    -> 1.2.0.0, exe neu gebaut, SHA256 in installer.json nachgezogen und gegen
    die veroeffentlichte Datei geprueft. Waere die Version stehengeblieben,
    haette der Installer das Update endlos angeboten.

>>> OFFEN, BRAUCHT DEN USER: der Merge-Commit mit der heutigen Navigationsarbeit
    liegt LOKAL auf main, der Push wurde von der Berechtigungspruefung
    abgelehnt. Der Release ist davon nicht betroffen (Tag zeigt auf den
    Bump-Commit). Zu tun: `git push origin main`, dann den Zweig
    `nav-2026-08-22` loeschen.

>>> SCHOENHEITSFEHLER, NICHT ANGEFASST: System.Speech.dll liegt DOPPELT im ZIP
    (einmal oben, einmal unter runtimes/win/lib/net9.0/), je 685 KB. Daher der
    Sprung von 974 KB auf 1,52 MB. Kurz vor einem Release nicht mehr angefasst -
    fuer das naechste Mal notiert.

## FRUEHERER STAND (2026-08-22, "DIE KAMERA SCHAUTE GENAU VERKEHRT HERUM")

>>> RUECKMELDUNG DES USERS ZUM TEST: *"nein er steht warscheinlich immer noch
    falsch das mit dem uebergang hat immernoch nicht funktioniert aber die
    entfernungsangabe sollte jetzt stimmen"*. Also: Punkt 3 der Testliste
    (Entfernung) bestaetigt, Punkte 2 (Drehung) und 7 (Uebergang) nicht.

>>> DIE DREHUNG WAR EIN RECHENFEHLER, UND ER STAND SEIT HEUTE FRUEH IM LOG.
    Die manuelle Taste loggt `[Face] vorher/nachher`. Ueber 14 Messungen am
    2026-08-22 zwischen 12:45 und 12:48 gilt in der Ruhelage ausnahmslos:
      Rotation - DirH = 3,142   (= Pi, auf drei Stellen, jedes Mal)
    Das galt auch dort, wo der Spieler sich zwischen zwei Messungen selbst
    gedreht hatte - die Kopplung wanderte mit. Es ist also die Ruhelage des
    Spiels und kein Zufall einer Pose.
      - Bedeutung: `Camera.DirH` ist NICHT die Blickrichtung der Kamera, sondern
        die Richtung von der Figur ZUR Kamera. Die Kamera steht hinter der Figur.
      - Wir schrieben `DirH = rotation`. Das zielte die Kamera um eine halbe
        Drehung verkehrt - und weil der Bewegungsmodus KAMERARELATIV ist
        (moveMode=0, im selben Log mitgemessen), lief "geradeaus" damit vom Ziel
        WEG statt hin. Genau die Beschwerde des Users.
      - GEFIXT in FacingService: `DirH = rotation - Pi`, in [-Pi, Pi] gefaltet.
        Debug 0/0, liegt in devPlugins.

>>> NOCH OFFEN AN DER DREHUNG, deshalb neu gemessen statt geraten: haelt unser
    Schreiben ueberhaupt? Im Log stand der Wert Sekunden spaeter wieder bei
    rotation - Pi, auch wo die Figur sich nicht gedreht hatte. Zwei moegliche
    Ursachen, die ein Ruecklesen im SELBEN Frame nicht trennen kann - das Spiel
    zieht die Kamera zurueck, oder unser Schreiben erreicht gar nicht die Kamera,
    die das Spiel liest.
      - NEU: `FacingService.Tick`, laeuft jeden Frame in Plugin.cs (bewusst
        AUSSERHALB des Laufs, denn gedreht wird im Moment des Laufende).
      - Liest 0,2 s und 1,0 s nach jeder Drehung Rotation und DirH erneut und
        loggt Soll, Ist und Abweichung, dazu `ActiveCameraIndex`.
      - Log-Marker: `[Drehung] gesetzt:` und `[Drehung] nach 0,2s:`.

>>> DER UEBERGANG: DAS NETZ ENDET VIER METER ZU FRUEH, UND DA IST NICHTS IM WEG.
    Bewiesen aus zwei Quellen desselben Logs, Neu-Gridania nach Tiefer Wald:
      - Der Auto-Lauf endet reproduzierbar mit `restWp=1, Netzende=True` bei
        dist 3,5 bis 4,0 m zum Grenzpunkt (12:36:53, 12:37:04, 12:37:22,
        12:38:31, 12:39:39 - fuenf Laeufe, dasselbe Bild).
      - Danach ging der Spieler die Strecke ZU FUSS: die ExitProbe hat sie
        aufgezeichnet (12:44:42). Von X=148,7 ueber 154,5 bis 161,6, und die
        Hoehe blieb dabei konstant bei Y=-12,9. Also ebener, freier Boden -
        keine Stufe, keine Wand, kein Moebelstueck.
      - Ab X=154,5 meldete die Sonde DRIN. Unser Grenzpunkt liegt bei X=156,4,
        also korrekt innerhalb der Box. Das Ziel stimmt, der Weg dorthin fehlt.
    ERGEBNIS: die neue Zielkoordinate ist richtig und die Entfernungsansage
    stimmt (der User bestaetigt es, das Log zeigt 4,4 m statt 18,6 m). Es
    scheitert allein daran, dass vnavmeshs Netz an dieser Stelle vier Meter vor
    der Grenze aufhoert.

>>> ERSTER TEST DES USERS: Drehung technisch bestaetigt, Uebergang weiter tot.
    `[Drehung] nach 0,2s` und `nach 1,0s` melden beide Abweichung 0,000 bei
    kameraIndex=0 - unser Schreiben HAELT also, die zweite offene Frage von oben
    ist damit beantwortet. Der User hat ausserdem `/vnav rebuild` laufen lassen
    (13:15:01): die Laeufe davor und danach stoppen bei identisch X ~ 152,5, der
    Rebuild aendert nichts.

>>> UND DANN DIE FRAGE DES USERS, DIE ALLES AUFGELOEST HAT: *"wir haben doch die
    quelle wir koennen das wegenetz aus dem spiel extrahieren das heisst wir
    wissen auch wo gegenstaende sind und was auf welchen koordinaten ist"*.
    Richtig - und genau das war der fehlende Schritt.

>>> NEU: tools/zone-probe (Konsolenprogramm, Lumina, offline gegen das sqpack).
      `zoneprobe <territoryId> <x> <z> <radius> [y]` listet jedes Layout-Objekt
      im Umkreis, nach Typ gruppiert und nach Entfernung sortiert.
      Es sind DIESELBEN Daten, aus denen vnavmesh rechnet: lebendes Layout plus
      die .pcb-Kollisionsdateien (SceneExtractor.cs:165/177/305). Was hier mit
      Kollision steht, ist auch fuer das Netz eine Wand.

>>> DAS ERGEBNIS - `zoneprobe 132 154.5 152.0 10 -12.9`, 37 Objekte:
      - SIEBEN FAESSER mit Box-Kollision (`f1t0_a0_taru1.mdl`) bei X 150,6-154,6
        und Z 152,4-154,0. Genau dort, wo die Figur stehenbleibt.
      - DAS TORBAUWERK mit echter .pcb-Kollision: `gatdr` bei Z 154,8, Bogen
        `gat00` bei Z 158,0, `gatdl` bei Z 161,2.
      - DREI UNSICHTBARE WAENDE (CollisionBox, `pushPlayerOut=31`) bei
        X 153,2-156,0, zusammen durchgehend von Z 147 bis Z 168. Layer
        `QST_OP_ENPC_001` - eine Quest-Ebene, die das Spiel je nach Fortschritt
        schaltet. Ob sie fuer diesen Spieler aktiv ist, sagen die Dateien NICHT.
      - Der Rest ist Deko mit `collision=None` und stoppt niemanden.
    MEINE ERKLAERUNG VON VORHER WAR FALSCH: das Netz endet dort nicht zu frueh,
    es endet KORREKT vor den Faessern. "Ich stecke fest" war die richtige
    Meldung. Der Fehler lag bei uns.

>>> GEBAUT: der ZoneBorderService bietet jetzt MEHRERE KANDIDATEN an.
      - Bisher: der geometrisch naechste Punkt der Grenzbox. Der lag bei Z 150,5,
        also mitten in den Faessern. Der User kam zu Fuss bei Z 155,5-156,9
        durch - durch die Toroeffnung, noerdlich an den Faessern vorbei.
      - Jetzt: Kandidaten im 3-m-Abstand entlang der Grenze, abwechselnd nach
        beiden Seiten, hoechstens neun. Der naechste, den das Netz annimmt,
        gewinnt. Reihenfolge = Entfernung, es wird also nur weitergesucht, wo
        der nahe Punkt blockiert ist - im Normalfall genau eine Abfrage.
      - Gestreut wird entlang der Achse, durch die der Spieler NICHT hereinkommt
        (aus der Ueberschreitung je Halbmass abgeleitet), also ueber die BREITE
        der Grenze - dort, wo ein Durchgang sich verstecken kann.
      - Die Pruefung ist vnavmeshs eigenes `Query.Mesh.NearestPointReachable`,
        das mit einem `FloodFillAwareFilter` filtert - Erreichbarkeit, nicht
        blosses "ist da Boden" (NavmeshQuery.FindNearestMeshPoly).
      - WICHTIG, das war der stille Fehler: die Abfrage laeuft mit 2 m Suchradius
        statt der 20 m, die ResolveReachablePoint benutzt. Bei 20 m antwortet sie
        fast immer und verschiebt das Ergebnis meterweit - so ging ein Ziel
        MITTEN IN DEN FAESSERN als erreichbar durch (Log 12:37: NearestPoint-
        Reachable schob 146,2 auf 150,5). Neue Methode: AutoWalkService.
        ProbeReachable, eng und ohne Rueckfall.
      - Lehnt das Netz ALLE Kandidaten ab, bleibt es beim naechsten Punkt und der
        Lauf meldet ehrlich Rest und Richtung. Ohne Pruef-Funktion verhaelt sich
        der Dienst exakt wie vorher.
    OFFLINE NACHGERECHNET fuer den Fall des Users: Streuachse Z (richtig),
    Kandidat 1 = (156,5|153,5), KANDIDAT 2 = (156,5|156,5) - das ist die
    Toroeffnung. Debug 0/0 und Release 0/0.

>>> ZU TESTEN:
      1. Uebergang: in Neu-Gridania zum Uebergang nach Tiefer Wald laufen. Im Log
         muss `[Grenze] ... Kandidat N/9, M abgelehnt` stehen. Kommt die Figur
         diesmal an der Grenze an und wechselt die Zone?
      2. Drehung im Spielgefuehl: die Werte stimmen jetzt nachweislich. Fuehrt
         geradeaus nach der Ansage auch wirklich zum Ziel?
      3. Gegenprobe an einer Grenze OHNE Hindernis (z. B. eine andere Zone), dass
         die Kandidatenwahl nichts kaputt macht: dort muss `Kandidat 1/9` stehen.

## FRUEHERER STAND (2026-08-22, "ANKUNFT HIESS NICHT ANKUNFT - UND NIEMAND STAND RICHTIG")

>>> AUFTRAG DES USERS, nachdem der Navimesh-Weg geklaert war: *"fuer uns waere
    halt wichtig das wir immer richtig ankommen, immer zum ziel gedreht"* und
    *"wenn man zu einem uebergang laeuft ... das man wenn man gradeaus laeuft
    direkt drauf zu geht und nicht versehen zu einer wand guckt"*.
    Entscheidung des Users: erst Teil 1 und 2, Drehung UEBERALL. Die echten
    Zonengrenzen (Teil 3) kommen spaeter.

>>> DER FEHLER STAND IM LOG UND WAR UNSERER. ArrivalSlack war 1,5 m, und der
    Kommentar daneben begruendete das damit, die Figur schiesse ueber das Ziel
    HINAUS. Gemessen tut sie das Gegenteil - sie bleibt DAVOR stehen. Log
    2026-08-21 22:11:03, Lauf zum Rudererquartier mit stopRange 1,0:
      - vnavmesh gab bei 2,4 m auf, wir meldeten "Ziel erreicht"
      - der Spieler drueckte erneut, vnavmesh fand einen Weg mit 4 Wegpunkten
        (also einen Bogen um ein Hindernis), und wir beendeten den Lauf nach
        18 Millisekunden, ohne einen einzigen Schritt
      - danach dreimal "Falsche Aktion oder Ziel": er stand 2,4 m daneben

>>> DAS SCHLIMMERE AM ZU GROSSEN ZUSCHLAG: er hat nicht nur falsch gemeldet, er
    hat ALLES DARUNTER ABGESCHALTET. Die Zweige "hier endet der begehbare Weg",
    "festgesteckt" und die aufgezeichnete Spur haengen alle hinter dieser
    Pruefung und wurden nie erreicht. Die ehrlichen Meldungen gab es laengst -
    wir haben sie uns selbst abgeschnitten.

>>> GEBAUT (Debug 0/0, liegt in devPlugins, IN-GAME UNGETESTET):
      - ArrivalSlack 1,5 -> 0,3 m (nur noch Frame-Versatz).
      - Nachfassen: geht vnavmesh vor dem Ziel aus, wird der Weg bis zu zweimal
        neu angefordert statt den Lauf zu beenden. Zweifach begrenzt - hoechstens
        MaxReengages Versuche, und nur solange jeder Versuch messbar naeher kam
        (ReengageProgress 0,5 m). Wo das Netz wirklich endet, faellt die zweite
        Bedingung sofort durch und der Spieler hoert die Wahrheit.
      - Drehung zum Ziel UEBERALL: bei der Ankunft, bei "du bist schon da", und
        auch dann, wenn wir es NICHT geschafft haben - die Meldung nennt Rest und
        Richtung, und geradeaus soll dann stimmen.
      - Neu FacingService: die Dreh-Mechanik an EINER Stelle. Sie schreibt zwei
        Felder, und das zweite ist das wichtige: der Standard-Bewegungsmodus ist
        KAMERARELATIV, geradeaus geht also dorthin, wo die KAMERA schaut. Nur die
        Figur zu drehen haette das Problem nicht geloest.
      - NavigationService.FaceGuideDirection (die manuelle Taste) nutzt jetzt
        denselben Code, damit beide nicht auseinanderlaufen koennen.
      - Nach einem Nachfassen ohne Weg sagt der Lauf nicht mehr "kein Weg zu X",
        sondern nennt Restweg und Richtung.

>>> NEBENBEFUND, schon im Code dokumentiert und jetzt bestaetigt: vnavmeshs
    Cache-Schluessel ist {bg}__{filter}__{festivals}__{zoneSGs} - KEINE
    Spielversion. Netze ueberdauern Patches. Im Cache liegen Dateien vom 29.
    Juli, das Spiel ist von 2026.08.11.

>>> NACHGEZOGEN auf Rueckfrage des Users (*"und er sollte sich beim navigieren
    und beim autolaufen richtig hindrehen?"*):
      - JEDES Ende eines Auto-Laufs dreht jetzt zum Ziel, nicht nur die Ankunft:
        auch "festgesteckt", "Netz endet hier" und "keine Annaeherung". Die
        Meldung nennt Rest und Richtung - geradeaus muss dann stimmen. Einzige
        Ausnahme ist der Zonenwechsel: dort gehoert das Ziel zur alten Karte.
      - Die Gehhilfe dreht bei ihrer Ankunft ebenfalls; vorher tat sie es gar
        nicht.
      - REIHENFOLGE KORRIGIERT, das war ein echter Fehler im ersten Wurf: erst
        Finish (das haelt vnavmesh an), DANN drehen. vnavmeshs FollowPath haelt
        eine OverrideCamera, solange es steuert - eine Drehung davor kann es uns
        direkt wieder abnehmen.

>>> BEWUSST NICHT GEBAUT, weil es eine Entscheidung des Users braucht:
    fortlaufendes Drehen WAEHREND des Laufens.
      - Auto-Lauf: vnavmesh haelt OverrideMovement UND OverrideCamera, solange es
        steuert. Jeder Frame, in dem wir dagegen drehen, waere ein Ringen mit ihm.
      - Gehhilfe: dort fuehrt der PEIL-TON, und der ist Absicht - der Spieler
        dreht sich selbst, bis es still ist. Automatisches Drehen wuerde ihm
        diese Steuerung abnehmen. Frage liegt beim User.

>>> DIE FRAGE DES USERS *"bei welchen koordinaten liegt das ziel und warum
    laeuft vnavmesh nicht richtig zum ziel, da ist eine wand die vnavmesh
    anscheinend nicht in seinen koords hat"* - offline beantwortet, und das
    Ergebnis ist genau UMGEKEHRT:

    - Das Ziel war (-97,0|64,0) auf Karte 31. Karte 31 ist die SASTASHA-HOEHLE
      (Territory 1036) - der Vorfall passierte im Dungeon, was auch der Rest des
      Logs bestaetigt (Kampf-NPCs Stufe 15, Errungenschaft "Kartograph:
      Sastasha"). Die Koordinate stammt aus dem MapMarker-Sheet, DataType 0 =
      benannter Ort. Also KEIN Uebergang - die ExitRange-Daten helfen hier nicht.
      Offline nachgerechnet (Pixel 830|1152, SizeFactor 200) ergibt exakt die
      Log-Koordinate; die Umrechnung des Plugins stimmt.
    - Am Zielpunkt steht sehr wohl etwas, und vnavmesh KENNT es: im 12-m-Umkreis
      liegen 61 Hintergrundobjekte des Layers "hideout", das naechste 1,1 m vom
      Ziel entfernt, dazu FUENF ChairMarker (LVD_chair_01) im Umkreis von 1,3 bis
      5,7 m. Das "Rudererquartier" ist ein moebliertes Zimmer, und der
      Kartenmarker sitzt in der RAUMMITTE - also mitten im Mobiliar.
    - Damit ist die Diagnose: vnavmesh laeuft nicht falsch. Es haelt korrekt vor
      Moebeln, die es kennt. FALSCH WAR UNSER ZIELPUNKT - eine Stelle, auf der
      man gar nicht stehen kann. Die Figur blieb 2,44 m davor.

>>> DIE KORREKTUR LAG SCHON IM PROJEKT: ResolveReachablePoint
    (NearestPointReachable) gab es bereits fuer das Tiefe Gewoelbe, wo dasselbe
    Problem geloest wurde - der Raumursprung aus der Layout-Datei liegt dort oft
    in einer Wand. Nur benutzten die normalen Ortsziele weiterhin
    ResolveFloorPoint (NearestPoint), das den Punkt bloss senkrecht aufs Netz
    hebt und dabei im Moebelstueck bleibt. Ortsmarker gehen jetzt ueber
    ResolveReachablePoint; der Ruecksprung auf den alten Weg ist eingebaut.

>>> DER UEBERGANGS-FALL DES USERS, gemessen (Log 2026-08-22 10:53, Neu-Gridania
    nach Tiefer Wald - *"das ist das wo er stehen geblieben ist"*):
      - Kartensymbol (170,0|162,0), echte ExitRange (170,1|-10,6|159,0) - nur
        3,0 m auseinander. Das Kartensymbol war hier also NICHT das Problem, und
        Teil 3 (echte Grenzen als Ziel) haette diesen Fall NICHT geloest.
      - Der Lauf endete bei (152,5|-13,0|165,0): 18,6 m vor der Grenze, 2,4 m
        tiefer. Im Log ueber Sekunden konstant restWp=1, distNextWp=17,8 - das
        ist Fakt 3, vnavmesh haengt das Ziel unbedingt an und schiebt gegen den
        Netzrand.
      - Dazwischen liegt ein Torbauwerk (Layer f0t0_b1_gate, 25 Objekte auf zwei
        Hoehen), dahinter steigt das Gelaende.
      - Also: DAS NETZ REICHT NICHT BIS ZUR GRENZE. Passt zur Parameter-
        Hypothese (0,50 m gegen 0,60-1,00 m Stufenhoehe), beweist sie aber nicht.

>>> NEU GEBAUT: Debug-Sonde ZoneExitProbe (nur #if DEBUG, nach der Messung
    loeschen). Sie beantwortet die eine Frage, die den Unterschied macht: ist
    ExitRange.Scale das HALB- oder das VOLLMASS? Bei Halbmass reicht die Box bis
    X = 154,5 und der Spieler stand bei 152,5 - nur ZWEI Meter davor, und ein
    Ziel am BOXRAND statt in der Boxmitte wuerde reichen, ganz ohne besseres
    Netz. Bei Vollmass fehlen zehn Meter.
      - Ringpuffer der letzten 4 Sekunden (10 Positionen je Sekunde), weil die
        Position im Moment des Ausloesens spaeter nicht mehr abfragbar ist.
      - Beim Wechsel wird JEDE gepufferte Position gegen beide Lesarten geprueft,
        in Boxkoordinaten (Drehung der Box herausgerechnet).
      - Boxen aus planmap.lgb ueber Lumina, nicht aus der Layout-Engine: fuer
        eine Messung braucht es keine Zeigerketten, die bei Patches brechen.
      - Kein Startbefehl noetig - der Puffer muss ja VOR dem Wechsel laufen.

>>> SONDE HAT GEMESSEN, FRAGE IST BEANTWORTET: ExitRange.Scale ist das HALBMASS.
    Zwei Durchgaenge durch dieselbe Grenze, in beide Richtungen:
      - Hinweg 132 -> 148, Box Scale (15,6|3,8|15,0): HALB ab 0,8 s vor dem
        Wechsel DRIN, VOLL NIE.
      - Rueckweg 148 -> 132, Box Scale (36,5|40,4|15,3): HALB ab 1,0 s DRIN,
        VOLL NIE.
      - VOLL ist damit widerlegt - der Uebergang loeste beide Male aus, obwohl
        die Figur nach dieser Lesart nie in der Box war. Die 0,8 bis 1,0 s sind
        die Uebergangsverzoegerung, in beiden Richtungen fast gleich.
    ZWEI BESTAETIGUNGEN GRATIS: die Laufrichtung stimmt auch in-game (85 Grad =
    +X, gelaufen von X 153 auf 159; 180 Grad = -Z, gelaufen von Z -298 auf -321),
    und das ZielTerritory der Layout-Daten stimmte beide Male mit der wirklich
    geladenen Zone ueberein.

>>> GEBAUT: ZoneBorderService. Uebergangs-Marker zielen jetzt auf den NAECHSTEN
    PUNKT DER GRENZBOX statt auf das Kartensymbol in ihrer Mitte. Fuer den Fall
    des Users heisst das: Boxrand bei X = 154,5, Lauf endete bei X = 152,5 -
    2,0 m statt 18,6 m.
      - Rechnet in Boxkoordinaten, eine gedrehte Grenze wird also genauso
        behandelt wie eine achsenparallele.
      - 2 m Versatz nach INNEN statt genau auf die Kante (eine Kante ist ein
        Muenzwurf), aber nie ueber die Mitte hinaus.
      - Mehrere Grenzen zur selben Zone: die naechstgelegene gewinnt (Gridania
        hat zwei nach Tiefer Wald).
      - Ohne passende Grenze - Tueren, Instanz-Eingaenge - bleibt alles beim
        Alten. Danach laeuft der Punkt durch ResolveReachablePoint wie jedes
        andere Ziel.
      - BEWUSST KLEIN: kein Durchlauf, keine Umleitung, keine Aenderung an der
        Wegfindung. Nur eine bessere Zielkoordinate. Der alte, groessere
        ZoneExitService bleibt geloescht.
    Debug 0/0 UND Release 0/0.

>>> BEKANNTE KLEINIGKEIT: die Ortsliste sagt weiter die Entfernung zum
    KARTENSYMBOL an ("23 Meter"), gelaufen wird zur Grenze. Die Zahlen koennen
    also ein paar Meter auseinanderliegen. Bewusst so gelassen - die Liste
    umzustellen haette Sortierung und Ansagen aller Wegpunkte beruehrt.

>>> ZU TESTEN (das kann nur der User):
      1. Zu einem Ortsmarker laufen, an dem es frueher "Ziel erreicht" hiess und
         die Interaktion dann scheiterte. Kommt die Figur jetzt heran?
      2. Steht sie nach der Ankunft zum Ziel gedreht - fuehrt geradeaus hin?
      3. Wenn es nicht klappt: kommt statt "Ziel erreicht" die ehrliche Ansage
         mit Restmetern und Richtung?
      4. Log-Marker fuer die Nachlese: "[Nav] Auto-Lauf: Nachfassen 1/2".
      5. In einem Raum mit Moebeln (z. B. Sastasha "Rudererquartier") - im Log
         muss jetzt "[Orte] NearestPointReachable" stehen statt
         "[Orte] NearestPoint", und der Lauf sollte im Raum ankommen.
      6. ERLEDIGT: Sonde hat gemessen, Scale ist das Halbmass.
      7. NEU: Auto-Lauf zu "Uebergang nach Tiefer Wald" in Neu-Gridania. Im Log
         muss "[Grenze] Ziel-Territory 148 ... -> naechster Punkt" stehen, und
         die genannte Entfernung sollte deutlich kleiner sein als die zur Mitte.
         Kommt die Figur diesmal an der Grenze an?

## FRUEHERER STAND (2026-08-22, "DAS SPIEL-NAVIMESH LIEGT AUF DEM SERVER - ABER DAS REZEPT LIEGT BEI")

>>> BESTAETIGT VOM USER: die Warnstimme ueber SAPI funktioniert im Spiel. Damit
    ist der zweite Sprachkanal fertig. Das Log zeigt beim Laden
    "[Warnstimme] SAPI bereit. Stimmen: Microsoft Hedda Desktop, SAPI2SR,
    Microsoft David Desktop, Microsoft Zira Desktop. Gewaehlt: 'Microsoft Hedda
    Desktop'." - die offene Frage, ob SAPI im Dalamud-Prozess ueberhaupt laeuft,
    ist damit beantwortet. Der COM-Ausweichweg wird nicht gebraucht.

>>> DER AUFTRAG DES USERS: weg von vnavmesh. Zwei Gruende, beide gut. Erstens
    steht das Plugin durch die Abhaengigkeit im Automatisierungs-Oekosystem und
    faengt sich den Botting-Vorwurf ein. Zweitens sollen die Wege endlich
    stimmen - inklusive Treppen.

>>> DIE ERSTE ANTWORT IST EIN NEGATIVBEFUND, und er ist hart gemessen: das
    fertige Navimesh des Spiels ist NICHT im Client. Jede Zonendatei .lvb nennt
    ihre Bestandteile im Klartext, und die Navimesh-Zeilen tragen das Praefix
    /server/data/. Ueber alle Zonen: 873 von 873 verweisen dorthin, NULL Zonen
    haben eine .nvm oder .nvx im sqpack. Kein Werkzeug holt heraus, was nie
    ausgeliefert wurde - auch FFXIV Explorer nicht.

>>> DIE ZWEITE ANTWORT WIEGT DAS AUF: das Netz fehlt, das REZEPT liegt bei. Das
    Sheet RecastNavimesh steht im Client, 8 Zeilen, und Lumina liefert die
    Spaltennamen mit: TileSize, CellSize, CellHeight, AgentHeight, AgentRadius,
    AgentMaxClimb, AgentMaxSlope und der Rest von rcConfig. Square Enix baut
    seine Netze also mit Recast - derselben Bibliothek wie vnavmesh.

>>> DER VERGLEICH ZEIGT EINE ECHTE LUECKE, nicht nur Rundungsrauschen. Die
    Kletterhoehe wird in Recast in Voxeln gerechnet (RcConfig.WalkableClimb ist
    Int32, daneben WalkableClimbWorld in Metern):
      - vnavmesh:            0,5 / 0,25 = 2 Voxel = 0,50 m
      - Spiel (default):     0,6 / 0,2  = 3 Voxel = 0,60 m
      - Spiel (w1d1, r1f1):  1,0 / 0,2  = 5 Voxel = 1,00 m
    vnavmesh haelt also nur halb so hohe Stufen fuer begehbar wie das Spiel in
    seinen grosszuegigen Zonen, und arbeitet zusaetzlich groeber aufgeloest
    (0,25 statt 0,2 in Breite und Hoehe).

>>> HYPOTHESE, AUSDRUECKLICH NOCH NICHT BEWIESEN: das ist die Ursache unserer
    wiederkehrenden Netzschaeden - Oestliches La Noscea zerfaellt, Astalicia
    unerreichbar, Wohngebiet-Zaeune, fehlende Treppen. Beweis geht nur ueber
    einen Neubau unter Spielwerten plus Gegentest an einer bekannt kaputten
    Stelle.

>>> VON AUSSEN AENDERBAR IST DAS NICHT: vnavmesh haelt seine Settings im Code,
    inklusive fest einkompilierter NavmeshCustomization-Klassen pro Zone.
    vnavmesh.json kennt nur StopOnStuck/RetryOnStuck/BuildMaxCores.

>>> WAS DEN EIGENBAU REALISTISCH MACHT: DotRecast liegt als fertige
    C#-Portierung vor (Core, Recast, Detour, Detour.Extras) - vnavmesh liefert
    sie selbst mit. Zu bauen waere die Extraktion der Kollisionsgeometrie und
    die Wegsuche darauf, nicht Recast selbst.

>>> NEU/GEAENDERT: nur Dokumentation - docs/game-api.md um zwei Abschnitte
    ergaenzt (Serverbefund + Spaltenzuordnung/Vergleich). Kein Code angefasst.

>>> NAECHSTER SCHRITT, offen zur Entscheidung: Gegentest, ob die Spielwerte die
    kaputten Stellen heilen, bevor Monate in einen Eigenbau fliessen.

## FRUEHERER STAND (2026-08-21, "WARNSTIMME: DIE WARNUNG BEKOMMT EINEN EIGENEN KANAL")

>>> WUNSCH DES USERS: *"die angriffs warunugen kommen ja ueber nvda aber das kann
    weggedrueckt werden koennen wir das ueber sapi hoerbar machen?"* - und der
    Befund dahinter stimmt: der Screenreader hat GENAU EINE Sprachwarteschlange,
    und das Plugin raeumt sie selbst staendig ab. Jedes SpeakInterrupt schneidet
    ab, was gerade laeuft. Ein Zielwechsel, eine Chatzeile oder die Stopptaste des
    Spielers loescht damit eine Warnung mitten im Satz.

>>> ZWEI ENTSCHEIDUNGEN HAT DER USER SELBST GETROFFEN (gefragt, weil sie das
    Ergebnis aendern):
      1. NUR SAPI, nicht zusaetzlich zu NVDA - kein Echo, kein doppelter Satz.
      2. NUR DIE VIER KAMPFWARNUNGEN: Zauber-Warnung mit Gefahrenform, "du stehst
         in der Flaeche", Fluchtrichtung und "kein sicherer Punkt". HP-Warnung und
         "Kampf beginnt/endet" bleiben auf dem Screenreader.

>>> TOLK KANN DAS NICHT LEISTEN, und das ist der Grund fuer den eigenen Dienst:
    die Bibliothek kennt SAPI zwar (Tolk_TrySAPI), benutzt es aber als ERSATZ,
    wenn kein Screenreader laeuft - nicht daneben. Ueber Tolk gaebe es weiterhin
    einen einzigen Kanal, nur mit einer anderen Stimme darin.

>>> GEMESSEN STATT ANGENOMMEN, drei Dinge:
    1. System.Speech 9.0.0 laesst sich fuer net10.0-windows aufloesen und bauen
       (Testprojekt im Scratchpad, nicht im Repo).
    2. SpeakAsync kehrt nach 1 ms zurueck. Das ist die Bedingung dafuer, dass der
       Aufruf im Frame-Update stehen darf - blockierendes Sprechen wuerde das
       Spiel bei jeder Warnung anhalten.
    3. Auf diesem Rechner sind Stimmen fuer BEIDE Plugin-Sprachen da: Hedda
       (de-DE), David und Zira (en-US).

>>> EINE FALLE, DIE ERST IM KAMPF ZUGESCHNAPPT WAERE: das NuGet-Paket liefert
    System.Speech ZWEIMAL - flach die plattformneutrale Fassung (310 KB, wirft
    ausserhalb von Windows) und unter runtimes\win\lib\ die echte
    Windows-Implementierung (685 KB). Welche geladen wird, entscheidet sonst der
    Abhaengigkeits-Aufloeser anhand der deps.json - und die liegt in devPlugins
    gar nicht, weil der Debug-Deploy nur einzelne DLLs kopiert. Das neue Target
    FlattenSpeechRuntime kopiert die Windows-Fassung flach darueber; geprueft ist
    es an der Dateigroesse in devPlugins (685360 Bytes) und im Release-ZIP.

>>> DER RUECKFALL IST DER WICHTIGE TEIL. Uebernimmt der zweite Kanal nicht (keine
    Sprachausgabe im System, abgeschaltet, Lautstaerke auf 0), geht die Warnung
    wieder ueber den Screenreader. CombatService.SpeakWarning ist genau diese eine
    Verzweigung. Eine Warnung, die verschwindet, weil ein Kanal fehlt, waere das
    schlechtestmoegliche Ergebnis - Stille ist von "keine Gefahr" nicht zu
    unterscheiden.

>>> WAS SONST STILL VERLOREN GEGANGEN WAERE: SpeakInterrupt macht mehr als
    sprechen - es reinigt SeString-Reste (in einer Cast-Warnung steckt der
    AKTIONSNAME des Spiels, und der kommt mit Nutzlasten und Symbolzeichen),
    entprellt gleiche Texte innerhalb einer halben Sekunde und protokolliert
    jeden Sprechvorgang. Der neue Kanal tut alles drei selbst, mit derselben
    Schwelle. Die Entprellung gibt dabei ABSICHTLICH true zurueck: mit false
    haette der Aufrufer den Satz auf den Screenreader zurueckgeworfen, und der
    Spieler haette ihn doch zweimal gehoert, nur auf zwei Stimmen verteilt.

>>> IM MENUE unter "Toene": Schalter, Lautstaerke (Standard 1,0 - dieser Kanal
    muss gegen Spielgeraeusche UND die laufende Screenreader-Stimme ankommen),
    Tempo in sieben Stufen mit Worten statt Zahlen, und die Stimmenwahl mit
    "Automatisch" zuoberst. Jede Wahl spielt sofort einen echten Warnsatz vor -
    "Kegel von vorne. Nach rechts ausweichen, sieben Meter." - und keinen
    Test-eins-zwei: beurteilt werden soll, ob man SIE im Kampf versteht.

>>> NEU/GEAENDERT: WarningVoiceService.cs (neu), CombatService.cs (SpeakWarning +
    vier Aufrufstellen), Configuration.cs, AccessibilityStrings.Chat.cs,
    OptionsMenu.cs, Plugin.cs, FF14Accessibility.csproj, THIRD-PARTY-NOTICES.md
    (System.Speech, MIT - Hinweispflicht), README.md, README.en.md.
    Build Debug 0/0 UND Release 0/0, liegt in devPlugins. IN-GAME UNGETESTET.

>>> DIE EINE OFFENE FRAGE, DIE NUR DAS SPIEL BEANTWORTET: ob SAPI im
    Spielprozess ueberhaupt laeuft. Es ist COM, und Dalamud laedt Plugins in einen
    eigenen Kontext - das ist am Schreibtisch nicht zu beweisen. Das Log sagt es
    beim Laden: "[Warnstimme] SAPI bereit. Stimmen: ..." oder "[Warnstimme] SAPI
    nicht verfuegbar (...)". Kommt die zweite Zeile, ist der Ausweichweg SAPI
    direkt ueber COM (SAPI.SpVoice per Reflection), ganz ohne das NuGet-Paket.

## FRUEHERER STAND (2026-08-21, "PR 6 WAR FAST GANZ DA - OFFEN WAR EIN EINZIGER COMMIT")

>>> FRAGE DES USERS: *"was hat pr sechs fuer konflikte schau auf den github"*.
    Die Antwort war nicht die erwartete: PR #6 lag zum groessten Teil laengst in
    main. Der Hauptcommit bf5db54 ist der GEMEINSAME VORFAHRE beider Zweige, er
    liegt 26 Commits zurueck. Alle sieben DeepDungeon*.cs sind da, sechs davon
    byte-identisch mit dem PR-Stand.

>>> OFFEN WAR GENAU EIN COMMIT: 6693669 vom 13.08., der Nachreich-Commit. Er
    bringt zwei Dinge, beide an vorhandene Anschlusspunkte gehaengt, ohne neue
    Taste:
      - Die Beschreibung EINES Platzes der Charakterinfo an die Detailtaste
        (TryReadDeepPanelDetail, in der Kette hinter TryReadItemDetail). Der
        fokussierte Knoten kommt LIVE aus AtkStage, nicht aus dem gemerkten
        _lastFocusedNodePtr - der zeigt nach dem Schliessen des Fensters auf
        freigegebenen Speicher, und ein Lesevorgang darauf wuerde das Spiel
        abstuerzen lassen statt eine Ausnahme zu werfen.
      - Die ebenenweiten Wirkungen an der Ebenen-Taste (Strg+F). Sie stehen
        NICHT in der StatusList des Spielers - ein Effekt-Leser sieht sie nie;
        sie liegen auf dem Content-Director. Laeuft nichts, wird nichts
        angehaengt, statt "keine Wirkungen" zu sagen.

>>> VIER KONFLIKTE, KEINER EIN INHALTLICHER WIDERSPRUCH. Sie kommen alle aus den
    26 Commits, die main seit dem Abzweig dazubekommen hat:
      - Configuration.cs, zweimal: main hat KeyOptionsMenu, KeyToggleBeacon und
        TargetBeaconEnabled dazu, der PR beruehrte nur die Kommentare daneben.
        main behalten, das Praefix [Tiefes Gewoelbe] nachgezogen.
      - Plugin.cs: der PR bringt eine ZWEITE IGameConfig-Deklaration mit, die in
        main schon zusammengefuehrt wurde (der Kommentar dort sagt das
        ausdruecklich). Eine zweite waere ein Compile-Fehler.
      - NavigationService.cs, zweimal: dazwischen liegen inzwischen der
        Jagdtagebuch-Zaehler, die Dungeonliste und die Zweige fuer Gegner,
        Verbuendete und Inhalte. Alle bleiben.
      - UIReaderService.cs: DescribeGearsetMark (main) und TryReadDeepPanelDetail
        (PR) landeten an derselben Stelle, haben aber nichts miteinander zu tun.
        Beide behalten.

>>> DAS PRAEFIX [Tiefes Gewoelbe] IST UEBERALL NACHGEZOGEN, auch in den vier
    aufgeloesten Stellen. Es war die Merge-Landkarte des Beitragenden (28
    Stellen, per grep auffindbar); jetzt, wo der Merge durch ist, bleibt es als
    Herkunftsvermerk stehen statt halb da und halb weg zu sein.

>>> EIN PUNKT AUS DEM PR SELBST, AUF ANSAGE DES USERS NACHGETRAGEN: der
    Kommentarblock in DeepDungeonCategories sagte, die Kategorie "Verbuendete"
    fehle bewusst, weil sie einem anderen PR gehoere - und nannte die EINE Zeile,
    die sie nachtraegt, falls jener zuerst landet. Jener PR (#3) liegt seit dem
    10.08. in main, die Zeile steht jetzt drin:

        (NavCategory.Allies,       new[] { ObjectKind.BattleNpc, ObjectKind.Pc }),

>>> BEIDE BEHAUPTUNGEN DES BEITRAGENDEN SIND VORHER GEGEN DEN CODE GEPRUEFT
    WORDEN, nicht geglaubt:
    1. "Der vorhandene Filter passt unveraendert" - stimmt: der Zweig
       `cat == NavCategory.Allies` in GetCategoryObjects haengt am Kategoriewert,
       nicht am Satz, und CombatSide.IsAlly nimmt nur heraus, was das SPIEL als
       Begleiter oder Gruppenmitglied fuehrt.
    2. "Sie wird auch leer angeboten" - stimmt ebenfalls, und zwar von selbst:
       IsCategoryAvailable hat Sonderregeln nur fuer Angelplaetze, Freibriefe,
       FATEs, Jagdziele und Sammelpunkte. Verbuendete faellt durch und bekommt
       `return true`. Es war also NICHTS zusaetzlich zu tun - haette ich die
       Regel nicht nachgelesen, waere eine ueberfluessige Ausnahme entstanden.
    Die Arten sind zeichengleich mit dem Weltsatz (BattleNpc + Pc), damit der
    Browser Trust-Trupp und echte Mitspieler unter einer Kategorie findet.

>>> MITGEZOGEN: README.md und README.en.md zaehlen die Kategorien drinnen auf -
    aus "vier Antworten" sind fuenf geworden, an beiden Stellen je Sprache
    (Abschnitt "Tiefe Gewoelbe" und die Tastenliste).

>>> AUSSERDEM COMMITTET (b5fdc0e), was seit dem 19.08. unversioniert herumlag:
    Fluchtrichtung, Warnton-Auswahl, Fang-Zustand, aufgeraeumte Inhaltssuche.
    Ein BOM, das sich in CombatService.cs eingeschlichen hatte, ist raus - die
    Datei war die einzige im Projekt mit einem.

    Build Debug 0/0 UND Release 0/0, liegt in devPlugins. NICHT GEPUSHT.

>>> ZU TESTEN, IN EINEM TIEFEN GEWOELBE: Strg+F nennt jetzt zusaetzlich die
    ebenenweiten Wirkungen; in der Charakterinfo beschreibt die Detailtaste den
    Platz, auf dem der Fokus steht. Das Log schreibt "[DeepPanel] Detail: ...".

## FRUEHERER STAND (2026-08-21, "FANG-ZUSTAND VERALLGEMEINERT - UND ES GIBT NUR EINE MECHANIK")

>>> FRAGE DES USERS: *"bei den freibriefen muss man unterschiedliche gegner
    einfangen, kann man das verallgemeinern, also dass egal wen ich einfangen
    muss gekennzeichnet ist welches monster schon besaenftigt wurde?"*

>>> DIE ANTWORT WAR ZUR HAELFTE SCHON GEBAUT: die Erkennung haengt am Status des
    Objekts, nicht an Art, Freibrief oder Name - sie galt also von Anfang an fuer
    jeden Gegner. Offen war nur, ob JEDER Fang dieselbe Mechanik benutzt. Das ist
    jetzt im Sheet nachgesehen statt vermutet.

>>> ES GIBT GENAU EINE ZAEHM-MECHANIK IM SPIEL. Im LogMessage-Sheet stehen ihre
    Meldungen als geschlossener Block:
      1805 "... wurde gezähmt. (n/m)"
      1806 "... konnte nicht gezähmt werden und verfällt in Raserei."
      1807 "... ist bereits zahm."
      1808 "... ist in Raserei verfallen und lässt sich nicht beruhigen."
      1809 "... ist nicht mehr zahm"
    Und daneben die beiden EINZIGEN Anleitungen: 1837 fuer das Emote
    "Beruhigen", 1838 fuer den Schluesselgegenstand. Zwei Wege hinein, eine
    Mechanik dahinter. Damit traegt die Verallgemeinerung.

>>> ZEILE 1809 IST EIN EIGENER FUND: der Zustand kann ENDEN. Das war bisher nur
    aus dem Flag IsPermanent=False abgeleitet, jetzt hat das Spiel eine eigene
    Meldung dafuer. Es bestaetigt die Entscheidung, gezaehmte Gegner zu sortieren
    statt auszublenden - und es zeigt, warum das Lesen des Status richtig war:
    haette das Plugin selbst mitgezaehlt, waere seine Buchfuehrung in genau
    diesem Moment falsch geworden und haette es nie gemerkt.

>>> NEU DAZU: STATUS 214 "AUFSTACHELUNG". Liegt im Sheet direkt neben 213
    (Symbol 216302 gegen 216301) und ist dessen Gegenstueck: "Nach misslungener
    Besänftigung noch wilder als zuvor." Ein Gegner mit 214 ist voruebergehend
    NICHT zaehmbar (1808) und schlaegt dabei haerter zu. Fuer den Spieler ist das
    dieselbe Art Auskunft wie "schon zahm", nur aus dem anderen Grund - er sagt
    jetzt ", rasend, nicht zaehmbar".

>>> SORTIERUNG NACH BRAUCHBARKEIT statt nach Schwere: erst die, die gerade
    zaehlen, dann die aufgestachelten, zuletzt die gezaehmten. Der aufgestachelte
    steht VOR dem gezaehmten, weil seine Absage voruebergehend ist und er danach
    wieder zaehlt - der gezaehmte hat bereits gezaehlt.

>>> UNBELEGT BLEIBT GENAU EIN GLIED, und das steht so im Code: ob auch der Weg
    ueber den Schluesselgegenstand (1838) denselben Status setzt. Gemessen ist
    nur der Emote-Weg. Da es keinen zweiten "zahm"-Status im Sheet gibt, waere
    ein eigener Zustand dafuer ein Sonderfall ohne Zeile.

>>> GEAENDERT: NavigationService.cs (TameRank statt IsTamed, 214 dazu, Trace),
    AccessibilityStrings.cs (Baustein DE+EN), docs/game-api.md, README.md,
    README.en.md.
    Build Debug 0/0 UND Release 0/0, liegt in devPlugins. IN-GAME UNGETESTET.

## FRUEHERER STAND (2026-08-21, "WARNTON: VIER STIMMEN ZUR WAHL, UND DER REGLER WAR NIE ERREICHBAR")

>>> WUNSCH DES USERS: *"wie sollten den ton fuer flaechenangriffe auch aendern
    der ist nervig und man sollte dafuer die lautstaerke auch aendern koennen"*.
    Auf die Rueckfrage hat er "mehrere Klaenge zur Auswahl" gewaehlt, statt dass
    ich einen festen neuen setze. Richtig so: wie ein Ton wirkt, der minutenlang
    durchhaelt, kann nur hoeren, wer ihn hoert.

>>> ZWEI FEHLER GEFUNDEN, BEIDE UNABHAENGIG VOM GESCHMACK:
    1. AoeWarnVolume gibt es seit langem in der Konfiguration - und in KEINEM
       Menue. Der Wert war fuer den Spieler schlicht nicht erreichbar.
    2. Und selbst wenn: er wurde nur EINMAL beim Anlegen des Tonerzeugers
       gelesen. Eine Aenderung haette erst nach einem Neustart des Plugins
       gewirkt. Der Peil-Ton macht es seit jeher richtig (zieht jeden Frame
       nach), der Warnton nicht. Jetzt zieht er nach.

>>> VIER STIMMEN, im Menue unter "Toene", jede beim Anwaehlen SOFORT kurz
    vorgespielt (1,5 s):
      - Hell: der bisherige blanke Sinus auf 660 Hz. Bleibt waehlbar, damit die
        Umstellung umkehrbar ist.
      - Weich: 300 Hz mit etwas Oberton - nimmt dem Sinus die Schaerfe.
      - Tiefes Brummen: 120 Hz. Liegt unter der gesamten Peil-Ton-Leiter und
        deckt die Sprachausgabe am wenigsten zu.
      - An- und abschwellend: 300 Hz, zweimal je Sekunde bis auf die halbe
        Lautstaerke herunter und zurueck.

>>> DIE FREQUENZEN SIND GEWAEHLT, NICHT GEGRIFFEN. Der Peil-Ton belegt mit 247,
    330, 392, 440, 494, 523, 587, 659 und 784 Hz eine ganze diatonische Leiter,
    und seine Stimmen werden je nach Winkel noch bis zu eine halbe Oktave nach
    unten gebeugt. Der bisherige Warnton auf 660 Hz sitzt praktisch AUF dem
    Sammelpunkt-Ton (659 Hz) - auseinanderzuhalten war er nur, weil er
    durchhaelt und jener schlaegt. Die neuen Stimmen liegen deshalb zwischen den
    Stufen beziehungsweise unter ihnen.

>>> WAS BEWUSST NICHT PASSIERT IST: kein Puls. Der Dauerton war die Entscheidung
    des Users vom 2026-07-26, und sie bleibt stehen - auch die schwellende
    Stimme reisst NIE ab. Eine Luecke waere genau das Merkmal der Peil-Schlaege,
    und beide laufen gleichzeitig: wer in einer Flaeche steht, hoert die Warnung
    UND den Fluchtton. Zwei Signale mit Pausen im selben Moment sind nicht mehr
    zu trennen.

>>> EIN DETAIL, DAS SONST ALLES VERDORBEN HAETTE: ToneSynth.Timbre teilt fest
    durch 1,47 (die Amplitudensumme bei voller Helligkeit). Beim blanken Sinus
    waeren dadurch nur noch gut zwei Drittel Pegel herausgekommen - der
    "bisherige Klang" waere beim Vergleichen also vor allem LEISER gewesen statt
    gleich, und man haette Lautheit mit Klangfarbe verwechselt. Der Warnton
    rechnet deshalb mit der Summe, die bei SEINER Helligkeit wirklich anfaellt.
    Nachgerechnet liegen Hell/Weich/Tief bei RMS 0,71 / 0,64 / 0,66 - also
    innerhalb von rund einem Dezibel. Die schwellende Stimme liegt
    prinzipbedingt rund 3 dB darunter, weil sie die halbe Zeit leiser ist.
    Fuer den Peil-Ton bleibt die feste Teilung richtig (dort laeuft die
    Helligkeit mit der Huellkurve mit) - er ist unangetastet.

>>> DIE PROBE IST DIE QUITTUNG: die Klangzeile spricht den Namen NICHT noch
    einmal. Die Sprachausgabe laeuft nicht blockierend, sie laege also mitten
    auf dem Ton, den man gerade beurteilen will. Der Name stand ohnehin in der
    Zeile, auf der man steht. Kommt kein Ton heraus (kein Audio-Geraet,
    Lautstaerke auf aus), wird sehr wohl gesprochen - eine Wahl, die weder
    klingt noch spricht, waere von einem Fehlschlag nicht zu unterscheiden.

>>> STANDARD IST JETZT "WEICH", nicht mehr der alte Klang. Begruendung: der
    bisherige ist der eine, von dem gemeldet ist, dass er auf Dauer stoert - als
    Vorgabe fuer alle anderen taugt er damit nicht. Welche Stimme wirklich die
    beste ist, entscheidet das Ohr des Spielers.

>>> GEAENDERT: AoeWarnTone.cs (neu: Enum + Klangwerte), AoeWarningService.cs
    (Stimmen, Nachziehen, Vorhoeren), Configuration.cs (AoeWarnSound),
    OptionsMenu.cs (zwei Zeilen + Auswahl-Untermenue + Service),
    AccessibilityStrings.Chat.cs (6 Bausteine DE+EN), Plugin.cs (Verdrahtung),
    README.md, README.en.md.
    Build Debug 0/0 UND Release 0/0, liegt in devPlugins. IN-GAME UNGETESTET.

>>> ZU TESTEN: Einstellungen -> Toene -> "Klang AoE-Warnung". Erwartet: vier
    Zeilen, jede spielt beim Druecken ihren Ton, das Menue bleibt offen. Danach
    "Lautstaerke AoE-Warnung" verstellen und pruefen, ob die Aenderung beim
    naechsten Warnton SOFORT gilt (das war der zweite Fehler).

## FRUEHERER STAND (2026-08-21, "SCHON GEZAEHMT - DAS SPIEL FUEHRT ES SELBST")

>>> VOM USER BESTAETIGT: die Freibrief-Kategorie funktioniert und ist getestet.

>>> NEUER WUNSCH: *"wenn ziele beruhigt werden sollen sollte man auch sehen das
    man diesen npc oder gegner schon hatte"*.

>>> DAS PROBLEM STAND IM LOG VON HEUTE FRUEH, nicht in einer Vermutung
    (dalamud.log 2026-08-21, Freibrief "Im Namen des Fortschritts", 4 Faenge):
    Der Browser bot ELF Pyrit-Kobalos an - und ein gezaehmter verschwindet
    nicht, verliert keine HP und heisst weiter genauso. Dreimal ist der User zu
    einem gelaufen, den er schon hatte, und hat es erst an der Abweisung
    gemerkt: "Der Pyrit-Kobalos ist bereits zahm." (09:35:51, 09:37:21,
    09:37:40). Ein sehender Spieler sieht das Symbol ueber dem Gegner stehen,
    bevor er losgeht.

>>> DIE QUELLE WAR SCHON DA - die Fang-Sonde hatte sie mitgeschrieben, ohne dass
    jemand hingesehen hat. 09:36:01, an genau dem Gegner, der zehn Sekunden
    vorher "bereits zahm" gemeldet hatte:
      [FangSonde] -> Ziel 'Pyrit-Kobalos' HP 268/268 Status: 213:'Besänftigung'
    Also nichts nachbauen und nichts mitzaehlen: das Spiel fuehrt den Zustand
    am Gegner, wir lesen ihn.

>>> IM SHEET GEGENGEPRUEFT (Lumina offline gegen die installierten Spieldaten,
    kein Rateschritt): Zeile 213 heisst DE "Besänftigung" / EN "Pacification"
    und beschreibt sich selbst als "Zahm und greift nicht mehr an." Sie ist die
    EINZIGE Zeile des ganzen Sheets mit dem Symbol 216301. Die drei anderen
    Zeilen, die englisch auch "Pacification" heissen (6, 620, 5188), sind der
    Spieler-Debuff "Pacem" mit Symbol 215017 - Verwechslung ausgeschlossen.
    Geprueft wird die ID, nie der Name: der Name wechselt mit der Client-
    Sprache, die Id nicht.

>>> GEBAUT:
    - ", schon gezaehmt" haengt jetzt an JEDER Zielansage, die den Gegner
      betrifft: Zielwechsel per Spieltaste, Objekt-Browser und Freibrief-Gegner.
    - BEWUSST NICHT hinter dem "Ziel angenommen"-Gatter, hinter dem Stufe und HP
      stehen. Jene beiden stehen in der ZIEL-LEISTE, die bei einer Ablehnung
      leer bleibt; der Besaenftigungs-Status haengt am Gegner selbst und ist
      ueber seinem Kopf zu sehen, ohne ihn anzuvisieren.
    - Schon gezaehmte Gegner rutschen im Browser ans ENDE der Liste, davor
      unveraendert nach Entfernung. Sie fallen NICHT raus: der Status ist laut
      Sheet nicht dauerhaft (IsPermanent=False), und ein Gegner, den das Plugin
      faelschlich verschwiegen haette, waere ohne Ansage nicht mehr auffindbar.
      Hinten in der Liste ist er beides - aus dem Weg und noch da.
    - Der [Leve]-Trace schreibt jetzt "davon N schon gezaehmt" samt Ids mit.

>>> ZWEI DINGE SIND OFFEN, und beide beantwortet derselbe Test:
    1. Ob die Statusliste auch fuer einen NICHT anvisierten Gegner gefuellt ist.
       Trifft das nicht zu, sagt die Ansage schlicht nichts - falsch wird sie
       dadurch nicht.
    2. Ob 213 bei JEDEM Fang-Freibrief benutzt wird. Gemessen ist nur "Im Namen
       des Fortschritts"; bei "Dodos an Bord" ist es unbelegt.
    Der Trace beantwortet beides: stehen dort waehrend eines Fang-Freibriefs
    Ids, tragen beide Annahmen. Bleibt die Zahl bei 0, obwohl das Spiel
    "ist bereits zahm" sagt, tut sie es nicht.

    Build Debug 0/0 UND Release 0/0, liegt in devPlugins. IN-GAME UNGETESTET.

>>> ZU TESTEN: einen Fang-Freibrief laufen lassen, einen Gegner zaehmen, dann im
    Browser durch die Freibrief-Gegner blaettern. Erwartet: der gezaehmte kommt
    zuletzt und traegt ", schon gezaehmt" am Ende der Ansage.

>>> NEBENBEFUND AUS DEMSELBEN LOG, NICHT ANGEFASST: beim Freibrief "Tonscherben
    fuer die Forscher" listet die Kategorie "Freibrief-Gegner: Hedwig" (das ist
    das eigene Reittier/der Begleiter, Stufe 46) und "Lehmschale". Die
    Aufgabenzeile las sich zugleich als 'H?3I?4Altertuemliches TongefaessIH' -
    also eine Zeile mit ungelesenen SeString-Nutzlasten. Beides gehoert
    angesehen, ist aber ein anderes Thema als dieser Wunsch.

## FRUEHERER STAND (2026-08-19, "FLUCHTRICHTUNG: WOHIN, NICHT NUR DASS")

>>> WUNSCH DES USERS: *"wir muessen eine bessere moeglichkeit finden damit wir
    aus flaechenangriffen oder anderen bossmechaniken in sichere bereiche kommen
    die sehenden sehen wo sie hinlaufen muessen bzw koennen aber wir nicht"*.
    Auf die Rueckfrage hat er den vollen Weg gewaehlt, nicht die kleine Loesung
    ("hinter den Gegner").

>>> DER KERNGEDANKE: bisher war die AoE-Warnung ein HEISS-KALT-Signal. Der Ton
    brummt, solange man drinsteht, und verstummt beim Heraustreten - er sagt
    also, DASS man falsch steht, nie WOHIN. Dabei ist IsPlayerInAoe laengst eine
    reine Funktion einer Position. Man kann sie also nicht nur fuer den Punkt
    aufrufen, auf dem der Spieler steht, sondern fuer Probepunkte ringsum. Aus
    "stehe ich drin?" wird damit "wo ist der naechste Punkt, der draussen ist?".

>>> GEBAUT:
    - EscapeRouteService (neu): Faecher aus 24 Strahlen mal 1 m Schritten bis
      25 m. Ein Kandidat muss aus ALLEN aktiven Flaechen heraus sein, mit 2 m
      Sicherheitsabstand zur Kante (wer auf der Linie steht, ist getroffen, und
      der Gegner dreht sich waehrend des Casts weiter).
    - DangerZone (neu): die Flaeche als reine Geometrie, ohne Gegner und ohne
      Sheet-Zeile. CombatService baut sie und prueft SEINEN EIGENEN WARNTON
      damit - Warnung und Fluchtrichtung koennen deshalb nicht auseinander-
      laufen. Das war der Umbau von IsPlayerInAoe zu BuildZone.
    - Begehbarkeit ueber NearestPointReachable. Ein Fluchtpunkt, zu dem man
      nicht laufen kann, ist schlimmer als keiner - man rennt gegen eine Wand,
      waehrend der Cast ablaeuft. Findet das Netz nichts, kommt die ehrliche
      Absage statt einer Wegweisung ins Nichts.
    - Peil-Ton mit eigener Stimme (BeaconKind.Escape, 247 Hz H3, DREI Schlaege -
      tiefer und dringender als alles andere). Er hat VORRANG vor Gehhilfe und
      Zielpeilung: wer in einer Flaeche steht, hat genau eine Aufgabe. Der
      Tonerzeuger konnte bisher nur ein oder zwei Schlaege, jetzt bis drei;
      zwei verhalten sich exakt wie vorher.
    - Gesprochen EINMAL je Gefahrenlage: "Raus nach rechts, 8 Meter." Danach
      fuehrt der Ton. Oefter waere ein Wortschwall genau in den Sekunden, in
      denen man rennen muss.

>>> ZWEI EIGENE FEHLER, VOR DEM TEST GEFUNDEN UND BEHOBEN:
    1. Die Suche brach zu frueh ab. Das Budget fuer die Netzabfragen war schon
       im ersten Meter aufgebraucht, danach sah sie gar nicht mehr weiter
       aussen nach - der Spieler haette "kein sicherer Weg" gehoert, waehrend
       zwei Meter weiter einer lag. Jetzt zwei Durchgaenge: erst rechnerisch
       sammeln (kostet nichts), dann der Reihe nach ueber das Netz pruefen.
    2. Ein Kreis AUF DEM SPIELER laesst sich nicht verlassen - seine Mitte
       wandert jeden Frame mit. Die Suche haette einen endlos vor sich
       hergetrieben. Solche Flaechen sind jetzt von der Wegweisung
       ausgenommen; Ansage ("Kreis um dich") und Warnton bleiben.

>>> WAS ES BEWUSST NICHT KANN, und was der User dazu wissen muss:
    - Flaechen, die nur als Bodeneffekt existieren (die Mitte steht in keinem
      Feld, das wir lesen). Im Code seit laengerem als ASSUMPTION markiert.
    - Alles, was kein Cast ist: Tuerme, Sammelpunkte, Seile, Rueckstoss.
    - Nur BELEGTE Formen gehen in die Suche. Der Warnton darf eine unbekannte
      Form vorsichtshalber als Kreis behandeln (lieber zu oft warnen), aber eine
      erfundene Flaeche wuerde den Spieler aktiv in eine Richtung schicken, fuer
      die es keinen Grund gibt.
    - "Sicher jetzt" ist nicht "sicher beim Einschlag": dreht der Boss sich
      weiter, wandert der Kegel mit.

    Build Debug 0/0 UND Release 0/0, liegt in devPlugins. IN-GAME UNGETESTET.

>>> ZU TESTEN: einen Gegner mit Kegel- oder Linien-Cast suchen (Leveldungeon
    oder Freibrief-Gegner reichen), in die Flaeche stellen und hoeren, ob die
    Ansage kommt und der Ton in eine Richtung fuehrt, die WIRKLICH heraus
    fuehrt. Das Log schreibt "[Flucht] Sicherer Punkt X Grad, Y m" bzw.
    "[Flucht] Kein sicherer Punkt in Reichweite gefunden".

## FRUEHERER STAND (2026-08-19, "EINSTELLUNGEN DER INHALTSSUCHE: FERTIG UND BESTAETIGT")

>>> VOM USER IN-GAME BESTAETIGT ("funktioniert"). Das Fenster ist damit
    abgeschlossen. Was es kann:
      - EINE Ansage je Bewegung statt vier sich abschneidender.
      - "Keine Beschraenkungen, Schalter, an" - der Zustand geht beim Umschalten
        SOFORT mit, weil er aus der Arbeitskopie in der Zeile kommt und nicht
        aus dem gespeicherten Stand.
      - "Beuteregeln, Auswahlliste, Standard.", "Sprache Deutsch, Schalter, an".
      - "ausgegraut", wo der Inhalt eine Einstellung nicht zulaesst.
      - Unterzeile beim Oeffnen (beide Fenster heissen "INHALTSSUCHE").
      - Erklaerungstext nach 0,4 s Verweilen.

>>> AUFGERAEUMT nach Konvention (Sonde nach Feature-Ende loeschen): die
    Messsonde, die tote Spaltenlogik und die DEBUG-Protokollzeile sind raus.
    Das Wissen steht in docs/game-api.md, nicht mehr im Code.
    Build Debug 0/0 UND Release 0/0.

>>> NOCH NICHT COMMITTET. Offen ist die Entscheidung, ob das zusammen mit der
    bereits gemergten Arbeit aus PR #8 in ein Release geht.

## FRUEHERER STAND (2026-08-19, "ARBEITSKOPIE GEFUNDEN - DER ZUSTAND IST ZURUECK, JETZT RICHTIG")

>>> DIE SONDE HAT GELIEFERT (Log 19:36). Der Umschaltmoment steht zweimal im
    Log, in BEIDE Richtungen, und beide Male ist die Richtung gegen die
    Bestaetigung des Spiels selbst geprueft:
      19:36:22  Fenster auf, gespeichert=True. Zeile zeigt Bild-Teil 0.
      19:36:28  User schaltet AUS -> Bild-Teil springt auf 1.
      19:36:33  Ok -> Spiel schreibt in den Chat "... festgelegt: -" (nichts).
      19:36:39  Fenster auf, gespeichert=False. Zeile zeigt Bild-Teil 1.
      19:36:46  User schaltet AN -> Bild-Teil springt auf 0.
      19:36:48  Ok -> Spiel schreibt "... festgelegt: Keine Beschraenkungen".
    Damit ist bewiesen: Bild-Teil 0 = AN, Bild-Teil 1 = AUS.

>>> VIER DINGE FLIPPEN GEMEINSAM (alle in der Zeile selbst):
      AN : Bild-Teil 0, NineGrid 8 sichtbar / 9 versteckt, Text 6 sichtbar
      AUS: Bild-Teil 1, NineGrid 9 sichtbar / 8 versteckt, Text 7 sichtbar
    Gelesen wird das ZUSTANDSSYMBOL am rechten Rand - das einzige davon, das
    fuer sich eine Bedeutung hat: es ist das Kaestchen, auf das ein sehender
    Spieler schaut. Gefunden ueber die GEOMETRIE (das am weitesten rechts
    liegende Bild der Zeile, in deren rechter Haelfte), damit keine Knoten-Id
    ueber Patches hinweg stillhalten muss. Die Zeile traegt links noch ein
    zweites Bild - das ist NICHT der Zustand.

>>> WIDERLEGT UND NOTIERT: AtkComponentButton.IsChecked (Bit 18 in Flags) blieb
    bei jedem Umschalten False - genau wie FFXIVClientStructs es dokumentiert
    ("used by AtkComponentCheckBox and AtkComponentRadioButton"). Flags selbst
    blieb konstant 0x20810100. Beides taugt hier nicht.

>>> GEBAUT: ContentsFinderSettingText.RowState liest das Symbol, die sieben
    Zeilen sagen ihren Zustand wieder an - "Keine Beschraenkungen, Schalter, an".
    Zeigt das Symbol je einen nie gemessenen Teil, sagt die Zeile den Zustand
    NICHT und es gibt eine Warnung im Log. Zeilen ohne Symbol ("Ok",
    "Schliessen" tragen ueberhaupt kein Bild) fallen durch zum generischen
    Leser, der ihre Namen laengst richtig sagt.
    Sonde und die tote Spaltenlogik sind wieder raus (Konvention: Sonde nach
    Feature-Ende loeschen). Eine schlanke DEBUG-Zeile bleibt, sie schreibt bei
    JEDER Zustandsaenderung angesagten Stand + Bild-Teil + gespeicherten Stand -
    damit waere eine falsche Ansage ohne neue Messrunde zu diagnostizieren.

    Build Debug 0/0 UND Release 0/0, liegt in devPlugins.

>>> ZU TESTEN: einmal durch die sieben Zeilen, eine umschalten, hoeren ob die
    Ansage sofort mitgeht. Danach ist das Fenster fertig.

## FRUEHERER STAND (2026-08-19, "GEMESSEN: DAS FENSTER ARBEITET AUF EINER KOPIE")

>>> IN-GAME-TEST GELAUFEN (Log 19:29:52 bis 19:30:52). Das Fenster liest jetzt
    sauber - EINE Ansage je Bewegung statt vier:
      "An laufenden Einsaetzen teilnehmen, Schalter"
      "Keine Beschraenkungen, Schalter, aus"
      "Beuteregeln, Auswahlliste, Standard."
      "Sprache Japanisch, Schalter, aus" / "Sprache Deutsch, Schalter, an"
      "Richte Bedingungen fuer die Teilnahme an Inhalten ein." beim Oeffnen
      "Erklaerung: ..." nach dem Verweilen
    Kein "INHALTSSUCHE" mehr, keine Dopplung, nichts abgeschnitten.

>>> PUNKT B GEKLAERT - die Sprach-Zuordnung STIMMT. Von links kamen
    "Japanisch, aus", "Englisch, aus", "Deutsch, an" (19:30:38 bis 19:30:44).
    Der User spielt auf deutschem Client mit Deutsch als einziger gewaehlter
    Sprache - JA/EN/DE/FR von links ist damit belegt, nicht mehr angenommen.

>>> PUNKT A GEKLAERT, UND ZWAR GEGEN MEINE ERWARTUNG: ContentsFinder.Instance()
    fuehrt die GESPEICHERTEN Einstellungen, NICHT den Stand im offenen Fenster.
    Beweis aus demselben Log:
      - Alle sechs Felder blieben ueber die volle Minute auf False.
      - Um 19:30:33 fragte das Spiel "Die Einstellungen wurden noch nicht
        gespeichert. Verwerfen und schliessen?" - es hatte also sehr wohl eine
        Aenderung. Der User antwortete "Nein" und drueckte um 19:30:51 Ok.
      - Darauf schrieb das Spiel selbst in den Chat: "Teilnahmebedingungen
        wurden wie folgt festgelegt: Keine Beschraenkungen" (19:30:52).
    "Keine Beschraenkungen" war also EINGESCHALTET, waehrend Feld und Ansage
    die ganze Zeit "aus" sagten. Das Fenster arbeitet auf einer Arbeitskopie und
    schreibt sie erst beim Ok zurueck.

>>> KONSEQUENZ, SOFORT GEZOGEN: die sechs Zeilen sagen ihren Zustand NICHT mehr.
    Sie sagen nur noch "<Name>, Schalter". Ein "aus", das nach dem Umschalten
    immer noch "aus" sagt, ist schlimmer als gar keine Angabe - der User kann es
    nicht sehen und wuerde der falschen Auskunft glauben. Die Sprach-Kaestchen
    und das Beuteregeln-Feld sind NICHT betroffen: die lesen ihren Stand direkt
    aus dem Bedienelement (CheckBox.IsChecked bzw. der Auswahlwert) und waren im
    Test korrekt.

>>> NEUE SONDE, um die Arbeitskopie zu finden (ProbeDutySettingRow, #if DEBUG):
    schreibt fuer die fokussierte Zeile alle in Frage kommenden Traeger mit -
    Flags der Komponente samt IsChecked-Bit, die Knoten-Flags jedes Kindes und
    die PartId der beiden Bilder (Umschalt-Symbol links, Haken rechts). Sie
    schreibt bei JEDER AENDERUNG dieser Werte, nicht nur beim Fokuswechsel:
    das Umschalten bewegt den Fokus nicht, und genau dieser Moment ist die
    Messung. Eine Zeile je Aenderung, keine je Frame.

>>> WAS DER USER JETZT TUN MUSS (kurz): Einstellungen oeffnen, auf "Keine
    Beschraenkungen" stehenbleiben, EINMAL umschalten, kurz warten, noch einmal
    umschalten. Dann Log schicken. Der Unterschied zwischen den beiden
    "[Inhaltssuche-Sonde]"-Zeilen zeigt genau den Wert, der den Schalter fuehrt -
    danach kann der Zustand wieder angesagt werden, dann richtig.

    Build Debug 0/0 UND Release 0/0, liegt in devPlugins.

## FRUEHERER STAND (2026-08-19, "EINSTELLUNGEN DER INHALTSSUCHE: VIER ANSAGEN, DIE SICH GEGENSEITIG ABSCHNITTEN")

>>> ANLASS: Der User schickte Log und UI-Dump ("da gibts ein menue was richtig
    vorgelesen werden soll"). Der Dump vom 18:34:31 enthaelt drei Fenster:
    _MainCommand, ContentsFinder und ContentsFinderSetting. Die Inhaltssuche
    selbst liest sauber ("St. 16, Stufensteigerung", "Auswahl aufheben",
    "Stufe aufsteigend" plus die Beschreibung aus JournalDetail). Kaputt war das
    EINSTELLUNGS-Fenster - vom User bestaetigt.

>>> BEFUND, aus dem Log belegt (18:34:18, und in jeder anderen Zeile gleich):
    bei JEDEM Fokuswechsel feuerten VIER unterbrechende Ansagen in 2 ms.
      1. "Standard"          - der Wert unter dem Fokus
      2. "INHALTSSUCHE"      - Rauschen
      3. "Stelle die Regeln fuer die Beuteverteilung ein..." - der Hilfetext
      4. "Beuteregeln"       - derselbe Name noch einmal
    Weil alle vier SpeakInterrupt benutzen, schnitt jede die vorige ab. Zu
    hoeren war nur die letzte: das nackte Wort.

>>> DREI URSACHEN, jede einzeln nachgewiesen:
    1. Der Fenstertitel kam vom generischen FindFocusedText. Das Kollisionsfeld
       ueber dem GANZEN Fenster (Comp(1004) Knoten 31, Kind 13, 880x440) traegt
       das Fokus-Bit - der Leser fand also den Fensterrahmen statt des
       Bedienelements. Im Log steht deshalb in JEDER Zeile derselbe Key=31013.
       Dasselbe Muster bei ContentsFinder (Key=76012).
    2. Name und Hilfetext doppelte der Text-Scanner aus dem rechten Feld
       (key=260004 und id=25).
    3. Der ZUSTAND wurde nie gesagt. Die sieben Zeilen sind Umschalter, angesagt
       wurde nur ihr Name - in einem Einstellungs-Fenster die kleinere Haelfte
       der Auskunft. Dazu waren die vier Sprach-Kaestchen voellig stumm
       ("[Focus] STUMM", 18:34:19), und das Beuteregeln-Auswahlfeld nannte nur
       seinen Wert, nicht wofuer er gilt.

>>> ZUSTANDSQUELLE, mit ilspycmd gegen die installierte FFXIVClientStructs.dll
    geprueft (NICHT geraten): Client::Game::UI::ContentsFinder.Instance() fuehrt
    LootRules@24 (Normal/GreedOnly/Lootmaster), IsUnrestrictedParty@25,
    IsMinimalIL@26, IsSilenceEcho@27, IsExplorerMode@28, IsLevelSync@29,
    IsLimitedLevelingRoulette@30. Das deckt SECHS der sieben Umschalter ab.
    Alles in docs/game-api.md notiert.

>>> BEWUSST OHNE ZUSTAND GEBLIEBEN: die Zeile "An laufenden Einsaetzen
    teilnehmen". Sie hat kein Feld in ContentsFinder, und in ganz
    FFXIVClientStructs war keines zu finden. Dalamuds UiConfigOption kennt
    ContentsFinderSupplyEnable, das inhaltlich passen KOENNTE - ungeprueft.
    Ein falsches "an" kann ein blinder Spieler nicht selbst korrigieren,
    deshalb sagt diese Zeile nur "Name, Schalter" ohne Stand.

>>> GEBAUT:
    - ContentsFinderSettingText.cs (neu): welche Zeile welche Einstellung ist
      (ueber die BILDSCHIRMORDNUNG, nicht ueber die Knotenreihenfolge - die
      laeuft hier rueckwaerts, id 10 steht unten), Zustand aus dem
      ContentsFinder-Singleton, Namen der vier Sprachen.
    - UIReaderService: TryReadContentsFinderSettingRow als erster Leser der
      Fokus-Kette. Sagt jetzt "Keine Beschraenkungen, Schalter, aus",
      "Beuteregeln, Auswahlliste, Standard", "Sprache Deutsch, Schalter, an".
      Ausgegraute Zeilen ("Stufenanpassung" trug F=0x2013 gegen 0x2033 der
      Nachbarn) werden als solche angesagt.
    - ContentsFinderSetting in SpecialUpdateAddons: killt Titel-Rauschen und
      die Scanner-Dopplung in einem Zug.
    - Beim Oeffnen kommt die UNTERZEILE ("Richte Bedingungen fuer die Teilnahme
      an Inhalten ein."), weil Inhaltssuche UND Einstellungen beide
      "INHALTSSUCHE" heissen und sonst nicht zu unterscheiden waeren.
    - Hilfetext nach kurzem Verweilen (0,4 s), Entscheidung des Users - genau
      wie bei Gegenstands- und Skill-Beschreibungen. Beim schnellen Blaettern
      hoert man ihn gar nicht.
    - Das Auswahlfeld benutzt ReadFocusedControlValue: bei GEOEFFNETER Liste ist
      der fokussierte Eintrag der Wert, sonst wuerde jeder Schritt den
      gespeicherten Stand wiederholen.
    - AccessibilityStrings: DutyLanguage* und SettingHelp, gleich zweisprachig.

    Build Debug 0/0 UND Release 0/0, liegt in devPlugins. IN-GAME UNGETESTET.

>>> WAS DER USER TESTEN MUSS (eine Runde reicht fuer beides):
    Inhaltssuche oeffnen (Taste U), "Inhaltssuche-Einstellungen oeffnen"
    aktivieren, einmal durch alle Zeilen blaettern, dann EINE Option umschalten
    und noch einmal darueber blaettern. Danach Log schicken.
    Damit klaeren sich die zwei offenen Punkte:
      a) Laufen die ContentsFinder-Felder LIVE mit, oder erst nach "Ok"? Die
         DEBUG-Zeile "[Inhaltssuche] Zeile N '<Name>' (Feld …): …" schreibt bei
         jedem Fokuswechsel alle sieben Werte mit.
      b) Stimmt die Zuordnung der vier Sprach-Kaestchen (JA/EN/DE/FR von links)?
         Ein Kaestchen umschalten und hoeren, welcher Name sich aendert.

## FRUEHERER STAND (2026-08-19, "AUFZUEGE: ERST MESSEN, WAS EIN AUFZUG UEBERHAUPT IST")

>>> MELDUNG DES USERS: "hm es gibt aufzuege aber ich weiss nicht ob ich richtig
    stehe". Das ist der urspruengliche Anlass des Peil-Tons - und die Stelle, an
    der ich NICHT weiterbauen kann, ohne zu raten.

>>> WAS NACHGESEHEN WURDE, und was dabei herauskam:
    - Die Layout-Engine kennt KEINEN Aufzug-Typ. Die vollstaendige
      InstanceType-Liste der installierten FFXIVClientStructs.dll wurde
      durchgesehen: ExitRange (41), DoorRange (58), CollisionBox (57),
      ClickableRange (70), GimmickRange (67) - nichts, was eine Hebebuehne
      benennt.
    - Im EObj-Sheet gibt es Objekte mit Aufzug-Namen: "Aufzug zur Bruecke" und
      "Aufzug zur Theaterwerkstatt" (Block 0x000D, mit Level-Position),
      "Fahrstuhl zu den Wohnungen" und "Aufzug zum Dach" (Block 0x000B),
      29 "Aufzugshebel"/"Aufzugsteuerpult" (Block 0x000F), zwei "Hexalift"
      (gar kein Event). Die MEISTEN tragen keine Position im Level-Sheet - sie
      werden zur Laufzeit gesetzt.
    - Damit ist die entscheidende Frage offen: ist der Aufzug des Users ein
      ANVISIERBARES OBJEKT (dann heisst "richtig stehen" nur "in Reichweite",
      und der Peil-Ton genuegt schon heute), oder eine reine PLATTFORM, auf der
      man physisch stehen muss?

>>> DESHALB GEBAUT: /acc lift (LiftProbe.cs, #if DEBUG) - eine Messung statt
    einer Vermutung.
    - Momentaufnahme aller Objekte in 25 m, mit WAAGERECHTEM und SENKRECHTEM
      Abstand GETRENNT. Genau darin steckt die Antwort: wer auf einer Plattform
      steht, ist waagerecht fast ueber ihrem Mittelpunkt und senkrecht darueber.
      Dazu je Objekt: anvisierbar ja/nein, DataId, Name.
    - Danach 20 Sekunden Mitschrift der eigenen Position, zwei Zeilen je Sekunde,
      mit dem jeweils naechsten Objekt. Faehrt der Aufzug los, waehrend man
      draufsteht, wandert die eigene Hoehe mit - das ist der BEWEIS "ich stand
      richtig", und aus dem Abstand in genau diesen Zeilen laesst sich ablesen,
      welcher Abstand "drauf" bedeutet.
    - Erkennt die Sonde eine senkrechte Bewegung ohne waagerechte, sagt sie das
      SOFORT an ("Du faehrst, Hoehe steigt"). Der User soll die Antwort im Moment
      des Geschehens hoeren, nicht erst im Log.

>>> BEWUSST NICHT GEBAUT: das Auslesen der Layout-Instanzen (Kollisionsboxen).
    Der Weg dorthin ist verifiziert und notiert - LayoutWorld.Instance() ->
    ActiveLayout@32 -> Layers@552 (StdMap<ushort, Pointer<LayerManager>>) ->
    LayerManager.Instances@40 (StdMap<uint, Pointer<ILayoutInstance>>), Typ in
    ILayoutInstance.Id@24 (Identifier.Type@1), Position ueber die virtuelle
    GetTranslation. Er fuehrt aber ausschliesslich ueber VTable-Aufrufe, und ein
    Fehlgriff dort stuerzt das SPIEL ab. Das ist der zweite Schritt, falls die
    Messung oben nicht reicht - nicht der erste.

>>> WAS DER USER TUN MUSS: an einen Aufzug stellen, "/acc lift" eingeben, sich
    daraufstellen und ihn ausloesen. Danach das Log schicken. Und mir sagen,
    WELCHER Aufzug es ist (Gebiet/Stadt) - dann kann ich die Objekte dieser Zone
    offline gegenpruefen.

>>> GEAENDERT: LiftProbe.cs (neu, DEBUG), Plugin.cs (Sonde verdrahtet, /acc lift).
    Build Debug 0/0 UND Release 0/0. IN-GAME UNGETESTET.

## FRUEHERER STAND (2026-08-19, "STILLE HEISST RICHTIG - DER PEIL-TON WURDE UMGEDREHT")

>>> WUNSCH DES USERS: "es waere gut wenn wir verschiedene ziele durch toene
    hoerbar machen sobald sie getrackt sind objekte gegner uebergaenge usw aber
    fuer jedes einen unterschiedlichen und nicht so nervent er soll auch
    lautstaerke technisch und richtungsbezogen kommen um so weiter man weg ist um
    so leiser und die richtung in die man sich drehen muss und wenn man 100%
    richtig steht soller ausgehen es geht mir dabei auch um aufzuege bzw
    plattformen ob man richtig steht".

>>> DREI ENTSCHEIDUNGEN HAT ER SELBST GETROFFEN:
    1. Der Ton laeuft auch OHNE Gehhilfe - dann aber NUR auf ein anvisiertes
       Ziel. Nachgeschaerft von ihm: "die toene fuer getrackte sachen sollen auch
       kommen wenn die gehhilfe aus ist aber dann nur wenn was anvisiert ist".
       Eine blosse Browser-Auswahl loest also keinen Ton aus - der Browser
       blaettert im Sekundentakt, und jeder Schritt wuerde sonst einen neuen
       Dauerton starten. Dazu eine Taste, die ihn stumm schaltet.
    2. Er verstummt bei richtiger AUSRICHTUNG (nicht erst bei Ankunft). Das ist
       die Aufzug-/Plattform-Hilfe: drehen, bis es still ist.
    3. Er ERSETZT den bisherigen Gehhilfe-Ton, statt daneben zu laufen.

>>> DAS IST EINE UMKEHRUNG, kein Anbau. Bisher war "geradeaus" ein hoher,
    mittiger Ton - jetzt ist geradeaus STILLE. Alles andere musste sich danach
    richten.

>>> DIE STILLE-FALLE, und wie sie geloest ist: fuer einen blinden Spieler ist
    Stille nicht von "kaputt", "Ziel verloren" oder "Ton aus" zu unterscheiden.
    Deshalb kommt beim UEBERGANG von falsch auf richtig EIN kurzer Quittungston
    (CueService.PlayAlignedTone, 880 Hz - oberhalb des gesamten Peil-Tonvorrats
    von 330 bis 784 Hz, also nicht verwechselbar). Die Stille ist damit belegt.

>>> SIGNAL IM EINZELNEN (BeaconService):
    - Stille-Zone 6 Grad, Rueckkehr erst ab 10 Grad (Hysterese) - ohne die
      flattert der Ton am Rand im Takt der Mausbewegung.
    - Je genauer gezielt, desto ruhiger: Schlagfolge 0,4 s (voellig daneben) bis
      0,95 s (kurz vorm Einrasten). Das ist der Teil, der "nicht so nervent"
      beantwortet - es wird ruhiger zum Ziel hin, nicht hektischer.
    - Lautstaerke: voll bis 5 m, linear herunter auf 15% ab 150 m.
    - Seite ueber Stereo, "hinter dir" ueber eine dunklere Tonlage (bei genau
      180 Grad ist das Stereobild wieder mittig, Seite allein reicht also nicht).
    - Ankunft (<= 2,5 m) schweigt unabhaengig von der Blickrichtung.

>>> ACHT STIMMEN, je Zielart ein Grundton, zwei Schlaege fuer alles, wo etwas zu
    TUN ist:
      Gegner 330 Hz doppelt | Inhalts-Eingang 392 doppelt | Uebergang 440 doppelt
      NPC 494 einfach | Quest-Ziel 523 doppelt | Objekt 587 einfach
      Sammelpunkt 659 einfach | Aetheryt 784 einfach
    Gegner und Verbuendete sind beide BattleNpc - getrennt ueber CombatSide,
    dieselbe Unterscheidung wie in den Kategorien. Ein Trupp-Kollege darf nicht
    wie eine Warnung klingen.

>>> ZIELWECHSEL SCHNEIDET AB (nachgereichte Vorgabe: "wenn was anderes getrackt
    wird bei den objekten und generell soll der ton fuer das vorher getrackte
    aufhoeren"). Jeder Ton traegt jetzt eine Kennung - die Objekt-Id beim
    anvisierten Ziel, eine laufende Nummer beim Gehhilfe-Ziel. Aendert sie sich,
    bricht der alte Ton ab und der neue faengt mit vollem Anschlag an.
    WARUM DAS MEHR IST ALS KOSMETIK: ohne den Schnitt schleppte der Ton den
    Zustand des alten Ziels mit. Stand man auf das alte eingerastet (= still),
    blieb er fuer das NEUE Ziel stumm, solange dieses zufaellig in der
    Hysterese-Zone lag - der Spieler haette ein neues Ziel gehabt und die
    Bestaetigung des alten gehoert. Beim Wechsel kommt bewusst KEIN Quittungston:
    das Anvisieren quittiert das Spiel schon mit seinem eigenen Ton, und die
    Zielansage sagt dazu, was es ist.
    Die Kennungen der Gehhilfe starten bei 2^60, damit sie nie mit einer
    Objekt-Id zusammenfallen.

>>> ZIEL VERSCHWUNDEN = TON AUS (nachgereichte Vorgabe: "wenn das ziel
    verschwindet soll der ton auch aufhoeren"). Drei Wege dorthin, weil es drei
    verschiedene Arten von Verschwinden gibt:
      - Anvisiertes Objekt entladen: das Spiel setzt sein Ziel selbst auf null,
        darunter liegt jetzt IGameObject.IsValid() als Netz - ein Zeiger auf ein
        entladenes Objekt lieferte sonst eine Position, die niemand besetzt.
      - Gegner erlegt: IsDead, aber NUR fuer BattleNpc und Pc. Was IsDead an
        einer Tuer oder Truhe bedeutet, ist nicht gemessen - eine Tuer, die sich
        faelschlich fuer tot haelt, waere ein stummer Ton ohne Erklaerung.
      - Zonenwechsel/Logout: LocalPlayer wird null, und dort verstummte der Ton
        bisher NICHT - er lief mit dem letzten Stand ueber den Ladebildschirm
        weiter. Das war ein echter Fehler, kein Feinschliff.
    Die Gehhilfe hatte den Fall schon: verschwindet ihr Ziel, endet sie mit
    "Ziel verloren", und ihr Ende schaltet den Ton still.

>>> ZWEITE FOLGE DAVON, die im Blick bleiben muss: was das Spiel nicht
    anvisieren laesst - Quest-Requisiten, Kartenmarker, Quest-Ziele, die
    Eintraege der Dungeonliste - bekommt ohne Gehhilfe keinen Ton. Fuer die ist
    die Gehhilfe der Weg, sie zeigt auch auf eine blosse Position. WENN EIN
    AUFZUG SICH NICHT ANVISIEREN LAESST, faellt genau der Anlass des Wunsches in
    diese Luecke - das ist der Punkt, den der Test zuerst klaeren muss.

>>> BEWUSSTE GRENZE: Ziele in ANDEREN Zonen bekommen keinen Ton. Die Luftlinie
    zeigt dort durch die Zonenwand, und der Spieler wuerde gegen Felsen laufen.
    Sobald die Gehhilfe zum Uebergang laeuft, fuehrt sie den Ton wieder - und
    zwar mit der Uebergangs-Stimme, nicht mit der des Ziels dahinter.

>>> GEAENDERT: BeaconService.cs (neu geschrieben: BeaconKind, Stimmen, Stille-Zone,
    Hysterese, Idle), CueService.cs (PlayAlignedTone), NavigationService.cs
    (UpdateTargetBeacon, TryGetBeaconTarget, BeaconKindForObject/Selection,
    Gehhilfe uebergibt Art + Ankunft), Configuration.cs (TargetBeaconEnabled +
    KeyToggleBeacon = Strg+Umschalt+F9), Plugin.cs (Cue vor Beacon konstruiert,
    ToggleTargetBeacon, Tastenuebersicht, /acc soundtest komplett neu),
    OptionsMenu.cs (Schalter), AccessibilityStrings(.Chat).cs (DE+EN),
    README.md, README.en.md.
    Build Debug 0/0 UND Release 0/0, liegt in devPlugins. IN-GAME UNGETESTET.

>>> ZU PRUEFEN BEIM TEST:
    1. ZUERST "/acc soundtest" - der fuehrt alles ohne Herumlaufen vor: erst die
       Steuerung von 180 Grad schrittweise aufs Ziel (man muss hoeren, wie die
       Schlaege auseinandergehen und dann der Quittungston kommt), danach die
       acht Stimmen einzeln, jede vorher benannt.
    2. Sind die acht Stimmen wirklich auseinanderzuhalten? Besonders Gegner
       (330) gegen Inhalts-Eingang (392) - die liegen am dichtesten.
    3. Ein Objekt im Browser waehlen und sich drehen: verstummt der Ton beim
       richtigen Stand, kommt der Quittungston, meldet er sich beim Weiterdrehen
       wieder?
    4. AUFZUG/PLATTFORM - der eigentliche Anlass: LAESST SICH DER AUFZUG
       UEBERHAUPT ANVISIEREN? Wenn ja, funktioniert der Ton dort direkt; wenn
       nein, braucht es die Gehhilfe darauf - und dann muessen wir darueber
       reden, ob solche Objekte doch ohne Anvisieren toenen sollen.
    5. NERVT ER IM KAMPF? Im Kampf ist fast immer ein Gegner anvisiert, also
       laeuft der Ton, sobald man nicht genau auf ihn zielt. Wenn das stoert:
       Strg+Umschalt+F9 ist der Notausschalter - und dann bitte sagen, ob der
       Ton im Kampf ganz schweigen soll (das waere eine bewusste Zusatzregel,
       nicht geraten).
    6. Gehhilfe: klingt sie jetzt wie die Zielart, zu der sie fuehrt?
    7. ZIELWECHSEL: mit Tab durch mehrere Gegner schalten - hoert der Ton fuer
       den vorigen wirklich sofort auf, und faengt der neue sauber an?
    8. ZIEL WEG: einen anvisierten Gegner erlegen - hoert der Ton auf, ohne dass
       man das Ziel von Hand abwaehlen muss? Und schweigt er beim Zonenwechsel?

## FRUEHERER STAND (2026-08-19, "ALLE DUNGEONS DER WELT, NACH STUFE, MIT WEG DORTHIN")

>>> WUNSCH DES USERS: "erstmal brauchen wir eine kategorie wo man zu den dungeons
    laufen kann die kann man ja durch portale betreten sie sollen in der kategorie
    nach stufe sortiert sein und man soll map uebergreifend hinlaufen koennen".

>>> DREI ENTSCHEIDUNGEN HAT ER SELBST GETROFFEN (gefragt, weil sie das Ergebnis
    veraendern, nicht weil sie unklar waren):
    1. Umfang: Dungeons UND Pruefungen UND Raids (nicht nur Dungeons).
    2. Filter: ALLE anzeigen, gesperrte mit dem Wort "gesperrt" - nicht nur die
       freigeschalteten. Er will sehen, was noch kommt.
    3. Neue Kategorie NEBEN der alten "Inhalte", die unveraendert bleibt.

>>> GEBAUT: Kategorie "Alle Inhalte" im Objekt-Browser (Strg+Bild-ab), neuer
    DutyEntranceService.cs.
    - 145 Eintraege, nach Stufe aufsteigend, erster ist Stufe 15 "Sastasha".
    - Ansage: "Dungeon: Sastasha, Stufe 15" + "gesperrt" wenn noch nicht
      freigeschaltet + in dieser Zone Entfernung/Richtung, sonst Gebietsname und
      der naechste Zonenuebergang dorthin + Zaehler.
    - Numpad3 laeuft hin, ueber Zonengrenzen hinweg. Das ist KEINE neue Mechanik,
      sondern genau der Weg, den Quest- und Jagdziele seit Wochen gehen:
      PlacesService.FindFirstHopToMap fuehrt zum naechsten Uebergang, nach dem
      Zonenwechsel zeigt die Liste den naechsten. In der Zielzone selbst laeuft er
      auf die Tuer.

>>> WOHER DIE DATEN KOMMEN, offline gegen das installierte sqpack gemessen (kein
    Raten, kein In-Game-Test noetig fuer diesen Teil):
    - Tuer -> Inhalt: DungeonSide (war schon da). 182 EObj, davon 155 Dungeon/
      Pruefung/Raid.
    - Tuer -> Ort: Level-Sheet. Level.Object = EObj-Zeile, dazu Territory, Map und
      X/Y/Z. Die HOEHE IST ECHT - anders als bei jedem Kartenmarker, der 2D ist
      und geschaetzt werden muss.
    - Der Join ist eindeutig BEWIESEN, nicht plausibel: Level.Type 45 hat einen
      eigenen Object-Id-Bereich (2000002..2015509), der sich mit keinem anderen
      Typ ueberschneidet, und alle 175 auflaesbaren Eingaenge tragen ihn.
    - 3 der 155 haben keine Level-Zeile ("Saegerschrei", zwei Eingaenge zu
      "Verschlungene Schatten 3 - 1"). Die fallen RAUS statt geraten zu werden.
    - 7 Inhalte haben mehrere Tueren, teils in verschiedenen Zonen. Gezeigt wird
      die naechste: gleiche Zone gewinnt, sonst die wenigsten Zonenwechsel. Dafuer
      gibt es PlacesService.GetHopDistances (EIN Breitensuchlauf statt 145).
    - "gesperrt" fragt das SPIEL: UIState.IsInstanceContentUnlocked, gegen die
      installierte FFXIVClientStructs.dll geprueft. Aus Stufe oder Quests wird
      nichts abgeleitet; antwortet das Spiel nicht, schweigt die Ansage.
    Alles in docs/game-api.md eingetragen.

>>> GEAENDERT: DutyEntranceService.cs (neu), DungeonSide.cs (ContentId + All()),
    PlacesService.cs (GetHopDistances), NavigationService.cs (Kategorie
    WorldDuties, CycleWorldDuty, Auswahl-Property, Reset-Regel), Plugin.cs
    (Service + Auto-Lauf-Zweig), AccessibilityStrings.cs (5 Bausteine DE+EN),
    README.md, README.en.md, docs/game-api.md.
    Build Debug 0/0 UND Release 0/0, liegt in devPlugins. IN-GAME UNGETESTET.

>>> ERWARTUNGSWERTE FUER DEN TEST, offline vorausberechnet - damit der erste Blick
    ins Log sofort zeigt, ob die Auswertung im Spiel dasselbe tut:
    - Log beim ersten Oeffnen der Kategorie:
      "[Inhalte] 152 Eingaenge zu Dungeons, Pruefungen und Raids mit Ort;
       3 ohne Ortsangabe im Level-Sheet uebergangen."
    - Kategorie-Ansage: "Alle Inhalte: 145, davon N freigeschaltet."
    - Erste Eintraege beim Blaettern: Stufe 15 Sastasha, 16 Totenacker Tam-Tara,
      17 Kupferglocken-Mine, 20 Das Grab der Lohe (Pruefung), 20 Halatali,
      24 Tausend Loecher von Toto-Rak, 28 Haukke-Herrenhaus.
    - Bis Stufe 30 sind es 7 Eintraege - der Charakter ist Stufe 30, die Zahl
      "davon N freigeschaltet" sollte in dieser Groessenordnung liegen.

>>> ZU PRUEFEN BEIM TEST:
    1. Strg+Bild-ab bis "Alle Inhalte": kommt die Zahl 145?
    2. Bild-ab blaettern: stimmen Reihenfolge und Stufen mit der Liste oben?
    3. Sagt er bei hochstufigen Inhalten "gesperrt"? (Das ist die einzige
       Angabe, die NUR im Spiel pruefbar ist - IsInstanceContentUnlocked laesst
       sich offline nicht messen.)
    4. Numpad3 auf einem Eintrag in einer ANDEREN Zone: laeuft er zum Uebergang?
    5. Numpad3 auf einem Eintrag in der EIGENEN Zone: landet er an der Tuer?

## FRUEHERER STAND (2026-08-19, "SONDERAKTIONEN AUF TASTEN - UND DER FANG-FREIBRIEF IST ETWAS ANDERES")

>>> WUNSCH DES USERS: "in freibriefen oder bei quests gibt es sachen wo man
    gegner fangen oder schwaechen muss, ich vermute da gibt es dann einen button
    den man im richtigen moment druecken muss der wahrscheinlich erst sichtbar
    wird wenns soweit ist - das muessen wir auf eine taste legen".

>>> SEINE VERMUTUNG STIMMT, aber es sind DREI getrennte Mechaniken, und sein
    konkreter Fall war die dritte:
    1. SONDERAKTIONSLEISTE ("Duty Actions") - genau der beschriebene Knopf, der
       nur auftaucht, wenn der Inhalt ihn braucht. GEBAUT (unten).
    2. SCHLUESSELGEGENSTAND (EventItem) - liegt im Inventar, ist auf die Leiste
       legbar. Weg steht in game-api.md, nicht Teil dieser Runde.
    3. EMOTE - und DAS war sein Freibrief. Im Log (2026-08-19 09:33:23):
       Systemmeldung "Besaenftige rasende Ziele, indem du das Emote
       'Beruhigen' (/ruhig) auf sie anwendest", Tracker-Zeile "Schwaeche das
       Ziel vorm Besaenftigen, aber schlag es nicht k. o.". Hier gibt es GAR
       KEINE Sonderaktionsleiste - die Taste unten haette ihm bei den Dodos
       also nicht geholfen.

>>> GEBAUT: DutyActionService.cs (neu).
    - Sagt mit einem Ton an, sobald die Leiste auftaucht oder sich aendert:
      "Sonderaktion verfuegbar, 1: <Name>. Taste Strg+Numpad7."
    - Umschalt+F10 / Umschalt+F11 loesen Platz 1 und 2 aus, Strg+Umschalt+F8
      sagt die Leiste noch einmal an.
    - TASTEN EINMAL GEWECHSELT, und der Grund gehoert ins Protokoll: zuerst lagen
      sie auf Strg+Numpad7/9/1, weil der Nummernblock im Kampf schneller zu
      treffen ist. Der User hat sofort widersprochen ("er nimmt die strg taste
      nicht"). AUF NACHFRAGE BESTAETIGT: Strg+Numpad3 (Gehhilfe) FUNKTIONIERT bei
      ihm - es ist also keine allgemeine Strg-Schwaeche, sondern betrifft genau
      die Ziffern 1/7/9. Damit ist die Zeile "Strg+Numpad1/3/5/7/9 frei" in
      game-api.md fuer 1/7/9 widerlegt: der Dump sagt nur, dass das SPIEL sie
      nicht belegt, nicht dass sie beim Plugin ankommen. Ursache ungeklaert und
      NICHT geraten - in game-api.md als Korrektur eingetragen, mit der Regel
      "fuer neue Tasten nur Strg+Numpad0/3/5, die drei nachweislich
      funktionierenden".
      Umschalt+F10/F11 sind die letzten freien Plaetze des Umschalt+F-Clusters
      und einhaendig zu treffen; nur die Ansage, die nicht eilt, liegt im
      langsameren Dreifachgriff.
    - Migration auf Version 12 mitgeliefert: wer die Zwischenfassung per
      Hot-Reload schon gespeichert hat, bekommt die alten Vorgaben umgezogen -
      eine eigene Belegung bleibt stehen.
    - WARUM DAS UEBERHAUPT NOETIG IST: im Live-Tastenbelegungs-Dump vom
      2026-08-09 (679 Eintraege) hat diese Leiste KEINE EINZIGE Belegung. Das
      Spiel erwartet dort einen Mausklick - sie ist per Tastatur ueberhaupt
      nicht erreichbar, und ihr Auftauchen ist rein visuell.
    - Ausgeloest wird ueber RaptureHotbarModule.ExecuteDutyActionSlot, also die
      spieleigene Methode; der Rueckgabewert wird AUSGEWERTET ("<Name> geht
      gerade nicht"), statt ihn wie Dalamud beim Ziel-Setzen wegzuwerfen.
    - Die API ist nicht nur der Doku entnommen, sondern vom COMPILER gegen die
      echte FFXIVClientStructs.dll bestaetigt: GetInstanceIfReady, ActionId[],
      NumValidSlots, ActionsPresent, ExecuteDutyActionSlot uebersetzen alle.
    - Standard AN (AnnounceDutyActions): hier wird nichts berechnet und nichts
      behauptet, die Leiste ist da oder nicht.

>>> GEBAUT 2 - SONDE FUER DEN FANG-FREIBRIEF ([FangSonde], #if DEBUG).
    Ab WANN ein Gegner "schwach genug" zum Besaenftigen ist, steht in KEINER
    bekannten Struktur - weder Leve-Sheet noch Director fuehren eine Schwelle,
    und Kampflogik raten ist hier verboten. Also wird gemessen: bei JEDER
    Fehlermeldung landen HP-Anteil und die vollstaendige Statusliste des Ziels
    im Log. Gelingt ein Versuch, klammern die Zeilen davor und danach die
    Schwelle ein - und die Statusliste zeigt zugleich, ob es ueberhaupt an den
    HP haengt oder an einem Zustand, den der Gegner erst bekommt.
    Eng gehalten (Lehre vom selben Tag): nur bei Fehlern, nur mit Ziel, nur
    Debug.

>>> GEAENDERT: DutyActionService.cs (neu), AccessibilityStrings.cs (5 Bausteine
    DE+EN), Configuration.cs (3 Tasten + AnnounceDutyActions + Reset-Block),
    Plugin.cs (Verdrahtung, Tasten, Tastenuebersicht), ToastService.cs (Sonde +
    ITargetManager), README.md, README.en.md.
    Build Debug 0/0 UND Release 0/0, liegt in devPlugins. IN-GAME UNGETESTET.

>>> ZU PRUEFEN BEIM TEST:
    1. Einen Inhalt mit Sonderaktionsleiste betreten: kommt Ton + Ansage?
    2. Strg+Numpad7: loest sie aus? Bei Ablehnung die Ansage "geht gerade
       nicht"?
    3. Strg+Numpad1 ausserhalb: sagt "Keine Sonderaktion vorhanden."?
    4. WICHTIG - stoert Umschalt+F10/F11 beim normalen Spielen? Der Dump meldet
       sie als frei, aber genau darauf war bei Strg+Numpad1/7/9 kein Verlass.
    5. Fang-Freibrief noch einmal versuchen und danach das Log schicken: die
       [FangSonde]-Zeilen beantworten die Schwellenfrage.

>>> NOCH OFFEN, BRAUCHT DIE MESSUNG AUS 5: die eigentliche Hilfe fuer den
    Fang-Freibrief waere eine Ansage "jetzt besaenftigen" im richtigen Moment.
    Ohne die gemessene Schwelle waere jede solche Ansage geraten.

>>> ERSTER TEST GELAUFEN (User: "irgendwie hat er immer gesagt keine
    sonderaktion vorhanden"). AUSWERTUNG:

    1. DIE ANSAGE WAR RICHTIG, nicht kaputt. Er hat im Dodo-/Rattenmull-
       Freibrief gedrueckt - genau dem Inhalt, der KEINE Sonderaktionsleiste hat
       (Emote-Mechanik, siehe oben). Dass die Taste ueberhaupt spricht, beweist
       zugleich: Strg+Umschalt+F8 kommt an. Die Leiste selbst ist damit WEITER
       UNGETESTET - dafuer braucht es einen Inhalt, der wirklich eine hat.

    2. MANGEL IM EIGENEN CODE, gefixt: Announce() hat NICHTS protokolliert. Aus
       "keine Sonderaktion vorhanden" liess sich nicht ablesen, ob wirklich
       keine Leiste da war oder ob der Zugriff daneben griff. Jetzt trennt das
       Log die beiden Faelle ("GetInstanceIfReady ist null" vs. "Leiste
       vorhanden, aber alle Plaetze leer").

    3. DIE [FangSonde] LIEFERT - und sie widerlegt die naheliegende Annahme.
       Gemessen am 2026-08-19:
         11:01:22  'Kann noch nicht verwendet werden' -> Rattenmull   100 %
         10:58:20  'Kann noch nicht verwendet werden' -> Rattenmull    48 %
         11:00:51  'Kann noch nicht verwendet werden' -> Dodo          18 %, Status nur 'Blitz' (eigener DoT)
       Bei 18 Prozent wird also NOCH IMMER abgelehnt, und der Gegner traegt
       keinen eigenen Zustand. Damit liegt die Schwelle entweder unter 18
       Prozent - oder die Meldung heisst gar nicht "zu stark". NICHT ENTSCHIEDEN,
       es fehlt der eine Datenpunkt, der alles klaeren wuerde: ein GELUNGENER
       Versuch. Der Zaehler stand die ganze Sitzung auf 0/3.

    4. WARUM ER NIE DAHIN KOMMT, und das ist der eigentliche Befund:
         11:00:51  Ziel HP: 18 Prozent   (Schwelle 25 unterschritten)
         11:00:54  Ziel HP:  2 Prozent   (Schwelle 10 unterschritten)
         11:00:57  "Du hast den verwilderten Dodo besiegt."
       Sechs Sekunden von 18 auf tot. Die Tracker-Zeile sagt woertlich "schlag
       es nicht k. o." - er schlaegt es k. o., weil zwischen den Ansagen bei 25
       und 10 Prozent nichts mehr kommt und sein Blitz-DoT weitertickt. Die
       HP-Stufen 75/50/25/10 sind fuer den normalen Kampf richtig und fuer
       einen Fang-Auftrag zu grob.
       ABHILFE VOM USER FREIGEGEBEN UND GEBAUT (siehe unten).

>>> GEBAUT: FEINE ZIEL-HP IM FREIBRIEF (CombatService.ThresholdsFor).
    Solange ein Freibrief laeuft, gilt unterhalb von 30 Prozent die Reihe
    30/25/20/15/10/5 statt 75/50/25/10. Die Zahl im Satz war immer schon der
    ECHTE Wert - die Stufe entscheidet nur, WANN gesprochen wird.
    Gegenprobe an der Messung von 11:00:51: mit den groben Stufen kam zwischen
    18 Prozent und dem Tod genau EINE Ansage, mit den feinen sind es die
    Uebergaenge bei 15, 10 und 5.
    WARUM JEDER FREIBRIEF UND NICHT NUR FANG-AUFTRAEGE: ob ein Freibrief fangen
    oder toeten will, steht in keinem Feld, das dieses Projekt nachgemessen hat.
    Die Aufgabenzeile sagt es in WORTEN ("besaenftige") - darauf zu pruefen
    wuerde im englischen Client sofort brechen. Also gilt die feine Reihe fuer
    jeden laufenden Freibrief; der Preis sind ein paar Ansagen mehr auf einem
    Toetungs-Auftrag, und dafuer gibt es den Schalter.
    KOSTEN IM NORMALEN KAMPF: keine. Die Freibrief-Abfrage (GetRunningLeve liest
    frisch die Director-Liste) laeuft ERST unterhalb von 30 Prozent - darueber
    sind beide Reihen ohnehin gleich.
    Neuer Schalter FineTargetHpDuringLeve (Standard AN), im gesprochenen
    Einstellungsmenue unter "Feine Ziel-Lebenspunkte im Freibrief".

>>> GEAENDERT: CombatService.cs (HpThresholdsFine, FineBandCeiling,
    ThresholdsFor, LevequestEnemyService injiziert), Configuration.cs,
    Plugin.cs (Konstruktor), OptionsMenu.cs, AccessibilityStrings.Chat.cs
    (DE+EN), DutyActionService.cs (Abfrage protokolliert jetzt), README.md,
    README.en.md.
    Build Debug 0/0 UND Release 0/0, liegt in devPlugins. IN-GAME UNGETESTET.

>>> RAT AN DEN USER FUER DEN NAECHSTEN VERSUCH: den DoT (Blitz) weglassen und
    mit Grundzaubern arbeiten. Der DoT tickt weiter, waehrend er das Emote
    versucht, und genau das hat den Dodo zweimal ueber die Schwelle geschoben.

## FRUEHERER STAND (2026-08-19, "DIE FREIBRIEF-MELDUNG LAS SICH IM SEKUNDENTAKT SELBST VOR")

>>> MELDUNG DES USERS: "wenn ich einen gegner fangen oder schwaechen soll kann
    man mit der numpad 1 auf die meldung gehen die wird aber ununterbrochen
    vorgelesen".

>>> IM LOG NACHGEMESSEN, nicht vermutet (dalamud.log 2026-08-19, 09:38:34 bis
    09:39:52, ueber eine Minute am Stueck):
      [Focus] addon='_ToDoList' id=13 ptr=0x2A1E4C10950
        Text='Schwaeche den Gegner und besaenftige ihn mit dem Emote
              "Beruhigen", St. 14, 19:38, Dodos an Bord'
      ... dieselbe Zeile mit 19:37, 19:36, 19:35, 19:34 ...
    Der Knoten-Zeiger ist IMMER derselbe - der Fokus hat sich nie bewegt.
    Geaendert hat sich nur die RESTZEIT des Freibriefs.

>>> URSACHE: der Fokus-Leser entdoppelt ueber
      if (ptr == _lastFocusedNodePtr && text == _lastFocusedNodeText) return;
    Die Tracker-Zeile ist aber aus mehreren Knoten ZUSAMMENGESETZT (Ziel,
    Stufe, Restzeit, Freibrief-Name). Mit dem Countdown darin ist der Text
    jede Sekunde ein anderer, also greift die Entdopplung nie.
    Der vorhandene Zaehler-Schutz `IsBareNumber` half hier NICHT: er prueft
    einen Text, der NUR aus Ziffern und ':' besteht. Als eigener Knoten wurde
    der Timer korrekt geschluckt (Log: "[Scan] _ToDoList key=40005: '19:51'
    (Zaehler, nicht gesprochen)") - in einem Satz mitgeschleift eben nicht.

>>> GEFIXT: die Entdopplung vergleicht jetzt den Text OHNE laufende Uhren
    (`StripLiveClocks`, neuer Vergleichsschluessel `_lastFocusedNodeStable`).
    Die Zeit wird weiterhin GESPROCHEN - auf einem Freibrief mit Frist ist sie
    echte Information und steht beim ersten Landen auf der Zeile mit drin.
    Nur ihr Ticken zaehlt nicht mehr als neuer Eintrag.
    BEWUSST ENG GEHALTEN: nur Wortformen h:mm / m:ss / h:mm:ss, beidseitig von
    Nicht-Ziffern begrenzt. Breiter waere gefaehrlich - ein Reglerwert, den der
    Spieler gerade mit links/rechts aendert ("40" -> "50"), sitzt auf demselben
    Knoten und MUSS weiter ansagen.
    Die fuenf Stellen, die den Fokus-Zustand zuruecksetzen, setzen den neuen
    Schluessel mit zurueck.
    Build Debug 0 Warnungen / 0 Fehler, liegt in devPlugins. IN-GAME UNGETESTET.

>>> ZU PRUEFEN BEIM TEST: auf die Freibrief-Meldung gehen. Erwartet: EINMAL
    vorgelesen, inklusive Restzeit, danach Ruhe, solange der Fokus dort steht.
    Weggehen und wieder hin -> wieder einmal.

>>> SONDEN AUFGERAEUMT (Auftrag des Users direkt danach). Nicht pauschal
    geloescht - je Sonde geprueft, ob ihre Frage beantwortet ist:

    GEFIXT STATT GELOESCHT - [DeepTraps] (25.254 von 31.315 Zeilen, 85 Prozent
    des ganzen Logs). Loeschen waere falsch gewesen: an dieser Sonde haengt der
    OFFENE Fallen-Umweg (Wunsch vom 2026-08-13). Sie hatte ZWEI echte Fehler:
    1. SIE LIEF UEBERALL. NavigationService ruft DeepDungeon.Poll bei jedem
       Frame, sobald der Dienst existiert - nicht erst im Tiefen Gewoelbe. Eine
       FALLEN-Sonde protokollierte damit die Dodos von Unter-La Noscea. Jetzt
       steht `if (!_floor.IsActive) return;` davor. Keine einzige der 25.254
       Zeilen stand in einem Tiefen Gewoelbe.
    2. IHRE DROSSEL WAR WIRKUNGSLOS. Die Signatur entstand aus der nach
       ENTFERNUNG sortierten Liste; zwei Gegner in aehnlichem Abstand tauschen
       beim Laufen staendig die Plaetze, also war die Signatur immer neu.
       Signatur jetzt nach Objekt-Id sortiert - die Sonde fragt nach "welche
       Objekte, anvisierbar ja/nein", und darauf hat die Reihenfolge keine
       Antwort.
    3. Ausserdem war sie NICHT `#if DEBUG` gekapselt, lief also in jedem
       veroeffentlichten Release mit. Jetzt gekapselt (Methode, Feld, Konstante
       und Aufrufstelle), Release baut 0/0.

    GELOESCHT, weil die Frage beantwortet und das Feature gebaut ist:
    - [ShopProbe] (ProbeShopRow): sollte messen, wo in der Zeile der Preis
      steht. Preis + "du hast N" stehen seit 2026-08-16 in der Ansage. Im
      Quelltext stand woertlich "Faellt raus, sobald der Preis gebaut ist".
    - Die ProbeAddonTexts("_ToDoList")-Sonde am Kategorie-Wechsel (897 Zeilen
      je Sitzung, weil sie bei JEDEM Wechsel den ganzen Tracker dumpte). Der
      Ziel-Leser steht (QuestMarkerService liest _ToDoList, DirectorTodoService
      die Aufgabenliste). ProbeAddonTexts SELBST bleibt - sie ist das Werkzeug
      fuer die naechste unbekannte Oberflaeche.

    BEWUSST STEHEN GEBLIEBEN, weil ihre Messung laut diesem Dokument NOCH
    OFFEN ist:
    - [RestedProbe] - "solange bleibt RestedProbe drin" (zweite Messung bei
      anderem EXP-Stand fehlt).
    - [GatherProbe] - ueberwacht, ab welcher Entfernung ein Sammelpunkt auf
      nutzbar umspringt. Loggt nur Zustandswechsel, 17 Zeilen je Sitzung.
    - [NavDirProbe] - der Richtungsbeweis steht noch aus. Feuert nur je
      Tastendruck.
    - [ListProbe] - laufende Diagnose fuer stumme Listen.
    - [UiProbe] (ProbeFocusContext) - SocialList ist noch offen. Feuerte in
      dieser Sitzung 0 mal.
    - [NavDiag] - eine Zeile je Sekunde, nur waehrend eines Auto-Laufs. Der
      Auto-Lauf hat gerade eine ungetestete Aenderung (Stopp in der Mitte bei
      Freibrief-Zielen), also bleibt die Diagnose diese Runde noch drin.

    ERWARTUNG FUER DIE NAECHSTE SITZUNG: statt 31.315 noch rund 5.100
    Log-Zeilen. Build Debug 0/0 UND Release 0/0.

>>> URSPRUENGLICHER BEFUND: der Log besteht zu 85 Prozent aus
    Debug-Sonden (26.688 von 31.315 Zeilen). Der schlimmste Einzeltaeter ist
    [DeepTraps]: 31 Kampf-NPCs, ZWEIMAL PRO SEKUNDE, komplett mit Position.
    Dazu [Probe] _ToDoList, [GatherProbe], [NavDirProbe], [ShopProbe],
    [ListProbe], [NavDiag]. Nach der Sonden-Konvention gehoeren die nach
    Feature-Ende raus. Sie kosten Bildfrequenz und machen jede kuenftige
    Diagnose muehsam - die Suche nach dieser Meldung war zu 90 Prozent
    Wegfiltern. VORSCHLAG: aufraeumen, sobald die offenen Tests durch sind.

## FRUEHERER STAND (2026-08-19, "FORMANSAGE + VORWARNUNG - UND WARUM DER BOSS SCHWIEG")

>>> AUSLOESER: User ueberlegt, das fremde Kampf-Plugin wieder rauszunehmen - "es hilft mir in
    meiner quest bei dem boss nicht weil es da nichts spricht". Vorschlag von
    ihm: "wenn wir wuessten wie die ganzen bossmechaniken funktionieren
    koennte man was eigenes bauen". Auftrag danach: "bau die formansage und
    die vorwarnung".

>>> DER EIGENTLICHE FUND, und er ist aelter als das fremde Plugin: DIE BOSS-CAST-ANSAGE
    VOM 2026-08-18 WAR TOT AUSGELIEFERT. `UpdateAoeWarning` begann mit
      if (!_config.AnnounceAoeWarning) { _aoeWarn.SetActive(false); return; }
    und `AnnounceAoeWarning` ist STANDARD AUS (bewusst, seit 2026-07-26 Opt-in).
    Die Cast-Ansage war am 2026-08-18 in genau diese Methode gewandert, haengt
    aber an `AnnounceEnemyCast` (Standard AN). Ergebnis: wer nicht zufaellig
    Strg+Umschalt+F3 gedrueckt hatte, bekam KEINE Gegner-Cast-Ansage - egal wie
    die Option stand. Damit erklaert sich das Schweigen beim Boss auch ohne fremde Hilfe.
    GEFIXT: die Methode heisst jetzt `UpdateEnemyCastWarnings`, laeuft sobald
    EINE der beiden Optionen an ist, und jede Option steuert nur noch ihre
    eigene Ausgabe.
    ACHTUNG BEIM TEST: das heisst auch, dass die 08-18er Aenderung ("alle
    Zauber des Ziels") in Wahrheit noch NIE gelaufen ist. Sie wird jetzt zum
    ersten Mal wirklich getestet, nicht ein zweites Mal.

>>> GEBAUT 1 - FORMANSAGE. Hinter der Cast-Ansage steht jetzt Form + Groesse:
    "Gegner wirkt Frostatem. Kegel, 90 Grad, 6 Meter."
    Das Formwort kommt NICHT aus neuem Code, sondern aus dem vorhandenen
    `ActionShapeService` - demselben Describer, der die Form im Faehigkeiten-
    Tooltip nennt. Damit gibt es weiter genau EINE Stelle, die CastType zu
    einem Wort macht, und Tooltip und Kampf koennen nicht auseinanderlaufen.
    Neu ist hier nur die ZAHL: im Kampf steht kein Tooltip daneben, der die
    Reichweite schon genannt haette.
    Der Describer schweigt bei jedem CastType ohne nachgemessene Form
    (`AoeShape.HasProvenShape`) - dieses Schweigen wird durchgereicht. Ein
    geratenes "Fläche, 30 Meter" waere die schlimmere Auskunft: bei Kegel und
    Linie ist EffectRange die LAENGE, nicht ein Radius.
    Sonderfall mitgenommen: Kreis, dessen Ziel der Spieler selbst ist ->
    "Kreis um dich, 5 Meter". Dort hilft Ausweichen nicht, nur Abstand.

>>> GEBAUT 2 - VORWARNUNG "du stehst drin", zwei Faelle:
    - Beim Cast-Beginn schon drin: haengt an derselben Ansage.
      "Gegner wirkt Frostatem. Kegel, 90 Grad, 6 Meter. Du stehst drin, 3 Sekunden."
    - Waehrend des Casts hineingelaufen: eigener Satz "Achtung, du stehst
      drin, 2 Sekunden."
    Restzeit = `TotalCastTime - CurrentCastTime`, also die spieleigene
    Cast-Leiste, nicht geschaetzt. Aufgerundet; unter einer Sekunde "sofort".
    Doppelansage ausgeschlossen: wer den Satz an der Cast-Ansage bekommt, ist
    in `_aoeInside` vermerkt und wird vom Einlauf-Waechter uebersprungen.
    Verlaesst man die Flaeche, faellt der Vermerk - Wiedereintritt warnt erneut.
    HAENGT AN `AnnounceAoeWarning` (Standard AUS), weil es die noch nicht
    bestaetigte Geometrie in Worte fasst. -> USER MUSS Strg+Umschalt+F3
    DRUECKEN, sonst kommt nur Name + Form.

>>> NEBENBEI KORRIGIERT: `IsPlayerInAoe` schaltete auf die nackten Zahlen
    2/3/4, obwohl `AoeShape` seit 2026-08-09 sieben bestaetigte CastTypes
    kennt. Eine 30-Meter-LINIE mit CastType 8/12 landete damit im
    default-Zweig und wurde als 30-Meter-KREIS um den Gegner gerechnet -
    genau der V1-Fehler, und eine Erklaerung fuer einen Ton, obwohl man
    hinter dem Gegner sicher steht. Jetzt ueber die AoeShape-Konstanten,
    inkl. 5 (Kreis), 8/12 (Linie), 13 (Kegel).

>>> GEAENDERT: CombatService.cs (Optionsgatter getrennt + umbenannt,
    DescribeCastShape, RemainingCastTime, TrackAoeEntry, _aoeInside,
    IsPlayerInAoe auf AoeShape), AccessibilityStrings.cs (AoeShapeWithRange,
    AoeShapeWithRangeOnYou, AoeStandingInIt, AoeEnteredZone, AoeSeconds,
    CastWithDanger - alle DE+EN), AoeWarningService.cs / Plugin.cs /
    OptionsMenu.cs (Verweise auf den alten Methodennamen), README.md,
    README.en.md.
    Build Debug 0 Warnungen / 0 Fehler, liegt in devPlugins. IN-GAME UNGETESTET.

>>> ZU PRUEFEN BEIM TEST (Reihenfolge wichtig):
    1. ERST Strg+Umschalt+F3 druecken (Flaechenwarnung an), sonst fehlt die
       halbe Ansage.
    2. Irgendein Gegner anvisieren, der castet: kommt ueberhaupt eine
       Cast-Ansage? Das ist der Test des Options-Fixes.
    3. Kommt Form + Meterzahl dahinter, und klingt die Zahl plausibel?
    4. In eine Flaeche hineinstellen: kommt "Du stehst drin, N Sekunden"?
    5. Waehrend eines laufenden Casts hineinlaufen: kommt "Achtung ..."?
    6. WICHTIGE STOERFRAGE: redet es im Bosskampf jetzt zu viel? Der Satz ist
       deutlich laenger geworden, und jeder Cast unterbricht per
       SpeakInterrupt. Falls ja: die Form kuerzen (Winkel weg) oder die
       Vorwarnung von der Form trennen.

>>> ZUM FREMDEN KAMPF-PLUGIN, ENTSCHEIDUNG VERTAGT: nicht rausgeworfen. Belegt
    bleibt, dass es fuer Levelinhalte fast nichts hat (Stein-Vigil: 1 Trigger, nichts fuer
    den Endboss). Der eigene Weg oben deckt jeden Gegner im Spiel ab, aber
    NICHT die Bedeutung ("zusammenstehen", "auseinander", "Tankbuster") -
    das ist Handarbeit je Boss und steht in keinem Sheet. Erst testen, dann
    entscheiden.

## FRUEHERER STAND (2026-08-18, "DAS SPIEL SAGT SELBST, WEM DER GEGNER GEHOERT")

>>> TESTERGEBNIS DES USERS: die Freibrief-Gegner tauchten in der Kategorie NICHT
    auf. Dazu seine Beobachtung: "in der normalen gegner liste stehen sie mit
    einem quest symbol". Diese Beobachtung hat den Fall geloest.

>>> IM LOG NACHGEMESSEN (dalamud.log 2026-08-18, 22:09 und 22:21) - das Objekt
    traegt die EventId SEINES Freibrief-Directors:
      'Streunender Dodo'    EventId=2147549737 (BattleLeveDirector/32769, Eintrag 553)
      'Traeger-Marienkaefer' EventId=2147549739 (BattleLeveDirector/32769, Eintrag 555)
    Jedes normale Zonentier daneben - auch die Dodos DERSELBEN Art - liest
    EventId=0. Damit trennt genau ein Feld Freibrief-Spawn von Wildwuchs.
    EventId-Struktur per ilspycmd geprueft: Id@0 (ganze 4 Byte), EntryId@0,
    ContentId@2. Verglichen wird die VOLLE Id, nicht nur die Director-Art -
    der Eintrag unterscheidet unseren Freibrief vom Freibrief des Spielers,
    der danebensteht.

>>> DIE ALTE ANNAHME WAR AN ZWEI STELLEN FALSCH, beides jetzt im Code korrigiert:
    - "Namensschild-Symbol ist 0 auf Leve-Monstern" - NEIN, beide gemessenen
      Spawns trugen Symbol 71244. Genau das hoert der User als "Quest" in der
      Gegner-Liste.
    - GetEventHandlersImpl ist eine SACKGASSE: es meldet fuer jedes Monster
      denselben zonenweiten Handler (25770280390 fuer den Leve-Marienkaefer UND
      fuer die wilden Marienkaefer), nie die Director-Adresse. Deshalb fand die
      Kategorie 0 Gegner, obwohl der Freibrief lief und der Gegner 26 m weit weg
      stand. Raus aus dem Code, als Sackgasse dokumentiert.

>>> ZUM VORSCHLAG "die Gegner mit in die Quest-Gegner schmeissen": das tut die
    Kategorie BEREITS. Der Quest-Gegner-Filter nimmt jedes Kampf-NPC mit einem
    Symbol aus 71000-71999, und 71244 liegt darin. Im Log ist der Beweis, dass
    der Weg traegt: "Quest-Gegner: 1 von 33 (per Symbol 1). Symbole:
    Randalranke=71244". Bei der Messung um 22:24 stand nur deshalb 0 da, weil
    der markierte Marienkaefer zwei Minuten vorher gefallen war (HP 0 Prozent
    um 22:22:11). Es wird also NICHTS doppelt gebaut.

>>> GEAENDERT: LevequestEnemyService.cs (BelongsToDirector -> BelongsToLeve ueber
    EventId, LayoutIdOf raus, RunningLeve traegt DirectorEventId),
    NavigationService.cs (Bindung + Doku-Kommentare richtiggestellt, Log nennt
    jetzt Director-EventId und listet Spawns eines FREMDEN Freibriefs).
    Build Debug 0 Warnungen / 0 Fehler, liegt in devPlugins. IN-GAME UNGETESTET.

>>> NACHTRAG DESSELBEN ABENDS: "ich sehe die gegner nicht die ich finden sollte
    und der freibrief fuehrt mich nur in die naehe - bin ich genau in dem kreis?"
    Antwort aus dem Log: NEIN. Der Zielkreis 'Gefraessige Puks' hat r=50 um
    (162,1|47,5|179,8); der Spieler stand 75 m von der Mitte, also 25 m
    DRAUSSEN. Und er konnte es nicht wissen - die Ansage nannte den Kreis nie.

>>> URSACHE DES "nur in die Naehe" (Log 22:41:18 und 22:41:25):
    "Auto-Lauf: gestartet zu Gefraessige Puks (stopRange=50,0, dist=74,8)"
    -> "beendet (angekommen, dist=51,4)", und der zweite Druck meldete sofort
    "angekommen, dist=51,3". Die Stopp-Reichweite ist der KREISRADIUS, der Lauf
    endet also 50 m vor der Mitte. Bei einem "Such am Zielort"-Freibrief ist der
    Rand wertlos: die Gegner erscheinen erst, wenn man IM Kreis heruemlaeuft.

>>> ZWEI AENDERUNGEN DARAUS (beide gebaut, Build 0/0, in devPlugins):
    - Ansage nennt den Kreis: "im Zielkreis" bzw. "noch 25 Meter bis zum
      Zielkreis" (GoalCircleHint, aus QuestDestination.Radius, DE+EN). Gilt fuer
      Quest- UND Freibrief-Ziele; bei Punkt-Markern (Radius 0) bleibt es still.
    - Numpad 3 laeuft bei FREIBRIEF-Zielen bis zur Mitte statt zum Rand
      (Rolle LeveObjective). Vom User auf Nachfrage so entschieden. Normale
      Quest-Marker behalten den Rand-Stopp - dort war das eine bewusste
      Entscheidung vom 2026-08-03.

>>> WEITER OFFEN: die Gegner selbst. Der neue EventId-Weg LAEUFT (Log 22:37 bis
    22:41: "Director-EventId 2147549742, Eintrag 558"), findet aber 0 - und
    meldet zugleich "Spawns eines FREMDEN Freibriefs: keine". Also trug KEIN
    einziges Objekt der Zone eine Leve-EventId: die Gegner waren schlicht nicht
    da. Passt zu 'Fortschritt' Zaehler=0 und dazu, dass der Spieler ausserhalb
    des Kreises stand. Naechster Test: erst in den Kreis (jetzt laeuft Numpad 3
    dorthin), dann die Kategorie.

>>> OFFEN: Strg+Umschalt+F7 (Aufgabenliste) hat im ganzen Log kein einziges Mal
    ausgeloest - kein "[Aufgaben]"-Eintrag. Wurde die Taste ueberhaupt gedrueckt?
    Erst danach laesst sich sagen, ob die Ansage oder die Tastenerkennung haengt.

## FRUEHERER STAND (2026-08-18, "AUFGABENLISTE WIRD VORGELESEN")

>>> AUFTRAG DES USERS nach der Freibrief-Messreihe: "bau die aufgabenliste als
    ansage ein". Dazu der Hinweis, er muesse den Freibrief mit seiner Klasse
    vielleicht gar nicht machen - er testet das noch.

>>> GEBAUT: Strg+Umschalt+F7 liest die Aufgabenliste des LAUFENDEN INHALTS vor.
    Nicht nur Freibrief - dieselben Felder tragen Dungeon und FATE, also gilt
    die Taste ueberall. Das sind genau die Zeilen, die ein sehender Spieler
    dauerhaft am Bildschirmrand stehen hat und auf die der User bisher KEINEN
    Zugriff hatte.
    Beispiel aus dem Log: "Aufgaben: Laestige Nager. Abgesuchte Gebiete.
    Streunender Dodo."

>>> AUF EINER TASTE UND NIE AUTOMATISCH, mit Absicht: die Liste aendert sich
    bei jedem Kill, eine Ansage je Aenderung wuerde in den Kampf hineinreden.

>>> QUELLE (ilspycmd-verifiziert): Director.Title@760, Objective@864,
    DirectorTodos@1088. Je Zeile: Enabled@0, Type@4 (TodoType), Text@8,
    Complete@112 und eine UNION @120/@124 - CurrentCount/NeededCount bei den
    Bruch-Arten, CurrentPercentage bei den Balken-Arten, EndTimestamp@128 bei
    der Zeit-Art. Der Typ wird ausgewertet, statt ein Feld blind zu lesen:
    ein Prozentwert als Anzahl vorgelesen waere eine Zahl ohne Bedeutung.
    Alles liegt in der 1120-Byte-Basis, also ohne Unterklassen-Erkennung.
    Der TEXT kommt vom Spiel und wird NICHT uebersetzt - nur der Fortschritt
    dahinter bekommt hier seine Worte (DE+EN).

>>> GEAENDERT: DirectorTodoService.cs (neu), AccessibilityStrings.cs (Ansage-
    Bausteine + Hilfetext DE/EN), Configuration.cs (KeyReadTasks),
    Plugin.cs (Verdrahtung + AnnounceActiveTasks), README.md, README.en.md.
    Build Debug 0 Warnungen / 0 Fehler, liegt in devPlugins. IN-GAME UNGETESTET.

## FRUEHERER STAND (2026-08-18, "FREIBRIEF-GEGNER IN DER FREIBRIEF-KATEGORIE")

>>> WUNSCH DES USERS: "kategorie einbauen fuer freibrief ziele? wenn man
    gegner toeten muss dass man sie ueber die freibriefe kategorie direkt
    anlaufen kann". Die Kategorie "Freibriefe" gab es schon (Geber + Ziel-
    Marker), aber ein Marker zeigt nur die FLAECHE. Ein sehender Spieler
    sieht dort die Monster stehen - der blinde Spieler stand im Kreis und
    hatte nichts mehr, woran er sich orientieren konnte.

>>> GEBAUT: Die Kategorie fuehrt jetzt zusaetzlich die LEBENDEN Gegner des
    gerade LAUFENDEN Freibriefs, naechster zuerst, VOR Geber und Ziel.
    Auswahl setzt das Spielziel (angreifen) UND merkt das Objekt (Numpad 3
    laeuft hin, auch wenn das Spiel das Anvisieren ablehnt).
    Ansage: "Freibrief-Gegner: Stolper-Fungus, 7 gesucht, 12 Meter, vorne
    rechts, Stufe 1, 100 Prozent, 2 von 5."
    Zaehl-Ansage der Kategorie nennt die Gegner nur, wenn es welche gibt.

>>> NUR WAEHREND DER FREIBRIEF LAEUFT, mit Absicht: die Gegner eines Freibriefs
    heissen wie ganz normales Wild ("Nussknackerhoernchen"). Aus einem
    angenommenen, aber nicht gestarteten Freibrief gelistet, wuerde die
    Kategorie auf zufaellige Viecher zeigen, deren Toeten nicht zaehlt.

>>> BELEGT (ilspycmd + Offline-Sheet-Dump, steht in docs/game-api.md):
    - Laufender Freibrief = Director in EventFramework->DirectorModule.
      DirectorList mit Info.EventId.ContentId == GuildLeveAssignment(6);
      die Nummer wird gegen QuestManager.LeveQuests gegengeprueft.
    - FALLE umgangen: Director ist exakt 1120 Byte, LeveDirector.LeveId liegt
      bei 1120. Es gibt kein Feld, an dem man die Unterklasse erkennt - also
      wird NICHTS ab 1120 gelesen. Freibrief-Nummer kommt aus EventId.EntryId.
    - Gegner kommen aus den Sheets: Leve.DataId -> BattleLeve (Typ 1
      "Soeldner") bzw. CompanyLeve (Typ 13-15), je Slot BNpcName +
      Stueckzahl. Zeilennummern der vier Leve-Sheets sind disjunkt, deshalb
      loest RowRef eindeutig auf (offline an den Spieldaten gemessen).

>>> ERSTE ANNAHME IST GEFALLEN - und das Log hat sie gefaellt (21:38/21:41):
    "Content=BattleLeveDirector(32769) Entry=537 DirectorContentId=528
     Titel='Laestige Nager'"
    - Die Director-Art steht in EventId.ContentId, aber mit Werten ab 0x8000:
      BattleLeveDirector 32769, GatheringLeveDirector 32770,
      CompanyLeveDirector 32775. GuildLeveAssignment(6) ist der Geber-NPC-
      Handler und kommt hier NIE vor. MEINE ANNAHME WAR FALSCH.
    - Die Freibrief-Nummer steht in Director.ContentId@736 (528, stand in der
      Liste der angenommenen; Leve 528 heisst im Sheet "Laestige Nager").
      EventId.EntryId war bei zwei Laeufen 537 bzw. 542 - eine Instanz-Nummer.
    - Der Director erscheint erst beim STARTEN des Freibriefs, nicht beim
      Annehmen (21:38:51 nur FateDirector, 21:41:11 wieder BattleLeveDirector).
    Beides korrigiert, Erwartungswerte fuer die 9 angenommenen Freibriefe
    offline vorausgerechnet: der laufende (528) verlangt streunender Dodo
    (1096), Buschratte (1101), Rattenmull (1083).

>>> ZWEITE RUNDE, ZWEITER BEFUND (Log 21:46-21:50): Director und Gegnerarten
    wurden erkannt, aber 0 Objekte gefunden. Der Objekt-Dump des Users hat es
    aufgeklaert - DER FREIBRIEF SPAWNT KEINE EIGENE ART:
    - Leve 528 Slot 0 nennt BNpcName 1096 "streunender Dodo". Das Monster, das
      2,9 m vor dem Spieler erschien, trug NameId 393 "Dodo" - dieselbe Art wie
      die wilden Dodos 40 m weiter. Zuordnung ueber BNpcName oder ueber den
      Namen findet deshalb NICHTS. Genau das tat der erste Bau.
    - Was zusammenpasst: Slot.BaseID (BNpcBase 339) mit obj.BaseId (339). Die
      wilden Dodos tragen 339 aber auch - die Art allein trennt nicht.
    - Nebenbei bestaetigt: ICharacter.NameId IST die BNpcName-Zeile
      (393 "Dodo", 405 "winzige Mandragora", 115 "Wind-Exergon").

>>> DRITTE RUNDE (Log 21:57): BEIDE Signale gemessen TOT.
    - "0 Director-Objekte": EventHandler.EventObjects ist bei laufendem
      Freibrief LEER. Weg (1) faellt aus.
    - Jeder Dodo hat "Symbol=0", auch die zwei direkt beim Spieler (5 m, 9 m).
      Der Nameplate-Weg faellt auch aus. Die Notiz vom 2026-07-28, Freibrief-
      Gegner trugen ein Quest-Symbol, trifft auf diesen Fall nicht zu.
    - Damit ist belegt: 17 Dodos in 130 m, alle BaseId 339, alle Symbol 0 -
      ueber Art und Symbol sind die zwei Freibrief-Dodos nicht auffindbar.

>>> VIERTE RUNDE (Log 22:04) - FELD-ABZUG DA, UND ER SAGT ETWAS ANDERES:
    'Dodo' 4m  Handler=[257DF5AFC80] EventId=0 LayoutId=3766591 OwnerId=E0000000
    'Dodo' 9m  Handler=[257DF5AFC80] EventId=0 LayoutId=3766592 OwnerId=E0000000
    'Dodo' 44m Handler=[257DF5AFC80] EventId=0 LayoutId=3766584 OwnerId=E0000000
    - GetEventHandlers LIEFERT etwas, aber fuer ALLE Dodos denselben Handler
      (257DF5AFC80), und der ist NICHT der Director (25777735EE0).
    - Alle Felder identisch: EventId 0, EventState 0, FateId 0, Symbol 0,
      GimmickId 0, OwnerId E0000000.
    - Das EINZIGE, was sich unterscheidet, ist LayoutId - und die liegt bei den
      nahen wie bei den fernen Dodos im selben zusammenhaengenden Block
      (3766581..3766596). Das ist die Signatur von ZONEN-Objekten.
    >>> DARAUS FOLGT DER VERDACHT, DEN ICH NICHT SELBST PRUEFEN KANN: die
        Freibrief-Gegner waren zum Messzeitpunkt GAR NICHT in der Objekttabelle.
        Kein einziger Buschratte (BaseId 37) oder Rattenmull (BaseId 205) war je
        in Reichweite, und die Dodos sehen alle nach Zonen-Wild aus.
        Leve 528 ist Rule 8 "BattleLeveRound", Ziel "Beseitige die Gegner im
        Zielgebiet" - moeglicherweise erscheinen die Gegner erst IM Zielkreis.

>>> FUENFTE RUNDE (Log 22:09) - DER VERDACHT IST BESTAETIGT:
    "Aufgabenzeilen: 'Abgesuchte Gebiete' fertig=False Zaehler=1 /
     'Streunender Dodo' fertig=False Zaehler=1.
     Ohne Layout-Eintrag (also nachtraeglich gesetzt): keine."
    - Der Freibrief VERLANGT einen "Streunenden Dodo" - die Aufgabenzeile des
      Spiels sagt es woertlich.
    - Es gibt aber KEIN einziges Objekt ohne Layout-Eintrag, also nichts, was
      der Director nachtraeglich gesetzt haette. Und kein Objekt traegt
      NameId 1096 (streunender Dodo); alle Dodos in Reichweite sind NameId 393.
    >>> Der gesuchte Gegner war zu keinem Messzeitpunkt in der Objekttabelle.
        Leve 528 ist Rule 8 "BattleLeveRound": man sucht GEBIETE ab
        ("Abgesuchte Gebiete" ist die erste Aufgabenzeile), und die Gegner
        erscheinen erst dort. Der Code hat also vermutlich die ganze Zeit
        richtig gearbeitet und nur nichts zu finden gehabt.

>>> STAND: HIER WIRD NICHT WEITER GERATEN (Regel "Stuck -> stop and report").
    Vier Erkennungswege gemessen: BNpcName/Name tot, EventObjects leer,
    NamePlateIcon 0, GetEventHandlers liefert fuer alle denselben Nicht-
    Director-Handler. Der fuenfte Abzug legt nahe, dass gar kein Gegner da war.
    OFFENE FRAGE AN DEN USER: kam bei den Laeufen ueberhaupt ein Gegner?
    ERST danach ist zu entscheiden, ob die Erkennung noch ein Problem hat.

>>> NEBENERGEBNIS, DAS ETWAS TAUGT: Director.DirectorTodos liefert genau die
    Zeilen, die ein sehender Spieler in der Aufgabenliste liest
    ("Abgesuchte Gebiete", "Streunender Dodo"). Der User hat darauf bisher
    KEINEN Zugriff. Als eigene Ansage anzubieten.

>>> MESSUNG ERWEITERT (kein neuer Rateversuch):
    - Director.DirectorTodos wird protokolliert - die spieleigene Aufgabenzeile
      ("... 0/4"). Die sagt, ob der Freibrief ueberhaupt Gegner erwartet.
    - Jeder Kampf-NPC OHNE Layout-Eintrag (LayoutId 0) wird protokolliert,
      unabhaengig von der Art. Ein vom Director gesetzter Gegner sollte keinen
      Zonen-Layout-Eintrag haben - das ist das einzige Feld, das im Abzug
      ueberhaupt variiert hat.

>>> ZWEI FEHLGRIFFE, ALSO KEIN DRITTER BLIND: jetzt fragt das Plugin das SPIEL.
    GameObject.GetEventHandlersImpl (virtual function 30) fuellt ein Array mit
    den Event-Handlern, zu denen ein Objekt gehoert. Der Leve-Director ist
    darunter oder nicht - das ist die spieleigene Antwort auf "wessen Objekt
    ist das". Vergleich ueber die Director-Adresse.
    ABSICHERUNG FUER DEN FALL, DASS AUCH DAS NICHTS BRINGT: fuer die vier
    naechsten Artgenossen OHNE Director-Bindung schreibt das Log jetzt den
    vollen Feld-Abzug (EventId inkl. Content/Entry, LayoutId, EventState,
    OwnerId, FateId, Symbol, GimmickId) plus die gemeldeten Handler-Adressen.
    Damit faellt die Entscheidung beim naechsten Mal an einem Abzug, nicht an
    einer weiteren Vermutung.

>>> AUCH GEMESSEN: BattleLeve.ToDoNumberInvolved taugt NICHT als Stueckzahl.
    Ein Monster fuellt oft mehrere Slots (Leve 530: Bienenwolke 4x mit je 2;
    Leve 527: Wander-Mandragora 5 und 0), Leve 528 hat ueberall 0. Die Zahl
    wird deshalb nur angesagt, wenn eine Art genau einen Slot belegt.

>>> GEAENDERT: LevequestEnemyService.cs (neu), MonsterNameText.cs (neu -
    Namensaufloesung aus HuntingLogService herausgezogen, beide nutzen sie
    jetzt), NavigationService.cs (Gegner in der Freibrief-Kategorie),
    QuestMarkerService.cs (Rolle LeveEnemy), AccessibilityStrings.cs (DE+EN),
    Plugin.cs (Verdrahtung), docs/game-api.md.
    Build Debug 0 Warnungen / 0 Fehler, liegt in devPlugins.
    IN-GAME UNGETESTET.

>>> ZU PRUEFEN BEIM TEST (Kampf-Freibrief annehmen UND starten):
    - Sagt die Kategorie beim Aufrufen "Freibriefe: N Gegner, ..."?
    - Kommen die Gegner beim Blaettern vor Geber und Ziel?
    - Laeuft Numpad 3 wirklich zum Gegner?
    - Falls nichts kommt: die beiden [Leve]-Zeilen aus dem Log schicken.

## FRUEHERER STAND (2026-08-18, "BOSS-ZAUBER WERDEN ANGESAGT")

>>> AUSLOESER: User meldet, das fremde Kampf-Plugin sage ausser dem einen Satz nichts mehr,
    beim Drachen am Ende von Stein-Vigil komme gar keine Meldung. Frage des
    Users: "was soll es alles ansagen?"

>>> ZWEI URSACHEN, beide am Quelltext belegt, nicht vermutet:
    1. DAS FREMDE PLUGIN HAT FUER DEN DRACHEN NICHTS. In seiner
       Ausloeser-Datenbank steht fuer Stein-Vigil genau EIN Eintrag (id 387,
       Quelle Chudo-Yudo), nichts fuer den Endboss, in keiner Sprache. Muster
       ueber alle ARR-Dungeons: 0 bis 6 Eintraege je Instanz, meist 1 bis 3.
       Solche Werkzeuge zielen auf aktuelle Raids, nicht auf Levelinhalte -
       da ist nichts nachzujustieren.
    2. UNSER Kampfteil schwieg AUS REGEL, nicht aus Fehler. CombatService
       sprach nur bei `CastTargetObjectId == playerId` (Entscheid 2026-07-25).
       Ein Boss wirft aber fast alles auf den Boden oder auf den Tank, also
       kam nichts. Der AoE-Ton greift erst, wenn man SCHON in der Flaeche
       steht - als Vorwarnung taugt er damit nicht.

>>> ENTSCHEIDUNG DES USERS: "alle zauber des bosses". Umgesetzt als: jeder
    Zauber des ANVISIERTEN Gegners wird angesagt, dazu weiterhin jeder Zauber
    auf den Spieler von beliebigen Gegnern. Warum das Ziel und nicht "jeder
    Gegner": das Ziel ist der eine Gegner, den der Spieler bewusst gewaehlt
    hat - in der Praxis der Boss -, waehrend Trash-Gruppen still bleiben. Ein
    verlaessliches "ist ein Boss"-Kennzeichen gibt es auf IBattleChara nicht.

>>> WORTLAUT GEAENDERT, das ist die Falle fuer den Nutzer: "Gegner wirkt X."
    hiess bisher IMMER "auf dich". Jetzt bedeutet es das Gegenteil. Deshalb
    bekommt der gezielte Fall den Zusatz: "Gegner wirkt X auf dich." bzw.
    "<Name> wirkt X auf dich." (EnemyCastsAtYou / NamedEnemyCastsAtYou, DE+EN).
    Ohne den Zusatz klaenge der gefaehrliche Fall wie der harmlose.

>>> GEAENDERT: CombatService.cs (AnnounceCastAtMe + Bedingung in
    UpdateAoeWarning), AccessibilityStrings.cs (zwei neue Strings).
    Build Debug 0 Warnungen / 0 Fehler, liegt in devPlugins.
    IN-GAME UNGETESTET.

>>> ZU PRUEFEN BEIM TEST:
    - Redet es im Bosskampf zu viel? Jeder Boss-Cast unterbricht per
      SpeakInterrupt die laufende Ausgabe.
    - Trash-Gegner, die man anvisiert, casten auch - stoert das beim Leveln?
    - Kommt "auf dich" hoerbar rueber, oder ist es zu spaet im Satz?

## FRUEHERER STAND (2026-08-18, "EINSTELLUNGEN: FALSCHE UND FEHLENDE ANSAGEN")

>>> NACH DEM RELEASE GEMERGT, noch in KEINEM Release (waere v5.89):
    - PR #8 (dnz3d4c): Jagdziel in anderer Zone klebte Lebensraum und Gebiet
      zusammen ("lebt in Kaktusgrundim Gebiet Ost-Thanalan"). Gegengeprueft:
      HuntingArea endet ohne Leerzeichen und liefert "" ohne Lebensraum,
      InArea/InAnotherArea beginnen ohne eines, und die beiden anderen
      InArea-Aufrufstellen (Quest :806, Freibrief :954) setzen ihr ", " selbst -
      der Fix gehoert also an die Aufrufstelle, nicht in den String. Merge-
      sauber, Build 0/0. Autor hat nur einen koreanischen Client, die deutsche
      und englische Formulierung habe ich an den Strings selbst nachgesehen.
    - Schwesterstelle gleich mitgezogen (NavigationService :1143): Jagdziel in
      DERSELBEN Zone ohne Lebensraum ergab ", , 30 Meter, Norden". Andere
      Aufrufstellen geprueft: HuntingAreaUnknown faengt den leeren Fall selbst
      ab, HuntingMonsterNearby ist nie leer.
    Beides in-game ungetestet.

>>> ALS v5.88 VEROEFFENTLICHT (2026-08-18). Verifiziert: Tag v5.88 ist Latest,
    6 Assets dran, und die latest-Weiterleitung liefert byteweise dieselbe
    Datei wie der lokale Build (SHA 8F6B46D5...), Manifest im ZIP steht auf
    5.88.0.0. Antwort an den Spieler liegt in `antwort-spieler-fps-anzeige.txt`.
    OFFEN BLEIBT: die Optionsmatrix im Grafik-Reiter ist weiter ungetestet,
    ebenso die neue Seitenansage mit der Anzahl und F10 im Konfigurationsfenster.

>>> AUSLOESER: User meldet "in den Einstellungen wird in einigen Menues nicht
    alles vorgelesen". Log 2026-08-18 13:26-13:31 + Dump (ConfigSystem, 593
    Nodes) ausgewertet. Vier verschiedene Fehler, alle belegt, alle gebaut.
    Build Debug 0 Warnungen / 0 Fehler, liegt in devPlugins. IN-GAME UNGETESTET.

>>> FEHLER 1, der schwerste: FALSCHE BESCHRIFTUNG bei Auswahllisten/Reglern.
    Die Beschriftungssuche lief ueber die KNOTENREIHENFOLGE (rueckwaerts).
    Die Liste ist aber nach absteigender NodeId sortiert, nicht nach Layout,
    und die Richtung ist je Panel verschieden. Beweis aus demselben Dump:
    Aufklappfeld id 374 (Wert "Mittel: 1024 Pixel") liegt auf NodeList[225],
    davor [222] "Schattenkaskadierung", dahinter [226] "Schattenauflösung" —
    angesagt wurde die falsche der beiden. Ebenso: "* Erfordert Neustart"
    statt Texturauflösung, "Lebendige Körperdarstellung" statt
    Anisotropischer Filter, "Blendeffekte (Glare)" statt Raumtiefe (SSAO).
    Der Barrierefreiheits-Reiter war ZUFAELLIG richtig (Label davor) — deshalb
    ist es nie aufgefallen.
    GEBAUT: `ConfigLabelByGeometry` — Beschriftung ueber ScreenX/ScreenY
    (gleiche Zeile, Text links; sonst naechste Ueberschrift darueber). Gilt
    fuer ConfigSystem UND die ConfigChara*-Panels. Listenreihenfolge bleibt nur
    als Rueckfall und LOGGT, wenn sie greift.

>>> FEHLER 2: OPTIONSMATRIX ohne Zeilennamen. "Lebendige Körperdarstellung"
    sind 4 Zeilen (Selbst/Gruppe/Andere/Gegner) mal 3 Knoepfe (Aus/Einfach/
    Voll), Dump-Nodes 402-420. Angesagt wurde nur das Optionswort — zwoelf
    Knoepfe klangen gleich. GEBAUT: Auswahlknoepfe bekommen den Zeilennamen
    links vorangestellt ("Gegner, Einfach, ausgewählt"). Nur bei
    RadioButton und nur aus der ZEILE (eine Ueberschrift darueber wuerde
    sonst an jeder Option der ganzen Gruppe kleben).

>>> FEHLER 3: DEBOUNCE VERSCHLUCKTE GANZE ANSAGEN. TolkService unterdrueckte
    gleichen Text innerhalb 0,5 s — in der Matrix hiess das: Schritt auf einen
    ANDEREN Knopf, und es kam gar nichts (Log 13:31:00.598 und 13:31:01.584,
    beide "DEBOUNCED 'Einfach'"). GEBAUT: `SpeakInterrupt(text, source)` mit
    dem Fokusknoten als Quelle. Gleicher Knoten = weiterhin unterdrueckt,
    anderer Knoten = spricht. Aufrufer ohne Quelle verhalten sich unveraendert.

>>> FEHLER 4: DOPPELANSAGE mit falscher Aussage. Nach dem CS-Leser sagte der
    allgemeine Fokusleser 7 ms spaeter den Wert nochmal als "…, Schalter, aus"
    — ein Aufklappfeld ist kein ausgeschalteter Schalter. Ursache: das
    Anzeigefeld eines Aufklappfelds IST eine CheckBox-Komponente. Fuer
    ConfigChara* war das schon gefixt, fuer ConfigSystem ausdruecklich nicht.
    GEBAUT: `IsGlobalConfigControl` — in ConfigSystem schweigt der allgemeine
    Leser bei Slider/DropDownList.

>>> WERKZEUG: der Node-Dump (F5) schreibt jetzt `@ScreenX,ScreenY BreitexHoehe`
    in jede Zeile. Ohne Koordinaten war die Layoutfrage am Dump gar nicht zu
    beantworten — genau deshalb lief die Beschriftungssuche ueber die
    Knotenreihenfolge. Neu dokumentiert in game-api.md.

>>> ERSTER TESTLAUF (13:53-13:54) AUSGEWERTET:
    - Beschriftungen: 14 von 14 ueber die Geometrie, Rueckfall auf die
      Listenreihenfolge KEIN einziges Mal. Neu und richtig: Grafik-
      Voreinstellungen, Auflösungstyp, Art der Hochskalierung, Gezackte Ränder
      glätten, Durchscheinendes Licht, Reflexionseffekte in Echtzeit,
      Hauptbildschirm wählen, Voreinstellungen, Standardgröße, Aktivieren bei.
      Elf per RowLeft, drei per HeadingAbove.
    - NACHGEBESSERT: die Doppelansage war nur halb weg. Das falsche
      ", Schalter, aus" fiel weg, aber der allgemeine Pfad holte den Wert als
      nackten Text nach (13:53:34.055 Ansage, 13:53:34.061 nochmal
      "Mittel (Desktop)"). Jetzt schweigt der allgemeine Leser in ConfigSystem
      komplett bei Regler/Aufklappfeld.
    - Optionsmatrix noch NICHT getestet, der Block kam im Testlauf nicht vor.
      Bei den beruehrten Auswahlknoepfen kam kein Zeilenname - dafuer loggt
      jetzt `[CS-Zeile]` die Entscheidung.

>>> ZWEI FRAGEN DES USERS, beide am Log geklaert:
    - "man muss zwei mal auf grafik druecken": GEMESSEN, es ist das SPIEL.
      Ein Tastendruck setzt den Fokus auf den Reiter, ~500 ms spaeter wechselt
      die Seite von selbst (Event 29 param=14, dazwischen KEIN Eingabe-Event,
      13:53:33.540 bis 13:53:34.040). Beim ERSTEN Besuch einer Seite bleibt der
      Fokus dabei auf der Reiterleiste, beim zweiten springt er in die Seite -
      zweimal reproduziert (Grafik 13:53:27 vs. 13:53:34, Anzeige 13:53:44 vs.
      13:53:46). Plugin.cs fasst ConfigSystem nirgends an, schluckt also nichts.
    - "gibts noch andere Menuepunkte ausser Grafik-Voreinstellungen": die
      Seitenansage sprach nur die erste Ueberschrift, und die heisst dort
      genauso wie die erste Einstellung. GEBAUT: Seitenansage nennt jetzt die
      ANZAHL ("Grafik-Voreinstellungen, 21 Einstellungen",
      `CountVisibleConfigOptions`).
    - MITGEFUNDEN: F10 (ganzes Fenster vorlesen) las im Konfigurationsfenster
      alle acht Seiten am Stueck vor - dieselbe Sichtbarkeitsfalle wie bei den
      Beschriftungen (`TryReadWholeWindow` nahm das eigene Flag statt
      IsEffectivelyVisible). Behoben, gilt fuer jedes Fenster.

>>> SPIELERMELDUNG (englischer Client, 2026-08-18): "when I go to the settings
    for configure the fps limit, nvda say all the time the fps number, in all
    settings, and it cut the other infos". GEMEINT ist die laufende
    Bildfrequenz-Anzeige IM Systemkonfigurations-Fenster (deutscher Dump:
    NodeList[590], id 4, "59 fps") - sie laeuft auf allen acht Seiten mit und
    aendert sich jede Sekunde. Der Aenderungs-Scanner sagte sie an und schnitt
    dabei jede andere Ansage ab (SpeakInterrupt).
    URSACHE: der Filter erkannte sie am WORT "fps" bzw. an einer reinen
    Ganzzahl. Beides ist sprach- und formatabhaengig - "59.9" oder eine
    uebersetzte Einheit rutschen durch. Welche der beiden Luecken es bei ihm
    ist, ist UNBEKANNT (kein Log von ihm).
    GEBAUT (`IsLiveConfigText`), zwei sprachunabhaengige Kriterien:
    1. Ein Node, der sich binnen 3 s dreimal aendert, ist eine laufende
       Anzeige - ab dann dauerhaft stumm, mit einer Logzeile als Beweis.
    2. Ein geaenderter Text, der mit einer Ziffer beginnt, wird im
       AENDERUNGS-Scanner nicht angesagt (Werte von Bedienelementen sagt der
       Fokus-Leser ohnehin mit Beschriftung an).
    Beschriftungen mit fuehrender Ziffer sind nicht betroffen: sie aendern sich
    nicht und erreichen den Scanner nie.
    OFFEN: Rueckmeldung des Spielers, ob damit Ruhe ist.

>>> ZU TESTEN (in dieser Reihenfolge, alles im Systemkonfigurations-Fenster):
    1. Reiter Grafik: ueber die Aufklappfelder blaettern. Erwartet sind jetzt
       "Schattenauflösung", "Texturauflösung", "Anisotropischer Filter",
       "Raumtiefe betonen (SSAO)" statt der Nachbarnamen. Und jeder Eintrag
       darf nur EINMAL kommen, ohne "Schalter, aus" hinterher.
    2. Reiter Grafik, Block "Lebendige Körperdarstellung": durch die 12
       Knoepfe gehen. Erwartet "Selbst, Aus" / "Selbst, Einfach" / ... und
       KEINE stille Stelle mehr.
    3. Reiter Barrierefreiheit (war vorher schon richtig): "Stärke",
       "Größe", "Transparenz" muessen genauso bleiben — das ist die
       Gegenprobe, dass die Geometrie nichts kaputtmacht.
    4. Charakterkonfiguration (ConfigChara*, z.B. Chatlog-Einstellungen):
       stichprobenartig, dieselbe Umstellung greift dort auch.
    5. OFFEN, noch nicht gebaut (Fehler 5): im Reiter Farbschema stehen zwei
       Knoepfe komplett ohne Text (Dump-Nodes 503/504, nur Collision+Image,
       Tooltip leer) — der Fokus landet drauf und es kommt nichts (Log
       13:31:17.887 / 13:31:18.916, "STUMM"). Vermutung, NICHT belegt: Pfeile
       neben der Farbschema-Auswahl. MESSUNG dazu: auf dem Reiter Farbschema
       F5 druecken (Dump hat jetzt Koordinaten), dann auf einen der beiden
       Knoepfe gehen und ihn druecken — aendert sich danach der Wert der
       Auswahlliste "Farbschema wählen", ist die Funktion bewiesen und die
       Knoepfe koennen benannt werden.

## FRUEHERER STAND (2026-08-17, "v5.87 IN-GAME BESTAETIGT + LEBENSRAUM-LUECKE VERMESSEN")

>>> v5.87 JAGDZIELE: VOM USER IN-GAME BESTAETIGT. Blaettern und Numpad3 zum
    Monster funktionieren. Der offene Test aus den Bloecken darunter ist damit
    ERLEDIGT.

>>> RESTBESCHWERDE DES USERS: "manchmal, wenn er keine Monster in Reichweite
    bzw. im Gebiet findet - da muesste man schauen, wo welche rumlaufen."

>>> OFFLINE VERMESSEN (Scratchpad huntdump, Lumina gegen das installierte
    Spiel - kein In-Game-Test noetig). Ueber ALLE 362 eindeutigen
    Jagdtagebuch-Ziele, 368 Lebensraum-Zeilen:
    - Zone ohne Karte:            0
    - ohne Teilgebiet:            0
    - Teilgebiet OHNE Marker:    20
    - Teilgebiet MIT Marker:    348  (= laufbare Weltposition vorhanden)
    Der Lebensraum-Rueckfall hat also in 95 % der Faelle schon eine
    Koordinate. Die Marker-Kette PlaceNameLocation -> MapMarker.
    PlaceNameSubtext -> Weltposition traegt breiter als gedacht.

>>> DIE 20 AUSREISSER SIND ALLE DASSELBE, und zwar bewiesen, nicht vermutet:
    Halatali (Karte 46), Versunkener Tempel von Qarn (42), Palast des
    Wanderers (32), Saegerschrei (52). Jedes dieser Teilgebiete traegt eine
    EIGENE Karte - es sind Instanzen/Dungeons, keine Orte im Freien. Deshalb
    gibt es keinen Marker auf der Zonenkarte, und deshalb ist eine Koordinate
    dort auch die falsche Antwort: man laeuft nicht hin, man geht hinein.
    Erkennungsregel, generisch: Teilgebiet-PlaceName hat eine eigene Map
    (mapByZonePlace) => Instanz.

>>> NOCH NICHT GEKLAERT: welchen Fall der User konkret erlebt hat. Kandidaten:
    (a) Monster lebt in einer ANDEREN Zone -> Lauf geht zum Gebietsuebergang,
    (b) einer der 20 Dungeon-Faelle, (c) am Gebietsmarker angekommen, aber die
    Monster stehen verstreut / ausserhalb der Streaming-Reichweite.
    NICHT bauen, bevor das geklaert ist - sonst wird der falsche Fall gefixt.

>>> GRENZE, ehrlich vermerkt: echte SPAWN-Koordinaten pro Monster liefern die
    Excel-Sheets nicht. Das Level-Sheet fuehrt BNpcBase-Positionen (Type 9,
    game-api.md:783), aber die Verbindung BNpcBase <-> BNpcName steht nicht in
    den Sheets (siehe Warnung game-api.md:794). Ob es dafuer einen sauberen
    Weg gibt, ist UNGEPRUEFT - keine Behauptung in beide Richtungen.

## FRUEHERER STAND (2026-08-17, "ANALYSE: EIGENES WEGENETZ? - ERGEBNIS: NEIN, ALLES BLEIBT")

>>> NICHTS GEBAUT, reine Untersuchung. Der In-Game-Test zu v5.87 aus dem Block
    darunter STEHT WEITER AUS.

>>> FRAGE DES USERS: vnavmesh koennte von den Entwicklern als Botting gewertet
    werden - koennen wir ein eigenes Wegenetz bauen, die Wege irgendwo
    extrahieren, lokal mitliefern (spart evtl. Ladezeiten)?

>>> WOHER vnavmesh DIE WEGE NIMMT - am dekompilierten Code geprueft, nicht
    geraten (ilspycmd gegen die installierte vnavmesh.dll):
    - Es gibt KEINE Wege-Datenbank, weder im Spiel noch bei vnavmesh. Es rechnet.
    - Quelle 1: das lebende Layout aus dem Spielspeicher, SceneDefinition.
      FillFromActiveLayout (SceneDefinition.cs:35) liest aus LayoutWorld->
      ActiveLayout, welche Terrains/Objekte/Kollisionsboxen wo stehen.
    - Quelle 2: die .pcb-Kollisionsdateien aus dem sqpack ueber Lumina,
      Service.DataManager.GetFile (SceneExtractor.cs:165/177/305).
    - Danach voxelisiert Recast (DotRecast) die Geometrie zur begehbaren
      Flaeche. Ergebnis ist eine FLAECHE, keine Route - Detour sucht den Weg
      bei jeder Anfrage neu darauf.
    => "Wege extrahieren" hat kein Ziel. Es existiert nur Kollisionsgeometrie.

>>> VORBERECHNET MITLIEFERN - drei harte Waende (gemessen):
    - Groesse: 57 Cache-Dateien auf dem Rechner des Users = 88 MB, und das sind
      nur die besuchten Gebiete. Eine grosse Zone (r1f1) allein 11 MB, dieselbe
      Zone liegt mehrfach vor (Festivals/ZoneSGs gehen in den Cache-Schluessel
      ein, NavmeshManager.GetCacheKey). Unser Release-ZIP ist 972 KB.
    - Rechtslage: abgeleitete Arbeit aus SE-Geometriedaten im eigenen Release
      verteilen ist deutlich exponierter als lokal rechnen. Geht in die
      FALSCHE Richtung, gemessen am Ziel.
    - Patch-Haltbarkeit: Format Version 25 + CustomizationVersion + Layout-
      abhaengiger Cache-Schluessel. Jeder Patch entwertet.

>>> KERN-EINWAND, vom User akzeptiert: nach Botting sieht nicht das Wegenetz
    aus, sondern die automatische Steuerung der Figur. Ein eigenes Netz senkt
    das Risiko um null. Ausserdem ist aus SE-Sicht jedes Drittanbieter-Plugin
    inklusive Dalamud selbst schon ausserhalb.

>>> ZWEITE FRAGE: Auto-Lauf rausnehmen, nur manuell laufen? Befund:
    - Die Gehhilfe (Umschalt+Numpad3, NavigationService.WalkGuideFrame:2250)
      ist BEREITS eine vollstaendige Wegpunkt-Fuehrung ueber Nav.Pathfind
      (reine Abfrage, bewegt nichts - RouteService.cs:71). Mit Vorschau in
      Himmelsrichtungen, Neuberechnung, Hoehen-Hinweis, Netz-Ende-Erkennung.
    - Beide Tasten laufen ueber dieselbe TryResolveMarkerDestination
      (Plugin.cs:1384 vs. :1406) - der Auto-Lauf ist nur der Zwilling.
    - Nur DREI Stellen haengen exklusiv am Auto-Lauf und muessten umgehaengt
      werden: Bestiarium (Plugin.cs:1402), Sammelpunkt (:960), Tiefes
      Gewoelbe (:954).
    - FUND: die Gehhilfe kann bewegte Ziele schon - WalkGuideFrame:2253-2264
      holt die Zielposition jeden Frame neu. Ein "Folgen per Ansage" waere ein
      kleiner Umbau, keine neue Funktion.
    - Echt verloren ginge nur die TrailWalking-Phase (aufgezeichnete Spuren
      per Path.MoveTo) - die existiert gerade, WEIL das Netz dort keine
      Verbindung kennt.

>>> ENTSCHEIDUNG DES USERS: alles bleibt wie es ist. Begruendung: das Folgen
    soll automatisch passieren, "das gibts ja vom Spiel auch so".

>>> OFFEN, daraus entstanden: Hat FFXIV ein natives Folgen (Textbefehl
    /follow)? Der Kommentar in Plugin.cs:1415 BEHAUPTET "FFXIV has no
    plugin-callable native follow" - das ist NIE geprueft worden. Gaebe es
    den Befehl, koennte unser vnavmesh-getriebenes Folgen durch den
    spieleigenen ersetzt werden; dann bewegt das Spiel die Figur, nicht wir.
    Pruefung waere das TextCommand-Sheet (Verfahren: Offline-Sheet-Dump).
    Der User hat das zur Kenntnis genommen, aber noch nicht beauftragt.

## FRUEHERER STAND (2026-08-17, "RELEASE v5.87 - JAGDZIELE: DIREKT ZUM MONSTER")

>>> VEROEFFENTLICHT als v5.87, aber IN-GAME NOCH NICHT GETESTET - der User hat
    den Release direkt nach dem Bauen verlangt. Der Test unten steht weiter aus.

>>> VERSIONS-SYNC (alle drei Stellen auf 5.87): Plugin.cs PluginVersion "5.87",
    csproj 5.87.0 / 5.87.0.0, repo.json "5.87.0.0". Die veroeffentlichte
    repo.json auf main traegt 5.87.0.0 - ueber raw.githubusercontent.com
    nachgeprueft, nicht nur lokal.

>>> SECHS ASSETS am Release v5.87: latest.zip (972.384) und
    FF14Accessibility-v5.87.0.zip (dieselbe Datei), FF14AccessibilityInstaller.
    exe (162.517.183) + installer.json - Installer seit v5.86 UNVERAENDERT
    (git log Installer/ leer), darum exe vom letzten Release wiederverwendet,
    SHA256 gegen installer.json geprueft; dazu LICENSE und THIRD-PARTY-NOTICES.md.

>>> VERIFIZIERT: gh release list zeigt v5.87 als "Latest", und
    releases/latest/download/latest.zip liefert die 972.384 Bytes - ausgepackt,
    die DLL darin traegt 5.87.0.0 und LICENSE/THIRD-PARTY liegen im Archiv.

>>> HINWEIS ZUM ABLAUF: GitHub hatte waehrenddessen eine Stoerung ("Partial
    System Outage", seit 13:40 UTC); der Releases-Endpunkt gab mehrfach 503.
    Anlegen und Upload liefen erst per Wiederholungslauf durch.

>>> WUNSCH DES USERS: In der Browser-Kategorie "Jagdziele" soll Numpad3 zum
    MONSTER laufen, nicht nur in dessen Gebiet.

>>> GEBAUT:
    - HuntingLogService.FindNearestLive(name): naechstes lebendes Exemplar aus
      der Objekttabelle (ObjectKind.BattleNpc, Name case-insensitive, CurrentHp
      0 raus). Das ist jetzt die EINZIGE Suchstelle - der Bestiarium-Weg
      (Plugin.TrackBestiaryMonster) benutzt dieselbe Methode statt einer
      eigenen Kopie.
    - Numpad3 auf einem Jagdziel: steht ein lebendes Exemplar in Reichweite,
      wird es anvisiert und der Lauf laeuft ueber den ZIEL-Pfad (MarkerResolve.
      None) - der liest die Position jeden Frame neu, deshalb funktioniert das
      auch bei patrouillierenden Monstern. Lehnt das Spiel das Anvisieren ab,
      wird auf die zuletzt gesehene Position gelaufen. Kein Exemplar da: alles
      unveraendert wie bisher (Gebietsmarker bzw. Gebietsuebergang).
    - Ansage beim Blaettern nennt in dem Fall Entfernung und Richtung zum
      MONSTER ("in der Naehe, 23 Meter, Norden") statt zum Gebiet, und Ziele
      mit lebendem Exemplar stehen vorne in der Liste.
    - NavigationService.TargetFromBrowser() ist die gemeinsame Ziel-Setzung
      (setzt _ownSelectionId, sonst verwirft der Ziel-Waechter die Auswahl).

>>> OFFENE ANNAHME, bewusst so gebaut: die Zuordnung Log-Eintrag -> Monster in
    der Welt laeuft ueber den NAMEN. Fuer [a]-Namen ist das gedeckt (V5.86),
    fuer die Platzhalter [p]/[t] weiter unbewiesen. Findet die Suche nichts,
    schreibt eine DEBUG-Sonde die acht naechsten Kampf-NPCs mit Name und NameId
    ins Log - ein Schreibfehler faellt dort sofort auf.

>>> ZU TESTEN: In einem Gebiet mit offenem Jagdziel die Kategorie oeffnen,
    blaettern (sagt es "in der Naehe" mit plausibler Entfernung?) und Numpad3
    (laeuft er zum Monster und ist es anvisiert?).

## FRUEHERER STAND (2026-08-17, "RELEASE v5.86 - JAGDZIELE IM OBJEKT-BROWSER")

>>> VEROEFFENTLICHT. Alles aus dem Block darunter ist damit oeffentlich:
    Bestiarium-Rangzeilen und die Kategorie "Jagdziele".

>>> VERSIONS-SYNC (alle drei Stellen auf 5.86, vor dem Release abgeglichen):
    Plugin.cs PluginVersion "5.86", csproj 5.86.0 / 5.86.0.0, repo.json
    "5.86.0.0". Die veroeffentlichte repo.json auf main traegt 5.86.0.0 -
    nachgeprueft ueber raw.githubusercontent.com, nicht nur lokal.

>>> SECHS ASSETS am Release v5.86 (die vier ueblichen plus die beiden, die seit
    heute frueh offen waren):
    - latest.zip (971.687) und FF14Accessibility-v5.86.0.zip (dieselbe Datei)
    - FF14AccessibilityInstaller.exe (162.517.183) + installer.json - Installer
      seit v5.85 UNVERAENDERT (git log Installer/ leer), darum die exe vom
      letzten Release wiederverwendet; SHA gegen installer.json geprueft, passt.
    - LICENSE und THIRD-PARTY-NOTICES.md nun auch als EIGENE Assets, damit sie
      sieht, wer nur die Installer-EXE zieht. Damit ist der offene Punkt vom
      Vormittag erledigt.

>>> VERIFIZIERT, dass Spieler die neue Fassung bekommen: gh release list zeigt
    v5.86 als "Latest", und releases/latest/download/latest.zip liefert
    tatsaechlich 971.687 Bytes (v5.85 waren 949.395) mit der DLL vom 17.8. 18:31
    und der LICENSE im Archiv - heruntergeladen und ausgepackt, nicht nur
    angenommen.

>>> WEITERHIN OFFEN (unveraendert): die zugesagte inhaltliche Rueckmeldung unter
    PR #7 (CLAUDE.de.md verweist an vier Stellen auf docs/game-api.md; welche
    der beiden game-api-Fassungen fuehrt kuenftig?) und die ausstehenden
    Lizenz-Zustimmungen von bladestorm360 und blindndangerous.

## FRUEHERER STAND (2026-08-17, "BESTIARIUM: RANG-ZEILEN BENANNT, JAGDZIEL-KATEGORIE GEBAUT")

>>> ZWEI WUENSCHE DES USERS: (1) die Bestiarium-Zeilen, die nur "1, 10 von 10"
    heissen, sollen sagen, was sie sind. (2) Frage, ob eine Objekt-Browser-
    Kategorie moeglich ist, mit der man direkt zu den Monstern laufen kann,
    "evtl auch map uebergreifend". Grundlage: frischer MonsterNote-Dump des
    Users (Desktop\FFXIV_UI_Dump.txt, 16:10).

>>> TEIL 1 GEBAUT UND DEPLOYT (Debug, 0 Warnungen), IN-GAME UNGETESTET:
    Die Rang-Liste (Dump-Node 16, eine List AUSSERHALB der TreeList) rendert
    ihre Zeilen als nackte Zahl + Zaehler. Neu gesprochen:
    "Rang 1, alle 10 Eintraege erledigt" / "Rang 3, 0 von 10 Eintraegen erledigt".
    - UIReaderService.TryFormatBestiaryRank, erkannt an der FORM (Zahl + genau
      ein Fortschritts-Token), NICHT an Node-Ids - Ids wiederholen sich, und die
      einzige andere Liste im Fenster (Klassenfilter) traegt Namen.
    - IsSpokenProgress ist jetzt ein Aufsatz auf TryParseSpokenProgress, damit
      die Zahlen nur an einer Stelle geparst werden.
    - AccessibilityStrings.BestiaryRankRow, DE+EN.

>>> WAS DER ZAEHLER ZAEHLT - GEMESSEN, NICHT GERATEN (das war die offene Frage:
    Kopf sagt "Rang 3" und "3/48", die Rangzeile "10/10"):
    Offline-Sheet-Tool (Scratchpad, Lumina gegen das installierte Spiel):
    - MonsterNote hat 600 Zeilen in ZWOELF Bloecken zu 50 (neun Klassen + drei
      Staatliche Gesellschaften), lueckenlos: Thaumaturg = 70001..70050.
    - 5 Raenge zu je 10 Eintraegen. Rang 3 = 70021..70030.
    - Die Kill-Zahlen dieser zehn Zeilen summieren sich auf GENAU 48 - das ist
      die "3/48" im Fensterkopf. Damit ist bewiesen: die Rangzeile zaehlt
      EINTRAEGE (10), der Kopf zaehlt KILLS (48).

>>> TEIL 2: MACHBARKEIT GEKLAERT, VOM USER ENTSCHIEDEN, NOCH NICHT GEBAUT.
    Gemessen (dasselbe Tool):
    - 647 Lebensraum-Angaben im Jagdtagebuch, ALLE 647 lassen sich einer Karte
      zuordnen. 590 (91 %) haben zusaetzlich einen Kartenmarker auf ihrer
      eigenen Zonenkarte, also eine Koordinate. Die 40 ohne Marker sind
      Dungeon-Gebiete (Halatali, Palast des Wanderers) - dorthin laeuft man
      ohnehin nicht.
    - ClassJob.MonsterNote ist KEIN Zeilenverweis, sondern der Klassen-Index des
      Jagdtagebuchs (Gladiator 0 ... Thaumaturg 6, Hermetiker 7, Schurke 11;
      Handwerker/Sammler 127, Jobs nach ARR -1). Exakt der classIndex-Parameter
      von AgentMonsterNote.OpenWithData. Jobs erben den Index ihrer Klasse.
    - Ein Block gehoert seiner Klasse ueber den NAMEN ("Thaumaturg 21"); fuer
      alle neun Klassen-Bloecke gegen ClassJob.Name geprueft.
    - Fortschritt liegt in MonsterNoteManager.RankData (12 Slots, je Rank/Index/
      Flags und 10 Eintraege zu 4 Kill-Zaehlern; ilspycmd-geprueft).
    ENTSCHEIDUNG DES USERS: Kategorie zeigt die noch OFFENEN Ziele des AKTUELLEN
    RANGS der aktuellen Klasse; Ziele in anderen Zonen werden angesagt und
    Numpad3 laeuft zum passenden Zonenuebergang (vorhandene Uebergangs-Route,
    kein neuer Mechanismus).

>>> GEBAUTES FUNDAMENT (Debug deployt, in-game ungetestet):
    - HuntingLogService: Klassen-Index, Block-Zuordnung, die zehn Sheet-Zeilen
      eines Rangs, Lebensraeume mit Koordinate.
    - PlacesService.FindMapByPlaceName + FindMarkerPosition: Ort auf einer
      FREMDEN Karte aufloesen. GetPlaces konnte bisher nur die Karte, auf der
      der Spieler steht.

>>> SPEICHERLAYOUT GEMESSEN (Log 2026-08-17 17:00, /acc huntprobe) - ALLE DREI
    OFFENEN PUNKTE BEANTWORTET:
    - RankData ist NACH DEM KLASSEN-INDEX indiziert: jeder der 12 Slots loggte
      seine eigene Position im Feld Index (Slot 6 = Index 6 = Thaumaturg).
    - Rank ist 0-BASIERT: der Charakter steht im Fenster auf Rang 3, der Slot
      meldet Rank=2.
    - Counts stehen in Sheet-Reihenfolge: Eintrag 0 las 0/2, waehrend das Fenster
      Wuchernde Efeuranke 0/4 und Daemonenfliegenfalle 2/2 zeigt - Ziel 0 und 1
      von Zeile 70021. Gegenprobe: die Zaehler des Rangs summieren sich auf 3,
      exakt die "3/48" im Kopf.
    - Zusatzbefund Gladiator (Slot 0, Rank=1 -> Rang 2): Eintraege 0/1/2 stehen
      auf 3/3, 3, 3 - Zeile 10011 verlangt Bomber x3 + Kupfer-Kobalos x3, 10012
      und 10013 je x3. Passt exakt, zweite unabhaengige Bestaetigung.
    - Flags bleibt unangetastet: Bedeutung ungeklaert, "Eintrag fertig" folgt
      ohnehin aus den Zaehlern. NICHT geraten.

>>> VOM USER IN-GAME BESTAETIGT (17.8. abends, "ok funktioniert") - knapp
    zurueckgemeldet, ohne Zahlen. Was er im Einzelnen geprueft hat (nur die
    Rang-Zeilen, nur die Kategorie oder beides) ist damit NICHT belegt; die
    vorausberechneten 13 Ziele sind nicht gegengezaehlt worden.

>>> SONDE WIEDER ENTFERNT (Konvention: Sonden verschwinden mit dem Feature).
    /acc huntprobe und HuntingLogService.ProbeHuntingLog sind raus, die
    Messergebnisse stehen oben und als Kommentar an GetProgress.

>>> TEIL 2 GEBAUT (Debug deployt):
    - Neue Browser-Kategorie "Jagdziele" (NavCategory.HuntingTargets). Sie
      erscheint NUR, wenn der aktuelle Rang noch etwas offen hat - Handwerker,
      Jobs nach ARR und ein fertiger Rang liefern eine leere Liste, und eine
      leere Kategorie ist im Durchblaettern nur Rauschen (Regel wie Angelplaetze
      und FATEs).
    - Ansage je Eintrag: Monster, "x von y erlegt", Gebiet. In der eigenen Zone
      zusaetzlich Entfernung + Richtung; in einer anderen Zone deren Name plus
      der Uebergang dorthin, wie bei den Quest-Zielen.
    - Numpad3 laeuft zum Gebietsmarker bzw. zum ersten Zonenuebergang.
    - Deklination geloest: BNpcName.Pronoun ist das Genus, also wird aus
      "wuchernd[a] Efeuranke" ein sprechbares "wuchernde Efeuranke"
      (0 = -er, 1 = -e, 2 = -es). Zwei der drei Formen sind gegen echte
      UI-Namen belegt (Dump heute: "Wuchernde Efeuranke" bei Pronoun 1;
      Log 19.7.: "Rostiger Kobalos" bei Pronoun 0), Neutrum ist deutsche
      starke Deklination. UNBELEGT und darum ersatzlos entfernt: die
      Platzhalter [p] (1195 Namen) und [t] (201) - Weglassen kann eine Endung
      kosten, aber nie einen falschen Namen erfinden.

>>> ERWARTUNGSWERTE FUER DEN ERSTEN TEST (offline vorausberechnet, Stand der
    Sonde): Thaumaturg Rang 3 hat GENAU 13 OFFENE ZIELE, alle mit Marker:
    Efeuranke 0/4 Ostwald/Neun Efeuranken; Wald-Yarzon 0/4 Oberes La Noscea/
    Eichenwald; Aas-Yarzon 0/2 und Lachkroete 0/4 Westliches Thanalan/Tal der
    Spuren; Rindenmolch 0/2 Suedwald/Obere Pfade; Haarmuecke 0/4 und
    Gluehwuermchen 0/2 Ostwald/Brombeerlichtung; Fluss-Yarzon 0/4 Suedwald/
    Florarium der Stille; Lehmfliegenschwarm 0/4 und Knoecheltaenzer 0/4
    Suedliches Thanalan/Mordsdurst; Flausch 0/4 Oestliches Thanalan/
    Wellwick-Forst; Feuer-Exergon 1/4 Suedliches Thanalan/Das Rote Labyrinth;
    Todesweide 0/4 Tiefer Wald/Sauerampferweide.
    Weicht die Ansage davon ab, stimmt die Auswertung nicht.

>>> EINSCHRAENKUNG, DIE DER USER KENNT: Der Marker ist der MITTELPUNKT DES
    GEBIETS, nicht das Monster. Die Kategorie fuehrt ins richtige Gebiet, das
    Suchen davor uebernimmt die Gegner-Kategorie - derselbe Stand, den ein
    sehender Spieler mit dem Jagdtagebuch hat.

## FRUEHERER STAND (2026-08-17, "CONTROLLER-BEDIENUNG: MACHBARKEIT GEPRÜFT, NICHTS GEBAUT")

>>> FRAGE DES USERS: Wie aufwendig wäre es, das Plugin auch mit Controller
    bedienbar zu machen - "wir müssten aber schauen auf welche Controlertasten
    wir die Mod-Tasten legen". Reine Recherche, kein Code geschrieben.

>>> ERGEBNIS IN EINEM SATZ: Die Technik ist der einfache Teil, die Tastenknappheit
    ist das Problem - 52 belegte Kombinationen im Plugin gegen 16 Knöpfe am
    Controller, und die sind im Gamepad-Modus alle vom Spiel belegt.

>>> VERIFIZIERT (ilspycmd gegen Dalamud.dll + FFXIVClientStructs.dll, Details in
    docs/game-api.md -> "Gamepad-Eingabe"):
    - Dalamud hat IGamepadState mit Pressed/Repeat/Released/Raw + beide Sticks.
      Das ist genau die Flankenerkennung, die Plugin.cs für die Tastatur selbst
      baut - müsste also nicht nachgebaut werden.
    - GamepadButtons kennt exakt 16 Knöpfe (D-Pad 4, Gesichtstasten 4, L1/L2/L3,
      R1/R2/R3, Start, Select).
    - Keybind.GamepadSettings (2 Slots pro Aktion) steht in DERSELBEN Tabelle,
      die KeybindService schon ausliest. Die Frage "welche Knöpfe sind frei"
      lässt sich also messen statt raten - genau wie beim Tastatur-Dump 2026-07-10.
    - Plugin-Seite: 52 Key-Felder in Configuration.cs, alle als Text über
      ParseKeySpec. Ein zweiter Eingabeweg dockt zentral an, die 52 Funktionen
      dahinter blieben unangetastet. SpokenMenu.cs (347 Zeilen, hierarchisch)
      wäre das fertige Gerüst für ein Ringmenü.

>>> DAS NADELÖHR, NICHT GEMESSEN: Einzelne Knöpfe schlucken geht über Dalamuds
    öffentliche API NICHT. Bei der Tastatur genügt KeyState[key]=false; das
    Gamepad-Interface ist rein lesend. Dalamud-intern gibt es nur alles-oder-
    nichts (ImGui NavEnableGamepad -> ButtonsPressed=None). Ansatzpunkte wären
    GamepadInputAddress oder die spieleigene UIInputData.FilterGamepadInputs,
    aber ob ein gefilterter Knopf die Spiellogik wirklich nicht mehr erreicht,
    steht in KEINER Quelle. Ohne diese Messung löst jeder Mod-Knopf zusätzlich
    seine Spielfunktion aus.

>>> VORGESCHLAGENE ETAPPEN (mit dem User noch nicht entschieden):
    1. KeybindService um GamepadSettings erweitern -> Dump, welche Knöpfe belegt
       sind. Klein, risikolos, Grundlage für alles Weitere. UNGEPRÜFT dabei: ob
       die Key-Werte in GamepadSettings dieselben VK-Codes sind wie bei der
       Tastatur oder eine eigene Nummerierung.
    2. Schluck-Sonde: erreicht ein gefilterter Knopf das Spiel noch?
    3. Eingabeschicht: Gamepad-Bindung neben jedem KeyXxx.
    4. Bedienkonzept: modales Ringmenü über SpokenMenu, weil 52 > 16.
    Etappe 1+2 je ein überschaubarer Happen, 3+4 zusammen in der Größenordnung
    des Nachlese-Browsers.

## FRUEHERER STAND (2026-08-17, "LIZENZ UND FREMDSOFTWARE-HINWEISE")

>>> ANLASS: PR #7 (blindndangerous, Übersetzung der deutschen Dokumente) merkte
    am Rande an, dass es keine LICENSE-Datei gibt. Beim Nachprüfen war das nicht
    der eigentliche Fund.

>>> DER EIGENTLICHE FUND: Das ausgelieferte ZIP enthält seit jeher DREI fremde
    Bibliotheken OHNE jeden Lizenzhinweis - nachgezählt in
    dist/FF14Accessibility-v5.85.0.zip:
    - Tolk.dll -> LGPL-3.0, "(c) 2014-2019, Davy Kager" (aus der
      Versionsressource der DLL selbst gelesen, nicht geraten)
    - nvdaControllerClient64.dll -> LGPL-2.1 (NV Access; die DLL hat KEINE
      Versionsressource, Copyright-Zeile daher nicht wörtlich belegbar)
    - NAudio 2.2.1, sieben DLLs -> MIT, "Copyright 2020 Mark Heath" (aus
      ~/.nuget/packages/naudio/2.2.1/license.txt)
    MIT verlangt den Copyright-Vermerk "in all copies", LGPL einen Lizenzhinweis
    samt Bezugsquelle. Beides fehlte 85 Versionen lang.

>>> LIZENZWAHL AGPL-3.0, vom User beauftragt ("so dass wir keine Probleme
    kriegen"), Begründung: Dalamud selbst ist AGPL-3.0, und goatcorps offizielles
    SamplePlugin - die Vorlage für jedes Dalamud-Plugin - ebenfalls, mit dem
    README-Hinweis, die Lizenz sei bereits passend gewählt. MIT (Vorschlag des
    PR-Autors, gestützt auf docs/distribution-guide.md) stünde im
    Spannungsverhältnis zu Dalamuds AGPL; der Ratgeber im Repo ist ein
    generisches Setup-Template und kennt Dalamud nicht.

>>> GEBAUT:
    - LICENSE - AGPL-3.0-Volltext, per Invoke-WebRequest von
      gnu.org/licenses/agpl-3.0.txt geholt (34.523 Bytes, 661 Zeilen). NICHT aus
      dem Gedächtnis getippt - ein verfälschter Lizenztext wäre wertlos.
    - THIRD-PARTY-NOTICES.md - Tolk, NVDA Controller Client, NAudio (MIT-Volltext),
      Newtonsoft.Json + .NET-Runtime für den Installer, dazu die nur referenzierten
      Dalamud-Assemblies und ein Square-Enix-Markenhinweis.
    - FF14Accessibility.csproj: beide Dateien als <Content> mit <Link>, damit sie
      flach im Output und damit IM ZIP landen. Im Repo allein zu liegen genügt
      nicht - Nutzer bekommen nur das Archiv.
    - README.md + README.en.md: Abschnitt "Lizenz" / "Licence".

>>> VERIFIZIERT, nicht angenommen: Release-Build (0 Warnungen), danach das frische
    latest.zip aufgelistet - LICENSE (34.523) und THIRD-PARTY-NOTICES.md (5.734)
    sind drin.

>>> OFFEN, BRAUCHT EINE ENTSCHEIDUNG DES USERS: Die sieben bereits gemergten PRs
    von bladestorm360 und blindndangerous kamen in ein Repo OHNE Lizenz.
    GitHubs ToS D.6 ("inbound=outbound") greift wörtlich nur "whenever you add
    Content to a repository containing notice of a license" - ohne LICENSE also
    gerade nicht. Ab jetzt greift sie automatisch. Für die Altbeiträge ist die
    übliche Praxis, die beiden kurz zu informieren und zustimmen zu lassen.
    NACH FREIGABE DES USERS ABGESCHICKT: bilinguale Kommentare unter PR #7
    (blindndangerous, beantwortet zugleich seine MIT-Frage) und unter PR #6
    (bladestorm360, für seine fünf gemergten PRs). Beide um eine kurze
    Bestätigung gebeten. ANTWORTEN STEHEN NOCH AUS.
    OFFENE ZUSAGE: im Kommentar unter PR #7 steht, dass zum INHALT des PRs noch
    eine Rückmeldung kommt (die Review von 2026-08-17: CLAUDE.de.md verweist an
    vier Stellen weiter auf docs/game-api.md, und die Frage, welche der beiden
    game-api-Fassungen künftig die führende ist). Noch nicht geschrieben.

>>> EBENFALLS OFFEN: Beim nächsten Release LICENSE und THIRD-PARTY-NOTICES.md
    zusätzlich als eigene Release-Assets anhängen. Wer nur die Installer-EXE zieht
    (self-contained, enthält Newtonsoft + .NET-Runtime), sieht sonst keine
    Hinweise, bevor er installiert hat.

## FRUEHERER STAND (2026-08-16, RELEASE v5.85 - "DREI FENSTER OHNE WORTE")

>>> VEROEFFENTLICHT. Alles unten aus diesem Tag ist damit oeffentlich:
    - Tauschfenster: Preis samt Waehrung, Beschreibung, eigener Bestand.
    - Vermoegen: jede Zeile nennt ihre Waehrung ("49.457 Gil").
    - Errungenschaften: Punkte + Zertifikate beim Oeffnen und am Symbol.
    - Chat-Kanaele als flache Liste im Optionsmenue (Umschalt+F9), in BEIDEN
      Chatsystemen.
    - TooltipService liest jetzt SeString statt roher Bytes - das repariert
      symbolgetriebene Beschriftungen im ganzen Plugin, nicht nur hier.

>>> VOM USER IN-GAME BESTAETIGT (16.8. nachmittags): Vermoegen-Zeilen,
    Errungenschaftspunkte, und nach dem Zeiger-Fix auch die Errungenschafts-
    zeilen wieder ("ja passt alles").

>>> NICHT GEGENGEPRUEFT im Release: die Chat-Kanal-Schalter von heute Vormittag
    (Punkte 6 bis 9 im Block darunter) - gebaut und deployt, aber vom User nicht
    ausdruecklich zurueckgemeldet.

## FRUEHERER STAND (2026-08-16, "CHAT-KANAELE INS OPTIONSMENUE" - GEBAUT, ZU TESTEN)

>>> ANLASS: Der User wollte das Projekt im audiogames.net-Forum vorstellen und
    fragte beim Korrekturlesen des Beitrags nach, WIE man Chat-Kanaele einzeln
    stummschaltet - beide READMEs behaupten das seit jeher. Antwort nach
    Codesuche: gar nicht. Die sieben Schalter (Configuration.cs:250-256) wurden
    NUR in LegacyChatReaderService.ShouldRead gelesen, kein Menue, kein /acc-
    Befehl, kein Fenster fasste sie an. Der einzige Weg war pluginConfigs/
    FF14Accessibility.json von Hand - genau das, was der Klassenkommentar von
    OptionsMenu ausschliesst ("Asking a blind player to edit ... is not an
    option"). Das NEUE Chatsystem (PR #5) hatte seine Bedienung unter
    "Chat-Register", das GEWOHNTE - die Voreinstellung - hatte gar keine.

>>> ZWEITER FUND BEIM BAUEN, und er hat das Verhalten geaendert: ShouldRead stand
    VOR dem Archivieren (Zeile 74, Archiv erst 105). Ein abgeschalteter Kanal war
    damit nicht nur still, sondern auch aus der Nachlese verschwunden. Im neuen
    System gilt ausdruecklich das Gegenteil ("A switch here NEVER touches the
    buffer", OptionsMenu.cs:128). Aufgefallen ist es nie, weil die Schalter
    unerreichbar und deshalb durchweg an waren; mit einem Menue davor waere es
    eine Falle geworden. DEM USER VORGELEGT, ER HAT ENTSCHIEDEN: nur stumm, die
    Nachlese bleibt.

>>> GEBAUT:
    - LegacyChatReaderService: ShouldRead in IsKnownChannel (entscheidet ueber
      das Archivieren, ohne Schalter) und ShouldSpeak (die Schalter, abgefragt
      hinter dem Archiv und hinter _isActive) getrennt. Die [ChatAlt]-Sonde zeigt
      jetzt "bekannt=" und "gesprochen=" statt eines einzigen "gelesen=".
    - OptionsMenu: BuildChatChannels mit ZEHN Zeilen. Die oberste Ebene zeigt
      "Chat-Kanaele" ODER "Chat-Register", je nach aktivem System - ein Abschnitt,
      der im anderen System nichts tut, wird gar nicht erst angeboten. Dafuer hat
      die oberste Ebene jetzt Rebuild=Build, sonst zeigte sie nach dem Umschalten
      noch auf den Abschnitt des alten Systems.
    - Die Kanalnamen kommen aus AccessibilityStrings.LegacyChatCategoryName, also
      woertlich aus der Nachlese: wer etwas stummschaltet, findet es dort unter
      demselben Wort wieder. Reihenfolge = LegacyChatHistoryService.Order.
      Ausnahme "Sammeln" (eigener Schalter, wird unter System archiviert).
    - Beim ABSCHALTEN sagt die Bestaetigung dazu "Steht weiter zum Nachlesen
      bereit." - nur dann, nicht beim Einschalten und nicht in der Beschriftung.
    - AnnounceLoot ist mit hineingenommen: geprueft, es wirkt ausschliesslich in
      ShouldSpeak (LootNotice) und nirgends sonst.

>>> BEWUSST OHNE ZEILE: ErrorMessage und Echo stehen im Leser fest auf true. Eine
    Menuezeile ohne Schalter dahinter waere eine Einstellung, die nichts einstellt.

>>> ZU TESTEN (Debug gebaut + deployt, 0 Warnungen):
    1. Umschalt+F9: heisst der Chat-Abschnitt "Chat-Kanaele"? (Voreinstellung ist
       das gewohnte System.)
    2. Einen Kanal abschalten - kommt "... aus. Steht weiter zum Nachlesen bereit."?
    3. Gegenprobe: kommen in diesem Kanal wirklich keine Ansagen mehr?
    4. WICHTIGSTE PROBE: Alt+Bild-auf/-ab zu diesem Kanal - stehen die neuen
       Nachrichten trotzdem drin?
    5. Chatsystem umschalten (Zeile darunter, Menue bleibt offen): wird die Zeile
       darueber sofort zu "Chat-Register"?

>>> READMES + FORUMSBEITRAG NACHGEZOGEN: beide READMEs nennen jetzt Umschalt+F9,
    die vollstaendige Kanalliste und den Satz, dass Abschalten die Nachlese nicht
    anfasst. forum-post-audiogames.txt (neu, im Projektwurzelverzeichnis) ist der
    Vorstellungstext fuer audiogames.net, vom User noch nicht freigegeben.

>>> NACHTRAG, UND ES WAR EIN EIGENER FEHLER: die erste Fassung zeigte "Chat-
    Kanaele" NUR im gewohnten System, im neuen weiterhin "Chat-Register". Der User
    laeuft aber auf dem NEUEN (Config sagt UseLegacyChatSystem=False, das Log zeigt
    [Chat] statt [ChatAlt]) - er bekam also genau die Funktion nicht zu sehen,
    nach der er gefragt hatte, und meldete "hat sich nichts geaendert". Die
    Diagnose kam komplett aus Log + Config, ohne dass er etwas nachspielen musste:
    Spielstart 11:24:03 lag NACH dem Build 11:22:11 (neue DLL war also geladen),
    "[Menue] 'Chat-Register' mit 4 Eintraegen" um 11:25:58, danach sechsmal
    Chatsystem hin und her.

>>> DARAUF GEBAUT (auf Ansage des Users): BuildGameChatChannels - dieselbe flache
    Liste fuer das NEUE System, aus den Kanaelen des Spiels quer ueber alle
    Register, sortiert nach GameChatChannel.Sort (Reihenfolge des spieleigenen
    Einstellungsfensters). "Chat-Kanaele" steht jetzt in BEIDEN Systemen an
    derselben Stelle; "Chat-Register" kommt im neuen zusaetzlich darunter fuer die
    feine Ebene (Filterzeilen, ausgeteilt vs. erlitten).
    - EINE Zeile schaltet den Kanal in ALLEN Registern, die ihn zeigen, und meldet
      "an", sobald EIN Register ihn spricht. Das ist dieselbe Oder-Regel, mit der
      ChatReaderService entscheidet (Zeile 315-322, ueber die Routen), nur von der
      anderen Seite gelesen.
    - IsChannelAudible prueft bis auf die FILTERZEILEN hinunter, weil ein
      gespeicherter Zeilen-Schalter seinen Kanal aussticht (RowIsOn).
    - Neu in ChatTabSpeech: ClearRows. Ohne das wuerde die flache Zeile luegen -
      nach "aus" spraeche eine einzeln eingeschaltete Zeile weiter, nach "an"
      bliebe eine einzeln abgeschaltete stumm. MUSS vor SetChannel laufen: in den
      Kategorien 1 und 2 ist die Row-Id dieselbe Zahl wie der Kanal-Key.
    - Build() ist von Objekt-Initialisierer auf Methode umgebaut, weil eine
      bedingte Zeile sonst als null in der Liste gelandet waere.

>>> ZUSAETZLICH ZU TESTEN (Debug gebaut, 0 Warnungen - das Spiel lief seit 11:24
    und muss fuer diese Fassung NEU GESTARTET werden):
    6. Umschalt+F9 im neuen System: stehen jetzt FUENF Zeilen da (Toene, Ansagen,
       Chat-Kanaele, Chat-Register, Chatsystem)?
    7. "Chat-Kanaele" oeffnen: kommt eine flache Liste mit den Kanalnamen des
       Spiels?
    8. Freie Gesellschaft abschalten, dann eine FC-Nachricht abwarten: still?
       Und steht sie trotzdem in der Nachlese?
    9. Gegenprobe auf die Anzeige: Zeile wieder an, Menue schliessen und neu
       oeffnen - meldet sie weiterhin "an"?

## STAND JETZT (2026-08-16, "DREI STUMME FENSTER" - 1 VON 3 GEBAUT, 2 BRAUCHEN NOCH EINE RUNDE)

>>> ERSTE MESSRUNDE IST DA ([UiProbe], 12:23:30 bis 12:24:26). Sie hat EINEN der
    drei Punkte fertig aufgeklaert, bei den anderen beiden lag die Sonde selbst
    zu flach - beides unten korrigiert.

>>> AUFGEKLAERT UND GEBAUT - DAS WELTFELD DER SPIELERSUCHE. Der User: "man kann
    wohl auch die welten aussuchen wo man sucht aber das konnte ich nicht
    auslesen". Der Befund ist ein anderer als die Meldung:
      [UiProbe] PcSearchDetail: Fokus id=5 typ=8 in Comp id=27 typ=CheckBox
                -> Text2='Welt'
    Es ist KEINE Auswahlliste, sondern ein ANKREUZFELD. Gesprochen wurde nur die
    Beschriftung "Welt", nie der Zustand - angekreuzt und nicht angekreuzt
    klangen gleich, und genau deshalb wirkte das Feld tot.
    NICHT ZU VERWECHSELN mit dem SUCHBEREICH: der liegt in einem eigenen Fenster
    (`PcSearchSelectLocation`) und geht laengst - La Noscea, Limsa Lominsa,
    Norvrandt, Eulmore, Crystarium, Garlemald, Thavnair, Radz-at-Han kamen alle
    sauber (Log 11:45:23 bis 11:45:30).
    GEBAUT: `TryReadPlayerSearchFocus` (UIReaderService, vor dem allgemeinen
    Pfad). Ankreuzfeld -> "Welt, Schalter, an/aus" (+ "ausgegraut", wenn
    NodeFlags.Enabled fehlt), Zahlenfeld -> Wert statt Stille. Wortgleich zu den
    Konfigurationsfenstern, damit derselbe Bedienelementtyp ueberall gleich
    klingt.
    EIGENE METHODE, KEINE ERWEITERUNG von TryReadConfigFocusRow: jene ist mit
    Absicht auf Config* beschraenkt, weil dort ein Aufklappfeld selbst eine
    CheckBox-Komponente ist und als "Schalter, aus" angesagt wuerde. Diese
    Einschraenkung ist teuer erkauft und bleibt.
    ZAHLENFELDER: die Stufengrenzen waren ganz stumm ("[Focus] STUMM id=5 typ=3"
    bei gleichzeitig vorhandenem "Text5='1'"). Angesagt wird nur der WERT -
    welche Grenze es ist, sagt kein Text der Komponente, und ein geratenes
    "Mindeststufe" waere schlimmer als keines. Offen, braucht eine eigene Messung.

>>> ERLEDIGT AM 16.8. NACHMITTAGS - VERMOEGEN IST GEBAUT. Die zweite Messrunde
    (Strg+F5-Dump 15:04:36 + [UiProbe] 15:03:59 bis 15:04:34) hat die offene
    Frage von unten beantwortet: DER TOOLTIP TRAEGT DEN NAMEN.
      Comp id=20     -> Tooltip='...Gil...'
      Comp id=200403 -> Tooltip='...Legionstaler...'
    Er kam nur nie sauber an: `TooltipService.OnAttach` las
    `args->TextArgs.Text` mit `ptr.ToString()`, also ROH - bei einem
    Gegenstandsverweis stehen dann die SeString-Steuerbytes mit drin
    ("H?%I?&GilIH"). Einfache Beschriftungen ueberlebten das
    ("Waehrungseinstellungen"), Waehrungen nicht.
    GEBAUT 1 - `TooltipService.ReadTooltipText`: liest ueber Dalamuds
    SeString-Leser + TolkService.Sanitize, mit AtkText.IsReadable davor (der
    Lauf geht bis zur Null, eine nicht gemappte Seite wuerde sonst
    ueberlaufen). Wirkt fuer ALLE symbolgetriebenen Fenster, nicht nur hier -
    alle fuenf Aufrufer sprechen den Text nur aus, keiner zerlegt ihn.
    GEBAUT 2 - `TryReadCurrencyFocusRow` (UIReaderService, direkt nach der
    Spielersuche in der Kette): Name aus dem Tooltip, Stand aus den SICHTBAREN
    Textkindern. Drei Dinge, die der generische Leser falsch machte:
    - "Woche"/"Gesamt" laufen nicht mehr mit, wo das Spiel sie ausblendet
      (Dump: Kinder id=4/id=3 der Zeile id=20 haben F=0x2023, also kein
      Sichtbar-Bit). `GetTextFromNodeTree` prueft das Bit nicht.
    - Der Stand "6" (Wertmarken) waere ganz verschwunden: der generische Leser
      wirft Texte mit einem einzigen Zeichen weg (t.Length > 1).
    - Reihenfolge wie vom User festgelegt: "49.457 Gil". Der Stand wird als der
      Teil MIT ZIFFER herausgegriffen, damit ein sichtbar gebliebenes
      Spaltenwort nicht an die Stelle der Zahl rutscht.
    OHNE TOOLTIP steigt die Regel aus und ueberlaesst die Zeile dem bisherigen
    Weg - lieber die nackte Zahl als eine geratene Waehrung. Die
    Gruppen-Ueberschriften des Fensters (Comp(1014): "Gil", "Staatstaler",
    "Wertmarken", "Manderville Gold Saucer-Punkte") taugen als Ersatz NICHT:
    unter "Staatstaler" steht der "Legionstaler", die Ueberschrift benennt die
    Gruppe, nicht die Zeile.
    KATEGORIE-REITER: die neun Comp(1011)-RadioButtons (ids 6 bis 14, davon
    fuenf sichtbar) sind reine Symbole. Sie bekommen denselben Weg (Tooltip +
    "ausgewaehlt" per IsChecked) - OB an ihnen ueberhaupt ein Tooltip haengt,
    ist NICHT gemessen. Haengt keiner dran, bleibt es beim bisherigen
    Verhalten. Der WECHSEL der Kategorie wird schon angesagt: ScanAddonTexts
    spricht den geaenderten Text id=4 ("Allgemein", Log 15:04:01).
    Der Ersatzweg ueber die Sheets (unten) wird damit nicht gebraucht.
    IN-GAME BESTAETIGT (Log 15:18:45 bis 15:19:11, noch mit der Zwischenfassung
    "Name vorn"): "Gil: 49.457", "Legionstaler: 1.652/10.000", "Wertmarke: 6",
    "Wolfsmarke: 0/20.000", "Waehrungseinstellungen". Damit sind alle drei
    Fehler des generischen Lesers belegt behoben - die Waehrung ist benannt,
    "Woche/Gesamt" ist weg, und die einstellige "6" faellt nicht mehr raus.
    NOCH NICHT GETESTET ist die REIHENFOLGE: seit dem Build 15:45 steht die Zahl
    vorn ("49.457 Gil"), wie vom User festgelegt. Gehoert hat er bisher nur die
    Fassung mit dem Namen vorn.

>>> ALTER STAND DAZU (vor der zweiten Messrunde) - VERMOEGEN: die Sonde fand in KEINER Zeile ein Symbol.
      Comp id=20     typ=Base -> Text5='49.457'      | Text4='Woche' | Text3='Gesamt'
      Comp id=200403 typ=Base -> Text5='1.652/10.000'| Text4='Woche' | Text3='Gesamt'
      Comp id=200404 typ=Base -> Text5='6'           | ...
      Comp id=200405 typ=Base -> Text5='499'         | ...
    Damit ist der geplante Weg (Icon-Id -> ResolveIconItem) WIDERLEGT: es gibt
    keine Icon-KOMPONENTE in der Zeile. Entweder ist das Symbol ein reiner
    Bildknoten (dann fuehrt der Weg ueber den Texturpfad, wofuer es im Projekt
    bisher keinen Code gibt), oder der Name kommt aus dem Tooltip. Die Sonde
    fragt jetzt den Tooltip mit ab - denselben Weg geht das Plugin bei den
    symbolgetriebenen Konfigurations-Reitern schon.
    NEBENBEFUND: "Woche" und "Gesamt" stehen in JEDER Zeile - sie gehoeren zur
    Zeile, sind also keine einmaligen Spaltenueberschriften.
    FORM VOM USER FESTGELEGT: der Name kommt HINTER die Zahl ("49.457 Gil"),
    nicht davor.
    ERSATZWEG, falls der Tooltip nichts liefert: das Spiel fuehrt eigene
    Verzeichnisse - die Sheets `Currency`, `Tomestones` und `TomestonesItem`
    sind als Lumina-Typen vorhanden (geprueft in Lumina.Excel.dll). Damit waere
    der Name aus den Spieldaten zu holen. NICHT gebaut: die Zuordnung Zeile ->
    Sheet-Eintrag ist ungemessen, und eine geratene Reihenfolge haengt die
    falsche Waehrung an die richtige Zahl.

>>> ERRUNGENSCHAFTSPUNKTE - GEMESSEN UND GEBAUT (Frage des Users 16.8.: "wie
    bzw wo sehe ich meine errungenschaftspunkte").
    DIE MESSUNG IST DA, Log 15:42:08 - beide Symbole tragen einen Tooltip:
      id=23 / id=8  = "350" -> "Errungenschaftspunkte"
      id=26 / id=11 = "1"   -> "Errungenschaftszertifikat"
    Damit ist geklaert, was der Dump offenliess: die groessere Zahl sind die
    Punkte. Nebenbefund zur ZEIT: die erste Messung derselben Sitzung (15:29:06)
    fand KEINEN Tooltip - das Fenster war vor dem Hot-Reload aufgebaut, die
    Attach-Aufrufe liefen also am neuen Hook vorbei. Und eine Zehntelsekunde vor
    der guten Messung (15:42:08.185) standen die Zahlenfelder noch leer.
    GEBAUT: `AnnounceAchievementHeader` - sagt beim Oeffnen des Fensters "350
    Errungenschaftspunkte, 1 Errungenschaftszertifikat". Wartet, bis Zahl UND
    Tooltip dastehen (bis 8 s), und faellt danach ersatzlos aus, statt eine Zahl
    ohne ihr Wort zu sprechen. Beide Werte stehen doppelt im Fenster (Listen-
    und Empfehlungs-Seite), JoinDistinctParts wirft die Wiederholung raus.
    Gesprochen mit Speak statt SpeakInterrupt, damit Fenstertitel und
    Listenansage nicht abgeschnitten werden.
    Die Wortwahl kommt komplett vom Spiel (Tooltip in Client-Sprache), die Mod
    steuert nur die Reihenfolge bei: `AccessibilityStrings.CurrencyRow` heisst
    jetzt `AmountWithLabel` und wird von Vermoegen UND Errungenschaften genutzt.
    Die Sonde `ProbeAchievementHeader` ist damit erledigt und wieder raus;
    `FindTooltipInSubtree` bleibt (sucht den Tooltip an der Komponente, ihren
    Kindern und am Bildknoten daneben - nach UNTEN, anders als TryGetTooltipDeep).
    IN-GAME GELAUFEN (Log 16:52:56.841): "[Achievement] Kopf: 350
    Errungenschaftspunkte, 1 Errungenschaftszertifikat" ging raus - ABER 15 ms
    spaeter kam "[Speak] INT 'Legacy, Vergütung'" (erste Fokusmeldung, mit
    Unterbrechung). Der Spieler hat die Ansage also mit hoher Wahrscheinlichkeit
    NIE GEHOERT. Zwei Nachbesserungen daraus:
    - AchievementHeaderDelayS = 1,5 s: die Kopf-Ansage wartet, bis Titel, erste
      Fokusmeldung und die "Keine Eintraege"-Meldung (1,0 s) durch sind.
    - `TryReadAchievementHeaderFocus`: das Punkte-Symbol IST mit der Tastatur
      erreichbar (Log 16:52:58.829, Fokus id=3 in Comp id=24). Seit dem
      Tooltip-Fix sagte es "Errungenschaftspunkte" - das Wort ohne den Wert.
      Jetzt kommt "350 Errungenschaftspunkte". Erkannt wird an der
      Eltern-Komponente (Knoten-Id, sprachunabhaengig), nicht am Tooltip-Wort.
    REGRESSION AUS GENAU DIESER NACHBESSERUNG, vom User sofort gemeldet ("er
    liest mir die punkte vor aber jetzt nicht mehr was ich freigeschalten
    habe") und im Log bestaetigt (16:58:18 bis 16:58:20: jede Zeile meldete
    "350 Errungenschaftspunkte"): der Fokusknoten der LISTENZEILEN hat die Id
    25 - dieselbe wie das Bild neben der Punktzahl. Knoten-Ids sind eben nur
    innerhalb ihres Containers eindeutig, und mein Vergleich lief ueber die Id.
    GEFIXT: `FindTopLevelNode` sucht auf der FENSTEREBENE
    (addon->UldManager.NodeList) und liefert einen ZEIGER; `IsFocusInside`
    vergleicht Zeiger statt Ids. Auch die Oeffnungs-Ansage nutzt jetzt diesen
    Weg statt addon->GetNodeById - die Funktion ist nativ, ob sie in
    Komponenten absteigt, ist nicht nachpruefbar.
    ZU TESTEN: Errungenschaften oeffnen - (1) kommt die Zeile mit Punkten und
    Zertifikat vollstaendig durch, (2) sagen die Zeilen wieder die
    Errungenschaften, (3) sagt das Symbol beim Anfahren die Zahl mit?

>>> AUSGANGSBEFUND DAZU - Dump 15:22:58 (Addon `Achievement`) und
    Log 15:22:40 bis 15:22:58:
    - Das Fenster zeigt im Kopf ZWEI Zahlen, jede neben einem Symbol und ohne
      ein Wort dazu: Text id=26 = "1" und Text id=23 = "350". Dieselben zwei
      Werte noch einmal als id=11 und id=8 - je einmal fuer die Listen- und die
      Empfehlungs-Seite. WELCHE davon die Punkte sind, sagt der Dump nicht.
    - Warum der Spieler sie nicht hoert: sie stehen in keiner Liste, sie
      aendern sich beim Blaettern nicht, und ScanAddonTexts spricht nackte
      Zahlen grundsaetzlich nicht ("BARE NUMBERS ARE NEVER SPOKEN HERE") -
      sonst wuerde jeder Zaehler im Sekundentakt dazwischenreden.
    - Die ZEILEN gehen dagegen schon, inklusive Punktwert am Ende: "Vergütung,
      10 verschiedene Dungeons oder Prüfungen erfolgreich abgeschlossen., Auf
      in die Dungeons II, 10" (15:22:44).
    - KEINE API-QUELLE (ilspycmd auf FFXIVClientStructs, 16.8.): der Struct
      `Achievement` fuehrt nur die Bitmap der abgeschlossenen Errungenschaften,
      fuenf Verlaufseintraege und den Fortschritt einer einzelnen - keine
      Punktsumme. `AgentAchievement` hat kein Punktefeld, ein
      `AddonAchievement` existiert gar nicht. Der Fensterknoten ist also die
      einzige Quelle.
    Die Sonde `[AchProbe]` hat das aufgeklaert (siehe Block darueber). Der
    Ersatzweg - Punktsumme aus dem Achievement-Sheet ueber die
    CompletedAchievements-Bitmap - wird damit nicht gebraucht; er waere
    Nachrechnen statt Ablesen gewesen.

>>> NOCH OFFEN 2 - SUCHERGEBNISSE: "[UiProbe] SocialList: Fokus id=7 typ=8 in
    Comp id=14 typ=List -> NICHTS LESBAR". Das lag an der SONDE, nicht an der
    Liste: sie sah nur die direkten Kinder, und in einer Liste ist jede Zeile
    eine eigene Komponente (ListItemRenderer) mit den Namen eine Ebene tiefer.
    Ausserdem hat der User in dieser Runde gar keine Suche mit Treffern
    gestartet (Log: Felder angefahren, dann Fenster zu).
    NACHGEBESSERT: `CollectProbeParts` sammelt jetzt zwei Ebenen tief (gedeckelt
    bei 40 Eintraegen gegen Log-Flut) und meldet bei Listen zusaetzlich Laenge
    und Auswahl.

>>> ZU TESTEN / ZU MESSEN (Debug gebaut, 0 Warnungen, Spiel neu starten):
    1. Spielersuche, Feld "Welt" anfahren: kommt "Welt, Schalter, an" bzw. "aus"?
       Und aendert sich die Ansage, wenn man es umschaltet?
    2. Stufenfelder anfahren: kommt jetzt eine Zahl statt Stille?
    3. ERLEDIGT - der Tooltip stand im Log, siehe oben.
    4. Suche mit ECHTEN Treffern starten und durch die Ergebnisse blaettern.
       Das ist die Messung, die weiterhin komplett fehlt.
    5. NEU (Vermoegen, Debug gebaut 16.8. nachmittags, 0 Warnungen, Spiel neu
       starten): ueber die Zeilen blaettern - kommt "49.457 Gil" und
       "1.652/10.000 Legionstaler" statt "49.457, Woche, Gesamt"?
    6. Die Zeile, die im Dump auf "6" steht (Wertmarken): kommt sie jetzt
       ueberhaupt mit einer Zahl? Vorher fiel sie ganz weg.
    7. Kategorie wechseln und dort blaettern - werden auch die Waehrungen der
       anderen Reiter benannt (im Dump z.B. die Wolfsmarke)?
    8. Die Kategorie-Reiter selbst anfahren: sagen sie einen Namen? Wenn nicht,
       haengt an ihnen kein Tooltip - dann bitte melden, das braucht einen
       eigenen Weg.
    9. Nebenwirkung des Tooltip-Fixes gegenpruefen: Konfigurations-Reiter und
       Reittier-Fenster klingen weiterhin richtig?

## FRUEHERER STAND (2026-08-16, "DREI STUMME FENSTER" - SONDE GEBAUT, MESSUNG STAND AUS)

>>> MELDUNG DES USERS: "mir ist noch was aufgefallen was nicht richtig
    barrierefrei ist schau in die log und dump datei". Der Dump (11:48:36) war
    SocialList, das Log lief bis 11:49. Auf Nachfrage: ALLE DREI Fundstellen
    sollen bearbeitet werden.

>>> BEFUND 1 - VERMOEGEN (Addon `Currency`), Log 11:49:04 bis 11:49:12:
    Gesprochen wurde "49.457, Woche, Gesamt", "1.652/10.000, Woche, Gesamt",
    "Woche, Gesamt" (ganz ohne Zahl) und "499, Woche, Gesamt". Es fehlt die
    WAEHRUNG - der Spieler hoert Zahlen ohne zu wissen, wovon. Ein Sehender
    erkennt sie am Symbol. "Woche, Gesamt" sind Spaltenueberschriften, die jedes
    Mal mitlaufen.
    AddonCurrency EXISTIERT in FFXIVClientStructs, hilft aber kaum: das einzige
    eigene Feld ist `_tabs` (FixedSizeArray5<AtkComponentRadioButton>), also die
    fuenf Reiter. Die Zeilen selbst sind nicht benannt.
    WEG, DER TRAGEN SOLLTE: Icon-Id am Zeilen-Symbol -> InventoryService.
    ResolveIconItem (Item/EventItem-Rueckwaertssuche, dieselbe Aufloesung wie im
    Inventar und beim Reittier-Fenster). NICHT gebaut, weil die Icon-Ids dieses
    Fensters nicht gemessen sind.

>>> BEFUND 2 - SUCHERGEBNISSE DER SOZIALLISTE (`SocialList`), Log 11:45:11 und
    11:45:12: zweimal "[Focus] STUMM addon='SocialList' id=7", typ=8
    (Kollisionsknoten) in einer Listen-Komponente (Comp 1013).
    EINSCHRAENKUNG, DIE STEHENBLEIBEN MUSS: im Dump war die Liste LEER
    (ListLen=0, "Freunde in dieser Gruppe: 0", Nachbarn '--'). Ob die Zeilen mit
    echten Treffern Text tragen, ist damit NICHT gemessen. Ohne diese Messung
    waere jeder Handler geraten.

>>> BEFUND 3 - SPIELERSUCHE (`PcSearchDetail`), Log 11:45:15 bis 11:45:18:
    - id=5 zweimal STUMM; das Event-Ziel des Knotens verraet den Inhalt ("Kein
      Set ausgewaehlt.").
    - id=9 beim ersten Betreten STUMM, liest erst nach Eingabe ("100").
    - Beim Oeffnen eine Sammelansage aus reinen Beschriftungen: "Sprache. SG.
      Status. Ort. Max.. Min.. Stufe. Klasse/Job. Name" - Feldnamen ohne Werte.
    - Das Namensfeld liest die Zeichenzahl mit ("10/15, Gordankane"), beim
      Loeschen also "9/15, Gordankan", "8/15, Gordanka" ...
    KEIN AddonPcSearchDetail und KEIN AddonSocialList in FFXIVClientStructs
    (gepruefte Typnamen in der DLL) - hier gibt es nichts als die Knoten.

>>> GEBAUT: EINE Sonde fuer alle drei, `ProbeFocusContext` in UIReaderService
    (#if DEBUG, Log-Praefix `[UiProbe]`). Sie haengt im Fokus-Pfad und
    protokolliert je Fokuswechsel den ELTERN-Container mit allen Kindern: Texte
    mit Knoten-Id UND Icon-Ids, letztere gleich ueber ResolveIconItem aufgeloest.
    Der Strg+F5-Dump kann das nicht - er zeigt keine Icon-Ids, und genau daran
    haengt Befund 1. Eigener Zeigervergleich (_lastProbedNodePtr) statt
    _lastFocusedNodePtr, weil der erst weiter unten gesetzt wird.

>>> BEFUND 3b - DIE WELTAUSWAHL, vom User nachgereicht ("man kann wohl auch die
    welten aussuchen wo man sucht aber das konnte ich nicht auslesen"). Das Log
    trennt hier zwei Dinge, die leicht verwechselt werden:
    - Der SUCHBEREICH geht bereits: eigenes Addon `PcSearchSelectLocation`
      ("SUCHBEREICH ANPASSEN"), und dort kommt alles sauber an - La Noscea,
      Limsa Lominsa, Norvrandt, Eulmore, Crystarium, Garlemald, Thavnair,
      Radz-at-Han (Log 11:45:23 bis 11:45:30). Das ist der ORT, nicht die Welt.
    - Das WELTFELD ist halb stumm: 11:45:19 "[Focus] id=5 Text='Welt'" - nur die
      Beschriftung, NICHT der eingestellte Wert. Beim Aktivieren (11:45:20,
      ButtonClick param=1200 auf Knoten 27) folgt im Log KEIN neues Addon; der
      Fokus springt einfach weiter. Beim Suchbereich erscheint an derselben
      Stelle sekundenschnell "Addon: PcSearchSelectLocation".
    OFFEN, AUS DEM LOG NICHT ZU ENTSCHEIDEN: ob die Weltliste ein Aufklappfeld
    INNERHALB von PcSearchDetail ist (Liste als Kindknoten, vom Leser nicht
    gefunden) oder ob der Klick gar nichts oeffnet. Genau dafuer die Sonde.

>>> WAS DER USER MESSEN MUSS (Spiel neu starten, dann durch die Fenster):
    1. Vermoegen oeffnen, ueber MEHRERE Waehrungen blaettern.
    2. Spielersuche oeffnen, jedes Feld einmal anfahren - und das WELTFELD
       zusaetzlich aktivieren, damit im Log steht, was dabei aufgeht.
    3. Suche mit ECHTEN Treffern starten und durch die Ergebnisliste blaettern -
       das ist die Messung, die noch komplett fehlt.
    Danach reicht das Log; die Dump-Datei wird nicht gebraucht (die Dump-Zeilen
    stehen ohnehin auch im Log, und das wird nicht ueberschrieben).

>>> NICHTS DAVON IST GEBAUT. Bewusst: ohne die Messung waere jede Ansage geraten,
    und bei einer Waehrung heisst geraten "falsche Zahl zur falschen Marke".

## FRUEHER (2026-08-16, "TAUSCHFENSTER: PREIS UND BESCHREIBUNG" - GEBAUT, ZU TESTEN)

>>> ANLASS: Der Errungenschafts-NPC. Fenster ist `ShopExchangeCurrency`. Der
    Spieler hoerte dort NUR den Namen ("Schwarzes Chocobo-Kueken", Log 00:30),
    brauchte aber beides: was es an Marken kostet und was das Ding ueberhaupt
    ist. Bei Ausruestung kamen Werte, bei allem anderen nichts - Ursache:
    `AppendShopGearInfo` gleicht gegen `BuildGearNameCache` ab, und der enthaelt
    NUR Ausruestung (jede Zeile ohne EquipSlotCategory faellt raus).

>>> ERSTER ANSATZ WIDERLEGT - hier festgehalten, damit ihn niemand nochmal geht:
    Der Fokus-Text sieht im Log wie ein Item-Link aus ("H?%I?&ZeigerhaendchenIH"),
    also sollte die Item-Id aus dem SeString-Payload kommen. Die Sonde sagt fuer
    JEDEN Text JEDER Zeile `link=0` (00:30/00:31) - Dalamuds Parser findet dort
    keinen ItemPayload. `AtkText.LinkedItemId` wurde wieder ausgebaut.

>>> GEMESSEN STATT GERATEN (Sonde [ShopProbe], 2026-08-16):
    - Fokus sitzt auf einem Collision-Knoten, dessen ELTERN die Zeile ist:
      "[0] id=12 typ=8 [1] id=41005 typ=1019 komp=ListItemRenderer [2] id=20
      komp=TreeList".
    - In der Zeile tragen genau drei Texte etwas: id=3 der NAME, id=6 der PREIS,
      id=8 eine zweite Zahl (auf jeder gemessenen Zeile "0").
    - BEWEIS fuer id=6: um 00:30:10 fragte das Spiel selbst "Den folgenden
      Gegenstand gegen 2 Errungenschaftszertifikate tauschen?" - und id=6 stand
      auf "2".
    - id=8 bleibt UNGESPROCHEN: die Bedeutung ist nicht belegt (vermutlich der
      eigene Bestand, aber alle Messungen zeigten 0, also ohne Varianz).

>>> GEBAUT: `SpecialShopService` (neu) + `InventoryService.ResolveItemIdByName`
    (Namens-Cache ueber ALLE Items, nicht nur Ausruestung) + Zeilenlesen im
    Fokus-Pfad. Die Waehrung kommt aus dem Sheet `SpecialShop` ueber das PAAR
    (Ware, Kostenzahl) - die Shop-Id fuehrt das Spiel in keiner benannten
    Struktur (FFXIVClientStructs hat nur `AgentShop` fuer AgentId.Shop, den
    Gil-Laden). Mehrdeutige Paare liefern LEER, dann kommt nur die Zahl: eine
    erfundene Einheit ("2 Marken") waere schlimmer als keine.
    Plural aus den spieleigenen Sheet-Spalten Singular/Plural, nicht selbst gebeugt.

>>> OFFLINE GEGENGEPRUEFT (Lumina-Werkzeug im Scratchpad, siehe
    offline_sheet_dump_tool - der User musste dafuer NICHTS nachspielen):
    Namens-Cache 50174 eindeutige Namen (203 mehrdeutige verworfen), Preis-Index
    15016 Paare (306 mehrdeutig). Alle SIEBEN Waren aus dem Log loesen eindeutig
    auf und liefern "2 Errungenschaftszertifikate" plus eine Beschreibung -
    dieselbe Zahl, die die UI-Sonde gemessen hat, und dieselbe Formulierung wie
    im Bestaetigungsdialog. Drei unabhaengige Quellen, ein Ergebnis.

>>> IN-GAME BESTAETIGT (User 2026-08-16): Preis und Beschreibung kommen.

>>> NACHGELEGT AUF WUNSCH ("wieviele marken habe ich"): der eigene Bestand.
    Waehrung ist Item 21172 "Errungenschaftszertifikat" (offline bestimmt, Plural
    "Errungenschaftszertifikate", Kategorie 100). Gezaehlt wird mit der
    spieleigenen Funktion `InventoryManager.GetInventoryItemCount` - NICHT ueber
    eine Summe der Container: eine Waehrung liegt gar nicht in den Taschen, und
    eine nachgebaute Container-Liste driftet beim naechsten Patch.
    ANSAGE-REGEL: nicht an jeder Zeile (beim Blaettern aendert sich der Bestand
    nicht, das waere Geplapper) und nicht auf einer Taste (ein Sehender liest die
    Zahl dauerhaft im Fenster). Gesagt wird sie, sobald sie sich AENDERT - beim
    ersten Eintrag einer Waehrung und wieder nach einem Tausch.

>>> ZU TESTEN (Debug gebaut + deployt):
    1. Gegenprobe bei einem Ausruestungsteil: Preis UND Stufe/Werte, in dieser
       Reihenfolge?
    2. Erste Zeile im Tauschfenster: kommt ", du hast N" hinter dem Preis?
    3. Etwas eintauschen und weiterblaettern: kommt die NEUE Zahl von selbst?
    4. Gegenprobe auf Geplapper: beim Blaettern ohne Kauf darf sie NICHT wiederholt
       werden.
    Danach faellt [ShopProbe] raus.

>>> OFFEN, NICHT ANGEFASST: der Knoten id=7 liest die GANZE Liste als eine Zeile
    vor ("Goblin-Kappe, Chocomoppel-Maske, Bunte Haarschleife, Stufe 1, ...",
    Log 00:16:30). Gemeldet, auf Antwort des Users wartend.

## FRUEHER (2026-08-15 ABENDS, "V5.84 RELEASED - MIT DEN SECHS BEITRAEGEN")

>>> AUF ANSAGE DES USERS: alles gemerged, Release geschnitten, beide READMEs
    nachgezogen. test/prs ging als FAST-FORWARD nach main (main war vollstaendig
    darin enthalten, kein Merge-Commit, keine Konflikte). Tag v5.84 steht,
    4 Assets haengen dran, `releases/latest/download/latest.zip` liefert die
    neuen 939109 Bytes - ein Spieler zieht also wirklich die neue Fassung.
    Installer unveraendert (1.1.0.0), exe+installer.json vom v5.83-Release
    uebernommen, SHA gegengeprueft (5787445B...).

>>> VERSIONS-SYNC an allen drei Stellen: csproj 5.84.0/5.84.0.0, repo.json
    5.84.0.0, Plugin.cs PluginVersion "5.84" (der Testfassungs-Zusatz ist raus).
    Das gebaute Manifest FF14Accessibility.json zeigt 5.84.0.0.

>>> ZWEI FALSCHANGABEN IN DEN READMES GEFUNDEN UND KORRIGIERT - beide waren
    Beschreibungen von Code, den es nicht mehr gibt:
    1. "Endet der Weg kurz vor dem Ziel, werden die letzten Meter mitgefahren"
       - diese Umleitung ist in V5.78 zurueckgebaut worden (kein NearMiss-Code
       mehr in AutoWalkService). An ihrer Stelle stehen jetzt die Spuren.
    2. "wird beim Anmelden als 3 Tastenkonflikte gemeldet" - stimmt nicht mehr,
       Numpad5 (CAMERA_FOCUS) kam dazu. Jetzt ohne feste Zahl.
    Ausserdem fehlten SIEBEN aktive Tasten in der Uebersicht: Umschalt+L,
    Strg+F, Umschalt+F7/F8, Strg+Umschalt+F6, Umschalt+F9, Alt+Pos1/Ende,
    Umschalt+Pos1/Ende, Numpad5.

>>> ENTSCHEIDUNG DES USERS ZUR HP-ANSAGE, GEGEN DIE EIGENE FRUEHERE REGEL:
    PR 1 dreht `HpSentence`/`VitalStatus` von Prozent auf "HP 4523 von 5100"
    zurueck. Das widerspricht der Festlegung vom 2026-08-07 (nur Prozent), und
    genau dieses Format war in V5.31 schon einmal unbemerkt gekippt - deshalb
    wurde gefragt statt stillschweigend gemerged. Der User hat sich fuer die
    ZAHL entschieden (Argument des PR-Autors: das Spiel zeigt die eigenen HP
    selbst als Zahl, "87 Prozent" beantwortet nicht, ob ein Trank reicht).
    MP und Ziel-HP bleiben prozentual. Memory ist entsprechend umgeschrieben.

>>> WAS DAMIT ERSTMALS OEFFENTLICH IST, ALLES IN-GAME UNGEPRUEFT: PR 1 (Stufe+HP
    beim Blaettern), PR 2 (Form der Wirkflaeche), PR 3 (Verbuendete/Inhalte),
    PR 4 (Aussehen in der Charaktererstellung), PR 5 (Chat-Puffer an den
    Spielregistern), PR 6 (Tiefes Gewoelbe). Der Umschalter fuer das Chatsystem
    steht bewusst auf DEM GEWOHNTEN - wer nichts umstellt, hoert v5.83.
    Die Release-Notes sagen das offen, samt Rat, bei v5.83 zu bleiben.

>>> OFFENE PRUEFPUNKTE UNVERAENDERT (die Liste unten gilt weiter):
    Strg+F gegen die Spielfunktion FACE, MonitorHousingMesh (ob IsLoaded zu
    frueh true meldet), TryTakeTrail beim Festsitzen, [PlotProbe] fuer die
    Eingaenge, zweite Messung des Erholungsbonus, Ausruestungsset-Marke im
    Arsenal - und jetzt zusaetzlich die gesamte Mechanik der sechs Beitraege.

## FRUEHER (2026-08-15, "UNBEKANNTE OBJEKTE IM WOHNGEBIET" - GEBAUT, ZU TESTEN)

>>> ANLASS: Spielermeldung "in den wohngebieten gibt es unbekannte objekte".
    Log 2026-08-15 14:44 (terr 339 = Dorf des Nebels): bis zu acht Eintraege
    "Objekt ohne Namen" / "Objekt ohne Namen 2" AUF EINEM PUNKT (alle dx=13,84
    dz=-3,06, 14 m), daneben ganz normal "Informationstafel" und "Eingang".

>>> GEMESSEN mit /acc objprobe (15:58, dieselbe Stelle). WICHTIG FUER DAS
    NAECHSTE MAL: Strg+F5 taugt dafuer NICHT - der Menue-Dump gewinnt dort
    immer (Plugin.cs:536 sagt das auch so), der erste Testversuch ging deshalb
    ins Leere. Der Chat-Befehl ist der einzige verlaessliche Weg.
    Ergebnis: EventObj, DataId 2003757, zielbar=True, name=''.

>>> WAS ES IST (offline gegen die Sheets aufgeloest, Lumina):
    - U+E034 ist ein Private-Use-Glyph = Dalamud `SeIconChar.BotanistSprout`.
      Das Spiel beschriftet diese Objekte also mit einem SYMBOL statt mit einem
      Wort - ein sehender Spieler sieht ein Sprossen-Icon.
    - `EObj[2003757].Data` -> `CustomTalk 721047 "CmnDefHousingGardeningPlant_00151"`.
      Skript-Schluessel: PLANT_TITLE, FC_AUTHORITY_SEEDING, GARDENING_ERR_NO_SEED,
      ITEM_CATEGORY_SEED/SOIL/FERTILIZER, HOWTO_HARVEST -> GARTENBEETE.

>>> UMFANG GEMESSEN, NICHT GESCHAETZT: von 7571 namenlosen EObj-Zeilen haengen
    GENAU ZWEI an einem CustomTalk, beide mit der generischen MainOption
    "Plaudern". Ein allgemeiner Mechanismus haette also nichts geholfen; die
    Tabelle mit zwei Eintraegen ist das ganze Problem, keine Anzahlung.
    Der zweite Fall: EObj 2000032 -> CustomTalk 720898 "CmnDefMogLetter_00002"
    (LETTER_BOX_USAGE, HOWTO_MOGLETTER) = der Postkasten.

>>> DIE WOERTER SIND DIE DES SPIELS, nicht unsere Erfindung:
    - PLANT_TITLE zeigt auf `Addon 6420` = "Beet , Furche " (DE) bzw.
      " Bed,  Patch" (EN) - die Vorlage aus dem Aussaeen-Fenster.
    - Gegenprobe auf derselben Sheet-Zeile: EObjName 2003757 traegt das Glyph
      nur in DE und EN; die FRANZOESISCHE Fassung sagt "emplacement", die
      JAPANISCHE U+755D (Furche). Die Bedeutung ist also belegt, nicht geraten.
    - Postkasten: LogMessage 3902/3903 nennen ihn "Postkasten" / "mailbox".

>>> GEBAUT: `ObjectNameService.IconNamed` (2003757 -> "Beet"/"Garden bed",
    2000032 -> "Postkasten"/"Mailbox"), gezogen NACH dem Sheet-Versuch, also
    ohne Einfluss auf alles andere. Strings bilingual in AccessibilityStrings.

>>> ZWEITER FEHLER IM SELBEN LOG, MITGEFIXT: der Auto-Lauf sagte "Laufe zu ."
    - ganz ohne Objekt. `AutoWalkService.Toggle` nahm `target.Name.TextValue`
    ROH, also das Glyph, das der Sprecher wegputzt. Jetzt `Describe`. Dieselbe
    Klasse Fehler beim Folgen (Taste +): dort stand `IsNullOrWhiteSpace`, was
    Glyphen und "?" durchlaesst - auch auf `Describe` umgestellt.

>>> ZU TESTEN (Debug gebaut + deployt, Hot-Reload):
    1. In Mist an die Stelle: sagt der Browser jetzt "Beet", "Beet 2", ...?
    2. Numpad3 auf so ein Beet: kommt "Laufe zu Beet." statt "Laufe zu ."?
    3. Taste + auf ein namenloses Objekt: kommt ein Name statt einer Luecke?

## NACHTRAG 2026-08-15 ("CHOCOBO-STALL FEHLTE GANZ" + EINGAENGE GEMESSEN)

>>> IN-GAME BESTAETIGT: die Beete heissen jetzt "Beet". Zwei Rueckfragen kamen
    dazu: "kann man die eingaenge benennen?" und "irgendwo muss da auch ein
    chocobostall sein".

>>> CHOCOBO-STALL - EIGENER FEHLER, GEFUNDEN IM SCHON VORHANDENEN DUMP:
    Er stand die ganze Zeit 12,5 m neben dem Spieler, mit Namen und
    zielbar=True. Der Browser konnte ihn nur nicht sehen, weil
    `ObjectKind.HousingEventObject` (12) in KEINER Kategorie stand -
    AllBrowseKinds und NavCategory.Objects kannten nur EventObj + Treasure.
    Betroffen war die ganze Klasse: "Chocobo-Stall" (131129), "Mogry-Briefkasten"
    (131076), "Dodo-Diarium-Pult" (131257) - alle vom Spiel benannt, alle
    zielbar, alle unsichtbar fuer den Browser.
    GEBAUT: HousingEventObject in AllBrowseKinds + NavCategory.Objects (NICHT in
    die Quest-Varianten - Moebel sind nie ein Questziel). Dazu in
    ObjectMemoryService.IsLandmark, damit die Nummerierung greift: im Dump
    stehen zwei "Mogry-Briefkasten" und zwei "Mogul Mog-Briefkasten" in 60 m.
    Artname: "Einrichtung" / "Furnishing".

>>> ZAEUNE: WARUM DER AUTO-LAUF IM WOHNGEBIET FESTSITZT - BEWIESEN, NICHT
    VERMUTET. Der User kam weder zu den Beeten noch zum Chocobo-Stall (12,6 m,
    Stillstand ueber 4 s, Position wandert 13 cm, vnavmesh queued im
    Sekundentakt neu). Es ist das Grundstueck SEINER Freien Gesellschaft, er hat
    dort also Zutritt - kein "fremde Parzelle"-Fall.
    ERSTE HYPOTHESE WAR FALSCH und wurde verworfen: "Pfad aus 2 identischen
    Wegpunkten" ist KEIN Fehlersignal. Dieselbe Zeile steht in der vorigen
    Sitzung bei Laeufen, die ANKAMEN (Eingang ueber 17 m, Gebrauchtwarenhaendler,
    Eingang zu anderen Zimmern). Sie heisst nur "freie Bahn, geradeaus".
    ERSTER SCHLUSS WAR FALSCH - HIER STEHEN GELASSEN, WEIL DER IRRWEG LEHRREICH
    IST: die beiden Cache-Netze fuer Mist,
    `ffxiv_sea_s1_hou_s1h1_level_s1h1__14C0A__AE__0.navmesh` (15.08. 14:44) und
    `...__14C0A____0.navmesh` (2. August), waren BYTE-IDENTISCH (SHA256
    16CF2384..., beide 543568 Bytes). Daraus hatte ich geschlossen, ein
    Wohngebiets-Netz enthalte grundsaetzlich keine spielergesetzten Strukturen
    und ein Rebuild koenne nicht helfen. BEIDES FALSCH.

>>> WIDERLEGT DURCH DIE MESSUNG (17:04, `/vnav rebuild` auf Ansage des Users):
    - die Datei wuchs von 543568 auf 652058 Bytes, neuer Hash 1FEFDE75... Das
      sind 108490 Bytes = rund ein Fuenftel MEHR Geometrie.
    - der Auto-Lauf, der vorher stur in den Zaun lief, lieferte danach
      "Pfad steht (6 Wegpunkte)" mit einer echten Route mit Kurven
      ((-650,8|-696,0) -> (-651,5|-697,8) -> (-652,0|-702,8) -> (-651,0|-704,0)
      -> (-650,5|-704,0)) und "beendet (angekommen, dist=3,9)".
    DIE HAEUSER SIND ALSO IM NETZ - sie kamen nur zu spaet fuer den Bau.

>>> DIE ECHTE URSACHE (Hypothese, passt auf jede Messung, nicht direkt
    beobachtet): vnavmesh baut beim Laden der Zone. Im Log steht der
    Zonenwechsel um 14:43:47 und die Netzdatei wurde 14:44:04 geschrieben - 17
    Sekunden spaeter, waehrend das Spiel die Haeuser noch nachlaedt. Ergebnis
    ist ein Netz des LEEREN Wohngebiets, und danach korrigiert es nie jemand:
    `NavmeshManager.GetCacheKey` ist {bg}__{filter}__{festivals}__{zoneSGs}
    (dekompiliert) und enthaelt nichts ueber Parzellen. Der veraltete Stand
    bleibt also fuer immer gueltig.
    NEBENBEFUND: vnavmesh hat KEINE Einstellung fuer dynamische Objekte; die
    Config kennt nur Bau- und Laufparameter (BuildMaxCores, StopOnStuck, ...).

>>> DARAUS GEBAUT (drei Sachen):
    1. `MonitorHousingMesh` (NEU, User-Entscheidung "automatisch beim
       Betreten"): baut das Netz einmal pro Zonenbesuch neu, sobald
       `HousingManager.CurrentTerritory->IsLoaded()` true meldet. AUSLOESER IST
       DAS SPIELEIGENE SIGNAL, kein geratener Timer - ein Timer muesste auf eine
       fremde Leitung geeicht werden. Sagt dabei an, WARUM gewartet wird.
       ZU PRUEFEN: ob IsLoaded nicht doch zu frueh true wird. Das zeigt sich
       daran, dass der Neubau nichts aendert; die Log-Zeile haelt den Moment
       fest.
    2. `TryTakeTrail` wird beim Stillstand jetzt IMMER versucht, nicht nur bei
       Netzende (restWp<=1). Vorher lief bei restWp=2 - also genau in diesem
       Fall - die Spur-Suche gar nicht an, und ein Spieler, der den Weg schon
       aufgezeichnet hatte, bekam trotzdem "Ich stecke fest".
    3. `TrailHint` sagt jetzt das Richtige: "Das Wegenetz ist hier aelter als
       die Haeuser. Mit dem Befehl vnav rebuild neu bauen lassen." (vorher der
       falsche Rat, eine Spur aufzuzeichnen).

>>> IN-GAME 16:51: STALL ERREICHT, ABER NICHT UEBER EINE SPUR. Der Hinweistext
    ist bestaetigt (kam zweimal woertlich). Der erfolgreiche Lauf
    ("gestartet zu Chocobo-Stall dist=15,2 -> angekommen, dist=4,0") startete
    aber von einer ANDEREN Stelle: der User war ueber "Eingang" im Haus
    (Kraemerin, Ausgang) und stand danach innerhalb des Zauns. Vom alten
    Standpunkt steckte es erneut fest (12,5 m, gleiche Meldung).
    HEISST: Punkt 1 des Fixes (TryTakeTrail auch beim Festsitzen) ist WEITER
    UNGETESTET - es liegt schlicht noch keine Spur dort. Zu pruefen, sobald der
    User einmal von der Strasse durchs Tor aufzeichnet.

>>> EINGAENGE - NOCH NICHT GEBAUT, ERST GEMESSEN. Vier Tueren in 50 m teilen
    sich DataId 2002737 und das eine Wort "Eingang", zwei davon zielbar. Der
    Name unterscheidet also nichts; was ein sehender Spieler dort liest, ist die
    PARZELLE.
    WAS DIE QUELLE HERGIBT (ilspycmd auf FFXIVClientStructs.dll, 2026-08-15):
    `HousingManager` hat GetCurrentWard / GetCurrentPlot / GetCurrentDivision /
    GetCurrentRoom / GetCurrentHouseId - jede einzelne davon heisst CURRENT,
    zielt also auf die Stelle, wo der SPIELER steht, nicht auf ein Objekt, auf
    das man zeigt. Ein "zu welcher Parzelle gehoert diese Tuer" gibt es nicht.
    DESHALB SONDE STATT VERMUTUNG: `[PlotProbe]` haengt an /acc objprobe und
    loggt genau diese Werte. Zwei moegliche Ausgaenge:
     - die Werte AENDERN sich beim Betreten einer Parzelle -> die Nummer ist
       echter Spielzustand, und der Eingang kann benannt werden, sobald man
       draufsteht ("Eingang, Parzelle 23").
     - sie bleiben draussen stehen -> das Spiel fuehrt es dort nicht, und eine
       Benennung aus der Ferne waere Nachbau. Dann bleibt es, wie es ist.
    ZU MESSEN: /acc objprobe einmal auf freier Strasse, einmal direkt vor einem
    Haus, einmal auf der eigenen Parzelle - und die drei [PlotProbe]-Zeilen
    vergleichen.

>>> DAS FESTSITZEN IST JETZT AUFGEKLAERT - siehe Abschnitt "ZAEUNE" unten.

## FRUEHER (2026-08-14, "ERHOLUNGSBONUS / RUHEBEREICH" - GEBAUT, ZU MESSEN)

>>> ANLASS: Der User hat beim Lesen des Tutorials "Ruhebereiche" gemerkt, dass
    das Plugin den Erholungsbonus ueberhaupt nicht kennt. Wunsch: Sonde bauen und
    die Ansage AUTOMATISCH beim Betreten.

>>> QUELLEN (ilspycmd auf FFXIVClientStructs.dll, 2026-08-14):
    - `AddonExp` (Addon "_Exp"): `MoonIconNode` @632, `CurrentExp` @656,
      `RequiredExp` @660, `RestedExp` @664.
    - `AgentHUD`: `ExpCurrentExperience` @13856, `ExpNeededExperience` @13860,
      `ExpRestedExperience` @13864, `ExpLevel` @13888.
    - `PlayerState.BaseRestedExperience` @744.
    - `AgentHudExpFlag` hat KEIN Ruhebereich-Flag (nur Synced/ExpLocked/MaxLevel/
      InEureka/InOccultCrescent) - deshalb laeuft die Erkennung ueber den Mond.

>>> GEBAUT in CombatService (dort laufen Level- und XP-Verfolgung schon):
    - `TrackRestedArea`: sagt "Ruhebereich. Erholungsbonus sammelt sich." bzw.
      "Ruhebereich verlassen." beim Wechsel des Sichelmonds an der EP-Leiste -
      also genau an dem Zeichen, das der Tutorialtext beschreibt und das ein
      sehender Spieler sieht. Nicht unterbrechend.
    - "Kein Messwert" (Ladebildschirm, HUD-Element weg, Addon nicht da) ist
      BEWUSST von "Mond aus" getrennt, sonst kaeme bei jedem Ladebildschirm ein
      falsches "Ruhebereich verlassen".
    - Erster Messwert nach dem Anmelden setzt nur den Ausgangszustand (kein
      "betreten" fuer einen Ort, in dem man schon stand).
    - Abschaltbar ueber `Configuration.AnnounceRestedArea` (Standard an).

>>> NOCH NICHT ANGESAGT: die HOEHE des Bonus. Die Werte gibt es, aber die
    Struktur sagt nichts ueber die EINHEIT (beides nackte uint). Dafuer laeuft
    die Sonde `RestedProbe` (#if DEBUG, eine Zeile pro AENDERUNG):
    `[RestedProbe] mond=True addonRested=... hudRested=... hudExp=.../... stufe=...
    basisRested=...`

>>> ZU MESSEN: mit dieser Debug-Fassung in ein Gasthaus/eine Stadt gehen und
    wieder heraus. Erwartet: die Ansage beim Wechsel, und im Log die
    RestedProbe-Zeilen. Aus dem Verhaeltnis hudRested zu hudExp-Nenner wird dann
    der Satz fuer die Bonus-Hoehe gebaut; danach faellt die Sonde raus.

## NACHTRAG 2026-08-14 ABENDS ("ABFRAGE AUF STRG+L" + ERSTE MESSWERTE)

>>> ERSTE MESSREIHE LIEGT VOR (dalamud.log 19:22-19:25, Stufe 41, im Ruhebereich
    stehend). Was sie zeigt:
    - `basisRested` (PlayerState.BaseRestedExperience) waechst EXAKT 1 pro Sekunde
      (120761 -> 120941 in 180 s). Das ist ein Zaehltakt, keine EXP-Zahl.
    - `hudRested` waechst 0,8 pro Sekunde (97638 -> 97782 in 180 s). Der
      Zusammenhang ist im Messfenster affin: hudRested = 0,8 * basisRested + 1029,2
      trifft alle Stichproben auf die Einheit genau (Probe: basis=120851 ->
      erwartet 97710, geloggt 97710).
    - `addonRested` == `hudRested`, sobald das "_Exp"-Addon gebaut ist; vorher 0,
      waehrend der Agent den Wert schon fuehrt. Deshalb liest die Abfrage AgentHUD.
    - Der Mond stand ueber die ganze Reihe auf True bzw. war nicht ablesbar
      (Ladephase) - ein WECHSEL wurde noch nicht gemessen, im Log steht nur
      `[Rested] Ausgangszustand: imRuhebereich=True`.

>>> EINHEIT GEKLAERT (19:36, ohne Kampf-Test): ueber den Balken, den der sehende
    Spieler sieht - `AtkComponentGaugeBar.RestedExpNode` @376. Messung bei Stufe 41:
    Balkenbreite 482, Fuellknoten 91 bei 27523/163000 = 16,89 %, Ruheknoten 375.
    Beide Knoten folgen `Breite = 471 * Anteil + 11,5`; die PROBE auf diese
    Anpassung haelt (Anteil 1 ergibt 482,5 = volle Breite), sie war also nicht
    bloss hineingerechnet. Der Ruheknoten steht damit bei 77,2 %, und das ist
    exakt (27523 + 98283) / 163000. Also zaehlt RestedExp EXP-PUNKTE auf derselben
    Skala wie CurrentExp -> hudRested / needed ist ein Prozentsatz EINER STUFE.
    Nebenbefund: die Leiste rechnet in Promille (`skala=0..10000`,
    `balkenWert=1689` = exakt der EXP-Stand).

>>> EIGENE TASTE UMSCHALT+L (`KeyRestedStatus`, auf Wunsch des Users): die Abfrage
    haengt NICHT mehr an Strg+L, die Stufen-Ansage ist wieder wie vorher. Umschalt+L
    steht nicht in der Belegt-Liste des Keybind-Dumps (dort nur Umschalt+Tab/T/F/M/V)
    und war im Plugin frei. Als eigenstaendiger Satz sagt sie jetzt auch das Nein:
    "Kein Ruhebereich." Bei nicht ablesbarer Leiste (Ladebildschirm, HUD aus) faellt
    der Ortsteil weg statt geraten zu werden; bleibt gar nichts zu sagen, kommt
    "Erholungsbonus nicht verfügbar." Steht als "Erholungsbonus" in der Tastenhilfe.

>>> ANSAGE HAT JETZT DIE ZAHL: " Erholungsbonus für N Prozent einer Stufe."
    Ein Pool > 0, der auf unter 1 % rundet, sagt "1 Prozent" statt
    "kein Erholungsbonus" - vorhanden und leer duerfen nicht gleich klingen.
    ZU BESTAETIGEN: eine zweite Messung bei ANDEREM EXP-Stand (also nach etwas
    Kampf) gegen die Formel; solange bleibt RestedProbe drin.

>>> WAS DIE QUELLE NICHT HERGAB (heute geprueft):
    AddonExp hat KEINEN Textknoten und keine Formatiermethode fuer RestedExp
    (ilspycmd: Felder enden bei CurrentExp/RequiredExp/RestedExp), und in den
    Sheets steht keine Zeile "Erholungsbonus: x". Gefunden wurden nur
    LogMessage 732 "Du hast einen Ruhebereich betreten." und 733 "Du hast den
    Ruhebereich verlassen." -> DAS SPIEL MELDET DEN WECHSEL SELBST. Ob unser
    Chat-Leser diese Systemzeile ohnehin schon spricht, ist die zweite offene
    Frage; wenn ja, ist TrackRestedArea eine Dopplung und faellt weg.

>>> GEBAUT: die Abfrage haengt jetzt an der vorhandenen Stufen-Taste STRG+L
    (`CombatService.DescribeRestedState`), nicht auf einer neuen Taste - es ist
    dieselbe Anzeige (EP-Leiste) und spart eine Tastenkollision. Sie sagt
    " Im Ruhebereich." nur, wenn der Mond wirklich ablesbar ist, und
    " Erholungsbonus vorhanden." / " Kein Erholungsbonus." nach hudRested > 0.
    BEWUSST OHNE ZAHL, solange die Einheit nicht gemessen ist. Build Debug
    0 Warnungen / 0 Fehler, liegt in devPlugins.

>>> TESTSTAND:
    1. ERLEDIGT: die Ansage kommt (User 19:33 an Strg+L bestaetigt, Anlass fuer
       die Zahl war genau, dass die Hoehe fehlte). Sie liegt jetzt auf Umschalt+L
       und ist dort noch nicht gedrueckt worden.
    2. OFFEN: Ruhebereich verlassen -> kommt die Ansage EIN- oder ZWEIMAL?
       Zweimal hiesse: das Spiel meldet 732/733 schon selbst ueber den Chat, dann
       ist TrackRestedArea eine Dopplung und faellt weg.
    3. OFFEN (nur noch Gegenprobe): Strg+L nach etwas Kampf. Erwartet bei Stufe 41
       und ~98400 Bonus: "Erholungsbonus für 60 Prozent einer Stufe". Sinkt der
       Prozentsatz mit dem Verbrauch und passen `[RestedProbe]`-Knotenbreiten
       weiter zur Formel, faellt die Sonde raus.

## AUCH NEU 2026-08-14 ("AUSRUESTUNGSSET-MARKE HOERBAR" - GEBAUT, ZU TESTEN)

>>> SPIELERWUNSCH (ueber den User): ein Sehender sieht am Inventar-Symbol, dass
    ein Ausruestungsteil zu einem gespeicherten Set einer ANDEREN Klasse gehoert -
    "stop, nicht verkaufen". Diese Marke soll hoerbar werden.

>>> QUELLE, und zwar eine ausdrueckliche: `RaptureGearsetModule
    .IsItemRegisteredToGearset(InventoryItem*, itemRow=null, equipSlotIndex=14)`.
    Die ClientStructs-Doku sagt woertlich "Used for the gearset mark on inventory
    item icons" - es ist also GENAU die Pruefung, mit der das Spiel das Symbol
    zeichnet, keine nachgebaute. equipSlotIndex 14 laesst das Spiel den Slot aus
    der EquipSlotCategory des Gegenstands selbst aufloesen.
    Die Funktion wird per Signatur aufgeloest und WIRFT bei Fehlschlag
    (ThrowNullAddress) - daher try-catch mit Log, kein stiller Fehlschlag.

>>> WORTWAHL AUS DEM SPIEL (Sheet-Dump 2026-08-14): der Begriff ist
    "Ausrüstungsset" (Addon 756), und fuer genau diesen Fall gibt es
    Addon 11993 "Dieser Gegenstand ist in einem Ausrüstungsset gespeichert."
    sowie Addon 8895/4649 als Warnungen beim Fortfahren/Abliefern. Die Ansage
    spricht also die Sprache der Oberflaeche.

>>> GEBAUT, zwei Stellen:
    - Inventar vorlesen (Strg+F3): markierte Teile haengen ", im Ausrüstungsset"
      an. Kurzform, weil sie hinter jedem Gegenstand der Liste stehen kann.
    - Gegenstands-Tooltip (Strg+F10): " Achtung: in einem Ausrüstungsset
      gespeichert, nicht verkaufen." Der Tooltip selbst enthaelt die Tatsache
      NICHT - das Spiel malt sie nur als Symbol aufs Icon, ein Textleser kann sie
      also nie aufsammeln.

>>> EINE BEWUSSTE UNSCHAERFE: der Tooltip kennt nur die Id (AgentItemDetail.ItemId
    @312), nicht den Slot, auf den gezeigt wird. Bei ZWEI gleichen Teilen, von
    denen nur eines registriert ist, warnt er darum fuer beide. Die fehlende
    Warnung waere der teurere Fehler. Im Inventar-Durchlauf ist die Pruefung
    exakt, dort liegt pro Slot der echte Zeiger vor (Dalamud
    GameInventoryItem.Address zeigt IN den lebenden Container, nicht auf eine
    Kopie - ilspycmd geprueft).

>>> NACHGESCHAERFT nach Rueckfrage des Users ("es geht ums ARSENAL beim
    VERKAUFEN"): die Marke kommt jetzt AUTOMATISCH beim Durchgehen, nicht erst auf
    Strg+F10. Angehaengt wird sie im Fokus-Pfad (`ResolveFocusedItemName`), also
    genau dort, wo beim Wandern durchs Gitter ohnehin schon "Name, Stufe, tragbar"
    entsteht. KEIN neuer ItemDetail-Listener: der Tooltip ist seit 2026-07-19
    bewusst aus dem generischen Scanner ausgesperrt, weil dieser seine 7-8
    Text-Knoten einzeln mit SpeakInterrupt sprach und sich selbst uebertoente.
    Der Fokus-Pfad rechnet nur bei FOKUSWECHSEL (Zeile ~1961), nicht pro Frame -
    darum ist der Container-Scan hier tragbar; zusaetzlich steigt er sofort aus,
    wenn der Gegenstand gar keine Ausruestung ist.

>>> AUSSERDEM: KLASSEN-ANSAGE (`GearInfoService.DescribeOwnClasses`), auf Wunsch
    des Users. Genannt werden NUR die EIGENEN Klassen mit vollem Namen
    ("für deine Klassen Ritter, Gladiator"). Grund steht im Sheet-Dump: der
    ClassJobCategory-NAME ist eine Abkuerzungsliste - haeufigster Wert nach
    "Alle Klassen" ist "GLA MAR PLD KRG DKR REV" auf 1912 Teilen, was ein
    Screenreader als Buchstabensalat vorliest; sechs volle Namen pro Gegenstand
    wuerden die Ansage begraben. Eigene Klasse = ClassJobLevels > 0, Index ist
    ExpArrayIndex aus dem ClassJob-Sheet (die Struktur dokumentiert genau diesen
    Index). ACHTUNG, noch offen: die BESTEHENDE Ansage "nur für ..." bei nicht
    tragbaren Teilen spricht weiterhin die rohen Abkuerzungen - bewusst nicht
    angefasst, waere der naechste Schritt.

>>> NICHT angefasst: die zweite Fokus-Stelle im Quest-Belohnungsfenster
    (JournalResult). Dort besitzt man das Teil noch gar nicht, eine
    Verkaufswarnung waere dort sinnlos.

>>> ZU TESTEN: 1. Im Arsenal/Verkaufsfenster durch die Teile gehen - kommt
    ", für deine Klasse X" und bei Set-Teilen ", im Ausrüstungsset"?
    2. Strg+F10 auf so einem Teil - kommt die lange Warnung "nicht verkaufen"?
    3. Strg+F3 (Taschen) - Marke an markierten Teilen?
    WICHTIG fuer die Deutung: ohne angelegte Ausruestungssets kann nichts markiert
    sein. Genau dafuer steht die Log-Zeile "Keine Ausrüstungssets angelegt -
    keine Markierung möglich." (RaptureGearsetModule.NumGearsets == 0).
    OFFEN: verkauft man ein Set-Teil, sollte das Spiel selbst nachfragen
    (Addon 8895 "...als Teil eines Ausrüstungssets registriert. Trotzdem
    fortfahren?"); SelectYesno liest das Plugin bereits - ungeprueft, ob dieser
    Dialog beim Verkaufen wirklich kommt.

## FRUEHER (2026-08-13 NACHTS, "TUTORIAL-FENSTER HowTo SPRICHT" - IN-GAME BESTAETIGT)

>>> VOM USER IM SPIEL BESTAETIGT: Ueberschrift, Seite UND Text kommen. Damit ist
    auch die offene Frage beantwortet - das Spiel setzt `IsChecked` auf den
    Seiten-RadioButtons, die Seitenangabe kommt also aus dem Spielzustand und
    nicht aus einer Ersatzquelle.

>>> GEBAUT nach der Analyse von 21:45 (Abschnitt weiter unten). Neuer dedizierter
    Leser `OnHowToUpdate` in UIReaderService: sagt bei JEDER Aenderung des Inhalts
    Ueberschrift, "Seite X von Y" und den Tutorialtext an - also beim Oeffnen und
    bei jedem Blaettern. Build Debug 0 Warnungen / 0 Fehler, liegt in devPlugins.

>>> WIE ES LIEST (alles am Dump 21:45 belegt):
    - Ueberschrift id=5 und Text id=11 ueber `AtkText.ReadClean`, weil der Text
      Item-Verweis-Bytes traegt ("...H??I??RuhebereichIH...").
    - Seiten aus den RadioButtons id=17..21: die ANZAHL ist die Zahl der
      SICHTBAREN davon (im Dump ist id=21 unsichtbar, das Thema hat also vier,
      nicht fuenf Seiten - die feste "von 5" aus der Analyse waere falsch
      gewesen), die AKTUELLE ist die mit `IsChecked` (wie der Staatstaler-Shop).
    - Ist kein Knopf checked, faellt die Seitenangabe WEG statt geraten zu
      werden; die Log-Zeile `[HowTo] Seite 0/4: ...` zeigt genau diesen Fall.
    - Die Blaetterpfeile (id=22/16) bleiben unangetastet - sie sind nicht
      verifiziert, und die Ansage haengt ohnehin nur am geaenderten Inhalt,
      funktioniert also mit Numpad 4/6 genauso wie mit Maus oder Controller.

>>> AUSGESPERRT: "HowTo" steht jetzt in SpecialSetupAddons UND
    SpecialUpdateAddons, damit der generische Leser nicht weiter die Fussnote
    (id=14) spricht. Die THEMENLISTE "HowToList" ist NICHT eingetragen - die
    funktioniert und bleibt im generischen Pfad.

>>> GETESTET UND IN ORDNUNG: Tutorial-Fenster oeffnen (Themenliste -> Thema
    waehlen) spricht "Ueberschrift. Seite X von Y. Text", Blaettern spricht die
    neue Seite. Log-Zeile zum Nachsehen: `[HowTo] Seite X/Y: '...'`.

## FRUEHER (2026-08-13 SPAET, "PR #6 TIEFES GEWOELBE IM TESTZWEIG" - GEBAUT, UNGETESTET)

>>> ES GIBT JETZT DOCH EINEN NEUEN: PR #6 "Tiefes Gewoelbe: Raeume, Truhen und
    die Charakterinfo werden sprechend", eingegangen 2026-08-13 21:25, wieder
    von bladestorm360. 3293 Zeilen, sieben neue Dateien (DeepDungeonFloor, -Mesh,
    -Nav, -Panel, -RoomMap, -State, -Text), neue Objekt-Kategorien "Schaetze" und
    "Leuchten", neue Taste Strg+F fuer "welches Gewoelbe, welche Ebene".
    Gemergt in test/prs, Build Debug 0 Warnungen / 0 Fehler.
    Die Versionsansage sagt jetzt "5.83 Testfassung mit SECHS Beitraegen".

>>> VIER KONFLIKTE, alle "beide Seiten behalten". Einer war mehr als Formsache:
    PR #6 bringt eine ZWEITE `IGameConfig`-Deklaration mit (der Autor kennt den
    Zweig nicht, auf dem sie schon steht). Uebernommen wurde nur der neue
    `ISeStringEvaluator`; sonst waere derselbe Doppel-Fehler wie beim
    main-Merge entstanden. In NavigationService liegen die Kategorien aus PR #3
    (Verbuendete/Inhalte) und PR #6 (Schaetze/Leuchten) in derselben Methode -
    beide Bloecke bleiben, die doppelte `var cat`-Zeile ist entfallen.

>>> ZU PRUEFEN BEIM TESTEN - MOEGLICHE TASTENFALLE, NICHT GEMESSEN: PR #6 legt
    seine Ebenen-Ansage auf `Strg+F`. Der Keybind-Dump (Desktop, 21:40) zeigt
    Strg+F als frei, ABER bare F ist `FACE` (344). Genau diese Lage hat in
    V5.25 (Strg+H trotz Modifier -> MENU_CRAFT) und V5.49 (Strg+, / Strg+. ->
    MENU_MOUNT) schon zweimal zugeschlagen: das Spiel feuert auf die BASISTASTE,
    obwohl Strg gehalten wird. Ob es hier passiert, sagt nur der Test - wenn
    beim Druck auf Strg+F zusaetzlich die Spielfunktion FACE ausloest, ist das
    diese Falle und kein Zufall.

>>> KEINER DER SECHS IST VOM AUTOR IM SPIEL GETESTET, er schreibt das selbst
    dazu. Fuer PR #6 heisst das: die ganze Gewoelbe-Mechanik ist ungeprueft.

## FRUEHER (2026-08-13, "TUTORIAL-FENSTER HowTo IST STUMM" - ANALYSE, INZWISCHEN GEBAUT)

>>> MELDUNG DES USERS mit Dump (C:\Users\brued\Desktop\FFXIV_UI_Dump.txt,
    21:45:06, enthaelt BEIDE Fenster) - "die Sachen sind nicht auslesbar".

>>> GENAUER BEFUND, das ist nicht ein Fenster sondern zwei:
    - `HowToList` (die Themenliste) GEHT BEREITS: Log 21:45
      "[Focus] addon='HowToList' id=5 Text='Ruhebereiche'" -> "[Speak]
      'Ruhebereiche'". Auch "Alle" (der Kategorie-Reiter) kommt.
    - `HowTo` (der Tutorialtext selbst) ist stumm bzw. sagt das Falsche:
      beim Oeffnen wird die FUSSNOTE gesprochen ("* Tutorial-Fenster unter
      Charakterkonfiguration im Untermenue "UI" de-/reaktivierbar", Knoten
      id=14) - der einzige Text, den der allgemeine Leser greifen konnte.
      Beim Blaettern: "[Focus] STUMM addon='HowTo' id=4 typ=8 Text=''".

>>> WAS DER DUMP AN VERWERTBAREN KNOTEN ZEIGT (Zeilen 394-458):
    - id=5  Text = die UEBERSCHRIFT ("Ruhebereiche")
    - id=11 Text = der TUTORIALTEXT ("Sobald du einen ...Ruhebereich...
      betrittst, erscheint ein..."). Enthaelt Item-Verweis-Bytes, braucht also
      AtkText.ReadClean wie das Sammel-Fenster.
    - id=17..21 = fuenf RadioButton(4) mit den Beschriftungen "1".."5" - das
      sind die SEITEN. Im Dump ist id=17 (Seite 1) aktiv; welche gilt, sauber
      ueber RadioButton.IsChecked lesen, nicht ueber die Sichtbarkeit des
      Image-Knotens (so macht es schon der Grand-Company-Shop).
    - id=22 und id=16 Comp(1008) Button = vermutlich die beiden Blaetterpfeile,
      NICHT verifiziert.
    - id=14 Text = die Fussnote, die faelschlich gesprochen wird.

>>> ZU BAUEN: beim Oeffnen und bei JEDEM Seitenwechsel Ueberschrift, "Seite X
    von 5" und den Text sprechen. Der Fokus springt beim Blaettern zwischen den
    RadioButtons (Log: eltern=[17:1009 ...] dann [18:1009 ...]), Numpad 4/6 ist
    also der Weg des Spielers - daran haengt die Ansage.

## FRUEHER (2026-08-13 ABENDS, "EINGEHENDES FLUESTERN FEHLTE IM NEUEN PUFFER" - IN-GAME BESTAETIGT)

>>> BESTAETIGT AM LOG, Test des Users 21:24 (die DLL lag um 21:05:14 vor, der
    Tell um 21:04:43 lief noch ohne - der Unterschied ist im Log sichtbar):
    - 21:24:07.050 "TellIncoming hat keinen eigenen Schalter - zusaetzlich in
      den Kanal 'Fluestern' von TellOutgoing archiviert."
    - 21:24:10.845 geblaettert: "Elonea Mondfeder: was kommt dann ? hab nix
      verstanden, 3 von 3" - die EINGEHENDE Nachricht im Puffer.
    - 21:24:14.895 eine Zeile weiter die eigene: "Perrox Torran an Elonea
      Mondfeder: bald gibts ein update ...".
    Beide Richtungen stehen also in einem Puffer, in Ankunftsreihenfolge.

>>> EBENFALLS BESTAETIGT: pro Nachricht genau EIN `[Speak]`, waehrend `[ChatAlt]`
    still mitschreibt. Der Umschalter macht keine doppelten Ansagen - das war
    das einzige Risiko, das ich nur am Code pruefen konnte.

>>> NACHGEZOGEN (gebaut, ungetestet): ENTER ANTWORTET AUCH IM NEUEN SYSTEM, wenn
    gerade ein Fluestern gelesen wird - der zweite Teil des User-Wunsches.
    WARUM DAS KEIN RUECKFALL HINTER PR #5 IST: dessen Einwand lautet, aus einem
    Empfangsfilter folge kein Sendekanal, und das gilt fuer die Kanaele
    unveraendert. Beim Fluestern wird das Ziel aber gar nicht aus dem Puffer
    abgeleitet - es steht als Nutzlast IN der gelesenen Nachricht (Name +
    Heimatwelt, vom Spiel geliefert, `TellTarget`). Gelesen statt geraten,
    deshalb zulaessig.
    `ChatChannelService.TryAnswerBrowsedTell(partner, lastActivity)` ist der
    gemeinsame Weg; die 30-Sekunden-Frist des alten Systems gilt unveraendert.
    OHNE Fluester-Partner bleibt es STILL - anders als im alten System, wo "kein
    Partner" eine Meldung wert war: dort stand der Spieler in der Kategorie
    Fluestern, hier kann der Puffer jeder Kanal oder ein ganzes Register sein,
    und eine Meldung bei jedem Enter waere Laerm.
    ZU TESTEN: im neuen System eine Fluester-Nachricht lesen, Enter druecken -
    die Eingabezeile muss mit "... zufluestern" aufgehen. Das Log zeigt es als
    `[ChatKanal] Fluester-Ziel '<Name>@<Welt>' gesetzt: True`.


>>> MELDUNG DES USERS: im neuen Chatsystem entsteht der Fluester-Puffer nur,
    wenn er selbst fluestert; angefluesterte Nachrichten landen nicht darin.

>>> URSACHE, AUS DEM LOG BELEGT (2026-08-13 20:58:42 / 20:59:59, beide Zeilen
    stehen woertlich im dalamud.log):
    - `kind=TellIncoming (13) ... register=keins kanal=keine`
    - `kind=TellOutgoing (12) ... register=0 kanal=Fluestern`
    Das LogFilter-Sheet fuehrt fuer Kind 13 KEINE Zeile (der Code meldet das als
    "kein LogFilter-Schalter fuer diese Art"), fuer Kind 12 schon. Eingehende
    Fluester laufen deshalb durch `ArchiveUnfilterable` und landen nur in den
    "Alles"-Puffern der Register, nie in einem Kanal-Puffer. Der PR-Autor hat
    diesen Fall im Kommentar sogar benannt - TellIncoming steht in seiner Liste
    der 21 Arten ohne Schalter -, nur die Folge fuer die Nachlese nicht gezogen.

>>> NICHT BETROFFEN WAR DAS SPRECHEN: "Fluestert von Elonea Mondfeder: ..." kam
    im selben Moment sauber (Log 20:58:42.007). Es fehlte ausschliesslich die
    Nachlese.

>>> GEBAUT: `GameChatFilters.ChannelOfKind(kind)` schlaegt den Kanal einer
    Chat-Art im Sheet nach, und `ChatReaderService.SameConversationAs` sagt, dass
    eingehendes und ausgehendes Fluestern dieselbe Unterhaltung sind. Eine Zeile
    ohne eigenen Schalter wird zusaetzlich in den Kanal der Gegenrichtung
    archiviert. Der Kanalschluessel wird nachgeschlagen, NICHT als Zahl
    hingeschrieben - eine feste Id waere genau das, was ein Patch still umlegt.

>>> BEWUSST NUR DAS ARCHIV, NICHT DIE SPRECH-ENTSCHEIDUNG. Die bleibt beim
    Unfiltered-Schalter. Wuerde die Zeile ab jetzt dem Register-Schalter ihres
    neuen Kanals folgen, koennte ein ausgeschaltetes Register eingehende
    Fluester verstummen lassen - und dagegen hat der Spieler im Spiel selbst
    keinen Schalter.

>>> NEBENBEFUND, UND ER ENTKRAEFTET DAS EINZIGE OFFENE RISIKO DES UMSCHALTERS:
    im Log laufen beide Chat-Leser nebeneinander (`[Chat]` und `[ChatAlt]`), und
    pro Nachricht steht dort GENAU EIN `[Speak]`. Die befuerchteten doppelten
    Ansagen treten also nicht auf - das war bis eben nur am Code geprueft.
    Gesprochen hat das neue System, der alte Leser hat still mitarchiviert.

>>> Build Debug 0 Warnungen / 0 Fehler, nach devPlugins deployt. Zweig test/prs.
    ZU TESTEN: einmal angefluestert werden, ohne vorher selbst zu fluestern -
    der Puffer "Fluestern" muss die eingehende Nachricht fuehren. Das Log zeigt
    es als "TellIncoming hat keinen eigenen Schalter - zusaetzlich in den Kanal
    'Fluestern' von TellOutgoing archiviert."

## FRUEHER AM TAG (2026-08-13, "TESTZWEIG AKTUELL + UMSCHALTER ZWISCHEN DEN CHATSYSTEMEN")

>>> ES GIBT KEINE NEUEN PRS. Der User vermutete welche; nachgesehen mit `gh pr
    list --state all`: es sind dieselben fuenf von bladestorm360, der juengste
    (#5) vom 2026-08-11. Der Zweig `test/prs` hatte sie schon, ihm fehlte nur
    der Stand von main.

>>> TESTZWEIG AUF STAND GEBRACHT (Merge main -> test/prs, Commit df194cb): jetzt
    v5.83 + Verlosungswerte + die fuenf Beitraege. Fuenf Konflikte, alle daher,
    dass die drei Features auf main als cherry-pick und hier als eigener Commit
    liegen; aufgeloest zugunsten der Seite mit dem PR-Code. `IGameConfig` war
    danach doppelt deklariert (PR #5 fuer LogTabFilterN, main fuer den
    Bewegungsmodus) - jetzt eine Deklaration fuer beide.
    Die Versionsansage sagt "5.83 Testfassung mit fuenf Beitraegen".

>>> UMSCHALTER ZWISCHEN DEN CHATSYSTEMEN (Wunsch des Users, weil PR #5 seine
    gewohnte Nachlese samt Enter-Antwort ersetzt). Im Optionsmenue (Umschalt+F9)
    steht jetzt ganz oben "Chatsystem: gewohnt, feste Kanaele" bzw. "neu,
    Register des Spiels".
    - VORBELEGT AUF DAS GEWOHNTE: wer nichts umstellt, hoert v5.83.
    - BEIDE SYSTEME LAUFEN IMMER MIT. Jede Chat-Zeile geht an beide Leser, beide
      Nachlesen werden gefuellt; der Schalter entscheidet NUR, wer spricht und
      wer die Tasten bekommt. Deshalb hinterlaesst Umschalten keine Luecke - die
      Ansage sagt das auch ("Beide Nachlesen laufen mit").
    - Zurueckgeholt als eigene Klassen, damit nichts nachgebaut werden muss:
      `LegacyChatHistoryService`, `LegacyChatReaderService` (beide wortgleich zu
      main, Log-Praefix `[ChatAlt]`) und `ChatChannelService` (Enter antwortet im
      gelesenen Kanal, v5.67).
    - DAS RISIKO WAREN DOPPELTE ANSAGEN. Beide Leser haengen an derselben Quelle.
      Gegengeprueft: JEDE Sprechstelle beider Leser liegt hinter dem Schalter,
      inklusive der Filterwarnung des neuen Systems und der Registeransage
      (`FollowChatTab`). Der inaktive Leser archiviert nur und ruft insbesondere
      KEIN `RememberSpokenVariant` - sonst wuerde er den Echo-Schutz des anderen
      verbrauchen.
    - Was die Chat-Leser nicht sehen (Dialogfenster, System-Meldungen, XP), geht
      ueber `MessageHistoryService.Mirror` in die alte Nachlese. Die Chat-Zeilen
      selbst laufen dort NICHT durch (`mirror: false` an den fuenf Add-Stellen
      des neuen Lesers), sonst staende jede Zeile doppelt im alten Verlauf.
    - Vier Tasten gibt es nur im neuen System (Puffer-Anfang/-Ende,
      Registertasten). Im alten sagen sie "Diese Taste gehoert zum neuen
      Chatsystem", statt stumm zu bleiben.

>>> Build Debug 0 Warnungen / 0 Fehler, nach devPlugins deployt. Zweig test/prs.

>>> EIGENER FEHLER, BEHOBEN, ZUR WARNUNG NOTIERT: eine Sammelersetzung per
    PowerShell (`Get-Content -Raw` + `Set-Content -Encoding utf8`) hat in
    ChatReaderService.cs vier Zeilen mit Umlauten/Sonderzeichen zerschossen -
    Windows PowerShell 5.1 liest UTF-8 ohne BOM als ANSI. Gefunden durch eine
    Mojibake-Suche ueber alle geaenderten Dateien, von Hand repariert.
    NIE WIEDER Dateien mit Nicht-ASCII per PowerShell umschreiben.
    (Nebenbefund: Plugin.cs traegt 11 solche Stellen schon laenger - alle in
    KOMMENTAREN, keine gesprochene Zeichenkette betroffen, deshalb hier nur
    vermerkt und nicht angefasst.)

>>> ZU TESTEN, in dieser Reihenfolge:
    1. Laden: die Ansage muss "5.83 Testfassung mit fuenf Beitraegen" sagen.
       Kommt "5.83" blank, laeuft die veroeffentlichte Fassung.
    2. Chat ohne Umstellen: muss klingen wie v5.83 (Alt+Bild-auf/-ab durch die
       Kategorien, Umschalt+Bild blaettern, Enter antwortet im Kanal).
    3. Umschalt+F9, oberste Zeile, umschalten - dann dieselben Tasten im neuen
       System, und einmal zurueck.
    4. DABEI AUF DOPPELTE ANSAGEN HOEREN. Das ist die eine Stelle, die ich nur
       am Code pruefen konnte.

>>> WARNUNG ZU PR #1, dem User schon gesagt: er macht die EIGENEN HP wieder zu
    "X von Y". Prozent war die Entscheidung vom 2026-08-07 und ist in V5.31
    schon einmal unbemerkt gekippt - siehe den Testzweig-Abschnitt unten.

## FRUEHER AM TAG (2026-08-13, "AUSRUESTUNGSWERTE IN DER VERLOSUNG" - GEBAUT, UNGETESTET)

>>> WUNSCH DES USERS: Ruestungsteile in der Beute-Verlosung sollen ihre Werte
    nennen, "so wie im Arsenal bzw. Ausruestungs-Menue".

>>> KEIN NEUER CODE FUER DIE WERTE: `GearInfoService.DescribeGear` gibt es seit
    v5.70 und liefert genau diesen Satz ("Stufe 15, tragbar, Gegenstandsstufe 20,
    Verteidigung 31, Staerke plus 4") - alle Zahlen aus dem Item-Sheet gelesen,
    nichts nachgerechnet. Der LootRollService bekommt den Dienst jetzt per
    Konstruktor (Plugin.cs 211), `AccessibilityStrings.LootRollRow` hat einen
    zusaetzlichen `gear`-Baustein direkt hinter dem Namen (DE+EN).

>>> BEWUSST NUR BEIM BLAETTERN (`DescribeRollRow`), vom User so entschieden:
    die selbsttaetige Ansage beim Aufgehen der Verlosung und die Uebersicht auf
    Umschalt+F7 bleiben kurz. Begruendung: die beiden laufen mitten im Kampf und
    ueber mehrere Gegenstaende auf einmal, der volle Wertesatz waere dort nicht
    mehr anhoerbar. Die Zeile dagegen IST der Moment der Entscheidung.

>>> NICHT-AUSRUESTUNG BLEIBT WIE BISHER (ebenfalls User-Entscheid): fuer Materia,
    Bausteine usw. wird `DescribeItemBasics` NICHT angehaengt, `DescribeGear`
    liefert dort ohnehin "". Falls das spaeter doch gewuenscht ist, ist es eine
    Zeile an derselben Stelle.

>>> Build Debug 0 Warnungen / 0 Fehler, nach devPlugins deployt.
    ZU TESTEN: eine Verlosung mit einem Ruestungsteil, Zeile ansteuern - es muss
    Name, dann Stufe/Tragbarkeit/Gegenstandsstufe/Verteidigung/Attribute, dann
    Optionen und Restzeit kommen. Das Log zeigt es als `gear='...'` in der
    `[Loot] Zeile`-Zeile.

## FRUEHER (2026-08-12, "NUMPAD5: ZUR WEGRICHTUNG DREHEN" - GEBAUT, UNGETESTET)

>>> WUNSCH DES USERS: beim MANUELLEN Laufen per Tastendruck in die Richtung
    gedreht werden, in die man laufen muss. Ein Druck = einmal ausrichten
    (so gewaehlt, nicht dauerhaftes Nachfuehren).

>>> TASTE: bare Numpad5. Im Keybind-Dump ist das CAMERA_FOCUS - der User hat die
    Kamerafunktion bewusst geopfert (rein visuell), weil Numpad5 die tastbare
    Erhebung traegt. Das Plugin SCHLUCKT die Taste (`KeyState[key]=false`, wie
    beim Skill-Menue), sonst wuerde das Spiel die Kamera zusaetzlich zentrieren
    und gegen die eben gesetzte Richtung arbeiten. Umschalt+Numpad war nie eine
    Option (Windows-Falle, game-api.md).

>>> WAS GEDREHT WIRD - UND WARUM BEIDES: gesetzt werden `GameObject.Rotation`
    (@192) UND `Camera.DirH` (@320, beide ilspycmd 2026-08-12). Grund: ob beim
    manuellen Laufen die FIGUR oder die KAMERA steuert, haengt am Bewegungsmodus
    (Standard = kamerarelativ, Legacy = figurrelativ), und das steht in keiner
    Struktur. Der Modus wird aus `GameConfig.UiControl["MoveMode"]` mitgeloggt.

>>> DER FIGUR-WINKEL IST EXAKT, DER KAMERA-WINKEL IST EINE MARKIERTE ANNAHME:
    Zielrotation = atan2(dx, dz) - dieselbe Konvention, auf der `RelativeAngle`
    steht (in-game verifiziert 2026-07-10). Fuer `DirH` wird DIESELBE Konvention
    ANGENOMMEN; ob das stimmt, ist offen. Deshalb loggt `[Face] vorher:` rot und
    dirH im selben Moment, BEVOR etwas geschrieben wird - ein einziger Druck mit
    der Kamera hinterm Ruecken zeigt, ob beide Werte zusammenpassen.

>>> ZIEL IST DER FUEHRUNGSPUNKT DER GEHHILFE (`_route[_routeCursor]`, sonst das
    Ziel selbst). Ohne laufende Gehhilfe sagt es "Kein Weg aktiv."

>>> Build Debug 0 Warnungen / 0 Fehler, nach devPlugins deployt.

>>> RELEASED als v5.83 (2026-08-12, 22:23) - OHNE die fuenf PRs, wie vom User
    angesagt. Auf main kamen die drei Features per cherry-pick vom Testzweig;
    beim Aufloesen fielen die PR-Anteile heraus (CharaMakeReader und der
    Chat-Puffer-Block existieren auf main nicht). Neu auf main noetig war nur der
    PluginService `IGameConfig` - den brachte bisher PR #5 mit.
    VERIFIZIERT: v5.83 ist "Latest", alle 4 Assets dran,
    `releases/latest/download/latest.zip` liefert 654.430 Bytes (= der neue
    Build), repo.json auf main steht auf 5.83.0.0. Der Installer war seit v5.82
    unveraendert (keine Commits unter Installer/), deshalb exe + installer.json
    unveraendert uebernommen - SHA256 gegengeprueft, stimmt ueberein.

>>> IN-GAME BESTAETIGT vor dem Release: Auswahllisten und die Numpad5-Drehung.
    Die Kamera-Annahme aus der Numpad5-Ansage ist noch nicht ausgewertet - dafuer
    muss einmal ein `[Face] vorher:` aus dem Log angeschaut werden.

>>> NACHTRAG 2026-08-13: Der User meldet "die Beuteverlosung funktioniert" -
    die Verlosungszeilen gelten damit als in-game bestaetigt. WICHTIG fuer die
    Bewertung: getestet hat NICHT der User selbst, sondern ein anderer Spieler,
    und ob mehrere Gegenstaende gleichzeitig zur Wahl standen, ist unsicher
    ("ich glaube es waren mehrere").
    EINSCHRAENKUNG, damit spaeter niemand mehr annimmt als belegt ist: im
    aktuellen `dalamud.log` (Session bis 12.08. 23:45, danach kein Neustart)
    steht KEINE einzige `[Loot] Zeile N von M ... lootSlot=X`-Zeile - die
    letzten `[Loot]`-Eintraege sind vom alten 20:37-20:45-Test. Das Log ist
    `_log.Info`, also nicht Debug-gated; es fehlt also der Papierbeleg.
    Damit bleibt die OFFENE MESSUNG von unten (Zeilenreihenfolge im Fenster ==
    Slot-Reihenfolge in `Loot`?) weiter offen - sie braucht eine Verlosung mit
    MEHREREN Gegenstaenden gleichzeitig und den Log-Vergleich rowIndex/lootSlot.

## FRUEHER AM TAG (2026-08-12, "AUSWAHLLISTEN IN DER KONFIGURATION SAGTEN IMMER DENSELBEN WERT")

>>> BEFUND (Dump + Log ConfigCharaOpeTarget "Zieleinstellungen", 21:31-21:32):
    Die SCHALTER des Fensters gehen einwandfrei ("Bei Kommando automatisch zum
    Ziel hinwenden, Schalter, an"). Die AUSWAHLLISTEN dagegen sagten bei jedem
    Schritt denselben Satz: "Art des automatischen Anvisierens, Auswahlliste,
    Direkte Sichtlinie." - egal auf welcher Option der Cursor stand. Im Log ist
    das eindeutig: der Fokuszeiger wechselt (0x...FB880 / 0x...4B6A710), die
    ConfigProbe daneben meldet abwechselnd 'Direkte Sichtlinie' und 'Naechster
    Gegner', gesprochen wurde jedes Mal derselbe Satz (21:31:48 bis 21:32:08).

>>> URSACHE: `AnnounceConfigGlobalFocus` bildet fuer eine DropDownList immer
    Label + `SelectedItemIndex` - also den GESPEICHERTEN Wert. Beim Blaettern in
    der offenen Liste sitzt der Fokus aber auf einer Zeile, und die wurde nie
    gelesen. Der Spieler konnte also nicht hoeren, was er gerade auswaehlt.

>>> ERSTER ANLAUF GING INS LEERE (Log 21:38, nach Hot-Reload der neuen DLL um
    21:36:53 - der Build lag um 21:36:50 vor, es lief also wirklich die neue
    Fassung): die Ansage kam weiterhin unveraendert. Grund: geaendert war
    `AnnounceConfigGlobalFocus` (Praefix `[CS]`), aber im ganzen Log steht keine
    einzige `[CS]`-Zeile - fuer die ConfigChara*-PANELS baut
    `TryReadConfigPanelControl` den Satz. Die Logik sitzt jetzt in
    `DescribeDropDown` und wird von BEIDEN Lesern benutzt.

>>> GEBAUT: `FindDropDownFocusRow` stellt ueber die Besitzverhaeltnisse fest, ob
    der Fokusknoten in einem ItemRenderer der Liste liegt (geschlossen sitzt er
    auf der eingebetteten CheckBox, offen auf einer Zeile - im Dump und im Log
    sauber unterscheidbar). Ist es eine Zeile, wird sie angesagt:
    "Naechster Gegner, 2 von 2" bzw. beim gespeicherten Wert "Direkte
    Sichtlinie, 1 von 2, ausgewaehlt". Sonst bleibt die alte Ansage.

>>> DASS DER GESPEICHERTE WERT BEIM BLAETTERN STEHEN BLEIBT, ist gemessen, nicht
    vermutet: `ReadConfigControlValue` liest genau `SelectedItemIndex`, und der
    lieferte waehrend des ganzen Blaetterns unveraendert "Direkte Sichtlinie".
    Darum taugt er als Markierung fuer "ausgewaehlt".

>>> NICHT BEURTEILT, weil im Test nicht angefasst: der Regler
    "Cursor-Geschwindigkeit (Freier Modus)" und die beiden Auswahlknoepfe unter
    "Einstellung fuer Gegner anvisieren". Beide stehen im Dump, ob sie sauber
    gelesen werden, zeigt erst ein Durchgang darueber.

>>> IM SPIEL BESTAETIGT vom User (2026-08-12, nach dem zweiten Anlauf): das
    Blaettern durch eine offene Auswahlliste nennt jetzt die Option.

>>> Build Debug 0 Warnungen / 0 Fehler, nach devPlugins deployt. Zweig test/prs.

## FRUEHER AM TAG (2026-08-12, "WUERFELN UM BEUTE - DIE ZEILEN SAGEN JETZT DEN GEGENSTAND")

>>> BEFUND AUS DEM DUNGEON-TEST DES USERS (Dump + Log 2026-08-12, 20:37-20:45):
    Was schon lief: der LootRollService hat alle sieben Verlosungen erkannt und
    angesagt (Log `[Loot] Neue Verlosung: slot=0 item=2684 'Gepluenderte
    Sturmhaube' x1 state=UpToGreed time=299,3`), und die Knoepfe "Bedarf",
    "Gier", "Passen" werden beim Navigieren gelesen.
    Was fehlte: beim Wechsel zwischen den LISTENZEILEN kam jedes Mal nur "0".
    Ursache im Dump belegt - die Zeilen (ListItemRenderer, Node 1008) enthalten
    keinen Namen, jeder Textknoten darin ist leer bis auf den Wurfwert. Der
    Gegenstandsname steht nur einmal im Fenster, im TextNineGrid Node 5, und
    dort in Item-Verweis-Bytes verpackt ("H'I(Gepluenderte SturmhaubeIH").

>>> GEBAUT: `LootRollService.DescribeRollRow(idx, out dedupKey)` liest die Zeile
    nicht aus Knoten, sondern aus der Tabelle, aus der das Fenster selbst
    zeichnet: `AddonNeedGreed.Items` - 16 `LootItemInfo` mit ItemName, ItemId,
    IconId, Roll, ItemCount, dazu `NumItems` und `SelectedItemIndex`
    (ilspycmd 2026-08-12). Gesprochen wird Name (+ Anzahl bei Stapeln), was
    laut RollState moeglich ist, und die Restzeit:
    "Gepluenderte Sturmhaube, nur Gier oder Passen moeglich, noch 252 Sekunden".

>>> WARUM DIE OPTIONEN DAZU GEHOEREN: im Test hat der User bei einem Gegenstand
    Bedarf gedrueckt und erst NACH dem Druck die Absage gehoert ("Du besitzt
    diesen Gegenstand bereits", Log 20:38:07). Der Knopf bleibt sichtbar, das
    Spiel weist erst den Klick zurueck - die Auskunft muss also aus dem
    Spielzustand kommen, nicht aus dem Fenster.

>>> OFFENE MESSUNG (bewusst nicht geraten): Ob Zeilenreihenfolge im Fenster
    gleich Slot-Reihenfolge in `Loot` ist, laesst sich aus den Strukturen NICHT
    beweisen - `LootItem` hat nur ChestObjectId/ChestItemIndex, `LootItemInfo`
    gar kein Slot-Feld. Darum wird der Slot ueber die ItemId gesucht, mit
    Vorrang fuer den gleichnummerigen Slot, und `[Loot] Zeile N von M: ...
    lootSlot=X` schreibt beide Nummern ins Log. Die naechste echte Verlosung mit
    mehreren Gegenstaenden klaert es.

>>> Die Restzeit ist absichtlich NICHT Teil des Wiederhol-Schutzes: die Sekunden
    aendern sich staendig, und der Cursor flackert laut Log mehrmals pro Sekunde
    zwischen den Zeilen. Der Schutz vergleicht darum ItemId + Anzahl.

>>> Build Debug 0 Warnungen / 0 Fehler, nach devPlugins deployt.
    ACHTUNG: gebaut auf dem Zweig `test/prs`, der auch die fuenf Test-Merges
    enthaelt - der In-Game-Test laeuft also mit beidem gleichzeitig.

## TESTZWEIG `test/prs` (2026-08-12) - FUENF FREMD-BEITRAEGE ZUM ANHOEREN

>>> WAS DAS IST: Die fuenf offenen Pull Requests von bladestorm360, alle auf
    einem lokalen Zweig zusammengefuehrt und als Debug nach devPlugins gebaut.
    NICHT auf main, nichts released. Der Zweig existiert nur, damit der User sie
    im Spiel hoeren kann, bevor entschieden wird.
    - PR #1 Gegnerstufe + HP beim Blaettern, eigene HP wieder als ZAHL
    - PR #2 Aktions-Tooltip nennt die Form der Wirkflaeche (Kreis/Kegel/Linie)
    - PR #3 Objekt-Kategorien "Verbuendete" und "Inhalte"
    - PR #4 Charaktererstellung, Schritt Aussehen (17.454 Zeilen)
    - PR #5 Chat-Puffer folgen den Spielregistern (loescht ChatChannelService)

>>> ZWEI PUNKTE DREHEN BESTAETIGTE ENTSCHEIDUNGEN UM - das ist die eigentliche
    Frage an den User, nicht die Technik:
    1. PR #1 macht die EIGENEN HP wieder zu "X von Y". Prozent war eine
       ausdrueckliche Entscheidung vom 2026-08-07, und das Format ist in V5.31
       schon einmal unbemerkt gekippt. Ziel-HP und MP bleiben Prozent.
    2. PR #5 loescht `ChatChannelService` - damit faellt weg, dass ENTER im
       Nachlese-Kanal antwortet (v5.67, in-game bestaetigt). Der Autor begruendet
       das: seine Puffer sind Empfangsfilter, daraus folgt kein Sendekanal.

>>> MERGE-LAGE: PR 1-3 fusionieren sauber. Zwei Konflikte von Hand geloest, beide
    trivial und beide Seiten behalten: `UIReaderService` Feldblock (ActionShape +
    CharaMake), `Plugin.cs` Konstruktor (CharaMake-Ctor + Chat-Puffer-Block, ohne
    die von PR #5 geloeschte `_chatChannel`-Zeile). Debug-Build 0 Warnungen /
    0 Fehler, 10 Dateien nach devPlugins deployt.
    Gegengeprueft, dass nichts aus V5.82 verloren ging: Spur-Strings,
    NPCDialogue-Kanaele und der Doppelungs-Schutz sind im Merge vorhanden.
    Tastenkollisionen geprueft: die vier neuen Kombis von PR #5 sind im Plugin
    nirgends sonst belegt (`KeyReadHotbar` ist Strg+F9, nicht Umschalt+F9).

>>> HOERBARE KENNUNG: Die Versionsansage beim Laden sagt "5.82 Testfassung mit
    fuenf Beitraegen". Kommt stattdessen die blanke "5.82", laeuft die
    veroeffentlichte Fassung - dann hat das Spiel devPlugins nicht geladen.

>>> ZURUECK AUF DIE VEROEFFENTLICHTE FASSUNG: `git checkout main` und Debug neu
    bauen, dann liegt wieder 5.82 in devPlugins. Einzelne Beitraege lassen sich
    zurueckdrehen, weil jeder ein eigener Merge-Commit auf `test/prs` ist.

>>> KEINER DER FUENF IST VOM AUTOR IM SPIEL GETESTET. Er schreibt das bei jedem
    selbst dazu; er spielt ebenfalls blind. Alle fuenf kompilieren sauber.

## FRUEHER (2026-08-10, V5.82 - SPUREN SELBST ABLAUFEN)

>>> DAS FEATURE: Eine Luecke im Wegenetz einmal selbst ablaufen, danach kennt
    der Auto-Lauf sie. Der Spieler muss die Stelle nicht SEHEN, er muss sie
    GEHEN - das ist der ganze Kniff.
    - Strg+Umschalt+F6 startet die Aufzeichnung, dieselbe Taste beendet sie.
      Waehrenddessen wird alle 2 m ein Punkt mitgeschrieben.
    - Gespeichert wird in der Plugin-Konfiguration (`Configuration.Trails`),
      pro Gebiet, mit automatischem Namen ("Verbindung 1").
    - `/acc trails` listet die Spuren des Gebiets auf, `/acc trail del <nr>`
      loescht eine.
    - Der Auto-Lauf greift NICHT beim Start darauf zu, sondern erst dort, wo er
      ohnehin feststellt "hier endet das Netz" (alle drei Stellen: keine
      Annaeherung, keine Bewegung mit restWp<=1, Pfad zu Ende). Passt eine Spur
      (Einstieg <= 15 m, bringt >= 10 m naeher), sagt er "Hier endet das
      Wegenetz, ich nehme Verbindung 1" und faehrt sie ab. Danach laeuft der
      normale Lauf weiter.

>>> WARUM AUFZEICHNEN STATT SUCHEN: Genau das gab es schon (NavmeshCacheService,
    V5.77) und der User liess es in V5.78 zurueckbauen - die Automatik hat zu
    oft falsch geraten und ihn einmal auf einem Plateau eingesperrt. Eine Spur,
    die der Spieler GELAUFEN ist, ist keine Schaetzung.

>>> DIE EINBAHN-FALLE IST MITBEDACHT: Beim Speichern wird die Hoehenspanne der
    ganzen Spur gemessen. Bleibt sie unter 1,5 m, gilt die Spur in beide
    Richtungen. Sonst nur in Laufrichtung, und das wird ANGESAGT ("Achtung,
    diese Spur ueberwindet 12 Meter Hoehe ... fuer den Rueckweg zeichne bitte
    eine eigene Spur auf"). Grund: die Figur laeuft Absaetze hinunter, aber
    nicht hinauf - genau daran ist die alte Automatik gescheitert.

>>> DEKOMPILIERTER FUND, DER DEN BAU BESTIMMT HAT: `Path.MoveTo` faehrt zwar
    eine feste Punktliste ohne Wegsuche - ABER kommt die Figur 500 ms nicht vom
    Fleck, wirft `FollowPath` unsere Liste weg und routet normal zum LETZTEN
    Punkt (`OnStuck` -> `AsyncMoveRequest.MoveTo`, weil `RetryOnStuck` beim User
    an ist). Ueber eine Luecke, die das Netz nicht kennt, wird daraus wieder ein
    Phantompfad. Deshalb ueberwacht `TrailWalkingUpdate` zwei Dinge: die
    Wegpunktzahl darf nur FALLEN (eine Neuberechnung ueber die Zone liefert
    mehr), und `PathfindInProgress` muss falsch bleiben (unsere Etappe rechnet
    nie). Trifft eines zu: ehrlich abbrechen statt weiterzudriften.

>>> Build Debug 0 Warnungen / 0 Fehler. Version 5.82.

>>> RELEASED als v5.82 (2026-08-10, 21:15). Verifiziert: v5.82 ist "Latest",
    4 Assets dran, `releases/latest/download/latest.zip` liefert 651.549 Bytes
    (= der neue Build), repo.json auf main steht auf 5.82.0.0.

>>> IM SPIEL BESTAETIGT (User, 2026-08-12): "das mit dem wegenetz funktioniert
    erstmal alles". Damit ist der gesamte Wegenetz-Block dieser Version
    abgehakt - Spur-Aufzeichnung (Strg+Umschalt+F6), `/acc trails`, das
    Aufgreifen der Spur am Netzende und der Weiterlauf danach. Keine offenen
    Wegenetz-Tests mehr.

>>> NOCH NICHT GEBAUT, bewusst: Die GEHHILFE nutzt die Spuren nicht - sie fuehrt
    an der Kante weiter in Luftlinie. Sinnvoll waere, die Spurpunkte dort als
    Wegpunkte anzusagen; das ist ein eigener Schritt.

>>> NPC-DIALOGE IM KAMPF - URSACHE GEFUNDEN UND BEHOBEN (User-Meldung
    2026-08-10, seine Vermutung "das sind wohl NPC-Chats" war richtig):
    `ChatReaderService.ShouldRead` kannte die beiden Kanaele gar nicht, sie
    fielen auf `_ => false` und wurden lautlos verworfen. Werte per ilspycmd aus
    Dalamud bestaetigt: `NPCDialogue = 61`, `NPCDialogueAnnouncements = 68`.
    Das _BattleTalk-FENSTER war laengst angebunden - der Chat-Weg derselben Rede
    nie. Gebaut: beide Kanaele lesen (neues Flag `ReadNpcDialogue`, Standard an),
    Nachlese-Kategorie "Dialoge" statt "System", kein Kanal-Wort davor (der
    Sprechername reicht: "Y'shtola: ..." statt "Chat von Y'shtola: ...").
    Doppelt gelesen wird nichts: der vorhandene Echo-Schutz
    (`WasRecentlySpoken`, 6 s) faengt es ab, wenn das Fenster denselben Satz
    schon gesprochen hat.
    IM SPIEL BESTAETIGT (Log 2026-08-10, 21:01-21:03, V5.82): alle 8
    NPCDialogue-Zeilen kamen mit `gelesen=True` an und wurden als
    "Wheiskaet: Wie kann ich euch helfen? ..." gesprochen. Der Kanal war also
    wirklich die Ursache.

>>> ABER: JEDE ZEILE KAM DOPPELT (in derselben Messung gefunden). Erst das
    Talk-Fenster ("[Speak] INT 'Kapitaen: Dies ist die Faehre...'" um
    21:01:28.852), dann die Chat-Zeile mit demselben Wortlaut um 21:01:34.344.
    URSACHE: Der Echo-Schutz im ChatReader prueft den BLANKEN Text ohne
    Sprechernamen, gespeichert war aber nur "Kapitaen: <Text>" - kein Treffer.
    Genau der Fall, fuer den `RememberSpokenVariant` existiert; der Talk-Leser
    hat ihn nur nie benutzt.
    GEBAUT (V5.82, zwei Teile):
    (a) Der Talk/_BattleTalk-Leser meldet den Wortlaut OHNE Namen zusaetzlich per
        `RememberSpokenVariant`.
    (b) Neue Liste im TolkService fuer "hat eine ANDERE Quelle schon gesagt"
        (`WasSpokenElsewhere`, 180 s Aufbewahrung), und der ChatReader prueft
        NPC-Dialoge dagegen mit 120 s Fenster.
    WARUM NICHT EINFACH DAS ALLGEMEINE FENSTER VERGROESSERN: der Abstand
    Fenster->Chat betrug gemessen 2,5 bis 5,5 s und waechst mit der Lesezeit des
    Spielers, ein 6-s-Fenster reicht also nicht. Ein langes Fenster auf der
    ALLGEMEINEN Historie wuerde aber auch einen Boss verschlucken, der dieselbe
    Warnung zweimal ruft - und genau das darf einem blinden Spieler nicht
    passieren. Die getrennte Liste trifft nur Fremdquellen-Wiederholungen.
    PUNKT (a) IM SPIEL BESTAETIGT (User + Log 21:11:18-28): Die drei
    Wheiskaet-Zeilen der Tauglichkeitspruefung wurden je EINMAL gesprochen -
    aus dem Fenster - und die nachfolgende Chat-Zeile erzeugte keine zweite
    Ansage mehr.
    OFFEN bleibt (b): ein Kampf-Ruf OHNE Textfenster. Das ist genau der Fall,
    um den es dem User urspruenglich ging, und er ist noch nicht getestet.

## FRUEHER (2026-08-10, V5.81 - GEHHILFE ERKENNT DAS NETZENDE)

>>> LOG-AUSWERTUNG DER SITZUNG 19:29-19:40 (dalamud.log), drei Ergebnisse:
    1. NETZ WURDE ECHT NEU GEBAUT (19:30:31-19:30:58, Fortschritt 20/40/60/80 %)
       mit vnavmesh 1.2.3.13.
    2. DIE TRENNUNG BESTEHT TROTZDEM. Lauf zu "Infame Informanten": Start
       (-17|70|-1), 24 Wegpunkte, 508 m produktiv bis (443|76|4), dann
       "keine Annaeherung seit 2,5 s bei restWp=1, dist=469,3 - Netz endet hier".
       Damit ist die letzte offene Frage beantwortet: NEUBAU + NEUE VNAVMESH-
       FASSUNG SCHLIESSEN DIE LUECKE IN OESTLICHEM LA NOSCEA NICHT. Plateau
       (Y 76) und Kueste bleiben getrennt, wie aus den Recast-Grenzen erwartet.
       Damit sind alle drei Erklaerungsversuche durch: Cache widerlegt, Neubau
       widerlegt, Version widerlegt. Bleibt nur eine NavmeshCustomization fuer
       Gebiet 135 - ungeprueft, ob das der Aufwand wert ist.
    3. AUTO-LAUF AN DER NETZKANTE ARBEITET WIE ENTWORFEN (19:32:21):
       "Pfad besteht nur aus dem angehaengten Ziel - keine Routen-Vorschau",
       2,3 s spaeter die ehrliche Absage. Kein Schieben mehr. V5.80 bestaetigt.

>>> NEUER BEFUND, IN V5.81 BEHOBEN: DIE GEHHILFE HATTE DIE V5.80-PRUEFUNGEN
    NICHT. Sie ist ein eigener Codepfad (NavigationService, "Gehhilfe"-Block),
    und im Log kam dort beides zurueck, was beim Auto-Lauf behoben ist:
    - Sie sagte die Phantom-Route an: "Weg zu Infame Informanten, 466 Meter:
      466 Meter nach Sueden" (19:32:31), obwohl die Route nur aus dem
      angehaengten Ziel bestand.
    - Danach 30 s lang alle 5 s "0,5 Kilometer, geradeaus, abwaerts" bei
      unveraendert dist=469,5 und wp=1/2 - sie merkte das Netzende nicht und
      schickte den Spieler weiter gegen die Kante (19:32:36-19:32:56).

>>> GEBAUT IN V5.81 (beides in NavigationService):
    (a) Besteht die Route nach dem Vorruecken nur noch aus dem angehaengten
        Ziel, wird keine Routen-Vorschau gesprochen (RouteIsOnlyAppendedDestination).
        Bewusst STILL statt "kein Weg gefunden": auf freiem Gelaende sieht eine
        echte Gerade genauso aus, und eine falsche Absage waere schlimmer als
        Schweigen. Die Wahrheit kommt aus (b), sobald der Spieler laeuft.
    (b) CheckMeshEnd: nur noch das angehaengte Ziel als Wegpunkt, Ziel weiter
        als Ankunftsreichweite+20 m, SPIELER BEWEGT SICH GERADE (Fenster 1 s)
        und ist seit 5 s nicht naeher gekommen -> einmalige Ansage
        GuideMeshEndsHere, danach Luftlinien-Fuehrung.

>>> ZWEI BEWUSSTE ABWEICHUNGEN VOM AUTO-LAUF, jeweils begruendet:
    - Die Gehhilfe wird NICHT beendet. Der Auto-Lauf muss stoppen, weil er die
      Figur steuert; die Gehhilfe steuert nichts. Abschalten haette dem Spieler
      nur die Fuehrung genommen, obwohl er sich selbst einen Weg suchen kann.
    - Das Zeitfenster ist 5 s statt 2,5 s, und die Pruefung greift nur waehrend
      echter Bewegung. Stillstand beweist beim manuellen Laufen nichts (Kampf,
      Menue), und ein Mensch dreht und tastet sich beim Laufen.
    - Nach dem Netzende bleibt die langsame 5-s-Ansage statt der 2-s-Luftlinien-
      Kadenz: bei 469 m Rest waere dieselbe Zeile alle 2 s reine Belastung.

>>> Build Debug 0 Warnungen / 0 Fehler, 10 Dateien deployt. Version 5.81.

>>> RELEASED als v5.81 (2026-08-10, 20:29). Verifiziert: v5.81 ist "Latest",
    4 Assets dran, `releases/latest/download/latest.zip` liefert 645.298 Bytes
    (= der neue Build), repo.json auf main steht auf 5.81.0.0. WICHTIG: repo.json
    hing auf 5.77 fest - 5.78, 5.79 und 5.80 wurden nie veroeffentlicht, Spieler
    steckten also drei Fassungen zurueck. Mit 5.81 ist das nachgezogen.
    ACHTUNG: veroeffentlicht auf ausdruecklichen Wunsch OHNE In-Game-Test; die
    drei Testpunkte unten stehen weiterhin aus.

>>> FLIEGEN ALS DAUERLOESUNG FUER NETZLUECKEN - RECHERCHIERT (2026-08-10):
    Fliegen scheitert NICHT am Reittier (in FF14 fliegt jedes Reittier) und
    NICHT an Aetherstroemen (die gibt es in ARR-Gebieten gar nicht - Fliegen kam
    dort erst mit Patch 5.3 dazu). Einzige Bedingung ist der Abschluss der
    2.0-Hauptstory ("Die ultimative Waffe", Stufe 50); danach ist Fliegen in
    La Noscea, Thanalan, Schwarzem Schilfguertel, Coerthas und Mor Dhona frei.
    Der User ist bei Story-Stufe 30 -> derzeit keine Option, aber sie kommt
    allein durchs Weiterspielen. Dann waere der Hoehenbruch in Oestlichem
    La Noscea gegenstandslos.
    OFFEN dafuer: ob vnavmesh fuer Gebiet 135 ueberhaupt ein Flugvolumen baut
    (`IsFlyingSupported` verlangt TerritoryIntendedUse 1, 47 oder 49 - fuer 135
    NICHT nachgesehen).

>>> KEIN FORK NIMMT UNS DIE ARBEIT AB (GitHub-API, 2026-08-10): vnavmesh hat 38
    handgemachte Zonen-Anpassungen bei ueber tausend Gebieten - 134 (Mittleres
    La Noscea) hat eine, 135 nicht. Von den 51 Forks hat KEINER mehr als 38
    (aethergel 36, lilasrepo 36, Lunarisnia 36, alydevs 37, Jaksuhn 34,
    HoshinoCorp 32), alle sind 9-64 Commits hinterher. Einzige echte
    Weiterentwicklung ist AtmoOmen/ffxiv_navmesh-cn (356 Commits voraus), aber
    das ist die Fassung fuer den chinesischen Client, unbrauchbar fuer Global.
    Aufschlussreich sind deren Commit-Titel: "Sprung/Fall standardmaessig
    einschalten" wurde kurz darauf WIEDER ZURUECKGENOMMEN - dort hat also jemand
    genau das versucht, was bei uns als naechster Schritt notiert war.

>>> NAECHSTES FEATURE, vom User beauftragt: SPUR AUFZEICHNEN. Taste startet die
    Aufzeichnung, der Spieler laeuft die Luecke einmal selbst ab (Gehhilfe fuehrt
    ihn seit V5.81 auch dort in Luftlinie), Taste beendet und benennt die
    Verbindung. Danach nutzt der Auto-Lauf sie automatisch IN BEIDE RICHTUNGEN,
    wenn das Netz an der Kante endet. Bewusst keine Automatik-Suche wie in V5.78
    - genau die hat falsch geraten und den Spieler eingesperrt.

>>> ZU TESTEN (in-game noch UNGEPRUEFT):
    1. Gehhilfe auf ein Ziel jenseits der Netzkante (z. B. wieder "Infame
       Informanten" von oben): beim Start darf KEINE "466 Meter nach Sueden"-
       Route mehr kommen.
    2. Dann losgehen bis zur Kante und weiterlaufen: nach etwa 5 s Laufen ohne
       Annaeherung soll einmal kommen "Hier endet der begehbare Weg. Noch X
       Meter nach Sueden, ich fuehre ab jetzt in Luftlinie."
    3. GEGENPROBE, wichtig: normale Gehhilfe zu einem erreichbaren Ziel - die
       Routen-Vorschau muss weiterhin kommen, und unterwegs stehenbleiben
       (Kampf, Menue) darf die Netzende-Ansage NICHT ausloesen.

## FRUEHER (2026-08-10, V5.80 - IN-GAME VERIFIZIERT + NACHSCHLIFF)

>>> V5.79 IM SPIEL BESTAETIGT (Log 2026-08-10, 18:25-18:32). Alle drei Fixes
    greifen nachweislich:
    - Der Stopp haelt: nach dem Ende um 18:30:46 kam KEIN weiterer vnavmesh-
      Auftrag mehr (vorher 91 in einer Minute). Das lautlose Weiterschieben ist
      weg.
    - Kein Fehlabbruch: der Lauf zu "Infame Informanten" (18:26:33) lief 106 s
      durch, bis der User selbst stoppte. Vorher stieg er nach Millisekunden aus.
    - Ankunft: "Ziel erreicht: Weinhafen" bei dist=2,5.
    - Ehrliche Ansage statt Luege: "Weiter komme ich nicht, hier endet der
      begehbare Weg. Noch 413 Meter nach Osten." An genau der Stelle, wo vorher
      "praktisch am Ziel" behauptet wurde.

>>> RESTPROBLEM AUS DEM TEST, in V5.80 behoben:
    Stand der Spieler schon an der Netzkante, sagte das Plugin trotzdem
    "Weg zu Freibriefe der Sonnenkueste, 411 Meter: 411 Meter nach Osten" -
    eine Route, die nur aus dem angehaengten Wunschziel bestand. Danach wurde
    4 bis 12 Sekunden gegen den Fels gedrueckt, bis die Stillstandspruefung
    ansprang (sie wird durch das Rutschen bei jedem Retry immer wieder
    zurueckgesetzt).
    GEBAUT: (a) besteht die "Route" nur aus dem angehaengten Ziel, wird sie gar
    nicht erst angesagt; (b) neue Pruefung "keine Annaeherung": nur noch ein
    Wegpunkt uebrig, Ziel weiter als stopRange+20 m weg, und 2,5 s lang nicht
    naeher gekommen -> Netz endet hier. Das Kriterium ist die Annaeherung, nicht
    die Bewegung, deshalb taeuscht das Rutschen es nicht.
    Absichtlich NICHT gebaut: eine Verweigerung beim Start. Der Lauf zu "Infame
    Informanten" hat ebenfalls ein Netzende ~466 m vor dem Ziel und brachte den
    Spieler trotzdem ueber 500 m weit. Genau dieses vorschnelle Verweigern war
    es, was der User in 5.78 zurueckbauen liess.

>>> WARUM DAS NETZ NICHT ALLE WEGE KENNT (Frage des Users, jetzt beantwortet
    und in docs/game-api.md dokumentiert): vnavmesh berechnet das Netz SELBST
    mit Recast aus der Kollisionsgeometrie - es kommt nicht vom Spiel. Grenzen
    laut `Navmesh.NavmeshSettings`: max. 55 Grad Steigung, max. 0,5 m Absatz,
    und `GenerateEdgeClimbLinks` ist standardmaessig AUS, es gibt also keine
    "hier kann man runterspringen"-Verbindungen. Alles, was man nur durch
    Springen oder ueber einen steilen Hang erreicht, existiert im Netz nicht.
    Deshalb zerfaellt Oestliches La Noscea in Plateau (Y 59-76) und Kueste
    (Y 17-20).
    NAECHSTE SCHRITTE dafuer, in dieser Reihenfolge: `/vnav rebuild` in der Zone
    (Cache ist vom 02.08.), danach ggf. `GenerateEdgeClimbLinks` einschalten und
    neu bauen (ob das die Luecke schliesst, ist UNGEPRUEFT).

>>> Build Debug 0 Warnungen / 0 Fehler, 10 Dateien deployt. Version 5.80.
>>> V5.80 IN-GAME BESTAETIGT (Log 18:48-18:53): "keine Annaeherung seit 2,5 s
    bei restWp=1" greift genau wie entworfen, statt 12 s Schieben. Der Lauf um
    18:48:44 lief 83 s produktiv, bevor das Netzende kam.

>>> CACHE-THEORIE WIDERLEGT (2026-08-10 abends): Der User hat `/vnav rebuild`
    ausgefuehrt. Die Cache-Datei s1f3 wurde nachweislich neu geschrieben
    (18:48:27), die Trennung besteht trotzdem — Lauf endete wieder bei 413,5 m,
    "Landpomeranzen" bei 485,9 m. Das Netz wird REPRODUZIERBAR getrennt gebaut.
    Ursache ist also die Geometrie plus die Recast-Grenzen, nicht eine kaputte
    Datei. Die Datei liegt gesichert in `meshcache_backup_20260810`.

>>> VNAVMESH AKTUALISIERT: 1.2.3.10 (25.07.) -> 1.2.3.13 (05.08.), verifiziert
    (Assembly-Version 1.2.3.13, DLL 448 statt 439 KB). Alte Fassung gesichert in
    `devPlugins\vnavmesh_backup_1.2.3.10`. Neu darin u.a. "Better road pathing in
    western thanalan", "Link limsa ship interior stairs", "add pathfindavoid" —
    fuer Oestliches La Noscea ist NICHTS dabei, eine Besserung dort ist also
    nicht zu erwarten. Wichtig ist das Muster: solche Luecken werden bei
    vnavmesh durch handgemachte Zonen-Anpassungen (`NavmeshCustomization`)
    behoben, und fuer Gebiet 135 existiert keine.
    HINWEIS: Downloads/Installationen von Fremd-Plugins blockiert der
    Sicherheitsfilter dieser Sitzung — der Kopierschritt muss vom User selbst
    ausgefuehrt werden (`! cp -r <stage> <devPlugins\vnavmesh>`).

>>> ZU TESTEN, NAECHSTE SITZUNG:
    1. Einloggen, Oestliches La Noscea betreten: das Netz wird neu gebaut (Cache
       entfernt + neue vnavmesh-Version). Auf "Wegenetz fertig geladen" warten.
    2. Lauf zur Sonnenkueste: kommt er jetzt durch, oder wieder Stopp bei ~413 m?
    3. An der Netzkante Numpad3: keine Route-Ansage, Absage nach gut 2 s.

## FRUEHER (2026-08-10, V5.79 "AUTO-LAUF KOMPLETT NEU AUF VNAVMESH")

>>> USER-AUFTRAG: "mach das mit dem wegenetz einfach komplett neu mit dem
    vnavmesh" - nachdem der Rueckbau auf 5.73 die Symptome NICHT behoben hat
    ("er bleibt manchmal mitten auf der straeke und laeuft obwohl er
    stehenbleiben sollte").

>>> URSACHE, AUS DEM LOG BEWIESEN (dalamud.log 2026-08-10, 07:54-08:06):
    Es lag NICHT an unserer zurueckgebauten Logik, sondern daran, dass das
    Plugin vnavmesh falsch abgefragt hat. Drei dekompilierte Fakten (jetzt in
    docs/game-api.md -> "Wie vnavmesh Pfade wirklich startet und beendet"):
    1. `MoveTo` ist asynchron und stoppt den laufenden Pfad NICHT. Direkt nach
       dem Start beschreibt `Path.IsRunning` noch den VORIGEN Lauf.
       Beleg 08:05:05: Auftrag "Weinhafen", 52 ms spaeter "beendet, noch 499 m" -
       und vnavmesh steuerte danach 50 m weit los, ohne Aufsicht.
    2. vnavmesh startet sich selbst neu (`StopOnStuck` + `RetryOnStuck`, beide
       beim User an). `Path.IsRunning` blinkt dadurch jede Sekunde auf false,
       ohne dass der Lauf zu Ende ist. Beleg 08:04:24-08:05:55: 91 "Queueing
       move-to" im Sekundentakt, waehrend das Plugin schon ausgeklinkt war -
       eine Minute lautloses Schieben gegen die Netzkante.
    3. Der letzte Wegpunkt ist frei erfunden: `PathfindMesh` haengt das
       Wunschziel unbedingt an, erreichbar oder nicht. Beleg 08:04:23:
       `restWp=1 distNextWp=453,8`.
    Beide Symptome des Users sind derselbe Mechanismus: das Plugin hielt den
    Selbst-Neustart fuer das Ende ("bleibt stehen"), klinkte sich aus OHNE
    `Path.Stop` zu rufen, und vnavmesh lief weiter ("laeuft, obwohl er
    stehenbleiben sollte").

>>> NEU GEBAUT:
    - `Services/NavmeshIpc.cs` (NEU): alle vnavmesh-Gates an einer Stelle,
      einmal gekapselt. `LastCallFailed` trennt "vnavmesh fehlt" von
      "vnavmesh sagt nein". Die Lauflogik enthaelt kein try-catch mehr.
    - `Services/AutoWalkService.cs` neu geschrieben, oeffentliche API
      unveraendert (Plugin.cs blieb unangetastet). Zustandsmaschine
      Idle -> Starting -> Walking -> Guarding:
      * Vor jedem Start `Path.Stop` - toetet einen laufenden Retry-Zyklus und
        macht die Statusabfragen eindeutig.
      * `Starting` urteilt ueber gar nichts, bis der eigene Pfad wirklich steht.
      * Ankunft entscheidet die ENTFERNUNG, nicht "vnavmesh ist still".
      * Pfadende erst nach 1,6 s durchgehender Stille (Entprellung gegen den
        Sekundentakt des Stuck-Retry).
      * Stillstand 4 s: ist nur noch EIN Wegpunkt uebrig, endet das Netz dort -
        eigene, ehrliche Ansage statt "festgesteckt".
      * Jeder Ausgang ruft `Path.Stop`. Danach `Guarding` (3 s): belebt ein
        Task-in-flight oder der Retry den Lauf wieder, wird erneut gestoppt.
      * "Du bist schon bei X" statt eines Laufs ueber 1,5 m.
    - `RouteService.DescribeRoute` nimmt jetzt die Spielerposition entgegen und
      zaehlt die Strecke bis zum ersten Wegpunkt mit. Vorher wurden 454 m als
      "praktisch am Ziel" angesagt (Log 08:04:45).
    - Neue bilinguale Strings: `WalkMeshEndsHere`, `AlreadyAtTarget`.

>>> Build Debug 0 Warnungen / 0 Fehler, 10 Dateien deployt. Version 5.79
    (die Ansage beim Login sagt die Versionsnummer - so ist hoerbar, dass der
    neue Stand laeuft; das Log vom 10.08. zeigte noch V5.77, weil die DLL eine
    Minute vor dem Versions-Commit gebaut worden war).

>>> ZU TESTEN, IN DIESER REIHENFOLGE:
    1. Einloggen: sagt er "Version 5.79 bereit"? Sonst laeuft die alte DLL.
    2. Irgendein normaler Lauf zu einem NPC: geht er los, kommt er an, sagt er
       "Ziel erreicht"?
    3. Zweimal Numpad3 hintereinander: stoppt der zweite Druck wirklich, und
       bleibt die Figur dann auch stehen?
    4. Der Sonnenkueste-Fall in Oestlichem La Noscea: er sollte die ~238 m
       laufen und dann sagen "Weiter komme ich nicht, hier endet der begehbare
       Weg. Noch 454 Meter nach Osten." - und danach STILLSTEHEN, nicht weiter
       gegen die Kante druecken.
    5. Wenn der Lauf an einem NPC oder Stein haengenbleibt: nach 4 s "Ich stecke
       fest" - und dann Ruhe.

>>> NICHT GELOEST, BEWUSST: dass das Wegenetz von Oestlichem La Noscea in zwei
    unverbundene Haelften zerfaellt. Das ist ein Fehler in vnavmeshs Cache
    (Datei vom 02.08.2026), nicht in unserem Plugin - dorthin fuehrt schlicht
    kein Weg, den man lesen koennte. Der naechste Versuch waere `/vnav rebuild`
    in der Zone (baut das Netz ohne Cache neu). Das Plugin sagt den Zustand
    jetzt wenigstens ehrlich an, statt stumm zu schieben.

## FRUEHER (2026-08-09, "WEGENETZ ZURUECK AUF 5.73" - AUF ANSAGE DES USERS)

>>> USER-ENTSCHEIDUNG: "mach das alles rueckgaengig, in v5.73 hat alles was das
    wegenetz angeht funktioniert, unsere extra sachen haben irgendwas kaputt
    gemacht."

>>> WELCHER STAND GENAU: ein Tag v5.73 gibt es nicht (Releases springen von
    v5.72 auf v5.74). Die PLUGIN-Version 5.73 endete mit Commit db04160, und
    dessen Release v5.74 hat den `AutoWalkService` NICHT angefasst - der
    Wegenetz-Stand von 5.73 und 5.74 ist also nachweislich identisch
    (`git diff db04160^ v5.74 -- AutoWalkService.cs` = leer). Damit ist v5.74
    der saubere Bezugspunkt.

>>> ZURUECKGEBAUT (seit v5.74 waren 2.460 Zeilen dazugekommen):
    - `AutoWalkService.cs` per `git checkout v5.74 --` auf den alten Stand.
      Weg sind damit: die Pruefung "Ziel haengt an einer anderen Flaeche"
      (RouteIsWalkable), die Uebergangs-Suche, die Zugangssuche mit Ringgitter,
      die Umleitungen (falsche Etage / near miss), der Durchlauf durch
      Zonengrenzen, `/acc zugang`, `/acc netz`, `/acc planke`, `/acc boden`.
    - `NavmeshCacheService.cs` GELOESCHT (Cache-Analyse, Flaechen-Flood-Fill).
    - `ZoneExitService.cs` GELOESCHT (echte Zonengrenzen, `/acc uebergang`).
    - `Plugin.cs`: alle Aufrufe, Konstanten und Befehle dazu entfernt.
    - `AccessibilityStrings`: die verwaisten Texte entfernt.

>>> EINZIGE ABWEICHUNG VOM ALTEN STAND, und sie ist erzwungen:
    `AccessibilityStrings.Unnamed` existiert nicht mehr (das Objektnamen-Feature
    aus v5.75 hat es durch `UnnamedOfKind` ersetzt, und DAS wird nicht
    zurueckgebaut). Eine Zeile im Folgen-Code nutzt jetzt `UnnamedOfKind`.
    `git diff v5.74 -- AutoWalkService.cs` zeigt genau diese eine Zeile.

>>> WAS DAMIT AUCH WEG IST - bewusst, der User hat den Rueckbau angeordnet:
    die Ueberquerung abgetrennter Flaechen (war in-game bestaetigt), der
    Durchlauf durch Zonenuebergaenge (nie getestet), die reparierte
    Wegenetz-Fortschrittsansage und `/acc netz`. Die letzten beiden sind reine
    Ansage-Fixes ohne Einfluss aufs Laufen und koennen auf Wunsch einzeln
    wieder drauf - das Wissen dazu steht in docs/game-api.md und in der
    Historie unter v5.76/v5.77.

>>> Build Debug 0 Warnungen / 0 Fehler, 10 Dateien deployt. Version 5.78.

>>> ZU TESTEN: genau die Faelle, die vorher scheiterten - der Lauf zur
    Sonnenkueste in Oestlichem La Noscea und der Quest-Marker 'Infame
    Informanten'. Erwartung nach dem Rueckbau: der Auto-Lauf laeuft den
    vnavmesh-Pfad, ohne vorher zu urteilen.

## FRUEHER (2026-08-09, "WEGENETZ-ANSAGE WAR TOT" - URSACHE BEWIESEN, IN 5.77 GEFIXT, MIT DEM RUECKBAU WIEDER RAUS)

>>> USER-MELDUNG: "ich bin auf einer map wo er angeblich keine wege findet."

>>> BEWIESENE URSACHE - STARTREIHENFOLGE (Log 2026-08-09):
      21:31:11,472 [WRN] Wegenetz-Fortschritt nicht lesbar - Ueberwachung aus.
                   IpcNotReadyError: IPC method vnavmesh.Nav.BuildProgress
                   was not registered yet
      21:31:12,358 [INF] Finished loading vnavmesh
    Das Plugin fragt die vnavmesh-IPC 0,9 SEKUNDEN zu frueh ab, faengt den
    Fehler, setzt `_meshMonitorOff = true` - und schaltet die Ansage damit fuer
    die GANZE SITZUNG ab. vnavmesh ist eine Sekunde spaeter da, aber es fragt
    nie wieder nach.
    FOLGE: der Spieler hoert NIE "Wegenetz wird geladen / 40 % / fertig". Genau
    diese Ansage unterscheidet "Netz baut noch" von "es gibt wirklich keinen
    Weg". Der Kommentar ueber der Methode sagt das selbst ("no way to tell
    'still loading' from 'broken'") - die Funktion war seit dem Bau wirkungslos,
    sobald das Plugin vor vnavmesh geladen wurde.

>>> GEFIXT: `IpcNotReadyError` schaltet die Ueberwachung NICHT mehr ab, sondern
    wird alle 5 s neu versucht (einmal geloggt, nicht pro Frame). Nur andere
    Ausnahmen schalten weiterhin hart ab. Kommt die IPC hoch, steht
    "Wegenetz-Ueberwachung laeuft wieder" im Log.

>>> NEU `/acc netz` (bzw. `/acc mesh`): sagt auf Zuruf, ob das Netz fertig ist,
    gerade gebaut wird (mit Prozent) oder ob vnavmesh fehlt. Damit laesst sich
    die Frage jederzeit selbst klaeren, statt auf eine Ansage zu warten.

>>> ZWEITE URSACHE, DAVON UNABHAENGIG - UND SIE IST DIE EIGENTLICHE (Log
    21:41-21:42, zweiter Versuch des Spielers):
      [Netz] ffxiv_sea_s1_fld_s1f3_level_s1f3__11F3E____0.navmesh:
             Spieler 0,10 m vom Netz.
      [Netz] Spielerflaeche 9434 Polygone, Zielflaeche 10860 - getrennt.
      [Netz] Die beiden Flaechen kommen sich nirgends auf 6 m nahe genug.
    DAS NETZ IST FERTIG - der Cache liess sich lesen, die Polygone zaehlen, und
    der Spieler steht 0,10 m davon entfernt. Die zunaechst geaeusserte Vermutung
    "Netz baut noch" ist damit WIDERLEGT.
    Oestliches La Noscea (s1f3) zerfaellt im gecachten Wegenetz in ZWEI GROSSE,
    unverbundene Flaechen: die Spielerseite oben (Weinhafen, Y~70) und die
    Ostkueste unten (Costa del Sol, Y~19). BEIDE Ziele des Spielers
    ('Infame Informanten' <560,4|20,8|455,9> und Aetheryt Sonnenkueste
    <490,5|19,0|466,6>) liegen auf der zweiten. Zu Fuss kommt man dort
    selbstverstaendlich hin - das Netz ist falsch, nicht die Welt.
    Die Cache-Datei ist vom 02.08.2026.

>>> DAS PLUGIN VERHAELT SICH RICHTIG: es erkennt die Trennung und verweigert
    einen sinnlosen 692-m-Lauf. vnavmesh selbst lieferte einen "Pfad" mit 21
    Wegpunkten, dessen LETZTER Sprung 452 m Luftlinie betrug - ohne die
    Trennungspruefung waere der Lauf angetreten und gescheitert.

>>> DER SPIELER HATTE RECHT - DAS PLUGIN STAND IM WEG (Nachtrag, Code gelesen):
    Beim Befund "haengt an einer anderen Flaeche" ging Fall 3 SOFORT in die
    Uebergangs- und Zugangssuche und rief `Stop()`. Der Lauf wurde also gar
    nicht erst angetreten - obwohl vnavmesh einen Weg mit 21 Wegpunkten und
    rund 500 m Laenge geliefert hatte, der 238 m naeher ans Ziel fuehrt
    (690 m -> 452 m). Die Grenze `NearMissGap` = 15 m entscheidet ueber
    "fahrbare Restluecke", wurde hier aber zur Entscheidung ueber "ueberhaupt
    losgehen" gemacht. Bei einer Zone, deren Netz in zwei Haelften zerfaellt,
    heisst das: kein einziger Schritt.

>>> GEBAUT: Fall 3 laeuft den vnavmesh-Weg jetzt, wenn er echten Fortschritt
    bringt (`MinUsefulProgress` = 20 m, gemessen als Entfernung-zum-Ziel vorher
    minus nachher). Erst wenn der Weg NICHTS bringt, kommen die Suchen - genau
    der Fall vom ersten Versuch um 21:32, wo der Pfad 0,7 m neben dem Spieler
    endete und 466 m vom Ziel. Das eine Kriterium trennt beide Faelle sauber.
    Dazu: `FinishNearMiss` prueft den Boden nicht mehr bei Resten ueber 15 m -
    das waere bei 452 m ein IPC-Aufruf pro Meter fuer eine schon bekannte
    Antwort. Stattdessen direkt die Ansage "noch N Meter nach <Richtung>".

>>> WEITERHIN SINNVOLL FUER DEN SPIELER: `/vnav rebuild` - verifiziert aus
    vnavmesh.dll (Navmesh.Plugin, Hilfetext: "rebuild current territory's
    navmesh from scratch"). Baut das Netz der aktuellen Zone neu und ersetzt den
    alten Cache. Wenn danach immer noch getrennt, ist es eine echte Grenze des
    Netzbaus, und dann waere die Offline-Analyse des Caches der naechste Schritt
    (vgl. Astalicia-Untersuchung).

>>> IN-GAME BESTAETIGT (User 2026-08-09): "laufen geht erstmal wieder".
    Der Auto-Lauf tritt den vnavmesh-Weg wieder an.

>>> RELEASE v5.77 RAUS (2026-08-09 20:00 UTC). Versionen an allen drei Stellen
    auf 5.77 / 5.77.0.0, vier Assets dran, Installer unveraendert
    wiederverwendet (SHA passt zu installer.json). VERIFIZIERT: v5.77 ist
    "Latest", und releases/latest/download/latest.zip liefert HTTP 200 mit
    668.543 Bytes - exakt die neu gebaute Datei.

>>> NOCH OFFEN AN DIESEM FALL: ob nach `/vnav rebuild` die beiden Flaechen von
    Oestlichem La Noscea zusammenhaengen. Bleibt die Trennung, laeuft der
    Spieler kuenftig den grossen Teil der Strecke und bekommt den Rest angesagt -
    aber die letzten ~450 m muessen von Hand oder per Aetheryt ueberbrueckt
    werden. Dann waere die Offline-Analyse des Caches der naechste Schritt.

## FRUEHER (2026-08-09, "KARTENUEBERGAENGE" - GEMESSEN, GEBAUT, ALS v5.76 RELEASED)

>>> RELEASE v5.76 RAUS (2026-08-09 18:41 UTC). Versionen an allen drei Stellen
    auf 5.76 / 5.76.0.0 (Plugin.cs, csproj, repo.json). Vier Assets am Release,
    Installer-exe unveraendert wiederverwendet (SHA
    5787445B...CAD49 stimmt mit installer.json ueberein).
    VERIFIZIERT: `gh release list` zeigt v5.76 als "Latest", und
    releases/latest/download/latest.zip liefert HTTP 200 mit 667.834 Bytes -
    exakt die neu gebaute Datei. Spieler ziehen also wirklich die neue Version.

>>> ACHTUNG: v5.76 enthaelt FUENF in-game ungetestete Neuerungen (Uebergaenge,
    Beute auswuerfeln, Auf-/Absteigen, Begleiter-Verzeichnis, tote
    Sammelpunkte). Der User hat das Release bewusst vor dem Test gewollt. Die
    Testpunkte unten gelten unveraendert weiter - kommt eine Rueckmeldung aus
    der Spielerschaft, zuerst dort nachsehen.

>>> MESSUNG GELAUFEN (Log 18:01:57 Territory 130 Nald-Kreuzgang, 18:02:26
    Territory 131 Thal-Kreuzgang - der Spieler ist zwischen beiden Laeufen
    durchgegangen). ALLE DREI FRAGEN BEANTWORTET:

>>> 1. DIE URSACHE, SCHWARZ AUF WEISS (Log 18:01:47.805):
      "Auto-Lauf: Pfad beendet, dist=0,4, angekommen=True.
       Ich <93,1, 4,0, -109,7> Ziel <93,5, 4,2, -109,5>"
       -> "Ziel erreicht: Uebergang nach Thal-Kreuzgang."
    Die ECHTE Grenze (key=2377064) liegt bei (98,89 | 8,21 | -105,41).
    Der Spieler stand also 6,77 m WAAGERECHT daneben und 4,16 m TIEFER.
    Er stand nicht schief - er stand woanders. Das Kartensymbol war das Ziel.

>>> 2. PlayerRunningDirection IST RADIANT. Beweis ueber alle 10 gemessenen
    Werte: als Radiant gelesen ergeben sie glatte 5-Grad-Vielfache
    (15 / 45 / 45 / 75 / 90 / 165 / 195 / 225 / 230 / 270), als Grad gelesen
    strukturlose Bruchteile (0,3 / 0,8 / 0,8 / 1,3 / 1,6 / 2,9 / ...).
    Level-Designer setzen Winkel in 5-Grad-Schritten, nicht in 0,8-Grad.

>>> 3. DIE RICHTUNG ZEIGT IN DIE NEUE ZONE. Zwei Partner-Paare beweisen es
    geometrisch (Partner erkennbar an ueberkreuzten Dest/Return-Ids):
      key 2377082 (Ul'dah-Seite, X=-115,58) -> 90 Grad = +X
      key 2379246 (Kreuzgang-Seite, X=-114,31, also 1,27 m weiter in +X) -> 270 Grad = -X
    Jede Seite zeigt zur jeweils anderen Zone. Gleiches Bild bei 2377078/2379249.
    Dazu der eigene Durchgang: Bewegung von (93,13|-109,66) nach
    (100,28|-99,78) = Richtung 36 Grad, die Box sagt 45 Grad.
    Konvention = dieselbe wie player.Rotation, atan2(dx, dz).

>>> 4. Scale IST HALB-AUSDEHNUNG (Hinweis, kein Beweis): Box-Mitte Y=8,21 bei
    Y-Scale 4,29. Als Halb-Ausdehnung reicht die Box von 3,92 bis 12,50 - der
    begehbare Boden bei Y~4 liegt gerade drin. Als Vollgroesse waere sie
    6,06-10,36, also KOMPLETT UEBER dem Boden, durch den man laeuft.
    Der XZ-Achsentest der Sonde taugt hier nicht: die Box ist um 50 Grad gedreht.

>>> 5. NICHT JEDES KARTENSYMBOL HAT EINE ECHTE GRENZE. In Ul'dah 10 Symbole
    gegen 7 Grenzen. Ohne Entsprechung: 'Die Sanduhr' (37 m zur naechsten Box),
    'Wachstube der Legion' (91 m), ein 'Thal-Kreuzgang' bei (-30|-41,5) (21 m).
    Das sind Tueren / Instanz-Eingaenge - die funktionieren NICHT ueber
    Durchlaufen. Fuer die muss das heutige Verhalten bleiben.
    Wo es eine echte Grenze gibt, liegt das Symbol 0,27 bis 6,77 m daneben.

>>> GEBAUT (Umbau, in-game UNGETESTET):
    - `ZoneExitService.FindExitForMap(zielMap, symbolPos)`: ordnet dem
      Kartensymbol die echte Grenze zu. ZWEI Bedingungen, beide noetig - die
      Grenze muss zur genannten Karte fuehren UND innerhalb 15 m liegen. Die
      Schwelle trennt die gemessenen Gruppen sauber (echte Paare 0,27-6,77 m,
      Symbole ohne Grenze 21 / 37 / 91 m).
    - `ZoneExitService.PointBeyond(...)`: Punkt jenseits der Grenze,
      Richtungsvektor (sin, 0, cos) aus RunningDirection.
    - `Plugin.TryResolveMarkerDestination`: bei Uebergaengen zielt der Lauf
      jetzt auf die Grenze (X/Z von der Box, HOEHE weiter per Navmesh - die
      Box-Mitte liegt im Luftraum darueber). stopRange 3 m statt 0,5 m: die
      Ankunft ist nur die erste Etappe, eine enge Schwelle haette die zweite
      nie ausgeloest.
    - `AutoWalkService._zoneExitPush` + `StartZoneExitPush`: nach der Ankunft
      12 m durch die Grenze, ohne Wegsuche (Path.MoveTo), abgesichert durch
      `GroundIsContinuous` wie die anderen Blindfahrten. Die vorhandene
      `FinalHopUpdate` erkennt den Zonenwechsel bereits und meldet ihn.
    - Ohne passende Grenze (Tueren, Instanz-Eingaenge) bleibt ALLES beim Alten.

>>> BEWUSST GEWAEHLT, NICHT GEMESSEN: die 12 m Durchlaufstrecke. Die Boxen
    haben Halb-Ausdehnungen von 2,77 bis 15,56 m; 12 m deckt die meisten von
    der Mitte aus. Ein Zuviel ist harmlos, weil die Fahrt beim Zonenwechsel
    sofort endet - ein Zuwenig waere der alte Fehler.

>>> BEKANNTE GRENZE: braucht der Lauf zur Grenze eine Umleitung (Pfad endet
    kurz davor, falsches Stockwerk), setzt `BeginWalk` den Durchlauf zurueck
    und es bleibt beim alten Verhalten. Kein Rueckschritt, aber auch keine
    Loesung fuer diesen Fall.

>>> Build Debug 0 Warnungen / 0 Fehler, 10 Dateien deployt.

>>> ZU TESTEN:
    1. Genau der Uebergang von vorhin (Ul'dah -> Thal-Kreuzgang): im Objekt-
       Browser waehlen, Numpad 3. Die Figur muss diesmal DURCHLAUFEN und
       "Neues Gebiet erreicht" kommen - nicht "Ziel erreicht".
    2. Ein Uebergang, der eine TUER ist ('Die Sanduhr', 'Wachstube der
       Legion'): muss sich verhalten wie bisher, also bis zum Symbol laufen
       und dort halten. Log: "keine Grenze innerhalb 15 m vom Symbol".
    3. Eine Freiluft-Zonengrenze (Ul'dah -> Zentrales Thanalan): dort sind die
       Boxen groesser (Ausdehnung bis 15,56), also der eigentliche Test fuer
       die 12-m-Strecke.
    LOG-BELEG: "[Uebergang] Karte N: echte Grenze key=... Laufrichtung N Grad",
    dann "[Uebergang] Durchlauf: fahre N m ohne Wegsuche durch die Grenze",
    dann "Gebiet gewechselt".

## FRUEHER AM TAG (2026-08-09, "KARTENUEBERGAENGE" - SONDE GEBAUT)

>>> USER-FRAGE: "kann man dafuer sorgen das kartenuebergaenge so angelaufen
    werden das der char gleich in die neue map geht ich bin jetzt wieder an
    einem uebergang wo ich nicht rueberkomme weil ich evtl schief stehe."

>>> URSACHE, AUS DEM CODE BELEGT: der Auto-Lauf zielt auf das KARTENSYMBOL des
    Uebergangs - MapMarker-Sheet, Karten-Pixel, umgerechnet in Weltkoordinaten
    (`PlacesService.cs:260`), Stopp 0,5 m davor
    (`Configuration.AutoWalkTransitionStopRange`). Ein Kartensymbol ist Grafik:
    es hat KEINE Ausdehnung und KEINE Richtung. Der Lauf endet also NEBEN der
    Grenze statt HINDURCH. Der Spieler steht richtig - das Ziel war falsch.

>>> DAS SPIEL FUEHRT DIE ECHTE GRENZE (ilspycmd 2026-08-09, in game-api.md
    dokumentiert): `ExitRangeLayoutInstance`, `InstanceType.ExitRange = 41`.
      Transform@64 (geerbt) - Mitte UND Ausdehnung der Trigger-Box
      PlayerRunningDirection (float @148) - eine Richtung durch den Uebergang
      TerritoryType (ushort @134) - die Zielzone
      ExitType - ZoneLine=1 (durchlaufen) / Invisible=2
    Zugriff: LayoutWorld.Instance()->ActiveLayout->Layers -> LayerManager.Instances,
    gefiltert auf Id.Type == ExitRange.

>>> GEBAUT: neuer `ZoneExitService`.
    - `ReadExitRanges()` liest alle Uebergaenge der Zone (spaeter das Laufziel).
    - `/acc uebergang` (bzw. `/acc exitprobe`, nur Debug): misst drei Dinge -
      1. Abstand Kartensymbol <-> echte Grenze je Uebergang. DAS ist die Zahl,
         die den Fehlschlag erklaert.
      2. Entfernung/Peilung des Spielers zu jeder Grenze, plus ob er nach
         Scale-halb bzw. Scale-voll INNERHALB der Box steht.
      3. PlayerRunningDirection roh, als Radiant gelesen und als Grad gelesen,
         daneben Box-Yaw und Spieler-Rotation.
    - Ansage (bilingual): Anzahl Uebergaenge + naechster mit Entfernung.

>>> BEWUSST NOCH NICHT GEBAUT: der Umbau des Auto-Laufs. Was
    `PlayerRunningDirection` bedeutet (Einheit, Bezugssystem, welche der beiden
    Richtungen) und ob `Scale` Halb- oder Vollausdehnung ist, ist NICHT
    gemessen. Eine falsch gedeutete Laufrichtung wuerde die Figur von der
    Grenze WEG steuern - schlimmer als der heutige Zustand.

>>> Build Debug 0 Warnungen / 0 Fehler, 10 Dateien deployt.

>>> ZU TESTEN (am besten direkt an dem Uebergang, an dem es klemmt):
    1. Direkt an der Stelle stehen, wo der Auto-Lauf "Ziel erreicht" gemeldet
       hat, dann `/acc uebergang`. Die Ansage nennt Anzahl + naechsten.
    2. Ein paar Schritte machen, bis der Zonenwechsel kommt - VOR dem Wechsel
       nochmal `/acc uebergang`. Aus den zwei Messungen ergibt sich, wo die
       Linie wirklich liegt und aus welcher Richtung man sie nimmt.
    3. Log auf "[ExitProbe]" ansehen, besonders die Zeile "ABSTAND=... m"
       (Kartensymbol gegen echte Grenze) und "RunningDirection roh=...".

## STAND JETZT (2026-08-09, "UEBERQUERUNG ZU ABGETRENNTEN FLAECHEN" - IN-GAME BESTAETIGT)

>>> USER-MELDUNG: "ich muss fuer eine quest zu einem objekt aber er bleibt 23
    meter vorher stehen und laeuft nicht direkt hin."

>>> URSACHE, GEMESSEN (Log 16:03:45 + Offline-Analyse des vnavmesh-Caches):
    Das Ziel (Quest-Sammelpunkt "Natuerlicher Magnet", Westliches Thanalan) liegt
    auf einer im Wegenetz ABGETRENNTEN Flaeche - 29 Polygone gegen 17.570 der
    Spielerflaeche, Ueberschneidung NULL. Am Ziel liegt sehr wohl Netz (0,37 m
    neben der Objektposition), es fehlt nur die Verknuepfung. Die Zugangssuche
    lieferte daraufhin einen Punkt 2 m vom Spieler, 23,8 m vom Ziel - und meldete
    "Ziel erreicht".

>>> GEBAUT UND IN-GAME BESTAETIGT (beide Magnete angelaufen):
    - NEU `NavmeshCacheService`: liest die gecachte .navmesh-Datei per Reflection
      und beantwortet per Flood-Fill ueber Polygon-Links, ob zwei Stellen zur
      selben Flaeche gehoeren. Datei ueber `TerritoryType.Bg` gefunden.
    - Uebergang in drei Etappen im `AutoWalkService`: normaler Lauf zur Stelle,
      `Path.MoveTo` ueber die Luecke, dann normale Wegsuche weiter.
    - `FindWayOff`: Ausweg von einer kleinen Flaeche, auf der man sonst FESTSITZT
      (genau das passierte um 16:57 - "0 von 57 Kandidaten erreichbar").
    - `MidPointIsSafe` sichert die blind gefahrene Strecke gegen Loecher.
    - Zugangspunkt meldet keine falsche Ankunft mehr.

>>> GRENZEN, BEWUSST: HINUNTER bis 5 m geht (Fallen), HINAUF nur 1 m - die Figur
    klettert nicht und verkeilt sich an einer Stufe. Luecke bis 6 m, gemessen
    zwischen Polygon-MITTELPUNKTEN (die echte Luecke ist kleiner).

>>> OFFEN: Nur in Westliches Thanalan erprobt. Ohne Cache-Datei fuer eine Zone
    faellt alles still auf das alte Verhalten zurueck.

## FRUEHER (2026-08-09, "TOTE SAMMELPUNKTE IM BROWSER" - GEMESSEN + GEBAUT)

>>> USER-MELDUNG: "Gaertner (Abholzen), Stufe 5 1, 21 Meter, hinter links, 1 von
    21. und da kann ich nicht abbauen aber warum? es gibt welche wo ich abbauen
    kann und wo nicht."

>>> URSACHE, GEMESSEN (Sonde [GatherProbe], Log 12:56-12:58): der Browser bot
    Baeume an, an denen NICHTS steht. Von 16 gelisteten Sammelpunkten an einer
    Stelle war GENAU EINER nutzbar:
      nutzbar : id=4000018B, 9 m, Anvisierbar=True, TargetableStatus=123
                (Bit ObjectTargetableFlags.IsTargetable=2 gesetzt),
                RenderFlags=None -> danach "Laufe zu Nutzbaum", "Ziel erreicht"
      tot     : alle uebrigen, Anvisierbar=False, TargetableStatus=248 bzw. 120
                (Bit fehlt), RenderFlags=128 -> weder gezeichnet noch ansprechbar
    ENTFERNUNG IST AUSGESCHLOSSEN: zwei der toten wurden aus 2,2 m bzw. 2,3 m
    gemessen und blieben tot. Der Spieler stand direkt davor.
    Das Spiel fuehrt also jede moegliche Platzierung einer Gegend als Objekt mit
    und hebt immer nur eine Handvoll davon an.

>>> WARUM ES DURCHRUTSCHTE: `IsWorthBrowsing` liess ObjectKind.GatheringPoint als
    EINZIGE Objektart bedingungslos durch - fuer alles andere galt "hat einen
    Namen ODER ist anvisierbar". Sammelpunkte haben keinen eigenen Namen, deshalb
    war die Ausnahme urspruenglich noetig; sie hat aber gleich die Anvisierbarkeit
    mit abgeschaltet.

>>> GEBAUT: Sammelpunkte muessen jetzt dieselbe Frage bestehen wie jedes andere
    Objekt - das Spiel muss sie anvisierbar melden. USER-ENTSCHEIDUNG war
    ausdruecklich "das was der spieler auch sieht ... nicht dass es als cheaten
    gezaehlt wird": der Filter NIMMT Information weg, er fuegt keine hinzu.

>>> NOCH NICHT GEMESSEN, UND DESHALB UEBERWACHT: ob ein LEBENDER Punkt auch aus
    60-80 m schon als anvisierbar gemeldet wird. Tut er das nicht, versteckt der
    Filter brauchbare Punkte vor der Suche. Die Sonde
    `TrackGatheringAvailability` loggt jeden Zustandswechsel mit Entfernung
    ("[GatherProbe] Wechsel: ... nutzbar=True ... Entfernung=..."), also faellt
    im normalen Spielen auf, ab welcher Entfernung ein Punkt umspringt.

>>> Build Debug 0 Warnungen / 0 Fehler, 10 Dateien deployt.

>>> ZU TESTEN:
    1. Objekt-Browser, Kategorie Sammelpunkte: die Zahl muss deutlich kleiner
       sein als vorher (statt "16 in der Naehe" nur noch die echten).
    2. Zu einem gelisteten Punkt laufen: da MUSS abbaubar sein.
    3. Log auf "[GatherProbe] Wechsel: ... nutzbar=True" ansehen - steht dort
       eine grosse Entfernung, ist die Fernsuche in Ordnung. Kommt der Wechsel
       erst bei wenigen Metern, muss der Filter umgebaut werden.
    4. Meldet die Kategorie oft "keine in Reichweite", obwohl Baeume da sind ->
       sofort melden, dann ist der Filter zu scharf.

## STAND JETZT (2026-08-09, "BEUTE AUSWUERFELN" - TEIL 1 GEBAUT, UNGETESTET)

>>> USER-MELDUNG: "in dungeons muss man wenn man in der gruppe loot bekommt das
    auswuerfeln, da popt ein fenster auf und das muss fuer uns fokussiert werden
    so dass wir das dann auslesen koennen."

>>> ENTSCHEIDENDER BEFUND: DAS FENSTER MUSS GAR NICHT GELESEN WERDEN. Der ganze
    Zustand liegt im Spiel selbst - `Client.Game.UI.Loot` (ilspycmd-verifiziert
    2026-08-09), erreichbar ueber `Loot.Instance()`:
      Items = 16x LootItem, SelectedIndex
      LootItem: ItemId, ItemCount, RollState, RollResult, RollValue,
                Time, MaxTime, LootMode
      RollState  (byte): UpToNeed=0, UpToGreed=1, UpToPass=2, Rolled=17,
                         Unavailable=21  -> kumulativ: was DU noch tun darfst
      RollResult:        UnAwarded=0, Needed=1, Greeded=2, Passed=5,
                         Awarded=6     -> was du schon getan hast
      LootMode:          Normal, GreedOnly, Unavailable, LootMasterGreedOnly
    `AddonNeedGreed` selbst traegt KEINE eigenen Datenfelder (nur AtkUnitBase +
    VirtualTable) - UI-Scraping haette hier also nur Umwege gebracht.
    ⇒ Die Ansage ist unabhaengig davon, ob das Fenster Fokus hat, gescrollt ist
    oder ueberhaupt offen ist. Das Fokus-Problem entfaellt damit fuer das LESEN.

>>> GEBAUT (Teil 1, Lesen): neuer `LootRollService`.
    - Automatische Ansage, sobald eine Verlosung aufgeht: "Verlosung: <Name>
      mal N. Bedarf, Gier oder Passen moeglich." Nur fuer Eintraege, bei denen
      der Spieler ueberhaupt noch handeln darf (RollState UpToNeed/Greed/Pass) -
      eine schon abgehandelte Zeile ist keine Neuigkeit.
    - Neue Taste **Umschalt+F7**: liest alle offenen Verlosungen vor, mit Name,
      Anzahl, was noch moeglich ist und was man selbst schon gewuerfelt hat
      ("du hast Bedarf gewuerfelt, 87"). Sagt auch, wenn gerade nichts laeuft.
    - Dubletten-Schutz ueber (Slot-Index + ItemId): das Spiel verwendet Slots
      wieder, die Id allein oder der Index allein wuerde die zweite Verlosung
      im selben Slot verschlucken.
    - Schalter Configuration.AnnounceLootRolls, Taste KeyReadLootRolls.

>>> BEWUSST NOCH NICHT ANGESAGT: die Restzeit. `Time` und `MaxTime` sind
    verifizierte Felder, aber WELCHES davon herunterzaehlt, ist nicht gemessen -
    und bei einem Timer waere eine falsche Ansage schlimmer als keine. Beide
    Werte stehen in JEDER Log-Zeile ("time=... maxTime=..."), der erste Dungeon
    klaert es also sofort, dann kommt die Ansage nach.

>>> TEIL 2 (Bedienen) - USER-KORREKTUR, UND SIE FUEHRT ZUM BESSEREN WEG:
    "das sollte funktionieren, da geht dann ein menue auf, deswegen soll ja das
    fenster mit dem menue fokussiert werden damit man da rein kommt."
    Das Plugin soll die Knoepfe also NICHT selbst druecken - der Spieler will
    mit der spieleigenen Cursor-Navigation (Nummernblock) ins Fenster und dort
    auswaehlen. Das ist genau die Playability-Regel: mit der Spielmechanik
    arbeiten, nicht daneben.
    GEFUNDEN (ilspycmd 2026-08-09, AtkUnitBase): `Focus()` als eigene Methode
    des Fensters, dazu `SetFocusNode(node, setCursorFocusNode, focusParam)` und
    die Felder `FocusNode`, `CursorTarget`, `ComponentFocusNode`.
    GEBAUT: neue Taste **Umschalt+F8** holt das NeedGreed-Fenster in den Fokus
    (`AtkUnitBase.Focus()`), danach navigiert der Spieler wie in jedem anderen
    Menue. BEWUSST NICHT automatisch beim Aufgehen: ein Fenster, das sich mitten
    im Kampf den Fokus greift, schluckt den Nummernblock, waehrend man noch
    laufen muss.
    NOCH NICHT BELEGT: ob `Focus()` ALLEIN reicht, damit die Tastatur im Fenster
    landet, oder ob zusaetzlich ein Startknoten gesetzt werden muss
    (`SetFocusNode`). Deshalb loggt die Taste die drei Fokus-Felder VOR und NACH
    dem Aufruf ("[Loot] Vor Focus(): ... / Nach Focus(): ..."). Bleiben sie
    unveraendert bzw. reagiert die Navigation nicht, ist SetFocusNode mit dem
    ersten Knopf der naechste Schritt - dann steht im Log schon, was fehlt.

>>> Build Debug 0 Warnungen / 0 Fehler, 10 Dateien deployt.

>>> ZU TESTEN:
    1. Dungeon, Gruppe, Beute faellt: die Ansage muss von selbst kommen.
    2. Umschalt+F7 waehrenddessen: die Liste muss stimmen.
    3. **Umschalt+F8**: Fenster in den Fokus holen, dann mit Nummernblock
       navigieren und Bedarf/Gier/Passen waehlen. DAS ist der eigentliche Test.
    4. Nach dem eigenen Wurf erneut Umschalt+F7: der eigene Wurf muss genannt
       werden.
    5. Log auf "time=/maxTime=" ansehen - daraus ergibt sich die Restzeit-Ansage.
    6. Log auf "[Loot] Vor/Nach Focus()" ansehen - zeigt, ob Focus() reicht.

## STAND JETZT (2026-08-09, "AUF- UND ABSTEIGEN" - GEBAUT, UNGETESTET)

>>> USER-FRAGE: "wir brauchen noch eine taste um auf reittiere auf und absteigen
    zu koennen oder gibts das schon?"

>>> BEFUND: DAS SPIEL HAT DAFUER KEINE TASTE. Im Live-Tastenbelegungs-Dump
    (679 Eintraege, 2026-08-09 07:44) gibt es keine Mount-Aktion; das
    naechstliegende `MOVE_DESCENT` (Strg+SPACE) ist Sinkflug, nicht Absteigen.

>>> ABER DAS SPIEL FUEHRT BEIDES ALS AKTION (GeneralAction-Sheet, offline
    ausgelesen 2026-08-09):
      #9  'Reittier-Roulette'   #23 'Absteigen'
      #24 'Flugreittier-Roulette'   #10 'Begleiter-Roulette'
    Dieselbe Sorte wie #4 Sprint, #7 Teleport, #8 Rueckfuehrung. Ein sehender
    Spieler zieht sie aus dem Aktionsfenster auf die Leiste - einen anderen Weg
    gibt es im Spiel nicht. Dazu kommen 366 einzeln belegbare Reittiere.

>>> ENTSCHEIDUNG (User zugestimmt): KEINE eigene Mod-Taste, sondern zwei weitere
    Listen im Zuweisungs-Menue (Strg+Numpad0). Grund: eine Mod-Taste muesste den
    Ruf selbst ausloesen und alles nachbauen, was das Spiel drumherum prueft
    (Zone, Kampf, Flugfreigabe). Ueber die Leiste macht das Spiel das selbst.
    Das Menue hat damit FUENF Listen: Skills, Gegenstaende, Quest-Gegenstaende,
    Allgemeine Aktionen, Reittiere. Numpad 6 vor, Numpad 4 zurueck.

>>> FILTER, BEIDE VOM SPIEL BEANTWORTET, NICHT GERATEN:
    - Allgemeine Aktionen: `UnlockLink` == 0 oder
      `UIState.IsUnlockLinkUnlockedOrQuestCompleted` == true - derselbe Aufruf,
      den die Skill-Liste schon benutzt.
      BEWUSST NICHT nach `UIPriority` gefiltert: genau die gewuenschten
      Eintraege ('Absteigen' #23, 'Flugreittier-Roulette' #24) haben Prioritaet
      0, diese Spalte haette sie also weggeworfen.
    - Reittiere: `PlayerState.IsMountUnlocked(rowId)` (ilspycmd-verifiziert
      2026-08-09). Ohne diese Frage haette die Liste 366 Eintraege, von denen
      dem Spieler die meisten nicht gehoeren.
    Sortierung jeweils alphabetisch - eine durchblaetterte Liste muss
    vorhersagbar sein, und die Sheet-Reihenfolge ist wegen Prioritaet 0
    unbrauchbar.

>>> Belegt wird ueber HotbarSlotType.GeneralAction bzw. .Mount, durch denselben
    gemessenen Pfad wie alles andere (PlaceOnSlot: Set + WriteSavedSlot +
    LoadSavedHotbar, Read-back nach 2 Frames). Leiste vorlesen benennt beide
    Typen jetzt ueber ihr eigenes Sheet.

>>> Build Debug 0 Warnungen / 0 Fehler, 10 Dateien deployt.

>>> ZU TESTEN:
    1. Strg+Numpad0, dann mit Numpad 6 durchsteppen bis "Allgemeine Aktionen":
       'Absteigen' und 'Reittier-Roulette' muessen in der Liste stehen.
    2. Beide auf zwei freie Leistentasten legen, Leiste vorlesen: die Namen
       muessen stimmen.
    3. Im Spiel: Reittier-Roulette-Taste = aufsteigen, Absteigen-Taste = runter.
    4. Liste "Reittiere": nur die eigenen, nicht alle 366.
    LOG-BELEG: "[Hotbar] Allgemeine Aktionen: N (...)" und
    "[Hotbar] Freigeschaltete Reittiere: N (...)".

## STAND JETZT (2026-08-09, "BEGLEITER-VERZEICHNIS" - GEBAUT, UNGETESTET)

>>> USER-MELDUNG: "schau dir mal den dump an das ist das begleiter verzeichnis
    eigentlich sollte da einer drin stehen."

>>> DER DUMP BEWEIST, DASS DIE BEGLEITER DA SIND (FFXIV_UI_Dump.txt,
    2026-08-09 09:57:27, MinionNoteBook, 79 Nodes):
      [23] id=58 Text V "Gesamt: 2"
    Das Fenster zaehlt also selbst zwei Begleiter. Das Plugin hat sie nur nie
    genannt.

>>> URSACHE: das Fenster fuehrt UEBERHAUPT KEINEN Namen als Text. Im ganzen
    Dump gibt es genau vier lesbare Texte: der Titel "BEGLEITER" (id=3/id=65),
    das Favoriten-Kaestchen (id=2), sein Hinweis (id=72) und "Gesamt: 2"
    (id=58). Jeder Begleiter ist eine reine Icon-Kachel
    (Comp(1017) CT=DragDrop -> Kind id=2 Comp(1019) CT=Icon). Deshalb kam beim
    Blaettern nur "Leer" bzw. der Zeichenzaehler "0/40" des Suchfelds
    (id=17/id=21) heraus.
    Die [ListProbe]-Zeile "Len=0" ist kein Fehler: dieses Fenster hat gar keine
    AtkComponentList, genau wie das Reittier-Verzeichnis.

>>> LOESUNG: dasselbe Muster wie beim Reittier-Verzeichnis (V5.53, in-game
    bestaetigt). Neu `TryReadMinionNoteBookFocusRow`: Icon-Id der fokussierten
    Kachel -> Name aus dem Companion-Sheet (Spalte `Singular`, es gibt KEINE
    Spalte `Name`). Suchfeld wird benannt statt "0/40" vorzulesen.
    EINDEUTIGKEIT GEMESSEN (offline Sheet-Dump 2026-08-09): 589 benannte
    Companion-Zeilen, 589 verschiedene Icon-Ids, NULL Kollisionen - die
    Zuordnung Icon -> Begleiter ist also unmissverstaendlich.

>>> Build Debug 0 Warnungen / 0 Fehler, 10 Dateien deployt.

>>> ZU TESTEN:
    1. Begleiter-Verzeichnis oeffnen, ueber die Kacheln blaettern: die beiden
       Begleiter muessen mit Namen angesagt werden, leere Plaetze weiter "Leer".
    2. Ins Suchfeld: "Begleiter suchen, Eingabefeld." statt "0/40".
    LOG-BELEG: "[Minion] Icon-Map gebaut: N Begleiter." einmal, danach je
    Kachel "[Minion] node id=... icon=... -> 'Name'".

>>> NACHTRAG (zweiter Dump 10:04:59): der User schickte einen weiteren Dump -
    das war aber ein ANDERES Fenster, `LovmPaletteEdit`
    ("TRABANTEN-KOMMANDOMENUE", die Aufstellung fuer Herr der Trabanten), nicht
    das Verzeichnis. Gleiches Grundproblem, andere Auspraegung:
    - Kacheln sind wieder Icon-only (Comp(1015) CT=DragDrop -> Comp(1005)
      CT=Icon). Vorgelesen wurde der interne Zaehler-Text "-1" bzw. "Leer"
      (Log 10:04:52).
    - Die Namen stehen NUR im Detail-Panel rechts: id=48 Name
      ('Aufzieh-Luftschiff'), id=50 Typ ('Apparat'), id=53/54 Kosten,
      id=55/56 LP, id=57/58 ATT, id=59/60 ABW, id=61/62 GSW, id=63/64/65
      Auto-Attacke, id=80/81 Technik, id=83 Beschreibung, id=84/85
      Technikpunkte, id=86/87 Techniktyp.
    - Der generische Scanner las dieses Panel als Salve von fuenf Ansagen vor,
      Name zuletzt (Log 10:04:56).
    GEBAUT: die Icon-Aufloesung deckt jetzt BEIDE Fenster ab
    (TryReadMinionTileFocusRow prueft MinionNoteBook und LovmPaletteEdit).
    NICHT GEBAUT: das Detail-Panel geordnet vorlesen. Grund: die Zuordnung
    Beschriftung -> Wert ist bisher nur aus der Node-Reihenfolge abgeleitet
    (id=53 'Kosten' -> id=54 '25'), nicht gemessen. Bei Kampfwerten waere eine
    vertauschte Zuordnung schlimmer als keine Ansage. Auf Zuruf des Users
    nachziehen.

>>> OFFEN, EHRLICH: im Dump-Moment hatte nur EINE Kachel (id=77, die einzelne
    oben neben dem Favoriten-Kaestchen) ein sichtbares Icon-Kind; alle
    Gitter-Kacheln id=57 abwaerts hatten es unsichtbar. Warum, ist NICHT
    geklaert - moeglich ist ein aktiver Filter/Reiter oder ein noch nicht
    gefuelltes Gitter zum Aufnahme-Zeitpunkt. Falls beim Test Kacheln stumm
    bleiben, obwohl dort ein Begleiter steht, ist das die Spur: dann klaeren,
    welcher Reiter aktiv war (das Reittier-Fenster hat dafuer
    OnMountNoteBookUpdate mit AgentMountNoteBook - ein Pendant fuer Begleiter
    ist noch nicht gebaut).

## V5.75 OEFFENTLICH RELEASED (2026-08-09)

>>> Tag v5.75, Titel "v5.75 - Quest-Gegenstaende im Kampf".
    https://github.com/derbruedi/ff14-accessibility/releases/tag/v5.75

>>> VERSIONS-SYNC (alle drei Stellen auf 5.75, vor dem Release geprueft):
    csproj 5.75.0 / 5.75.0.0, Plugin.cs PluginVersion "5.75",
    repo.json AssemblyVersion "5.75.0.0".

>>> VERIFIKATION, dass Spieler die neue Version wirklich ziehen:
    - `gh release list`: v5.75 traegt "Latest".
    - 4 Assets dran: latest.zip (644.978 B), FF14Accessibility-v5.75.0.zip
      (644.978 B), FF14AccessibilityInstaller.exe (162.517.183 B),
      installer.json (165 B).
    - Weiterleitung releases/latest/download/latest.zip -> HTTP 200 mit
      644.978 B (v5.74 hatte 620.928 B, also wirklich die neue Datei).
    - raw.githubusercontent .../main/repo.json meldet 5.75.0.0.
    - Installer unveraendert (1.1.0.0): exe + installer.json vom v5.74-Release
      uebernommen, SHA256 gegengeprueft (5787445B...D57CAD49 stimmt ueberein).

>>> INHALT: alles seit v5.74 - Quest-Gegenstaende (unten), unbenannte und
    gleichnamige Objekte, geleerte Truhen, Systemmeldung-Dublette,
    AoeCastProbe-Absturz, zwei vnavmesh-Endpunkte, Weg endet kurz vorm Ziel,
    Handwerker-Notizbuch, HP/MP/SP in Prozent.
    ACHTUNG: 14 der 15 Bloecke sind GEBAUT, ABER IN-GAME UNGETESTET. Der
    Release ging auf ausdruecklichen Wunsch des Users trotzdem raus (er testet
    die Release-Version selbst).

## STAND JETZT (2026-08-09, "QUEST-GEGENSTAENDE IM KAMPF" - GEBAUT, UNGETESTET)

>>> USER-FRAGE: "es gibt quests wo man mit gegenstaenden im kampf sachen
    ausloesen muss ne idee wie wir das fuer blinde barrierefrei machen
    koennen?" - genannter Fall: "Stufe 28, Nebenauftrag: Ein Licht fuer die
    Nacht".

>>> DER FALL IST OFFLINE AUFGEKLAERT (Lumina gegen sqpack, kein Spiel noetig,
    User musste nichts nachspielen). Quest **66333 "Ein Licht fuer die Nacht"**
    (Stufe 28, JournalGenre 113 "Nebenauftraege Finsterwald", Nordwald):
      QuestParams: ITEM0 = EventItem 2000627 "Bergmannslampe" (Stapel 1, Cast 1s)
                   ITEM1 = EventItem 2000628 "Gleissende Lampe" (Stapel 2, Cast 3s)
                   ENEMY0 = 2266
    Also KEINE Duty-Action-Leiste, sondern ein Schluesselgegenstand.

>>> ZWEI MECHANIKEN, NICHT VERMISCHEN (beide in docs/game-api.md dokumentiert):
    A) Schluesselgegenstand (EventItem) - dieser Fall. Auf die Leiste legbar,
       dann per normaler Spieltaste ausloesbar.
    B) Duty Actions (Sonderaktions-Leiste in Instanzen) - DutyActionManager +
       RaptureHotbarModule.ExecuteDutyActionSlot. Im Tastenbelegungs-Dump
       (679 Eintraege, 2026-08-09) gibt es dafuer KEINE Belegung, das Spiel
       erwartet einen Mausklick. Getrenntes Feature, auf Wunsch des Users als
       naechstes.

>>> GEBAUT (A):
    1. InventoryService.CollectQuestItems() - die getragenen Schluessel-
       gegenstaende, die etwas TUN. Filter ist die spieleigene Spalte
       EventItem.Action != 0, genau das Gegenstueck zu Item.ItemAction bei den
       Beutel-Gegenstaenden. MESSUNG (Sheet-Dump 2026-08-09): von 3534 benannten
       EventItem-Zeilen haben 1708 eine Action (1570 davon Action#1
       "Schluesselgegenstand", Rest Wurf-/Trankartiges); die 1826 ohne Action
       sind reine Beleg-Stuecke wie "Diebesgut", fuer die das Spiel selbst keine
       Benutzung anbietet.
    2. Zuweisungs-Menue (Strg+Numpad0) hat jetzt DREI Listen statt zwei:
       Skills / Gegenstaende / Quest-Gegenstaende. Numpad 6 vor, Numpad 4
       zurueck. Eine Liste ohne Eintraege wird UEBERSPRUNGEN statt als Fehler
       angesagt - Blaettern soll immer irgendwo Brauchbarem landen. Ist gar
       nichts anderes da: "Keine andere Liste verfuegbar."
    3. Ansage beim Blaettern nennt auch die WIRKZEIT ("Gleissende Lampe,
       2 Stueck, Wirkzeit 3 Sekunden, 1 von 1") - im Kampf ist das eine
       Entscheidung, und ein sehender Spieler liest sie vom Tooltip ab.
    4. Belegen laeuft ueber HotbarSlotType.EventItem + EventItem-Zeilen-Id,
       durch denselben gemessenen Pfad wie Skills/Gegenstaende (Set +
       WriteSavedSlot + LoadSavedHotbar, PlaceOnSlot). BEWUSST NICHT
       HotbarSlotType.KeyItem: dessen Id ist laut Struct-Doku ein SLOT-INDEX im
       Schluesselgegenstand-Container (DragDrop-Sonderform) - das braeche, sobald
       der Container umsortiert.
    5. Leiste vorlesen benennt EventItem-Slots jetzt ueber das EventItem-Sheet
       statt ueber den Anzeigetext.
    6. Erhalt-Ansage: "Quest-Gegenstand zum Benutzen: <Name>. Mit Strg und
       Nummernblock 0 auf die Leiste legen." Bewusst NICHT dasselbe wie der
       Beute-Kanal (der sagt nur, DASS etwas ankam) - Schalter
       Configuration.AnnounceQuestItems.

>>> LOGIN-GEPLAPPER AUSGESCHLOSSEN, OHNE TIMER-HACK: Dalamuds IGameInventory
    taugt direkt nach dem Login nicht als Neuigkeits-Quelle - sein Vergleichs-
    Cache wird pro Container beim ersten Sehen LEER angelegt (Dalamud.Game.
    Inventory.GameInventory, dekompiliert 2026-08-09), also meldet er jeden
    getragenen Gegenstand als "Added". Deshalb keine Events, sondern eine stille
    GRUNDLINIE: die erste Beobachtung nach dem Login schreibt nur mit und sagt
    nichts; erst spaetere Neuzugaenge werden angesagt. Beim Ausloggen wird die
    Grundlinie verworfen. Log-Beleg: "[QuestItem] Grundlinie gesetzt: N ...
    (stumm)".

>>> Build Debug 0 Warnungen / 0 Fehler, 10 Dateien deployt.

>>> ZU TESTEN:
    1. Strg+Numpad0 oeffnen, mit Numpad 6 zweimal weiter: Skills ->
       Gegenstaende -> Quest-Gegenstaende. Die "Gleissende Lampe" muss dort mit
       Stueckzahl und Wirkzeit erscheinen.
    2. Sie auf eine Taste legen und die Leiste vorlesen: der Platz muss
       "Gleissende Lampe" sagen (nicht "EventItem, 2000628").
    3. Im Kampf die Taste druecken - loest das Spiel die Lampe aus?
    4. Neuen Quest-Gegenstand abholen: die Ansage muss EINMAL kommen, und beim
       naechsten Einloggen NICHT erneut.
    OFFEN UND EHRLICH: dass das Setzen mit Typ EventItem wirklich haftet, ist
    NICHT vorab gemessen - nur der Pfad ist derselbe, der fuer Action/Item in
    zwei Jobs gemessen wurde. Der Read-back nach 2 Frames meldet ehrlich
    "keine Aenderung", falls das Spiel den Typ anders behandelt. Genau dieser
    Fall (SetAndSaveSlot war job-abhaengig wirkungslos) ist hier schon einmal
    passiert.

## VORHERIGER STAND (2026-08-09, "GELEERTE TRUHEN RAUS AUS DER LISTE" - GEBAUT, UNGETESTET)

>>> USER-ANSAGE: "objekte die man aufhebt in dugeons bzw generell sollten aus
    der liste verschwinden."

>>> IM LOG BELEGT (dalamud.log 2026-08-09 00:19:35):
    "Schatztruhe 2, Schatz, schon besucht, 2 Meter, geradeaus, 1 von 26."
    Die Truhe belegt nach dem Besuch weiter einen Listenplatz.

>>> NEBENBEI BESTAETIGT: die Features von gestern LAUFEN im Spiel.
    00:15:31 "[Ortsgedaechtnis] Besucht: 'Schatztruhe' (id=4002003A,
    art=Treasure)" und 00:19:26 "'Schatztruhe' kommt mehrfach vor - ab jetzt
    nummeriert" -> danach "Schatztruhe 2". Nummer und Besuchsmarke tun also
    beide, was sie sollen. (Der User hat das noch nicht selbst bewertet.)

>>> DAS SPIEL FUEHRT DEN ZUSTAND SELBST - nichts nachgebaut.
    FFXIVClientStructs `Treasure` (ilspycmd 2026-08-09):
      State (Offset 416): Unopened=0, Opening=1, Opened=2, Unk3=3,
                          FadingOut=4, FadedOut=5
    Alles ausser Unopened = erledigt. Die Truhe bleibt danach noch kurz in der
    ObjectTable, nur um ihr Ausblenden zu spielen.
    WARUM State UND NICHT Flags.Opened: die Struct-Doku nennt beide
    ueberlappend und sagt zu Flags ausdruecklich "sometimes set when fading
    starts, sometimes when fading is complete" - also unzuverlaessig. State ist
    eine geordnete Folge und deckt auch Opening ab, wo die Sache schon
    entschieden ist.

>>> NUR DIE BROWSER-LISTE wird gefiltert (NavigationService.IsWorthBrowsing ->
    IsEmptiedTreasure). Visiert der Spieler die Truhe mit den SPIELTASTEN an,
    wird sie weiterhin angesagt - das Spiel laesst sie anvisieren, und dort zu
    schweigen wuerde etwas verstecken, das der Spieler bewusst gewaehlt hat.

>>> GRENZE, EHRLICH: das gilt fuer SCHATZTRUHEN (ObjectKind.Treasure), weil es
    dafuer eine belegte Zustandsquelle gibt. Fuer andere "erledigte" Objekte -
    betaetigte Schalter, benutzte EventObj - ist KEINE Quelle geprueft.
    GameObject.EventState existiert, aber was seine Werte bedeuten, ist nicht
    belegt; das waere zu messen, nicht zu raten. Bitte melden, wenn es konkrete
    andere Objekte gibt, die haengenbleiben.

>>> Build Debug 0 Warnungen / 0 Fehler, 10 Dateien deployt.

>>> ZU TESTEN:
    1. Dungeon, Truhe oeffnen, dann Objekt-Browser: die Truhe darf NICHT mehr
       in der Liste auftauchen, und die Gesamtzahl ("... von N") muss um eins
       kleiner werden.
    2. Ungeoeffnete Truhen muessen weiterhin erscheinen.
    3. Die geoeffnete Truhe direkt anvisieren (Tab/F-Tasten): sie soll
       weiterhin angesagt werden.
    LOG-BELEG: "[Nav] Schatztruhe <id> ist 'Opened' - faellt aus der
    Browser-Liste." (einmal je Truhe).

## VORHERIGER STAND (2026-08-08, "SYSTEMMELDUNG DOPPELT" + GEFUNDENER CRASH - GEBAUT, UNGETESTET)

>>> USER-ANSAGE: "wenn system meldungen kommen werden die zwei mal vorgelesen
    einmal als system meldung und dann noch mal so schau in die log."

>>> IM LOG BELEGT, dieselbe Meldung auf zwei Wegen (dalamud.log 2026-08-08):
      23:43:53.396 [Chat] kind=SystemMessage (57) text='Sind alle Gruppen...'
      23:43:53.396 [Speak] 'System: Sind alle Gruppenmitglieder kampfunfaehig...'
      23:43:53.397 [Toast] Toast: 'Sind alle Gruppenmitglieder kampfunfaehig...'
    Also einmal ueber ChatGui, 1 ms spaeter einmal ueber ToastGui.
    Gegenprobe: NICHT jede Systemmeldung ist doppelt - "'Haukke-Herrenhaus' hat
    begonnen" (23:43:51) kam nur ueber den Chat. Und die meisten Toasts haben
    gar kein Chat-Pendant (Ortsnamen wie "Zwieselgrund"). Ein pauschales
    Abschalten eines der beiden Kanaele waere also falsch gewesen.

>>> URSACHE: der Dublettenschutz existierte bereits, griff aber nur in EINE
    Richtung. TolkService.WasRecentlySpoken vergleicht ganze Zeichenketten
    (`t == text`). Der Chat sprach MIT Kanal-Praefix ("System: ..."), der Toast
    fragte OHNE Praefix - kein Treffer, also zweimal gesprochen. Umgekehrt
    (Toast zuerst) funktionierte es immer, weil der Chat-Reader den nackten Text
    gegen einen nackten Eintrag prueft.

>>> FIX: neue Methode TolkService.RememberSpokenVariant(text) legt einen Text in
    die Verlaufsliste, ohne ihn zu sprechen. Der ChatReaderService meldet damit
    nach dem Sprechen zusaetzlich den praefixlosen Wortlaut - und nur dann, wenn
    ueberhaupt ein Praefix angehaengt wurde.
    VERWORFENE ALTERNATIVE: WasRecentlySpoken praefix-tolerant machen ("endet
    auf"). Das haette kurze echte Wiederholungen still verschluckt. Die Quelle,
    die das Praefix ANHAENGT, weiss als einzige, was ihr nackter Wortlaut war.

>>> DABEI GEFUNDEN, NICHT GEMELDET, ABER GRAVIEREND: NullReferenceException in
    CombatService.AoeCastProbe Zeile 573, laufend waehrend des Dungeons
    (23:43:47 und fortlaufend). Dalamud implementiert IsCasting als
    `Struct->GetCastInfo()->IsCasting` OHNE Null-Pruefung (Dalamud.dll
    dekompiliert 2026-08-08). Die Ausnahme faellt bis in OnFrameworkUpdate
    durch - alles, was nach dem Sonden-Aufruf laeuft, faellt in dem Frame aus.
    FIX: derselbe ObjectKind-Filter (BattleNpc), den die produktive Schleife
    Zeile 392 schon hat und der sie nachweislich verschont; zusaetzlich an der
    zweiten ungefilterten Stelle (Zeile 423), die dasselbe Muster hat und nur
    seltener laeuft.
    EHRLICH: WELCHER ObjectKind den Nullzeiger liefert, ist NICHT ermittelt. Der
    Filter ist von der Schleife uebernommen, die es nachweislich ueberlebt, nicht
    aus einer Diagnose. Die Sonde ist eine #if-DEBUG-Sonde und koennte nach
    Abschluss des AoE-Themas ganz weg - das ist eine Entscheidung des Users.

>>> Build Debug 0 Warnungen / 0 Fehler, 10 Dateien deployt.

>>> ZU TESTEN:
    1. Dungeon betreten: die Hinweistexte ("Sind alle Gruppenmitglieder...",
       "Routine-Bonus") duerfen nur noch EINMAL kommen.
    2. Ortsnamen-Einblendungen (z. B. beim Betreten eines Gebiets) muessen
       WEITERHIN kommen - die haben kein Chat-Pendant und duerfen nicht
       mitgefiltert werden.
    3. Im Log darf "NullReferenceException ... AoeCastProbe" nicht mehr
       auftauchen.

## VORHERIGER STAND (2026-08-08, "ZWEI VNAVMESH-ENDPUNKTE" - GEBAUT, UNGETESTET)

>>> USER-FRAGE: "gibt es eine moeglichkeit das vnavmesh objekte erreicht die
    uebereinem liegen?" -> vnavmesh.dll komplett dekompiliert (ilspycmd,
    2026-08-08). Vollstaendige Analyse in docs/game-api.md.

>>> KERNBEFUND ZUR FRAGE: das Gehnetz kann Hoehe (Treppen, Rampen, Bruecken sind
    drin). Was es nicht kann, ist eine Verbindung, die es begehbar nicht gibt.
    Der `fly`-Parameter waehlt einen ANDEREN SUCHRAUM (QueryPath Zeile 189):
    `flying ? PathfindVolume : PathfindMesh`. Wir uebergeben ueberall false.

>>> EINE FRUEHERE EMPFEHLUNG WAR ZU OPTIMISTISCH UND IST HIER KORRIGIERT:
    `Nav.PathfindWithTolerance` bringt beim LAUFEN nichts Neues.
    `AsyncMoveRequest.MoveTo(dest, fly, range)` reicht `range` bereits an
    QueryPath weiter - unser `_moveCloseTo(dest, false, stopRange)` nutzt die
    Toleranz also seit jeher. NEU ist sie nur bei den reinen ABFRAGEN, die mit
    range=0 liefen.

>>> EINGEBAUT, ZWEI STELLEN:

    1. `Query.Mesh.NearestPointReachable` -> AutoWalkService.SnapToReachableMesh,
       benutzt in der Kandidatensuche der Zugangssuche.
       WAS "ERREICHBAR" WIRKLICH HEISST (dekompiliert, nicht angenommen): NICHT
       "von meinem Standort aus". Das Gate setzt allowUnreachable=false, das
       tauscht den Filter gegen FloodFillAwareFilter, und der verwirft Polygone
       mit Flag 0x10. Gesetzt wird das Flag einmal pro Zone von
       NavmeshManager.Prune per Flood-Fill von Saatpunkten. Die Eigenschaft ist
       also "haengt mit der Hauptflaeche der Zone zusammen" - eine vorberechnete
       Karteneigenschaft.
       NUTZEN: abgetrennte Inseln (der Astalicia-Fall) kommen gar nicht erst in
       die Kandidatenliste, statt je einen vollen Pathfind zum Aussortieren zu
       kosten.
       GRENZE: Prune laeuft nur, wo FloodFill.TryLookup Saatpunkte fuer die Zone
       hat. Ohne sie ist nichts markiert und es verhaelt sich exakt wie vorher.

    2. `Nav.PathfindWithTolerance` -> RouteService.RequestPath(from, to,
       tolerance), gesetzt in der Gehhilfe (_walkArrivalRange) und in der
       Routenvorschau (ArrivalDistance).
       WAS DIE TOLERANZ TUT: > 0 tauscht die A*-Heuristik gegen
       GoalRadiusHeuristic, die -1 liefert sobald ein Knoten im Radius liegt -
       der Knoten wird als Ziel akzeptiert.
       GRENZE, WICHTIG: das Zielpolygon wird trotzdem zuerst gesucht, mit
       vnavmeshs eigenem Standard-Extent von 5 m (PathfindMesh ruft
       FindNearestMeshPoly(to) ohne Argumente). Liegt das Ziel weiter als das
       vom Netz weg, gibt es kein Polygon und damit keine Route - egal wie gross
       die Toleranz ist. Toleranz rettet "knapp neben der Flaeche", nicht "weit
       davon weg". Der Haukke-Fall (14,6 m) faellt NICHT darunter.

>>> BEIDE MIT RUECKFALL: ein aelteres vnavmesh registriert die Gates nicht und
    wirft beim INVOKE. Dann wird auf den alten Aufruf zurueckgefallen und einmal
    gewarnt (nicht still, nicht pro Frame).

>>> Build Debug 0 Warnungen / 0 Fehler, 10 Dateien deployt.

>>> ZU TESTEN:
    1. Gehhilfe zu einem Ziel, das frueher in den Luftlinien-Modus fiel
       ("Kein Wegenetz, Luftlinie") - kommt jetzt eine echte Route?
    2. Routenvorschau auf einen Kartenmarker: kommt haeufiger eine Route?
    3. Zugangssuche: im Log muesste "[Zugang] N Kandidatenpunkte" jetzt eher
       WENIGER Kandidaten zeigen als frueher, dafuer bessere.
    LOG-BELEG: "[Zugang] NearestPointReachable nicht verfuegbar" bzw.
    "[Route] Nav.PathfindWithTolerance nicht verfuegbar" duerfen NICHT
    erscheinen - wenn doch, ist das installierte vnavmesh zu alt.

>>> NICHT GEBAUT, bewusst: Fliegen (fly=true). Dafuer muesste erst gemessen
    werden, ob das Voxel-Volumen in den fraglichen Zonen ueberhaupt existiert -
    NavmeshQuery legt VolumeQuery nur an wenn navmesh.Volume != null, sonst
    kommt "Nav volume was not built". Und eine Flugroute nuetzt nur mit
    freigeschaltetem Flug und Mount.

## VORHERIGER STAND (2026-08-08, "GLEICHNAMIGE OBJEKTE UNTERSCHEIDEN" - GEBAUT, UNGETESTET)

>>> USER-ANSAGE: "ob man die objekte wenn man mehrere von einer sorte hat wie in
    dungeons benennen kann so das ich weiss ob ich da schon mal war bzw damit ich
    weiss wo ich zuerst hin muss."

>>> DAS PROBLEM IM CODE BELEGT, nicht vermutet: GetObjectsOfKinds sortiert die
    Browser-Liste bei JEDEM Tastendruck neu nach Entfernung
    (NavigationService.cs:1009). Damit ist "3 von 8" ein Listenplatz, kein
    Objekt - zwei Schritte weiter ist dieselbe Truhe "2 von 8". Vier Truhen
    hiessen alle gleich, und nichts in der Ansage blieb beim Objekt.

>>> NEU: ObjectMemoryService (Ortsgedaechtnis). Zwei Dinge, bewusst getrennt:
    1. NUMMER: "Truhe 2, Schatz". Vergeben in der Reihenfolge der ersten Ansage,
       gebunden an das Objekt, nicht an die Liste. Erst ab dem ZWEITEN
       gleichnamigen Objekt - eine einzelne Truhe bleibt "Truhe".
    2. BESUCHT: ", schon besucht", sobald der Spieler naeher als 5 m dran war.

>>> WARUM GETRENNT: der erste Entwurf hatte beides in einer Struktur, und die
    Positionssuche lief dann ueber alle Namensgruppen - eine neu geladene Truhe
    haette den Eintrag der Tuer daneben bekommen. Getrennt sucht die Nummer nur
    innerhalb ihrer Namensgruppe, und "besucht" braucht gar keinen Namen.

>>> IDENTITAET ZWEISTUFIG: GameObjectId solange geladen (das nutzt der Rest des
    Plugins schon so), sonst Position auf 1 m genau. Begruendung: ob das Spiel
    nach dem Ausladen dieselbe Id wieder vergibt, ist NICHT belegt - also nicht
    angenommen. Eine Truhe bewegt sich nicht, die Position traegt.

>>> EHRLICHE GRENZE: ein Objekt, das sich BEWEGT UND auslaedt (patrouillierender
    NPC), kann mit neuer Nummer zurueckkommen. Kampf-NPCs und Spieler sind
    deshalb ganz ausgeschlossen (IsLandmark) - ein respawnender Gegner ist kein
    Ort. Gedaechtnis gilt fuer EventObj, Schatz, Sammelpunkt, Aetheryt, EventNpc.

>>> ZONENWECHSEL LEERT ALLES: beim zweiten Dungeon-Besuch sind die Truhen wieder
    voll, "schon besucht" waere dort schlicht gelogen.

>>> ZUM ZWEITEN TEIL DER FRAGE ("wo muss ich zuerst hin"): das beantwortet die
    Nummer NICHT - sie ist Unterscheidung, keine Reihenfolge. Was der Spieler
    bekommt, ist die Gegenprobe: was noch nicht "schon besucht" sagt, steht noch
    aus. Eine echte Reihenfolge waere Spielwissen, das auch ein sehender Spieler
    nicht angezeigt bekommt.

>>> Build Debug 0 Warnungen / 0 Fehler, 10 Dateien deployt.
    (Hinweis: scripts/Build-Mod.ps1 aus CLAUDE.md existiert hier nicht - der
    Deploy nach devPlugins haengt am Target DeployToDevFolder im csproj.)

>>> ZU TESTEN:
    1. Dungeon mit mehreren gleichnamigen Objekten: sagt der Browser jetzt
       "Truhe 1", "Truhe 2" - und BLEIBT die Nummer dieselbe, wenn du naeher
       rangehst oder wieder weggehst?
    2. An einer Truhe stehen, weglaufen, spaeter zurueckblaettern: kommt
       ", schon besucht"?
    3. Stoert die Nummer irgendwo, wo Objekte zwar gleich heissen, aber egal
       sind (viele gleiche NPCs in der Stadt)? Dann bitte melden, dann grenzen
       wir die Arten ein.
    LOG-BELEG: Zeilen mit "[Ortsgedaechtnis]" - "kommt mehrfach vor - ab jetzt
    nummeriert", "Besucht: ..." und die Zonenwechsel-Zeile.

## VORHERIGER STAND (2026-08-08, "WEGE WERDEN NICHT GEFUNDEN" - GEBAUT, UNGETESTET)

>>> USER-ANSAGE: "eine sache zum navigieren er findet manchmal wege nicht schau
    das er das navmesh wegenetz nuzt dann sollte er alle wege finden die dort
    verfuegbar sind."

>>> IM LOG GEFUNDEN, nicht vermutet - ein Fall, viermal wiederholt
    (dalamud.log 2026-08-08 18:32:15, 18:32:23, 18:32:33, 18:33:13).
    Ziel "Haukke-Herrenhaus" (Kartenmarker, 603 m):
      1. vnavmesh liefert 54 Wegpunkte. Echtes Pfadende <-575,8|67,2|64,1>,
         also 14,6 m waagerecht und 13,8 m hoch vor dem Ziel <-590,4|81,0|63,6>.
      2. Fall 1 "falsche Etage" greift und leitet um auf <-590,4|67,2|63,6>
         (Marker-XZ + Pfadende-Hoehe).
      3. Der Lauf dorthin meldet restWp=0 -> "Kein Weg zu Haukke-Herrenhaus
         gefunden." ENDE.
    Der Spieler hoerte also viermal eine Absage, waehrend das Wegenetz einen
    603-m-Weg bis auf 14,6 m ans Ziel hatte. Genau die gemeldete Beschwerde.

>>> ZWEI URSACHEN, beide behoben:

    1. DER KORRIGIERTE PUNKT WAR NIE AUF DEM WEGENETZ. `_destPosition with
       { Y = realEnd.Y }` ist reine Rechnung: Marker-X/Z plus eine Hoehe, die
       woanders gemessen wurde. Dass dort Boden liegt, hat nie jemand geprueft -
       hier lag er daneben, darum 0 Wegpunkte. Der Kommentar an der Stelle
       behauptete "the chain cannot run away" - sie lief sehr wohl weg, weil der
       Rueckfall einen PFAD voraussetzt und es gar keinen gab.
       FIX: `NearestMeshPoint(computed, 3 m waagerecht, 2 m hoch)` legt den Punkt
       aufs Netz, bevor umgeleitet wird. Die Hoehenbox bleibt bei ImpossibleRise
       (2 m) - die Etage ist ja der ganze Zweck der Korrektur, ein weiter Griff
       nach oben wuerde genau den Fehler wiederholen, den sie beheben soll.
       Findet sich dort kein Netz, wird gar nicht erst umgeleitet, sondern
       direkt die Zugangssuche gestartet.

    2. "KEIN WEG GEFUNDEN" WAR EINE SACKGASSE. Der Zweig lief in eine Absage,
       ohne die vorhandene Zugangssuche auch nur zu versuchen - obwohl Fall 3
       sie fuer den verwandten Fall schon nutzt und die Regel des Users dieselbe
       ist ("es sollen alle angelaufen werden koennen die das navmesh hat").
       Das trifft JEDES Ziel, dessen exakter Punkt neben dem Netz liegt, nicht
       nur diesen einen.
       FIX: keine Route -> Zugangssuche um das Ziel, still. Erst wenn auch die
       nichts findet, kommt die Absage.

>>> GEGEN DIE ENDLOSSCHLEIFE, die dabei droht: die Zugangssuche endet selbst in
    einem Lauf, der wieder scheitern koennte. `_approachTried` erlaubt genau
    EINEN Versuch je Ziel; der Lauf, den die Suche startet, setzt das Flag
    selbst, statt es zuruecksetzen zu duerfen.

>>> DAZU `_walkOrigin` NEU: ein Lauf wird intern mehrfach neu gestartet (falsche
    Etage, Pfadende, Zugangspunkt), und jeder Neustart ueberschreibt
    `_destPosition` mit einem Zwischenpunkt. Die Zugangssuche muss aber um das
    suchen, was der SPIELER genannt hat - sonst sucht sie den Zugang zu einem
    Punkt, den nie jemand wollte. BeginWalk setzt es, die beiden Umleitungen in
    Update stellen es danach wieder her.

>>> KEIN INFORMATIONSVERLUST BEI DER ABSAGE: die alte Zeile trug den
    Aetheryt-Hinweis (BuildNoPathHint, "Reise per Aethernet dorthin"). Der geht
    jetzt als `noPathHint` an die Suche mit und haengt an ApproachNone. Kein
    neuer String noetig - der Hinweis ist bereits ein eigener Satz mit
    fuehrendem Leerzeichen und in beiden Sprachen vorhanden.

>>> Build Debug 0 Warnungen / 0 Fehler, 10 Dateien deployt.

>>> ZU TESTEN:
    1. Objekt-Browser -> "Haukke-Herrenhaus" -> Numpad 3. Erwartung: KEINE
       Absage mehr. Entweder laeuft er los und kommt in der Naehe an, oder er
       sagt am Ende ehrlich, dass es keinen erreichbaren Punkt gibt.
    2. Bitte auch bei anderen Zielen achten, wo frueher "Kein Weg zu ...
       gefunden" kam - die muessten jetzt meist losgehen.
    3. Wenn er losgeht und irgendwo stehenbleibt: bitte melden WO. Im Log
       stehen dann "[Zugang] Naechster erreichbarer Punkt" mit Koordinaten und
       Hoehenunterschied.
    LOG-BELEG: die Zeilen "[Nav] Auto-Lauf: keine Route zu <...> - suche den
    naechsten erreichbaren Punkt um '<Ziel>'" und "[Zugang] N von M Kandidaten
    sind erreichbar."

>>> EHRLICHE GRENZE: dass der Zugang zum Haukke-Herrenhaus im Wegenetz
    UEBERHAUPT existiert, ist nicht belegt - gemessen ist nur, dass das Netz bis
    14,6 m heranreicht. Die Zugangssuche tastet Ringe bis 28 m ab; findet sie
    nichts, ist der Eingang wirklich nicht angebunden. Das entscheidet erst der
    Test.

## VORHERIGER STAND (2026-08-08, "UNBENANNTE OBJEKTE" - GEBAUT, UNGETESTET)

>>> USER-ANSAGE: "ist es moeglich alle objekte die unbenannt sind zu benennen?
    weil manchmal unbenanntes objekt da steht so das man nicht weiss was es
    ist." Gehoert wird es beim Blaettern im Objekt-Browser UND beim Anvisieren.

>>> DIE NAHELIEGENDE ANTWORT IST FALSCH, OFFLINE BELEGT (Lumina-Dump gegen das
    installierte Spiel, 2026-08-08): Die namenlosen Objekte haben AUCH IM SPIEL
    keinen Namen. Alle 25 namenlosen EventNpc aus dem Live-Log (DataIds 1001183,
    1003517, 1007977, ...) stehen in ENpcResident mit Singular='' UND Title=''.
    Der EventObj 2013278: EObjName.Singular=''. Es gibt also nichts zu holen -
    ein Name dafuer waere Erfindung.
    GEGENPROBE, damit das kein Werkzeugfehler ist: fuer die Objekte, die das
    Spiel benannt hat, steht der Name auch im Sheet - Robyn, Hasthwab, Muriel,
    Sekka, Gelbjacken-Wache exakt gleich. Das Auslesen stimmt also.
    Alle 25 hatten zielbar=False: Statisten, Kulisse, unsichtbare Ausloeser.

>>> ABER ZWEI ECHTE FEHLER GEFUNDEN, beide im Log belegt:

    1. DER NAMENSFILTER WAR IN DER KATEGORIE "ALLES" KOMPLETT AUS.
       NavigationService.GetObjectsOfKinds fragte
         var isGathering = kinds.Contains(ObjectKind.GatheringPoint);
         ... && (isGathering || <Name nicht leer>)
       Die Ausnahme war fuer Sammelpunkte gedacht (die haben nie einen eigenen
       Namen), galt aber PRO KATEGORIE. AllBrowseKinds enthaelt GatheringPoint
       -> in "Alles" war der Filter fuer JEDES Objekt abgeschaltet.
       BELEG im Log: "Auswahl: , Objekt, 24 Meter, geradeaus, 7 von 68"
       (2026-08-08 00:40) und "Auswahl: , NPC, 7 Meter, 1 von 68" (2026-08-06
       20:49) - Ansagen, die mit einem leeren Namen anfangen, mitten in einer
       68 Eintraege langen Liste.

    2. NAMEN OHNE SPRECHBAREN INHALT rutschten durch jede Leer-Pruefung.
       EObjName 2004123 heisst im Spiel woertlich "?" -> Log 2026-08-06 19:49:
       "Auswahl: ?, Objekt, 55 Meter". IsNullOrWhiteSpace sagt "nicht leer",
       der Screenreader sagt nichts Brauchbares. Kein Einzelfall: 52 Zeilen in
       EObjName tragen kein einziges Buchstaben-/Ziffernzeichen.

>>> DAZU EIN DRITTER, VOM USER NICHT GEMELDETER FEHLER: derselbe Gegenstand
    hiess je nach Taste anders. Der Browser loeste Sammelpunkte sauber auf
    ("Erzader, Stufe 20"), der Auto-Lauf merkte sich aber nur den ROHEN Namen
    (leer) -> Numpad 3 sagte danach "Laufe zu Unbenannt". Dasselbe beim
    Anvisieren (NavigationService:186) und beim Ziel-Folgen
    (AutoWalkService:355). Vier Stellen, vier verschiedene Antworten.

>>> GEBAUT - Services/ObjectNameService.cs (neu), EINE Quelle fuer die Frage
    "wie heisst das":
    - IsSpeakable(text): mindestens ein Buchstabe ODER eine Ziffer nach
      Sanitize. Ersetzt "nicht leer" ueberall - faengt "?", Icon-Glyphen und
      Nullbreiten-Fueller in einem Zug.
    - Resolve(obj): roher Name -> sonst Sheet ueber BaseId -> sonst null.
      Sheet-Zuordnungen BELEGT, nicht angenommen:
        EventNpc -> ENpcResident (dieselbe Bindung, die NpcPrefix seit jeher
          fuer NPC-Titel nutzt; Gegenprobe oben).
        EventObj -> EObjName (gleiche Zeilenzahl wie EObj, 15710; und Zeile
          2004123 liest "?" - genau der Name, den das Spiel live fuer das
          Objekt mit dieser BaseId zeigte. Die Zeilennummern decken sich also).
      NICHT aufgeloest: BattleNpc (BaseId adressiert BNpcBase, der Name liegt
      unter einer anderen Id in BNpcName) und Treasure - keine Quelle, also
      kein Rateversuch.
    - Deklinationsmarker werden entfernt: die Sheets fuehren "Soldat[p] von
      Nophicas Schar", "Highwind-Bedienstet[a]". Offline gezaehlt sind es genau
      drei Marker ([a] 6679x, [p] 2246x, [t] 32x), das Muster ist also
      geschlossen. Das Spiel setzt die Endung zur Laufzeit aus dem Satz - den
      haben wir nicht, also faellt der Marker weg statt geraten zu werden.
    - Describe(obj): Name, oder ehrlicher Platzhalter "Objekt ohne Namen" /
      "NPC ohne Namen" (User-Entscheidung 2026-08-08: Art plus Hinweis, damit
      klar ist dass das Spiel dort nichts hat und nicht das Plugin versagt).

>>> FILTERREGEL NEU, jetzt pro OBJEKT (IsWorthBrowsing):
    drin bleiben Sammelpunkte (Typ+Stufe sind die Beschreibung), alles mit
    sprechbarem Namen, und namenlose Dinge, die das Spiel ANVISIEREN laesst -
    die markiert es selbst als benutzbar, die zu verstecken koennte etwas
    Nutzbares verstecken. Raus fliegt nur namenlos UND nicht anvisierbar.
    Erwartung fuer die gemessene Stelle: 25 der 38 Objekte verschwinden.

>>> ALLE VIER STELLEN NUTZEN JETZT DIESELBE AUFLOESUNG: Browser (CycleObject +
    gemerkte Auswahl fuer Numpad 3), Zielwechsel-Ansage, Auto-Lauf zum
    Spielziel, Ziel-Folgen, Annaeherungs-Ansage. Neue gemeinsame Methode
    DescribeObject laesst das Art-Wort in zwei Faellen weg, damit nichts doppelt
    kommt: bei Sammelpunkten (sonst "Erzader, Stufe 20, Sammelpunkt") und bei
    namenlosen (sonst "Objekt ohne Namen, Objekt").
    AccessibilityStrings.Unnamed ("Unbenannt") ist ersatzlos entfernt.

>>> NEUE ANSAGE bilingual: UnnamedOfKind.

>>> ZUR VERIFIKATION eingebaut: eine Log-Zeile pro Tastendruck, aber nur wenn
    wirklich etwas ausgeblendet wurde -
    "[Nav] Browser: 40 von 68 Objekten (28 ohne Namen und nicht anvisierbar
    ausgeblendet)."

>>> Build Debug 0 Warnungen / 0 Fehler, 10 Dateien deployt.

>>> NACHTRAG SELBE SITZUNG - USER-BEISPIEL: "Zielort, Objekt, 88 Meter, rechts,
    1 von 1. und da weiss man nicht was es ist." Das ist ein ANDERER Fall als
    oben: hier IST ein Name da, er sagt nur nichts.

>>> OFFLINE GEMESSEN, wie generisch die Objektnamen sind (EObjName + EObj.Data):
      1667x "Zielort"   -> davon 1547 mit Quest-Bindung
       183x "Portal"    -> 175 ohne auswertbare Bindung
       152x "Windaetherquelle"
       135x "Ausgang"   -> davon 96 mit Warp-Bindung (Ziel-Gebiet bekannt)
       104x "Miniatur-Aetheryt"
        41x "Transportvorrichtung" -> alle ohne auswertbare Bindung
        13x "Abkuerzung"           -> alle ohne auswertbare Bindung
    "Zielort" ist also der Sammelname fuer Quest-Objekte, und das Spiel weiss
    bei 93 % davon, zu WELCHER Quest. Bei "Ausgang" kennt es das Ziel-Gebiet
    ("Neu-Gridania", "Noerdliches Thanalan").

>>> GEBAUT - ObjectNameService.Qualifier(obj), aus EObj.Data:
    - Quest-Bindung  -> " fuer <Questname>"  ("Zielort fuer Narben im Wald")
    - Warp-Bindung   -> " nach <Gebiet>"     ("Ausgang nach Neu-Gridania")
    - sonst leer. Fuer Portal/Abkuerzung/Transportvorrichtung fuehrt das Spiel
      nichts Auswertbares - das bleibt ehrlich unbeantwortet statt geraten.
    Gilt nur fuer EventObj: nur die tragen die EObj.Data-Bindung.

>>> FREMDE QUESTS WERDEN AUSGEBLENDET (User-Entscheidung 2026-08-08). Ein
    "Zielort" einer NICHT angenommenen Quest verschwindet aus dem Browser: er
    ist gerade nicht benutzbar, und ein sehender Spieler bekommt dort auch
    keinen Questnamen zu sehen - ihn zu nennen waere mehr als Paritaet.
    QUELLE: QuestManager.NormalQuests (30 Plaetze, QuestWork.QuestId @8,
    ilspycmd 2026-08-08) - ein schlichtes Datenfeld, bewusst der per Signatur
    gesuchten Funktion IsQuestAccepted vorgezogen.
    ID-UMRECHNUNG BELEGT: das Sheet fuehrt uint-RowIds, das Journal ushort.
    Alle 5533 Questzeilen liegen zwischen 0x10000 und 0x1159C, die unteren
    16 Bit sind also KOLLISIONSFREI eindeutig (offline geprueft).
    LIMIT, ehrlich vermerkt: nur normale Quests. Freibrief-Ziele stehen in
    LeveQuests und haben ihre eigene Browser-Kategorie.

>>> REGRESSION, NOCH AM SELBEN TAG GEMELDET UND ZURUECKGENOMMEN:
    USER: "er liest nicht mehr alle kategorien und jetzt seh ich an der stelle
    wo vorhin ein objekt war keins mehr."
    IM LOG SAUBER EINGEKREIST, welcher der beiden Filter es war:
      18:00:25  "Browser: 1 von 4 (3 namenlos ausgeblendet)" + Ansage
                "Zielort, Objekt, 88 Meter, 1 von 1"  -> Objekt DA
      18:13:02  "Browser: 0 von 4 (ausgeblendet: 4 fremde Quest)" + Ansage
                "Keine Objekte in 100 Metern."        -> Kategorie STUMM
    Der Namensfilter war um 18:00 bereits aktiv und hat den Zielort gerade
    NICHT gefressen. Schuldig ist allein der Fremd-Quest-Filter: er haelt
    Quests fuer fremd, auf denen der Spieler tatsaechlich ist.
    "Liest nicht mehr alle Kategorien" ist dieselbe Ursache: leergefilterte
    Kategorien sagen "Keine Objekte in 100 Metern".

>>> MEIN FEHLER, benannt statt umschrieben: ich habe offline geprueft, dass
    (RowId & 0xFFFF) EINDEUTIG ist (keine Kollisionen unter 5533 Zeilen) - und
    daraus geschlossen, dass es die RICHTIGE Umrechnung ist. Das folgt nicht.
    Dass QuestManager.NormalQuests dieselbe Konvention benutzt, war nie belegt.
    Eindeutigkeit ist keine Korrektheit.

>>> SOFORT ENTSCHAERFT: Die Ausblendung ist raus, IsWorthBrowsing filtert
    wieder nur namenlos+nicht anvisierbar. Ein Objekt zu viel kostet einen
    Tastendruck, ein fehlendes kostet dem Spieler sein Questziel.
    Qualifier/BelongsToForeignQuest bleiben im Code, aber ungenutzt fuer den
    Filter - der Questname wird weiter angesagt, WENN die Pruefung anschlaegt.

>>> SONDE [QuestProbe] GELAUFEN UND AUSGEWERTET (Log 2026-08-08 18:17), Sonde
    danach wieder entfernt. ERGEBNIS - die Technik war NICHT kaputt:
      Journal: 801, 788, 789, 1106, 794, 790, 797
      Objekte: 2560 (RowId 68096), 2572 (68108), 2575 (68111)
    Rueckgerechnet ergeben die Journal-Ids lauter echte Questnamen ('Die
    Tragoedie der Dartancours', 'Ein Schluessel und ein Schussel', ...), die
    Umrechnung 65536+low stimmt also. Die drei Objekt-Quests ('Thal zu Ehren',
    'Die hohe Kunst des Schwertkampfs') sind schlicht Gladiator-Klassenquests,
    die der Spieler wirklich nicht angenommen hat. angenommen=False war KORREKT.
    Falsch war nicht die Messung, sondern der Schluss daraus.

>>> USER-EINWAND, der die Sache entscheidet: "die objekte haben nicht immer was
    mit quests zu tun, es koennen auch einfach objekte sein, mit denen man
    unabhaengig agieren kann." Genau so ist EObj.Data zu lesen: es sagt, dass
    ein Objekt AUCH in einer Quest vorkommt - nicht, dass es sonst wertlos ist.
    Quest-Zustand ist damit dauerhaft KEIN Filterkriterium mehr.

>>> QUESTNAME WIRD JETZT IMMER GENANNT, auch bei nicht angenommener Quest.
    Die vorherige Regel (nur bei eigener Quest) liess ausgerechnet den Fall
    stumm, der die ganze Meldung ausgeloest hat: ein nacktes "Zielort". Der
    Questname ist das EINZIGE, was das Spiel ueber so ein Objekt weiss - ihn
    zurueckzuhalten hilft niemandem. Der Spieler hoert jetzt "Zielort fuer
    Thal zu Ehren" und weiss, woran er ist.
    Falls Questtitel je stoeren (Story-Spoiler), ist die Stelle eine Zeile:
    ObjectNameService.Qualifier.

>>> AUFGERAEUMT: IsQuestAccepted, BelongsToForeignQuest und die Sonde sind
    raus - toter Code nach dieser Entscheidung. Die verifizierte Umrechnung
    (Journal-QuestId = RowId - 65536, kollisionsfrei ueber alle 5533 Zeilen)
    steht hier dokumentiert, falls sie je wieder gebraucht wird.

>>> NEBENBEFUND aus derselben Sonde: 3 der 4 Objekte (BaseId 2008321, 2008175,
    2008186) haben AUCH im EObjName-Sheet keinen Namen und sind nicht
    anvisierbar - die bleiben also ausgeblendet. Nur 2008101 heisst "Zielort".

>>> ZWEITER FEHLER, SCHWERER ALS GEDACHT (User: "die quest npcs werden nicht
    vorgelesen"). Der Log hatte den Stacktrace:
      System.NullReferenceException
        at ObjectNameService.Qualifier(IGameObject) : line 123
        at ObjectNameService.Describe(IGameObject)  : line 93
        at NavigationService.CycleObject(Int32)
    URSACHE: `return default` bei einem `readonly record struct ObjectPurpose`
    laesst die STRING-Felder NULL - nicht leer. `purpose.QuestName.Length`
    warf danach. Der Compiler warnt dabei NICHT, auch mit Nullable an.
    TRAGWEITE: nicht nur Quest-NPCs. Es traf JEDES Objekt ohne Quest-/Warp-
    Bindung, also fast alle - NPCs, Gegner, Spieler. Die Ausnahme flog mitten
    in CycleObject, also VOR der Ansage: Tastendruck = gar nichts. Nur der
    "Zielort" ging, weil der eine Bindung hat und darum echte Strings bekam.
    Deshalb sah es wie ein Quest-NPC-Problem aus.
    FIX: ObjectPurpose.None (leere Strings) ueberall statt `default`, plus
    string.IsNullOrEmpty statt .Length als zweite Sicherung.
    LEHRE fuer record structs mit Referenztypen: `default` ist nie "leer".

>>> NEUE ANSAGEN bilingual: ForQuest, LeadsToArea.

>>> Build Debug 0 Warnungen / 0 Fehler, 10 Dateien deployt (beide Teile).

>>> ZU TESTEN:
    1. Bild-auf/-ab in Kategorie "Alles": kommt noch eine Ansage, die mit einem
       Komma anfaengt? Und wird die Liste spuerbar kuerzer (Zaehler "x von y")?
    2. Einen Sammelpunkt anwaehlen, dann Numpad 3: muss "Laufe zu Erzader,
       Stufe 20" sagen, nicht mehr "Unbenannt".
    3. Der gemeldete "Zielort" muss jetzt entweder "Zielort fuer <Questname>"
       heissen ODER ganz aus der Liste sein (wenn die Quest nicht angenommen
       ist). Beides ist richtig - wichtig ist, dass kein nacktes "Zielort"
       mehr kommt.
    4. Ein "Ausgang" sollte "Ausgang nach <Gebiet>" sagen.
    5. Falls irgendwo "Objekt ohne Namen" kommt: bitte melden WO - dann steht
       im Log die BaseId und ich kann pruefen, ob das Spiel dafuer doch einen
       Namen fuehrt.
    LOG-BELEG dafuer: "[Nav] Browser: 40 von 68 Objekten (ausgeblendet: 12
    fremde Quest, 16 namenlos und nicht anvisierbar)."

## VORHERIGER STAND (2026-08-08, HANDWERKER-NOTIZBUCH LESBAR - GEBAUT, TEILWEISE BESTAETIGT)

>>> USER-ANSAGE: "jetzt ist es an der zeit mal was fuer die sammler und
    handwerker berufe zu machen. das handwerker notizbuch aendert sich ja je
    nach dem welche handwerkerklasse man grad ist aber es ist nicht richtig
    auslesbar."

>>> IST-ZUSTAND GEMESSEN, NICHT VERMUTET (Live-Log 2026-08-08, Oeffnen um
    09:52:04 und Navigation um 09:38:18-22). Gesprochen wurde AUSSCHLIESSLICH:
      09:52:04.838  "HANDWERKER-NOTIZBUCH"
      09:52:04.894  "NEU"
      09:52:04.904  "Menue, 1 Eintraege"
      09:52:04.912  "Favoriten, NEU"
      danach beim Blaettern: "0/40", "Zuletzt gesucht", "Favoriten, NEU"
    Klasse, Stufe, Rezeptname und JEDER Rezeptwert kamen NIE vor. Das Fenster
    war also nicht "schlecht lesbar", sein Inhalt wurde gar nicht angefasst.

>>> DREI URSACHEN, alle im UI-Dump vom selben Tag belegt (109 Nodes):
    1. "NEU" ist ein UNSICHTBARER Marker-Text in jeder Listenzeile (id=3 bzw.
       id=10, Flag 0x0023 = kein V). Der generische Fokus-Leser griff ihn ab
       statt der Zeilenbeschriftung ("1-5", "6-10").
    2. "Menue, 1 Eintraege" zaehlte die FALSCHE Liste: die Favoriten-Liste
       id=30 mit ListLen=1. Die echte Rezeptliste ist eine TreeList (id=45)
       und wurde nie erreicht.
    3. "0/40" ist der Zeichenzaehler des Suchfelds (id=26 -> id=17).

>>> DATENQUELLE: nicht Node-IDs, sondern die BENANNTEN Felder von
    AddonRecipeNote (ilspycmd 2026-08-08 gegen FFXIVClientStructs.dll). Der
    Dump bestaetigt jede Zuordnung: CurrentJobName id=10 "Alchemist",
    CurrentJobLevel id=7 "Stufe 5", SelectedRecipeName id=63,
    SelectedRecipeDifficulty id=66 (Label id=65 "Fertig mit"),
    SelectedRecipeDurability id=69 (id=68 "Belastbar bis"),
    SelectedRecipeMaximumQuality id=74 (id=71 "Qualitaet"),
    ...QuantityCraftable... id=78 (id=77 "Herstellbar"),
    Ingredients id=94..89 (Name id=18 als Item-Link -> ReadClean),
    Crystals id=83/82, CharacteristicsTexts id=54..50.
    Zusaetzlich verfuegbar und fuer die Sonde genutzt: AgentRecipeNote
    (SelectedCraftType/-RecipeCategory/-RecipeIndex) und die Spieldaten
    RecipeNote.Instance()->RecipeList (SelectedIndex, RecipeCount).

>>> GEBAUT:
    - RecipeNote in SpecialSetup- + SpecialUpdateAddons: der generische Pfad,
      der nur Rauschen lieferte, schweigt komplett.
    - OnRecipeNoteUpdate: sagt die HANDWERKERKLASSE beim Oeffnen an
      ("Handwerker-Notizbuch, Alchemist, Stufe 5") und erneut bei jedem
      Klassenwechsel - genau der Punkt aus der User-Ansage.
    - Zeile unter dem Cursor kurz ansagen (User-Entscheidung 2026-08-08):
      "Destilliertes Wasser, Stufe 1, 3 von 12". Fokus-Weg wie beim
      Bestiarium (ClimbToItemRenderer), weil Listen-Indizes in TreeLists bei
      Tastaturnavigation nachweislich stehen bleiben (Log 2026-07-12).
      Item-Link-Namen ueber ReadClean, unsichtbare Nodes raus -> das ist es,
      was den "NEU"-Muell entfernt.
      Namen fuehren vor Zahlen: das Spiel listet den Stufen-Chip ("St. 1",
      id=7) VOR dem Namen (id=6). Sortiert wird auf ZIFFERN, nicht auf "St.",
      damit es im englischen Client ("Lv. 1") genauso stimmt.
    - Strg+F10 im Notizbuch liest das ganze Rezept: Name, Klasse+Stufe,
      Fertig mit / Belastbar bis / Qualitaet maximal / Startqualitaet (nur
      wenn != 0) / Herstellbar / Im Beutel, dann jedes Material mit
      "n benoetigt, x NQ, y HQ" (User-Entscheidung: NQ und HQ IMMER beide,
      weil HQ-Material die Startqualitaet hebt), Kristalle und die
      Voraussetzungszeilen ("Empfohlen: Kunstfertigkeit min. 22").
      Steht VOR TryReadItemDetail, damit ein offener Material-Tooltip das
      Rezept nicht verdeckt.
    - Suchfeld: statt "0/40" jetzt der fenstereigene Label-Node
      (SearchHintText, "Rezeptsuche").
    - Der globale Fokus-Leser schweigt NUR fuer Listenzeilen des Notizbuchs
      (Zugehoerigkeit ueber den Node-Baum geprueft, nicht ueber "Fenster
      offen"). Knoepfe wie "Synthese"/"Eilsynthese" bleiben generisch
      ansagbar, sonst waeren sie nicht mehr ansteuerbar.

>>> KEIN VERLUST durch die Stilllegung geprueft: RecipeNote landet nicht mehr
    im Menue-Stack, aber der steuert nur die PFEILTASTEN-Navigation
    (Plugin.cs 1128 -> Navigate). Der User navigiert per Numpad, und der Stack
    haette hier ohnehin die Favoriten-Liste mit 1 Eintrag bedient.

>>> NEUE ANSAGEN bilingual: RecipeNoteOpened, RowWithPosition,
    RecipeDifficulty, RecipeDurability, RecipeMaxQuality, RecipeStartQuality,
    RecipeCraftable, RecipeInBag, RecipeMaterial, RecipeCrystal,
    RecipeNoSelection.

>>> OFFEN, EHRLICH VERMERKT - drei Punkte, die die Quelle nicht beantwortet:
    1. Ob der Fokus-Weg der Numpad-Navigation in DIESEM Fenster folgt, ist
       nicht gemessen (im Bestiarium tut er es, das ist keine Garantie). Die
       Debug-Sonde [RecipeProbe] loggt bei jeder Aenderung Fokus-Node,
       Detail-Name und die Agent-Indizes - falls der Fokus-Weg tot ist, zeigt
       das Log sofort, welcher Agent-Index stattdessen traegt.
    2. Auf WELCHE der beiden TreeLists AddonRecipeNote.RecipeList zeigt
       (id=45 Rezepte oder id=39 Stufenbereiche) ist unbelegt - nur die
       Rezeptliste darf die "x von y"-Position liefern. Die Sonde loggt die
       Node-Id mit.
    3. Die Klassen-REITER selbst (TabButtons, 9 Stueck) sind icon-only und
       werden beim Durchtabben nicht benannt. Abgedeckt ist bisher nur der
       Fall, dass ein Reiterwechsel den Fensterinhalt umstellt - dann greift
       die Klassen-Ansage. Ob das Durchtabben allein schon umstellt: ungeprueft.

>>> KRISTALLNAMEN bewusst NICHT erfunden: CrystalNodes traegt Image, aber
    keinen Namens-Node (ilspycmd). Die Ansage lautet "Kristall, 1 benoetigt,
    256 im Beutel". Ein Weg ueber das Recipe-Sheet waere moeglich, ist aber
    noch nicht gebaut.

>>> SAMMLER-NOTIZBUCH: noch offen. In FFXIVClientStructs gibt es dafuer KEIN
    Addon-Struct mit benannten Nodes (nur AgentGatheringNote + Game.UI.
    GatheringNote), also braucht es dort einen eigenen UI-Dump.

>>> Build Debug 0 Warnungen / 0 Fehler, 10 Dateien deployt.

>>> IM SPIEL BESTAETIGT (Live-Log 2026-08-08 10:20-10:23), die drei offenen
    Punkte von oben sind damit erledigt:
    - "Handwerker-Notizbuch, Alchemist, Stufe 5" beim Oeffnen. STIMMT.
    - "Destilliertes Wasser, St. 1, 1 von 8" ... "Gegengift, St. 5, 8 von 8"
      beim Blaettern - der Fokus-Weg TRAEGT also auch in diesem Fenster.
    - tree=id45: RecipeList zeigt auf die RICHTIGE Liste (Rezepte), nicht auf
      die Stufenbereiche. Die Position ist damit belegt korrekt.
    - Agent-Indizes UND gameList[sel] folgen der Navigation synchron 0..7,
      stehen also als Ersatzquelle bereit, falls der Fokus-Weg je ausfaellt.
    - Stufenbereiche ("1-5", "6-10") und "Favoriten" werden gelesen.
    - Strg+F10 liefert: "Gegengift. Alchemist Stufe 5. Fertig mit 33.
      Belastbar bis 60. Qualitaet maximal 150. Herstellbar 0. Im Beutel 5".

>>> VIER FEHLER AUS DEMSELBEN LOG, drei davon gefixt:
    1. "NEU" kam WEITER (10:20:10.907). Ursache: der Fokus lag auf dem
       TreeList-CONTAINER (id=39), nicht auf einer Zeile - die Pruefung auf
       einen Zeilen-Renderer liess das durch. Jetzt schweigt der generische
       Leser auch fuer den Container (IsInsideListComponent).
    2. "0/40" kam WEITER (10:21:38). Ursache: SearchHintText ist der
       Platzhalter IM Feld und liest leer. Fallback auf den sichtbaren
       Label-Node daneben (Dump id=25 "Rezeptsuche").
    3. Gesprochen wurde "St. 1" statt "Stufe 1". Jetzt wie beim Sammel-Fenster
       expandiert (Ziffern bleiben unangetastet).
    4. OFFEN: die MATERIALIEN fehlen in der Detail-Ansage komplett - die
       Ausgabe bricht nach "Im Beutel 5" ab, obwohl das Fenster Materialien
       hat (der Cursor des Users lief Minuten vorher ueber die Slots).
       NICHT geraten: die Struct-Offsets sind geprueft und sauber
       (CrystalNodes 32 B + IngredientNodes 144 B kacheln ab 1280 lueckenlos),
       es muss also Laufzeit-Zustand sein. Sonde [RecipeMat] gebaut, die BEIDE
       Quellen nebeneinander loggt: die Addon-Nodes (aktuell benutzt) und die
       Spieldaten RecipeEntry.Ingredients (Name/Amount/NQCount/HQCount, ganz
       ohne UI-Nodes). Wer echte Werte traegt, gewinnt.

>>> NEBENBEFUND, noch nicht angefasst: beim Fokussieren der Material-Slots
    sprach der generische Item-Resolver unpassende Namen ("Phantasmasalz,
    Gegenstandsstufe 547", "Federfall-Giftschlange, 430") bei einem
    Stufe-5-Rezept - die Icon->Item-Aufloesung greift dort offenbar daneben.
    Erst nachgehen, wenn die Materialien selbst sauber gelesen werden.

>>> NAECHSTES FENSTER, vom User schon gedumpt (2026-08-08 10:23): "Synthesis"
    (101 Nodes) - das eigentliche Handwerken. Traegt Zustand ("Ausreichend"),
    Belastbar 40/40, Qualitaet 0/80, Fortschritt 0/9, Schritt 1, HQ-Chance
    1 %, Rezeptname. Dazu "CraftActionSimulator" (Synthese-Planer). Fuer
    Synthesis gibt es ein AddonSynthesis-Struct in FFXIVClientStructs, also
    voraussichtlich wieder benannte Felder statt Node-Raten.

## VORHERIGER STAND (2026-08-07, "ALLES ANLAUFBAR, WAS DAS NETZ HAT" - GEBAUT, UNGETESTET)

>>> USER-ANSAGE: "manche sachen sind nicht erreichbar ... ich will das aber
    nicht mit jedem objekt machen, also das ich dir alle sagen muss die nicht
    erreichbar sind - es sollen alle angelaufen werden koennen die das navmesh
    hat." Also KEINE Einzelfall-Flicken mehr, sondern die Ursachen.

>>> IM LOG STANDEN FUENF ABBRUECHE, ZWEI VERSCHIEDENE URSACHEN:
    - waagerecht knapp daneben: Uebergang Nordwald 9,1 m, Uebergang Westliches
      La Noscea 4,1 m, Chocobo-Staelle 5,0 m.
    - falsche ETAGE: Aetheryt Herbstkuerbis-See, waagerecht nur 2,7 m, aber
      9,0 m Hoehenunterschied.
    - und ein Ausreisser: Quest "Halb getanzt ist ganz verheimlicht", 139,7 m.

>>> URSACHE DER ETAGEN-FAELLE, OFFLINE BELEGT (Cache f1f4, Suedwald):
    Kartendaten sind 2D, die Zielhoehe wird geraten - und zwar mit der
    SPIELERHOEHE als Referenz. Ueber XZ (-44|228) liegen ZWEI Netz-Ebenen:
    Y -49 (nicht erreichbar) und Y -39 (erreichbar). Der Spieler stand 232 m
    entfernt auf Y -54, also gewann die naechstliegende - die falsche. Der
    echte Pfad endete korrekt auf Y -40. Nicht der Weg fehlte, die geratene
    Hoehe war falsch.
    -> Das betrifft JEDEN Kartenmarker (Orte, Aetheryte, Uebergaenge,
       Questziele, getippte Koordinaten), nicht diesen einen Aetheryt.

>>> GEBAUT - drei Faelle, in dieser Reihenfolge geprueft:
    1. FALSCHE ETAGE: Zielhoehe war geraten (neues Flag `_destHeightIsGuess`,
       von `TryResolveMarkerDestination` durchgereicht - Spielobjekte tragen
       ihre echte Hoehe und sind ausgenommen), waagerecht <= 15 m, Hoehe
       >= 2 m. Dann glaubt der Code dem Wegenetz statt der Schaetzung und
       zielt auf Marker-XZ mit der Hoehe des Pfadendes. Genau das trennt den
       Fall vom Astalicia-Schiff, wo die Hoehe BEKANNT war und die Luecke
       echt: dort bleibt es beim Abbruch.
    2. KNAPP DANEBEN, gleiche Ebene: wie vorher - bis zum Pfadende laufen,
       Rest ohne Wegsuche fahren.
    3. ALLES ANDERE: statt "nicht erreichbar" laeuft jetzt automatisch die
       schon vorhandene Zugangssuche (Ringe ums Ziel, naechster erreichbarer
       Punkt) - im neuen STILLEN Modus, also ohne die Zwischenansagen von
       `/acc zugang`. Nur wenn sie gar nichts findet, wird das gesagt.

>>> ERWARTUNGSWERTE OFFLINE GEGENGERECHNET fuer den Aetheryt-Fall: das
    korrigierte Ziel <-44,0|-40,0|228,0> liegt auf erreichbarem Netz, und der
    Weg dorthin endet mit 0,00 m Abstand exakt darauf (19 Wegpunkte). Der
    Spieler landet also auf dem Marker, nicht daneben.

>>> NICHT GEPRUEFT, ehrlich vermerkt: der 139-m-Quest-Fall. Ihn faengt Fall 3
    ab, aber ob die Ringsuche dort etwas findet, weiss ich nicht - Zone und
    Zielkoordinate stehen nicht im Log. Fall 3 kostet ausserdem viele
    Wegsuchen und kann ein paar Sekunden brauchen, bevor die Figur losgeht.

>>> IM SPIEL BESTAETIGT (Live-Log 2026-08-07, beide Mechaniken):
    - RESTFAHRT: 23:12:24 "Letztes Stueck: fahre die restlichen 9,9 m nach
      Norden ohne Wegsuche zu 'Uebergang nach Nordwald'", 23:12:26 "Gebiet
      gewechselt (148 -> 154), erreicht". Der Zonenwechsel ist der Beweis,
      dass die Figur wirklich IN den Uebergang gelaufen ist - genau der
      Punkt, an dem der Lauf vorher gar nicht erst losging.
    - ETAGEN-KORREKTUR: 23:14:14 "umgeleitet (falsche Etage): die geratene
      Zielhoehe lag 9,0 m neben der begehbaren. Neues Ziel <-44,0, -40,0,
      228,0>", 23:14:47 "Ziel erreicht: Herbstkuerbis-See." Der offline
      vorausberechnete Punkt war exakt dieser.
    - Fall 3 (automatische Zugangssuche) ist dabei NICHT ausgeloest worden,
      steht also weiter aus.

>>> DANACH NEUE USER-MELDUNG: "ich bin grad bei einem etheryten, konnte auch
    hinlaufen, aber er wird nicht markiert so das ich in nutzen kann."
    URSACHE: Die Aetheryten-Kategorie browst KARTENDATEN (PlacesService), dort
    gibt es kein Spielobjekt - also auch kein Ziel, und ohne Ziel keine
    Benutzung. Kein Fehler im Lauf, eine Luecke im Konzept der
    Marker-Kategorien.
    GEBAUT: `TryTargetMarkerObject` - bei jeder Marker-Auswahl wird geprueft,
    ob das echte Objekt (ObjectKind.Aetheryte) geladen und hoechstens 15 m vom
    Marker entfernt ist; dann wird es als Spielziel gesetzt und die Ansage
    haengt "Angezielt." an. 15 m deckt die Pixel-Ungenauigkeit der Marker ab,
    ohne den naechsten Aethernet-Splitter zu erwischen. Findet sich nichts,
    bleibt alles still - sonst wuerde das Blaettern durch weit entfernte
    Aetheryten staendig kommentiert. `_ownSelectionId` wird gesetzt, damit der
    Ziel-Waechter die Markerauswahl nicht verwirft.
    IM SPIEL BESTAETIGT (User, 2026-08-07: "ok funktioniert") - der Aetheryt
    steht also als ObjectKind.Aetheryte in der ObjectTable und das Spiel nimmt
    das Anvisieren an. Damit ist der Marker benutzbar.

>>> NEUE ANSAGE bilingual: `MarkerTargeted`.

>>> Build Debug 0 Warnungen / 0 Fehler, 10 Dateien deployt.

## VORHERIGER STAND (2026-08-07, WEG ENDET KURZ VORM ZIEL - GEBAUT, UNGETESTET)

>>> USER-MELDUNG: "manchmal hat der das problem das er wege nicht findet
    obwohl es glaub gehen muesste, schau in die log".

>>> IM LOG GEFUNDEN (2026-08-07 21:51, dreimal hintereinander): Auto-Lauf zum
    "Uebergang nach Nordwald" (Zentralwald, Map 4), 652 m. vnavmesh findet
    einen vollstaendigen Pfad ueber 62 Wegpunkte - der endet bei
    <-506,2|74,2|-354,5>, das Ziel liegt bei <-503,0|74,8|-363,0>. 9,1 m
    Luecke, erlaubt waren 3,5 m -> harter Abbruch, "dorthin fuehrt kein Weg".

>>> OFFLINE NACHGEMESSEN, NICHT VERMUTET (vnavmesh-Cache `f1f1`, Werkzeug wie
    bei der Astalicia, Breitensuche ueber die Polygon-Verbindungen):
    - Vom Spieler erreichbar: 22.937 Polygone.
    - Der Zielpunkt liegt auf einer ABGETRENNTEN Flaeche von 57 Polygonen
      (X -515..-484, Y 74,2..78,8, Z -392..-356) - keine einzige Verbindung.
    - DER BODEN IST DURCHGEHEND. Strecke alle 0,5 m abgetastet: an JEDEM Punkt
      liegt Netz, Hoehe steigt sanft 74,2 -> 74,8. Kein Loch, keine Wand - nur
      die Polygon-Verknuepfung fehlt. Die Trennung sitzt zwischen 0,5 m und
      1,0 m hinter dem Pfadende. Beide Polygone liegen sogar im selben Tile
      (Refs ...90002C und ...900017).
    - Naechster erreichbarer Punkt am Ziel: 8,5 m davor.
    -> Die Ansage war technisch richtig und praktisch irrefuehrend: 650 m Weg
       wurden wegen der letzten 9 m verweigert, die man zu Fuss einfach geht.
    -> NICHT belegt und daher NICHT behauptet: dass alle Zonenuebergaenge so
       gebaut sind. Gemessen ist genau dieser eine.

>>> ENTSCHEIDUNG DES USERS (gefragt, weil sie den harten Abbruch aus der
    Vorsession beruehrt): Bis zum letzten erreichbaren Punkt HINLAUFEN und den
    Restweg ansagen. Grenze 15 m waagerecht, darueber bleibt der Abbruch.

>>> GEBAUT (AutoWalkService):
    - `NearMissGap` = 15 m waagerecht. Zusaetzlich muss der Hoehenunterschied
      unter `ImpossibleRise` (2 m) liegen - das ist die schon vorhandene,
      begruendete Schwelle fuer "kann die Figur steigen", keine neue Zahl. Sie
      trennt diesen Fall vom Astalicia-Fall, wo das Ziel 9,1 m SENKRECHT ueber
      dem Pfadende lag: dort haette "9 m vor dem Ziel" geheissen, direkt
      darunter zu stehen.
    - Umleitung laeuft ueber `_pendingNearMissWalk` (naechster Frame, wie
      Zugangs-Suche und Planke) - der Check sitzt in der Wegpunkt-Auswertung
      des Laufs, den er ersetzt.
    - `_nearMissGoal` haelt das echte Ziel waehrend des umgeleiteten Laufs;
      geloescht in `Stop()` und in `BeginWalk()`, damit es nie in einen
      spaeteren, fremden Lauf leckt. Beide Endzweige (sauberes Pfadende UND
      "keine Bewegung seit 5 s") lesen es vor dem Loeschen.
    - Statt "Ziel erreicht" (waere eine Luege, die der User nicht pruefen kann)
      kommt Entfernung + Himmelsrichtung.

>>> NEUE ANSAGEN bilingual: `NearMissRedirect`, `NearMissArrived`.

>>> README DE + EN: Auto-Lauf-Zeile nennt das neue Verhalten.

>>> NACHTRAG, USER-WUNSCH direkt danach: "er soll direkt bis zu den
    uebergaengen laufen so wie navmesh es macht" - also nicht 9 m davor
    absetzen, sondern die letzten Meter mitfahren, damit der Uebergang
    ausloest.

>>> DAFUER GEBAUT (dieselbe Mechanik wie die Planke, die in-game schon
    getragen hat - `Path.MoveTo` faehrt eine feste Punktliste OHNE Wegsuche):
    - `FinishNearMiss` haengt am Pfadende die Restfahrt an.
    - `GroundIsContinuous` ist die Sicherung davor und der Grund, warum blind
      fahren hier vertretbar ist: die Strecke wird alle 1 m abgetastet, an
      jedem Punkt muss Netz in einer engen Box liegen (1 m waagerecht, 1,5 m
      hoch), und zwischen den Punkten darf kein Sprung stehen, den die Figur
      nicht steigen kann (dieselbe Regel wie `RouteHasImpossibleJump`). Fehlt
      irgendwo Boden, wird NICHT gefahren, sondern der Restweg angesagt.
      Ohne diese Pruefung wuerde die Figur ueber die Kante gesteuert.
    - `FinalHopUpdate` beobachtet die Fahrt von aussen, weil `Path.MoveTo`
      nichts zurueckmeldet: Zonenwechsel = angekommen, 2 m ans Ziel =
      angekommen, nach 20 s Aufgabe mit ehrlicher Rest-Ansage.
    - Die Auto-Lauf-Taste bricht auch die Restfahrt ab (`StopFinalHopIfRunning`)
      - fuer den Spieler ist sie der Schwanz desselben Laufs.
    - `MoveWithoutPathfinding` + `NearestMeshPoint` sind dafuer aus dem
      `#if DEBUG`-Block der Planke herausgezogen worden; der Planken-Befehl
      selbst bleibt Debug.

>>> ANSAGEN WIEDER ENTFERNT auf Zuruf des Users, noch vor dem ersten Test:
    "ich will nicht das er ansagt wann er stopt, das ist evtl zu viel info,
    ich werd ja sehen wie weit er vom ziel weg ist". Geloescht sind alle drei
    frisch gebauten Bausteine (`NearMissRedirect`, `FinalHopStarting`,
    `NearMissArrived`) - tot ist tot, sie stehen nicht als Leichen herum; an
    ihrer Stelle steht eine Notiz in AccessibilityStrings, damit sie niemand
    aus Versehen neu erfindet. Der ganze Ablauf laeuft jetzt STILL durch.
    NICHT still bleibt der Fall, in dem der Lauf ohne Ankunft endet - dann
    kommt die laengst vorhandene Standardzeile "Auto-Lauf beendet, noch X
    Meter.", die JEDER abgebrochene Lauf schon immer gesprochen hat. Stille
    waere hier das eine, was der User nicht von Erfolg unterscheiden kann.

>>> Build Debug 0 Warnungen / 0 Fehler, 10 Dateien deployt. IM SPIEL
    UNGETESTET. ERWARTUNGSWERTE fuer genau den Log-Fall vorausberechnet
    (Uebergang nach Nordwald, aus Zwieselgrund):
    (a) beim Start nur "Laufe zu Uebergang nach Nordwald." - sonst nichts.
    (b) die Figur laeuft die vollen ~650 m (vorher lief sie gar nicht los)
        und faehrt die letzten 9 m ohne weitere Ansage durch.
    (c) am Ende "Angekommen, neues Gebiet erreicht." - der Zonenwechsel ist
        der eigentliche Beweis, dass die Figur bis in den Uebergang gekommen
        ist. Kommt sie nicht rein: nach 20 s "Auto-Lauf beendet, noch 9
        Meter."
    (d) im Log: "[Nav] Auto-Lauf umgeleitet ...", dann "[Nav] Letztes Stueck:
        fahre die restlichen 9,1 m nach Norden ohne Wegsuche ...".
    DIE BODENPRUEFUNG IST OFFLINE GEGENGERECHNET (gleicher Cache, gleiche
    Parameter wie im Code): 10 Abtastpunkte, groesste Abweichung zum Netz
    0,40 m (erlaubt 1 m), groesste Stufe 0,08 m -> Restfahrt startet JA.
    Gegenprobe, dass die Sicherung noch greift: ein Ziel auf einem echten
    anderen Stockwerk muss weiterhin "ist nicht erreichbar" sagen.

## VORHERIGER STAND (2026-08-07, HP/MP/SP IN PROZENT - GEBAUT, UNGETESTET)

>>> USER-WUNSCH: "ich wurde darauf hingewiesen das es besser waere hp und mp in
    prozent anzuzeigen, da das spiel wohl schon einen hp prozent leser hat; man
    sieht die hp / den balken wohl auch nur in prozent."

>>> ENTSCHEIDUNG DES USERS (gefragt, weil es das Ansage-Format aller Vitalwerte
    aendert): NUR Prozent, absolute Zahlen fallen ganz weg. SP/GP wird
    mitumgestellt (User hat sich ausdruecklich fuer "SP auch in Prozent"
    entschieden, gegen meine Empfehlung, die absolute SP-Kosten anfuehrte).

>>> HISTORIE VORHER GEPRUEFT, NICHT ANGENOMMEN: Die Ansage WAR frueher schon
    Prozent. Commit 01a144c (V5.31, 22.07.2026) hat sie auf "X von Y" gedreht -
    laut derselben STATUS-Datei als "vorbestehende, bis dahin UNDOKUMENTIERTE +
    vermutlich ungetestete WIP-Arbeit aus fruehrer Session" mitgezogen, ohne
    festgehaltene Begruendung und ohne User-Wunsch. Wir kehren also zum
    aelteren Verhalten zurueck, nicht gegen eine bewusste Entscheidung.

>>> GEBAUT - eine einzige Rechenstelle, `AccessibilityStrings.Percent(cur, max)`:
    - abgerundet (Integer-Division, dieselbe Formel wie das schon vorhandene
      `CombatService.HpPercent`), damit "50 Prozent" nie "knapp unter der
      Haelfte" heisst.
    - EINE bewusste Klemmung unten: 5 von 5000 HP wuerde auf 0 abrunden, und
      "HP 0 Prozent" bei lebender Figur klingt wie tot. Alles ueber 0 meldet
      daher mindestens 1 Prozent; die 0 bleibt der leeren Leiste vorbehalten.
      (Nach oben ist keine Klemmung noetig - abgerundet wird 100 nur bei
      cur == max erreicht.)
    - max == 0 gibt 0 - unveraendertes Verhalten fuer Jobs ohne Mana.

>>> UMGESTELLT (alle Aufrufer bleiben unveraendert, die Signaturen nehmen
    weiterhin cur/max):
    - `HpSentence` - eigene HP-Schwelle im Kampf (75/50/25/10).
    - `TargetHpSentence` - Ziel-HP-Schwelle im Kampf.
    - `TargetHpFragment` - HP-Anhang beim Anvisieren / im Objekt-Browser
      (NavigationService.DescribeTargetHp).
    - `VitalStatus` - Strg+Entf bzw. `/acc status`, eigene HP + MP.
    - `TargetStatusClause` - Ziel-Anhang derselben Statusabfrage.
    - `GpValue` - Strg+Ende, SP-Stand.
    Alle sechs bilingual DE/EN ("Prozent" / "percent"), passend zu den schon
    vorhandenen Prozent-Ansagen (FATE-Fortschritt, Wegenetz-Ladestand).

>>> GELOESCHT: `HpValue`/`MpValue` - Bausteine ohne einen einzigen Aufrufer.
    Dass sie tot waren, ist nicht vermutet: Grep ueber das ganze Repo fand nur
    ihre Definition, und der Build danach ist 0/0.

>>> LOGS BLEIBEN ABSOLUT (z. B. `[SP] 480/600`) - fuer die Fehlersuche ist der
    Rohwert die bessere Quelle, gesprochen wird Prozent.

>>> README DE + EN: Kampfstatus-Zeile nennt jetzt das Prozent-Format.

>>> Build Debug 0 Warnungen / 0 Fehler, 10 Dateien nach devPlugins deployt.

>>> IM SPIEL BESTAETIGT (User, 2026-08-07): "das mit den prozenten
    funktioniert". Damit ist die Umstellung durch.

## VORHERIGER STAND (2026-08-07, PLANKE IN BEIDE RICHTUNGEN - GEBAUT, UNGETESTET)

>>> USER-MELDUNG: "ich kann jetzt nichts mehr anlaufen auch keine questziele".

>>> WAS WIRKLICH LOS WAR (am Log belegt, nicht vermutet): Die Figur stand auf
    dem abgetrennten SCHIFFSNETZ der Astalicia. Alle 13 Abbrueche der Sitzung
    00:14-00:46 kamen von dort. Die Route 00:46:29 startet bei
    <-271,2|11,9|189,6> und fuehrt nur noch das Schiff HINAUF (12,0 -> 15,0 ->
    16,0 -> 17,8 -> 19,2 -> 24,2), Ende oben auf dem Oberdeck. Der gleiche
    Endpunkt bei verschiedenen Startpunkten kam daher, dass alle Startpunkte
    auf derselben isolierten Flaeche lagen - NICHT von einer Suchgrenze (das
    war meine erste, falsche Deutung).
    -> Die Ansagen "nicht erreichbar" waren also inhaltlich RICHTIG. Der Fehler
       lag woanders: `/acc planke` fuhr nur HIN. Das Feature hat den Spieler
       auf dem Schiff eingesperrt.

>>> ZWEITER, ECHTER FEHLER, den derselbe Log aufdeckt: Der Lauf 00:46:18 zur
    Uebergangsstelle sah erfolgreich aus ("angekommen=True"), sein echter Pfad
    endete aber bei <-272,8|12,0|190,0> - auf der SCHIFFSSEITE der 1,2-m-
    Luecke, 1,3 m vom Kai-Punkt entfernt. Die 3-m-Toleranz `UnreachableGap`
    hat das durchgewunken. Hinueber ist die Figur nie gekommen.

>>> ENTSCHEIDUNG DES USERS (gefragt, weil sie das Grundverhalten aendert):
    (a) Harter Abbruch bei "kein Weg" BLEIBT wie er ist - kein Loslaufen auf
        Verdacht. Der Preis (ein Fehlalarm macht bewegungsunfaehig) ist
        ausdruecklich in Kauf genommen.
    (b) Die Planke bekommt einen Rueckweg.

>>> GEBAUT:
    1. `RouteReachesSpot(route, spot, from)` - eine Pruefstelle fuer alle
       Punkte, die AUF dem Netz liegen (Zugangs-Kandidaten wie Uebergangs-
       stellen): Abstand des vorletzten Wegpunkts <= `ApproachSnapTolerance`
       (1 m, nicht 3 m) UND kein unmoeglicher Sprung. Genau die 1,3-m-
       Fehldiagnose von oben faellt damit durch. Die Kandidatenpruefung in
       AnnounceApproach benutzt jetzt dieselbe Methode statt eigener Kopie.
       Die ZIELpruefung des normalen Auto-Laufs bleibt unveraendert bei 3 m.
    2. `/acc planke` erkennt die Seite selbst und faehrt vorwaerts ODER
       rueckwaerts. Entschieden wird NICHT ueber die Hoehe - die beiden Seiten
       trennen nur 0,5 m und die Figur steht gewohnheitsmaessig dazwischen
       (gemessen: Y 11,9 auf der Schiffsseite, Kai liegt bei 11,5) - sondern
       ueber das Wegenetz: erreichbar ist die eigene Seite. Kai zuerst
       geprueft (Normalfall), sonst Deck, sonst Ansage "von hier fuehrt kein
       Weg zur Uebergangsstelle".
    3. Zonenpruefung: die Koordinaten gelten nur in TerritoryType 129 und 404
       (beide Bg `ffxiv/sea_s1/twn/s1t2/level/s1t2`, offline aus dem
       TerritoryType-Sheet gelesen 2026-08-07). Sonst Ansage und Abbruch -
       vorher haette der Befehl in JEDER Zone zu diesen Koordinaten gesteuert.
    4. Die Seitenpruefung laeuft auf einem Worker (Pfadsuchen sind async) und
       parkt das Ergebnis in `_pendingPlankRun`; `Update()` startet den Lauf
       im naechsten Frame - vor dem Ueberquerungs-Block, dessen `!_active`
       sonst im selben Frame ausloesen wuerde.

>>> NEUE ANSAGEN bilingual: `GapCrossWrongZone`, `GapCrossNoSide`.

>>> Build Debug 0 Warnungen / 0 Fehler, nach devPlugins deployt.

>>> IM SPIEL BESTAETIGT (User, 2026-08-07): "ja funktioniert, /acc planke hat
    mich wieder weg gefuehrt" - der Rueckweg vom Schiff greift, die
    Seitenerkennung ueber das Wegenetz liegt richtig. Damit ist die
    Einbahn-Falle (Spieler auf dem Schiff eingesperrt) behoben.

## STAND JETZT II (2026-08-07, VNAVMESH-ALTERNATIVE GEPRUEFT + BODENSONDE GEBAUT)

>>> USER-FRAGE: "gibt es eine alternative zu vnavmesh das aktueller ist?"

>>> ANTWORT: NEIN, und ein Wechsel wuerde auch nichts bringen.
    - Installiert 1.2.3.10 (DLL 25.07.2026), neueste ist 1.2.3.11 (29.07.2026).
      Also praktisch aktuell.
    - Es gibt keinen Konkurrenten. Questionable, GatherBuddyReborn, AutoDuty
      benutzen vnavmesh SELBST. Das einzige "andere" ist ein chinesischer Fork
      desselben Codes (AtmoOmen/ffxiv_navmesh-cn).
    - Das Problem ist nicht das Plugin, sondern dass das Wegenetz aus der
      Kollisionsgeometrie GEBAUT wird und dabei Flaechen verliert.

>>> BAU-PARAMETER aus der installierten DLL per Reflection ausgelesen
    (Navmesh.NavmeshSettings, 2026-08-07):
    AgentHeight 2, AgentRadius 0,5, AgentMaxClimb 0,5, AgentMaxSlopeDeg 55,
    CellSize/CellHeight 0,25, GenerateEdgeClimbLinks FALSE,
    GenerateEdgeJumpLinks FALSE, ClimbDownMinHeight 1,5, ClimbDownMaxHeight
    3,2, ClimbDownDistance 0,4, EdgeJumpMinDrop 1,5, EdgeJumpHeight 1,8.

>>> VNAVMESH HAT EINEN VORGESEHENEN MECHANISMUS FUER GENAU DIESES PROBLEM:
    `NavmeshCustomization` je Gebiet. 35 Zonen haben schon eine, darunter
    Z0128LimsaLominsaUpperDecks (128) - Inhalt dort nur eine Zeile,
    `Settings.AgentRadius = 0.75f`. Fuer UNTERE Decks (129) gibt es keine.
    `CustomizeScene` ist im Original dokumentiert als "customization point to
    add or remove colliders in the scene".

>>> VERDACHT (NICHT BELEGT, ausdruecklich als Vermutung markiert): Die
    Kanten-Links sind vermutlich NICHT unsere Loesung - sie greifen erst ab
    1,5 m Hoehenunterschied (ClimbDownMinHeight/EdgeJumpMinDrop), unsere Luecke
    hat aber nur 0,5 m. Naeher liegt die Erosion: AgentRadius 0,5 nimmt von
    JEDER Kante 0,5 m weg, eine Laufplanke unter 1,0 m Breite verschwindet
    damit vollstaendig aus dem Netz.

>>> DIE GEPLANTE OFFLINE-MESSUNG IST NICHT MOEGLICH. `SceneDefinition` kennt
    nur `FillFromActiveLayout()` und `FillFromLayout(LayoutManager*)` - die
    Kollisionsgeometrie kommt aus dem LAUFENDEN SPIELPROZESS, nicht aus
    sqpack-Dateien. Ein Neubau des Netzes am Rechner scheidet damit aus.

>>> ERSATZ GEBAUT - `/acc boden` (DEBUG): stellt den Kollisionsboden des
    SPIELS dem Wegenetz gegenueber. Raster +/-3 m um den Spieler in 0,25-m-
    Schritten (= CellSize, also kann die Sonde nichts sehen, was der Netzbau
    nicht auch gesehen haette); je Punkt ein Strahl von 3 m ueber Kopfhoehe
    nach unten via `BGCollisionModule.RaycastMaterialFilter` (statische
    Ueberladung, unabhaengig von vnavmesh), dann Vergleich mit
    `NearestPoint` in einer ENGEN Box (0,5 m) - die weite Box von SnapToMesh
    (3 m / 15 m) wuerde fast ueberall "ja" sagen.
    Geloggt werden Treffer gesamt, mit Netz, ohne Netz, und je Fehlstelle
    Position und Neigung (ueber 55 Grad darf der Bau ohnehin verwerfen).

>>> SO IST DAS ERGEBNIS ZU LESEN:
    - Viele Treffer OHNE Netz an der Planke = der Boden ist da, das Netz
      verwirft ihn -> eine Zonen-Anpassung koennte die Stelle richtig
      reparieren, und alle vergleichbaren Stellen mit.
    - Keine Treffer ueber der Luecke = dort ist wirklich kein Boden -> nur
      `Path.MoveTo` kann hinueber, so wie es heute schon laeuft.

>>> NACHTRAG: HELFEN DIE ZWEI SCHALTER? NEIN - am Generierungscode belegt.
    Edge-Climb-Links spannen `-ClimbDownMaxHeight` bis `-ClimbDownMinHeight`
    (-3,2 bis -1,5 m) bei einer Reichweite von `CellSize + 2*AgentRadius +
    ClimbDownDistance` = 1,65 m; Edge-Jump-Links -500 bis -1,5 m bei 2 m.
    BEIDE verlangen mindestens 1,5 m Hoehenunterschied und sind reine
    ABWAERTS-Verbindungen. Unsere Luecke hat 0,5 m - sie ist fuer diese
    Schalter zu KLEIN, nicht zu gross. Die Reichweite waagerecht (1,65 m gegen
    1,2 m) haette sogar gereicht.

>>> KANN DER USER SIE IM SPIEL ANSCHALTEN? PRAKTISCH NEIN:
    - `NavmeshSettings.Draw()` ist public, es gibt also eine Oberflaeche. Das
      einzige Feld dieses Typs ausserhalb von Builder und Zonen-Anpassungen
      sitzt aber in `Navmesh.Debug.DebugNavmeshCustom+Customization` - also im
      Debug-Teil, an einem separat gebauten Testnetz. (Aus der Feldverteilung
      geschlossen; welche Methode Draw() aufruft, wurde nicht im IL verfolgt.)
    - NICHT GESPEICHERT: `Navmesh.Config` - die Klasse mit Save/Load - haelt
      kein NavmeshSettings-Feld. Nach einem Neustart waere alles zurueck.
    - Und es ist ImGui: fuer NVDA nicht lesbar.

>>> Build Debug 0 Warnungen / 0 Fehler, deployt. IM SPIEL UNGETESTET.
    Zu tun: an der Planke stehen und `/acc boden` eingeben.

## VORHERIGER STAND (2026-08-06, ZUGANGS-ERKENNUNG - GEBAUT, UNGETESTET)

>>> USER-WUNSCH: "wo ich nicht zu dem npc kommen konnte ist auf einem schiff und
    da gibt es treppen die muesste man evtl anlaufen koennen kannst du rausfinden
    bei welchen coords die treppen sind so das man da wegpunkte macht"

>>> ANTWORT AUF DIE FRAGE: Es gibt dort KEINE Treppe im Wegenetz, auf die man
    Wegpunkte setzen koennte. Das Schiff ist eine vollstaendig abgetrennte
    Flaeche. Wegpunkte helfen prinzipiell nicht.

>>> WIE GEMESSEN (kein Test im Spiel noetig, keine Vermutung): vnavmesh legt
    jedes gebaute Wegenetz unter pluginConfigs\vnavmesh\meshcache\*.navmesh ab.
    Ein Konsolenprogramm im Scratchpad laedt die Datei von Limsa Lominsa
    (sea_s1_twn_s1t2, gebaut 2026-08-06 19:42) ueber den ORIGINALCODE von
    vnavmesh (Navmesh.Deserialize, Verweis auf vnavmesh.dll + DotRecast) und
    verfolgt die Polygon-Verbindungen wie NavmeshQuery.FindReachableMeshPolys.
    - Kai-Flaeche des Spielers: 1468 Polygone, Y 6,0 bis 11,5.
    - Schiffsflaeche mit dem NPC: 129 Polygone, Y 12,0 bis 24,8.
    - Verbindungen zwischen beiden: NULL.
    - Engste Stelle: Kai (-274,0 | 11,5 | 190,0) -> Schiff (-272,8 | 12,0 |
      190,0), nur 1,2 m waagerecht und 0,5 m hoch (die Laufplanke). Alle
      anderen Kandidaten haben 3,5 m Hoehenunterschied = Wand.
    - Beide Teilstrecken sind fahrbar: Spieler -> Kai-Punkt 11 Wegpunkte /
      32,8 m; Schiff-Punkt -> NPC 13 Wegpunkte / 39,9 m ueber den Schiffs-
      aufgang Deck 12 -> 15 -> 16,5.

>>> GEGEN DIE REALITAET GEPRUEFT: Dieselbe Wegsuche offline auf Spieler -> NPC
    endet nach 0,9 m - exakt der Fehlschlag aus dem Log vom 06.08. 20:03.

>>> DAMIT IST DIE ALTE DEUTUNG WIDERLEGT (STATUS 2026-08-01: "vnavmesh kann
    keine senkrechten Aufstiege"): Der "9-Meter-Sprung" war nie eine Route.
    NavmeshQuery.PathfindMesh haengt das ZIEL immer als letzten Wegpunkt an,
    auch wenn die Suche es nie erreicht hat (dekompiliert 2026-08-06). Der
    VORLETZTE Wegpunkt ist das Ende des echten Pfades. Es ist keine
    Steilheits-Grenze, sondern eine fehlende Verbindung.

>>> GEBAUT, zwei Teile in AutoWalkService:
    1. `RouteReachesGoal` + Pruefung beim Auto-Lauf-Start: ist das Ziel nicht
       erreichbar, wird SOFORT abgebrochen und "X ist nicht erreichbar -
       dorthin fuehrt kein Weg" angesagt, statt stumm loszulaufen und nach
       zwei Metern stehenzubleiben. Toleranz = 3 m + stopRange (bei Quest-
       Zielen endet der Pfad absichtlich im Radius, sonst Fehlalarm).
    2. `AnnounceApproach` / Befehl `/acc zugang`: sucht den naechsten Punkt,
       an den man herankommt, und nennt Richtung, Gehstrecke, Restabstand und
       Hoehenunterschied. Kandidaten auf Ringen um das ZIEL, je 16 Richtungen
       und 4 Hoehenebenen zwischen Spieler- und Zielhoehe.

>>> ZWEI FALLEN, die die Vorausberechnung VOR dem Test aufgedeckt hat:
    - Sondiert man nur von der eigenen Fusshoehe, schnappt JEDER Kandidat auf
      die eigene Etage. Ergebnis waere "3 Meter suedwestlich" gewesen - der
      Spieler haette sich unter das Schiff gestellt. Daher 4 Hoehenebenen.
    - Nach reiner 3D-Naehe gewinnt immer ein Punkt direkt unter dem Ziel.
      Hoehe zaehlt deshalb 5-fach gegen waagerechte Naehe.

>>> ERWARTUNGSWERTE VORAUSBERECHNET (dieselbe Logik offline, 156 Kandidaten,
    58 erreichbar), so muss `/acc zugang` am Kai der Astalicia klingen:
    "Kein durchgehender Weg zu Mitglied der Galgenvoegel. Am naechsten kommst
    du 31 Meter nach Nordwesten. Von dort ist das Ziel noch 18 Meter entfernt,
    5 Meter ueber dir."  (bester Punkt (-276,4 | 11,5 | 187,7) - die obere
    Kai-Ebene, 3,3 m neben der Laufplanke)

>>> Build Debug 0 Warnungen / 0 Fehler, nach devPlugins deployt. IM SPIEL
    UNGETESTET. Offen: (a) sagt Numpad3 auf den NPC jetzt "nicht erreichbar"?
    (b) fuehrt `/acc zugang` auf die obere Kai-Ebene? (c) kommt man von dort
    ueber die 1,2-m-Luecke aufs Schiff - das ist der einzige Punkt, den die
    Messung NICHT beantworten kann.

>>> NOCH KEINE TASTE: `/acc zugang` ist bisher nur ein Befehl. Wenn sich das
    Feature bewaehrt, gehoert es auf eine Taste.

>>> ERSTER TEST IM SPIEL 2026-08-07 00:16 - BEIDE TEILE FUNKTIONIEREN:
    - Numpad3 auf den NPC: "Auto-Lauf abgebrochen ... Echter Pfad endet bei
      <-261,8, 7,5, 194,2>, 8,9 m vor dem Ziel (erlaubt waeren 5,5 m)" ->
      Ansage "ist nicht erreichbar - dorthin fuehrt kein Weg". Wie entworfen.
    - `/acc zugang`: 156 Kandidaten, 65 erreichbar, bester Punkt
      <-255,7 | 16,5 | 194,2>, 5 m Weg nach Osten, von dort noch 5,0 m zum
      Ziel bei 0,1 m Hoehenunterschied. Suche dauerte 5,3 s.

>>> DER GEFUNDENE PUNKT IST VIEL BESSER als die Vorausberechnung (die sagte
    31 m nach Nordwesten): er liegt auf DERSELBEN HOEHE wie der NPC und nur
    5 m entfernt. ABER die Abweichung ist noch nicht erklaert - offline galten
    58 von 156 Kandidaten als erreichbar, im Spiel 65. Verdacht: die
    Kandidatenpruefung lief mit der 3-m-Toleranz von UnreachableGap, die fuer
    auf das Netz geschnappte Punkte zu lasch ist.
    -> NACHGEZOGEN: eigene, strengere Schwelle `ApproachSnapTolerance` = 1 m
       fuer Kandidaten, und der Restabstand des besten Punktes wird jetzt
       geloggt ("Route endet X,XX m neben dem Punkt"). Beim naechsten Lauf
       zeigt diese Zahl, ob <-255,7 | 16,5 | 194,2> echt erreichbar ist
       (nahe 0,00) oder ein Grenzfall war.

>>> ZWEITER TEST 2026-08-07 00:25 - DER LAUF ZUM ZUGANGSPUNKT SCHLUG FEHL,
    UND DAS HAT EINEN ECHTEN FEHLER IM KRITERIUM AUFGEDECKT:
    Route zum Punkt <-255,7 | 16,5 | 194,2> war
    "(-260,9|7,4|194,2) -> (-255,7|16,5|194,2) -> (-255,7|16,5|194,2)",
    Ergebnis "angekommen=False, Hoehenunterschied=9,1 m". Der Punkt war NIE
    erreichbar - die Pruefung hat ihn mit "Route endet 0,00 m neben dem Punkt"
    durchgewinkt.
    URSACHE: Findet die Wegsuche GAR KEINEN Pfad, liefert sie nur das
    Startpolygon; FindStraightPath macht daraus [Start, Ziel] und PathfindMesh
    haengt das Ziel nochmal an. Damit IST der vorletzte Wegpunkt das Ziel und
    der Abstand misst null. Beim NPC fiel das nicht auf, weil dort ein
    Teilpfad ueber zwei Polygone lief. Erklaert auch 65 statt 58 Kandidaten:
    sieben Fehlalarme derselben Art.
    -> BEHOBEN: `RouteHasImpossibleJump` als ZWEITE, unabhaengige Pruefung.
       Ein Segment mit mindestens 2 m Anstieg, das mehr als das 1,5-fache
       seiner Bodenstrecke steigt, ist ein Loch im Wegenetz. Beide Pruefungen
       zusammen in `RouteIsWalkable`; die Abstandspruefung faengt Teilpfade,
       die Sprungpruefung die leeren. An echter Geometrie gegengeprueft:
       der Schiffsaufgang steigt 3,0 m auf 5,1 m (0,59), der Scheinsprung
       9,1 m auf 5,2 m (1,75) - die Schwelle 1,5 trennt sauber, keine
       Fehlalarme auf echten Treppen/Rampen.
    -> Damit hatte die urspruengliche Aufstiegs-Sonde in EINEM Punkt recht:
       die Steilheitspruefung ist noetig. Sie reicht nur nicht allein.

>>> DRITTER TEST 2026-08-07 00:30: DER SPRUNG-FIX GREIFT NICHT. Die falschen
    Punkte haben Steigungen von 1,12 und 1,32 (9,1 m hoch auf 8,1 bzw. 6,9 m
    Boden) - die Schwelle stand auf 1,5. Sie HOEHER zu setzen geht nicht:
    vnavmesh baut Flaechen bis `AgentMaxSlopeDeg = 55` Grad als begehbar
    (= Steigung 1,43, aus NavmeshSettings dekompiliert). Ein Phantomsprung mit
    1,12 liegt also INNERHALB dessen, was echte Geometrie sein darf.
    -> UEBER DIE STEILHEIT SIND DIE FAELLE NICHT UNTERSCHEIDBAR. Die
       Steilheitspruefung bleibt drin (sie faengt die krassen Faelle), taugt
       aber nicht als alleiniges Kriterium.
    -> Sauberer waere: liegt die MITTE eines Wegabschnitts auf begehbarem
       Boden? Bei einer echten Rampe ja, bei einem Sprung durch die Luft nein.
       Braucht `Query.Mesh.NearestPoint` und damit den Framework-Thread - die
       Kandidatenpruefung laeuft heute auf einem Worker. NOCH OFFEN.

>>> USER-FRAGE ("nutzt du die lauffunktion vom vnavmesh und das genaue
    wegenetz oder berechnest du die route?"): Wir berechnen NICHTS selbst.
    Gelaufen wird mit `SimpleMove.PathfindAndMoveCloseTo` (vnavmesh sucht UND
    steuert), geprueft mit `Nav.Pathfind` (dieselbe Suche ohne Bewegung),
    Bodenpunkte aus `Query.Mesh.NearestPoint`. Daraus folgt zwingend: wir
    koennen NIE weiter kommen als vnavmesh, weil wir dieselbe Karte fragen.

>>> DARAUS DER NEUE ANSATZ (User: "ueber die planke steuern"): `Path.MoveTo`
    faehrt eine feste Punktliste ab, GANZ OHNE Wegsuche (IPCProvider ->
    FollowPath.Move). Damit laeuft die Figur auch ueber Boden, den das Netz
    nicht kennt - der einzige Weg an Bord.
    GEBAUT als `/acc planke` (nur DEBUG, Koordinaten hartkodiert, Versuch an
    EINER Stelle): Etappe 1 laeuft normal zur Uebergangsstelle
    (-274,0 | 11,5 | 190,0); ist der Lauf beendet und die Figur hoechstens 3 m
    daneben, faehrt Etappe 2 ohne Wegsuche ueber (-272,8 | 12,0 | 190,0) nach
    (-271,0 | 12,0 | 189,5) an Bord. Danach traegt das Wegenetz wieder bis zum
    NPC (Etappe 3 war offline durchgerechnet: 13 Wegpunkte, 39,9 m).

>>> IM SPIEL BESTAETIGT 2026-08-07 00:37 - DER GANZE WEG TRAEGT. Log:
    - 00:37:41 Etappe 1 gestartet, dist=14,3.
    - 00:37:46 "Pfad beendet, dist=0,9, angekommen=True" - Figur bei
      <-274,9 | 11,3 | 190,0>, also 0,9 m neben der Uebergangsstelle.
    - 00:37:46 "Etappe 2: an der Uebergangsstelle (0,9 m daneben). Fahre ohne
      Wegsuche" -> Path.MoveTo abgesetzt.
    - 00:38:06 Auto-Lauf zum NPC "angekommen=True", Figur bei
      <-262,2 | 16,4 | 196,1>, 2,5 m vom NPC. Y 16,4 = OBERES DECK.
    Damit ist die komplette Kette bewiesen: Wegenetz -> Path.MoveTo ueber die
    Luecke -> Wegenetz. Die Offline-Vermessung des Uebergangs war korrekt.
    USER-BESTAETIGUNG: "das hat funktioniert man ist bis zu dem punkt gelaufen
    und dann konnte man den npc ueber die liste anlaufen".

>>> NAECHSTER SCHRITT OFFEN - Verallgemeinerung. `/acc planke` hat die
    Koordinaten hartkodiert und gilt nur fuer diese eine Stelle. Damit das ein
    echtes Feature wird, muss das Plugin die Uebergangsstelle SELBST finden,
    und das heisst: die beiden getrennten Flaechen kennen. Ueber IPC geht das
    nicht (siehe Phantompunkt-Problem). Die drei Wege:
    (a) Das Plugin liest die .navmesh-Cachedatei selbst und macht denselben
        Flood-Fill wie das Scratchpad-Programm. Praezise und bewiesen, aber
        Kopplung an ein undokumentiertes Fremdformat (Magic+Version im Header
        pruefen, sonst Feature aus) und die Poly-Links muessten rekonstruiert
        werden - Detour baut sie erst beim AddTile, serialisiert sind nur
        `neis`.
    (b) Eine mitgelieferte Datentabelle bekannter Uebergangsstellen je Gebiet,
        offline ausgemessen. Sofort machbar und exakt, skaliert aber nicht auf
        alle Zonen.
    (c) Zur Laufzeit Kandidatenpaare per NearestPoint/Pathfind suchen - das
        ist genau der Weg, an dem die Zugangssuche schon scheitert.

>>> NEU AUF USER-WUNSCH ("kannst du ihn automatisch zum besten punkt laufen
    lassen?"): `/acc zugang` laeuft den gefundenen Punkt jetzt selbst an.
    Die Suche laeuft auf einem Worker-Thread und darf weder ObjectTable noch
    vnavmesh-IPC anfassen - sie parkt den Punkt in `_pendingApproachWalk`,
    `Update()` startet den Lauf im naechsten Frame (vor dem _active-Check,
    der Takt laeuft auch ohne aktiven Lauf). Zielname wird zu "Zugang zu X",
    damit die Laufansage nicht behauptet, es ginge zum NPC selbst.
    Selbstkorrigierend: ist der Punkt doch nicht erreichbar, greift beim
    Hinlaufen dieselbe Unerreichbar-Pruefung.

## VORHERIGER STAND (2026-08-06, KATEGORIE + GEGENSTANDSSTUFE IM BEUTEL - GEBAUT, UNGETESTET)

>>> USER-WUNSCH: "da gibt es wohl eine kategorie und eine gegenstandsstufe die
    sollte bei den sachen wenn man auf dem gegenstand steht auch noch angesagt
    werden."

>>> AUSGANGSLAGE: `GearInfoService.DescribeGear` steigt bei allem aus, was keine
    Ausruestung ist (`EquipSlotCategory.RowId == 0`). Ein Sehender liest im
    Tooltip auch beim Trank Kategorie und Gegenstandsstufe - der Blinde hoerte
    nur "10 mal Kupfererz".

>>> AM SHEET GEPRUEFT, NICHT VERMUTET (Offline-Dump ueber Lumina + sqpack,
    2026-08-06, siehe Konvention "Sheets offline auslesen"):
    - `Item.ItemUICategory` ist ein RowRef auf `ItemUICategory`, dessen `Name`
      in Spielsprache steht ("Arznei", "Baustein", "Angelkoeder", "Kristall",
      "Metall", "Materia", "Zutat"). Wird GELESEN, nicht uebersetzt - genau wie
      die Attributnamen bei DescribeStats.
    - Gegenstandsstufe = `Item.LevelItem.RowId`, dieselbe Quelle wie bei der
      Ausruestung. 21.733 von 21.781 Nicht-Ausruestungs-Gegenstaenden tragen
      eine - die Zahl ist echte Spieldaten, keine leere Spalte.

>>> GEBAUT: `GearInfoService.DescribeItemBasics(itemId)`, aufgerufen in
    `UIReaderService.ResolveFocusedItemName` - also genau beim "auf dem
    Gegenstand stehen" (Beutel, Ruestkammer, Laden-Kacheln). Die Bulk-Ansagen
    (Quest-Belohnung) bleiben bewusst unveraendert, die sind schon lang.
    - Bei Ausruestung wird die Gegenstandsstufe NICHT wiederholt: die kommt dort
      schon aus `DescribeStats`. Es kommt nur die Kategorie dazu.
    - Ist die Kategorie wortgleich mit dem Namen ("Leder" in Kategorie "Leder"),
      faellt sie weg - sonst haette es gestottert.

>>> ERWARTUNGSWERTE VORAUSBERECHNET (dieselbe Logik offline gefahren), so muss
    es im Spiel klingen:
    - "Heiltrank, Arznei, Gegenstandsstufe 10"
    - "Kupfererz, Baustein, Gegenstandsstufe 1"
    - "Rattenschwanz, Angelkoeder, Gegenstandsstufe 15"
    - "Staerke-Materia I, Materia, Gegenstandsstufe 15"
    - "Leder, Gegenstandsstufe 1"  (Kategorie unterdrueckt)
    - Ausruestung: "Bronze-Gladius, Hauptwaffe der Gladiatoren, Stufe 6,
      tragbar, Gegenstandsstufe 6, ..."

>>> KEINE neuen Ansage-Texte noetig: `ItemLevelValue` gibt es bilingual schon,
    die Kategorie kommt aus dem Sheet.

>>> Build Debug 0 Warnungen / 0 Fehler, nach devPlugins deployt. IM SPIEL NOCH
    UNGETESTET. Offen fuer den Test: ist die Ansage beim Blaettern durch den
    Beutel zu lang? Falls ja, ist die Kategorie bei AUSRUESTUNG der erste
    Kandidat zum Streichen (dort steht die Klasse schon in der Tragbarkeit).

## VORHERIGER STAND (2026-08-06, V5.74 OEFFENTLICH RELEASED)

>>> RELEASE v5.74 IST DRAUSSEN (Commit db04160, Tag v5.74,
    https://github.com/derbruedi/ff14-accessibility/releases/tag/v5.74).
    Versions-Sync war wie immer noetig: Plugin.cs stand auf 5.73,
    csproj/repo.json noch auf 5.72 -> alle drei jetzt 5.74(.0.0).

>>> VERIFIZIERT, nicht angenommen:
    - `gh release list`: v5.74 traegt "Latest".
    - Alle 4 Assets state=uploaded (latest.zip 620.928,
      FF14Accessibility-v5.74.0.zip 620.928, Installer-exe 162.517.183,
      installer.json 165).
    - Der Weg des SPIELERS geprueft: Download ueber
      releases/latest/download/latest.zip liefert 620.928 Bytes, und das
      Manifest IM Paket traegt AssemblyVersion 5.74.0.0.
    - repo.json auf main ueber raw.githubusercontent gegengelesen:
      AssemblyVersion 5.74.0.0, DownloadLinkInstall zeigt auf latest.
    - Installer unveraendert (git diff b22c1ac..HEAD -- Installer/ leer),
      exe + installer.json aus v5.72 uebernommen; SHA256 der exe stimmt mit
      der installer.json ueberein (5787445B...CAD49), Version 1.1.0.0.

>>> INHALT: Belegen-Fix (der wichtigste Punkt - betraf ALLE Spieler und
    beide Zuweisungsarten), Gegenstaende auf die Leiste, Quest-Art,
    Zauber-Warnung von allen Gegnern, Anmelde-Ruhephase, Folgen-Taste
    repariert, README DE+EN abgeglichen.

>>> IM RELEASE MITGEGANGEN, ABER IN-GAME UNGETESTET (in den Release-Notes
    ausdruecklich so benannt): Anmelde-Ruhephase, erweiterte Zauber-Warnung,
    reparierte Folgen-Taste. Bestaetigt waren: Belegen-Fix, Gegenstaende,
    Quest-Art.

## VORHERIGER STAND (2026-08-06, README DE+EN GEGEN DEN CODE ABGEGLICHEN)

>>> USER-AUFTRAG: "aktualisiere mal die readme in deutsch und englisch und
    schau das die tasten die wirklich funktionieren drin stehen und nicht
    versehens noch alte."

>>> GEFUNDENE KARTEILEICHEN (beide READMEs, Abschnitt "Skill-Browser"):
    Umschalt+F7/F8 (Skill vor/zurueck), Umschalt+F11 (Ziel-Leiste),
    Umschalt+F9 (Ziel-Taste), Umschalt+F10 (ablegen) - FUENF Tasten, die es
    seit dem Umbau auf das modale Menue (Strg+Numpad0) nicht mehr gibt.
    Wer danach gegriffen hat, hat ins Leere gedrueckt.

>>> METHODE, damit das nicht wieder passiert: Abgleich per Skript statt per
    Auge. (a) Alle `public string Key* = "..."` aus Configuration.cs ziehen,
    (b) gegenpruefen, dass jede in Plugin.cs auch benutzt wird, (c) jede in
    beiden READMEs suchen (mit Uebersetzungstabelle DE/EN fuer BildAb ->
    Page Down usw.), (d) die alten Tastennamen explizit als "darf NICHT
    vorkommen" pruefen.
    ERGEBNIS: 41 Tasten definiert, 41 benutzt, 0 tote Konfigfelder;
    41 von 41 in beiden READMEs vorhanden; 0 alte Tasten uebrig.

>>> NEU IN DIE READMEs AUFGENOMMEN (fehlten komplett): + (Ziel folgen),
    Strg+Umschalt+F3 (AoE-Warnton, MIT dem Hinweis "Standard aus und warum"),
    Strg+Umschalt+F4/F5 (Triple Triad), Strg+Numpad0 samt der menue-internen
    Nummernblock-Steuerung inkl. 4/6 fuer Gegenstaende.
    Bei "+" steht ausdruecklich "die normale Plus-Taste, nicht die des
    Nummernblocks" und in der EN-Fassung der Layout-Hinweis (dort braucht +
    oft Umschalt -> umbelegen).

>>> NEUER ABSCHNITT "Ueberschneidungen mit Spiel-Tasten": nennt die drei
    Konflikte beim Namen (Bild-auf/-ab = Kamera-Zoom, Strg+Ende =
    Kamera-Preset), erklaert warum sie folgenlos sind, und sagt was zu tun
    ist, wenn beim Anmelden eine ANDERE Zahl als 3 gemeldet wird. Die drei
    stammen aus dem Log-Dump, nicht aus dem Gedaechtnis.

>>> Feature-Listen nachgezogen (fehlten): Zauber-Warnung von allen Gegnern,
    AoE-Warnton, Faehigkeit-bereit, XP/Beute, Folgen, Angeln, Sammeln,
    Reittiere, Triple Triad, Gegenstaende auf die Leiste, Anmelde-Ruhephase.
    Chat-Befehle vervollstaendigt (fish/gather/cd/lang/dump/soundtest/help);
    Debug-Befehle (objprobe, hotbarprobe) bewusst NICHT dokumentiert.
    Sprach-Abschnitt berichtigt: er behauptete "Ansagen ueberwiegend
    Deutsch" - seit v5.59 ist die Ansage-Schicht zweisprachig, `/acc lang`
    steht jetzt drin.

## VORHERIGER STAND (2026-08-06, ANMELDE-RUHEPHASE - GEBAUT, UNGETESTET)

>>> USER-MELDUNG: "wenn man sich einlogt kommen von der mod sehr viele meldungen
    die sich gegenseitig weg druecken und die frage ist ob die wirklich noetig
    sind." Antwort nach Log-Auswertung: NEIN, fast nichts davon.

>>> GEMESSEN, NICHT VERMUTET (Log 2026-08-06, [Speak]-Zeilen ab 17:35:28):
    rund 15 Ansagen in EINER Sekunde, die meisten davon INT (unterbrechend):
    "FORTSCHRITT, Unterbrochen!", "SEITE AN SEITE", "Nach Mindeststufe
    sortieren", "Benachrichtigung. Mit Strg+F12 annehmen.", "Jobwechsel.
    Klassenwechsel", "INVENTAR" (+5x DEBOUNCED), "0/140", "Ziel", "Alte
    Herausforderungen neu erleben", "Tastenbelegung gespeichert: 183
    Aktionen...", "Echokraut v0.19.3.0", "Zum Lodestone", "EMPFEHLUNGEN".
    URSACHE: das sind keine Ansagen des Mods, sondern die HUD-Fenster, die der
    CLIENT beim Anmelden aufbaut - jedes davon kommt als PostSetup bei
    `OnAnyAddonOpen` an und wird pflichtschuldig gemeldet. Weil sie
    unterbrechend sind, schneiden sie sich gegenseitig ab: der Spieler hoert
    Fetzen. Fremd-Plugin-Fenster (Echokraut) sind mit dabei.

>>> GEBAUT: Anmelde-Ruhephase. `UIReaderService.BeginLoginQuiet(sekunden)` legt
    die AUTOMATISCHEN Leser still - `OnAnyAddonOpen`, `OnAnyAddonUpdate` und
    `UpdateGlobalFocus`. Ausgeloest von `ClientState.Login` (Plugin.OnLogin).
    Dauer `Configuration.LoginQuietSeconds = 6` (gemessene Lawine ~2-4 s,
    Rest ist Reserve).
    WICHTIG - WAS NICHT STUMM IST: alles vom Spieler AUSGELOESTE. Tastendruck-
    Ansagen, gezielt geoeffnete Fenster und die Navigation laufen nicht ueber
    diese drei Einstiege. Verschluckt wird ausschliesslich der Selbstaufbau.
    `[Accessibility] Addon: <name>` wird WEITERHIN geloggt - die Ruhephase macht
    die Diagnose also nicht blind, sie macht nur den Lautsprecher still.

>>> HOT-RELOAD-FALL BEDACHT: laedt das Plugin, waehrend man schon in der Welt
    ist (devPlugins/Neu laden), steht das HUD laengst - dann greift keine
    Ruhephase (nur eine Log-Zeile). Beim naechsten echten Login greift sie.

>>> MITGENOMMEN: die Keybind-Meldung beim Login. Sie kam nur, weil 3 Konflikte
    bestehen (`announce:false`, aber `conflictCount > 0`), war aber ein langer
    Satz mitten in der Lawine. Jetzt beim Login nur noch "3 Tastenkonflikte."
    (neu `KeybindConflictsShort`, DE+EN) und nicht mehr unterbrechend; der
    vollstaendige Satz bleibt fuer den ausdruecklichen `/acc keys`.

>>> BLEIBT HOERBAR (User-Wahl "Ruhe, bis das HUD steht"): die
    Bereitschaftsmeldung "FF14 Accessibility Version X bereit." - sie ist die
    einzige, die eine echte Aussage traegt (Plugin laeuft ueberhaupt).
    Build Debug 0/0, deployt. IN-GAME UNGETESTET - Test ist ein Aus- und
    wieder Einloggen.

>>> NICHT ANGEFASST, WEIL NICHT VERLANGT: der Zonenwechsel. Der baut ebenfalls
    Fenster auf (Log 17:35:40 "Wegenetz wird geladen", "Muehlenbruch"), ist aber
    deutlich harmloser. Wenn es dort auch stoert, ist es dieselbe Mechanik mit
    `TerritoryChanged` als Ausloeser.

## VORHERIGER STAND (2026-08-06, ZAUBER-WARNUNG VON ALLEN GEGNERN - GEBAUT, UNGETESTET)

>>> USER-WUNSCH: "kann man auch ne warnung einbauen wenn ein gegner auf mich
    zielt bzw einen zauber auf mich zaubert, so das man evtl ausweichen kann?"

>>> WICHTIG FUER DEN UEBERBLICK: BEIDES GAB ES SCHON ZUR HAELFTE.
    Der User erinnerte sich richtig ("wir hatten doch schonmal was mit
    flaechenschaden") - der AoE-Warnton ist seit V5.55 gebaut UND released,
    steht aber auf OPT-IN/STANDARD AUS (Strg+Umschalt+F3), weil er in-game nie
    bestaetigt wurde. Und die Ansage "Gegner wirkt X" existierte ebenfalls
    (AnnounceEnemyCast, Standard AN) - aber NUR fuer das anvisierte Ziel.
    Die eigentliche Luecke war also: ein Gegner, den man NICHT anvisiert hat,
    zaubert auf einen -> es blieb still. Genau der Fall, um den es geht.

>>> GEBAUT: die Cast-Ansage haengt jetzt nicht mehr am Ziel, sondern laeuft in
    DERSELBEN Gegner-Schleife wie der AoE-Ton mit (`UpdateAoeWarning`) - kein
    zweiter Frame-Durchlauf. Bedingung unveraendert streng:
    `CastTargetObjectId == playerId`, also nur Zauber AUF MICH; Zauber auf
    andere bleiben still (User-Entscheid 2026-07-25 gilt weiter).
    Die Pruefung sitzt VOR dem EffectRange-Filter, damit auch Einzelziel-Zauber
    ohne Bodenflaeche gemeldet werden - die will man gerade hoeren.

>>> NAME NUR WENN NOETIG (User-Wahl aus drei Varianten): ist der Zaubernde das
    eigene Ziel, bleibt es beim knappen "Gegner wirkt Verwuesten." - der Spieler
    weiss ohnehin, wer gemeint ist. Ist es ein ANDERER, faellt der Name:
    "Amalj'aa-Seher wirkt Feuer." Kurz gehalten, weil die Warnung ankommen muss,
    solange noch Zeit zum Ausweichen ist. Neu: `NamedEnemyCasts` (DE+EN).
    NICHT gebaut (bewusst, User-Wahl): Meldung, wenn ein Gegner einen nur ins
    Visier nimmt, ohne zu zaubern - haette im Kampf deutlich mehr geredet.

>>> ENTPRELLUNG PRO ZAUBERNDEM, nicht global: `_castsAtMe` haelt casterId ->
    zuletzt angesagte Aktion. Damit warnt jeder Gegner einzeln genau einmal je
    Zauber, und ein zweiter Gegner wird nicht vom ersten verschluckt. Eintraege
    fallen weg, sobald der Zauber endet, das Ziel wechselt oder der Gegner aus
    der Objektliste verschwindet (Aufraeum-Durchlauf mit vorgehaltenen
    Sammlungen, damit der Frame nichts allokiert).
    Build Debug 0/0, deployt. IN-GAME UNGETESTET.

>>> ZUM MITTESTEN, WEIL ES DENSELBEN WEG BETRIFFT: der AoE-Warnton ist weiter
    STANDARD AUS. Er wartet seit V5.55 auf genau eine Bestaetigung (Kegel von
    vorn/hinten, Linienbreite, Kreismittelpunkt; Testfeld Kampfuebungsplatz).
    Faellt die positiv aus, sollte der Standard auf AN gedreht werden - dann ist
    auch Spielervorschlag (5) wirklich erledigt.

## VORHERIGER STAND (2026-08-06, BELEGEN WAR JOB-ABHAENGIG KAPUTT - FIX BESTAETIGT)

>>> DAS BELEGEN WAR SEIT LAENGEREM DEFEKT, UND ZWAR NICHT NUR FUER GEGENSTAENDE.
    User: "das belegen hat nicht funktioniert, ist mir letztens schon
    aufgefallen". Das neue Gegenstands-Feature hat es also nicht kaputt
    gemacht - es hat einen bestehenden Defekt sichtbar gemacht.

>>> ENTSCHEIDENDER HINWEIS KAM VOM USER, NICHT AUS DEM LOG: "das ist job
    abhaengig, ich bin in den job gegangen wo es funktioniert hat und da gehts".
    Ohne diesen Hinweis waere die Sonde weiter im falschen Job gelaufen.

>>> GEMESSEN MIT `/acc hotbarprobe` (DEBUG-Sonde, arbeitet nur auf einem Slot
    der Hauptleiste und stellt ihn danach wieder her - Original type+id gemerkt).
    Zwei Laeufe, 17:36 Uhr:
    KLASSE THAUMATURG (CurrentClassJobId=7, ActiveHotbarClassJobId=7):
      A1 SetAndSaveSlot(Item)   -> live UNVERAENDERT
      A2 nach LoadSavedHotbar   -> type=Item id=4555  (also: SetAndSaveSlot
         schreibt NUR den gespeicherten Stand, das Laden zieht ihn auf die Leiste)
      D1/D2 dasselbe mit Action 142 -> funktioniert ebenfalls
    JOB SCHWARZMAGIER (CurrentClassJobId=25, ActiveHotbarClassJobId=25):
      A1 -> unveraendert, A2 nach LoadSavedHotbar -> ALTER Inhalt zurueck
      D1/D2 mit Action -> genauso wirkungslos
    -> SetAndSaveSlot ist bei Job 25 fuer BEIDE Typen wirkungslos.

>>> EINE HYPOTHESE WURDE DABEI WIDERLEGT: die Vermutung, Klasse und Job wuerden
    unter verschiedenen Ids gefuehrt (Klasse/Job teilen sich in FFXIV die
    Leisten). Gemessen sind CurrentClassJobId und ActiveHotbarClassJobId in
    BEIDEN Faellen identisch (7/7 und 25/25). Daran liegt es nicht.

>>> WAS TRAEGT (in BEIDEN Jobs gemessen, Sondenschritte F1/F2):
      F1 HotbarSlot.Set(type,id)                  -> live sofort gesetzt
         + WriteSavedSlot(job, bar, slot, live,
                          ignoreSharedHotbars:false, isPvpSlot:false)
      F2 danach LoadSavedHotbar                   -> Eintrag BLEIBT stehen
    WriteSavedSlot ist das direkte Gegenstueck zu LoadSavedHotbar. Beides sind
    Spielfunktionen - hier wird nichts nachgebaut und nichts umgangen.

>>> VERBLEIBENDE, EHRLICH MARKIERTE VERMUTUNG zur Job-Abhaengigkeit:
    `SetAndSaveSlot` hat den Vorgabewert `allowSaveToPvP: true`, unser
    WriteSavedSlot-Aufruf sagt dagegen ausdruecklich `isPvpSlot: false`.
    Jobs haben PvP-Leisten, Klassen nicht - das PASST zum Befund, ist aber
    NICHT gemessen. Wer es genau wissen will: SetAndSaveSlot mit
    `allowSaveToPvP: false` in Job 25 probieren. Nicht noetig fuer den Fix.

>>> FIX GEBAUT: neue gemeinsame Methode `HotbarService.PlaceOnSlot` geht den
    gemessenen Weg (Set -> WriteSavedSlot -> LoadSavedHotbar) und wird von
    Faehigkeiten- UND Gegenstands-Zuweisung benutzt. `SetAndSaveSlot` kommt im
    Feature-Code nicht mehr vor.
    LoadSavedHotbar bleibt am Ende ABSICHTLICH stehen: es zieht den
    gespeicherten Stand ueber die Live-Leiste, ein misslungenes Speichern faellt
    dadurch auf die Fuesse und die 2-Frame-Ruecklese meldet ehrlich
    "hat nicht gewirkt" - statt einer Aenderung, die nur bis zum naechsten
    Nachladen echt aussieht.
    Der ueberholte V4.76-Kommentar ueber SetAndSaveSlot wurde ersetzt.
    Build Debug 0/0, deployt.
    IN-GAME BESTAETIGT (User 2026-08-06: "ok es geht jetzt in beiden jobs").

>>> DIE SONDE `/acc hotbarprobe` BLEIBT VORERST STEHEN - bewusste Abweichung von
    der Konvention "Sonde nach Feature-Ende loeschen". Grund: dieser Weg ist
    erwiesen patch-anfaellig (SetAndSaveSlot trug frueher, heute nicht mehr, und
    der Ausfall war still). Sie ist [Conditional]-frei, aber in `#if DEBUG`
    gekapselt, kostet die Release-Version also nichts, und beantwortet nach dem
    naechsten Spiel-Patch in einem Lauf, welcher Weg noch traegt.

## VORHERIGER STAND (2026-08-06, GEGENSTAENDE AUF DIE LEISTE - GEBAUT)

>>> (2) TRAENKE/ELIXIERE AUF DIE LEISTE LEGEN (Spielerwunsch). Gebaut als
    ERWEITERUNG des bestehenden Zuweisungs-Menues (Strg+Numpad0), nicht als
    zweites Menue: die tragende Kette (SetAndSaveSlot -> LoadSavedHotbar ->
    Ruecklese-Bestaetigung nach 2 Frames) bleibt unveraendert, sie ist erprobt.
    BEDIENUNG (User-Wahl 2026-08-06): Numpad 4 ODER 6 wechselt zwischen
    Faehigkeiten- und Gegenstandsliste. Beide Tasten sind im Spiel Drehen und
    werden - wie 8/2/0/Komma - nur solange das Menue offen ist geschluckt
    (SkillMenuVks um 0x64/0x66 erweitert). Das Menue oeffnet weiterhin auf der
    gewohnten Faehigkeitenliste.
    Ansage je Eintrag: "Heiltrank, 12 Stueck, liegt auf Taste 5, 3 von 8".
    Die Anzahl ist bewusst dabei - ein Trank mit Bestand 1 ist eine andere
    Entscheidung als einer mit 20.

>>> WELCHE GEGENSTAENDE IN DER LISTE STEHEN, ENTSCHEIDET DAS SPIEL, NICHT WIR:
    Rucksackinhalt, dessen Item-Sheet-Zeile eine `ItemAction` hat - die
    spieleigene Markierung fuer "tut etwas, wenn man es benutzt". OFFLINE
    GEMESSEN (Sheet-Dump 2026-08-06): 4987 von 50773 benannten Gegenstaenden,
    angefuehrt von Arznei (Heiltrank/Supertrank/Megatrank), Gericht,
    Verschiedenes, Notenrolle, Begleiter. Damit braucht der Mod KEINE
    handgepflegte Kategorienliste, die bei jedem Patch veraltet.
    Gleiche Stapel ueber mehrere Taschenseiten werden zusammengezaehlt,
    HQ und NQ bleiben getrennt (verschiedene Ids, der Spieler kann beides haben).

>>> HQ WIRD NICHT SELBST AUSGERECHNET - das war die einzige echte Unsicherheit.
    Kursierendes Halbwissen ("HQ = Id + 1000000") wurde NICHT eingebaut.
    Stattdessen ilspycmd-verifiziert an Dalamud.dll, GameInventoryItem:
    `ItemId` ist die Id MIT HQ-/Collectible-Offset ("Gets the item id"),
    `BaseItemId` die ohne ("without HQ or Collectible offset applied",
    ueber ItemUtil.GetBaseId). Die Zuweisung nimmt also schlicht `ItemId` -
    genau den Wert, den das Spiel selbst fuehrt. `BaseItemId` dient nur dem
    Sheet-Nachschlagen (Name, ItemAction).
    NOCH NICHT BELEGT: dass RaptureHotbarModule die HQ-Id auch annimmt. Das
    beantwortet die vorhandene Ruecklese-Bestaetigung beim ersten Test von
    selbst - sie meldet Erfolg nur, wenn Typ UND Id im Slot wirklich stehen.
    Faellt sie negativ aus, hoert der User "Belegen hat nicht gewirkt", statt
    dass still etwas Falsches auf der Taste landet.

>>> `HotbarSlotType.Item` ilspycmd-verifiziert (FFXIVClientStructs,
    RaptureHotbarModule). Der Sonderfall `InventoryItem` (Id kodiert
    InventoryType+Slot, das Spiel loest selbst auf) waere der Drag-und-Drop-Weg,
    ist aber NICHT benutzt: die Kodierung ist in den Strukturen nicht
    dokumentiert, und raten kam nicht in Frage. Falls HQ ueber `Item` scheitert,
    ist das die naechste Spur - dann aber erst messen.

>>> BERUEHRTE DATEIEN: InventoryService (neu: `UsableItem` + `CollectUsableItems`,
    haelt die Inventar-Logik an EINEM Ort), HotbarService (Quelle umschaltbar,
    `AssignItemToSlot`, `FindSlotLocationFor` + `VerifyAssignment` um den Typ
    erweitert), AccessibilityStrings (3 neue Bausteine DE+EN), Plugin.cs
    (Numpad 4/6 + Konstruktor-Reihenfolge: Inventar vor Hotbar).
    Die Item-Liste wird bei JEDEM Wechsel neu gebaut, nie zwischengespeichert -
    ein ausgetrunkener Trank darf nicht mehr angeboten werden.
    Build Debug 0/0, deployt. IN-GAME UNGETESTET.

## VORHERIGER STAND (2026-08-06, FOLGEN-TASTE REPARIERT + QUEST-ART BESTAETIGT)

>>> SPIELER-VORSCHLAEGE VOM USER WEITERGEREICHT (2026-08-06), fuenf Stueck:
    (1) Wuerfel-Fenster (Bedarf/Gier) barrierefrei, (2) Traenke/Elixiere auf die
    Leiste legen koennen, (3) Quest-Art ansagen ("job oder zugang", so wie es
    Story schon gibt), (4) "folgen geht nicht, habe es mit plus probiert",
    (5) Warnton, wenn man in einer Schadensflaeche (AoE) steht.
    Reihenfolge vom User bestimmt: Kampf-Nachlese und Wuerfeln sind gerade NICHT
    testbar (keine Gruppe, kein Kampf), deshalb zuerst Quest-Art.

>>> (4) FOLGEN WAR NIE AKTIV - SEIT V5.57, FUER JEDEN. KEIN SPIELERFEHLER.
    URSACHE, hart belegt: `ParseKeySpec` (Plugin.cs ~502) zerlegte die
    Tastenangabe an '+' (dem Modifier-Trenner). Der Tastenname IST hier aber
    "+", also blieben nach `Split('+', RemoveEmptyEntries)` NULL Teile uebrig,
    `valid` wurde false und Vk blieb -1. Damit hat `UpdateKeyEdges` die Taste
    nie auch nur abgefragt - kein Tastendruck konnte je ankommen.
    BELEG, nicht Vermutung: dalamud.log 2026-08-06 14:02:55
    "[WRN] Unbekannte Tastenangabe in der Konfiguration: '+'" - das ist der
    else-Zweig aus Plugin.cs:517, der genau in diesem Fall greift. Die Warnung
    stand seit V5.57 in jedem Log, es hat nur nie jemand danach gesucht.
    NICHT die Ursache waren: vnavmesh (dessen Fehlen meldet sich erst in
    FollowUpdate mit eigener Ansage) und die Konfiguration des Spielers.
    FIX: der Tastenname wird jetzt ZUERST abgetrennt ("+" am Ende = Tastenname),
    erst der Rest wird an '+' in Modifier zerlegt. "Strg++" (Strg plus +) geht
    damit ebenfalls, "Strg+Numpad3" und "N" unveraendert.
    Build Debug 0/0, deployt. IN-GAME UNGETESTET (Ziel anvisieren, dann +).

>>> OFFEN GESAGTE GRENZE ZUR TASTE: auf einer ENGLISCHEN Tastatur liegt "+" auf
    Shift+=. `IsJustPressed` (Plugin.cs ~539) verlangt exakte Modifier-Gleichheit
    ("kein Shift"), also bleibt die Taste dort vermutlich weiter tot. VERMUTUNG
    ueber fremde Layouts, nicht gemessen - wenn der Vorschlag von einem
    englischsprachigen Spieler kam, braucht es eine layout-feste Taste oder eine
    Umbelegung. Nicht angefasst, weil unbelegt.

>>> (3) QUEST-ART GEBAUT (Ansage "Job: ", "Freundesvolk: ", "Chronik: ",
    Hauptszenario weiter "Story: "). Quelle ist die JOURNAL-TAXONOMIE DES
    SPIELS, nichts geraten: Quest -> JournalGenre -> JournalCategory ->
    JournalSection. Neue `QuestKind`-Enum + `QuestKinds()` in
    QuestMarkerService; `QuestDestination.IsMainStory` ist zu
    `QuestDestination.Kind` geworden.

>>> ABSCHNITTE OFFLINE AUS DEN SPIELDATEIEN GELESEN (2026-08-06), nicht aus dem
    Gedaechtnis: 0 Hauptszenario (ARR-EW), 1 Hauptszenario (Dawntrail),
    2 Chroniken der neuen Aera, 3 Nebenauftraege, 4/5 Freundesvoelker,
    6 Klassen und Jobs, 7 Sonstige, 8 Freibriefe, 9 Inhalte.
    WERKZEUG: kleines Konsolenprogramm gegen Lumina + das installierte sqpack
    (K:\SteamLibrary\...\game\sqpack), Scratchpad-Ordner JournalDump. Braucht
    WEDER laufendes Spiel NOCH eine Debug-Sonde - fuer reine Sheet-Fragen ist
    das der schnellere Weg und sollte wieder so gemacht werden.

>>> DABEI EINEN ECHTEN MANGEL GEFUNDEN UND MITBEHOBEN: die alte Pruefung war
    `JournalSection.RowId == 0`. Das Hauptszenario von DAWNTRAIL liegt aber in
    Abschnitt 1 - 139 Quests wurden also nie als Story angesagt. Jetzt 0 ODER 1.

>>> WARUM DER NAMENS-ABGLEICH TRAEGT (MarkerInfo hat keinen Quest-Zeiger, nur
    ein Label): im Sheet stehen Quest-Namen doppelt, mit widersprechenden
    Abschnitten. GEMESSEN am Sheet-Dump: 44 von 5276 Namen widersprechen sich;
    ueberspringt man die Zeilen ohne JournalGenre ("Ungueltige Kategorie",
    RowId 0), sind es EXAKT 0. Genau das tut `QuestKinds()` jetzt.

>>> ERSTE FASSUNG IM SPIEL BESTAETIGT (Log 2026-08-06 15:29:39):
    "[Quest] Quest-Arten geladen: 2800 benannte Quests (1038 Hauptszenario,
     850 Job, 716 Freundesvolk, 196 Chronik)" - exakt die offline
    vorausberechneten Zahlen. Die Sheet-Auswertung im Plugin stimmt also.

>>> DANN ABER NACHGEBESSERT, WEIL STILLE MEHRDEUTIG IST (User 2026-08-06):
    In der ersten Fassung blieben Nebenauftraege ohne Wort ("nur wo es etwas
    sagt"). Der User blaetterte durch 9 Ziele, hoerte kein einziges Mal eine
    Art und fragte: "ist das mit den quests schon drin, bei mir hat sich nichts
    veraendert". NACHGEPRUEFT: alle 9 waren tatsaechlich Nebenauftraege aus
    "Auftraege Finsterwald" (Abschnitt 3) - das Feature lief korrekt.
    GENAU DAS IST DAS PROBLEM: der Spieler kann "ist ein Nebenauftrag" nicht von
    "Feature kaputt" unterscheiden, waehrend ein sehender Spieler das Symbol
    sieht. Stille als Bedeutungstraeger funktioniert hier nicht.
    JETZT WIRD JEDE BEKANNTE ART GENANNT: "Story: ", "Job: ", "Freundesvolk: ",
    "Chronik: ", "Nebenauftrag: ", "Sonstiges: ".

>>> `QuestKind.Ordinary` HEISST JETZT `QuestKind.Unknown` UND BEDEUTET ETWAS
    ANDERES: nicht mehr "unauffaellige Quest", sondern "im Sheet nicht
    gefunden". Nur dieser Fall bleibt still - dort haetten wir fuer kein Wort
    einen Beleg. Abschnitte 8/9 (Freibriefe/Inhalte, enthalten gemessen keine
    Quests) fallen ebenfalls hierhin, statt in ein benachbartes Etikett gefaltet
    zu werden, das sie falsch benennen wuerde. FATE-Ziele nutzen ebenfalls
    Unknown (ein FATE ist keine Quest).

>>> NEUE ERWARTUNGSWERTE NACH DER NACHBESSERUNG (offline vorausberechnet).
    Beim Laden muss im Log stehen:
    "[Quest] Quest-Arten geladen: 5145 benannte Quests (1038 Hauptszenario,
     2024 Nebenauftrag, 850 Job, 716 Freundesvolk, 196 Chronik,
     321 Sonstiges)."
    Build Debug 0/0, deployt.
    IN-GAME BESTAETIGT (User 2026-08-06: "ok das mit den quests funktioniert").

>>> (3, ZWEITE HAELFTE) "ZUGANG" IST NICHT GEBAUT - UND ZWAR ABSICHTLICH.
    Der Spieler wollte "job ODER zugang". Job ist drin, Zugang nicht:
    das Quest-Sheet markiert Freischaltungen nur lueckenhaft. GEMESSEN:
    `InstanceContentUnlock` ist bei 43 Quests gesetzt, die Collection
    `InstanceContent` bei weiteren 17 - zusammen 60, waehrend
    ContentFinderCondition 857 benannte Instanzen fuehrt. Eine Ansage daraus
    waere bei der grossen Mehrheit still, und eine Auskunft, die meistens
    schweigt, ist schlimmer als keine (der Spieler kann den Unterschied nicht
    sehen). `IconSpecial` ist NICHT der Weg - die Verteilung zeigt reine
    Saison-Event-Symbole (80101 Feuerfest, 80103 Allerseelen, ...).
    NAECHSTE SPUR, noch nicht ausgewertet: `ContentFinderCondition.UnlockCriteria`
    + `UnlockType` (Byte, duerfte das Zielsheet bestimmen). Erst messen.

## VORHERIGER STAND (2026-08-04, V5.73 GEBAUT - KATEGORIE-ANSAGE GEKUERZT)

>>> WORT "KATEGORIE" RAUS AUS DEN OBJEKT-BROWSER-ANSAGEN (User 2026-08-04:
    "nimm bei den kategorieen das wort kategorie weg so das nur npc haendler
    questziele usw da steht"). Aus "Kategorie Haendler: 2 in der Naehe." wird
    "Haendler: 2 in der Naehe."
    BEGRUENDUNG, die auch fuer spaetere Ansagen gilt: der Spieler hat gerade die
    Kategorie-Taste gedrueckt, der Zusammenhang ist also schon klar - nur der
    Name traegt Information. Die Nachlese macht es seit V4.90 genau so
    ("Beute, 3 Nachrichten"), der Browser zieht jetzt nach.
    GEAENDERT in AccessibilityStrings.cs, DE UND EN gleichzeitig (7 Methoden):
    CategoryQuestCount, CategoryWaypointCount, CategoryAetheryteCount,
    CategoryFateCount, CategoryLevequestCount, CategoryFishingCount,
    CategoryObjectCount. Die Kategorienamen selbst (CategoryLabel(NavCategory))
    sind unveraendert - nur das vorangestellte Wort faellt weg.
    Build Debug 0/0, deployt. V5.73. IN-GAME UNGETESTET.

>>> BEWUSST NICHT MITGEAENDERT, WEIL ANDERER ORT: `CategoryLabel(string name)`
    (AccessibilityStrings ~40) sagt weiterhin "Kategorie Kopf." - das ist der
    REITER im Waffenschrank (UIReaderService:1454) und im Staatliche-
    Gesellschaft-Fenster (:1490), nicht der Objekt-Browser. Dort steht der Name
    allein ("Kopf.", "Haende.") ohne Kontext. Wenn der User es dort auch kuerzer
    will, ist es dieselbe Ein-Zeilen-Aenderung - er hat es aber nicht verlangt.

## VORHERIGER STAND (2026-08-04, KAMPF-NACHLESE IN DER MESSPHASE)

>>> Aktive Baustelle ist die Kampf-Nachlese (Abschnitt "NAECHSTE BAUSTELLE"
    weiter unten) - Messung 2 offen, zwei Log-Werte fehlen, nichts gebaut.
    Letztes Release ist unveraendert v5.72 (Abschnitt direkt darunter).

## LETZTES RELEASE (2026-08-03, V5.72 HAENDLER-KATEGORIE - RELEASED, TEILGETESTET)

>>> RELEASE v5.72 IST DRAUSSEN (Commit b22c1ac, Tag v5.72).
    Versions-Sync wie immer noetig (csproj + repo.json standen auf 5.71).
    VERIFIZIERT: v5.72 ist "Latest"; alle 4 Assets "uploaded"; der Download
    ueber releases/latest/download/latest.zip liefert 615.366 Bytes mit
    Manifest 5.72.0.0 und DLL-Dateiversion 5.72.0.0; repo.json auf main
    traegt 5.72.0.0 (ueber die API gegengelesen).
    Installer unveraendert (git diff bff9bed..HEAD -- Installer/ leer) ->
    exe + installer.json aus v5.71 uebernommen, SHA256 stimmt
    (5787445B...CAD49).

>>> NEUE KATEGORIE "HAENDLER" IM OBJEKT-BROWSER (User-Wunsch: "nur Haendler
    sehen"). Released mit v5.72, erster Teil-Test bestanden (siehe unten).
    ERKENNUNG KOMMT VOM SPIEL, NICHT VON UNS: `ENpcBase.ENpcData` haelt bis zu
    32 Verweise je NPC, und Lumina loest jeden gegen die 25 Sheet-Typen auf,
    die das Spiel dort erlaubt (ilspycmd-verifiziert 2026-08-03 an
    Lumina.Excel.Sheets.ENpcBase, Methode ENpcDataCtor: ChocoboTaxiStand,
    CollectablesShop, ContentNpc, CraftLeve, CustomTalk, DefaultTalk,
    DisposalShop, DpsChallengeOfficer, EventPathMove, FccShop, GCShop,
    GilShop, GuildOrderGuide, GuildOrderOfficer, GuildleveAssignment,
    InclusionShop, LotteryExchangeShop, PreHandler, Quest, SpecialShop, Story,
    SwitchTalk, TopicSelect, TripleTriad, Warp).
    Ein NPC gilt als Haendler, wenn mindestens ein Verweis ein Shop-Sheet IST -
    `RowRef.Is<T>()` (ilspycmd-verifiziert an Lumina.Excel.RowRef in Lumina.dll).
    Nichts wird aus Namen, Titeln oder Symbolen erraten.
    ZWEI ARTEN, weil der Unterschied fuer den Spieler zaehlt:
    - GilShop -> "Laden" / "shop"
    - SpecialShop/CollectablesShop/GCShop/FccShop/InclusionShop/DisposalShop/
      LotteryExchangeShop -> "Tausch" / "exchange"
    In der Haendler-Kategorie ersetzt dieses Wort das generische "NPC" -
    dass es NPCs sind, weiss der Spieler in der Kategorie schon.
    DIE BASE-ID-ZUORDNUNG IST NICHT NEU ERFUNDEN: `NavigationService.NpcPrefix`
    liest ENpcResident laengst ueber `obj.BaseId` und spricht damit korrekte
    NPC-Titel - derselbe Schluessel, nur ein anderes Sheet.
    OFFEN GESAGTE GRENZE: Lumina nimmt den ERSTEN Sheet-Typ, in dem die RowId
    existiert (RowRef.GetFirstValidRowOrUntyped). Wo eine Id in mehreren dieser
    Sheets gueltig ist, kann der gemeldete Typ der falsche sein. Deshalb loggt
    `ShopNpcService.LogMerchants` JEDEN Treffer mit Id, Sheet-Name und Art -
    ein Gang durch ein Marktviertel zeigt, ob die Liste stimmt.

>>> ERSTER TEIL-TEST BESTANDEN (User 2026-08-03: "hier sehe ich erstmal
    Haendler, das muss ich spaeter noch mal testen").
    Log 17:25 belegt zweierlei ohne Vermutung:
    "[Shop] Haendler: 2 von 28 NPCs. 'Ruestungshaendlerin'(Id 1003735,
     Sheet 'Ruestungshaendlerin')=GilShop, 'Haendler'(Id 1003737,
     Sheet 'Haendler')=GilShop"
    (1) Der Sheet-Name deckt sich mit dem angezeigten Namen -> die BaseId
        adressiert wirklich den NPC vor dem Spieler. Das war die offene Frage.
    (2) Der Filter arbeitet scharf: 26 von 28 NPCs fielen raus.
    NOCH OFFEN, weil aus dem Log NICHT ableitbar: ob ein Haendler FEHLT (die
    Gegenrichtung sieht nur der Spieler vor Ort) und ob die "Tausch"-Seite
    stimmt - bisher kamen ausschliesslich GilShops vor, kein Siegel-/Stein-
    Haendler (Staatliche Gesellschaft, Rowena).

>>> QUEST-ZIEL-STOPPREICHWEITE: USER HAT ENTSCHIEDEN, NICHTS ZU AENDERN
    (2026-08-03: "nee wir lassen das da so erstmal ich glaub in dem fall darf
    ich nicht ganz ran"). Der Vorschlag, bis zum Mittelpunkt zu laufen, ist
    damit ABGELEHNT - nicht vergessen, sondern verworfen.
    SEINE BEGRUENDUNG IST PLAUSIBEL und stuetzt die urspruengliche Regel: bei
    einer Ausspaeh-Quest ist Abstand halten der Sinn der Aufgabe; "ganz ran"
    waere das Gegenteil. Der 20-m-Kreis kann also genau der richtige Bereich
    sein.
    DER BEFUND BLEIBT TROTZDEM GUELTIG UND IST HIER FESTGEHALTEN, falls das
    Thema wiederkommt: `TryResolveMarkerDestination` (Plugin.cs:1165) setzt
    stopRange = max(AutoWalkPlaceStopRange, quest.Radius). Bei einem Marker mit
    r=20 heisst das: wer naeher als 20 m steht, gilt sofort als angekommen und
    laeuft KEINEN Schritt (Log 13:45:39: "stopRange=20,0, dist=10,8" -> 0,1 s
    spaeter "Ziel erreicht"). Wer sich also wundert, warum Numpad3 bei einem
    Quest-Ziel nichts tut: das ist der Grund, kein Fehler.
    Die Regel stammt aus V4.29 mit der Begruendung "Questkreis betreten reicht"
    (STATUS.md-Abschnitt weiter unten).
    NEBENBEFUND, ebenfalls nicht weiterverfolgt: an der Marker-Position lag ein
    EventObj namens 'Zielort' (DataId 2001957, 10,1 m entfernt, X/Z identisch
    mit dem Marker). Solche Objekte sind ueber die Kategorie "Objekte"
    erreichbar - der Weg, falls doch mal jemand exakt auf den Punkt will.
    Karten-EventMarkers waren dabei 0, die Minimap-Symbole ohne Beschriftung.

## NAECHSTE BAUSTELLE (2026-08-04, KAMPF-NACHLESE - SONDE GEBAUT, MESSUNG OFFEN)

>>> SPIELER-VORSCHLAG (vom User weitergereicht): "a combat log for damage
    announcements, hits, misses etc so people can look back at a fight later to
    see how their build is doing or why they died."

>>> DAS IST KEIN NEUBAU, SONDERN EIN WIEDERAUFGREIFEN. Die Infrastruktur steht:
    `MessageHistoryService` hat 9 Kategorien und einen unbegrenzten Puffer,
    Alt+BildAuf/Ab wechselt die Kategorie, Umschalt+BildAuf/Ab blaettert.
    Eine Kategorie "Kampf" GAB ES in V4.90 schon und wurde in V4.91 wieder
    ausgebaut (STATUS-Abschnitt 2026-07-18, Zeile ~4208).

>>> DIE ENTSCHEIDENDE FRAGE IST BIS HEUTE UNBEANTWORTET, NICHT BEANTWORTET-UND-
    NEGATIV. Wortlaut von damals: "Die V4.90-Fassung kam in-game nie an; statt
    zu debuggen wurde sie auf Wunsch zurueckgebaut ... die offene Frage war nur,
    ob Typ-43-Zeilen ueberhaupt ankommen - das klaert ein Log mit aktiver
    Roh-Probe." Es ist also NICHT belegt, dass der Weg ueber IChatGui nicht
    traegt; es ist nur nie gemessen worden. Ohne diese Messung wird hier nichts
    gebaut (Fact Discipline).

>>> SONDE GEBAUT (2026-08-04): `ChatReaderService.ProbeCombatLine`, [Conditional
    ("DEBUG")], sitzt genau an der Stelle, an der Kampfzeilen bisher still
    verworfen werden (`if (IsCombatLogLine(...)) { ProbeCombatLine(msg); return; }`).
    Loggt je Zeile: laufende Nummer, RAW-LogKind hex, maskierte Basis (&0x7F),
    Sender, Payload-Typen und Text. Die Payload-Typen sind bewusst dabei - dort
    wuerde ein spaeteres Feature WER-auf-WEN lesen, statt Namen aus einem
    lokalisierten Satz zu klauben.
    Build Debug 0/0, nach devPlugins deployt. Release bleibt unberuehrt
    (Conditional), es ist noch NICHTS am Verhalten geaendert.

>>> MESSUNG 1 AUSGEWERTET (2026-08-04, 104 Zeilen im Log 08:32-08:40).
    ERGEBNIS 1 - DER WEG TRAEGT. Die Zeilen kommen sehr wohl bei
    IChatGui.ChatMessage an. Die V4.91-Notiz "kam in-game nie an" war also ein
    Fehlschluss aus einem anderen Fehler, nicht die Wahrheit ueber den Kanal.
    Verteilung: 43 Aktion (49x), 41 Schaden (23x), 46 Buff (23x), 48 Buff weg
    (6x), 42 Fehlschlag (1x), 47 Debuff (1x), 49 Debuff weg (1x).
    Textbeispiele, wie das Spiel sie fertig liefert:
    "Der Morbol trifft dich und verursacht 232 Punkte Schaden.",
    "   Kritischer Treffer! Die Riesenbiene erleidet 40618(+60%) Punkte Schaden.",
    "Du bleibst unbeeinflusst.", "   Du erleidest den Effekt von  Gift."
    (fuehrende Leerzeichen und Doppel-Leerzeichen sind echt -> beim Einbauen
    durch AtkText.ReadClean-artiges Trimmen schicken).
    ERGEBNIS 2 - DIE ALTE ANNAHME IM CODE-KOMMENTAR IST WIDERLEGT. Dort stand,
    reale Zeilen kaemen als KOMBINIERTE Werte mit Quell-/Ziel-Bits im hoeheren
    Byte. In ALLEN 104 Zeilen gilt raw == basis (nur 0x0029-0x0031, kein
    einziges gesetztes hohes Bit). Ueber den LogKind ist eigen/fremd NICHT
    trennbar. Die Maskierung `& 0x7F` schadet nicht, aber sie leistet nichts.
    ERGEBNIS 3 - PAYLOADS REICHEN AUCH NICHT. Fremde Spieler tragen einen
    Player-Payload ("Horst Brot setzt Blutige Faenge ein."), eigene Zeilen nie
    (das Spiel schreibt "Du"/"dich" statt eines Links). Aber Gegner-gegen-
    Gegner-Zeilen ("Die Riesenbiene trifft den Harzbohrer") haben ebenfalls
    keinen Player-Payload - "kein Player" heisst also NICHT "betrifft mich".

>>> DIE SAUBERE QUELLE IST GEFUNDEN - UND ZWAR IN DALAMUD, NICHT IM SATZTEXT.
    `IChatMessage` hat neben LogKind zwei weitere Felder: `SourceKind` und
    `TargetKind`, beide vom Typ `XivChatRelationKind` (ilspycmd-verifiziert
    2026-08-04 an Dalamud.dll, dev-Hooks). Werte:
    None, LocalPlayer, PartyMember, AllianceMember, OtherPlayer, EngagedEnemy,
    UnengagedEnemy, FriendlyNpc, PetOrCompanion, PetOrCompanionParty,
    PetOrCompanionAlliance, PetOrCompanionOther.
    Damit waere "betrifft mich" exakt SourceKind==LocalPlayer (ich handle) oder
    TargetKind==LocalPlayer (mich trifft es) - sprachunabhaengig, ohne ein
    einziges geparstes Wort. Genau das braucht die vom User gewaehlte
    Bedienform.
    NOCH NICHT BELEGT: dass diese Felder bei Kampflog-Zeilen auch GEFUELLT sind
    (sie koennten None sein). Deshalb Messung 2.

>>> SONDE ERWEITERT + GEBAUT (Debug 0/0, deployt): loggt jetzt zusaetzlich
    quelle=<SourceKind> ziel=<TargetKind> je Zeile.

>>> MESSUNG 2 ANGEFANGEN, ABER NOCH NICHT VOLLSTAENDIG (Stand 2026-08-04,
    Sitzungsende). Bisher genau EINE Zeile im Log:
    08:44:58 "#1 raw=0x002B basis=43 quelle=OtherPlayer ziel=None
              text='Emo Yumi hat den Gesellschafts-Chocobo bestiegen.'"
    WAS DAS SCHON BELEGT: die Felder werden GEFUELLT, nicht auf None gelassen -
    ein fremder Spieler wurde als OtherPlayer erkannt, ohne Namensvergleich.
    Die Hauptsorge aus Messung 1 ist damit weitgehend erledigt.
    WAS ES NICHT BELEGT (ein Fall, ein LogKind): dass auch LocalPlayer gesetzt
    wird. Genau daran haengt alles.

>>> STAND DER MESSUNG 2 AM 2026-08-06 (64 Sonden-Zeilen im Log, statt einer):
    (a) IST BELEGT. quelle=LocalPlayer kommt bei eigenen Aktionen:
        #54 "Du setzt Sprint ein." (basis=43), #59 "Du setzt Rueckfuehrung ein.",
        #58 "Du beginnst, Rueckfuehrung einzusetzen." (quelle UND ziel
        LocalPlayer). Eigene Handlungen sind damit sprachunabhaengig erkennbar.
    (a2) ziel=LocalPlayer wird ebenfalls gefuellt, belegt an Buffs auf mich
        selbst: #55/#56 "Du erhaeltst den Effekt von ...", #57 "Du verlierst ...".
    (a3) SCHADENSZEILEN FUELLEN BEIDE FELDER SAUBER: #44 "Mortholas Tolkien
        erleidet 98 Punkte Schaden." kam mit quelle=UnengagedEnemy
        ziel=OtherPlayer. Im ziel-Feld steht also wirklich der GETROFFENE.
    (b) FEHLT WEITERHIN - der eine direkte Fall: eine Schadenszeile (basis=41)
        mit ziel=LocalPlayer, also der Spieler selbst wird getroffen. Alle vier
        gemessenen Schadenszeilen betrafen fremde Spieler und Gegner.
        Aus (a) und (a3) folgt es sehr wahrscheinlich - aber NICHT behaupten,
        bevor es im Log steht. An genau diesem Wert haengt die Live-Ansage
        "erlittener Schaden".
        SO ZU HOLEN: irgendeinen Gegner angreifen und ein paar Treffer
        kassieren, danach grep "quelle=" im Log.
    Erst danach bauen. PartyMember/AllianceMember sind NICHT blockierend (User
    2026-08-04: "das mit party membern mache ich spaeter") - sie fallen ohnehin
    in "Kampf Umgebung"; OtherPlayer ist als Stellvertreter bereits belegt.
    Auswertungsbefehl: grep "quelle=" in
    C:\Users\brued\AppData\Roaming\XIVLauncher\dalamud.log

>>> ARBEITSBAUM: `FF14Accessibility/Services/ChatReaderService.cs` ist GEAENDERT
    und NICHT committed (nur die [Conditional("DEBUG")]-Sonde + der berichtigte
    Kommentar). Am Verhalten der Release-Version ist NICHTS geaendert - die
    Kampfzeilen werden weiterhin verworfen. Wer hier weitermacht, faengt also
    mit einem sauberen Release-Stand plus Sonde an.

>>> ZU BERICHTIGEN, WENN GEBAUT WIRD: der Kommentar ueber `IsCombatLogLine`
    behauptet noch "Real messages can arrive as combined values with
    source/target bits set high". Das ist durch Messung 1 widerlegt (104 von
    104 Zeilen flach). Nicht stehen lassen.

>>> BEDIENFORM VOM USER ENTSCHIEDEN (2026-08-04):
    - ZWEI getrennte Nachlese-Kategorien: "Kampf" (was mich betrifft: eigene
      Aktionen, eigener Schaden, Treffer/Fehlschlaege auf mich) und
      "Kampf Umgebung" (Gruppe, Gegner untereinander). Der Spieler waehlt beim
      Blaettern, was er hoeren will.
    - LIVE nur der ERLITTENE Schaden. Alles andere bleibt im Kampf still.

>>> ACHTUNG - BEIDE ENTSCHEIDUNGEN HAENGEN AN DERSELBEN UNGEKLAERTEN FRAGE:
    sie brauchen WER-auf-WEN. Ohne eine belastbare Trennung "betrifft mich" vs.
    "betrifft mich nicht" gibt es weder zwei Kategorien noch eine Live-Ansage
    fuer erlittenen Schaden. Genau das misst die Sonde (hohe Bits des LogKind
    und/oder Payloads). Faellt die Messung negativ aus, muss die Bedienform
    neu besprochen werden - dann NICHT heimlich auf Namens-Parsing im
    lokalisierten Satztext ausweichen (waere Workaround-Discipline-pflichtig).

## VORHERIGE STUFE (2026-08-03, SPRACH-AUDIT V5.71 - RELEASED, IN-GAME UNGETESTET)

>>> RELEASE v5.71 IST DRAUSSEN (Commit bff9bed, Tag v5.71).
    Versions-Sync war wieder noetig: csproj + repo.json standen auf 5.70,
    Plugin.cs schon auf 5.71 - der uebliche Drift.
    VERIFIZIERT, nicht nur behauptet: v5.71 ist "Latest"; alle 4 Assets
    "uploaded"; der Download ueber releases/latest/download/latest.zip
    liefert 613.664 Bytes mit Manifest 5.71.0.0 und DLL-Dateiversion
    5.71.0.0; repo.json auf main traegt 5.71.0.0 (ueber die API gegengelesen).
    Installer unveraendert (git diff dd24a14..HEAD -- Installer/ ist leer)
    -> exe + installer.json aus v5.70 uebernommen, SHA256 gegengeprueft
    (5787445B...CAD49 stimmt), damit der Update-Pfad fuer Nutzer mit
    aelterem Installer intakt bleibt.
    ACHTUNG: IN-GAME UNGETESTET released - weder deutsch noch englisch.
    Der englische Nutzer ist der einzige, der die EN-Seite pruefen kann.

>>> USER-FRAGE: "kommen noch deutsche Sachen, die nicht uebersetzt werden?"
    JA - 24 Stellen gefunden und behoben. Alle liefen ueber harte Literale
    mitten im Service-Code, also GESPROCHEN und an /acc lang vorbei.
    Warum Teil 1 sie verfehlt hat: der damalige Sweep suchte nach Umlauten
    und Ansage-Literalen; Woerter ohne Umlaut ("Chance", "Betrag", "rar",
    "Leer") und zusammengesetzte Formen ("{n} von {m}") blieben unsichtbar.
    Diesmal gesucht wurde nach Ansage-BAUFORMEN (parts.Add / sb.Append /
    Speak / desc= / text=) plus deutschen Funktionswoertern.
    BEHOBEN, nach Fenster sortiert:
    - Sammel-Fenster: "Chance N Prozent", "Bonus N Prozent", "rar",
      "verborgen", "Belastbarkeit N von M"
    - Gil-Depot: "Gil-Depot", "Betrag N", "X: derzeit A, danach B",
      "Truhe X", "Hinterlegen"/"Entnehmen" (beide Ableitungswege)
    - Inventar/Gegenstands-Slots: "N mal Name", "Leer"
    - Chat-Eingabezeile: "Chat-Eingabe" / "Chat-Eingabe, <Kanal>"
    - Weltenwahl: "Datenzentrum waehlen. Regionen: ..." (hatte zusaetzlich
      einen kaputten Umlaut U+FFFD, jetzt sauber)
    - Quest-Detail: "Ziel: ...", "Beschreibung: ..." (letzteres auf den
      schon vorhandenen Baustein ItemDescription gelegt)
    - Bestiarium: ". Lebt in X" und der Verbinder ", oder " zwischen den
      Fundgebieten
    - Konfig-Reiter ohne Beschriftung: "Reiter N von M."
    - Listen-/Knopf-Positionen an 6 Stellen: "N von M" -> Counter()
      (Baustein war da, wurde nur nicht benutzt; neuer String-Overload fuer
      Fortschrittsanzeigen wie "3/5")
    - Plugin-Liste: "Unbenanntes Plugin"
    - Ja/Nein-Dialog: die Fallback-Beschriftungen (greifen nur, wenn die
      Knopf-Nodes leer sind - normal werden die Labels gelesen)
    NEBENFUND UND MITGEFIXT: BestiaryService cachte den FERTIGEN Satz
    inklusive Verbinder. Ein spaeteres /acc lang haette den nicht mehr
    erreicht. Jetzt werden die Gebiete als Array gecacht und erst beim
    Sprechen verbunden (CLAUDE.md: "Cache references, never values").
    NICHT ANGEFASST, mit Absicht:
    - Match-Strings ("Schliessen", "Ok", Journal-Header, Social-Reiter,
      "Aetheryt"/"Aethernet", "St."-Ersetzung) - das ist Teil 2, dort macht
      Uebersetzen die Erkennung KAPUTT.
    - Debug-Sonden (Objekt-/Marker-Sonde, ConfigSystem-Dump, fishobj) und
      die /acc keys-Dumpdatei - Diagnose-Export, bleibt deutsch.
    - Alle _log-Zeilen.
    TOTER FUND: `KeyNames.Speak()` ("Strg plus Umschalt plus N", "keine
    Taste") wird von NIEMANDEM aufgerufen. Deshalb nicht uebersetzt, sondern
    zum Loeschen vorgemerkt - erst pruefen, ob es fuer die Tastenansage
    gedacht war und vergessen wurde.
    Build: Debug 0 Warnungen / 0 Fehler, nach devPlugins deployt. V5.71.
    IN-GAME UNGETESTET - was sich fuer den deutschen Spieler aendern soll:
    NICHTS. Alle deutschen Wortlaute wurden 1:1 uebernommen.

>>> RUECKMELDUNG DES ENGLISCHEN NUTZERS (via User, 2026-08-03), im Wortlaut:
    "the only german messages left that I'm noticing are just single words,
    like von and mal, usually to do with quantities or lists. like in the
    hunting log, the entries say 2 von 10. in inventory, the quantity is
    potion mal 5, or also when selecting a quest reward"
    ALLE DREI SIND VOM AUDIT OBEN ERFASST - im Code nachgewiesen, nicht
    vermutet:
    (1) Jagdjournal "2 von 10" = `TryFormatProgress` (UIReaderService:7464),
        wandelt den Client-Text "2/10" in Sprache -> laeuft jetzt ueber
        Counter().
    (2) Inventar-Menge = der Item-Slot-Fokus (UIReaderService:2522).
    (3) Quest-Belohnung = DERSELBE Pfad wie (2). Belegt durch den Kommentar
        bei :2006, der die Belohnungs-Slots ausdruecklich als "10 mal
        Universalkoeder" beschreibt - die Belohnungen werden seit 2026-08-02
        ausschliesslich ueber den Fokus-Pfad gelesen.
    OFFENE UNSCHAERFE, NICHT WEGERKLAERT: er sagt "potion mal 5" (Name
    zuerst). Unser Fokus-Pfad baut "5 mal Potion" (Menge zuerst). Die Form
    "Name mal Menge" gibt es nur in `ItemStack` (Taschen-Liste,
    InventoryService) - und die ist seit Teil 1 zweisprachig ("Potion times
    5"). Entweder hat er aus dem Gedaechtnis zitiert, oder er laeuft auf
    einer aelteren Version. BEIM NAECHSTEN KONTAKT KLAEREN: welche Version
    zeigt ihm `/acc` beim Start an?

>>> DABEI EINEN ECHTEN FEHLER GEFUNDEN, DEN MEIN EIGENER FIX ERZEUGT HAETTE:
    `IsSpokenProgress` (UIReaderService:7311) erkennt die Fortschritts-Zahl
    einer Jagdjournal-Zeile daran, dass das mittlere Wort "von" ist - also
    an unserer EIGENEN deutschen Ausgabe. Sobald Counter() englisch "2 of 10"
    spricht, greift der Vergleich nicht mehr, `TryExtractBestiaryMonster`
    liefert false, und die Zeile wird mit `continue` UEBERSPRUNGEN: die
    Jagdjournal-Uebersicht waere auf Englisch komplett LEER gewesen.
    Behoben: neuer Baustein `AccessibilityStrings.CounterConnector`
    ("von"/"of"), den sowohl Counter() als auch der Vergleich benutzen.
    Das ist genau die im Lokalisierungs-Memory notierte Falle "manche
    deutschen Strings sind zugleich Vergleichswerte" - nur diesmal nicht
    gegen Client-Text, sondern gegen unseren eigenen.
    Gegengeprueft: ein Grep ueber alle Vergleiche gegen Text-Literale zeigt
    sonst nur Tastenbelegungen (sprachneutral) und bekannte Teil-2-Match-
    Strings gegen Client-Text.

>>> DOPPELUNG BESEITIGT: mein neuer `HabitatSuffix` war eine zweite Fassung
    des schon vorhandenen `LivesIn`. HabitatSuffix ist raus, beide Bestiarium-
    Stellen sagen jetzt dasselbe.

>>> ZU TESTEN (V5.71): einmal deutsch gegenpruefen, dass keine Ansage
    verstummt oder anders klingt (Sammel-Fenster, Gil-Depot, Inventar,
    Chat-Eingabe, Bestiarium/Jagdjournal). Danach `/acc lang en` und
    dieselben Fenster - dort darf kein deutsches Wort mehr kommen, und das
    Jagdjournal MUSS weiterhin Zeilen liefern (siehe Fund oben).

## VORHERIGE STUFE (2026-08-03, AUSRUESTUNGS-WERTE IN-GAME BESTAETIGT)

>>> IN-GAME BESTAETIGT (User: "das mit der ruestung funktioniert").
    Log-Beleg (dalamud.log, 2026-08-02 21:26 bis 23:06), 14 verschiedene
    Teile geloggt, z.B.:
    - 'Eisen-Schuppenpanzer' (Id 3057): LevelItem.RowId=23 LevelEquip=23
      -> Gegenstandsstufe 23, Verteidigung 57, Magieabwehr 57, Staerke +4,
         Konstitution +4, Direkter Treffer +4, 2 Materia-Slots
    - 'Gepluenderte Guisarme' (Id 31423): LevelItem.RowId=17 LevelEquip=15
      -> Gegenstandsstufe 17, Angriff 24, Magieschaden 12,
         Verzoegerung 2,8 Sekunden, + vier Attribute
    Waffen-Zweig (Angriff/Magieschaden/Verzoegerung) und Ruestungs-Zweig
    (Verteidigung/Magieabwehr) greifen beide. Attributnamen kommen sauber
    in Spielsprache. Keine Fehler, keine leeren Attributnamen im Log.

>>> GEPLANTE GEGENPROBE IST INS LEERE GELAUFEN - ANNAHME BLEIBT OFFEN.
    `LogStatsOnce` sollte die Item-Beschreibung als unabhaengigen Beleg
    dafuer liefern, dass Gegenstandsstufe = `LevelItem.RowId` ist. In ALLEN
    14 Log-Zeilen ist `Beschreibung: ''` - Standard-Ausruestung hat schlicht
    keine Description im Sheet. Die Gegenprobe existiert also nicht.
    WAS TATSAECHLICH GESTUETZT IST: bei allen 13 Ruestungsteilen gilt
    LevelItem.RowId == LevelEquip; nur die Quest-Waffe weicht ab (17 vs 15),
    was fuer Quest-Waffen erwartbar ist. Das ist ein Plausibilitaets-Indiz,
    KEIN Beweis. Die relative Ordnung (Teil A hoeher als Teil B) stimmt in
    jedem Fall - genau darum ging es dem User.
    OFFEN: harte Gegenprobe gegen eine externe Item-Datenbank oder gegen
    ein Item MIT Beschreibung, falls die absolute Zahl je strittig wird.
    Wer den Beschreibungs-Beleg nicht mehr braucht, kann `LogStatsOnce`
    bei Gelegenheit entfernen (Debug-Sonden-Konvention).

>>> LAENGE IST OK (User 2026-08-03: "das ist ok"). Nicht kuerzen.

>>> `LogStatsOnce` ENTFERNT (2026-08-03): die Sonde konnte ihren Zweck nicht
    mehr erfuellen, weil die Beschreibungen leer sind. Der XML-Kommentar an
    `DescribeStats` haelt jetzt fest, was belegt ist und was nicht.

>>> WEITERHIN UNGETESTET AUS V5.69: unbegrenzter Nachlese-Puffer.

## VORHERIGE STUFE (2026-08-02 SPAETABENDS, V5.70 RELEASED)

>>> RELEASE v5.70 IST DRAUSSEN (Commit dd24a14, Tag v5.70).
    Versions-Sync war noetig: csproj + repo.json standen noch auf 5.67,
    Plugin.cs schon auf 5.70 (der uebliche Drift - Plugin.cs wird pro Feature
    gebumpt, die anderen beiden nur beim Release).
    VERIFIZIERT, nicht nur behauptet: v5.70 ist "Latest"; alle 4 Assets
    hochgeladen; der Download ueber releases/latest/download/latest.zip
    liefert 612.987 Bytes mit Manifest 5.70.0.0 und DLL-Dateiversion
    5.70.0.0; repo.json auf main traegt 5.70.0.0.
    Installer unveraendert -> exe + installer.json aus v5.67 uebernommen,
    SHA256 gegengeprueft (5787445B...CAD49 stimmt) - der Update-Pfad fuer
    Nutzer mit aelterem Installer bleibt intakt.
    ACHTUNG: Ausruestungs-Werte und der unbegrenzte Nachlese-Puffer sind
    UNGETESTET released (User wollte den Release, Hinweis war gegeben).

## VORHERIGE NOTIZ (Bauzustand V5.70)

>>> AUSRUESTUNGS-WERTE HINTER DER BESCHREIBUNG (User-Wunsch: "damit man weiss
    welches Teil evtl. besser ist"). Gebaut, IN-GAME UNGETESTET.
    ANSATZPUNKT WAR SCHON DA: `GearInfoService.DescribeGear` ist die EINE
    Stelle, durch die alle Ausruestungs-Ansagen laufen (Quest-Belohnung
    UIReaderService:6427, Inventar/Laden-Fokus :2517, Laden-Zeilen ueber
    DescribeByName, getragene Teile ueber EquipmentService). Werte dort
    angehaengt = ueberall vorhanden, ohne eine einzige Aufrufstelle zu aendern.
    NEUE METHODE `DescribeStats(row)` liest (alle ilspycmd-verifiziert
    2026-08-02 an Lumina.Excel.Sheets.Item): LevelItem@138 (Gegenstandsstufe),
    DefensePhys@50, DefenseMag@52, DamagePhys@40, DamageMag@42, Delayms@44,
    BaseParam/BaseParamValue (parallele 6er-Collections), MateriaSlotCount@103.
    Nullwerte fallen raus - der Spiel-Tooltip zeigt sie auch nicht.
    Attributnamen kommen aus `BaseParam.Name` in Spielsprache, werden also
    GELESEN und nicht von uns uebersetzt.
    BEWUSST NICHT DRIN: HQ-Bonus (BaseParamSpecial/-ValueSpecial) und Materia-
    Effekte - die gehoeren zum konkreten Stueck im Beutel, die Sheet-Zeile
    beschreibt das Grundteil. Ein Grundwert als Endwert waere eine Luege.
    KURZ-MODUS UNVERAENDERT: Strg+F6 (alle 12 getragenen Teile) bleibt bei
    "Stufe N" ohne Werte - zwoelf Wertebloecke waeren nicht hoerbar.
    EINE ANNAHME, DIE DER ERSTE TEST BELEGT: Gegenstandsstufe = RowId von
    `LevelItem` (das ItemLevel-Sheet ist NACH Gegenstandsstufe indiziert).
    Deshalb loggt `LogStatsOnce` (einmal je Item-Id) die RowId ZUSAMMEN mit
    der Item-Beschreibung - viele Beschreibungen schreiben "Gegenstandsstufe: N"
    aus, das ist die unabhaengige Gegenprobe. Beim naechsten Test pruefen.

>>> ZU TESTEN (V5.70): Quest-Belohnung mit Ruestung oeffnen und durchblaettern,
    dann ein Ruestungsteil im Inventar fokussieren. Kommen Gegenstandsstufe,
    Verteidigung und Attribute? Und: ist es zu lang zum Navigieren? (Der User
    entscheidet, ob gekuerzt wird - z.B. nur Gegenstandsstufe + Verteidigung.)

## VORHERIGE STUFE (2026-08-02 SPAETABENDS, V5.69)

>>> NACHLESE-PUFFER OHNE MENGENBEGRENZUNG (User-Wunsch, V5.69).
    `MessageHistoryService`: die Konstante `Max = 50` je Kategorie ist weg,
    es wird nichts mehr vorne abgeschnitten. Damit entfaellt auch das
    Nachziehen des Blaetter-Cursors - ein Eintrag behaelt seinen Index die
    ganze Sitzung. Die uebrige Logik war schon groessenunabhaengig.
    Beim Beenden des Spiels ist der Verlauf weg (reiner Arbeitsspeicher,
    nichts wird auf Platte geschrieben) - das war vorher auch so.
    READMEs (DE+EN) angepasst: "je 50 Nachrichten" -> ohne Begrenzung.

>>> STUMME STELLE GEMELDET, NOCH NICHT GEKLAERT: Chocobo/Mitstreiter.
    Log 21:10:29 zeigt: Fenster `Buddy` ("MITSTREITER") und Kommando-Ring
    `BuddyAction`. Fuer BEIDE gibt es KEINEN Handler im Code - das lief nur
    ueber den allgemeinen Pfad. Zwei Symptome im Log, User-Klaerung steht aus:
    (1) Buddy sagt beim Oeffnen nur 'Rang:' - nackte Beschriftung ohne Wert,
        Chocobo-Name fehlt ganz.
    (2) Im BuddyAction-Ring ist ein Eintrag stumm (Text='', zwischen
        'Fortschicken' und 'Heilen'); beim Oeffnen wird u.a. "Auf-/Absteigen"
        genannt, was zum Aufsteigen passen wuerde.
    MOEGLICHE URSACHE, NICHT BELEGT: Timing - beim Oeffnen war ein Textfeld
    leer und 34 ms spaeter mit 'Herbeigerufen' gefuellt (bekannte Falle,
    siehe game-api.md "LISTEN-TIMING").
    NAECHSTER SCHRITT: Dump angefordert, User tippt bei offenem Fenster
    `/acc dump Buddy BuddyAction`. Nichts gebaut, nichts vermutet.

## VORHERIGE STUFE (2026-08-02 SPAETABENDS, QUEST-KATEGORIEN GEFIXT + IN-GAME BESTAETIGT, V5.68)

>>> IN-GAME BESTAETIGT (User: "ok funktioniert", Log 20:01, Limsa Lominsa):
    "[Nav] Quest-NPCs: 2 von 60 Objekten (per Marker 2, per Symbol 2;
    7 Ids aus Markern). Symbole: Baensyng=71203, Thubyrgeim=71351"
    - Beide Quellen liefern UNABHAENGIG dieselben zwei NPCs. Kein Widerspruch,
      keine falschen Treffer beobachtet.
    - Die Filter arbeiten sichtbar: von 10 Marker-Orten fielen genau drei weg
      (1x Obj=0 Typ=51 = reiner Ortsmarker, 2x fremde Zone terr=250/144),
      7 Ids blieben. Level-Sheet loest sauber auf, z.B.
      LevelId=4069594->Obj=1003272 Typ=8 terr=129.
    - NEUE MESSWERTE fuer NamePlateIconId: 71203, 71351 (dazu 71201 von
      Buscarron). ALLE drei echten Werte liegen bei 712xx/713xx - die
      Bereiche 71001-71006 ("verfuegbar") und 71021-71046 ("aktiv") in
      QuestMarkerHint haben weiterhin NULL Messwerte und greifen nie.
      Wer sie feiner aufteilen will, muss erst messen (z.B. ein NPC mit
      annehmbarer Quest vs. einer mit abgabebereiter Quest).
    OFFEN, weil in Limsa nicht pruefbar: Quest-Objekte (0 von 11) und
    Quest-Gegner - dort gab es schlicht keine. Erst bei passender Quest testen.

## VORHERIGE ZWISCHENSTUFE (2026-08-02 SPAETABENDS, FIX GEBAUT)

>>> BEFUND DES USERS: "in der Kategorie Quest-NPCs steht nichts drin, obwohl
    die NPCs in der normalen NPC-Liste stehen." Log bestaetigt es eindeutig:
    "[Nav] Quest-NPCs: 0 von 60 Objekten ... (0 IDS AUS MARKERN)" - es lag
    nicht am Abgleich, es kamen ueberhaupt keine Ids aus den Markern.

>>> URSACHE (ilspycmd 2026-08-02, KEINE Vermutung): `MapMarkerData.DataId`
    taugt nicht als Objekt-Id. Zwei unabhaengige Belege:
    (1) Es ist ein **ushort** - eine NPC-BaseId (1.000.000+) passt nicht in
        16 Bit, der Abgleich konnte NIE treffen.
    (2) `SetData(levelId, tooltip, icon, x,y,z, radius, territoryTypeId,
        mapId, placeNameZoneId, placeNameId, recommendedLevel, eventState)`
        hat gar keinen dataId-Parameter - das Feld wird nie geschrieben und
        bleibt 0.
    LEHRE: Ein Feld, das dem Namen nach passt, ist noch kein Beleg. Der
    SETZER haette es gestern verraten - die Signatur stand bereits im
    dekompilierten Struct, direkt ueber dem Feld.

>>> FIX (V5.68): zwei spieleigene Quellen, ODER-verknuepft, keine Heuristik.
    (1) MARKER ueber das Level-Sheet: `MapMarkerData.LevelId`@0 (= erster
        SetData-Parameter) ist die Zeile im Lumina-Sheet `Level`, und die
        traegt `Object` (uint @20) - die Datensatz-Id des Objekts an diesem
        Ort - typisiert ueber `Type` @32 (8=ENpcBase, 9=BNpcBase, 45=EObj).
        Dieselbe Id, die der Browser als `IGameObject.BaseId` sieht;
        Gegenprobe: die NPC-Titel kommen schon laenger ueber
        `ENpcResident.TryGetRow(obj.BaseId)` und stimmen.
        `Level.Territory` wird gegen die aktuelle Zone geprueft.
    (2) NAMENSSCHILD-SYMBOL: `GameObject.NamePlateIconId` - das Zeichen, das
        ein sehender Spieler ueber dem Kopf sieht. War laengst im Code
        (NpcPrefix sagt "Quest verfuegbar"), nur nie zum Filtern benutzt.
        Gemessen bisher genau ein Wert: 71201 bei Buscarron (Log 19:49).
    Methode heisst jetzt `QuestMarkerService.GetQuestObjectIds()`.

>>> DAS LOG BEANTWORTET BEIM NAECHSTEN TEST ALLES ALLEIN:
    - "[Quest] Objekt-Ids aus Markern (N): 'Quest'[1] LevelId=x->Obj=y Typ=8
      terr=129 | ..." - zeigt pro Marker-Ort, ob LevelId 0 ist, ob die Zeile
      im Sheet steht, welches Objekt und welche Zone herauskommt.
    - "[Nav] Quest-NPCs: A von B Objekten (per Marker X, per Symbol Y; N Ids
      aus Markern). Symbole: Name=71201, ..." - trennt die beiden Quellen
      und listet JEDES Symbol ungleich 0 mit Objektnamen.
    Damit laesst sich `QuestMarkerHint` (Bereiche 71001-71006 usw. sind bis
    heute NICHT messbelegt!) aus echten Daten schaerfen.

>>> ZU TESTEN: In einer Zone mit Quest-NPC den Browser auf "Quest-NPCs"
    stellen. Steht der NPC drin? Und stehen NUR Quest-NPCs drin (Symbol-
    Bereich koennte zu weit sein)? Danach Log ansehen.
    Offen bleibt bewusst: bei `Type=9` (BNpcBase) gilt eine Id fuer ALLE
    Gegner derselben Art in der Zone - fuer "toete 3 Kaefer" richtig, aber
    es ist eine Art, kein Einzelgegner.

## VORHERIGER STAND (2026-08-02 ABENDS, CHAT-KANAELE + CHATLOG-EINSTELLUNGEN, V5.67 RELEASED)

>>> ALLES UNTEN als V5.67 released. A und B in-game bestaetigt; die
    Quest-Kategorien (unten, eigener Block) fahren UNGETESTET mit.

    NACHTRAG ZU B: `SetContextTellTarget` funktioniert NICHT mit accountId/
    contentId = 0 - das Spiel lehnte jeden Versuch ab (Log 18:01-18:52
    "gesetzt: False"). Ersetzt durch `ChangeChatChannel(17, 0, "Name@Welt", true)`;
    ChatType 17 = Fluestern ist gemessen (Probe 17:47, Label "... zufluestern"
    mit gefuelltem TellName/TellWorld). In-game bestaetigt.
    GEMESSENE ChatTypes jetzt: 1 = Sagen, 2 = Gruppe, 6 = Freie Gesellschaft,
    17 = Fluestern. Weiterhin offen: Rufen, Schreien, Allianz.

    NACHTRAG ZU C: Die Icon-Messung wurde NICHT gebraucht - siehe Quest-
    Kategorien unten. Die Sonde bleibt trotzdem drin (kostenlos, hilft falls
    der DataId-Filter jemanden uebersieht).

>>> QUEST-KATEGORIEN GEBAUT (User-Entscheid "eigene Kategorien dazu"),
    IN-GAME UNGETESTET, mit v5.67 released:
    Drei neue Kategorien hinter "Objekte": Quest-NPCs, Quest-Objekte,
    Quest-Gegner (NavCategory + Categories-Tabelle + bilinguale Labels).
    MECHANIK - WICHTIG, ERSPART RATEREI: `MapMarkerData.DataId` (@68,
    ilspycmd 2026-08-02) traegt die Datensatz-Id des Objekts, auf das ein
    Quest-Marker zeigt - dieselbe Id wie `IGameObject.BaseId` im Browser.
    Neue Methode `QuestMarkerService.GetQuestObjectDataIds()` sammelt sie aus
    QuestMarkers + UnacceptedQuestMarkers; `GetCategoryObjects` filtert danach.
    KEINE Icon-Tabelle, KEINE Abstands-Heuristik noetig.
    Grenzen bewusst: nur Marker der AKTUELLEN Zone (sonst markiert eine Id aus
    einer anderen Zone einen gleich aussehenden NPC nebenan), DataId 0 wird
    uebersprungen (reine Ortsmarker ohne Objekt).
    OFFEN in-game: zeigt "Quest-NPCs" wirklich die richtigen? Log-Zeile
    "[Nav] Quest-NPCs: X von Y Objekten tragen eine Quest-DataId" zeigt sofort,
    ob der Filter zu streng oder zu locker ist.
    USER-HINWEIS, DER ZEIT SPARTE: "du solltest eigentlich schon wissen wie das
    aussehen muss, in den normalen Kategorien ist es ja schon drin" - richtig,
    QuestMarkerService las die Marker laengst, nur die DataId wurde nie
    ausgewertet. ERST PRUEFEN WAS DA IST (zweites Mal an einem Tag).

    A) IN NACHLESE-KANAL SCHREIBEN (User-Wunsch, IN-GAME BESTAETIGT "das
       funktioniert"). Stehst du in der Nachlese auf einer Kategorie und
       druECKST ENTER, setzt das Plugin vorher den Sende-Kanal. Neue Datei
       `ChatChannelService.cs`. Bestaetigung ist die vorhandene Ansage
       "Chat-Eingabe, <Kanal>" - bewusst KEINE zweite Ansage.
       Drei Sicherungen: (1) nur 30 s nach der letzten Nachlese-Aktion
       (MessageHistoryService.LastActivity), (2) NIE bei offener Eingabezeile
       (dort sendet Enter - Kanalwechsel wuerde die Nachricht fehlleiten),
       (3) nur GEMESSENE Kanaele.
       KANAL-ZUORDNUNG GEMESSEN (Sonde [ChatTypeProbe], Log 17:22-17:23):
       ChatType 1 = Sagen, 2 = Gruppe, 6 = Freie Gesellschaft.
       NOCH NICHT GEMESSEN: Rufen, Schreien, Allianz, Fluestern -> dort sagt
       das Plugin "Kanal X kann noch nicht gesetzt werden" statt zu raten.
       Nachtragen NUR aus Sondendaten (User schaltet mit /sh, /y, /t durch).

    B) FLUESTER-ANTWORT AUS DEM PUFFER (gebaut, IN-GAME UNGETESTET).
       Anlass: /t braucht zwingend "Name@Welt", und der Weltname ist genau das,
       was ein blinder Spieler nirgends nachschlagen kann (User scheiterte
       17:40-17:44, "Shiva" war falsch geraten - aus einer Listenspalte).
       LOESUNG: Der Nachlese-Puffer speichert bei Fluestern jetzt den PARTNER
       (Record `TellTarget(Name, World)`) aus dem PlayerPayload der Nachricht -
       Spiel-eigene Daten, kein Namens-Parsing. Gilt fuer beide Richtungen
       (bei TellOutgoing steht die Gegenseite ebenfalls in Sender).
       Bedienung: Alt+Bild auf "Fluestern", ggf. mit Umschalt+Bild zur richtigen
       Nachricht blaettern, Enter -> `SetContextTellTarget(name, world, 0,0,0,0,
       true)`. accountId/contentId/reason sind 0 (Chat-Payload traegt keine Ids);
       der Rueckgabewert wird GEPRUEFT und bei false angesagt, damit ein
       stiller Fehlschlag nicht wie ein gesetztes Ziel wirkt.
       Buffer-Umbau: `List<string>` -> `List<Entry(Text, Partner)>`,
       neue Eigenschaft `CurrentTellPartner` (sucht ab Cursor rueckwaerts den
       naechsten Eintrag MIT Partner).
       OFFEN in-game: Fluestern empfangen -> Kategorie Fluestern -> Enter ->
       geht die naechste Zeile an die Person?

    C) OBJEKT-SONDE ERWEITERT (Vorarbeit fuer die naechste Baustelle):
       `DumpNearbyObjects` loggt jetzt zusaetzlich `NamePlateIconId` (@272) und
       `EventId` (@244), beide ilspycmd-verifiziert 2026-08-02.

>>> NAECHSTE BAUSTELLE: QUEST-KATEGORIEN IM OBJEKT-BROWSER (User-Wunsch,
    Bedienform ENTSCHIEDEN: "eigene Kategorien dazu", also drei neue -
    Quest-NPCs, Quest-Objekte, Quest-Gegner; NICHT als Filter-Umschalter).
    ANSATZ: `GameObject.NamePlateIconId` ist genau das Symbol, das ein SEHENDER
    Spieler ueber dem Kopf sieht - Filtern danach gibt dieselbe Information
    statt einer Rekonstruktion. Welche Icon-Nummer was bedeutet, ist NICHT
    dokumentiert und wird gemessen, NICHT geraten.
    FEHLT NOCH: zwei Messungen per `/acc objprobe` - einmal bei einem NPC mit
    verfuegbarer Quest (Ausrufezeichen), einmal an einem aktiven Quest-Ziel.
    Danach: Icon-Werte in eine Tabelle, drei Kategorien in `Categories`
    (NavigationService) ergaenzen, Labels bilingual in AccessibilityStrings.

## VORHERIGER STAND (2026-08-02, CHATLOG-EINSTELLUNGEN GELOEST + IN-GAME BESTAETIGT, LAENGST RELEASED)

>>> CHATLOG-EINSTELLUNGEN SIND FERTIG. Alle drei Befunde unten abgearbeitet,
    User: "das von vorhin funktioniert".

>>> KORREKTUR 2026-08-04: der Satz "NICHT committed, NICHT released" darunter
    war nur der Bauzustand von damals und ist seit v5.67 ueberholt.
    NACHGEPRUEFT, nicht vermutet: `TryReadConfigPanelControl` und
    `NearestPanelLabel` stehen in UIReaderService.cs (ab Zeile 2683); der
    einfuehrende Commit ist 70e3a28 "Release v5.67", er traegt die Tags v5.67
    und v5.70 und ist Vorfahr von HEAD (`git merge-base --is-ancestor` -> ja).
    Damit steckt der Fix auch im verifizierten v5.72-Paket. Fuer das naechste
    Release ist hier also NICHTS mehr einzupacken.

    FIX 1 - Dropdown-Fehlfund: `FindListInAddon` (~7018) ueberspringt in der
    INNEREN Schleife jetzt Komponenten vom Typ DropDownList. Die falsche Ansage
    "24-Stunden-Format, 2 Eintraege" beim Oeffnen ist weg, und das Panel wird
    nicht mehr per PushMenu als Listen-Menue registriert. Log-Beleg der Wirkung:
    "ConfigCharaChatLogGen: Formular-Fenster, keine Sammel-Ansage beim Oeffnen."

    FIX 2 - Config-Panels lesen beim Oeffnen nicht mehr ihren ganzen Text vor
    (OnPostSetup, `name.StartsWith("Config")` -> return vor der ReadAllTexts-
    Ansage). Begruendung: Formular, kein Textblock; der Kategoriename kommt
    ohnehin vom Reiter-Fokus in ConfigCharacter. Fenstertitel bleibt, Text-Cache
    wird weiter initialisiert (Aenderungserkennung intakt).

    FIX 3 - Slider + DropDownList im Config-Panel-Leser: neue Methoden
    `TryReadConfigPanelControl` und `NearestPanelLabel` (UIReaderService).
    Erkennung ueber den TOP-LEVEL-Owner des Fokus-Nodes (FindTopLevelOwner) -
    noetig, weil das Anzeigefeld eines Dropdowns selbst eine CheckBox-Komponente
    ist und der alte Nahbereich-Aufstieg daraus "12, Schalter, aus" machte
    (Log 2026-08-02 16:35:57). Nur fuer ConfigChara*, weil ConfigSystem dieselben
    Typen ueber AnnounceConfigGlobalFocus liest (sonst doppelte Ansage).
    WICHTIG - LABEL-RICHTUNG IST NICHT EINHEITLICH: in ConfigSystem steht das
    Label VOR dem Control (verifiziert 2026-07-16), im Chatlog-Panel DAHINTER
    (Dump 2026-08-02: Slider Index 34, Label Index 35; alle drei Dropdowns +1).
    NearestPanelLabel sucht deshalb erst vorwaerts, dann rueckwaerts als Rueckfall.
    Neuer bilingualer String `AccessibilityStrings.NoLabel`; die hartcodierte
    Rueckfall-Zeichenkette in NearestPrecedingLabel nutzt ihn jetzt auch.
    BESTAETIGT in-game: Schriftgroesse/Zeitanzeige/Zeiteinstellungen als
    "Auswahlliste" mit Wert, Transparenz als Prozent-Regler.

>>> NAECHSTE BAUSTELLE: CHATFENSTER SELBST (User-Frage 2026-08-02: "was kann man
    da drin noch machen ausser schreiben, sind da Schalter?").
    Bereits vorhanden und dem User genannt: die Nachlese ueber
    MessageHistoryService - Alt+BildAuf/BildAb wechselt die Kategorie (Dialoge,
    Sagen, Rufen, Gruppe, Allianz, Fluestern, Freie Gesellschaft, System, Beute),
    Umschalt+BildAuf/BildAb blaettert darin (50 Nachrichten je Kategorie).
    OFFEN: Struktur des NATIVEN Fensters unbekannt. Addons laut [Win]-Liste:
    `ChatLog` + `ChatLogPanel_0/_1/_2`. Dump angefordert, User tippt:
    `/acc dump ChatLog ChatLogPanel_0 ChatLogPanel_1 ChatLogPanel_2`.
    NICHTS dazu ist gebaut, nichts vermutet.

>>> URSPRUENGLICHE BEFUNDE (jetzt alle abgearbeitet, zur Nachvollziehbarkeit):

    FENSTER: `ConfigCharaChatLogGen` (Charakterkonfiguration -> Kategorie
    "Chatlog"). Dump gesichert unter `docs/dumps/ConfigCharaChatLogGen_2026-08-02.txt`
    (46 Nodes) - der Desktop-Dump wird beim naechsten Strg+F5 ueberschrieben.
    Inhalt laut Dump: 4 Reiter (Allgemein/Kampf/Ereignis/"Reiter 4"), 7 CheckBoxen
    mit Klartext, 3 DropDownLists (Zeitanzeige "24-Stunden-Format" ListLen=2,
    Zeiteinstellungen "Ortszeit" ListLen=2, Schriftgroesse "12" ListLen=12),
    1 Slider "Transparenz der Chat-Eingabe" (Wert 40), 3 Buttons (Festlegen /
    Einstellungen / Namensanzeige).

    BEFUND 1 - FALSCHE ANSAGE, URSACHE BELEGT. User: "er liest Sachen vor die
    eigentlich nicht in dem Fenster sein sollten". Log 2026-08-02:
    15:34:56.523 'Immer, 3 Eintraege' (beim Oeffnen ConfigCharacter) und
    15:35:01.408 '24-Stunden-Format, 2 Eintraege' (1 s nach Kategoriewechsel auf
    Chatlog). URSACHE: `FindListInAddon` (UIReaderService ~7018) sucht die
    Fenster-Hauptliste und steigt dabei EINE Ebene in jede Komponente hinab -
    dort findet es die List(9) INNERHALB der DropDownList(10) und haelt sie fuer
    das Fenstermenue. Folgen: (a) der Wert des ersten Auswahlfelds wird beim
    Oeffnen als Fensterinhalt gesprochen, (b) `PushMenu` registriert das Panel
    als Listen-Menue -> die Navigation verfolgt danach dieses eine Dropdown
    statt der echten Bedienelemente (passt zu "Menue wird nicht aktualisiert").
    GEPLANTER FIX: in der INNEREN Schleife von FindListInAddon Komponenten vom
    Typ DropDownList ueberspringen (aeussere Ebene unveraendert lassen).
    NEBENWIRKUNG BEDENKEN: ohne Listen-Treffer faellt das Panel in den
    generischen Pfad und `ReadAllTexts` wuerde beim Oeffnen ~20 Textfragmente am
    Stueck vorlesen. Mit dem User klaeren, ob das gewollt ist oder ob beim
    Oeffnen gar nichts kommen soll (Kategoriename "Chatlog" wurde ohnehin gerade
    angesagt).

    BEFUND 2 - OFFEN, NICHT ENTSCHIEDEN: Bewegt sich der Fokus im Panel
    ueberhaupt? Im Log steht GENAU EINE Fokuszeile in ConfigCharaChatLogGen
    (15:35:08, Button "Namensanzeige"), danach 7 s nichts, dann zurueck in
    ConfigCharacter - aber genau in diese 7 s fielen Strg+F5 (15:35:10) und
    Strg+F2 (15:35:12). Also unklar, ob der User nicht navigiert hat oder ob der
    Fokus dort tot ist. TEST, DER NOCH FEHLT: Panel oeffnen, NUR mit Nummernblock
    2/8/4/6 ca. 10 s durch die Optionen, keine Dump-Taste dazwischen, dann Log
    auf [Focus]/[ConfigProbe] mit addon=ConfigCharaChatLogGen pruefen.
    In der Kategorieliste (ConfigCharacter) wandert der Fokus sauber - dort
    kommen Tooltips wie "Steuerung"/"UI"/"Chatlog" an.

    BEFUND 3 - BEKANNTE LUECKE: `TryReadConfigFocusRow` (~2582) kennt nur
    DragDrop (Reiter), CheckBox und RadioButton. DropDownList und Slider werden
    NICHT behandelt - und davon hat gerade dieses Panel vier Stueck. Muss
    ergaenzt werden, sobald Befund 2 geklaert ist.

    WERKZEUG-HINWEIS (fuer Tests durch Dritte): Strg+F5 dumpt die fokussierten
    Fenster nach `Desktop\FFXIV_UI_Dump.txt` (UTF-8) UND ins Log; die Datei wird
    bei JEDEM Druck ueberschrieben -> sofort umbenennen. Strg+F2 sagt das aktive
    Fenster an und listet alle sichtbaren ins Log (so kam der Addon-Name).
    Nicht Debug-gated, laeuft also auch in der Release-Version.

## VORHERIGER STAND (2026-08-02, BELOHNUNGSFENSTER NUR NOCH BEIM BLAETTERN + OBJEKT-BROWSER LAEUFT ZU NICHT-ZIELBAREN OBJEKTEN, V5.66 RELEASED)

>>> DREI AENDERUNGEN, ALLE NACH V5.65, als V5.66 released. Punkt 1 und 2 sind
    in-game bestaetigt ("funktioniert"), Punkt 3 faehrt ungetestet mit:

    1. ERFAHRUNG + GIL BEIM BLAETTERN (User-Wunsch, IN-GAME BESTAETIGT "funktioniert").
       Die Waehrungszellen von JournalResult tragen nur nackte Zahlen ("400"),
       die der Fokus-Leser pauschal unterdrueckte (Zeile ~1982). Jetzt: bei
       gehaltener Richtungstaste wird die Zelle benannt statt uebersprungen -
       neue Methode DescribeFocusedRewardCurrency. Der Typ kommt aus der
       POSITION unter den Waehrungszellen (Erfahrung, dann Gil) - exakt die
       Annahme, die BuildRewardText fuer die Zusammenfassung schon nutzt und
       die in-game bestaetigt ist. Zuordnung Fokus->Zelle primaer ueber die
       ParentNode-Kette (IsDescendantOf), Betrags-Vergleich als Fallback (nur
       bei genau einem Treffer); welcher Weg griff, steht im Log
       ([Quest] Belohnungs-Waehrung N (Baum|Betrag)).
       GEERBTE GRENZE: gibt eine Quest Gil OHNE Erfahrung, waere das Label
       falsch. Saubere Quelle waeren die Quest-Sheet-Daten - offen.

    2. KEINE ZUSAMMENFASSUNG MEHR BEIM OEFFNEN (User-Entscheid 2026-08-02:
       "die belohnungen sollen nur beim durchblaettern vorgelesen werden",
       Auswahl "gar nichts mehr vorlesen"). OnQuestWindowUpdate steigt fuer
       JournalResult jetzt frueh aus; BuildRewardText laeuft nur noch fuer
       seine [Quest]-Logzeile (Diagnose), spricht aber nichts. Der frueher
       noetige _dialogOpenedAt-Guard ist entfernt: er schuetzte nur die
       Zusammenfassung vor dem auto-fokussierten "Abschliessen"-Knopf - dessen
       Ansage ist jetzt die gewollte Rueckmeldung, dass das Fenster offen ist.
       Kommentare an den Unterdrueckungsstellen entsprechend richtiggestellt
       (sie begruendeten sich mit der nicht mehr existierenden Zusammenfassung).
       IN-GAME BESTAETIGT ("ok funktioniert"): beim Oeffnen nur noch der Knopf,
       alles Weitere per Blaettern.

    3. OBJEKT-BROWSER: HINLAUFEN ZU NICHT-ZIELBAREN OBJEKTEN (User-Meldung:
       Quest-Faeden sichtbar, aber "kann sie nicht anwaehlen so dass ich
       hinlaufen kann"; NVDA sagte "Kein Ziel ausgewaehlt"). URSACHE im Code
       belegt: der Browser filtert NICHT nach IsTargetable (GetObjectsOfKinds,
       NavigationService ~907) und der Auto-Lauf holt sein Ziel ausschliesslich
       aus dem Spiel-Ziel (AutoWalkService.Toggle ~224) - bei Objekten wurde
       keine eigene Position gemerkt (anders als Quest/Wegpunkt/Angelplatz/FATE).
       FIX: neues Record ObjectDestination + SelectedObjectDestination, in
       CycleObject gesetzt, bei Kategoriewechsel und bei fremdem Hart-Ziel
       verworfen ("zuletzt gewaehlt gewinnt"). Plugin.TryResolveMarkerDestination
       loest sie auf, ABER nur wenn das Objekt NICHT das aktuelle Hart-Ziel ist -
       so bleibt fuer alles Anvisierbare der alte Pfad mit Live-Nachfuehrung
       beweglicher Ziele. Haltedistanz = AutoWalkService.StopRange (2,5 m,
       jetzt public). Gilt automatisch auch fuer Gehhilfe und Routen-Vorschau.
       NEU AUSSERDEM: /acc objprobe (#if DEBUG) - die Objekt-Sonde war auf
       Strg+F5 praktisch nicht ausloesbar, weil dort der Menue-Dump zuerst
       greift und in der freien Welt immer Fenster sichtbar sind (Plugin.cs:992).
       IN-GAME UNGETESTET (User loeste die Quest anders: es war ein NPC).
       Ursache "warum haelt das Ziel nicht" damit weiterhin UNBELEGT - die
       Sonde wuerde IsTargetable der Faeden zeigen.

## VORHERIGER STAND (2026-08-02, BELOHNUNGS-BESCHREIBUNG BEIM BLAETTERN, IN-GAME BESTAETIGT, V5.65 RELEASED)

>>> QUEST-BELOHNUNGEN: BESCHREIBUNG BEIM DURCHBLAETTERN (User-Wunsch 2026-08-02:
    "so wie bei den Skills oder den Items im Inventar, auch wenn es mehrere sind").
    Bisher: beim Blaettern kam nur Name (+Stufe/Tragbarkeit) - die Beschreibung
    gab es nur einmal in der Zusammenfassung beim Oeffnen (BuildRewardText).
    URSACHE: HandleItemDescriptionDwell (das Dwell-Muster von Inventar/Skills:
    Name sofort, Beschreibung nach 0,4 s Verweilen) hatte JournalResult per
    `IsAddonVisible("JournalResult")` pauschal ausgeschlossen.
    FIX (UIReaderService):
    - Ausschluss entfernt -> Belohnungs-Slots (JournalResult) UND das
      Auswahlgitter (JournalRewardItem) durchlaufen jetzt denselben Dwell wie
      Inventar/Skills; Item-Id kommt aus ResolveFocusedItemName (setzt
      _lastFocusedItemId auch fuer Belohnungs-Slots), Text aus
      InventoryService.ResolveItemDescription.
    - NEU `_itemDwellArmed`: die Beschreibung kommt nur, wenn der NAME auch
      wirklich gesprochen wurde. Bei jedem echten Fokuswechsel false, direkt vor
      dem SpeakInterrupt auf itemBranchActive gesetzt. Damit erzeugt das ~1 s
      Fokus-Oszillieren des Spiels (bewusst stumm, kein navKeyHeld) auch keine
      Beschreibungen - genau die Doppelansage, die der alte Pauschal-Ausschluss
      verhindern sollte, ist praeziser abgefangen. Ein Frame-Flag haette nicht
      gereicht: der Dwell feuert 0,4 s spaeter, da ist eine getippte Taste laengst
      losgelassen.
    - Latch gilt fuer ALLE Item-Slots (auch Beutel/Laden): dort wurde der Name
      ohnehin immer gesprochen, Verhalten unveraendert.
    Build 0/0. IM SPIEL BESTAETIGT (User 2026-08-02: "das mit den Belohnungen
    funktioniert") -> als V5.65 released (Version-Bump an allen 3 Stellen,
    4 Assets am GitHub-Release, Installer unveraendert wiederverwendet).
    GRENZE (bekannt, keine Regression): Lumina Item.Description ist bei RUESTUNG
    meist leer - dort bleibt es bei "Name, Stufe X, tragbar" ohne Zusatztext,
    genau wie im Inventar.

## VORHERIGER STAND (2026-08-01, RICHTUNGS-SONDE UMGEBAUT, TEST OFFEN)

>>> RICHTUNGS-TEST OHNE GEGNER moeglich gemacht (User-Wunsch: "kann man das
    anders testen?"). Die alte [NavDirProbe]-Sonde sass NUR in
    NavigationService.AnnounceDirection (/acc nav) und brauchte damit ein
    Tab-Ziel + /acc set. Zwei Umbauten, beide #if DEBUG:
    a) Sonde von AnnounceDirection nach CalculateDirection verschoben (mit
       [CallerMemberName] caller). Damit protokolliert JEDE Richtungsansage,
       die dieselbe Formel benutzt: Objekt-Browser (CycleObject), Quest-Ziele
       (CycleQuestDestination), Zielwechsel-Ansage und /acc nav. Kein Gegner,
       kein /acc set noetig.
    b) NEUE Ground-Truth-Sonde im Auto-Lauf (AutoWalkService.Update, im
       bestehenden 1-Sekunden-[NavDiag]-Block): loggt rot, dx/dz zum NAECHSTEN
       vnavmesh-Wegpunkt, angleWp+wortWp sowie angleZiel+wortZiel. Logik:
       vnavmesh steuert die Figur aktiv auf nextWp zu, also MUSS wortWp
       'geradeaus' lauten. Sagt es 'hinten', ist die Formel um 180 Grad
       verdreht - beweisbar ohne Drehtaste und ohne dass der User seine eigene
       Blickrichtung einschaetzen muss. angleZiel darf um Ecken abweichen
       (Luftlinie), das ist kein Fehler.
    Build 0/0 (Debug), nach devPlugins deployt. NICHTS committed, NICHTS
    released - reine Debug-Sonden (kompilieren in Release nicht mit).
    OFFEN (User testet): Objekt-Browser -> beliebiges Ziel (NPC/Aetheryt/
    Sammelpunkt) -> Numpad3 Auto-Lauf -> ein paar Sekunden laufen lassen.
    Danach dalamud.log auf [NavDirProbe] auswerten. Erst NACH diesem Beweis an
    der Formel selbst etwas aendern (sie war 2026-07-10 per Beacon-Hoertest
    verifiziert).

## VORHERIGER STAND (2026-08-01, v5.64 OEFFENTLICH RELEASED)

>>> RELEASE v5.64 (2026-08-01): Quest-Belohnung mit mehreren festen Items (User-
    bestaetigt "geht erstmal"). Versions-Sync 5.64 (csproj/Plugin.cs/repo.json),
    Commit 10e30ad auf main gepusht, 4 Assets released (latest.zip +
    versionierte Kopie neu; Installer-exe + installer.json unveraendert aus
    v5.63 uebernommen, SHA verifiziert MATCH). `gh release list` zeigt v5.64 =
    Latest, latest.zip-Weiterleitung liefert die neue Datei (603477 Bytes).
    Enthaelt nebenbei die #if DEBUG [NavDirProbe]-Sonde fuer Punkt 1 (kompiliert
    in Release NICHT mit rein, kein Verhaltens-Risiko).

## VORHERIGER STAND (2026-08-01, QUEST-BELOHNUNG BESTAETIGT, ZWEI PUNKTE NOCH OFFEN)

>>> Session 2026-08-01, drei Baustellen parallel angefasst. Punkt 3 (Quest-
    Belohnung) vom User als funktionierend bestaetigt ("geht erstmal"), Punkte 1+2
    testet der User bei Gelegenheit. NICHTS committed, NICHTS released - alles nur
    Debug gebaut + nach devPlugins deployt.

    3. QUEST-BELOHNUNG MIT MEHREREN FESTEN ITEMS — IN-GAME BESTAETIGT (User
       2026-08-01, "geht erstmal"). Zwei Fixes uebereinander:
       a) BuildRewardText (UIReaderService) haengt an jedes Ruestungs-Item jetzt
          zusaetzlich die Stufe/Tragbarkeit an (_gearInfo.DescribeGear), weil
          Lumina Item.Description bei Ruestung meist LEER ist.
       b) UpdateGlobalFocus hat jetzt Parameter navKeyHeld (Plugin.cs prueft
          GEHALTENEN Zustand von Pfeiltasten UND Nummernblock 2/4/6/8); Belohnungs-
          felder werden nur noch stumm geschaltet, wenn KEINE Richtungstaste
          gehalten wird - Blaettern durch Belohnungsfelder funktioniert jetzt.
       Kein detaillierter Log-Beweis fuer alle drei Einzelpunkte eingeholt, User-
       Bestaetigung war pauschal ("geht erstmal") - bei erneuten Problemen hier
       nachhaken. NOCH NICHT COMMITTED/RELEASED.

    1. RICHTUNGS-VERDACHT (User: "links ist rechts, vorne ist hinten" bei Ziel-/
       Wegpunkt-Richtung, /acc nav). NavigationService.AnnounceDirection hat jetzt
       eine #if DEBUG [NavDirProbe]-Sonde (rot/dx/dz/angle/word). Testablauf
       vereinbart: Tab-Ziel + /acc set + /acc nav, dann Numpad-Rechtsdrehung (User
       bestaetigt Standard-Steuerung, nicht Legacy), erneut /acc nav, vergleichen ob
       Ansage Richtung "geradeaus" wandert. NOCH NICHT DURCHGEFUEHRT (kein Gegner in
       der Naehe). OFFEN: Test nachholen (User testet bei Gelegenheit), danach Sonde
       wieder raus.

    2. AUTO-LAUF SCHEITERT AN SENKRECHTEM AUFSTIEG (Quest "Mitglied der
       Galgenvoegel" auf dem Schiff "Astalicia", Limsa Lominsa/Fischers Bodden).
       Log-Beweis (dalamud.log 11:22-11:55): Route springt in ~1 m seitlicher
       Strecke 9 m nach oben (Y 7,4 -> 16,4) - vermutlich Leiter/Deck-Aufgang, den
       vnavmesh/SimpleMove nicht erklimmen kann (KEIN Mod-Bug, vnavmesh-Grenze wie
       das bereits dokumentierte Netz-Bug-Muster von 2026-07-12/13). ABER: "Die
       Astalicia" als QUEST-ZIEL (Kategorie Quest-Ziele, nicht die NPC-Kategorie)
       laeuft zuverlaessig an Bord (Log: zweimal angekommen=True, keine Hoehen-
       Spruenge). Empfehlung an User: darueber an Bord, dann manuell im
       abgegrenzten Schiffsbereich nach Treppe/Leiter suchen.
       DARAUS NEUER FEATURE-WUNSCH (User): generische Erkennung von Eingaengen/
       Treppen fuer blinde Spieler. Noch NICHT recherchiert. Naechster Schritt
       verabredet: User soll Strg+F5 (DumpNearbyObjects, bereits vorhanden) direkt
       am Aufstiegspunkt der Astalicia ausloesen, damit [ObjProbe]-Log zeigt, ob
       Leiter/Tuer/Aufgang als eigenes Objekt (z.B. EventObj) existiert, auf das man
       aufbauen koennte. USER HAT DIE SONDE NOCH NICHT AUSGELOEST - im Log bisher
       keine [ObjProbe]-Zeilen von dieser Session. User testet bei Gelegenheit.

    NEBENBEI: uia_test.ps1 (unbekanntes UIAutomation-Testskript fuer den
    Installer, nicht dokumentiert) auf User-Wunsch geloescht.

## VORHERIGER STAND (2026-07-31, v5.63 OEFFENTLICH RELEASED)

>>> RELEASE v5.63 (2026-07-31): buendelt zwei in-game bestaetigte Features:
    - FATE-Kategorie im Objekt-Browser (aktive Welt-FATEs finden + Numpad3 hinlaufen).
    - Skill-Beschreibung in der Kommandoliste wieder da (Tastatur-Regression: ActionId
      jetzt aus der Action-Tooltip-Bindung statt aus dem toten AgentActionDetail).
    Versions-Sync 5.63 (csproj/Plugin.cs/repo.json), Commit 38960a9 auf main gepusht,
    4 Assets released (latest.zip + versionierte Kopie neu; Installer-exe + installer.json
    unveraendert aus v5.62 uebernommen, SHA verifiziert MATCH). API bestaetigt v5.63 = latest,
    kein Draft/Prerelease. KEINE offenen Verifikationen.


>>> BUG (User 2026-07-31): In der Kommandoliste (Addon ActionMenu, "Aktionen &
    Talente") wird die Skill-BESCHREIBUNG nicht mehr vorgelesen (Name+Stufe kommt).
    ROOT CAUSE per echtem dalamud.log belegt (nicht geraten): [ActionMenuProbe]
    zeigt bei jedem Tastaturfokus auf einem DragDrop-Slot agentId=0 agentKind=None -
    AgentActionDetail.ActionId ist unter Tastaturfokus tot (Regression v5.30->v5.62),
    keine einzige [ActionDetail]-Erfolgszeile. Name kommt weiter ueber den generischen
    Tree-Reader (Zeile hat inzwischen Text-Node), Beschreibung gibt es NUR ueber die ID.
    FIX (saubere Spiel-Quelle statt Agent): Das Spiel bindet pro Slot einen
    Action-Tooltip mit der ActionId+DetailKind (AtkComponentDragDrop.AttachTooltip,
    type=Action, args.Id = AgentActionDetail.ActionId lt. ilspycmd). Diese Bindung
    entsteht beim Bauen des Addons -> auch bei Tastaturfokus da.
    - TooltipService: erfasst jetzt zusaetzlich Action-Tooltips (node->ActionRef{Id,Kind}),
      neuer Leser TryGetActionDeep(node); gleiche Detach/Dispose-Aufraeumung wie Text.
      #if DEBUG [TooltipAction]-Log als Beweis.
    - UIReaderService: TryReadActionMenuFocusRow + HandleActionMenuDwell holen id+kind
      jetzt aus _tooltips.TryGetActionDeep statt aus AgentActionDetail. Alter
      [ActionMenuProbe]-Block entfernt. Lumina-Aufloesung (DescribeAction/Trait +
      ActionMenuDescription) unveraendert.
    - Build 0/0 (Debug), deployt. IM SPIEL NOCH UNGETESTET.
    OFFEN (In-Game verifizieren):
      1. Skill in Kommandoliste per Tastatur anwaehlen: Name+Stufe SOFORT, nach ~0,4 s
         die Beschreibung? (Kommandos UND Eigenschaften/Traits.)
      2. Log-Beweis: [TooltipAction ...] beim Oeffnen + [ActionDetail ... -> '...']
         beim Blaettern; kein Silence mehr.
      3. GeneralAction (z.B. Sprint) bleibt weiter ohne Beschreibung (bekannte Grenze).

## VORHERIGER STAND (2026-07-31, FATE-KATEGORIE - IN-GAME BESTAETIGT, RELEASE OFFEN)

>>> FATE-KATEGORIE IN-GAME BESTAETIGT (User „das mit den fates funktioniert").
    #if DEBUG [FateProbe]-Sonde entfernt (FateService nimmt jetzt nur IClientState).
    Build 0/0, deployt. NUR NOCH RELEASE offen (Versions-Bump 5.63 + voller Ablauf).
    Grenze: NUR aktuelle Zone - map-uebergreifend client-seitig unmoeglich (Server pusht
    aktive FATEs nur fuer die aktuelle Zone; auch sehende Spieler sehen fremde nicht).

## VORHERIGER STAND (2026-07-31, FATE-KATEGORIE - GEBAUT, IN-GAME UNGETESTET)

>>> FATE-KATEGORIE im Objekt-Browser (User-Wunsch 2026-07-31): FATEs sehen und
    direkt hinlaufen. FATEs stehen NIE im Aufgaben-Journal (reine Welt-Ereignisse)
    -> blind sonst nicht auffindbar.
    - NEUER FateService.cs: liest FateManager.Instance()->Fates (StdVector<Pointer<
      FateContext>>), filtert State Running(4)=aktiv + Preparing(3)=startet gleich,
      liefert FateInfo (Name/Level/Progress/Position/IsPreparing). Alles ilspycmd-
      verifiziert -> docs/game-api.md "FATE". #if DEBUG [FateProbe] loggt jedes FATE
      (Position-Verifikation), nach In-Game-Test loeschen.
    - NavigationService: neue NavCategory.Fates (null-Kinds, wie Quest-Ziele). Kategorie
      erscheint NUR wenn die Zone aktive FATEs hat (IsCategoryAvailable). Ansage beim
      Blaettern: "Name, Stufe X, Y Prozent" (bzw. "startet gleich"), + Distanz + Richtung
      + Zaehler (User-Wahl: voller Kontext inkl. Fortschritt; aktive + erscheinende).
    - HINLAUFEN geschenkt: FATE-Ziel wird als in-Zone-QuestDestination gesetzt ->
      fliesst durch den BESTEHENDEN Numpad3-Auto-Lauf (SelectedQuestDestination),
      keine neue Lauf-Logik. FateContext.Location = Weltkoordinate.
    - Bedienung: Objekt-Browser wie gehabt (Strg+Bild-auf/-ab = Kategorie bis "FATEs",
      Bild-auf/-ab = FATE waehlen, Numpad3 = hinlaufen). Keine neue Taste.
    - Dateien: FateService.cs (neu), NavigationService.cs (Kategorie+Cycle+Verfuegbar),
      AccessibilityStrings.cs (CategoryLabel/CategoryFateCount/FateEntry/NoFatesInZone,
      DE+EN), Plugin.cs (FateService konstruiert+injiziert), docs/game-api.md.
    - Build 0/0 (Debug), deployt. IM SPIEL NOCH UNGETESTET.
    OFFEN (In-Game verifizieren):
      1. Erscheint "FATEs" im Kategorie-Rad, wenn ein FATE aktiv ist? Zahl korrekt?
      2. Ansage beim Blaettern sinnvoll (Name/Stufe/Prozent/Distanz/Richtung)?
      3. Laeuft Numpad3 wirklich zum FATE (Location = Weltkoordinate, [FateProbe]-Log
         gegen Spielerposition pruefen)? Landet man IM FATE-Kreis?
      4. "startet gleich" fuer Preparing-FATEs korrekt?

## VORHERIGER STAND (2026-07-31, v5.62 OEFFENTLICH RELEASED)

>>> RELEASE v5.62 (2026-07-31): Quest-Belohnungen nennen jetzt auch die
    GEGENSTANDS-BESCHREIBUNG (User-Wunsch), Muster wie bei den Fähigkeiten: erst
    der Belohnungs-Name, dann die Beschreibung.
    - InventoryService.ResolveItemDescription(itemId) -> Item.Description (Lumina).
      UIReaderService.BuildRewardText holt via ResolveIconItem jetzt Name+ItemId,
      haengt FlattenDescription(desc) an (AccessibilityStrings.RewardItemWithDescription
      = "name. desc"). Jede Item-Belohnung ist eine eigene, mit ". " getrennte Einheit.
    - BUG beim ersten Test (Log 16:46:38): String war KORREKT (Beschreibung drin),
      aber die Ansage wurde 4 ms spaeter vom generischen Fokus-Leser abgeschnitten,
      der den auto-fokussierten "Abschliessen"-Knopf ansagte; danach Oszillations-
      Spam (Spiel wechselt Fokus alle ~1 s ueber Belohnungs-Slot/Waehrung). ZWEI FIXES:
      1. OnQuestWindowUpdate setzt beim JournalResult-Reward _dialogOpenedAt ->
         InDialogOpenGuard (1 s) unterdrueckt die Erst-Knopf-Ansage.
      2. UpdateGlobalFocus: bei sichtbarem JournalResult werden Item-Slots
         (_lastFocusedItemName gesetzt) uebersprungen -> kein "10 mal X"-Spam mehr;
         Ablehnen/Abschliessen-Knoepfe kommen weiter durch.
    - Build 0/0. IN-GAME nach den Fixes noch nicht rueckgemeldet (User bat direkt um Release).
    OFFEN (In-Game verifizieren): Belohnung inkl. Beschreibung komplett ohne Abschneiden?
    Kein "10 mal X"-Spam? Knopf-Wechsel Ablehnen/Abschliessen hoerbar?



>>> RELEASE v5.61 (2026-07-31): buendelt drei Dinge seit v5.60:
    - Faehigkeit-wieder-bereit-Ansage (unten) - vom User IN-GAME BESTAETIGT ("funktioniert").
    - Skill-Zuweisungs-Menue (Strg+Numpad0) - war schon in-game bestaetigt.
    - LOKALISIERUNGS-FIX Quest-Belohnungen: bei englischer Ausgabe standen noch zwei
      deutsche Woerter drin. Behoben in AccessibilityStrings: RewardPrefix ("Belohnung: "/
      "Reward: ") und RewardItemQuantity (DE "5 mal Trank" / EN einfach "5 Potion", ohne
      "times" - User-Wunsch). UIReaderService.BuildRewardText nutzt jetzt beide.
    - Versions-Sync 5.61 in Plugin.cs/csproj/repo.json, 4 Assets released (Installer-exe +
      installer.json unveraendert von v5.60 uebernommen, SHA verifiziert).

>>> FAEHIGKEIT-WIEDER-BEREIT (User-Wunsch 2026-07-30, IN-GAME BESTAETIGT): Ton + Ansage, sobald eine
    Fähigkeit mit echter Abklingzeit wieder einsetzbar ist (blind kein Cooldown-
    Icon sichtbar). User waehlte "automatisch alle Fähigkeiten".
    - NEUER CooldownService (jeden Frame aus OnFrameworkUpdate): Standard-Leisten
      0..9 durchgehen, Action-Slots dedupen, pro Action via ActionManager pruefen.
    - GCD AUSGESCHLOSSEN: Angriffs-Skills teilen den ~2,5-s-Global-Cooldown ->
      wuerden nonstop feuern. Schwelle GetRecastTime > 3 s trennt GCD (<=2,5 s) von
      echten oGCD-Fähigkeiten, ohne die build-spezifische GCD-Gruppen-Id zu raten.
    - Kante on->off Cooldown (IsRecastTimerActive true->false) -> Ton + Name
      ("Blutbad bereit"). Ladungs-Fähigkeiten (GetMaxCharges>1): bei neuer Ladung
      via GetCurrentCharges ("... bereit, 1 von 2 Ladungen"). Erst-Sichtung feuert
      NICHT (TryGetValue-Guard); Jobwechsel loescht Zustand (keine Falsch-Kante).
    - Ton: CueService.PlaySkillReadyTone = STEIGENDER Zweiklang (784->1047 Hz),
      klar getrennt von Wegpunkt (stetig) und Ankunft (fallend). Eigene Lautstaerke
      SkillReadyCueVolume (0.5). Config AnnounceSkillReady STANDARD AN, Toggle per
      /acc cooldowns (oder /acc cd).
    - ActionManager-Cooldown-API ilspycmd-verifiziert -> docs/game-api.md.
    - Dateien: CooldownService.cs (neu), CueService.cs (PlaySkillReadyTone),
      Configuration.cs (AnnounceSkillReady/SkillReadyCueVolume), AccessibilityStrings.cs
      (SkillReady/SkillChargeReady/Toggle + Hilfe), Plugin.cs (Konstruktion,
      Update-Aufruf, /acc cooldowns).
    - Build 0/0 (Debug), nach devPlugins deployt. IM SPIEL NOCH UNGETESTET.
    OFFEN (In-Game verifizieren):
      1. Fähigkeit auf Cooldown -> kommt beim Bereitwerden Ton + korrekter Name?
      2. KEIN GCD-Spam (Angriffsskills alle 2,5 s)? Falls doch: Schwelle/Gruppe pruefen.
      3. Ladungs-Fähigkeiten (z. B. 2 Ladungen): Ansage pro Ladung sinnvoll?
      4. Lautstaerke ok? /acc cooldowns schaltet an/aus?

## VORHERIGER STAND (2026-07-30, SKILL-MENUE - IN-GAME BESTAETIGT)

>>> SKILL-ZUWEISUNGS-MENUE (User-Wunsch 2026-07-30, IN-GAME BESTAETIGT): geführtes modales Menue
    zum Umbelegen der Aktionsleisten, ersetzt die frueheren 5 Umschalt+F7-F11-
    Einzeltasten (User fand das Jonglieren "bloed").
    - Ablauf: Strg+Numpad0 oeffnet -> mit Numpad 8/2 durch die Skills blaettern
      -> Numpad 0 waehlt -> durch die Ziel-Tasten blaettern (angesagt als echte
      Taste + was drauf liegt) -> Numpad 0 belegt -> Erfolg. Numpad-Komma =
      zurueck/abbrechen. Strg+Numpad0 nochmal = schliessen (Toggle).
    - Mechanik war KOMPLETT schon da (HotbarService: Skill-Liste, Slots,
      SetAndSaveSlot+LoadSavedHotbar verifiziert). NEU nur die modale Menue-
      Schicht + Tasten-ABFANGEN.
    - TASTEN-ABFANGEN ist der einzige neue, UNVERIFIZIERTE Teil: der Numpad ist
      im Spiel fast komplett belegt (8/2/4/6=Bewegung, 0=OK, Komma=CANCEL, 7/9=
      Tab, 5=Kamera; NUR Numpad3 frei - Keybind-Dump 2026-07-30). Das Plugin
      setzt daher solange das Menue offen ist KeyState[vk]=false fuer Numpad
      8/2/0/Komma (Plugin.HandleSkillMenuKeys), damit die Figur stillsteht.
      Ob dieses Schlucken zuverlaessig greift, MUSS in-game geprueft werden -
      das Plugin hat vorher NIE Tasten geschluckt (reines Lesen).
    - Ziel-Liste: Leiste 1 immer (Tasten 1-0,11,12) + auf Leisten 2-10 nur Slots
      mit gebundener Taste (nur die koennen feuern). Flache Liste, blaetterbar.
    - Dateien: HotbarService.cs (SkillMenuStep-Maschine, ToggleSkillMenu/
      SkillMenuBrowse/Confirm/Back, BuildTargetList, AssignSkillToSlot-Core),
      Plugin.cs (KeyNameToVK: Numpad0=0x60/NumpadKomma=0x6E; HandleSkillMenuKeys;
      Dispatch), Configuration.cs (KeySkillMenu, alte 5 raus),
      AccessibilityStrings.cs (SkillMenu*-Strings DE+EN, Hilfe-Text).
    - Build 0/0 (Debug), nach devPlugins deployt. IM SPIEL NOCH UNGETESTET.
    OFFEN (naechste Session, In-Game verifizieren):
      1. Oeffnet Strg+Numpad0 das Menue? Blaettern mit 8/2, waehlen mit 0,
         zurueck mit Komma?
      2. STEHT DIE FIGUR STILL beim Blättern (Tasten-Schlucken wirkt)?
      3. Wird der Skill wirklich belegt (Erfolgsansage + im Spiel sichtbar)?
      4. NumLock-Tuecke: sieht das Plugin bare Numpad ueberhaupt (bei NumLock an
         ja laut Numpad3-Auto-Lauf; bei aus evtl. nicht)?
    Falls Schlucken zickt: Rueckfall auf freie F-Tasten (mit User besprochen).

## VORHERIGER STAND (2026-07-28, FREIBRIEFE - IN ARBEIT, NICHT RELEASED)

>>> FREIBRIEFE (Levequests) - neue Objekt-Browser-Kategorie (User-Wunsch 2026-07-28):
    Eine Kategorie "Freibriefe" im Kategorie-Rad (Bild-auf/-ab), aus der man SOWOHL
    zum Geber-NPC (Levemete) ALS AUCH zum Ziel des angenommenen Freibriefs laufen kann.
    - VERIFIZIERT (ilspycmd, Map-Singleton): GuildLeveAssignmentMarkers (StdList) = Geber,
      LevequestMarkers (Span 16) = Ziele. Beide MarkerInfo -> AddMarkerDestinations
      komplett wiederverwendet. Leve-Ziel ist eine QuestDestination -> fliesst in
      SelectedQuestDestination -> Numpad3-Auto-Lauf/Gehhilfe/Zonen-Routing geschenkt.
    - Neues QuestMarkerRole-Enum (Quest/LeveGiver/LeveObjective) an QuestDestination.
      Rollen-Prefix bilingual via AccessibilityStrings.LeveRolePrefix. Kategorie nur
      sichtbar wenn Leve-Marker vorhanden (wie Angelplaetze).
    - IN-GAME BESTAETIGT: "Freibrief-Geber: Gildenfreibriefe, direkt neben dir" /
      "Freibrief-Ziel: Stufe 10, Reinemachen, 0,2 km, hinter rechts". Geber icon=71041,
      Ziele icon=60492, Label/Tooltip = Leve-Name, terr/map korrekt.
    - GEGNER: Freibrief-Gegner werden schon durch bestehende NpcPrefix->QuestMarkerHint
      (NamePlateIconId 71000er -> "Quest") als quest-relevant angesagt. User akzeptiert
      "Quest" -> KEINE eigene Freibrief-Gegner-Erkennung noetig. Temp-Sonde wieder entfernt.
    - DEDUP: Spiel liefert pro Leve mehrere identische Marker (gleiche Position) -> roh 11.
      GetLevequestDestinations() fasst nach (Role, Name, gerundete X/Z) zusammen; Zaehl-
      Ansage nutzt dieselbe deduplizierte Liste.
    OFFEN (naechste Session): Dedup-Zahl in-game bestaetigen (soll ~5 statt 11 sein).
    Danach releasen. Debug-Build deployt, 0/0. NOCH NICHT released.
    Dateien: QuestMarkerService.cs (Role-Enum + GetLevequestDestinations), NavigationService.cs
    (NavCategory.Levequests + Cycle/Dedup), AccessibilityStrings.cs (Labels/Rollen), keine
    neue Taste.
    NEBENHER uncommitted (SEPARATES Feature, NICHT anfassen): Klangtest ToneSynth/
    /acc soundtest (BeaconService/CueService/Plugin.cs).

## VORHERIGER STAND (2026-07-27, V5.59 OEFFENTLICH RELEASED)

>>> V5.59 RELEASE (2026-07-27): "Latest", latest.zip-Weiterleitung 592.761 B
    verifiziert (vorher 587.056). Installer unveraendert (1.1.0.0, exe+installer.json von
    v5.58 wiederverwendet, SHA 5787445B... verifiziert passend zur exe). 4 Assets dran.
    Versionen synchron csproj/repo.json/Plugin.cs auf 5.59.

>>> MOD-ANSAGEN KOMPLETT ZWEISPRACHIG (User-Wunsch 2026-07-27, in v5.59 released):
    Der Client laeuft eh auf Englisch - es mussten nur die MOD-Ausgaben uebersetzt
    werden. Gefunden & behoben: ~120 hartkodierte deutsche Fragmente in 13 Dateien
    liefen an Loc/AccessibilityStrings vorbei -> EN-Nutzer hoerten dort trotz
    /acc lang en IMMER Deutsch. Alle jetzt via AccessibilityStrings (IsGerman ? de : en).
    Migriert: AutoWalkService (Auto-Lauf/Folgen/Wegenetz, ~30), Plugin.cs (Koordinaten-
    Lauf, Versionsansage, kompletter /acc help-Text, Himmelsrichtung, Quest/Marker,
    Bestiarium), HotbarService (Aktionsleiste/Skill-Browser inkl. SlotLabel/
    TargetBarSummary/SkillBrowseEntry), NavigationService (Gehhilfe), EmoteService,
    DalamudPluginsService (inkl. Describe/BuildOverview-Zustandswoerter), Fishing- +
    GatheringService (CompassDirection -> AccessibilityStrings.CompassAdjectives,
    SpotListLine), InventoryService, MessageHistoryService (ChatCategoryName),
    BeaconService, ChatReaderService (Kanal-Praefixe ChatPrefix/OwnChatPrefix +
    von/an-Konnektoren - versteckte Luecke!), PlacesService (gesprochene NAMEN
    FlagName/TransitionToName/AetheryteFallbackName; TypeLabel bleibt dt. Identitaet),
    UIReader-Reste (Kategorie/Benachrichtigung/Countdown).
    FIX: veraltete Ansage "Erst mit N ein Objekt waehlen" -> "Bild ab"/"Page Down"
    (Objekt-Browser liegt seit V5.31 auf Bild-auf/-ab, nicht mehr N).
    BEWUSST deutsch: Debug-Sonden (#if DEBUG) + /acc keys-Desktop-Dumpdatei (Diagnose).
    Build 0/0 (Debug + Release). IM SPIEL NOCH UNGETESTET (nur Kompilierung verifiziert).
    OFFEN (Teil 2, eigenes Projekt): Client-Match-Strings (Buttons/Journal-Header)
    robust machen - betrifft v.a. UIReaderService, fuer EN-Client noch relevant.

## VORHERIGER STAND (2026-07-27, V5.58 OEFFENTLICH RELEASED)

>>> V5.58 RELEASE (2026-07-27): "Latest", latest.zip-Weiterleitung 587.056 B verifiziert
    (vorher 579.413). Installer unveraendert (1.1.0.0, exe+installer.json von v5.57
    wiederverwendet, SHA 5787445B... verifiziert). 4 Assets dran.

>>> SYSTEMKONFIGURATION BARRIEREFREI (User-Wunsch 2026-07-27, IN-GAME BESTAETIGT, in v5.58 released):
    Runde Feinschliff am Reiter "Sound" + Config allgemein (UIReaderService/AccessibilityStrings):
    - Lautstaerke-Regler: KURZFORM "Hauptlautstaerke, 100 %" statt langem "Regler, von 0 bis 100"
      (Langform wurde beim schnellen Navigieren abgeschnitten). Beim Verstellen nur "99 %".
      0..100-Slider werden generell als Prozent gelesen (SliderPercent).
    - Doppel-Ansage gefixt: der generische Fokus-Leser sprach die nackte Slider-Zahl ("100")
      ~14 ms nach der Config-Ansage und wuergte das Label ab -> nackte Zahlen in ConfigSystem
      unterdrueckt (Muster wie JournalResult).
    - Schalter: "Label, Schalter, an/aus"; deaktivierte zusaetzlich "ausgegraut" aus
      NodeFlags.Enabled (0x20, ilspycmd-verifiziert; aktiv F=0x2033 vs. ausgegraut F=0x2013).
    - Barrierefreiheit-Reiter (Reiter 8): Seite schaltet beim Navigieren um und wird gelesen
      (Enter schluckt das Spiel, IKeyState sieht es nicht - fuer den Wechsel nicht noetig).
    - Strg+F5-Menue-Dump wird nicht mehr von der Objekt-Sonde ueberschrieben (DumpFocusedAddon
      gibt bool zurueck; Objekt-Sonde nur noch ohne offenes Fenster).
    Versionen synchron csproj/repo.json/Plugin.cs auf 5.58.
    OFFEN (kosmetisch): Barrierefreiheit-Seite meldet Ueberschrift "Anzeigeeinstellungen".
    MITGEBUENDELT ungetestet: Triple Triad + Gathering (aus frueheren Sessions, im Arbeitsbaum).

>>> V5.57 RELEASE (2026-07-26): "Latest", latest.zip-Weiterleitung 579.413 B verifiziert.
    Installer unveraendert (1.1.0.0, exe+installer.json von v5.56 wiederverwendet, SHA ok).

## VORHERIGER STAND (2026-07-26, V5.57: Ziel folgen - IN-GAME BESTAETIGT)

>>> ZIEL FOLGEN (User-Wunsch 2026-07-26, IN-GAME BESTAETIGT "folgt dem spieler"):
    Neue Taste + (BARE VK_OEM_PLUS 0xBB, NICHT Numpad; im Keybind-Dump frei) folgt dem
    anvisierten Ziel fortlaufend. WICHTIG: FFXIV hat KEIN plugin-aufrufbares natives
    Follow (ilspycmd-verifiziert, siehe game-api.md "Spieler folgen") - daher selbst
    ueber vnavmesh gebaut: AutoWalkService.FollowUpdate loest PathfindAndMoveCloseTo
    fortlaufend auf die aktuelle Zielposition neu aus (Abstand 3 m, Re-Path ab 1,5 m
    Drift/Pfad-Ende, throttled 0,4 s). Haelt an wenn Ziel steht; stoppt bei Ziel-weg/
    Zonenwechsel; schliesst sich mit Auto-Lauf/Gehhilfe gegenseitig aus. Config
    KeyFollowTarget="+", IsFollowing unterdrueckt Ziel-Ansagen wie IsActive.
    Versionen synchron auf 5.57.

## VORHERIGER STAND (2026-07-26, V5.56 OEFFENTLICH RELEASED)

>>> V5.56 RELEASE (2026-07-26): Numpad3-Auto-Lauf folgt dem Tab/F11-Ziel statt einem
    alten Browser-Marker (siehe Kampf-Fix unten). Versionen synchron csproj/repo.json/
    Plugin.cs auf 5.56. Installer unveraendert (1.1.0.0, exe+installer.json von v5.55
    wiederverwendet, SHA verifiziert). v5.56 = "Latest", latest.zip-Weiterleitung liefert
    578.240 Bytes (verifiziert). Buendelt weiterhin die ungetesteten Teile aus v5.55 mit
    (Charakterkonfiguration V5.54; AoE-Warnton opt-in, Standard AUS).

>>> KAMPF-FIX (User-Wunsch 2026-07-26, IN-GAME BESTAETIGT "laeuft zum gegner", in v5.56 released):
    Numpad3 (Auto-Lauf) lief bisher NUR zum Spiel-Ziel, wenn KEINE Browser-Marker-
    auswahl aktiv war. Sobald man vorher im Objekt-Browser eine Quest/Wegpunkt/
    Aetheryt/Angelplatz gewaehlt hatte, blieb diese Marker-Auswahl gespeichert und
    Numpad3 lief weiter zum alten Marker statt zum mit Tab/F11 anvisierten Gegner
    (TryResolveMarkerDestination prueft Marker VOR dem Spiel-Ziel).
    FIX (NavigationService.Update, Debug gebaut, 0 Warnungen, nach devPlugins deployt):
    "Zuletzt gewaehlt gewinnt" - ein NEU anvisiertes HART-Ziel (Tab/F1-F12/F/Klick)
    verwirft eine noch aktive Browser-Markerauswahl (SelectedQuest/PlaceDestination
    = null). Nur Hart-Ziel, nicht SoftTarget (vorbeilaufende NPCs duerfen die Auswahl
    nicht abraeumen); Browser-eigene Gegnerauswahl via _ownSelectionId ausgenommen.
    Neues Feld _lastSeenHardTargetId. Log-Zeile "[Nav] Spiel-Ziel ... anvisiert".
    NAECHSTER SCHRITT: Release schneiden (wird V5.56) - noch NICHT versioniert/released.

## VORHERIGER STAND (2026-07-26, V5.55 OEFFENTLICH RELEASED)

>>> V5.55 RELEASE (2026-07-26): buendelt alles seit 5.52 - Reittier-Verzeichnis
    (V5.53, bestaetigt), Charakterkonfiguration (V5.54, ungetestet), Kampf-Sprechblasen
    _BattleTalk (BESTAETIGT) und AoE-Warnton (OPT-IN, STANDARD AUS - in-game noch nicht
    bestaetigt, User-Entscheid). Neue Taste KeyToggleAoeWarning = Strg+Umschalt+F3
    schaltet den AoE-Ton an/aus (Ansage "Flaechenwarnung an/aus", lokalisiert).
    Versionsdrift geheilt: csproj/repo.json/Plugin.cs alle auf 5.55. Installer
    unveraendert (1.1.0, exe+installer.json von v5.52 wiederverwendet, SHA verifiziert).
    NAECHSTER SCHRITT: AoE-Warnton in-game bestaetigen (Kegel vorn/hinten, Linien-
    Breite, Kreis-Zentrum), dann spaeterer Release dreht den Standard auf AN.

## VORHERIGER STAND (2026-07-26, AoE-WARNTON: Geometrie je Form + Dauerton)

>>> AoE-AUSWEICHEN (User-Wunsch, Testfeld = Kampfuebungsplatz/Hall of the Novice).
    User-Spezifikation praezisiert: DAUERTON solange man in der Flaeche steht, startet
    mit dem Cast, verstummt beim Verlassen der Flaeche ODER Cast-Ende (kein Sprach-
    Countdown noetig).
    GEBAUT (Debug, 0 Warnungen, nach devPlugins deployt):
    - AoeWarningService.cs (neu): eigener MONO-Pulston (660 Hz, Puls 140/110 ms), klar
      unterscheidbar vom Stereo-Navi-Beacon (880 Hz). SetActive(bool) idempotent,
      Audio-Device lazy + gegated-still (klickfrei). Config AnnounceAoeWarning (an),
      AoeWarnVolume (0.5). In Plugin.cs verdrahtet + disposed.
    - CombatService.UpdateAoeWarning (jeden Frame, unabhaengig vom InCombat-Flag):
      iteriert ObjectTable, fuer jeden castenden BattleNpc mit EffectRange>0 -> Kreis
      um den Caster, Radius = EffectRange, HORIZONTALE (XZ) Distanz. Spieler drin ->
      Ton an, sonst aus.
    GEOMETRIE-MODELL V1 (WORKAROUND, in Code+Config markiert): Kreis um Caster,
    r=EffectRange. Echte Telegraph-Form/-Position aus der Omen/VFX noch NICHT gelesen
    (harter Recherche-Pfad). Gilt sauber nur fuer caster-zentrierte Kreise; Kegel/
    Linien/boden-platzierte AoEs kommen als naechstes.
    VERIFIZIERT bisher (Log 15:34): Kahlrodung (Marodeur-Lehrer) castId=5780,
    CastType=3, EffectRange=6, Omen=4, atMe=False (Trainings-AoE zielt NICHT auf
    Spieler -> Geometrie statt "Cast auf mich" ist richtig).
    SONDE erweitert: [AoeProbe] loggt jetzt zusaetzlich OmenPath (Grafik-Dateiname
    verraet echte Form: gl_fan*=Kegel, gl_circle*=Kreis, gl_line*=Linie).
    ITERATION 2 (Log 15:46-15:48 ausgewertet): OmenPath belegte die echten Formen ->
    V1-Kreismodell war fuer ALLE drei Formen falsch. CASTTYPE->FORM verifiziert:
    2=Kreis (Feura r5), 3=Kegel (Kahlrodung 'gl_fan090', 6=Laenge), 4=Linie (Spalten
    'general02', 30=LAENGE nicht Radius, Breite=XAxisModifier). Linie-als-Kreis war die
    Hauptursache fuer die zufaellig wirkenden Piepser (30m-Kreis = halbe Arena).
    User-Feedback: Ton soll DURCHGEHEND sein, nicht "ein paar mal piepen".
    GEBAUT (Iteration 2, 0 Warnungen, deployt):
    - CombatService.IsPlayerInAoe: echte Geometrie je CastType (Kreis am Ziel / Kegel
      mit fan-Winkel aus OmenPath / Linie entlang Blickrichtung mit Laenge+Breite);
      unbekannte Typen -> konservativer Caster-Kreis. Alles XZ-horizontal.
    - AoeAlarmSampleProvider: DURCHGEHENDER Ton (geglaetteter Gain, ~8ms Ramp
      klickfrei) statt Puls.
    OFFEN/ANNAHMEN zu verifizieren: (a) Linien-Halbbreite = XAxisModifier? (b) Kreis-
    Zentrum bei boden-platzierten AoEs (nur in VFX, noch nicht loesbar). (c) Ton endet
    mit Cast-Ende = Telegraph verschwindet (game-korrekt, User-Erwartung klaeren).
    TEST OFFEN: Kampfuebungsplatz - Kegel (Marodeur) von vorn = Ton, von hinten =
    still; Linie (Spalten) nur wenn wirklich in der Bahn; Kreis (Thaumaturg). Ton
    durchgehend solange drin. Version NICHT gebumpt (kein Release).

>>> _BattleTalk (Kampf-Sprechblase) VORLESEN (2026-07-26): User meldete, in der
    Kampfarena kommt Text, der nicht vorgelesen wird. DEBUG-Sonde ArenaTextProbe
    (UIReaderService, #if DEBUG, loggt sichtbaren Text unbedienter Anweisungs-Addons)
    -> Log 16:26 belegte: Quelle = _BattleTalk (Waffenmeister-Ansagen "Erledigt
    zuerst den Thaumaturgie-Lehrer", "Das ist der falsche Gegner!"). Struktur:
    Sprecher=id4, Text=id6. _BattleTalk stand in den Ausschluss-Listen, hatte aber
    KEINEN Leser. FIX: _BattleTalk an OnTalkUpdate registriert; Sprecher-Node-Id jetzt
    addon-abhaengig (Talk=2, _BattleTalk=4).
    >>> IN-GAME BESTAETIGT (Log 16:34): [Speak] "Waffenmeister: Ja! Du hast die Uebung
    bestanden.", "Waffenmeister: Denk daran ...", auch zweiter Sprecher
    "Gilden-Gladiator: So ist es richtig!". Sprecher-zuerst + Dedup greifen.
    ArenaTextProbe wieder ENTFERNT (Zweck erfuellt). Gilt generell fuer alle
    Instanz-/Boss-Sprechblasen, nicht nur die Arena.

## VORHERIGER STAND (2026-07-26, V5.54 GEBAUT/DEBUG-DEPLOYT, IN-GAME UNGETESTET - Charakterkonfiguration lesbar)

>>> V5.54 (2026-07-26): CHARAKTERKONFIGURATION (ConfigCharacter + ConfigChara*-
    Unter-Addons) barrierefrei. Struktur per AUTO-Sonde ConfigProbeTick verifiziert
    (Log 2026-07-26 11:06): 6 Kategorie-Reiter = DragDrop-Icons in ConfigCharacter,
    Namen aus TOOLTIP (Steuerung/Gegenstaende und Inventar/UI/Namensanzeige/
    Kommandomenue/Chatlog). Einstellungen liegen in Unter-Addons (ConfigCharaHotbar
    Display/XHB/XHBCustom/Common ...) als CheckBox/RadioButton mit Text-Label +
    IsChecked. Luecke: Fokus-Leser sagte nur das Label, nicht den Zustand.
    Umgesetzt: neuer TryReadConfigFocusRow (erster Zweig in UpdateGlobalFocus,
    auf Config*-Addons beschraenkt) -> (a) Icon-Reiter per Tooltip benannt (statt
    „Leer"), (b) CheckBox -> „Label, an/aus", RadioButton -> „Label, ausgewaehlt"
    (nur wenn checked). Strings StateOn/StateOff/RadioSelected (de/en).
    SONDEN-UMBAU (User-Wunsch): Debug-Sonden laufen jetzt AUTOMATISCH per #if DEBUG
    (kein /acc-Toggle mehr); ConfigProbeTick loggt auto in Config*-Addons, komplett
    aus Release rauskompiliert. Alte Mount-Sonde (/acc mountprobe) GELOESCHT.
    Plugin.cs auf 5.54; csproj/repo.json noch 5.52 (Release-Sync spaeter).
    TEST OFFEN: Charakterkonfig oeffnen, Reiter fokussieren -> „Steuerung" usw.;
    Einstellung fokussieren -> „Reaktivierungszeiten anzeigen, an". OFFEN/optional:
    Reiter-Ansage bei Schulter-Tasten-Wechsel OHNE Fokuswechsel (bisher nur via
    Fokus/Tooltip); Eingabefelder in Config (falls vorhanden) benennen.

## VORHERIGER STAND (2026-07-26, V5.53 GEBAUT/DEBUG-DEPLOYT - Reittier-Verzeichnis lesbar, IN-GAME BESTAETIGT)

>>> V5.53 (2026-07-26): REITTIER-VERZEICHNIS (MountNoteBook) barrierefrei. Root-Cause
    war: die Kacheln sind icon-only DragDrop-Slots ohne Namensknoten; der generische
    Fokus-Leser behandelte sie als Item-Slots -> leer=„Leer", gefuellt=stumm (Item-
    Aufloesung scheitert bei Mount-Icon).
    Verifiziert per NEUER Sonde /acc mountprobe (Ein-Tasten-Raster-Scan, keine
    Navigation noetig): liest je Kachel FindSlotIcon->IconId und loest ueber das
    Mount-Sheet (Icon-Feld -> Singular) auf. Live-Log 2026-07-26:
    „tile node=28 icon=4001 -> 'Gesellschafts-Chocobo'", 31 leere Kacheln (icon=0).
    ERKENNTNIS: nur BESESSENE Reittiere haben ein Icon; Rest = icon=0/leer.
    Verworfen: AgentMountNoteBook.CurrentSelection->Id (blieb 0 bei Hover, nur bei
    Bestaetigung gesetzt); Info-Panel id93/id86 (nur Hilfetext, nie Name).
    Umgesetzt (User-Wahl „echtes Spielfenster lesen"): neuer Zweig
    TryReadMountNoteBookFocusRow in UpdateGlobalFocus -> gefuellte Kachel wird per
    Icon->Mount benannt, leere bleibt „Leer". Icon-Map MountByIcon() gecacht (349).
    NACHTRAG (User-Wunsch): (a) Ansichts-Reiter-Ansage bei Wechsel via
    OnMountNoteBookUpdate (PostUpdate) aus agent->ViewType (Favorites=1/Normal=2/
    Search=3) -> „Favoriten/Alle Reittiere/Suche"; Seitenwechsel aus
    CurrentSelection->Page -> „Seite X". (b) Suchfeld (TextInput) wird im Fokus als
    „Reittier suchen, Eingabefeld" angesagt statt „0/40". Strings in
    AccessibilityStrings (de/en). Plugin.cs auf 5.53. csproj/repo.json noch 5.52.
    TEST OFFEN: Reittier-Verzeichnis oeffnen, navigieren -> gefuellte Kachel sagt
    „Gesellschafts-Chocobo", leere „Leer"; Reiter wechseln -> Ansicht/Seite; Fokus
    auf Suchfeld -> „Reittier suchen, Eingabefeld". /acc mountprobe bleibt als
    Debug-Sonde drin. OFFEN/optional: Suchfeld-Tipp-Echo (Braille), Checkbox-Status.

## VORHERIGER STAND (2026-07-26, V5.52 OEFFENTLICH RELEASED - XP-Gewinn- + Beute-Ansage, KOMPLETT IN-GAME BESTAETIGT)

>>> V5.52 (2026-07-26): XP- und Loot-Ansage + neuer Nachlese-Kanal "Beute". Auf
    User-Wunsch (jeder XP-Gewinn sofort, Loot live + Nachlese). Released, KOMPLETT
    in-game bestaetigt (2026-07-26: User meldet Looten UND Beute-Kanal funktionieren).
    (1) XP-Gewinn live: CombatService.TrackXpGain liest GetCurrentClassJobExp jeden
        Frame, sagt Delta an ("X Erfahrung", nicht-unterbrechend) + schreibt in den
        Beute-Kanal. Baseline pro Job + Level-Up-Ruecksprung stumm nachgezogen.
        >>> IN-GAME BESTAETIGT (Live-Log 2026-07-25: +94/+132/+542, Level-Up sauber).
    (2) Loot live: XivChatType.LootNotice (62), aus Live-[Chat]-Log VERIFIZIERT -
        "Du hast X erhalten" (Gegner-Drops, Gil, GC-Taler, Kristalle). ShouldRead ->
        Config AnnounceLoot, Nachlese-Kanal "Beute". >>> IN-GAME BESTAETIGT (2026-07-26).
    (3) Neue Nachlese-Kategorie Category.Loot = "Beute" (MessageHistoryService), XP +
        Loot gemeinsam. Erreichbar: Alt+Bild-ab bis "Beute", Umschalt+Bild-auf/-ab.
        >>> IN-GAME BESTAETIGT (2026-07-26: Beute-Kanal funktioniert).
    Enthaelt ausserdem den bereits verdrahteten Fisch-Sonden-Code (inaktiv, /acc fish;
    Feature ruht, braucht sehende Hilfe). repo.json/csproj/Plugin.cs auf 5.52 synchron.
    Installer unveraendert (1.1.0, exe+installer.json vom v5.51-Release wiederverwendet,
    SHA verifiziert). uia_test.ps1 bleibt untracked (temp UIA-Helfer, nicht im Release).
    OFFEN: Dungeon-Wuerfel-Beute (LootRoll?) noch nicht verifiziert; XP/Loot bei Spam
    ggf. buendeln (User-Rueckmeldung abwarten).

## VORHERIGER STAND (2026-07-25, V5.51 OEFFENTLICH RELEASED - Gegner-Cast-Filter + Chat-Tasten, NEUE FEATURES IN-GAME UNGETESTET)

>>> V5.51 (2026-07-25): Release geschnitten, um eine VERSIONSDRIFT zu heilen. Problem:
    Plugin.cs war intern schon auf 5.50/5.51 gebumpt, aber csproj UND repo.json hingen
    beide noch auf 5.47 -> seit v5.47 kam KEIN Release mehr raus, Nutzer bekamen weiter
    5.47. Fix: alle drei Versionsstellen (Plugin.cs / csproj / repo.json) auf 5.51
    synchronisiert und v5.51 released. MERKE: Plugin.cs wird in Feature-Commits gebumpt,
    csproj/repo.json nur beim Release-Schneiden -> bei mehreren Feature-Bumps zwischen
    zwei Releases driften die Stellen; vor jedem Release alle drei abgleichen.
    Inhalt (alles seit v5.47):
    (1) V5.51 (uncommitted -> jetzt released): Gegner-Cast-Ansage feuert nur noch, wenn
        der Zauber auf DEN SPIELER gerichtet ist (target.IsCasting && CastTargetObjectId
        == playerId). Casts auf andere Ziele = Laerm, jetzt gefiltert. Edge-State heisst
        jetzt _targetWasCastingAtMe. (CombatService.cs). >>> IN-GAME NOCH NICHT GETESTET.
    (2) V5.48-5.50 (Commit fe5e7f2): Chat-Nachlese-/Kategorie-Tasten auf den Bild-Cluster
        verlegt (Alt+Bild-auf/-ab), weg von Komma/Punkt (= Reittier-Menue MENU_MOUNT).
    Installer unveraendert (1.1.0, exe+installer.json vom v5.47-Release wiederverwendet,
    SHA verifiziert). uia_test.ps1 bleibt untracked (temp UIA-Helfer, nicht im Release).

## VORHERIGER STAND (2026-07-25, V5.47 OEFFENTLICH RELEASED - Grand-Company-Shop lesbar, IN-GAME BESTAETIGT)

>>> V5.47 (2026-07-25): Grand-Company-Staatstaler-Shop (GrandCompanyExchange) komplett
    barrierefrei, in-game bestaetigt. (1) Item-Zeilen „Name, X Staatstaler, Besitz Y"
    (dedizierter ReadGrandCompanyRow statt kryptischem „0, 1.060, ..."; Doppel weg).
    (2) Kategorie-Reiter „Kategorie Waffen/Ruestung/..." (OnGrandCompanyUpdate, aktiver
    Reiter via RadioButton.IsChecked/Flags-Bit 18). Details in den NEU-Bloecken unten +
    docs/game-api.md. repo.json/csproj/Plugin.cs auf 5.47. Installer unveraendert (1.1.0).

## VORHERIGER STAND (2026-07-25, V5.46 OEFFENTLICH RELEASED - Chat-Eingabe-Fixes, IN-GAME BESTAETIGT)

>>> V5.46 (2026-07-25): Chat-Eingabe-Verbesserungen, vom User in-game bestaetigt und
    released. (1) Mod-Hotkeys stehen still, solange ein Spiel-Textfeld fokussiert ist
    (RaptureAtkModule.IsTextInputActive-Gate in IsJustPressed). (2) Beim Tippen spiegelt
    die Braillezeile still die aktuelle Eingabezeile (neuer Tolk_Braille-Pfad). Details
    in den NEU-Bloecken unten. repo.json/csproj/Plugin.cs auf 5.46. Installer unveraendert
    (1.1.0, exe+installer.json vom v5.45-Release wiederverwendet, SHA passt).

>>> NEU (2026-07-25, GEBAUT 0/0, DEBUG-DEPLOYT, UNGETESTET, -> V5.47): GRAND-COMPANY-
    SHOP LESBAR. Menue „GrandCompanyExchange" (Staatstaler gegen Gegenstaende).
    Verifiziert per Dump + dalamud.log: die generische Listen-Navigation griff schon,
    las aber kryptisch „0, 1.060, Legionaers-Schwert" (Spaltenreihenfolge, ohne Label,
    teils doppelt bei Sichtbarkeits-Flackern der Spalten). Fix: dedizierter Zeilen-Leser
    ReadGrandCompanyRow (Node-IDs aus dem Renderer: id4=Name, id7=Preis, id10=Besitz)
    -> „Name, X Staatstaler, Besitz Y". Eingehaengt im name-switch von TrackListIndices
    (neben ConfigKeybind). Stabiler Text ⇒ idx|text-Dedup beseitigt das Doppel.
    AccessibilityStrings.GrandCompanyRow (de/en). Node-Struktur dokumentiert in
    docs/game-api.md. OFFEN/optional: Rang + Staatstaler-Guthaben beim Oeffnen ansagen
    (Root-Nodes id6/id8; PostSetup-Timing noch unverifiziert - bewusst nicht gebaut).
    TEST: Shop oeffnen, Liste durchblaettern -> „Legionaers-Schwert, 1.060 Staatstaler,
    Besitz 0" statt „0, 1.060, ..."; kein Doppel mehr.
    NACHTRAG (2026-07-25): Kategorie-REITER (Waffen/Ruestung/Militaerbedarf/Materialien/
    Besondere Artikel) waren stumm. Fix: OnGrandCompanyUpdate (PostUpdate) sagt bei
    Reiterwechsel „Kategorie X". Aktiver Reiter = RadioButton mit IsChecked (Flags-Bit 18,
    ilspycmd-verifiziert); Label = Text-Kind id=2; Rang-Icons Comp(1016) per leerem Label
    gefiltert. AccessibilityStrings.CategoryLabel (de/en). Registrierung analog ArmouryBoard.
    TEST: Reiter wechseln -> „Kategorie Ruestung" o.ae.

## VORHERIGER STAND (2026-07-25, V5.45 OEFFENTLICH RELEASED - aber IM SPIEL noch UNGETESTET)

>>> RELEASE-INFO: v5.45 ist auf GitHub veroeffentlicht (Latest) mit 4 Assets:
    latest.zip, FF14Accessibility-v5.45.0.zip, FF14AccessibilityInstaller.exe (1.1.0,
    Quellcode unveraendert, SHA256 in installer.json). repo.json + csproj + Plugin.cs
    PluginVersion stehen auf 5.45. Nutzer bekommen das Update ueber Dalamud.
    ACHTUNG: Der Code ist trotz Release IM SPIEL weiterhin UNGETESTET - Test-Schritte
    unten abarbeiten und bei Fehlern Patch-Release nachschieben.

>>> NEU (2026-07-25, GEBAUT 0/0, UNGETESTET): CHAT-EINGABE-GATE. Problem (User):
    beim Tippen im Spiel-Chat feuerten Mod-Tasten (N, Numpad, Pfeile, Return...) mit.
    Fix: IsJustPressed() gibt false zurueck, solange ein Spiel-Textfeld fokussiert ist.
    Erkannt ueber die spieleigene Funktion RaptureAtkModule.IsTextInputActive()
    (ilspycmd-verifiziert in FFXIVClientStructs.dll: RaptureAtkModule ->
    AtkModule.IsTextInputActive, die native Routing-Funktion des Spiels). Zustand wird
    1x pro Frame in _textInputActive gecacht (Plugin.cs, OnFrameworkUpdate). Die
    Per-Frame-Update()-Aufrufe (Gehhilfe/Beacon/Heading/Fokus-Reader) laufen weiter,
    da sie NICHT durch IsJustPressed gehen. Debug-Log [TextInput] active=... loggt bei
    jedem Wechsel -> im Spiel pruefen, dass es genau beim Chat oeffnen/schliessen flippt.
    TEST: Chat oeffnen (Enter), "n" tippen -> schreibt "n", kein Objekt-Cycle; Pfeile
    bewegen Cursor; nach Chat-Schliessen wieder normale Mod-Tasten. Gamepad-D-Pad
    (SelectYesno) bewusst NICHT gegatet (Tastatur-Nutzer, laeuft nicht ueber IsJustPressed).

>>> NEU (2026-07-25, GEBAUT 0/0, DEBUG-DEPLOYT, UNGETESTET): CHAT-EINGABE AUF BRAILLE.
    User-Wunsch: beim Tippen im Chat die aktuelle Zeile auf der Braillezeile nachlesen
    koennen (nur sehen, KEIN Sprach-Echo - das gesprochene Zeichen-Echo hatte User
    2026-07-22 abgeschaltet, EchoTypedCharacters=false, bleibt aus).
    Loesung: reiner Braille-Ausgang ergaenzt. Tolk_Braille in Tolk.dll ist exportiert
    (verifiziert 2026-07-25 via Symbol-Scan; auch Tolk_HasBraille/HasSpeech vorhanden).
    - TolkNative.Tolk_Braille deklariert (Cdecl, LPWStr).
    - TolkService.Braille(text): nur Braille, keine Sprache, keine History/Dedup; leere
      Zeile -> " " damit Tolk das Display leert.
    - UIReaderService.OnChatLogUpdate: bei jeder Textaenderung _tolk.Braille(text) (volle
      aktuelle Zeile). Laeuft unter EchoChatInput (true); unabhaengig von
      EchoTypedCharacters (das steuert nur das gesprochene Echo via SpeakTextEchoDiff).
    Die vorhandene Infrastruktur (V4.90 OnChatLogUpdate, AddonChatLog.TextInput->IsActive,
    EvaluatedString) war schon da - es fehlte nur der stille Braille-Pfad.
    TEST: Chat oeffnen (Enter), tippen -> Braillezeile zeigt live die Zeile; loeschen ->
    aktualisiert; leer -> Display leer; kein gesprochenes Zeichen-Echo.

## FRUEHERER STAND (2026-07-24, V5.45: Englische Ausgabe - Build repariert + Navigation + UIReaderService + Equipment/Combat uebersetzt - GEBAUT, UNGETESTET)

>>> FUER NEUEN CHAT ("weiter"): Lokalisierung Teil 1 (Mod-Ansagen ins Englische, /acc
    lang de|en|auto) laeuft. Naechste Gruppe nach Spielrelevanz: Plugin.cs-Rest +
    PlacesService (schliesst die in der Navigation offen gelassene TypeLabel-Entkopplung).
    Danach Hotbar, AutoWalk, Inventory, Emote, Bank, Gathering, DalamudPluginsService.
    ALLES ist GEBAUT (0/0) aber IM SPIEL UNGETESTET - Test-Schritte siehe unten.
    Build: dotnet build H:\ff14\FF14Accessibility\FF14Accessibility.csproj -c Release
    (kein scripts/-Buildscript trotz CLAUDE.md-Template). Deploy macht der User selbst.
    Details/Architektur: Memory localization_english.md.
    Teil 2 (Match-Strings client-sprachrobust fuer EN-Client) = eigenes Projekt, offen.

Ziel: Mod komplett auf Englisch spielbar machen (Ansagen umschaltbar via /acc lang).
Zielgruppe geklaert: echte EN-Spieler mit ENGLISCHEM Client.
Ansatz nach Spielrelevanz, saubere Uebersetzung direkt durch Claude, nach jeder Gruppe berichten.

FERTIG (Teil 1, Mod-Ansagen): Titel/Menue/Config/Charaktererstellung (Erst-Batch) +
Navigations-Gruppe (NavigationService, RouteService, HeadingService) + UIReaderService
KOMPLETT (7465 Zeilen, 2 Etappen) + Equipment/Combat (EquipmentService, GearInfoService,
CombatService; VitalsService brauchte nichts). Alle Builds 0/0.
Zentrale Tabelle: FF14Accessibility/Services/AccessibilityStrings.cs (Muster IsGerman?de:en).

KRITISCHER FUND: Das Projekt kompilierte vorher GAR NICHT. Die /acc lang-Verkabelung
rief SetLanguage() auf (Plugin.cs:213), aber diese Methode wurde nie geschrieben ->
CS0103. Der letzte Stand (V5.43/V5.44) war also nicht baubar; die "gebaut, ungetestet"-
Features wurden vermutlich aus aelterem Code deployt. JETZT behoben.

Was in V5.45 fertig:
1. SetLanguage() implementiert (Plugin.cs): /acc lang de|en|auto schaltet wirklich um,
   speichert in Config (ueberlebt Neustart), bestaetigt gesprochen in der neuen Sprache.
   Loc.cs + Configuration.Language + Loc.Mode-Wiring existierten schon.
2. NAVIGATIONS-GRUPPE komplett uebersetzt (NavigationService.cs, RouteService.cs,
   AccessibilityStrings.cs):
   - Himmelsrichtungen (8 Sektoren), relative Richtung ("leicht links" -> "slightly left"),
     Distanz ("12 Meter" -> "12 meters"). Betrifft auch neuen HeadingService (V5.43).
   - Objekt-Browser-Kategorien: sprachunabhaengig per enum NavCategory entkoppelt (Label
     war vorher Vergleichswert -> waere bei Sprachwechsel gebrochen). Alle Kategorie-Ansagen.
   - Ziel-/Objekt-/Quest-/Wegpunkt-Ansagen, Gehhilfe/Routen-Meldungen, Routen-Vorschau
     (DescribeRoute), DescribeKind, DescribeQuestMarker, DescribeGatheringPoint.
   - Alle neuen Strings zentral in AccessibilityStrings (IsGerman ? de : en).
   BEWUSST NOCH DEUTSCH (gehoert zu spaeteren Gruppen, dokumentiert im Code):
   - place.TypeLabel (NavigationService:467) -> an PlacesService-Vergleichslogik gekoppelt
     (IsAetherytePlace, "Ätheryt"/"Aethernet"), wird mit PlacesService-Gruppe entkoppelt.
   - DescribeTargetHp (Ziel-Ansage-Suffix) -> Vitals-Gruppe.

Build: dotnet build Release, 0 Warnungen/0 Fehler.

TEST (V5.45): /acc lang en eingeben -> Bestaetigung auf Englisch. Dann:
- N-Objektbrowser durchblaettern: Kategorien + Objekte auf Englisch ("Enemies", "3 nearby",
  "Player, 12 meters, slightly left, 1 of 5").
- Himmelsrichtung beim Drehen auf Englisch ("East").
- Gehhilfe/Routen-Vorschau: englische Richtungs-/Distanz-Ansagen.
- /acc lang de -> alles wieder Deutsch. /acc lang auto -> folgt Windows-Sprache.
- PRUEFEN: Wegpunkt-Ansage zeigt TypeLabel noch deutsch (erwartet, s.o.).

UIReaderService (groesster Brocken, 7465 Zeilen) - IN ARBEIT, thematische Etappen:
- ETAPPE 1 FERTIG: alle DIREKTEN _tolk.Speak/SpeakInterrupt-Literale uebersetzt
  (~40 Stellen): Social/Menue-Listen (ListSummary "Menu, N entries", NoEntries,
  SocialTabHeader, OnlineWindowPrefix), Text-Eingabe-Echo (empty/deleted),
  Benachrichtigung, ContentsTutorial-Popup (PageOf/EnterCloses/Closed/...),
  Bestiarium, Gegenstand-Abliefern (Delivery), Zufaelliges Aussehen, Datenzentrum,
  Gamepad-Kalibrierung, Uebung/Beginnen, Reiter, Kein aktives Menue, Dump-Meldungen.
  NEBENBEI REPARIERT: 5 kaputt gespeicherte Umlaute (U+FFFD "w�hlen" etc.) in
  Speak-Strings - durch Ersetzung mit korrektem AccessibilityStrings-Text.
- ETAPPE 2 FERTIG (zusammengesetzte Ansagen): Inventar/Sammeln "N Gegenstaende" +
  Item-Stufe/Menge, Item-Tooltip-Stufe, Unbekannter Gegenstand, Konfig-Steuerelemente
  (Regler/Auswahlliste/Eingabefeld), Reward-Labels (Erfahrung/Gil/weitere Verguetung),
  Keybind-Zeile (", Taste"/", keine Taste"), Anfaenger-Arena, Benachrichtigung aktivieren.
  UIReaderService TEIL 1 KOMPLETT: kein deutsches Ansage-Literal mehr (final gegrept).
  Grenzfall dokumentiert: level.Replace("St.", LevelWord) - Ziel-Wort folgt /acc lang,
  aber der Match-Input "St." ist DE-Client-spezifisch (Teil 2).
- BLEIBT DEUTSCH (Match gegen Spiel-UI, Teil 2): "Schließen", "Bestätigen"/"Ok"
  (ConfirmButtonLabels), Journal-Header "Zusammenfassung"/"Optionen"/"Vergütung"
  (5497), SocialTabFallback-Liste. Im Code markiert.

WICHTIG - ZWEITEILIGES ZIEL (geklaert 2026-07-24): Zielgruppe = echte EN-Spieler mit
ENGLISCHEM Client. Daraus folgt:
  Teil 1 = Mod-Ansagen uebersetzen (/acc lang) - laeuft.
  Teil 2 = MATCH-STRINGS client-sprachrobust machen (Buttons/Journal-Header per
  Node-ID oder Lumina-Addon-Sheet statt dt. Text finden) - PFLICHT, eigenes Projekt,
  betrifft v.a. UIReaderService. Ohne Teil 2 brechen Klick-/Match-Interaktionen im
  EN-Client. Noch NICHT begonnen.

EQUIPMENT + COMBAT GRUPPE FERTIG:
- EquipmentService: Ausruestungsliste, Slot-Namen (Waffe/Kopf/.. -> Weapon/Head/..),
  HQ, empfohlene Ausruestung anlegen (alle Status-/Fehlermeldungen), Item-Fallback.
- GearInfoService: Stufe/tragbar/nicht tragbar + Gruende (ab Stufe X, nur fuer Klasse,
  nicht fuer dein Volk).
- CombatService: HP/MP-Schwellen, Ziel-HP, Gegner-Cast, Level-Up/Level-Exp,
  Kampf-Beginn/-Ende, HP/MP-Status, SP/GP (SP=dt., GP=engl. beachtet!).
- VitalsService: KEINE Aenderung noetig (nur Toene, "HP"/"MP" sind Log-Labels).
- DescribeTargetHp (lag in NavigationService) mit-uebersetzt (TargetHpFragment).
- Baustein-Methoden fuer "X von Y" -> "X of Y" (HpValue/MpValue/GpValue/...).

NAECHSTE GRUPPEN (nach Spielrelevanz): Plugin.cs (restliche Commands/Hotkey-Ansagen),
PlacesService (inkl. TypeLabel-Entkopplung), HotbarService, AutoWalkService,
InventoryService, EmoteService, Bank-Handler (V5.44), Gathering, DalamudPluginsService.

---

## STAND VORHER (2026-07-24, V5.44: Gil-Depot (Bank) barrierefrei - GEBAUT, UNGETESTET)

Neuer Handler fuer das Addon "Bank" (Gil beim Gehilfen anvertrauen/entnehmen),
gebaut aus dem Dump vom 2026-07-24 (Desktop\FFXIV_UI_Dump.txt, "Bank" Nodes=37).

Vorher: der generische Pfad las beim Oeffnen ALLE Texte als eine Wortkette
("Gil-Depot, Abbrechen, Ausfuehren, 0, Hinterlegen, Entnehmen, 9.824, 9.824,
Perrox Torran, Danach, Derzeit, Truhe, 30, ...") - Labels von Werten getrennt,
einmalig, KEIN Echo beim Tippen des Betrags.

V5.44: "Bank" in SpecialSetup/UpdateAddons (generisch stumm) + eigener
OnBankUpdate (PostUpdate, deduped). Gelesene Top-Level-Text-Nodes (kollisionsfrei
per ReadTopText): Spieler id=10 Name / id=12 Derzeit / id=14 Danach; Truhe id=17
Label / id=23 Name / id=24 Derzeit / id=25 Danach; Betrag = NumericInput-
Komponente id=32, Kind id=5. Modus-Checkbox id=28.
- Beim Oeffnen: volle Uebersicht ("Gil-Depot, <Modus>. Betrag 0. <Name>: derzeit
  X, danach Y. Truhe <Name>: derzeit A, danach B.").
- Beim Tippen: kompakt "Betrag <n>, <Name> danach <Wert>." (PostUpdate-Dedup).
- Modus (Hinterlegen/Entnehmen): abgeleitet aus der Spieler-Bilanz (danach<derzeit
  = Hinterlegen), also aus echtem Spielzustand korrekt sobald ein Betrag steht;
  nur bei Betrag 0 Notbehelf ueber Checkbox id=28 (ANNAHME checked=Entnehmen,
  wird geloggt).
- Knoepfe (Ausfuehren/Abbrechen, +/-) sagt weiter der globale Fokus-Leser beim
  Durchtabben an (UpdateGlobalFocus, laeuft unabhaengig).

TEST (V5.44): Bei einem Gehilfen "Gil anvertrauen/entnehmen" oeffnen. Erwartung:
1. Oeffnen sagt Uebersicht mit beiden Kontostaenden + Betrag 0.
2. Betrag tippen -> jede Aenderung sagt "Betrag <n>, <Name> danach <Wert>".
3. Zwischen Hinterlegen/Entnehmen umschalten -> Ansage des Modus.
4. PRUEFEN: Stimmt der angesagte Modus? (Bei Betrag 0 haengt er an der Annahme
   checked=Entnehmen - Log [Bank] Modus-Checkbox zeigt den Rohwert.) Falls
   vertauscht: in DeriveBankMode die Checkbox-Zuordnung drehen.
5. PRUEFEN: Sind Derzeit/Danach auf der TRUHE-Seite (id=24/25) richtig herum?
   (Aus dem Dump nur best-effort; Spieler-Seite id=12/14 ist eindeutig.)

---

## STAND VORHER (2026-07-24, V5.43: Timer-Spam + Koords-Kopieren + SP-Stand + Himmelsrichtung-beim-Drehen - GEBAUT, UNGETESTET)

Vier Aenderungen im selben Batch (Build 2026-07-24, 0 Warnungen/0 Fehler, nach devPlugins deployt):

1) DUNGEON-TIMER-SPAM (User-Meldung): Im Dungeon las der generische Text-Scanner
   das Addon "_ToDoList" key=40005 (das Dungeon-Zeitlimit, "87:54" runterzaehlend)
   JEDE Sekunde vor ([Speak] INT '87:54'). Ursache: IsBareNumber (UIReaderService.cs
   :2040) erkannte nur Ziffern + ' . , / %' als Zaehler - der DOPPELPUNKT fehlte, also
   galt "87:54" als echter Text und wurde bei jeder Aenderung gesprochen. FIX: ':' in
   die Whitelist aufgenommen -> jedes Zeit-/Timerformat (M:SS, H:MM:SS) gilt generell
   als Zaehler und wird NIRGENDS mehr sekuendlich vorgelesen. Die echten _ToDoList-
   Ziele (Textinhalte) bleiben. Behebt denselben Grundfehler wie V5.41/42, aber generisch.

2) KOORDS KOPIEREN (User-Wunsch): neue Taste Strg+Umschalt+F2 (KeyCopyCoords) kopiert
   die eigene aktuelle Karten-Koordinate ("24.1, 21.0") in die Zwischenablage - zum
   Weitergeben im Chat. Gegenstueck zu Strg+Umschalt+F1 (KeyGotoCoords = zu Koords
   laufen); das "X, Y"-Format ist genau das, was GotoClipboardCoords zurueckparst.
   Neu: PlacesService.WorldToMapCoord (exakte algebraische Inverse der verifizierten
   MapCoordToWorld, Round-Trip bewiesen) + Plugin.WriteClipboardText (Win32-Schreib-
   Gegenstueck zu ReadClipboardText, kein WinForms/ImGui).

3) SP-STAND-ANSAGE (User-Wunsch): neue Taste Strg+Ende (KeySpStatus) sagt den
   aktuellen SP-Stand an ("SP 480 von 500") - SP = Sammelpunkte (engl. GP), der
   Vorrat, den Sammler fuer Sammel-Fertigkeiten verbrauchen und der sich mit jedem
   Abbauversuch/ueber Zeit regeneriert. Gegenstueck zur HP/MP-Kampfansage.
   CombatService.AnnounceGatheringPoints liest player.CurrentGp/MaxGp; diese lesen
   CharacterData.GatheringPoints/MaxGatheringPoints direkt aus dem Spiel (verifiziert
   an Dalamud Character 2026-07-24, ICharacter.CurrentGp/MaxGp). Gate: MaxGp==0 ->
   "Keine Sammelpunkte, SP gibt es nur als Sammler" (kein erfundener Wert).
   ACHTUNG Tasten-Ueberschneidung: Strg+Ende ist im Keybind-Dump CAMERA_SAVE
   (Kamera-Preset speichern). Plugin schluckt die Taste nicht, also feuert die
   rein visuelle Kamera-Funktion mit - fuer blindes Spiel folgenlos (wie die
   akzeptierte Bild-Tasten/Kamera-Zoom-Ueberschneidung). Falls stoerend: Taste in
   der Config aendern.

4) HIMMELSRICHTUNG BEIM DREHEN (User-Wunsch): neuer HeadingService.cs sagt beim
   Drehen die Himmelsrichtung an, in die man SCHAUT ("Osten"). Blickvektor =
   (sin(rot), cos(rot)) in Welt-XZ - die in NavigationService.RelativeAngle
   verifizierte Rotations-Konvention (Live-Log 2026-07-10) - durch dasselbe
   verifizierte SectorOf-Mapping wie Positions-Peilungen. Neu in RouteService:
   HeadingSector(rot) + SectorWord(sektor). Anti-Spam (der Knackpunkt): sagt nur
   an, wenn die Drehung AUFHOERT (Rotation ~0.15 s still) UND in einem NEUEN
   8er-Sektor landet - schnelles Durchdrehen mehrerer Sektoren sagt nur den
   Endsektor. Umschaltbar mit bare N (KeyToggleHeading, in V5.31 freigeraeumt);
   Toggle sagt "Himmelsrichtung an. <Richtung>." bzw. "aus". Default AN.

TEST 1 (Timer): In einen Dungeon gehen. Erwartung: KEIN sekuendliches Vorlesen des
   Restzeit-Timers mehr. Dungeon-Ziele werden weiter angesagt.
TEST 2 (Koords): Irgendwo in der Spielwelt Strg+Umschalt+F2 druecken. Erwartung:
   Ansage "Koordinaten X, Y kopiert." + im Chat/Notepad einfuegbar (Strg+V) als "X, Y".
   Gegenprobe: kopierte Koords mit Strg+Umschalt+F1 wieder anlaufen -> muss zur selben
   Stelle fuehren.
TEST 3 (SP): Als Minenarbeiter/Gaertner Strg+Ende druecken. Erwartung: Ansage
   "SP X von Y" mit dem aktuellen Sammelpunkte-Stand. Nach Einsatz einer SP-Fertigkeit
   sinkt X, ueber Zeit steigt es wieder. Gegenprobe auf einer Kampfklasse: Ansage
   "Keine Sammelpunkte, SP gibt es nur als Sammler".
TEST 4 (Himmelsrichtung): Im Spiel die Figur drehen / in eine neue Richtung laufen
   und kurz halten. Erwartung: Ansage der Himmelsrichtung ("Norden"/"Osten"/...)
   nach dem Stehenbleiben, NICHT waehrend des Drehens, und nur bei echtem
   Richtungswechsel (nicht dauernd). Zu gespraechig? Mit N abschalten -> "Himmelsrichtung
   aus". Wieder N -> "Himmelsrichtung an. <aktuelle Richtung>". PRUEFEN: stimmt die
   angesagte Richtung mit der tatsaechlichen Blickrichtung ueberein (Nord/Ost nicht
   vertauscht)? Ggf. an einem bekannten Ausrichtungspunkt gegenpruefen.

---

## HISTORIE (2026-07-23, V5.42: Dungeon-Beitritt Sekunden-Spam behoben - GEBAUT, UNGETESTET)

V5.41-TEST (Log 2026-07-23 12:17): Mein Countdown-Handler funktioniert
("Noch 40 Sekunden zum Beitreten" bei 40). ABER: der GENERISCHE Text-Scanner
(ScanAddonTexts in OnAnyAddonUpdate) las id=60 JEDE Sekunde vor ("[Speak] INT
'0:44'/'0:43'/...") -> User "er zaehlt immer noch runter". Bei frueheren Pops
(12:00) sprach der Scanner den Timer nicht (Zustandsabhaengig), bei 12:17 doch.

V5.42 FIX: "ContentsFinderConfirm" in SpecialUpdateAddons aufgenommen ->
OnAnyAddonUpdate returned frueh (UIReaderService.cs:745), ScanAddonTexts laeuft
nicht mehr fuer dieses Addon -> kein Sekunden-Spam. Oeffnungs-Ansage bleibt
(kommt aus OnAnyAddonOpen/PostSetup, NICHT betroffen; Addon nicht in
SpecialSetupAddons). Countdown-Handler ist eigener Listener -> laeuft weiter.
Button-Fokus ('Warten'/'Teilnehmen') kommt vom globalen Fokus-Leser -> bleibt.

TEST: Duty anmelden, Pop abwarten. Erwartung: Oeffnungs-Ansage (Dungeon-Name)
+ NUR alle 10 s "Noch X Sekunden zum Beitreten", KEIN Sekunden-Runterzaehlen.

---

## HISTORIE (2026-07-23, V5.41: Dungeon-Beitritt Countdown alle 10 s)

User: bei der Anfrage "Dungeon beitreten?" (ContentsFinderConfirm) lief ein
Timer, der nicht angesagt wurde - alle 10 s ansagen reicht.

BEFUND (Log 2026-07-23 12:00 + 12:12): Addon "ContentsFinderConfirm", Countdown
in Text-Node id=60 im Format "M:SS" ("0:44" -> zaehlt von 45 runter). Der
generische Scanner LOGGT id=60 jede Sekunde ([Scan]), spricht ihn aber nicht.
Buttons: "Teilnehmen" (id=63, ButtonClick param=8), "Zurueckziehen" (id=65,
param=9), "Warten" - werden vom Fokus-Leser beim Durchtabben schon angesagt.

V5.41: OnContentsFinderConfirmUpdate (PostUpdate). Liest id=60, ParseClock
("M:SS"->Sekunden), sagt bei jedem vollen 10er (40/30/20/10) EINMAL an "Noch X
Sekunden zum Beitreten." via _tolk.Speak (NICHT unterbrechend, damit Button-
Navigation nicht abgeschnitten wird). Reset bei !IsVisible. Addon NICHT in
SpecialUpdate -> generische Oeffnungs-Ansage (Dungeon-Name etc.) bleibt.

TEST: Duty ueber Inhaltssuche anmelden, bei der Pop-Anfrage warten. Erwartung:
"Noch 40/30/20/10 Sekunden zum Beitreten." Buttons weiter per Tab + Enter.

---

## HISTORIE (2026-07-23, V5.40: Anfaenger-Arena Uebungsauswahl lesbar - BESTAETIGT ✓)

V5.40 BESTAETIGT (User 2026-07-23): "das scheint zu funktionieren, hak die
Arena erstmal ab". Vorleser (Uebung + Rolle beim Oeffnen) + Enter=Beginnen
funktionieren. NICHT weiter getestet/gebaut: Uebungs-Navigation (zwischen
Angreifer 1/2/... wechseln, Pfeil-Buttons) + Verguetung-Ansage - bei Bedarf
spaeter nachruesten.

---

## HISTORIE (2026-07-23, V5.40: Anfaenger-Arena Uebungsauswahl lesbar)

User in der Anfaenger-Arena, dumpte Addon "BeginnersMansionProblem"
(Uebungsauswahl-Fenster, 46 Nodes). Struktur:
  id=21 Text  = gewaehlte Uebung ("Angreifer 1")
  id=39 Text  = Klasse/Rolle ("Thaumaturg (Angreifer)")
  id=31/32    = Verguetung (1440 / 200, verschachtelt)
  id=9  Button = Hauptaktion ("Beginnen"/"Uebung wiederholen", Ch=5)
  id=45 "Schliessen", id=43 "Erklaerung", id=14 "Alle abgeschlossenen
  Uebungen", id=12 "Zur Grundausbildung wechseln", id=16/id=3/id=4 = Pfeile?

V5.40 (analog ContentsTutorial): dedizierter Leser OnBeginnersArenaUpdate
(PostUpdate, Dedup): sagt beim Oeffnen/Wechsel "Anfaenger-Arena. Uebung: X.
Klasse. Enter beginnt." In SpecialSetup/UpdateAddons (Text-Scanner aus,
Fokus-Leser bleibt fuer Button-Navigation). Enter (HandleConfirmKey) klickt
Hauptaktion id=9 via DispatchClick -> "Uebung gestartet."

OFFEN FUER TEST: (1) sagt der Leser die richtige Uebung? (2) startet Enter/id=9
die Uebung wirklich? (3) UEBUNGS-NAVIGATION (zwischen Angreifer 1/2/... wechseln)
noch NICHT gebaut - Mechanik unklar (welche Pfeil-Buttons id=16/3/4). Beim Test
mit dem globalen Fokus-Leser durchtabben, dann [Focus]-Log zeigt die Pfeile ->
danach Navigation nachruesten. Verguetung noch nicht vorgelesen.

HINWEIS: seit V5.35 haben sich VIELE ungetestete Features angesammelt (5.35
Objektfilter, 5.36 Tutorial-Popup, 5.37-5.39 Dungeon-Marker-Sonden, 5.40
Arena). Beim naechsten Spielen einmal durchtesten und abhaken.

---

## STAND (2026-07-23, V5.39: Wegpunkt-Diagnose - GEBAUT, UNGETESTET)

User-Klaerung: mit "Orte koennen nicht angelaufen werden" waren die WEGPUNKTE
(PlacesService-Kategorie) gemeint.

BEFUND: Im Dungeon (terr=1036, aktuelle map=31) liest die Wegpunkte-Kategorie
15 statische Marker aus dem Lumina-MapMarker-Sheet fuer map 31. Log zeigt
"Keine Uebergangs-Route von Map 31 nach Map 20/21/22/3" -> das sind
Western-La-Noscea-Overworld-Sub-Karten. VERDACHT (noch nicht hart bestaetigt):
map 31 ist die Overworld-Karte ueber der Instanz; die 15 "Wegpunkte" sind
Overworld-Marker (Uebergaenge/Orte), die geografisch AUSSERHALB des
Dungeon-Meshes liegen -> vnavmesh findet keinen Weg -> nicht anlaufbar.
PlacesService.GetPlaces() nutzt _clientState.MapId + PixelToWorld; Y=0 wird vor
dem Lauf per Navmesh aufgeloest (im Dungeon schlaegt das an einer
Overworld-Position fehl).

V5.39 = DumpMapMarkers (Strg+F5) loggt jetzt ZUSAETZLICH jeden Wegpunkt mit
Name/Typ/Weltposition ([MarkerProbe] Ort '...'). Damit EIN Dungeon-Dump alles
klaert: EventMarkers (=0 bestaetigt), MiniMapMarkers (offen), Wegpunkt-Details
(offen).

WENN Verdacht bestaetigt (Wegpunkte = Overworld-Muell im Dungeon): Wegpunkte-
Kategorie im Dungeon unterdruecken ODER durch echte Dungeon-Ziele ersetzen
(MiniMapMarker-Objective, falls vorhanden; sonst QuestMarker-Endziel +
Objekt-Browser-Tore/Schalter). Parallel bleibt Option B (Auto-Lauf kampf-fest)
der pragmatischste Dungeon-Fortschritt.

---

## STAND (2026-07-23, V5.38: Marker-Sonde erweitert um MiniMapMarkers - GEBAUT, UNGETESTET)

V5.37-SONDEN-ERGEBNIS (Log 2026-07-23, IM Dungeon Sastasha terr=1036 map=31,
ObjProbe zeigte Wellenfahrer-Tor/Gefangene/Duty-Support-NPCs -> eindeutig
Instanz): EventMarkers gesamt = 0. HYPOTHESE WIDERLEGT - das Dungeon-Ziel
steckt NICHT in AgentMap.EventMarkers.

WICHTIGER NEBENBEFUND: Das Dungeon-ENDZIEL ist bereits anlaufbar! Der
QuestMarker "Das Geheimnis der Sastasha-Hoehle" hat IM Dungeon eine Position
(-312|5|311), und der Auto-Lauf dorthin FUNKTIONIERTE (vnavmesh baute
64-Wegpunkte-Pfad, "laeuft dist=790"). Der echte Blocker: der Lauf stoppt bei
JEDEM Kampf (10:11/10:19/10:20 je nach ~8 s gestoppt) -> User kommt nur in
Mini-Schritten voran (840 -> 788 -> 773 m). Ausserdem fuehrt der Direktweg zum
Endziel durch Gegnergruppen + Tore (Wellenfahrer-Tor muss geoeffnet werden).

V5.38 = SONDE ERWEITERT: DumpMapMarkers loggt jetzt zusaetzlich
AgentMap.MiniMapMarkers (die Minimap-Icons; MapMarkerBase verifiziert:
IconId@4, Subtext@16 CStringPointer, X@44/Y@46 short = MAP-PIXEL, nicht Welt).
Das leuchtende Objective-Icon wird hier vermutet. Pixel->Welt umrechenbar wie
PlacesService (welt=(pixel-1024)*100/SizeFactor-Offset), Y-Hoehe via vnavmesh
PointOnFloor.

NAECHSTER TEST: erneut in einen Dungeon, Strg+F5. [MarkerProbe] Mini[..]-Zeilen
zeigen, ob ein MiniMapMarker das aktuelle Ziel ist (icon/sub/X/Y). Falls ja ->
Kategorie "Dungeon-Ziel" bauen. Falls auch leer -> das Ziel ist evtl. NUR der
QuestMarker (Endziel) + Objekt-Browser (Tore/Gegner als EventObj/BattleNpc);
dann Fokus auf Option B (Auto-Lauf kampf-fest) statt neuer Ziel-Quelle.

STRATEGIE-NOTIZ: Sastasha-Ziele stehen im _ToDoList als Text ("Korallenmecha-
nismus betaetigen 0/1", "Sastasha erkunden"). Die interaktiven Ziele (Tore,
Schalter) sind als EventObj IM Objekt-Browser (Wellenfahrer-Tor zielbar=True).
Der Dungeon-Fortschritt = Gegner-Pulls + Tore oeffnen; Duty-Support-NPCs
fuehren durch -> denkbare Alternative: dem vorangehenden NPC-Begleiter folgen.

---

## STAND (2026-07-23, V5.37: DUNGEONS barrierefrei - Schritt 1: Ziel-Marker-Sonde - GEBAUT)

User-Ziel: Dungeons barrierefrei machen. User-Wahl fuer den ersten Baustein:
"Dungeon-Ziel anlaufbar machen" (das leuchtende Story-Ziel im Dungeon als
anlaufbaren Punkt).

BEFUND aus Log 2026-07-23 (Weg zum Sastasha-Eingang, terr=1036/map=31):
- Auto-Lauf FUNKTIONIERT grundsaetzlich (vnavmesh baute 64-Wegpunkte-Pfad,
  978 m). ABER: bricht bei jedem Zufallskampf ab ("Kampf." -> "Auto-Lauf
  gestoppt", Zeile 3009) und muss neu gestartet werden.
- Das Overworld-QuestMarker-System (Map.Instance()->QuestMarkers, gelesen von
  QuestMarkerService) zeigt im Dungeon-Gebiet nur den EINGANG (map 31), nicht
  das dungeon-interne Ziel. Dungeon-Aufgaben stehen im _ToDoList als TEXT ohne
  Weltkoordinaten ("Korallenmechanismus betaetigen 0/1", "Sastasha erkunden").

RECHERCHE (ilspycmd, AgentMap verifiziert): Das dynamische Dungeon-Ziel steckt
wahrscheinlich in AgentMap.Instance()->EventMarkers (StdVector<MapMarkerData>,
Doc: "FateManager, EventFramework and SequentialEvent"). Dungeons laufen ueber
EventFramework/Director -> Objective-Marker dort erwartet. Gleicher
MapMarkerData-Typ (Position/IconId/TooltipString/Radius), den QuestMarkerService
schon liest. NOCH NICHT verifiziert, ob es im Dungeon das Ziel enthaelt und in
welchem Koordinatensystem (Welt vs. Pixel).

V5.37 = SONDE (kein fertiges Feature): NavigationService.DumpMapMarkers() haengt
an Strg+F5 (neben DumpNearbyObjects). Loggt [MarkerProbe] alle EventMarkers
(pos/icon/radius/tooltip) + Spielerposition + terr.

NAECHSTER SCHRITT / TEST: In Sastasha (oder irgendeinen Dungeon) gehen, dort
Strg+F5 druecken. Log-Zeilen [MarkerProbe] zeigen, ob EventMarkers das aktuelle
Ziel enthaelt. Falls ja -> als anlaufbare Position in eine Kategorie
"Dungeon-Ziel" bauen (Positions-Lauf wie Quest-Ziele). Falls EventMarkers leer/
ohne Ziel -> MiniMapMarkers (FixedSizeArray100<MiniMapMarker>) oder
MapMarkers (FixedSizeArray132<MapMarkerInfo>) proben. NICHT vorher bauen.
OFFEN nebenbei: Auto-Lauf nach Kampf automatisch fortsetzen (User-Option B).

---

## STAND (2026-07-23, V5.36: ContentsTutorial - Freischaltungs-Popup lesbar + schliessbar - GEBAUT, UNGETESTET)

User steckte in einem Fenster fest: "mir wurde gesagt dass ich die Arena
freigeschaltet hab, aber ich kann die Meldung nicht wegmachen, es geht auch
nicht mehr zurueck." Dump (Strg+F5, jetzt via V5.35-Fallback erfasst,
Desktop\FFXIV_UI_Dump.txt) zeigte Addon "ContentsTutorial" (Titel
"INHALTSFUEHRER"):
  id=2  Text  = Ueberschrift ("Anfaenger-Arena")
  id=5  Text  = Fliesstext ("Die Anfaenger-Arena wurde freigeschaltet! ...")
  id=7  Text  = Seitenzaehler ("1/1")
  id=11 Button "Schliessen" = EINZIGER Weg raus (Spiel ignoriert Escape hier)
  id=10 CheckBox "Verstanden! Ich bin bereit!"

DOPPELTES PROBLEM: (1) stumm - ContentsTutorial stand in NotificationAddons,
OnNotification las nur den ERSTEN Text (leeres id=4/Titel), nie den Body.
(2) nicht schliessbar - blinder Nutzer kann den "Schliessen"-Button nicht
klicken, Escape wird vom Spiel ignoriert.

ZWEITER DUMP (09:57): MEHRSEITIGES Tutorial "Dungeon-Inhalte", Seite "1/8".
Kritisch: der "Schliessen"-Button (id=11) ist auf Seite 1 UNSICHTBAR (Dump:
kein V-Flag) - er erscheint erst auf der LETZTEN Seite. Stattdessen zwei
Bild-Buttons (Pfeile) id=8 (ButtonClick param=1) und id=9 (param=2) =
Weiter/Zurueck (aus [Focus]-Eventlog). Der User steckte fest, weil man erst
durch 8 Seiten blaettern muss, bevor "Schliessen" da ist.

FIX (V5.36):
- ContentsTutorial aus NotificationAddons RAUS. Neuer dedizierter Leser
  OnContentsTutorialUpdate (PostUpdate, Dedup, Reset bei !IsVisible): sagt
  "Ueberschrift. Body. [Seite X von Y.] Enter schliesst/blaettert weiter."
  Body via AtkText.Read (Sanitize entfernt die HI..IH-Payload-Glyphen).
  Hinweis haengt an id=11-Sichtbarkeit: sichtbar -> "Enter schliesst", sonst
  "Enter blaettert weiter". Bleibt in SpecialSetup/UpdateAddons.
- Enter (HandleConfirmKey -> AdvanceOrCloseContentsTutorial): wenn id=11
  sichtbar (letzte Seite) -> TryClickButton "Schliessen" ("Geschlossen.");
  sonst -> DispatchClick auf id=8 (Weiter-Pfeil), naechste Seite wird
  vorgelesen. So blaettert der User mit mehrfachem Enter durch und schliesst
  am Ende. Einseitige Popups schliessen beim ersten Enter.

VERSION HOCHGESETZT: 5.34 -> 5.36 (csproj + Plugin.cs), damit die Ladeansage
"Version 5 Punkt 36" bestaetigt, dass die neue DLL aktiv ist. WICHTIG: der
User hatte V5.34 geladen und die neuen Handler liefen nicht (kein [Tutorial]
im Log) - immer erst pruefen, ob die aktuelle Version wirklich geladen ist.

OFFENE PUNKTE FUER DEN TEST:
- Ist id=8 wirklich der WEITER-Pfeil? ANNAHME aus param=1. Falls die Seite
  nicht hochgeht ([Tutorial]-Log "Seite vorher=X/8" bleibt gleich): id=8/id=9
  tauschen.
- Schliesst der Event-Dispatch auf der letzten Seite wirklich?
- Kommt der Body sauber (ohne HI/IH-Glyphen)? [Tutorial]-Log zeigt es.
- Duty-startende Tutorials verlangen evtl. die Checkbox "Ich bin bereit!"
  (id=10) vor dem Schliessen - dann eigene Taste noetig.

TESTPLAN: In so ein Popup gehen. Erwartung: jede Seite wird vorgelesen; Enter
blaettert weiter, bis Seite Y/Y -> Enter schliesst ("Geschlossen.").

---

## STAND (2026-07-23, V5.35: namenlose Objekte im Browser AUSBLENDEN - GEBAUT, UNGETESTET)

User-Beobachtung (Log 2026-07-23): in Wohnvierteln (Dorf des Nebels) blaehte
der Objekt-Browser die Liste auf (52 Objekte), viele mit LEEREM Namen -
Ansage "leer, Objekt, 20 Meter, hinter rechts, 3 von 52" (ids 4000BA0E/BA0F,
EventObj). User-Entscheid: solche Objekte AUSBLENDEN.

ROOT CAUSE: der Namensfilter in NavigationService.GetObjectsOfKinds pruefte
den ROHNAMEN (o.Name.TextValue) mit IsNullOrWhiteSpace. Diese Objekte tragen
einen Namen aus Icon-Glyphen / SeString-Payload-Bytes -> roh nicht leer
(Filter laesst durch), aber TolkService.Sanitize (U+E000-F8FF + 0x02..0x03
Payloads) reduziert ihn beim Sprechen auf nichts -> leere Ansage.

FIX: der Filter prueft jetzt den SANITISIERTEN Namen
(!IsNullOrWhiteSpace(TolkService.Sanitize(o.Name.TextValue))). Ursachen-
unabhaengig robust, konsistent mit dem Rest (ueberall wird vor dem Sprechen
sanitisiert). Sammelpunkte bleiben ausgenommen (eigener Fallback-Name).

TEST: in der gleichen Gegend (Dorf des Nebels) mit BildAuf/BildAb durch die
Kategorie "Objekte" blaettern - die namenlosen Eintraege sollten weg sein,
Anzahl kleiner als 52, keine "leer"-Ansagen mehr.

---

## STAND (2026-07-23, V5.34: Zeichen-Echo beim Tippen AUS)

User-Wunsch: beim Schreiben in Chats und Eingabefeldern sollen die einzelnen
ZEICHEN nicht mehr vorgelesen werden.
- Neuer Config-Schalter EchoTypedCharacters (Default FALSE). Gate zentral in
  SpeakTextEchoDiff (fruehes return), damit ALLE drei Tipp-Echos auf einen
  Schlag schweigen: Chat (OnChatLogUpdate), Namensfeld (OnCharaMakeNameUpdate),
  Kommentarfeld (OnCharaMakeInputUpdate).
- BEWUSST erhalten (nicht "die Zeichen"): die Kontext-Ansagen laufen weiter -
  "Chat-Eingabe, <Kanal>" beim Oeffnen, Kanalwechsel, Feld-Label
  ("Vorname"/"Nachname"). Nur das Zeichen-fuer-Zeichen-Echo haengt am Schalter.
- FOLGE (dem User mitgeteilt): In der Charaktererstellung hoert man beim
  Tippen des Namens jetzt auch nichts mehr - nur noch das Feld-Label. Falls
  das stoert, EchoTypedCharacters gezielt fuer die Namensfelder wieder an
  (oder global). EchoChatInput bleibt true (Chat-Eingabe-Kontext bleibt).

---

## STAND (2026-07-22, V5.33: SAMMELN - Cursor-Zeile + Ausbeute-Ansage - GEBAUT, UNGETESTET)

Aufbauend auf V5.32 (bestaetigt, s.u.). User: "mach beide" (A Pro-Gegenstand-
Ansage beim Durchblaettern, B Ausbeute-Ansage).

A - PRO-GEGENSTAND BEIM DURCHBLAETTERN:
- Neuer Zweig in UpdateGlobalFocus (ZUERST in der if/else-Kette):
  TryReadGatheringFocusRow. Wenn der globale FocusedNode in einer Item-Zeile
  des offenen "Gathering"-Addons sitzt, wird die Zeile ueber das saubere
  DescribeGatheringItem vorgelesen (Name via ReadClean + Stufe + Chance).
- SELBSTDIAGNOSE: Falls der FocusedNode im Sammel-Fenster NICHT wandert
  (im V5.32-Test war beim Abbauen kein [Focus] im Log - aber unklar, ob der
  User ueberhaupt geblaettert hat), bleibt der Zweig einfach stumm und die
  vorhandenen [Focus]-Zeilen zeigen, was der Fokus stattdessen tut. Dann
  Pivot auf ein Auswahl-Index/Highlight-Feld.
- Mengen-Fix: der Ausbeute-Node (Icon-Comp id=31 -> id=7) ist "unsichtbar"
  markiert, wird jetzt OHNE Sichtbarkeits-Gate gelesen; "Menge" wird nur
  angesagt, wenn >1 ("Menge 1" waere Laerm).

B - AUSBEUTE-ANSAGE (Chat):
- Loot kommt als XivChatType.Gathering (67), Log-verifiziert: "Du beginnst
  abzuholzen." / "Du hast ein Ahorn-Holzscheit erhalten." / "Du bist fertig
  mit dem Abholzen." (bisher gelesen=False). ChatReaderService liest den Typ
  jetzt (Config ReadGatheringMessages=true), OHNE Kanal-Praefix (Satz ist
  komplett). Menge steckt im Text ("ein"/"drei"). Doppel-Space (gestripptes
  Icon-Glyph) faengt TolkService.Sanitize.
- NICHT gelesen: Progress (64) "Deine Routine steigt um X" (Sammel-EXP) -
  bewusst ausgelassen (Spam); Achievements sind auch Progress(64).

### V5.32 BESTAETIGT (User + Log 2026-07-22 21:20)
"es wurde was angesagt und wenn ich drauf druecke baut er es ab". Log:
[Gather] Sammel-Fenster gelesen: 4 Gegenstaende, Kopf='Abholzen.
Belastbarkeit 4 von 4' -> Ansage "...1. Ahornast, Stufe 5, Chance 95
Prozent. 2. Latex, ...". NAMEN SAUBER (ReadClean funktioniert). Abbauen
laeuft ueber den Spiel-Cursor.
NEBENBEFUND (loest V5.12-Sorge): der Objekt-Browser zeigt Minen-Punkte sehr
wohl - im selben Log "Minenarbeiter (Abbauen), Stufe 10, 100 Meter" neben 20
"Gaertner (Abholzen)"-Punkten. "Nur Gaertnersachen" lag an der Entfernung.
Punkte werden nach Klasse gelabelt.

### Beim naechsten Test (V5.33)
1. "Version 5 Punkt 33 bereit".
2. Sammel-Fenster oeffnen, mit Pfeiltasten durch die Liste: wird pro Zeile
   Name+Stufe+Chance angesagt (A)? Oder bleibt es still (dann [Focus]-Log
   pruefen: wandert der Fokus ueberhaupt)?
3. Abbauen: kommt die Ausbeute-Ansage "Du hast X erhalten" (B)? Nicht zu
   viel Laerm (Routine-EXP soll NICHT kommen)?
4. Sauber, kein Praefix-Kauderwelsch vor der Loot-Zeile?

---

## STAND (2026-07-22, V5.32: SAMMEL-FENSTER wird vorgelesen)

User-Auftrag: das Sammel-Fenster (Erz abbauen / Holz faellen) barrierefrei
machen - "man sollte die Materialien sehen". User lieferte einen sauberen
Dump vom Faellen (Desktop\FFXIV_UI_Dump.txt): Addon "Gathering", 40 Nodes.

STRUKTUR (verifiziert am Dump 2026-07-22, Node-IDs gepinnt):
- 8 Item-Zeilen = Comp(1010) CheckBox (Ch=34). Gefuellt, wenn Name (id=23)
  nicht leer. Pro Zeile: id=23 Name (Item-Link-SeString!), id=21 "St. X"
  (Stufe), id=16 Bonus-%, id=10 Sammelchance-%, id=7 "Rar"/id=6 "Verborgen"
  (nur sichtbar wenn zutreffend), Ausbeute in Icon-Comp(1005) id=31 -> id=7.
- Kopf: Aktion in Window-Comp(1013) id=39 -> id=3 ("Abholzen"); Belastbarkeit
  aktuell id=12 / max id=9 (Top-Level).
- AddonGathering-Struct (ilspycmd) haelt die Liste NICHT (nur TooltipActive,
  ItemListHovered, GatherStatus) -> Werte kommen aus den UI-Nodes.

NEU IN V5.32:
- Dedizierter OnGatheringUpdate (PostUpdate, einmal pro Oeffnen; Flag reset
  bei !IsVisible). "Gathering" in SpecialSetup- + SpecialUpdateAddons, damit
  der generische Leser die Item-Link-Namen nicht als Rohtext scrapt.
- AtkText.ReadClean(): liest Node-Text als Dalamud-SeString (MemoryHelper,
  Adress+Laenge-Ueberladung) -> Item-Link-Payload faellt weg. Loest das
  Glyphen-Problem "H?%I?&Ahorn-Holzscheit...IH" -> "Ahorn-Holzscheit".
  Gleiche Guard wie AtkText.Read (TryValidate extrahiert).
- Ansage beim Oeffnen: "Abholzen. Belastbarkeit 4 von 4. 4 Gegenstaende:
  1. Ahorn-Holzscheit, Stufe 3, Menge 1, Chance 95 Prozent. 2. ...".

ANNAHME, markiert: "St. X" = Stufe (Item-Sammelstufe). Im Dump korreliert
hoeheres St. mit niedrigerer Chance (St.5 Ahornast=85%, St.1=100%) - aber
in-game NICHT bestaetigt, koennte auch Sterne sein. Nur die Abkuerzung wird
expandiert, die Zahl bleibt woertlich.

### Beim naechsten Test (V5.32)
1. "Version 5 Punkt 32 bereit".
2. Erzader/Baum ansteuern und INTERAGIEREN (Sammel-Fenster oeffnen): wird
   beim Oeffnen die Liste vorgelesen (Aktion + Belastbarkeit + jeder
   Gegenstand mit Name/Stufe/Menge/Chance)?
3. Sind die NAMEN sauber (kein "H%I&...IH"-Muell)?
4. Stimmt "Stufe X" - oder ist es im Fenster etwas anderes (Sterne)?
5. Ist "Belastbarkeit 4 von 4" richtig herum (bei angebrochenem Knoten z.B.
   "3 von 4")?
6. Log-Kontrolle: Zeile "[Gather] Sammel-Fenster gelesen: N Gegenstaende".
7. OFFEN fuer v2 (nach Test): Wie waehlt man einen Gegenstand aus und baut ab?
   Cursor-/Fokus-Mechanik im Fenster ist noch nicht erschlossen - beim Test
   mit Pfeiltasten durch die Liste gehen und berichten, ob etwas angesagt
   wird (globaler Fokus-Leser) oder Stille herrscht.

---

## STAND (2026-07-22, V5.31: OBJEKT-BROWSER auf Bild-Tasten, N frei - RELEASED)

### V5.31 BESTAETIGT (User im Spiel: "ok funktioniert")
Objekt-Browser von der N-Familie auf die Bild-Tasten umgezogen, damit N
kuenftig fuer etwas anderes frei ist:
- Unterkategorien: Bild-ab (vor) / Bild-auf (zurueck) = KeyNextObject / KeyPrevObject
- Kategorien: Strg+Bild-ab / Strg+Bild-auf = KeyCategory / KeyCategoryPrev

VERIFIZIERT am echten Keybind-Dump (Desktop\FFXIV_Keybinds.txt): bare
Bild-auf/Bild-ab = CAMERA_ZOOMIN/ZOOMOUT. Das Plugin schluckt Tasten
NICHT (liest nur IKeyState), also zoomt die Kamera beim Unterkategorie-
Wechsel visuell mit - fuer blindes Spiel folgenlos, User hat das bewusst
akzeptiert. Strg+Bild-auf/-ab laut Dump voellig frei. Umsetzung: Config-
Defaults + Migration Version 8 (nur unveraenderte Standardwerte), BildAuf
=0x21/BildAb=0x22 in die AKTIVE Tabelle Plugin.cs KeyNameToVK (NICHT
KeyNames.cs - toter Code), Hilfetext angepasst.

MOJIBAKE-FIX: Plugin.cs hatte an 31 Umlaut-Stellen doppelte Kodierung
(UTF-8 als cp1252 gelesen, wieder UTF-8) - u.a. der GESPROCHENE Hilfetext
(Strg+F1), "in der Naehe", Konflikt-Labels. Datei war GEMISCHT (manche
Umlaute korrekt), darum nur die echten Ã-Mojibake-Paare zurueckgerechnet,
korrekte Umlaute unangetastet. BOM + Zeilenenden erhalten. Nur Plugin.cs
war betroffen, alle anderen .cs sauber.

MITGEZOGEN (vorbestehende, bis dahin UNDOKUMENTIERTE + vermutlich
ungetestete WIP-Arbeit aus frueherer Session - User-Entscheid "alles
zusammen" releasen): Goto-Karten-Koordinaten aus Zwischenablage (Strg+
Umschalt+F1: GotoClipboardCoords/ParseMapCoords/ReadClipboardText/
PlacesService.MapCoordToWorld), Objekt-Sonde (Strg+F5:
NavigationService.DumpNearbyObjects), HP-Ansage als absolute Werte
"HP X von Y" statt Prozent (CombatService + NavigationService).
>>> DIESE DREI SIND IM RELEASE, ABER NICHT IN-GAME GETESTET. <<<

### RELEASE v5.31 (2026-07-22)
- Versionen synchron: Plugin.cs 5.31, csproj 5.31.0.0, repo.json 5.31.0.0
  (byte-sicher via Python-Bytes-Replace: 958 Bytes, 10 Nicht-ASCII-Bytes
  vorher wie nachher, genau 1 Zeile geaendert).
- latest.zip aus Release-Build (0/0): Manifest 5.31.0.0, Tolk +
  nvdaControllerClient64 + alle NAudio-DLLs drin (549248 B).
- Installer-EXE UNVERAENDERT aus release_v5.30 uebernommen; Sha256 gegen
  installer.json geprueft, stimmt exakt.
- Assets in dist/release_v5.31: latest.zip, FF14Accessibility-v5.31.0.zip,
  FF14AccessibilityInstaller.exe, installer.json, notes.md.
- GitHub-Release v5.31 als "Latest"; releases/latest/download/latest.zip
  per HEAD verifiziert: HTTP 200, 549248 B = passt zur lokalen ZIP.
- Untracked NICHT committet: KeyNames.cs (toter Code), uia_test.ps1.

### BEIM NAECHSTEN TEST offen (V5.31-WIP): (1) Goto-Koordinaten Strg+
Umschalt+F1: Koordinaten kopieren, laeuft er hin? (2) Objekt-Sonde
Strg+F5: Log [ObjProbe] gefuellt? (3) HP-Ansage sagt "X von Y"?

---

## ARCHIV V5.30 (2026-07-21, SKILL-FENSTER liest Level+Beschreibung - BESTAETIGT + RELEASED)

### V5.30 BESTAETIGT (User im Spiel)
User: "funktioniert, sagt name stufe und beschreibung an". Beim Navigieren
durch die Skills kommt "Name, Stufe X, <Beschreibung>". Die offene Laufzeit-
frage ist damit geklaert: AgentActionDetail.ActionId zieht bei TASTATUR-
Navigation mit (Maus nicht noetig).

### RELEASE v5.30 (2026-07-21)
- repo.json BYTE-SICHER auf 5.30.0.0 (ISO-8859-1 hin/zurueck): 958 Bytes und
  10 Nicht-ASCII-Bytes vorher wie nachher, genau 1 Zeile geaendert.
- latest.zip aus Release-Build: Manifest 5.30.0.0, Tolk + nvdaControllerClient64
  + alle NAudio-DLLs drin.
- Installer-EXE UNVERAENDERT aus release_v5.29 uebernommen; Sha256 per
  Get-FileHash gegen installer.json geprueft, stimmt exakt (Installer nicht
  geaendert -> Hash bleibt gueltig).
- Assets in dist/release_v5.30: latest.zip, FF14Accessibility-v5.30.0.zip,
  FF14AccessibilityInstaller.exe, installer.json, notes.md.

Ziel (User-Wunsch): im Skill-Fenster (Addon `ActionMenu`, "Aktionen &
Talente") soll pro Skill Name + Stufe + Beschreibung in EINER Zeile kommen.
User-Entscheid: volle Beschreibung, Traits mit Name+Stufe.

VERIFIZIERT (ilspycmd + Dump/Log 2026-07-21 14:50):
- ActionMenu-Liste = reines ICON-Raster (ListItemRenderer -> DragDrop -> Icon,
  KEIN Text-Node). Name/Level/Beschreibung stehen NICHT im UI.
- Bisher las nur der Tooltip-Fallback Name+Level ("VortexschnittSt. 1", ohne
  Trennung, ohne Beschreibung).
- SAUBERE QUELLE: AgentActionDetail (ilspycmd) haelt ActionId @60 + ActionKind
  (DetailKind-Enum: Action/CraftingAction/Trait/...). Lumina: Action.Name +
  Action.ClassJobLevel (das "St. X") + ActionTransient.Description (gleiche
  RowId); Trait.Name + Trait.Level (Trait-Sheet hat KEINE Description).

FIX V5.30 (UIReaderService.cs): neuer Fokus-Leser TryReadActionMenuFocusRow.
Wenn ActionMenu offen + Fokus auf echtem Skill-Slot (FocusIsActionSlot: Icon/
DragDrop-Komponente, nicht Button) -> AgentActionDetail lesen -> Lumina ->
"Name, Stufe X, Beschreibung". Sektions-Kopfzeilen (Kommandos/Rolle/
Eigenschaften) bleiben generisch. Hat Vorrang vor der Item-Aufloesung.

OFFENE LAUFZEITFRAGE (nicht geraten, per Log zu klaeren): zieht
AgentActionDetail.ActionId auch bei TASTATUR-Navigation mit (nicht nur Maus)?
Indiz: der Text-Tooltip erscheint bei Tastaturfokus. BEWEIS liefert die neue
Logzeile "[ActionDetail] node id=.. kind=.. id=.. -> '<text>'": steht dort
beim Durchgehen jeweils der Skill, auf dem der Fokus sitzt -> bestaetigt.
Falls die id nachhinkt -> Fallback: Hook auf AgentActionDetail.HandleActionHover.

BEIM TEST: (1) "Version 5 Punkt 30 bereit" muss kommen. (2) ActionMenu oeffnen,
mit Pfeiltasten durch Skills gehen: es MUSS "Name, Stufe X, <Beschreibung>"
kommen. (3) Traits (Eigenschaften-Bereich): "Name, Stufe X". (4) Kopfzeilen
weiter "Kommandos/Rolle/Eigenschaften". (5) Log [ActionDetail] gegenpruefen.

Committet + als GitHub-Release v5.30 veroeffentlicht. (Details im Memory:
skill_window_actionmenu.md)

---

## STAND (2026-07-21, EMOTE-BILDSCHIRM: Untersuchung, pausiert)

Ziel: das in-game Emote-Fenster (Addon `Emote`) soll beim Navigieren jedes
Emote vorlesen. User-Entscheid: das ECHTE Spielfenster lesbar machen (nutzt
Favoriten/Kategorien), NICHT nur den vorhandenen Tasten-Browser.
User-Status 2026-07-21: "bis jetzt funktioniert das mit den emotes" - keine
akute Fehlfunktion, Arbeit pausiert, nur Zwischenstand gesichert.
(Volldetails im Memory: emote_screen_investigation.md)

VERIFIZIERT (Quellcode + dalamud.log 2026-07-21 09:03-09:11):
- KEIN Spieldaten-Zeiger auf den markierten Emote: AgentEmote (272 B) hat kein
  Selected-Feld, es gibt keine AddonEmote-Struct in FFXIVClientStructs. UI-
  Auslesen ist alternativlos.
- Emote-Fenster = TreeList (Comp id=47, CT=TreeList(12), 36 ListItemRenderer).
  Emote-Name im id=5 Text-Node jeder Zeile.
- Navigation bewegt nur den globalen FocusedNode (id=7), NICHT die TreeList-
  Index-Felder (HoveredItemIndex2 blieb -1). Daher greift TrackListIndices
  nicht - nur der [Focus]-Pfad (UpdateGlobalFocus, jeden Frame, Plugin.cs:501).
- Der [Focus]-Pfad liest den Namen bereits korrekt bei befuellten Kategorien
  (Log 09:03: "Freuen/Aufmuntern/Willkommen/Beruhigen" angesagt).

LUECKEN (sicher behebbar, noch nicht gebaut):
1. Leere/recycelte Zeilen (id=5="") -> Stille. Dump 09:11: alle 36 Zeilen leer
   (Items.LongCount=0) = vermutlich leere Favoriten/Zuletzt-Tab.
2. Keine Kategorie-Ansage (man hoert das Emote, nicht die Kategorie/Tab).

OFFEN - nur in-game klaerbar (nicht raten): kommt man per Tastatur an ALLE
Emotes/Kategorien heran (Raster-Fenster, evtl. mehrere Tabs)? Wie Kategorie
wechseln? Naechster Schritt: User navigiert eine BEFUELLTE Kategorie mit
Pfeiltasten + frischer Strg+F5-Dump.

Vorhandener Tasten-Browser (funktioniert, Fallback): EmoteService.cs,
Umschalt+F4/F5 blaettern alle freigeschalteten Emotes, Umschalt+F6 fuehrt aus.

---

## STAND (2026-07-20 abends, V5.29: ABSTURZ-FIX BESTAETIGT)

### RELEASE v5.29 VEROEFFENTLICHT (2026-07-20 ~22:30)
Commits b7625d1 (V5.29) + add14b8 (repo.json 5.29.0.0) nach origin/main
gepusht. GitHub-Release v5.29 mit 4 Assets, alle state=uploaded, v5.29
ist "Latest" (per gh api releases/latest geprueft):
latest.zip (545602 B), FF14Accessibility-v5.29.0.zip,
FF14AccessibilityInstaller.exe, installer.json.
- latest-Link per HEAD verifiziert: HTTP 200, 545602 B - passt zur ZIP.
- ZIP-Inhalt geprueft: Manifest 5.29.0.0, Tolk.dll +
  nvdaControllerClient64.dll + alle NAudio-DLLs drin.
- Installer-EXE UNVERAENDERT aus release_v5.28 uebernommen; Sha256 per
  Get-FileHash gegen installer.json geprueft, stimmt exakt.
- repo.json byte-sicher via ISO-8859-1: 958 Bytes und 10 Nicht-ASCII-
  Bytes vorher wie nachher.
- Dringlichkeit war hoch: unter v5.28 konnte JEDER Nutzer sein Spiel mit
  Taste O zum Absturz bringen.

### Merke fuer kuenftige Sessions
- KeyNames.cs (untracked) ist toter Code: die Klasse KeyNames wird
  nirgends referenziert (die Treffer auf "KeyNames" sind SlotKeyNames in
  HotbarService). Ein frischer Clone baut also. Entweder loeschen oder
  bewusst so lassen - nicht versehentlich fuer noetig halten.
- Byte-Vergleich von Dateien NIE mit PowerShells ">" dumpen, das fuegt
  ein UTF-8-BOM hinzu und taeuscht 3 Bytes Differenz vor.
  Richtig: cmd /c "git show HEAD:pfad > datei".
### V5.29 BESTAETIGT (User "ja passt" + Log 22:09-22:13)
Der Beweis ist staerker als "es ist nichts passiert" - der AUSLOESENDE
PFAD lief erneut durch und hielt:
22:09:35 _NotificationFriend MouseClick -> Social geoeffnet. Exakt die
Sequenz, die um 21:53 noch das Spiel gerissen hat. Kein Absturz.

- KEIN Crash-Log mehr nach 21:54:52 (das war der letzte unter V5.28).
- ALLE Registerkarten-Labels kamen "aus ButtonTextNode": Gruppe,
  Freunde, Suche. KEIN EINZIGES MAL die Fallback-Liste => das befuerchtete
  Ladezustands-Problem tritt NICHT auf, ein IsReady/IsFullyLoaded-Gate
  wird NICHT gebraucht.
- KEINE REGRESSION durch die 46 umgestellten Lesestellen: 326
  Sprachausgaben im Log, davon 0 leer.

### Nebenbefunde (NICHT vom Fix verursacht, nicht beauftragt)
1. "Liste NICHT gefunden" beim ERNEUTEN Oeffnen derselben Registerkarte
   (22:09:41, 22:10:20) - Karte wird angesagt, Inhalt fehlt. Beim
   Durchschalten geht es wieder. Vermutlich Kind-Fenster-Id-Logik
   (_outgoingSocialChildId), NICHT untersucht.
2. Stumme Listenzeilen FriendList/PartyMemberList/SocialList (15x
   "[Focus] STUMM", id=7 typ=8). VORBESTEHEND seit V5.27, steht dort
   schon offen. Dass die Nachbartexte ('--') gelesen werden, zeigt dass
   AtkText dort arbeitet - der fokussierte Node traegt selbst keinen Text.
## Der Fix im Detail (V5.29)

### Was passiert war
Vier Spielabstuerze in 25 Minuten (21:31, 21:33, 21:53, 21:54).
User-Meldung: Taste O (Online) und "Anfragen annehmen". BEIDES DERSELBE
BUG - der Klick auf die Freundschafts-Benachrichtigung oeffnet dasselbe
Fenster wie Taste O (Log 21:53:04 _NotificationFriend MouseClick, danach
Absturz).

### ROOT CAUSE (am Quellcode belegt, nicht vermutet)
Alle vier Crash-Logs haben denselben Stack:
AnnounceSocialTabIfChanged -> Utf8String.ToString() -> AccessViolation.

FFXIVClientStructs' Utf8String.ToString() ist UNGESCHUETZT (ilspycmd
gegen die DLL vom 2026-07-17):
    Length  => Math.Max(0, (int)(BufUsed - 1));
    AsSpan() => new ReadOnlySpan<byte>((byte*)StringPtr, Length);
    ToString() => Encoding.UTF8.GetString(AsSpan());
Kein Null-Check, kein Bounds-Check. Auf einem Node, den das Spiel zwar
angelegt aber noch nicht gefuellt hat, stehen in StringPtr und BufUsed
noch die alten Bytes -> GetString liest einen Muell-Zeiger ueber eine
Muell-Laenge und laeuft aus der Speicherseite.

KEIN Offset-Drift! Um 09:31 und 21:35 hat dasselbe Fenster korrekt
"Gruppe", "Freunde", "Suche" angesagt. Es ist ein TIMING-Problem: bei
frisch aufgebautem Fenster laeuft unser PostUpdate-Handler zu frueh.

Der bisherige Schutz "textNode != null" (UIReaderService:604) reicht
nicht - der Zeiger WAR gesetzt, nur der Text dahinter war leer.

WICHTIG: try-catch haette das NIE gefangen. AccessViolationException ist
in .NET eine Corrupted State Exception; der Check muss VOR dem Lesen
passieren.

### FIX V5.29
Services/AtkText.cs (neu) - EIN abgesicherter Leser fuer alle UI-Texte:
- prueft den Utf8String selbst per VirtualQuery
- prueft die Struct-Invariante BufUsed <= BufSize (die das Spiel fuehrt)
- prueft den Puffer an BEIDEN Enden (nur Anfang pruefen reicht nicht,
  ein langer Lesevorgang liefe sonst trotzdem aus der Seite)
- liefert im Zweifel "" statt zu raten

ALLE 46 Aufrufe von NodeText.ToString() umgezogen (45 in
UIReaderService.cs, 1 in QuestMarkerService.cs). Der Social-Pfad war nur
die Stelle, die es zuerst erwischt hat - dieselbe Falle lag ueberall.

Aufgeraeumt: IsReadable/DescribeMemory gab es danach doppelt. Die
Fassungen in UIReaderService sind jetzt duenne Weiterleitungen an
AtkText, die zweite Kopie der MEMORY_BASIC_INFORMATION-Struct ist weg -
genau ueber so eine zweite Kopie koennte der 44-statt-48-Byte-Bug von
2026-07-09 unbemerkt zurueckkommen.

Byte-Sicherheit: UIReaderService.cs hat vorbestehend kaputte Umlaut-
Bytes. Alle Aenderungen liefen ueber ISO-8859-1 hin und zurueck.
Gegengeprueft: 660 Nicht-ASCII-Bytes vorher wie nachher, git-Diff genau
46 Zeilen. (Eine Zwischenmessung zeigte 3 Bytes Differenz - das war ein
BOM, das PowerShells ">" beim Vergleichs-Dump selbst hinzugefuegt hat,
kein Schaden an der Datei. Merke: zum Byte-Vergleich cmd /c nutzen.)

### BEIM NAECHSTEN TEST (das ist der offene Punkt!)
V5.29 ist gebaut und deployt, aber NICHT in-game getestet.
1. "Version 5 Punkt 29 bereit" muss beim Start kommen.
2. Taste O mehrfach oeffnen/schliessen - kein Absturz.
3. Freundschaftsanfrage annehmen - kein Absturz.
4. Registerkarten durchschalten: es MUSS weiter "Gruppe", "Freunde",
   "Suche" sagen (die echten Spiel-Labels).
   WENN stattdessen "Gruppenmitglieder"/"Freundesliste"/"Schwarze Liste"
   kommt, greift die Fallback-Liste - dann war der Text zum Lesezeitpunkt
   noch nicht da. Kein Absturz, aber im Log steht dann
   "Label aus Fallback-Liste" statt "Label aus ButtonTextNode".
   Das waere der Hinweis, dass zusaetzlich ein Ladezustands-Gate noetig
   ist (AtkUnitBase.IsReady bzw. die VTable-Funktion IsFullyLoaded -
   beides existiert, beides bisher UNGENUTZT im Plugin).
5. Quer gegenpruefen, dass nichts stumm wurde: Ansagen haengen jetzt ALLE
   am neuen Leser. Wenn UI-Text flaechendeckend verstummt, zuerst
   MEMORY_BASIC_INFORMATION in AtkText.cs pruefen (Size = 48!).

Noch nicht committet, nicht releast. Letzter Release: v5.28.

---

## STAND (2026-07-20, V5.28: HP/MP-Toene)

### RELEASE v5.28 VEROEFFENTLICHT (2026-07-20 ~11:27)
Commit bdc8452 (V5.28) nach origin/main gepusht. GitHub-Release v5.28
mit 4 Assets: latest.zip (544299 B), FF14Accessibility-v5.28.0.zip,
FF14AccessibilityInstaller.exe, installer.json. Alle vier state=uploaded,
v5.28 ist "Latest". repo.json auf 5.28.0.0.
- repo.json BYTE-SICHER geaendert (ISO-8859-1 hin und zurueck, das bildet
  alle 256 Bytewerte bijektiv ab): Groesse 958 vorher wie nachher, GENAU
  EIN Byte geaendert. Die Datei hat vorbestehend kaputte Umlaute - nicht
  neu kodieren!
- Installer-EXE UNVERAENDERT aus release_v5.27 uebernommen (am Installer
  hat sich nichts geaendert, so bleibt der Sha256 in installer.json
  gueltig). Per Get-FileHash gegen installer.json geprueft: stimmt exakt.
- latest-Link per HEAD verifiziert: HTTP 200, 544299 B - passt zur ZIP.
- ZIP-Inhalt geprueft: Manifest 5.28.0.0, alle NAudio-DLLs + Tolk +
  nvdaControllerClient64 drin.
- dist/ ist in .gitignore, die Release-Artefakte gehoeren nicht ins Repo.
- MERKE: Invoke-WebRequest braucht hier -UseBasicParsing, sonst bricht es
  im NonInteractive-Modus ab.
- uia_test.ps1 weiterhin bewusst nicht committet.

### V5.28 KOMPLETT BESTAETIGT (2026-07-20)
User: "ok funktioniert" fuer das Grundfeature, danach noch einmal
"das funktioniert" fuer die beiden Nachbesserungen. Alles in-game
bestaetigt, nichts mehr offen ausser dem Log-Nachweis unten.

Zwei Nachbesserungen auf User-Wunsch, beide bestaetigt:
1. TONHOEHEN GETAUSCHT: HP ist jetzt der HOHE Ton (1046 Hz), MP der
   TIEFE (523 Hz). Vorher andersherum - der User hat beides gehoert und
   sich so entschieden.
2. TOENE NUR BEI FOKUSSIERTEM SPIELFENSTER. Quelle ist das Spiel selbst:
   Framework.WindowInactive, bool auf FieldOffset 6104 (ilspycmd-
   verifiziert, siehe game-api.md). Bewusst NICHT ueber die Windows-
   API GetForegroundWindow - das Spiel fuehrt das Flag ohnehin, eine
   zweite Wahrheitsquelle koennte davon abdriften.
   WICHTIG: im Hintergrund werden die Stufen WEITER GETRACKT, nur der
   Ton entfaellt. Sonst wuerde alles, was waehrend des Alt-Tab passiert
   ist, beim Zurueckkommen in einem Rutsch nachgepiept.
   NOCH ZU BELEGEN: was das Flag zur Laufzeit genau abdeckt (Alt-Tab,
   Minimieren, Overlay davor). Der Name legt es nahe, mehr nicht -
   deshalb loggt jeder Flankenwechsel eine [Vitals]-Zeile.

### V5.28 Grundfeature (vom User bestaetigt)
V5.27 ist vom User bestaetigt ("es passt"), inklusive des offenen
Wiederoeffnen-Falls. Neues Feature auf User-Wunsch: HP und MP
nicht-sprachlich hoerbar machen.

VitalsService.cs (neu) - bewusst NICHT in CueService, weil die Pieptoene
laut User spaeter durch echte Sounds ersetzt werden; dann aendert sich
nur dieser eine Service. Austauschpunkt ist die Methode PlayTone.

REGEL: bei jedem Ueberschreiten einer 10-Prozent-Stufe ein kurzer Ton
(90 ms). Beide Richtungen - Schaden UND Heilung/Regeneration.
- STEREO-POSITION = FUELLSTAND (User-Entscheid, nach kurzem Probieren
  am 2026-07-20 umgedreht): 100% = ganz RECHTS, 50% = Mitte,
  0% = ganz LINKS. Schaden wandert nach links, Heilung nach rechts.
  Equal-Power-Pan, uebernommen aus BeaconService.
- HP = 1046 Hz (C6, hoch), MP = 523 Hz (C5, tief). Genau eine Oktave,
  damit die beiden Balken sich nicht verwechseln lassen. Beide NICHT auf
  den Beacon-Frequenzen (880/440/220) und nicht auf den Routen-Cues
  (990-1568).
- GILT IMMER, auch ausserhalb des Kampfes (User-Entscheid): gerade die
  Regeneration nach dem Kampf soll hoerbar sein. Die gesprochenen
  HP-Schwellen in CombatService bleiben unveraendert daneben bestehen -
  Sprache unterbricht, diese Toene nicht.

DREI FALLEN, die im Code adressiert sind:
1. HYSTERESE 2 Prozentpunkte (StepFor). Ohne sie koennte ein Wert direkt
   auf einer Stufengrenze - Regen-Tick gegen Schadens-Tick - zwischen
   zwei Stufen hin und her rattern und dauerpiepen. Sonderfall 100%:
   ueber der Obergrenze ist kein Platz mehr fuer die Hysterese.
2. BASELINE STILL bei Login, Zonenwechsel, MaxHp/MaxMp==0 (Ladebildschirm,
   Jobs ohne Mana). Stufe -1 = "noch kein Vergleichswert", der naechste
   Frame setzt nur, sagt nichts. Sonst klingt jeder Ladebildschirm wie
   ein voller Balken Schaden.
3. EIN Ton pro Sprung, nicht einer pro uebersprungener Stufe. Ein Treffer
   von 100% auf 30% gibt einen Ton bei 30%, keine Salve.

HP und MP koennen im selben Frame beide eine Stufe wechseln, deshalb hat
der Provider eine kleine Warteschlange (max 4, 40 ms Luecke) statt den
laufenden Ton zu ueberschreiben - sonst verschluckt einer den anderen.

Config neu: AnnounceVitalCues (bool), VitalCueVolume (0.4). Build 0/0,
deployt als 5.28.0.0 (Manifest gegengeprueft), csproj + Plugin.cs
synchron. ACHTUNG csproj: Version UND AssemblyVersion UND FileVersion -
Dalamud vergleicht die AssemblyVersion, die stand erst noch auf 5.27.
Auto-Deploy nach devPlugins laeuft NUR bei -c Debug, nicht Release.

### Beim naechsten Test (V5.28)
1. "Version 5 Punkt 28 bereit".
2. Schaden nehmen: Toene muessen mit sinkender HP nach LINKS wandern.
3. Danach stehen bleiben und regenerieren: dieselben Toene wandern
   wieder nach RECHTS zurueck.
4. Einen Zauber wirken: der MP-Ton ist deutlich TIEFER als der HP-Ton.
5. Ladebildschirm/Zonenwechsel: darf KEINE Toene ausloesen.
6. Alt-Tab in ein anderes Fenster, waehrend HP sich aendern: es darf
   NICHTS zu hoeren sein - auch nicht nachtraeglich beim Zurueckkommen.
   Im Log muss "[Vitals] Spielfenster im Hintergrund" stehen.
7. Im Log stehen [Vitals]-Zeilen mit alter und neuer Stufe (Debug-Level).

## ARCHIV V5.27 (2026-07-20, Tooltip-Sonde)

DER SHEET-WEG AUS V5.20 IST WIDERLEGT - durch die eigenen Daten. Die
events-Sonde hat geliefert, und zwar gegen die Hypothese:

- Die Maus-Events tragen an JEDEM Knopf in JEDEM Fenster dieselbe feste
  Serie param=256..260 (Character id=3 wie Social id=5). Das Addon-Sheet
  macht daraus jedes Mal "Attacke/Verteidigung/Praezision/Ausweichen/
  MAGIE" - fuenf identische Woerter, egal welcher Knopf. Also eine feste
  Event-Serie, kein Bezeichner.
- FocusStart param=3 erscheint in FriendList, PartyMemberList und
  TelepotTown gleichermassen.
- Die ListItem*-Events tragen den Zeilenindex (2 bzw. 4), keinen Namen.

Warum es bei _MainCommand (V5.18) trotzdem ging: DIESE Knoepfe oeffnen
per Definition MainCommands, ihr ButtonClick-param IST die Sheet-Zeile.
Im Charakter-Fenster ist param=20 nur ein hausinterner Callback-Index.
Blind uebernommen haette der Knopf "Tastenbelegung" geheissen.

BRAUCHBAR BLEIBT: der ButtonClick-param ist eine STABILE Knopf-Kennung
(Character/Comp1013 -> 20, Social/Comp1006 -> 4). Nur ohne Namen.

### SONDE BESTAETIGT (Log 2026-07-20 09:31, 241 Aufrufe)
AttachTooltip laeuft BEIM AUFBAU, nicht beim Hovern. Das Oeffnen des
Charakter-Fensters allein lieferte alle Namen im Klartext, in
Landessprache, vom Spiel selbst:
- Fenster 125 (Character), 4 Knoepfe: "Ausruestung optimieren",
  "Projektionsplatte", "Liste der Ausruestungssets", "Aktualisieren"
- 5 CheckBoxen: "Ausruestung am Kopf anzeigen", "Einstellungen fuer
  Ausruestung am Kopf", "Weggesteckte Waffen/Werkzeuge anzeigen",
  "Waffe ziehen/wegstecken", "Gesichtsaccessoires"
- dazu "Zurueck zur Frontalansicht", "Stick bewegt Charaktermodell"
- Fenster 126 (Attribute): ALLE 22 Attributs-Erklaerungen im Volltext
  ("Konstitution: Beeinflusst die Hoehe der maximalen Lebenspunkte.")

ENTSCHEIDEND: die vom Hook gemeldeten Node-Zeiger sind DIESELBEN, auf
denen der Tastatur-Fokus sitzt (Buttons id=3, CheckBoxen id=4 - exakt
was die [Focus] STUMM-Zeilen meldeten). Zuordnung direkt ueber Zeiger,
ohne Raterei.

### RELEASE v5.27 VEROEFFENTLICHT (2026-07-20 ~10:05)
Commit 791ca1d (V5.26+V5.27) nach origin/main gepusht. GitHub-Release
v5.27 mit 4 Assets: latest.zip (541328 B), FF14Accessibility-v5.27.0.zip,
FF14AccessibilityInstaller.exe, installer.json. repo.json auf 5.27.0.0
(byte-sicher ersetzt - die Datei hat vorbestehend kaputte Umlaute, nicht
neu kodieren!). latest-Link per HEAD verifiziert: HTTP 200, 541328 B.
Installer-EXE UNVERAENDERT aus release_v5.25 uebernommen (am Installer
hat sich nichts geaendert, so bleibt der Sha256 in installer.json
gueltig - per Get-FileHash gegengeprueft, stimmt exakt).
uia_test.ps1 weiterhin bewusst nicht committet.

### V5.27 BESTAETIGT (User "ok funktioniert" + Log 2026-07-20 09:58/09:59)
- "[Tooltip] Hooks aktiv" um 09:58:34.
- Gesprochen: "Liste der Ausruestungssets", "Aktualisieren",
  "Ausruestung optimieren".
- HARTE ZAHL: vorher 41 STUMM-Faelle im Charakter-Fenster, nachher
  GENAU EINER - und der ist addon='?' id=0, das Uebergangs-Artefakt
  beim Fensterwechsel (gehoert zu keinem Addon). Kein echter Knopf
  ist mehr stumm.

NOCH NICHT BELEGT: das Wiederoeffnen des Fensters (Detach-Bereinigung).
Alle Ansagen lagen in einem 14-Sekunden-Fenster, aus dem Log ist kein
zweiter Aufbau ableitbar. Beim naechsten Mal einmal schliessen +
wieder oeffnen; kaemen dort falsche Namen, ist es der einzige noch
offene Fehlerpfad.

### V5.27 UMSETZUNG
ROOT-CAUSE-KORREKTUR zu V5.19/V5.20: AttachTooltip und ShowTooltip sind
ZWEI Funktionen (ilspycmd-verifiziert, AtkTooltipManager).
- ShowTooltip zeigt das FENSTER - nur bei Maus-Hover. Genau das hat die
  V5.19-Sonde gemessen, und daraus faelschlich "kein Text" geschlossen.
- AttachTooltip BINDET Text an einen Node - vermutlich beim Aufbau des
  Addons, also lange vorher und unabhaengig von der Maus.

Wenn die Bindung beim Aufbau passiert, steht der Name jedes Icon-Knopfes
die ganze Zeit im Speicher: vom Spiel geliefert, in Landessprache, fuer
ALLE Fenster gleich. Keine Handarbeit, keine Tabelle.

HYPOTHESE, NICHT BELEGT: dass Addons AttachTooltip beim Aufbau rufen.
Folgt aus Benennung + Funktionstrennung, mehr nicht. Die Sonde klaert es.

TooltipService.cs (ersetzt die Sonde TooltipProbeService): haelt eine
LIVE-Zuordnung Node-Zeiger -> Text, drei Hooks (Attach / Detach /
DetachByAddonId).

WARUM LIVE UND NICHT FESTE TABELLE: die Zeiger wechseln bei jedem
Neuaufbau des Fensters - im Sonden-Log stehen zwei verschiedene Saetze
fuer dieselben neun Knoepfe. Eine feste Zeiger-Tabelle wuerde nach dem
ersten Wiederoeffnen falsche Namen liefern. Detach wird deshalb
mitgehookt: ein freigegebener Zeiger fliegt sofort raus, ein recycelter
erbt nie den Namen seines Vorgaengers. Fuer einen blinden Spieler ist
das der Unterschied zwischen Stille und dem falschen Fenster.

REIN LESEND: jeder Detour merkt sich und reicht an Original weiter.
Keine synthetischen Maus-Events, kein erzwungener Tooltip.
Text nur bei gesetztem Text-Flag gelesen (AtkTooltipArgs ist eine UNION,
alle Varianten auf Offset 0 - sonst wuerde eine Id als Zeiger gedeutet).

UIReaderService: im Fokus-Pfad fragt der Leser TryGetTooltipDeep (bis 3
Eltern hoch, weil der Fokus oft auf dem Collision-KIND sitzt). RANGFOLGE
bewusst: echter Node-Text > Tooltip > Positions-Notbehelf. Damit aendert
sich NICHTS an dem, was heute schon spricht - der Tooltip springt nur
dort ein, wo bisher Stille war. TryGetTooltip gibt null zurueck wenn
nichts bekannt ist; der Leser schweigt dann, statt zu raten.
Plugin.cs: IGameInteropProvider neu als PluginService.

Build 0/0, deployt (5.27.0.0), csproj + Plugin.cs synchron.

### Beim naechsten Test (V5.27)
1. "Version 5 Punkt 27 bereit".
2. Im Log muss "[Tooltip] Hooks aktiv (Attach/Detach/DetachByAddon)."
   stehen. Fehlt sie, steht dort "[Tooltip] Hooks fehlgeschlagen" + Grund.
3. Charakter-Fenster oeffnen, mit den Pfeiltasten ueber die Icon-Knoepfe
   oben laufen. Erwartung: "Aktualisieren", "Ausruestung optimieren",
   "Projektionsplatte", "Liste der Ausruestungssets" usw. werden gesagt.
4. Fenster SCHLIESSEN und ERNEUT OEFFNEN, nochmal drueberlaufen. Das ist
   der eigentliche Pruefpunkt: kommen dieselben Namen? Wenn hier falsche
   Namen kaemen, greift die Detach-Bereinigung nicht.
5. Attribute-Fenster: die Erklaerungstexte sollten jetzt lesbar sein.
6. Weitere Fenster mit Icon-Knoepfen (Social, Teleport) gegenpruefen.
7. Alles was danach noch stumm ist, steht als "[Focus] STUMM" im Log -
   die Diagnosezeile bleibt bewusst drin.

### Nebenfund vom 20.07., noch nicht angefasst
Stumm sind nicht nur Icon-Knoepfe, sondern auch LISTENZEILEN in
FriendList, PartyMemberList und TelepotTown (Teleport-Fenster). Eigener
Bug, praktisch vermutlich wichtiger als die Icon-Knoepfe. User gefragt,
ob vorziehen - noch keine Antwort.

---

## STAND (2026-07-19, V5.26: Bestiarium-Sonde entfernt)

ProbeBestiaryRow ist RAUS (User-Wunsch) - samt Aufruf in
OnMonsterNoteUpdate. Sie hatte ihren Zweck erfuellt: sie hat die
Deklinations-Namen aufgedeckt. Ab jetzt ist das Bestiarium-Log ruhig.

Rein subtraktiv, kein Verhalten geaendert: die Ansage selbst laeuft
unveraendert. `out var item` wurde zu `out _`, weil das Item NUR die
Sonde brauchte. Build 0/0 - kein toter Code uebrig.

BEHALTEN: ProbeMiss in BestiaryService. Die meldet kuenftige
Namens-Faelle von selbst ("[Bestiary] MISS ..."), sonst faellt ein
fehlender Lebensraum nur als Stille auf.

Build 0/0, deployt (5.26.0.0). NOCH NICHT RELEASET - v5.25 ist der
veroeffentlichte Stand.

### Beim naechsten Test (V5.26)
1. "Version 5 Punkt 26 bereit".
2. Bestiarium einmal durchlaufen: Ansagen muessen unveraendert sein
   (Monster + Fundort). Im Log duerfen KEINE "[Bestiary] Probe"-Zeilen
   mehr stehen.

### OFFEN, vom User auf spaeter verschoben (2026-07-19)
Die STUMMEN ICON-KNOEPFE (V5.20, Abschnitt weiter unten). Stand dort:
- Tooltip-Weg ist WIDERLEGT (Spiel oeffnet Tooltips nur bei Maus-Hover,
  nicht bei Tastatur-Fokus) - belegt, nicht vermutet.
- AddonCharacter traegt keine benannten Knopf-Felder (ilspycmd).
- V5.20 loggt als naechsten Versuch `events=[...]` pro stummem Knopf.
  DAFUER FEHLT NOCH DAS TEST-LOG. Ohne das geht es nicht weiter.
- Beim Test zwingend dazu: welche Knoepfe gibt es in dem Fenster
  WIRKLICH und in welcher Reihenfolge? Nur der Abgleich mit der
  Wirklichkeit beweist die Zuordnung, das Sheet allein tut es nicht.
- Falls `events=[]` leer bleibt, ist auch dieser Weg tot; dann bliebe
  nur, ein MouseOver an den Knopf zu schicken - ein Eingriff, der
  ausdrueckliches OK braucht.

Ausserdem offen (klein): V5.26 ist gebaut und deployt, aber NICHT
committet und NICHT releast. v5.25 ist der veroeffentlichte Stand.

---

## STAND (2026-07-19, V5.25 RELEASET - alles bestaetigt)

v5.25 ist veroeffentlicht. ALLE offenen Testpunkte sind abgehakt:

1. HP/MP auf Strg+Entf BESTAETIGT (Log 19:30:48 'HP 100 Prozent, MP 100
   Prozent.', KEIN RecipeNote danach).
2. Zaehler am Ende BESTAETIGT (Log 19:30:51 'Vanille Farron, Spieler,
   direkt neben dir, rechts, 1 von 138.').
3. Bestiarium-Fundort BESTAETIGT (Log 19:18:04/05):
     'Rostiger Kobalos, 3 von 3. Lebt in Westliches Thanalan, Haemmerweide'
     'Gefraessiger Yarzon, 3 von 3. Lebt in Westliches Thanalan, Haemmerweide'
   Keine MISS- und keine MEHRDEUTIG-Zeile mehr - die Platzhalter-
   Aufloesung greift und sie greift eindeutig.
4. Bestiarium-Filter + Rang-Auswahl BESTAETIGT (siehe V5.23-Abschnitt).

### Aufraeum-Kandidat fuers naechste Mal
`ProbeBestiaryRow` in UIReaderService loggt bei JEDEM Zeilenwechsel im
Bestiarium alle Textknoten ("[Bestiary] Probe rendererId=..."). Die Sonde
hat ihren Zweck erfuellt (sie hat die Deklinations-Namen aufgedeckt) und
ist jetzt nur noch Log-Ballast. Kann raus, sobald ohnehin an der Stelle
gearbeitet wird. Dasselbe gilt fuer ProbeMiss in BestiaryService -
DIESE aber besser BEHALTEN: sie meldet kuenftige Namens-Faelle.

### Nicht im Repo, absichtlich
`uia_test.ps1` liegt im Arbeitsverzeichnis, soll dort bleiben, gehoert
aber nicht ins Repo (User 2026-07-19). Nicht committen, nicht loeschen.

---

## STAND (2026-07-19, V5.25: HP-Ansage weg von Strg+H)

USER-MELDUNG: Strg+H (HP/MP) funktioniert nicht mehr, seit das
Handwerker-Notizbuch da ist.

URSACHE IM LOG BEWIESEN (nicht vermutet), ein einziger Tastendruck:
  19:19:00.837  [Speak] 'HP 100 Prozent, MP 100 Prozent.'
  19:19:00.850  RecipeNote Fokus: HANDWERKER-NOTIZBUCH
  19:19:00.850  [Speak] 'HANDWERKER-NOTIZBUCH'
Die HP-Ansage FEUERT also - sie wird 13 ms spaeter von der
Fenster-Ansage (SpeakInterrupt) abgeschnitten. Fuer den User klingt das
wie "geht nicht".

Da IsJustPressed exakt auf gedruecktes Strg prueft, MUSS Strg gehalten
worden sein - das Spiel loeste MENU_CRAFT (Grundtaste H) trotzdem aus.
Der Keybind-Dump listet Strg+H zwar als "frei"; das Spiel wertet hier
aber nur die Grundtaste.

OFFEN GEBLIEBEN (ehrlich): Strg+L laeuft sauber (Log 19:16:49, 19:18:35,
19:18:42 "Stufe 11 ..."), obwohl L = MENU_LINKSHELL ist. VERMUTUNG: der
User hat keine Linkshell freigeschaltet, das Notizbuch aber schon -
passt zu "seit man das Notizbuch HAT". NICHT belegt.

FIX (User-Wahl): HP/MP liegt jetzt auf **Strg+Entf**. Entf taucht im
Keybind-Dump NIRGENDS auf - damit kann sich kein Spielfenster
dazwischenschieben. Config-Migration V6->V7 stellt alte Konfigurationen
automatisch von "Strg+H" um; KeyNameToVK kennt jetzt "Entf" (VK 0x2E);
Hilfetext angepasst.

VERWORFEN und WARUM: Umschalt+Numpad3 kommt bei aktivem NumLock gar nicht
beim Plugin an (steht schon in Migration V5->V6). Strg+Umschalt+H waere
wirkungslos - die Grundtaste H bleibt das Problem. Einfg ist NVDAs
eigene Modifiertaste.

Enthaelt V5.24 (Zaehler ans Ende), siehe unten. Beides noch ungetestet.
Build 0/0, deployt (5.25.0.0). Versionen csproj + Plugin.cs synchron.

### Beim naechsten Test (V5.25)
1. "Version 5 Punkt 25 bereit".
2. Strg+Entf: muss HP und MP ansagen, OHNE dass ein Fenster aufgeht.
3. Gegenprobe: Strg+H darf jetzt nichts mehr ausloesen (ausser dem
   Notizbuch, das ist die Spiel-Taste).
4. Offen aus V5.24: Taste N - Name zuerst, "1 von 30" am Schluss?
5. Offen aus V5.23: sagen "Gefraessiger Yarzon" und "Rostiger Kobalos"
   jetzt ihren Fundort?

---

## STAND (2026-07-19, V5.24: Zaehler ans ENDE der Ansage)

USER-WUNSCH: bei den NPC-Ansagen "und so" soll das "1 von 30" nicht mehr
vorne stehen, sondern zum Schluss. Der Name ist das, worauf der User
wartet - der Zaehler haelt ihn nur auf.

GEAENDERT - Objekt-Browser (Taste N) und die gleichartigen Zykel-Browser:
  vorher: "1 von 30: Ulta, NPC, 12 Meter, vorne links."
  jetzt : "Ulta, NPC, 12 Meter, vorne links, 1 von 30."

Betroffen:
  - NavigationService: Objekte/NPCs, Quest-Ziele, Wegpunkte (Taste N)
  - EmoteService, HotbarService, MessageHistoryService,
    DalamudPluginsService - gleiches Muster, mitgezogen damit es
    ueberall gleich klingt

DETAIL Objekt-Browser: die Warnung "Achtung, nicht anvisiert" bleibt GANZ
am Schluss, hinter dem Zaehler - eine Warnung soll das letzte sein, was
haengen bleibt.
DETAIL Quest-Ziele: der Zaehler wird erst nach den Wegbeschreibungen
("Dorthin ueber ...") angehaengt, sonst stuende er mitten im Satz.

NICHT ANGEFASST: die Listen-Navigation in SPIELMENUES (UIReaderService).
Das ist ein anderer Mechanismus, und der Wunsch bezog sich auf die
Objekt-Ansagen. Im Bestiarium ist der Zaehler seit V5.21 ohnehin weg.
Falls es dort auch stoert, ist es dieselbe Umstellung.

Build 0/0, deployt (5.24.0.0). Versionen csproj + Plugin.cs synchron.
NICHT releast - v5.23 ist der letzte veroeffentlichte Stand.

### Beim naechsten Test (V5.24)
1. "Version 5 Punkt 24 bereit".
2. Taste N druecken: Name muss ZUERST kommen, "1 von 30" am Schluss.
3. Ebenso pruefen: Quest-Ziele und Wegpunkte (Kategorie mit Strg+N),
   Emotes, Faehigkeiten, Nachrichtenverlauf, Plugin-Liste.
4. Offen aus V5.23 (noch ungetestet): sagen "Gefraessiger Yarzon" und
   "Rostiger Kobalos" im Bestiarium jetzt ihren Fundort?

---

## STAND (2026-07-19, V5.23: Deklinations-Namen aufgeloest)

V5.21-FILTER BESTAETIGT (Log 18:27:34-18:28:11): Im Bestiarium wurden
NUR Monster gesprochen - keine "Verguetung", keine "Thaumaturg 01".
GEGENPROBE EBENFALLS BESTANDEN: die Rang-Auswahl sagt weiter an
("1, 5 von 10", 18:27:34 und 18:28:11). Die befuerchtete Regression ist
ausgeblieben.

SONDE HAT GELIEFERT (Log 18:27:41 / 18:27:44) - Ursache belegt, nicht
mehr vermutet:
  MISS 'gefraessiger yarzon' -> Sheet: 'gefraessig[a] yarzon'
  MISS 'rostiger kobalos'    -> Sheet: 'rostig[a] kobalos'
`[a]` ist ein Platzhalter fuer die Adjektivendung, den das Spiel je nach
Fall einsetzt. Der UI-Name kann darum NIE woertlich passen. Die frueher
vermutete Erklaerung (ExtractText liefere nur den Wortkern) war FALSCH -
der Platzhalter bleibt im Text stehen.

FIX (BestiaryService): Sheet-Namen mit Platzhalter werden zu verankerten
Mustern - "gefraessig[a] yarzon" -> ^gefraessig\w*\ yarzon$. Greift erst
NACH dem exakten Lookup. Bei MEHR als einem passenden Muster wird
NICHTS gesagt und "[Bestiary] MEHRDEUTIG" geloggt: ein falscher
Lebensraum schickt den User in die falsche Zone, das waere schlimmer als
Stille.

MUSTER GEGEN DIE ECHTEN LOG-NAMEN GEPRUEFT (PowerShell-Regex, alle
Kandidaten aus der Sonde): 'gefraessiger yarzon' und 'gefraessige
yarzon' treffen; 'aas-yarzon', 'wald-yarzon', 'kupfer-kobalos',
'blei-kobalos' werden korrekt abgelehnt. Der Wortstamm muss weiter
passen, die Wildcard deckt nur die Endung.

Build 0/0, deployt (5.23.0.0). Versionen csproj + Plugin.cs synchron.

### Beim naechsten Test (V5.23)
1. "Version 5 Punkt 23 bereit".
2. Bestiarium oeffnen, ueber "Gefraessiger Yarzon" und "Rostiger
   Kobalos" laufen. Erwartet: beide sagen jetzt "Lebt in ...".
3. Im Log pruefen: Zeile "Lebensraum-Tabelle: N Monster ..., davon M mit
   Deklinations-Platzhalter" zeigt, wie verbreitet das Muster ist.
   Bleiben "MISS"- oder "MEHRDEUTIG"-Zeilen uebrig, gibt es noch einen
   zweiten Namens-Fall - dann weiter mit denselben Daten arbeiten.

---

## STAND (2026-07-19, V5.22: Sonde fuer fehlende Lebensraeume)

USER-MELDUNG: nicht bei allen Monstern kommt der Fundort.

BELEG IM LOG (5 Treffer, 2 verschiedene Monster):
  [Bestiary] Kein Lebensraum fuer 'Gefraessiger Yarzon'
  [Bestiary] Kein Lebensraum fuer 'Rostiger Kobalos'
Beide tragen ein VORANGESTELLTES ADJEKTIV - Muster, kein Zufall. An
Gross-/Kleinschreibung liegt es nicht, der Lookup normalisiert bereits
auf ToLowerInvariant.

ilspycmd-VERIFIZIERT (2026-07-19, Lumina.Excel.Sheets.BNpcName): das
Sheet hat NUR `Singular`/`Plural` als Text; `Adjective`, `Article`,
`Pronoun`, `StartsWithVowel` sind sbyte-GRAMMATIKCODES, kein Text. Der
Name kann also nur aus `Singular` kommen.

HYPOTHESE, NOCH NICHT BELEGT: deutsche Namen tragen die Deklination als
eingebettete SeString-Bausteine, und `ExtractText()` liefert davon nur
den Kern ("Yarzon" statt "Gefraessiger Yarzon"). NICHT als Fix gebaut -
ein falsch zugeordneter Lebensraum schickt den User in die falsche Zone.

WAS NEU IST (nur Diagnose, kein Verhalten geaendert): BestiaryService.
ProbeMiss loggt bei jedem Fehlschlag die Sheet-Eintraege, die ein Wort
(>=4 Zeichen) mit dem UI-Namen teilen:
  [Bestiary] MISS 'gefraessiger yarzon' - N Sheet-Kandidat(en): '...'
Die ANZAHL entscheidet ueber den Fix: genau 1 Kandidat = eindeutig, ein
gelockerter Abgleich waere sicher. Mehrere ("rostiger Kobalos" vs.
"eisiger Kobalos" mit verschiedenen Zonen) = gelockerter Abgleich waere
gefaehrlich, dann braucht es einen anderen Weg.

Build 0/0, deployt (5.22.0.0). Versionen csproj + Plugin.cs synchron.
Enthaelt den V5.21-Filter (siehe unten), der noch UNGETESTET ist.

### Beim naechsten Test (V5.22)
1. "Version 5 Punkt 22 bereit".
2. Bestiarium oeffnen, durch die Liste laufen - moeglichst ueber die
   Monster MIT Adjektiv im Namen. Dann Log schicken; entscheidend sind
   die "[Bestiary] MISS ..."-Zeilen.
3. Weiterhin offen aus V5.21 (mitpruefen): nur Monster werden gesprochen?
   Und laesst sich der RANG noch wechseln und wird er angesagt?

---

## STAND (2026-07-19, V5.21: Bestiarium zeigt nur noch Monster)

USER-WUNSCH: im Bestiarium nur die Monster und ihren Fundort hoeren, die
uebrigen Zeilen irritieren.

BEFUND AUS DUMP + LOG (nicht geraten): Die TreeList von `MonsterNote`
mischt drei Zeilentypen, im Log 17:38:48-52 alle belegt:
  - Comp(1015) Rang-Ueberschrift -> "1 von 30, Erledigt!, Thaumaturg 01"
  - Comp(1017) MONSTER           -> "2 von 30, Marienkaefer, 3 von 3.
                                     Lebt in Zentrales La Noscea, ..."
  - Comp(1018) Verguetung        -> "3 von 30, 75, Verguetung"

Der Fundort lief schon vorher: BestiaryService laedt 403 Monster aus dem
Sheet `MonsterNoteTarget`. Es fehlte nur der Filter.

GEAENDERT (OnMonsterNoteUpdate): Nicht-Monster-Zeilen INNERHALB der
TreeList bleiben stumm (User-Entscheid: komplett stumm, kein Signalton).
Positionspraefix "x von 30" entfaellt bei Monstern - die 30 zaehlte die
weggefilterten Zeilen mit.

WICHTIGE ABGRENZUNG, sonst waere die Rang-Auswahl kaputtgegangen: Die
Rang-Zeilen ("1, 2 von 10", Log rendererId=2) liegen AUSSERHALB der
TreeList und kommen mit index<0 an. Die werden weiter angesagt - sonst
liesse sich kein Rang mehr waehlen. Filter greift nur bei index>=0.

Ebenso gefiltert: AnnounceBestiaryOverview (Uebersichtstaste) liest jetzt
nur noch Monster, sagt "Bestiarium, N Monster".

Build 0/0, deployt (5.21.0.0). Versionen csproj + Plugin.cs synchron.

### Beim naechsten Test (V5.21)
1. "Version 5 Punkt 21 bereit".
2. Bestiarium oeffnen, mit den Pfeiltasten durch die Liste. Erwartet:
   nur Monster werden gesprochen ("Marienkaefer, 3 von 3. Lebt in ..."),
   dazwischen Stille wo Rang-Ueberschriften und Verguetungen liegen.
3. GEGENPROBE: laesst sich der RANG noch wechseln und wird er angesagt?
   Das ist die Stelle, die durch den Filter kaputtgehen koennte.
4. Uebersichtstaste im Bestiarium: sollte "Bestiarium, N Monster" sagen.

---

## STAND (2026-07-19, V5.20: Icon-Knoepfe - Tooltip tot, Events dran)

DIE TOOLTIP-IDEE AUS V5.19 IST WIDERLEGT. Sauber, mit Daten:
alle 20 stummen Faelle im Log (15:45:18-33) tragen `tooltip=[]`. Das ist
NICHT "leerer Text" - es war ueberhaupt KEIN Tooltip-Fenster sichtbar,
waehrend der Tastatur-Fokus auf den Knoepfen sass (die Sonde listet
sichtbare Fenster auch dann, wenn ihr Text leer ist - die Liste war ganz
leer). Das Spiel oeffnet Tooltips bei MAUS-Hover, nicht bei
Tastatur-Fokus. Es wurde nie eine falsche Beschriftung gesprochen.

ZWEITE TUER ZU: `AddonCharacter` existiert in FFXIVClientStructs (anders
als AddonMainCommand), traegt aber nur TabIndex und TabCount - keine
benannten Knopf-Felder. ilspycmd-verifiziert 2026-07-19.

DER DUMP BESTAETIGT DIE LAGE ENDGUELTIG: die stummen Knoepfe sind
Comp(1010)/(1011)/(1013)/(1015) = Buttons und Comp(1017)-(1021) =
CheckBoxen. Jeder traegt ausschliesslich einen Collision- und einen
Image-Knoten. Kein Text, nirgends, `nachbarn=[]` ebenfalls leer.

USER-PRAEZISIERUNG: es geht um die ICON-KNOEPFE ("wie aktualisieren und
so"), NICHT um die Ausruestungsplaetze. Solche Knoepfe gibt es in vielen
Fenstern - deshalb zaehlt ein GENERISCHER Weg mehr als eine Loesung nur
fuer das Charakter-Fenster.

WAS NEU IST (nur Diagnose, kein Verhalten geaendert): Die STUMM-Zeile
nennt statt `tooltip=[...]` jetzt `events=[...]` - jedes am Knopf und
seinen Eltern registrierte Event mit seinem Parameter, und zu jedem
Parameter die Zeile, die er in den Sheets `Addon` (das UI-Beschriftungs-
Sheet des Spiels) und `MainCommand` treffen wuerde.

WARUM DIESER WEG: Es ist exakt der, der sich in V5.18 BEWAEHRT hat. Die
_MainCommand-Knoepfe waren genauso textlos, und ihr ButtonClick-Parameter
war die MainCommand-Sheet-Zeile - damals durch drei unabhaengige Proben
bestaetigt. Beide Kandidaten-Sheets werden geloggt, damit die DATEN
entscheiden, welches passt, statt dass ich eines vorab rate.

BEWUSST NOCH KEINE ANSAGE: Ein falscher Name schickt einen blinden
Spieler ins falsche Fenster - schlimmer als Stille. Erst wenn die
Zuordnung gegen das steht, was die Knoepfe TATSAECHLICH tun, wird
gesprochen. Dann ist es ein Zweizeiler.

Build 0/0, deployt (5.20.0.0). Versionen csproj + Plugin.cs synchron.

### Beim naechsten Test (V5.20)
1. "Version 5 Punkt 20 bereit".
2. Charakter-Fenster oeffnen, mit den Pfeiltasten ueber die Icon-Knoepfe
   oben laufen (die stummen), dann das Log schicken. Entscheidend sind
   die "[Focus] STUMM ... events=[...]"-Zeilen.
3. WICHTIG dazu, sonst ist das Log nicht auswertbar: Sag mir, welche
   Knoepfe es in dem Fenster WIRKLICH gibt und in welcher Reihenfolge -
   also welcher davon "Aktualisieren" ist. Nur der Abgleich mit der
   Wirklichkeit beweist die Zuordnung; das Sheet allein tut es nicht.
4. Gern auch in ANDEREN Fenstern mit Icon-Knoepfen durchlaufen. Je mehr
   stumme Faelle im Log, desto sicherer wird der generische Weg.
5. Falls "events=[]" leer bleibt: die Knoepfe tragen gar keine
   registrierten Events - dann ist auch dieser Weg tot und es bleibt nur
   noch, ein MouseOver an den Knopf zu schicken, damit das Spiel seinen
   eigenen Tooltip fuellt. Das waere ein Eingriff und braucht dein OK.

---

## STAND (2026-07-19, V5.19: Sonde fuer das Charakter-Fenster)

V5.18 BESTAETIGT (User: "das erste menue funktioniert jetzt") - die
Namensansage im _MainCommand-Menue laeuft.

NEUER DUMP: `Character`, 83 Knoten (Desktop\FFXIV_UI_Dump.txt, 15:41). Das
ist das Charakter-/Ausruestungsfenster.

WAS DARIN SCHON LESBAR IST (Textknoten vorhanden): Titel "CHARAKTER",
Name "Perrox Torran", Klasse "Goldschmied", "Stufe 3", "Ausruestungsset",
und die vier Registerkarten als RadioButtons MIT Text - Attribute, Profil,
Klassen/Jobs, Ansehen (letztere unsichtbar geschaltet).

WAS STUMM IST - und das ist im Log belegt, nicht vermutet:
  [Focus] STUMM addon='Character' id=3 typ=8 eltern=[14:1013 ...] nachbarn=[]
  [Focus] STUMM addon='Character' id=4 typ=8 eltern=[73:1021 ...] nachbarn=[]
  [Focus] STUMM addon='Character' id=4 typ=8 eltern=[74:1018 ...] nachbarn=[]
Der Fokus lief ueber die ICON-KNOEPFE oben (Comp 1010-1021: Buttons und
Checkboxen). Der Dump bestaetigt warum: die tragen ausschliesslich
Collision- und Image-Knoten, keinen Text. `nachbarn=[]` wieder leer.
Dasselbe Muster wie _MainCommand - nur ohne dessen Rettungsanker, denn
diese Knoepfe haben keine MainCommand-Sheet-Zeile.

WARUM HIER NICHTS GEBAUT WURDE: Der naheliegende Weg ist der TOOLTIP - ein
Sehender erfaehrt genau so, was so ein Icon tut. Aber ob das Tooltip-Fenster
sich fuellt, waehrend der TASTATUR-Fokus auf dem Knopf sitzt, ist NICHT
belegt. Im Log steht dazu genau eine Zeile ("Addon: Tooltip") - und zwar
deshalb, weil V5.14 diese Fenster als HUD-Laerm stummgeschaltet hat. Es
gibt also schlicht keine Daten. Ein geratener Fix wuerde hier JEDEN
Icon-Knopf im Spiel betreffen.

WAS NEU IST (nur Diagnose, kein Verhalten geaendert): Die STUMM-Zeile
nennt jetzt zusaetzlich "tooltip=[...]" - Inhalt der offenen
Tooltip-Fenster (Tooltip / ItemDetail / ActionDetail) im Moment des
stummen Fokus. Faellt die Antwort positiv aus, loest das textlose
Icon-Knoepfe GENERISCH, im ganzen Spiel, nicht nur hier. Faellt sie
negativ aus, ist die Idee widerlegt, ohne dass je eine falsche
Beschriftung gesprochen wurde.

Build 0/0, deployt (5.19.0.0). Versionen csproj + Plugin.cs synchron.

### Beim naechsten Test (V5.19)
1. "Version 5 Punkt 19 bereit".
2. Charakter-Fenster oeffnen und mit den Pfeiltasten ueber die
   Icon-Knoepfe oben laufen, bis es stumm bleibt. Dann das Log schicken.
   Entscheidend sind die "[Focus] STUMM ... tooltip=[...]"-Zeilen.
3. GEGENPROBE, damit klar wird was schon geht: Auf den Registerkarten
   (Attribute / Profil / Klassen-Jobs) - werden die angesagt? Die haben
   echten Text, sollten also laufen.
4. Strg+F10 im offenen Charakter-Fenster: kommen Name, Klasse und Stufe?
5. ZUR ERINNERUNG, weil es schon existiert: Was du traegst, liest der
   EquipmentService komplett vor ("Waffe: ..., Kopf: ...") - dafuer
   braucht es das Fenster gar nicht.

### Noch ungeklaert
Die 13 Ausruestungsplaetze (DragDrop, Comp 1023/1024) sind ebenfalls
textlos. Der Weg dorthin waere Slot-Index -> EquippedItems -> Item-Sheet.
NICHT gebaut: welcher Knoten welchem Slot-Index entspricht, ist nicht
verifiziert, und ein verschobener Index benennt jedes Ruestungsteil falsch.
Klaert sich mit derselben Tooltip-Antwort - oder braucht eine eigene Sonde.

---

## STAND (2026-07-19, V5.18: Menue-Eintraege haben NAMEN)

Der User hat einen frischen Strg+F5-Dump gemacht (Desktop\FFXIV_UI_Dump.txt,
15:33) und die V5.16-Sonde hat im selben Moment geliefert. Damit ist die
Frage, an der V5.16/V5.17 haengengeblieben sind, ENTSCHIEDEN.

DER DUMP BESTAETIGT DIE DIAGNOSE ENDGUELTIG: `_MainCommand` hat 15 Knoten -
sieben Buttons (id=2..8) und sonst nur Image-Knoten. KEIN EINZIGER
Textknoten im ganzen Fenster. Es gab wirklich nichts zu lesen; die
Kletterhoehe war nie das Problem.

DIE ZUORDNUNG IST BEWIESEN, NICHT ANGENOMMEN. Die Sonde zeigte fuer alle
sieben Knoepfe je ein ButtonClick-Event (typ=25):
  id=2 param=1 -> Initiative          id=6 param=5 -> Timer
  id=3 param=2 -> Charakter           id=7 param=6 -> Errungenschaften
  id=4 param=3 -> Kommandoliste       id=8 param=7 -> Sammler-Notizbuch
  id=5 param=4 -> Archiv
Drei unabhaengige Belege, dass param die MainCommand-Sheet-Zeile ist:
1. Die Params laufen lueckenlos 1..7 parallel zu den Knopf-Ids.
2. Die aufgeloesten Namen ergeben zusammen eine echte, zusammenhaengende
   FFXIV-Menuegruppe - kein Zufallstreffer aus dem Sheet.
3. KREUZPROBE: id=2 steht in der Knotenliste GANZ HINTEN, bekommt durch die
   Z-Order-Umkehr Position 1 - und traegt param 1. Position und Sheet-Zeile
   laufen exakt parallel durch alle sieben. Die V5.17-Umkehrung und die
   Namenszuordnung bestaetigen sich damit gegenseitig.

WAS NEU IST: Der Fokus sagt jetzt "Charakter, 2 von 7" statt "2 von 7".
Der Name kommt aus dem ButtonClick-Event des Knopfes selbst (ueber
FindEventOfType, derselbe Pfad wie DispatchClick), nicht aus einer
Positionsindizierung des Sheets - ein Menue, das Eintraege gewinnt,
verliert oder umsortiert, sagt damit trotzdem richtig an. Fehlt Event oder
Sheet-Zeile, bleibt die reine Position: duenn, aber nie falsch.

SONDE ENTFERNT: ProbeMainCommandButton ist raus. Sie hat ihre Frage
beantwortet und lief entgegen ihrem eigenen Kommentar ("once per focus
change") pro FRAME - 40 identische Zeilen in einer Sekunde im Log.

OFFEN GEBLIEBEN: Welches Menue das aus Spielersicht ist, sagt weiterhin
niemand. Der User: "es ist nicht das hauptmenue". Jetzt weniger dringend -
die Eintraege nennen sich selbst beim Namen, ein Fenstertitel ist Kuer.

Build 0/0, deployt (5.18.0.0). Versionen csproj + Plugin.cs synchron.

### Beim naechsten Test (V5.18)
1. "Version 5 Punkt 18 bereit".
2. Das Menue oeffnen, mit den Pfeiltasten durch: kommt bei JEDEM Eintrag
   Name und Position ("Initiative, 1 von 7" ... "Sammler-Notizbuch,
   7 von 7")?
3. ENTSCHEIDEND: Stimmen die Namen mit dem ueberein, was der Eintrag
   tatsaechlich OEFFNET? Einmal draufdruecken und pruefen - ein falscher
   Name schickt dich ins falsche Fenster.
4. Stimmt die Reihenfolge (faengt es bei 1 an, nicht bei 7)?
5. Regressionstest aus V5.17: kommen in ANDEREN Menues die Eintraege
   weiter mit ihrem echten Namen? Der Namenszweig hier ist auf
   _MainCommand begrenzt, sollte also nichts anderes beruehren.

---

## STAND (2026-07-19, V5.17: V5.16 war eine REGRESSION - zurueckgebaut)

User-Meldung zu V5.16: "zum einen es ist falschrum es faengt mit 7 an und
es ist nicht das hauptmenue das ding ist er liest es ja manchmal vor aber
manchmal auch nicht jetzt sagt er nur hauptmenue".

MEIN FEHLER, klar benannt: Ich habe den Positions-Zweig VOR den generischen
Textleser gesetzt. Damit gewann die Position IMMER - auch in den Faellen,
in denen vorher ein echter Name gefunden wurde. V5.16 hat also
funktionierende Ansagen durch "Hauptmenü, X von 7" ERSETZT statt eine
Luecke zu fuellen. Ein Rueckfall darf nie den echten Text ueberstimmen.
Genau das erklaert auch, warum der User sagt "er liest es ja manchmal
vor": es gab dort sehr wohl lesbare Faelle - ich habe sie zugedeckt.

DREI KORREKTUREN IN V5.17:
1. RUECKFALL STATT ERSATZ: Die Positionsansage laeuft jetzt erst, wenn der
   generische Leser (eigener Baum + bis zu 3 Elternebenen) NICHTS gefunden
   hat. Wo vorher ein Name kam, kommt wieder der Name.
2. REIHENFOLGE UMGEDREHT: Der erste Eintrag meldete sich als "7 von 7".
   Die Knotenliste laeuft der sichtbaren Reihenfolge ENTGEGEN - dieselbe
   Z-Order-Umkehr, die fuer JournalDetail und FreeCompanyProfile schon
   dokumentiert ist. Jetzt `position = Anzahl - Index`.
3. NAME "HAUPTMENUE" RAUS: war schlicht falsch (User: "es ist nicht das
   hauptmenue"). Der interne Addon-Name (_MainCommand) ist fuer einen
   Spieler ebenfalls kein Begriff. Angesagt wird jetzt nur noch, was
   wirklich bekannt ist: "3 von 7".

AUSSERDEM, fuer die eigentliche Frage ("mal gehts, mal nicht"): JEDE
[Focus]-Zeile nennt jetzt das Fenster, nicht nur die stummen. Bisher stand
der Addon-Name nur in den STUMM-Zeilen - die gelungenen und die
misslungenen Faelle waren damit nicht vergleichbar, und genau der
Vergleich fehlt fuer die Diagnose.

Build 0/0, deployt (5.17.0.0). Versionen csproj + Plugin.cs synchron.

### Beim naechsten Test (V5.17)
1. "Version 5 Punkt 17 bereit".
2. Das Menue von vorhin: kommen die Eintraege, die FRUEHER vorgelesen
   wurden, wieder mit ihrem Namen? (Das ist der Regressionstest - wichtiger
   als alles andere.)
3. Wo gar kein Name existiert: kommt "1 von 7" beim ERSTEN Eintrag,
   aufsteigend bis "7 von 7"? (Vorher genau andersherum.)
4. WELCHES Menue ist es ueberhaupt? Das Log nennt es intern
   `_MainCommand`, aber der User sagt, es ist nicht das Hauptmenue.
   Offene Frage an den User: mit welcher TASTE oeffnest du es? Daraus
   laesst sich der richtige Name bestimmen - im Keybind-Dump steht zu
   jeder Taste die Spielaktion.
5. Log: die "[Focus] addon='...'"-Zeilen zeigen jetzt bei JEDEM Eintrag
   Fenster und Text. Daran ist ablesbar, welche Eintraege einen Namen
   liefern und welche nicht - das klaert das "mal so, mal so" endgueltig.

---

## STAND (2026-07-19, V5.16: Positionsansage - siehe Regression oben)

DIE SONDE HAT GELIEFERT. Alle 14 stummen Faelle (Log 12:41:04-08) kamen aus
EINEM Fenster:

  [Focus] STUMM addon='_MainCommand' id=6 typ=8 eltern=[2:1001 1:1] nachbarn=[]

Auswertung:
- `_MainCommand` = das HAUPTMENUE.
- typ=8 = Collision-Knoten, Elternteil typ=1001 = Button.
- Die Eltern-Id wandert 2,3,4,5,6,7,8 und dann wieder von vorn - der User
  ist zweimal durch alle SIEBEN Eintraege gelaufen und hat KEIN EINZIGES
  Mal etwas gehoert. Im ganzen Zeitfenster steht sonst nichts im Log:
  keine Ansage, kein Tooltip, kein Scan.

BEIDE MEINE HYPOTHESEN AUS V5.15 SIND WIDERLEGT - genau deshalb wurde
nicht geraten:
- Geschwister-Knoten: `nachbarn=[]` war bei allen 14 Faellen LEER.
- Kletterhoehe: der Button liegt direkt daruber, 3 Ebenen reichten.
Die Wahrheit ist simpler: die Hauptmenue-Knoepfe tragen NUR Symbole,
ueberhaupt keinen Text. Es gab nichts zu finden.

WAS NEU IST: Der Fokus im Hauptmenue sagt die POSITION an - "Hauptmenü,
3 von 7". Das ist gemessen, nicht erfunden: die Knoepfe werden in der
Knotenliste des Addons gezaehlt und der fokussierte per Identitaet
lokalisiert. Dasselbe Muster nutzt der Aussehen-Picker der
Charaktererstellung fuer seine textlosen Zeilen schon.

WARUM NOCH KEINE NAMEN (bewusst): Die Namen stehen im Lumina-Sheet
`MainCommand` (Name/Description/Icon, ilspycmd-verifiziert 2026-07-19).
WIE ein Knopf auf eine Sheet-Zeile zeigt, ist aber NICHT geklaert -
`AddonMainCommand` und `AgentMainCommand` existieren in
FFXIVClientStructs NICHT (beide Lookups leer). Ein falscher Name schickt
einen blinden Spieler ins falsche Fenster; das ist schlimmer als eine
Position. Deshalb loggt V5.16 pro Knopf dessen registrierte Events und
zu jedem Parameter die Sheet-Zeile, die er treffen wuerde:

  [MainCmd] Knopf 3 id=4 events=[typ=25 param=3 -> 'Inventar' | ...]

Stimmen diese Namen mit der tatsaechlichen Reihenfolge im Menue ueberein,
ist die Zuordnung bewiesen und die Namensansage ist danach ein Zweizeiler.
Stimmen sie nicht, ist die Idee widerlegt, ohne dass je ein falscher Name
gesprochen wurde.

Build 0/0, deployt (5.16.0.0). Versionen csproj + Plugin.cs synchron.
UIReaderService bekommt dafuer neu IDataManager (Sheet-Zugriff).

### Beim naechsten Test (V5.16)
1. "Version 5 Punkt 16 bereit".
2. Hauptmenue oeffnen, mit den Pfeiltasten durch: kommt jetzt bei JEDEM
   Eintrag "Hauptmenü, X von 7"? (Vorher: komplett still.)
3. Stimmt die Anzahl - hat dein Hauptmenue wirklich 7 Eintraege?
4. LOG-FRAGE, die ueber die Namen entscheidet: In den "[MainCmd]"-Zeilen
   steht pro Knopf ein aufgeloester Name. Bitte einmal durchgehen und
   sagen, welcher Eintrag an welcher Position WIRKLICH steht (z.B. "1 ist
   Charakterinfo, 2 ist Inventar, ..."). Passt das zur Log-Reihenfolge,
   kommen die echten Namen in die naechste Version.
5. Falls "[MainCmd]"-Zeilen ganz fehlen: der Knopf traegt keine
   registrierten Events - dann brauche ich einen Strg+F5-Dump bei
   offenem Hauptmenue.

### Weiterhin offen
- Die stummen Faelle betrafen NUR _MainCommand. Ob es in anderen Menues
  ebenfalls klemmt, zeigt die "[Focus] STUMM"-Sonde - sie bleibt drin.
- Overlay-Fenster: braucht den User EINGELOGGT (siehe V5.13-Abschnitt).

---

## STAND (2026-07-19, V5.15: Sonde fuer stumme Menues)

User: "ich hab das fenomen das manchmal menues nicht vorgelesen werden
obwohl es mal ging also mal gehts und mal nicht".

DAS PHAENOMEN IST IM LOG BELEGT - es ist nicht Einbildung und nicht
NVDA: von 40 Fokuswechseln am 2026-07-19 lieferten 19 einen LEEREN Text
("[Focus] ... Text=''"). Bei leerem Text wird nichts gesprochen
(UpdateGlobalFocus, `if (!string.IsNullOrEmpty(text))`). Fast die Haelfte
aller Fokusbewegungen war also stumm. Sichtbar u.a. um 12:28:36-43 waehrend
der SystemMenu-Navigation: einmal kam "Dalamud Plugins", danach dreimal
nichts.

WARUM ICH HIER NICHT GEFIXT HABE: Der Log nennt nur Node-Id und Zeiger -
NICHT das Fenster und nicht den Knotentyp. Damit ist kein einziger der 19
Faelle zuzuordnen. Ich habe zwei plausible Ursachen im Code gefunden:
1. `GetTextFromNodeTree` verwirft Texte der LAENGE 1 (Zeile "t.Length > 1")
   - im Code selbst schon als Ursache dokumentiert, warum Hotbar-Zeilen
   ohne ihre Tastenbezeichnung angesagt wurden.
2. Die Suche geht ins eigene Unterbaum und dann maximal 3 ELTERN hoch -
   sitzt die Beschriftung in einem GESCHWISTER-Knoten (klassisch:
   Collision-Knoten neben Text-Knoten), wird sie nie gefunden.
BEIDES sind Hypothesen. Welche zutrifft - oder ob es eine dritte gibt -
entscheidet das Log, nicht mein Bauchgefuehl. Ein geratener Fix an dieser
Stelle trifft den gesamten Fokus-Pfad, also praktisch jedes Menue im Spiel.

WAS NEU IST (nur Diagnose, kein Verhalten geaendert):
Bei leerem Fokustext schreibt das Plugin jetzt eine Zeile
"[Focus] STUMM addon='<Fenster>' id=<n> typ=<n> eltern=[...] nachbarn=[...]".
- `addon` kommt aus einem IDENTITAETSvergleich (Wurzelknoten des Fokus
  gegen RootNode aller geladenen Addons), nicht aus Namensraterei.
- `eltern` zeigt die Typkette nach oben - daran ist ablesbar, ob 3 Ebenen
  zu knapp waren.
- `nachbarn` liest die Geschwisterknoten. Steht dort Text, ist Hypothese 2
  bewiesen und der Fix ist exakt bestimmt.
Laeuft NUR im Fehlerfall und einmal pro Fokuswechsel (die Dedup-Zeile
darueber verhindert Frame-Spam).

Build 0/0, deployt (5.15.0.0). Versionen csproj + Plugin.cs synchron.

### Beim naechsten Test (V5.15) - hier brauche ich ECHTE Daten
1. "Version 5 Punkt 15 bereit".
2. Genau das tun, was das Problem ausloest: Menues oeffnen und mit den
   Pfeiltasten durchgehen, BIS es stumm bleibt. Ruhig mehrere Menues
   (Systemmenue, Journal, Inventar, Optionen) - je mehr stumme Faelle im
   Log, desto genauer der Fix.
3. Danach das Log schicken. Interessant sind die "[Focus] STUMM"-Zeilen.
4. NICHT noetig: raten, welches Menue es war. Die Zeile nennt das Fenster
   jetzt selbst.

### Danach (wenn die Daten da sind)
Aus "nachbarn=" und "eltern=" ergibt sich der Fix direkt:
- Text in `nachbarn` -> Geschwistersuche in die Textermittlung aufnehmen.
- Text erst weit oben in `eltern` -> Kletterhoehe erhoehen.
- Ueberall leer -> das Fenster traegt seine Beschriftung woanders
  (Komponentendaten statt Textknoten), dann braucht es einen
  Strg+F5-Dump dieses Fensters.

---

## STAND (2026-07-19, V5.14: Gegenstaende wurden uebertoent)

User: "schau mal in die log da bekomme ich meldungen die stoeren und die
items nicht richtig ansagen". Log-Auswertung hat BEIDES als EINEN Fehler
entlarvt.

ROOT CAUSE (Log 2026-07-19, 11:43:24, eindeutig): `ScanAddonTexts` spricht
JEDEN geaenderten Text-Node EINZELN mit `SpeakInterrupt`. Der
Gegenstands-Tooltip `ItemDetail` traegt 7-8 solcher Nodes. Der Ablauf pro
Gegenstand sah so aus:
- 'Hanf-Arbeitshandschuhe, Stufe 6, tragbar'  <- die RICHTIGE Ansage
- 'Verkaufswert: 3' / 'Haendlerpreis: 145' / 'Farbe: Moosgruen' /
  'Haende' / '(Besitz: 0 / 0)' / nochmal der Name
- 'Strg HQ-Gegenstandsbeschreibung anzeigen　Alt Beschreibung ausblenden'
Jede Ansage schnitt die vorherige ab. Hoerbar blieb nur die LETZTE - der
BEDIENHINWEIS. Der Gegenstand war also nicht bloss verrauscht, er war
faktisch nicht ansagbar, obwohl der Fokus-Pfad die korrekte Ansage
("Name, Stufe, tragbar") die ganze Zeit erzeugt hat. Dasselbe Muster wie
V5.5/V5.7: ein Kontext, den sein eigener Inhalt abschneidet.

FIX 1: `ItemDetail` und `Tooltip` in HudNoiseAddons. Kein
Informationsverlust - die uebertoenten Zeilen waren ohnehin unhoerbar.

FIX 2 (damit die Details nicht verloren gehen): `TryReadItemDetail()` -
Strg+F10 liest den offenen Tooltip als EINEN Satz, Name zuerst. Eingehaengt
direkt hinter dem Journal-Zweig in ReadCurrentFocus, mit derselben Logik:
Tooltip offen -> der User will den GEGENSTAND lesen.
- RUECKWAERTS durch die Node-Liste (Name steht spaet in Node-Reihenfolge,
  Z-Order - dasselbe Muster wie JournalDetail/FreeCompanyProfile).
- NUR Top-Level-Text-Nodes. Das ist KEIN Textraten, sondern strukturell:
  im Log stehen die echten Fakten als direkte Text-Nodes (id=33 Name,
  id=34 Besitz, id=35 Slot, id=42/44/48 Farbe/Preise), der Bedienhinweis
  dagegen als Komponenten-Kind (key=30002). Komponenten auszulassen wirft
  den Hinweis raus, ohne eine Phrase hart zu verdrahten. Das Ergebnis wird
  als "[Item] Tooltip: N Teile - ..." geloggt, damit die Annahme
  ueberpruefbar bleibt (sie stuetzt sich bisher auf EIN Log-Beispiel).

FIX 3 (der Log-Spam): `[Quest] JournalResult Belohnung` lief pro Frame -
dieselbe Zeile alle ~75 ms, rund 2000 identische Eintraege um 11:44, die
alles andere im Log begraben haben. Jetzt nur noch bei ECHTER Aenderung
(_lastRewardLog).

ZWEI EIGENE VERMUTUNGEN, DIE SICH BEIM PRUEFEN ALS FALSCH ERWIESEN
(dokumentiert, damit sie nicht wiederkommen):
- `amounts=[1435,194]` sah nach kaputten Werten aus, ist aber korrekt:
  zwei Werte, Erfahrung 1435 und Gil 194.
- Ein `\ WORKAROUND` in der Grep-Ausgabe sah nach Syntaxfehler aus, war
  aber ein Artefakt der Ausgabe - im Code steht `//`.

Build 0/0, deployt (5.14.0.0). Versionen csproj + Plugin.cs synchron.

### Beim naechsten Test (V5.14)
1. "Version 5 Punkt 14 bereit".
2. Im Inventar/Laden ueber Gegenstaende gehen: kommt jetzt "Name, Stufe,
   tragbar" - und NICHT mehr "Strg HQ-Gegenstandsbeschreibung anzeigen"?
3. Ist das Gerede dazwischen (Verkaufswert/Farbe/Besitz einzeln) weg?
4. Auf einem Gegenstand Strg+F10: kommt der volle Tooltip als EIN Satz,
   mit dem Namen zuerst?
5. Falls bei Strg+F10 der Bedienhinweis doch mitkommt oder der Name fehlt:
   die Zeile "[Item] Tooltip:" im Log zeigt genau, was gelesen wurde -
   dann stimmt die Top-Level-Annahme nicht und ich brauche einen
   Strg+F5-Dump bei offenem Tooltip.
6. Ist der Name sauber, oder stecken Glyphen-Reste drin? Im Log stand der
   Tooltip-Name als 'H?%I?&Hanf-Arbeitshandschuhe...' - Sanitize sollte das
   abraeumen, ist fuer DIESEN Pfad aber ungetestet.

### OFFEN aus der Log-Auswertung (nicht beauftragt, nicht gebaut)
Der Scanner liest auch `CharacterStatus` (80x), `_AreaText` (41x),
`Gathering` (20x), `_ToDoList` (12x) Node fuer Node - dasselbe
Interrupt-Muster wie bei ItemDetail. Ob das stoert, weiss nur der User;
bisher keine Meldung dazu.

---

## STAND (2026-07-19, V5.13: Dalamud-Plugin-Liste vorlesbar)

User: "im menue gibt es dalamud plugins und dalamud einstellungen die sind
auch nicht barrierefrei" (dazu: stoerende Fenster ueber dem Spiel - siehe
OFFEN unten).

WARUM DAS STUMM WAR (keine Luecke im Plugin): Dalamuds Oberflaeche -
Plugin-Installer, Einstellungen, auch die vnavmesh-Fenster - ist ImGui.
ImGui hat keinen AtkUnitBase, keine Nodes, keinen Baum. Unser gesamter
Vorlese-Apparat haengt an genau diesem Baum, und NVDA findet dort ebenso
wenig. Es gibt also nichts zu "reparieren"; ein UI-Scraping-Weg existiert
schlicht nicht.

DER WEG STATTDESSEN: nicht die UI lesen, sondern die DATEN dahinter.
`IDalamudPluginInterface.InstalledPlugins` liefert `IExposedPlugin` je
Plugin mit Name, Version, IsLoaded, IsOutdated, IsBanned, IsDev,
HasConfigUi + `OpenConfigUi()` (ilspycmd-verifiziert 2026-07-19 gegen
Dalamud.dll). Das ist OEFFENTLICHE, versionierte API - kein Reflection,
kein Interna-Zugriff, kann durch ein Dalamud-Update nicht still brechen.

WAS NEU IST (DalamudPluginsService.cs):
- Umschalt+F1 blaettert vorwaerts. Der ERSTE Druck sagt zuerst die
  Uebersicht: "12 Plugins, alle geladen." bzw. "12 Plugins, 1 nicht
  geladen." Das beantwortet die eigentliche Frage ("laeuft alles?") ohne
  eine zweite Taste zu verbrauchen.
- Danach je Eintrag: "3 von 12: vnavmesh, Version 1.2.3.8, geladen, hat
  Einstellungen." Auffaelliges wird ergaenzt: nicht geladen / veraltet /
  gesperrt / Entwickler-Plugin. "nicht geladen" ist die wichtigste
  Information - sie erklaert, warum ein Feature fehlt (Auto-Lauf ohne
  vnavmesh).
- Umschalt+F2 blaettert zurueck, Umschalt+F12 oeffnet die Einstellungen
  des gewaehlten Plugins.
- Die Liste wird bei JEDEM Druck neu gelesen (Plugins koennen zur Laufzeit
  laden/entladen); der Cursor bleibt dabei auf demselben Plugin.

EHRLICH DAZU - Umschalt+F12 nuetzt dir allein wenig: `OpenConfigUi()`
oeffnet wieder ein ImGui-Fenster. Du kannst es oeffnen, aber nicht lesen.
Deshalb sagt die Ansage das ausdruecklich mit ("Das Fenster ist nicht
vorlesbar."). Sinnvoll ist es nur, wenn jemand Sehendes danebensitzt.

BEWUSST NICHT GEBAUT (User-Entscheid 2026-07-19): installieren,
aktualisieren, entfernen, an-/abschalten. Diese Methoden existieren
(`InstallPluginAsync`, `UpdateSinglePluginAsync`, `UpdatablePlugins`,
`RemovePlugin`), liegen aber in `Dalamud.Plugin.Internal.PluginManager` -
internal, nur per Reflection erreichbar, bricht potenziell still bei jedem
Dalamud-Update. Der User hat sich fuer den stabilen Nur-Lesen-Weg
entschieden; Installation/Update laufen weiter ueber die Installer-EXE
ausserhalb des Spiels (docs/installer-architektur.md).

TASTEN: Umschalt+F1/F2/F12 sind die letzten freien F-Kombis - Strg+F1..F12
sind komplett vergeben, Umschalt+F3..F11 ebenfalls (game-api.md -> Safe
Mod Keys, Live-Dump).

Build 0/0, deployt (5.13.0.0). Versionen csproj + Plugin.cs synchron.

### Beim naechsten Test (V5.13)
1. "Version 5 Punkt 13 bereit".
2. Umschalt+F1 druecken: kommt zuerst "N Plugins, alle geladen" und dann
   der erste Eintrag?
3. Weiter mit Umschalt+F1 / zurueck mit Umschalt+F2: kommen Name, Version
   und Zustand? Taucht vnavmesh auf, und steht dort "geladen"?
4. Stimmt die Zahl mit dem ueberein, was ein Sehender im Plugin-Installer
   sieht?
5. Umschalt+F12 auf einem Plugin mit Einstellungen: kommt die Bestaetigung?
6. Log-Kontrolle bei Problemen: die "[DalamudPlugins]"-Zeilen zeigen
   Uebersicht, geoeffnete Fenster und jede nicht lesbare Eigenschaft.

### OFFEN: stoerende Fenster ueber dem Spiel
Noch nicht angefasst, bewusst zurueckgestellt - der User kennt das Problem
nur aus zweiter Hand ("jemand Sehendes hat es mir gesagt"), es stoert ihn
selbst also nicht. Es sind ebenfalls ImGui-Overlays (vnavmesh-Fenster,
Dalamud-Fenster). Zu pruefen, wenn es drankommt: Dalamuds eigener
UI-Hide-Mechanismus und ob sich vnavmesh-Fenster ueber dessen Config
geschlossen halten lassen. NICHT recherchiert, daher keine Aussage dazu.

---

## STAND (2026-07-19, V5.12: Lagerstaetten fuer Minenarbeiter)

User ist jetzt Minenarbeiter und fragt, wie er Lagerstaetten findet;
Wunsch: die Kategorie soll nur erscheinen, wenn die Sammler-Klasse aktiv
ist.

BEFUND: Die Kategorie "Sammelpunkte" (ObjectKind.GatheringPoint) gab es
schon - aber mit zwei Problemen:
1. STUMMER FILTER: GetCategoryObjects verwarf JEDES Objekt ohne Namen
   (`!IsNullOrWhiteSpace(o.Name)`). Sammelpunkte tragen aber typisch einen
   LEEREN Objektnamen - ihre Beschreibung steht in den Spieldaten hinter
   der BaseId. Der Filter hat also genau das weggeworfen, wonach der User
   sucht. UNBESTAETIGT, ob in-game wirklich alle namenlos sind - deshalb
   ist der Filter nur fuer diese Kategorie gelockert, nicht global.
2. Die Kategorie lief immer mit, auch als Kaempfer.

WAS NEU IST:
1. Sammelpunkte werden mit TYP UND STUFE angesagt: "1 von 3: Erzader,
   Stufe 20, 15 Meter, Nordosten". Datenweg (ilspycmd-verifiziert):
   GameObject.BaseId -> Sheet GatheringPoint -> GatheringPointBase ->
   GatheringType.Name (lokalisiert!) + GatheringLevel (byte @36).
   Der Typname wird GELESEN, nicht aus einer selbst erfundenen Id-Tabelle
   abgeleitet - GatheringType hat KEINE Klassen-Spalte, jede Zuordnung
   "0 = Minenarbeiter" waere unsere Erfindung. Ergebnis pro BaseId
   gecached und einmal geloggt ("[Gather] DataId=... Typ=... Stufe=...").
2. Die Kategorie wird beim Durchschalten UEBERSPRUNGEN, wenn keine
   Sammler-Klasse aktiv ist. Sie bleibt aber sichtbar, solange Punkte in
   Reichweite sind - so kann der Filter nie etwas verstecken, das es
   wirklich gibt. Und als Sammler bleibt sie auch bei 0 Treffern
   erreichbar, denn "hier ist nichts" ist eine gueltige Antwort.

ANNAHME, ausdruecklich markiert: "ist Sammler" = ClassJob.DohDolJobIndex
>= 0 und BattleClassIndex < 0. Dass diese Felder EXISTIEREN, ist
ilspycmd-verifiziert; ihre konkreten WERTE stehen in Spieldaten, die
offline nicht lesbar sind. Deshalb loggt IsGatheringClass bei jedem
Klassenwechsel Name, Abkuerzung, RowId und beide Index-Felder. Faellt die
Annahme, bricht nichts still zusammen - der Fallback "Punkte in
Reichweite" haelt die Kategorie trotzdem verfuegbar.

Build 0/0, deployt (5.12.0.0). Versionen csproj + Plugin.cs synchron.

### Beim naechsten Test (V5.12)
1. "Version 5 Punkt 12 bereit".
2. Als Minenarbeiter mit Strg+N durch die Kategorien: kommt
   "Sammelpunkte"? Und als Kaempfer (Klasse wechseln): faellt sie weg?
3. In der Kategorie mit N blaettern: kommen Typ und Stufe
   ("Erzader, Stufe 20") statt eines leeren Namens?
4. Numpad 3 auf einem Sammelpunkt: laeuft er hin?
5. LOG-FRAGE (entscheidet ueber die Annahme oben): Was steht in der Zeile
   "[Gather] Klasse: ... DohDolJobIndex=... BattleClassIndex=...
   -> Sammler=..."? Steht dort als Minenarbeiter "Sammler=True" und als
   Kaempfer "Sammler=False", ist die Annahme bestaetigt und kann in
   game-api.md als Fakt wandern.
6. Falls die Kategorie als Minenarbeiter LEER bleibt, obwohl Erzadern in
   Sicht sind: dann liegen Sammelpunkte nicht als GatheringPoint in der
   ObjectTable - das zeigt sich daran, dass keine "[Gather] DataId"-Zeile
   erscheint. Dann brauche ich einen Objekt-Dump an einer Erzader.

---

## RELEASE v5.11 VEROEFFENTLICHT (2026-07-18 abends)

Commits 303fbc3 (Code V4.98-V5.11) + b47853f (repo.json 5.11) auf main
gepusht. GitHub-Release v5.11 mit allen vier Assets:
latest.zip / FF14Accessibility-v5.11.0.zip (je 530718 Bytes,
Release-Build, Manifest 5.11.0.0 im ZIP geprueft) +
FF14AccessibilityInstaller.exe + installer.json.
Alle drei latest-Download-Links verifiziert (HTTP 200).

INSTALLER unveraendert uebernommen (Code seit 9fc809a nicht angefasst):
EXE + installer.json aus dem v4.97-Release kopiert, SHA256 gegen das
Manifest geprueft - stimmt ueberein. Damit bleiben der README-Link und
das Selbst-Update funktionsfaehig; ohne diese beiden Assets im NEUESTEN
Release waeren beide tot gewesen.

NICHT committet: uia_test.ps1 (loses Testskript, gehoert nicht zum
Projekt).

ACHTUNG - im Release steckt UNGETESTETER Code: V5.7 (Online-Fenster),
V5.9 (Einladung per Strg+F12) und V5.10 (Fenster vorlesen) sind gebaut,
aber nie in-game bestaetigt. Die Testpunkte stehen unten bei den
jeweiligen Versionen.

---

## STAND (2026-07-18, V5.11: Ziel-Ton entfernt)

User: "wenn man einen gegner anvisiert gibts ein piepen von der mod mach
das weg man hoert vom spiel einen ton wenn man einen gegner im visier
hat".

QUELLE: NavigationService Zeile 110, `_cue.PlayTargetTone()` - ein
zweitoeniger Blip bei jedem Zielwechsel auf einen BattleNpc. Das Spiel
spielt fuer dasselbe Ereignis bereits einen eigenen Ton, der Mod-Ton war
also reine Doppelung.

WICHTIG - warum nicht einfach der Config-Schalter: es GAB bereits
`EnableTargetTone`, aber ein geaenderter Default haette nichts bewirkt.
Die Konfiguration ist gespeichert, der abgelegte Wert (true) haette den
neuen Default ueberschrieben und es haette weiter gepiept. Deshalb ist
der Aufruf ersatzlos entfernt.

RUECKGEBAUT: Aufruf in NavigationService, `CueService.PlayTargetTone()`,
Config-Felder `EnableTargetTone` + `TargetToneVolume`.
UNBERUEHRT: die gesprochene Ziel-Ansage ("Ziel: Name, Art, Entfernung,
Richtung") und die Gehhilfe-Toene (Wegpunkt erreicht / angekommen) - die
haben kein Gegenstueck im Spiel und bleiben.

Build 0/0, deployt (5.11.0.0). Versionen csproj + Plugin.cs synchron.
V5.10 (Fenster vorlesen) ist enthalten und weiterhin UNGETESTET.

### Beim naechsten Test (V5.11)
1. "Version 5 Punkt 11 bereit".
2. Gegner anvisieren (Tab / F11 / N): nur noch der Spielton, kein
   Mod-Piepen mehr - aber die gesprochene Ziel-Ansage kommt weiter?
3. Gehhilfe/Auto-Lauf: Wegpunkt- und Ankunftston noch da?

---

## STAND 2026-07-18 (V5.10: ganzes Fenster vorlesen)

User-Wunsch nach einem Dump des Gesuch-Fensters: "ich will wissen was da
angezeigt wird also alles auch das eingabefelder benannt werden".

DUMP-ANALYSE (Desktop\FFXIV_UI_Dump.txt, 18:46) - drei Fenster:
FriendList, `FreeCompanyProfile` (64 Nodes) und
`FreeCompanyInputMessage` (7 Nodes).

INHALT `FreeCompanyProfile` ("PROFIL DER FREIEN GESELLSCHAFT"):
HOME SWEET HOME «HSH», Grossgesellschaft "Legion der Unsterblichen",
Meister Soluna Stella, Rang 29, Mitglieder "Auf Stammwelt online: 26 von
171", gegruendet 10.6.2026, "Keine Unterkunft vorhanden.", Wahlspruch
"»Deutsch & English« | »Newbies & Veterans« | »One BIG family« Raids,
Events, ...", Aktiv "Jeden Tag", Rekrutierung "Nimmt Gesuche an",
Knoepfe "Gesuch stellen" und "Schliessen".

LUECKE, die der Dump zeigt: "Aktivitaeten" (9 Komponenten) und "Sucht"
(5 Komponenten) tragen NUR Bilder, keinen Text - die zugehoerigen
Texte "Keine Angabe" sind unsichtbar geschaltet (F=0x2023 ohne V), d.h.
es IST etwas eingetragen, aber ausschliesslich als Icon. Fuer diese
beiden Zeilen gibt es also nichts vorzulesen; das braeuchte eine
Icon-ID->Name-Aufloesung wie beim Inventar. NICHT gebaut, nicht geraten.

INHALT `FreeCompanyInputMessage` ("BEITRITTSGESUCH") - das ist das
Bewerbungsfenster: Label "Nachricht", ein TextInput(7) mit
Zeichenzaehler ("1/2"), Knoepfe "Ok" und "Abbrechen".

WAS NEU IST (V5.10): Strg+F10 liest jetzt auch Fenster vor, die WEDER
Liste noch Dialog sind - vorher kam dort "Kein aktives Menue"
(ReadCurrentFocus hatte keinen Zweig dafuer). TryReadWholeWindow nimmt
das oberste fokussierte sichtbare Fenster und liest alle sichtbaren
Texte.
- RUECKWAERTS durch die Node-Liste: FFXIV stellt Labels in der
  Node-Reihenfolge HINTER ihren Inhalt (Z-Order). Das Muster war fuer
  JournalDetail schon dokumentiert, der Dump bestaetigt es exakt ("29"
  dann "Rang", "Soluna Stella" dann "Meister"). Rueckwaerts gelesen
  ergibt das von selbst "Rang, 29" statt "29, Rang".
- EINGABEFELDER WERDEN BENANNT: eine TextInput-Komponente wird als
  "Eingabefeld: <Inhalt>" bzw. "Eingabefeld, leer" ausgegeben - zusammen
  mit dem davor stehenden Label also "Nachricht, Eingabefeld, leer".
  Ohne das ist ein leeres Textfeld schlicht unsichtbar, und man weiss
  nicht, dass hier etwas getippt werden KANN.
  Inhalt aus `AtkComponentInputBase.EvaluatedString` - dasselbe Feld,
  das Chat- und CharaMake-Echo schon benutzen (ilspycmd-verifiziert).
- Zeichenzaehler ("1/2") werden uebersprungen (IsBareNumber): sie sitzen
  im Eingabefeld und sagen ohne ihren Kasten nichts aus.

Build 0/0, deployt (5.10.0.0). Versionen csproj + Plugin.cs synchron.

### Beim naechsten Test (V5.10)
1. "Version 5 Punkt 10 bereit".
2. FC-Profil oeffnen, Strg+F10: kommt das ganze Profil (Name, Meister,
   Rang, Mitglieder, Wahlspruch, Rekrutierung, Knoepfe)?
3. "Gesuch stellen" -> Beitrittsgesuch-Fenster, Strg+F10: kommt
   "Nachricht, Eingabefeld, leer" und "Ok"/"Abbrechen"?
4. Beim Tippen im Feld: kommt das Zeichen-Echo (das laeuft ueber einen
   anderen Pfad und ist fuer DIESES Fenster ungetestet)?
5. Ist die Reihenfolge verstaendlich, oder klingt etwas verdreht? Das Log
   zeigt unter "[Fenster] <Name>: N Teile - ..." genau die Reihenfolge.
6. Falls ein Fenster stumm bleibt: es war nicht in FocusedUnitsList -
   dann brauche ich den Namen aus Strg+F2.

---

## STAND 2026-07-18 (V5.9: Einladungen per Tastatur annehmen)

User-Frage: "ich habe grad die einladung zu einer freien gesellschaft
bekommen wo koennte ich die annehmen und wie?"

BEFUND: Es gab keinen Weg. Die Einladung vom 18:15:47 lief um 18:20:48 ab
("Die Einladung von Soluna Stella ... wurde abgebrochen"). Im
Keybind-Dump des Spiels existiert KEINE Aktion fuer Benachrichtigungen -
ein Sehender klickt das Popup an, und genau dieser eine Schritt fehlte.

WAS NEU IST:
1. Beim Eintreffen einer Einladung sagt das Plugin, WIE man antwortet:
   "Benachrichtigung. Mit Strg+F12 annehmen." Die Meldung selbst kam schon
   vorher ueber Chat und Toast - was fehlte, war der Handlungsweg.
2. Strg+F12 (KeyNotification, laut Keybind-Dump frei) aktiviert die offene
   Benachrichtigung: Klick-Event des besten Kandidaten wird an den
   Listener dispatcht - derselbe Pfad wie beim Mausklick und wie bei der
   Volksauswahl (DispatchClick). Danach uebernimmt das Spiel; ein
   folgender Ja/Nein-Dialog wird vom Plugin bereits vorgelesen.
3. Vor dem Druecken wird angesagt, WAS gedrueckt wird ("Aktiviere: ..."),
   damit ein falsches Ziel (etwa "Ablehnen") hoerbar ist statt still.

QUELLENLAGE (ilspycmd 2026-07-18):
- Fenster-Namen aus dem LOG, nicht geraten: _NotificationFcJoin,
  _NotificationParty, _NotificationFriend, _Notification.
- Die Node-Struktur dieser Fenster ist NICHT bekannt (nie gedumpt).
  Deshalb wird der ganze Node-Baum nach registrierten Klick-Events
  durchsucht und ALLES ins Log geschrieben ("[Notify] ... Events=[...]
  Kandidat=... Text='...'"). Beim Oeffnen wird zusaetzlich das
  Text-Inventar geloggt - damit klaert sich die Struktur bei der naechsten
  echten Einladung von selbst, ohne dass der User im 5-Minuten-Fenster
  einen Strg+F5-Dump machen muss.
- ALTERNATIVE, falls der UI-Weg scheitert (recherchiert, NICHT gebaut):
  `InfoProxyFreeCompanyInvite.RespondToInvitation(inviterName, accept)`
  (vtable @104, auch in InfoProxyInvitedList). Das ist die Spielfunktion
  selbst, braucht aber den Namen des Einladenden - im Proxy stehen dafuer
  nur private "UnkString"-Felder (@72/@176), also unverifiziert. Der
  UI-Weg braucht den Namen gar nicht, das Spiel kennt ihn selbst.

Build 0/0, deployt (5.9.0.0). Versionen csproj + Plugin.cs synchron.

### Beim naechsten Test (V5.9) - braucht eine ECHTE Einladung
1. "Version 5 Punkt 9 bereit".
2. Einladung schicken lassen: kommt "Benachrichtigung. Mit Strg+F12
   annehmen."?
3. Strg+F12 druecken: was wird angesagt ("Aktiviere: ...")? Passiert
   danach etwas - ein Dialog, eine Systemmeldung?
4. WICHTIG unabhaengig vom Ergebnis: die "[Notify]"-Zeilen im Log
   schicken. Sie enthalten das Text-Inventar und alle gefundenen Events -
   daraus laesst sich der richtige Knopf exakt bestimmen, falls der erste
   Versuch daneben greift.
5. Falls "Benachrichtigung reagiert nicht": das Popup traegt keine
   Klick-Events auf den durchsuchten Knoten -> dann auf den
   InfoProxy-Weg wechseln (siehe oben).

---

## STAND 2026-07-18 (V5.8: kein Sekunden-Countdown mehr)

User-Meldung: "es gibt irgendwas was runterzaehlt ich weiss aber nicht
was das eritiert aber die meldung soll bleiben das runterzaehlen nervt".

QUELLE (Log 18:15:47-18:20, eindeutig): Addon `_NotificationFcJoin` - die
Benachrichtigung ueber eine Einladung in eine Freie Gesellschaft. Sie
enthaelt eine Ablauffrist von 300 Sekunden, und der generische
Text-Scanner hat deren Zaehler-Node (key=20005) bei JEDER Aenderung
vorgelesen: 300, 299, 298 ... eine Zahl pro Sekunde, fuenf Minuten lang,
jeweils mit SpeakInterrupt (schneidet also auch alles andere ab).

DIE MELDUNG BLEIBT, wie gewuenscht - sie kommt aus einer ANDEREN Quelle
und ist vom Fix nicht beruehrt (Log 18:15:47.740/.741):
"System: Du wurdest von Soluna Stella in eine Freie Gesellschaft
eingeladen." ueber Chat UND Toast.

FIX V5.8: ScanAddonTexts spricht nackte Zahlen nicht mehr; geloggt werden
sie weiter, jetzt mit dem Zusatz "(Zaehler, nicht gesprochen)". Regel gilt
generell, nicht nur fuer dieses Addon - ein Text-Node, der sich in eine
reine Zahl aendert, ist ein Zaehler (Timer, fps, Fortschritt). Eine Zahl
ohne ihr Label traegt ohnehin keine Information: was "298" zaehlt, steht
nur auf dem Bildschirm. Dieselbe Regel gilt im Fokus-Pfad schon punktuell
(IsBareNumber bei JournalResult/CharaMakeDataInputString).

Build 0/0, deployt (5.8.0.0). Versionen csproj + Plugin.cs synchron.
V5.7 (Online-Fenster) ist enthalten und weiterhin UNGETESTET.

### Beim naechsten Test (V5.8)
1. "Version 5 Punkt 8 bereit".
2. FC-Einladung (oder aehnliche Benachrichtigung mit Frist): kommt die
   Einladungs-Meldung noch, aber ohne das Sekundengezaehle?
3. Faellt woanders eine Ansage weg, die vorher nuetzlich war? Im Log
   stehen die unterdrueckten Faelle als "(Zaehler, nicht gesprochen)" -
   daran ist ablesbar, ob die Regel zu breit greift.
4. AUSSERDEM offen aus V5.7: das Online-Fenster (Punkt 2 unten).

---

## STAND 2026-07-18 (V5.7: Inhalt der NEUEN Karte, nicht der alten)

User-Meldung zu V5.6: "es wird immer gleich der erste eintrag vorgelesen
wenn ich die registerkarte wechsel".

ROOT CAUSE (Log 17:14:54, eindeutig): Es war der Eintrag der VORHERIGEN
Karte. Zeitlicher Ablauf eines Tab-Wechsels:
- .081 Tab-Wechsel erkannt, Ansage vorbereitet
- .152 das NEUE Kind-Fenster (FriendList) oeffnet sich erst jetzt
- .189 das ALTE (PartyMemberList) schliesst sich sogar noch spaeter
Mein Flush hat bei .081 die erstbeste nicht-leere Liste genommen - und
das war die noch offene, noch gefuellte Liste der ALTEN Karte. Belegt im
Log: "Freunde ... 2 Eintraege (Liste aus PartyMemberList)" und
"Suche ... 1 Eintraege: HSH... (Liste aus FriendList)".
DENKFEHLER: Ich habe "count > 0" als Beweis genommen, dass der Inhalt da
ist. Nicht-leer heisst aber nicht NEU.

FIX V5.7:
1. Beim Tab-Wechsel wird die Id des Kind-Fensters der VERLASSENEN Karte
   gemerkt und bei der Inhaltssuche uebersprungen
   (FindListInHostOrChild(.., excludeId)). Die Ansage wartet damit
   zwingend auf das Fenster der neuen Karte - oder faellt nach 0,7 s auf
   den blossen Kartennamen zurueck.
2. Der globale Fokus-Pfad schweigt waehrend der Tab-Ansage. Er ist
   frame-getrieben und lief am Addon-Guard aus V5.6 vorbei: im Log kam
   'HSH, Thal-Kreuzgang...' 92 ms nach der Tab-Ansage und hat sie
   abgeschnitten - derselbe Fehler wie in V5.5, nur eine Ebene tiefer.
3. Keine Doppel-Ansage mehr: hat die Tab-Ansage den Inhalt eines
   Kind-Fensters gesprochen, wird dessen aufgeschobene eigene Ansage
   verworfen (im Log kam "Menue, 2 Eintraege" eine Sekunde hinterher).

Build 0/0, deployt (5.7.0.0). Versionen csproj + Plugin.cs synchron.

### Beim naechsten Test (V5.7)
1. "Version 5 Punkt 7 bereit".
2. O druecken, Karten durchwechseln: gehoert jetzt der Inhalt der Karte,
   auf der du GERADE stehst? Gegenprobe: "Gruppe" muss Gruppenmitglieder
   nennen, "Freunde" die Freundesliste - vorher war es genau verschoben.
3. Kommt nach der Tab-Ansage noch etwas hinterhergeschoben, oder ist es
   ein sauberer Satz?
4. Log-Kontrolle: die Zeile "[Social] Ansage: ..." nennt in Klammern, aus
   welchem Fenster die Liste kam. Dort muss das Fenster stehen, das zur
   angesagten Karte gehoert (Gruppe->PartyMemberList,
   Freunde->FriendList).

### Offene Frage fuer spaeter (nicht geraten, nicht gebaut)
Das Spiel meldet den Wechsel selbst: "Social ReceiveEvent:
type=ChildAddonAttached param=126/127". Sehr wahrscheinlich ist param die
Addon-Id des neuen Kind-Fensters - dann koennte die Zuordnung exakt vom
Spiel kommen statt ueber den Ausschluss des alten Fensters. UNVERIFIZIERT,
deshalb bewusst nicht darauf gebaut.

---

## STAND 2026-07-18 (V5.6: Inhalt der Registerkarte gefunden)

V5.5-TEST AUSGEWERTET (Log 17:05-17:06, User hat getestet):
- Die Registerkarten-Ansage KAM sauber und ohne Abschneiden: "Freunde,
  Registerkarte 2 von 4", "Suche, 3 von 4", "Gruppe, 1 von 4" - Label
  jedes Mal aus dem echten ButtonTextNode, nie aus der Fallback-Liste.
- Der Inhalt fehlte, und das Log nennt den Grund selbst:
  "(... Liste NICHT gefunden)". Der Dump war nicht noetig.

ROOT CAUSE: Der Inhalt liegt NICHT im Social-Fenster. Beim Tab-Wechsel
haengt das Spiel ein eigenes Addon an ("Social ReceiveEvent:
type=ChildAddonAttached") und oeffnet FriendList / SocialList /
PartyMemberList als separates Fenster. Wir haben nur im Host gesucht und
dort korrekt nichts gefunden.

ZWEITER BEFUND: Jedes dieser Kind-Fenster sagte beim Oeffnen "Menue, 0
Eintraege" - und das war falsch. [ListProbe] zeigt Len=0 beim PostSetup,
35 ms spaeter stehen die Freunde drin. "0 Eintraege" heisst fuer einen
blinden Spieler "hier ist nichts, geh weiter" - die schlimmere Sorte
Falschmeldung, weil sie ihn von funktionierendem Inhalt wegschickt.

FIX V5.6:
1. FindListInHostOrChild sucht die Liste auch in den ANGEHAENGTEN
   Kind-Fenstern. Verknuepft wird ueber Ids, nicht ueber Namen:
   AtkUnitBase traegt Id/ParentId/HostId (ilspycmd-verifiziert
   2026-07-18); beide Rueckverweise werden akzeptiert und das Log nennt
   den, der gematcht hat ("via HostId"/"via ParentId"). Keine
   hartcodierte Kind-Namensliste.
2. Die Tab-Ansage wartet jetzt auf ihren Inhalt, statt ihn zu verpassen:
   AnnounceSocialTabIfChanged legt den Text nur zurueck,
   FlushPendingSocialTab spricht ihn, sobald Eintraege da sind -
   spaetestens nach 0,7 s auch ohne. Ergebnis: EIN Satz,
   "Freunde, Registerkarte 2 von 4, 12 Eintraege: <erster Eintrag>."
3. Die Kind-Fenster schweigen waehrend der Ansage (IsSocialChildDuringGrace,
   ueber dieselbe Id-Verknuepfung). Im Log lag ihre Fokus-Ansage 87 ms
   nach der Tab-Ansage und hat sie mit SpeakInterrupt abgeschnitten -
   genau das war der V5.5-Fehler, nur eine Ebene tiefer.
4. "0 Eintraege" wird nirgends mehr sofort gesagt: leere Listen landen in
   _emptyListSince und werden von AnnounceLateFilledList nachgereicht,
   sobald sie gefuellt sind. Bleibt eine Liste 1 s lang leer, kommt ein
   ehrliches "Keine Eintraege". Das gilt fuer ALLE Fenster, nicht nur
   fuer das Online-Fenster.

Build 0/0, deployt (5.6.0.0). Versionen csproj + Plugin.cs synchron.

### Beim naechsten Test (V5.6)
1. "Version 5 Punkt 6 bereit".
2. O druecken, Registerkarten wechseln: kommt jetzt die VOLLE Ansage
   "<Karte>, Registerkarte X von 4, N Eintraege: <erster Eintrag>"?
3. Pfeiltasten durch die Eintraege: werden sie einzeln angesagt, und
   schneidet nichts mehr die Tab-Ansage ab?
4. Kommt beim Oeffnen irgendwo noch "Menue, 0 Eintraege"?
5. Log-Kontrolle bei Problemen: "[Social] Ansage: ... (Liste aus
   FriendList (via HostId), 12 Eintraege)". Steht dort "Liste NICHT
   gefunden", greift die Id-Verknuepfung nicht - dann brauche ich einen
   Strg+F5-Dump bei offenem Fenster.

### Nebenbefund aus dem Log (nicht beauftragt)
Emotes kommen an, werden aber verworfen: "[Chat] kind=StandardEmote (29)
... gelesen=False text='Chriss Yorha schnippt mit den Fingern.'". Falls
gewuenscht, ist das eine Zeile in ShouldRead.

---

## STAND 2026-07-19 (V5.5: Registerkarte wird nicht uebersprochen)

User-Meldung zu V5.4: "wenn ich auf die registerkarte gehe wird der
spieler gleich angesagt aber nicht welche registerkarte das grad ist bzw
die eintraege - ich weiss also nicht was ich noch machen kann".

ROOT CAUSE: Die Tab-Ansage KAM, wurde aber sofort abgeschnitten. Der
generische Listen-/Fokus-Pfad laeuft im selben Frame direkt danach und
spricht den ersten Listeneintrag mit SpeakInterrupt - das unterbricht die
laufende Ansage. Gehoert wurde also nur noch der Spielername. Genau
deshalb war "kein return" in V5.4 falsch gedacht: der Inhalt darf nicht
NACH dem Kontext kommen, er muss MIT ihm kommen.

FIX:
1. Die Tab-Ansage nimmt den Listeninhalt gleich mit, in EINER Ansage:
   "Freundesliste, Registerkarte 2 von 4, 12 Eintraege: <erster Eintrag>."
   Bei leerer Liste "keine Eintraege" - auch das ist eine Antwort auf
   "was kann ich hier machen".
2. Nach einem Tab-Wechsel bleibt der generische Pfad fuer 1 Sekunde still
   (SocialTabGraceS), damit ihm nicht doch noch etwas dazwischenfunkt.
   Das ist KEINE Umgehung von Spiellogik, sondern eine Reihenfolge-Regel
   im Sprachlayer: ein Kontext, der von seinem eigenen Inhalt
   abgeschnitten wird, ist schlimmer als nutzlos.
3. Das Log sagt jetzt zusaetzlich, ob im Fenster ueberhaupt eine Liste
   gefunden wurde ("Liste gefunden/NICHT gefunden").

Build 0/0, deployt (5.5.0.0). Versionen csproj + Plugin.cs synchron.

### Beim naechsten Test (V5.5)
1. "Version 5 Punkt 5 bereit".
2. O druecken und Registerkarte wechseln: kommt jetzt die VOLLE Ansage
   "<Karte>, Registerkarte X von 4, N Eintraege: <Eintrag>"?
3. Danach mit den Pfeiltasten durch die Eintraege: werden sie einzeln
   angesagt?
4. Falls im Log "Liste NICHT gefunden" steht, ist die Freundesliste
   anders aufgebaut als unsere Listen-Erkennung erwartet - dann brauche
   ich einen Strg+F5-Dump bei offenem Fenster.

### Noch offen / Idee fuer danach
Eine Abfragetaste "wo bin ich" fuer das Online-Fenster (Strg+F10 koennte
die aktive Registerkarte mit ansagen). Nicht gebaut, weil der Kernfix
Vorrang hatte.

---

## STAND 2026-07-19 (V5.4: Online-Fenster, Registerkarten)

User-Wunsch: "das online menue barrierefrei machen was man mit o aufmacht
so das ich weis was welche registerkarte ist wie freunde gruppen usw".

WAS NEU IST:
Beim Oeffnen des Fensters kommt "Online-Fenster. <Karte>, Registerkarte
X von 4."; bei jedem Wechsel die neue Karte. Die generische
Listen-Navigation laeuft danach WEITER (kein return im Handler), liest
also weiterhin den Inhalt der gewaehlten Karte.

QUELLENLAGE (ilspycmd, ohne Dump ermittelt - in docs/game-api.md):
- Taste O = MENU_PARTY_MEMBER (271) laut Keybind-Dump, Addon "Social".
- `AddonSocial` haelt die vier Karten als AtkComponentRadioButton*:
  PartyMembersRadioButton@680, FriendListRadioButton@688,
  BlacklistRadioButton@696, PlayerSearchRadioButton@704.
- Aktiv = `AtkComponentButton.IsChecked` (Bit 18 der Flags).
- Das gesprochene Label kommt aus `ButtonTextNode`, ist also die
  LOKALISIERTE Spielbeschriftung, keine eigene Uebersetzung. Die
  Fallback-Liste (Gruppenmitglieder/Freundesliste/Schwarze Liste/
  Spielersuche) greift nur, wenn der Textknoten leer ist - das Log sagt
  pro Ansage, welche Quelle benutzt wurde.

Build 0/0, deployt (5.4.0.0). Versionen csproj + Plugin.cs synchron.

### Beim naechsten Test (V5.4)
1. "Version 5 Punkt 4 bereit".
2. O druecken: kommt "Online-Fenster. <Karte>, Registerkarte X von 4"?
3. Karte wechseln: wird die neue jedes Mal angesagt?
4. Stimmen die Namen mit dem ueberein, was im Spiel steht? Das Log zeigt
   unter "[Social]", ob das Label aus dem ButtonTextNode kam oder aus der
   Fallback-Liste (letzteres waere ein Hinweis, dass wir den falschen
   Textknoten lesen).
5. Wird der INHALT der Karte vorgelesen (Freundesliste durchblaettern)?
   Falls nicht: das ist der naechste Schritt - dann brauche ich einen
   Strg+F5-Dump bei offenem Fenster.

---

## STAND 2026-07-19 (V5.3: eigener Name statt Empfaengername)

User-Meldung: "beim chat soll wenn ich was geschrieben hab mein name
kommen nicht der name an den ich schreibe".

URSACHE: Bei einem AUSGEHENDEN Tell traegt das Spiel den EMPFAENGER im
Sender-Feld. Der Nachrichten-Puffer hat dieses Feld ungeprueft als
Absender uebernommen (Zeile "archived = senderText: text") - die eigene
Zeile sah im Verlauf also aus, als haette sie der andere geschrieben.

FIX: Die Eigen-Erkennung (isOwn) wird jetzt VOR dem Archivieren
bestimmt, damit Puffer und Sprachansage dieselbe Wahrheit benutzen.
- Puffer: eigene Zeilen stehen unter dem EIGENEN Charakternamen.
- Der Empfaenger geht nicht verloren, sondern wird zum Adressaten:
  "<mein Name> an <Empfaenger>: <Text>" - beim Nachlesen eines Gespraechs
  ist sonst nicht mehr erkennbar, wem man geantwortet hat.
- Sprachansage entsprechend: "Du fluesterst an <Empfaenger>: <Text>".

Build 0/0, deployt (5.3.0.0). Versionen in csproj UND Plugin.cs geprueft
synchron (der Fehler aus V4.98-V5.0).

### Beim naechsten Test (V5.3)
1. "Version 5 Punkt 3 bereit".
2. /tell an jemanden: kommt "Du fluesterst an <Name>: <Text>"?
3. Im Puffer nachlesen: steht dort DEIN Name vorne, nicht der des
   Empfaengers?
4. /say: kommt "Du sagst: <Text>" und im Puffer dein Name?

### V5.2 (in V5.3 enthalten)
Vier fehlende Kanaele ergaenzt: TellOutgoing (12), Yell (30),
CrossParty (32), Echo (56) - sie fielen vorher in den `_ => false`-Zweig
und wurden weder gesprochen noch archiviert. Dazu die [Chat]-Probe im
Log (kind/sender/gelesen/text), mit der ein stummer Kanal ablesbar wird.

---

## STAND 2026-07-19 (V5.2: eigene Chat-Nachrichten)

User-Wunsch: "wenn ich in den chats schreibe soll meine nachricht auch
wieder gegeben werden wenn ich enter druecke und im buffer landen".

GEFUNDENE URSACHE (ilspycmd Dalamud XivChatType, 2026-07-19):
In ShouldRead fehlten VIER Kanaele komplett - sie fielen in den
`_ => false`-Zweig und wurden weder gesprochen NOCH archiviert:
- `TellOutgoing` (12) = eigenes /tell
- `Yell` (30) = /yell
- `CrossParty` (32) = welteruebergreifende Gruppe
- `Echo` (56) = /echo
Eigene Say/Party/Shout/FC-Nachrichten dagegen kommen als der normale
Kanal-Typ mit dem eigenen Namen als Sender an und sollten schon vorher
funktioniert haben - ob sie es taten, klaert jetzt die Probe (siehe unten).

WAS NEU IST:
1. Die vier Kanaele sind ergaenzt, in ShouldRead, MapCategory (Buffer!)
   und den Prefixen.
2. Eigene Nachrichten werden als solche angesagt: "Du sagst: ...",
   "Du fluesterst: ...", "Du zur Gruppe: ..." statt "Sagt von <eigener
   Name>: ...". Erkennung: TellOutgoing, oder Sender == eigener
   Charaktername (ObjectTable.LocalPlayer). Ohne Zeichen-Echo in der
   Eingabezeile ist diese Zeile die EINZIGE Bestaetigung, dass das
   Getippte rausgegangen ist - sie muss sofort von fremder Rede
   unterscheidbar sein.
3. PROBE (bleibt drin): jede Nicht-Kampf-Chatzeile wird geloggt als
   "[Chat] kind=<Name> (<Zahl>) sender='..' gelesen=True/False text='..'".
   Damit ist ein stummer Kanal kuenftig ablesbar statt Ratesache.
   Kampflog ist vorher schon herausgefiltert, also keine Log-Flut.

Build 0/0, deployt (5.2.0.0).

### Beim naechsten Test (V5.2)
1. "Version 5 Punkt 2 bereit".
2. Etwas in /say schreiben, Enter: kommt "Du sagst: <Text>"?
3. Ein /tell an jemanden: kommt "Du fluesterst: <Text>"?
4. Landen beide im Nachrichten-Puffer (Verlauf durchblaettern)?
5. Falls weiterhin still: die "[Chat]"-Zeilen im Log zeigen, welcher
   kind= beim Enter-Druecken ankommt und ob gelesen=False steht.

---

## STAND 2026-07-18 (V5.1: Wegenetz-Ladefortschritt wird angesagt)

User-Wunsch: "wenn das wegenetz laed das es alle 20% angesagt wird damit
man weiss das es geladen bzw fertig ist".

WAS NEU IST (AutoWalkService.MonitorMeshBuild):
- Aufbau startet -> "Wegenetz wird geladen."
- danach "Wegenetz 20/40/60/80 Prozent."
- Ende -> "Wegenetz fertig geladen." bzw. "Wegenetz-Aufbau abgebrochen."
  (unterschieden ueber Nav.IsReady)
Laeuft auch OHNE aktiven Auto-Lauf - der Sinn ist ja gerade zu wissen,
wann man wieder laufen kann. Abschaltbar: Config AnnounceMeshProgress.

QUELLENLAGE (vnavmesh NavmeshManager dekompiliert, 2026-07-18):
`LoadTaskProgress` ist -1 solange kein Aufbau laeuft, wird beim Start auf
0 gesetzt, waechst in BuildTiles auf 1 und wird in einem OnDispose wieder
auf -1 gesetzt. Fertig = der Ruecksprung auf -1; ob erfolgreich oder
abgebrochen sagt NUR Nav.IsReady. Aus dem Tile-Cache bediente Ladevorgaenge
koennen so schnell sein, dass keine Zwischenstufe sichtbar wird - dann
kommen nur Start und Ende. Das ist korrekt und kein Fehler.

Build 0/0, deployt (5.1.0.0).

### WICHTIG: Versionsansage war seit V4.98 falsch (mein Fehler)
Die csproj-Version und die Konstante `PluginVersion` in Plugin.cs muessen
synchron sein (steht als Kommentar in der csproj) - ich habe bei V4.98,
V4.99 und V5.0 nur die csproj angehoben. Das Plugin hat deshalb weiter
"Version 4 Punkt 97 bereit" gesagt, obwohl der neue Code lief. Ab V5.1
sind beide wieder synchron. Merke fuer kuenftige Versionsspruenge: BEIDE
Stellen aendern, und die csproj NICHT per PowerShell-Ersetzung anfassen
(zerstoert Umlaute und trifft auch "DalamudPackager 15.0.0").

### Beim naechsten Test (V5.1)
1. "Version 5 Punkt 1 bereit" - jetzt stimmt die Zahl wieder.
2. Zonenwechsel: kommt "Wegenetz wird geladen", dann Prozentschritte,
   dann "Wegenetz fertig geladen"?
3. Falls nur Start und Ende kommen: war der Aufbau aus dem Cache (schnell)?
   Das Log zeigt die Schritte mit progress-Werten.
4. Nervt die Ansage bei jedem Zonenwechsel? Dann AnnounceMeshProgress aus.

### V5.0 BESTAETIGT (User, 2026-07-18: "das mit dem automatisch laufen
### ist auch ok")
Auto-Lauf sagt nur noch alle 50 zurueckgelegten Meter "Noch X Meter".

---

## STAND 2026-07-18 (V5.0: Auto-Lauf spricht nur noch bei Fortschritt)

User-Meldung: "was wir weg machen muessen ist bei auto laufen die
staendige meter ansage".

WAS GEAENDERT WURDE:
"Noch X Meter" haengt nicht mehr an der Uhr (alle 3 s), sondern am
zurueckgelegten Weg: eine Zeile pro 50 zurueckgelegten Metern
(Config `AutoWalkProgressStep`, 0 = ganz aus). Kurze Laeufe bleiben
damit komplett still, ein langer Lauf meldet sich eine Handvoll Mal.

WARUM NICHT ERSATZLOS RAUS: die Ansage wurde am 2026-07-11 GENAU DESHALB
eingebaut, weil der Beacon-Ton allein den User im Unklaren liess und er
Laeufe abgebrochen hat (steht als Kommentar im Code). Die Rueckmeldung
"es geht voran" bleibt also erhalten, nur die Dauerbeschallung faellt
weg. Nebeneffekt der Distanz-Kopplung: ein blockierter oder sehr
langsamer Lauf plappert nicht mehr, waehrend gar nichts passiert -
Stille bedeutet jetzt "kein Fortschritt" und ist selbst ein Signal.

Build 0/0, deployt (5.0.0.0).

### Beim naechsten Test (V5.0)
1. "Version 5 Punkt 0 bereit".
2. Langer Auto-Lauf: kommt "Noch X Meter" nur noch alle ~50 m?
3. Kurzer Auto-Lauf (unter 50 m): komplett still bis "angekommen"?
4. Reicht das als Rueckmeldung, oder fuehlt es sich zu still an? Falls zu
   still: AutoWalkProgressStep auf 25 setzen. Falls immer noch zu viel:
   auf 100 oder auf 0 (ganz aus).

### V4.99 BESTAETIGT (User, 2026-07-18: "ok funktioniert")
Quests nach Stufe sortiert, Stufe wird angesagt. OFFEN BLEIBT die
Log-Frage: stehen bei "lvlMarker=" echte Werte oder ueberall 0? Davon
haengt ab, ob der ungenaue Namens-Fallback bleiben muss.

---

## STAND 2026-07-18 (V4.99: Quests nach Stufe sortiert)

User-Wunsch: die Quests nach Stufe sortieren - annehmbare Quests UND
Quest-Ziele, beide im Objekt-Browser (das Journal des Spiels ist davon
NICHT betroffen, dessen Reihenfolge legt das Spiel fest).

WAS NEU IST:
1. Beide Quest-Kategorien sind jetzt nach Stufe sortiert. Reihenfolge:
   erst was im aktuellen Gebiet liegt, dann Stufe aufsteigend, dann
   Entfernung. Erreichbarkeit schlaegt Stufe bewusst - eine passende
   Quest drei Gebiete weiter ist nicht das naechste Laufziel.
   Unbekannte Stufe (0) sortiert ans ENDE, nicht als "Stufe 1".
2. Die Stufe wird angesagt: "1 von 5: Stufe 15, Story: <Quest>, ...".
   Ohne Ansage waere die Sortierung eine stumme Regel. Bei unbekannter
   Stufe entfaellt der Teil - kein erfundenes "Stufe 0".

QUELLENLAGE (ilspycmd-verifiziert, 2026-07-18):
- `MapMarkerData.RecommendedLevel` (ushort @64) - der Marker traegt die
  Stufe SELBST, kein Raten ueber den Quest-Namen noetig. Gegenprobe im
  Struct: SetData(.., ushort recommendedLevel, ..).
- FALLBACK Lumina `Quest.ClassJobLevel[0]` per Namensabgleich, falls das
  Spiel RecommendedLevel auf 0 laesst (Laufzeitverhalten UNBEKANNT).
  Namensabgleich ist unpraezise (FFXIV vergibt Quest-Namen mehrfach,
  z.B. Wiederholbare) - deshalb nur Rueckfall, nie erste Wahl.
- Beide Werte stehen pro Marker im Log ("lvlMarker=.. lvlSheet=..") -
  daraus laesst sich nach dem Test entscheiden, ob der Fallback ueberhaupt
  je greift und ob die Werte uebereinstimmen.

Build 0/0, deployt (4.99.0.0).

### Beim naechsten Test (V4.99)
1. "Version 4 Punkt 99 bereit".
2. Kategorie "Annehmbare Quests" durchblaettern: kommen die Stufen mit,
   und steigen sie an?
3. Dasselbe in "Quest-Ziele".
4. Stimmen die Stufen mit dem ueberein, was im Journal steht?
5. LOG-FRAGE (wichtiger als sie klingt): stehen in den "[Quest]"- bzw.
   "[OpenQuest]"-Zeilen bei lvlMarker echte Werte oder ueberall 0? Davon
   haengt ab, ob der ungenaue Namens-Fallback ueberhaupt bleiben muss.

---

## STAND 2026-07-18 (V4.98: Karten-Markierung als Ziel)

Aufgegriffen aus `docs-de/ideen/ff14-small-hints.md` Punkt 2 (Vorschlag des
KOTOR-Accessibility-Mods). Die Flagge ist in Gruppen die Art, wie sich
Spieler dirigieren ("geh zur Markierung") - fuer einen blinden Spieler war
sie bisher unsichtbar.

WAS NEU IST:
1. Die gesetzte Karten-Markierung erscheint als Wegpunkt "Markierung" in
   der Kategorie Wegpunkte. Damit ist sie ohne Sonderweg auch Ziel fuer
   Gehhilfe und Auto-Lauf - sie laeuft durch denselben Pfad wie jeder
   andere Wegpunkt (PlacesService.GetPlaces).
2. Wird eine NEUE Markierung gesetzt, wird sie angesagt:
   "Neue Markierung, 120 Meter, Nordosten." Kompassrichtung, weil die
   Flagge ein Ziel zum Planen ist, keine Lenkanweisung. Nur bei echter
   Neuplatzierung (>= 1 m Abstand zur vorigen), Zonenwechsel schaerft die
   Ansage neu. Abschaltbar via Config AnnounceMapFlag.

QUELLENLAGE (ilspycmd-verifiziert, in docs/game-api.md):
AgentMap.FlagMarkerCount + FlagMapMarkers[0] mit TerritoryId/MapId/
XFloat/YFloat. XFloat/YFloat sind WELT-Koordinaten (X und Z), NICHT
Karten-Pixel - bewiesen durch AgentMap.SetFlagMapMarker(.., Vector3 world),
das world.X/world.Z genau dorthin schreibt. Die Pixel-Umrechnung der
anderen Wegpunkte gilt hier also ausdruecklich nicht.

Build 0/0, deployt (4.98.0.0).

### Beim naechsten Test (V4.98)
1. "Version 4 Punkt 98 bereit".
2. Markierung auf der Karte setzen (oder von einem Gruppenmitglied setzen
   lassen): kommt "Neue Markierung, X Meter, <Kompassrichtung>"?
3. Stimmt die Richtung? Gegenprobe: hinlaufen und pruefen, ob die Distanz
   faellt. Falls die Richtung gespiegelt ist, steht der Rohwert im Log
   unter "[Nav] Neue Karten-Markierung: pos=.. dist=.. <Richtung>".
4. Kategorie Wegpunkte durchblaettern: taucht "Markierung, Markierung"
   auf, und fuehrt Numpad 3 dorthin?
5. Wird die Ansage NICHT wiederholt, solange die Flagge liegen bleibt?

### V4.97 BESTAETIGT (User, 2026-07-18: "das mit den untertiteln
### funktioniert")
Untertitel-Fix haelt: jede Zeile genau einmal, bei wachsenden Zeilen nur
der neue Teil. Damit ist der Dialog-/Untertitel-Block abgeschlossen -
JoinDistinctParts und die _lastSpokenDialog-Praefixlogik sind bewaehrt.

---

## STAND 2026-07-18 (V4.97: Untertitel)

### User-Meldung: "die Untertitel werden auch mehrfach vorgelesen"
LOG-BEWEIS (Dialog-Nodes-Probe, 11:20-11:34) - ZWEI Ursachen:
1. TalkSubtitle haelt JEDE Zeile in DREI Text-Nodes (id2, id3, id4) mit
   identischem Inhalt. Der Join hat daraus
   'Hoer hin .... Hoer hin .... Hoer hin ...' gemacht.
2. Zwischensequenz-Untertitel WACHSEN im selben Node:
   11:20:23 'Hoer hin ...' -> 11:20:33 'Hoer hin ... Fuehl es ...' ->
   11:21:06 'Hoer hin ... Fuehl es ... Denk nach ...'. Jede Erweiterung
   wurde komplett neu vorgelesen, der Anfang also drei Mal.

FIX (beides):
1. OnTalkUpdate joint Segmente jetzt ueber JoinDistinctParts (wie Listen);
   ReadAllTexts ebenso (Separator ". ").
2. Waechst der Text und beginnt mit dem zuletzt Gesprochenen, wird NUR der
   neue Teil angesagt (_lastSpokenDialog je Addon, beim Schliessen
   geleert). Eine komplett neue Zeile ist nicht betroffen und wird voll
   vorgelesen. Geloggt als "nur der neue Teil wird gesprochen".

Build 0/0, deployt (4.97.0.0).

### Beim naechsten Test (V4.97)
1. "Version 4 Punkt 97 bereit".
2. Zwischensequenz mit Untertiteln: jede Zeile genau EINMAL, und bei
   wachsenden Zeilen nur der neue Teil?
3. Normale NPC-Dialoge weiterhin vollstaendig (Sprechername + Text)?

---

## STAND 2026-07-18 (V4.96: Doppel-/Dreifach-Ansagen entfernt)

### User-Meldung: "manche Meldungen kommen doppelt bis dreifach"
LOG-BEFUND (11:24 / 11:26, Antwort-Auswahl im Gespraech mit Brennan) -
eine einzige Antwortzeile erzeugte VIER Ansagen:
1. 'Ja, 2 Eintraege'      (Menue-Kopf beim Oeffnen)
2. 'Ja'                   (Listen-/Fokus-Leser)
3. 'Ja, Ja, Ja, Ja'       (!)
4. 'Ja'
Gleiches Muster bei 'Um Staerke zu erlangen.' (4x).

ZWEI URSACHEN, beide gefixt:
1. WIEDERHOLTER TEXT INNERHALB EINER ANSAGE: FFXIV-Listenzeilen enthalten
   dasselbe Label mehrfach als Text-Node (Schatten-/Highlight-Kopien im
   ListItemRenderer). GetTextFromNodeTree und ReadListItemText haben stumpf
   alle zusammengehaengt. Neu: JoinDistinctParts behaelt je Teil nur das
   erste Vorkommen -> aus 'Ja, Ja, Ja, Ja' wird 'Ja'.
   (Danach greift der bestehende 0,5-s-Debounce fuer die Wiederholungen.)
2. KOPFZEILE + NACKTES LABEL: 'Ja, 2 Eintraege' gefolgt von 'Ja' ist fuer
   den Debounce nicht identisch. Neu in TolkService.SpeakInterrupt:
   beginnt die vorige Ansage (< 1 s) mit genau diesem Text plus Komma,
   wird die nackte Wiederholung unterdrueckt ([Speak] TEIL-DEBOUNCED).
   Wirkt auch bei 'Elezen, maennlich' -> 'Elezen'.

Build 0/0, deployt (4.96.0.0). V4.95 (Beschreibungs-Reihenfolge) ist darin
enthalten und weiterhin ungetestet.

### V4.94-4.96 BESTAETIGT (User, 2026-07-18: "ok funktioniert")
Damit ist der Charaktererstellungs-Block abgeschlossen:
- Volksbeschreibung kommt beim Blaettern (Hover-Nachstellung)
- Reihenfolge "Volk, Geschlecht" -> vollstaendige Beschreibung, kein Abbruch
- keine Doppel-/Dreifach-Ansagen mehr (Zeilen-Dedup + TEIL-DEBOUNCE)
NOCH NICHT COMMITTET/RELEASED - letzter Release ist v4.73.

### Offen / naechste Kandidaten
- Commit + Release v4.96 (Ablauf steht in der Release-Notiz zu v4.73)
- Login-Geplapper (User: "nicht so schlimm", zurueckgestellt)
- _StatusCustom0-Countdown + _FlyText-Spamfilter (nie beauftragt)
- Sounds austauschen (User-Wunsch "bei Gelegenheit", Sinus -> angenehmer)

---

## STAND 2026-07-18 (V4.95: Beschreibung wird nicht mehr abgeschnitten)

### V4.94 BESTAETIGT (Log 11:08-11:09) - Hover-Hypothese war richtig
Alle 8 Voelker liefern beim Blaettern jetzt ihren Beschreibungstext
(Hyuran 493 / Elezen 482 / Lalafell 392 / Miqo'te 437 / Roegadyn 537 /
Au Ra 707 / Hrothgar 545 / Viera 655 Zeichen), danach auch der
Volksstamm-Schritt (Wieslaender inkl. Attributen). Beide Events waren
vorhanden - Event-Inventar der Zeile: MouseOver, MouseOut, ButtonClick.

### ABER: Ansage-Reihenfolge zerschnitt den Text (deshalb hoerte der User nichts)
Log 11:08:56: .881 Beschreibung (Speak) -> .886 SpeakInterrupt
"Elezen, maennlich". Die Kopfansage kam 5 ms SPAETER und hat die
Beschreibung sofort abgewuergt. Dazu wurde der Volksname doppelt gesagt
(Fokus-Leser "Elezen" + RaceGender "Elezen, maennlich").

### V4.95 (gebaut + deployt, 0/0)
1. OnCharaMakeHelpUpdate spricht nicht mehr sofort, sondern PUFFERT den
   Text; die RaceGender-Ansage gibt ihn direkt nach der Kopfzeile frei
   (Speak, nicht interruptend). Reihenfolge jetzt:
   "Elezen, maennlich" -> vollstaendige Beschreibung.
2. Fallback: der Frame-Tick (UpdateGlobalFocus) spricht einen Puffer, dem
   nach 250 ms keine Kopfzeile folgt - deckt das Oeffnen des Fensters und
   den Volksstamm-Schritt ab, wo die Beschreibung allein kommt.
3. Doppelter Volksname weg: TrySelectFocusedCharaMakeRow gibt jetzt bool
   zurueck; bei echter Auswahl schweigt der generische Fokus-Leser.

### Beim naechsten Test (V4.95)
1. "Version 4 Punkt 95 bereit".
2. Volk & Geschlecht durchblaettern: pro Volk EINMAL "Volk, Geschlecht"
   und danach die KOMPLETTE Beschreibung, ohne Abbruch?
3. Ist die Beschreibung beim schnellen Blaettern zu lang/stoerend, koennen
   wir sie auf Wunsch auf eine Taste legen statt automatisch zu sprechen.

---

## STAND 2026-07-18 (V4.94: Hover-Nachstellung fuer die Beschreibung)

### V4.93-TEST AUSGEWERTET (Log 10:58-10:59)
User: "liest Namen und Geschlecht vor, aber nicht die Beschreibung".

BEWIESEN, dass V4.93 im Kern FUNKTIONIERT - der synthetische Klick bewegt
die ECHTE Auswahl, nicht nur ein Anzeige-Bit:
- "Vorschau sichtbar" wechselt beim Blaettern das 3D-Modell mit:
  [200]=Hyuran m, [204]=Elezen m, [208]=Lalafell m, [201]/[205] weiblich.
- Beim Fokus zurueck auf Hyuran wird ERNEUT geklickt - das geht nur, wenn
  die Auswahl vorher wirklich auf Elezen stand (Gleichheits-Guard).
- Geschlecht bleibt beim Volkswechsel erhalten (Slot-Trick greift).

EINZIGE Luecke: _CharaMakeHelp id=4 behaelt konstant den Hyuran-Text
(len=493, [HelpProbe] "Text unveraendert"), auch wenn die Auswahl steht.
Der Beschreibungstext haengt also NICHT am Auswahl-Zustand.

### V4.94: Maus-Hover nachgestellt (HYPOTHESE, markiert)
Vermutung: das Spiel fuellt den Hilfetext aus dem MouseOver-Handler der
Zeile - ein Klick direkt auf die Checkbox erreicht ihn nie.
TrySelectFocusedCharaMakeRow feuert jetzt vor dem Klick
MouseOut (alte Zeile) + MouseOver (neue Zeile), gesucht auf der Zeile und
ihren Komponenten-Kindern (Tiefe 2, Enthaltensein statt ParentNode-Aufstieg).
AtkEventType-Werte ilspycmd-verifiziert (MouseOver=6, MouseOut=7).
Einmal pro Sitzung wird das Event-Inventar der Zeile geloggt
([RaceSelect] Events der Zeile: [...]) - bleibt es stumm, nennt das Log die
tatsaechlich vorhandenen Events statt uns erneut raten zu lassen.
Build 0 Fehler / 0 Warnungen, deployt (4.94.0.0).

### Beim naechsten Test (V4.94)
1. "Version 4 Punkt 94 bereit".
2. Volk & Geschlecht durchblaettern: kommt nach Name + Geschlecht jetzt
   auch die Beschreibung?
3. Falls nicht: Log-Zeile "[RaceSelect] Events der Zeile: [...]" schicken.
   FALLBACK ist schon recherchiert: Beschreibungen koennten als Spieldaten
   im Lumina-Sheet "Lobby" liegen (Spalten Text/Unknown0/Unknown1,
   Zuordnung ueber CharaMakeType.CharaMakeStruct[].Menu) - dann lesen wir
   sie direkt statt die UI zum Umschreiben zu zwingen.

---

## STAND 2026-07-18 (V4.93: Volk-Auswahl folgt dem Fokus)

### URSACHE BEWIESEN (V4.92-Proben, Log 10:34-10:35)
- 10:34:31 [HelpProbe] "Text unveraendert (Laenge 493)" = Hyuran-Text.
  Danach blaettert der User 10:34:33-39 durch ALLE 8 Voelker (Elezen,
  Lalafell, Miqo'te, Roegadyn, Au Ra, Hrothgar, Viera) -> KEINE einzige
  Zustandsaenderung. _CharaMakeHelp bleibt auf dem GEWAEHLTEN Volk stehen.
- [DescProbe] bei der Stamm-Auswahl listet ALLE CharaMake-Addons: nur
  _CharaMakeHelp id=4 traegt einen echten Beschreibungstext, alle anderen
  nur statische Hilfetexte ("Bestimme das Aussehen deines Charakters").
  Beim Blaettern existiert also NIRGENDWO ein Text zum markierten Volk.
- Gegenprobe: 10:35:09 echte Stamm-AUSWAHL -> Beschreibung kam sofort
  ("Der Volksstamm der Wieslaender ..."), begleitet von "Tribe gewaehlt".
  Blaettern zu "Hochlaender" danach -> wieder nichts.
- Fokus-Pfad geklaert: [Focus] id=5, pro Volk eigener Node-Pointer. Die
  Pfeiltasten bewegen NUR den globalen Fokus, nicht die Auswahl.

FAZIT: kein kaputter Handler. Das Spiel schreibt Beschreibung + Vorschau
nur bei echter AUSWAHL um. Mit der Maus faellt das nicht auf (ein Klick
waehlt sofort aus); bei Tastaturnavigation klafft die Luecke.

### V4.93: Auswahl zieht dem Fokus nach (User-Entscheid)
User waehlte "Blaettern waehlt aus" - Paritaet zum Mausklick.
TrySelectFocusedCharaMakeRow (aufgerufen aus UpdateGlobalFocus, nur bei
echtem Fokuswechsel):
1. Ermittelt, welcher Checkbox-SLOT (Node id=3 oder 4) aktuell gecheckt ist.
2. Findet die Zeile, in der der Fokus-Node sitzt (Fokus liegt auf id=5,
   also innerhalb der Zeilen-Komponente - Parent-Kette wird hochgeklettert).
3. Klickt in DIESER Zeile die Checkbox mit DEMSELBEN Slot, per Dispatch des
   registrierten Klick-Events (bewaehrter PressFocusedOk-Pfad).
Ist die fokussierte Zeile schon die gewaehlte, passiert nichts (kein
Klick-Sturm). Fehlende Checkbox/fehlendes Event werden geloggt statt still
verschluckt ([RaceSelect]-Zeilen).
CLEVER DABEI: das Geschlecht bleibt erhalten, OHNE die bis heute ungeklaerte
Symbol-Zuordnung (U+00AE / U+00A9) zu kennen - es wird schlicht derselbe
Node-Slot geklickt, der vorher gecheckt war.
Build 0/0, deployt (Manifest 4.93.0.0). Die V4.92-Proben bleiben drin.

### Beim naechsten Test (V4.93)
1. "Version 4 Punkt 93 bereit".
2. Volk & Geschlecht, mit Pfeiltasten blaettern: kommt jetzt nach jedem
   Volksnamen die Beschreibung? Wechselt das Geschlecht dabei NICHT?
3. Log-Kontrolle bei Problemen: [RaceSelect] zeigt jeden Klick;
   "Kein Klick-Event registriert" hiesse, der Dispatch-Pfad passt nicht
   (dann Checkbox-Kind statt Komponenten-Node anklicken).
4. Volksstamm-Schritt: dort blaettert es weiterhin ohne Auswahl (Fix ist
   bewusst erst nur fuer Volk & Geschlecht) - sagt der User, dass es dort
   genauso stoeren soll, ziehen wir es nach.

---

## STAND 2026-07-18 (V4.92 = Diagnose-Proben, gebaut + deployed)

### User-Meldung: Beschreibung kommt "wieder nicht" nach dem Rassennamen

LOG-AUSWERTUNG (dalamud.log 2026-07-18 10:26, [Speak] zeigt jede Ansage):
- 10:26:43.873 Beschreibung Hyuran WIRD gesprochen (einmal, beim Oeffnen)
- 10:26:45.416 INT 'Elezen'  -> KEINE Beschreibung
- 10:26:46.183 INT 'Hyuran'  -> KEINE Beschreibung
Es gibt NUR EINE "CharaMake-Beschreibung"-Zeile im ganzen Log. Der Handler
feuert also beim Oeffnen und danach nie wieder.

ZWEITER BEFUND (wichtig): beim Blaettern folgt KEIN "RaceGender gewaehlt"-
Log. Die Ansage 'Elezen' kam ueber den Event-Target-/Hover-Pfad, die
CHECKBOX-AUSWAHL blieb auf Hyuran stehen.

VERMUTUNG (NICHT BEWIESEN, deshalb Probe statt Fix): das Spiel schreibt
_CharaMakeHelp id=4 nur um, wenn ein Volk wirklich AUSGEWAEHLT wird, nicht
beim blossen Durchblaettern/Hovern. Dann gaebe es beim Blaettern schlicht
keinen Elezen-Text zu lesen, und der V4.83/84-Ansatz waere prinzipiell an
die Auswahl gebunden - beim damaligen Test hat der User die Voelker
vermutlich tatsaechlich ausgewaehlt. Alternativen (Text steht woanders,
Node/Addon unsichtbar) sind ebenso moeglich; der Handler hatte DREI stille
Ausstiege, aus dem Log war der Grund nicht ableitbar (Diagnose-Falle).

### V4.92: zwei Audit-Proben (kein Fix - erst Ursache belegen)
1. [HelpProbe] in OnCharaMakeHelpUpdate: jeder bisher stille Ausstieg loggt
   jetzt seinen Grund ("Addon unsichtbar" / "Node id=4 fehlt" / "Node id=4
   unsichtbar" / "Text unveraendert (Laenge n)" / "gesprochen"). Nur bei
   ZUSTANDSWECHSEL, kein Frame-Spam.
2. [DescProbe] ProbeDescriptionLocation: durchsucht bei jedem Volk-/Stamm-
   Wechsel ALLE geladenen CharaMake-Addons nach sichtbaren Text-Nodes ab 40
   Zeichen und loggt Addon, Node-Id, Sichtbarkeit, Laenge, Textanfang.
   Trigger: Hover-Ansage (Event-Target) UND echte Auswahl - so ist
   unterscheidbar, ob der Text nur bei Auswahl erscheint.
Build 0/0, deployt (Manifest 4.92.0.0).

### Beim naechsten Test (V4.92)
1. "Version 4 Punkt 92 bereit".
2. Charaktererstellung -> Volk & Geschlecht. Erst NUR BLAETTERN (mehrere
   Voelker durchgehen, ohne auszuwaehlen).
3. Dann ein Volk WIRKLICH AUSWAEHLEN (Enter/Bestaetigen) und hoeren, ob
   die Beschreibung dabei kommt. Das ist der entscheidende Vergleich.
4. Danach Log an Claude. Die [HelpProbe]- und [DescProbe]-Zeilen zeigen,
   ob der Text beim Blaettern ueberhaupt existiert - daraus folgt der Fix:
   entweder anderen Node/anderes Addon lesen, oder die Beschreibung aus
   Lumina holen und selbst beim Blaettern ansagen.

---

## STAND 2026-07-18 (V4.91 released + Installer 1.1.0 mit Selbst-Update)

### INSTALLER 1.1.0: Selbst-Update (User-Wunsch, END-TO-END VERIFIZIERT)
User: "kann man in den installer auch einbauen das er wenns vom installer
updates gibt den auch nachlaed den alten beendet und den neuen gleich
startet? so das man nichts per hand runterladen muss?"

BEFUND VORAB: der vorhandene Hinweis-Mechanismus war TOTER CODE.
CheckInstallerUpdateHint las die Version per Regex aus dem Asset-NAMEN,
das Asset heisst aber versionslos "FF14AccessibilityInstaller.exe" - der
Regex traf nie, der Hinweis erschien nie. Ersatzlos entfernt.

UMGESETZT (Details + Entscheidungshistorie: docs/installer-architektur.md
Abschnitt 4.3):
- Versionsquelle ist das neue Release-Asset "installer.json"
  ({InstallerVersion, AssetName, Sha256}), NICHT der Dateiname - so bleibt
  der Download-Link stabil und die README-Anleitung stimmt weiter.
- Phase 1 (TrySelfUpdateAsync): Manifest lesen, bei hoeherer Version per
  MessageBox mit Downloadgroesse fragen (User-Entscheid: vorher fragen),
  Download nach %TEMP%, SHA256-Abgleich, neue EXE mit
  "--apply-update <Zielpfad> <PID>" starten, alte Instanz beenden.
- Phase 2 (SelfUpdate.cs): auf Ende der alten PID warten, sich selbst ueber
  die Original-EXE kopieren (20 Versuche a 500ms - Windows haelt die Datei
  kurz gesperrt), diese mit "--updated" starten.
- Neustart: Sprachdialog wird uebersprungen, Update wird per Dialog gemeldet,
  Installation laeuft automatisch weiter (User-Entscheid: sofort weiter).
  Ausserdem werden alte Downloads (je ~160 MB) aus %TEMP% geloescht.
- Scheitert das Ersetzen (Schreibschutz), wird das ehrlich gemeldet und der
  Installer arbeitet aus %TEMP% weiter - die Installation gelingt trotzdem.
- ParseVersionLoose fuellt jetzt IMMER auf 4 Stellen auf: "1.1.0" gilt sonst
  als KLEINER als "1.1.0.0" (nicht gesetzte Stellen = -1) und ein
  dreistelliger Manifest-Eintrag haette das Update still nie ausgeloest.

VERIFIKATION (nicht nur gebaut - real durchgespielt): kuenstlicher
1.0.0-Build gegen das echte Release v4.91, via UI-Automation gesteuert:
Erkennung -> Dialog ("1.1.0.0 ... etwa 154 Megabyte") -> Ja -> Download
(~20s) -> Hash ok -> Originaldatei von 1.0.0.0 auf 1.1.0.0 ersetzt ->
Neustart aus dem ORIGINALPFAD -> "Installer wurde auf 1.1.0.0 aktualisiert"
-> Installation lief automatisch durch -> Folgelauf meldet "Der Installer
ist aktuell" (KEINE Endlosschleife) -> Temp-Download aufgeraeumt.
Getestet wurde exakt die EXE, die im Release liegt.

WICHTIG FUER DEN UEBERGANG: die im Umlauf befindliche 1.0.0-EXE kennt den
Mechanismus noch nicht. Sie muss EINMAL von Hand ersetzt werden; ab 1.1.0
laeuft es automatisch.

VOM USER BESTAETIGT (2026-07-18): "ok funktioniert" - das Selbst-Update
laeuft auch beim Nutzer auf echtem Weg durch.

---

## STAND 2026-07-18 (V4.91 gebaut + deployed)

### V4.90-CHAT BESTAETIGT (User 2026-07-18): "das mit dem chat funktioniert"
Damit sind in-game bestaetigt: Tipp-Echo im Chat-Eingabefeld, Kanal-Ansage
beim Oeffnen/Wechseln, und der Nachlese-Browser inkl. der Tasten Komma und
Punkt (Strg+,/Strg+. Kategorie, ,/. blaettern). Der offene
VERIFIKATIONSPUNKT "sieht Dalamud VK 0xBC/0xBE?" ist damit erledigt: JA.

### V4.91: Kampflog-Vorlesen wieder ENTFERNT (User-Entscheid)
User: "ausser die kampf meldungen aber das mit dem kampf koennen wir auch
erstmal raus nehmen". Die V4.90-Fassung (Aktions-Zeilen Typ 43 vorlesen +
Roh-Log fuer den Eigen-Filter) kam in-game nie an; statt zu debuggen wurde
sie auf Wunsch zurueckgebaut. Rueckgebaut wurde:
- ChatReaderService: TryHandleCombat -> IsCombatLogLine (verwirft
  Kampflog-Zeilen 41-49 still, keine Ansage, kein [Combat]-Log, kein
  History-Eintrag). Der Filter bleibt bewusst drin, damit Kampflog-Verkehr
  hier explizit aussortiert wird statt durch ShouldRead zu fallen.
  IPluginLog-Abhaengigkeit des Service damit entfallen.
- Configuration: ReadCombatMessages entfernt.
- MessageHistoryService: Kategorie "Kampf" raus (Enum + Durchschalt-
  Reihenfolge + Name), damit beim Blaettern keine tote Kategorie kommt.
  Nachlese hat jetzt 8 Kategorien: Dialoge, Sagen, Rufen, Gruppe, Allianz,
  Fluestern, Freie Gesellschaft, System.
Build 0 Fehler/0 Warnungen, deployt (Manifest 4.91.0.0 verifiziert).
UNBERUEHRT: Chat-Empfang, Tipp-Echo, Kanal-Ansage, Nachlese-Browser.

### Beim naechsten Test (V4.91)
1. "Version 4 Punkt 91 bereit".
2. Chat wie gehabt: Empfangen, Tippen, Kanal, Nachlese - alles noch da?
3. Beim Durchschalten der Nachlese-Kategorien kommt KEIN "Kampf" mehr?
4. Im Kampf: keine Aktions-Ansagen mehr (Ruhe), aber HP/Ziel-Ansagen des
   CombatService (Strg+H, Kampf/Kampf vorbei) laufen weiter?
FALLS spaeter doch gewuenscht: der Weg ueber IChatGui war grundsaetzlich
richtig, die offene Frage war nur, ob Typ-43-Zeilen ueberhaupt ankommen -
das klaert ein Log mit aktiver Roh-Probe.

---

## STAND 2026-07-17 abends (V4.90 gebaut + deployed, UNCOMMITTET)

### Chat-EMPFANGEN BESTAETIGT (User 2026-07-17)
Eingehende Nachrichten werden vorgelesen — der ChatReaderService
(IChatGui.ChatMessage, Say/Ruf/Gruppe/Allianz/Fluester/FC/System/Fehler)
funktioniert in-game. War bis dahin nie bestaetigt.

### V4.90: Chat-SENDEN (Tipp-Echo + Kanal) + Chat NACHLESEN
User-Auftrag: "die chats barrierefrei machen". Empfangen laeuft (s.o.),
also SENDEN + Nachlesen. KEIN programmatisches Senden (ToS): das Spiel
oeffnet/tippt/sendet selbst (Enter/Tab/Alt), wir sagen nur an.

TEIL 1 - Tipp-Echo (BEIDES per Log 21:37 schon belegt):
- AddonChatLog.TextInput @608 (Direktzeiger), AtkComponentTextInput.
  IsActive = Gate "Eingabemodus offen", EvaluatedString = Text.
- OnChatLogUpdate (PostUpdate "ChatLog"), gegated auf IsActive; Tipp-Echo
  via SpeakTextEchoDiff. LOG-BEWEIS 21:37: 'f'->'ff'->'fff'->'ffff' und
  Loeschen zurueck bis '' -> Echo funktioniert.
- Generischer Fokus-Leser stumm bei IsChatInputActive().
- Config EchoChatInput (Default true).

TEIL 2 - Kanal-Ansage (GELOEST, User-Meldung "hoere nur chat eingabe"):
- AddonChatLog.CurrentChannelTextNode @335 (AtkTextNode) = Kanal-Label
  wie das Spiel es rendert (lokalisiert, kein int-Raten!). ReadChatChannel
  liest ->NodeText, sanitized. Ansage beim Oeffnen "Chat-Eingabe, <Kanal>"
  und bei Kanalwechsel waehrend des Tippens (Tab/Alt) der neue Kanal.
- (RaptureShellModule.ChatType lieferte im Test 1/2/4, aber der Textnode
  ist die verlaessliche Quelle - int->Name bleibt ungenutzt/ungesichert.)

TEIL 3 - Nachlese-BROWSER mit Kategorien (User-Wunsch, praezisiert):
"kanalwechsel mit strg+, und ., nachrichten im kanal lesen , und .,
buffer fuer dialoge und system getrennt".
- Neuer MessageHistoryService: pro Kategorie ein Ringpuffer (50).
  Kategorien (Durchschalt-Reihenfolge): Dialoge, Sagen, Rufen, Gruppe,
  Allianz, Fluestern, Freie Gesellschaft, System(+Fehler).
- ChatReaderService schreibt Chat rein (Kategorie per XivChatType, ohne
  Kanal-Prefix - Kategorie traegt ihn); UIReaderService.OnTalkUpdate
  spiegelt NPC-Dialoge in "Dialoge".
- Tasten (Komma/Punkt im Spiel NICHT belegt, Dump 2026-07-17):
  Strg+, / Strg+. = Kategorie zurueck/vor ("Gruppe, 4 Nachrichten" /
  "..., leer"); , / . = aeltere/neuere Nachricht ("i von n: text",
  Grenzen "Anfang/Ende des Verlaufs").
- Config KeyChatCatPrev/Next + KeyChatReadOlder/Newer. ERSETZT das
  V4.90-Provisorium Umschalt+F1/F2 (war uncommittet/ungetestet).
- ABSICHERUNG: KeyNameToVK += ","=0xBC "."=0xBE; UpdateKeyEdges prueft
  IKeyState.IsVirtualKeyValid (kein Crash) + loggt einmalig, falls das
  Spiel Komma/Punkt nicht trackt -> dann greifen die Tasten nicht und wir
  brauchen andere (VERIFIKATIONSPUNKT).

TEIL 4 - Kampflog: eigene Aktion vorlesen (User-Wunsch "wenn ein zauber
ausgefuehrt wird die meldung vom spiel hoeren"):
- Weg gewaehlt: echte Spiel-Meldung "Du wirkst X." aus dem Kampflog
  (via IChatGui, NICHT synthetisch). ChatReaderService.TryHandleCombat
  laeuft VOR ShouldRead. XivChatType-Basis (Low-7-Bits) Action=43 =
  Aktion eingesetzt (game-api.md "Kampflog").
- Erste Fassung liest ALLE Aktions-Zeilen (Typ 43, eigen UND fremd) +
  loggt jede roh ([Combat] Aktion type=0x…). PROBE: aus dem Log filtere
  ich dann den EIGEN-Code (hohe Bits) heraus, damit nur deine Aktionen
  kommen. Auch neue Nachlese-Kategorie "Kampf". Config ReadCombatMessages
  jetzt Default true.
Build 0/0, deployt (Manifest 4.90.0.0).

### Beim naechsten Test (V4.90)
1. "Version 4 Punkt 90 bereit".
2. Enter (Chat oeffnen): kommt "Chat-Eingabe, <Kanal>" MIT Kanalname
   (Sagen/Gruppe/...)? Kanal vorher wechseln (Alt+S/G/P/R): stimmt er?
   Wechsel WAEHREND offen (Tab): wird der neue Kanal angesagt?
3. Tippen: jedes Zeichen? Ruecktaste/"leer"? Enter senden: eigene
   Nachricht kommt als Vorlesung zurueck?
4. NACHLESE-BROWSER (WICHTIG - klaert ob Komma/Punkt greifen):
   - Strg+. mehrmals: schaltet die Kategorie durch (Dialoge -> Sagen ->
     ... -> System) mit Anzahl-Ansage? Strg+, zurueck?
   - In einer Kategorie mit Nachrichten , und . druecken: blaettert es
     "i von n: text"? Grenzen angesagt?
   - Falls GAR NICHTS passiert: Log-Warnung "VK 0xBC/0xBE wird nicht
     getrackt"? Dann sieht Dalamud Komma/Punkt nicht -> andere Tasten.
5. Falls Kanal leer bleibt (nur "Chat-Eingabe"): Log an Claude -
   CurrentChannelTextNode evtl. anders auszulesen.
6. KAMPFLOG: eine Aktion einsetzen/Zauber wirken -> kommt "Du wirkst X."?
   WICHTIG fuers Filtern: ein paar EIGENE Aktionen + (falls moeglich) eine
   FREMDE (Gegner/Gruppe) ausloesen, dann Log an Claude - die [Combat]-
   Zeilen (type=0x…) zeigen, wie eigen vs. fremd codiert ist, damit ich auf
   nur DEINE Aktionen filtere. Zu geschwaetzig? Sag Bescheid (dann nur
   Zauber mit Cast, oder Ein/Aus-Schalter).

---

## STAND 2026-07-17 abends (V4.89 COMMITTET + RELEASED v4.89)

### RELEASE v4.89 VEROEFFENTLICHT (17.07. abends)
Code-Commit 1e4ee57 (V4.82-V4.89) + repo.json-Commit 44ec959 (Version
4.89) gepusht. GitHub-Release v4.89 mit latest.zip /
FF14Accessibility-v4.89.0.zip (je 518046 Bytes, Release-Build,
Manifest 4.89.0.0 im Zip verifiziert) + Installer-EXE (unveraendert
seit 4.74er-Aktivierungs-Fix). latest-Link verifiziert (HTTP 200,
518046 Bytes). uia_test.ps1 weiterhin absichtlich uncommittet.
WICHTIG: V4.82-V4.89 (ganze Charaktererstellung) sind released, aber
noch NICHT in-game getestet - Testpunkte unten gelten weiter.



### V4.89: Namensfelder Vorname/Nachname werden benannt (User-Wunsch)
User: "die felder fuer vor und nachname muessen auch noch benannt
werden". Dump _CharaMakeCharaName (Desktop 17:57): zwei sichtbare
TextInputs (id=9/7) OHNE Label im Feld - die Labels stehen als
separater Top-Level-Text daneben (id=8 "Nachname", id=6 "Vorname").
FIX (UIReaderService): dedizierter Handler OnCharaMakeNameUpdate
(PostUpdate _CharaMakeCharaName):
- Fokus-Node -> enthaltendes sichtbares TextInput (FindFocusedName-
  Field, prueft node==field oder in dessen Kind-Liste).
- Bei Feldwechsel: Label + aktueller Inhalt ("Vorname" bzw.
  "Vorname, Max"). Label per PHYSISCHER NAEHE (X/Y Feld vs. kurze
  Top-Level-Texte, "/" gefiltert = keine Zaehler) - robuster als
  das id-1-Muster, sprachunabhaengig.
- Gleiches Feld, Inhalt geaendert: Tipp-Echo (EvaluatedString-Diff,
  gemeinsamer Helfer SpeakTextEchoDiff mit dem Kommentar-Feld).
- Generischer Fokus-Leser fuer Namensfelder stumm (IsFocusInside-
  NameField) - sonst spraeche er den Zaehler "0/15". Knoepfe
  (Bestaetigen/Zurueck) bleiben generisch lesbar.
Build 0/0, deployt (Manifest 4.89.0.0).
OFFEN/UNVERIFIZIERT: wie der Nutzer zwischen den Feldern wechselt
(Tab/Klick) - Laufzeit-Log war rotiert. Die Naehe-Paarung ist
gegenueber dem id-1-Muster abgesichert, aber ungetestet.

### Beim naechsten Test (V4.89)
1. "Version 4 Punkt 89 bereit".
2. Charaktererstellung bis zum Namensfenster ("Name des Charakters").
   In ein Feld gehen (Tab? Pfeil? Klick?): sagt er "Vorname" bzw.
   "Nachname"? Ins andere Feld: anderes Label?
3. Tippen: jedes Zeichen? Ruecktaste "X geloescht"? KEIN "0/15"-
   Zaehler-Gequatsche?
4. Knoepfe Bestaetigen/Zurueck: werden die noch normal angesagt?
5. Falls Label falsch/vertauscht: Log an Claude - [Name]-Zeile zeigt
   Feld-id + gewaehltes Label.

### V4.87/4.88 weiterhin ungetestet (in der 17:23-17:42-Session nicht ausgeloest)
- Picker-Pfeile (Frisur/Farb-Raster navigieren + Wirkung auf Vorschau)
- Tipp-Echo im Aussehen-Speichern-Kommentarfeld

---

## STAND 2026-07-17 abends (V4.88 gebaut + deployed, UNCOMMITTET)

### Neu entdeckt (User-Dump 17:42): "Charakterdaten speichern"-Dialog
User hat im Aussehen-Schritt den Speichern-Weg probiert (Ok ->
SelectYesno "Einstellungen speichern?" -> Ja). Drei neue Addons:
- CharaMakeDataExport ("CHARAKTERDATEN SPEICHERN"): List(9) mit 40
  Speicherslots, Zeilen MIT Text (id=6 Volksstamm/Geschlecht, id=5
  "Speicherslot N", id=4 Datum). FUNKTIONIERT SCHON KOMPLETT: Titel,
  "Menue, 40 Eintraege", Zeilen-Ansagen beim Navigieren (Hov2-Pfad),
  Enter -> Ueberschreiben-Dialog (CharaMakeDataImportDialog) wurde
  vorgelesen, Ok fuehrte weiter. KEIN Fix noetig.
- CharaMakeDataInputString: Kommentar-Dialog mit TextInput (Zaehler
  "0/40"), Speichern/Abbrechen. Oeffnungs-Ansage lief ("Kommentar.
  Das Aussehen von ... wird in Slot 2 gespeichert."), aber TIPPEN
  war stumm (Textfeld-Echo = aelteste offene Baustelle).

### V4.88: Tipp-Echo + zwei Absicherungen
1. TIPP-ECHO (OnCharaMakeInputUpdate, PostUpdate CharaMakeData-
   InputString): liest EvaluatedString der TextInput-Komponente
   (AtkComponentInputBase @224, ilspycmd-verifiziert) pro Frame,
   spricht die DIFFERENZ: getippte Zeichen / "X geloescht" /
   kompletten Text nach Editieren mittendrin / "leer".
   Erster genereller Textfeld-Echo-Baustein - wenn er sich hier
   bewaehrt, auf andere Textfelder (Chat, Suche) ausweiten.
2. Zaehler-Spam stumm: Addon in SpecialUpdateAddons (Scanner haette
   "1/40" + Inhalt pro Tastendruck unterbrechend gesprochen) +
   IsBareNumber-Guard im globalen Fokus-Leser (Fokus sitzt auf dem
   Zaehler-Node "3/40").
3. Picker-Navigation abgesichert: Pfeile greifen nur noch, wenn der
   CMF-Picker das OBERSTE Menue im Stack ist - Log 17:42 zeigte
   Stack [BgSelector, CMFIconHair, CharaMakeDataExport]: die Pfeile
   haetten sonst die VERSTECKTE Frisur-Liste unterm Speicher-Dialog
   bewegt.
HINWEIS: V4.87-Picker-Pfeile wurden in der Session NICHT getestet
(keine [Key]/[CMF]-Zeilen im Log; User hat stattdessen den
Speichern-Weg erkundet). Nebenbefund: [Key]-Zeilen erscheinen NUR,
wenn das Spiel die Pfeile nicht selbst verbraucht (17:24 Frisur-
Raster: Zeilen da; native Listen: keine) - IKeyState sieht offenbar
nur unverbrauchte Tasten. Gut fuer uns: kein Doppel-Navigieren.

### Beim naechsten Test (V4.88)
1. "Version 4 Punkt 88 bereit".
2. PICKER-PFEILE (V4.87, weiter ungetestet): Aussehen -> Frisur,
   Pfeiltasten: "52 von 53"...? Aendert sich die Frisur wirklich
   (Fenster zu/auf: startet bei neuer Nummer)?
3. TIPP-ECHO: Aussehen speichern -> Slot waehlen -> Ok -> im
   Kommentarfeld tippen: jedes Zeichen wird gesprochen? Ruecktaste:
   "X geloescht"? KEIN "1/40"-Geplapper dazwischen?
4. Speichern druecken: Bestaetigung vom Spiel? Danach im Slot-
   Fenster: neuer Eintrag mit Datum?

---

## STAND 2026-07-17 abends (V4.87 gebaut + deployed, UNCOMMITTET)

### V4.86-Testauswertung (Log 17:23-17:25): 2x BESTAETIGT, 1 Blocker
- BESTAETIGT Beschreibungen: Volk UND Volksstamm sprechen Name ->
  Beschreibung in richtiger Reihenfolge (Halmlinge/Sandlinge inkl.
  Start-Attribute).
- BESTAETIGT Strg+F8: "Zufaelliges Aussehen gedrueckt", Werte aendern
  sich real (Koerpergroesse 50 -> 64 -> 34). 3x ausgeloest.
- BESTAETIGT Positions-Ansagen: "3 von 4" (FaceType), "1 von 192"
  (CMFColorL) - der ListProbe-Pfad (TrackListIndices-Fallback) und
  der Fokus-Pfad greifen BEIDE (Focus-Zeile '3 von 4' + DEBOUNCED).
  Diese Bewegungen kamen aber von Maus-Hover/Klicks, NICHT von
  Pfeiltasten.
- BLOCKER (User: "an einem punkt kam ich nicht mit der tastatur
  weiter"): CMFIconHair (Frisur, 49 Eintraege, Sel=46) offen,
  17:24:47-48 alle VIER Pfeiltasten gedrueckt ([Key]-Zeilen) ->
  KEINE ListProbe-Aenderung, KEIN Fokus-Wechsel. Das SPIEL ignoriert
  Pfeiltasten in den Icon-/Farb-Rastern komplett (mausbedient).

### V4.87: Plugin navigiert die Aussehen-Picker selbst
FIX (UIReaderService.TryNavigateCharaMakePicker, laeuft VOR dem
SelectYesno-Zweig in Navigate; Plugin.cs ruft Navigate bei aktivem
Menue fuer alle 4 Pfeile):
- Aktiver Picker = oberstes sichtbares CMF-Menue im Stack MIT
  Eintraegen (inaktive Picker sind geladen, aber 0 Eintraege -
  log-belegt); Fallback: Scan aller sichtbaren CMF-Addons.
- Pfeil = +-1 (alle vier Richtungen gleich, linear durchs Raster),
  Klemmen an den Enden, Start bei Sel (= aktuell angewandte Wahl).
- list->SelectItem(idx, dispatchEvent:true) = spieleigener Auswahl-
  Pfad (ilspycmd-verifiziert an AtkComponentList) + ScrollToItem.
  dispatchEvent:true soll die Klick-Reaktion des Addons ausloesen
  (Vorschau-Update) - Laufzeit-Wirkung UNVERIFIZIERT, Log-Zeile
  [CMF] Picker-Navigation zeigt jeden Schritt.
- Ansage "13 von 49", Dedup gegen den ListProbe-Pfad geprimt.
Build 0/0, deployt (Manifest 4.87.0.0).

### Beim naechsten Test (V4.87)
1. "Version 4 Punkt 87 bereit".
2. Aussehen -> Frisur oeffnen ("Menue, 49 Eintraege"), Pfeiltasten:
   "47 von 49", "48 von 49"...? An den Enden klemmt es (keine
   Endlos-Schleife)?
3. WICHTIG: Aendert sich die FRISUR im Spiel wirklich mit (z.B.
   Ok druecken, Fenster neu oeffnen: startet bei der neuen Nummer?
   Oder Strg+F8-Gegenprobe)? Falls die Vorschau nicht mitgeht,
   Log an Claude - dann probieren wir DispatchItemEvent statt
   SelectItem.
4. Farb-Raster (Haarfarbe 192): gleiche Probe.
5. Enter auf gewaehltem Eintrag: was passiert/wird angesagt?

---

## STAND 2026-07-17 abends (V4.86 gebaut + deployed, UNCOMMITTET)

### V4.86: Strg+F8 = "Zufaelliges Aussehen" (User-Wunsch)
User: "es sollte auch einen schalter zufaellige beschreibung oder so
geben" - gemeint ist der spieleigene Knopf "Zufaelliges Aussehen"
(_CharaMakeFeature, Top-Level-Button id=4, Dump-verifiziert 16:35).
Sehende klicken ihn mit der Maus; ob die Spiel-Tastaturnavigation ihn
erreicht, ist UNVERIFIZIERT - Plugin-Taste daher gerechtfertigt.
NEU: Strg+F8 (Config KeyRandomLook, laut Keybind-Dump frei) drueckt
den Knopf per ButtonClick-Dispatch (bewaehrter PressFocusedOk-Pfad),
Matching per NODE-ID (sprachunabhaengig, nicht per Label).
Ansagen ehrlich: "Zufaelliges Aussehen gedrueckt." nach dem Dispatch
(NICHT "Aussehen geaendert" - Wirkung nicht auslesbar), "Kein
Aussehen-Fenster offen..." ausserhalb, Warnungen ins Log wenn Knopf/
Event fehlen. Hilfe-Text (Strg+F1) + /acc keys Konflikt-Liste
ergaenzt. Build 0/0, deployt (Manifest 4.86.0.0).
HINWEIS: id=4 traegt im Volksstamm-Schritt den "Aussehen"-
Fortschritts-Button in _CharaMakeProgress - aber wir greifen NUR auf
_CharaMakeFeature zu, das nur im Aussehen-Schritt sichtbar ist.

### Beim naechsten Test (V4.86; ersetzt V4.85-Punkte, alle noch offen)
1. "Version 4 Punkt 86 bereit".
2. Aussehen -> Frisur, Pfeiltasten durch die Icons: "12 von 52"?
   Scrollen: Zahlen laufen weiter? Farb-Raster ebenso?
3. Strg+F8 im Aussehen-Schritt: "Zufaelliges Aussehen gedrueckt."
   + danach aendern sich Werte (z.B. Slider-Ansagen)? Vor dem
   Schritt (z.B. bei Volk): kommt die "Kein Aussehen-Fenster"-Ansage?
4. Enter auf einem Icon-Eintrag: was passiert/wird angesagt?
5. Falls Listen stumm: Log an Claude ([ListProbe]/[Focus] zeigen den
   aktiven Pfad).

---

## STAND 2026-07-17 abends (V4.85 gebaut + deployed, UNCOMMITTET)

### V4.84 BESTAETIGT (User: "ok das funktioniert")
Reihenfolge Name -> Beschreibung sitzt. Neuer Auftrag: "jetzt muessen
wir das aussehen barrierefrei machen".

### V4.85: Aussehen-Schritt - Icon-/Farb-Listen sagen Position an
BESTANDSAUFNAHME (Logs 16:31-16:36): Im Aussehen-Schritt sprechen
schon: Kategorie-Buttons (_CharaMakeFeature, "Frisur"...), Slider
(CMFSlider: "50, ORIGINAL, 50", "Etwa 192,5 cm"), Radio-Fenster
(CMFRadio2/4/6: "Typ 2"). LUECKE: die Icon-/Farb-Picker (CMFIconHair
52 Eintraege, CMFColorL/Hair 192, CMFColorFacePaint 96...) - Zeilen
sind reine Bild-Felder OHNE Text (Dump 16:35), bisher nur "Menue,
52 Eintraege" beim Oeffnen, danach Stille. Da Blinde die Optik eh
nicht bewerten koennen, ist Paritaet hier: Position kennen + waehlen
koennen -> Ansage "12 von 52".
FIX V4.85 (UIReaderService), ZWEI Pfade, weil unverifiziert ist, ob
die Tastatur dort die Listen-Indizes oder den globalen Fokus bewegt
(beide Muster existieren im Spiel, vgl. ConfigKeybind vs. Listen):
1. TrackListIndices-Fallback: leerer Zeilentext + Addon-Praefix
   "CMF" -> "{idx+1} von {count}".
2. Globaler Fokus-Pfad: TryReadCharaMakeIconFocusRow - Fokus-Node
   zum ListItemRenderer klettern (bewaehrter Bestiarium-Pfad),
   Renderer per Zeiger-Vergleich einer sichtbaren CMF-Liste zuordnen,
   Index = renderer->ListItemIndex (Offset 388, ilspycmd-verifiziert;
   Daten-Zeile, korrekt auch bei gescrollter Liste). Gate:
   _CharaMakeTitle sichtbar (Dump-belegt: ganze Erstellung ueber).
Log zeigt beim Test, welcher Pfad greift ([ListProbe] vs. [Focus]).
Build 0/0, deployt (Manifest 4.85.0.0).

### Beim naechsten Test (V4.85)
1. "Version 4 Punkt 85 bereit".
2. Charaktererstellung -> Aussehen -> Frisur oeffnen, mit Pfeiltasten
   durch die Icons: "12 von 52"-Ansagen? Scrollen (ueber den
   sichtbaren Bereich hinaus): Zahlen laufen korrekt weiter?
3. Haarfarbe/Tattoofarbe (Farb-Raster 192/96): Position wird
   angesagt?
4. Enter auf einem Eintrag: uebernimmt das Spiel die Wahl (Vorschau-
   Modell aendert sich - hoerbar leider nicht)? Was wird angesagt?
5. Falls stumm: Log an Claude - [ListProbe]-Zeilen zeigen, ob sich
   die Indizes bewegen, [Focus]-Zeilen den Fokus-Pfad.

---

## STAND 2026-07-17 abends (V4.84 gebaut + deployed, UNCOMMITTET)

### V4.84: Beschreibung kommt jetzt NACH dem Namen (V4.83-Testbefund)
User-Test V4.83 (Log 16:56): "er liest den namen vor aber er sollte
erst den namen lesen und dann die beschreibung". ROOT CAUSE im Log:
die Beschreibung lief DOPPELT - der GENERISCHE Text-Scanner
(ScanAddonTexts, [Scan] _CharaMakeHelp id=4) sprach sie mit
SpeakInterrupt und schnitt damit die gerade laufende Namens-Ansage
("Lalafell") sofort ab; der neue dedizierte Handler legte sie danach
nochmal (korrekt, nicht-unterbrechend) in die Warteschlange.
FIX: "_CharaMakeHelp" in SpecialUpdateAddons (gleiches Muster wie
_CharaMakeRaceGender/_CharaMakeTribe) - der generische Update-Pfad
ist fuer das Pane jetzt stumm, es spricht NUR noch
OnCharaMakeHelpUpdate: Name (Interrupt) zuerst, Beschreibung
(Warteschlange) hinterher. Build 0/0, deployt (Manifest 4.84.0.0).

### Beim naechsten Test (V4.84)
1. "Version 4 Punkt 84 bereit".
2. Volk & Geschlecht: ERST "Lalafell" (bzw. Volk), DANN die
   Beschreibung - und nur EINMAL?
3. Weiterblaettern mitten in der Beschreibung: bricht ab, naechster
   Name + Beschreibung?
4. Volksstamm: gleiche Reihenfolge?

---

## STAND 2026-07-17 spaeter nachmittag (V4.83 gebaut + deployed, UNCOMMITTET)

### V4.83: Volk-/Volksstamm-Beschreibung wird vorgelesen (GEFUNDEN!)
User-Meldung: Beim Volk-Waehlen wird die BESCHREIBUNG nicht vorgelesen.
V4.82 (Dump-Erweiterung: Strg+F5 nimmt alle sichtbaren CharaMake-
Addons mit) lieferte die Antwort - User hat an MEHREREN Schritten
gedumpt (6 Dumps 16:31-16:35 im Log; der erste Blick auf die Desktop-
Datei erwischte nur den letzten, am Aussehen-Schritt):
- BEFUND (Dumps 16:31:39 + 16:31:49 Volk, 16:31:57 Volksstamm):
  Beschreibung steht in _CharaMakeHelp, Top-Level-TEXT-NODE id=4,
  und wird beim Markieren live umgeschrieben ("Die Elezen sind stolze
  Nomaden..." / "Der Volksstamm der Wieslaender macht die grosse
  Mehrheit im Volk der Hyuran aus. ...").
- _CharaMakeInfo ist es NICHT (Text-Nodes leer, auch waehrend die
  Beschreibung auf dem Schirm stand). game-api.md dokumentiert.
FIX V4.83 (UIReaderService): PostUpdate-Listener auf _CharaMakeHelp,
Aenderungs-Detektor auf dem id=4-Text, Ansage NICHT-unterbrechend
(kommt nach "Elezen, maennlich"; Weiterblaettern schneidet sie ab).
PostSetup reset, damit die Beschreibung beim Wiederbetreten erneut
kommt. Build 0 Fehler/0 Warnungen, deployt (Manifest 4.83.0.0).

### Nebenbefunde aus den Dumps (fuer spaeter)
- CMFIconFeature (Gesichtsmerkmale): Listeneintraege sind reine
  Icon-ListItemRenderer OHNE Text - vorlesbar hoechstens als
  "Eintrag X von Y". Gleiches Muster vermutlich bei CMFIconHair/
  FaceType/Tatoo/FacePaint (Frisuren, Tattoos etc.).
- _CharaMakeFeature: Kategorie-Buttons + MouseOver-Ansagen liefen
  laut Log sauber (Frisur, Tattoofarbe, Farbe des Merkmals...).

### Beim naechsten Test (V4.83)
1. "Version 4 Punkt 83 bereit".
2. Charaktererstellung -> Volk & Geschlecht: nach "Hyuran, maennlich"
   kommt die Volk-Beschreibung hinterher? Weiterblaettern bricht sie
   ab und die naechste kommt?
3. Volksstamm-Schritt: Beschreibung des Stamms ebenso?
4. V4.81/4.80 weiter offen: Umschalt+F11 Ziel-Leiste, Fehler-Toasts
   (Testpunkte im V4.81-Abschnitt unten).

---

## STAND 2026-07-17 nachmittags (V4.81 gebaut + deployed + RELEASED)

### RELEASE v4.81 VEROEFFENTLICHT (17.07. ~16:05)
Commits 5f86d43 (Code V4.75-V4.81) + 7b6813b (repo.json 4.81) + a1abe28
(README DE+EN) gepusht. GitHub-Release v4.81 mit latest.zip /
FF14Accessibility-v4.81.0.zip (je 514759 Bytes, Release-Build,
Manifest 4.81.0.0 im Zip verifiziert) + Installer-EXE (unveraendert
seit 4.74er-Aktivierungs-Fix). latest-Link verifiziert (HTTP 200,
514759 Bytes). uia_test.ps1 weiterhin absichtlich uncommittet.
HINWEIS: V4.80 (Toasts) und V4.81 (Ziel-Leiste) sind released, aber
noch NICHT in-game getestet - Testpunkte unten gelten weiter.

### V4.79 BESTAETIGT (User): Tastenbelegung sagt Befehl + Taste an
User: "das mit den tasten funktioniert, wenn ich bei den leisten bin
werden die tasten auch angesagt". Der Fokus-Pfad-Fix (globaler Fokus
-> ClimbToItemRenderer -> dedizierter Zeilen-Leser) ist damit
verifiziert. Enter auf einer Zeile (Erfassungsmodus) weiter offen.

### Neu in V4.81: Skill-Browser kann alle 10 Leisten (User-Wunsch)
User: "es gibt ja mehrere leisten, wie kann ich skills auf die
zweite leiste ziehen?" Ilspycmd-verifiziert: RaptureHotbarModule.
StandardHotbars = Hotbars[0..9] (10 Stueck; 10-17 = Gamepad-Kreuz),
GetSlotById/SetAndSaveSlot/LoadSavedHotbar nehmen alle die Leisten-
Nummer. Live-Keybind-Dump als Ground Truth: InputId-Namen
HOTBAR_{Leiste}_{1..9,0,A,B}; Leiste 2 ist beim User schon auf
Strg+1..Strg+0 gebunden -> direkt nutzbar!
NEU:
- Umschalt+F11 = Ziel-Leiste weiterschalten ("Ziel-Leiste 2, 0 von
  12 belegt", + Warnung ", keine Tasten zugewiesen" wenn die Leiste
  keine Tasten hat). Slot-Wahl wird beim Wechsel zurueckgesetzt.
- KeybindService.GetBoundKey(InputId-Name): liest die LIVE gebundene
  Taste aus der Keybind-Tabelle ("Strg+3", KEY_-Prefix gestrippt).
  Alle Ansagen nennen sie: Ziel-Slot ("Ziel-Leiste 2, Taste Strg+3:
  leer"), Belegen ("X liegt jetzt auf Leiste 2, Taste Strg+3."),
  Skill-Fundort ("liegt auf Leiste 2, Taste Strg+3"), unbelegte
  Leisten sagen "Slot n" statt Taste.
- FindSlotLocationFor durchsucht jetzt ALLE 10 Leisten (vorher nur 1).
- Strg+F9 liest die GEWAEHLTE Leiste (Default weiter Leiste 1).
- Config KeySkillBar="Umschalt+F11" (laut Dump frei), Hilfe-Text
  aktualisiert. Leiste-1-Ansagen unveraendert ("Ziel-Taste 3").

### Beim naechsten Test (V4.81 + V4.80)
1. "Version 4 Punkt 81 bereit".
2. Umschalt+F11: "Ziel-Leiste 2, X von 12 belegt"? Weiter bis 10 und
   Umlauf zurueck zu 1?
3. Auf Leiste 2: Umschalt+F9 ("Ziel-Leiste 2, Taste Strg+1: leer"?),
   Skill waehlen, Umschalt+F10 -> "liegt jetzt auf Leiste 2, Taste
   Strg+1"? Dann Strg+1 druecken: Skill feuert?
4. Strg+F9 nach Leisten-Wechsel: liest Leiste 2?
5. V4.80-Toasts: Zauber auf zu fernes Ziel -> "Das Ziel ist zu weit
   entfernt."? Abklingzeit-Spam ertraeglich? Gebiets-Toast einmal?
6. Offen von frueher: Enter in der Tastenbelegung (Erfassungsmodus),
   Braillezeile, Strg+F6 Stufen, Einstellungs-Reiter Enter.

---

## STAND 2026-07-17 nachmittags (V4.80 gebaut + deployed)

### Neu in V4.80: Fehlermeldungen des Spiels werden vorgelesen (User-Wunsch)
User: "wenn ein zauber nicht ausgeloest wird ... kommt vom spiel eine
meldung, aber die wird nicht vorgelesen". BEFUND: Diese Meldungen
("Das Ziel ist zu weit entfernt.") sind FEHLER-TOASTS im _TextError-
Overlay. Der alte Ansatz (NotificationAddons: PostSetup+PostRefresh
-> OnNotification) war dafuer tot: Log 2026-07-17 zeigt als einziges
Lifecycle-Event das LEERE PostSetup beim Login (13:10:15), danach
nie wieder - PostRefresh feuert fuer _TextError schlicht nicht.
In den Chat gespiegelt werden die meisten Aktions-Fehler auch nicht
(ChatReaderService liest ErrorMessage zwar, sah sie aber nie).
FIX: neuer ToastService.cs via Dalamud IToastGui (Interface per
ilspycmd an der installierten Dalamud.dll verifiziert: ErrorToast/
Toast/QuestToast-Events, feuern auf dem Show-Toast-Aufruf des
Spiels selbst):
- Fehler-Toasts: SpeakInterrupt (Feedback zur eben gedrueckten
  Taste), Log [Toast] Fehler
- Info-/Quest-Toasts: Speak (nicht unterbrechend) mit Echo-Schutz
  (WasRecentlySpoken 6s - manche laufen zusaetzlich als _WideText/
  _ScreenText oder Chat-Echo); OnNotification hat den Schutz jetzt
  in Gegenrichtung (4s)
- Config: AnnounceErrorToasts / AnnounceInfoToasts (Default true)

### Beim naechsten Test (V4.80)
1. "Version 4 Punkt 80 bereit" (AutomaticReloading laeuft).
2. Zauber auf zu weites / nicht sichtbares Ziel: "Das Ziel ist zu
   weit entfernt." o.ae. wird gesprochen?
3. Skill waehrend Abklingzeit spammen: Ansage kommt, aber kein
   Dauergeplapper (0,5s-Debounce)?
4. Gebiets-/Quest-Toast (neues Gebiet betreten): EINMAL gesprochen,
   nicht doppelt?
5. V4.79 weiter offen: TASTENBELEGUNG mit Taste ("Vorwaerts, Taste
   W"?), Enter auf einer Zeile (Erfassungsmodus), Braillezeile,
   Strg+F6 Stufen, Einstellungs-Reiter Enter.

---

## STAND 2026-07-17 nachmittags (V4.79; V4.78-Hotbar-Fix BESTAETIGT)

### Neu in V4.79: Tastenbelegung spricht jetzt WIRKLICH Befehl + Taste
Log-Auswertung 13:06-13:12 (V4.78 lief): Im Fenster TASTENBELEGUNG
bewegen die Pfeiltasten den GLOBALEN Fokus (AtkInputManager.
FocusedNode), NICHT die Listen-Indizes - nur EINE List-Navigation-
Zeile beim Oeffnen ([0] "Laufen und Steuern, keine Taste"), danach
ausschliesslich [Focus]-Zeilen. Der V4.77-Fix (ReadConfigKeybindRow)
sass im Listen-Pfad und kam daher NIE zum Zug. ZWEITE Ursache:
der generische Baum-Leser (GetTextFromNodeTree) verwirft Texte der
Laenge 1 - einstellige Tasten ("W", "1", "C") fehlten deshalb
("Kommandomenue 1 - Slot 1" ohne Taste; "Tab, Gegner durchschalten"
HATTE die Taste, weil "Tab" 3 Zeichen lang ist).
FIX (UIReaderService): UpdateGlobalFocus prueft bei sichtbarem
ConfigKeybind, ob der Fokus-Node in einem ListItemRenderer liegt
(ClimbToItemRenderer, bewaehrter Bestiarium-Pfad) und liest die
Zeile mit dem dedizierten Leser ("Befehl, Taste X" / ", keine
Taste"). Laeuft pro Frame, weil die Liste UNTER dem festen Fokus-
Node scrollt (gleicher Node-Ptr, neuer Zeilentext - Log 13:12:06-08).
NEBENBEI: Zeilen OHNE Belegungs-Buttons (Abschnitts-Koepfe) sagen nur
noch ihr Label, ohne falsches ", keine Taste".

### V4.78 BESTAETIGT (User "jetzt kann ich tasten zuweisen" + Log 13:22)
V4.79 lud per AutomaticReloading mitten in der Session (13:19:50
"Version 4 Punkt 79 bereit", kein Spiel-Neustart noetig). Danach
kompletter Skill-Browser-Durchlauf im Log: Blaettern ("9 von 12:
Schwaere, Stufe 10"), "liegt auf Taste X"-Hinweise, Ziel-Tasten-
Zyklus, Schutz-Ansage ("Keine Ziel-Taste gewaehlt"), dann ZWEI
erfolgreiche Zuweisungen auf BELEGTE Slots: Taste 7 Juwelenschein
-> Schwaere, Taste 8 Stumpfsinn -> Energieentzug. Live-Slot stand
DIREKT nach dem Call auf der neuen Action, 2-Frame-Read-back
bestaetigte beide. LoadSavedHotbar war der fehlende Baustein.
Skill-Filter griff ebenfalls: 10 Nicht-Spieler-Actions raus, Liste
12 statt 22 Eintraege (Job 26, Stufe 12).

### Noch offen zu testen (V4.79)
1. TASTENBELEGUNG mit V4.79, Pfeiltasten: "Vorwaerts, Taste W"?
   Schnelltasten-Reiter: "Kommandomenue 1 - Slot 1, Taste 1"?
   (Fix kam 13:19 per Reload; Fenster wurde danach nicht mehr
   geoeffnet - der 13:12-Durchlauf lief noch auf V4.78.)
2. ENTER auf einer Tastenbelegungs-Zeile: was passiert / was wird
   angesagt? Danach Log an Claude (klaert den Erfassungsmodus
   fuers Umbelegen).
3. Offen von frueher: Braillezeile, Strg+F6 Stufen, Einstellungs-
   Reiter Enter.

### Session-Notiz 13:06-13:12 (V4.78-Kurztest)
Start + Login sauber (Warteschlangen-Hinweis gesprochen), SystemMenu-
Navigation ok, TASTENBELEGUNG geoeffnet und Zeilen durchlaufen
(Befund oben). KEIN [Hotbar]-Eintrag im Log - der V4.78-Belegen-Fix
wurde nicht getestet, Enter in der Tastenbelegung auch nicht.

---

## STAND 2026-07-17 mittags (V4.78 gebaut + deployed; Abschnitt nachgetragen)

### V4.78: Skill-Belegen-Fix nach V4.76-Probe
V4.76-Probe-Beweis (Log 11:59): SetAndSaveSlot schreibt nur den
GESPEICHERTEN Zustand - die 09:43-Zuweisung erschien erst nach dem
Relog auf der Leiste. FIX (HotbarService): nach SetAndSaveSlot zieht
LoadSavedHotbar(CurrentClassJobId, Leiste 0) den gespeicherten Stand
sofort in die Live-Leiste (FFXIVClientStructs-Doku, game-api.md).
Erfolg wird weiter erst nach dem 2-Frame-Read-back gemeldet.
Ausserdem: Skill-Liste filtert Nicht-Spieler-Actions (5x
"Ausweichen"). IN-GAME NOCH UNGETESTET (Testpunkt 4 oben).

---

## STAND 2026-07-17 (V4.77 gebaut + deployed; Fix kam nie zum Zug - siehe V4.79)

### Neu in V4.77: Tastenbelegung (ConfigKeybind) sagt Befehl + Taste an
User hat statt Benutzermakros das Fenster TASTENBELEGUNG gedumpt
(ConfigKeybind, Reiter Schnelltasten; Struktur in game-api.md ->
"ConfigKeybind"). Log-Befund 09:45/10:14: Listen-Navigation lief dort
SCHON (Pfeiltasten, Kategorie-Wechsel), aber Zeilen wurden OHNE die
belegte Taste angesagt ("Vorwaerts" statt "Vorwaerts, Taste W") -
ROOT CAUSE: Tasten-Texte stecken in Button-KOMPONENTEN in der Zeile
(id=6 Belegung 1, id=5 Belegung 2, Tasten-Text = Text id=5 darin),
ReadListItemText liest nur direkte Text-Nodes. FIX: dedizierter
ReadConfigKeybindRow ("Befehl, Taste X" / "Befehl, keine Taste"),
generischer Leser als Fallback.
WICHTIG fuer den User geklaert: Dieses Fenster aendert TASTE->SLOT
("welche Taste feuert Kommandomenue 1 - Slot 1"), NICHT welcher Skill
im Slot liegt. Skill->Slot ist der Skill-Browser (V4.75/76).
OFFEN: Enter auf einer Zeile (Tasten-Erfassungsmodus?) - nie
getestet; User hat Session um 10:15 beendet ohne Enter zu druecken.

### Beim naechsten Start testen (V4.77)
1. "Version 4 Punkt 77 bereit".
2. TASTENBELEGUNG oeffnen, Pfeiltasten: "Vorwaerts, Taste W"?
   Schnelltasten-Reiter: "Kommandomenue 1 - Slot 1, Taste 1"?
   Unbelegte: "..., keine Taste"?
3. ENTER auf einer Zeile druecken: Was passiert / was wird angesagt?
   Danach Log an Claude (klaert den Erfassungsmodus fuers Umbelegen).
4. V4.76-Probe: Umschalt+F10 auf belegte UND leere Taste (Skill-
   Browser), Log an Claude ([Hotbar]-Zeilen vorher/sofort/2 Frames).
5. Offen von frueher: Braillezeile, Strg+F6 Stufen, Einstellungs-
   Reiter Enter.

---

## STAND 2026-07-17 (V4.76 gebaut + deployed)

### V4.75-Testbefund: Skill-Belegen ohne Wirkung -> Probe in V4.76
User: "konnte keine Taste zuweisen, die schon belegt war". Log 09:43:15
(einziger Versuch): SetAndSaveSlot(0, 0, Action, 25798 Karfunkel-
Beschwoerung) lief OHNE Exception durch, Slot blieb aber auf Action 163
(Ruin) - sofortiger Read-back meldete ehrlich "Belegen fehlgeschlagen".
Skill-Liste selbst OK (Job 26, Stufe 12, 22 Skills, plausibel).
GitHub-Doku (aers/FFXIVClientStructs) sagt: SetAndSaveSlot setzt den
LIVE-Slot und speichert via WriteSavedSlot - haette also sofort greifen
muessen. Native Internas nicht einsehbar -> V4.76 = Audit-Probe:
- loggt Slot-Zustand VORHER / SOFORT nach dem Call / NACH 2 FRAMES
  (RunOnTick, Signatur ilspycmd-verifiziert) + IsHotbarShared(0)
- Ansage kommt erst nach dem 2-Frame-Read-back (trennt "Spiel lehnt ab"
  von "Live-Slot zieht einen Frame nach")
Naechster Test: Umschalt+F10 auf belegte UND auf leere Taste, dann Log
an Claude ([Hotbar]-Zeilen zeigen das Urteil).

### RICHTUNGSWECHSEL (User 2026-07-17): Makro-Fenster statt/neben Skill-Browser
User will die Hotbar-Frage "lieber uebers Menue" loesen und das Fenster
Benutzermakros barrierefrei machen. OFFENE FRAGEN an den User (gestellt,
noch unbeantwortet): (1) Ziel = Makros erstellen/bearbeiten (auch als
Chat-Ersatz!) oder Skills auf die Leiste? (2) Skill-Browser V4.75
behalten oder raus? HINWEIS gegeben: Makro-Fenster ist texteingabe-
lastig, Textfeld-Echo ist die aelteste offene Baustelle; moeglicher
Ausweg RaptureMacroModule (lesen/schreiben ohne UI) - noch NICHT
dekompiliert/verifiziert. Workflow sobald geklaert: User oeffnet
Benutzermakros, Strg+F5-Dump + Strg+F2, Log an Claude.

---

## STAND 2026-07-17 frueher (V4.75 gebaut + deployed)

### Neu in V4.75: Skill-Browser - Aktionsleiste 1 per Tastatur umbelegen
User-Auftrag: Hotbars barrierefrei machen, Skills auf Tasten 1-8 aendern.
Das Spiel hat dafuer KEINEN Tastatur-Weg (Sehende ziehen Actions per Maus
aus "Aktionen & Traits") -> Plugin-Tasten gerechtfertigt.

Alles ilspycmd-verifiziert (game-api.md -> "Hotbar UMBELEGEN"):
- RaptureHotbarModule.SetAndSaveSlot = spieleigener Speicher-Pfad
  (identisch mit Drag-and-drop, je Job persistent)
- Skill-Liste aus Lumina Action-Sheet: !IsPvP, ClassJobLevel 1..Stufe,
  ClassJobCategory enthaelt Job (Spalten-Aufloesung wiederverwendet:
  GearInfoService.AllowsJob jetzt public), UnlockLink-Quest-Gate via
  UIState.IsUnlockLinkUnlockedOrQuestCompleted
- Read-back nach dem Setzen: Erfolg wird NUR gemeldet, wenn der Slot
  danach wirklich die neue Action traegt

NEUE TASTEN (Umschalt+F7-F10, laut Keybind-Dump frei; kein Config-
Migrations-Bedarf, neue Felder bekommen Defaults):
- Umschalt+F7 / Umschalt+F8 = Skill-Browser zurueck / vor
  ("5 von 24: Vollschlag, Stufe 4", + "liegt auf Taste 2" falls belegt;
  Liste nach Stufe sortiert wie das Aktionen-Fenster, baut sich bei
  Job-/Stufenwechsel neu)
- Umschalt+F9 = Ziel-Taste weiterschalten ("Ziel-Taste 3: Vollschlag" /
  "leer" - man hoert, was ueberschrieben wuerde)
- Umschalt+F10 = zuweisen ("X liegt jetzt auf Taste 3.")
Strg+F9 (Leiste vorlesen) unveraendert. Hilfe-Text (Strg+F1) ergaenzt.

### Beim naechsten Start testen (V4.75)
1. "Version 4 Punkt 75 bereit".
2. Umschalt+F8 mehrmals: kommen die Skills deines Jobs mit Stufe, nach
   Stufe sortiert? Passt die ANZAHL grob zum Aktionen-Fenster?
   (Log-Zeile "[Hotbar] Skill-Liste gebaut" zeigt die Zahl.)
3. Umschalt+F9 mehrmals: "Ziel-Taste 1..." mit aktueller Belegung?
4. Skill waehlen, Ziel-Taste waehlen, Umschalt+F10: "X liegt jetzt auf
   Taste Y"? Danach Taste Y druecken: feuert der neue Skill? Strg+F9
   liest die neue Belegung? Nach Neustart noch da (SetAndSaveSlot
   speichert je Job)?
5. Offene Punkte von V4.73/74: Braillezeile, Strg+F6 Stufen-Ansage,
   Einstellungs-Reiter mit Enter.

---

## STAND 2026-07-16 abends (V4.72 gebaut + deployed)

### Neuinstallation: Installer aktivierte Plugins nicht (16.07. spaet abends, GEFIXT)
User hat alles neu installiert; Plugins (FF14Accessibility + vnavmesh)
lagen in devPlugins, luden aber nicht. ROOT CAUSE (dekompiliert
verifiziert, Dalamud 15.0.2.2 PluginManager Zeile "if (configuration.
DevMode == true)"): Dalamud scannt DevPluginLoadLocations NUR bei
DevMode=true - der Installer setzte das nie. Dadurch entstanden auch
nie Profil-Eintraege, und der alte Installer-Pfad "Spiel einmal
starten, dann erneut ausfuehren" konnte prinzipiell nie greifen.

Sofort-Fix in dalamudConfig.json (Backup: .bak-vor-plugin-enable):
DevMode=true; pro Plugin DevPluginSettings-Eintrag (Schluessel =
DLL-Pfad, StartOnBoot=true, feste WorkingPluginId-GUID) + DefaultProfile-
Eintrag mit DERSELBEN GUID und IsEnabled=true. Mechanik verifiziert:
LocalDevPlugin uebernimmt eine vorhandene WorkingPluginId aus
DevPluginSettings unveraendert; Profile.WantsPlugin matcht per GUID;
Laden beim Boot nur bei IsEnabled=true UND StartOnBoot=true. BOM-frei
geschrieben. AutomaticReloading bewusst false (kein unangekuendigter
Plugin-Reload mitten im Spiel).

Installer dauerhaft gefixt (InstallerService.cs): PatchDalamudConfig
setzt jetzt DevMode=true und saet DevPluginSettings + Profil-Eintraege
selbst (neue Methode EnableDevPlugin, ersetzt EnableProfilePlugins).
Loc-Strings "PluginNotEnabledYet*" raus, "ProfileStructureUnexpected*"
rein. Neue EXE gebaut + nach dist/ kopiert. NICHT released/committet -
beim naechsten Release mitnehmen.

LADEN BESTAETIGT (Log 22:16): Beide Plugins luden ("Loading dev
plugin", Profil-Eintraege state true, V4.73-Startzeile im Log).

### V4.74 (gebaut + deployed, ungetestet): NVDA stumm nach Neuinstallation
User-Meldung: Plugin laeuft, aber Screenreader spricht nicht. Log
22:16: "Tolk geladen. Screenreader: Keiner erkannt" - NVDA lief
(PID 5940). ROOT CAUSE: Tolk.dll laedt nvdaControllerClient64.dll
NATIV per LoadLibrary(Basisname) - der SetDllImportResolver greift
nur fuer managed P/Invoke. Nativer Loader sucht Spielverzeichnis/
System/PATH, NICHT den Plugin-Ordner. Vorher lagen die DLLs im alten
Spielverzeichnis; neues Steam-Verzeichnis (K:\SteamLibrary\...) hat
sie nicht. FIX V4.74: TolkNative.Initialize laedt
nvdaControllerClient64.dll VORAB mit vollem Pfad aus dem Plugin-
Ordner (NativeLibrary.TryLoad) - Windows-Loader gibt bei spaeterem
LoadLibrary mit gleichem Basisnamen das geladene Modul zurueck.
Plugin damit unabhaengig vom Spielverzeichnis, kein Game-Dir-Kopieren
mehr noetig. Log zeigt jetzt "NVDA-Client vorab geladen: True/False"
+ Warnung bei keinem erkannten Screenreader.

NEBENBEI GEFIXT (csproj): DeployToDevFolder lief VOR DalamudPackager
(beide AfterTargets="Build", NuGet-Import registriert spaeter) ->
devPlugins-Manifest hing immer einen Build hinterher (4.74-Build
deployte 4.73-Manifest). Jetzt AfterTargets="DefaultDalamudPackagerDebug".

AutomaticReloading=true gesetzt (User-Wunsch): in dalamudConfig.json
fuer beide Plugins UND als Installer-Standard - neue Deploys werden
ohne Spiel-Neustart uebernommen. Installer-EXE erneut publiziert
(dist/). Weiterhin NICHT committet/released.

V4.74 BESTAETIGT (User 16.07. spaet abends): "ok funktioniert" -
Sprachausgabe ist nach Neuinstallation wieder da. Der komplette
Block Neuinstallation (Plugin-Aktivierung + NVDA-Preload) ist damit
verifiziert.

RELEASE v4.74 VEROEFFENTLICHT (16.07. ~22:45): Commits c864ae9
(Code) + 1ddd395 (repo.json) gepusht; GitHub-Release v4.74 mit
latest.zip / FF14Accessibility-v4.74.0.zip / neuer Installer-EXE
(mit Aktivierungs-Fix). latest-Link verifiziert (HTTP 200, 508508
Bytes = 4.74-Zip). uia_test.ps1 (UIA-Testskript, Repo-Wurzel)
absichtlich uncommittet gelassen.

### Neu in V4.72: Laden-Fix (User-Test 18:21 zeigte das Problem)
V4.71-Testauswertung aus dem Log:
- Item-Slot-Ansage BESTAETIGT: Charakterfenster 18:14:27 sprach
  "Leder-Grimoire, Stufe 5, tragbar" (Job 26 -> Spalte ACN korrekt).
- Laden: Addon heisst wirklich "Shop" (Erkennung lief, Namens-Cache
  wurde gebaut), aber KEIN Treffer beim Namens-Match. ROOT CAUSE:
  Shop-Zeilen-Fokus-Text = '226, <0x02-Payload>Laien-Hanfbundhaube
  <0x03>' - der Item-Name steckt in SeString-Payload-Huellen, der
  Match lief auf dem ROHEN Text. FIX: AppendShopGearInfo sanitized
  jede Zeilen-Teil vor dem Match (TolkService.Sanitize jetzt public).

### Strg+F7 BESTAETIGT (User + Log 18:21:58, noch in der V4.71-Session)
Setup Job 26 -> EquipRecommendedGear -> "4 Plaetze geaendert". Der
spieleigene Optimierer laeuft sauber ueber unsere Taste.

### Gehhilfe BESTAETIGT (User 16.07. abends)
Manuelles Laufen mit Wegpunkt-Routing (Strg+Numpad3, V4.63-4.65)
funktioniert in der Praxis. OFFEN (User-Wunsch, bei Gelegenheit):
Sounds austauschen - aktuell Sinus-Piepser (Beacon 880Hz-Familie,
Wegpunkt-Cue 1568Hz, Ankunft 1320->990Hz). Vor Umsetzung fragen,
welche Sounds stoeren und was stattdessen gewuenscht ist (andere
Toene oder echte Sound-Dateien).

### V4.72 Laden BESTAETIGT (User + Log 18:46)
Beide Faelle sauber: "Laien-Hanfbundhaube: Stufe 10, tragbar" /
"Messingbrille: Stufe 13, nicht tragbar, ab Stufe 13". Der
Payload-Sanitize-Fix war der fehlende Baustein. Ausruestungs-Block
(User-Auftrag Stufe+Tragbarkeit + Bestes anlegen) damit KOMPLETT
bestaetigt bis auf:

### Neu in V4.73 (gebaut + deployed, ungetestet): Braillezeile
User-Wunsch: alles Gesprochene auch auf der Braillezeile. Fix:
TolkService ruft jetzt Tolk_Output (Sprache UND Braille, laut
Tolk-API die empfohlene Ausgabefunktion) statt Tolk_Speak (nur
Sprache). Tolk_Output war in TolkNative schon deklariert, nur
ungenutzt. Bei NVDA geht Braille ueber nvdaController_brailleMessage
(macht Tolk intern).

### RELEASE v4.73 veroeffentlicht (16.07. abends)
Commits 37d51da + 49acfb7 gepusht, GitHub-Release v4.73 mit
latest.zip / FF14Accessibility-v4.73.0.zip / Installer-EXE.
latest-Download-Link verifiziert (HTTP 200). repo.json auf 4.73.

### Noch offen zu testen
1. Braillezeile (V4.73): zeigt jede Ansage an? Startansage
   "Version 4 Punkt 73 bereit" muesste schon auf der Zeile stehen.
2. Strg+F6: jedes getragene Teil mit "Stufe X"?
3. Einstellungen (V4.70/71-Fix): Reiter + Enter -> "Ueberschrift,
   Tab X von 8"? (Rest der Optionen-Baustelle ist eh zurueckgestellt)

---

## STAND V4.71 (2026-07-16, Item-Slots BESTAETIGT, Laden-Match kaputt - Fix in V4.72)

### Neu in V4.71: Ausruestung - Stufe + Tragbarkeit (User-Auftrag)
User will: im Laden UND am Koerper hoeren, welche Stufe ein
Ausruestungsteil hat und ob er es tragen kann; plus "Bestes anlegen".
Letzteres EXISTIERT schon: Strg+F7 (V4.66, noch nie getestet) ruft die
spieleigene "Empfohlene Ausruestung" auf.

Neu: GearInfoService.cs - liest NUR Spiel-Datenblaetter (ilspycmd-
verifiziert): Item.LevelEquip (noetige Stufe), Item.ClassJobCategory
(bool-Spalte je Job, Spaltenwahl ueber ENGLISCHE ClassJob-Abkuerzung,
kein Spalten-Reihenfolge-Raten), Item.EquipRestriction (Volk/Geschlecht),
Spielerseite aus PlayerState (CurrentLevel/CurrentClassJobId/Race/Sex).
Unbekannte Spalte/Werte -> nur "Stufe X", NIE geratenes Urteil (Log
[Gear] zeigt die Luecke). Die native InventoryManager.CanEquip existiert,
braucht aber einen rohen itemRow-Zeiger -> Crash-Risiko, nicht benutzt.

Eingebaut an 3 Stellen:
1. Item-Slot-Navigation (Inventar/Charakterfenster/Arsenal): Ansage
   jetzt "Bronzegladius, Stufe 5, tragbar" bzw. "..., nicht tragbar,
   ab Stufe 26" / "nur fuer Gladiator" / "nicht fuer dein Volk".
   Icon-Aufloesung kennt jetzt auch getragene + Arsenal-Items
   (EquippedItems + Armory*-Container in der Icon-Map).
2. Strg+F6 (Ausruestung vorlesen): pro Teil ", Stufe X" angehaengt;
   "tragbar" wird dort nur bei PROBLEM gesprochen (nicht 12x "tragbar").
3. Laden-Listen: waehrend ein Shop-Fenster offen ist, wird an gesprochene
   Zeilen die Gear-Info angehaengt (Namens-Match gegen Ausruestungs-
   Namen). ACHTUNG: Shop-Addon-Namen (Shop, ShopExchangeItem,
   ShopExchangeCurrency, InclusionShop) sind UNVERIFIZIERT (Community-
   Wissen) - wenn im Laden nichts angehaengt wird, nennt die
   "[Accessibility] Addon:"-Logzeile den echten Fensternamen.

Nebenbei-Fund (CS0649-Warnung): V4.70 hat _csExpectedTabIdx/
_csTabActivatedAt deklariert, aber NIE gesetzt -> "Tab X von 8"-Ansage
nach Enter und der 1,5s-Fallback ("Reiter gedrueckt, aber kein
Seitenwechsel") waren tote Pfade. Jetzt verdrahtet: Enter-Dispatch merkt
sich den gedrueckten Reiter (NodeId -> _csTabs-Index).

### Beim naechsten Start testen (V4.71)
1. "Version 4 Punkt 71 bereit".
2. Charakterfenster/Inventar: Slot mit Ruestung fokussieren ->
   "Name, Stufe X, tragbar"? Leere Slots weiter "Leer"?
3. Strg+F6: jedes Teil mit "Stufe X"?
4. Laden (Haendler) oeffnen, durch Waren gehen: haengt "Stufe X,
   tragbar/nicht tragbar" hinten dran? Falls stumm: Log an Claude
   (Addon-Name pruefen).
5. Strg+F7 (aeltester offener Test): legt empfohlene Ausruestung an,
   Ansage "X Teile gewechselt"?
6. Einstellungen (V4.70-Fix): Reiter fokussieren, Enter -> jetzt
   "Ueberschrift, Tab X von 8"? Bei totem Reiter nach 1,5 s ehrliche
   Meldung?
7. Log-Kontrolle danach: [Gear]-Zeilen (Job-Spalten-Zuordnung,
   Laden-Treffer).

---

## STAND V4.70 (2026-07-16 gebaut, Reiter-Merken war UNVERDRAHTET - in V4.71 gefixt)

### Neu in V4.70: Enter aktiviert Einstellungs-Reiter
V4.69 BESTÄTIGT (Log 16:25): Slider ("Regler ... von X bis Y"),
Auswahllisten ("Bildschirmmodus, Auswahlliste, NVIDIA...") und Reiter
("Reiter 1-8 von 8") werden beim Fokussieren angesagt - der
Enthaltensein-Ansatz (FindTopLevelOwner) war richtig.
USER-MELDUNG: Reiter fokussierbar, aber "wenn ich drücke passiert
nichts" - der Seitenwechsel fand nie statt (keine Tab-Wechsel-Ansage im
Log). Fokus allein aktiviert die DragDrop-Reiter nicht.
FIX: TryActivateFocusedConfigTab in HandleConfirmKey (Enter): dispatcht
das registrierte Klick-Event des fokussierten Reiters an seinen Listener
- gleicher Mechanismus wie das bewährte PressFocusedOk (Enter=Ok der
Charaktererstellung). Kandidaten-Reihenfolge DragDropClick(58) >
MouseClick(9) > ButtonClick(25); ALLE registrierten Event-Typen des
Reiter-Nodes werden geloggt ([CS] Reiter-Aktivierung), damit ein
falscher Kandidat sofort im Log erkennbar ist. Nach dem Wechsel sagt der
vorhandene Tab-Wechsel-Detektor die neue Seiten-Überschrift an.

### Beim nächsten Start testen (V4.70)
1. "Version 4 Punkt 70 bereit".
2. Systemkonfiguration: Reiter fokussieren ("Reiter 3 von 8"), ENTER:
   wechselt die Seite ("<Überschrift>, Tab 3 von 8" wird angesagt)?
3. Falls nicht: Log-Zeile "[CS] Reiter-Aktivierung: ... Events=[...]"
   an Claude - sie zeigt, welche Events der Reiter wirklich registriert.
4. Rest von V4.69: Slider-Werte beim Schieben, Auswahllisten.

---

## STAND V4.69 (2026-07-16 gebaut, BESTÄTIGT)

### Neu in V4.69: Einstellungen-Fix Nummer 2 (V4.68 blieb stumm)
V4.68-Test (Log 16:14): RadioButtons/Buttons sprachen (generischer Leser),
Slider/Auswahllisten weiter stumm, KEINE "[CS] Fokus (global)"-Zeile, keine
Exception. BEFUND: Der V4.68-Ansatz kletterte per ParentNode vom Fokus-Node
zur Fenster-Wurzel - die Eltern-Kette Komponenten-INTERNER Nodes erreicht
die Wurzel aber nicht zuverlässig (Handler stieg still am Wurzel-Check aus).
FIX: Zuordnung umgedreht - FindTopLevelOwner durchsucht die Top-Level-
Komponenten des Fensters danach, WELCHE den Fokus-Node ENTHÄLT (rekursiv
bis Tiefe 3, Dropdown-Fokus sitzt in der eingebetteten Checkbox-Komponente).
Owner-Komponente wird pro Fokus-Wechsel EINMAL gesucht und gecacht
(Wert-Verfolgung nutzt den Cache). Zusätzlich Diagnose-Zeile wenn der
Fokus-Node keinem Top-Level-Control zugeordnet werden kann.

### Beim nächsten Start testen (V4.69) - wie V4.68-Plan
1. "Version 4 Punkt 69 bereit".
2. Systemkonfiguration, Pfeiltasten: Slider "Label, Regler, Wert, von X
   bis Y"? Auswahllisten "Label, Auswahlliste, Eintrag"?
3. Regler links/rechts: neuer Wert gesprochen?
4. Reiter fokussieren: "Reiter X von 8"?
5. Falls wieder stumm: Log hat jetzt die Zeile "[CS] Fokus (global): Node
   ... gehört keinem Top-Level-Control" - dann weiß ich, wo es klemmt.

---

## STAND V4.68 (2026-07-16 gebaut, WIDERLEGT - siehe V4.69)

### Neu in V4.68: Einstellungen - Slider/Auswahllisten/Reiter sprechen
User-Meldung + Log 15:52 + frischer ConfigSystem-Dump: Pfeiltasten-Fokus
wanderte zwischen zwei Slidern ("Transparenz"/"Größe", Seite
Farbwahrnehmung), beide OHNE Text -> Stille. ROOT CAUSE doppelt:
(1) Slider/DropDownList/Reiter tragen keinen Text-Node (Probe [CS-OPT]
    zeigt ""), das Label steht als EIGENER Top-Level-Text DIREKT VOR dem
    Control in der Node-Liste (Dump: "Transparenz" vor Slider id=570);
(2) FindFocusedText sucht das Fokus-BIT an den Nodes, die Tastatur bewegt
    aber den globalen AtkInputManager.FocusedNode (V4.35-Erkenntnis).
FIX (UIReaderService, AnnounceConfigGlobalFocus, laeuft im ConfigSystem-
PostUpdate): globalen FocusedNode zum Top-Level-Ancestor klettern;
Slider -> "{Label}, Regler, {Wert}, von {Min} bis {Max}" (Felder
Value/MinValue/MaxValue ilspycmd-verifiziert); DropDownList ->
"{Label}, Auswahlliste, {gewaehlter Eintrag}" (List->SelectedItemIndex);
Kategorie-Reiter (DragDrop id 7-14) -> "Reiter X von 8". Bleibt der Fokus
auf dem Control, werden nur WERT-Aenderungen gesprochen (Slider-Schieben,
Dropdown-Auswahl). Label = naechster sichtbarer Top-Level-Text VOR dem
Control (volatile Texte wie fps uebersprungen). Controls MIT Text
(CheckBox/RadioButton/Buttons) sagt weiterhin der generische Fokus-Leser
an - keine Doppel-Ansage.
OFFEN: Charakterkonfiguration (ConfigCharacter) vermutlich gleiches
Layout, aber eigener Addon-Name - nach CS-Test pruefen/nachziehen.

### Beim naechsten Start testen (V4.68)
1. "Version 4 Punkt 68 bereit".
2. Systemkonfiguration oeffnen, mit Pfeiltasten durch die Controls:
   Slider sagen "Label, Regler, Wert"? Auswahllisten "Label, Auswahlliste,
   Eintrag"? Checkboxen weiter wie bisher?
3. Auf einem Regler links/rechts druecken: wird der neue Wert gesprochen?
4. Reiter fokussieren: "Reiter X von 8"? Reiter aktivieren: Seiten-
   Ueberschrift wie gehabt?
5. V4.67-Punkte falls offen: "Leer" in der Tasche, Arsenal-Kategorien,
   Strg+F6/F7.

---

## STAND V4.67 (2026-07-16 gebaut)

### Neu in V4.67: Inventar/Arsenal Stufe 2 (aus den User-Dumps vom 16.07.)
DUMP-AUSWERTUNG (InventoryGrid 38 Nodes, Currency 111, ArmouryBoard 125;
Bäume stehen komplett im dalamud.log, die Desktop-Datei wird pro Strg+F5
ÜBERSCHRIEBEN - nur ArmouryBoard liegt noch auf Platte):
- Item-Slots = DragDrop(17) mit Icon-Kind; GEFÜLLTE Slots wurden schon
  gesprochen (Log 14:28: "7 mal Heiltrank", "Hanfbundhaube des Eifers" -
  der generische Item-Slot-Leser im Fokus-Pfad greift).
- LÜCKE 1: LEERE Slots (IconId=0) blieben stumm - Cursor-Bewegung nicht
  von Stillstand unterscheidbar. FIX: Icon-/DragDrop-Komponente mit
  IconId=0 sagt jetzt "Leer" (nur echte Slot-Typen; Icon-dekorierte
  Controls wie ConfigSystem-Tabs haben echte IconIds bzw. matchen nur
  über den Wrapper-Zweig und bleiben stumm).
- LÜCKE 2: Arsenal-Kategorie-Reiter sind reine Icons ohne Text. FIX:
  neuer OnArmouryBoardUpdate-Handler liest den Kategorie-Titel (Text-Node
  id=121, Dump-verifiziert) - beim Öffnen angehängt ("Kategorie Kopf"),
  bei Reiter-Wechsel per Interrupt.
- Charakterfenster-Dump fehlt noch (User erwischte stattdessen Currency);
  Ausrüstungs-Slots dort vermutlich gleiche Icon-Slot-Struktur - der
  "Leer"-Fix gilt generisch, Test zeigt ob mehr nötig ist.
V4.66-BEFUND aus demselben Log: Spiel hat ein eigenes Fenster "AUSRÜSTUNG
OPTIMIEREN" (Addon RecommendEquip, Knöpfe Anlegen/Abbrechen wurden vom
Fokus-Leser gesprochen) - User hat den Spielweg benutzt; Strg+F6/F7
(EquipmentService) noch UNGETESTET.

### Beim nächsten Start testen (V4.67)
1. "Version 4 Punkt 67 bereit".
2. Tasche öffnen, mit Numpad-Pfeilen über LEERE Felder: sagt er "Leer"?
3. Arsenal öffnen: "Kategorie Kopf" (o.ä.) nach dem Fensternamen? Reiter
   wechseln: neue Kategorie sofort angesagt?
4. Strg+F6 (Ausrüstung vorlesen) und Strg+F7 (empfohlene anlegen) - beides
   noch ungetestet aus V4.66.
5. Charakterfenster öffnen + Strg+F5 (Dump fehlt noch; Fenster heißt im
   Hauptmenü "Charakter").

---

## STAND V4.66 (2026-07-16 gebaut)

### Neu in V4.66: Ausrüstung (Stufe 1 - datenbasiert, ohne UI)
User-Wunsch: Inventar-/Rüstungs-/Arsenal-MENÜS barrierefrei + "optimale
Rüstung anziehen". Stufe 1 jetzt, komplett ohne UI-Scraping:
(1) NEU EquipmentService.cs. Strg+F6 = angelegte Ausrüstung vorlesen
    ("Waffe: Bronzegladius. Kopf: ... X Plätze frei."). Quelle:
    IGameInventory EquippedItems-Container; Slot-NAMEN kommen aus der
    EquipSlotCategory-Zeile des jeweiligen Items (Sheet-Spalten ilspycmd-
    verifiziert) - keine Slot-Index-Raterei.
(2) Strg+F7 = EMPFOHLENE AUSRÜSTUNG ANLEGEN. Nutzt den SPIEL-EIGENEN
    Optimierer (gleicher Code wie der Knopf im Charakterfenster):
    UIModule.GetRecommendEquipModule -> SetupForClassJob(CurrentClassJobId)
    -> IsUpdating abwarten (Timeout 3 s) -> EquipRecommendedGear. Alles
    ilspycmd-verifiziert (game-api.md-würdig). Ergebnis-Ansage über
    Vorher/Nachher-Vergleich der EquippedItems: "X Teile gewechselt" bzw.
    "unverändert (schon optimal oder gerade nicht möglich)" - ehrliches
    Feedback statt blindem Erfolgs-Claim (Kampf-Sperre etc.).
STUFE 2 OFFEN (braucht F5-Dumps vom User): Navigation IN den Fenstern
Inventar/Charakter/Arsenal (Cursor über Item-Slots sprechen - Slots haben
keine Text-Nodes, Zuordnung Fokus-Slot -> Container-Index nötig, vgl.
V4.46 InventoryEventGrid). Dump-Wunschliste: Tasche offen + Strg+F5,
Charakterfenster offen + Strg+F5, Arsenal offen + Strg+F5.

### Beim nächsten Start testen (V4.66)
1. "Version 4 Punkt 66 bereit".
2. Strg+F6: Ausrüstung wird mit Slot-Namen vorgelesen?
3. Strg+F7: "Lege empfohlene Ausrüstung an" -> Ergebnis-Ansage? Bei
   schlechterem Zeug in der Tasche: wird wirklich gewechselt (Strg+F6
   danach zeigt neue Teile)?
4. V4.65-Beacon: Ton leise bei fernem Ziel, lauter Richtung Ankunft?
5. Für Stufe 2: Tasche/Charakter/Arsenal einzeln öffnen, jeweils Strg+F5
   (Dump), Log/Desktop-Dateien an Claude.

---

## STAND V4.65 (2026-07-16 gebaut)

### Neu in V4.65: Beacon-Lautstärke = Ziel-Distanz (User-Feedback aus V4.64-Test)
V4.64 BESTÄTIGT (Log 10:33): Gehhilfe auf Strg+Numpad3 startet ("Gehhilfe an:
Der einsame Leuchtturmwärter", 343 m, 15 Wegpunkte), Vorschau gesprochen,
Wegpunkt-Erreichen + Skip-Ahead funktionieren (Sprung zu 3/15), Richtung ok.
USER-FEEDBACK: Ton soll leiser sein, je weiter Wegpunkt/Ziel weg ist. Befund:
Lautstärke hing am NÄCHSTEN WEGPUNKT (immer nah → dauernd laut, obwohl das
Ziel 280 m entfernt war). FIX: Signale getrennt — Tonhöhe/Stereo steuern
weiter zum Wegpunkt, die LAUTSTÄRKE folgt jetzt der Rest-Distanz zum ZIEL
(leise = weit, schwillt bis zur Ankunft an). Kurve gestreckt: voll ≤5 m,
linear bis 20 % ab 200 m (vorher 80 m — zu kurz für Quest-Distanzen);
20-%-Boden hält den Ton hörbar. BeaconService.Update + Aufrufer, V4.65.

### Beim nächsten Start testen (V4.65)
1. "Version 4 Punkt 65 bereit".
2. Gehhilfe zu fernem Ziel (Strg+Numpad3): Ton startet LEISE und wird auf
   dem Weg zum Ziel stetig lauter? An Ecken ändert sich nur Tonhöhe/Seite,
   nicht die Lautstärke?
3. Restpunkte aus V4.63/64: Ecken-Führung um Hindernis, Wegpunkt-Ton +
   Ankunfts-Doppelton, Questziel-Führung, Kompass-Check, Auto-Lauf-Vorschau.

---

## STAND V4.64 (2026-07-16 gebaut, in-game bestätigt)

### Neu in V4.64: Gehhilfe-Taste repariert (Windows-NumLock-Umschalt-Falle)
User-Test V4.63 (Log 2026-07-16 08:59): Umschalt+Numpad3 löste NICHTS aus —
kein einziger [Nav]-Gehhilfe-Eintrag, während Strg+Numpad3 (Vorschau) sofort
funktionierte ("Weg zu Verwirrter Fuhrmann, 10 Meter: 10 Meter nach Westen").
ROOT CAUSE: Windows-Tastaturtreiber-Eigenheit — bei aktivem NumLock wird
Umschalt+Numpad-Ziffer in die NAVIGATIONS-Taste umgewandelt (Numpad3 →
Bild-ab, Umschalt künstlich losgelassen). Das Plugin sieht nie VK Numpad3;
Bild-ab ist im Spiel obendrein CAMERA_ZOOMOUT. Die Gehhilfe war damit seit
dem V4.61-Tastenumzug NIE auslösbar. Dokumentiert in game-api.md →
"Safe Mod Keys" (Numpad-Ziffern nie mit Umschalt, nur mit Strg).
FIX: Gehhilfe = Strg+Numpad3 (nachweislich ankommend, neben Auto-Lauf
Numpad3), Routen-Vorschau = Strg+Numpad5 (Numpad5 hat die tastbare
Erhebung; bare Numpad5=CAMERA_FOCUS, Strg+Numpad5 laut Dump frei).
Config-Migration V5→6 (Vorschau ZUERST von Strg+Numpad3 wegziehen, dann
Gehhilfe drauf). Hilfe-Text (Strg+F1) aktualisiert.
Version 4.64, Build 0 Fehler/0 Warnungen, Deploy bestätigt.

### Beim nächsten Start testen (V4.64)
1. "Version 4 Punkt 64 bereit".
2. GEHHILFE: Ziel mit N wählen, dann STRG+Numpad3 — kommt jetzt
   "Gehhilfe an: <Name>" + Beacon + Routen-Vorschau?
3. VORSCHAU: STRG+Numpad5 — "Weg zu <Name>, X Meter ..."?
4. Restliche Testpunkte aus V4.63 unten (Ecken-Führung, Questziel-Führung,
   Abkürzen/Re-Route, Kompass-Check, Auto-Lauf-Vorschau).

---

## STAND V4.63 (2026-07-16 gebaut, Wegpunkt-Routing)

### Neu in V4.63: Wegpunkt-Routing (Routen-Vorschau + pfadbasierte Gehhilfe)
Auftrag: Offener Verdent-Wunsch vom 15.07. — "ansagen über welche Wegpunkte
man den gewünschten Punkt erreicht" und "manuell über mehrere Wegpunkte,
um Hindernisse herum". Umsetzung nach dem extern gelieferten Ratgeber
docs-de/ideen/ff14-route-guidance-guide.md (KOTOR-Mod-Team) plus Verdents
docs/manuelle-navigation-konzept.md.

WICHTIGE KORREKTUR am Konzeptdokument: vnavmesh `Nav.Pathfind` gibt
`Task<List<Vector3>>` zurück, NICHT `List<Vector3>` (ilspycmd an der
installierten DLL, 2026-07-16: QueryPathBasic ist async). Der Task wird
pro Frame gepollt, nie blockiert. Dokumentiert in game-api.md.

NEUE TEILE:
(1) RouteService.cs (neu): Nav.Pathfind-IPC (reine Abfrage, KEINE
    Auto-Bewegung) + Segment-Builder für die Sprach-Vorschau: Wegpunkt-
    Hops werden in 8 Kompass-Sektoren gefaltet (gleiche Richtung =
    zusammengelegt, Hops unter 1 m wandern in den nächsten Abschnitt =
    Mesh-Zittern raus), max. 4 gesprochene Segmente, danach "dann weiter".
    Ansage: "Weg zu Ätheryt, 62 Meter: 25 Meter nach Norden, dann 30
    Meter nach Nordosten, dann weiter."
    Kompass-Konvention Norden=−Z/Osten=+X hergeleitet aus der verifizierten
    Pixel→Welt-Formel + genordeter Spielkarte (game-api.md, neue Sektion);
    jede Vorschau loggt Segment 1 samt Rohvektor als Prüfpunkt.
(2) ROUTEN-VORSCHAU auf Strg+Numpad3 (neben Numpad3=Auto-Lauf und
    Umschalt+Numpad3=Gehhilfe; Numpad3-Kombis laut Keybind-Dump frei,
    kein NVDA-Konflikt bei NumLock an): sagt den Weg zum gewählten Ziel
    an OHNE zu laufen — Questmarker/Wegpunkt aus dem Objekt-Browser oder
    aktuelles Spielziel. Bei "kein Weg": Aethernet-Tipp wie beim Auto-Lauf
    (BuildNoPathHint ist dafür von AutoWalkService nach PlacesService
    umgezogen, ein gemeinsamer Code-Pfad).
(3) GEHHILFE PFADBASIERT (Kern-Upgrade): Beacon + Richtungsansagen
    verfolgen den NÄCHSTEN Wegpunkt der vnavmesh-Route statt der Luftlinie
    — um eine Ecke zeigt der Ton auf die Ecke statt in die Wand.
    Beim Start: Kompass-Vorschau der Route. Danach ereignisgesteuert:
    Wegpunkt erreicht (3-m-Radius) → kurzer hoher Ton (CueService, 1568 Hz,
    mittig) + EINE Ansage zum nächsten Abschnitt relativ zur Blickrichtung
    ("15 Meter, leicht links", bei >1,5 m Höhenunterschied "aufwärts"/
    "abwärts"); dazwischen nur alle 5 s eine Wiederhol-Ansage (vorher
    stur alle 2 s dasselbe). Skip-Ahead: wer die Ecke schneidet oder schon
    nahe einem SPÄTEREN Wegpunkt ist, wird nicht zurückgeschickt (Cursor
    springt still weiter). Drift-Re-Route: >10 m neben der Route (oder
    Ziel-NPC >10 m weitergelaufen) → stilles Neu-Berechnen, Ansage nur
    wenn sich die Richtung dadurch ändert ("Neuer Weg: rechts.").
    Ankunft: fallender Doppelton + "Ziel erreicht" wie bisher.
    Ohne vnavmesh/ohne Route: alte Luftlinien-Führung als Fallback
    ("Kein Wegenetz, führe in Luftlinie."), Config-Schalter
    WalkGuideRouteMode=false erzwingt sie. Neue Config RouteCueVolume.
(4) GEHHILFE KANN JETZT AUCH MARKER-ZIELE: Quest-Ziele, Wegpunkte,
    Ätheryten aus dem Objekt-Browser (vorher nur echte Spielobjekte;
    Fremdzonen-Quests führen zum Übergang, wie beim Auto-Lauf). Die
    Ziel-Auflösung (Zonen-Check, Übergangs-Routing, Navmesh-Höhe) ist
    dafür in Plugin.cs zu TryResolveMarkerDestination zusammengezogen —
    Auto-Lauf, Gehhilfe und Vorschau nutzen denselben Code.
(5) AUTO-LAUF sagt beim Start jetzt einmal die Routen-Vorschau an
    (liest die schon vorhandene Path.ListWaypoints-Diagnose); der
    3-s-Fortschritts-Timer startet 5 s später, damit er die Vorschau
    nicht abschneidet.
Version 4.63 (csproj + Plugin.cs synchron), Build 0 Fehler/0 Warnungen,
Deploy nach devPlugins bestätigt. Commit steht noch aus (erst testen).

### Beim nächsten Start testen (V4.63)
1. "Version 4 Punkt 63 bereit".
2. VORSCHAU: Mit N ein Ziel wählen (z.B. NPC ~50 m weg), Strg+Numpad3:
   kommt "Weg zu <Name>, X Meter: ... nach <Himmelsrichtung> ..."?
   KOMPASS-CHECK: bei einem Ziel mit bekannter Richtung prüfen ob die
   Himmelsrichtung stimmt (Log [Route] zeigt Segment 1 + Vektor —
   falls gespiegelt, bitte Log schicken).
3. GEHHILFE UM ECKE: Ziel hinter einer Ecke/Mauer wählen,
   Umschalt+Numpad3: Vorschau kommt? Beacon zeigt zur ECKE (nicht in
   die Wand)? An der Ecke: kurzer hoher Ton + neue Richtungsansage?
   Am Ziel: fallender Ton + "Ziel erreicht"?
4. GEHHILFE MIT QUESTZIEL: Kategorie Quest-Ziele (Strg+N), Ziel wählen,
   Umschalt+Numpad3 — führt die Gehhilfe? (Vorher ging das gar nicht.)
5. ABKÜRZEN: Beim geführten Laufen absichtlich neben dem Weg laufen
   (>10 m): sagt er nach ein paar Sekunden neue Richtung an bzw. führt
   ohne Streit weiter? Kein Ansage-Spam?
6. AUTO-LAUF: Numpad3 wie gewohnt — zusätzlich einmal die Weg-Vorschau
   nach "Laufe zu ..."? Fortschritt "Noch X Meter" kommt weiter?
7. OHNE ROUTE: Ziel auf getrennter Mesh-Insel (z.B. andere Stadt-Ebene):
   Gehhilfe sagt "Kein Weg gefunden, führe in Luftlinie" + Aethernet-Tipp?
8. V4.62-REST (falls noch nicht getestet): Sprint ohne Countdown-Spam,
   kein "+Sprint"/Kampfzahlen-Spam, Login unverändert.

---

## STAND V4.62 (2026-07-15 gebaut)

### Neu in V4.62: Ansage-Spam gefiltert (_StatusCustom0-Sprint-Countdown, _FlyText)
Auftrag: Top-5-Punkt 1 aus docs/verbesserungsvorschlaege.md umsetzen, aber NUR
den _StatusCustom0/_FlyText-Teil - Login-Geplapper (Zeile 101 unten) laut
User-Entscheid bewusst NICHT anfassen ("damit zufrieden").
BEFUND: Beide Quellen liefen NICHT ueber HudNoiseAddons (das ist eine
hartkodierte Konstante ohne Laufzeit-Schalter), sondern waren schlicht noch
gar nicht gefiltert - der generische Text-Scanner (ScanAddonTexts) und der
Fokus-Leser (FindFocusedText/OnAnyAddonReceive) liefen fuer beide Addons ganz
normal mit. _StatusCustom0 traegt laut Aufbau NUR den Sprint-Countdown als
Text-Node (Statuseffekt-NAMEN kommen nicht als Node-Text, nur per Tooltip) -
eine Vollsperre verliert also keine Information. _FlyText-Popups ("+Sprint",
"700", "(+100 %)") dupliziert nur, was CombatService (HP-Schwellen, Cast-
Ansagen) ohnehin schon sauber aufbereitet ansagt.
FIX (UIReaderService + Configuration):
(1) Zwei neue Configuration.cs-Schalter: SuppressStatusBarSpam (Default true)
    und SuppressFlyTextSpam (Default true) - im Zweifel konfigurierbar statt
    hart verdrahtet, wie vom User verlangt.
(2) Neue Helper-Methode IsSuppressedAddon(name), die HudNoiseAddons PLUS die
    beiden neuen Flags kombiniert; ersetzt HudNoiseAddons.Contains(name) an
    allen 3 Stellen (OnAnyAddonOpen, OnAnyAddonUpdate-Scanner, OnAnyAddonUpdate-
    Fokus) SOWIE zusaetzlich in OnAnyAddonReceive (dort lief bislang GAR KEIN
    HudNoiseAddons-Check - _StatusCustom0/_FlyText haetten also auch ueber den
    ReceiveEvent-Fokus-Pfad durchrutschen koennen).
(3) UIReaderService bekommt Configuration als neuen Konstruktor-Parameter
    (Plugin.cs entsprechend angepasst).
Wichtige Statuseffekte (neue Debuffs/Buffs) werden dadurch NICHT stummgeschaltet -
es wird weiterhin nichts an dieser Stelle je nach Statuseffekt-Namen gefiltert,
sondern das GESAMTE Addon (das ohnehin nur den Countdown zeigt); eine gezielte
Buff-/Debuff-Ansage nach Status-ID ist als eigener Vorschlag in
verbesserungsvorschlaege.md vermerkt (separates Vorhaben, nicht Teil dieses
Auftrags). Login-Geplapper (INVENTAR/SEITE AN SEITE/Menü-Text) unveraendert.
Version 4.62, Build 0 Fehler/0 Warnungen, Deploy nach devPlugins bestaetigt.

### Beim naechsten Start testen (V4.62)
1. "Version 4 Punkt 62 bereit".
2. SPRINT: Sprint/Dauerlauf aktivieren - KEIN Countdown-Sekundentakt ("20s"..
   "1s") mehr aus der Buff-Leiste, KEIN "+Sprint"/"700"/"(+100 %)" mehr aus
   FlyText?
3. LOGIN: unveraendert - weiterhin wie in V4.61 (Server-Warteschlange wird
   vorgelesen, kein "Beenden"-Spam, keine "Tastenbelegung gespeichert"-Ansage)?
4. Neue Statuseffekte/Debuffs im Kampf (z.B. Silence, Vulnerability-Stack):
   werden andere HUD-Ansagen (Kampf-HP, Cast-Ansagen) weiterhin normal gehoert?

---

## STAND V4.61 (2026-07-13 gebaut)

### Neu in V4.61: Auto-Lauf-Wächter repariert (kein Fehlabbruch bei Umwegen)
User-Meldung + Log 2026-07-13 01:08: "Komme nicht näher"-Meldung kam kurz nach
dem Start, obwohl der Charakter lief — vnavmesh nimmt Umwege, dabei steigt die
Luftlinie zum Ziel zeitweise. Zweiter Befund aus demselben Log: Der alte
Abbruch setzte nur `_active=false`, stoppte den vnavmesh-Pfad NICHT — deshalb
lief der Charakter nach der Meldung weiter (und kam sogar an).
FIX (AutoWalkService):
(1) Wächter misst jetzt die BEWEGUNG DES CHARAKTERS (Positionswechsel >=0,5 m
    setzt den 5s-Timer zurück), nicht mehr die Zieldistanz. Umwege = Bewegung =
    kein Abbruch. Echtes Verkeilen (Position friert ein, wie 26 m vor dem
    Übergang nach Unteres La Noscea, La Thagran-Grenzposten) wird weiter erkannt.
(2) Bei echtem Feststecken wird jetzt auch der vnavmesh-Pfad gestoppt
    (Stop(announce:false)), Ansage: "Ich stecke fest, noch X Meter."
(3) csproj-Version hing noch auf 4.59 (Plugin.cs sagte 4.60) — beides auf 4.61
    synchronisiert.

### Auch in V4.61: Tasten-Umzug (Strg+Alt+N war NVDA-Hotkey!)
User-Meldung: Strg+Alt+N (Kategorie zurück, V4.59) ist der Windows-Hotkey zum
NVDA-Start — unbenutzbar. Alt+N ist im Spiel CMD_BEGINNER (Neulings-Chat).
User-Entscheid (3 Optionen angeboten): TAUSCH —
- Kategorie zurück = Strg+Umschalt+N (Logik "Umschalt = rückwärts" wie N/Umschalt+N)
- Gehhilfe = Umschalt+Numpad3 (neben Auto-Lauf Numpad3; Numpad3-Kombis laut
  Keybind-Dump frei, Numpad 0-9 sonst alle vom Spiel belegt)
Config-Migration Version 4→5 (gezielt, nur wenn alte Defaults gesetzt; Gehhilfe
ZUERST umziehen, dann Kategorie-zurück — sonst greift der zweite Check auf den
frisch vergebenen Wert). Hilfe-Text (Strg+F1) aktualisiert.

### V4.60-Testergebnis (Log 2026-07-13 01:00) — BESTÄTIGT
- Bestiarium-Zeilen werden angesagt ("1 von 31, Hermetiker 01", "2 von 31,
  Marienkäfer, 0 von 3", "3 von 31, 75, Vergütung") ✓
- Strg+F4-Übersicht liest die Liste ✓ — ABER nur 20 von 31 Items (Rest fehlt,
  vermutlich eingeklappte/virtuelle Zeilen ohne Renderer)
- Rang-Namen-Frage GEKLÄRT: die "0/10, NEU"-Zeilen sind eine ZWEITE Liste
  (Rang-Auswahl 1–5). Textnodes: id=7 '0/10', id=6 Rang-NUMMER '1'..'5',
  id=5 unsichtbar 'NEU'; StringValues leer ("(kein Item)"). Es gibt dort
  KEINEN Klartext-Namen. Ansage derzeit "1, 0 von 10" — Aufwertung zu
  "Rang 1, 0 von 10, neu" möglich (User noch nicht gefragt).

### Auch in V4.61: Ätheryten zonenweit + Kein-Weg-Aethernet-Tipp (User-Wunsch)
User: (1) Ätheryten über 100 m hinaus sehen/hinlaufen; (2) bei "kein Weg
gefunden" über andere Wegpunkte routen. EINORDNUNG zu (2): "Kein Weg" =
getrennte Mesh-Inseln (Stadt-Ebenen, nur per Aufzug/Aethernet verbunden) —
Laufen über Zwischenpunkte kann die Lücke NIE schließen, spielgerechter Weg
ist das Aethernet. Umsetzung:
(1) Kategorie "Ätheryten" im Objekt-Browser jetzt SHEET-basiert (PlacesService/
    MapMarker DataType 3+4 = Ätheryt + Aethernet-Splitter, zonenweit statt
    ObjectTable ≤100m). CyclePlaceDestination(aetherytesOnly), Ansage
    "Kategorie Ätheryten: N im Gebiet", Numpad 3 läuft hin (Places-Pipeline,
    Y via Navmesh). ObjectKind.Aetheryte-Filter entfällt.
(2) AutoWalkService.BuildNoPathHint (PlacesService injiziert): Bei "Kein Weg
    zu X gefunden" wird der zielnächste Ätheryt/Splitter genannt, wenn er ≤100m
    (2D) am Ziel liegt: "Das Ziel liegt nahe Aethernet <Name>. Reise per
    Aethernet dorthin." + [Nav] Kein-Weg-Tipp-Log. Workflow für den User:
    Strg+N → Ätheryten → N → Numpad 3 → am Splitter interagieren (Spiel-Menü
    wird von der Listen-Navigation gelesen) → Ziel-Splitter wählen → weiter.

### Auch in V4.61: Bestiarium-Lebensraum + Monster-Tracking (User-Wunsch)
User: "kann man die Monster auch tracken bzw. ansagen in welche Gegend man muss?"
DATENLAGE (ilspycmd 2026-07-13, Lumina.Excel.dll): MonsterNoteTarget-Sheet hat
pro Monster BNpcName-Ref + PlaceNameZone[3] + PlaceNameLocation[3] (Zone +
Untergebiet, bis zu 3 Fundorte) — exakt die Lebensraum-Info der Sehenden-UI.
MonsterNote-Sheet: Name, Reward, 4× Target-Ref + 4× Count.
NEU: BestiaryService.cs — lazy Dictionary BNpcName.Singular (lowercase) →
Lebensraum-Text ("Zone, Untergebiet, oder Zone2, …").
(1) Bestiarium-Zeilen-Ansage + Strg+F4-Übersicht hängen bei Monster-Zeilen
    ". Lebt in <Lebensraum>" an. Monster-Erkennung: Zeile hat Fortschritts-
    Token ("X von Y") + Name trifft im Sheet (Rang-Zeilen/Vergütungen nicht).
    Sheet-Fehltreffer werden geloggt ("[Bestiary] Kein Lebensraum für …").
(2) TRACKING: UIReaderService.SelectedBestiaryMonster (fokussierte Monster-
    Zeile, nur solange MonsterNote sichtbar). Numpad 3 bei offenem Bestiarium:
    sucht nächsten LEBENDEN BattleNpc gleichen Namens (CurrentHp>0) in der
    ObjectTable → anvisieren (mit Read-back-Warnung) + Auto-Lauf hin;
    keiner in der Nähe → "Kein <Name> in der Nähe. Lebt in <Lebensraum>."
UNVERIFIZIERT (Log klärt es): UI-Monstername == BNpcName.Singular (Groß-/
Kleinschreibung egal, aber Wortlaut muss stimmen).

### Auch in V4.61: Login-/Lobby-Fixes (User-Feedback 2026-07-13)
(1) SelectOk-Dialog (Server-Warteschlange "hoher Andrang"): Text wurde
    gesprochen, aber 14 ms später vom Fokus-Leser ("Abbrechen") ABGESCHNITTEN
    → User hörte nur "Abbrechen". Fix: OnAnyAddonOpen setzt bei SelectOk
    dieselbe 1s-Dialog-Schutzsperre wie SelectYesno (_dialogOpenedAt).
(2) "Beenden"-Spam: _CharaSelectReturn (trägt nur den Beenden-Knopf) meldete
    bei jedem Lobby-Fenster-Neuaufbau ungefragt Fokus → in HudNoiseAddons.
    Gezielte Navigation dorthin sagt weiter der globale FocusedNode-Leser an.
(3) Auto-Keybind-Dump nach Login ist jetzt STUMM (DumpKeybinds announce:false,
    nur Log/Datei); gesprochen wird nur noch bei echtem Tasten-Konflikt oder
    manuellem /acc keys. "Tastenbelegung gespeichert…" bei jedem Login war Lärm.

### Spam-Quellen aus dem Log (Stand V4.61 - _StatusCustom0/_FlyText inzwischen in V4.62 gefixt s.o.)
- _StatusCustom0 (Buff-Leiste): Sprint-Countdown "20s".."1s" im Sekundentakt
  gesprochen → GEFIXT in V4.62 (SuppressStatusBarSpam, Default an)
- _FlyText: "+Sprint", "+Dauerlauf", "-Sprint", "700", "(+100 %)" gesprochen
  → GEFIXT in V4.62 (SuppressFlyTextSpam, Default an)
- Restliches Login-Geplapper (00:59: "INVENTAR", "SEITE AN SEITE", "Menü, 0
  Einträge", ".", "Ziel.") — User: "nicht so schlimm", bewusst zurückgestellt
  (bleibt weiterhin unangetastet, User-Entscheid 2026-07-15 bestätigt)
- V4.59-Test (Quest-Objective) steht WEITER AUS (keine [Quest]
  Objective-Zeilen im Log)

### Beim nächsten Start testen (V4.61)
1. "Version 4 Punkt 61 bereit".
2. AUTO-LAUF mit Umwegen (Numpad 3, längere Strecke): KEIN "Komme nicht
   näher"-Fehlabbruch mehr kurz nach Start?
3. An einer Verkeil-Stelle (z.B. Übergang Unteres La Noscea): kommt "Ich
   stecke fest, noch X Meter" und der Charakter hört auf zu laufen (Pfad
   wird jetzt wirklich gestoppt)?
4. TASTEN-UMZUG: Meldet der Start "Konflikt"-frei? Strg+Umschalt+N blättert
   die Kategorie RÜCKWÄRTS (nicht mehr Gehhilfe!), Umschalt+Numpad3 schaltet
   die Gehhilfe?
5. V4.59 nachholen: Quest-Ziele blättern (Strg+N) → Objective hinter dem
   Namen?
6. LOGIN: Bei der Server-Warteschlange ("hoher Andrang") wird jetzt der
   TEXT gesprochen (nicht mehr nur "Abbrechen")? Kein ungefragtes "Beenden"
   mehr in der Charakterauswahl? Keine "Tastenbelegung gespeichert"-Ansage?
7. BESTIARIUM: Monster-Zeile fokussieren → kommt "… Lebt in <Gebiet>"?
   Numpad 3 bei fokussiertem Monster: läuft hin (wenn eins in der Nähe)
   bzw. sagt "Kein X in der Nähe. Lebt in …"? Falls kein Lebensraum kommt:
   Log mitschicken ([Bestiary] Kein Lebensraum für … = Namens-Mismatch).
8. ÄTHERYTEN: Strg+N bis "Kategorie Ätheryten: N im Gebiet" → zählt sie
   die ganze Zone (auch weiter als 100 m)? N blättert mit Distanz/Richtung,
   Numpad 3 läuft hin?
9. KEIN-WEG-TIPP: An einer bekannten "kein Weg"-Stelle (z.B. anderes
   Stadt-Level in Limsa) → kommt "… Das Ziel liegt nahe Aethernet <Name>"?

---

## STAND V4.60 (2026-07-13 gebaut, BESTÄTIGT s.o.)

### Neu in V4.60: Bestiarium (Jagdtagebuch, "MonsterNote") barrierefrei
User-Wunsch: Bestiarium vorlesbar machen; User wählte "Beides" (UI-Mitlesen +
Übersichts-Taste). Dump 2026-07-12 (dalamud.log ~Zeile 456) analysiert.
STRUKTUR: MonsterNote = ein Addon mit einer TreeList (Comp CT=TreeList). Zeilen
sind ListItemRenderer in drei Templates: Rang-Überschriften (Comp 1015: Name +
Badge "Erledigt!"), Monster-Zeilen (Comp 1017: Fortschritt "0/3" + Name + Icons)
und Vergütungen (Comp 1018: Betrag + "Vergütung"). Datenmodell ilspycmd-verifiziert:
AtkComponentTreeList.Items (@432) = logische Zeilen in visueller Reihenfolge;
AtkComponentTreeListItem trägt StringValues (@24, Spiel-Anzeige-Strings) + Renderer
(@48). MonsterNoteManager (12× MonsterNoteRankInfo, RankData[10], Kill-Counts) +
Lumina MonsterNote-Sheet existieren (für spätere direkte Datenmodell-Lesung notiert).
ROOT CAUSE des Bugs (Log 21:34): Beim Navigieren bewegen sich die Listen-Indizes
(Hovered/Selected) NICHT (alle -1), aber der globale FocusedNode wandert. Der
generische Fokus-Leser las darum immer nur "0/10, NEU" (Fortschritt + Badge einer
Rang-Überschrift OHNE Rang-Namen) — User hörte bestätigt nur das.
FIX (UIReaderService):
(1) Dedizierter Handler OnMonsterNoteUpdate (PostUpdate "MonsterNote"): klettert
    vom FocusedNode hoch zum ListItemRenderer, ordnet ihn per Items→Renderer der
    logischen Zeile zu, liest deren StringValues (Fallback: sichtbare Text-Nodes
    des Renderers). FormatBestiaryRow stellt "0/3, Marienkäfer" → "Marienkäfer,
    0 von 3" um (Fortschritt ans Ende, ausgeschrieben). Dedup pro Renderer+Text.
    Ansage "X von Y, <Zeile>".
(2) UpdateGlobalFocus schweigt jetzt, solange MonsterNote sichtbar ist (kein
    "0/10, NEU"-Doppel mehr).
(3) ÜBERSICHTS-TASTE Strg+F4 (KeyBestiary, Keybind-Dump frei): AnnounceBestiary-
    Overview liest die ganze offene TreeList am Stück (alle Items in visueller
    Reihenfolge). Kein Sheet-Mapping — liest die vom Spiel gesetzten Strings.
(4) GROUND-TRUTH-PROBE [Bestiary]: pro Zeilenwechsel werden ALLE Text-Nodes der
    Zeile (sichtbar UND unsichtbar, mit id/vis) + die Roh-StringValues geloggt.
    Klärt, wo bei unfertigen Rängen ("0/10, NEU") der Rang-NAME steckt (der Dump
    zeigte nur fertige Ränge mit Namen). Danach ggf. Rang-Namen-Ansage nachziehen.

### Beim nächsten Start testen (V4.60)
1. "Version 4 Punkt 60 bereit".
2. BESTIARIUM öffnen, mit Pfeiltasten/Controller durch die Liste navigieren:
   werden jetzt Rang-Überschriften, Monster ("Marienkäfer, 0 von 3") und
   Vergütungen sauber angesagt — nicht mehr nur "0/10, NEU"?
3. Kommt bei den Rang-Überschriften ein NAME (z.B. "Hermetiker 01") oder weiter
   nur "0 von 10, NEU"? Die [Bestiary] Probe-Logzeilen mitschicken (zeigen alle
   Text-Nodes inkl. unsichtbar + Roh-Strings → daraus ziehe ich den Rang-Namen).
4. Strg+F4 → wird die ganze Liste am Stück vorgelesen? Stimmen Namen/Fortschritt?
   [Bestiary] Übersicht-Logzeile mitschicken.

---

## STAND V4.59 (2026-07-12, V4.59 gebaut)

### Neu in V4.59: Quest-Objective + Kategorie-zurück-Taste
User-Wünsche: (1) bei Quest-Zielen hinter dem Namen zeigen, was noch fehlt;
(2) Taste, um Objekt-Kategorien RÜCKWÄRTS zu blättern.
(1) OBJECTIVE: QuestManager liefert nur Sequenz-Zahlen, kein Klartext; der
Objective-Text liegt nur im laufenden Quest-Tracker (_ToDoList). QuestMarkerService.
GetQuestObjectives liest ihn via RaptureAtkUnitManager. Node-ID-Muster (aus Probe
19:59 verifiziert): Header 70000+slot = Quest-Name, Objective 20000+slot*100+idx.
Map Quest-Name → Objective wird in CycleQuestDestination angehängt: "1 von 3:
Story: Fast wie zu Hause, Baderon Bericht erstatten, 55 Meter, links." Nur
GETRACKTE Quests haben ein Objective (Tracker zeigt begrenzt viele); sonst wie
bisher. Jede Zuordnung als [Quest] Objective-Log.
(2) KATEGORIE ZURÜCK: neue Taste Strg+Alt+N (KeyCategoryPrev, laut Keybind-Dump
frei; bare N-Familie war voll). NextCategory/PreviousCategory teilen CycleCategory.

### Beim nächsten Start testen (V4.59)
1. "Version 4 Punkt 59 bereit".
2. OBJECTIVE: Quest-Ziele (Strg+N bis Quest-Ziele) durchblättern → steht jetzt
   hinter dem Quest-Namen das aktuelle Ziel ("… , Aurelias mit Hermetik erlegen
   0/3, …")? Stimmt die Zuordnung Quest→Objective? [Quest] Objective-Logzeilen
   mitschicken (zeigen slot/name/objective — verifiziert das ID-Muster).
3. KATEGORIE ZURÜCK: Strg+Alt+N → blättert die Kategorie rückwärts (Gegenprobe
   zu Strg+N vorwärts)?

---

## STAND V4.58 (2026-07-12, gebaut)

### V4.57 BESTÄTIGT (Log 2026-07-12, komplettes Limsa-Tutorial gespielt)
Story-Kennzeichnung funktioniert ("Story: Fast wie zu Hause", 899 Hauptszenario-
Namen geladen). Das Plugin trägt sauber durchs Tutorial: NPC-Dialoge, Auto-Lauf,
Quest-Annahme/Abschluss, Kräutersammeln. Log-Analyse ergab 5 Ansage-Ärgernisse
→ in V4.58 behoben.

### Neu in V4.58: Ansagen entrümpelt (5 Log-Befunde behoben)
Alle aus der Log-Auswertung 2026-07-12 (komplettes Tutorial), Ursachen am
Quellcode verifiziert.
1. **SeString-Payloads (GENERELL, TolkService.Sanitize):** Roher
   Utf8String.ToString() reicht FFXIV-Payload-Steuerbytes durch (START 0x02 …
   END 0x03). Belohnungs-Gil-Zelle kam als "H%I&GilIH", NPC-Dialoge trugen
   "\x02\x10\x01\x03"-Umbruch-Chunks. Byte-Analyse aus dem Log bestätigt die
   Delimiter. Sanitize verwirft jetzt ganze Payloads + verirrte C0-Steuerzeichen
   (nicht \t\n\r). Wirkt auf ALLE Ansagen.
2. **Quest-Fenster-Reiter (UIReaderService.BuildQuestText):** Beim Öffnen von
   JournalAccept/Result las der Text-Fallback in den ersten Frames alle Canvas-
   Texte = die Reiter "Zusammenfassung. Optionen. Vergütung bei Erfolg …" vor der
   Beschreibung. Statische Header-Blockliste (QuestPanelHeaders) filtert sie raus.
3. **Belohnungs-Zahlen (UIReaderService.UpdateGlobalFocus):** Navigiert man im
   JournalResult die Währungszellen, kamen nackte "400"/"103". Da die
   Zusammenfassung ("Belohnung: Erfahrung 400. Gil 103") schon beim Öffnen kommt,
   werden reine Zahlen bei sichtbarem JournalResult stumm gehalten (IsBareNumber
   + IsAddonVisible). Buttons/Item-Namen (nicht-numerisch) bleiben.
4. **Doppel-Meldungen (Tolk-Verlauf + ChatReaderService):** Toast-Notification
   (_TextError, INT) UND Chat-SystemMessage lasen dieselbe Meldung ~3 s versetzt
   ("Du hast einen Auftrag angenommen!" 2×). TolkService führt jetzt einen 10-s-
   Verlauf (Remember); ChatReaderService überspringt die Chat-Zeile, wenn der
   präfixlose Text in den letzten 6 s schon gesprochen wurde (WasRecentlySpoken).
5. **Hotbar-Keybinds (HotbarService.CleanUpHelp):** "Spielanleitung [9]",
   "Teleport [0]" — der Keybind-Hinweis in eckigen Klammern wird jetzt am Ende
   abgeschnitten.
6. **Cross-Zone-Quest-Dedup (NavigationService.GetQuestDestinations):** Mehrere
   Marker DERSELBEN Quest in einer Fremdzone lösten denselben langen Routing-Satz
   3× aus ("1 von 3 … 2 von 3 …", identisch). Cross-Zone-Marker werden auf einen
   Eintrag je (Quest, Zielkarte) reduziert (nächster überlebt); In-Zone-Marker
   bleiben einzeln.
7. **HP/MP-Abfrage auf Strg+H (User-Wunsch):** Die HP/MP-Ansage (AnnounceStatus:
   "HP X Prozent, MP Y Prozent", im Kampf + Ziel-HP) liegt jetzt auf **Strg+H**
   statt Strg+F12. Verifiziert am Live-Keybind-Dump: bare H = MENU_CRAFT (belegt),
   Modifier+H frei. Config-Migration Version 3→4 stellt gespeichertes "Strg+F12"
   → "Strg+H" um. Format ist weiterhin PROZENT (falls absolute Zahlen gewünscht:
   AnnounceStatus in CombatService anpassen).
8. **BUG-FIX Strg+L (Level) war tot:** KeyNameToVK kannte nur "N" als Buchstabe,
   NICHT "L" — das in V4.56 eingeführte Strg+L konnte nie geparst werden
   (IsJustPressed immer false, nur Log-Warnung). "H"=0x48 und "L"=0x4C zum
   Dictionary hinzugefügt → Strg+H UND Strg+L funktionieren jetzt.
9. **Auto-Lauf ohne Beacon-Piepen (User-Wunsch):** Der Richtungs-Beacon (Piep-Ton)
   lief beim Auto-Lauf (Numpad3) mit und war störend, da das Spiel ohnehin selbst
   steuert. Beacon komplett aus dem AutoWalkService entfernt (Feld/Parameter/alle
   Aufrufe); gesprochener Fortschritt bleibt. Der Beacon bleibt bei der MANUELLEN
   Gehhilfe (Strg+Umschalt+N) erhalten — dort steuert man selbst per Ton. Beide
   schließen sich weiter aus.

### Beim nächsten Start testen (V4.58)
1. "Version 4 Punkt 58 bereit".
2. QUEST ABSCHLIESSEN: Belohnungsfenster → kommt "Belohnung: Erfahrung X. Gil Y"
   OHNE anschließenden "H%I&GilIH"-Müll? Beim Durchnavigieren KEINE nackten
   Zahlen mehr (Buttons/Items schon)?
3. QUEST ANNEHMEN (JournalAccept): kommt gleich "Beschreibung: …" OHNE führendes
   "Zusammenfassung. Optionen. Vergütung …"?
4. NPC-DIALOGE: hört sich der Text sauber an (keine seltsamen Steuerzeichen/
   Zeichenketten mittendrin)?
5. AUFTRAG ANNEHMEN/ABSCHLIESSEN: kommt "Du hast einen Auftrag angenommen!" nur
   noch EINMAL (nicht zusätzlich als "System: …")?
6. HOTBAR Strg+F9: "Taste 9, Spielanleitung" OHNE "[9]" am Ende?
7. QUEST-ZIELE (Strg+N): mehrere Story-Marker in Fremdzone → wird die Quest nur
   noch EINMAL angesagt statt "1 von 3/2 von 3/3 von 3" identisch?
8. HP/MP: **Strg+H** → "HP X Prozent, MP Y Prozent"? (Strg+F12 tut jetzt nichts.)
9. STUFE: **Strg+L** → "Stufe X. Noch N Erfahrungspunkte…"? (War vorher tot,
   sollte jetzt gehen.)
10. AUTO-LAUF (Numpad3): läuft OHNE Piep-Ton, nur mit "Noch X Meter"? Manuelle
    Gehhilfe (Strg+Umschalt+N) piept weiterhin?

---

### Neu in V4.57: Story-Quests gekennzeichnet + NPC-Ansage umgestellt
User-Wünsche: (1) Hauptszenario-Quests als "Story" kennzeichnen; (2) bei NPCs
zuerst Beruf/Quest, dann Name.
(1) STORY: QuestMarkerService erkennt MSQ über Quest-Sheet (JournalGenre →
JournalCategory → JournalSection.RowId==0 = Hauptszenario, ilspycmd-verifiziert),
baut 1× ein Namens-HashSet, matcht Marker-Label. Quest-Ansage: "Story: <Quest>,
…" bei MSQ. IDataManager in QuestMarkerService injiziert. [Quest] Hauptszenario-
Namen-Log zeigt Anzahl.
(2) NPC-ANSAGE: DescribeNpc → NpcPrefix (liefert "Beruf, Quest, " als PRÄFIX).
Neu: "Ziel: Marktverwalter, Quest verfügbar, Miounne, NPC, 12 Meter, geradeaus."
statt Name zuerst. Gilt für Zielwechsel-Ansage UND Objekt-Browser (N).

### Beim nächsten Start testen (V4.57)
1. "Version 4 Punkt 57 bereit".
2. STORY: Quest-Ziele (Strg+N bis Quest-Ziele) durchblättern → werden MSQ-Quests
   mit "Story:" angesagt, Nebenquests ohne? [Quest] Hauptszenario-Namen-Zeile
   mitschicken (zeigt, ob Erkennung greift).
3. NPC: einen NPC anvisieren/mit N durchblättern → kommt Beruf/Quest ZUERST,
   dann der Name?

---

## STAND V4.56 (2026-07-12)

### Neu in V4.56: Level-Ansage auf Strg+L
User-Wunsch: Level-Ansage weg von Umschalt+F12. Bare "L" ist im Spiel belegt
(MENU_LINKSHELL, Keybind-Dump), Modifier+L aber frei → Strg+L (L=Level).
Config-Migration Version 2→3: bestehendes "Umschalt+F12" wird automatisch auf
"Strg+L" umgestellt (gezielt, andere Tastenanpassungen bleiben).
TASTEN-ÜBERSICHT aktuell: N/Umschalt+N/Strg+N/Strg+Umschalt+N = Objekt-Browser,
Numpad3 = Auto-Lauf, Strg+F1..F12 = Hilfe/Fenster/Inventar/Dump/Hotbar/Menü/
Stille/Kampf, Umschalt+F3 = Gil, Umschalt+F4/F5/F6 = Emote, Strg+L = Stufe/EXP.

### Beim nächsten Start testen (V4.56)
1. "Version 4 Punkt 56 bereit".
2. Strg+L → kommt "Stufe X. Noch N Erfahrungspunkte…"? (Umschalt+F12 tut jetzt nichts.)

---

## STAND V4.55 (2026-07-12)

### Neu in V4.55: Quest-Belohnung als verständliche Zusammenfassung
User: "bei der Questbelohnung nur noch Zahlen — ich will wissen WAS der Eintrag
ist." DIAGNOSE (UI-Dump JournalResult + [Focus]-Log): KEIN Bug der Stückzahl-
Funktion (benannte Items lösen weiter auf). Das Belohnungsfenster hat 2 Sorten
Einträge: ITEMs (Comp(1010) mit Icon → Name) und WÄHRUNG/EXP (Comp(1007), Betrag
in TextNineGrid, Typ nur als Bild → Fokus las nur "260"/"127"/"50"/"103").
FIX: UIReaderService.BuildRewardText liest beim Öffnen von JournalResult eine
Zusammenfassung: "Belohnung: <Item mal N>, Erfahrung X, Gil Y". Items per
Icon→Name, Beträge per Wert. WORKAROUND (dokumentiert + geloggt): Währungs-TYP
steht nur als UI-Bild ohne Icon-Id → Label per Position (Erfahrung zuerst, dann
Gil = Standard-FF14-Reihenfolge). [Quest] JournalResult Belohnung-Log zeigt
items/amounts zur Verifikation. Struktur in game-api.md → "JournalResult".
HINWEIS: Die nackten Zahlen bei Fokus-Navigation der Währungszellen bleiben
vorerst (die Zusammenfassung beim Öffnen deckt den Inhalt ab); bei Bedarf
Fokus-Zellen zusätzlich labeln.

### Beim nächsten Start testen (V4.55)
1. "Version 4 Punkt 55 bereit".
2. QUEST ABSCHLIESSEN: Belohnungsfenster öffnet → kommt "Belohnung: <Items>,
   Erfahrung X, Gil Y"? Stimmen Item-Namen + Beträge? Sind Erfahrung/Gil richtig
   herum (nicht vertauscht)? [Quest] JournalResult Belohnung-Logzeile mitschicken.

---

## STAND V4.54 (2026-07-12)

### Neu in V4.54: Emote-Browser (Verbeugen & Co. ohne Chat)
User braucht für eine Quest das Emote "Verbeugen", kann aber Chat nicht tippen
und fand das Gesten-Menü nicht. WICHTIG: "/verbeugen" existiert NICHT als
Textbefehl (User bestätigt) — deutscher /befehl ≠ Anzeigename. Lösung: EmoteService
löst Emotes direkt über die Spielfunktion aus (AgentEmote.ExecuteEmote,
ilspycmd-verifiziert), gefiltert auf freigeschaltete (CanUseEmote). Namen +
echter /befehl kommen aus dem Lumina-Emote-Sheet (nichts geraten).
TASTEN: Umschalt+F5 = nächstes Emote ansagen ("3 von 45: Verbeugen, Befehl /x"),
Umschalt+F4 = vorheriges, Umschalt+F6 = ausführen. Liste alphabetisch, lazy beim
ersten Druck gebaut (braucht eingeloggten Char + AgentEmote bereit).

### Beim nächsten Start testen (V4.54)
1. "Version 4 Punkt 54 bereit".
2. EMOTE: Umschalt+F5 mehrfach → werden Emote-Namen angesagt (inkl. Befehl)?
   Bis "Verbeugen" blättern → Umschalt+F6 → verbeugt sich der Charakter? Zählt
   die Quest es? [Emote]-Logzeilen mitschicken (id/name + echter /befehl).
3. Umschalt+F4 blättert rückwärts?

---

## STAND V4.53 (2026-07-12)

### Neu in V4.53: Stufe + EXP anzeigen + automatische Level-Up-Ansage
User-Wunsch: eigenes Level sehen, wie viel EXP bis Level-Up fehlt, und beim
Leveln die Meldung hören. Alles aus PlayerState (ilspycmd-verifiziert,
game-api.md → "Stufe / Erfahrung"), kein UI-Scraping.
(1) TASTE Umschalt+F12 (CombatService.AnnounceLevelExp): "Stufe X. Noch N
Erfahrungspunkte bis zur nächsten Stufe." (Maximalstufe → "Stufe X,
Maximalstufe erreicht."). CurrentLevel + ps->GetCurrentClassJobExp() /
GetCurrentClassJobNeededExp(); "noch" = needed − current.
(2) AUTOMATISCH: CombatService.TrackLevelUp (jeden Frame) sagt bei Stufenanstieg
"Stufe X erreicht." Nur bei Anstieg für DENSELBEN Job (Jobwechsel ändert
CurrentLevel auch → kein Fehlalarm; Baseline nach Login/Jobwechsel still gesetzt).
Config KeyLevelExp="Umschalt+F12" + Konflikt-Check.

### Beim nächsten Start testen (V4.53)
1. "Version 4 Punkt 53 bereit".
2. STUFE/EXP: Umschalt+F12 → "Stufe X. Noch N Erfahrungspunkte …"? Zahl plausibel?
3. LEVEL-UP: einen Gegner/eine Quest zum Stufenaufstieg → kommt automatisch
   "Stufe X erreicht."? [Level]-Logzeile mitschicken.

---

## STAND V4.52 (2026-07-12)

### V4.51 BESTÄTIGT (User + Log 10:56–11:07): Quest-Vorlesen funktioniert
Journal (JournalDetail) liest Ziel + Beschreibung sauber vor
("Ziel: Mit Chansteloup sprechen. Beschreibung: …"). JournalResult (abschließen)
feuert ebenfalls. ABER Log-Befunde:
- Node id=38 = "Bonus." (EXP-Bonus-Abzeichen), NICHT der Titel — hing als Rauschen
  vor jeder Quest ("Bonus. Ziel: …"). Über mehrere Quests konsistent.
- JournalResult liefert NUR "Bonus." — Belohnungen/Abschlusstext fehlen (Reward-
  Slots sind Icons, Narrativ evtl. in anderen Nodes). Struktur noch unbekannt.
- Mein Diagnose-Logging ([Quest] canvas textNode / probe:) feuerte NICHT im
  getesteten Build → in V4.52 robuster (Reset beim Schließen).
- JournalAccept tauchte im Log NICHT auf (Annehmen evtl. nicht getestet ODER
  anderer Fenstername) → beim nächsten Test [Accessibility] Addon:-Zeile prüfen.

### Neu in V4.52: "Bonus."-Rauschen raus + robustere Diagnose
(1) Node id=38 wird aus der Quest-Ansage entfernt (bleibt im Diagnose-Log
sichtbar). Journal liest jetzt "Ziel: … Beschreibung: …" ohne führendes "Bonus.".
Quest-NAME wird bewusst nicht wiederholt (kommt bei Listen-Auswahl).
(2) _questProbed wird beim Schließen zurückgesetzt → erneutes Öffnen loggt die
Canvas-Node-Struktur frisch. OFFEN: JournalResult (Abschluss) liest nach id=38-
Ausschluss evtl. gar nichts mehr (nur "Bonus." war da) → braucht eigenen Reward-
Reader, dafür 1 frisches [Quest]-Log nötig.

### Beim nächsten Start testen (V4.52)
1. "Version 4 Punkt 52 bereit".
2. JOURNAL (J), Quest auswählen → kommt "Ziel: … Beschreibung: …" OHNE "Bonus."?
3. QUEST ANNEHMEN bei einem NPC → wird die Beschreibung gelesen? Welcher
   Fenstername steht im Log ([Accessibility] Addon: …)?
4. QUEST ABSCHLIESSEN → was wird gelesen? Bitte die [Quest]-Zeilen (canvas
   textNode id=… / probe:) mitschicken — daraus baue ich den Belohnungs-Reader.

---

## STAND V4.51 (2026-07-12)

### Neu in V4.51: Quest-Fenster automatisch vorlesen
User-Wunsch: Questbeschreibung fehlt (alle 4 Situationen), erstmal AUTOMATISCH.
Dedizierter Handler OnQuestWindowUpdate (PostUpdate + Dedup pro Addon) für
JournalDetail (Journal, Taste J), JournalAccept (Quest annehmen), JournalResult
(Quest abschließen). Alle drei aus dem generischen Pfad genommen (HudNoiseAddons),
damit kein Doppel-Lesen. Timing: Text wird erst Frames nach Öffnen gesetzt +
ändert sich bei Seitenwechsel → PostUpdate liest bei jeder Änderung.
BuildQuestText verallgemeinert TryReadQuestDetail: findet die JournalCanvas-
Komponente; für JournalDetail strukturiert (verifizierte Node-IDs 38 Titel/
9 Stufe/8 Beschreibung/Ziel-Zeilen Multipurpose id=3), für Annehmen/Abschließen
(IDs NICHT verifiziert) Fallback = alle sichtbaren Canvas-Texte in Reihenfolge.
Strg+F10 (TryReadQuestDetail) nutzt jetzt dieselbe BuildQuestText-Basis.
DIAGNOSE: Jeder Canvas-Textknoten wird 1× als [Quest]-Zeile geloggt (id + Text);
Fenster OHNE Canvas werden per ProbeQuestStructure einmal strukturell geloggt
([Quest] … probe: node/comp/vis) → daraus baue ich danach präzise Reader.
UNVERIFIZIERT: (1) heißen die Fenster wirklich JournalAccept/JournalResult?
(2) haben Annehmen/Abschließen eine JournalCanvas? Das [Accessibility] Addon:-Log
+ die [Quest]-Zeilen beantworten beides beim nächsten Test.

### Beim nächsten Start testen (V4.51)
1. "Version 4 Punkt 51 bereit".
2. QUEST ANNEHMEN: NPC mit Quest ansprechen → wird die Beschreibung automatisch
   vorgelesen? [Quest]- und [Accessibility] Addon:-Logzeilen mitschicken.
3. JOURNAL (Taste J): Quest auswählen → kommt automatisch Titel/Ziel/Beschreibung?
4. QUEST ABSCHLIESSEN: Abgabe-/Belohnungsfenster → wird der Abschlusstext gelesen?
5. Wenn eine Situation STUMM bleibt: die [Accessibility] Addon:-Zeile aus dem Log
   schicken (zeigt den echten Fenster-Namen) — dann ziehe ich den Handler nach.

---

## STAND V4.50 (2026-07-12)

### Neu in V4.50: Eigene Gil-Taste (Umschalt+F3)
User will NICHT Strg+F3 nutzen (liest alles vor), sondern Gil separat abfragen.
Neue Taste **Umschalt+F3** = reine Gil-Ansage (InventoryService.AnnounceGil,
nur "Gil: N", kein Inventar-Vorlesen). Umschalt+F1..F12 laut Keybind-Dump
(2026-07-10, game-api.md) frei. Config-Feld KeyReadGil + Konflikt-Check.
Antwort auf User-Frage "stehen Gil im Inventar": Ja, das Inventar-FENSTER zeigt
den Gil-Betrag unten an — wir lesen ihn aber direkt über die API (kein Fenster
nötig).

### Beim nächsten Start testen (V4.50)
1. "Version 4 Punkt 50 bereit".
2. GIL-TASTE: Umschalt+F3 → wird NUR der Gil-Stand angesagt (nicht das ganze
   Inventar)? Stimmt die Zahl? [Inventory] Currency-Logzeile mitschicken.
3. STÜCKZAHL (aus V4.49): über gestapelte Items navigieren → "N mal Name"?

### Neu in V4.49: Stückzahl vor Item-Name + Gil in der Inventar-Ansage
User-Feedback zu V4.48: Item-Namen werden beim Navigieren angesagt, aber die
STÜCKZAHL fehlte davor; außerdem 2 Fragen (Kategorien? Gil?).
(1) STÜCKZAHL: Beim Navigieren über Item-Slots wird jetzt "10 mal Eichenholz"
gesagt (Menge vorangestellt). Quelle: AtkComponentIcon.QuantityText (Offset 256,
ilspycmd-verifiziert) — direkt gelesen statt per GetTextFromNodeTree (das verwirft
1-Zeichen-Strings → einstellige Mengen wären verloren). Nur bei sichtbarem
Mengen-Node + rein numerisch + ≠ "1" (Einzel-Items ohne Präfix). FindIconId →
FindSlotIcon refaktoriert (gibt jetzt AtkComponentIcon* zurück). [Focus]-Log zeigt
iconId/qty/name.
(2) GIL: Strg+F3 (Inventar vorlesen) nennt jetzt zuerst den Gil-Stand. Quelle:
IGameInventory Currency-Container (GameInventoryType.Currency=2000, ilspycmd),
Item-ID 1 = Gil; Menge ist int (deckt Gil-Cap 999.999.999). Label über Item-Sheet
Zeile 1 (spielseitig "Gil", nicht hartkodiert). [Inventory] Currency-Logzeile.
KATEGORIEN-FRAGE beantwortet: Die Tasche (Inventory1-4) ist NICHT thematisch
sortiert (nur 4×35 Slots). Getrennt hält das Spiel: Schlüsselgegenstände, Währung
(Gil/…), Kristalle, Rüstkammer (nach Ausrüstungs-Slot). Wir lesen aktuell
Schlüsselgegenstände + Tasche + jetzt Gil; Kristalle/Rüstkammer könnten wir bei
Bedarf ergänzen.

### Beim nächsten Start testen (V4.49)
1. "Version 4 Punkt 49 bereit".
2. INVENTAR (I): über gestapelte Items navigieren → kommt "N mal Name"
   (z.B. "5 mal Antidot")? Einzel-Items ohne Zahl? [Focus]-Logzeilen mitschicken.
3. GIL: Strg+F3 → wird der Gil-Stand zuerst angesagt und stimmt die Zahl?
   [Inventory] Currency-Logzeile mitschicken.

---

## STAND V4.48 (2026-07-12, getestet: Item-Name ✓, Stückzahl fehlte → V4.49)

### Neu in V4.48: Fix Item-Auto-Ansage + NPC-Rolle/Quest-Marker
(1) BUG aus V4.47-Log (00:50): belegte Inventar-Slots sagten die STÜCKZAHL
("10") statt des Namens — GetTextFromNodeTree fand die Mengen-Textzeile des
Slots, und die Icon-Auflösung lief nur bei LEEREM Text. FIX: In
UpdateGlobalFocus hat die Icon->Name-Auflösung jetzt VORRANG vor dem rohen
Text (Item-Name schlägt Menge); Auflösung nur bei Node-Wechsel, gecacht in
_lastFocusedItemName. ResolveFocusedItemName präzisiert: stoppt an der ersten
Steuerungs-Komponente (kein Weiterklettern ins Addon → Buttons werden nicht
fälschlich als Item gelesen). [Focus] Item-Slot iconId/name loggt jeden Treffer.
(2) NPC-INFO (User-Wunsch): NavigationService.DescribeNpc hängt an die
Zielansage + Objekt-Browser-Ansage die ROLLE (ENpcResident.Title, z.B.
"Marktverwalter") und einen QUEST-MARKER an (GameObject.NamePlateIconId ->
DescribeQuestMarker: 71001-06 "Quest verfügbar", 71021-46 "Quest aktiv", sonst
71xxx "Quest"). NamePlateIconId wird geloggt zum Verfeinern der Ranges.
IDataManager in NavigationService injiziert; DataId->BaseId (Dalamud-Umbenennung).
OFFEN (in-game verifizieren): (1) werden Inventar-Items jetzt mit NAMEN
angesagt (nicht "10")? (2) stimmen NPC-Rolle + Quest-Marker? [Nav] NPC-Logzeilen
zeigen die rohen NamePlateIconIds → Ranges ggf. anpassen.

### Beim nächsten Start testen (V4.48)
1. "Version 4 Punkt 48 bereit".
2. INVENTAR (I): mit Tastatur/Controller über Items → wird jetzt der NAME
   angesagt (statt "10")? [Focus] Item-Slot-Logzeilen mitschicken.
3. NPCs anvisieren/mit N durchblättern → kommt Rolle ("Marktverwalter") +
   ggf. "Quest verfügbar"? [Nav] NPC-Logzeilen (NamePlateIconId) mitschicken.

### Neu in V4.47: AUTO-ANSAGE von Item-Slots beim Navigieren (kein Strg+F3 mehr)
User-Wunsch: Items sollen beim Navigieren automatisch vorgelesen werden; bei
Questbelohnungen muss man auswählen. Log 22:56/23:11 BESTÄTIGT: HandIn-Icon->
Name funktioniert ("Hausgemachte Aalpastete", "Proviantpaket", "Metallbeschlag"
inkl. KeyItems). Belohnungsauswahl = Addons `JournalResult` (Abschließen/
Ablehnen) + `JournalRewardItem` ("Wähle die Vergütung.", Icon-Slots Comp(1006)
->Icon(15)). DURCHBRUCH aus [Focus]-Log: der globale FocusedNode WANDERT beim
Navigieren der Item-Slots (Reward: wechselt id=4 'Abschließen' <-> id=3 ''
=Slots; HandIn: id=4 ''=Ablage-Slots) — Text leer, weil Name nicht in der UI.
LÖSUNG: UpdateGlobalFocus ruft bei leerem Text ResolveFocusedItemName() — klettert
vom Fokus-Node bis 4 Eltern hoch zur Slot-Komponente (FindIconId: Icon->IconId,
DragDrop->AtkComponentIcon.IconId, sonst Icon-Kind der Multipurpose-Hülle) und
löst per InventoryService.ResolveIconName auf. Deckt Inventar + Ablieferung +
Belohnung mit EINEM Mechanismus ab, nur bei Node-WECHSEL (nicht pro Frame).
WICHTIG: Belohnungs-Items besitzt man noch nicht → ResolveIconName fällt aufs
volle Item/EventItem-Sheet zurück (Icon->Name-Cache, einmal gebaut), sonst
eigenes Inventar (kollisionsfrei). Buttons (Übergeben/Abschließen) werden weiter
vom Fokus-Text-Pfad angesagt.
OFFEN (in-game verifizieren): (1) klettert der Fokus-Node wirklich zur Slot-
Komponente (wie bei Buttons bewiesen)? (2) stimmen die Reward-Namen aus dem
Sheet-Fallback? [Focus] Item-Slot iconId/name-Logzeilen zeigen es.

### Beim nächsten Start testen (V4.47)
1. "Version 4 Punkt 47 bereit".
2. INVENTAR (I) öffnen, mit Tastatur/Controller über Items navigieren → wird
   jeder Gegenstand automatisch angesagt (ohne Strg+F3)?
3. NPC-ABLIEFERUNG: über die Slots navigieren → Item-Namen automatisch?
4. QUESTBELOHNUNG (JournalRewardItem): über die Belohnungen navigieren → Namen
   angesagt? Eine auswählen + Abschließen.
5. [Focus]-Logzeilen mitschicken (zeigen iconId+name pro Slot).

### Neu in V4.46: NPC-Ablieferung "GEGENSTAND ABLIEFERN" (Request)
User-Ablauf geklärt (Log+Dump 22:40): Parsemontret-Quest → Talk → `Request`-
Fenster ("GEGENSTAND ABLIEFERN", Nodes: Window-Titel id=3, Button id=14
"Übergeben"/id=15 "Abbrechen", DragDrop-Ablage-Slots) + Geschwister-Addon
`InventoryEventGrid` (35 DragDrop-Slots, aufs infrage kommende Item gefiltert
— im Dump nur 1 Slot mit sichtbarem Icon). WICHTIGER BEFUND: Item-NAMEN
stehen NICHT in der UI (Slots haben nur Icon + leere Mengen-Textzeile id=8/id=7
"") → Name nur per Tooltip. LÖSUNG (ilspycmd-verifiziert): pro DragDrop-Slot
AtkComponentIcon.IconId (uint) lesen und gegen eine Icon→Name-Tabelle auflösen,
die InventoryService.BuildIconNameMap() aus dem EIGENEN Inventar baut (Item-
Sheet Icon@136 / EventItem Icon@24). Node-Zugriff wie im Restcode
(Type>=1000 → AtkComponentNode->Component → ComponentType.DragDrop →
AtkComponentDragDrop->AtkComponentIcon->IconId).
- OnRequestOpen sagt beim Öffnen: "Gegenstand abliefern. Drücke Strg F3 …".
- Strg+F3 ist jetzt kontextabhängig: bei offenem Request → UIReaderService.
  TryAnnounceHandOver() liest die passenden Items aus InventoryEventGrid
  ("Ein passender Gegenstand: X. Auswählen und Übergeben."); sonst normales
  Inventar-Vorlesen (V4.45). Manuell getriggert, weil das Grid erst ein paar
  Frames nach Request-PostSetup gefüllt ist.
- Jeder Slot als [HandIn]-Logzeile (node/iconId/name = Ground Truth, falls
  Icon→Name mal danebenliegt).
OFFEN (noch nicht in-game verifiziert): (1) stimmt die Icon→Name-Auflösung?
(2) die eigentliche AUSWAHL des Items im Grid (blind auf den Slot kommen +
platzieren) ist noch NICHT gelöst — evtl. brauchen wir Grid-Slot-Navigation
per FocusedNode. Die Request-Buttons (Übergeben/Abbrechen) werden schon vom
bestehenden Fokus-Leser angesagt (Log 22:40 bestätigt).

### Beim nächsten Start testen (V4.46)
1. "Version 4 Punkt 46 bereit".
2. Parsemontret ansprechen → kommt "Gegenstand abliefern …"?
3. Strg+F3 drücken → wird der/die passende(n) Gegenstand/Gegenstände mit
   Namen angesagt? [HandIn]- und [Inventory]-Logzeilen mitschicken.
4. Item auswählen + Übergeben: schaffst du die Auswahl (Maus/Controller)?
   Falls nicht: sagen wie du navigierst (Pfeiltasten? Controller?), dann baue
   ich die Grid-Slot-Ansage.

### Neu in V4.45: Inventar vorlesen (Strg+F3)
User braucht fürs Quest-Abschließen einen Gegenstand aus dem Inventar. Neuer
InventoryService liest das Inventar via Dalamud IGameInventory (KEIN UI-Scraping,
funktioniert auch bei geschlossenem Beutel). Strg+F3 sagt: erst
Schlüsselgegenstände (KeyItems-Container, quest-relevant), dann Tasche
(Inventory1-4). Namen: normale Items über Lumina Item-Sheet (BaseItemId, HQ/
Collectible-Offset via GameInventoryItem.BaseItemId), Schlüsselgegenstände über
EventItem-Sheet. Jeder Gegenstand als [Inventory]-Logzeile (Ground Truth: id/
qty/name — zeigt v.a. ob KeyItem->EventItem-Mapping stimmt). Alles ilspycmd-
verifiziert: IGameInventory.GetInventoryItems(GameInventoryType) ->
ReadOnlySpan<GameInventoryItem>. Stapel als "Name mal Anzahl".
OFFEN: Falls die Quest verlangt, den Gegenstand IN DER UI auszuwählen/zu
benutzen (Rechtsklick->Benutzen, Übergabe-Fenster), reicht Vorlesen nicht →
dann Inventar-Addon dumpen (Strg+F5 bei offenem Beutel) und Grid-Navigation
bauen. Erst v1 (Vorlesen) testen.

### Beim nächsten Start testen (V4.45)
1. "Version 4 Punkt 45 bereit".
2. Strg+F3: werden Schlüsselgegenstände + Taschen-Inhalt vorgelesen? Ist der
   Quest-Gegenstand dabei? [Inventory]-Logzeilen mitschicken.
3. Falls die Quest den Gegenstand in der UI benutzen will: sagen, was genau
   die Quest verlangt (Beutel öffnen + benutzen? Übergabe an NPC?) → dann
   bauen wir die passende Navigation.

### PARKIERT: Auto-Lauf-Übergang "Tiefer Wald" (V4.44-Diagnose ausgewertet)
[NavDiag]-Log (21:38) ist EINDEUTIG: Unser Zielpunkt ist KORREKT — vnavmesh
findet einen vollständigen Weg, letzter Wegpunkt liegt exakt auf dem Ziel
(letzter->Ziel=0,0 m). ABER der Charakter bewegt sich KEINEN Meter (pos 5 s
lang eingefroren bei 152,5|165,0 trotz running=True); letzter Wegabschnitt ist
ein einzelner ~18-m-Sprung geradeaus. Ursache: vnavmesh-Netz stimmt an DIESEM
Ausgang nicht mit der echten Spiel-Kollision überein, Char verkeilt sich.
NICHT bei uns fixbar (Netz pro Zone im vnavmesh-Code, kein Config-Wert).
User BESTÄTIGT: andere NPCs und Übergänge funktionieren einwandfrei → es ist
EINE einzelne kaputte Stelle (vnavmesh-Netz-Bug an diesem Ausgang), kein
allgemeines Problem. Optionaler künftiger Hebel auf UNSERER Seite: sanfter
Notausstieg (bei Übergangs-Stau auf Gehhilfe/Beacon umschalten) ODER die
Stelle vnavmesh-Autor melden. NavDiag-Logs sind noch drin (bewusst).

### Neu in V4.44: Diagnose des Auto-Lauf-Feststeckens am Zonen-Übergang
Befund V4.43 (Log 21:12): Auto-Lauf zum Übergang "Tiefer Wald" jammt 18 m vor
dem Ziel fest — vnavmesh meldet einen gültigen Pfad + running=True, der
Charakter kriecht aber nur ~1 m und die Stillstand-Erkennung stoppt korrekt
("Komme nicht näher, noch 18 Meter"). Offen: liegt unser Zielpunkt (Karten-
Symbol-Position) HINTER einem Hindernis, oder hat das Netz dort eine LÜCKE?
Geklärt: vnavmesh-Mesh-Settings (AgentRadius usw.) sind pro Zone im
Plugin-Code FEST verdrahtet (NavmeshCustomization), NICHT per Config änderbar
— nur Neubau von vnavmesh würde sie ändern (Fremdcode, nicht gemacht).
Einzige Config-Änderung: vnavmesh.json BuildMaxCores 1->0 (alle CPU-Kerne,
schnellerer Mesh-Bau neuer Zonen; Backup vnavmesh.json.bak-vor-buildcores).
DIAGNOSE-BUILD: AutoWalkService loggt jetzt via IPC Path.ListWaypoints den
tatsächlich verfolgten Pfad ([NavDiag]): einmal die volle Wegpunkt-Route +
Abstand des letzten Wegpunkts zum Ziel; jede Sekunde Live-Position +
Rest-Wegpunkte + Abstand zum nächsten Wegpunkt. Auswertung:
- letzter Wegpunkt NAH am Ziel (< ~2 m) → Ziel erreichbar, Char jammt an
  echter Kollision (Netz sagt begehbar, Spiel blockiert).
- letzter Wegpunkt WEIT vom Ziel → Netz-Route endet vorher (Lücke / falscher
  Zielpunkt) → Fix in UNSEREM Code (Ziel auf letzten erreichbaren Netzpunkt).

### Beim nächsten Start testen (V4.44)
1. "Version 4 Punkt 44 bereit".
2. Denselben Übergang ansteuern: Quest-Ziel/Wegpunkt "Übergang nach Tiefer
   Wald" mit N wählen → Numpad 3. Feststecken abwarten ("Komme nicht näher").
3. Log an Claude schicken — die [NavDiag]-Zeilen beantworten: Hindernis vor
   dem Ziel oder Netz-Lücke?

---

## Historie: V4.43 (2026-07-11)

### Neu in V4.43: Auto-Lauf-Rückmeldung + garantierte Termination
User: „läuft immer noch nicht richtig ran". LOG-BEFUND (21:00): In der GANZEN
Historie feuert NIE eine Ankunft — Lauf lief 48 s (190 m → 18 m), dann
manuell gestoppt. Ursachen: (1) beim Laufen KEINE gesprochene Rückmeldung,
nur Beacon-Ton → User unsicher, ob es arbeitet, bricht ab; (2) bei einem
Übergang meldet vnavmesh nie Ankunft (Ziel liegt jenseits der Zonenlinie).
FIX (AutoWalkService):
- Gesprochener Fortschritt alle 3 s („Noch 120 Meter").
- ZONENWECHSEL = Erfolg: TerritoryType ändert sich → „Angekommen, neues
  Gebiet erreicht" + Stopp (echtes Ankunftssignal beim Übergang).
- STILLSTAND-ABBRUCH: kein Fortschritt (>1 m) seit 5 s → Stopp mit „Komme
  nicht näher, noch X Meter" statt ewig weiterzulaufen.
- Diagnose-Log alle 3 s: dist/running/computing (klärt beim nächsten Test,
  ob es zügig hinläuft oder an der Zonenkante hängt).
IClientState in AutoWalkService injiziert.

### Beim nächsten Start testen (V4.43)
1. „Version 4 Punkt 43 bereit".
2. AUTO-LAUF: N (Ziel/Wegpunkt) → Numpad 3: hörst du jetzt alle 3 s „Noch X
   Meter"? Läuft die Zahl runter? Kommt am Ende „Ziel erreicht"?
3. ÜBERGANG: Quest anderes Gebiet / Übergangs-Wegpunkt → Numpad 3: läuft er
   hin, und kommt beim Zonenwechsel „Angekommen, neues Gebiet erreicht"?
   Falls er hängt: „Komme nicht näher, noch X Meter" (dann sagt mir X — das
   zeigt, ob der Übergangs-Marker falsch positioniert ist).

---

## V4.42 (2026-07-11)

### Neu in V4.42: Dialog liest wieder die FRAGE vor (User-Meldung)
Problem: Bestätigungsfenster (SelectYesno) sagten nur noch „Ok"/„Abbrechen",
nicht mehr die Frage — man wusste nicht, was man bestätigt. ROOT CAUSE (Log
19:52 + 20:05): DREI Ansager, alle SpeakInterrupt: OnYesNoOpen liest Frage+
Buttons, aber OnDialogButtonProbe UND UpdateGlobalFocus ([Focus]) sagen den
fokussierten Button ~6 ms später an → schneiden die Frage ab. [Focus] liest
die echte Spiel-Fokus-Node (brauchen wir für Links/Rechts), darf nicht weg.
FIX: Schutzfenster von 1 s nach Dialog-Öffnung (InDialogOpenGuard) — beide
Button-Ansager führen ihren Status weiter mit, sprechen aber nicht; Navigation
nach dem Fenster wird normal angesagt. Gilt für SelectYesno + SelectString.

### Beim nächsten Start testen (V4.42)
1. „Version 4 Punkt 42 bereit".
2. DIALOG: ein Bestätigungsfenster öffnen (z. B. Ausloggen, Quest annehmen/
   ablehnen): kommt jetzt die ganze FRAGE + „Ok oder Abbrechen"? Dann
   Links/Rechts: sagt er die neue Auswahl an?

---

## V4.41 (2026-07-11)

### Log-Auswertung V4.40 (Log 19:48-19:52) — was funktioniert / Bugs
FUNKTIONIERT: ✓ Hotbar-Vorlesen Strg+F9 (alle Slots sauber: Gewaltiger
Schuss, Direkter Schuss, Teleport, Sprint …); ✓ Ok/Abbrechen-Dialog
Links/Rechts + Ansage („Das Spiel beenden? Ok oder Abbrechen …") — der
alte Dauerbrenner geht endlich!; ✓ neue Übergangs-Stopp-Distanz 0,5 greift.
NICHT getestet: Kampf (User war in der Stadt, kein Gegner anvisiert).
2 BUGS gefunden + in V4.41 gefixt:

### Neu in V4.41: zwei Fixes aus der Log-Auswertung
1. CAST-BALKEN-SPAM: der Text-Scanner las den EIGENEN Zauber-Countdown
   (_CastBar id=7: „00.63"…„00.02") jeden Frame vor (beim Teleportieren).
   Fix: _CastBar in HudNoiseAddons. (Eigene Casts später sauber via
   LocalPlayer.IsCasting, nicht per Text-Scan.)
2. ÜBERGANG LANDETE IM TIEFGESCHOSS: PointOnFloor castet nach UNTEN → vom
   Steg (Y=-12,9) auf den Boden 37 m tiefer (-50,5); 18-m-Übergang wurde
   40-m-Lauf in die falsche Etage (User brach 2× ab). Fix: ResolveFloorPoint
   nutzt jetzt vnavmesh NearestPoint(10,10) (begrenzte Box um die Höhe),
   PointOnFloor nur noch Fallback. (game-api.md → vnavmesh-FALLE.)

### Beim nächsten Start testen (V4.41)
1. Ansage „Version 4 Punkt 41 bereit".
2. TELEPORT/RÜCKFÜHRUNG wirken: KEIN „00.30 00.28…"-Countdown-Spam mehr?
3. ÜBERGANG: Quest in anderem Gebiet / Übergangs-Wegpunkt → Numpad 3:
   läuft er jetzt die ~18 m auf gleicher Höhe zur Zonenlinie (nicht 40 m
   nach unten)? Log: „[Orte] NearestPoint …" statt „-50,5". Wechselt Gebiet?
4. KAMPF (noch offen aus V4.39): Gegner anvisieren → Ziel-Ton + HP? Angriff
   → „Ziel HP …"? „Gegner wirkt …"? Strg+F12 mit Ziel-HP?

### Neu in V4.40: Auto-Lauf kommt dichter ans Ziel + Übergänge auslösen
User: „läuft nicht immer ganz zu gewissen Orten; bei Übergängen soll er
gleich ins Gebiet rein." ROOT CAUSE: vnavmesh `PathfindAndMoveCloseTo`
hält absichtlich `range` Meter VOR dem Ziel; range war überall 2,5 m
(Interaktionsdistanz). Zusätzlich wurden Questziele nicht aufs Netz
eingerastet (nur Wegpunkte) → bei off-mesh-Markern stoppte er weit vorher.
1. Getrennte Stopp-Distanzen (Config): NPCs bleiben 2,5 m; Orte, Wegpunkte
   und Questziele 1 m (AutoWalkPlaceStopRange); Zonen-Übergänge 0,5 m
   (AutoWalkTransitionStopRange), damit man auf die Zonenlinie läuft und
   der Übergang auslöst.
2. Questziele werden jetzt vor dem Laufen per vnavmesh PointOnFloor aufs
   begehbare Netz eingerastet (Fallback: rohe Marker-Position), damit die
   enge Stopp-Distanz überhaupt erreichbar ist.
3. ToggleToPosition nimmt jetzt die fertige Stopp-Distanz statt eines
   Radius; der Aufrufer (Plugin.cs) wählt sie nach Kontext.

### SORTIERUNG: schon vollständig da (User-Frage geklärt)
User bat, „nicht nur Quests, alle Kategorien nach Entfernung zu sortieren".
Prüfung: ALLE Kategorien sortieren bereits nach Entfernung, nächstes zuerst
— Objekt-Kategorien (GetCategoryObjects, OrderBy Distance), Wegpunkte
(OrderBy Distance2D) und Quests (V4.38). Nichts zu ändern. Falls im Spiel
eine Kategorie doch unsortiert wirkt: konkretes Beispiel (welche Kategorie,
gehörte Reihenfolge) → dann echte Ursache suchen.

### Beim nächsten Start testen (V4.40)
1. Ansage „Version 4 Punkt 40 bereit".
2. AUTO-LAUF GENAUIGKEIT: N (Wegpunkt/Questziel) → Numpad 3: kommt er jetzt
   dichter ran (~1 m statt ~2,5 m)? „Ziel erreicht" statt „beendet, noch X"?
3. ÜBERGANG: Quest in anderem Gebiet oder Wegpunkt-Übergang → Numpad 3:
   läuft er auf die Zonenlinie und wechselt das Gebiet von selbst?
4. Sortierung stichprobenartig: NPCs/Objekte mit N durchblättern — nächstes
   zuerst?

### Ältester offener V4.39-Testblock (Kampf) siehe unten — mittesten:

### V4.39: Kampf-Wahrnehmung (Gegner-HP, Cast, Ziel-Ton, Hotbar)
- Gegner anvisieren (Tab/F11): Ziel-Ton + HP-Ansage? Kampf: „Ziel HP …",
  „Gegner wirkt …", Strg+F12 mit Ziel-HP? Strg+F9: Aktionsleiste vorgelesen?

### V4.38 (mit V4.39 ungetestet): Entfernungssortierung + Annehmbare Quests

### Neu in V4.39: Kampf-Wahrnehmung (barrierefrei kämpfen)
User-Wunsch: „bau alles damit Kämpfen barrierefrei wird, plus ein Ton wenn
ein Gegner anvisiert ist." Alle Structs ilspycmd-verifiziert (game-api.md
→ „Kampf"). Umgesetzt in diesem Batch:
1. GEGNER-HP: Beim Zielwechsel wird die HP des Ziels mit angesagt („Ziel:
   Name, Kampf-NPC, 12 Meter, geradeaus, HP 100 Prozent"). Im Kampf sagt
   das Plugin die HP des aktuellen Ziels in Stufen (75/50/25/10 %) an, damit
   man hört, ob der Angriff wirkt. Strg+F12 nennt jetzt auch die Ziel-HP.
   (CombatService + NavigationService.DescribeTargetHp.)
2. GEGNER-CAST: Wirkt das Ziel eine Aktion, kommt „Gegner wirkt <Name>"
   (Aktionsname aus Lumina Action-Sheet, einmal pro Cast). Vorwarnung für
   große Angriffe. (CombatService.UpdateTarget.)
3. ZIEL-TON: Kurzer 2-Ton-Piepser (steigend, 990→1320 Hz), sobald ein
   GEGNER (BattleNpc) anvisiert wird — auch bei Auswahl per N. Neuer
   CueService (eigener NAudio-Einzelton, unabhängig vom Gehhilfe-Beacon).
   Config: EnableTargetTone, TargetToneVolume=0.4.
4. HOTBAR VORLESEN: Strg+F9 liest Aktionsleiste 1 vor („Taste 1, Vollschlag.
   Taste 2, …"). FF14 hat keinen Angriff-Knopf — man zielt und drückt die
   Zahlentasten 1–0 (= Hotbar-1-Slots). Neuer HotbarService liest
   RaptureHotbarModule, Namen via Lumina Action + PopUpHelp-Fallback.
   Jeder Slot wird als [Hotbar]-Zeile geloggt (Ground Truth).

### Beim nächsten Start testen (V4.39)
1. Ansage „Version 4 Punkt 39 bereit".
2. GEGNER ANVISIEREN (Tab oder F11 oder N-Kategorie „Gegner"): kommt der
   kurze Ziel-Ton? Wird die Gegner-HP mit angesagt?
3. KAMPF: einen schwachen Gegner angreifen (Zahlentasten): sinkt-Ansagen
   „Ziel HP 75/50/25/10 Prozent"? Sagt „Gegner wirkt …" wenn er zaubert?
   Strg+F12 → eigene HP + Ziel-HP?
4. HOTBAR: Strg+F9 → werden die Aktionsnamen der Tasten 1–0 vorgelesen?
   (Log-Zeilen [Hotbar] bitte mitschicken, dann sehe ich rohe type/id/name.)
5. Noch aus V4.38 mittesten: Sortierung nach Entfernung, Kategorie
   „Annehmbare Quests" (Strg+N).

### NÄCHSTER BATCH (V4.40, noch NICHT gebaut) — bewusst verschoben
Diese zwei brauchen eigene Verifikation und kommen als Nächstes:
- Cooldown/GCD-Feedback („bereit"): ActionManager (GetRecastTime/
  IsRecastActive) + HotbarSlot.IsSlotUsable — noch nicht verifiziert.
- Aktions-Fehler („außer Reichweite", „nicht genug MP"): _TextError wird
  schon gelesen (nie in-game bestätigt) bzw. UseAction-Hook — in-game prüfen.

---

### V4.38: Entfernungssortierung + Kategorie „Annehmbare Quests"
User-Wünsche: (1) alles nach Entfernung sortiert, damit klar ist was am
nächsten ist; (2) Kategorie für noch nicht angenommene Quests im Gebiet.
1. SORTIERUNG: Quest-Ziele werden jetzt durchgängig nach Lauf-Entfernung
   sortiert. Im Gebiet nach Luftlinie; in Fremdzonen nach Distanz zum
   ersten Übergang (das ist, wohin man tatsächlich läuft) — „am nächsten"
   bleibt so über Zonengrenzen sinnvoll. In-Gebiet-Ziele kommen weiter
   zuerst. (NavigationService.EffectiveWalkDistance.)
2. NEUE KATEGORIE „Annehmbare Quests" (Strg+N): liest
   Map.UnacceptedQuestMarkers (StdList<MarkerInfo>, ilspycmd-verifiziert)
   → annehmbare Quests in der Nähe mit Name, Entfernung, Richtung. Numpad 3
   läuft hin (nutzt dieselbe SelectedQuestDestination-Pipeline inkl.
   Zonen-Routing, kein Plugin.cs-Eingriff nötig).
   QuestMarkerService.GetUnacceptedDestinations + geteilter Marker-Reader
   AddMarkerDestinations.

### Beim nächsten Start testen (V4.38)
1. Ansage „Version 4 Punkt 38 bereit".
2. ANNEHMBARE QUESTS: Strg+N bis „Kategorie Annehmbare Quests: X im
   Gebiet" → N mehrfach: werden annehmbare Quests mit Entfernung/Richtung
   angesagt? → Numpad 3: läuft er zum Quest-Geber? Dort Quest annehmen.
3. SORTIERUNG: Mehrere Quests aktiv → Quest-Ziele durchblättern: kommt das
   nächstgelegene zuerst? Fremdzonen-Quests nach Übergangs-Nähe sortiert?
4. Weiter offen aus V4.37 (unverändert):

### Neu in V4.37: Zonen-Routing für Quests + Beschreibungs-Vorarbeit
User-Fragen: (1) Quest in anderem Gebiet — wie erfährt ein Blinder, WO
er hinlaufen muss? (2) Quests werden angesagt, aber ohne Beschreibung.
1. ZONEN-ROUTING: PlacesService baut aus den MapMarker-Übergängen einen
   statischen Karten-Graphen (FindFirstHopToMap: Breitensuche, gecacht).
   QuestDestination trägt jetzt MapId.
   - Quest-Ansage bei fremdem Gebiet: „…, im Gebiet Alt-Gridania.
     Dorthin über Übergang nach Alt-Gridania, 150 Meter, links[, danach
     noch 2 weitere Übergänge]. Nummernblock 3 läuft zum Übergang."
   - Numpad 3 läuft bei Fremd-Gebiet-Quest jetzt ZUM ÜBERGANG (mit
     PointOnFloor-Höhe) statt „dorthin kann ich nicht laufen".
   - Kein Weg im Graphen (nur Fähre/Teleport erreichbar) → klare Ansage.
2. Quest-Ansage hängt Marker-Tooltip an, wenn er mehr sagt als der Name.
3. [Probe] _ToDoList: bei jedem Kategorie-Wechsel (Strg+N) loggt das
   Plugin die sichtbaren Texte des Quest-TRACKERS (Node-Ids). Der Tracker
   zeigt das AKTUELLE ZIEL jeder Quest — perfekte Quelle für „Beschreibung
   ansagen", aber Struktur unbekannt → erst Probe, dann Reader (V4.38).

### Beim nächsten Start testen (V4.37)
1. Ansage „Version 4 Punkt 37 bereit".
2. QUEST IN ANDEREM GEBIET: Kategorie Quest-Ziele → N: sagt er Gebiet +
   Übergang an? → Numpad 3: läuft er zum Übergang? Durch den Übergang
   gehen (am Ende selbst durchlaufen), drüben neu ansagen lassen.
3. Strg+N einmal durchschalten (erzeugt [Probe]-Zeilen vom Quest-Tracker
   — die brauche ich für die Beschreibungs-Ansage).
4. Weiter offen: Links/Rechts-Dialog ([Focus]/[Key]), Journal-Zeilen,
   Optionen, Ätheryt-Distanz-Vergleich (Formel-Check), unentdeckte Orte?

---

## V4.36 (2026-07-11 Nachmittag — BESTÄTIGT: Wegpunkte lesen ✓)

### Neu in V4.36: Kategorie „Wegpunkte" (User-Wunsch: Ziele außer Reichweite)
Problem: Quests/NPCs außerhalb der 100m-Reichweite bzw. in anderen Zonen —
es fehlten anlaufbare Zwischenziele (Ausgänge, Ätheryten, Orte).
1. PlacesService (neu): liest die STATISCHEN Karten-Symbole der aktuellen
   Karte aus dem Lumina-Sheet „MapMarker" (Zeilen via Map.MapMarkerRange):
   - DataType 1/2 = ZONEN-ÜBERGANG (DataKey = Ziel-Karte → „Übergang
     nach Alt-Gridania")
   - DataType 3 = Ätheryt (Name aus Aetheryte-Zeile), 4 = Aethernet
   - sonst benannte Orte (PlaceNameSubtext: Gilden, Marktbrett …)
   Pixel→Welt-Formel aus Dalamuds MapUtil hergeleitet (dekompiliert,
   Konsistenz geprüft): welt = (pixel−1024)×100÷SizeFactor − Offset.
2. Objekt-Browser: neue Kategorie „Wegpunkte" (Strg+N), N/Umschalt+N
   blättert nach 2D-Distanz: „3 von 12: Übergang nach Alt-Gridania,
   Übergang, 150 Meter, links." ([Orte]-Logzeilen als Probe).
3. Numpad 3 läuft hin: Kartendaten sind 2D (keine Höhe!) → vorher
   vnavmesh-Query `Query.Mesh.PointOnFloor` (IPC dekompiliert-verifiziert,
   vnavmesh nutzt sie selbst für „FlagToPoint") ermittelt den begehbaren
   Punkt; Fehlschlag → klare Ansage „Kein begehbarer Punkt gefunden".
4. csproj referenziert jetzt Lumina + Lumina.Excel (aus DALAMUD_HOME).
OFFENE LAUFZEITFRAGEN (Log zeigt es): (a) stimmt die Pixel→Welt-Formel
in der Praxis? Ätheryt-Wegpunkt vs. Ätheryt-Objekt (Kategorie Ätheryten)
müssen ~gleiche Distanz melden; (b) MapMarkerUnlockedBit ignorieren wir —
evtl. tauchen unentdeckte Orte auf (Fog-of-War-Frage, ggf. filtern).

### Beim nächsten Start testen (V4.36, inkl. V4.34/V4.35-Punkte)
1. Ansage „Version 4 Punkt 36 bereit".
2. WEGPUNKTE: Strg+N bis „Kategorie Wegpunkte: X im Gebiet, davon Y
   Übergänge" → N mehrfach: Übergänge/Ätheryten/Orte mit Distanz?
   Vergleich: Ätheryt in Kategorie Ätheryten vs. Wegpunkte — ähnliche
   Distanz? → Numpad 3: läuft er zum Übergang? Ansage bei Ankunft?
3. Ok/Abbrechen-Dialog: Links/Rechts → Ansage? ([Focus]/[Key]-Probe)
4. Journal (J): Pfeiltasten → Quest-Zeilen?
5. Optionen: bedienbar? Einloggen/Laufen: Ruhe?

---

## V4.35 (2026-07-11 Mittag, im selben Build)

### Neu in V4.35: Globaler Fokus-Melder + Pfeiltasten-Sonde
User-Meldung: Links/Rechts in Ok/Abbrechen-Dialogen wurde SCHON ÖFTER
gedrückt — weder Navigate-Log noch BtnProbe-Flags registrierten es.
Konsequenz: nicht weiter an Addon-Flags raten, sondern die QUELLE lesen:
1. UpdateGlobalFocus (jeden Frame): liest
   AtkStage.Instance()->AtkInputManager->FocusedNode (@6272, ilspycmd) —
   den Node, den DAS SPIEL für tastatur-fokussiert hält. Bei Wechsel:
   [Focus]-Log + Ansage (Text via GetTextFromNodeTree, klettert bis 3
   Eltern hoch, weil Fokus oft auf dem Collision-Kind sitzt).
   Deckt potenziell ALLE Fenster ab (Dialoge, Optionen, …).
   Identische Doppel-Ansagen (z. B. parallel zur Listen-Navigation)
   fängt der 0,5s-Debounce; gemeldete Dopplungen dann gezielt abdrehen.
2. [Key]-Sonde: loggt jede erkannte Pfeiltaste, solange ein Menü/Dialog
   aktiv ist → klärt endgültig, ob IKeyState Pfeiltasten überhaupt
   SIEHT, wenn ein Dialog offen ist (Spiel könnte sie verschlucken).

### Beim nächsten Start testen (V4.35, inkl. V4.34-Punkte)
1. Ansage „Version 4 Punkt 35 bereit".
2. Ok/Abbrechen-Dialog: Links/Rechts → wird der Knopf jetzt angesagt?
   (Danach zeigen [Focus]/[Key]-Zeilen im Log, was das Spiel tut.)
3. JOURNAL (J): Pfeiltasten → Quest-Zeilen angesagt? (V4.34-Fix)
4. Optionen: bedienbar ohne „Dump"-Ansagen? Pfeiltasten → Ansagen?
5. Einloggen/Umherlaufen: Ruhe? (kein „INVENTAR", kein Nameplate-Geplapper)
   Falls dir dabei etwas FEHLT (z. B. Chat-Meldungen), bitte melden.

---

## V4.34 (2026-07-11 Mittag, im selben Build)

### V4.33-Testauswertung (Log 10:33–10:42, dank [Speak]/[Scan] voll attribuierbar)
- ✓ Optionen öffnen sauber: „Systemeinstellungen. Anzeigeeinstellungen,
  Tab 8 von 8" — KEIN fps-Spam mehr.
- ✓ Kein Koordinaten-/Uhr-Spam beim Laufen (_NaviMap/_DTR-Ignore greift).
- ✓ SelectString-Navigation lief („Ja"/„Nein" bei Pfeiltasten).
- ✓ Talk-Sprecher-zuerst weiter bestätigt (id2-Pinning aktiv ab V4.33).
- ✗ Journal: Liste GEFUNDEN, Hov2 BEWEGT sich (Pfeiltasten!), aber
  Ansage blieb stumm. ROOT CAUSE: TreeList lässt Basis-ListLength auf 0
  (eigener Items-Vektor @432, ilspycmd) → ReadListItemText-Guard
  `idx >= ListLength` verwarf jede Zeile; Ansage war „Menü, 0 Einträge".
- ✗ [Scan]-Log überführte NEUE Spam-Quellen (alle wurden gesprochen!):
  NamePlate-„Fokus" (pendelte alle 2s: Bertennant/Ulta), _TargetInfo
  (doppelt die [Nav]-Ansage), ChatLogPanel_0 (jede Chat-Zeile),
  _MiniTalk (Sprechblasen), _ParameterWidget/_Exp (HP/XP-Ticks),
  _GetAction, JournalDetail (unsichtbare Buttons „Entfernen/Neuer
  Versuch/Karte"), _CharaSelectTitle.
- ✗ JournalDetail sprach beim Journal-Öffnen einen UNSICHTBAREN
  Fehlertext („Du kannst den Auftrag nicht annehmen …") — ReadAllTexts
  las auch versteckte Nodes.
- ✗ ConfigSystem: CS-DIAG-Dump feuerte bei JEDEM Tastendruck (1400
  Zeilen + Ansage „ConfigSystem Dump. 593 Nodes." — 40k Zeilen Flut,
  Options-Bedienung dadurch unbenutzbar).
- ✗ Beim Einloggen „INVENTAR" 3× u.ä.: PostSetup-Ansagen für
  UNSICHTBARE vorerzeugte Fenster + Menü-Stack-Müll (Stack-Tiefe 7).
- Links/Rechts in Ok/Abbrechen-Dialogen: wieder nicht gedrückt (keine
  Navigate-Zeilen); BtnProbe zeigt konstant kein Fokus-Bit auf Buttons.

### Neu in V4.34
1. Journal-Fix: ReadListItemText begrenzt auf AllocatedItemRendererList-
   Length statt ListLength; Eintragszahl via TreeList.Items.LongCount
   (GetListEntryCount) → Journal-Zeilen sollten jetzt sprechen.
2. HudNoiseAddons (erweitert, alles log-bewiesen): NamePlate, _TargetInfo*,
   ChatLog*, _MiniTalk, _ParameterWidget, _Exp, _GetAction, JournalDetail,
   _CharaSelectTitle + bisherige _NaviMap/_DTR. Gilt für Scanner UND
   Fokus-Pfad UND OnOpen. (Chat-Vorlesen später sauber via IChatGui.)
3. Sichtbarkeits-Gate in OnAnyAddonOpen: unsichtbare PostSetup-Addons
   (vorerzeugte Fenster beim Zone-in) werden nur geparkt (_noListCache);
   Ansage kommt erst, wenn sie sichtbar werden (Späte-Listen-Pfad).
4. ReadAllTexts + ScanAddonTexts lesen nur noch SICHTBARE Text-Nodes.
5. CS-DIAG-Dump (Options-Flut) entfernt.

### Beim nächsten Start testen (V4.34)
1. Ansage „Version 4 Punkt 34 bereit".
2. Einloggen: deutlich weniger Geplapper (kein „INVENTAR" 3×)?
3. JOURNAL (J): Pfeiltasten → wird jetzt jede Quest-Zeile angesagt?
4. Optionen (Escape → Systemkonfiguration): normal bedienbar, keine
   „Dump"-Ansagen? Was sagen Pfeiltasten/Tab an?
5. Umherlaufen/NPCs anschauen: Ruhe (kein Nameplate-/Chat-Geplapper)?
   Melde, falls dir dabei ETWAS FEHLT, das du vorher nützlich fandest
   (z. B. Chat-Zeilen) — das bauen wir dann als sauberes Feature.
6. Ok/Abbrechen-Dialog: bitte diesmal LINKS/RECHTS drücken (Probe wartet).

### Antwort auf User-Frage: „Orte" als Kategorie (Recherche 2026-07-11)
JA, machbar — Quellen (ilspycmd-verifiziert):
- AgentMap.EventMarkers (StdVector<MapMarkerData> @232) = dynamische
  Karten-Marker; AgentMap hat CurrentTerritoryId/MapId + SizeFactor/Offsets.
- Statische Karten-Symbole (Ätheryten, ZONEN-AUSGÄNGE, Marktbrett, Läden,
  Gilden): Lumina-Sheet „MapMarker" via IDataManager (X/Y pro Karte,
  IconId, PlaceName). ABER: nur 2D — Y-Höhe fehlt → für Numpad-3-Lauf
  vnavmesh-Query (PointOnFloor) nötig. Plan: neue Kategorie „Orte" im
  Objekt-Browser, gleiche Mechanik wie Quest-Ziele (Positions-Lauf).

---

## Historie: V4.33 (2026-07-11 Mittag)

### V4.32-Testauswertung (Log 10:12–10:17): ZWEI DURCHBRÜCHE + neue Bugs
- 🎉 SYSTEMMENU-NAVIGATION FUNKTIONIERT: alle 15 Einträge wurden beim
  Pfeiltasten-Navigieren angesagt (hoch UND runter, mit Umbruch).
  PROBE-ERGEBNIS: `HoveredItemIndex2` (@344) ist DAS Tastatur-Feld
  (ändert sich zuerst, HoveredItemIndex zieht 1 Frame nach; Sel bleibt -1;
  Enter setzt HeldItemIndex). Dokumentiert in docs/game-api.md.
- 🎉 TALK-SPRECHER ZUERST funktioniert („Capucine: He, du! …").
  PROBE: Name = Node id=2, Text = id=3 (jede Seite konsistent).
- ✗ NEU: ConfigSystem (Optionen, User-Dump 593 Nodes): fps-Zähler wurde
  als Tab-Überschrift gewählt → „59 fps, Tab 8 von 8" JEDE SEKUNDE.
  Root Cause: GetConfigSectionHeading sucht rückwärts, fps-Node (id=4)
  liegt am Ende der Node-Liste VOR der echten Überschrift (id=22
  „Anzeigeeinstellungen"); Volatile-Filter fehlte im Tab-Pfad.
  Zudem loggte „Tab fokussiert" jeden Frame (tausende Zeilen).
- ✗ NEU: Beim Auto-Lauf Spam durch generischen Text-Scanner:
  Koordinaten „X:12,4 Y:13,4", Serveruhr, vnavmesh-Status „Mesh: Ready |
  Moving" — Quelle plausibel _NaviMap + _DTR (Scanner loggte Quelle nicht).
- ✗ Kaputte Symbol-Glyphen in Ansagen („H(icon) Dalamud Plugins",
  Uhr-Icon vor Serverzeit) — FFXIV bettet Icons als Private-Use-Zeichen ein.
- Journal + Links/Rechts in Dialogen: diesmal NICHT getestet (fps-Spam
  dominierte). BtnProbe zeigt: im Ruhezustand trägt kein Dialog-Knopf
  das Fokus-Bit 0x100.

### Neu in V4.33
1. ConfigSystem: Volatile-Filter (fps/Zahlen) in GetConfigSectionHeading —
   Überschrift ist jetzt „Anzeigeeinstellungen" statt „59 fps"; kein
   Ansage-/Log-Spam mehr (Frame-Logs entfernt, Aufrufer loggt Wechsel).
2. Text-Scanner: _NaviMap + _DTR ausgenommen (ScanIgnoredAddons);
   JEDE Scanner-Ansage loggt jetzt ihre Quelle ([Scan] Addon id: 'Text')
   → künftiger Spam ist sofort attribuierbar.
3. TolkService filtert Symbol-Glyphen (U+E000–U+F8FF, U+FFFD) aus ALLEN
   Ansagen → „Dalamud Plugins" statt „H-Kauderwelsch-Dalamud Plugins".
4. Talk: Sprecher-Node fest auf id=2 gepinnt (Probe-verifiziert) —
   Seiten ohne Sprecher und TalkSubtitle werden nie falsch umsortiert.

### Beim nächsten Start testen (V4.33)
1. Ansage „Version 4 Punkt 33 bereit".
2. OPTIONEN (Escape → Systemkonfiguration): Wird beim Öffnen
   „Systemeinstellungen. Anzeigeeinstellungen, Tab 8 von 8" gesagt und
   dann RUHE (kein fps-Spam)? Mit Maus/Tastatur durch Optionen: was
   wird angesagt? Tab wechseln (andere Kategorie-Icons): Ansage?
3. Auto-Lauf (N → Numpad 3): unterwegs KEINE Koordinaten-/Uhr-/
   Mesh-Ansagen mehr?
4. Escape → SystemMenu: „Dalamud Plugins" jetzt sauber (ohne
   Kauderwelsch)?
5. Journal (J): Pfeiltasten → Zeilen angesagt? (V4.32-Listen-Fix nie
   im Journal getestet!)
6. Dialog mit Ok/Abbrechen: LINKS/RECHTS drücken → Ansage? (Log
   zeigt danach [BtnProbe]/Navigate-Zeilen — diesmal bitte testen.)

---

## Historie: V4.32 (2026-07-11 Mittag)

### V4.31-Testauswertung (User + Log 09:34–09:45)
- ✓ NPC-DIALOGE FUNKTIONIEREN (User bestätigt, 26 Talk-Ansagen im Log,
  komplette Miounne-Szene). User-Wunsch: Name ZUERST („Miounne: Text"),
  bisher hing er hinten dran („Text. Miounne.") — Name ist eigener
  Text-Node, kommt in Node-Reihenfolge zuletzt (alle 26 Seiten, 2 Sprecher).
- ✗ Journal + SystemMenu weiter stumm bei Pfeiltasten. ABER: V4.30-Fix
  GREIFT — beide Male „Menü geöffnet" (PushMenu = Liste GEFUNDEN).
  Neue Erkenntnis: SelectedItemIndex BEWEGT SICH NICHT bei Tastatur
  (kein einziger Index-Wechsel im Log trotz Navigation).
- ✗ SelectYesno/JournalResult: Links/Rechts-Wechsel stumm. Navigate()
  loggte nichts → nicht diagnostizierbar (dieselbe Falle wie V4.21:
  TolkService loggte Sprachausgaben nicht).
- JournalResult („Ablehnen"/„Abschließen") ist KEIN SelectYesno — hatte
  gar keinen Links/Rechts-Handler; Fokus-Scan fand nur 1× statisch
  „Ablehnen" (Key=38004, Collision-Fallback matcht immer Node 38).

### Neu in V4.32 (Probes + Fixes)
1. SPRACH-LOG: TolkService loggt jetzt JEDE Ansage ([Speak]-Zeilen,
   auch Debounce-Verwerfungen). Log-Stille beweist ab jetzt Stummheit.
2. Talk: Sprecher ZUERST („Miounne: …"). Annahme (log-verifiziert,
   26 Seiten): letzter Text-Node = Name. Probe-Zeile „Dialog-Nodes:
   [id..]='…'" pinnt die Node-Id dauerhaft fest.
3. Listen-Probe (Journal/SystemMenu): AtkComponentList hat 5 Index-
   Kandidaten (ilspycmd: Selected@308, Held@312, Hovered@316,
   Hovered2@344, Hovered3@352) + IsHighlighted pro Zeile. V4.32 trackt
   ALLE, loggt Änderungen ([ListProbe]) und sagt die Zeile des bewegten
   Index an. Das Log zeigt, welches Feld die Tastatur wirklich trackt.
4. Dialog-Button-Fokus-Probe (SelectYesno + JournalResult): loggt alle
   Button-Flags bei Änderung ([BtnProbe]) und sagt den Button mit
   Fokus-Bit (0x100) an. Navigate() loggt jetzt zusätzlich jeden Aufruf.

### Beim nächsten Start testen (V4.32)
1. Ansage „Version 4 Punkt 32 bereit".
2. NPC ansprechen: kommt der Name jetzt ZUERST („Miounne: …")?
3. J → Journal: Pfeiltasten hoch/runter → wird jede Zeile angesagt?
   (Falls ja: fertig. Falls nein: [ListProbe]-Zeilen zeigen mir warum.)
4. Escape → SystemMenu: Pfeiltasten → „Ausloggen" usw. angesagt?
5. Beenden-Dialog (oder Quest-Dialog): Links/Rechts drücken → wird
   „Ok"/„Abbrechen" (bzw. „Ablehnen"/„Abschließen") angesagt?
6. Danach Log an Claude — die Probe-Zeilen ([ListProbe], [BtnProbe],
   [Speak], Dialog-Nodes) beantworten alle offenen Laufzeitfragen.

---

## Historie: V4.30/V4.31 (2026-07-11 Vormittag)

### V4.29-Testauswertung (Log 08:19–09:17): QUEST-NAVI KOMPLETT BESTÄTIGT
- Quest-Kategorie + Probe: Marker „Willkommen in Gridania" terr=183 ==
  aktuell (Zonen-Feld KORREKT), pos-Y=-8.0 lief direkt (Höhe passt aufs
  Mesh) → beide offenen Laufzeitfragen positiv beantwortet.
- Ansage „1 von 1: …, 100 Meter, geradeaus" → Numpad 3 → 99,9m Auto-Lauf
  → „angekommen=True" (08:26:29). Positions-Variante (id=0) funktioniert.
- JournalDetail-Dump kam an (Strg+F5 dumpte Journal + JournalDetail).
- ABER: Journal/SystemMenu/SelectString-Listen WEITER STUMM → Root Cause
  gefunden (s.u.), Timing-Hypothese von V4.28 war falsch.

### Neu in V4.30: Listen-Erkennung repariert + Quest-Text vorlesen
1. ROOT CAUSE (endlich): FindListInAddon prüfte `Type != NodeType.Component`
   — Komponenten tragen aber ROHE Typwerte ≥1000 (Component=10000 gibt nur
   GetNodeType() zurück). Die Bedingung war IMMER falsch → universelle
   Listen-Navigation war seit Einführung TOTER CODE. Fix: ≥1000-Check wie
   im restlichen Code. Repariert Journal, SystemMenu, SelectString (der
   stumme Ja/Nein-Dialog aus dem 08:27-Dump) und jedes andere Listen-Menü.
   Doku: docs/game-api.md → „FALLE NodeType".
2. Strg+F10 im Journal liest jetzt die QUEST vor: Titel, Stufe,
   „Ziel: Mit Miounne sprechen", Beschreibung (JournalCanvas-Struktur
   aus dem Dump, docs/game-api.md → „Journal / JournalDetail").
3. vnavmesh meldet beim Boot FileNotFound für seine Config (vnavmesh.json
   fehlt) — harmlos (Erststart, Defaults), Auto-Lauf lief trotzdem.

### Neu in V4.31: NPC-Dialoge (User: „noch NIE Sprachausgabe gehört")
- Befund: Talk-Handler las nur 1× bei PostSetup via ReadFirstText
  (Node-IDs 2–12) — Talk setzt den Text aber erst NACH PostSetup und
  wechselt Dialogseiten im SELBEN Fenster → praktisch immer stumm.
  Zudem loggte er nichts (unsichtbar in jeder Diagnose).
- Fix: OnTalkUpdate (PostUpdate) liest jeden Frame ReadAllTexts, spricht
  nur bei ÄNDERUNG (Dedup pro Addon, Reset bei Unsichtbar/Close), loggt
  jede Ansage („{name} Dialog: '…'"). Gilt für Talk UND TalkSubtitle
  (Untertitel-Addon, tauchte 08:26:41 auf, hatte NIE einen Handler).
- AddonTalk-Struct hat nur unbenannte TextNode-Felder (AtkTextNode220…)
  → bewusst generisch gelesen statt Offsets zu raten.
- OFFEN (Log zeigt es): Tippt der Talk-Text buchstabenweise ein
  (Typewriter), spammen wachsende Ansagen → dann Stabilitäts-Check
  nachrüsten. Log-Zeilen verraten es sofort.

### Beim nächsten Start testen (V4.31)
1. Ansage „Version 4 Punkt 31 bereit".
2. NPC ansprechen (Numpad 0 auf Miounne/Bertennant): wird der Dialog-
   text VORGELESEN? Weiterklicken → jede neue Seite angesagt?
   Log-Kontrolle: Zeilen „Talk Dialog: '…'".
3. J → Journal: Pfeiltasten → jede Zeile angesagt („St. 1, Willkommen
   in Gridania")? Log: „List-Navigation".
4. Im Journal Strg+F10 → Quest komplett vorgelesen (Titel, Ziel,
   Beschreibung)?
5. Escape → SystemMenu: Pfeiltasten → „Ausloggen" usw. angesagt?
6. NPC-Dialog mit Auswahlliste → Optionen angesagt?
7. Fehler-Popup: weit entfernten NPC anvisieren, Numpad 0 → „zu weit
   entfernt" hörbar?

---

## Historie: V4.27–V4.29 (2026-07-10 spät)

### 🎉 MEILENSTEIN: Auto-Lauf via vnavmesh FUNKTIONIERT (User + Log 21:20/21:21)
- vnavmesh lädt jetzt: Profil-Eintrag IsEnabled musste auf true (Dalamud
  trägt neue Dev-Plugins by design mit false ein; dekompiliert verifiziert:
  PluginManager.LoadPluginAsync — IsEnabled=true + StartOnBoot=true → lädt).
  Config-Edit bei beendetem Spiel, BOM-frei; Backup
  dalamudConfig.json.bak-vor-vnavmesh-enable.
- 3 erfolgreiche Läufe im Log: „Pfad beendet, dist=2,4/2,5, angekommen=True"
  (Honoraint 33,6m, Bertennant 30,2m). Fehlerpfad davor auch sauber
  (21:10:18 „vnavmesh-IPC fehlgeschlagen" bei noch deaktiviertem Plugin).

### Neu in V4.27 (User-Feedback: „nicht immer Koordinaten ansagen")
- Während des Auto-Laufs sind die automatischen Zielwechsel-Ansagen
  STUMM (Plugin.cs: `_navigation.Update(… && !_autoWalk.IsActive)`).
  Ursache des Genervt-Seins: beim automatischen Laufen greift das Spiel
  laufend vorbeiziehende NPCs als Soft-Target → jedes wurde mit „Ziel:
  Name, Art, X Meter, Richtung" angesagt (Log 21:20:44, 21:20:59 …).
  Auto-Lauf-eigene Ansagen (Laufe zu/Ziel erreicht/gestoppt) bleiben.
  Dedup-Id läuft stumm mit → keine nachgeholte Alt-Ansage nach Ankunft.

### V4.27 BESTÄTIGT (User, 21:37): Auto-Lauf ohne Ansage-Spam. ✓

### Neu in V4.28: Journal + SystemMenu (Dump-Analyse 21:37/21:38)
Befund aus den User-Dumps:
- Journal (Taste J, „ARCHIV"): Quest-Liste ist Comp TreeList(12) mit
  ListItemRenderer-Zeilen (id=4 „St. 1", id=3 Quest-Name; Kategorie-
  Zeilen mit Gebiets-/Add-on-Namen). Wurde nie angesagt, weil
  FindListInAddon nur ComponentType.List akzeptierte.
- SystemMenu (Escape): normale List(9) mit 15 Einträgen [ListLen=15],
  aber bei PostSetup existierte die Liste noch NICHT (Fenstertitel kam,
  „Menü geöffnet" fehlt im Log; Dump 5s später zeigt die Liste) →
  Liste wird erst nach PostSetup aufgebaut.
Fixes (alle in UIReaderService):
1. FindListInAddon akzeptiert auch TreeList — Cast sicher, ilspycmd:
   AtkComponentTreeList [Inherits<AtkComponentList>(0)]
2. Späte Listen: OnAnyAddonUpdate prüft _noListCache-Addons jeden Frame
   erneut; sobald Liste da → PushMenu + Ansage „Eintrag, X Einträge"
3. ReadListItemText liest ALLE sichtbaren Texte der Zeile (Journal:
   „St. 1, Willkommen in Gridania" statt nur „St. 1"); NEU: unsichtbare
   Text-Nodes werden übersprungen (vorher zählten sie mit!)
4. Strg+F5 dumpt Begleitfenster mit: fokussiertes Addon + „…Detail"
   (Journal → JournalDetail = Quest-Beschreibung, Struktur noch unbekannt)

### Neu in V4.29: Quest-Ziele im Objekt-Browser (QuestMarkerService.cs neu)
1. Neue Kategorie „Quest-Ziele" (Strg+N bis zur Ansage): N/Umschalt+N
   blättert durch die Marker der ANGENOMMENEN Quests. Im Gebiet:
   „1 von 2: Questname, 150 Meter, links"; fremdes Gebiet: „… in einem
   anderen Gebiet."
2. Numpad 3 läuft zum gewählten Quest-Ziel (Positions-Variante des
   Auto-Laufs — Marker sind keine GameObjects, kein Target-Set).
   Fremdes Gebiet → klare Ansage statt Lauf (Zonen-Check frisch beim
   Tastendruck). Stop-Range = max(2,5m, Marker-Radius) — Questkreis
   betreten reicht (Radius kommt vom Spiel, kein Magic Value).
3. Quellen ilspycmd-verifiziert: Map.Instance()->QuestMarkers (30×
   MarkerInfo: Label, MarkerData-Vector mit Position/Radius/
   TerritoryTypeId@66) — docs/game-api.md → „Quest-Marker".
4. DEBUG-PROBE eingebaut: jeder N-Druck in der Kategorie loggt alle
   Marker ([Quest]-Zeilen: pos/r/terr/map/render) → klärt die zwei
   offenen Laufzeitfragen (Zonen-Feld korrekt? Y-Höhe auf Mesh?).
5. AutoWalkService refaktoriert: TryStartPath/BeginWalk gemeinsam,
   _destPosition (Objekt = jeden Frame nachgeführt, Position = fix),
   Beacon läuft jetzt auch ohne Objekt.

### Beim nächsten Start testen (V4.29)
1. Ansage „Version 4 Punkt 29 bereit".
2. JOURNAL (V4.28): J drücken → „ARCHIV" + Listen-Ansage? Pfeiltasten
   hoch/runter → jede Zeile angesagt? Im Journal Strg+F5 → Dump enthält
   jetzt AUCH JournalDetail (für Quest-Text-Vorlesen).
3. SYSTEMMENU (V4.28): Escape → beim Pfeiltasten-Navigieren werden
   „Ausloggen", „Systemkonfiguration" usw. angesagt?
4. QUEST-NAVI (V4.29): Quest annehmen (falls keine aktiv), Strg+N bis
   „Kategorie Quest-Ziele: …", N → Ansage mit Distanz? Numpad 3 →
   läuft hin + „Ziel erreicht"? Danach [Quest]-Logzeilen an Claude
   (Probe-Auswertung Zone/Höhe).
5. FEHLER-POPUPS (User-Frage): weit entfernten NPC anvisieren (N),
   Interaktion versuchen (Numpad 0) → wird „Das Ziel ist zu weit
   entfernt." gesprochen? (_TextError-Handler existiert seit früh,
   in-game NIE verifiziert!)
6. Falls Journal-Navigation stumm: SelectedItemIndex der TreeList
   trackt Tastatur evtl. nicht → Log an Claude, dann Highlight-
   NineGrid-Weg (Dump: gewählte Zeile hat id=7 NineGrid sichtbar).

### Offene Baustellen
- V4.25-Features ungetestet: Beacon-Lautstärke nach Distanz,
  Ablehnungs-Melder („Achtung, nicht anvisiert")
- Namenseingabe-Echo, Aussehen-Regler, Lumina ID→Name,
  Quest-NPC-Erkennung (Nameplate-Icon)

### vnavmesh-Installations-Krimi (GELÖST nach 3 Fehlversuchen): BOM!
- Dateien liegen korrekt in devPlugins\vnavmesh (v1.2.3.8, ApiLevel 15 ✓)
- Dev-Plugins lädt Dalamud NUR über DevPluginLoadLocations in
  dalamudConfig.json — Ordner allein reicht nicht
- Die per PowerShell geschriebene Config verschwand 3× „von Geisterhand":
  PS 5.1 schreibt UTF-8 MIT BOM → Dalamuds ReliableFileStorage liest rohe
  Bytes (kein BOM-Strip) → JsonReaderException → STILLER Fallback auf
  SQLite-Backup (dalamudVfs.db) → alter Stand überschreibt die Datei.
  Beweis: lokales Repro mit Dalamuds Serializer-Settings (scratchpad)
- Fix: Desktop\vnavmesh_aktivieren.ps1 schreibt jetzt BOM-los
  ([IO.File]::WriteAllText mit UTF8Encoding(false)) + prüft erstes Byte
- Stand JETZT: Eintrag drin, BOM-frei verifiziert (erstes Byte 0x7B),
  kein Spielprozess — Spielstart durch User steht aus
- Backups: dalamudConfig.json.bak-vor-vnavmesh (mehrfach überschrieben,
  enthält Vor-Edit-Stand des letzten Laufs)

### VORZEICHEN BESTÄTIGT (User-Hörtest V4.24): positiv = rechts stimmt,
Beacon-Panning wanderte auf die richtige Seite. Richtungssystem ist damit
KOMPLETT verifiziert (Nullpunkt per F-Snap-Log, Vorzeichen per User-Ohr).

### Neu in V4.26: Auto-Lauf zum Ziel (Numpad 3) via vnavmesh-IPC
1. Numpad 3 = automatisch zum aktuellen Ziel laufen (Toggle). vnavmesh
   findet den Weg übers Navmesh und steuert um Hindernisse herum.
2. Ansagen: „Laufe zu X" / „Ziel erreicht: X" / „Kein Weg zu X gefunden" /
   „Auto-Lauf gestoppt" / bei fehlendem vnavmesh klare Fehlermeldung.
3. Beacon läuft während des Auto-Laufs mit (Richtung+Distanz hörbar).
4. Gehhilfe und Auto-Lauf schließen sich gegenseitig aus (teilen den Ton).
5. Stoppt bis 2,5 m vor dem Ziel (Interaktionsreichweite); Ankunft wird
   per Distanz-Check verifiziert, sonst „Auto-Lauf beendet, noch X Meter".
6. Alle IPC-Signaturen quellcode-verifiziert: docs/game-api.md → vnavmesh-IPC
7. AUSSTEHEND: vnavmesh selbst installieren (Fremd-Plugin!). Download nach
   devPlugins wurde vorbereitet, wartet auf User-Entscheidung. Version
   1.2.3.8, ApiLevel 15, Quelle https://puni.sh/api/repository/veyn

### V4.24-Testergebnis (Log 16:36–16:42): GEHHILFE-VOLLTEST BESTANDEN
- RICHTUNGS-FIX BESTÄTIGT: nach F ist relAngle=0 (vorher 29°) — die
  V4.23-Formel stimmt. Ganzer Ablauf N→F→W→„angekommen, dist=2,9" lief!
- Ankunft + Auto-Aus funktionieren (16:38:16). Offene Frage an User:
  wurde die Ansage („Angekommen bei Bertennant") auch GEHÖRT?
- Vorzeichen (links/rechts) weiter unbestätigt — Frage an User: Drehung
  16:38:05 (vor dem F) — war das A oder D? rot fiel dabei (1,11→0,50)
- NEUER BUG ENTDECKT: 16:39:26–16:39:44 lehnte das Spiel ALLE Target-Sets
  ab (Hard-Target klebte auf Honoraint; jeder N-Druck sprach zusätzlich
  „Ziel: Honoraint" = Spam). Ursache unklar; SetHardTarget gibt bool
  zurück, Dalamud verwirft ihn (docs/game-api.md → „SetHardTarget kann
  ABLEHNEN"). Zeitgleich hing der User vermutlich an einem Hindernis
  (dist fror bei 28,5 m ein)

### Neu in V4.25 (User-Wünsche + Bugfixes)
1. Beacon-Lautstärke nach Distanz: volle Lautstärke ≤5 m, linear leiser
   bis 20% ab 80 m (User-Wunsch: näher = lauter)
2. Ankunfts-Ansage heißt jetzt „Ziel erreicht: Name" (User-Formulierung);
   Gehhilfe + Ton gehen dabei automatisch aus (war schon so)
3. Ablehnungs-Melder: nach jedem Target-Set liest das Plugin das Ziel
   zurück; bei Ablehnung Ansage „Achtung, nicht anvisiert" + Logzeile
   „[Nav] Target-Set ABGELEHNT" (sonst dreht F still zum FALSCHEN Ziel!)
4. Zielwechsel-Ansage feuert nur noch bei ECHTER Änderung des Ziels
   (Spam-Fix); manuelles Gehhilfe-Aus wird geloggt
5. OFFEN/RECHERCHE: Hindernis-Ausweichen gibt es nicht (Luftlinie!) —
   Stufe 2 Auto-Walk via vnavmesh-IPC steht als Recherche an

### Neu in V4.24: Audio-Beacon in der Gehhilfe (User-Wunsch)
1. Gehhilfe (Strg+Umschalt+N) spielt jetzt zusätzlich Piepser (2×/Sekunde,
   NAudio-Sinuston, jedes Frame aktualisiert — nicht nur alle 2s):
   - Ziel geradeaus = hoch (880 Hz) und mittig
   - Ziel seitlich = Ton wandert auf die Zielseite und wird tiefer
     (eine Oktave pro 90 Grad)
   - Ziel hinter dir = ganz tief (220 Hz), wieder mittig
2. Sprachansagen alle 2s bleiben; „Angekommen" prüft jetzt jedes Frame
3. Der Beacon ist gleichzeitig der VORZEICHEN-TEST: Wandert der Ton beim
   Rechtsdrehen (D) nach LINKS, stimmt alles. Wandert er nach RECHTS,
   ist links/rechts gespiegelt → melden, Fix ist eine Zeile
4. Lautstärke: Config BeaconVolume (0..1, Standard 0,35)
5. Kein Audiogerät → Ansage „Ton-Beacon nicht verfügbar", Sprach-Gehhilfe
   läuft trotzdem weiter
6. Technik: NAudio 2.2.1 (NuGet), CopyLocalLockFileAssemblies=true nötig
   (sonst landen NuGet-DLLs nicht im Output); BeaconService.cs neu
7. Nebenbei-Fix: Gehhilfe lief bisher nur, wenn AnnounceTargetChanges an
   war (Update()-Aufruf war daran gekoppelt) — jetzt entkoppelt

### V4.22-Testergebnis (Log 15:17–15:42): Browser ✓, Gehhilfe ✓, Richtung ✗ (Fix in V4.23)
- Objekt-Browser BESTÄTIGT: N/Umschalt+N blättern durch 10–11 NPCs
  (distanzsortiert, Umbruch am Ende), Ziel wird real gesetzt
- Zielwechsel-Ansage (V4.21-Fix) BESTÄTIGT: 15:26:33 echte Spiel-Targeting-
  Ansage („Ziel: Ulta") via ITargetManager — Tab/F12-Pipeline funktioniert
- Gehhilfe BESTÄTIGT: 2s-Ticks laufen; „Angekommen" ungetestet (nie ≤3m)
- ROTATIONS-KONVENTION GEKNACKT (Beweis im Log): F-Snap rastete 2× auf
  rot=-1,83 ein = exakte Zielrichtung atan2(dx,dz)=-105°=-1,83 rad →
  Blickvektor = (sin rot, cos rot). Alte Formel („0=Norden", atan2(dx,-dz))
  war eine SPIEGELUNG: Plugin sagte „leicht rechts" (29°), während der User
  nachweislich EXAKT aufs Ziel blickte. Details: docs/game-api.md →
  „Rotations-Konvention"

### Neu in V4.23: Richtungs-Fix
1. RelativeAngle nutzt jetzt `atan2(dx, dz) - rot` — Nullpunkt („geradeaus")
   ist damit log-verifiziert korrekt
2. OFFEN: Vorzeichen (positiv = rechts?) noch unverifiziert. TESTPLAN:
   a) N drücken, Richtungsansage merken (z. B. „links")
   b) A drücken/halten (links drehen), N erneut: Ansage muss Richtung
      „geradeaus" wandern — wenn sie Richtung „hinter" wandert, ist das
      Vorzeichen falsch (dann einfach melden, Fix ist eine Zeile)
   c) Gehhilfe-Log liefert Zweitbeweis (rot-Verlauf beim D-Halten)
3. Volltest Gehhilfe: N → F (hindrehen, muss jetzt „geradeaus" sagen!) →
   W bis „Angekommen bei …"

### V4.21-Testergebnis (Log 15:03–15:06): Tab/F weiter stumm — ABER unklar warum
- Sprachausgaben werden NICHT geloggt (TolkService loggt Speak nicht) →
  Log-Stille beweist nichts. V4.22 loggt jetzt [Nav]-Ereignisse.
- Verdacht: Tab schaltet nur GEGNER durch (TARGET_NEXT) — um den User
  herum waren nur friedliche NPCs (Bertennant, Ulta) → Tab tat evtl.
  gar nichts; F (FACE) dreht nur, sagt nie etwas. F12 (nächster NPC)
  wäre der richtige Test gewesen. NamePlate-Wechsel 15:05:41 zeigt
  UI-Aktivität, aber ohne [Nav]-Logs nicht zuordenbar.

### USER-WUNSCH (neu): Auswahl-System für Objekte in der Nähe + automatisch
hinlaufen. Stufe 1 (V4.22, gebaut): Browser+Gehhilfe (User läuft selbst mit W).
Stufe 2 (offen): echtes Auto-Laufen — Recherche nötig (vnavmesh-IPC vs.
Input-Injection; Workaround-Disziplin: erst Optionen sammeln, User fragen!)

### Neu in V4.22: Objekt-Browser + Gehhilfe (NavigationService)
1. N = nächstes Objekt der Kategorie (nach Distanz sortiert, ≤100m):
   visiert es WIRKLICH an (ITargetManager.Target = obj) + sagt
   „2 von 5: Name, Art, Distanz, Richtung". Umschalt+N = zurück.
2. Strg+N = Kategorie: Alles/NPCs/Gegner/Spieler/Objekte/Sammelpunkte/
   Ätheryten (Ansage mit Anzahl). „Ausgänge" gibt es in der ObjectTable
   NICHT — Recherche AgentMap/Map-Marker steht aus.
3. Strg+Umschalt+N = Gehhilfe: alle 2s „Distanz, Richtung", bei ≤3m
   „Angekommen" (auto-aus). Workflow: N → F (Spiel dreht hin) → W/R laufen.
4. Debug-Logs [Nav] für Zielwechsel/Auswahl/Gehhilfe-Ticks inkl. relAngle
   + rot → klärt endlich die Rotations-Konvention UND die Tab-Stummheit.
5. Konflikt-Check jetzt Modifier-exakt (Strg+F1 ≠ F1; die 10 „KONFLIKT"-
   Warnungen von 15:04 waren falsch-positiv, echte Konflikte: 0 erwartet)
6. Config-Felder umbenannt (KeyNextObject/KeyPrevObject/KeyCategory/
   KeyWalkGuide) — alte Namen fallen weg, Werte = Defaults

### V4.20-Testergebnis (Log 13:31–13:34): Dump ✓, Ziel-Ansage ✗ → Ursache gefunden
- Auto-Keybind-Dump LIEF: 171 Aktionen, 10 Konflikte, Datei auf Desktop —
  von Claude analysiert, Ergebnisse in docs/game-api.md → „Safe Mod Keys"
- Tab-Targeting gab KEINE Ansage. Ursache (Log: NamePlate-Fokus da, aber
  kein „Ziel:", kein Fehler): `LocalPlayer.TargetObject` trackt UI-Targeting
  nicht. Fix: Dalamud `ITargetManager.Target ?? SoftTarget` (V4.21, auch
  in SetTargetFromGameTarget gefixt)

### Neu in V4.21
1. Ziel-Ansage + Ziel-Verfolgung lesen jetzt ITargetManager (s.o.)
2. KOLLISIONSFREIE TASTEN (Dump-Ground-Truth: alle F1–F12 vom Spiel belegt,
   N = einziger freier Buchstabe). Neue Belegung:
   - N = Objekte in der Nähe; Umschalt+N = Richtung zum Ziel;
     Strg+N = Ziel verfolgen; Strg+Umschalt+N = Verfolgung beenden
   - Strg+F1 Hilfe, Strg+F2 Fenster, Strg+F5 UI-Dump, Strg+F10 Menü
     vorlesen, Strg+F11 Stille, Strg+F12 Kampfstatus
   - ACHTUNG WORKFLOW: Dumps in der Lobby jetzt Strg+F2/Strg+F5 statt F2/F5!
3. IsJustPressed kann Modifier (exakte Übereinstimmung, Edge-Detection
   1× pro Frame und VK — vier Funktionen teilen sich Taste N)
4. Config-Migration V1→V2 setzt alte F-Tasten-Belegung automatisch um
5. Hilfe-Ansagen (Strg+F1, /acc help) nennen die neuen Tasten

### DURCHBRUCH-ERKENNTNIS: Offizielle Tastenliste (User-Link, dokumentiert
in docs/game-api.md → „Offizielle Standard-Tastaturbelegung")
- ALLE F-Tasten F1–F12 sind vom Spiel belegt (F1=selbst, F2–F8=Gruppe,
  F9=Begleiter, F10=Fokus, F11=nächster GEGNER, F12=nächster NPC/OBJEKT)
- → Spiel hat EINGEBAUTE Navigations-Tasten (F11/F12/Tab)! Unser Job ist
  nur die Ansage. Plugin-Tasten müssen umziehen (Kandidaten: N, NUM3,
  Strg/Umschalt+F-Kombis — Dump abwarten, IsJustPressed kann noch keine Modifier)

### Neu in V4.20: Ziel-Ansage bei Zielwechsel (NavigationService.Update)
Bei jedem Zielwechsel automatisch: „Ziel: Name, Art, Entfernung, Richtung."
Damit sind Tab, F1–F12, T sofort blind nutzbar — Navigation über die
spieleigenen Targeting-Tasten statt eigener Parallel-Mechanik.
Art aus Dalamud ObjectKind (Pc/BattleNpc/EventNpc/Treasure/Aetheryte/
GatheringPoint/EventObj/Mount/Companion/Retainer — ilspycmd-verifiziert).
Abschaltbar per Config AnnounceTargetChanges. Richtungsworte weiter
unter Rotations-Vorbehalt (s.u.).

### Neu in V4.19: Keybind-Dump läuft AUTOMATISCH nach dem Login
User kann den Chat noch nicht öffnen (`/acc keys` unerreichbar) → das Plugin
dumpt jetzt einmal pro Sitzung automatisch, sobald eingeloggt und die
Keybind-Tabelle lesbar ist (KeybindService.IsReady, Retry pro Frame bis
bereit). Ansage: „Tastenbelegung gespeichert: … Konflikte …".
Chat-Öffnen-Frage des Users: Standard-Taste ist Enter (CMD_CHAT) —
wird durch den Dump bestätigt. ABER: NVDA liest getippte Zeichen im
Spiel-Chat nicht (gleiche Lücke wie Namensfeld → Textfeld-Echo-Baustelle).

### User-Richtung: Tutorial läuft (wird vorgelesen!), jetzt gewünscht:
1. Alle Spiel-Tasten dokumentieren (Gate Check für neue Mod-Tasten)
2. Navigationssystem: NPCs, Gegenstände, Quest-Ziele finden (Orientierung)

### Neu in V4.18: `/acc keys` — Spiel-Tastenbelegung dumpen (KeybindService)
- Liest die LIVE-Keybind-Tabelle aus dem Spiel:
  `UIInputData.Instance()->InputData.GetKeybindSpan()` (ilspycmd-verifiziert,
  dokumentiert in docs/game-api.md → „Keybind-System").
- Schreibt alle belegten Aktionen nach `Desktop\FFXIV_Keybinds.txt`
  (Format: `AKTION (InputId): Taste1 ; Taste2`, deutsche Modifier Strg/Umschalt/Alt).
- Konflikt-Check: welche Spiel-Aktionen liegen auf unseren Plugin-Tasten
  (F1–F12)? → Zeilen `KONFLIKT F1 (...)` in Datei + Log.
  VERDACHT: F1–F8 = TARGET_P1–P8 (Gruppenmitglieder) → unsere Tasten
  kollidieren in-game. Dump liefert Ground Truth, dann Tasten neu wählen.

### Beim nächsten Start testen (V4.22)
1. Ansage „Version 4 Punkt 22 bereit".
2. N drücken → „1 von X: Name, Art, Distanz, Richtung"? Mehrfach N →
   blättert weiter? Umschalt+N zurück?
3. Strg+N → „Kategorie NPCs: X in der Nähe"?
4. Objekt wählen (N), dann Strg+Umschalt+N → „Gehhilfe an", F drücken
   (dreht hin), W halten → alle 2s Ansage, am Ende „Angekommen"?
5. F12 → „Ziel: …"-Ansage (Spiel-eigenes Targeting)?
6. Danach Log an Claude: [Nav]-Zeilen klären Rotations-Konvention
   (relAngle sollte beim Zulaufen gegen 0 gehen).
3. V4.17-Punkte weiter offen: kein _LimitBreak-Logspam mehr? Enter im
   Namensdialog (falls neuer Charakter)?
4. Navigation antesten (existiert schon als Befehle, nie in-game getestet):
   `/acc near` (Objekte in der Nähe), Ziel anvisieren + `/acc set`, dann
   `/acc nav` (Richtung+Distanz). ACHTUNG: Richtungsansage (links/rechts/
   geradeaus) basiert auf unverifizierter Rotations-Konvention — bitte
   testen: NPC anvisieren, hindrehen, `/acc nav` → sagt er „geradeaus"?

### Danach (Navigations-Ausbau, nach Keybind-Analyse)
- Sichere Mod-Tasten festlegen, F1–F12-Belegung ggf. umziehen
- Kategorien-Navigation: NPCs / Questgeber / Gegenstände / Aetheryten
  getrennt durchblättern (ObjectKind-Filter), Uhrzeiger- statt
  links/rechts-Ansage, Quest-Marker aus AgentMap lesen (recherchieren)
- Alte Baustellen: SelectString/SystemMenu-Dumps (Log 11:42/11:44),
  Namenseingabe-Echo, Aussehen-Regler, Lumina ID→Name

---

## Historie: V4.17 (2026-07-10 Mittag)

### 🎉 MEILENSTEIN: Charakter erstellt, User ist IM SPIEL (Log 11:16–11:44)
Kompletter CharaMake-Durchlauf hat funktioniert; danach In-Game-Fenster
(SelectString 11:42, SystemMenu 11:44 — F5-Dumps davon liegen im Log!).
Keine Fehler/Exceptions in der ganzen Session.
V4.17-TEIL-BESTÄTIGUNG (mündlich, 2026-07-10 Nachmittag): Tutorial läuft,
Anweisungen werden vorgelesen. Logspam/Namensdialog noch nicht rückgemeldet.

### V4.16-Testergebnisse (Log 11:00–11:16) — fast alles bestätigt
- **Geschlecht KORREKT:** „Hyuran, männlich" angesagt; Widerspruchs-Zeile
  bestätigt erneut: Checkbox-Symbol hätte fälschlich „weiblich" gesagt,
  sichtbares Modell (Sex=0) gilt. Ground-Truth-Weg funktioniert.
  (User hat nicht getoggelt — dank Ground Truth aber egal, Label stimmt immer.)
- **Volksstamm-Handler BESTÄTIGT:** „Wiesländer" gewählt angesagt,
  Hover Hochländer/Wiesländer/Ok funktioniert.
- **Enter=Ok BESTÄTIGT** auf _CharaMakeFeature (param=37) und
  _CharaMakeRaceGender (param=28) — Event-Dispatch-Klick funktioniert!
- **Enter=Ok FEHLTE im Namensdialog:** _CharaMakeCharaName hat keinen
  „Ok"-Button — der Knopf heißt „Bestätigen" (node id=16, Zurück=id=3;
  User musste mit Maus klicken, Log 11:15:48).
- **In-Game-Logspam:** _LimitBreak feuert TimelineActiveLabelChanged 3×
  pro Frame, _ScreenInfo* TimerTick → ~98.000 Zeilen in 26 Minuten.

### Neu in V4.17 (beide Punkte gefixt)
1. `ConfirmButtonLabels = ["Ok", "Bestätigen"]` — Enter drückt jetzt auch
   den Bestätigen-Knopf im Namensdialog.
2. `IgnoredEventTypes` + 64/65/66/74 (TimerTick/End/Start,
   TimelineActiveLabelChanged, Werte per ilspycmd) — Log-Spam weg; die
   Events sind reine Animations-/Timer-Ticks, nie Navigation.

### Beim nächsten Start testen (V4.17)
1. Ansage „Version 4 Punkt 17 bereit".
2. Im Spiel: Log darf nicht mehr fluten (kein _LimitBreak-Spam).
3. Falls neuer Charakter-Durchlauf: Enter im Namensdialog → „Ok"-Ansage
   und Dialog bestätigt?
4. In-Game-Menüs erkunden (Escape → SystemMenu, NPC-Dialoge): was wird
   angesagt, was ist stumm? F2/F5 auf stummen Fenstern.

### Nächste Baustellen (In-Game-Phase!)
- SelectString- und SystemMenu-Dumps aus dem Log analysieren (11:42/11:44)
- Namenseingabe-Echo (getippte Zeichen ansagen) — für den nächsten Charakter
- Aussehen-Feinauswahl (CMFSlider/CMFIcon*/CMFColorL — Dumps vorhanden)
- Alte Liste: Cooldown-Ansagen, Audio-Beacon, Marktbrett, Inventar,
  Zielverfolgung per Name

---

## Historie: V4.16 (2026-07-10 Vormittag)

### V4.15-Testergebnisse (Log 10:18–10:22) — Probe erfolgreich, Session sehr ergiebig
- **Sichtbarkeits-Probe funktioniert:** genau 1 von 32 Vorschau-Modellen
  sichtbar (`Vorschau sichtbar: [200] Sex=0`); die 31 versteckten tragen
  RenderFlags=0x40. Damit haben wir Ground Truth fürs angezeigte Modell.
- **Indiz für VERTAUSCHTE Zuordnung:** Beim Öffnen war Checkbox id=3 (©)
  checked → Plugin sagte „weiblich", aber das sichtbare Modell hatte Sex=0
  (=männlich). Nur 1 Datenpunkt (User hat nicht getoggelt, Modell könnte
  beim Öffnen theoretisch nachhinken) → nicht hart geflippt, sondern:
- **User hat F2/F5-Dumps geliefert:** _CharaMakeTribe + _CharaMakeProgress
  (10:20:10), _CharaMakeFeature + CMFSlider (10:20:38), CMFColorL (10:21),
  SelectYesno (10:22) — alle im Log, analysiert: siehe docs/game-api.md
  (NEU angelegt, CharaMake-Sektion).

### Neu in V4.16
1. **Geschlechts-Ansage = Sex-Byte des SICHTBAREN Vorschau-Modells**
   (V4.14-Idee, aber jetzt das richtige Objekt). Checkbox bleibt
   Änderungs-Detektor + Fallback-Label; Widerspruch wird geloggt
   (`RaceGender: Vorschau-Sex=.. widerspricht Checkbox-Symbol ..`).
   Probe läuft nur bei Auswahl-Änderung — kein Spam.
2. **_CharaMakeTribe-Handler** (Volksstamm, der erste bisher stumme Schritt):
   dediziert in SpecialUpdateAddons; Hover per Event-Target
   („Hochländer"/„Wiesländer"/Ok/Zurück), Auswahl-Änderung per checked
   Top-Level-CheckBox (Label ≥2 Zeichen filtert die ®/©-Glyphen-Boxen).
3. **Enter = Ok in Lobby/Charaktererstellung** (User-Wunsch, alter offener
   Punkt „Taste für Ok im DC-Fenster"): `PressFocusedOk` sucht im obersten
   fokussierten _CharaMake*/CharaMake*/TitleDCWorldMap-Addon den sichtbaren
   Button „Ok" und feuert dessen registriertes ButtonClick-Event an den
   Listener — derselbe Weg wie ein echter Mausklick, KEIN Callback-Raten.
   (Alle Structs per ilspycmd verifiziert: AtkEventManager.Event-Kette,
   AtkEvent.State.EventType/Param/Listener, ReceiveEvent, AtkEventData=40B,
   ButtonClick=25.) Reihenfolge wichtig: von HINTEN durch die Fokus-Liste,
   damit der Schritt-Ok und nicht der finale Progress-Ok getroffen wird.
   Ansage „Ok" bei Erfolg, „Kein Ok-Knopf gefunden" sonst. Enter behält
   überall sonst seine Spielfunktion (Whitelist).

### Beim nächsten Start testen (V4.16)
1. Ladeansage „Version 4 Punkt 16 bereit".
2. Volk & Geschlecht: Geschlecht MEHRMALS mit Links/Rechts wechseln —
   wird es angesagt und stimmt männlich/weiblich jetzt? (Im Log muss
   `Vorschau sichtbar` bei jedem Wechsel das Sex-Byte umschalten.)
3. **Enter drücken** bei Volk & Geschlecht: sagt „Ok" und wechselt zum
   Volksstamm? (Falls „Kein Ok-Knopf gefunden" → Log zeigt warum.)
   Dann beim Volksstamm: Hoch/Runter → werden „Hochländer"/
   „Wiesländer" (bzw. Stämme des gewählten Volks) angesagt? Maus-Hover?
   Wieder mit Enter weiter. ACHTUNG bei der Namenseingabe: Enter könnte
   dort doppelt wirken (Spiel-Enter im Textfeld + unser Ok) — Log prüfen.
4. Weiter durchklicken: Aussehen usw. — F2/F5 auf allem was stumm ist
   (Dumps von Feature/Slider/Farbe sind schon da, aber mehr schadet nicht).
5. Log an Claude.

### Danach geplant
- _CharaMakeFeature/CMFSlider/CMFColorL-Handler (Dumps liegen schon im
  Log vom 10.07., Analyse siehe docs/game-api.md)
- _CharaMakeProgress vorlesen (Schritt-Übersicht mit aktuellen Werten)
- Namenseingabe-Echo (_CharaMakeCharaName): getippte Zeichen ansagen —
  AtkComponentTextInput vorher per ilspycmd verifizieren
- Lumina ID→Name (IDataManager, Excel-Sheets)

---

## Historie: V4.15 (2026-07-10)

### V4.14-Testergebnisse (Log 2026-07-10 09:46) — Ansatz widerlegt
- Die Probe zeigte: **32 Pc-Objekte** (Indizes 200–231, Sex abwechselnd 0/1,
  keine Namen) = alle 8 Völker × 2 Stämme × 2 Geschlechter sind GLEICHZEITIG
  in der ObjectTable. Es gibt nicht „den einen" Vorschau-Charakter.
- Folge: Code nahm das erste Pc (Index 200, immer Sex=0) → Geschlecht hing
  auf „männlich" fest, Links/Rechts sagte NICHTS mehr an (Rückschritt zu V4.13).
- Nebenbefund: Kontroll-Logzeile feuerte jeden Frame → 10.792 Spam-Zeilen.

### Neu in V4.15: Checkbox als Quelle zurück + Sichtbarkeits-Probe
- Geschlecht kommt wieder aus den Checkboxen (id=4/id=3) — Wechsel-Erkennung
  in V4.13 nachweislich zuverlässig. Offen bleibt NUR die Label-Zuordnung.
- Neue Probe `LogPreviewActors` (bei jeder Auswahl-Änderung): loggt pro
  Pc-Objekt Sex, RenderFlags und `DrawObject.IsVisible` (Felder verifiziert
  per ilspycmd: GameObject.DrawObject@256, RenderFlags@280, VisibilityFlags
  None=0/Model=2/Nameplate=0x800; DrawObject.IsVisible existiert).
- HYPOTHESE: genau EIN Vorschau-Modell ist sichtbar; sein Sex-Byte beim
  Geschlechtswechsel bestätigt (oder widerlegt) id=4=männlich.
- Log-Spam behoben: Probe läuft nur bei Änderung, Voll-Dump 1× pro Screen.

### Beim nächsten Start testen (V4.15)
1. Ladeansage „Version 4 Punkt 15 bereit".
2. Charaktererstellung → Volk wählen, dann Geschlecht mehrmals mit
   Links/Rechts wechseln (wird wieder angesagt wie in V4.13).
3. Danach Log an Claude: Zeilen `Vorschau sichtbar: [idx] Sex=..` —
   wechselt das Sex-Byte des sichtbaren Modells synchron mit der Ansage?
   - Sichtbar-Sex=0 wenn „männlich" angesagt → Zuordnung KORREKT.
   - Umgekehrt → Zuordnung drehen.
   - „KEINES" oder 32 sichtbar → IsVisible taugt nicht, anderer Weg nötig.
4. DANN WEITERKLICKEN: Mit Ok zum nächsten Bildschirm (Aussehen usw.).
   Auf JEDEM stummen Bildschirm F2 (Fenstername) + F5 (Struktur-Dump)
   drücken — User-Befund 2026-07-10: alles nach Volk/Geschlecht ist noch
   stumm (Geburtsdatum, Schutzgott, Startklasse, Stadt haben keine Handler).
   Dumps aus dem Log → Handler bauen.

### Offene Frage des Users: Namenseingabe
Das Eingabefeld für den Charakternamen kommt am Ende der Charaktererstellung.
Fenstertitel „Name des Charakters" + Ok/Abbrechen werden schon angesagt
(V4.10 bestätigt). FEHLT: Echo der getippten Zeichen (NVDA liest das
Spiel-Textfeld nicht mit). Nächstes Feature nach dem V4.15-Test:
Textfeld-Inhalt bei Änderung ansagen (AtkComponentTextInput o.ä. — vorher
per ilspycmd verifizieren).

---

## Historie: V4.13 (2026-07-09)

### V4.12-Testergebnisse (Log 22:17) — teils Erfolg
- **Geschlechts-Ansage funktioniert:** Links/Rechts sagt männlich/weiblich. ✓
- **Völker WERDEN erkannt** (V4.11 Event-Target): alle 8 kommen als
  „Fokus via Event-Target: 'Miqo'te…', 'Roegadyn…'" — ABER mit kaputten
  Symbolen dran, und…
- **…„Zurück"-Spam:** Nach jeder Volk-Ansage feuerte der generische Update-
  Handler `FindFocusedText` → Collision-Heuristik fand immer den Zurück-Button
  (Key=19004) → sagte „Zurück" und überdeckte das Volk. Deshalb hörte der User
  bei Hoch/Runter nur „Zurück".

### Neu in V4.13: RaceGender komplett dediziert
- `_CharaMakeRaceGender` ist jetzt in SpecialUpdateAddons → beide generischen
  Pfade (Update = Zurück-Spam, ReceiveEvent) sind aus.
- Dedizierter `OnRaceGenderReceive` (MouseOver): sagt das Volk via Event-Target
  an, gesäubert mit CleanRaceName („Miqo'te\t glyphs" → „Miqo'te").
- `OnRaceGenderUpdate` (Geschlecht) bleibt unverändert.
- Erwartung: Hoch/Runter (Volk wählen) → gewählter Zustand ändert sich →
  OnRaceGenderUpdate sagt „Volk, Geschlecht"; Maus-Hover → sauberes Volk;
  Links/Rechts → Geschlecht. Kein „Zurück"-Spam mehr.

### V4.13-Testergebnisse (Log 22:24/22:25) — ERFOLG
- Kein „Zurück"-Spam mehr. ✓
- Völker sauber angesagt: „Hyuran", „Elezen", „Lalafell", „Miqo'te"
  (CleanRaceName inkl. Apostroph korrekt). ✓
- Volk+Geschlecht bei Auswahl: „Lalafell, männlich/weiblich" schaltet um. ✓
- Symbol-Codepoints geloggt: id4(als männlich)=C2 AE (®, U+00AE),
  id3(als weiblich)=C2 A9 (©, U+00A9).

### V4.14: Geschlecht aus Vorschau-Charakter (Daten-Weg statt Symbol)
Statt die kaputten Symbole zu deuten, liest das Plugin jetzt das echte
`Sex`-Byte (0=männlich, 1=weiblich, FFXIV-Standard) des Vorschau-Charakters
aus der Dalamud-Objekttabelle (`Character.Sex`). Symbol-Zuordnung bleibt nur
als Fallback. Cross-Check wird geloggt (Vorschau-Sex vs Symbol).
- Recherche-Fakten: `CustomizeData` hat Sex@1/Race@0/Tribe@4 (0/1 Bytes);
  `Character.Sex` (geerbt von GameObject) direkt lesbar; kein sauberer Live-
  Zeiger auf die CharaMake-Customize (AgentLobby/CharacterManager) → daher
  Vorschau-Charakter über ObjectTable.
- **PROBE im Build:** Beim Öffnen von RaceGender werden ALLE Objekte einmal
  geloggt (`CharaMake-Objekt[idx]: 'Name' Kind=.. Sex=..`), um den Vorschau-
  Charakter zu identifizieren. HYPOTHESE: Vorschau = ObjectKind.Pc.

### Beim nächsten Start testen (V4.14)
1. Ladeansage „Version 4 Punkt 14 bereit".
2. Charaktererstellung → Volk & Geschlecht: Geschlecht mit Links/Rechts
   wechseln → wird männlich/weiblich angesagt und stimmt es jetzt?
3. WICHTIG: Log ansehen — steht dort `CharaMake-Objekt[..] Kind=Pc Sex=..`?
   Falls die Objektliste LEER ist oder kein Pc dabei: Vorschau ist nicht in
   der ObjectTable → anderer Weg nötig (ClientObjectManager direkt).
4. Danach Log an Claude.

### DANACH (zugesagt): Lumina ID→Name einrichten
IDataManager durchreichen, Excel-Sheets nutzen (Race, Item, Action …) um
IDs in saubere Namen zu übersetzen statt kaputten UI-Text zu lesen. Kommt
nach dem Vorschau-Charakter-Test, um nicht 2 ungetestete Brocken zu stapeln.

### (V4.12 war) Geschlechts-Ansage in der Charaktererstellung
- Dedizierter PostUpdate-Handler `OnRaceGenderUpdate` für `_CharaMakeRaceGender`.
- Liest den ECHTEN Auswahlzustand statt die kaputten Symbole zu deuten:
  jedes Volk (Comp CT=Base) hat zwei Geschlechts-Checkboxen — Node id=4
  (männlich-Symbol) und id=3 (weiblich-Symbol). `AtkComponentCheckBox.IsChecked`
  (verifiziert per ilspycmd) verrät das gewählte Geschlecht.
- Ansage bei Änderung: Volkwechsel → „Viera, weiblich"; nur Geschlecht
  (Links/Rechts) → „männlich"/„weiblich". Volksname wird von Symbolen
  gesäubert (CleanRaceName: nur Buchstaben/Leer/Apostroph bis zum Tab).
- **ANNAHME (Test!):** id=4 = männlich, id=3 = weiblich (FFXIV-Konvention
  männlich zuerst). Codepoints beider Symbole werden EINMAL geloggt
  (`RaceGender-Symbole: id4(als männlich)='..' id3(als weiblich)='..'`),
  damit der Testlauf die Zuordnung bestätigt.
- **HYPOTHESE (Test!):** Das gewählte Volk hat eine checked Geschlechts-
  Checkbox. Falls im Log keine `RaceGender gewählt`-Zeile erscheint, stimmt
  das nicht → Zustand liegt woanders (dann neu ansetzen).
- Encoding-Trap behoben: In UIReaderService.cs war in CleanRaceName ein
  U+2000-Space statt 0x20 gerutscht (Edit-Matching scheiterte) — per awk
  byte-sicher ersetzt.

### Beim nächsten Start testen (V4.12)
1. Ladeansage „Version 4 Punkt 12 bereit".
2. Charaktererstellung → Volk & Geschlecht: durch die 8 Völker gehen
   (werden sie angesagt? — V4.11-Fix) und mit Links/Rechts das Geschlecht
   wechseln → wird „männlich"/„weiblich" angesagt?
3. WICHTIG: Einmal auf ein dir bekanntes Geschlecht stellen und sagen, was
   angesagt wurde → so bestätigen wir die männlich/weiblich-Zuordnung.
4. Danach Log an Claude („schau in die log").

## (vorher) Version 4.11

### V4.10-Testergebnisse (Log 21:43–21:45) — vieles bestätigt
- **Fenstertitel funktioniert:** „Name des Charakters" (_CharaMakeCharaName),
  „Weltenauswahl" (_CharaSelectWorldServer) werden angesagt. ✓
- **SelectYesno echte Labels:** „Die Charaktererschaffung abbrechen? … 
  Buttons=[Ok|Abbrechen]". ✓
- **Callback-Zuordnung BESTÄTIGT:** ButtonClick param=1 (Abbrechen) → nichts;
  param=0 (Ok) → zurück zur Charakterauswahl. Also id=8/„Ok" = Index 0 stimmt. ✓
- **ABER Charaktererstellung stumm:** In `_CharaMakeRaceGender` (8 Völker:
  Viera, Hrothgar, Au Ra, Roegadyn, Miqo'te, Lalafell, Elezen, Hyur) feuern
  MouseOver param 1–8, aber NICHTS wird angesagt.

### Root Cause (gefunden) → Fix in V4.11
- `_CharaMakeRaceGender`: jedes Volk ist ein Comp(1003)-Node; dessen Kind
  id=4 trägt ein statisches Collision-Bit (0x10). FindFocusedText Durchlauf 2
  (Collision-Fallback, eig. für DC gedacht) matchte deshalb IMMER dasselbe
  Volk — unabhängig vom MouseOver → Dedup unterdrückte alles.
- **V4.11-Fix:** Bei MouseOver(6)/ButtonClick(25) hat jetzt der Event-Target-
  Pointer VORRANG (TryAnnounceEventTarget gibt bool zurück); nur wenn er nicht
  mappt, greift die Flag-Heuristik FindFocusedText. Das ist das verifizierte
  DC-Muster, jetzt als bevorzugter Pfad für alle Addons.

### Offene Annahme / evtl. nächster Schritt
- Ob der Event-Target bei RaceGender auf die richtige Komponente zeigt und
  „Viera" etc. sauber vorliest, zeigt erst der nächste Log. Der Volkstext
  enthält im Dump kaputte Geschlechtssymbole („Viera\t © ®") — Encoding
  ggf. später säubern. Erst Log ansehen, dann entscheiden.
- CMFSlider/CMFIcon* (Aussehen-Regler) sind der übernächste Brocken.

### Frühere bestätigte Ergebnisse
- V4.6: Welten-Ansage nach DC-Klick; Geplapper in Charaktererstellung weg.
- V4.7: F2 / `/acc win`. V4.8: F5 dumpt alle fokussierten Fenster.
- V4.10: Fenstertitel + echte Dialog-Knöpfe + Klick queued statt unterbricht.
- Noch offen: Taste für den Ok-Knopf im DC-Fenster.
- Hinweis: F2 = Spiel-Standardtaste „Gruppenmitglied 2" (im Kampf ggf. Konflikt).

### Beim nächsten Start testen
1. Ladeansage „Version 4 Punkt 11 bereit".
2. Charaktererstellung öffnen, mit Pfeiltasten/Maus durch die 8 Völker:
   werden sie jetzt einzeln angesagt (Viera, Hrothgar, …)?
3. Falls ja: klingt der Volksname sauber oder mit Zusatz-Zeichen?
4. Weitere Regler/Icons (CMFSlider, CMFIcon*) durchgehen — was kommt?
5. Danach Log an Claude („schau in die log").

### Wichtige Betriebs-Fakten
- Dalamud lädt das Plugin DIREKT aus `H:\ff14\FF14Accessibility\bin\Debug\net10.0-windows\`
  (DevPluginLoadLocations in dalamudConfig.json). Der devPlugins-Ordner ist NICHT
  die Ladequelle. **Nach jedem Build: Spiel komplett neu starten.**
- Build: `$env:DALAMUD_HOME = "C:\...\XIVLauncher\addon\Hooks\dev"; dotnet build H:\ff14\FF14Accessibility\FF14Accessibility.csproj`
- Versionskennung: EINE Konstante in Plugin.cs (`PluginVersion`/`PluginVersionTag`)
  speist Log-Zeile UND Sprachansage. Bei jeder Code-Änderung hochzählen — nur so
  ist zweifelsfrei erkennbar, welcher Build im Spiel läuft.
- **F5** = Node-Dump des aktiven Addons → `FFXIV_UI_Dump.txt` auf dem Desktop.
  Bestes Diagnose-Werkzeug: User drückt F5 im fraglichen UI-Zustand.
- Git: Letzter Commit 2026-05-30, große ungecommittete Änderungen im Arbeitsverzeichnis.
- UIReaderService.cs hat gemischte Zeichenkodierung (alte Umlaute als kaputte
  Bytes). Bei Edits: old_strings ohne Umlaute wählen. Neue Sprach-Strings nur
  über `AccessibilityStrings.cs` (sauberes UTF-8, DE/EN).

## Heute behoben (2026-07-09) — Details

### 1. Debug-Deploy kopierte nie die neue DLL (csproj)
`ResolvedOutDir` wurde aus `$(OutDir)` in einer statischen PropertyGroup berechnet —
bei SDK-Projekten ist `$(OutDir)` dort noch leer. Es wurde nur die Manifest-JSON
kopiert, die Erfolgsmeldung prüfte bloß die Existenz irgendeiner DLL am Ziel.
Fix: Berechnung IM Target; Meldung basiert auf `CopiedFiles`; nichts kopiert = Build-Fehler.
(Anmerkung: Da Dalamud ohnehin aus bin/ lädt, war das nicht die Ursache des
Hauptproblems — aber der Deploy ist jetzt ehrlich.)

### 2. IsReadable()/VirtualQuery war IMMER kaputt (UIReaderService)
MEMORY_BASIC_INFORMATION war mit Size=44 statt 48 deklariert (4 Padding-Bytes
fehlten). Windows lehnt zu kleine Puffer mit ERROR_BAD_LENGTH ab → `IsReadable()`
gab seit Einführung für JEDEN Pointer false zurück. Alle abgesicherten Lesepfade
(ReadListItemText, DC-Zuordnung, …) liefen still auf Leer-Fallbacks.
Bewiesen per Standalone-Repro (dwLength=44 → ret=0/err=24; 48 → ok).
**Lehre: Sicherheitsnetz-Funktionen brauchen einen Positivtest. Und: stille
Frühausstiege sind verboten — jeder Exit loggt seinen Grund (hat hier einen
kompletten Testlauf gekostet).**

### 3. DC-Auswahl innerhalb einer Region — BESTÄTIGT funktionierend (Log 19:44)
Links/Rechts zwischen Light/Chaos wird angesagt. Mechanik: MouseOver-Event
(Typ 6) → `AtkEvent->Target` per Pointer-Vergleich gegen die beim Öffnen
gesammelten DC-Buttons (Comp 1015) matchen, Text frisch vom Node lesen.
WICHTIG: `AtkEvent->Node` ist bei diesem Addon 0x0 — immer BEIDE Pointer
(Node UND Target) prüfen. Event-Parameter sind KEINE Node-IDs (Regionen:
1=Japan, 7=Europa, 13=Ozeanien, 19=Nordamerika als Fallback-Map verifiziert;
DC-Buttons z.B. 9/10 in Europa, undokumentiert → nur Pointer-Match nutzen).

### 4. Gesprochene Version war hartcodiert „4.1"
Zweiter, separater String neben der Log-Zeile. Jetzt eine gemeinsame Konstante.

### 5. Log-Spam entfernt (V4.5) + Fokus-Dedup pro Addon (V4.6)
- Titelmenü loggte 60 Zeilen/s (per-Frame-Debug in AnnounceTitleMenuFocusIfChanged) — entfernt.
- Charaktererstellung: mehrere gleichzeitig sichtbare Addons (CMFSlider, CMFIcon*)
  überschrieben den GLOBALEN Fokus-Dedup-Zustand gegenseitig → jede Ansage feuerte
  jeden Frame (Dauergeplapper). Fix: `_lastFocusByAddon` Dictionary pro Addon-Name,
  Aufräumen bei Addon-Close. TEST AUSSTEHEND.

## Neu in V4.6: Welten-Ansage nach DC-Klick (TEST AUSSTEHEND)

Node-Dump 2026-07-09 (Zustand nach Klick auf Light) hat die Struktur geklärt:
- Im Region-Panel steht in der NodeList direkt VOR jedem DC-Button (Comp 1015)
  dessen Welten-Liste (Comp 1019): vor „Light" Alpha..Zodiark, vor „Chaos"
  Cerberus..Spriggan. Die Listen werden erst beim Öffnen der Region befüllt.
- Im Panel gibt es einen „Ok"-Button (Comp 1006) zum Bestätigen.
Implementierung: ButtonClick (Event-Typ 25) → `TryAnnounceDCSelection` matcht
per Pointer, liest Welten FRISCH aus der 1019-Liste (nie cachen), Ansage via
`AccessibilityStrings.DCSelected`. Region-Klicks/Ok-Klicks landen im Log als
„ButtonClick not mapped" — normal.

## UIReaderService – Architektur

### Menü-Stack
- `_menuStack` (Stack): PostSetup = Push + vorlesen, PreFinalize = Pop,
  PostUpdate nur fürs oberste Element (SelectedItemIndex-Änderung → ansagen),
  PostReceiveEvent = Fallback für Nicht-Listen-Addons.
- Fokus-Dedup: `_lastFocusByAddon` (pro Addon-Name; NIE wieder global).

### Spezial-Addons
- Talk, SelectYesno: eigene Handler, kein Stack-Eintrag.
- SelectString/SelectIconString: eigener PostSetup-Handler (Fragetext + Liste),
  dann Stack + universelles Update.
- TitleDCWorldMap: eigener Handler-Satz (s.o.), `IsDCMapOpen` für Plugin.cs.
- ConfigSystem: Rückwärts-Scanning (`HasFocusBit` bei Child-Nodes) für Slider,
  Dropdowns, Checkboxen; eigener Text-Cache.
- Benachrichtigungen: eigene Handler.

### Navigation (Plugin.cs)
- Pfeiltasten nur bei `HasActiveMenu`; Spiel navigiert nativ, Update-Hook sagt an.
- Enter/Escape: SuppressKey + FireCallback (kein Doppel-Trigger).
- Controller: D-Pad Links/Rechts → NavigateGamepad(±1) für SelectYesno.
- Nummernblock 2/4/6/8: Navigation im DC-Fenster (ForceDCMapRead setzt nur Dedup zurück).

## Werkzeuge & Diagnose
- F5: Node-Dump aktives Addon → Desktop `FFXIV_UI_Dump.txt` (+ ins Log).
- Dalamud-Log: `C:\Users\brued\AppData\Roaming\XIVLauncher\dalamud.log` —
  DC-Zeilen mit Präfix `[DC]`, ConfigSystem `[CS]`.
- `/acc dump <AddonName>`: Dump per Chat-Befehl.
- ilspycmd (global installiert): Dalamud.dll/FFXIVClientStructs.dll dekompilieren.
- VirtualQuery-Testprogramm u.a. im Claude-Scratchpad (Session-spezifisch, ggf. neu anlegen).

## Chat-Befehle
- /acc win — aktives Fenster ansagen (wie F2)
- /acc set — aktuell anvisiertes Ziel verfolgen
- /acc nav — Richtung + Distanz ansagen
- /acc near — Objekte in der Nähe auflisten
- /acc ui — aktuelles Menü vorlesen
- /acc stop — Sprache stoppen
- /acc help — Hilfe

## Tastenbelegung
- F1 — Kontext-Hilfe
- F2 — aktives Fenster ansagen + sichtbare Fenster ins Log (Diagnose)
- F5 — Node-Dump des fokussierten Addons auf Desktop (Diagnose)
- F6 — Richtung + Distanz zum Ziel
- F7 — aktuelles Spielziel verfolgen
- F8 — Zielverfolgung beenden
- F9 — Objekte in der Nähe
- F10 — aktuelles Menü vorlesen
- F11 — Sprache stoppen
- F12 — HP/MP-Status ansagen
- Nummernblock 2/4/6/8 — Navigation in der Datenzentrums-Auswahl

## Umgebung
- .NET 10 SDK; Dalamud ApiLevel 15, DalamudPackager 15.0.0 (csproj)
- `DALAMUD_HOME` = `C:\Users\brued\AppData\Roaming\XIVLauncher\addon\Hooks\dev`
- Tolk.dll + nvdaControllerClient64.dll liegen im Output-Verzeichnis
- NVDA als Screenreader

## Weitere Features (geplant)
- [ ] Charaktererstellung vollständig zugänglich (NÄCHSTER GROSSER BROCKEN)
- [ ] Cooldowns ansagen (ActionManager.GetRecastGroupDetail via FFXIVClientStructs)
- [ ] Audio-Beacon (Stereo-Panning zum Ziel)
- [ ] Auktionshaus/Marktplatz vollständig lesbar machen
- [ ] Alle Untermenüs (Ausrüstung, Inventar) — testen ob universeller Handler reicht
- [ ] Zielverfolgung per Name über Chat-Befehl
