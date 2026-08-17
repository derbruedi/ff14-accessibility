using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

/// <summary>
/// One monster the hunting log still asks for, with the place it lives.
/// </summary>
/// <param name="MonsterName">Monster name as the hunting log spells it.</param>
/// <param name="Killed">Kills already registered for this entry.</param>
/// <param name="Required">Kills the entry asks for.</param>
/// <param name="ZoneName">Zone place name ("Zentrales La Noscea").</param>
/// <param name="AreaName">Sub-area place name ("Sommerfurt").</param>
/// <param name="MapId">Map the area belongs to, 0 when unknown.</param>
/// <param name="InCurrentZone">True when that map is the one the player is on.</param>
/// <param name="Position">World position of the AREA (its map marker), or null
/// when the area carries no marker. Y is unknown (map data is 2D) and resolved
/// via navmesh before walking, like every other waypoint.</param>
public sealed record HuntingTarget(
    string MonsterName,
    int Killed,
    int Required,
    string ZoneName,
    string AreaName,
    uint MapId,
    bool InCurrentZone,
    Vector3? Position);

/// <summary>
/// The hunting log ("Bestiarium") as a source of PLACES to go, not just a window
/// to read: which monsters the player's current rank still asks for, and where
/// they live.
///
/// Verified 2026-08-17, all of it against the game's own data:
/// - MonsterNote holds 600 rows in twelve blocks of 50 (one per hunting log
///   class plus the three grand companies), each block five ranks of ten
///   entries: Thaumaturg is 70001..70050, so its rank 3 is 70021..70030. That
///   this is the rank grouping is confirmed by the window itself - the kill
///   counts of those ten rows add up to 48, the "3/48" the header shows for
///   rank 3.
/// - ClassJob.MonsterNote is NOT a row reference but the class index the game
///   uses for the hunting log (Gladiator 0 ... Thaumaturg 6, Hermetiker 7,
///   Schurke 11; crafters 127 and post-ARR jobs -1 have no log). It is the same
///   classIndex AgentMonsterNote.OpenWithData takes.
/// - A block belongs to a class by NAME: every entry is called "Thaumaturg 21".
///   Checked for all nine class blocks against ClassJob.Name.
/// - MonsterNoteTarget names up to three habitats per monster as zone plus
///   sub-area. 590 of 647 habitat entries have a map marker on their own zone
///   map, so their position is known; the 40 without one are dungeon areas.
/// </summary>
public sealed class HuntingLogService
{
    private readonly IDataManager _data;
    private readonly IObjectTable _objectTable;
    private readonly IClientState _clientState;
    private readonly PlacesService _places;
    private readonly IPluginLog _log;

    /// <summary>Entries per rank, and ranks per class - the block layout above.</summary>
    private const int EntriesPerRank = 10;
    private const int RanksPerClass = 5;

    /// <summary>ClassJob.MonsterNote for a class without a hunting log.</summary>
    private const uint NoHuntingLog = 127;

    // Class index -> first MonsterNote row of that class's block. Built once
    // from the sheet; static per game version.
    private Dictionary<uint, uint>? _blockStarts;

    public HuntingLogService(IDataManager data, IObjectTable objectTable, IClientState clientState,
                             PlacesService places, IPluginLog log)
    {
        _data = data;
        _objectTable = objectTable;
        _clientState = clientState;
        _places = places;
        _log = log;
    }

    /// <summary>
    /// The hunting log class index of the job the player is currently on, or
    /// null when this job has no hunting log (crafters, gatherers, and every
    /// job introduced after A Realm Reborn). Jobs inherit their class's log -
    /// the sheet already resolves that (Schwarzmagier carries Thaumaturg's 6).
    /// </summary>
    public uint? GetCurrentClassIndex()
    {
        var player = _objectTable.LocalPlayer;
        if (player == null) return null;

        var jobId = player.ClassJob.RowId;
        if (!_data.GetExcelSheet<ClassJob>().TryGetRow(jobId, out var job)) return null;

        var index = job.MonsterNote.RowId;
        // 127 = crafter/gatherer, uint.MaxValue = job without a hunting log.
        if (index == NoHuntingLog || index == uint.MaxValue) return null;
        return index;
    }

    /// <summary>
    /// First MonsterNote row of a class's block, or null when the class has no
    /// block. Matched by name: hunting log entries are called "&lt;class&gt; 21",
    /// which is the only link the sheet carries.
    /// </summary>
    public uint? GetBlockStart(uint classIndex)
    {
        _blockStarts ??= BuildBlockStarts();
        return _blockStarts.TryGetValue(classIndex, out var start) ? start : null;
    }

