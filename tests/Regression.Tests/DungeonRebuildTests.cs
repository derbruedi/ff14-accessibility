using System.Reflection;
using System.Runtime.CompilerServices;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using FF14Accessibility;
using FF14Accessibility.Services;

namespace Regression.Tests;

public sealed class DungeonRebuildTests
{
    [Theory]
    [InlineData(1330, true, false)]
    [InlineData(1330, false, true)]
    [InlineData(171, true, true)]
    public void PendingDzemaelRepairClearsOnlyWhenItsRebuildIsAccepted(int territory, bool accepted, bool pending)
    {
        var (plugin, calls) = Create((uint)territory, -1, accepted);
        Invoke(plugin);
        Assert.True(Assert.Single(calls));
        Assert.Equal(pending, GetPending(plugin));
    }

    [Fact]
    public void RunningBuildDoesNotStartAnotherRebuildOrForgetThePendingRepair()
    {
        var (plugin, calls) = Create(1330, 0.5f, true);
        Invoke(plugin);
        Assert.Empty(calls);
        Assert.True(GetPending(plugin));
    }

    [Fact]
    public void UnavailableBuildStatusDoesNotChangeCompatibilityOrRequestARebuild()
    {
        var (plugin, calls) = Create(1330, -1, true);
        var walk = (AutoWalkService)typeof(Plugin).GetField("_autoWalk", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(plugin)!;
        Set(walk.Navmesh, "_buildProgress", NavigationStopTests.Proxy.Of<ICallGateSubscriber<float>>(_ =>
            throw new InvalidOperationException("IPC unavailable")));
        Invoke(plugin);
        Assert.Empty(calls);
        Assert.True(GetPending(plugin));
    }

    private static (Plugin Plugin, List<bool> Calls) Create(uint territory, float progress, bool accepted)
    {
        var calls = new List<bool>();
        var plugin = (Plugin)RuntimeHelpers.GetUninitializedObject(typeof(Plugin));
        var walk = (AutoWalkService)RuntimeHelpers.GetUninitializedObject(typeof(AutoWalkService));
        var nav = (NavmeshIpc)RuntimeHelpers.GetUninitializedObject(typeof(NavmeshIpc));
        var log = NavigationStopTests.Proxy.Of<IPluginLog>(_ => null);
        Set(nav, "_log", log);
        Set(nav, "_buildProgress", NavigationStopTests.Proxy.Of<ICallGateSubscriber<float>>(_ => progress));
        Set(nav, "_rebuild", NavigationStopTests.Proxy.Of<ICallGateSubscriber<bool>>(_ =>
        {
            calls.Add(GetPending(plugin));
            return accepted;
        }));
        Set(walk, "_nav", nav);
        Set(plugin, "_autoWalk", walk);
        Set(plugin, "_navigation", RuntimeHelpers.GetUninitializedObject(typeof(NavigationService)));
        Set(plugin, "_tolk", RuntimeHelpers.GetUninitializedObject(typeof(TolkService)));
        Set(plugin, "_dzemaelMeshNeedsRebuild", true);
        typeof(Plugin).GetProperty("ClientState", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(plugin,
            NavigationStopTests.Proxy.Of<IClientState>(m => m.Name switch
            {
                "get_TerritoryType" => territory, "get_MapId" => 86u,
                _ => throw new NotSupportedException(m.Name)
            }));
        typeof(Plugin).GetProperty("Log", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(plugin, log);
        return (plugin, calls);
    }

    private static bool GetPending(Plugin plugin) => (bool)typeof(Plugin)
        .GetField("_dzemaelMeshNeedsRebuild", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(plugin)!;
    private static void Invoke(Plugin plugin) => typeof(Plugin)
        .GetMethod("RebuildNavigationMesh", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(plugin, null);
    private static void Set(object target, string name, object value) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
}
