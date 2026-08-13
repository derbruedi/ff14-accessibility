using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Plugin.Services;
using Lumina.Data.Files;
using Lumina.Excel;

namespace FF14Accessibility.Services;

/// <summary>
/// WO EIN RAUM WIRKLICH LIEGT.
///
/// Das Problem von Anfang an: der Content-Director sagt, in welchem RAUM etwas ist, und
/// nie, wo der Raum liegt - ein Raum konnte also benannt, aber nicht angelaufen werden.
///
/// DAS SPIEL HAT DIE KOORDINATEN, und die Datei, in der sie stehen, ist bekannt - die
/// <c>planmap.lgb</c> des Gebiets, in der jedes Raummodul eine
/// <c>SharedGroup</c>-Layout-Instanz mit vollstaendiger Transformation ist, und Spalte 0
/// von <c>DeepDungeonRoom</c> ist die Instanz-Id. Dieser Befund hielt stand. **Falsch war
/// der Weg von einem Raum-INDEX des Directors zu einer <c>DeepDungeonRoom</c>-ZEILE**,
/// und die Widerlegung wurde festgehalten: die Vermutung
/// <c>row = ActiveLayoutIndex * 100 + index</c> wurde von der Selbstpruefung auf jeder
/// Ebene des 2026-08-12 verworfen, mal um einen Raum (17-28 Yalm) und mal um einen ganzen
/// Layout-Block (185-539 Yalm).
///
/// DIE ECHTE KETTE, von Anfang bis Ende aus den spieleigenen Sheets gelesen
/// (Offline-Auszug <c>tools/deepdungeon-dump</c>, 2026-08-12) und ohne eine einzige
/// geratene Rechnung darin:
///
/// <list type="number">
/// <item><b>Das Gebiet besitzt einen festen Satz Raummodule.</b> Die <c>planmap.lgb</c>
///   lesen und die <c>DeepDungeonRoom</c>-Zeilen behalten, deren Instanz-Id darin
///   vorkommt, liefert sie: 42 Zeilen fuer jedes Gebiet des Palasts der Toten (zwei
///   Bloecke zu 21), 54 fuer den Himmelsberg (21 + 21 + 12). Die Bloecke sind die
///   physischen KOPIEN des Raumrasters, die das Gebiet vorhaelt - Gebiet 561 des Palasts
///   der Toten hat die Raeume 1-21 um x -180..-420 und die Raeume 101-121 um
///   x 184..430.</item>
/// <item><b><c>ActiveLayoutIndex</c> waehlt einen Block</b>, gezaehlt innerhalb des
///   Gebiets: er war 0 und 1 in Gebiet 561, das genau zwei hat.</item>
/// <item><b><c>DeepDungeonMap5X</c> legt diesen Block als 5x5-Raster aus</b> - 5 Unterzeilen
///   mit je 5 <c>DeepDungeonRoom</c>-Referenzen, 0 wo das Raster keinen Raum hat. Die
///   Zeile wird ueber ihren Inhalt GEFUNDEN, nie berechnet: die Reihenfolge des Sheets
///   folgt den Bloecken nicht (Zeile 15 haelt Block 16, Zeile 17 haelt Block 14),
///   <c>row = block + 1</c> waere also fuer vier der 61 Zeilen falsch.</item>
/// <item><b>Der Raumindex des Directors ist eine ZELLE dieses Rasters, zeilenweise
///   gelesen:</b> <c>grid[index / 5][index % 5]</c>. Die Spaltenreihenfolge ist die
///   x-Achse, die Unterzeilenreihenfolge die z-Achse - gegen die Koordinaten selbst
///   geprueft, wo jede Rasterspalte ein x-Band ist (Raeume 1-3 bei x -402..-419, 4-8 bei
///   -355..-376, 9-13 bei -288..-319, 14-18 bei -222..-235, 19-21 bei -182) und z in
///   jeder Spalte nach unten steigt.</item>
/// </list>
///
/// **DIE KETTE WURDE VOR DER AUSLIEFERUNG GEGEN JEDE EBENE NACHGESPIELT, DIE DER USER
/// GELAUFEN IST.** Seine Logs halten eine Brotkrume in dem Augenblick fest, in dem der
/// Director ihn zum ersten Mal in einen neuen Raum setzt - also im TUERRAHMEN stehend.
/// Ein Tuerrahmen hat eine Signatur, die keine falsche Abbildung faelschen kann: er liegt
/// zwischen genau zwei Raummodulen, und diese beiden sind Nachbarn im Raster. Ueber beide
/// Sitzungen des 2026-08-12 - 14 Ebenen, beide Gebiete des Palasts der Toten, beide
/// Layout-Bloecke - **passen 32 von 32 Tuerrahmen, ohne einen einzigen Fehlschlag**
/// (<c>tools/deepdungeon-dump verify</c>). Der Uebergang von Raum 7 zu Raum 8 auf Ebene 5
/// etwa wurde bei (-269, 252) aufgezeichnet: der Mittelpunkt der Module 10 (-300, 245)
/// und 15 (-235, 242), und genau diese beiden weist diese Abbildung jenen Raeumen zu.
///
/// Zum Vergleich: die widerlegte Abbildung lag auf jeder Ebene um 17 bis 539 Yalm daneben.
///
/// SIE MUSS SICH TROTZDEM AUF JEDER EBENE LIVE VERDIENEN. Eine falsche Position schickt
/// einen blinden Spieler in eine Wand, also bleibt <see cref="Confirmed"/> false, bis das
/// Spiel selbst zustimmt: an der Stelle, an der der Spieler dem Modul seines eigenen
/// Raumes am NAECHSTEN gekommen ist, muss dieses Modul das naechstgelegene von allen sein.
/// Die groesste Annaeherung statt jedes Frames ist das, was die Pruefung ehrlich macht -
/// im Tuerrahmen ist das Nachbarmodul zu Recht naeher, und dort zu urteilen wuerde eine
/// richtige Abbildung verwerfen.
/// </summary>
public sealed class DeepDungeonRoomMap
{
    private readonly IDataManager _data;
    private readonly IPluginLog   _log;

