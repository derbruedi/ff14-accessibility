using System.Reflection;
using System.Runtime.CompilerServices;
using Dalamud.Plugin.Services;
using FF14Accessibility;

namespace Regression.Tests;

public sealed class FollowTargetKeyTests
{
    [Theory]
    [InlineData(14, "+")]
    [InlineData(15, "+")]
    [InlineData(16, "+")]
    [InlineData(16, "=")]
    public void SavedOldKeyMovesWithoutResettingOtherPreferences(int version, string key)
    {
        var config = new Configuration { Version = version, KeyFollowTarget = key, KeyCompanionWindow = "Strg+Umschalt+V" };
        Assert.True(config.MigrateFollowTargetKey());
        Assert.Equal("Alt+F", config.KeyFollowTarget);
        Assert.Equal("Strg+Umschalt+V", config.KeyCompanionWindow);
        Assert.Equal(17, config.Version);
        Assert.False(config.MigrateFollowTargetKey());
    }

    [Theory]
    [InlineData("Strg+F")]
    [InlineData("Alt+F")]
    [InlineData("")]
    public void ExplicitCustomOrDisabledKeySurvivesMigration(string key)
    {
        var config = new Configuration { Version = 16, KeyFollowTarget = key };
        Assert.True(config.MigrateFollowTargetKey());
        Assert.Equal(key, config.KeyFollowTarget);
    }

    [Theory]
    [InlineData(17)]
    [InlineData(18)]
    public void MigrationDoesNotOverrideLaterChoicesOrDowngradeSettings(int version)
    {
        var config = new Configuration { Version = version, KeyFollowTarget = "+" };
        Assert.False(config.MigrateFollowTargetKey());
        Assert.Equal(version, config.Version);
        Assert.Equal("+", config.KeyFollowTarget);
    }

    [Fact]
    public void DefaultAndResetUseAltFWithoutAnotherPluginDefaultOnThatChord()
    {
        var (plugin, _) = CreateInput();
        var parse = Method("ParseKeySpec");
        var config = new Configuration();
        var binding = parse.Invoke(plugin, [config.KeyFollowTarget]);
        Assert.Equal((0x46, false, false, true), binding);
        var fields = typeof(Configuration).GetFields().Where(f => f.FieldType == typeof(string) && f.Name.StartsWith("Key"));
        Assert.Single(fields, f => Equals(binding, parse.Invoke(plugin, [f.GetValue(config)!])));
        config.KeyFollowTarget = "+";
        config.ResetKeysToDefaults();
        Assert.Equal("Alt+F", config.KeyFollowTarget);
    }

    [Theory]
    [InlineData(0xBB, false, false, false, false, false)] // '=' on the hotbar
    [InlineData(0xBB, false, true, false, false, false)] // '+' with Shift
    [InlineData(0x46, false, false, false, false, false)] // ordinary F
    [InlineData(0x46, false, false, true, false, true)] // Alt+F
    [InlineData(0x46, true, false, true, false, false)] // Ctrl+Alt+F
    [InlineData(0x46, false, true, true, false, false)] // Shift+Alt+F
    [InlineData(0x46, false, false, true, true, false)] // chat owns the chord
    public void OnlyExactFollowChordFiresAndIsConsumed(int vk, bool ctrl, bool shift, bool alt, bool textInput, bool expected)
    {
        var (plugin, keys) = CreateInput();
        keys.Down[vk] = true;
        keys.Down[0x11] = ctrl;
        keys.Down[0x10] = shift;
        keys.Down[0x12] = alt;
        Field("_textInputActive").SetValue(plugin, textInput);
        Method("UpdateKeyEdges").Invoke(plugin, null);
        Assert.Equal(expected, FollowPressed(plugin));
        Assert.Equal(!expected, keys.Down[vk]);
        Assert.Equal(alt, keys.Down[0x12]);
    }

    [Fact]
    public void HoldingFollowDoesNotRetoggleOrLeakToGameAndRepressWorks()
    {
        var (plugin, keys) = CreateInput();
        keys.Down[0x12] = true;
        keys.Down[0x46] = true;
        Method("UpdateKeyEdges").Invoke(plugin, null);
        Assert.True(FollowPressed(plugin));
        Assert.False(keys.Down[0x46]);
        keys.Down[0x46] = true; // physical key is still held in the next frame
        Method("UpdateKeyEdges").Invoke(plugin, null);
        Assert.False(FollowPressed(plugin));
        Assert.False(keys.Down[0x46]);
        Method("UpdateKeyEdges").Invoke(plugin, null); // release
        Assert.False(FollowPressed(plugin));
        keys.Down[0x46] = true;
        Method("UpdateKeyEdges").Invoke(plugin, null);
        Assert.True(FollowPressed(plugin));
    }

    private static bool FollowPressed(Plugin plugin) =>
        (bool)Method("IsJustPressed").Invoke(plugin, [new Configuration().KeyFollowTarget, false, true])!;

    internal static (Plugin, ShopQuantityTests.KeyStateProxy) CreateInput()
    {
        var plugin = (Plugin)RuntimeHelpers.GetUninitializedObject(typeof(Plugin));
        var keys = DispatchProxy.Create<IKeyState, ShopQuantityTests.KeyStateProxy>();
        typeof(Plugin).GetProperty("KeyState", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(plugin, keys);
        Field("_keySpecCache").SetValue(plugin, new Dictionary<string, (int, bool, bool, bool)>());
        Field("_keyWasDown").SetValue(plugin, new bool[256]);
        Field("_keyJustPressed").SetValue(plugin, new bool[256]);
        Field("_warnedInvalidVk").SetValue(plugin, new HashSet<int>());
        return (plugin, (ShopQuantityTests.KeyStateProxy)(object)keys);
    }

    private static MethodInfo Method(string name) => typeof(Plugin).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static FieldInfo Field(string name) => typeof(Plugin).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
}
