using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Plugin.Services;
using Lumina.Data.Files;
using Lumina.Data.Parsing.Layer;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

/// <summary>
/// Turns a map transition into a point one can actually walk to: the nearest
/// spot inside the border's trigger box, instead of the map symbol in its middle.
///
/// <para>
/// WHY. A transition marker from the MapMarker sheet is map graphics. Walking to
/// it aims at the MIDDLE of the border, and the middle can sit far outside the
/// walkable mesh. Measured case Neu-Gridania to Tiefer Wald (log 2026-08-22
/// 10:53): the walk gave up 18,6 m short, with the last waypoint being the
/// destination itself - vnavmesh shoving the character at a point it cannot
/// reach. Yet the border box reaches to X = 154,5 and the walk stopped at
/// X = 152,5: <b>two metres</b>, not eighteen.
/// </para>
///
/// <para>
/// WHAT IS MEASURED AND WHAT IS NOT. That <c>Transform.Scale</c> is the HALF
/// extent was measured in-game on 2026-08-22 (probe <c>ZoneExitProbe</c>, both
/// directions through the same border): under the half reading the character was
/// inside the box roughly a second before the change, under the full reading
/// never - while the transition demonstrably fired. The border's
/// <c>TerritoryType</c> matched the zone that actually loaded, both times. See
/// docs/game-api.md.
/// </para>
///
/// <para>
/// THE NEAREST POINT IS NOT ALWAYS THE ONE TO WALK TO, measured 2026-08-22 with
/// <c>tools/zone-probe</c> against the layout in the sqpack. Within ten metres of
/// where the walk kept stalling stand seven barrels with box collision
/// (<c>f1t0_a0_taru1.mdl</c>, X 150,6-154,6 / Z 152,4-154,0) plus the gate
/// structure itself with real <c>.pcb</c> collision. The player crossed the same
/// border on foot at Z 155,5-156,9 - through the gate opening, north of the
/// barrels. Our geometrically nearest point sat at Z 150,5, i.e. inside the
/// barrels. The border box is 30 m wide; only part of it is a way through.
/// </para>
///
/// <para>
/// SO SEVERAL CANDIDATES ARE OFFERED ALONG THE BORDER and the nearest one the
/// mesh accepts wins. The test is vnavmesh's own
/// <c>Query.Mesh.NearestPointReachable</c>, which filters with a
/// <c>FloodFillAwareFilter</c> - reachability, not just "is there floor"
/// (decompiled NavmeshQuery.FindNearestMeshPoly). It is asked with a SMALL search
/// radius on purpose: at the 20 m the callers use, the query answers for almost
/// any point and shifts the result metres away, which is how a target inside the
/// barrels passed as reachable in the first place.
/// </para>
///
/// <para>
/// DELIBERATELY SMALL. A previous <c>ZoneExitService</c> was removed on the
/// user's instruction because the walking changes around it broke things. This
/// service does one thing: it answers "where is the border". It does not steer,
/// does not run through the border, does not reroute. The caller resolves the
/// point onto the mesh exactly like any other destination, and without a test
/// function it behaves exactly as before.
/// </para>
/// </summary>
public sealed class ZoneBorderService
{
    /// <summary>How far inside the box the target is placed. A point exactly on the
    /// edge is a coin flip - the walk stops within its own tolerance and may end up
    /// just outside. Two metres is inside the box for every border measured so far
    /// (smallest half extent seen: 15,0) and still far short of the middle.</summary>
    private const float InsetMeters = 2.0f;

    /// <summary>Spacing of the candidates along the border. Three metres is narrow
    /// enough to find the gate opening measured in Gridania (roughly 3 m wide
    /// between gate post at Z 154,8 and arch at Z 158,0) without turning a 30 m
    /// border into dozens of queries.</summary>
    private const float CandidateSpacing = 3.0f;

    /// <summary>Upper bound on candidates, counting the nearest point itself. Nine
    /// covers 12 m to either side; past that the border is not the way through and
    /// a longer list would only add IPC calls.</summary>
    private const int MaxCandidates = 9;

    /// <summary>Search radius for the reachability test. Deliberately far below the
    /// 20 m the callers use elsewhere - the point of the test is that a candidate
    /// standing in scenery FAILS instead of being shifted somewhere else.</summary>
    private const float ProbeRadiusXZ = 2.0f;

    /// <summary>Vertical search for the same test. Borders can sit a couple of
    /// metres above or below the ground the marker implies.</summary>
    private const float ProbeRadiusY = 5.0f;

    private readonly IDataManager _data;
    private readonly IClientState _clientState;
    private readonly IPluginLog _log;

    /// <summary>Borders of the loaded zone. Layout data never changes at runtime,
    /// so this is read once per zone.</summary>
    private readonly List<Border> _borders = new();
    private ushort _loadedFor = ushort.MaxValue;