    public DeepDungeonRoomMap(IDataManager data, IPluginLog log)
    {
        _data = data;
        _log  = log;
    }

    /// <summary>Raumindex des Directors -> Weltposition, fuer die gerade geladene Ebene.</summary>
    private readonly Dictionary<int, Vector3> _positions = new();

    private ushort _loadedTerritory;
    private byte   _loadedLayout;
    private bool   _loaded;

    /// <summary>
    /// Ob die Lesart Index-zu-Raum auf dieser Ebene durch einen Live-Vergleich bestaetigt
    /// wurde. Zu einer Layout-Position wird nichts angelaufen, solange das false ist.
    /// </summary>
    public bool Confirmed { get; private set; }

    /// <summary>Vergisst alles - eine neue Ebene ist ein neuer Block und ein neuer Satz
    /// Raeume.</summary>
    public void Reset()
    {
        _positions.Clear();
        _loadedTerritory = 0;
        _loaded          = false;
        Confirmed        = false;
        _closest.Clear();
    }

    /// <summary>
    /// Die Weltposition eines Raumes, oder null, wenn sie unbekannt oder unbestaetigt ist.
    /// </summary>
    public Vector3? PositionOf(ushort territory, byte layout, int roomIndex)
    {
        Load(territory, layout);
        if (!Confirmed) return null;
        return _positions.TryGetValue(roomIndex, out var p) ? p : null;
    }

    /// <summary>
    /// Wo das Modul eines Raumes steht, OHNE dass die Abbildung dieser Ebene schon
    /// bestaetigt sein muss.
    ///
    /// Das ist fuer Pruefungen und fuer das Log, nie fuer ein Laufziel -
    /// <see cref="PositionOf"/> ist die Methode, die auf <see cref="Confirmed"/> wartet,
    /// und das bleibt so, weil eine Route das ist, was ein blinder Spieler nicht
    /// nachpruefen kann.
    ///
    /// Es gibt sie, weil die beiden Beduerfnisse wirklich verschieden sind. Die Abbildung
    /// zu bestaetigen braucht eine Live-Probe, in den ersten Augenblicken einer Ebene ist
    /// also ueberhaupt nichts bekannt - und genau dann muss das Plugin entscheiden, ob die
    /// Stelle, auf der der Spieler steht, ein merkenswerter Tuerrahmen ist oder die
    /// Ladeposition ausserhalb des Gewoelbes. "Welches Modul ist am naechsten" zu
    /// beantworten braucht nur platzierte Module und sonst nichts; dort falsch zu liegen
    /// kostet eine gespeicherte Brotkrume.
    /// </summary>
    public Vector3? ModulePosition(ushort territory, byte layout, int roomIndex)
    {
        Load(territory, layout);
        return _positions.TryGetValue(roomIndex, out var p) ? p : null;
    }