    private Dictionary<uint, uint> BuildBlockStarts()
    {
        // Class name -> class index, for every class that has a hunting log.
        var indexByName = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        foreach (var job in _data.GetExcelSheet<ClassJob>())
        {
            var index = job.MonsterNote.RowId;
            if (index == NoHuntingLog || index == uint.MaxValue) continue;
            var name = job.Name.ExtractText().Trim();
            if (name.Length == 0) continue;
            // Only the CLASS names a block; the jobs on top of it (Schwarzmagier)
            // share the index but never appear in an entry name.
            indexByName.TryAdd(name, index);
        }

        var starts = new Dictionary<uint, uint>();
        foreach (var row in _data.GetExcelSheet<MonsterNote>())
        {
            var name = row.Name.ExtractText().Trim();
            if (name.Length == 0) continue;
            var cut = name.LastIndexOf(' ');
            if (cut <= 0) continue;
            var prefix = name[..cut];
            if (!indexByName.TryGetValue(prefix, out var index)) continue;
            // The lowest row of the block is its start.
            if (!starts.TryGetValue(index, out var known) || row.RowId < known)
                starts[index] = row.RowId;
        }

        _log.Info($"[Jagd] Klassen-Bloecke: {starts.Count} " +
                  string.Join(", ", starts.OrderBy(s => s.Key).Select(s => $"{s.Key}->{s.Value}")));
        return starts;
    }

    /// <summary>
    /// The ten MonsterNote rows of one rank (1-based) of a class, in log order.
    /// Empty when the class has no block or the rank is out of range.
    /// </summary>
    public List<MonsterNote> GetRankEntries(uint classIndex, int rank)
    {
        var result = new List<MonsterNote>();
        if (rank < 1 || rank > RanksPerClass) return result;
        if (GetBlockStart(classIndex) is not { } start) return result;

        var sheet = _data.GetExcelSheet<MonsterNote>();
        var first = start + (uint)((rank - 1) * EntriesPerRank);
        for (var i = 0u; i < EntriesPerRank; i++)
        {
            if (sheet.TryGetRow(first + i, out var row)) result.Add(row);
        }
        return result;
    }

    /// <summary>
    /// The rank the player is currently working on for a class (1-based, as the
    /// window shows it), together with the kill counters of its ten entries.
    /// Null when the game has no progress data.
    ///
    /// Layout measured in-game 2026-08-17 (/acc huntprobe), not assumed:
    /// - RankData is indexed BY the class index - every slot logged its own
    ///   position in the Index field (slot 6 = Index 6 = Thaumaturg).
    /// - Rank is 0-BASED: the character standing on rank 3 in the window logged
    ///   Rank=2.
    /// - Counts sit in sheet order: entry 0 read 0/2 while the window showed
    ///   Wuchernde Efeuranke 0 of 4 and Dämonenfliegenfalle 2 of 2, which are
    ///   targets 0 and 1 of sheet row 70021. The counters of that rank add up to
    ///   3, exactly the "3/48" of the window header.
    /// The Flags field is left alone: what it means is not established, and
    /// "entry done" follows from the counters anyway.
    /// </summary>
    private unsafe (int Rank, MonsterNoteRankInfo Info)? GetProgress(uint classIndex)
    {
        var manager = MonsterNoteManager.Instance();
        if (manager == null)
        {
            _log.Info("[Jagd] MonsterNoteManager.Instance() ist null - kein Fortschritt lesbar.");
            return null;
        }
        if (classIndex >= (uint)manager->RankData.Length)
        {
            _log.Info($"[Jagd] Klassen-Index {classIndex} liegt ausserhalb der {manager->RankData.Length} Slots.");
            return null;
        }

        var info = manager->RankData[(int)classIndex];
        return (info.Rank + 1, info);
    }

