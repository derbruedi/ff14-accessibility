using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using DirectorRoomFlags = FFXIVClientStructs.FFXIV.Client.Game.InstanceContent.InstanceContentDeepDungeon.RoomFlags;

namespace FF14Accessibility.Services;

/// <summary>
/// DIE EBENE SELBST: welche Raeume es gibt, welche davon der Spieler aufgedeckt hat, in
/// welchem Raum er steht, und was laut Spiel in jedem davon ist.
///
/// WARUM DAS NICHT TEIL DES OBJEKT-BROWSERS IST. Der Browser kann nur anbieten, was in
/// der laufenden Objekttabelle steht, und das Spiel entlaedt, wovon der Spieler weggeht.
/// Genau das meldet der User: *"one can be close enough to a treasure or enemy to see it
/// but then navigate too far away from it to get to it."* Der Content-Director hat dieses
/// Problem nicht - er traegt den Inhalt der Ebene, ob etwas geladen ist oder nicht - der
/// RAUM ist also die stabile Einheit, und der Objekt-Browser bleibt, was er ist (es wird
/// nie etwas zu einem entladenen Objekt geroutet).
///
/// ALLES HIER WIRD GELESEN, NICHTS ABGELEITET:
/// <list type="bullet">
/// <item><c>MapData</c> - 25 x <c>RoomFlags</c>: ConnectionN/S/W/E, Return, Passage,
///   Home, Revealed. Der spieleigene Datensatz je Raum.</item>
/// <item><c>Party</c> - 4 x {EntityId, RoomIndex}: in welchem Raum jedes Mitglied ist,
///   die spieleigene Antwort auf "wo bin ich".</item>
/// <item><c>Chests</c> - 16 x {ChestType, RoomIndex}: jede Truhe auf der Ebene mit dem
///   Raum, in dem sie steht.</item>
/// <item><c>ActiveLayoutIndex</c> + <c>DeepDungeonMap5X</c> - das 5x5-Raster des
///   Ebenen-Layouts (61 Layouts, 5 Unterzeilen x 5 Spalten DeepDungeonRoom-Referenzen,
///   0 = kein Raum in dieser Zelle; Offline-Auszug 2026-08-12).</item>
/// <item><c>AgentDeepDungeonMap.Data</c> - <c>Map[25]</c> und
///   <c>RoomIndex[25]</c>+<c>RoomIndexCount</c>: was das Kartenfenster selbst
///   zeichnet.</item>
/// </list>
///
/// DAS EINE UNGEKLAERTE, UND WIE DIESE KLASSE DAMIT UMGEHT. Nichts Statisches sagt, ob
/// der <c>RoomIndex</c> des Directors RASTERZELLEN (0-24) oder RAEUME innerhalb des
/// Layouts (1-21) zaehlt - und das ist nicht geklaert. Diese Klasse sagt deshalb nur, was
/// so oder so gilt (die eigenen Flags des Raumes, seine Truhen, ob der Spieler darin
/// steht), und behandelt eine RASTERPOSITION als Behauptung, die sich verdienen muss:
/// <see cref="RoomPosition"/> gibt nichts zurueck, bis ein Live-Vergleich dem Sheet
/// zustimmt. <see cref="LogSnapshot"/> schreibt das ganze Bild einmal je Ebene ins Log,
/// damit die Frage durch gewoehnliches Spielen beantwortet wird und nicht durch einen
/// Sondenlauf.
///
/// GLEICHSTELLUNG: <see cref="RevealedRooms"/> filtert auf das spieleigene
/// <c>Revealed</c>-Flag. Raeume aufzulisten, die der Spieler nicht aufgedeckt hat, hiesse
/// ihm eine Karte zu geben, die kein sehender Spieler hat - Nebel des Krieges und
/// unentdeckte Gebiete sind Spiellogik und bleiben verborgen.
/// </summary>
public sealed class DeepDungeonFloor
{
    private readonly IDataManager       _data;
    private readonly IPluginLog         _log;
    private readonly DeepDungeonState   _state;
    private readonly DeepDungeonRoomMap _rooms;
    private readonly IClientState       _clientState;

    public DeepDungeonFloor(IDataManager data, IPluginLog log, DeepDungeonState state,
                            DeepDungeonRoomMap rooms, IClientState clientState)
    {
        _data        = data;
        _log         = log;
        _state       = state;
        _rooms       = rooms;
        _clientState = clientState;
    }

    /// <summary>
    /// Wo ein Raum liegt, aus den spieleigenen Layout-Daten, oder null, wenn die nicht
    /// verfuegbar oder fuer diese Ebene nicht bestaetigt sind - dann faellt der Aufrufer
    /// auf die Brotkrume unten zurueck. Siehe <see cref="DeepDungeonRoomMap"/>.
    /// </summary>
    public unsafe System.Numerics.Vector3? RoomPosition(int room)
    {
        var dd = _state.GetDirector();
        return dd == null
            ? null
            : _rooms.PositionOf((ushort)_clientState.TerritoryType, dd->ActiveLayoutIndex, room);
    }

    /// <summary>Prueft die Layout-Lesart gegen den Ort, an dem der Spieler wirklich
    /// ist.</summary>
    public unsafe void VerifyRoomPositions(uint playerEntityId, System.Numerics.Vector3 playerPos)
    {
        var dd = _state.GetDirector();
        if (dd == null) return;
        _rooms.Verify((ushort)_clientState.TerritoryType, dd->ActiveLayoutIndex,
                      PlayerRoom(playerEntityId), playerPos);
    }