    // ── Die Live-Pruefung ──
    //
    // Je Raum, in dem der Spieler war: wie nah er dem Modul, das diese Abbildung ihm
    // zuweist, je gekommen ist, und welches Modul in jenem Augenblick am naechsten war.
    // Die GROESSTE ANNAEHERUNG zu nehmen ist der ganze Witz. Jeden Frame zu urteilen
    // verwirft eine richtige Abbildung in dem Moment, in dem der Spieler im Tuerrahmen
    // steht - genau das waren die hin- und herspringenden Urteile vom 2026-08-12
    // 11:11:01/11:11:02.

    private readonly Dictionary<int, (float Self, bool Agrees, int Nearest)> _closest = new();

    /// <summary>
    /// Prueft die Lesart gegen den Ort, an dem der Spieler tatsaechlich ist, und behaelt
    /// das Urteil fuer diese Ebene.
    ///
    /// DIE PRUEFUNG IST IN BEIDEN TEILEN MASSSTABSFREI. Sie fragt nie "ist der Spieler
    /// innerhalb von N Yalm des Raumes" - N waere eine hier erfundene Zahl, und der
    /// aufgezeichnete Punkt eines Raumes ist der URSPRUNG seines Moduls, nicht dessen
    /// Mitte. Sie fragt nach Rang und Nachbarschaft, und das sind Tatsachen ueber die
    /// ganze Menge, die ohne Justierung auskommen:
    ///
    /// <list type="number">
    /// <item>das eigene Modul des Spielers ist das NAECHSTE, oder</item>
    /// <item>es ist eines der beiden naechsten UND diese beiden sind NACHBARN IM RASTER -
    ///   und so sieht es aus, wenn man im Tuerrahmen steht, und sonst nichts.</item>
    /// </list>
    ///
    /// Die zweite Klausel ist keine Aufweichung, sie ist die Messung. Jeder der 32
    /// Tuerrahmen, durch die der User am 2026-08-12 gegangen ist, wurde in dem Moment
    /// aufgezeichnet, in dem der Director ihn erstmals in den neuen Raum setzte - also
    /// genau auf der Grenze - und alle 32 liegen zwischen zwei im Raster benachbarten
    /// Modulen, mit dem betretenen Raum darunter, mehrere davon mit dem Nachbarn knapp
    /// naeher (Ebene 4, Raum 13: Modul 18 bei 14 Yalm, das eigene Modul 21). Die allein
    /// nach dem Rang zu beurteilen wuerde eine nachweislich richtige Abbildung verwerfen.
    /// Streng bleibt sie da, wo es zaehlt: die widerlegte Abbildung lag um 17 bis 539 Yalm
    /// daneben und wuerde ueberall an beiden Klauseln scheitern, denn einen ganzen Block
    /// weiter ist zu nichts benachbart.
    ///
    /// Es zaehlt nur die GROESSTE ANNAEHERUNG des Spielers an jeden Raum, und das ist es,
    /// was ein Urteil davon abhaelt, Frame um Frame zu springen, waehrend er eine Schwelle
    /// ueberquert (das Paar 11:11:01-bestaetigt / 11:11:02-widerlegt im selben Log).
    /// </summary>
    public void Verify(ushort territory, byte layout, int playerRoom, Vector3 playerPos)
    {
        Load(territory, layout);
        if (_positions.Count == 0 || playerRoom < 0) return;
        if (!_positions.TryGetValue(playerRoom, out var mine)) return;

        var self = Flat(mine, playerPos);

        // Es zaehlt nur die groesste Annaeherung an den EIGENEN Raum des Spielers, damit
        // eine Probe aus dem Tuerrahmen nie eine bessere ueberschreibt.
        if (_closest.TryGetValue(playerRoom, out var had) && had.Self <= self) return;

        var ranked = _positions.OrderBy(p => Flat(p.Value, playerPos)).Select(p => p.Key).ToList();
        var agrees = ranked[0] == playerRoom
                     || (ranked.Count > 1 && ranked[1] == playerRoom && Neighbours(ranked[0], ranked[1]));

        _closest[playerRoom] = (self, agrees, ranked[0]);

        var wrong = _closest.Where(k => !k.Value.Agrees).ToList();
        var ok    = wrong.Count == 0;
        if (ok == Confirmed) return;
        Confirmed = ok;

        if (ok)
            _log.Info($"[DeepRooms] Raumlage bestaetigt: {_closest.Count} Raeume geprueft, "
                      + $"zuletzt Raum {playerRoom} - naechstgelegenes Raummodul ist {ranked[0]} "
                      + $"({self:F1} Yalm zum eigenen). {_positions.Count} Raeume mit Koordinaten.");
        else
            _log.Warning($"[DeepRooms] Raumlage WIDERLEGT: "
                         + string.Join("; ", wrong.Select(w =>
                               $"in Raum {w.Key} war Modul {w.Value.Nearest} naeher, und die beiden "
                               + $"sind keine Nachbarn im Raster ({w.Value.Self:F1} Yalm zum eigenen)"))
                         + " - es wird nichts angelaufen.");
    }

