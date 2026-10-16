using System.Numerics;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

/// <summary>An interaction entrance, not a walk-through zone border.</summary>
public sealed record InteriorEntrance(uint SourceMapId, uint TargetMapId, uint BaseId,
    byte LevelType, Vector3 Position);

internal sealed record LocalTransfer(uint MapId, uint BaseId, byte LevelType,
    Vector3 Position, Vector3 Arrival, uint WarpId)
{
    internal bool HasArrived(Vector3 player) => TravelLayout.Finite(player)
        && Vector3.Distance(player, Arrival) <= 2f
        && Vector3.Distance(player, Position) > AutoWalkService.StopRange + 0.3f;
}

/// <summary>
/// Adds entrances absent from MapMarker. EObj -> Warp -> PopRange is an explicit
/// game link; PopRange is the ARRIVAL and must never be used as the entrance.
/// NPC Warp events and their checked wrappers use the same resolver as doors.
/// Layout objects fill gaps in Level (especially inn exits). Opaque scripts
/// require a separately checked link; their destination is never guessed.
/// Native interaction and the game's conditions still decide whether to enter.
/// </summary>
internal sealed class InteriorEntranceService(IDataManager data, IPluginLog log)
{
    private List<InteriorEntrance>? _all;
    private readonly List<LocalTransfer> _localTransfers = [];
    internal IReadOnlyList<LocalTransfer> LocalTransfers(uint mapId)
    {
        ForMap(mapId);
        return _localTransfers.Where(t => t.MapId == _mapIdentity.Canonical(mapId)).ToArray();
    }
    public IReadOnlyList<InteriorEntrance> All => _all ??= Build();
    private readonly HashSet<uint> _layoutsRead = [];
    private readonly Dictionary<(byte Type, uint Id), List<Warp>> _objectWarps = [];
    private Dictionary<uint, uint>? _singleMaps;
    private readonly TravelLayout _travelLayout = new(data, log);
    private readonly TravelMapIdentity _mapIdentity = new(data);

    internal bool HasWalkingBorder(uint sourceMap, uint targetMap)
    {
        return _travelLayout.WalkingBorders(sourceMap).Any(b => b.TargetMap == _mapIdentity.Canonical(targetMap));
    }

    private uint SingleMap(uint territoryId)
    {
        if (_singleMaps == null)
        {
            var maps = new Dictionary<uint, HashSet<uint>>();
            foreach (var map in data.GetExcelSheet<Map>())
            {
                var territory = map.TerritoryType.RowId;
                if (territory == 0 || map.RowId == 0) continue;
                if (!maps.TryGetValue(territory, out var byId)) maps[territory] = byId = [];
                byId.Add(_mapIdentity.Canonical(map.RowId));
            }
            _singleMaps = maps.ToDictionary(p => p.Key, p => p.Value.Count == 1 ? p.Value.First() : 0u);
        }
        return _singleMaps.GetValueOrDefault(territoryId);
    }

    internal IReadOnlyList<InteriorEntrance> ForMap(uint mapId)
    {
        mapId = _mapIdentity.Canonical(mapId);
        _ = All;
        if (!_layoutsRead.Add(mapId) || !data.GetExcelSheet<Map>().TryGetRow(mapId, out var map))
            return All;
        var territory = map.TerritoryType.ValueNullable;
        if (territory == null) return All;
        var layout = _travelLayout.ForTerritory(territory.Value.RowId);
        foreach (var obj in layout.Objects)
        {
            // A containing MapRange resolves floors. Without one, retain an
            // exact Level map or an unambiguous single physical map.
            var level = data.GetExcelSheet<Level>().GetRowOrDefault(obj.InstanceId);
            var position = obj.Position;
            if (level is { } source)
            {
                if (source.Type != obj.Type || source.Object.RowId != obj.BaseId
                    || source.Territory.RowId != territory.Value.RowId) continue;
                var sourceMap = _mapIdentity.Canonical(layout.ResolveMap(position, source.Map.RowId));
                if (sourceMap != 0)
                {
                    if (sourceMap != mapId) continue;
                    position = new(source.X, source.Y, source.Z);
                }
                else if (SingleMap(territory.Value.RowId) == 0) continue;
            }
            else if (layout.ResolveMap(position, SingleMap(territory.Value.RowId)) != mapId) continue;
            if (!_objectWarps.TryGetValue((obj.Type, obj.BaseId), out var warps)) continue;
            foreach (var warp in warps) Add(mapId, territory.Value.RowId, obj.Type, obj.BaseId, position, warp, _all!);
        }
        return All;
    }