    /// <summary>Ein Raum der aktuellen Ebene, so wie das Spiel ihn festhaelt.</summary>
    /// <param name="Index">Der Raumindex des Directors selbst - dieselbe Zahl, die in
    /// <c>Chests[].RoomIndex</c> und <c>Party[].RoomIndex</c> auftaucht, sie ist also
    /// ueber alles hinweg stimmig, was diese Klasse sagt, auch wenn nicht geklaert ist,
    /// was sie zaehlt.</param>
    /// <param name="Flags">Die Flags des Spiels fuer den Raum, woertlich.</param>
    /// <param name="Chests">Wie viele Truhen der Director in diesen Raum legt.</param>
    /// <param name="IsPlayerRoom">Ob der Spieler darin steht.</param>
    public readonly record struct Room(int Index, DirectorRoomFlags Flags, int Chests, bool IsPlayerRoom)
    {
        public bool HasReturnCairn  => Flags.HasFlag(DirectorRoomFlags.Return);
        public bool HasPassageCairn => Flags.HasFlag(DirectorRoomFlags.Passage);
        public bool IsStart         => Flags.HasFlag(DirectorRoomFlags.Home);
    }

    // ── Brotkrumen: wo der Spieler jeden Raum betreten hat ──
    //
    // User, 2026-08-12: *"rooms need to be navigable once entered."* Der Director sagt, in
    // welchem Raum etwas ist, und NICHT, wo der Raum liegt - es gibt also keine Koordinate
    // zum Hinlaufen, es sei denn, der Spieler war schon dort, und dann gibt es eine, die
    // ueberhaupt keine Folgerung braucht: die Stelle, auf der er stand, als er hereinkam.
    //
    // DAS IST NICHT DAS, WAS AUSGESCHLOSSEN WURDE. Ausgeschlossen war das Routen zu
    // ENTLADENEN OBJEKTEN ("that's a quick way to break things") - also das Wiederbeleben
    // eines Griffs, den das Spiel fallen gelassen hat. Eine Brotkrume ist ein Punkt, den
    // der Spieler selbst eingenommen hat; sie liegt bauartbedingt auf dem Wegenetz und ist
    // mit seinem jetzigen Standort ueber den Weg verbunden, den er gelaufen ist. Es wird
    // nichts wiederbelebt und nichts abgeleitet.
    //
    // Bei jedem Ebenenwechsel geloescht, denn die Ebene wird neu erzeugt, und Raum 6 der
    // naechsten Ebene liegt ganz woanders.

    private readonly Dictionary<int, System.Numerics.Vector3> _entryPoints = new();

    /// <summary>
    /// Merkt sich, wo der Spieler stand, als er einen Raum betrat. Nur die ERSTE Position
    /// je Raum wird behalten: das ist die Tuer, durch die er kam, und die ist vom Gang aus
    /// genauso erreichbar wie von innen.
    ///
    /// ES WARTET, BIS DER SPIELER WIRKLICH IM RAUM IST. Der Director veroeffentlicht den
    /// Raum der neuen Ebene, BEVOR der Spieler darauf gesetzt wurde, die erste Probe jeder
    /// Ebene war deshalb die Warteposition, auf der er waehrend des Ladens stand - dieselben
    /// (0, -292) Ebene um Ebene, Hunderte Yalm ausserhalb des Gewoelbes. Daher kam das
    /// *"285-yalm destination on a floor 200 across"*: die Raumzeile bot die Ladestelle als
    /// Laufziel an.
    ///
    /// Die Pruefung ist exakt statt eine Entfernung: bei platzierten Raummodulen muss der
    /// Spieler dem Modul des Raumes am naechsten sein, in dem er laut Director ist. Wo das
    /// Layout ueberhaupt nicht lesbar ist, wird der Punkt wie zuvor genommen - dieser Fall
    /// ist nicht schlechter als frueher und immer noch besser als gar keine Route.
    /// </summary>
    public void RememberEntry(int room, System.Numerics.Vector3 position)
    {
        if (room < 0 || _entryPoints.ContainsKey(room)) return;

        var at = RoomAt(position);
        if (at >= 0 && at != room) return;   // noch nicht auf der Ebene - oder nicht in dem Raum

        _entryPoints[room] = position;
        _log.Info($"[DeepFloor] Raum {room} betreten - Rueckweg gemerkt: {position}.");
    }

    /// <summary>Wo der Spieler einen Raum betreten hat, oder null, wenn nie.</summary>
    public System.Numerics.Vector3? EntryPoint(int room)
        => _entryPoints.TryGetValue(room, out var p) ? p : null;

    /// <summary>Eine Truhe, so wie der Director sie festhaelt.</summary>
    /// <param name="ChestType">Das spieleigene Typ-Byte. WELCHER Wert welche Farbe ist,
    /// steht in <see cref="ColourOf"/>.</param>
    /// <param name="RoomIndex">Der Raum, in dem sie steht.</param>
    public readonly record struct Chest(byte ChestType, int RoomIndex);

    /// <summary>Ob der Spieler gerade in einem Tiefen Gewoelbe ist.</summary>
    public unsafe bool IsActive => _state.GetDirector() != null;

