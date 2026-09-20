using FF14Accessibility.Services;

namespace FF14Accessibility.Tests;

public sealed class CrossHotbarChangeTrackerTests
{
    [Fact]
    public void InitialAndUnchangedSetsAreSilentButChangesAndWrapAreReported()
    {
        var tracker = new CrossHotbarChangeTracker();
        Assert.Null(tracker.Update(10));
        Assert.Null(tracker.Update(10));
        Assert.Equal(2, tracker.Update(11));
        Assert.Null(tracker.Update(11));
        Assert.Equal(8, tracker.Update(17));
        Assert.Equal(1, tracker.Update(10));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(18)]
    [InlineData(255)]
    public void UnavailableOrUnsupportedStateResetsTheAnnouncementBaseline(int? id)
    {
        var tracker = new CrossHotbarChangeTracker();
        tracker.Update(10);
        Assert.Null(tracker.Update(id));
        Assert.Null(tracker.Update(11));
        Assert.Equal(3, tracker.Update(12));
    }
}