    /// <summary>
    /// Ob zwei Rasterzellen eine Kante teilen. Das Raster wird zeilenweise gelesen
    /// (<c>cell = row * 5 + column</c>), das ist also der spieleigene Begriff davon, dass
    /// zwei Raeume nebeneinanderliegen - derselbe, den seine Flags ConnectionN/S/W/E
    /// beschreiben.
    /// </summary>
    private static bool Neighbours(int a, int b)
        => Math.Abs(a / 5 - b / 5) + Math.Abs(a % 5 - b % 5) == 1;

    private static float Flat(Vector3 a, Vector3 b)
        => Vector2.Distance(new Vector2(a.X, a.Z), new Vector2(b.X, b.Z));

    /// <summary>
    /// Loest jede Zelle des Ebenenrasters zu einer Weltposition auf, einmal je Ebene.
    /// </summary>
    private void Load(ushort territory, byte layout)
    {
        if (_loaded && _loadedTerritory == territory && _loadedLayout == layout) return;
        _positions.Clear();
        _closest.Clear();
        _loadedTerritory = territory;
        _loadedLayout    = layout;
        _loaded          = true;
        Confirmed        = false;

        // 1. Die Layout-Instanzen des Gebiets.
        var instances = LayoutInstances(territory, out var path);
        if (instances.Count == 0) return;

        // 2. Die DeepDungeonRoom-Zeilen, die in DIESEM Gebiet stehen, gruppiert in die
        //    Bloecke, die seine physischen Kopien des Raumrasters sind.
        var rooms = _data.Excel.GetSheet<RawRow>(null, "DeepDungeonRoom");
        if (rooms == null)
        {
            _log.Warning("[DeepRooms] DeepDungeonRoom nicht lesbar.");
            return;
        }

        var here = new Dictionary<uint, Vector3>();
        foreach (var row in rooms)
        {
            uint instanceId;
            try { instanceId = Convert.ToUInt32(row.ReadColumn(0) ?? 0u); }
            catch { continue; }
            if (instanceId != 0 && instances.TryGetValue(instanceId, out var pos))
                here[row.RowId] = pos;
        }

        var blocks = here.Keys.Select(id => id / 100).Distinct().OrderBy(b => b).ToList();
        if (blocks.Count == 0)
        {
            _log.Info($"[DeepRooms] {path}: keine DeepDungeonRoom-Zeile liegt in diesem Gebiet.");
            return;
        }
        if (layout >= blocks.Count)
        {
            _log.Warning($"[DeepRooms] {path}: Layout {layout}, aber das Gebiet hat nur "
                         + $"{blocks.Count} Raumbloecke [{string.Join(" ", blocks)}] - keine Koordinaten.");
            return;
        }
        var block = blocks[layout];

        // 3. Das Raster, das das Sheet fuer diesen Block haelt. Ueber seinen INHALT
        //    gefunden, weil die Zeilenreihenfolge des Sheets den Bloecken nicht folgt
        //    (Zeile 15 haelt Block 16).
        var grid = GridOf(block);
        if (grid == null)
        {
            _log.Warning($"[DeepRooms] Keine DeepDungeonMap5X-Zeile enthaelt Block {block} "
                         + "- keine Raumkoordinaten.");
            return;
        }

        // 4. Der Raumindex des Directors ist eine Zelle dieses Rasters, zeilenweise gelesen.
        for (var cell = 0; cell < 25; cell++)
        {
            var roomRow = grid[cell];
            if (roomRow == 0) continue;
            if (here.TryGetValue(roomRow, out var pos)) _positions[cell] = pos;
        }

        _log.Info($"[DeepRooms] {path}: {instances.Count} Layout-Instanzen, Bloecke "
                  + $"[{string.Join(" ", blocks)}], Layout {layout} -> Block {block}, "
                  + $"{_positions.Count} Raeume mit Koordinaten "
                  + $"[{string.Join(" ", _positions.OrderBy(k => k.Key).Select(k => $"{k.Key}@{k.Value.X:F0},{k.Value.Z:F0}"))}]");
    }

