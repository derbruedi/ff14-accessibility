using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Plugin.Services;

namespace FF14Accessibility.Services;

/// <summary>
/// Known gaps in vnavmesh's walkable mesh, and the short crossing that mends each
/// one. Same idea as a recorded trail (<see cref="TrailService"/>) - drive a fixed
/// list of points with vnavmesh's pathfinding out of the loop - but the points do
/// not come from the player walking them.
///
/// <para>
/// WHY THEY CANNOT COME FROM WALKING. A recorded trail asks the player to walk the
/// gap once. That works for a sighted player and not for ours: *"ich kann das nicht
/// selber ablaufen weil ich den weg nicht weiss deswegen nutze ich ja vnavmesh weil
/// ich nichts sehe"*. So the gaps get measured offline instead, with
/// <c>tools/navmesh-gaps</c>, straight out of the mesh vnavmesh already built.
/// </para>
///
/// <para>
/// WHY THE GAPS EXIST, read off vnavmesh's build code:
/// <c>NavmeshBuilder.cs:74</c> derives <c>walkableRadius = ceil(AgentRadius /
/// CellSize)</c> = 2 cells and <c>RcAreas.ErodeWalkableArea</c> shaves that off
/// every edge, so anything narrower than a metre stops being walkable. The two
/// built-in repairs both require at least 1,5 m of drop and are off by default.
/// vnavmesh calls the remedy <c>AreaId.Shortcut</c> - "walking through a gap that
/// recast thinks is too narrow" - and fixes such places by hand, one
/// <c>LinkPoints</c> per crossing. This table is the same fix on our side of the
/// fence, because a patched vnavmesh may not be redistributed (it ships without a
/// licence).
/// </para>
///
/// <para>
/// NOTHING IN HERE IS GUESSED. Every entry names how it was measured. A crossing is
/// only added once the geometry at that spot has been checked with
/// <c>tools/zone-probe</c> - two surfaces half a metre apart are sometimes a
/// gangplank and sometimes a railing, and generating links from geometry alone is
/// exactly what locked the player onto a plateau in V5.78.
/// </para>
/// </summary>
public sealed class MeshBridgeService
{
    /// <summary>How close the character has to be to the near end before the
    /// crossing is driven. The caller walks there normally first (the near end is
    /// on the reachable side by definition), so this only absorbs the stop range.</summary>
    private const float EntryRange = 4f;

    private readonly IClientState _clientState;
    private readonly IPluginLog _log;

    public MeshBridgeService(IClientState clientState, IPluginLog log)
    {
        _clientState = clientState;
        _log = log;
    }

    /// <summary>
    /// One measured gap. <see cref="Reach"/> is how far from <see cref="To"/> the
    /// detached surface extends - a destination further away than that is not on
    /// the island this crossing opens, so the crossing would not help.
    /// </summary>
    private readonly record struct Bridge(
        ushort Territory,
        string Name,
        Vector3 From,
        Vector3 To,
        float Reach,
        string Evidence);

    /// <summary>
    /// Measured crossings. Coordinates are world coordinates, exactly as
    /// navmeshgaps prints them.
    /// </summary>
    private static readonly Bridge[] Bridges =
    {
        // New Gridania -> Tiefer Wald. The whole border area hangs off a 36-polygon
        // island; from the player's 1466-polygon surface every one of the nine
        // border candidates answers "mesh here, but no route" (measured
        // 2026-08-22 with navmeshgaps --from/--at against the player's own cache).
        // From the island, all of them AND the trigger box centre are reachable.
        // Between the two surfaces: 1,46 m, no height difference at all.
        // zone-probe at the spot shows level ground - the barrels that stop the
        // walk sit further south, at Z 152,4-154,0.
        new(132, "Tor zum Tiefen Wald",
            new Vector3(153.75f, -12.75f, 160.25f),
            new Vector3(155.00f, -12.75f, 161.00f),
            40f,
            "navmeshgaps 2026-08-22: surface 1 (1466) <-> surface 47 (36), gap 1,46 m, drop 0,00 m"),

        // Mor Dhona -> the rock ramp above the scrap camp. Quest objects from three
        // different quests sit up there (QST_GaiUsc601/604/605), the highest at
        // Y 14,9; the mesh knows the whole ramp as a 26-polygon surface running from
        // Y 4,2 to Y 14,0, and it hangs off the player's 15578-polygon ground at
        // exactly one place. The step there is 0,75 m - a mere 25 cm over vnavmesh's
        // own AgentMaxClimb of 0,50 m (NavmeshSettings.cs:25), which is why recast
        // cut it. zone-probe at the spot shows bare rock with collision
        // (l1f1_t1_roc1d.pcb, 1,6 m away) and no railing; the nearest fence stands
        // 7,6 m off.
        //
        // Reach covers the ramp AND the small plateau that hangs off its far end:
        // the quest object "Untersuchungsort fuer Stoerkommando" at
        // (-174,9|10,8|-603,8) is 32,0 m from the far end. Crossing here does not
        // reach that plateau by itself - it is another 3,00 m drop down from the
        // ramp - but it gets the walk to the ramp instead of leaving it 12 m below
        // on the ground, which is where it ended before.
        new(156, "Aufstieg zum Felsplateau",
            new Vector3(-159.75f, 3.50f, -632.25f),
            new Vector3(-159.75f, 4.25f, -631.25f),
            35f,
            "navmeshgaps 2026-08-24: surface 0 (15578) <-> surface 30 (26), gap 1,00 m, drop 0,75 m"),
    };

    /// <summary>
    /// A crossing that leads to <paramref name="destination"/>, or null. Only
    /// called once the destination has been found unreachable, so the answer is
    /// "this gap is why" rather than a shortcut for its own sake.
    /// </summary>
    public IReadOnlyList<Vector3>? FindCrossing(Vector3 from, Vector3 destination, out string name, out Vector3 approach)
    {
        name = string.Empty;
        approach = default;

        var territory = (ushort)_clientState.TerritoryType;
        foreach (var bridge in Bridges)
        {
            if (bridge.Territory != territory) continue;

            // Does this crossing open the ground the destination sits on? Measured
            // per entry rather than assumed - a gap elsewhere in the zone is none
            // of this walk's business.
            var reach = Vector3.Distance(bridge.To, destination);
            if (reach > bridge.Reach) continue;

            name = bridge.Name;
            approach = bridge.From;
            _log.Info($"[Bruecke] '{bridge.Name}' passt: Ziel {reach:F1} m hinter dem Ende " +
                      $"(Reichweite {bridge.Reach:F0} m). {bridge.Evidence}");
            return new[] { bridge.From, bridge.To };
        }

        return null;
    }

    /// <summary>Is the character close enough to the near end to drive the crossing?</summary>
    public static bool AtEntry(Vector3 position, Vector3 entry)
        => Vector3.Distance(position, entry) <= EntryRange;
}
