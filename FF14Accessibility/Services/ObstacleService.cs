using System;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine.Layer;

namespace FF14Accessibility.Services;

/// <summary>
/// What is standing in the way. Turns "da steht etwas im Weg" into something the
/// player can act on - because the useful part is not THAT something blocks, it is
/// WHAT: another player wanders off in a moment, a barrier never does.
///
/// <para>
/// TWO SOURCES, AND THEY ARE NOT EQUALLY GENEROUS.
/// </para>
///
/// <para>
/// LIVING THINGS carry a name, a position and a <c>HitboxRadius</c> the game keeps
/// itself, so "wer steht im Weg" is a plain calculation and the answer can be
/// spoken as-is. This is also the common case: a city is full of players standing
/// on top of the one doorway you want.
/// </para>
///
/// <para>
/// SCENERY DOES NOT CARRY A NAME. Measured with tools/zone-probe on 2026-08-22:
/// background parts identify themselves only by model and collision file
/// (<c>f1t0_a0_taru1.mdl</c>, <c>f1t0_b0_gatdr.pcb</c>) plus a layout type. There
/// is no speakable name anywhere in the game data for a barrel or a fence, unlike
/// an NPC or a chest. So this service does NOT invent one - inventing plausible
/// German names for foreign model abbreviations is exactly the kind of guess the
/// rules of this repo forbid. What it CAN say honestly is the distinction that
/// actually matters:
/// </para>
///
/// <list type="bullet">
/// <item>an invisible barrier (<c>CollisionBox</c> with <c>pushPlayerOut</c>) -
/// this will never move, turn around</item>
/// <item>solid scenery (a <c>BgPart</c> carrying collision) - a wall, a crate, a
/// gate; also not going anywhere</item>
/// </list>
///
/// <para>
/// The model abbreviation goes to the LOG, not to the ear. It is what lets us
/// answer "what was that actually?" later without guessing at it now.
/// </para>
/// </summary>
public sealed unsafe class ObstacleService
{
    /// <summary>How far ahead to look. A blocker is by definition something the
    /// character has already run into, so this only has to cover the character's
    /// own reach plus a step.</summary>
    private const float LookAhead = 3f;

    /// <summary>Ignore anything this far above or below - a lamp overhead and a
    /// floor below block nothing, and both sit within metres horizontally.</summary>
    private const float HeightTolerance = 2f;

    /// <summary>Extra width on top of the two hitboxes. Running into someone stops
    /// the character slightly before the radii touch (collision uses capsules, not
    /// points), and without a little slack the culprit tests as "just missed".</summary>
    private const float ContactSlack = 0.5f;

    /// <summary>vnavmesh's own test for "this layout instance is switched on"
    /// (SceneDefinition.cs:63 and :85) - the same bit that decides whether a
    /// collider becomes part of the walkable mesh.</summary>
    private const byte ActiveFlag = 0x10;

    private readonly IObjectTable _objectTable;
    private readonly ObjectNameService _objectNames;
    private readonly IPluginLog _log;

    public ObstacleService(IObjectTable objectTable, ObjectNameService objectNames, IPluginLog log)
    {
        _objectTable = objectTable;
        _objectNames = objectNames;
        _log = log;
    }

    /// <summary>
    /// Names whatever blocks the way from <paramref name="from"/> towards
    /// <paramref name="towards"/>, as a subject ready to be put in front of "steht
    /// im Weg". Null when nothing is found - and then the caller must keep saying
    /// the honest vague thing rather than making something up.
    /// </summary>
    public string? DescribeBlocker(Vector3 from, Vector3 towards)
    {
        var direction = towards - from;
        direction.Y = 0;
        if (direction.LengthSquared() < 0.0001f) return null;
        direction = Vector3.Normalize(direction);

        // Living things first: they have a name, and they are the case that
        // resolves itself if the player just waits a moment.
        var creature = FindBlockingObject(from, direction);
        if (creature != null)
        {
            var name = _objectNames.Describe(creature);
            _log.Info($"[Hindernis] Wesen '{name}' (Art {creature.ObjectKind}, " +
                      $"Hitbox {creature.HitboxRadius:F1}) blockiert.");
            return name;
        }

        return DescribeBlockingGeometry(from, direction);
    }

