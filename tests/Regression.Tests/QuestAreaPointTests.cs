using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using FF14Accessibility;
using FF14Accessibility.Services;

namespace Regression.Tests;

public sealed class QuestAreaPointTests
{
    private static QuestDestination Area => new("Дилемма изящного вкуса", "", new(205.9f, 32.5f, 40.7f),
        35, 135, 16, true, QuestKind.Job, 5, TargetLevelType: 51, ObjectiveLevelId: 5058426);
    private static Vector3 LoggedFloor => new(205.8f, 48.1f, 40.7f);

    [Fact]
    public void RecordedSearchCircleAcceptsActualGroundAboveTheMarker()
    {
        var plan = QuestNavigationPlan.Resolve(Area, 135, 16, _ => null, _ => null)!;
        Assert.True(plan.HeightIsGuess);
        Assert.Equal(LoggedFloor, QuestAreaPoint.Resolve(Area.Position, Area.Radius, 5,
            (_, _, _) => LoggedFloor, _ => true));
    }

    [Theory]
    [InlineData(8, 1000001, QuestMarkerRole.Quest)]
    [InlineData(45, 2000001, QuestMarkerRole.Quest)]
    [InlineData(49, 0, QuestMarkerRole.QuestTrigger)]
    [InlineData(51, 0, QuestMarkerRole.QuestTrigger)]
    [InlineData(0, 0, QuestMarkerRole.Quest)]
    public void ActorsTriggersAndUnknownMarkersDoNotReceiveAreaHeightRules(byte type, uint id, QuestMarkerRole role)
    {
        var exact = Area with { TargetLevelType = type, TargetBaseId = id, Role = role };
        Assert.False(QuestAreaPoint.IsSearchArea(exact));
        Assert.False(QuestNavigationPlan.Resolve(exact, 135, 16, _ => null, _ => null)!.HeightIsGuess);
    }

    [Fact]
    public void AreaInAnotherZoneUsesTheLocalEntranceInsteadOfItsCircle()
    {
        var entrance = new InteriorEntrance(15, 16, 1000001, 8, new(10, 20, 30));
        var plan = QuestNavigationPlan.Resolve(Area, 134, 15,
            _ => new("Вход", "Entrance", entrance.Position, false, 16), _ => entrance)!;
        Assert.False(plan.HeightIsGuess);
        Assert.Equal(entrance.Position, plan.Position);
        Assert.Equal(16u, plan.NextMapId);
    }

    [Fact]
    public void MissingCentreFindsGroundInsideTheOuterPartOfTheCircle()
    {
        var calls = 0;
        var point = QuestAreaPoint.Resolve(Area.Position, 35, 5, (p, _, _) =>
        { calls++; return p.X > Area.Position.X + 22 ? p with { Y = 48 } : null; }, _ => true);
        Assert.NotNull(point);
        Assert.True(point.Value.X > Area.Position.X + 22);
        Assert.True(Vector2.Distance(new(point.Value.X, point.Value.Z), new(Area.Position.X, Area.Position.Z)) + 5 < 35);
        Assert.InRange(calls, 2, 130);
    }

    [Theory]
    [InlineData(240.9f, 48, 40.7f)] // Rim: stopping short could leave the player outside.
    [InlineData(241, 48, 40.7f)] // Outside circle.
    [InlineData(205.9f, 133, 40.7f)] // Outside bounded height search.
    [InlineData(float.NaN, 48, 40)]
    [InlineData(205, float.PositiveInfinity, 40)]
    public void InvalidOrOutsideReturnedPointsAreRejected(float x, float y, float z)
        => Assert.Null(QuestAreaPoint.Resolve(Area.Position, 35, 5, (_, _, _) => new(x, y, z), _ => true));

    [Fact]
    public void WrongKnownFloorIsRejectedEvenInsideTheCircle()
        => Assert.Null(QuestAreaPoint.Resolve(Area.Position, 35, 5, (_, _, _) => LoggedFloor, _ => false));

    [Fact]
    public void NoFilteredMeshNeverFallsBackToTheRawCentre()
    {
        var calls = 0;
        Assert.Null(QuestAreaPoint.Resolve(Area.Position, 35, 5, (_, _, _) => { calls++; return null; }, _ => true));
        Assert.Equal(130, calls);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(float.NaN, 1)]
    [InlineData(float.PositiveInfinity, 1)]
    [InlineData(35, float.NaN)]
    [InlineData(35, -1)]
    public void InvalidCircleOrStoppingDistanceDoesNotQueryMesh(float radius, float stop)
        => Assert.Null(QuestAreaPoint.Resolve(Area.Position, radius, stop,
            (_, _, _) => throw new Exception("Invalid query"), _ => true));

