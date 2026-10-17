using System.Reflection;
using FF14Accessibility;
using FF14Accessibility.Services;

namespace Regression.Tests;

public sealed class YesNoHoldKeyTests
{
    [Theory]
    [InlineData(0x60)]
    [InlineData(0x2D)]
    public void GameOkOnYesNoStaysWithTheGame(int vk)
    {
        var (plugin, keys) = Start();
        keys.Down[vk] = true;
        Edges(plugin);
        Assert.True(plugin.LeaveGameOkOnYesNo());
        Assert.True(keys.Down[vk]);
        Assert.False(Suppresses(plugin, vk));
        Assert.False(plugin.YesNoHoldNeedsReleaseSpeech);
    }

    [Theory]
    [InlineData(0x60)]
    [InlineData(0x2D)]
    public void HoldStartLeavesTheConfirmKeyDown(int vk)
    {
        var (plugin, keys) = Start();
        keys.Down[vk] = true;
        Edges(plugin);
        Assert.True(plugin.TakeYesNoConfirmKey(true));
        Assert.True(keys.Down[vk]);
        Assert.False(Suppresses(plugin, vk));
        Assert.False(plugin.YesNoHoldNeedsReleaseSpeech);
    }

    [Fact]
    public void EnterHoldStartLeavesEnterDown()
    {
        var (plugin, keys) = Start();
        keys.Down[0x0D] = true;
        Edges(plugin);
        Assert.True(plugin.TryLeaveEnterForYesNoHold(true, false, false, true));
        Assert.True(keys.Down[0x0D]);
        Assert.False(Suppresses(plugin, 0x0D));
    }

    [Theory]
    [InlineData(0x60)]
    [InlineData(0x2D)]
    [InlineData(0x0D)]
    public void HeldPollWhileTheDialogStaysOpenLeavesTheKey(int vk)
    {
        var (plugin, keys) = Start();
        keys.Down[vk] = true;
        Edges(plugin);
        plugin.BeginYesNoNativeHold(vk);
        Assert.False(plugin.ApplyYesNoHoldPoll(true));
        Assert.True(keys.Down[vk]);
        Assert.False(plugin.YesNoHoldNeedsReleaseSpeech);
        Assert.False(Suppresses(plugin, vk));
    }

    [Theory]
    [InlineData(0x60)]
    [InlineData(0x2D)]
    [InlineData(0x0D)]
    public void EarlyReleaseSetsTheSpeechFlagAndDoesNotClaim(int vk)
    {
        var (plugin, keys) = Start();
        keys.Down[vk] = true;
        Edges(plugin);
        plugin.BeginYesNoNativeHold(vk);
        keys.Down[vk] = false;
        Edges(plugin);
        Assert.True(plugin.ApplyYesNoHoldPoll(true));
        Assert.True(plugin.YesNoHoldNeedsReleaseSpeech);
        Assert.False(plugin.ApplyYesNoHoldPoll(true));
        Assert.False(Suppresses(plugin, vk));
    }

    [Theory]
    [InlineData(0x60)]
    [InlineData(0x2D)]
    [InlineData(0x0D)]
    public void ReleaseAfterTheDialogClosesDoesNotClaim(int vk)
    {
        var (plugin, keys) = Start();
        keys.Down[vk] = true;
        Edges(plugin);
        plugin.BeginYesNoNativeHold(vk);
        keys.Down[vk] = false;
        Edges(plugin);
        Assert.False(plugin.ApplyYesNoHoldPoll(false));
        Assert.False(plugin.YesNoHoldNeedsReleaseSpeech);
        Assert.False(Suppresses(plugin, vk));
    }

    [Theory]
    [InlineData(0x60)]
    [InlineData(0x2D)]
    [InlineData(0x0D)]
    public void ClosedDialogKeepsTheKeyUntilRelease(int vk)
    {
        var (plugin, keys) = Start();
        keys.Down[vk] = true;
        Edges(plugin);
        plugin.BeginYesNoNativeHold(vk);
        Assert.False(plugin.ApplyYesNoHoldPoll(false));
        Assert.False(keys.Down[vk]);
        Assert.False(plugin.YesNoHoldNeedsReleaseSpeech);
        keys.Down[vk] = true;
        Edges(plugin);
        Suppress(plugin);
        Assert.False(keys.Down[vk]);
        Edges(plugin);
        Suppress(plugin);
        keys.Down[vk] = true;
        Edges(plugin);
        Suppress(plugin);
        Assert.True(keys.Down[vk]);
    }

    [Theory]
    [InlineData(0x60)]
    [InlineData(0x2D)]
    public void OrdinaryFocusConsumesAndClaims(int vk)
    {
        var (plugin, keys) = Start();
        keys.Down[vk] = true;
        Edges(plugin);
        Assert.True(plugin.TakeYesNoConfirmKey(false));
        Assert.False(keys.Down[vk]);
        keys.Down[vk] = true;
        Edges(plugin);
        Suppress(plugin);
        Assert.False(keys.Down[vk]);
    }

    [Fact]
    public void OrdinaryEnterIsConsumedAndClaimed()
    {
        var (plugin, keys) = Start();
        keys.Down[0x0D] = true;
        Edges(plugin);
        Assert.False(plugin.TryLeaveEnterForYesNoHold(true, false, false, false));
        Assert.True(plugin.ConsumeOrdinaryEnter(true));
        Assert.False(keys.Down[0x0D]);
        keys.Down[0x0D] = true;
        Edges(plugin);
        Suppress(plugin);
        Assert.False(keys.Down[0x0D]);
    }

    [Fact]
    public void EnterHoldFrameDoesNotSwallowTheKey()
    {
        var (plugin, keys) = Start();
        keys.Down[0x0D] = true;
        Edges(plugin);
        Assert.True(plugin.TryLeaveEnterForYesNoHold(true, false, false, true));
        keys.Down[0x0D] = true;
        Edges(plugin);
        if (!plugin.TryLeaveEnterForYesNoHold(true, false, false, true))
            plugin.ConsumeOrdinaryEnter(true);
        Assert.True(keys.Down[0x0D]);
    }

    [Fact]
    public void ANewHoldClearsTheEarlyReleaseFlag()
    {
        var (plugin, keys) = Start();
        keys.Down[0x60] = true;
        Edges(plugin);
        plugin.BeginYesNoNativeHold(0x60);
        keys.Down[0x60] = false;
        Edges(plugin);
        Assert.True(plugin.ApplyYesNoHoldPoll(true));
        keys.Down[0x60] = true;
        Edges(plugin);
        plugin.BeginYesNoNativeHold(0x60);
        Assert.False(plugin.YesNoHoldNeedsReleaseSpeech);
        Assert.True(keys.Down[0x60]);
    }

    private static (Plugin Plugin, ShopQuantityTests.KeyStateProxy Keys) Start()
    {
        var (plugin, keys) = FollowTargetKeyTests.CreateInput();
        typeof(Plugin).GetField("_nativeButtonKeys", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(plugin, new NativeButtonKeyOwnership());
        return (plugin, keys);
    }

    private static void Edges(Plugin plugin) =>
        typeof(Plugin).GetMethod("UpdateKeyEdges", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(plugin, null);

    private static void Suppress(Plugin plugin) =>
        typeof(Plugin).GetMethod("SuppressOwnedNativeButtonHold", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(plugin, null);

    private static bool Suppresses(Plugin plugin, int vk)
    {
        var owned = (NativeButtonKeyOwnership)typeof(Plugin)
            .GetField("_nativeButtonKeys", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(plugin)!;
        return owned.Suppress(vk, true);
    }
}
