using System.Numerics;

namespace FF14Accessibility.Services;

/// <summary>A Type-51 map circle names a search area, not an actor's floor.
/// Keep every accepted mesh point (including the stopping distance) inside
/// that circle. Type-49 triggers and typed actors keep their exact height.</summary>
internal static class QuestAreaPoint
{
    internal static bool IsSearchArea(QuestDestination goal)
        => goal.TargetLevelType == 51 && goal.TargetBaseId == 0
            && goal.Role is QuestMarkerRole.Quest or QuestMarkerRole.LeveObjective
            && float.IsFinite(goal.Radius) && goal.Radius > 0;

    internal static Vector3? Resolve(Vector3 centre, float radius, float stopRange,
        Func<Vector3, float, float, Vector3?> reachable, Func<Vector3, bool> matchesMap)
        => ReachablePoints(centre, radius, stopRange, reachable, matchesMap)
            .Select(p => (Vector3?)p).FirstOrDefault();

    internal static IEnumerable<Vector3> ReachablePoints(Vector3 centre, float radius, float stopRange,
        Func<Vector3, float, float, Vector3?> reachable, Func<Vector3, bool> matchesMap)
    {
        if (!TravelLayout.Finite(centre) || !float.IsFinite(radius) || radius <= 0
            || !float.IsFinite(stopRange) || stopRange < 0) yield break;
        // The movement service may stop short: leave enough room to finish
        // inside the circle, even when only its outer ground is on the mesh.
        var usable = radius - stopRange - MathF.Min(0.5f, radius * 0.1f);
        if (usable <= 0) yield break;
        var accepted = new List<Vector3>();
        var xz = MathF.Min(6f, usable);
        foreach (var probe in Candidates(centre, usable))
        {
            if (!TravelLayout.Finite(probe)) continue;
            foreach (var height in new[] { 10f, 100f })
            {
                if (reachable(probe, xz, height) is not { } point || !TravelLayout.Finite(point)) continue;
                // Check the returned coordinate, not only the query box. vnavmesh
                // can project a polygon at a height beyond that box's centre.
                if (MathF.Abs(point.Y - centre.Y) > 100f
                    || MathF.Abs(point.X - probe.X) > xz || MathF.Abs(point.Z - probe.Z) > xz
                    || Vector2.Distance(new(centre.X, centre.Z), new(point.X, point.Z)) > usable
                    || !matchesMap(point)) continue;
                if (accepted.Any(p => Vector3.Distance(p, point) < 0.5f)) continue;
                accepted.Add(point);
                yield return point;
            }
        }
    }

    private static IEnumerable<Vector3> Candidates(Vector3 centre, float radius)
    {
        yield return centre;
        // Fixed work budget: 65 positions, at most 130 mesh calls. Search the
        // centre first, then broaden within the actual objective circle.
        for (var ring = 1; ring <= 4; ring++)
        for (var spoke = 0; spoke < 16; spoke++)
        {
            var angle = spoke * MathF.Tau / 16;
            var distance = radius * (ring / 4f);
            yield return centre + new Vector3(MathF.Sin(angle) * distance, 0, MathF.Cos(angle) * distance);
        }
    }
}
