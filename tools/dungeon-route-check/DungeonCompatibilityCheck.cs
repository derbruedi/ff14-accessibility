using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using FF14Accessibility;
using FF14Accessibility.Services;

// Runs in this tool's separate process. The plugin entry point is real; IPC,
// speech and game state are substitutes and no game or dependency files change.
internal static class DungeonCompatibilityCheck
{
    internal static object Run(Assembly vnav, IPluginLog log, List<string> errors)
    {
        var registryType = vnav.GetType("Navmesh.NavmeshCustomizationRegistry")!;
        var registry = (IDictionary)registryType.GetField("PerTerritory")!.GetValue(null)!;
        var original = registry[171u]!;
        var before = registry.Contains(1330u);
        var countBefore = registry.Count;
        var plugin = (Plugin)RuntimeHelpers.GetUninitializedObject(typeof(Plugin));
        var nav = (NavmeshIpc)RuntimeHelpers.GetUninitializedObject(typeof(NavmeshIpc));
        var walk = (AutoWalkService)RuntimeHelpers.GetUninitializedObject(typeof(AutoWalkService));
        var rebuildCalls = 0;
        var registeredBeforeRebuild = false;
        Set(nav, "_log", log);
        Set(nav, "_buildProgress", Proxy.Make<ICallGateSubscriber<float>>(_ => -1f));
        Set(nav, "_rebuild", Proxy.Make<ICallGateSubscriber<bool>>(_ =>
        {
            ++rebuildCalls;
            registeredBeforeRebuild = registry.Contains(1330u) && ReferenceEquals(registry[1330u], original);
            return true;
        }));
        Set(walk, "_nav", nav);
        Set(plugin, "_autoWalk", walk);
        Set(plugin, "_navigation", RuntimeHelpers.GetUninitializedObject(typeof(NavigationService)));
        Set(plugin, "_tolk", RuntimeHelpers.GetUninitializedObject(typeof(TolkService)));
        typeof(Plugin).GetProperty("ClientState", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(plugin,
            Proxy.Make<IClientState>(m => m.Name switch
            {
                "get_TerritoryType" => 1330u, "get_MapId" => 86u,
                _ => throw new NotSupportedException(m.Name)
            }));
        typeof(Plugin).GetProperty("Log", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(plugin, log);
        typeof(Plugin).GetMethod("RebuildNavigationMesh", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(plugin, null);
        var pending = (bool)typeof(Plugin).GetField("_dzemaelMeshNeedsRebuild", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(plugin)!;
        if (rebuildCalls != 1 || !registeredBeforeRebuild || pending)
            errors.Add("Explicit rebuild did not prepare Dzemael compatibility before requesting the new mesh.");
        if (registry.Count != countBefore + (before ? 0 : 1) || !ReferenceEquals(original, registry[171u]))
            errors.Add("Compatibility changed unrelated territory registrations.");

        // Exercise the selected customization's real code with a small scene.
        // This proves the door filter, not the connectivity of the actual map.
        var definition = Activator.CreateInstance(vnav.GetType("Navmesh.SceneDefinition")!);
        var extractorType = vnav.GetType("Navmesh.SceneExtractor")!;
        var scene = Activator.CreateInstance(extractorType, [definition])!;
        var meshes = (IDictionary)extractorType.GetProperty("Meshes")!.GetValue(scene)!;
        var meshType = extractorType.GetNestedType("Mesh")!;
        var instanceType = extractorType.GetNestedType("MeshInstance")!;
        var keys = new[]
        {
            "bg/ffxiv/roc_r1/rad/r1r1/collision/r1r1_a1_dor01.pcb",
            "bg/ffxiv/roc_r1/rad/r1r1/collision/r1r1_a1_dor02.pcb",
            "bg/ffxiv/roc_r1/rad/r1r1/collision/r1r1_a1_wall01.pcb"
        };
        var lists = keys.Select(key =>
        {
            var mesh = Activator.CreateInstance(meshType)!;
            var instances = (IList)meshType.GetField("Instances")!.GetValue(mesh)!;
            instances.Add(RuntimeHelpers.GetUninitializedObject(instanceType));
            meshes.Add(key, mesh);
            return instances;
        }).ToArray();
        var selected = registryType.GetMethod("ForTerritory")!.Invoke(null, [1330u])!;
        selected.GetType().GetMethod("CustomizeScene")!.Invoke(selected, [scene]);
        var doorsFiltered = lists[0].Count == 0 && lists[1].Count == 0 && lists[2].Count == 1;
        if (!doorsFiltered) errors.Add("The territory-1330 customization did not filter only Dzemael doors.");
        return new { PreviouslyRegistered = before, RebuildCalls = rebuildCalls, RegisteredBeforeRebuild = registeredBeforeRebuild,
            RepairPendingAfterAcceptedRebuild = pending, DoorFilterVerified = doorsFiltered,
            LiveIpcVerified = false, MeshTraversalVerified = false };
    }

    private static void Set(object target, string name, object value) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
}
