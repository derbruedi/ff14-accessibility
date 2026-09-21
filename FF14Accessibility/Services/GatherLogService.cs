using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

/// <summary>
/// Resolves gathering-notebook item names to a place (Ort) and territory
/// (Gebiet) for speech in the in-game <c>GatheringNote</c> UI. Sheet-backed
/// only — does not open a menu or start auto-walk.
/// </summary>
public sealed class GatherLogService
{
    private readonly IDataManager _data;
    private readonly IClientState _clientState;
    private readonly PlacesService _places;
    private readonly IPluginLog _log;

    // GatheringItem RowId -> spots that can yield it (built once).
    private Dictionary<uint, List<ItemSpot>>? _gatherSpotsByItem;
    // FishParameter RowId -> fishing spot (built once).
    private Dictionary<uint, FishSpot>? _fishSpotsByParam;
    // Display name (ordinal ignore-case) -> GatheringItem / FishParameter RowId.
    private Dictionary<string, uint>? _gatherItemIdByName;
    private Dictionary<string, uint>? _fishParamIdByName;

    private readonly record struct ItemSpot(
        uint GatheringPointBaseId,
        uint GatheringTypeId,
        int NodeLevel,
        uint TerritoryId,
        string PlaceName,
        Vector3 Position);

    private readonly record struct FishSpot(
        string SpotName,
        uint TerritoryId);

    /// <summary>Creates a location lookup for gathering-notebook speech.</summary>
    public GatherLogService(
        IDataManager data,
        IClientState clientState,
        PlacesService places,
        IPluginLog log)
    {
        _data = data;
        _clientState = clientState;
        _places = places;
        _log = log;
    }

