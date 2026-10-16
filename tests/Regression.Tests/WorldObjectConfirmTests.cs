using System.Reflection;
using System.Runtime.CompilerServices;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FF14Accessibility;
using FF14Accessibility.Services;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using System.Runtime.InteropServices;
using NativeTreasure = FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure;

namespace Regression.Tests;

public sealed class WorldObjectConfirmTests
{
    [Fact]
    public void QuestTransportNpcCanReceiveExactNativeInteractionDespiteOldEnemyTarget()
    {
        var npc = Object(1000106, ObjectKind.EventNpc);
        var targets = DispatchProxy.Create<ITargetManager, NavigationInputTests.TargetProxy>();
        var state = (NavigationInputTests.TargetProxy)targets;
        state.Accepts = false; state.Hard = Object(123, ObjectKind.BattleNpc);
        IGameObject? actual = null;
        var request = BrowserTargetSelection.ConfirmQuestOrWorldObject(targets, npc, true, obj => { actual = obj; return 0; });
        Assert.True(request.Requested); Assert.Same(npc, actual);
        Assert.False(BrowserTargetSelection.ConfirmQuestOrWorldObject(targets, npc, false, _ => throw new Exception()).Requested);
    }
    [Theory]
    [InlineData(ObjectKind.EventObj)]
    [InlineData(ObjectKind.Treasure)]
    [InlineData(ObjectKind.GatheringPoint)]
    [InlineData(ObjectKind.Aetheryte)]
    public void ExplicitPropChoiceOwnsConfirmWhenCombatStillHasAnEnemyTarget(ObjectKind kind)
    {
        var nav = CombatNavigation();
        typeof(NavigationService).GetProperty("SelectedObjectDestination")!
            .SetValue(nav, new ObjectDestination(0x10039A457, "Nest", Vector3.Zero, kind));
        Assert.True(nav.HasWorldObjectConfirmSelection());
    }

    [Fact]
    public void CombatEnemyWithoutExplicitPropChoiceKeepsNativeConfirm()
        => Assert.False(CombatNavigation().HasWorldObjectConfirmSelection());

