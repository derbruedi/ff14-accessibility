using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace FF14Accessibility.Services;

/// <summary>
/// Self-walked trails over gaps in the navmesh.
///
/// WHY THIS EXISTS: vnavmesh builds its mesh itself with Recast, for an idealised
/// figure - max 55 degrees of slope, max 0.5 m of step, and no jump-down links by
/// default (Navmesh.NavmeshSettings, decompiled 2026-08-10). Anything reachable
/// only over a steep slope or a ledge simply does not exist in the mesh, which is
/// why Eastern La Noscea falls apart into plateau and coast. Cache rebuild, a
/// newer vnavmesh and every one of its 51 forks were checked and change nothing,
/// and the Recast parameters are not settable from outside.
///
/// WHY RECORDING RATHER THAN SEARCHING: an earlier version searched for the
/// crossing automatically by reading the mesh cache. It guessed wrong often
/// enough to be worse than useless and once locked the player on a plateau (a
/// crossing that only works downhill is a trap), so the user had it removed in
/// V5.78. A trail the player has WALKED is not a guess. And the player does not
/// need to see the way to walk it - which is the whole point here.
///
/// The recorded points are then driven by <c>vnavmesh.Path.MoveTo</c>, which
/// walks a fixed list without any pathfinding. See docs/game-api.md.
/// </summary>
public sealed class TrailService
{
    /// <summary>Record a point every this many metres. Dense enough that
    /// FollowPath steers along the real walked line instead of cutting a corner
    /// into the rock, sparse enough that a long trail stays a handful of points.</summary>
    private const float RecordStepDistance = 2f;

    /// <summary>Shorter than this and it is not a crossing, it is a stumble.</summary>
    private const float MinTrailLength = 5f;

    /// <summary>A trail that stays within this much total height difference may
    /// also be walked backwards. Beyond it the trail goes DOWN somewhere, and the
    /// figure walks down ledges but never up them - offering the way back would
    /// steer it into a wall. The player records the way back separately.</summary>
    private const float BothWaysHeightTolerance = 1.5f;

    /// <summary>How close the player must be to a trail's first point for it to
    /// be offered. Generous, because the auto-walk stops wherever the mesh runs
    /// out, which is not exactly where the recording began.</summary>
    public const float EntryRange = 15f;

    /// <summary>A trail is only worth taking if its far end gets us at least this
    /// much closer to the destination than we are now.</summary>
    private const float MinGain = 10f;

    private readonly IObjectTable _objectTable;
    private readonly IClientState _clientState;
    private readonly TolkService _tolk;
    private readonly Configuration _config;
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly IPluginLog _log;

    private bool _recording;
    private ushort _recordTerritory;
    private readonly List<Vector3> _recorded = new();

    /// <summary>Whether a recording is currently running.</summary>
    public bool IsRecording => _recording;

    public TrailService(
        IDalamudPluginInterface pluginInterface,
        IObjectTable objectTable,
        IClientState clientState,
        TolkService tolk,
        Configuration config,
        IPluginLog log)
    {
        _pluginInterface = pluginInterface;
        _objectTable = objectTable;
        _clientState = clientState;
        _tolk = tolk;
        _config = config;
        _log = log;
    }

    // ── Aufzeichnen ──────────────────────────────────────────────────

    /// <summary>Starts a recording, or ends and stores the running one.</summary>
    public void ToggleRecording()
    {
        if (_recording) { FinishRecording(); return; }

        var player = _objectTable.LocalPlayer;
        if (player == null) return;

        _recording = true;
        _recordTerritory = (ushort)_clientState.TerritoryType;
        _recorded.Clear();
        _recorded.Add(player.Position);
        _log.Info($"[Spur] Aufzeichnung gestartet in Gebiet {_recordTerritory} bei ({Fmt(player.Position)})");
        _tolk.SpeakInterrupt(AccessibilityStrings.TrailRecordingStarted);
    }

    /// <summary>Runs every frame; appends a point once the player has moved far
    /// enough from the last one.</summary>
    public void Update()
    {
        if (!_recording) return;

        var player = _objectTable.LocalPlayer;
        if (player == null) return;

        // Leaving the zone ends the recording - the points would be meaningless
        // in another area, and silently keeping a dead recording running is worse
        // than saying so.
        if ((ushort)_clientState.TerritoryType != _recordTerritory)
        {
            _recording = false;
            _recorded.Clear();
            _log.Info("[Spur] Aufzeichnung abgebrochen: Gebiet gewechselt.");
            _tolk.SpeakInterrupt(AccessibilityStrings.TrailRecordingCancelledZone);
            return;
        }

        if (Vector3.Distance(player.Position, _recorded[^1]) < RecordStepDistance) return;
        _recorded.Add(player.Position);
    }

