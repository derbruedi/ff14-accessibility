using System.Numerics;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

internal sealed record TravelBorder(Vector3 Centre, Vector3 HalfExtent, float Yaw, uint Destination,
    uint ArrivalId = 0, uint InstanceId = 0)
{
    internal Vector3 Local(Vector3 world)
        => Vector3.Transform(world - Centre, Matrix4x4.CreateRotationY(-Yaw));

    internal Vector3 World(Vector3 local)
        => Centre + Vector3.Transform(local, Matrix4x4.CreateRotationY(Yaw));

    internal bool Contains(Vector3 point)
    {
        if (!TravelLayout.Finite(point)) return false;
        var local = Vector3.Abs(Local(point));
        return local.X <= HalfExtent.X && local.Y <= HalfExtent.Y && local.Z <= HalfExtent.Z;
    }
}

internal sealed record BorderResolution(bool HasBorder, Vector3? Position);

/// <summary>Resolves a real ExitRange volume onto the road. The centre of a
/// tall volume is not its floor. Geometry and bounded mesh probes supply the
/// destination; the existing movement and short border nudge remain unchanged.</summary>
public sealed class ZoneBorderService
{
    // More than the normal 0.5m transition stop range, but does not add two
    // unnecessary metres to the gap between the mesh edge and a thin trigger.
    private const float Inset = 1f;
    private const float MaxApproachGap = 5.5f; // Below ZoneTransitionHandler's 6m limit.
    private readonly IDataManager _data;
    private readonly IClientState _clientState;
    private readonly IPluginLog _log;
    private readonly TravelLayout _layout;

    public ZoneBorderService(IDataManager data, IClientState clientState, IPluginLog log)
    {
        _data = data; _clientState = clientState; _log = log;
        _layout = new(data, log);
    }

    public Vector3? FindBorderPoint(uint destinationMapId, Vector3 from,
        Func<Vector3, float, float, Vector3?>? reachable = null)
        => Resolve(destinationMapId, from, reachable).Position;

    internal BorderResolution Resolve(uint destinationMapId, Vector3 from,
        Func<Vector3, float, float, Vector3?>? reachable,
        Func<Vector3, Vector3?>? disconnectedFloor = null,
        Func<Vector3, bool>? hasMeasuredCrossing = null)
    {
        if (destinationMapId == 0 || !_data.GetExcelSheet<Map>().TryGetRow(destinationMapId, out var map))
            return new(false, null);
        var identity = new TravelMapIdentity(_data);
        var currentMap = identity.Canonical(_clientState.MapId);
        var borders = _layout.WalkingBorders(currentMap).Where(b => b.TargetMap == identity.Canonical(destinationMapId))
            .Select(b => b.Border).ToArray();
        var result = Resolve(borders, from, reachable, disconnectedFloor, hasMeasuredCrossing);
        _log.Info($"[Border] map={destinationMapId}, volumes={borders.Length}, from={from}, " +
            $"target={(result.Position is { } point ? point.ToString() : "unresolved")}");
        return result;
    }

    internal IReadOnlyList<Vector3> Targets(uint destinationMap, Vector3 from,
        Func<Vector3, float, float, Vector3?> probe)
    {
        var result = new List<Vector3>();
        var identity = new TravelMapIdentity(_data);
        foreach (var connection in _layout.WalkingBorders(_clientState.MapId)
            .Where(b => b.TargetMap == identity.Canonical(destinationMap))
            .OrderBy(b => HorizontalDistance(b.Border.Centre, from)))
        foreach (var target in Targets(connection.Border, from, probe))
            if (!result.Any(p => Vector3.Distance(p, target) < 0.5f)) result.Add(target);
        return result.Take(48).ToArray();
    }

    internal static IReadOnlyList<Vector3> Targets(TravelBorder border, Vector3 from,
        Func<Vector3, float, float, Vector3?> probe)
    {
        var result = new List<Vector3>();
        foreach (var tall in new[] { false, true })
        foreach (var radius in new[] { 2f, 6f })
        foreach (var candidate in Candidates(border, from))
        {
            var origin = tall ? candidate with { Y = border.Centre.Y } : candidate;
            var height = tall ? border.HalfExtent.Y : MathF.Min(5f, border.HalfExtent.Y);
            var target = ValidateMesh(border, origin, probe(origin, radius, height), radius, height);
            if (target is { } point && !result.Any(p => Vector3.Distance(p, point) < 0.5f)) result.Add(point);
        }
        return result;
    }