    /// <summary>
    /// Ob die Raum-Flags ueberhaupt gemeldet werden duerfen, LIVE bei jedem Aufruf
    /// geprueft.
    ///
    /// DIE PRUEFUNG, und warum sie die richtige ist. <c>MapData</c> sind 25
    /// Raum-Datensaetze und <c>RoomIndex</c> (auf <c>Party</c> und <c>Chests</c>) ist ein
    /// vorzeichenbehaftetes Byte, und nichts Statisches sagt, dass die beiden derselbe
    /// Indexraum sind - offen ist, ob dieser Index Rasterzellen (0-24) oder Raeume
    /// innerhalb des Layouts (1-21) zaehlt. Sind es NICHT dieselben Raeume, dann gehoert
    /// jedes Flag, das diese Klasse meldet, zu einem anderen Raum als dem, den sie nennt,
    /// und ein blinder Spieler hat keine Moeglichkeit, das zu bemerken.
    ///
    /// Der Spieler steht in einem Raum, das Spiel muss diesen Raum also als aufgedeckt
    /// markiert haben. Sagen die Flags an seinem eigenen Index etwas anderes, widersprechen
    /// sich die beiden Arrays, und alles daraus Abgeleitete bleibt ungesagt. Es kostet
    /// einen Array-Zugriff und widerlegt die Annahme in dem Moment, in dem sie falsch ist.
    /// </summary>
    public unsafe bool FlagsTrustworthy(uint playerEntityId)
    {
        var dd = _state.GetDirector();
        if (dd == null) return false;

        var here = PlayerRoom(playerEntityId);
        var mapData = dd->MapData;
        if (here < 0 || here >= mapData.Length) return false;

        if (mapData[here].HasFlag(DirectorRoomFlags.Revealed)) return true;

        // Eine Zeile je Ebene, nicht eine je Tastendruck: der Aufrufer fragt bei jedem
        // Blaettern.
        if (_lastMismatchFloor != dd->Floor)
        {
            _lastMismatchFloor = dd->Floor;
            _log.Warning($"[DeepFloor] Raumindex {here} ist in MapData NICHT als aufgedeckt markiert "
                         + $"(Flags {mapData[here]}) - MapData und RoomIndex sind offenbar verschiedene "
                         + "Indexraeume. Raumangaben bleiben stumm.");
        }
        return false;
    }

    private int _lastMismatchFloor = -1;

    /// <summary>Die Ebenennummer, oder 0 ausserhalb eines Tiefen Gewoelbes.</summary>
    public unsafe int Floor
    {
        get
        {
            var dd = _state.GetDirector();
            return dd == null ? 0 : dd->Floor;
        }
    }

    /// <summary><c>Addon</c>-Zeile fuer das spieleigene Wort des Gewoelbes fuer eine
    /// Ebene. Nur die Id - das Wort wird aus dem Sheet in der Sprache des Clients gelesen.
    /// Es steht im eigenen Block des Ergebnisschirms ("Results", "Floor", "Kills",
    /// "Score"), ist also die Beschriftung, die das Spiel dieser Zahl selbst gibt.</summary>
    private const uint AddonFloor = 10440;

    /// <summary>
    /// Welches Tiefe Gewoelbe das ist und welche Ebene davon, als eine Zeile Sprache -
    /// oder null, wenn der Spieler in keinem ist.
    ///
    /// User, 2026-08-12: *"we need a key to check dungeon floor, as I don't think every
    /// floor is announced in the system log."* Er hat recht damit, dass sie nicht
    /// verlaesslich angesagt wird, und die Ebene ist die eine Zahl, die alles Weitere hier
    /// entscheidet: wann der Boss faellig ist, ob sich die Suche nach der Wegleuchte
    /// lohnt, wie weit ein Wipe den Lauf zuruecksetzen wuerde.
    ///
    /// Beide Woerter kommen aus dem Spiel: der Name des Gewoelbes aus
    /// <c>DeepDungeon.Name</c> ("the Palace of the Dead", "Heaven-on-High", "Eureka
    /// Orthos", "Pilgrim's Traverse") und das Hauptwort aus der <c>Addon</c>-Zeile, mit
    /// der der Ergebnisschirm diese Zahl beschriftet.
    /// </summary>
    public unsafe string? DescribeFloor()
    {
        var dd = _state.GetDirector();
        if (dd == null) return null;

        var dungeon = _data.GetExcelSheet<DeepDungeon>()?.GetRowOrDefault(dd->DeepDungeonId)
                           ?.Name.ExtractText().Trim() ?? string.Empty;
        var word    = _data.GetExcelSheet<Addon>()?.GetRowOrDefault(AddonFloor)?.Text.ExtractText().Trim()
                      ?? string.Empty;

        _log.Info($"[DeepFloor] Ebenen-Ansage: Gewoelbe {dd->DeepDungeonId} '{dungeon}', Ebene {dd->Floor}, "
                  + $"Aetherpool Waffe={dd->WeaponLevel} Ruestung={dd->ArmorLevel}.");
        return AccessibilityStrings.DeepFloorLine(dungeon, word, dd->Floor);
    }

    /// <summary>
    /// Der Raum, in dem der Spieler steht, oder -1, wenn das Spiel nichts dazu sagt.
    ///
    /// Aus <c>Party</c> ueber die eigene EntityId des Spielers gelesen und nicht aus
    /// Platz 0: das Gruppen-Array ist nicht nach "ich zuerst" geordnet, und in einer vollen
    /// Gruppe ist Platz 0 der Raum von jemand anderem.
    /// </summary>
    public unsafe int PlayerRoom(uint playerEntityId)
    {
        var dd = _state.GetDirector();
        if (dd == null) return -1;

        var party = dd->Party;
        for (var i = 0; i < party.Length; i++)
            if (party[i].EntityId == playerEntityId)
                return party[i].RoomIndex;

        return -1;
    }

    /// <summary>Jede Truhe, die der Director fuer diese Ebene fuehrt.</summary>
    public unsafe List<Chest> Chests()
    {
        var chests = new List<Chest>();
        var dd = _state.GetDirector();
        if (dd == null) return chests;

        var array = dd->Chests;
        for (var i = 0; i < array.Length; i++)
        {
            // Ein RoomIndex von -1 ist ein leerer Platz: das Array ist fest auf 16 und die
            // Ebene fuellt es selten. Typ 0 wird NICHT als Leer-Test benutzt, denn 0 kann
            // durchaus ein echter Truhentyp sein.
            if (array[i].RoomIndex < 0) continue;
            chests.Add(new Chest(array[i].ChestType, array[i].RoomIndex));
        }
        return chests;
    }

