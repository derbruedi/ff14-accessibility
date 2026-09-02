using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
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
/// <param name="AreaPlaceNameId">Place name row of the habitat. Needed because
/// the map label is only ONE point of what is often a very large area - the
/// layout pieces behind that name are looked up with it, see
/// <see cref="AreaRangeService"/>.</param>
/// <param name="TerritoryId">Territory of the habitat's map, for the same
/// lookup. 0 when unknown.</param>
public sealed record HuntingTarget(
    string MonsterName,
    int Killed,
    int Required,
    string ZoneName,
    string AreaName,
    uint MapId,
    bool InCurrentZone,
    Vector3? Position,
    uint AreaPlaceNameId = 0,
    uint TerritoryId = 0);

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
///
/// THE THREE GRAND COMPANY LOGS ARE THE SAME MECHANIC, one block each, and the
/// game says so itself - measured offline against the sheets 2026-09-02:
/// - GrandCompany carries a MonsterNote field with exactly the semantics of the
///   ClassJob one: Mahlstrom 8, Bruderschaft der Morgenviper 9, Legion der
///   Unsterblichen 10, and 127 for "Keine" - the same 127 the crafters carry.
///   That closes the only gap: the nine class indices are 0..7 and 11, so which
///   of the twelve MonsterNoteManager slots belongs to which company was NOT
///   derivable from the sheet order (Schurke sits at 290001 but on index 11).
///   Nothing here is guessed from the gap; the number is read from the sheet.
/// - Their blocks are 1000001, 2000001 and 3000001 and are found by the very
///   same name match as the classes ("Mahlstrom 01"), once the sheet's
///   placeholder is dropped: GrandCompany.Name reads "Bruderschaft[p] der
///   Morgenviper" while the block is called "Bruderschaft der Morgenviper 01".
/// - A company block holds THREE ranks of ten, not five: rows 31..50 exist but
///   ask for nothing (all Count entries 0) in all three blocks.
/// - All 30 targets per company name a habitat, so the category can always say
///   where to go - checked for all three.
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

    /// <summary>Ranks a grand company log has, against five for a class: rows
    /// 31..50 of those blocks ask for nothing (measured, see class summary).</summary>
    private const int RanksPerGrandCompany = 3;

    /// <summary>ClassJob.MonsterNote for a class without a hunting log - and
    /// GrandCompany.MonsterNote for "Keine", which uses the same value.</summary>
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
    /// The hunting log index of the grand company the player belongs to (8, 9
    /// or 10), or null when they have not joined one yet - which is most of the
    /// early game, the companies open up in the level 20 story.
    ///
    /// PlayerState.GrandCompany is the game's own membership field and indexes
    /// the GrandCompany sheet (0 = none). The index itself is never derived from
    /// that number: it is read out of the sheet row, exactly like a class reads
    /// ClassJob.MonsterNote. A membership value the sheet does not know, or one
    /// whose row carries no log, yields null instead of a guess.
    /// </summary>
    public unsafe uint? GetGrandCompanyIndex()
    {
        var state = PlayerState.Instance();
        if (state == null)
        {
            _log.Info("[Jagd] PlayerState.Instance() ist null - Gesellschaft nicht lesbar.");
            return null;
        }

        var company = (uint)state->GrandCompany;
        if (company == 0) return null;                 // keiner Gesellschaft beigetreten
        if (!_data.GetExcelSheet<GrandCompany>().TryGetRow(company, out var row))
        {
            _log.Info($"[Jagd] Gesellschaft {company} steht nicht im GrandCompany-Sheet.");
            return null;
        }

        var index = row.MonsterNote.RowId;
        if (index == NoHuntingLog || index == uint.MaxValue) return null;

        // Einmal pro Wechsel ins Log, nicht bei jeder Abfrage: die Kategorie
        // fragt beim Blaettern staendig nach. Die Zeile belegt im Testlog, WELCHE
        // Gesellschaft erkannt wurde und auf welchen Block sie fuehrt.
        if (company != _lastLoggedCompany)
        {
            _lastLoggedCompany = company;
            _log.Info($"[Jagd] Gesellschaft {company} '{StripPlaceholders(row.Name.ExtractText())}' " +
                      $"-> Jagdtagebuch-Index {index}.");
        }
        return index;
    }

    /// <summary>Last grand company value written to the log, so the line above
    /// appears once per change instead of once per browser keypress.</summary>
    private uint _lastLoggedCompany = uint.MaxValue;

    /// <summary>
    /// The name of the player's grand company as the game spells it, or an
    /// empty string when they belong to none. Spoken in the category header so
    /// a wrong membership would be audible at once instead of silently listing
    /// the wrong company's monsters.
    /// </summary>
    public unsafe string GetGrandCompanyName()
    {
        var state = PlayerState.Instance();
        if (state == null) return string.Empty;
        var company = (uint)state->GrandCompany;
        if (company == 0) return string.Empty;
        if (!_data.GetExcelSheet<GrandCompany>().TryGetRow(company, out var row)) return string.Empty;
        return StripPlaceholders(row.Name.ExtractText());
    }

    /// <summary>
    /// Drops the German sheet's grammar placeholders from a company name
    /// ("Bruderschaft[p] der Morgenviper"). Unlike a monster name there is no
    /// ending to fill in - the block the game itself writes is called
    /// "Bruderschaft der Morgenviper 01", so the placeholder simply goes away.
    /// </summary>
    private static string StripPlaceholders(string text)
    {
        if (!text.Contains('[')) return text.Trim();
        return text.Replace("[a]", string.Empty)
                   .Replace("[p]", string.Empty)
                   .Replace("[t]", string.Empty)
                   .Trim();
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

        // Die drei Gesellschaften stehen in derselben Tabelle und werden ueber
        // denselben Namen gefunden - nur der Platzhalter der deutschen Zeile
        // muss weg, sonst passt "Bruderschaft[p] der Morgenviper" nie auf den
        // Block "Bruderschaft der Morgenviper 01".
        foreach (var company in _data.GetExcelSheet<GrandCompany>())
        {
            var index = company.MonsterNote.RowId;
            if (index == NoHuntingLog || index == uint.MaxValue) continue;
            var name = StripPlaceholders(company.Name.ExtractText());
            if (name.Length == 0) continue;
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

        _log.Info($"[Jagd] Bloecke (Klassen + Gesellschaften): {starts.Count} " +
                  string.Join(", ", starts.OrderBy(s => s.Key).Select(s => $"{s.Key}->{s.Value}")));
        return starts;
    }

    /// <summary>
    /// The ten MonsterNote rows of one rank (1-based) of a class or company, in
    /// log order. Empty when there is no block or the rank is out of range.
    /// </summary>
    /// <param name="classIndex">Hunting log index (class 0..11, company 8..10).</param>
    /// <param name="rank">Rank as the window counts it, 1-based.</param>
    /// <param name="maxRank">Ranks this block has - five for a class, three for
    /// a company.</param>
    public List<MonsterNote> GetRankEntries(uint classIndex, int rank, int maxRank = RanksPerClass)
    {
        var result = new List<MonsterNote>();
        if (rank < 1 || rank > maxRank) return result;
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
    public List<HuntingTarget> GetOpenTargets()
        => GetCurrentClassIndex() is { } classIndex
            ? GetOpenTargetsFor(classIndex, RanksPerClass, "Klasse")
            : new List<HuntingTarget>();

    /// <summary>
    /// The same list for the GRAND COMPANY log of the company the player
    /// belongs to. Empty when they belong to none or the rank is finished.
    ///
    /// Deliberately a second list and not merged into the one above: the two
    /// logs run independently (a company rank is not touched by class kills),
    /// and mixing them would leave the player unable to tell which log a kill
    /// still counts for - which is the whole reason to look.
    /// </summary>
    public List<HuntingTarget> GetOpenGrandCompanyTargets()
        => GetGrandCompanyIndex() is { } companyIndex
            ? GetOpenTargetsFor(companyIndex, RanksPerGrandCompany, "Gesellschaft")
            : new List<HuntingTarget>();

    /// <summary>
    /// Shared core of both lists above: the open entries of one block's current
    /// rank. Kept in one place so a class and a company entry can never be read
    /// or spelled differently.
    /// </summary>
    /// <param name="classIndex">Hunting log index of the block.</param>
    /// <param name="maxRank">Ranks the block has (5 class, 3 company).</param>
    /// <param name="what">What to call the block in the log line.</param>
    private unsafe List<HuntingTarget> GetOpenTargetsFor(uint classIndex, int maxRank, string what)
    {
        var result = new List<HuntingTarget>();
        if (GetProgress(classIndex) is not { } progress) return result;

        var rows = GetRankEntries(classIndex, progress.Rank, maxRank);
        if (rows.Count == 0)
        {
            // Ein fertig gejagter Block meldet sich genau so: das Spiel zaehlt
            // den Rang ueber den letzten hinaus, und dann gibt es keine Zeilen
            // mehr. Kein Fehler, nur nichts mehr zu tun.
            _log.Info($"[Jagd] {what} {classIndex}, Rang {progress.Rank}: keine Sheet-Zeilen.");
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
                    best.Position,
                    best.AreaId,
                    GetTerritoryOfMap(best.MapId)));
            }
        }

        _log.Info($"[Jagd] {what} {classIndex}, Rang {progress.Rank}: {result.Count} offene Ziele.");
        return result;
    }

    /// <summary>
    /// The spoken name of a monster, sheet placeholders resolved. The rules and
    /// what backs them live in <see cref="MonsterNameText"/> - the levequest
    /// category reads the same sheet and must spell a monster the same way.
    /// </summary>
    private string ResolveMonsterName(BNpcName nameRow)
        => MonsterNameText.Resolve(nameRow, _data.Language);

    /// <summary>
    /// The nearest LIVE specimen of a monster that the game currently has
    /// loaded, or null when none is in range. This is what turns a hunting log
    /// entry from a place into a target: the map marker is the centre of the
    /// habitat, the monster is what the player actually wants to reach.
    ///
    /// Matched by NAME, the same way the bestiary window match has worked since
    /// V5.86: the object table carries the displayed name, and that is exactly
    /// what <see cref="ResolveMonsterName"/> rebuilds from the sheet. Case is
    /// ignored - the sheet stem starts lowercase ("wuchernd[a] Efeuranke"),
    /// the game displays it capitalised.
    ///
    /// Dead ones are skipped (CurrentHp 0): a corpse is not huntable, and a
    /// respawn arrives as a new object anyway.
    /// </summary>
    public IGameObject? FindNearestLive(string monsterName)
    {
        if (monsterName.Length == 0) return null;
        var player = _objectTable.LocalPlayer;
        if (player == null) return null;

        IGameObject? nearest = null;
        var nearestDist = float.MaxValue;
        foreach (var obj in _objectTable)
        {
            if (obj.ObjectKind != Dalamud.Game.ClientState.Objects.Enums.ObjectKind.BattleNpc) continue;
            if (!string.Equals(obj.Name.TextValue, monsterName, StringComparison.OrdinalIgnoreCase)) continue;
            if (obj is IBattleChara { CurrentHp: 0 }) continue;
            var dist = Vector3.Distance(player.Position, obj.Position);
            if (dist < nearestDist) { nearest = obj; nearestDist = dist; }
        }
        return nearest;
    }

    /// <summary>
    /// Debug probe for the one assumption the match above rests on: that the
    /// name we rebuild from the sheet is spelled exactly like the one the game
    /// puts on the object. When nothing was found, the battle NPCs actually
    /// standing around are logged - a spelling mismatch shows up immediately as
    /// a near-identical name in that list, where a genuinely absent monster
    /// shows up as an unrelated one.
    /// </summary>
    public void LogNearbyBattleNpcs(string monsterName)
    {
#if DEBUG
        var player = _objectTable.LocalPlayer;
        if (player == null) return;

        var names = _objectTable
            .Where(o => o.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.BattleNpc)
            .Select(o => (Name: o.Name.TextValue,
                          NameId: (o as ICharacter)?.NameId ?? 0,
                          Dist: Vector3.Distance(player.Position, o.Position)))
            .OrderBy(x => x.Dist)
            .Take(8)
            .Select(x => $"'{x.Name}' (NameId={x.NameId}, {x.Dist:F0} m)");

        _log.Info($"[JagdSonde] Kein lebendes '{monsterName}' gefunden. In der Naehe: " +
                  string.Join(", ", names));
#endif
    }

    /// <summary>The territory a map belongs to, 0 when unknown. The layout
    /// files are keyed by territory, the hunting log only knows the map.</summary>
    private uint GetTerritoryOfMap(uint mapId)
        => mapId != 0 && _data.GetExcelSheet<Lumina.Excel.Sheets.Map>().TryGetRow(mapId, out var map)
            ? map.TerritoryType.RowId
            : 0;

    /// <summary>
    /// Habitats of a hunting log monster as walkable destinations: zone, area
    /// and - where the area carries a map marker - its world position. The
    /// area's place name ROW travels with it: the map label is one point of
    /// what can be a very large area, and the layout pieces behind that name
    /// are looked up by row (see <see cref="AreaRangeService"/>).
    /// </summary>
    public List<(string Zone, string Area, uint MapId, Vector3? Position, uint AreaId)> GetHabitats(MonsterNoteTarget target)
    {
        var result = new List<(string, string, uint, Vector3?, uint)>();
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
                pos = _places.FindMarkerPosition(mapId, areaRef.RowId, area);

            result.Add((zone, area, mapId, pos, areaRef.RowId));
        }
        return result;
    }
}
