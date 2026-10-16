using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;

namespace FF14Accessibility.Services;

// Object identity stays independent of translated names and native target filters.
internal static class SelectionObjectResolver
{
    internal static int NextIndex(IReadOnlyList<IGameObject> objects, ulong selectedId, int current, int direction)
    {
        if (selectedId != 0)
        {
            current = -1;
            for (var i = 0; i < objects.Count; i++)
                if (objects[i].GameObjectId == selectedId) { current = i; break; }
        }
        return BrowserTargetSelection.NextIndex(current, direction, objects.Count);
    }

    internal static IGameObject? Exact(IEnumerable<IGameObject> objects, ObjectDestination selected)
        => objects.FirstOrDefault(o => o.GameObjectId == selected.ObjectId && selected.ObjectId != 0
            && (selected.Kind == ObjectKind.None || o.ObjectKind == selected.Kind)
            && (selected.BaseId == 0 || o.BaseId == selected.BaseId) && TravelLayout.Finite(o.Position));

    internal static IGameObject? AetheryteMarker(IEnumerable<IGameObject> objects, Vector3 marker, ulong retainedId = 0)
    {
        if (!TravelLayout.Finite(marker)) return null;
        var candidates = objects.Where(o => o.ObjectKind == ObjectKind.Aetheryte && TravelLayout.Finite(o.Position)
            && Vector2.Distance(new(o.Position.X, o.Position.Z), new(marker.X, marker.Z)) <= 15f).ToArray();
        if (retainedId != 0) return candidates.FirstOrDefault(o => o.GameObjectId == retainedId);
        // A 2D map marker cannot distinguish stacked crystals. Do not guess a
        // floor or substitute a nearer crystal when the selected one unloads.
        return candidates.Length == 1 ? candidates[0] : null;
    }

    internal static IGameObject? Linked(IEnumerable<IGameObject> objects, uint baseId, byte levelType,
        Vector3 origin, ulong retainedId = 0)
    {
        var kind = levelType switch { 8 => ObjectKind.EventNpc, 9 => ObjectKind.BattleNpc,
            45 => ObjectKind.EventObj, _ => ObjectKind.None };
        if (baseId == 0 || kind == ObjectKind.None || !TravelLayout.Finite(origin)) return null;
        var matches = objects.Where(o => o.BaseId == baseId && o.ObjectKind == kind
            && TravelLayout.Finite(o.Position)).ToArray();
        if (retainedId != 0) return matches.FirstOrDefault(o => o.GameObjectId == retainedId);
        var candidates = matches.Where(o => MathF.Abs(o.Position.Y - origin.Y) < 5f)
            .Select(o => (Object: o, Gap: Vector2.Distance(new(o.Position.X, o.Position.Z), new(origin.X, origin.Z))))
            .Where(p => p.Gap <= 15f).OrderByDescending(p => p.Object.IsTargetable).ThenBy(p => p.Gap).ToArray();
        if (candidates.Length == 0) return null;
        // Equally plausible instances must not be decided by ObjectTable order.
        if (candidates.Length > 1 && candidates[0].Object.IsTargetable == candidates[1].Object.IsTargetable
            && candidates[1].Gap - candidates[0].Gap < 0.5f) return null;
        return candidates[0].Object;
    }

    internal static Vector3? Approach(Vector3 position, Func<Vector3, float, float, Vector3?> probe)
    {
        if (!TravelLayout.Finite(position)) return null;
        foreach (var radius in new[] { 1f, 2.5f })
        {
            var floor = probe(position, radius, 2f);
            // The mesh query searches a box. A valid approach near the corner
            // can be slightly farther than 2.5 m diagonally (2.885 m beside
            // Baderon's counter in the saved festival mesh). Keep a bounded
            // 3 m approach, with the same strict floor and query extents.
            if (floor is { } point && TravelLayout.Finite(point) && MathF.Abs(point.Y - position.Y) <= 2f
                && Vector2.Distance(new(point.X, point.Z), new(position.X, position.Z)) <= 3f) return point;
        }
        return null;
    }
}