    /// <summary>Jede Layout-Instanz der <c>planmap.lgb</c> des Gebiets.</summary>
    private Dictionary<uint, Vector3> LayoutInstances(ushort territory, out string path)
    {
        path = string.Empty;
        var result = new Dictionary<uint, Vector3>();

        var bg = _data.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>()
                      ?.GetRowOrDefault(territory)?.Bg.ExtractText() ?? string.Empty;
        if (bg.Length == 0 || !bg.Contains('/'))
        {
            _log.Info($"[DeepRooms] Gebiet {territory} hat keinen Bg-Pfad - keine Raumkoordinaten.");
            return result;
        }

        // "ffxiv/fst_f1/cnt/f1c1/level/f1c1" -> "bg/ffxiv/fst_f1/cnt/f1c1/level/planmap.lgb"
        path = $"bg/{bg[..bg.LastIndexOf('/')]}/planmap.lgb";

        LgbFile? lgb;
        try { lgb = _data.GetFile<LgbFile>(path); }
        catch (Exception ex)
        {
            _log.Warning($"[DeepRooms] {path} nicht lesbar: {ex.Message}");
            return result;
        }
        if (lgb == null)
        {
            _log.Info($"[DeepRooms] {path} gibt es nicht - keine Raumkoordinaten.");
            return result;
        }

        foreach (var layer in lgb.Layers)
            foreach (var obj in layer.InstanceObjects)
                result[obj.InstanceId] = new Vector3(obj.Transform.Translation.X,
                                                     obj.Transform.Translation.Y,
                                                     obj.Transform.Translation.Z);
        return result;
    }

    /// <summary>
    /// Die 25 <c>DeepDungeonRoom</c>-Zeilen-Ids des Rasters, das einen Block haelt, oder
    /// null, wenn keine Zeile das tut.
    ///
    /// <c>DeepDungeonMap5X</c> ist ein SUBROW-Sheet (der gewoehnliche Leser weist es ab -
    /// deshalb kam der erste Versuch hierzu leer zurueck): 61 Zeilen, 5 Unterzeilen zu
    /// 5 Spalten, 0 wo das Raster keinen Raum hat. Die Zeile wird ueber den Block
    /// identifiziert, den sie enthaelt, statt aus ihm berechnet - aus dem Grund im
    /// Klassenkommentar.
    /// </summary>
    private uint[]? GridOf(uint block)
    {
        var sheet = _data.GetSubrowExcelSheet<Lumina.Excel.Sheets.DeepDungeonMap5X>();
        if (sheet == null) return null;

        foreach (var row in sheet)
        {
            var cells = new uint[25];
            var found = false;
            for (ushort sub = 0; sub < row.Count && sub < 5; sub++)
            {
                var refs = row[sub].DeepDungeonRoom;
                for (var col = 0; col < refs.Count && col < 5; col++)
                {
                    var id = refs[col].RowId;
                    cells[sub * 5 + col] = id;
                    if (id != 0 && id / 100 == block) found = true;
                }
            }
            if (found) return cells;
        }
        return null;
    }
}