    internal static BorderResolution Resolve(IEnumerable<TravelBorder> source, Vector3 from,
        Func<Vector3, float, float, Vector3?>? reachable,
        Func<Vector3, Vector3?>? disconnectedFloor = null,
        Func<Vector3, bool>? hasMeasuredCrossing = null)
    {
        var borders = source.OrderBy(b => HorizontalDistance(b.Centre, from)).ToArray();
        if (!TravelLayout.Finite(from)) return new(borders.Length != 0, null);
        foreach (var border in borders)
        {
            var candidates = Candidates(border, from);
            if (reachable == null) return new(true, candidates[0]);
            // Prefer a road inside the trigger before accepting a short gap at
            // its edge. Search vertically only within the trigger's real height.
            foreach (var tall in new[] { false, true })
            foreach (var radius in new[] { 2f, 6f })
            foreach (var candidate in candidates)
            {
                var origin = tall ? candidate with { Y = border.Centre.Y } : candidate;
                var height = tall ? border.HalfExtent.Y : MathF.Min(5f, border.HalfExtent.Y);
                var mesh = reachable(origin, radius, height);
                var target = ValidateMesh(border, origin, mesh, radius, height);
                if (target != null) return new(true, target);
            }

            // Preserve the author's measured mesh crossings. A disconnected
            // floor alone never authorizes walking into an unknown mesh island.
            if (disconnectedFloor != null && hasMeasuredCrossing != null)
            foreach (var candidate in candidates)
            {
                var target = ValidateMesh(border, candidate, disconnectedFloor(candidate), 2f, 5f);
                if (target is { } point && hasMeasuredCrossing(point)) return new(true, point);
            }
        }
        // No fallback to a rejected box centre or a broadly snapped map symbol.
        return new(borders.Length != 0, null);
    }

    private static Vector3? ValidateMesh(TravelBorder border, Vector3 origin, Vector3? mesh,
        float radius, float height)
    {
        if (mesh is not { } point || !TravelLayout.Finite(point)
            || MathF.Abs(point.Y - origin.Y) > height + 0.01f
            || MathF.Abs(point.X - origin.X) > radius + 0.01f
            || MathF.Abs(point.Z - origin.Z) > radius + 0.01f
            || MathF.Abs(point.Y - border.Centre.Y) >= border.HalfExtent.Y) return null;
        var local = border.Local(point);
        // Keep the actual floor height and aim just inside the trigger in X/Z.
        var target = border.World(local with
        {
            X = Math.Clamp(local.X, -Limit(border.HalfExtent.X), Limit(border.HalfExtent.X)),
            Z = Math.Clamp(local.Z, -Limit(border.HalfExtent.Z), Limit(border.HalfExtent.Z)),
        });
        return border.Contains(target) && Vector3.Distance(point, target) <= MaxApproachGap ? target : null;
    }

    internal static List<Vector3> Candidates(TravelBorder border, Vector3 from)
    {
        var local = border.Local(from);
        var spreadZ = MathF.Abs(local.X) - border.HalfExtent.X >= MathF.Abs(local.Z) - border.HalfExtent.Z;
        var nearest = new Vector3(Math.Clamp(local.X, -Limit(border.HalfExtent.X), Limit(border.HalfExtent.X)),
            Math.Clamp(local.Y, -Limit(border.HalfExtent.Y), Limit(border.HalfExtent.Y)),
            Math.Clamp(local.Z, -Limit(border.HalfExtent.Z), Limit(border.HalfExtent.Z)));
        var result = new List<Vector3> { border.World(nearest) };
        var width = spreadZ ? border.HalfExtent.Z : border.HalfExtent.X;
        var start = spreadZ ? nearest.Z : nearest.X;
        for (var step = 1; result.Count < 9; step++)
        {
            var added = false;
            foreach (var offset in new[] { start + step * 3f, start - step * 3f })
            {
                if (MathF.Abs(offset) > Limit(width)) continue;
                result.Add(border.World(spreadZ ? nearest with { Z = offset } : nearest with { X = offset }));
                added = true;
                if (result.Count == 9) break;
            }
            if (!added) break;
        }
        return result;
    }

    private static float Limit(float half) => MathF.Max(half - MathF.Min(Inset, half / 2f), 0f);
    private static float HorizontalDistance(Vector3 a, Vector3 b)
        => Vector2.Distance(new(a.X, a.Z), new(b.X, b.Z));
}