    /// <summary>
    /// Raeume, die das Spiel NICHT als aufgedeckt markiert hat, deren Position aber aus
    /// den Layout-Daten bekannt ist - also dort, wo es noch Ebene zu erkunden gibt.
    ///
    /// WARUM DAS NICHT DER OBEN BEWACHTE BRUCH DER GLEICHSTELLUNG IST, und die
    /// Unterscheidung stammt vom User selbst (2026-08-12): *"a sighted player would be able
    /// to see what's not explored on the map, so even if not a room, just nearest unexplored
    /// space."* Die Nur-Aufgedecktes-Regel gibt es, damit das Plugin keine Karte des
    /// INHALTS der Ebene herausgibt, die ein sehender Spieler sich nicht verdient hat -
    /// welche Truhen, welche Leuchte, welcher Ausgang. Davon sagt das hier nichts. Es sagt
    /// nur, dass in einer Richtung unerforschter Raum liegt, und genau das sagt der leere
    /// Teil der Gewoelbe-Karte jedem, der hinsehen kann.
    ///
    /// Ein unerforschter Raum wird also als ZIEL angeboten und sonst nichts: keine
    /// Truhenzahl, keine Leuchte, keine Ausgaenge. Was darin ist, bleibt verborgen, bis das
    /// Spiel es aufdeckt.
    /// </summary>
    public unsafe List<int> UnexploredRooms(uint playerEntityId)
    {
        var rooms = new List<int>();
        var dd = _state.GetDirector();
        if (dd == null || !FlagsTrustworthy(playerEntityId)) return rooms;

        var mapData = dd->MapData;
        for (var i = 0; i < mapData.Length; i++)
        {
            if (mapData[i].HasFlag(DirectorRoomFlags.Revealed)) continue;
            if (RoomPosition(i) == null) continue;   // keine Koordinaten, nichts anzubieten
            rooms.Add(i);
        }
        return rooms;
    }

    /// <summary>
    /// Die Raeume, die der Spieler aufgedeckt hat, in der Indexreihenfolge des Directors,
    /// mit dem Raum des Spielers zuerst, sofern es einen gibt.
    ///
    /// <paramref name="playerEntityId"/> darf 0 sein (kein Spieler), dann wird keine Zeile
    /// als die des Spielers markiert.
    /// </summary>
    public unsafe List<Room> RevealedRooms(uint playerEntityId)
    {
        var rooms = new List<Room>();
        var dd = _state.GetDirector();
        if (dd == null || !FlagsTrustworthy(playerEntityId)) return rooms;

        var here     = PlayerRoom(playerEntityId);
        var chests   = Chests();
        var mapData  = dd->MapData;

        for (var i = 0; i < mapData.Length; i++)
        {
            var flags = mapData[i];
            if (!flags.HasFlag(DirectorRoomFlags.Revealed)) continue;
            rooms.Add(new Room(i, flags, chests.Count(c => c.RoomIndex == i), i == here));
        }

        // Der eigene Raum des Spielers fuehrt die Liste an: nach ihm wird meistens
        // gefragt, und er gibt dem Rest der Liste einen Anker.
        return rooms.OrderByDescending(r => r.IsPlayerRoom).ThenBy(r => r.Index).ToList();
    }

    /// <summary><c>Addon</c>-Zeilen fuer die drei spieleigenen Truhennamen. Nur Ids - die
    /// Woerter selbst werden immer aus dem Sheet in der Sprache des Clients gelesen
    /// (geprueft: 10420 "Gold Coffer", 10421 "Silver Coffer", 10422 "Bronze
    /// Coffer").</summary>
    private const uint AddonGoldCoffer   = 10420;
    private const uint AddonSilverCoffer = 10421;
    private const uint AddonBronzeCoffer = 10422;

