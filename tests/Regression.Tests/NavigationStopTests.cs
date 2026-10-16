using System.Reflection;
using System.Runtime.CompilerServices;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using FF14Accessibility;
using FF14Accessibility.Services;

namespace Regression.Tests;

public sealed class NavigationStopTests
{
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    public void StopIsAvailableBeforeModalReaderButDoesNotStealChatInput(bool reader, bool typing, bool stops)
    {
        var (plugin, keys) = FollowTargetKeyTests.CreateInput();
        var walk = (AutoWalkService)RuntimeHelpers.GetUninitializedObject(typeof(AutoWalkService));
        var nav = (NavmeshIpc)RuntimeHelpers.GetUninitializedObject(typeof(NavmeshIpc));
        var count = 0;
        Set(nav, "_stop", Proxy.Of<ICallGateSubscriber<object>>(_ => { ++count; return null; }));
        Set(walk, "_nav", nav);
        Set(walk, "_log", Proxy.Of<IPluginLog>(_ => null));
        Set(walk, "_tolk", RuntimeHelpers.GetUninitializedObject(typeof(TolkService)));
        Set(walk, "_phase", Enum.Parse(typeof(AutoWalkService).GetNestedType("Phase", BindingFlags.NonPublic)!, "Walking"));
        Set(plugin, "_autoWalk", walk); Set(plugin, "_config", new Configuration());
        Set(plugin, "_textInputActive", typing);
        keys.Down[0x63] = true;
        typeof(Plugin).GetMethod("UpdateKeyEdges", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(plugin, null);
        var result = typeof(Plugin).GetMethod("HandleNavigationStopKeys", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(plugin, [reader]);
        Assert.Equal(stops, result);
        Assert.Equal(stops ? 1 : 0, count);
        Assert.Equal(!stops, walk.IsActive);
        Assert.Equal(!stops, keys.Down[0x63]);
    }

    private static void Set(object value, string name, object field) => value.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(value, field);
    public class Proxy : DispatchProxy
    {
        private Func<MethodInfo, object?> _call = null!;
        internal static T Of<T>(Func<MethodInfo, object?> call) where T : class
        { var value = Create<T, Proxy>(); ((Proxy)(object)value)._call = call; return value; }
        protected override object? Invoke(MethodInfo? method, object?[]? args) => _call(method!);
    }
}
