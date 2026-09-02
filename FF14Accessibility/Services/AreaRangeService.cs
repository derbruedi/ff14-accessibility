using System.Numerics;
using Dalamud.Plugin.Services;
using Lumina.Data.Files;
using Lumina.Data.Parsing.Layer;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

/// <summary>
/// One piece of a named area, as the zone layout defines it.
/// </summary>
/// <param name="Centre">World position of the piece's centre, full 3D - unlike
/// a map marker, the layout knows the height.</param>
/// <param name="SpotName">Name of the sub-place, empty when the piece only
/// belongs to the block ("Sandtor") without a name of its own.</param>
/// <param name="Extent">Half-extent of the piece as the layout stores it. Used
/// ONLY to order pieces by size, never spoken as a measurement - see the class
/// summary for why that number is not verified.</param>
public sealed record AreaPart(Vector3 Centre, string SpotName, float Extent);

/// <summary>
/// The real outline of a named area, read from the zone's layout files instead
/// of the map.
///
/// WHY THIS EXISTS. The hunting log names a habitat ("Sandtor"), and the only
/// position the map sheet carries for it is the map's TEXT LABEL - one point
/// for the whole area. Measured 2026-09-02 for Östliches Thanalan: "Sandtor" is
/// not a point but SIX pieces spanning roughly 500 by 400 metres, and the label
/// sits near their edge at (-284|379), while the sub-place "Amalj'aa-Feldlager"
/// - part of the same block - sits at (-90|275), some 230 metres away. A player
/// walked to the label and found nothing, which is exactly what the data
/// predicts.
///
/// WHAT THE GAME DOES AND DOES NOT KNOW. There are no spawn points in the
/// client: the Level sheet holds battle NPCs only for quests (checked for
/// "Amalj'aa-Jäger" - two rows, both in other zones, none in Östliches
/// Thanalan), everything else the server spawns. So the best the mod can do is
/// take the player through the area and watch the object table. That is what
/// these pieces are for - they are search points, not monster positions.
///
/// SOURCE. LayerEntryType.MapRange in the zone's planmap.lgb / planevent.lgb,
/// the same files vnavmesh reads for collision. Each carries PlaceNameBlock
/// (the big area) and PlaceNameSpot (a named place inside it), so both the
/// block and its sub-places are found by the same lookup. Territory 145 holds
/// 101 of them, 66 in planmap.lgb and 35 in planevent.lgb; bg.lgb has none.
///
/// WHAT IS DELIBERATELY NOT CLAIMED: the meaning of Transform.Scale. It is
/// plausibly the half-extent (the Sandtor label falls inside the big cylinder
/// under that reading), but nothing measured proves it, so no size is ever
/// spoken and no grid is laid out from it. Only the CENTRES are used, and those
/// are certain.
/// </summary>
public sealed class AreaRangeService
{
    private readonly IDataManager _data;
    private readonly IPluginLog _log;

    // Territory -> every MapRange piece it holds. Layout files never change at
    // runtime, so one read per zone is enough.
    private readonly Dictionary<uint, List<Piece>> _byTerritory = [];

    /// <summary>One layout piece with the two place names it carries.</summary>
    private readonly record struct Piece(uint Block, uint Spot, Vector3 Centre, float Extent);

    public AreaRangeService(IDataManager data, IPluginLog log)
    {
        _data = data;
        _log = log;
    }