    private List<InteriorEntrance> Build()
    {
        var result = new List<InteriorEntrance>();
        foreach (var obj in data.GetExcelSheet<EObj>())
        {
            var warps = ResolveWarps(obj.Data, []);
            if (warps.Count > 0) _objectWarps[(45, obj.RowId)] = warps;
        }

        var refusals = new Dictionary<uint, List<Warp>>();
        foreach (var warp in data.GetExcelSheet<Warp>())
            if (warp.ConditionFailEvent.RowId != 0)
            {
                if (!refusals.TryGetValue(warp.ConditionFailEvent.RowId, out var list))
                    refusals[warp.ConditionFailEvent.RowId] = list = [];
                list.Add(warp);
            }
        foreach (var npc in data.GetExcelSheet<ENpcBase>())
        {
            var warps = new List<Warp>();
            foreach (var link in npc.ENpcData)
            {
                warps.AddRange(ResolveWarps(link, []));
                // A shared refusal event is ambiguous: accept only one Warp.
                if (link.Is<DefaultTalk>() && refusals.TryGetValue(link.RowId, out var list) && list.Count == 1)
                    warps.Add(list[0]);
            }
            if (warps.Count > 0) _objectWarps[(8, npc.RowId)] = warps.DistinctBy(w => w.RowId).ToList();
        }

        foreach (var level in data.GetExcelSheet<Level>())
            if (_objectWarps.TryGetValue((level.Type, level.Object.RowId), out var warps))
                foreach (var warp in warps) Add(level, warp);

        // Verified against quest 65983 and installed 2026.09.15 game tables:
        // Gods' Quiver bow (1000423), New Gridania -> Seat of the First Bow.
        // His DefaultTalk 590353 is Warp 131074's refusal event; the quest
        // listener names this same guard. No nearest-NPC or name guessing.
        if (data.GetExcelSheet<Warp>().TryGetRow(131074, out var firstBow)
            && firstBow.TerritoryType.RowId == 204
            && firstBow.ConditionFailEvent.RowId == 590353
            && data.GetExcelSheet<ENpcBase>().TryGetRow(1000423, out var guard)
            && guard.ENpcData.Any(d => d.Is<DefaultTalk>() && d.RowId == 590353)
            && data.GetExcelSheet<Level>().TryGetRow(3861082, out var guardLevel)
            && guardLevel.Type == 8 && guardLevel.Object.RowId == guard.RowId
            && guardLevel.Territory.RowId == 132 && guardLevel.Map.RowId == 2)
            Add(guardLevel, firstBow);
        else
            log.Warning("[Orte] First Bow entrance links changed; refusing an unverified guard route.");

        // SwitchTalk carries no client-side target. Check its measured actor,
        // the quest's territory/arrival constants, and the actual Warp together.
        if (data.GetExcelSheet<Warp>().TryGetRow(131121, out var lotus)
            && lotus.TerritoryType.RowId == 205 && lotus.ConditionFailEvent.RowId == 590536
            && data.GetExcelSheet<Quest>().TryGetRow(65985, out var quest)
            && quest.QuestParams.Any(p => p.ScriptInstruction.ExtractText() == "TERRITORYTYPE0" && p.ScriptArg == 205)
            && quest.QuestParams.Any(p => p.ScriptInstruction.ExtractText() == "POPRANGE1" && p.ScriptArg == lotus.PopRange.RowId)
            && quest.QuestListenerParams.Any(p => p.Listener == 1000460)
            && data.GetExcelSheet<ENpcBase>().TryGetRow(1000460, out var lotusGuard)
            && lotusGuard.ENpcData.Any(p => p.Is<SwitchTalk>() && p.RowId == 2031837)
            && data.GetExcelSheet<Level>().TryGetRow(3854439, out var lotusLevel)
            && lotusLevel.Type == 8 && lotusLevel.Object.RowId == 1000460
            && lotusLevel.Map.RowId == 3 && lotusLevel.Territory.RowId == 133)
            Add(lotusLevel, lotus);
        else log.Warning("[Orte] Lotus entrance links changed; refusing an unverified guard route.");

        log.Info($"[Orte] {result.Count} interaction entrances with verified Warp and Level links.");
        return result;

        void Add(Level level, Warp warp)
        {
            var actualMap = _travelLayout.ForTerritory(level.Territory.RowId).ResolveMap(new(level.X, level.Y, level.Z), level.Map.RowId);
            this.Add(actualMap, level.Territory.RowId, level.Type, level.Object.RowId,
                new(level.X, level.Y, level.Z), warp, result);
        }
    }