    /// <summary>
    /// Das spieleigene Farbwort zu einem Truhentyp, oder null, wenn die Farbe dieses Typs
    /// nicht belegt ist.
    ///
    /// **ALLE DREI SIND INZWISCHEN AUS DEN SPIELEIGENEN DATEN BELEGT.** Die Farben sind
    /// ueberhaupt keine Eigenschaft des Truhentyps - sie sind eine Eigenschaft des
    /// OBJEKTS, und das Spiel benennt seine eigenen Truhen-Requisiten in den Gewoelben,
    /// die nach dem Palast der Toten gebaut wurden. Diese benennen drei
    /// <c>ExportedSG</c>-Eintraege eindeutig (Offline-Auszug
    /// <c>tools/deepdungeon-dump coffers</c>, 2026-08-12; acht Zeilen, keine widerspricht
    /// einer anderen):
    ///
    /// <code>
    ///   sgbg_w_lvd_008_01a.sgb  "bronze coffer"  EObjName 2009532, 2014743
    ///   sgbg_w_lvd_009_01a.sgb  "silver coffer"  EObjName 2008882, 2009531, 2014742
    ///   sgbg_w_lvd_015_01a.sgb  "gold coffer"    EObjName 2009530, 2012936, 2014741
    /// </code>
    ///
    /// Die Truhen des Palasts der Toten heissen alle "treasure coffer", und deshalb hat
    /// das vier Sitzungen gedauert - aber es sind DIESELBEN REQUISITEN:
    ///
    /// <list type="bullet">
    /// <item><c>EObj 2007357</c> -> <c>sgbg_w_lvd_009_01a</c> -> <b>silbern</b>, und das
    ///   ist <c>ChestType</c> 2. **Das ist die Gegenprobe, die den Rest hier
    ///   rechtfertigt**: dass Typ 2 silbern ist, war auf einem voellig unabhaengigen Weg
    ///   schon bekannt - eine zu oeffnen hob den Aetherpool, und
    ///   <c>DescriptionString</c> 819 sagt *"Aetherpool gear can be enhanced by accessing
    ///   SILVER coffers"* - und der Modell-Weg landet ungefragt auf derselben
    ///   Antwort.</item>
    /// <item><c>EObj 2007358</c> -> <c>sgbg_w_lvd_015_01a</c> -> <b>golden</b>, und das ist
    ///   <c>ChestType</c> 3. Das ist die haeufige Truhe der Ebene, und genau die hoerte
    ///   der User als silbern bezeichnet: *"I proved this by obtaining a chest labelled as
    ///   a silver chest and it was gold."*</item>
    /// <item><c>ChestType</c> 1 ist ueberhaupt kein <c>EObj</c> - es ist ein
    ///   <c>ObjectKind.Treasure</c>-Objekt, <c>Treasure</c>-Zeilen 783/784, die
    ///   <c>sgbg_w_tbx_001_01a</c> setzen. Diese SGB und die bronzene
    ///   <c>sgbg_w_lvd_008_01a</c> setzen **dieselben zwei Modelldateien**
    ///   (<c>w_tbx_001_01a.mdl</c> und <c>w_tbx_001_01b.mdl</c>) - eine Requisite, zwei
    ///   Huellen, und die spaeteren Gewoelbe nennen diese Requisite die <b>bronzene</b>
    ///   Truhe.</item>
    /// </list>
    ///
    /// Die Ueberlegung des Users war also richtig, und die fruehere Weigerung war an der
    /// falschen Stelle uebervorsichtig: es gibt drei Truhenfarben (<c>Addon</c>
    /// 10420-10422), drei Truhentypen wurden gesehen, und jeder haengt jetzt an der
    /// Benennung des Spiels selbst statt an einem Ausschlussverfahren. Wirklich abwesend
    /// war - und bleibt - jeder REGELTEXT, der Gold oder Bronze nennt; jede Beschreibung,
    /// die das Spiel zeigt, wurde ausgelesen, und nur Silber wird darin genannt. Nur auf
    /// die Prosa zu schauen war der Fehler.
    /// </summary>
    public string? ColourOf(byte chestType)
    {
        var addon = chestType switch
        {
            1 => AddonBronzeCoffer,
            2 => AddonSilverCoffer,
            3 => AddonGoldCoffer,
            _ => 0u,
        };
        if (addon == 0) return null;

        var word = _data.GetExcelSheet<Addon>()?.GetRowOrDefault(addon)?.Text.ExtractText().Trim();
        return string.IsNullOrEmpty(word) ? null : word;
    }

    /// <summary>
    /// Der Raum, dessen Modul dem gegebenen Punkt am naechsten liegt, oder -1, solange die
    /// Layout-Daten fuer diese Ebene nicht lesbar sind.
    ///
    /// Das ist die fehlende Verbindung: ein Director-Eintrag nennt einen RAUM, ein
    /// Truhen-Objekt hat eine POSITION - mit platzierten Raeumen treffen sich beide. Es ist
    /// dieselbe Naechstes-Modul-Regel, die <see cref="DeepDungeonRoomMap.Verify"/> auf den
    /// Spieler selbst anwendet.
    ///
    /// Es liest die Module OHNE auf jenes Urteil zu warten, und der Unterschied ist
    /// wichtig: diese Antwort dient Pruefungen und dem Log, nie als Laufziel. Laufziele
    /// gehen ueber <see cref="RoomPosition"/>, und das bleibt abgesichert.
    /// </summary>
    public unsafe int RoomAt(System.Numerics.Vector3 point)
    {
        var dd = _state.GetDirector();
        if (dd == null) return -1;

        var best  = float.MaxValue;
        var found = -1;
        for (var cell = 0; cell < 25; cell++)
        {
            var pos = _rooms.ModulePosition((ushort)_clientState.TerritoryType, dd->ActiveLayoutIndex, cell);
            if (pos is not { } p) continue;
            var d = System.Numerics.Vector2.Distance(new System.Numerics.Vector2(p.X, p.Z),
                                                     new System.Numerics.Vector2(point.X, point.Z));
            if (d >= best) continue;
            best  = d;
            found = cell;
        }
        return found;
    }

    // ── ZURUECKGEZOGEN 2026-08-12, und die Ruecknahme ist der Sinn dieses Kommentars ──
    //
    // Hier stand eine Methode, die die Truhe vor dem Spieler benannte, sobald jede Truhe,
    // die der Director auffuehrte, denselben Typ hatte - mit der Begruendung, dann sei es
    // egal, welcher Eintrag die Truhe ist. **Die Voraussetzung war falsch, und die eigene
    // Ebene des Users hat sie binnen einer Stunde widerlegt:**
    //
    //   10:47:42  [DeepNav] Schatzkategorie: 2 von 3 Objekten in Reichweite.
    //                       Direktor fuehrt 1 Truhen [Typ2@Raum18].
    //
    // ZWEI Truhen-Objekte in Reichweite, EIN Eintrag im Array - die zweite Truhe wurde also
    // auf die Kraft eines Eintrags hin "Silberne Schatztruhe" genannt, der gar nicht von ihr
    // handelte. User: *"it's also re-labelling all treasure coffers as silver while one is
    // active."* Das ist genau der Fehler, von dem die Kommentare in dieser Datei sagen, dass
    // ein blinder Spieler ihn nicht bemerken kann, erzeugt von genau der Art ordentlich
    // aussehender Folgerung, vor der sie warnen.
    //
    // WAS DAS ARRAY WIRKLICH IST, ueber zwei Sitzungen gemessen: die Truhen, die das Spiel
    // ENTDECKT hat und die der Spieler noch nicht geoeffnet hat. Eintraege erscheinen,
    // sobald Raeume betreten werden (10:47:13 Raum 18, 10:48:21 Raum 13), und verschwinden
    // beim Oeffnen. Es ist keine Liste der Truhen der Ebene und kann deshalb nie ueber das
    // Abzaehlen mit einem lebenden Objekt verknuepft werden.
    //
    // Die Farbe erreicht den Spieler daher nur ueber die RAUMLISTE, wo der Director den Typ
    // fuer genau diesen Raum angibt und keine Verknuepfung noetig ist. Das ist auch die
    // nuetzlichere Haelfte: sie beantwortet "in welchem Raum liegt noch eine ungeoeffnete
    // silberne", und das ist der erklaerte Gewinn.