    /// <summary>
    /// The monsters the player's current rank still asks for, with the place
    /// each one lives. Empty when the class has no hunting log or the rank is
    /// finished. Read fresh per call - kill counters change while playing.
    ///
    /// One entry per MONSTER, not per habitat: a monster living in three zones
    /// would otherwise fill the browser with the same kill three times. The
    /// habitat in the player's current zone wins, so the list answers "what can
    /// I hunt right here" first; otherwise the first habitat with a known
    /// position.
    /// </summary>
    public unsafe List<HuntingTarget> GetOpenTargets()
    {
        var result = new List<HuntingTarget>();
        if (GetCurrentClassIndex() is not { } classIndex) return result;
        if (GetProgress(classIndex) is not { } progress) return result;

        var rows = GetRankEntries(classIndex, progress.Rank);
        if (rows.Count == 0)
        {
            _log.Info($"[Jagd] Klasse {classIndex}, Rang {progress.Rank}: keine Sheet-Zeilen.");
            return result;
        }

        var currentMap = _clientState.MapId;
        for (var e = 0; e < rows.Count; e++)
        {
            var row = rows[e];
            var counts = progress.Info.RankData[e].Counts;
            for (var t = 0; t < row.MonsterNoteTarget.Count && t < counts.Length; t++)
            {
                var required = t < row.Count.Count ? row.Count[t] : 0;
                if (required == 0) continue;              // slot unused by this entry
                var killed = counts[t];
                if (killed >= required) continue;         // already done

                var target = row.MonsterNoteTarget[t].ValueNullable;
                if (target == null) continue;
                var nameRow = target.Value.BNpcName.ValueNullable;
                if (nameRow == null) continue;
                var name = ResolveMonsterName(nameRow.Value);
                if (name.Length == 0) continue;

                var habitats = GetHabitats(target.Value);
                if (habitats.Count == 0) continue;

                // Current zone first, then the first habitat we can actually
                // walk to; a habitat without a marker is still worth naming.
                var best = habitats.FirstOrDefault(h => h.MapId == currentMap && currentMap != 0);
                if (best.MapId == 0) best = habitats.FirstOrDefault(h => h.Position != null);
                if (best.MapId == 0) best = habitats[0];

                result.Add(new HuntingTarget(
                    name, killed, required,
                    best.Zone, best.Area, best.MapId,
                    best.MapId != 0 && best.MapId == currentMap,
                    best.Position));
            }
        }

        _log.Info($"[Jagd] Klasse {classIndex}, Rang {progress.Rank}: {result.Count} offene Ziele.");
        return result;
    }

    /// <summary>
    /// The spoken name of a monster. German sheet names leave the adjective
    /// ending as a placeholder the game fills in per case ("wuchernd[a]
    /// Efeuranke"); read out raw, the brackets end up in the speech.
    ///
    /// BNpcName.Pronoun is the gender, so the nominative ending follows from it:
    /// 0 masculine "-er", 1 feminine "-e", 2 neuter "-es". Two of the three are
    /// confirmed against names the game itself displayed - Pronoun 1
    /// "wuchernd[a] Efeuranke" shows as "Wuchernde Efeuranke" (window dump
    /// 2026-08-17) and Pronoun 0 "rostig[a] Kobalos" as "Rostiger Kobalos" (log
    /// 2026-07-19); the neuter form is plain German strong declension.
    ///
    /// The other two placeholders in the sheet, [p] (1195 names) and [t] (201),
    /// are dropped without replacement. UNVERIFIED - no displayed name with one
    /// of them has been seen yet. Dropping can only cost an ending, never
    /// invent a wrong name, which is why it is the safe fallback.
    ///
    /// Only German gets endings: the placeholders are a property of the client
    /// language, and guessing suffixes for a language whose rules have not been
    /// measured would be worse than the bare stem.
    /// </summary>
    private string ResolveMonsterName(BNpcName nameRow)
    {
        var text = nameRow.Singular.ExtractText().Trim();
        if (!text.Contains('[')) return text;

        var ending = _data.Language == Dalamud.Game.ClientLanguage.German
            ? nameRow.Pronoun switch { 0 => "er", 1 => "e", 2 => "es", _ => string.Empty }
            : string.Empty;

        return text.Replace("[a]", ending)
                   .Replace("[p]", string.Empty)
                   .Replace("[t]", string.Empty)
                   .Trim();
    }

    /// <summary>
    /// Habitats of a hunting log monster as walkable destinations: zone, area
    /// and - where the area carries a map marker - its world position.
    /// </summary>
    public List<(string Zone, string Area, uint MapId, Vector3? Position)> GetHabitats(MonsterNoteTarget target)
    {
        var result = new List<(string, string, uint, Vector3?)>();
        for (var i = 0; i < target.PlaceNameZone.Count; i++)
        {
            var zoneRef = target.PlaceNameZone[i];
            if (zoneRef.RowId == 0) continue;
            var zone = zoneRef.ValueNullable?.Name.ExtractText().Trim() ?? string.Empty;

            var areaRef = i < target.PlaceNameLocation.Count ? target.PlaceNameLocation[i] : default;
            var area = areaRef.RowId == 0
                ? string.Empty
                : areaRef.ValueNullable?.Name.ExtractText().Trim() ?? string.Empty;

            var mapId = _places.FindMapByPlaceName(zoneRef.RowId);
            Vector3? pos = null;
            if (mapId != 0 && areaRef.RowId != 0)
                pos = _places.FindMarkerPosition(mapId, areaRef.RowId);

            result.Add((zone, area, mapId, pos));
        }
        return result;
    }
}
