using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Dalamud.Plugin.Services;
using Dalamud.Game.ClientState.Objects.Types;
using FF14Accessibility;
using FF14Accessibility.Services;

namespace Navigation.Tests;

// Exercise the production resolver without running the plugin constructors:
// those subscribe to the game and initialise audio/native services. Dependencies
// irrelevant to a readout stay null, so accidental navigation calls fail the test.
public class DestinationReadoutTests
{
    [Fact]
    public void SelectedQuestCanBeReadWithoutGameTargetOrNavmesh()
    {
        var (plugin, navigation) = Create();
        var destination = new QuestDestination("Вдали от дома", "", new(-32.6f, -1, -148.5f),
            1, 141, 21, true, default, 9);
        Set(navigation, nameof(NavigationService.SelectedQuestDestination), destination);

        var first = Read(plugin);
        Assert.Equal("Resolved", first.Result);
        Assert.Equal(destination.QuestName, first.Name);
        Assert.Equal(destination.Position, first.Position);
        Assert.True(first.HeightIsGuess);
        Assert.Equal(first, Read(plugin));
        Assert.Same(destination, navigation.SelectedQuestDestination);
        Assert.False(navigation.IsWalkGuideActive);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void MapLocationsDoNotRequireWalkableFloor(bool transition, bool water)
    {
        var (plugin, navigation) = Create();
        var destination = new PlaceDestination("Выбранное место", "", new(12, 0, 34), transition, 22, water);
        Set(navigation, nameof(NavigationService.SelectedPlaceDestination), destination);

        var readout = Read(plugin);
        Assert.Equal("Resolved", readout.Result);
        Assert.Equal(destination.Name, readout.Name);
        Assert.Equal(destination.Position, readout.Position);
        Assert.True(readout.HeightIsGuess);
    }

    [Fact]
    public void DungeonStepPreservesKnownHeight()
    {
        var (plugin, navigation) = Create();
        var step = new DungeonStep(1, DungeonStepKind.Waypoint, new(10, 25, 30), "Лестница");
        Set(navigation, nameof(NavigationService.SelectedDungeonStep), step);
        var readout = Read(plugin);
        Assert.Equal("Resolved", readout.Result);
        Assert.Equal(step.Name, readout.Name);
        Assert.Equal(step.Position, readout.Position);
        Assert.False(readout.HeightIsGuess);
    }

    [Fact]
    public void ChangingSelectionChangesTheNextReadout()
    {
        var (plugin, navigation) = Create();
        Set(navigation, nameof(NavigationService.SelectedPlaceDestination),
            new PlaceDestination("Первое", "", new(1, 0, 2), false, 0));
        Assert.Equal("Первое", Read(plugin).Name);
        Set(navigation, nameof(NavigationService.SelectedPlaceDestination),
            new PlaceDestination("Второе", "", new(3, 0, 4), false, 0));
        var readout = Read(plugin);
        Assert.Equal("Второе", readout.Name);
        Assert.Equal(new Vector3(3, 0, 4), readout.Position);
    }

    [Fact]
    public void EmptySelectionAllowsGameTargetFallback()
    {
        var (plugin, _) = Create();
        Assert.Equal("None", Read(plugin).Result);
    }

    [Fact]
    public void BrowserObjectCanBeReadWhenGameRefusesTargetAndPositionIsRefreshed()
    {
        var (plugin, navigation) = Create();
        var position = new Vector3(10, 20, 30);
        var live = StrictProxy.Of<IGameObject>(m => m.Name switch
        {
            "get_GameObjectId" => 123UL,
            "get_Position" => position,
            _ => throw new InvalidOperationException(m.Name)
        });
        Set(plugin, "ObjectTable", StrictProxy.Of<IObjectTable>(m => m.Name == "GetEnumerator"
            ? ((IEnumerable<IGameObject>)new[] { live }).GetEnumerator()
            : throw new InvalidOperationException(m.Name)));
        Set(plugin, "TargetManager", StrictProxy.Of<ITargetManager>(m => m.Name == "get_Target"
            ? null : throw new InvalidOperationException(m.Name)));
        Set(navigation, nameof(NavigationService.SelectedObjectDestination),
            new ObjectDestination(123, "Выбранный NPC", Vector3.Zero));
        Assert.Equal(position, Read(plugin).Position);
        position = new(40, 50, 60);
        var readout = Read(plugin);
        Assert.Equal("Resolved", readout.Result);
        Assert.Equal("Выбранный NPC", readout.Name);
        Assert.Equal(position, readout.Position);

    }

    [Fact]
    public void ReadingHuntAreaDoesNotAdvanceTheSearchAtAnAlreadyReachedPoint()
    {
        var (_, navigation) = Create();
        var searchType = typeof(NavigationService).GetNestedType("HuntSearch", BindingFlags.NonPublic)!;
        var search = RuntimeHelpers.GetUninitializedObject(searchType);
        Set(search, "Parts", new List<AreaPart>
        {
            new(Vector3.Zero, "Первый участок", 0),
            new(new(100, 0, 100), "Второй участок", 0)
        });
        Field(navigation, "_huntSearch", search);
        for (var i = 0; i < 3; i++)
        {
            var part = navigation.NextHuntSearchPart(Vector3.Zero, advance: false);
            Assert.Equal("Первый участок", part!.Value.Name);
            Assert.Equal(1, part.Value.Index);
        }
        Assert.Equal(0, searchType.GetProperty("Index")!.GetValue(search));
    }

    private static (Plugin, NavigationService) Create()
    {
        var plugin = (Plugin)RuntimeHelpers.GetUninitializedObject(typeof(Plugin));
        var navigation = (NavigationService)RuntimeHelpers.GetUninitializedObject(typeof(NavigationService));
        Field(plugin, "_navigation", navigation);
        Field(plugin, "_config", new Configuration());
        Set(plugin, "ClientState", StrictProxy.Of<IClientState>(m => m.Name == "get_TerritoryType" ? 141U : throw new InvalidOperationException(m.Name)));
        Set(plugin, "ObjectTable", StrictProxy.Of<IObjectTable>(m => m.Name == "get_LocalPlayer" ? null : throw new InvalidOperationException(m.Name)));
        return (plugin, navigation);
    }

    private static (string Result, Vector3 Position, string Name, bool HeightIsGuess) Read(Plugin plugin)
    {
        object?[] args = [default(Vector3), "", 0f, false, false];
        var method = typeof(Plugin).GetMethod("TryResolveDestinationReadout", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var result = method.Invoke(plugin, args)!;
        return (result.ToString()!, (Vector3)args[0]!, (string)args[1]!, (bool)args[3]!);
    }

    private static void Set(object instance, string property, object? value) =>
        instance.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(instance, value);

    private static void Field(object instance, string field, object value) =>
        instance.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(instance, value);

    public class StrictProxy : DispatchProxy
    {
        private Func<MethodInfo, object?> _call = null!;
        public static T Of<T>(Func<MethodInfo, object?> call) where T : class
        {
            var value = Create<T, StrictProxy>();
            ((StrictProxy)(object)value)._call = call;
            return value;
        }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => _call(targetMethod!);
    }
}
