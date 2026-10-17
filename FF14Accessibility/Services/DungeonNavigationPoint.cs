using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;

namespace FF14Accessibility.Services;

internal static class DungeonNavigationPoint
{
    // Recorded dungeon positions already have a measured height. Do not use
    // the tall-column projection intended for flat world-map markers.
    internal static Vector3 Resolve(Vector3 recorded, Func<Vector3, float, float, Vector3?> nearest)
    {
        var hit = nearest(recorded, 2.5f, 3f);
        if (hit is not { } point || !Finite(point) || MathF.Abs(point.Y - recorded.Y) > 3f
            || Vector2.Distance(new(recorded.X, recorded.Z), new(point.X, point.Z)) > 3f)
            return recorded;
        return point;
    }

    internal static bool Finite(Vector3 point)
        => float.IsFinite(point.X) && float.IsFinite(point.Y) && float.IsFinite(point.Z);

    internal static uint DataId(IReadOnlyList<string>? arguments)
    {
        var first = arguments?.FirstOrDefault()?.TrimStart();
        if (string.IsNullOrEmpty(first)) return 0;
        var digits = new string(first.TakeWhile(char.IsAsciiDigit).ToArray());
        return uint.TryParse(digits, System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture, out var id) ? id : 0;
    }

    internal static IGameObject? FindObject(IEnumerable<IGameObject> objects, DungeonStep step)
    {
        if (step.Kind is not (DungeonStepKind.Interact or DungeonStepKind.Treasure)) return null;
        var candidates = objects.Where(obj =>
            (step.Kind == DungeonStepKind.Treasure ? obj.ObjectKind == ObjectKind.Treasure
                : obj.ObjectKind is ObjectKind.EventObj or ObjectKind.EventNpc)
            && (step.DataId == 0 || obj.BaseId == step.DataId)
            && Finite(obj.Position) && MathF.Abs(obj.Position.Y - step.Position.Y) <= 3f
            && Vector3.Distance(obj.Position, step.Position) <= 10f)
            .OrderBy(obj => Vector3.Distance(obj.Position, step.Position)).Take(2).ToArray();
        if (candidates.Length == 0) return null;
        // Without an identity, two mechanisms in one room must not be guessed.
        if (candidates.Length > 1 && (step.DataId == 0
            || MathF.Abs(Vector3.Distance(candidates[0].Position, step.Position)
                - Vector3.Distance(candidates[1].Position, step.Position)) < 0.1f)) return null;
        return candidates[0];
    }
}
