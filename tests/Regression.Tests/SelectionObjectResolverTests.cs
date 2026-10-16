using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Collections;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using FF14Accessibility;
using FF14Accessibility.Services;

namespace Regression.Tests;

public sealed class SelectionObjectResolverTests
{
    [Theory]
    [InlineData("Ätheryt")]
    [InlineData("Aethernet")]
    public void LoadedMapCrystalUsesTheSameActualPositionForReadoutAndBothMovementModes(string type)
    {
        var crystal = Object(11, new(-144.2f, -1.8f, -168.5f), 9, ObjectKind.Aetheryte);
        var (plugin, nav) = PluginWithObjects([crystal]);
        typeof(NavigationService).GetProperty("SelectedPlaceDestination")!.SetValue(nav,
            new PlaceDestination("Ульда: Ступени Нальда", type, new(-144, 0, -168), false, 0));
        Field(nav, "_objectTable", typeof(Plugin).GetProperty("ObjectTable", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(plugin)!);
        Field(nav, "_clientState", QuestAreaPointTests.Proxy.Of<IClientState>((m, _) => m.Name switch
        { "get_MapId" => 13u, _ => throw new NotSupportedException(m.Name) }));
        foreach (var readout in new[] { false, true })
        {
            var (result, point) = Resolve(plugin, readout);
            Assert.Equal("Resolved", result);
            Assert.Equal(crystal.Position, point);
            Assert.Same(crystal, nav.GetSelectedNavigationObject());
        }
    }

    [Fact]
    public void AetheryteMarkersDoNotGuessBetweenFloorsOrReplaceARetainedInstance()
    {
        var marker = new Vector3(-144, 0, -168);
        var lower = Object(11, marker with { Y = -2 }, 9, ObjectKind.Aetheryte);
        var upper = Object(12, marker with { Y = 19 }, 10, ObjectKind.Aetheryte);
        Assert.Null(SelectionObjectResolver.AetheryteMarker([lower, upper], marker));
        Assert.Same(lower, SelectionObjectResolver.AetheryteMarker([lower, upper], marker, 11));
        Assert.Null(SelectionObjectResolver.AetheryteMarker([upper], marker, 11));
        Assert.Null(SelectionObjectResolver.AetheryteMarker([Object(13, marker)], marker));
        Assert.Null(SelectionObjectResolver.AetheryteMarker([lower], marker + new Vector3(30, 0, 0)));
    }

    [Fact]
    public void IntermediateTransferSelectsItsExactActorAndExpiresWhenTheSelectionChanges()
    {
        var actor = Object(11, new(10, 40, 20));
        var (plugin, navigation) = PluginWithObjects([actor, Object(12, new(30, 40, 20))]);
        var selected = new ObjectDestination(12, "goal", new(30, 40, 20), ObjectKind.EventNpc, 100);
        typeof(NavigationService).GetProperty("SelectedObjectDestination")!.SetValue(navigation, selected);
        Field(navigation, "_places", RuntimeHelpers.GetUninitializedObject(typeof(PlacesService)));
        Field(navigation, "_objectTable", typeof(Plugin).GetProperty("ObjectTable", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(plugin)!);
        Field(navigation, "_clientState", QuestAreaPointTests.Proxy.Of<IClientState>((m, _) => m.Name switch
        { "get_MapId" => 11u, "get_TerritoryType" => 128u, _ => throw new NotSupportedException(m.Name) }));
        navigation.SetLocalTransfer(new(11, 100, 8, actor.Position, new(40, 40, 20), 131128));
        Assert.Same(actor, navigation.GetSelectedNavigationObject());
        typeof(NavigationService).GetProperty("SelectedObjectDestination")!.SetValue(navigation, null);
        Assert.Null(navigation.GetSelectedNavigationObject());
    }

    [Fact]
    public void MissingIntermediateTransferActorDoesNotFallBackToTheFinalObject()
    {
        var (plugin, navigation) = PluginWithObjects([Object(12, new(30, 40, 20))]);
        var selected = new ObjectDestination(12, "goal", new(30, 40, 20), ObjectKind.EventNpc, 100);
        typeof(NavigationService).GetProperty("SelectedObjectDestination")!.SetValue(navigation, selected);
        Field(navigation, "_places", RuntimeHelpers.GetUninitializedObject(typeof(PlacesService)));
        Field(navigation, "_objectTable", typeof(Plugin).GetProperty("ObjectTable", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(plugin)!);
        Field(navigation, "_clientState", QuestAreaPointTests.Proxy.Of<IClientState>((m, _) => m.Name switch
        { "get_MapId" => 11u, _ => throw new NotSupportedException(m.Name) }));
        navigation.SetLocalTransfer(new(11, 100, 8, new(10, 40, 20), new(40, 40, 20), 131128));
        Assert.Null(navigation.GetSelectedNavigationObject());
    }
    [Theory]
    [InlineData(1, 2)]
    [InlineData(-1, 0)]
    public void ReorderedLiveBrowserContinuesFromSelectedObjectRatherThanTheOldIndex(int direction, int expected)
        => Assert.Equal(expected, SelectionObjectResolver.NextIndex(
            [Object(12, Vector3.Zero), Object(11, Vector3.Zero), Object(13, Vector3.Zero)], 11, 0, direction));

    [Fact]
    public void DisappearingBrowserEntryRestartsSafelyInBothDirections()
    {
        IGameObject[] objects = [Object(12, Vector3.Zero), Object(13, Vector3.Zero)];
        Assert.Equal(0, SelectionObjectResolver.NextIndex(objects, 11, 1, 1));
        Assert.Equal(1, SelectionObjectResolver.NextIndex(objects, 11, 1, -1));
        Assert.Equal(-1, SelectionObjectResolver.NextIndex([], 11, 0, 1));
    }

    private static IGameObject Object(ulong id, Vector3 position, uint baseId = 100,
        ObjectKind kind = ObjectKind.EventNpc, bool targetable = true)
        => QuestAreaPointTests.Proxy.Of<IGameObject>((m, _) => m.Name switch
        {
            "get_GameObjectId" => id, "get_BaseId" => baseId, "get_ObjectKind" => kind,
            "get_Position" => position, "get_IsTargetable" => targetable,
            _ => throw new NotSupportedException(m.Name)
        });

    [Fact]
    public void BrowserIdentityUsesLivePositionAndDoesNotSubstituteSameNameOrBase()
    {
        var selected = new ObjectDestination(11, "NPC", Vector3.Zero, ObjectKind.EventNpc, 100);
        var moved = Object(11, new(30, 4, 20));
        Assert.Same(moved, SelectionObjectResolver.Exact([Object(12, Vector3.Zero), moved], selected));
        Assert.Null(SelectionObjectResolver.Exact([Object(12, Vector3.Zero)], selected));
    }

    [Theory]
    [InlineData(101u, ObjectKind.EventNpc)]
    [InlineData(100u, ObjectKind.EventObj)]
    public void ReusedObjectIdWithDifferentBaseOrKindIsRejected(uint baseId, ObjectKind kind)
        => Assert.Null(SelectionObjectResolver.Exact([Object(11, Vector3.Zero, baseId, kind)],
            new(11, "NPC", Vector3.Zero, ObjectKind.EventNpc, 100)));

    [Theory]
    [InlineData(8, ObjectKind.EventNpc)]
    [InlineData(9, ObjectKind.BattleNpc)]
    [InlineData(45, ObjectKind.EventObj)]
    public void QuestRequiresTheExactSheetKindAndFloor(byte type, ObjectKind kind)
    {
        var right = Object(11, new(2, 1, 1), kind: kind);
        Assert.Same(right, SelectionObjectResolver.Linked([Object(12, new(0, 8, 0), kind: kind),
            Object(13, Vector3.Zero, 101, kind), right], 100, type, Vector3.Zero));
        Assert.Null(SelectionObjectResolver.Linked([right], 100, 51, Vector3.Zero));
    }

    [Fact]
    public void UsableInstanceWinsEvenIfAnUnavailableInstanceIsCloser()
    {
        var available = Object(12, new(8, 0, 0));
        Assert.Same(available, SelectionObjectResolver.Linked([Object(11, new(1, 0, 0), targetable: false), available],
            100, 8, Vector3.Zero));
    }

    [Fact]
    public void EquallyPlausibleInstancesAreNotDecidedByEnumerationOrder()
    {
        var first = Object(11, new(2, 0, 0)); var second = Object(12, new(-2, 0, 0));
        Assert.Null(SelectionObjectResolver.Linked([first, second], 100, 8, Vector3.Zero));
        Assert.Null(SelectionObjectResolver.Linked([second, first], 100, 8, Vector3.Zero));
    }

    [Fact]
    public void RetainedQuestActorCanMoveBeyondMarkerWithoutSwitchingToAnotherInstance()
    {
        var moved = Object(11, new(50, 7, 30));
        Assert.Same(moved, SelectionObjectResolver.Linked([Object(12, Vector3.Zero), moved], 100, 8, Vector3.Zero, 11));
        Assert.Null(SelectionObjectResolver.Linked([Object(12, Vector3.Zero)], 100, 8, Vector3.Zero, 11));
    }

    [Theory]
    [InlineData(4, 0, 0)]
    [InlineData(0, 3, 0)]
    [InlineData(float.NaN, 0, 0)]
    public void ActorApproachCannotMoveToAnotherFloorOrFarFromTheActor(float x, float y, float z)
        => Assert.Null(SelectionObjectResolver.Approach(Vector3.Zero, (_, _, _) => new(x, y, z)));

    [Fact]
    public void TightApproachCanUseTheSecondProbeWithoutAUnfilteredFallback()
    {
        var calls = 0;
        Assert.Equal(new Vector3(2, 1, 0), SelectionObjectResolver.Approach(Vector3.Zero,
            (_, xz, _) => { calls++; return xz > 1 ? new(2, 1, 0) : null; }));
        Assert.Equal(2, calls);
        Assert.Null(SelectionObjectResolver.Approach(Vector3.Zero, (_, _, _) => null));
    }

    [Fact]
    public void CallOfTheSeaAllowsTheRecordedApproachBesideTheCounter()
    {
        var marker = new Vector3(20.2978f, 40.1999f, -6.1024f);
        var floor = new Vector3(17.75f, 40.25f, -4.75f);
        Assert.InRange(Vector3.Distance(marker, floor), 2.8f, 3f);
        Assert.Equal(floor, SelectionObjectResolver.Approach(marker,
            (_, xz, y) => xz >= 2.5f && y == 2f ? floor : null));
    }

    [Theory]
    [InlineData(3.01f, 0f)]
    [InlineData(2.6f, 2.01f)]
    [InlineData(2.6f, float.NaN)]
    public void CounterApproachStillRejectsDistantOrDifferentFloorPoints(float distance, float height)
        => Assert.Null(SelectionObjectResolver.Approach(Vector3.Zero, (_, _, _) => new(distance, height, 0)));

    [Fact]
    public void RejectedFirstApproachDoesNotHideAValidSecondApproach()
    {
        var floor = new Vector3(2.9f, 0.05f, 0);
        Assert.Equal(floor, SelectionObjectResolver.Approach(Vector3.Zero,
            (_, xz, _) => xz == 1f ? new(0, 8, 0) : floor));
    }

    [Fact]
    public void SharedMovementResolverDoesNotProjectTwoDifferentNpcsToTheSameDistantPoint()
    {
        var (plugin, nav) = PluginWithObjects([Object(11, new(1, 40, 2)), Object(12, new(30, 40, 20))]);
        foreach (var (id, expected) in new[] {(11UL, new Vector3(1, 40, 2)), (12UL, new Vector3(30, 40, 20))})
        {
            typeof(NavigationService).GetProperty("SelectedObjectDestination")!.SetValue(nav,
                new ObjectDestination(id, "NPC", Vector3.Zero, ObjectKind.EventNpc, 100));
            var (result, point) = Resolve(plugin, false);
            Assert.Equal("Resolved", result); Assert.Equal(expected, point);
        }
    }

    [Fact]
    public void ReadoutWorksWithoutMeshAndReadsExactLiveHeight()
    {
        var (plugin, nav) = PluginWithObjects([Object(11, new(30, 91.5f, 20))]);
        typeof(NavigationService).GetProperty("SelectedObjectDestination")!.SetValue(nav,
            new ObjectDestination(11, "NPC", Vector3.Zero, ObjectKind.EventNpc, 100));
        Assert.Equal(new Vector3(30, 91.5f, 20), Resolve(plugin, true).Point);
    }

    [Fact]
    public void ActiveBorderReadoutUsesTheResolvedWalkingPointRatherThanTheMapGlyph()
    {
        var (plugin, nav) = PluginWithObjects([]);
        var place = new PlaceDestination("Transition", "Übergang", new(300, 0, 20), true, 12);
        typeof(NavigationService).GetProperty("SelectedPlaceDestination")!.SetValue(nav, place);
        Field(nav, "_walkGuideActive", true);
        Field(plugin, "_walkingPlaceSelection", place);
        Field(plugin, "_walkingObjectMap", 11u); Field(plugin, "_walkingObjectTerritory", 128u);
        Field(plugin, "_walkingPoint", (new Vector3(-97, 37, 103), "Transition", 0.5f, true));
        var client = QuestAreaPointTests.Proxy.Of<IClientState>((m, _) => m.Name switch
        { "get_MapId" => 11u, "get_TerritoryType" => 128u, _ => throw new NotSupportedException(m.Name) });
        typeof(Plugin).GetProperty("ClientState", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(plugin, client);
        Assert.Equal(new Vector3(-97, 37, 103), Resolve(plugin, true).Point);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ObjectTrackingDoesNotIssueAnotherMovementRequestWhileInitialSearchIsPending(bool pathRunning)
    {
        var (plugin, navigation) = PluginWithObjects([Object(11, new(10, 40, 20))]);
        var selected = new ObjectDestination(11, "NPC", Vector3.Zero, ObjectKind.EventNpc, 100);
        typeof(NavigationService).GetProperty("SelectedObjectDestination")!.SetValue(navigation, selected);
        Field(plugin, "_walkingBrowserSelection", selected); Field(plugin, "_walkingObject", selected);
        Field(plugin, "_walkingObjectMap", 11u); Field(plugin, "_walkingObjectTerritory", 128u);
        Field(plugin, "_needsObjectTracking", true);
        var client = QuestAreaPointTests.Proxy.Of<IClientState>((m, _) => m.Name switch
        { "get_MapId" => 11u, "get_TerritoryType" => 128u, _ => throw new NotSupportedException(m.Name) });
        typeof(Plugin).GetProperty("ClientState", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(plugin, client);
        var walk = typeof(Plugin).GetField("_autoWalk", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(plugin)!;
        var phase = walk.GetType().GetField("_phase", BindingFlags.Instance | BindingFlags.NonPublic)!;
        phase.SetValue(walk, Enum.Parse(phase.FieldType, "Starting"));
        var mesh = walk.GetType().GetField("_nav", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(walk)!;
        Field(mesh, "_isRunning", QuestAreaPointTests.Proxy.Of<ICallGateSubscriber<bool>>((_, _) => pathRunning));
        Field(mesh, "_pathfindInProgress", QuestAreaPointTests.Proxy.Of<ICallGateSubscriber<bool>>((_, _) => true));
        // Any actual retarget would call unset movement dependencies and fail.
        typeof(Plugin).GetMethod("PollWalkingObject", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(plugin, null);
        Assert.True((bool)typeof(Plugin).GetField("_needsObjectTracking", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(plugin)!);
    }

    [Fact]
    public void ObjectReadoutAndMovementKeepTheSelectedNpcRatherThanASuggestedAttendant()
    {
        var attendant = Object(22, new(10, 40, 20));
        var (plugin, nav) = PluginWithObjects([Object(11, new(30, 40, 20)), attendant]);
        var goal = new ObjectDestination(11, "Final NPC", new(30, 40, 20), ObjectKind.EventNpc, 100);
        typeof(NavigationService).GetProperty("SelectedObjectDestination")!.SetValue(nav, goal);
        Field(plugin, "_walkingBrowserSelection", goal);
        Field(plugin, "_walkingObjectMap", 11u); Field(plugin, "_walkingObjectTerritory", 128u);
        Field(plugin, "_walkingPoint", (new Vector3(10, 40, 20), "Attendant", 2.5f, false));
        Field(plugin, "_walkingObject", new ObjectDestination(22, "Attendant", new(10, 40, 20), ObjectKind.EventNpc, 100));
        var client = QuestAreaPointTests.Proxy.Of<IClientState>((m, _) => m.Name switch
        { "get_MapId" => 11u, "get_TerritoryType" => 128u, _ => throw new NotSupportedException(m.Name) });
        typeof(Plugin).GetProperty("ClientState", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(plugin, client);
        foreach (var readout in new[] { true, false })
        {
            object?[] args = [Vector3.Zero, "", 0f, false, false, readout];
            typeof(Plugin).GetMethod("TryResolveSelectedDestination", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(plugin, args);
            Assert.Equal(goal.Position, (Vector3)args[0]!); Assert.Equal("Final NPC", args[1]);
        }
    }

    internal static (Plugin Plugin, NavigationService Navigation) PluginWithObjects(IGameObject[] objects)
    {
        var plugin = (Plugin)RuntimeHelpers.GetUninitializedObject(typeof(Plugin));
        var navigation = (NavigationService)RuntimeHelpers.GetUninitializedObject(typeof(NavigationService));
        var mesh = (NavmeshIpc)RuntimeHelpers.GetUninitializedObject(typeof(NavmeshIpc));
        var walk = (AutoWalkService)RuntimeHelpers.GetUninitializedObject(typeof(AutoWalkService));
        var log = QuestAreaPointTests.Proxy.Of<IPluginLog>((_, _) => null);
        Field(mesh, "_log", log);
        Field(mesh, "_isReady", QuestAreaPointTests.Proxy.Of<ICallGateSubscriber<bool>>((_, _) => true));
        Field(mesh, "_nearestPointReachable", QuestAreaPointTests.Proxy.Of<ICallGateSubscriber<Vector3,float,float,Vector3?>>((_, a) => (Vector3)a![0]!));
        Field(walk, "_nav", mesh); Field(walk, "_log", log);
        Field(plugin, "_navigation", navigation); Field(plugin, "_autoWalk", walk);
        Field(plugin, "_config", new Configuration());
        var player = QuestAreaPointTests.Proxy.Of<IPlayerCharacter>((m, _) => m.Name == "get_Position"
            ? Vector3.Zero : throw new NotSupportedException(m.Name));
        var table = QuestAreaPointTests.Proxy.Of<IObjectTable>((m, _) => m.Name switch
        { "GetEnumerator" => ((IEnumerable<IGameObject>)objects).GetEnumerator(),
            "get_LocalPlayer" => player, _ => throw new NotSupportedException(m.Name) });
        typeof(Plugin).GetProperty("ObjectTable", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(plugin, table);
        return (plugin, navigation);
    }

    private static (string Result, Vector3 Point) Resolve(Plugin plugin, bool readout)
    {
        object?[] args = [Vector3.Zero, "", 0f, false, false, readout];
        var result = typeof(Plugin).GetMethod("TryResolveSelectedDestination", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(plugin, args);
        return (result!.ToString()!, (Vector3)args[0]!);
    }
    private static void Field(object instance, string field, object value)
        => instance.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(instance, value);
}
