using System.Reflection;
using System.Runtime.CompilerServices;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using FF14Accessibility;
using FF14Accessibility.Services;

namespace Regression.Tests;

public class ModSwitchTests
{
    [Theory]
    [InlineData(2)] // CollectionMissing
    [InlineData(3)] // ModMissing
    [InlineData(255)] // UnknownError
    public void ExplicitReadErrorsAreUnknownRatherThanDisabled(int status)
    {
        var fake = new FakePenumbra();
        fake.Enabled["a"] = true;
        fake.ReadStatus = status;
        Assert.Equal(2, fake.Service.Read().Unreadable);
        var result = fake.Service.AllOff();
        Assert.Equal(2, result.Skipped);
        Assert.True(fake.Enabled["a"]);
        Assert.Equal(0, result.Changed);
    }

    [Fact]
    public void RepeatedDisableKeepsEveryModStillWaitingForRestore()
    {
        var fake = new FakePenumbra();
        fake.Enabled["a"] = true;
        Assert.Equal(1, fake.Service.AllOff().Remembered);
        fake.Enabled["b"] = true;
        Assert.Equal(2, fake.Service.AllOff().Remembered);
        Assert.Equal(2, fake.Service.Restore().Changed);
        Assert.All(fake.Enabled.Values, value => Assert.True(value));
        Assert.False(fake.Config.ModsSwitchMemoryValid);
        Assert.Empty(fake.Config.ModsSwitchMemory);
        Assert.True(fake.Service.Restore().NothingToDo);
    }

    [Fact]
    public void PartialRestoreRetainsOnlyFailedItemsForRetry()
    {
        var fake = new FakePenumbra();
        fake.Enabled["a"] = fake.Enabled["b"] = true;
        fake.Service.AllOff();
        fake.FailRestore = "b";
        var first = fake.Service.Restore();
        Assert.Equal(1, first.Changed);
        Assert.Equal(1, first.Skipped);
        Assert.Equal(1, first.Remembered);
        fake.FailRestore = null;
        // A subsequent manual disable must not be undone by retrying the failed mod.
        fake.Enabled["a"] = false;
        Assert.Equal(1, fake.Service.Restore().Changed);
        Assert.False(fake.Enabled["a"]);
        Assert.True(fake.Enabled["b"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedListRequestsAreNotReportedAsAnEmptyInstallation(bool collections)
    {
        var fake = new FakePenumbra();
        fake.Enabled["a"] = true;
        fake.Service.AllOff();
        if (collections) fake.FailCollections = true;
        else fake.FailModList = true;
        Assert.False(fake.Service.Read().Answered);
        Assert.False(fake.Service.AllOff().Answered);
        Assert.Single(fake.Config.ModsSwitchMemory);
    }

    private sealed class FakePenumbra
    {
        internal readonly Dictionary<string, bool> Enabled = new() { ["a"] = false, ["b"] = false };
        internal readonly Configuration Config = new();
        internal readonly ModSwitchService Service;
        internal string? FailRestore;
        internal bool FailModList, FailCollections;
        internal int ReadStatus;

        internal FakePenumbra()
        {
            var log = Gate.Of<IPluginLog>((_, _) => null);
            var ipc = (PenumbraIpc)RuntimeHelpers.GetUninitializedObject(typeof(PenumbraIpc));
            Set(ipc, "_log", log);
            Set(ipc, "_getModList", Gate.Of<ICallGateSubscriber<Dictionary<string, string>>>((_, _) =>
                FailModList ? throw new InvalidOperationException("unavailable") : Enabled.ToDictionary(x => x.Key, x => x.Key)));
            Set(ipc, "_getCollections", Gate.Of<ICallGateSubscriber<Dictionary<Guid, string>>>((_, _) =>
                FailCollections ? throw new InvalidOperationException("unavailable") : new Dictionary<Guid, string> { [Guid.Empty] = "test" }));
            Set(ipc, "_getCurrentModSettings", Gate.Of<ICallGateSubscriber<Guid, string, string, bool,
                (int, (bool, int, Dictionary<string, List<string>>, bool)?)>>((_, args) =>
                ReadStatus != 0 ? (ReadStatus, ((bool, int, Dictionary<string, List<string>>, bool)?)null)
                    : (0, ((bool, int, Dictionary<string, List<string>>, bool)?)(Enabled[(string)args![1]!], 0, new(), false))));
            Set(ipc, "_trySetMod", Gate.Of<ICallGateSubscriber<Guid, string, string, bool, int>>((_, args) =>
            {
                var key = (string)args![1]!;
                var enabled = (bool)args[3]!;
                if (enabled && key == FailRestore) return 255;
                Enabled[key] = enabled;
                return 0;
            }));
            Service = new ModSwitchService(ipc, Config, log);
        }
    }

    private static void Set(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);

    public class Gate : DispatchProxy
    {
        private Func<MethodInfo, object?[]?, object?> _call = null!;
        public static T Of<T>(Func<MethodInfo, object?[]?, object?> call) where T : class
        {
            var value = Create<T, Gate>();
            ((Gate)(object)value)._call = call;
            return value;
        }
        protected override object? Invoke(MethodInfo? method, object?[]? args) => _call(method!, args);
    }
}