    private void FinishRecording()
    {
        _recording = false;

        var player = _objectTable.LocalPlayer;
        if (player != null && (_recorded.Count == 0 ||
                               Vector3.Distance(player.Position, _recorded[^1]) > 0.5f))
        {
            // The last step is rarely a full RecordStepDistance - without this the
            // trail would end short of where the player actually got through.
            _recorded.Add(player.Position);
        }

        var length = PathLength(_recorded);
        if (_recorded.Count < 2 || length < MinTrailLength)
        {
            _log.Info($"[Spur] Verworfen: {_recorded.Count} Punkte, {length:F1} m.");
            _recorded.Clear();
            _tolk.SpeakInterrupt(AccessibilityStrings.TrailTooShort);
            return;
        }

        // Height decides whether the way back is honest. Compared over the whole
        // trail, not just the ends: a trail that drops five metres and climbs them
        // again is a hollow, and its way back is just as walkable.
        var minY = _recorded.Min(p => p.Y);
        var maxY = _recorded.Max(p => p.Y);
        var bothWays = maxY - minY <= BothWaysHeightTolerance;

        var trail = new NavTrail
        {
            Territory = _recordTerritory,
            Name = NextTrailName(_recordTerritory),
            Points = _recorded.Select(p => new[] { p.X, p.Y, p.Z }).ToList(),
            BothWays = bothWays,
        };
        _config.Trails.Add(trail);
        _pluginInterface.SavePluginConfig(_config);

        _log.Info($"[Spur] Gespeichert: '{trail.Name}', {trail.Points.Count} Punkte, {length:F1} m, " +
                  $"Hoehe {minY:F1}..{maxY:F1}, beidseitig={bothWays}");
        _recorded.Clear();

        _tolk.SpeakInterrupt(AccessibilityStrings.TrailSaved(trail.Name, length));
        // The one-way case is a promise the plugin cannot keep in reverse, so it
        // is said out loud rather than hidden in a log line.
        if (!bothWays) _tolk.Speak(AccessibilityStrings.TrailOneWayOnly(maxY - minY));
    }

    private string NextTrailName(ushort territory)
    {
        var used = _config.Trails.Count(t => t.Territory == territory);
        return AccessibilityStrings.TrailDefaultName(used + 1);
    }

    // ── Verwalten ────────────────────────────────────────────────────

    /// <summary>Speaks every trail recorded for the current zone (/acc trails).</summary>
    public void AnnounceTrails()
    {
        var territory = (ushort)_clientState.TerritoryType;
        var here = _config.Trails.Where(t => t.Territory == territory).ToList();
        if (here.Count == 0)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.TrailNoneHere);
            return;
        }

        _tolk.SpeakInterrupt(AccessibilityStrings.TrailCount(here.Count));
        for (var i = 0; i < here.Count; i++)
        {
            var t = here[i];
            _tolk.Speak(AccessibilityStrings.TrailListEntry(
                i + 1, t.Name, PathLength(ToVectors(t)), t.BothWays));
        }
    }

    /// <summary>Deletes trail number <paramref name="number"/> of the current zone
    /// (1-based, as spoken by <see cref="AnnounceTrails"/>).</summary>
    public void DeleteTrail(int number)
    {
        var territory = (ushort)_clientState.TerritoryType;
        var here = _config.Trails.Where(t => t.Territory == territory).ToList();
        if (number < 1 || number > here.Count)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.TrailUnknownNumber);
            return;
        }

        var trail = here[number - 1];
        _config.Trails.Remove(trail);
        _pluginInterface.SavePluginConfig(_config);
        _log.Info($"[Spur] Geloescht: '{trail.Name}' in Gebiet {territory}.");
        _tolk.SpeakInterrupt(AccessibilityStrings.TrailDeleted(trail.Name));
    }

    // ── Benutzen ─────────────────────────────────────────────────────

    /// <summary>
    /// Looks for a trail that starts within reach of <paramref name="from"/> and
    /// ends measurably closer to <paramref name="destination"/>. Returns the
    /// points already in the direction to walk them, or null.
    ///
    /// Backwards use only for trails marked <see cref="NavTrail.BothWays"/> - see
    /// the field's remarks on why the way back is not free.
    /// </summary>
    public List<Vector3>? FindUsableTrail(Vector3 from, Vector3 destination, out string name)
    {
        name = string.Empty;
        var territory = (ushort)_clientState.TerritoryType;
        var currentGap = Vector3.Distance(from, destination);

        List<Vector3>? best = null;
        var bestGain = MinGain;

        foreach (var trail in _config.Trails.Where(t => t.Territory == territory && t.Points.Count >= 2))
        {
            var points = ToVectors(trail);

            foreach (var forward in new[] { true, false })
            {
                if (!forward && !trail.BothWays) continue;

                var ordered = forward ? points : Enumerable.Reverse(points).ToList();
                if (Vector3.Distance(from, ordered[0]) > EntryRange) continue;

                var gain = currentGap - Vector3.Distance(ordered[^1], destination);
                if (gain <= bestGain) continue;

                bestGain = gain;
                best = ordered;
                name = trail.Name;
            }
        }

        if (best != null)
        {
            _log.Info($"[Spur] Passend: '{name}', {best.Count} Punkte, Einstieg {Vector3.Distance(from, best[0]):F1} m, " +
                      $"bringt {bestGain:F1} m naeher.");
        }

        return best;
    }

    private static List<Vector3> ToVectors(NavTrail trail)
        => trail.Points.Select(p => new Vector3(p[0], p[1], p[2])).ToList();

    private static float PathLength(IReadOnlyList<Vector3> points)
    {
        var total = 0f;
        for (var i = 1; i < points.Count; i++) total += Vector3.Distance(points[i - 1], points[i]);
        return total;
    }

    private static string Fmt(Vector3 v) => $"{v.X:F1}|{v.Y:F1}|{v.Z:F1}";
}