    private static NavigationService CombatNavigation()
    {
        var nav = (NavigationService)RuntimeHelpers.GetUninitializedObject(typeof(NavigationService));
        var player = ChatPlayerTests.Proxy.Of<IPlayerCharacter>(m => m.Name == "get_StatusFlags"
            ? StatusFlags.InCombat : throw new NotSupportedException(m.Name));
        var objects = ChatPlayerTests.Proxy.Of<IObjectTable>(m => m.Name == "get_LocalPlayer"
            ? player : throw new NotSupportedException(m.Name));
        var targets = DispatchProxy.Create<ITargetManager, NavigationInputTests.TargetProxy>();
        ((NavigationInputTests.TargetProxy)targets).Hard = Object(0x400162A1, ObjectKind.BattleNpc);
        typeof(NavigationService).GetField("_objectTable", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(nav, objects);
        typeof(NavigationService).GetField("_targetManager", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(nav, targets);
        return nav;
    }

    [Fact]
    public void YarzonNestConfirmClearsNearbyEnemySoftTargetAndInteractsWithExactNest()
    {
        var selected = Object(0x10039A457, ObjectKind.EventObj);
        var sameNameNest = Object(0x10039A456, ObjectKind.EventObj);
        var enemy = Object(0x400162A1, ObjectKind.BattleNpc);
        var targets = DispatchProxy.Create<ITargetManager, NavigationInputTests.TargetProxy>();
        var state = (NavigationInputTests.TargetProxy)targets;
        state.Accepts = true;
        state.Hard = selected;
        state.Soft = enemy;
        var exact = BrowserTargetSelection.FindExact([enemy, sameNameNest, selected], 0x10039A457);
        Assert.Same(selected, exact);
        IGameObject? interacted = null;
        var request = BrowserTargetSelection.ConfirmWorldObject(targets, exact!, o => { interacted = o; return 7; });
        Assert.True(request.Requested);
        Assert.True(request.TargetAccepted);
        Assert.Equal(7UL, request.NativeResult);
        Assert.Same(selected, interacted);
        Assert.Same(selected, targets.Target);
        Assert.Null(targets.SoftTarget);
    }

    [Theory]
    [InlineData(true, false, ObjectKind.EventObj)]
    [InlineData(true, false, ObjectKind.GatheringPoint)]
    [InlineData(true, false, ObjectKind.Treasure)]
    [InlineData(true, false, ObjectKind.Aetheryte)]
    [InlineData(true, true, ObjectKind.BattleNpc)]
    [InlineData(true, true, ObjectKind.Pc)]
    public void UnavailableObjectOrCombatTargetNeverRequestsInteraction(bool accepts, bool targetable, ObjectKind kind)
    {
        var obj = Object(11, kind, targetable);
        var targets = DispatchProxy.Create<ITargetManager, NavigationInputTests.TargetProxy>();
        ((NavigationInputTests.TargetProxy)targets).Accepts = accepts;
        var calls = 0;
        Assert.False(BrowserTargetSelection.ConfirmWorldObject(targets, obj, _ => { calls++; return 0; }).Requested);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(0UL)]
    [InlineData(0x10039A457UL)]
    public void HardTargetFilterRefusalStillRequestsExactAvailableNest(ulong nativeResult)
    {
        var nest = Object(0x10039A457, ObjectKind.EventObj);
        var enemy = Object(0x400162A1, ObjectKind.BattleNpc);
        var targets = DispatchProxy.Create<ITargetManager, NavigationInputTests.TargetProxy>();
        var state = (NavigationInputTests.TargetProxy)targets;
        state.Accepts = false; state.Hard = enemy; state.Soft = enemy;
        var calls = 0;
        var request = BrowserTargetSelection.ConfirmWorldObject(targets, nest, actual =>
        {
            Assert.Same(nest, actual);
            calls++;
            return nativeResult;
        });
        Assert.True(request.Requested);
        Assert.False(request.TargetAccepted);
        Assert.Equal(nativeResult, request.NativeResult);
        Assert.Equal(1, calls);
        Assert.Same(enemy, targets.Target);
    }

    [Fact]
    public void NullNativeAddressCannotBePassedToInteraction()
    {
        var nest = Object(0x10039A457, ObjectKind.EventObj);
        ((WorldObjectProxy)nest).Address = 0;
        var targets = DispatchProxy.Create<ITargetManager, NavigationInputTests.TargetProxy>();
        var calls = 0;
        Assert.False(BrowserTargetSelection.ConfirmWorldObject(targets, nest, _ => { calls++; return 0; }).Requested);
        Assert.Equal(0, calls);
        Assert.Equal(0, ((NavigationInputTests.TargetProxy)targets).Requests);
    }

    [Theory]
    [InlineData(ObjectKind.GatheringPoint)]
    public void BrowserUsesCurrentAvailabilityAndRestoresReactivatedProp(ObjectKind kind)
    {
        var nav = (NavigationService)RuntimeHelpers.GetUninitializedObject(typeof(NavigationService));
        var browse = typeof(NavigationService).GetMethod("IsWorthBrowsing", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var obj = Object(0x10039A457, kind, false);
        Assert.False((bool)browse.Invoke(nav, [obj])!);
        ((WorldObjectProxy)obj).Targetable = true;
        Assert.True((bool)browse.Invoke(nav, [obj])!);
        ((WorldObjectProxy)obj).Targetable = false;
        Assert.False((bool)browse.Invoke(nav, [obj])!);
    }

    [Theory]
    [InlineData(ObjectKind.EventObj, false, "Запертая дверь", true)]
    [InlineData(ObjectKind.EventObj, true, "Запертая дверь", true)]
    [InlineData(ObjectKind.EventObj, false, "", false)]
    [InlineData(ObjectKind.EventObj, true, "", true)]
    [InlineData(ObjectKind.EventNpc, false, "Жёлтая куртка", true)]
    [InlineData(ObjectKind.HousingEventObject, false, "Почтовый ящик", true)]
    [InlineData(ObjectKind.GatheringPoint, false, "Месторождение", false)]
    [InlineData(ObjectKind.GatheringPoint, true, "Месторождение", true)]
    public void NamedWorldObjectsRemainDiscoverableWithoutClaimingTheyCanBeUsed(
        ObjectKind kind, bool targetable, string name, bool expected)
    {
        var nav = (NavigationService)RuntimeHelpers.GetUninitializedObject(typeof(NavigationService));
        var names = new ObjectNameService(QuestAreaPointTests.Proxy.Of<IDataManager>((_, _) => null));
        typeof(NavigationService).GetField("_objectNames", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(nav, names);
        var obj = Object(42, kind, targetable);
        ((WorldObjectProxy)obj).Name = name;
        var browse = typeof(NavigationService).GetMethod("IsWorthBrowsing", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.Equal(expected, browse.Invoke(nav, [obj]));
        var targets = DispatchProxy.Create<ITargetManager, NavigationInputTests.TargetProxy>();
        var calls = 0;
        BrowserTargetSelection.ConfirmWorldObject(targets, obj, _ => { calls++; return 0; });
        Assert.Equal(targetable ? 1 : 0, calls);
    }

    [Fact]
    public unsafe void OpenedTreasureStaysOutOfTheBrowserEvenWhenNamedAndTargetable()
    {
        var treasure = (NativeTreasure*)NativeMemory.AllocZeroed((nuint)sizeof(NativeTreasure));
        try
        {
            var nav = (NavigationService)RuntimeHelpers.GetUninitializedObject(typeof(NavigationService));
            typeof(NavigationService).GetField("_objectNames", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(nav, new ObjectNameService(null!));
            typeof(NavigationService).GetField("_reportedEmptyTreasures", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(nav, new HashSet<ulong>());
            typeof(NavigationService).GetField("_log", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(nav, QuestAreaPointTests.Proxy.Of<IPluginLog>((_, _) => null));
            var obj = Object(43, ObjectKind.Treasure); var state = (WorldObjectProxy)obj;
            state.Name = "Сундук"; state.Address = (nint)treasure;
            var browse = typeof(NavigationService).GetMethod("IsWorthBrowsing", BindingFlags.Instance | BindingFlags.NonPublic)!;
            treasure->State = NativeTreasure.TreasureState.Unopened;
            Assert.True((bool)browse.Invoke(nav, [obj])!);
            treasure->State = NativeTreasure.TreasureState.Opened;
            Assert.False((bool)browse.Invoke(nav, [obj])!);
        }
        finally { NativeMemory.Free(treasure); }
    }

    [Fact]
    public void UnloadedNestIsNeverReplacedWithAnotherNestOrEnemy()
    {
        var enemy = Object(0x400162A1, ObjectKind.BattleNpc);
        var otherNest = Object(0x10039A456, ObjectKind.EventObj);
        Assert.Null(BrowserTargetSelection.FindExact([enemy, otherNest], 0x10039A457));
        Assert.Null(BrowserTargetSelection.FindExact([enemy], 0));
    }

    [Fact]
    public void HeldConfirmCannotActivateTheWindowItJustOpened()
    {
        var (plugin, keys) = FollowTargetKeyTests.CreateInput();
        var method = typeof(Plugin).GetMethod("SuppressOwnedWorldConfirmHold", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var owned = typeof(Plugin).GetField("_worldConfirmKeyOwned", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var edges = typeof(Plugin).GetMethod("UpdateKeyEdges", BindingFlags.Instance | BindingFlags.NonPublic)!;
        owned.SetValue(plugin, true);
        keys.Down[0x60] = true;
        edges.Invoke(plugin, null);
        method.Invoke(plugin, null);
        Assert.False(keys.Down[0x60]);
        Assert.True((bool)owned.GetValue(plugin)!);
        // Native interaction has opened InventoryEventGrid; ownership survives.
        keys.Down[0x60] = true;
        edges.Invoke(plugin, null);
        method.Invoke(plugin, null);
        Assert.False(keys.Down[0x60]);
        edges.Invoke(plugin, null);
        method.Invoke(plugin, null);
        Assert.False((bool)owned.GetValue(plugin)!);
        keys.Down[0x60] = true;
        edges.Invoke(plugin, null);
        method.Invoke(plugin, null);
        Assert.True(keys.Down[0x60]); // A new press belongs to the open window.
    }

    private static IGameObject Object(ulong id, ObjectKind kind, bool targetable = true)
    {
        var obj = DispatchProxy.Create<IGameObject, WorldObjectProxy>();
        var state = (WorldObjectProxy)obj;
        state.Id = id; state.Kind = kind; state.Targetable = targetable;
        return obj;
    }

    public class WorldObjectProxy : DispatchProxy
    {
        public ulong Id;
        public ObjectKind Kind;
        public bool Targetable;
        public string Name = string.Empty;
        public nint Address = (nint)123;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name switch
        {
            "get_GameObjectId" => Id, "get_ObjectKind" => Kind, "get_IsTargetable" => Targetable,
            "get_Address" => Address,
            "get_Name" => new SeString(new TextPayload(Name)), "get_BaseId" => 0u,
            _ => throw new NotSupportedException(method?.Name),
        };
    }
}
