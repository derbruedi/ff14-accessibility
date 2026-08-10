using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;

namespace FF14Accessibility.Services;

/// <summary>
/// The single door to the external vnavmesh plugin. Every IPC gate we use sits
/// here, wrapped once, so the walk logic reads as plain steps instead of a wall
/// of try-catch. vnavmesh is a foreign plugin that can be missing, disabled or
/// mid-load, and an INVOKE on an unregistered gate throws (IpcNotReadyError) -
/// that is the one place where catching is right, and it is this class.
///
/// Gate names and signatures decompiled from the installed vnavmesh.dll
/// (Navmesh.IPCProvider, 2026-08-10). See docs/game-api.md -> "vnavmesh-IPC".
/// </summary>
public sealed class NavmeshIpc
{
    private readonly IPluginLog _log;

    private readonly ICallGateSubscriber<bool> _isReady;
    private readonly ICallGateSubscriber<float> _buildProgress;
    private readonly ICallGateSubscriber<Vector3, bool, float, bool> _moveCloseTo;
    private readonly ICallGateSubscriber<List<Vector3>, bool, object> _moveAlong;
    private readonly ICallGateSubscriber<object> _stop;
    private readonly ICallGateSubscriber<bool> _isRunning;
    private readonly ICallGateSubscriber<int> _numWaypoints;
    private readonly ICallGateSubscriber<List<Vector3>> _listWaypoints;
    private readonly ICallGateSubscriber<bool> _pathfindInProgress;
    private readonly ICallGateSubscriber<Vector3, float, float, Vector3?> _nearestPoint;
    private readonly ICallGateSubscriber<Vector3, float, float, Vector3?> _nearestPointReachable;
    private readonly ICallGateSubscriber<Vector3, bool, float, Vector3?> _pointOnFloor;

    /// <summary>True once any invoke has failed with the plugin absent, so callers
    /// can tell "vnavmesh is missing" from "vnavmesh says no".</summary>
    public bool LastCallFailed { get; private set; }

    public NavmeshIpc(IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        _log = log;

        // Subscribing is always safe - only INVOKE throws while vnavmesh is absent.
        _isReady               = pluginInterface.GetIpcSubscriber<bool>("vnavmesh.Nav.IsReady");
        _buildProgress         = pluginInterface.GetIpcSubscriber<float>("vnavmesh.Nav.BuildProgress");
        _moveCloseTo           = pluginInterface.GetIpcSubscriber<Vector3, bool, float, bool>("vnavmesh.SimpleMove.PathfindAndMoveCloseTo");
        _moveAlong             = pluginInterface.GetIpcSubscriber<List<Vector3>, bool, object>("vnavmesh.Path.MoveTo");
        _stop                  = pluginInterface.GetIpcSubscriber<object>("vnavmesh.Path.Stop");
        _isRunning             = pluginInterface.GetIpcSubscriber<bool>("vnavmesh.Path.IsRunning");
        _numWaypoints          = pluginInterface.GetIpcSubscriber<int>("vnavmesh.Path.NumWaypoints");
        _listWaypoints         = pluginInterface.GetIpcSubscriber<List<Vector3>>("vnavmesh.Path.ListWaypoints");
        _pathfindInProgress    = pluginInterface.GetIpcSubscriber<bool>("vnavmesh.SimpleMove.PathfindInProgress");
        _nearestPoint          = pluginInterface.GetIpcSubscriber<Vector3, float, float, Vector3?>("vnavmesh.Query.Mesh.NearestPoint");
        _nearestPointReachable = pluginInterface.GetIpcSubscriber<Vector3, float, float, Vector3?>("vnavmesh.Query.Mesh.NearestPointReachable");
        _pointOnFloor          = pluginInterface.GetIpcSubscriber<Vector3, bool, float, Vector3?>("vnavmesh.Query.Mesh.PointOnFloor");
    }

    private T Call<T>(ICallGateSubscriber<T> gate, T fallback, string what)
    {
        try
        {
            var result = gate.InvokeFunc();
            LastCallFailed = false;
            return result;
        }
        catch (Exception ex)
        {
            LastCallFailed = true;
            _log.Verbose(ex, $"[Nav] vnavmesh-IPC '{what}' nicht erreichbar");
            return fallback;
        }
    }

    /// <summary>Whether the zone's navmesh is loaded and usable.</summary>
    public bool IsReady => Call(_isReady, false, "Nav.IsReady");

    /// <summary>Build progress 0..1, or -1 when no build is running. Also -1 when
    /// vnavmesh is absent - callers distinguish via <see cref="LastCallFailed"/>.</summary>
    public float BuildProgress => Call(_buildProgress, -1f, "Nav.BuildProgress");

    /// <summary>Whether vnavmesh is currently steering the character
    /// (FollowPath.Waypoints.Count > 0).</summary>
    public bool IsRunning => Call(_isRunning, false, "Path.IsRunning");

    /// <summary>How many waypoints are left on the running path.</summary>
    public int NumWaypoints => Call(_numWaypoints, 0, "Path.NumWaypoints");

