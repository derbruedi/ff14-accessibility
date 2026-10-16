using System.Reflection;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FF14Accessibility;
using FF14Accessibility.Services;

namespace Regression.Tests;

public sealed class NavigationInputTests
{
    [Theory]
    [InlineData("BildAuf", 0x21)]
    [InlineData("BildAb", 0x22)]
    [InlineData("Numpad3", 0x63)]
    public void BrowserAndWalkKeysOwnPressAndHoldWithoutRetoggling(string spec, int vk)
    {
        var (plugin, keys) = FollowTargetKeyTests.CreateInput();
        keys.Down[vk] = true;
        Edges(plugin);
        Assert.True(Pressed(plugin, spec));
        Assert.False(keys.Down[vk]);
        keys.Down[vk] = true;
        Edges(plugin);
        Assert.False(Pressed(plugin, spec));
        Assert.False(keys.Down[vk]);
        Edges(plugin);
        keys.Down[vk] = true;
        Edges(plugin);
        Assert.True(Pressed(plugin, spec));
    }

    [Theory]
    [InlineData(0x11, false)] [InlineData(0x10, false)] [InlineData(0x12, false)]
    [InlineData(0, true)]
    public void ModifiedPageKeysAndTextInputKeepTheirNativeKeys(int modifier, bool typing)
    {
        var (plugin, keys) = FollowTargetKeyTests.CreateInput();
        keys.Down[0x22] = true;
        if (modifier != 0) keys.Down[modifier] = true;
        typeof(Plugin).GetField("_textInputActive", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(plugin, typing);
        Edges(plugin);
        Assert.False(Pressed(plugin, "BildAb"));
        Assert.True(keys.Down[0x22]);
    }

    [Theory]
    [InlineData(-1, -1, 8, 7)] [InlineData(-1, 1, 8, 0)]
    [InlineData(0, -1, 8, 7)] [InlineData(7, 1, 8, 0)]
    [InlineData(99, -1, 8, 7)] [InlineData(99, 1, 8, 0)]
    [InlineData(-1, -1, 1, 0)] [InlineData(0, 1, 0, -1)]
    public void InitialReverseBrowseStartsAtLastEntryAndShrinkingListsStayValid(int current, int direction, int count, int expected)
        => Assert.Equal(expected, BrowserTargetSelection.NextIndex(current, direction, count));

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void TemporaryTargetIsClearedOnlyAfterNativeHardTargetAccepts(bool accepts)
    {
        var obj = DispatchProxy.Create<IGameObject, ObjectProxy>();
        var targets = DispatchProxy.Create<ITargetManager, TargetProxy>();
        var state = (TargetProxy)targets;
        state.Accepts = accepts;
        state.Soft = DispatchProxy.Create<IGameObject, ObjectProxy>();
        ((ObjectProxy)state.Soft).Id = 22;
        Assert.Equal(accepts, BrowserTargetSelection.Select(targets, obj));
        Assert.Equal(1, state.Requests);
        Assert.Equal(accepts ? null : state.Soft, targets.SoftTarget);
        Assert.Equal(accepts ? obj : null, targets.Target);
        Assert.Equal(accepts ? 1 : 0, state.SoftClears);
    }

    private static MethodInfo Method(string name) => typeof(Plugin).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static void Edges(Plugin plugin) => Method("UpdateKeyEdges").Invoke(plugin, null);
    private static bool Pressed(Plugin plugin, string spec) => (bool)Method("IsJustPressed").Invoke(plugin, [spec, false, true])!;

    public class ObjectProxy : DispatchProxy
    {
        public ulong Id = 11;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
            => method?.Name == "get_GameObjectId" ? Id : throw new NotSupportedException(method?.Name);
    }

    public class TargetProxy : DispatchProxy
    {
        public bool Accepts;
        public int Requests, SoftClears;
        public IGameObject? Hard, Soft;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            switch (method?.Name)
            {
                case "get_Target": return Hard;
                case "set_Target": ++Requests; if (Accepts) Hard = (IGameObject?)args![0]; return null;
                case "get_SoftTarget": return Soft;
                case "set_SoftTarget": ++SoftClears; Soft = (IGameObject?)args![0]; return null;
                default: throw new NotSupportedException(method?.Name);
            }
        }
    }
}
