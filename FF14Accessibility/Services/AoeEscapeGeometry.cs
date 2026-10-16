using System.Numerics;

namespace FF14Accessibility.Services;

/// <summary>Continuous checks catch a cone tip between two floor samples.</summary>
internal static class AoeEscapeGeometry
{
    private const float Epsilon = 0.0001f;

    internal static bool IsValid(DangerZone zone) => AoeEscapePath.Finite(zone.Center)
        && float.IsFinite(zone.Range) && zone.Range > 0 && float.IsFinite(zone.Facing)
        && float.IsFinite(zone.HalfAngleRad) && float.IsFinite(zone.HalfWidth)
        && (zone.Shape == DangerShape.Circle
            || zone.Shape == DangerShape.Cone && zone.HalfAngleRad > 0 && zone.HalfAngleRad <= MathF.PI
            || zone.Shape == DangerShape.Line && zone.HalfWidth > 0);

    // Exact distance to the closed ground shape. margin / radius is only an
    // approximation to the cone's angular margin and can admit a nearby edge.
    internal static bool ContainsWithMargin(DangerZone zone, Vector3 point, float margin)
    {
        if (!IsValid(zone) || !AoeEscapePath.Finite(point) || !float.IsFinite(margin) || margin < 0) return true;
        var p = new Vector2(point.X - zone.Center.X, point.Z - zone.Center.Z);
        var radius = p.Length();
        if (zone.Shape == DangerShape.Circle) return radius <= zone.Range + margin;
        var forward = new Vector2(MathF.Sin(zone.Facing), MathF.Cos(zone.Facing));
        if (zone.Shape == DangerShape.Line)
        {
            var along = Vector2.Dot(p, forward);
            var across = Vector2.Dot(p, new Vector2(forward.Y, -forward.X));
            var outside = new Vector2(along - Math.Clamp(along, 0, zone.Range),
                across - Math.Clamp(across, -zone.HalfWidth, zone.HalfWidth));
            return outside.LengthSquared() <= margin * margin;
        }
        var angle = MathF.Abs(MathF.IEEERemainder(MathF.Atan2(p.X, p.Y) - zone.Facing, MathF.Tau));
        if (angle <= zone.HalfAngleRad) return radius <= zone.Range + margin;
        for (var sign = -1; sign <= 1; sign += 2)
        {
            var edgeAngle = zone.Facing + sign * zone.HalfAngleRad;
            var edge = new Vector2(MathF.Sin(edgeAngle), MathF.Cos(edgeAngle));
            var nearest = edge * Math.Clamp(Vector2.Dot(p, edge), 0, zone.Range);
            if (Vector2.DistanceSquared(p, nearest) <= margin * margin) return true;
        }
        return false;
    }

    internal static bool Intersects(DangerZone zone, Vector3 from, Vector3 to)
    {
        if (!AoeEscapePath.Finite(from) || !AoeEscapePath.Finite(to) || !IsValid(zone)) return true;
        if (zone.Contains(from) || zone.Contains(to)) return true;
        var a = new Vector2(from.X - zone.Center.X, from.Z - zone.Center.Z);
        var d = new Vector2(to.X - from.X, to.Z - from.Z);
        var length2 = d.LengthSquared();
        if (length2 < Epsilon * Epsilon) return false;
        if (zone.Shape == DangerShape.Line)
        {
            var forward = new Vector2(MathF.Sin(zone.Facing), MathF.Cos(zone.Facing));
            var side = new Vector2(forward.Y, -forward.X);
            var enter = 0f;
            var leave = 1f;
            return Clip(Vector2.Dot(a, forward), Vector2.Dot(d, forward), 0, zone.Range, ref enter, ref leave)
                && Clip(Vector2.Dot(a, side), Vector2.Dot(d, side), -zone.HalfWidth, zone.HalfWidth, ref enter, ref leave);
        }
        var nearest = a + Math.Clamp(-Vector2.Dot(a, d) / length2, 0, 1) * d;
        if (nearest.LengthSquared() > (zone.Range + Epsilon) * (zone.Range + Epsilon)) return false;
        if (zone.Shape == DangerShape.Circle) return true;
        if (zone.Shape != DangerShape.Cone) return true;

        for (var sign = -1; sign <= 1; sign += 2)
        {
            var angle = zone.Facing + zone.HalfAngleRad * sign;
            var ray = new Vector2(MathF.Sin(angle), MathF.Cos(angle));
            var determinant = Cross(d, ray);
            if (MathF.Abs(determinant) < Epsilon) continue;
            var t = Cross(-a, ray) / determinant;
            var along = Cross(-a, d) / determinant;
            if (t >= -Epsilon && t <= 1 + Epsilon && along >= -Epsilon && along <= zone.Range + Epsilon)
                return true;
        }
        // The outer arc is the remaining boundary of the sector.
        var projection = Vector2.Dot(a, d);
        var discriminant = projection * projection - length2 * (a.LengthSquared() - zone.Range * zone.Range);
        if (discriminant < 0) return false;
        var root = MathF.Sqrt(discriminant);
        for (var sign = -1; sign <= 1; sign += 2)
        {
            var t = (-projection + sign * root) / length2;
            if (t < -Epsilon || t > 1 + Epsilon) continue;
            if (zone.ContainsWithMargin(Vector3.Lerp(from, to, Math.Clamp(t, 0, 1)), Epsilon)) return true;
        }
        return false;
    }

    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

    private static bool Clip(float at, float delta, float min, float max, ref float enter, ref float leave)
    {
        if (MathF.Abs(delta) < Epsilon) return at >= min - Epsilon && at <= max + Epsilon;
        var first = (min - at) / delta;
        var last = (max - at) / delta;
        if (first > last) (first, last) = (last, first);
        enter = MathF.Max(enter, first);
        leave = MathF.Min(leave, last);
        return enter <= leave + Epsilon;
    }
}
