using FF14Accessibility.Services;

namespace FF14Accessibility;

public sealed partial class Plugin
{
    private bool _dzemaelCompatibilityReported;
    private bool _dzemaelMeshNeedsRebuild;

    // Called only on a user's navigation request, before any path is submitted.
    // The mesh builder must be idle before its customization registry changes.
    private bool PrepareDungeonNavigation()
    {
        if (ClientState.TerritoryType != 1330) return true;
        var nav = _autoWalk.Navmesh;
        var progress = nav.BuildProgress;
        if (nav.LastCallFailed) return true; // ordinary navigation reports missing IPC
        if (progress >= 0 || nav.PathfindInProgress || _autoWalk.IsActive
            || _navigation.IsWalkGuideActive || nav.IsRunning) return true;
        var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "vnavmesh");
        if (assembly == null) return true;
        try
        {
            var result = DungeonMeshCompatibility.Apply(assembly);
            if (!_dzemaelCompatibilityReported)
            {
                Log.Info($"[DungeonNav] Dzemael compatibility={result} vnav={assembly.GetName().Version}");
                _dzemaelCompatibilityReported = true;
            }
            if (result == DungeonMeshCompatibility.Result.Applied) _dzemaelMeshNeedsRebuild = true;
            if (!_dzemaelMeshNeedsRebuild) return true;
            _autoWalk.StopQuiet();
            _navigation.StopWalkGuideQuiet();
            var rebuilt = nav.Rebuild();
            if (rebuilt) _dzemaelMeshNeedsRebuild = false;
            _tolk.SpeakInterrupt(rebuilt ? AccessibilityStrings.NavigationMeshRebuilding
                : AccessibilityStrings.NavigationMeshRebuildFailed);
            return false;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[DungeonNav] Dzemael compatibility unavailable; dependency unchanged.");
            return true;
        }
    }

    private void ReadNavigationMeshStatus()
    {
        var nav = _autoWalk.Navmesh;
        var ready = nav.IsReady;
        var failed = nav.LastCallFailed;
        var progress = nav.BuildProgress;
        _dungeonRoute.Reload();
        var steps = _dungeonRoute.GetStepsForCurrentZone().Count;
        Log.Info($"[DungeonNav] Status territory={ClientState.TerritoryType} map={ClientState.MapId} "
            + $"player={ObjectTable.LocalPlayer?.Position} ready={ready} ipcFailed={failed} progress={progress} "
            + $"stations={steps} autoWalk={_autoWalk.IsActive} guide={_navigation.IsWalkGuideActive}");
        _tolk.SpeakInterrupt(failed ? AccessibilityStrings.AutoWalkUnavailable
            : progress >= 0 && !ready ? AccessibilityStrings.MeshStillLoading(progress * 100)
            : AccessibilityStrings.NavigationMeshStatus(ready, steps));
    }

    private void RebuildNavigationMesh()
    {
        var nav = _autoWalk.Navmesh;
        var progress = nav.BuildProgress;
        if (!nav.LastCallFailed && progress >= 0)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.MeshStillLoading(progress * 100));
            return;
        }
        _autoWalk.StopQuiet();
        _navigation.StopWalkGuideQuiet();
        var accepted = nav.Rebuild();
        Log.Info($"[DungeonNav] Rebuild territory={ClientState.TerritoryType} map={ClientState.MapId} accepted={accepted}");
        _tolk.SpeakInterrupt(accepted ? AccessibilityStrings.NavigationMeshRebuilding
            : AccessibilityStrings.NavigationMeshRebuildFailed);
    }
}
