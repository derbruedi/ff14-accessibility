using System.Reflection;
using FF14Accessibility;
using FF14Accessibility.Services;

namespace Regression.Tests;

public sealed class DialogueConfirmGuardTests
{
    [Theory]
    [InlineData(0x60)] [InlineData(0x2D)] [InlineData(0x0D)]
    public void TapPassesOnceAndHeldKeyCannotAdvanceAnotherPageOrWindow(int key)
    {
        var guard = new DialogueConfirmGuard();
        Assert.False(guard.Update(key, true, true, false));
        for (var frame = 0; frame < 20; frame++) Assert.True(guard.Update(key, true, true, false));
        Assert.True(guard.Update(key, true, false, false)); // dialog closed
        Assert.True(guard.Update(key, true, true, true)); // another dialog has a hold button
        Assert.False(guard.Update(key, false, true, false));
        Assert.False(guard.Update(key, true, true, false)); // a deliberate new press
    }

    [Theory]
    [InlineData(0x60)] [InlineData(0x2D)] [InlineData(0x0D)]
    public void KeyThatOpenedTheDialogCannotAlsoAdvanceItsFirstPage(int key)
    {
        var guard = new DialogueConfirmGuard();
        Assert.False(guard.Update(key, true, false, false));
        Assert.True(guard.Update(key, true, true, false));
    }

    [Fact]
    public void NativeHoldStaysWithTheGameAndOtherContextsDoNotArmTheGuard()
    {
        var guard = new DialogueConfirmGuard();
        for (var frame = 0; frame < 20; frame++)
        {
            Assert.False(guard.Update(0x60, true, true, true));
            Assert.False(guard.Update(0x0D, true, false, false));
        }
        Assert.False(guard.Update(0x60, false, false, false));
        Assert.False(guard.Update(0x0D, false, false, false));
        Assert.False(guard.Update(0x0D, true, true, false));
        Assert.False(guard.Update(0x2D, true, true, false)); // independent keys
    }

    [Theory]
    [InlineData(0x60)] [InlineData(0x2D)] [InlineData(0x0D)]
    public void ClearedGameKeyDoesNotFakePhysicalReleaseOrCreateANewPress(int key)
    {
        var (plugin, keys) = FollowTargetKeyTests.CreateInput();
        var physicallyDown = true;
        typeof(Plugin).GetField("_physicalConfirmKeyDown", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(plugin, (Func<int, bool>)(_ => physicallyDown));
        var update = typeof(Plugin).GetMethod("UpdateKeyEdges", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var held = (bool[])typeof(Plugin).GetField("_keyWasDown", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(plugin)!;
        var pressed = (bool[])typeof(Plugin).GetField("_keyJustPressed", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(plugin)!;
        keys.Down[key] = true;
        update.Invoke(plugin, null);
        Assert.True(pressed[key]);
        keys.Down[key] = false; // consumed by the plugin; physical key is still held
        update.Invoke(plugin, null);
        Assert.True(held[key]); Assert.False(pressed[key]);
        keys.Down[key] = true; // a repeated game input must not become another edge
        update.Invoke(plugin, null);
        Assert.True(held[key]); Assert.False(pressed[key]);
        physicallyDown = false;
        update.Invoke(plugin, null);
        Assert.False(held[key]); Assert.False(pressed[key]);
        keys.Down[key] = true; physicallyDown = true;
        update.Invoke(plugin, null);
        Assert.True(pressed[key]);
    }

    [Theory]
    [InlineData(0x60)] [InlineData(0x2D)] [InlineData(0x0D)]
    public void PluginPassesOneNativeTapAndBlocksHeldRepeatsUntilRealRelease(int key)
    {
        var (plugin, keys) = FollowTargetKeyTests.CreateInput();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var physical = true;
        typeof(Plugin).GetField("_physicalConfirmKeyDown", flags)!
            .SetValue(plugin, (Func<int, bool>)(_ => physical));
        typeof(Plugin).GetField("_dialogueConfirmGuard", flags)!.SetValue(plugin, new DialogueConfirmGuard());
        var update = typeof(Plugin).GetMethod("UpdateKeyEdges", flags)!;
        keys.Down[key] = true;
        update.Invoke(plugin, null);
        plugin.ApplyDialogueConfirmGuard(true, false);
        Assert.True(keys.Down[key]);
        for (var frame = 0; frame < 20; frame++)
        {
            // Include frames in which the game has not restored its cleared bit.
            if (frame % 2 == 0) keys.Down[key] = true;
            update.Invoke(plugin, null);
            plugin.ApplyDialogueConfirmGuard(frame < 10, false);
            Assert.False(keys.Down[key]);
        }
        physical = false;
        update.Invoke(plugin, null);
        plugin.ApplyDialogueConfirmGuard(false, false);
        physical = true; keys.Down[key] = true;
        update.Invoke(plugin, null);
        plugin.ApplyDialogueConfirmGuard(true, false);
        Assert.True(keys.Down[key]);
    }

    [Fact]
    public void GameInputWithoutPhysicalKeyboardDoesNotBecomeRepeatedFreshPresses()
    {
        var (plugin, keys) = FollowTargetKeyTests.CreateInput();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(Plugin).GetField("_physicalConfirmKeyDown", flags)!
            .SetValue(plugin, (Func<int, bool>)(_ => false));
        var update = typeof(Plugin).GetMethod("UpdateKeyEdges", flags)!;
        var pressed = (bool[])typeof(Plugin).GetField("_keyJustPressed", flags)!.GetValue(plugin)!;
        keys.Down[0x60] = true;
        update.Invoke(plugin, null);
        Assert.True(pressed[0x60]);
        for (var frame = 0; frame < 20; frame++)
        {
            update.Invoke(plugin, null);
            Assert.False(pressed[0x60]);
        }
    }
}