    private void Add(uint mapId, uint territory, byte type, uint baseId, Vector3 position,
        Warp warp, List<InteriorEntrance> result)
    {
        mapId = _mapIdentity.Canonical(mapId);
        var arrival = warp.PopRange.ValueNullable;
        var layout = _travelLayout.ForTerritory(warp.TerritoryType.RowId);
        Vector3 arrivalPosition;
        if (arrival is { } row)
        {
            if (row.Territory.RowId != warp.TerritoryType.RowId) return;
            arrivalPosition = new(row.X, row.Y, row.Z);
        }
        else if (!layout.Arrivals.TryGetValue(warp.PopRange.RowId, out arrivalPosition)) return;
        if (!TravelLayout.Finite(arrivalPosition)) return;
        var targetMap = layout.ResolveMap(arrivalPosition, arrival?.Map.RowId ?? SingleMap(warp.TerritoryType.RowId));
        // Some Warp arrival rows omit Map. Only a single-map territory is
        // unambiguous: never guess a floor in a multi-map territory.
        if (targetMap == 0) return;
        targetMap = _mapIdentity.Canonical(targetMap);
        // A gate can move the player to the other side of a wall on the SAME
        // map. It is an interaction step, not a graph loop or a walking bridge.
        if (mapId == targetMap && type is 8 or 45 && baseId != 0
            && TravelLayout.Finite(position) && Vector3.Distance(position, arrivalPosition) > 3f)
        {
            var transfer = new LocalTransfer(mapId, baseId, type, position, arrivalPosition, warp.RowId);
            if (!_localTransfers.Contains(transfer)) _localTransfers.Add(transfer);
            return;
        }
        var entrance = Validate(mapId, territory, type, baseId, position, warp.TerritoryType.RowId,
            targetMap, warp.TerritoryType.RowId,
            data.GetExcelSheet<Map>().GetRowOrDefault(mapId)?.TerritoryType.RowId ?? 0,
            data.GetExcelSheet<Map>().GetRowOrDefault(targetMap)?.TerritoryType.RowId ?? 0);
        if (entrance != null && !result.Any(e => e.SourceMapId == entrance.SourceMapId
            && e.TargetMapId == entrance.TargetMapId && e.BaseId == entrance.BaseId
            && e.LevelType == entrance.LevelType && Vector3.Distance(e.Position, entrance.Position) < 0.1f))
            result.Add(entrance);
    }

    private List<Warp> ResolveWarps(Lumina.Excel.RowRef link, HashSet<uint> visited, int depth = 0)
    {
        var result = new List<Warp>();
        if (link.RowId == 0 || depth >= 16 || !visited.Add(link.RowId)) return result;
        if (link.TryGetValue<Warp>(out var warp)) result.Add(warp);
        else if (link.TryGetValue<PreHandler>(out var pre)) result.AddRange(ResolveWarps(pre.Target, visited, depth + 1));
        else if (link.TryGetValue<ArrayEventHandler>(out var array))
            foreach (var child in array.Data) result.AddRange(ResolveWarps(child, visited, depth + 1));
        else if (link.TryGetValue<CustomTalk>(out var custom))
        {
            result.AddRange(ResolveWarps(custom.SpecialLinks, visited, depth + 1));
            foreach (var parameter in custom.Script)
                // Only an explicitly named Warp event, never a territory/quest
                // constant or an arbitrary number in a script.
                if (parameter.ScriptInstruction.ExtractText().StartsWith("WARP", StringComparison.Ordinal)
                    && data.GetExcelSheet<Warp>().TryGetRow(parameter.ScriptArg, out var scriptWarp))
                    result.Add(scriptWarp);
        }
        return result;
    }

    internal static InteriorEntrance? Validate(uint sourceMap, uint sourceTerritory, byte type, uint baseId,
        Vector3 position, uint warpTerritory, uint targetMap, uint arrivalTerritory,
        uint sourceMapTerritory, uint targetMapTerritory)
    {
        if (sourceMap == 0 || targetMap == 0 || sourceMap == targetMap || baseId == 0
            || type is not (8 or 45) || sourceTerritory == 0 || warpTerritory == 0
            || sourceTerritory != sourceMapTerritory
            || arrivalTerritory != warpTerritory || targetMapTerritory != warpTerritory
            || !float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z))
            return null;
        return new(sourceMap, targetMap, baseId, type, position);
    }
}
