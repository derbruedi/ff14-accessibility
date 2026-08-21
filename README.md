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
- Charaktererstellung, Schritt **Aussehen**: die rund zwanzig Wähler
  (Gesicht, Frisur, Farben, Statur …) werden einzeln angesagt; Strg+F10
  liest das gesamte Aussehen am Stück zurück. Keiner dieser Wähler ist im
  Spiel Text — ein Screenreader findet dort sonst nichts vor.
  (Beitrag von [bladestorm360](https://github.com/bladestorm360), PR #4)
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
  abschaltbar im Einstellungsmenü (Umschalt+F9, Punkt „Chat-Kanäle") —
  eine flache Liste, eine Zeile je Kanal. Im gewohnten Chatsystem sind das
  Dialoge, Sagen, Rufen, Gruppe, Allianz, Flüstern, Freie Gesellschaft,
  System, Sammeln und Beute; im neuen die Kanäle des Spiels selbst. Ein
  abgeschalteter Kanal wird nur nicht mehr vorgelesen — nachlesen kannst du
  ihn weiterhin.
- **Schreiben**: beim Öffnen der Chat-Eingabe wird der aktive Kanal
  angesagt („Chat-Eingabe, Sagen"), ein Kanalwechsel während des Tippens
  ebenso. Jedes getippte Zeichen wird gesprochen, ebenso Gelöschtes —
  denn das Eingabefeld des Spiels liest ein Screenreader nicht.
- **Nachlesen**: ein Verlaufs-Browser mit acht getrennten Kategorien
  (Dialoge, Sagen, Rufen, Gruppe, Allianz, Flüstern, Freie Gesellschaft,
  System) — ohne Mengenbegrenzung, die ganze Sitzung bleibt erhalten. So
  lässt sich Verpasstes in Ruhe nachhören, ohne den laufenden Chat zu stören.
  Innerhalb einer Kategorie springt man mit einer Taste an den Anfang oder
  ans Ende, weil ein Kampf-Verlauf in die Tausende Zeilen geht.
- **Antworten**: Enter im Nachlese-Browser beantwortet die gerade gelesene
  Nachricht im richtigen Kanal — ein gelesenes Flüstern wird direkt
  beantwortet, ohne den Namen abtippen zu müssen.
- **Zweites Chatsystem zur Wahl**: neben dem gewohnten Verlauf gibt es einen
  zweiten, dessen Puffer den **Registerkarten und Filtern des Spiels**
  folgen — also dem, was ein sehender Spieler in seinen Chat-Reitern
  eingerichtet hat. Die Reiter lassen sich per Taste umschalten (das Spiel
  selbst hat dafür keine). Umschaltbar im Einstellungsmenü (Umschalt+F9);
  **voreingestellt ist das gewohnte System**. Mitgeschrieben wird immer in
  beide Verläufe, ein Wechsel mitten in der Sitzung reißt also keine Lücke.
  Zusätzlich zu „Chat-Kanäle" gibt es in diesem System den Punkt
  „Chat-Register": dort geht es feiner — Register, darin die Kanäle, darin
  die Filterzeilen des Spiels, wo sich etwa ausgeteilter von erlittenem
  Schaden trennen lässt.
  (Beitrag von [bladestorm360](https://github.com/bladestorm360), PR #5)

### Navigation und Laufen

- **Objekt-Browser**: mit einer Taste durch Objekte in der Nähe blättern
  (NPCs, Händler, Gegner, **Verbündete**, Spieler, Objekte, **Inhalte**,
  **Alle Inhalte**, Quest-Ziele, Freibriefe, FATEs, Sammelpunkte,
  Angelplätze, Ätheryten, Kartenwegpunkte wie Zonen-Ausgänge). Ansage mit
  Name, Art, Entfernung und Richtung; das Objekt wird gleichzeitig
  anvisiert.
- Bei einem **Fang-Freibrief** („besänftige das Ziel mit dem Emote
  *Beruhigen*") sagt die Ansage **„schon gezähmt"**, wenn du diesen Gegner
  bereits hattest, und **„rasend, nicht zähmbar"**, wenn ein Versuch an ihm
  misslungen ist. Beide rutschen ans Ende der Liste, die noch offenen stehen
  vorn. Das gilt für **jeden** Fang, unabhängig von Monster und Freibrief — das
  Spiel führt dafür nur einen einzigen Zustand. Ein gezähmter Gegner
  verschwindet nicht und sieht aus wie jeder andere; ohne den Hinweis läufst du
  zu ihm und erfährst es erst an der Abweisung des Spiels.
- Die Kategorie **Verbündete** sammelt alles, was auf deiner Seite kämpft:
  Trupp-NPCs, Gruppe und Allianz, Karfunkel, Fee, Begleitchocobo.
  **Inhalte** listet nur die Türen, die in einen Dungeon, eine Prüfung,
  einen Raid oder eine PvP-Instanz führen — eine solche Tür ist ein Ziel,
  kein Möbelstück. (Beitrag von
  [bladestorm360](https://github.com/bladestorm360), PR #3)
- Die Kategorie **Alle Inhalte** geht darüber hinaus: sie listet **jeden
  Dungeon-, Prüfungs- und Raid-Eingang des Spiels**, nach Stufe sortiert —
  auch die in anderen Gebieten. Angesagt werden Name, Art, Stufe und, wenn
  der Inhalt noch nicht freigeschaltet ist, ein „gesperrt" (das fragt das
  Spiel selbst, es wird nicht aus deiner Stufe geraten). Liegt der Eingang
  in deinem Gebiet, kommen Entfernung und Richtung dazu, sonst der Name des
  Gebiets und der nächste Zonenübergang dorthin. **Numpad 3 läuft hin** —
  über Gebietsgrenzen hinweg, Übergang für Übergang, genau wie bei einem
  Quest-Ziel in einer anderen Zone.
- Auch **Einrichtung** in Wohngebieten ist auffindbar: Chocobo-Stall,
  Briefkasten, Beete. Objekte, die das Spiel nur mit einem Symbol statt mit
  einem Wort beschriftet, bekommen das Wort, das die Oberfläche dafür
  benutzt.
- **Peil-Ton**: sobald du etwas **anvisiert** hast, zeigt ein Ton
  die Richtung dorthin — die Seite über das Stereobild, „hinter dir" über
  eine dunklere Tonlage, die Entfernung über die Lautstärke (näher =
  lauter). **Jede Zielart klingt anders**: Gegner, NPC, Objekt,
  Sammelpunkt, Übergang, Ätheryt, Quest-Ziel und Inhalts-Eingang haben je
  einen eigenen Grundton, Gegner und Aufträge zusätzlich einen Doppelschlag.
  Je genauer du zielst, desto weiter gehen die Schläge auseinander — und
  **wenn du richtig stehst, verstummt er ganz**; ein kurzer Quittungston
  beim Einrasten sagt dir, dass die Stille gewollt ist und nichts kaputt.
  Genau dafür ist er bei Aufzügen und Plattformen da: drehen, bis es still
  wird. Eine bloße Auswahl im Objekt-Browser löst noch keinen Ton aus —
  erst das Anvisieren; für Ziele, die das Spiel gar nicht anvisieren lässt
  (Quest-Ziele, Kartenmarker, Dungeon-Eingänge in der Liste), führt ihn die
  **Gehhilfe**. Schalter: **Strg+Umschalt+F9**, Lautstärke im
  Einstellungsmenü. Alle Töne lassen sich mit `/acc soundtest` anhören.
- **Gehhilfe**: geführtes manuelles Laufen entlang des Wegenetzes, um
  Hindernisse herum — mit Wegpunkt-Tönen, Richtungsansagen relativ zur
  Blickrichtung und Ankunftston.
- **Auto-Lauf**: automatisch zum Ziel laufen (benötigt das Fremd-Plugin
  vnavmesh), mit Routen-Vorschau, Fortschrittsansagen und ehrlicher
  Meldung, wenn kein Weg gefunden wird.
- **Aufgezeichnete Spuren**: Stellen, die das Wegenetz nicht kennt, lassen
  sich einmal selbst ablaufen und aufzeichnen. Bleibt der Auto-Lauf dort
  später hängen, benutzt er die eigene Spur, statt aufzugeben.
- **Wohngebiete**: dort ist das Wegenetz oft älter als die Häuser, weil es
  gebaut wird, während das Spiel die Grundstücke noch nachlädt — der
  Auto-Lauf lief dann in Zäune. Das Netz wird jetzt einmal je Besuch neu
  gebaut, sobald das Spiel meldet, dass das Wohngebiet vollständig geladen
  ist; das Warten wird angesagt.
- **Folgen**: dem anvisierten Ziel dauerhaft hinterherlaufen — hält an,
  wenn das Ziel stehen bleibt, und endet bei Zonenwechsel oder wenn das
  Ziel verschwindet (benötigt ebenfalls vnavmesh).
- **Routen-Vorschau**: den Weg ansagen lassen, ohne zu laufen
  („Weg zu Ätheryt, 62 Meter: 25 Meter nach Norden, dann …").
- **Himmelsrichtung**: beim Drehen wird die Blickrichtung angesagt
  (Norden, Nordosten …), abschaltbar.
- **Koordinaten**: eigene Karten-Koordinaten in die Zwischenablage
  kopieren (zum Weitergeben im Chat) oder zu kopierten Koordinaten
  hinlaufen.
- Zielwechsel-Ansagen für die Spiel-eigenen Zieltasten (Tab, F1–F12).

### Kampf

- Kampfstatus auf Tastendruck: eigene HP als Zahl („HP 4523 von 5100") —
  so, wie das Spiel sie selbst im Partyfenster anzeigt und wie sich
  entscheiden lässt, ob ein Trank reicht. MP bleibt prozentual, weil das
  Maximum seit Patch 5.0 für jede Klasse 10000 ist.
- Ziel-HP in Stufen (Prozent — eine Zahl zeigt das Spiel für Gegner nie),
  kurzer Ton beim Anvisieren eines Gegners.
- Beim Blättern durch Gegner werden **Stufe und HP** mitgesagt; die Stufe
  steht auf der Ziel-Leiste eines sehenden Spielers ebenfalls. Das HP-Format
  und diese Ansage stammen aus PR #1 von
  [bladestorm360](https://github.com/bladestorm360).
- **Wirkfläche im Tooltip**: die Beschreibung einer Aktion nennt jetzt auch
  die **Form** des Wirkbereichs (Kreis, Kegel, Linie …) — das Spiel nennt
  im Text nur die Reichweite und zeichnet die Form. (Beitrag von
  [bladestorm360](https://github.com/bladestorm360), PR #2)
- **Zauber-Warnung**: **jeder** Zauber deines anvisierten Gegners wird
  angesagt — im Bosskampf also der ganze Ablauf des Bosses. Dazu jeder
  Zauber, der **auf dich** zielt, auch von einem Gegner daneben; dann fällt
  sein Name mit. Zielt der Zauber auf dich, sagt die Ansage das ausdrücklich
  dazu. Zauber auf andere Spieler bleiben still.
- **Form und Grösse der Fläche**: hängt an der Zauber-Ansage, sobald der
  Zauber eine Fläche auf den Boden legt — „Kegel, 90 Grad, 6 Meter",
  „Linie, 30 Meter", „Kreis um dich, 5 Meter". Damit weisst du, **wohin**
  du ausweichen musst und wie weit. Formen, die dieses Projekt nie
  nachgemessen hat, bleiben still statt zu raten.
- **Warnton für Schadensflächen** (AoE): ein Ton, der durchhält, solange du in
  einer angekündigten Fläche stehst; er verstummt, sobald du heraustrittst.
  Die Form (Kreis, Kegel, Linie) kommt aus den Daten des jeweiligen Zaubers.
  Standardmäßig ausgeschaltet, siehe Tastenübersicht.
- **Klang und Lautstärke der Warnung sind einstellbar** (Einstellungen →
  Töne). Vier Klänge stehen zur Wahl: *Hell* (der frühere Ton), *Weich*,
  *Tiefes Brummen* und *An- und abschwellend*. Jeder wird beim Anwählen
  sofort kurz vorgespielt, du entscheidest also am Ohr. Alle vier halten
  durch, solange die Gefahr besteht — auch der schwellende reißt nie ab, damit
  er nicht mit den Schlägen des Peil-Tons zu verwechseln ist.
- **Vorwarnung „du stehst drin"**: stehst du beim Beginn eines Zaubers in
  seiner Fläche, sagt die Ansage es mit dazu — samt der Zeit, die dir noch
  bleibt („Du stehst drin, 3 Sekunden."). Läufst du erst während des
  Zaubers hinein, kommt „Achtung, du stehst drin, 2 Sekunden." Gehört zur
  Flächenwarnung und ist deshalb mit ihr zusammen ein-/ausschaltbar.
- **Feine Ziel-Lebenspunkte im Freibrief**: Solange ein Freibrief läuft, wird
  unterhalb von 30 Prozent alle 5 Prozent angesagt statt nur bei 25 und 10.
  Fang-Aufträge wollen den Gegner *geschwächt*, nicht besiegt — mit den groben
  Stufen ist dieses Fenster kaum zu treffen. Im Einstellungsmenü abschaltbar.
- **Sonderaktionen im Auftrag**: Manche Aufträge blenden eine kleine
  Extra-Leiste ein (fangen, betäuben, ein Gerät auslösen), die erst
  auftaucht, wenn sie gebraucht wird. Das Spiel bietet sie **nur per
  Mausklick** an — im Tastenbelegungs-Dump hat sie keine einzige Belegung.
  Der Mod sagt mit einem Ton an, sobald sie da ist, und legt sie auf
  Umschalt+F10 und Umschalt+F11; Strg+Umschalt+F8 sagt sie noch einmal an.
- **Fähigkeit bereit**: Ton und Name, sobald eine Fähigkeit wieder
  einsatzbereit ist (`/acc cd`).
- HP und MP zusätzlich als Stereo-Töne (bei jeder 10-Prozent-Stufe zeigt
  die Stereo-Position den Füllstand an).
- Erfahrungsgewinn und Beute werden angesagt und in der Nachlese archiviert.
- **Beute-Verlosungen**: offene Würfe der Gruppe vorlesen (mit den
  Ausrüstungswerten des Gegenstands, damit sich Bedarf oder Gier überhaupt
  entscheiden lässt) und per Taste in das Verlosungs-Fenster springen, um
  dort mit dem Nummernblock zu wählen.
- **Erholungsbonus**: „Ruhebereich. Erholungsbonus sammelt sich." beim
  Betreten, und auf Tastendruck die Höhe in Prozent einer Stufe.
- SP-Stand für Sammler (Sammelpunkte/GP) auf Tastendruck.
- Stufe und fehlende Erfahrung auf Tastendruck.

### Inventar und Ausrüstung

- Item-Slots in Tasche, Charakterfenster und Arsenal werden mit Name,
  Stufe und Tragbarkeit angesagt („Bronzegladius, Stufe 5, tragbar" /
  „nicht tragbar, ab Stufe 26"); leere Felder sagen „Leer".
- Läden: an jede Ware wird Stufe und Tragbarkeit angehängt.
- **Werte statt bloßer Namen**: Gegenstandsstufe, Verteidigung und
  Attribute stehen in der Ansage; zu welchen Klassen ein Teil passt, wird
  mit **deinen eigenen** Klassen ausgesprochen („für deine Klassen Ritter,
  Gladiator") statt mit der Abkürzungsliste des Spiels.
- **Warnung vor dem Verkaufen**: gehört ein Teil zu einem Ausrüstungsset,
  sagt die Ansage beim Durchgehen „, im Ausrüstungsset" mit. Das Spiel
  malt diesen Hinweis sonst nur als Symbol auf das Icon — ein Textleser
  bekommt ihn nie zu sehen.
- Angelegte Ausrüstung komplett vorlesen; empfohlene Ausrüstung mit dem
  Spiel-eigenen Optimierer anlegen.
- Inventar und Gil auf Tastendruck.

### Aktionsleisten (Hotbars)

- Aktionsleiste vorlesen: welche Taste löst welche Fähigkeit aus.
- **Zuweisungs-Menü**: alle gelernten Fähigkeiten des aktuellen Jobs per
  Tastatur durchblättern und auf eine beliebige der 10 Leisten legen —
  komplett ohne Maus. Ansagen nennen die tatsächlich gebundene Taste
  (z. B. „Leiste 2, Taste Strg+3").
- Im selben Menü lassen sich auch **Gegenstände** ablegen: Tränke,
  Elixiere und Essen aus der Tasche, mit Bestand in der Ansage
  („Heiltrank, 12 Stück").

### Tiefe Gewölbe (Palast der Toten und Verwandte)

Dieser ganze Abschnitt ist ein Beitrag von
[bladestorm360](https://github.com/bladestorm360) (PR #6).

Ein Tiefes Gewölbe ist in sich geschlossen, und der Objekt-Browser stellt
sich darin um: statt sechzehn Weltkategorien gibt es vier Antworten.

- **Kategorien drinnen**: Gegner (aufgedeckte Fallen zählen mit — das Spiel
  führt sie selbst als Gegner), Truhen, die beiden Leuchten und die Räume.
- **Räume statt Objekte**: Der Inhalt einer Ebene wird beim Content-Director
  des Spiels abgefragt, nicht in der Objekttabelle. Deshalb verschwindet ein
  Ziel nicht mehr, sobald du weit genug weggehst, dass das Spiel es entlädt —
  und Räume lassen sich anlaufen, nicht nur benennen.
- **Raumwechsel** wird beim Betreten angesagt; ein sehender Spieler liest
  seine Position fortlaufend von der Gewölbe-Karte ab.
- **Welches Gewölbe, welche Ebene** auf Tastendruck (Strg+F) — die Zahl, in
  der der ganze Lauf gemessen wird und die das Spiel nur beiläufig nennt.
- **Charakterinfo**: das Fenster nennt seine Plätze mit Namen, Beschreibung
  und Anzahl. Es besteht fast nur aus Symbolen ohne Text, sagte deshalb
  bisher nur seinen eigenen Titel an.
- **Ebenenweite Wirkungen** (Leuchten-Effekte, Fallen, Ring-Bonus) werden
  erfasst. Sie liegen nicht in der Statusliste der Figur, sondern auf dem
  Director — der bisherige Effekt-Puffer konnte sie gar nicht sehen.

> Die Gewölbe-Funktionen sind noch **nicht im Spiel gegengeprüft**. Falls
> Strg+F bei dir zusätzlich die Spielfunktion „zum Ziel drehen" auslöst,
> lässt sich die Taste in den Einstellungen ändern.

### Sonstiges

- Emote-Browser: Emotes durchblättern und ausführen.
- Bestiarium (Jagdtagebuch) vorlesen, inklusive Lebensraum der Monster.
- **Angeln**: Angelplätze im Gebiet finden und ansteuern.
- **Sammeln**: Erz- und Holzvorkommen finden; das Sammelfenster wird
  vorgelesen.
- **Reittiere**, **Läden der Staatlichen Gesellschaft** und die
  **Charakterkonfiguration** sind bedienbar.
- **Tauschfenster** (Marken, Zertifikate): jede Zeile nennt den Gegenstand,
  seinen Preis samt Währung, deinen eigenen Bestand und die Beschreibung.
- **Vermögen**: jede Zeile sagt, um welche Währung es geht — „49.457 Gil",
  „1.652/10.000 Legionstaler". Vorher standen dort nur nackte Zahlen neben
  einem Symbol.
- **Errungenschaften**: beim Öffnen kommen Punktestand und Zertifikate
  („350 Errungenschaftspunkte, 1 Errungenschaftszertifikat"); dieselbe
  Auskunft gibt es jederzeit, wenn du das Symbol im Fenster anfährst.
- **Triple Triad**: Spielbrett und eigene Hand vorlesen.
- Beim Anmelden bleibt es ruhig: während das Spiel seine Fenster aufbaut,
  schweigen die automatischen Ansagen, damit sie sich nicht gegenseitig
  abschneiden.
- **Benachrichtigungen**: eingehende Einladungen (Freie Gesellschaft,
  Gruppe, Freundesliste) per Taste annehmen — das Popup lässt sich sonst
  nur mit der Maus bedienen.
- **Plugin-Liste**: die installierten Dalamud-Plugins per Tastatur
  durchblättern (Dalamuds eigenes Fenster ist nicht vorlesbar).
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

**Der Installer aktualisiert auch sich selbst** (ab Installer-Version 1.1).
Liegt eine neuere Installer-Version vor, fragt er nach:

1. Es erscheint eine Ja/Nein-Abfrage mit der Downloadgröße. „Ja" holt die
   neue Version, „Nein" arbeitet mit der vorhandenen weiter.
2. Bei „Ja" lädt er sie, schließt sich kurz und öffnet sich automatisch
   wieder — die Datei an deinem Speicherort wird dabei ersetzt, du musst
   also nichts von Hand herunterladen.
3. Nach dem Neustart meldet er „Der Installer wurde auf Version … 
   aktualisiert" und führt die Installation von selbst weiter aus. Ein
   Bestätigen mit Enter genügt.

Falls die Datei nicht ersetzt werden kann (z. B. wegen Schreibschutz),
sagt er das und macht trotzdem normal weiter.

### Was der Installer macht

- Prüft, ob **XIVLauncher** installiert ist, und bietet sonst an, das
  offizielle Setup herunterzuladen und zu starten.
- Kopiert die Plugin-Dateien in Dalamuds `devPlugins`-Ordner und aktiviert
  sie direkt in `dalamudConfig.json` (mit Sicherungskopie).
- Bietet an, das **vnavmesh**-Plugin (für den Auto-Lauf) vom Original
  herunterzuladen. vnavmesh stammt von einem anderen Autor und wird
  **nicht** von diesem Projekt mitverteilt.

## Tastenübersicht (Standard)

Diese Liste ist gegen die tatsächliche Tastenbelegung im Code abgeglichen —
alle aufgeführten Tasten sind aktiv. Die Tasten sind so gewählt, dass sie
laut Spiel-Tastenbelegung überwiegend frei sind; einige liegen bewusst auf
rein visuellen Kamera-Funktionen (siehe unten). Strg+F1 sagt jederzeit die
aktuelle Hilfe an. Alle Tasten lassen sich über die Einstellungen ändern.

### Objekte finden

- **Bild-ab** — nächstes Objekt ansagen und anvisieren
- **Bild-auf** — vorheriges Objekt
- **Strg+Bild-ab** — Objekt-Kategorie vorwärts (NPCs, Händler, Gegner,
  Verbündete, Spieler, Objekte, Inhalte, Alle Inhalte, Quest-Ziele,
  Freibriefe, FATEs, Sammelpunkte, Angelplätze, Ätheryten, Wegpunkte; im
  Tiefen Gewölbe stattdessen Truhen, Leuchten, Räume)
- **Strg+Bild-auf** — Objekt-Kategorie zurück

### Laufen und Führung

- **Nummernblock 3** — Auto-Lauf zum gewählten Ziel an/aus (braucht vnavmesh)
- **Strg+Nummernblock 3** — Gehhilfe an/aus (Ton-Führung beim manuellen
  Laufen, folgt dem Wegenetz um Hindernisse)
- **+** — dem anvisierten Ziel fortlaufend folgen an/aus (braucht vnavmesh).
  Gemeint ist die normale Plus-Taste, **nicht** die des Nummernblocks
- **Strg+Umschalt+F9** — Peil-Ton an/aus
- **Strg+Nummernblock 5** — Routen-Vorschau: Weg ansagen, ohne zu laufen
- **Strg+Umschalt+F1** — zu Koordinaten aus der Zwischenablage laufen
  (z. B. „24.1 21.0" kopieren, dann Taste)
- **Strg+Umschalt+F2** — eigene Karten-Koordinaten in die Zwischenablage
  kopieren
- **Nummernblock 5** — einmal in die Richtung drehen, in die die Gehhilfe
  weist
- **Strg+Umschalt+F6** — Spur aufzeichnen an/aus (eine Stelle, die das
  Wegenetz nicht kennt, einmal selbst ablaufen)
- **N** — Himmelsrichtungs-Ansage beim Drehen an/aus
- **F** — zum Ziel hindrehen (Spiel-Taste), **W** — laufen (Spiel-Taste)

### Vorlesen und Information

- **Strg+F1** — Hilfe (Tasten und Befehle)
- **Strg+F2** — aktives Fenster ansagen
- **Strg+F10** — aktuelles Menü vorlesen; bei offenem Journal: Quest vorlesen
- **Strg+F11** — Sprache sofort stoppen
- **Strg+Entf** — Kampfstatus: eigene HP und MP
- **Strg+Ende** — SP-Stand (Sammelpunkte/GP für Sammler)
- **Strg+L** — Stufe und fehlende Erfahrung
- **Umschalt+L** — Ruhebereich und Erholungsbonus
- **Strg+F** — Tiefes Gewölbe: welches Gewölbe, welche Ebene
- **Strg+Umschalt+F7** — Aufgabenliste des laufenden Inhalts vorlesen
  (Freibrief, Dungeon, FATE): genau die Zeilen, die am Bildschirmrand
  stehen, mit Zähler bzw. Restzeit
- **Umschalt+F9** — Einstellungsmenü öffnen (gesprochen bedienbar)
- **Strg+F3** — Inventar vorlesen (Tasche und Schlüsselgegenstände)
- **Umschalt+F3** — Gil-Stand
- **Strg+F4** — Bestiarium (Jagdtagebuch) vorlesen
- **Strg+F12** — offene Benachrichtigung/Einladung annehmen

### Kampf

- **Strg+Umschalt+F3** — Warnton für Schadensflächen an/aus.
  **Standardmäßig aus**, weil die Formerkennung im Spiel noch nicht
  abschließend bestätigt ist — ein falscher Warnton im Kampf wäre
  schlimmer als keiner
- **Umschalt+F7** — offene Beute-Verlosungen vorlesen
- **Umschalt+F8** — in das Verlosungs-Fenster springen (dort wählt der
  Nummernblock Bedarf, Gier oder Passen). Bewusst eine eigene Taste: ein
  Fenster, das sich mitten im Kampf den Fokus greift, würde den
  Nummernblock schlucken, während man noch laufen muss

### Ausrüstung

- **Strg+F6** — angelegte Ausrüstung vorlesen (mit Stufe und Werten)
- **Strg+F7** — empfohlene Ausrüstung anlegen (Spiel-eigener Optimierer)
- **Strg+F8** — zufälliges Aussehen (nur in der Charaktererstellung)

### Aktionsleisten belegen

- **Strg+F9** — erste Aktionsleiste vorlesen (was liegt auf Taste 1 bis 0)
- **Strg+Nummernblock 0** — Zuweisungs-Menü öffnen bzw. schließen

Im geöffneten Zuweisungs-Menü steuert der Nummernblock; die Tasten werden
so lange vom Spiel ferngehalten, damit die Figur nicht losläuft:

- **Nummernblock 8 / 2** — in der Liste blättern
- **Nummernblock 4 / 6** — zwischen **Fähigkeiten** und **Gegenständen**
  wechseln (Tränke, Elixiere, Essen aus der Tasche)
- **Nummernblock 0** — auswählen; danach die Zieltaste wählen und erneut
  Nummernblock 0 zum Ablegen
- **Nummernblock Komma** — einen Schritt zurück bzw. Menü schließen

### Chat nachlesen

- **Alt+Bild-auf** / **Alt+Bild-ab** — Kategorie zurück / vor (Dialoge,
  Sagen, Rufen, Gruppe, Allianz, Flüstern, Freie Gesellschaft, System,
  Beute); angesagt wird der Name mit der Anzahl der Nachrichten
- **Umschalt+Bild-auf** / **Umschalt+Bild-ab** — in der gewählten Kategorie
  zur älteren / neueren Nachricht blättern („3 von 12: …")
- **Umschalt+Pos1** / **Umschalt+Ende** — an den Anfang / ans Ende der
  Kategorie springen
- **Alt+Pos1** / **Alt+Ende** — Chat-Registerkarte des Spiels umschalten
  (das Spiel selbst hat dafür keine Taste — ein Sehender klickt das
  Register an)
- **Enter** — die gerade gelesene Nachricht im richtigen Kanal beantworten

### Emotes

- **Umschalt+F4** / **Umschalt+F5** — Emote zurück / vor
- **Umschalt+F6** — gewähltes Emote ausführen

### Triple Triad (Kartenspiel)

- **Strg+Umschalt+F4** — das Spielbrett vorlesen
- **Strg+Umschalt+F5** — die eigene Hand vorlesen

### Plugin-Liste

- **Umschalt+F1** / **Umschalt+F2** — nächstes / vorheriges installiertes
  Plugin ansagen
- **Umschalt+F12** — Einstellungen des gewählten Plugins öffnen

### Diagnose

- **Strg+F5** — UI-Dump des aktuellen Fensters auf den Desktop speichern
  (hilft bei Fehlerberichten)

### Überschneidungen mit Spiel-Tasten

Einige Plugin-Tasten liegen auf Funktionen, die das Spiel ebenfalls belegt.
Das ist bewusst so; beim Anmelden wird die Zahl der Überschneidungen
angesagt:

- **Bild-auf / Bild-ab** sind zusätzlich Kamera-Zoom
- **Strg+Ende** ist zusätzlich „Kamera-Einstellung speichern"
- **Nummernblock 5** ist zusätzlich „Kamera auf das Ziel richten"; diese
  Taste hält das Plugin vom Spiel fern, damit die Kamera nicht zusätzlich
  springt

Diese Funktionen sind rein visuell und damit für blindes Spiel folgenlos.
Steigt die angesagte Zahl gegenüber dem, was du kennst, lohnt ein Blick in
die Datei `FFXIV_Keybinds.txt` auf dem Desktop: dann überschneidet sich eine
Plugin-Taste mit einer echten Spielfunktion. Der eine Punkt, der dort noch
offen ist, ist **Strg+F** (Tiefes Gewölbe): der Tastendump führt Strg+F als
frei, das bloße **F** ist aber „zum Ziel drehen" — ob das Spiel bei Strg+F
trotzdem mitdreht, ist noch nicht im Spiel gemessen.

## Chat-Befehle

Viele Funktionen gibt es auch als Befehl:

- `/acc help` — Hilfe ansagen
- `/acc nav` — Richtung und Entfernung zum Ziel ansagen
- `/acc set` — aktuelles Ziel verfolgen
- `/acc clear` — Ziel aufheben
- `/acc near` — Objekte in der Nähe auflisten
- `/acc status` — HP und MP ansagen
- `/acc ui` — aktuelles Menü vorlesen
- `/acc win` — aktives Fenster ansagen
- `/acc keys` — Spiel-Tastenbelegung auf den Desktop speichern
- `/acc stop` — Sprache stoppen
- `/acc fish` — Angelplätze im Gebiet ansagen
- `/acc fishhere` — aktuellen Standort als Auswurfstelle merken
- `/acc gather` — Sammelpunkte im Gebiet ansagen
- `/acc gathergo` — zum nächsten Sammelpunkt laufen
- `/acc trails` — aufgezeichnete Spuren im Gebiet auflisten
- `/acc cd` (auch `/acc cooldowns`) — Ansage „Fähigkeit bereit" an/aus
- `/acc soundtest` — die Töne des Plugins zur Probe abspielen
- `/acc lang de|en|auto` — Sprache der Plugin-Ansagen umstellen
- `/acc dump <Fenstername>` — Fensterstruktur auf den Desktop speichern

## Sprache

Die Ansagen des Plugins gibt es auf **Deutsch und Englisch**. Ohne
Einstellung richtet sich die Sprache nach Windows; mit `/acc lang de`,
`/acc lang en` oder `/acc lang auto` lässt sie sich jederzeit umstellen.
Spieltexte (Dialoge, Menüs, Gegenstandsnamen) werden immer in der Sprache
des Spiel-Clients vorgelesen. Entwickelt und getestet wird vorrangig mit dem
deutschen Client.

## Mitwirkende

Sechs größere Funktionen dieses Plugins stammen von
**[bladestorm360](https://github.com/bladestorm360)**:

- **PR #1** — Stufe und HP beim Blättern durch Gegner; eigene HP wieder als
  Zahl statt als Prozentwert
- **PR #2** — die Form der Wirkfläche im Aktions-Tooltip (Kreis, Kegel,
  Linie …)
- **PR #3** — die Objekt-Kategorien **Verbündete** und **Inhalte**
- **PR #4** — der Schritt **Aussehen** in der Charaktererstellung
- **PR #5** — das zweite Chatsystem, dessen Puffer den Registerkarten und
  Filtern des Spiels folgen
- **PR #6** — die **Tiefen Gewölbe** (Räume, Truhen, Leuchten, Charakterinfo,
  ebenenweite Wirkungen)

Vielen Dank dafür.

## Hinweise

- Dieses Plugin läuft über **Dalamud/XIVLauncher**, das außerhalb der
  offiziellen Nutzungsbedingungen von Square Enix liegt. Die Nutzung
  erfolgt auf eigene Verantwortung.
- **vnavmesh** ist ein eigenständiges Fremd-Plugin
  ([github.com/awgil/ffxiv_navmesh](https://github.com/awgil/ffxiv_navmesh))
  und wird hier nur verlinkt bzw. nachgeladen, nicht mitverteilt.

## Lizenz

Dieses Projekt steht unter der **GNU Affero General Public License, Version 3**
(`LICENSE`) — derselben Lizenz wie Dalamud und wie die offizielle
Plugin-Vorlage von goatcorp. Du darfst das Plugin benutzen, verändern und
weitergeben; wer eine veränderte Fassung verbreitet oder über ein Netzwerk
anbietet, muss deren Quellcode ebenfalls offenlegen.

Mitgelieferte Fremdsoftware und ihre Lizenzen stehen in
`THIRD-PARTY-NOTICES.md` — das sind **Tolk** (LGPL-3.0), der
**NVDA Controller Client** (LGPL-2.1) und **NAudio** (MIT). Diese Datei liegt
auch im heruntergeladenen Archiv und muss bei einer Weitergabe dabeibleiben.

## Für Entwickler

- Plugin-Quellcode: `FF14Accessibility/`
- Installer-Quellcode: `Installer/`
- Custom Plugin Repository (für sehende Helfer, optionaler Weg): `repo.json`
- Projektstand und Testprotokoll: `STATUS.md`
- Verifizierte Spiel-Interna: `docs/game-api.md`