    // ── Die Belege, die die Truhentypen klaeren, aus gewoehnlichem Spielen gesammelt ──

    private List<Chest> _lastChests = new();
    private string      _lastChestSignature = string.Empty;
    private int    _lastLoggedFloor    = -1;
    private byte   _lastLoggedDungeon;

    /// <summary>Die tiefste Ebene, die dieser Lauf erreicht hat; ueberlebt, dass der
    /// Director zwischen den Gebieten eines Laufs verschwindet. Siehe
    /// <see cref="LogSnapshot"/>.</summary>
    private int    _lastRunFloor       = -1;

    /// <summary>
    /// Protokolliert das Truhen-Array des Directors, sobald es sich AENDERT, und das ist
    /// es, was die Typwerte ohne Sondenlauf und ohne eine einzige sehende Beobachtung
    /// festnagelt.
    ///
    /// ZWEI MECHANISMEN, und fuer den ersten muss der Spieler gar nichts tun:
    /// <list type="number">
    /// <item>Eine von einem besiegten Gegner fallen gelassene Truhe wird VOM SPIEL IN
    ///   SEINEN EIGENEN WORTEN ANGESAGT - <c>LogMessage</c> 2585-2587, *"A
    ///   gold/silver/bronze treasure coffer is discovered upon defeating ."* In diesem
    ///   Moment ERSCHEINT ein Eintrag in diesem Array. Das Farbwort und das neue Typ-Byte
    ///   liegen Sekunden auseinander im selben Log, und das Plugin schreibt ohnehin jede
    ///   Chat-Zeile mit Zeitstempel.</item>
    /// <item>Eine Truhe, die der Spieler oeffnet, verlaesst das Array, und was darin war,
    ///   steht ebenfalls im Chat - ein Aetherpool-Zuwachs benennt eine silberne
    ///   ausdruecklich (<c>LogMessage</c> 7277; <c>DescriptionString</c> 819 nennt die
    ///   Regel: *"Aetherpool gear can be enhanced by accessing silver coffers"*).</item>
    /// </list>
    ///
    /// Die Zeile unten nennt deshalb, was HINZUKAM und was WEGFIEL, statt zwei Listen zum
    /// Vergleichen mit dem Auge auszugeben. Das ist die Form, die die eigene Lehre dieses
    /// Repos verlangt - eine ausgelieferte Hypothese, die sich selbst prueft, schlaegt
    /// einen Sonden-Build - und sie kostet den Spieler nichts: keine Taste, keine
    /// Unterbrechung, keine Sprache.
    /// </summary>
    public unsafe void LogChestEvidence()
    {
        var dd = _state.GetDirector();
        if (dd == null)
        {
            _lastChestSignature = string.Empty;
            _lastChests         = new List<Chest>();
            _lastLoggedFloor    = -1;
            return;
        }

        var chests = Chests();
        var signature = string.Join(",", chests.Select(c => $"{c.ChestType}@{c.RoomIndex}"));
        if (signature == _lastChestSignature) return;

        var added   = chests.Except(_lastChests).ToList();
        var removed = _lastChests.Except(chests).ToList();

        // Eine Truhe, die das Array verlaesst, ist eine, die der Spieler geoeffnet hat. Sie
        // je Ebene zu zaehlen macht aus dem Ergebnisschirm einen BELEG: dieser Schirm zaehlt
        // "Coffers Discovered" nach Farbe (Addon 8937 "Gold Coffers", 8938 "Silver Coffers",
        // 8939 "Bronze Coffers", 8949 die Ueberschrift), der Scanner des Plugins schreibt
        // dessen Text ohnehin ins Log, und eine daneben stehende Zaehlung der auf derselben
        // Ebene geoeffneten Typen ordnet beides einander zu, ohne dass etwas gefolgert wird.
        foreach (var chest in removed)
        {
            _openedThisFloor[chest.ChestType] = _openedThisFloor.GetValueOrDefault(chest.ChestType) + 1;
            _openedThisRun[chest.ChestType]   = _openedThisRun.GetValueOrDefault(chest.ChestType) + 1;
        }

        _log.Info($"[DeepChests] Gewoelbe {dd->DeepDungeonId}, Ebene {dd->Floor}: "
                  + $"neu [{Describe(added)}] weg [{Describe(removed)}] "
                  + $"jetzt {chests.Count} [{Describe(chests)}]. "
                  + $"Aetherpool Waffe={dd->WeaponLevel} Ruestung={dd->ArmorLevel} Hort={dd->HoardCount}.");

        _lastChestSignature = signature;
        _lastChests         = chests;

        if (_openedThisFloor.Count > 0)
            _log.Info("[DeepChests] Auf dieser Ebene geoeffnet: "
                      + string.Join(" ", _openedThisFloor.OrderBy(k => k.Key)
                                                         .Select(k => $"Typ{k.Key} x{k.Value}"))
                      + " - beim Ebenen-Abschluss mit 'Coffers Discovered' vergleichen.");
    }

    /// <summary>Auf der aktuellen Ebene geoeffnete Typen, fuer den Vergleich oben.</summary>
    private readonly Dictionary<byte, int> _openedThisFloor = new();