    public ZoneBorderService(IDataManager data, IClientState clientState, IPluginLog log)
    {
        _data = data;
        _clientState = clientState;
        _log = log;
    }

    /// <summary>
    /// The nearest walkable-looking point of the border that leads to
    /// <paramref name="destinationMapId"/>, or null when this zone has no such
    /// border in its layout (doors and instance entrances have none - those are
    /// entered by talking to them). The caller still has to resolve the height.
    /// </summary>
    /// <param name="reachable">vnavmesh's reachability test, or null to skip it.
    /// Called as (point, halfExtentXZ, halfExtentY) and returns the mesh point it
    /// resolves to, or null when there is no reachable mesh that close. The radii
    /// are handed over rather than left to the caller because they are what makes
    /// the test meaningful - see <see cref="ProbeRadiusXZ"/>. Passed in as a
    /// delegate so this service keeps knowing nothing about walking.</param>
    public Vector3? FindBorderPoint(uint destinationMapId, Vector3 from,
                                    Func<Vector3, float, float, Vector3?>? reachable = null)
    {
        if (destinationMapId == 0) return null;
        EnsureLoaded();
        if (_borders.Count == 0) return null;

        if (!_data.GetExcelSheet<Map>().TryGetRow(destinationMapId, out var destinationMap)) return null;
        var destinationTerritory = destinationMap.TerritoryType.RowId;

        // Several borders can lead to the same zone (Gridania has two to Tiefer
        // Wald) - the nearest one is the one the player means.
        Border? chosen = null;
        var chosenDistance = float.MaxValue;
        foreach (var candidate in _borders)
        {
            if (candidate.Destination != destinationTerritory) continue;
            var distance = Vector2.Distance(new Vector2(candidate.Centre.X, candidate.Centre.Z), new Vector2(from.X, from.Z));
            if (distance >= chosenDistance) continue;
            chosenDistance = distance;
            chosen = candidate;
        }

        if (chosen == null)
        {
            _log.Info($"[Grenze] Karte {destinationMapId} (Territory {destinationTerritory}): keine ExitRange in dieser Zone.");
            return null;
        }

        var border = chosen.Value;
        var candidates = BuildCandidates(border, from);
        var point = candidates[0];
        var picked = 0;
        var rejected = 0;

        // Nearest first, so the extra queries only happen when the near ones are
        // blocked. In the common case this costs a single call.
        if (reachable != null)
        {
            for (var i = 0; i < candidates.Count; i++)
            {
                if (reachable(candidates[i], ProbeRadiusXZ, ProbeRadiusY) == null) { rejected++; continue; }
                point = candidates[i];
                picked = i;
                break;
            }

            // Every candidate rejected: the border is not reachable from here at
            // all. Keep the nearest point rather than inventing something - the
            // walk then reports honestly how far it got and in which direction.
            if (rejected == candidates.Count)
            {
                _log.Info($"[Grenze] Ziel-Territory {destinationTerritory}: alle {candidates.Count} " +
                          "Kandidaten vom Netz abgelehnt - bleibe beim naechsten Punkt.");
                picked = 0;
                point = candidates[0];
            }
        }

        _log.Info($"[Grenze] Ziel-Territory {destinationTerritory}: Boxmitte " +
                  $"({border.Centre.X:F1}|{border.Centre.Y:F1}|{border.Centre.Z:F1}) " +
                  $"Halbmass ({border.HalfExtent.X:F1}|{border.HalfExtent.Z:F1}) -> Punkt " +
                  $"({point.X:F1}|{point.Y:F1}|{point.Z:F1}), " +
                  $"{Vector2.Distance(new Vector2(from.X, from.Z), new Vector2(point.X, point.Z)):F1} m entfernt " +
                  $"statt {chosenDistance:F1} m zur Mitte. " +
                  $"Kandidat {picked + 1}/{candidates.Count}" +
                  (reachable == null ? " (ohne Netzpruefung)" : $", {rejected} abgelehnt") + ".");
        return point;
    }

    /// <summary>
    /// The nearest point of the border first, then alternating to both sides along
    /// it. Walking distance grows with the offset, so this order means "nearest
    /// usable" without sorting: the first candidate the mesh accepts is the answer.
    /// </summary>
    private static List<Vector3> BuildCandidates(Border border, Vector3 from)
    {
        var local = ToBoxSpace(from, border.Centre, border.Yaw);

        // Spread along the axis the player did NOT come in through - that is the
        // width of the border, the direction a way through can hide in. Which axis
        // that is follows from how far outside the box the player stands, relative
        // to each half extent: the one they overshoot more is the way in.
        var overshootX = MathF.Abs(local.X) - border.HalfExtent.X;
        var overshootZ = MathF.Abs(local.Z) - border.HalfExtent.Z;
        var spreadAlongZ = overshootX >= overshootZ;

        var half = spreadAlongZ ? border.HalfExtent.Z : border.HalfExtent.X;
        var basePoint = ClampInside(local, border.HalfExtent);
        var baseOffset = spreadAlongZ ? basePoint.Z : basePoint.X;

        var points = new List<Vector3> { ToWorld(basePoint, border) };

        for (var step = 1; points.Count < MaxCandidates; step++)
        {
            var reach = step * CandidateSpacing;
            var any = false;

            foreach (var offset in new[] { baseOffset + reach, baseOffset - reach })
            {
                // Stay inside the box by the same inset the near point uses; a
                // candidate on the very edge is the coin flip the inset exists for.
                var limit = MathF.Max(half - InsetMeters, 0f);
                if (MathF.Abs(offset) > limit) continue;

                var candidate = spreadAlongZ
                    ? basePoint with { Z = offset }
                    : basePoint with { X = offset };
                points.Add(ToWorld(candidate, border));
                any = true;
                if (points.Count >= MaxCandidates) break;
            }

            if (!any) break;
        }

        return points;
    }