    /// <summary>Whether a pathfind task is still computing. Crucial: during this
    /// window <see cref="IsRunning"/> still describes the PREVIOUS path.</summary>
    public bool PathfindInProgress => Call(_pathfindInProgress, false, "SimpleMove.PathfindInProgress");

    /// <summary>The waypoints of the running path, empty when none.</summary>
    public List<Vector3> Waypoints => Call(_listWaypoints, new List<Vector3>(), "Path.ListWaypoints") ?? new List<Vector3>();

    /// <summary>
    /// Queues a pathfind and starts walking, stopping <paramref name="range"/>
    /// short of the destination. False means vnavmesh refused - either a pathfind
    /// was already queued (AsyncMoveRequest.MoveTo returns false then) or the
    /// plugin is unreachable.
    /// </summary>
    public bool MoveCloseTo(Vector3 destination, float range)
    {
        try
        {
            var ok = _moveCloseTo.InvokeFunc(destination, false, range);
            LastCallFailed = false;
            return ok;
        }
        catch (Exception ex)
        {
            LastCallFailed = true;
            _log.Warning(ex, "[Nav] vnavmesh-IPC 'PathfindAndMoveCloseTo' fehlgeschlagen");
            return false;
        }
    }

    /// <summary>
    /// Walks a FIXED list of points, with no pathfinding at all: the list goes
    /// straight to <c>FollowPath.Move</c>. This is the only way to steer the
    /// character over ground the navmesh does not know - everything else asks the
    /// same map vnavmesh does and can never get further than vnavmesh itself.
    ///
    /// CAUTION (decompiled 2026-08-10): with the user's <c>StopOnStuck</c> +
    /// <c>RetryOnStuck</c>, half a second without movement makes FollowPath drop
    /// OUR list and re-route normally to its LAST point (<c>OnStuck</c> →
    /// <c>AsyncMoveRequest.MoveTo</c>). Over a gap the mesh does not know, that
    /// turns the crossing back into a phantom path. Callers must watch the
    /// waypoint count: it may only ever shrink while our list is being walked.
    /// </summary>
    public bool MoveAlong(List<Vector3> waypoints)
    {
        try
        {
            _moveAlong.InvokeAction(waypoints, false);
            LastCallFailed = false;
            return true;
        }
        catch (Exception ex)
        {
            LastCallFailed = true;
            _log.Warning(ex, "[Nav] vnavmesh-IPC 'Path.MoveTo' fehlgeschlagen");
            return false;
        }
    }

    /// <summary>
    /// Clears the running path. Note this does NOT cancel a pathfind that is
    /// already computing: AsyncMoveRequest.Update hands the finished result to
    /// FollowPath.Move regardless (decompiled 2026-08-10), so a stop can be
    /// undone a moment later by a task that was already in flight. Callers must
    /// keep watching - see AutoWalkService's stop guard.
    /// </summary>
    public void Stop()
    {
        try
        {
            _stop.InvokeAction();
            LastCallFailed = false;
        }
        catch (Exception ex)
        {
            LastCallFailed = true;
            _log.Warning(ex, "[Nav] vnavmesh-IPC 'Path.Stop' fehlgeschlagen");
        }
    }

    /// <summary>Nearest mesh point inside a box around <paramref name="p"/>, or null.</summary>
    public Vector3? NearestPoint(Vector3 p, float halfExtentXZ, float halfExtentY)
        => Query(_nearestPoint, p, halfExtentXZ, halfExtentY, "Query.Mesh.NearestPoint");

    /// <summary>
    /// Nearest mesh point that is actually REACHABLE from the current mesh
    /// component (vnavmesh applies its FloodFillAwareFilter, NavmeshQuery line 83).
    /// Returns null when the area around <paramref name="p"/> belongs to a mesh
    /// island the player cannot walk to - which is exactly how we tell "far away"
    /// from "no route exists".
    /// </summary>
    public Vector3? NearestPointReachable(Vector3 p, float halfExtentXZ, float halfExtentY)
        => Query(_nearestPointReachable, p, halfExtentXZ, halfExtentY, "Query.Mesh.NearestPointReachable");

    /// <summary>Walkable floor straight below <paramref name="p"/>, or null.</summary>
    public Vector3? PointOnFloor(Vector3 p, float halfExtentXZ)
    {
        try
        {
            var result = _pointOnFloor.InvokeFunc(p, false, halfExtentXZ);
            LastCallFailed = false;
            return result;
        }
        catch (Exception ex)
        {
            LastCallFailed = true;
            _log.Verbose(ex, "[Nav] vnavmesh-IPC 'Query.Mesh.PointOnFloor' nicht erreichbar");
            return null;
        }
    }

    private Vector3? Query(
        ICallGateSubscriber<Vector3, float, float, Vector3?> gate,
        Vector3 p, float halfExtentXZ, float halfExtentY, string what)
    {
        try
        {
            var result = gate.InvokeFunc(p, halfExtentXZ, halfExtentY);
            LastCallFailed = false;
            return result;
        }
        catch (Exception ex)
        {
            LastCallFailed = true;
            _log.Verbose(ex, $"[Nav] vnavmesh-IPC '{what}' nicht erreichbar");
            return null;
        }
    }
}
