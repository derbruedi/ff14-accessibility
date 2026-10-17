using System.Numerics;

namespace FF14Accessibility.Services;

/// <summary>
/// Which part of the Fall Guys duty the player is standing in.
/// Names are PlaceName rows 4470–4475 (DE/EN, sqpack 2026-10-07).
/// </summary>
internal enum BlundervillePlace
{
    None = 0,
    Lobby,
    Parcours,
    Race,
    Whirl,
    Siege,
    Peak,
}

/// <summary>What the next stretch of a course is, taken from the layout asset.</summary>
internal enum BlundervilleNote
{
    None = 0,
    Wind,
    Swing,
    Bridge,
    RollingBar,
    Pendulum,
    Seesaw,
    RollingBall,
    Middle,
    EastField,
    WestField,
    SouthField,
}

/// <summary>One guidance answer for the player's current position.</summary>
internal readonly record struct BlundervilleStep(
    BlundervillePlace Place,
    Vector3 Target,
    float Distance,
    bool AtTarget,
    BlundervilleNote Note,
    ushort BeaconKey);

/// <summary>
/// Course geometry for territory 1165 (Blunderville / Stolperville).
/// Measured offline from <c>bg/ffxiv/wil_w1/evt/w1ek/level/bg.lgb</c> and
/// <c>planmap.lgb</c> on 2026-10-07. The zone has no <c>list.pcb</c>. The five
/// courses are separate clusters in one territory, so a navmesh path is not
/// what a blind player can follow. These points are the floor models and the
/// finish triggers, in running order (north, which is decreasing Z).
/// </summary>
internal static class BlundervilleCourse
{
    public const ushort TerritoryId = 1165;

    public static readonly Vector3 WhirlCenter = new(200f, 10f, -125f);
    public static readonly Vector3 SiegeCenter = new(-200f, 7f, 252f);
    public static readonly Vector3 SiegeEast = new(-156.7f, 7f, 277.1f);
    public static readonly Vector3 SiegeWest = new(-243.5f, 7f, 276.9f);
    public static readonly Vector3 SiegeSouth = new(-200f, 7f, 202f);

    private readonly record struct Mark(Vector3 Position, BlundervilleNote Note, ushort Key, bool Goal = false);

    // stage1 / LVD_knight_fever. Floor models ktfv* along X = -200.
    // grc02 carries a wind effect, lhm01 swings left/right, brg01 is a bridge.
    private static readonly Mark[] ParcoursMarks =
    [
        new(new Vector3(-200f, 15f, -140f), BlundervilleNote.Wind, 11),
        new(new Vector3(-200f, 10f, -210f), BlundervilleNote.Wind, 12),
        new(new Vector3(-200f, 15f, -270f), BlundervilleNote.Swing, 13),
        new(new Vector3(-200f, 10f, -360f), BlundervilleNote.Bridge, 14),
        new(new Vector3(-200f, 13.7f, -368.9f), BlundervilleNote.None, 15, Goal: true),
    ];

    // stage2 / LVD_hit_parade. Floor models htpd* along X = 0.
    // lwb01 rolls (wpole_roll), wkb01 is a pendulum (pend_*).
    private static readonly Mark[] RaceMarks =
    [
        new(new Vector3(0f, 0f, -150f), BlundervilleNote.None, 21),
        new(new Vector3(0f, 0f, -220f), BlundervilleNote.RollingBar, 22),
        new(new Vector3(0f, 10f, -340f), BlundervilleNote.Pendulum, 23),
        new(new Vector3(0f, 18.9f, -381f), BlundervilleNote.None, 24, Goal: true),
    ];

    // stage5 / LVD_crown_mountain. Floor models crmt* along X = 0, climbing
    // toward the crown trigger at (0, 43.4, 118). flp01 flips, sph02/sph03 roll.
    private static readonly Mark[] PeakMarks =
    [
        new(new Vector3(0f, 5f, 290f), BlundervilleNote.None, 51),
        new(new Vector3(0f, 10f, 240f), BlundervilleNote.Seesaw, 52),
        new(new Vector3(0f, 30f, 160f), BlundervilleNote.RollingBall, 53),
        new(new Vector3(0f, 43.4f, 118f), BlundervilleNote.None, 54, Goal: true),
    ];

    /// <summary>
    /// How far ahead of the player the locked point sits on a straight course.
    /// Tied to the beacon's six-degree center: about one metre of sideways drift
    /// then leaves the middle. This is not a measured platform width, and it is
    /// not a gap beside an obstacle. Those side lanes are not in the layout.
    /// </summary>
    public const float SteerAhead = 8f;

    /// <summary>A course the guide can point along. The lobby only gets one sentence.</summary>
    public static bool IsCourse(BlundervillePlace place) => place is
        BlundervillePlace.Parcours or BlundervillePlace.Race or BlundervillePlace.Whirl
        or BlundervillePlace.Siege or BlundervillePlace.Peak;

    /// <summary>
    /// The point Numpad 5 and the beacon share. On a straight course it sits on
    /// the measured line, <see cref="SteerAhead"/> metres toward the next mark,
    /// so the tone stays in the center only while the player is on that line.
    /// The whirl and the siege walk toward their chosen point instead. False in
    /// the lobby and in open space.
    /// </summary>
    public static bool TryFixedTarget(Vector3 player, out Vector3 point)
        => TryFixedTarget(Guide(player), player, out point);

