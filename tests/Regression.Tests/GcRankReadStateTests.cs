using FF14Accessibility.Services;

namespace Regression.Tests;

public sealed class GcRankReadStateTests
{
    [Fact]
    public void LoggedThreeOfSixLayoutWarnsOnceAcross985FocusSamples()
    {
        var state = new GcRankReadState();
        Assert.True(state.ShouldWarn(100, 3, 6));
        for (var i = 1; i < 985; i++) Assert.False(state.ShouldWarn(100, 3, 6));
        Assert.True(state.ShouldWarn(100, 4, 6));
        Assert.True(state.ShouldWarn(200, 4, 6));
        Assert.False(state.ShouldWarn(200, 6, 6));
        Assert.True(state.ShouldWarn(200, 4, 6));
    }

    [Fact]
    public void FocusLoggingKeepsChangedButtonSelectionTextAndWindow()
    {
        var state = new GcRankReadState();
        Assert.True(state.ShouldLog(100, 37, "Ступень ранга 1 из 3, выбрано"));
        Assert.False(state.ShouldLog(100, 37, "Ступень ранга 1 из 3, выбрано"));
        Assert.True(state.ShouldLog(100, 38, "Ступень ранга 2 из 3"));
        Assert.True(state.ShouldLog(100, 38, "Ступень ранга 2 из 3, выбрано"));
        Assert.True(state.ShouldLog(200, 38, "Ступень ранга 2 из 3, выбрано"));
    }

    [Fact]
    public void ReopenedShopCanReportTheSameLayoutAndFocusAgain()
    {
        var state = new GcRankReadState();
        Assert.True(state.ShouldWarn(100, 3, 6));
        Assert.True(state.ShouldLog(100, 37, "Ступень ранга 1 из 3"));
        state.Reset();
        Assert.True(state.ShouldWarn(100, 3, 6));
        Assert.True(state.ShouldLog(100, 37, "Ступень ранга 1 из 3"));
    }
}