    /// <summary>
    /// Seit Beginn des Laufs geoeffnete Typen - die Zaehlung, mit der sich der
    /// Ergebnisschirm tatsaechlich vergleichen laesst.
    ///
    /// Die Zaehlung je EBENE wurde zuerst auf die Lesart gebaut, der Ergebnisschirm
    /// erscheine am Ende einer Ebene. Das tut er nicht. Der Lauf des Users vom 2026-08-12
    /// erreichte <c>DeepDungeonResult</c> ein einziges Mal, um 11:52:32, nachdem er auf
    /// Ebene 14 besiegt wurde - zehn Ebenen nach Beginn des Laufs - und die "Coffers
    /// Discovered" des Schirms zaehlen den GANZEN LAUF. Eine Zaehlung je Ebene laesst sich
    /// damit nicht vergleichen, und deshalb haben neun abgeschlossene Ebenen keine Antwort
    /// erbracht.
    ///
    /// Diese hier wird beim Betreten des Gewoelbes zurueckgesetzt und nicht bei einem
    /// Ebenenwechsel, damit beide Zahlen von derselben Sache handeln. Zusammen nageln sie
    /// Gold und Bronze aus dem spieleigenen Schirm fest, ohne dass etwas gefolgert wird -
    /// siehe <see cref="LogRunTally"/>.
    /// </summary>
    private readonly Dictionary<byte, int> _openedThisRun = new();

    /// <summary>
    /// Schreibt die Truhentyp-Zaehlung des Laufs, damit sie im Log neben den eigenen
    /// "Coffers Discovered"-Zahlen des Ergebnisschirms steht (<c>Addon</c> 8937 Gold, 8938
    /// Silber, 8939 Bronze). Wird aufgerufen, wenn dieser Schirm sich oeffnet.
    /// </summary>
    public void LogRunTally(string why)
    {
        if (_openedThisRun.Count == 0)
        {
            _log.Info($"[DeepChests] {why}: in diesem Lauf wurde keine Truhe geoeffnet.");
            return;
        }

        _log.Info($"[DeepChests] {why}: im ganzen Lauf geoeffnet "
                  + string.Join(" ", _openedThisRun.OrderBy(k => k.Key).Select(k => $"Typ{k.Key} x{k.Value}"))
                  + " - mit 'Coffers Discovered' auf diesem Schirm vergleichen (Gold/Silber/Bronze).");
    }

    private static string Describe(IEnumerable<Chest> chests) =>
        string.Join(" ", chests.Select(c => $"Typ{c.ChestType}@Raum{c.RoomIndex}"));

    /// <summary>
    /// Protokolliert die ganze Ebene einmal, beim Betreten und bei jedem Ebenenwechsel:
    /// das Layout, das Raster, das das Sheet dafuer hergibt, die eigenen Arrays des
    /// Kartenfensters, jedes Raum-Flag und jede Truhe.
    ///
    /// Das ist es, was die offene Frage beantwortet (zaehlt <c>RoomIndex</c> Rasterzellen
    /// oder Raeume?), und zwar aus gewoehnlichem Spielen. Es fuehrt ausserdem die EINE
    /// Pruefung durch, die sich ohne die Antwort machen laesst, und weigert sich, bis sie
    /// besteht, Positionen zu nennen: der Raum, in dem der Spieler steht, muss als
    /// Revealed markiert sein. Ist er das nicht, sind <c>MapData</c> und <c>RoomIndex</c>
    /// nicht derselbe Indexraum, und jedes Flag, das diese Klasse sonst melden wuerde,
    /// gehoert zu einem anderen Raum.
    /// </summary>
    public unsafe void LogSnapshot(uint playerEntityId)
    {
        var dd = _state.GetDirector();
        if (dd == null)
        {
            _lastLoggedFloor   = -1;
            _lastMismatchFloor = -1;
            return;
        }

        // Ebene 0 ist keine Ebene. Der Director meldet sie fuer ein paar Frames, waehrend
        // das Spiel den Spieler zwischen den Gebieten bewegt, auf die ein Tiefes Gewoelbe
        // aufgeteilt ist ("Gewoelbe 182, Ebene 0 ... Spielerraum -1" um 11:33:15, auf dem
        // Weg von Ebene 10 zu Ebene 11), und sie traegt keine Raeume und keine Truhen.
        // Darauf zu reagieren wuerde die Lauf-Zaehlung auf jeder zehnten Ebene loeschen.
        if (dd->Floor == 0) return;

        if (dd->Floor == _lastLoggedFloor && dd->DeepDungeonId == _lastLoggedDungeon) return;

        // Ein Lauf geht immer nur ABWAERTS. Eine Ebenennummer, die nicht hoeher ist als die
        // letzte, oder ein anderes Gewoelbe, ist deshalb ein neuer Lauf - und dann faengt
        // die Truhen-je-Lauf-Zaehlung von vorn an. Das ist der spieleigene Fortschritt und
        // keine Vermutung darueber, wann der Spieler gegangen ist.
        //
        // Verglichen wird gegen _lastRunFloor und NICHT gegen _lastLoggedFloor: letzteres
        // wird zurueckgesetzt, sobald der Director verschwindet, und das passiert auf dem
        // Weg zwischen den Gebieten, auf die ein Lauf aufgeteilt ist. Ein auf Ebene 11
        // fortgesetzter Lauf saehe dann genau aus wie ein frischer, der auf Ebene 1
        // beginnt.
        if (dd->DeepDungeonId != _lastLoggedDungeon || dd->Floor <= _lastRunFloor)
            _openedThisRun.Clear();
        _lastRunFloor = dd->Floor;

        _lastLoggedFloor   = dd->Floor;
        _lastLoggedDungeon = dd->DeepDungeonId;
        _lastMismatchFloor = -1;   // eine neue Ebene bekommt ihr eigenes Urteil
        // Die Ebene wird neu erzeugt, die Tueren von gestern sind also nirgends.
        _entryPoints.Clear();
        _openedThisFloor.Clear();
        _rooms.Reset();

        var here    = PlayerRoom(playerEntityId);
        var mapData = dd->MapData;

        var flagLines = new List<string>();
        for (var i = 0; i < mapData.Length; i++)
            if (mapData[i] != DirectorRoomFlags.None)
                flagLines.Add($"{i}={mapData[i]}");

        _log.Info($"[DeepFloor] Gewoelbe {dd->DeepDungeonId}, Ebene {dd->Floor}, "
                  + $"Layout {dd->ActiveLayoutIndex} (Init {dd->LayoutInitializationType}), "
                  + $"Spielerraum {here}. Raeume: {string.Join(" ", flagLines)}");
        _log.Info($"[DeepFloor] Truhen: {string.Join(" ", Chests().Select(c => $"Typ{c.ChestType}@Raum{c.RoomIndex}"))}");
        LogLayoutSheet(dd->ActiveLayoutIndex);
        LogMapAgent();

        // Fuehrt die Live-Pruefung auch hier einmal aus, damit ihr Urteil im Log neben den
        // Daten steht, aus denen es gefaellt wurde, und nicht erst beim naechsten
        // Tastendruck.
        _log.Info($"[DeepFloor] Raumangaben nutzbar: {FlagsTrustworthy(playerEntityId)}.");
    }