    /// <summary>Same lock as <see cref="TryFixedTarget(Vector3, out Vector3)"/> for a step already computed.</summary>
    public static bool TryFixedTarget(BlundervilleStep step, Vector3 player, out Vector3 point)
    {
        if (!IsCourse(step.Place))
        {
            point = default;
            return false;
        }

        point = step.Place is BlundervillePlace.Parcours or BlundervillePlace.Race or BlundervillePlace.Peak
            ? LinearSteer(player, step.Target)
            : Approach(player, step.Target);
        return true;
    }

    /// <summary>Which cluster contains <paramref name="position"/>. Boxes come from the floor models, with a margin.</summary>
    public static BlundervillePlace Identify(Vector3 position)
    {
        var x = position.X;
        var z = position.Z;
        if (x is >= -250f and <= -150f && z is <= -60f and >= -400f) return BlundervillePlace.Parcours;
        if (x is >= -50f and <= 50f && z is <= -60f and >= -420f) return BlundervillePlace.Race;
        if (x is >= 150f and <= 250f && z is <= -60f and >= -190f) return BlundervillePlace.Whirl;
        if (x is >= -270f and <= -140f && z is >= 160f and <= 330f) return BlundervillePlace.Siege;
        if (x is >= -50f and <= 50f && z is >= 70f and <= 360f) return BlundervillePlace.Peak;
        if (x is >= 150f and <= 260f && z is >= 80f and <= 200f) return BlundervillePlace.Lobby;
        return BlundervillePlace.None;
    }

    /// <summary>The point to walk toward from <paramref name="player"/>.</summary>
    public static BlundervilleStep Guide(Vector3 player)
    {
        var place = Identify(player);
        return place switch
        {
            BlundervillePlace.Parcours => Linear(place, player, ParcoursMarks),
            BlundervillePlace.Race => Linear(place, player, RaceMarks),
            BlundervillePlace.Peak => Linear(place, player, PeakMarks),
            BlundervillePlace.Whirl => Point(place, player, WhirlCenter, BlundervilleNote.Middle, 31, 4f),
            BlundervillePlace.Siege => Siege(player),
            _ => new BlundervilleStep(place, player, 0f, false, BlundervilleNote.None, 0),
        };
    }

    private static BlundervilleStep Linear(BlundervillePlace place, Vector3 player, Mark[] marks)
    {
        Mark? ahead = null;
        foreach (var mark in marks)
        {
            if (mark.Position.Z < player.Z - 1.5f)
            {
                ahead = mark;
                break;
            }
        }

        var chosen = ahead ?? marks[^1];
        var distance = Vector3.Distance(player, chosen.Position);
        var atTarget = chosen.Goal && Flat(player, chosen.Position) <= 6f;
        return new BlundervilleStep(place, chosen.Position, distance, atTarget, chosen.Note, chosen.Key);
    }

    private static BlundervilleStep Siege(Vector3 player)
    {
        var center = Flat(player, SiegeCenter);
        var bestNote = BlundervilleNote.Middle;
        var bestPoint = SiegeCenter;
        var best = center;
        Consider(SiegeEast, BlundervilleNote.EastField);
        Consider(SiegeWest, BlundervilleNote.WestField);
        Consider(SiegeSouth, BlundervilleNote.SouthField);

        // Stay on the crystal pile until a field is clearly the nearer place.
        if (bestNote != BlundervilleNote.Middle && best + 2f >= center)
        {
            bestNote = BlundervilleNote.Middle;
            bestPoint = SiegeCenter;
            best = center;
        }

        var key = bestNote switch
        {
            BlundervilleNote.EastField => (ushort)42,
            BlundervilleNote.WestField => (ushort)43,
            BlundervilleNote.SouthField => (ushort)44,
            _ => (ushort)41,
        };
        return new BlundervilleStep(
            BlundervillePlace.Siege, bestPoint, Vector3.Distance(player, bestPoint), best <= 6f, bestNote, key);

        void Consider(Vector3 point, BlundervilleNote note)
        {
            var distance = Flat(player, point);
            if (distance < best)
            {
                best = distance;
                bestPoint = point;
                bestNote = note;
            }
        }
    }

    private static BlundervilleStep Point(
        BlundervillePlace place, Vector3 player, Vector3 target, BlundervilleNote note, ushort key, float arrive)
    {
        var flat = Flat(player, target);
        return new BlundervilleStep(place, target, Vector3.Distance(player, target), flat <= arrive, note, key);
    }

    /// <summary>
    /// A point on the course line, a short way north of the player. North is
    /// decreasing Z on every straight course in this zone. The mark's own X is
    /// used, so a player who has drifted hears the line beside them instead of
    /// a far mark that is still almost straight ahead.
    /// </summary>
    private static Vector3 LinearSteer(Vector3 player, Vector3 target)
    {
        var remaining = player.Z - target.Z;
        if (remaining <= SteerAhead) return target;
        return new Vector3(target.X, target.Y, player.Z - SteerAhead);
    }

    /// <summary>Up to <see cref="SteerAhead"/> metres toward <paramref name="target"/>, never past it.</summary>
    private static Vector3 Approach(Vector3 player, Vector3 target)
    {
        var dx = target.X - player.X;
        var dz = target.Z - player.Z;
        var flat = MathF.Sqrt(dx * dx + dz * dz);
        if (flat <= SteerAhead || flat < 0.01f) return target;
        var scale = SteerAhead / flat;
        return new Vector3(player.X + dx * scale, target.Y, player.Z + dz * scale);
    }

    private static float Flat(Vector3 a, Vector3 b)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }
}
