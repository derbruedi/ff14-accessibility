using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;

namespace FF14Accessibility.Services;

/// <summary>Read-only, direct escape checks. This does not change or execute
/// vnavmesh routes. Missing collision data is not permission to turn.</summary>
internal static class AoeEscapePath
{
    internal static bool Finite(Vector3 p) => float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z);

    internal static Vector3? Check(Vector3 from, Vector3 to, IReadOnlyList<DangerZone> zones,
        Func<Vector3, Vector3?> floor, Func<Vector3, Vector3, bool> clear)
    {
        if (!Finite(from) || !Finite(to) || zones.Any(z => !AoeEscapeGeometry.IsValid(z))) return null;
        var delta = to - from;
        delta.Y = 0;
        var length = delta.Length();
        if (length < 0.1f || length > 25.01f) return null;
        var side = Vector3.Normalize(Vector3.Cross(delta, Vector3.UnitY)) * 0.35f;
        var steps = (int)MathF.Ceiling(length / 0.5f);
        var previous = from;
        var exited = new bool[zones.Count];
        var leftActual = new bool[zones.Count];
        for (var z = 0; z < zones.Count; z++) leftActual[z] = !zones[z].Contains(from);
        for (var z = 0; z < zones.Count; z++) exited[z] = !AoeEscapeGeometry.ContainsWithMargin(zones[z], from, 2f);
        for (var i = 0; i <= steps; i++)
        {
            var sample = from + delta * ((float)i / steps);
            sample.Y = previous.Y;
            if (floor(sample) is not { } center || !Finite(center)
                || Vector2.Distance(new(center.X, center.Z), new(sample.X, sample.Z)) > 0.1f
                || MathF.Abs(center.Y - previous.Y) > 0.35f) return null;
            // Support across the body width, not just a single point over a ledge.
            for (var sign = -1; sign <= 1; sign += 2)
            {
                var sampleEdge = center + side * sign;
                if (floor(sampleEdge) is not { } edge || !Finite(edge)
                    || Vector2.Distance(new(edge.X, edge.Z), new(sampleEdge.X, sampleEdge.Z)) > 0.1f
                    || MathF.Abs(edge.Y - center.Y) > 0.3f) return null;
            }
            if (i > 0 && !clear(previous, center)) return null;
            for (var z = 0; z < zones.Count; z++)
            {
                var inside = AoeEscapeGeometry.ContainsWithMargin(zones[z], center, 2f);
                // Being near a second AoE is not permission to cross its body.
                if (leftActual[z] && AoeEscapeGeometry.Intersects(zones[z], previous, center)) return null;
                if (!zones[z].Contains(center)) leftActual[z] = true;
                if (inside && exited[z]) return null;
                if (!inside) exited[z] = true;
            }
            previous = center;
        }
        return zones.Any(z => AoeEscapeGeometry.ContainsWithMargin(z, previous, 2f)) ? null : previous;
    }

    internal static unsafe Vector3? CheckLive(Vector3 from, Vector3 to, IReadOnlyList<DangerZone> zones,
        out string? failure)
    {
        failure = null;
        var framework = Framework.Instance();
        if (framework == null || framework->BGCollisionModule == null
            || framework->BGCollisionModule->ShuttingDown || framework->BGCollisionModule->LoadInProgressCounter != 0)
        {
            failure = "collision scene unavailable or loading";
            return null;
        }
        string? floorFailure = null;
        Vector3? ReadFloor(Vector3 sample)
        {
            var point = Floor(sample);
            if (point == null) floorFailure = "no supported ground hit";
            return point;
        }
        var result = Check(from, to, zones, ReadFloor, Clear);
        if (result == null) failure = floorFailure ?? "path blocked, steep, unsupported or crosses another AoE";
        return result;
    }

    private static unsafe Vector3? Floor(Vector3 sample) =>
        BGCollisionModule.RaycastMaterialFilter(sample + new Vector3(0, 0.6f, 0), -Vector3.UnitY, out var hit, 1.2f)
        ? GroundPoint(hit) : null;

    internal static Vector3? GroundPoint(RaycastHit hit)
    {
        if (!Finite(hit.Point) || !Finite(hit.Normal)) return null;
        var normal = hit.Normal;
        // RaycastHit.Normal is not filled for every collider. Mesh raycasts
        // still return the triangle; use its plane rather than reject all floor.
        // Source: FFXIVClientStructs BGCollisionModule / RaycastHit.
        if (normal == Vector3.Zero)
        {
            if (!Finite(hit.V1) || !Finite(hit.V2) || !Finite(hit.V3)) return null;
            normal = Vector3.Cross(hit.V2 - hit.V1, hit.V3 - hit.V1);
        }
        var lengthSquared = normal.LengthSquared();
        if (!Finite(normal) || !float.IsFinite(lengthSquared) || lengthSquared < 0.000001f) return null;
        normal /= MathF.Sqrt(lengthSquared);
        return normal.Y >= 0.65f ? hit.Point : null;
    }

    private static unsafe bool Clear(Vector3 from, Vector3 to)
    {
        var delta = to - from;
        var length = delta.Length();
        if (length < 0.01f) return true;
        var direction = delta / length;
        var side = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY)) * 0.35f;
        for (var h = 0; h < 3; h++)
        for (var sign = -1; sign <= 1; sign++)
        {
            var height = h == 0 ? 0.25f : h == 1 ? 0.8f : 1.4f;
            var offset = side * sign;
            if (BGCollisionModule.RaycastMaterialFilter(from + offset + new Vector3(0, height, 0), direction, out _, length)
                || BGCollisionModule.RaycastMaterialFilter(to + offset + new Vector3(0, height, 0), -direction, out _, length))
                return false;
        }
        return true;
    }
}
