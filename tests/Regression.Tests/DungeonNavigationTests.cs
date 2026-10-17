using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using FF14Accessibility.Services;

namespace Regression.Tests;

public sealed class DungeonNavigationTests
{
    [Fact]
    public void CompatibilityReusesOnlyTheOriginalCustomizationAndNeverReplacesANewerOne()
    {
        var original = new object();
        var registry = new System.Collections.Hashtable { [171u] = original };
        Assert.Equal(DungeonMeshCompatibility.Result.Applied, DungeonMeshCompatibility.Register(registry, original));
        Assert.Same(original, registry[1330u]);
        Assert.Equal(DungeonMeshCompatibility.Result.AlreadySupported, DungeonMeshCompatibility.Register(registry, original));
        var newer = new object(); registry[1330u] = newer;
        Assert.Equal(DungeonMeshCompatibility.Result.AlreadySupported, DungeonMeshCompatibility.Register(registry, original));
        Assert.Same(newer, registry[1330u]);
        registry.Remove(1330u);
        Assert.Equal(DungeonMeshCompatibility.Result.Unsupported, DungeonMeshCompatibility.Register(registry, newer));
        Assert.False(registry.Contains(1330u));
        Assert.Equal(DungeonMeshCompatibility.Result.Unsupported, DungeonMeshCompatibility.Apply(typeof(DungeonNavigationTests).Assembly));
    }
    [Theory]
    [InlineData(20, 0)]
    [InlineData(-20, 0)]
    [InlineData(0, 9)]
    public void RecordedDungeonFloorDoesNotProjectToAnotherFloorOrRoom(float height, float horizontal)
    {
        var recorded = new Vector3(20, -14, 94);
        Assert.Equal(recorded, DungeonNavigationPoint.Resolve(recorded, (point, xz, y) =>
        {
            Assert.Equal(2.5f, xz); Assert.Equal(3f, y);
            return point + new Vector3(horizontal, height, 0);
        }));
    }

    [Fact]
    public void MissingMeshPointPreservesMeasuredDestinationForAuthorMovement()
    {
        var recorded = new Vector3(20, -14, 94);
        Assert.Equal(recorded, DungeonNavigationPoint.Resolve(recorded, (_, _, _) => null));
        var surface = recorded + new Vector3(1, 0.2f, 1);
        Assert.Equal(surface, DungeonNavigationPoint.Resolve(recorded, (_, _, _) => surface));
        Assert.Equal(recorded, DungeonNavigationPoint.Resolve(recorded, (_, _, _) => new(float.NaN, 0, 0)));
    }

    [Theory]
    [InlineData("2001234 (terminal)", 2001234u)]
    [InlineData("1004346", 1004346u)]
    [InlineData("door 2001234", 0u)]
    [InlineData("", 0u)]
    public void ObjectIdentityComesFromTheNumericArgument(string argument, uint expected)
        => Assert.Equal(expected, DungeonNavigationPoint.DataId([argument]));

    [Fact]
    public void ExactMechanismWinsOverANearerDifferentDoorAndAnotherFloor()
    {
        var station = new DungeonStep(4, DungeonStepKind.Interact, new(10, -14, 20), "Door", 2001234);
        var wrong = Object(1, 2009999, new(10, -14, 20));
        var upstairs = Object(2, 2001234, new(10, -6, 20));
        var correct = Object(3, 2001234, new(12, -14, 20));
        Assert.Same(correct, DungeonNavigationPoint.FindObject([wrong, upstairs, correct], station));
        Assert.Null(DungeonNavigationPoint.FindObject([wrong, upstairs], station));
        Assert.Null(DungeonNavigationPoint.FindObject([wrong, correct], station with { DataId = 0 }));
        Assert.Null(DungeonNavigationPoint.FindObject([correct], station with { Kind = DungeonStepKind.Waypoint }));
    }

    [Fact]
    public void MechanismsCanBeEventNpcsAndDoNotDependOnTranslatedNames()
    {
        var station = new DungeonStep(1, DungeonStepKind.Interact, new(10, 1, 20), "Переведённое имя", 1004346);
        var npc = Object(4, 1004346, station.Position, ObjectKind.EventNpc);
        Assert.Same(npc, DungeonNavigationPoint.FindObject([npc], station));
    }

    [Theory]
    [InlineData(7, true, false)]
    [InlineData(9, true, false)]
    [InlineData(12, false, true)]
    [InlineData(13, false, true)]
    [InlineData(0, false, false)]
    public void NativeNameplateStatesKeepUnpulledEnemiesAndIdentifyFriendlyNpcs(byte plate, bool enemy, bool friendly)
    {
        Assert.Equal(enemy, CombatSide.IsEnemyNamePlate(plate));
        Assert.Equal(friendly, CombatSide.IsFriendlyNpcNamePlate(plate));
    }

    private static IGameObject Object(ulong id, uint data, Vector3 position, ObjectKind kind = ObjectKind.EventObj)
        => ChatPlayerTests.Proxy.Of<IGameObject>(method => method.Name switch
        {
            "get_GameObjectId" => id, "get_BaseId" => data, "get_Position" => position,
            "get_ObjectKind" => kind, _ => throw new NotSupportedException(method.Name)
        });
}
