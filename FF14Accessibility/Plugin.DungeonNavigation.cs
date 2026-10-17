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
        ApplyDungeonMeshCompatibility();
        if (!_dzemaelMeshNeedsRebuild) return true;
        RebuildNavigationMesh();
        return false;
    }

    // Both a movement request and explicit /acc navrebuild must register the
    // door customization before rebuilding. Rebuilding the unprepared scene
    // would reproduce the original plugin's disconnected territory-1330 mesh.
    private void ApplyDungeonMeshCompatibility()
    {
        if (ClientState.TerritoryType != 1330) return;
        var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "vnavmesh");
        if (assembly == null) return;
        try
        {
            var result = DungeonMeshCompatibility.Apply(assembly);
            if (!_dzemaelCompatibilityReported)
            {
                Log.Info($"[DungeonNav] Dzemael compatibility={result} vnav={assembly.GetName().Version} "
                    + $"loadedFromMemory={string.IsNullOrEmpty(assembly.Location)} mvid={assembly.ManifestModule.ModuleVersionId}");
                _dzemaelCompatibilityReported = true;
            }
            if (result == DungeonMeshCompatibility.Result.Applied) _dzemaelMeshNeedsRebuild = true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[DungeonNav] Dzemael compatibility unavailable; dependency unchanged.");
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
        if (nav.LastCallFailed)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.NavigationMeshRebuildFailed);
            return;
        }
        if (progress >= 0)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.MeshStillLoading(progress * 100));
            return;
        }
        _autoWalk.StopQuiet();
        _navigation.StopWalkGuideQuiet();
        ApplyDungeonMeshCompatibility();
        var accepted = nav.Rebuild();
        if (accepted && ClientState.TerritoryType == 1330) _dzemaelMeshNeedsRebuild = false;
        Log.Info($"[DungeonNav] Rebuild territory={ClientState.TerritoryType} map={ClientState.MapId} accepted={accepted}");
        _tolk.SpeakInterrupt(accepted ? AccessibilityStrings.NavigationMeshRebuilding
            : AccessibilityStrings.NavigationMeshRebuildFailed);
    }
}