    /// <summary>
    /// Looks up place (Ort) and area (Gebiet) for a gathering-notebook item
    /// name. Miner/botanist: <c>GatheringPoint.PlaceName</c> + territory;
    /// fisher: <c>FishingSpot.PlaceName</c> + territory. Returns false when no
    /// sheet spot is known — caller keeps the plain row speech.
    /// </summary>
    public bool TryDescribeLocation(string itemName, out string? place, out string? area)
    {
        place = null;
        area = null;
        if (string.IsNullOrWhiteSpace(itemName)) return false;

        EnsureIndexes();
        var key = itemName.Trim();

        if (_gatherItemIdByName != null
            && _gatherItemIdByName.TryGetValue(key, out var gatherId)
            && _gatherSpotsByItem != null
            && _gatherSpotsByItem.TryGetValue(gatherId, out var spots)
            && spots.Count > 0)
        {
            var spot = PickBestSpot(spots);
            area = TerritoryName(spot.TerritoryId);
            if (string.IsNullOrWhiteSpace(area)) return false;
            place = string.IsNullOrWhiteSpace(spot.PlaceName) ? null : spot.PlaceName;
            if (place != null
                && string.Equals(place, area, StringComparison.CurrentCultureIgnoreCase))
                place = null;
            return true;
        }

        if (_fishParamIdByName != null
            && _fishParamIdByName.TryGetValue(key, out var fishId)
            && _fishSpotsByParam != null
            && _fishSpotsByParam.TryGetValue(fishId, out var fish))
        {
            area = TerritoryName(fish.TerritoryId);
            if (string.IsNullOrWhiteSpace(area)) return false;
            place = string.IsNullOrWhiteSpace(fish.SpotName) ? null : fish.SpotName;
            if (place != null
                && string.Equals(place, area, StringComparison.CurrentCultureIgnoreCase))
                place = null;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Prefer a spot whose territory is reachable with fewest hops from the
    /// player's map graph (current zone first); otherwise the first sheet spot
    /// so speech still names where to go for teleport-only zones. Prefer a
    /// named PlaceName when hops are equal.
    /// </summary>
    private ItemSpot PickBestSpot(List<ItemSpot> spots)
    {
        if (spots.Count == 1) return spots[0];

        var currentTerritory = (uint)_clientState.TerritoryType;
        var inCurrent = spots.Where(s => s.TerritoryId == currentTerritory).ToList();
        if (inCurrent.Count > 0)
            return PreferNamed(inCurrent);

        var hopsByMap = _places.GetHopDistances();
        var mapForTerritory = new Dictionary<uint, int>();
        foreach (var (mapId, hops) in hopsByMap)
        {
            var terr = _places.GetTerritoryOfMap(mapId);
            if (terr == 0) continue;
            if (!mapForTerritory.TryGetValue(terr, out var existing) || hops < existing)
                mapForTerritory[terr] = hops;
        }

        ItemSpot? best = null;
        var bestHops = int.MaxValue;
        foreach (var s in spots)
        {
            if (!mapForTerritory.TryGetValue(s.TerritoryId, out var hops)) continue;
            if (hops > bestHops) continue;
            if (hops < bestHops
                || best == null
                || (string.IsNullOrWhiteSpace(best.Value.PlaceName)
                    && !string.IsNullOrWhiteSpace(s.PlaceName)))
            {
                bestHops = hops;
                best = s;
            }
        }

        return best ?? PreferNamed(spots);
    }

    private static ItemSpot PreferNamed(List<ItemSpot> spots)
    {
        foreach (var s in spots)
            if (!string.IsNullOrWhiteSpace(s.PlaceName)) return s;
        return spots[0];
    }

    private string TerritoryName(uint territoryId)
    {
        if (territoryId == 0) return string.Empty;
        if (_data.GetExcelSheet<TerritoryType>()?.TryGetRow(territoryId, out var row) != true)
            return string.Empty;
        return row.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty;
    }

    private void EnsureIndexes()
    {
        if (_gatherSpotsByItem != null && _fishSpotsByParam != null
            && _gatherItemIdByName != null && _fishParamIdByName != null)
            return;

        _gatherSpotsByItem = BuildGatherIndex();
        _fishSpotsByParam = BuildFishIndex();
        _gatherItemIdByName = BuildGatherNameIndex(_gatherSpotsByItem);
        _fishParamIdByName = BuildFishNameIndex(_fishSpotsByParam);
        _log.Info(
            $"[GatherLog] Index: {_gatherSpotsByItem.Count} Sammel-Gegenstände, " +
            $"{_fishSpotsByParam.Count} Fische mit Spot.");
    }

    private Dictionary<string, uint> BuildGatherNameIndex(Dictionary<uint, List<ItemSpot>> spots)
    {
        var result = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        var itemSheet = _data.GetExcelSheet<GatheringItem>();
        if (itemSheet == null) return result;

        foreach (var itemId in spots.Keys)
        {
            if (!itemSheet.TryGetRow(itemId, out var gi)) continue;
            if (gi.IsHidden) continue;
            var name = ResolveGatherItemName(gi);
            if (string.IsNullOrWhiteSpace(name)) continue;
            result.TryAdd(name, itemId);
        }
        return result;
    }

    private Dictionary<string, uint> BuildFishNameIndex(Dictionary<uint, FishSpot> fishSpots)
    {
        var result = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        var sheet = _data.GetExcelSheet<FishParameter>();
        if (sheet == null) return result;

        foreach (var fishId in fishSpots.Keys)
        {
            if (!sheet.TryGetRow(fishId, out var row)) continue;
            var name = FishItemName(row);
            if (string.IsNullOrWhiteSpace(name)) continue;
            result.TryAdd(name, fishId);
        }
        return result;
    }

    private Dictionary<uint, List<ItemSpot>> BuildGatherIndex()
    {
        var result = new Dictionary<uint, List<ItemSpot>>();
        var gpSheet = _data.GetExcelSheet<GatheringPoint>();
        var expSheet = _data.GetExcelSheet<ExportedGatheringPoint>();
        var baseSheet = _data.GetExcelSheet<GatheringPointBase>();
        if (gpSheet == null || expSheet == null || baseSheet == null)
        {
            _log.Warning("[GatherLog] GatheringPoint-Sheets fehlen.");
            return result;
        }

        var baseMeta = new Dictionary<uint, (uint Territory, string PlaceName, Vector3 Pos)>();
        foreach (var gp in gpSheet)
        {
            var baseId = gp.GatheringPointBase.RowId;
            if (baseId == 0 || baseMeta.ContainsKey(baseId)) continue;
            if (!expSheet.TryGetRow(baseId, out var exp)) continue;
            // EXDSchema GatheringPoint.PlaceName — map sub-area (narrower than
            // TerritoryType.PlaceName). Empty on some nodes; speech then uses Gebiet only.
            var place = gp.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty;
            baseMeta[baseId] = (gp.TerritoryType.RowId, place, new Vector3(exp.X, 0f, exp.Y));
        }

        foreach (var gpBase in baseSheet)
        {
            if (!baseMeta.TryGetValue(gpBase.RowId, out var meta)) continue;
            var typeId = gpBase.GatheringType.RowId;
            var nodeLevel = gpBase.GatheringLevel;

            foreach (var itemRef in gpBase.Item)
            {
                var itemId = itemRef.RowId;
                if (itemId == 0) continue;
                if (_data.GetExcelSheet<GatheringItem>()?.TryGetRow(itemId, out _) != true)
                    continue;

                if (!result.TryGetValue(itemId, out var list))
                {
                    list = [];
                    result[itemId] = list;
                }
                list.Add(new ItemSpot(
                    gpBase.RowId, typeId, nodeLevel, meta.Territory, meta.PlaceName, meta.Pos));
            }
        }

        return result;
    }

    private Dictionary<uint, FishSpot> BuildFishIndex()
    {
        var result = new Dictionary<uint, FishSpot>();
        var sheet = _data.GetExcelSheet<FishParameter>();
        var spotSheet = _data.GetExcelSheet<FishingSpot>();
        if (sheet == null || spotSheet == null) return result;

        foreach (var row in sheet)
        {
            if (!row.IsInLog || row.IsHidden) continue;
            var spotRef = row.FishingSpot;
            if (spotRef.RowId == 0) continue;
            if (!spotSheet.TryGetRow(spotRef.RowId, out var spot)) continue;
            if (spot.X == 0 && spot.Z == 0) continue;

            var spotName = spot.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty;
            result[row.RowId] = new FishSpot(spotName, spot.TerritoryType.RowId);
        }

        return result;
    }

    private string ResolveGatherItemName(GatheringItem gi)
    {
        var itemId = gi.Item.RowId;
        if (itemId == 0) return string.Empty;
        if (_data.GetExcelSheet<Item>()?.TryGetRow(itemId, out var item) == true)
        {
            var n = item.Name.ExtractText();
            if (!string.IsNullOrWhiteSpace(n)) return n;
        }
        if (_data.GetExcelSheet<EventItem>()?.TryGetRow(itemId, out var ev) == true)
        {
            var n = ev.Name.ExtractText();
            if (!string.IsNullOrWhiteSpace(n)) return n;
        }
        return $"#{gi.RowId}";
    }

    private string FishItemName(FishParameter row)
    {
        var itemId = row.Item.RowId;
        if (_data.GetExcelSheet<Item>()?.TryGetRow(itemId, out var item) == true)
            return item.Name.ExtractText();
        if (_data.GetExcelSheet<EventItem>()?.TryGetRow(itemId, out var ev) == true)
            return ev.Name.ExtractText();
        return $"#{row.RowId}";
    }

#if DEBUG
    /// <summary>DEBUG: dump notebook / index samples for game-api.md.</summary>
    public string Probe()
    {
        EnsureIndexes();
        var lines = new List<string>
        {
            $"GatherIndex={_gatherSpotsByItem?.Count} FishIndex={_fishSpotsByParam?.Count}",
        };

        var div = _data.GetExcelSheet<NotebookDivision>();
        if (div != null)
        {
            var n = 0;
            foreach (var row in div)
            {
                if (row.GatheringOpeningLevel == 0) continue;
                lines.Add($"NotebookDivision {row.RowId}: '{row.Name.ExtractText()}' openLvl={row.GatheringOpeningLevel}");
                if (++n >= 12) break;
            }
        }

        var lists = _data.GetExcelSheet<GatheringNotebookList>();
        if (lists != null)
        {
            foreach (var rowId in new uint[] { 1, 2, 10, 20 })
            {
                if (!lists.TryGetRow(rowId, out var list)) continue;
                var count = list.GatheringItem.Count(i => i.RowId != 0);
                lines.Add($"GatheringNotebookList {rowId}: {count} items");
            }
        }

        var msg = string.Join(" | ", lines);
        _log.Info($"[GatherLog] Probe: {msg}");
        return msg;
    }
#endif
}
