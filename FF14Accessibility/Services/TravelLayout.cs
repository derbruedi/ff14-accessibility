using System.Buffers.Binary;
using System.Numerics;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

internal sealed record TravelMapRange(uint MapId, Vector3 Position, Vector3 Rotation,
    Vector3 Scale, int Shape, short Priority)
{
    internal bool Contains(Vector3 point)
    {
        // Map boxes use half extents. Rotated X/Z boxes are not inferred.
        if (MathF.Abs(Rotation.X) > 0.0001f || MathF.Abs(Rotation.Z) > 0.0001f) return false;
        var local = Vector3.Transform(point - Position, Matrix4x4.CreateRotationY(-Rotation.Y));
        var scale = Vector3.Abs(Scale);
        return Shape switch
        {
            1 => MathF.Abs(local.X) <= scale.X && MathF.Abs(local.Y) <= scale.Y && MathF.Abs(local.Z) <= scale.Z,
            2 => scale.X > 0 && local.LengthSquared() <= scale.X * scale.X,
            3 => MathF.Abs(local.Y) <= scale.Y && local.X * local.X + local.Z * local.Z <= scale.X * scale.X,
            _ => false,
        };
    }
}

internal sealed class TravelLayoutData
{
    internal List<EntranceLayoutObject> Objects { get; } = [];
    internal Dictionary<uint, Vector3> Arrivals { get; } = [];
    internal List<TravelMapRange> Maps { get; } = [];
    internal List<(uint Territory, Vector3 Position)> Exits { get; } = [];
    internal List<TravelBorder> Borders { get; } = [];

    internal uint MapAt(Vector3 point)
    {
        var matches = Maps.Where(m => m.Contains(point)).ToArray();
        if (matches.Length == 0) return 0;
        var priority = matches.Max(m => m.Priority);
        var ids = matches.Where(m => m.Priority == priority).Select(m => m.MapId).Distinct().ToArray();
        return ids.Length == 1 ? ids[0] : 0;
    }

    internal uint ResolveMap(Vector3 point, uint fallback)
        => Maps.Any(m => m.Contains(point)) ? MapAt(point) : fallback;
}

/// <summary>Bounded reading of MapRange and PopRange, verified against Lumina
/// LayerCommon. An arrival's old Level.Map can name the city's main floor;
/// the containing MapRange supplies the actual floor, without nearest guessing.</summary>
internal sealed class TravelLayout(IDataManager data, IPluginLog log)
{
    private readonly Dictionary<uint, TravelLayoutData> _territories = [];
    private readonly TravelMapIdentity _mapIdentity = new(data);
    private Dictionary<uint, uint>? _singleMaps;

    internal uint SingleMap(uint territory)
    {
        _singleMaps ??= data.GetExcelSheet<Map>().Where(m => m.RowId != 0 && m.TerritoryType.RowId != 0)
            .GroupBy(m => m.TerritoryType.RowId).ToDictionary(g => g.Key, g =>
            {
                var maps = g.Select(m => _mapIdentity.Canonical(m.RowId)).Distinct().ToArray();
                return maps.Length == 1 ? maps[0] : 0u;
            });
        return _singleMaps.GetValueOrDefault(territory);
    }

    internal uint ArrivalMap(uint territory, uint instance)
    {
        var layout = ForTerritory(territory);
        var row = data.GetExcelSheet<Level>().GetRowOrDefault(instance);
        if (row is { } foreign && foreign.Territory.RowId != territory) return 0;
        if (row is { } level && level.Territory.RowId == territory)
            return _mapIdentity.Canonical(layout.ResolveMap(new(level.X, level.Y, level.Z), level.Map.RowId));
        return layout.Arrivals.TryGetValue(instance, out var point)
            ? _mapIdentity.Canonical(layout.ResolveMap(point, SingleMap(territory))) : SingleMap(territory);
    }

    internal IReadOnlyList<(uint TargetMap, TravelBorder Border)> WalkingBorders(uint sourceMap)
    {
        sourceMap = _mapIdentity.Canonical(sourceMap);
        var territory = data.GetExcelSheet<Map>().GetRowOrDefault(sourceMap)?.TerritoryType.RowId ?? 0;
        var layout = ForTerritory(territory);
        var result = new List<(uint, TravelBorder)>();
        foreach (var border in layout.Borders)
        {
            var source = data.GetExcelSheet<Level>().GetRowOrDefault(border.InstanceId);
            var fallback = source is { } level && level.Territory.RowId == territory ? level.Map.RowId : SingleMap(territory);
            if (_mapIdentity.Canonical(layout.ResolveMap(border.Centre, fallback)) != sourceMap) continue;
            var target = ArrivalMap(border.Destination, border.ArrivalId);
            if (target != 0 && target != sourceMap) result.Add((target, border));
        }
        return result;
    }