    [Theory]
    [InlineData(1f)]
    [InlineData(5f)]
    public void SharedMovementResolverKeepsTheValidatedAreaPoint(float configuredStop)
    {
        var plugin = Create((_, _, _) => LoggedFloor);
        var navigation = (NavigationService)RuntimeHelpers.GetUninitializedObject(typeof(NavigationService));
        var selected = Area;
        typeof(NavigationService).GetProperty(nameof(NavigationService.SelectedQuestDestination))!.SetValue(navigation, selected);
        Field(plugin, "_navigation", navigation);
        Field(plugin, "_config", new Configuration { AutoWalkPlaceStopRange = configuredStop });
        var client = Proxy.Of<IClientState>((m, _) => m.Name == "get_MapId" ? 16u : 135u);
        typeof(Plugin).GetProperty("ClientState", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(plugin, client);
        object?[] args = [default(Vector3), "", 0f, false, false, false];
        Assert.Equal("Resolved", Method("TryResolveSelectedDestination").Invoke(plugin, args)!.ToString());
        Assert.Equal(LoggedFloor, args[0]);
        Assert.True((bool)args[3]!);
        Assert.False((bool)args[4]!);
        Assert.InRange((float)args[2]!, 0, Area.Radius / 4);
        Assert.Same(selected, navigation.SelectedQuestDestination);
    }

    [Fact]
    public void ReadingAreaDoesNotQueryMeshOrClaimMarkerHeightIsGround()
    {
        var plugin = Create((_, _, _) => throw new Exception("Readout must not query geometry"), ready: false);
        var navigation = (NavigationService)RuntimeHelpers.GetUninitializedObject(typeof(NavigationService));
        typeof(NavigationService).GetProperty(nameof(NavigationService.SelectedQuestDestination))!.SetValue(navigation, Area);
        Field(plugin, "_navigation", navigation); Field(plugin, "_config", new Configuration());
        typeof(Plugin).GetProperty("ClientState", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(plugin,
            Proxy.Of<IClientState>((m, _) => m.Name == "get_MapId" ? 16u : 135u));
        object?[] args = [default(Vector3), "", 0f, false, false, true];
        Assert.Equal("Resolved", Method("TryResolveSelectedDestination").Invoke(plugin, args)!.ToString());
        Assert.Equal(Area.Position, args[0]);
        Assert.True((bool)args[3]!);
    }

    [Fact]
    public void TinyCircleReducesStopRangeToKeepThePlayerInside()
    {
        var goal = Area with { Radius = 1 };
        var plugin = Create((p, _, _) => p);
        var plan = QuestNavigationPlan.Resolve(goal, 135, 16, _ => null, _ => null)!;
        object?[] args = [goal, plan, goal.Position, 5f];
        Assert.Equal(goal.Position, Method("ResolveQuestGroundPoint").Invoke(plugin, args));
        Assert.Equal(0.25f, args[3]);
    }

    [Fact]
    public void ExactNpcStillRejectsTheLoggedWrongHeight()
    {
        var goal = Area with { TargetLevelType = 8, TargetBaseId = 1000001 };
        var plugin = Create((_, _, _) => LoggedFloor);
        var plan = QuestNavigationPlan.Resolve(goal, 135, 16, _ => null, _ => null)!;
        object?[] args = [goal, plan, goal.Position, 1f];
        Assert.Null(Method("ResolveQuestGroundPoint").Invoke(plugin, args));
    }

    [Theory]
    [InlineData(true, false, 0.2f)]
    [InlineData(false, false, 0.2f)]
    [InlineData(false, false, -1f)]
    [InlineData(false, true, -1f)]
    public void MeshReadinessDistinguishesLoadingMissingAndReady(bool ready, bool missing, float progress)
    {
        var plugin = Create((_, _, _) => throw new Exception("No geometry needed"), ready, missing, progress);
        var message = Method("QuestMeshUnavailableMessage").Invoke(plugin, null);
        Assert.Equal(ready ? null : missing ? AccessibilityStrings.AutoWalkUnavailable
            : progress >= 0 ? AccessibilityStrings.MeshStillLoading(progress * 100) : AccessibilityStrings.MeshNotReady, message);
    }

    private static Plugin Create(Func<Vector3, float, float, Vector3?> query,
        bool ready = true, bool missing = false, float progress = -1)
    {
        var plugin = (Plugin)RuntimeHelpers.GetUninitializedObject(typeof(Plugin));
        var nav = (NavmeshIpc)RuntimeHelpers.GetUninitializedObject(typeof(NavmeshIpc));
        var walk = (AutoWalkService)RuntimeHelpers.GetUninitializedObject(typeof(AutoWalkService));
        var log = Proxy.Of<IPluginLog>((_, _) => null);
        Field(nav, "_log", log);
        Field(nav, "_isReady", Proxy.Of<ICallGateSubscriber<bool>>((_, _) => ready));
        Field(nav, "_buildProgress", Proxy.Of<ICallGateSubscriber<float>>((_, _) => missing ? throw new Exception("Missing") : progress));
        var gate = Proxy.Of<ICallGateSubscriber<Vector3, float, float, Vector3?>>((_, a) =>
            query((Vector3)a![0]!, (float)a[1]!, (float)a[2]!));
        Field(nav, "_nearestPointReachable", gate);
        Field(walk, "_nav", nav); Field(walk, "_log", log);
        Field(plugin, "_autoWalk", walk);
        Field(plugin, "_places", RuntimeHelpers.GetUninitializedObject(typeof(PlacesService)));
        typeof(Plugin).GetProperty("Log", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(plugin, log);
        return plugin;
    }

    private static MethodInfo Method(string name) => typeof(Plugin).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static void Field(object instance, string name, object value)
        => instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(instance, value);

    public class Proxy : DispatchProxy
    {
        private Func<MethodInfo, object?[]?, object?> _call = null!;
        internal static T Of<T>(Func<MethodInfo, object?[]?, object?> call) where T : class
        { var result = Create<T, Proxy>(); ((Proxy)(object)result)._call = call; return result; }
        protected override object? Invoke(MethodInfo? method, object?[]? args) => _call(method!, args);
    }
}