    /// <summary>Position in the box's own coordinates: shift to the centre, then
    /// undo the box's yaw. Only the Y rotation is undone - border trigger boxes
    /// stand upright.</summary>
    private static Vector3 ToBoxSpace(Vector3 world, Vector3 centre, float yaw)
    {
        var sin = MathF.Sin(-yaw);
        var cos = MathF.Cos(-yaw);
        var delta = world - centre;
        return new Vector3(delta.X * cos - delta.Z * sin, delta.Y, delta.X * sin + delta.Z * cos);
    }

    /// <summary>Back from box coordinates to world. The border's own height is
    /// kept - the caller resolves the walkable height afterwards anyway.</summary>
    private static Vector3 ToWorld(Vector3 local, Border border)
    {
        var sin = MathF.Sin(border.Yaw);
        var cos = MathF.Cos(border.Yaw);
        return new Vector3(
            border.Centre.X + local.X * cos - local.Z * sin,
            border.Centre.Y,
            border.Centre.Z + local.X * sin + local.Z * cos);
    }

    /// <summary>
    /// Nearest point inside the box, pulled <see cref="InsetMeters"/> further in.
    /// In box coordinates, so a rotated border is handled like an axis-aligned one.
    /// </summary>
    private static Vector3 ClampInside(Vector3 local, Vector3 halfExtent)
    {
        local.X = Math.Clamp(local.X, -halfExtent.X, halfExtent.X);
        local.Z = Math.Clamp(local.Z, -halfExtent.Z, halfExtent.Z);

        // Pull towards the middle, but never past it: on a thin border the inset
        // would otherwise push the point out the far side.
        local.X = PullIn(local.X, halfExtent.X);
        local.Z = PullIn(local.Z, halfExtent.Z);
        return local;
    }

    private static float PullIn(float value, float halfExtent)
    {
        if (halfExtent <= InsetMeters) return 0f;
        var inset = MathF.Max(MathF.Abs(value) - InsetMeters, 0f);
        return MathF.Sign(value) * inset;
    }

    private void EnsureLoaded()
    {
        var territory = (ushort)_clientState.TerritoryType;
        if (territory == _loadedFor) return;

        _loadedFor = territory;
        _borders.Clear();

        if (!_data.GetExcelSheet<TerritoryType>().TryGetRow(territory, out var row)) return;
        var bg = row.Bg.ExtractText();
        if (string.IsNullOrEmpty(bg) || !bg.Contains("/level/")) return;

        var path = "bg/" + bg.Substring(0, bg.LastIndexOf("/level/", StringComparison.Ordinal) + 7) + "planmap.lgb";
        LgbFile? lgb;
        try
        {
            lgb = _data.GetFile<LgbFile>(path);
        }
        catch (Exception ex)
        {
            _log.Info($"[Grenze] {path} nicht lesbar: {ex.Message}");
            return;
        }

        if (lgb == null) return;

        foreach (var layer in lgb.Layers)
        {
            foreach (var instance in layer.InstanceObjects)
            {
                if (instance.AssetType != LayerEntryType.ExitRange) continue;
                var exit = (LayerCommon.ExitRangeInstanceObject)instance.Object;
                var transform = instance.Transform;
                _borders.Add(new Border(
                    new Vector3(transform.Translation.X, transform.Translation.Y, transform.Translation.Z),
                    new Vector3(transform.Scale.X, transform.Scale.Y, transform.Scale.Z),
                    transform.Rotation.Y,
                    exit.TerritoryType,
                    exit.PlayerRunningDirection));
            }
        }

        _log.Info($"[Grenze] Zone {territory}: {_borders.Count} Zonengrenzen aus {path} gelesen.");
    }

    /// <summary>One zone border. <see cref="HalfExtent"/> is the raw
    /// <c>Transform.Scale</c> - measured to be the HALF extent, see class docs.</summary>
    private readonly record struct Border(
        Vector3 Centre,
        Vector3 HalfExtent,
        float Yaw,
        uint Destination,
        float RunningDirection);
}