    internal TravelLayoutData ForTerritory(uint id)
    {
        if (_territories.TryGetValue(id, out var cached)) return cached;
        var result = new TravelLayoutData();
        _territories[id] = result;
        if (!data.GetExcelSheet<TerritoryType>().TryGetRow(id, out var territory)) return result;
        var bg = territory.Bg.ExtractText();
        var cut = bg.LastIndexOf("/level/", StringComparison.Ordinal);
        if (cut < 0) return result;
        foreach (var file in new[] { "planmap.lgb", "planevent.lgb", "planner.lgb" })
        {
            byte[]? bytes;
            try { bytes = data.GetFile<Lumina.Data.FileResource>("bg/" + bg[..(cut + 7)] + file)?.Data; }
            catch (Exception ex) { log.Warning($"[QuestRoute] Layout {id}/{file}: {ex.Message}"); continue; }
            if (bytes == null) continue;
            var parsed = Read(bytes);
            result.Objects.AddRange(parsed.Objects);
            result.Maps.AddRange(parsed.Maps.Where(m => data.GetExcelSheet<Map>().GetRowOrDefault(m.MapId)?.TerritoryType.RowId == id)
                .Select(m => m with { MapId = _mapIdentity.Canonical(m.MapId) }));
            result.Exits.AddRange(parsed.Exits);
            result.Borders.AddRange(parsed.Borders);
            foreach (var pair in parsed.Arrivals) result.Arrivals.TryAdd(pair.Key, pair.Value);
        }
        return result;
    }

    internal static TravelLayoutData Read(byte[] bytes)
    {
        var result = new TravelLayoutData();
        try
        {
            if (bytes.Length < 36 || !bytes.AsSpan(0, 4).SequenceEqual("LGB1"u8)
                || !bytes.AsSpan(12, 4).SequenceEqual("LGP1"u8)) return result;
            var table = checked(20 + I(28));
            var count = I(32);
            Bounds(table, count, 4);
            for (var n = 0; n < count; n++)
            {
                var layer = checked(table + I(table + n * 4));
                Bounds(layer, 1, 16);
                var objects = checked(layer + I(layer + 8));
                var objectCount = I(layer + 12);
                Bounds(objects, objectCount, 4);
                for (var k = 0; k < objectCount; k++)
                {
                    var obj = checked(objects + I(objects + k * 4));
                    Bounds(obj, 1, 48);
                    var type = I(obj);
                    var pos = V(obj + 12);
                    if (!Finite(pos)) continue;
                    if (type is 8 or 45)
                    {
                        Bounds(obj, 1, 52);
                        result.Objects.Add(new(U(obj + 4), (byte)type, U(obj + 48), pos));
                    }
                    else if (type == 40) result.Arrivals.TryAdd(U(obj + 4), pos);
                    else if (type == 41)
                    {
                        Bounds(obj, 1, 88);
                        if (bytes[obj + 54] != 0 && I(obj + 60) == 1) // ZoneLine, not an invisible scripted range.
                        {
                            result.Exits.Add((BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(obj + 66, 2)), pos));
                            var rot = V(obj + 24);
                            var scale = V(obj + 36);
                            var target = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(obj + 66, 2));
                            if (I(obj + 48) == 1 && target != 0 && Finite(rot) && Finite(scale)
                                && MathF.Abs(rot.X) < 0.0001f && MathF.Abs(rot.Z) < 0.0001f
                                && scale.X > 0 && scale.Y > 0 && scale.Z > 0)
                                result.Borders.Add(new(pos, scale, rot.Y, target, U(obj + 72), U(obj + 4)));
                        }
                    }
                    else if (type == 43)
                    {
                        Bounds(obj, 1, 110);
                        var rot = V(obj + 24);
                        var scale = V(obj + 36);
                        if (bytes[obj + 54] != 0 && bytes[obj + 93] != 0 && U(obj + 60) != 0
                            && Finite(rot) && Finite(scale) && scale.X > 0 && scale.Y > 0 && scale.Z > 0)
                            result.Maps.Add(new(U(obj + 60), pos, rot, scale, I(obj + 48),
                                BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(obj + 52, 2))));
                    }
                }
            }
        }
        catch (Exception ex) when (ex is ArgumentOutOfRangeException or OverflowException or InvalidDataException)
        { return new(); }
        return result;

        int I(int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4));
        uint U(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
        float F(int offset) => BitConverter.Int32BitsToSingle(I(offset));
        Vector3 V(int offset) => new(F(offset), F(offset + 4), F(offset + 8));
        void Bounds(int offset, int count, int size)
        {
            if (offset < 0 || count < 0 || count > 100_000 || (long)offset + (long)count * size > bytes.Length)
                throw new InvalidDataException("Invalid travel layout offset/count.");
        }
    }

    internal static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
}