    /// <summary>The nearest object whose hitbox overlaps the line of travel.</summary>
    private IGameObject? FindBlockingObject(Vector3 from, Vector3 direction)
    {
        var self = _objectTable.LocalPlayer;
        var ownRadius = self?.HitboxRadius ?? 0.5f;

        IGameObject? best = null;
        var bestAlong = float.MaxValue;

        foreach (var obj in _objectTable)
        {
            if (obj == null || !obj.IsValid()) continue;
            if (self != null && obj.GameObjectId == self.GameObjectId) continue;
            // Only things with a body. Event markers and area triggers share the
            // object table but nothing walks into them.
            if (obj.ObjectKind is not (ObjectKind.Pc or ObjectKind.BattleNpc or
                                       ObjectKind.EventNpc or ObjectKind.Retainer or
                                       ObjectKind.Companion or ObjectKind.HousingEventObject)) continue;

            var offset = obj.Position - from;
            if (Math.Abs(offset.Y) > HeightTolerance) continue;

            offset.Y = 0;
            var along = Vector3.Dot(offset, direction);
            if (along <= 0 || along > LookAhead) continue;      // behind, or too far

            // Distance from the line of travel: does the body overlap our path?
            var sideways = (offset - direction * along).Length();
            if (sideways > obj.HitboxRadius + ownRadius + ContactSlack) continue;

            if (along < bestAlong)
            {
                bestAlong = along;
                best = obj;
            }
        }

        return best;
    }

    /// <summary>
    /// Static geometry from the LIVING layout. Reads the same chain vnavmesh uses
    /// for its mesh build (<c>SceneDefinition.FillFromLayout</c>), including its
    /// active-bit test, so what counts as an obstacle here is what counts as one
    /// for the walkable mesh. Read-only, no hook.
    /// </summary>
    private string? DescribeBlockingGeometry(Vector3 from, Vector3 direction)
    {
        var world = LayoutWorld.Instance();
        if (world == null) return null;

        // An invisible barrier is worth checking first: it is the one that will
        // still be there in ten minutes, and the player should turn around.
        var barrier = FindBarrier(world->ActiveLayout, from, direction)
                   || FindBarrier(world->GlobalLayout, from, direction);
        if (barrier)
        {
            _log.Info("[Hindernis] Unsichtbare Absperrung (CollisionBox) blockiert.");
            return AccessibilityStrings.ObstacleBarrier;
        }

        var scenery = FindScenery(world->ActiveLayout, from, direction)
                   ?? FindScenery(world->GlobalLayout, from, direction);
        if (scenery != null)
        {
            // The abbreviation goes to the log only - see the class comment on why
            // it is not spoken.
            _log.Info($"[Hindernis] Kulisse blockiert, Kollisionskennung {scenery}.");
            return AccessibilityStrings.ObstacleScenery;
        }

        return null;
    }

    /// <summary>Is a switched-on collision box covering the step ahead?</summary>
    private static bool FindBarrier(LayoutManager* layout, Vector3 from, Vector3 direction)
    {
        if (layout == null || layout->InitState != 7) return false;
        if (!layout->InstancesByType.TryGetValuePointer(InstanceType.CollisionBox, out var bucket)
            || bucket == null || bucket->Value == null) return false;

        var probe = from + direction * (LookAhead / 2f);
        foreach (var (_, value) in *bucket->Value)
        {
            var box = (CollisionBoxLayoutInstance*)value.Value;
            if (box == null || (box->Flags3 & ActiveFlag) == 0) continue;

            // Scale on a trigger box is the HALF extent - measured in-game on
            // 2026-08-22 with the ZoneExitProbe, twice through the same border in
            // both directions.
            var centre = box->Transform.Translation;
            var half = box->Transform.Scale;
            if (Math.Abs(probe.X - centre.X) <= half.X
             && Math.Abs(probe.Y - centre.Y) <= half.Y
             && Math.Abs(probe.Z - centre.Z) <= half.Z)
                return true;
        }

        return false;
    }

    /// <summary>The collision id of the nearest switched-on background part that
    /// carries collision and sits on the step ahead, or null.</summary>
    private static string? FindScenery(LayoutManager* layout, Vector3 from, Vector3 direction)
    {
        if (layout == null || layout->InitState != 7) return null;
        if (!layout->InstancesByType.TryGetValuePointer(InstanceType.BgPart, out var bucket)
            || bucket == null || bucket->Value == null) return null;

        var probe = from + direction * (LookAhead / 2f);
        string? best = null;
        var bestDistance = float.MaxValue;

        foreach (var (_, value) in *bucket->Value)
        {
            var part = (BgPartsLayoutInstance*)value.Value;
            if (part == null || (part->Flags3 & ActiveFlag) == 0) continue;

            // No collision, no obstacle - most of a town is decoration.
            var analytic = part->AnalyticShapeDataCrc;
            var mesh = part->CollisionMeshPathCrc;
            if (analytic == 0 && mesh == 0) continue;

            var transform = value.Value->GetTransformImpl();
            if (transform == null) continue;

            var centre = transform->Translation;
            if (Math.Abs(centre.Y - from.Y) > HeightTolerance) continue;

            // The instance origin, not its outline - the game gives us no bounds
            // here. Good enough to name a culprit we are already standing against,
            // and it never invents one: without collision we never get this far.
            var distance = Vector3.Distance(new Vector3(centre.X, 0, centre.Z),
                                            new Vector3(probe.X, 0, probe.Z));
            if (distance > LookAhead) continue;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = analytic != 0 ? $"analytic {analytic:X8}" : $"pcb {mesh:X8}";
            }
        }

        return best;
    }
}
