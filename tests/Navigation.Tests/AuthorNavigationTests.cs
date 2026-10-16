using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using FF14Accessibility;
using FF14Accessibility.Services;

namespace Navigation.Tests;

// Exercise the copied author implementation through fake vnavmesh calls.
// No native game calls, movement or audio services are started by these tests.
public sealed class AuthorNavigationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PluginStartsRecordedMiounneDestinationWithoutSeparatePreflight(bool live)
    {
        var h = new Harness { TargetPosition = new(23.7793f, -8.042542f, 115.92273f), NativeSearching = true };
        var plugin = h.Plugin();
        var actor = live ? h.Target : null;
        SetField(plugin, "_resolvedNavigationObject", actor);
        SetField(plugin, "_resolvedCanTrackObject", true);
        InvokePlugin(plugin, "StartResolvedWalk", [h.TargetPosition, "Матушка Миунна", 2.5f, false]);
        Assert.Equal([(h.TargetPosition, false, 2.5f)], h.Requests);
        Assert.Equal(live ? 44UL : 0UL, h.Get("_targetId"));
        Assert.True(h.Walk.IsActive);
        if (live)
        {
            h.NativeSearching = false;
            InvokePlugin(plugin, "PollWalkingObject");
            Assert.Single(h.Requests);
            Assert.Equal(1, h.Stops);
        }
    }

    [Fact]
    public void PluginSecondResolvedPressStopsWithoutAnotherSearch()
    {
        var h = new Harness();
        var plugin = h.Plugin();
        InvokePlugin(plugin, "StartResolvedWalk", [h.TargetPosition, "Цель", 2.5f, false]);
        InvokePlugin(plugin, "StartResolvedWalk", [h.TargetPosition, "Цель", 2.5f, false]);
        Assert.Single(h.Requests);
        Assert.False(h.Walk.IsActive);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ManualGuideStartsWhileNativeRouteIsPendingOrPartial(bool completed)
    {
        var h = new Harness { TargetPosition = new(23.7793f, -8.042542f, 115.92273f) };
        var plugin = h.Plugin();
        var navigation = (NavigationService)typeof(Plugin).GetField("_navigation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(plugin)!;
        var routes = (RouteService)RuntimeHelpers.GetUninitializedObject(typeof(RouteService));
        var queries = 0;
        var result = new TaskCompletionSource<List<Vector3>>();
        if (completed) result.SetResult([Vector3.Zero, new(25, 2, 30), h.TargetPosition]);
        SetField(routes, "_navIsReady", Proxy.Of<ICallGateSubscriber<bool>>((_, _) => true));
        SetField(routes, "_navPathfindTolerance", Proxy.Of<ICallGateSubscriber<Vector3, Vector3, bool, float, Task<List<Vector3>>>>((_, _) =>
        { queries++; return result.Task; }));
        var beacon = (BeaconService)RuntimeHelpers.GetUninitializedObject(typeof(BeaconService));
        SetField(beacon, "_config", new Configuration { TargetBeaconEnabled = false });
        SetField(navigation, "_beacon", beacon);
        SetField(navigation, "_routes", routes);
        SetField(navigation, "_objectTable", h.Get("_objectTable"));
        SetField(navigation, "_clientState", h.Get("_clientState"));
        SetField(navigation, "_config", new Configuration { WalkGuideRouteMode = true });
        SetField(navigation, "_log", h.Get("_log"));
        SetField(navigation, "_tolk", h.Get("_tolk"));
        SetField(plugin, "_resolvedCanTrackObject", true);
        InvokePlugin(plugin, "StartResolvedGuide", [h.TargetPosition, "Матушка Миунна", 2.5f]);
        Assert.True(navigation.IsWalkGuideActive);
        Assert.Equal(1, queries);
        Assert.Equal(h.TargetPosition, typeof(NavigationService).GetField("_walkDestPosition", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(navigation));
        Assert.Empty(h.Requests);
    }

    [Fact]
    public void AutomaticRouteDiagnosticsDoNotCalculateOrSpeakTheLongerPathLength()
    {
        var h = new Harness();
        // No RouteService is attached: any attempt to describe/sum this path
        // fails. The automatic walk only retains its diagnostic waypoints.
        h.Invoke("SpeakRoutePreviewOnce", [Vector3.Zero, new List<Vector3> { new(0, 0, 100), new(20, 0, 100), h.TargetPosition }]);
        Assert.Equal(true, h.Get("_routeSpoken"));
    }

    [Fact]
    public void LiveObjectBindingKeepsTheAuthorsMeasuredCrossingUntilItsFinalStage()
    {
        var h = new Harness { Territory = 132, TargetPosition = new(160, -12.75f, 166), Reachable = _ => null };
        h.Set("_bridges", new MeshBridgeService((IClientState)h.Get("_clientState")!, (IPluginLog)h.Get("_log")!));
        var plugin = h.Plugin();
        SetField(plugin, "_resolvedNavigationObject", h.Target);
        InvokePlugin(plugin, "StartResolvedWalk", [h.TargetPosition, "NPC", 2.5f, false]);
        Assert.Equal(new Vector3(153.75f, -12.75f, 160.25f), Assert.Single(h.Requests).Position);
        Assert.NotNull(h.Get("_pendingCrossing"));
        h.Set("_phase", Enum.Parse(typeof(AutoWalkService).GetNestedType("Phase", BindingFlags.NonPublic)!, "Walking"));
        InvokePlugin(plugin, "PollWalkingObject");
        Assert.Single(h.Requests);
        Assert.NotNull(h.Get("_pendingCrossing"));
        // The bridge has been completed by the author's service. Only now may
        // the plugin bind the exact live actor for the final path.
        SetField(h.Walk, "_pendingCrossing", null);
        h.Reachable = p => p;
        InvokePlugin(plugin, "PollWalkingObject");
        Assert.Equal(2, h.Requests.Count);
        Assert.Equal(h.TargetPosition, h.Requests[1].Position);
        Assert.True(h.Walk.IsTrackingObject);
    }

    private static object? InvokePlugin(Plugin plugin, string method, object?[]? args = null)
        => typeof(Plugin).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(plugin, args);
    private static void SetField(object value, string name, object? field)
        => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(value, field);

    [Theory]
    [InlineData(0f)] [InlineData(15f)] [InlineData(-5f)]
    public void GroundWalkUsesNativeSearchAndMoveImmediately(float height)
    {
        var h = new Harness();
        var goal = new Vector3(20, height, 0);
        h.Walk.ToggleToPosition(goal, "Цель", 2.5f);
        Assert.Equal([(goal, false, 2.5f)], h.Requests);
        Assert.Equal(1, h.Stops);
        Assert.True(h.Walk.IsActive);
        Assert.Equal("Starting", h.Phase);
    }

    [Fact]
    public void SecondPositionWalkPressStopsInsteadOfStartingAnotherRoute()
    {
        var h = new Harness();
        h.Walk.ToggleToPosition(h.TargetPosition, "Цель", 2.5f);
        h.Walk.ToggleToPosition(new(30, 10, 0), "Другая цель", 2.5f);
        Assert.Single(h.Requests);
        Assert.Equal(2, h.Stops);
        Assert.False(h.Walk.IsActive);
        Assert.Equal("Guarding", h.Phase);
    }

    [Theory]
    [InlineData("Starting")] [InlineData("Walking")]
    [InlineData("TrailWalking")] [InlineData("Landing")]
    public void StoppingEveryAuthorWalkPhaseClearsTheNativePath(string phase)
    {
        var h = new Harness(phase) { Waypoints = [new(20, 0, 0)] };
        h.Walk.StopQuiet();
        Assert.Empty(h.Waypoints);
        Assert.Equal(1, h.Stops);
        Assert.Equal("Guarding", h.Phase);
        Assert.False(h.Walk.IsWalking);
    }

    [Fact]
    public void AuthorStopGuardClearsAPathDeliveredAfterStop()
    {
        var h = new Harness("Walking");
        h.Walk.StopQuiet();
        h.Waypoints = [new(20, 0, 0)];
        h.Invoke("GuardUpdate");
        Assert.Equal(2, h.Stops);
        Assert.Empty(h.Waypoints);
        Assert.Empty(h.Requests);
    }

    [Fact]
    public void StopGuardWaitsForPendingNativeSearch()
    {
        var h = new Harness("Walking");
        h.Walk.StopQuiet();
        h.Set("_guardUntil", DateTime.UtcNow.AddSeconds(-1));
        h.NativeSearching = true;
        h.Invoke("GuardUpdate");
        Assert.Equal("Guarding", h.Phase);
        h.NativeSearching = false;
        h.Set("_guardUntil", DateTime.UtcNow.AddSeconds(-1));
        h.Invoke("GuardUpdate");
        Assert.Equal("Idle", h.Phase);
    }

    [Fact]
    public void FlightRequestsNativeAirRouteWithoutASeparateQuery()
    {
        var h = new Harness();
        h.Invoke("BeginFlightPath", [new Vector3(20, 10, 0), "Цель", 2.5f, 0UL]);
        Assert.Equal([(new Vector3(20, 10, 0), true, 2.5f)], h.Requests);
        Assert.True(h.Walk.IsActive);
        Assert.True((bool)h.Get("_flying")!);
    }

    [Fact]
    public void BusyFlightDoesNotStartACompetingGroundRoute()
    {
        var h = new Harness { NativeSearching = true, AcceptMove = _ => false };
        h.Invoke("BeginFlightPath", [new Vector3(20, 10, 0), "Цель", 2.5f, 0UL]);
        Assert.Single(h.Requests);
        Assert.True(h.Requests[0].Fly);
        Assert.False(h.Walk.IsActive);
        Assert.Equal("Guarding", h.Phase);
    }

    [Fact]
    public void UnavailableFlightFallsBackToAuthorsGroundWalk()
    {
        var h = new Harness { AcceptMove = fly => !fly };
        h.Invoke("BeginFlightPath", [new Vector3(20, 10, 0), "Цель", 2.5f, 0UL]);
        Assert.Equal(new[] { true, false }, h.Requests.Select(r => r.Fly));
        Assert.True(h.Walk.IsActive);
        Assert.False((bool)h.Get("_flying")!);
        Assert.Equal("Starting", h.Phase);
    }

    [Fact]
    public void NativeFlightSearchCanContinueBeyondGroundTimeout()
    {
        var h = new Harness("Starting") { NativeSearching = true };
        h.Set("_flying", true);
        h.Set("_startedAt", DateTime.UtcNow.AddSeconds(-10));
        h.Invoke("StartingUpdate");
        Assert.True(h.Walk.IsActive);
        Assert.Empty(h.Requests);
        Assert.Equal(0, h.Stops);
    }

    [Fact]
    public void FollowUsesTheSameNativeSearchAndMove()
    {
        var h = new Harness();
        h.Follow();
        h.Invoke("FollowUpdate");
        Assert.Equal([(h.TargetPosition, false, 3f)], h.Requests);
        Assert.True(h.Walk.IsFollowing);
    }

    [Fact]
    public void FollowRepathsWhenTheTargetMoves()
    {
        var h = new Harness();
        h.Follow();
        h.Invoke("FollowUpdate");
        h.TargetPosition = new(25, 0, 0);
        h.Set("_lastFollowPathAt", DateTime.UtcNow.AddSeconds(-1));
        h.Invoke("FollowUpdate");
        Assert.Equal(2, h.Requests.Count);
        Assert.Equal(h.TargetPosition, h.Requests[1].Position);
    }

    [Fact]
    public void FollowWaitsForNativeSearchRatherThanIssuingAnotherRequest()
    {
        var h = new Harness { NativeSearching = true };
        h.Follow();
        h.Invoke("FollowUpdate");
        Assert.Empty(h.Requests);
        Assert.True(h.Walk.IsFollowing);
    }

    [Fact]
    public void FollowWithinNativeStopRangeMakesNoNewRequest()
    {
        var h = new Harness { TargetPosition = new(3, 0, 0) };
        h.Follow();
        h.Invoke("FollowUpdate");
        Assert.Empty(h.Requests);
        Assert.True(h.Walk.IsFollowing);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void FollowStopsOnZoneChangeOrDisappearingTarget(bool changedZone)
    {
        var h = new Harness();
        h.Follow();
        if (changedZone) h.Territory = 134;
        else h.TargetPresent = false;
        h.Invoke("FollowUpdate");
        Assert.False(h.Walk.IsFollowing);
        Assert.Equal(1, h.Stops);
        Assert.Empty(h.Requests);
    }

    [Fact]
    public void ManualFollowStopAlsoArmsTheLatePathGuard()
    {
        var h = new Harness();
        h.Follow();
        h.Walk.StopFollowQuiet();
        h.Waypoints = [new(20, 0, 0)];
        h.Invoke("GuardUpdate");
        Assert.False(h.Walk.IsFollowing);
        Assert.Equal(2, h.Stops);
        Assert.Empty(h.Waypoints);
    }

    private sealed class Harness
    {
        internal readonly AutoWalkService Walk = (AutoWalkService)RuntimeHelpers.GetUninitializedObject(typeof(AutoWalkService));
        private readonly NavmeshIpc _nav = (NavmeshIpc)RuntimeHelpers.GetUninitializedObject(typeof(NavmeshIpc));
        internal uint Territory = 133;
        internal Vector3 TargetPosition = new(20, 0, 0);
        internal bool TargetPresent = true, NativeSearching;
        internal List<Vector3> Waypoints = [];
        internal readonly List<(Vector3 Position, bool Fly, float Range)> Requests = [];
        internal Func<bool, bool> AcceptMove = _ => true;
        internal Func<Vector3, Vector3?> Reachable = p => p;
        internal int Stops;
        internal IGameObject Target = null!;
        internal string Phase => Get("_phase")!.ToString()!;

        internal Harness(string phase = "Idle")
        {
            var player = Proxy.Of<IPlayerCharacter>((m, _) => m.Name switch
            { "get_Position" => Vector3.Zero, "get_GameObjectId" => 1UL, "get_Address" => (nint)0,
                _ => throw new NotSupportedException(m.Name) });
            var target = Proxy.Of<IGameObject>((m, _) => m.Name switch
            { "get_Position" => TargetPosition, "get_GameObjectId" => 44UL,
                "get_ObjectKind" => Dalamud.Game.ClientState.Objects.Enums.ObjectKind.EventNpc, "get_BaseId" => 100u,
                _ => throw new NotSupportedException(m.Name) });
            Target = target;
            Set("_objectTable", Proxy.Of<IObjectTable>((m, _) => m.Name switch
            { "get_LocalPlayer" => player,
                "GetEnumerator" => (TargetPresent ? new List<IGameObject> { target } : []).GetEnumerator(),
                _ => throw new NotSupportedException(m.Name) }));
            Set("_clientState", Proxy.Of<IClientState>((m, _) => m.Name == "get_TerritoryType"
                ? Territory : throw new NotSupportedException(m.Name)));
            var log = Proxy.Of<IPluginLog>((_, _) => null);
            Set("_log", log);
            Set("_tolk", RuntimeHelpers.GetUninitializedObject(typeof(TolkService)));
            Set("_nav", _nav);
            Set("_config", new Configuration());
            Gate("_log", log);
            Gate("_stop", Proxy.Of<ICallGateSubscriber<object>>((_, _) => { ++Stops; Waypoints = []; return null; }));
            Gate("_isReady", Proxy.Of<ICallGateSubscriber<bool>>((_, _) => true));
            Gate("_isRunning", Proxy.Of<ICallGateSubscriber<bool>>((_, _) => Waypoints.Count > 0));
            Gate("_pathfindInProgress", Proxy.Of<ICallGateSubscriber<bool>>((_, _) => NativeSearching));
            Gate("_numWaypoints", Proxy.Of<ICallGateSubscriber<int>>((_, _) => Waypoints.Count));
            Gate("_listWaypoints", Proxy.Of<ICallGateSubscriber<List<Vector3>>>((_, _) => new List<Vector3>(Waypoints)));
            Gate("_nearestPoint", Proxy.Of<ICallGateSubscriber<Vector3, float, float, Vector3?>>((_, a) => (Vector3)a![0]!));
            Gate("_nearestPointReachable", Proxy.Of<ICallGateSubscriber<Vector3, float, float, Vector3?>>((_, a) => Reachable((Vector3)a![0]!)));
            Gate("_moveCloseTo", Proxy.Of<ICallGateSubscriber<Vector3, bool, float, bool>>((_, a) =>
            {
                var request = ((Vector3)a![0]!, (bool)a[1]!, (float)a[2]!);
                Requests.Add(request);
                var accepted = AcceptMove(request.Item2);
                if (accepted) Waypoints = [request.Item1];
                return accepted;
            }));
            Set("_startTerritory", (ushort)133);
            Set("_destPosition", TargetPosition);
            Set("_targetName", "Цель");
            Set("_stopRange", 2.5f);
            Set("_usedTrails", new HashSet<string>());
            Set("_phase", Enum.Parse(typeof(AutoWalkService).GetNestedType("Phase", BindingFlags.NonPublic)!, phase));
        }

        internal void Follow()
        {
            Set("_following", true); Set("_followTargetId", 44UL);
            Set("_followStartTerritory", (ushort)133); Set("_followName", "Цель");
        }
        internal Plugin Plugin()
        {
            var plugin = (Plugin)RuntimeHelpers.GetUninitializedObject(typeof(Plugin));
            SetField(plugin, "_autoWalk", Walk);
            SetField(plugin, "_navigation", RuntimeHelpers.GetUninitializedObject(typeof(NavigationService)));
            typeof(Plugin).GetProperty("ClientState", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(plugin,
                Proxy.Of<IClientState>((m, _) => m.Name switch
                { "get_MapId" => 2u, "get_TerritoryType" => Territory, _ => throw new NotSupportedException(m.Name) }));
            typeof(Plugin).GetProperty("ObjectTable", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(plugin, Get("_objectTable"));
            return plugin;
        }
        internal object? Invoke(string method, object?[]? args = null) => typeof(AutoWalkService)
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Walk, args);
        internal void Set(string name, object value) => Field(Walk, name).SetValue(Walk, value);
        internal object? Get(string name) => Field(Walk, name).GetValue(Walk);
        private void Gate(string name, object value) => Field(_nav, name).SetValue(_nav, value);
        private static FieldInfo Field(object value, string name) => value.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    }

    public class Proxy : DispatchProxy
    {
        private Func<MethodInfo, object?[]?, object?> _call = null!;
        internal static T Of<T>(Func<MethodInfo, object?[]?, object?> call) where T : class
        {
            var value = Create<T, Proxy>();
            ((Proxy)(object)value)._call = call;
            return value;
        }
        protected override object? Invoke(MethodInfo? method, object?[]? args) => _call(method!, args);
    }
}