    /// <summary>
    /// Schreibt das 5x5-Raster, das das Layout-Sheet fuer das aktive Layout haelt.
    ///
    /// <c>DeepDungeonMap5X</c> ist ein SUBROW-Sheet (der gewoehnliche Leser weist es ab -
    /// deshalb kam der erste Versuch hierzu leer zurueck): 61 Zeilen, je 5 Unterzeilen,
    /// 5 Spalten mit <c>DeepDungeonRoom</c>-Referenzen, und eine 0 heisst, dass in dieser
    /// Zelle kein Raum ist. Zeile 1 haelt die Ids 1-21, Zeile 2 die 101-121, Zeile 3 die
    /// 201-221 - die Ids sind also nach Layout geblockt, und das macht aus dem einen
    /// vorzeichenbehafteten Byte des Directors einen LAYOUT-LOKALEN Index statt einer
    /// Sheet-Id.
    /// </summary>
    private void LogLayoutSheet(byte layout)
    {
        var sheet = _data.GetSubrowExcelSheet<DeepDungeonMap5X>();
        if (sheet == null || !sheet.TryGetRow(layout, out var row))
        {
            _log.Info($"[DeepFloor] DeepDungeonMap5X hat keine Zeile {layout}.");
            return;
        }

        for (ushort sub = 0; sub < row.Count; sub++)
        {
            var cells = row[sub].DeepDungeonRoom;
            _log.Info($"[DeepFloor] Layout {layout} Reihe {sub}: "
                      + string.Join(" ", Enumerable.Range(0, cells.Count).Select(c => cells[c].RowId.ToString())));
        }
    }

    /// <summary>
    /// Schreibt, was das KARTENFENSTER des Tiefen Gewoelbes selbst haelt -
    /// <c>AgentDeepDungeonMap.Data</c>: <c>Map[25]</c>, <c>RoomIndex[25]</c> und
    /// <c>RoomIndexCount</c>.
    ///
    /// Das ist die spieleigene Uebersetzung zwischen dem Raster, das es zeichnet, und den
    /// Raumnummern, die es benutzt, und der kuerzeste Weg, jene Frage zu klaeren. Es wird
    /// protokolliert statt in Sprache umgesetzt, weil bislang nichts sagt, welches der
    /// beiden Arrays welche Richtung hat, und eine Karte ist genau das, was ein blinder
    /// Spieler nicht nachpruefen kann.
    /// </summary>
    /// <remarks>
    /// SICHERHEIT, und das ist nach dem 2026-08-12 keine Formsache: eine Spielstruktur
    /// ueber einen Agent-Zeiger zu lesen ist, wie der Charakterinfo-Leser an jenem Tag das
    /// Spiel zum Absturz gebracht hat. Diese hier bleibt, weil sie eine andere Art von
    /// Lesevorgang ist - 280 Byte schlichte sbytes, bools und ein byte, OHNE
    /// <c>Utf8String</c> und ohne einen Zeiger, dem zu folgen waere. Der Absturz kam von
    /// <c>Utf8String.AsSpan</c>, das aus einem ungeprueften Zeiger und einer ungeprueften
    /// Laenge eine Spanne baut; nichts dieser Art wird hier gelesen. Am selben Tag
    /// gemessen, waren die Daten schon gueltig, BEVOR das Kartenfenster je geoeffnet worden
    /// war (09:58:13.471, alles -1) - das Spiel legt sie also mit dem Agenten an und nicht
    /// mit dem Fenster.
    /// </remarks>
    private unsafe void LogMapAgent()
    {
        var agent = AgentDeepDungeonMap.Instance();
        if (agent == null || agent->Data == null)
        {
            _log.Info("[DeepFloor] AgentDeepDungeonMap liefert keine Daten (Fenster nie geoeffnet?).");
            return;
        }

        var d = agent->Data;
        _log.Info($"[DeepFloor] Kartenfenster: Gewoelbe={d->DeepDungeonId} gesperrt={d->MapLocked} "
                  + $"RoomIndexCount={d->RoomIndexCount}");
        _log.Info($"[DeepFloor] Kartenfenster Map[25]:       {string.Join(" ", ToArray(d->Map))}");
        _log.Info($"[DeepFloor] Kartenfenster RoomIndex[25]: {string.Join(" ", ToArray(d->RoomIndex))}");
    }

    private static IEnumerable<int> ToArray(Span<sbyte> span)
    {
        var copy = new int[span.Length];
        for (var i = 0; i < span.Length; i++) copy[i] = span[i];
        return copy;
    }
}