    /// <summary>
    /// The pieces of one named area in one zone: every layout range whose block
    /// OR sub-place is that name, nearest to <paramref name="from"/> first.
    ///
    /// Matched by place name ROW first and by TEXT as a fallback, because the
    /// game keeps several rows for the same name: the hunting log points at
    /// "Halatali" row 49 (the instance) while the zone layout and the map use
    /// row 305 (the place in the world). Same word, different row - and the
    /// row-only comparison is why the mod used to answer "not marked on the
    /// map" for a place that is right there.
    /// </summary>
    /// <param name="territoryId">Zone to read the layout of.</param>
    /// <param name="placeNameRowId">Place name row the hunting log names.</param>
    /// <param name="placeNameText">Its display text, for the fallback match.</param>
    /// <param name="from">Position to sort by - usually the player.</param>
    public List<AreaPart> GetParts(uint territoryId, uint placeNameRowId, string placeNameText, Vector3 from)
    {
        var result = new List<AreaPart>();
        if (territoryId == 0 || (placeNameRowId == 0 && placeNameText.Length == 0)) return result;

        var pieces = GetPieces(territoryId);
        if (pieces.Count == 0) return result;

        var names = _data.GetExcelSheet<PlaceName>();
        string TextOf(uint row) => row == 0
            ? string.Empty
            : names.TryGetRow(row, out var p) ? p.Name.ExtractText().Trim() : string.Empty;

        var wanted = placeNameText.Trim();
        bool Matches(uint row) =>
            row != 0 && (row == placeNameRowId
                         || (wanted.Length > 0
                             && string.Equals(TextOf(row), wanted, StringComparison.OrdinalIgnoreCase)));

        foreach (var piece in pieces)
        {
            if (!Matches(piece.Block) && !Matches(piece.Spot)) continue;
            result.Add(new AreaPart(piece.Centre, TextOf(piece.Spot), piece.Extent));
        }

        result.Sort((a, b) => Distance2D(from, a.Centre).CompareTo(Distance2D(from, b.Centre)));
        return result;
    }

    private static float Distance2D(Vector3 a, Vector3 b)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    /// <summary>
    /// Every MapRange piece of a zone, read once and kept. A zone that cannot
    /// be read at all is cached as empty - retrying it on every keypress would
    /// cost the same parse again for the same answer.
    /// </summary>
    private List<Piece> GetPieces(uint territoryId)
    {
        if (_byTerritory.TryGetValue(territoryId, out var cached)) return cached;

        var pieces = new List<Piece>();
        _byTerritory[territoryId] = pieces;

        if (!_data.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var territory))
        {
            _log.Info($"[Areal] Territory {territoryId} steht nicht im Sheet.");
            return pieces;
        }

        var bg = territory.Bg.ExtractText();
        var cut = bg.LastIndexOf("/level/", StringComparison.Ordinal);
        if (cut < 0)
        {
            _log.Info($"[Areal] Territory {territoryId} hat keinen Layout-Pfad ('{bg}').");
            return pieces;
        }
        var directory = "bg/" + bg[..(cut + 7)];

        // Only the two files that actually carry MapRange entries. bg.lgb is the
        // big one (7461 objects in Östliches Thanalan) and holds none of them -
        // reading it would cost the parse for nothing.
        foreach (var file in new[] { "planmap.lgb", "planevent.lgb" })
        {
            LgbFile? lgb;
            try
            {
                lgb = _data.GetFile<LgbFile>(directory + file);
            }
            catch (Exception ex)
            {
                // External file access: the layout of a zone can be missing or
                // renamed by a patch, and that must never take the browser down.
                _log.Warning($"[Areal] {directory}{file} nicht lesbar: {ex.Message}");
                continue;
            }
            if (lgb == null) continue;

            foreach (var layer in lgb.Layers)
            {
                foreach (var instance in layer.InstanceObjects)
                {
                    if (instance.AssetType != LayerEntryType.MapRange) continue;
                    if (instance.Object is not LayerCommon.MapRangeInstanceObject range) continue;

                    var t = instance.Transform;
                    pieces.Add(new Piece(
                        range.PlaceNameBlock,
                        range.PlaceNameSpot,
                        new Vector3(t.Translation.X, t.Translation.Y, t.Translation.Z),
                        MathF.Max(t.Scale.X, t.Scale.Z)));
                }
            }
        }

        _log.Info($"[Areal] Territory {territoryId}: {pieces.Count} Ortsbereiche aus {directory}.");
        return pieces;
    }
}
