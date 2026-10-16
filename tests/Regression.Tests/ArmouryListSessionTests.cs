using System.Reflection;
using Dalamud.Plugin.Services;
using FF14Accessibility;
using FF14Accessibility.Services;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace Regression.Tests;

[Collection("Language")]
public sealed class ArmouryListSessionTests : IDisposable
{
    private readonly LanguageMode previous = Loc.Mode;
    public ArmouryListSessionTests() => Loc.Mode = LanguageMode.Russian;
    public void Dispose() => Loc.Mode = previous;

    [Fact]
    public void OpeningAndBrowsingReadsOneListAndConsumesBothNumlockVariants()
    {
        var f = new Fixture(); f.Tick();
        Assert.Contains("все предметы: 2", f.Speech.Last());
        Assert.DoesNotContain("фильтр", f.Speech.Last());
        f.Press(0x66); Assert.Contains("Body", f.Speech.Last()); Assert.False(f.Keys.Down[0x66]);
        f.Release(); f.Press(0x25); Assert.Contains("Axe", f.Speech.Last()); Assert.False(f.Keys.Down[0x25]);
    }

    [Theory]
    [InlineData(0x62, 5)] [InlineData(0x28, 5)]
    [InlineData(0x68, 10)] [InlineData(0x26, 10)]
    [InlineData(0x66, 1)] [InlineData(0x27, 1)]
    [InlineData(0x64, 11)] [InlineData(0x25, 11)]
    public void AllFourInventoryDirectionsSelectItemsInsteadOfSlotControls(int key, int index)
    {
        var f = new Fixture();
        f.Access.Items = ArmouryListModel.Containers.Select((type, i) =>
            new ArmouryListItem(type, i, (uint)(100 + i), 1, false, "instance " + i, "Item " + i)).ToList();
        f.Tick(); f.Press(key);
        Assert.Contains("Item " + index + ",", f.Speech.Last());
        Assert.DoesNotContain("фильтр", f.Speech.Last());
        Assert.DoesNotContain("категор", f.Speech.Last());
        Assert.False(f.Keys.Down[key]);
        Assert.True(f.Service.IsOpen);
        Assert.Equal(0, f.Access.CloseCount);
        f.Release(); f.Press(0x60);
        Assert.Equal(ArmouryListModel.Containers[index], f.Access.Requested!.Container);
        Assert.Equal(index, f.Access.Requested.Slot);
    }

    [Fact]
    public void OpeningImmediatelySelectsAnItemWithoutReadingTheControlInstructions()
    {
        var f = new Fixture(); f.Tick();
        Assert.Contains("Axe", f.Speech.Single());
        Assert.DoesNotContain(AccessibilityStrings.ArmouryListHelp, f.Speech.Single());
    }

    [Theory]
    [InlineData(false, false, false, "ArmouryBoard")]
    [InlineData(true, true, false, "ArmouryBoard")]
    [InlineData(true, false, true, "ArmouryBoard")]
    [InlineData(true, false, false, "SelectYesno")]
    [InlineData(true, false, false, "Inventory")]
    [InlineData(true, false, false, null)]
    public void OtherWindowsTypingAndFocusLossKeepTheirKeyboard(bool active, bool typing, bool menu, string? focus)
    {
        foreach (var key in new[] { 0x62, 0x67, 0x24, 0x69, 0x21 })
        {
            var f = new Fixture(); f.Tick(); f.Keys.Down[key] = true;
            f.Tick(active, typing, menu, focus);
            Assert.False(f.Service.HandleKeys(false)); Assert.True(f.Keys.Down[key]);
        }
    }

    [Fact]
    public void ModifiersAndEscapeStayWithTheGame()
    {
        var f = new Fixture(); f.Tick(); f.Keys.Down[0x62] = true; f.Keys.Down[0x1B] = true; f.Tick();
        Assert.False(f.Service.HandleKeys(true)); Assert.True(f.Keys.Down[0x62]); Assert.True(f.Keys.Down[0x1B]);
    }

    [Fact]
    public void IncompleteContainersDoNotProduceAPartialListOrAnAction()
    {
        var f = new Fixture(); f.Access.Ready = false; f.Tick();
        Assert.Contains("загружается", f.Speech.Last()); f.Press(0x60);
        Assert.Null(f.Access.Requested);
        f.Release(); f.Access.Ready = true; f.Now += TimeSpan.FromSeconds(1); f.Tick();
        Assert.Contains("все предметы: 2", f.Speech.Last());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void TemporaryUpdateKeepsTheSelectedPhysicalItemAndRestoresItsSpeech(bool manualRead)
    {
        var f = new Fixture(); f.Tick(); f.Press(0x66); f.Release();
        f.Access.Ready = false;
        if (manualRead) f.Service.ReadSelected(true);
        else { f.Now += TimeSpan.FromSeconds(1); f.Tick(); }
        Assert.Contains("загружается", f.Speech.Last());
        f.Press(0x60); Assert.Equal(0, f.Access.OpenCount); f.Release();
        f.Access.Ready = true; f.Now += TimeSpan.FromSeconds(1); f.Tick();
        Assert.Contains("Body", f.Speech.Last());
        f.Press(0x60);
        Assert.Equal(InventoryType.ArmoryBody, f.Access.Requested!.Container);
        Assert.Equal(17, f.Access.Requested.Slot);
    }

    [Fact]
    public void ItemRemovedDuringAnUpdateCannotReceiveTheRecoveryConfirmPress()
    {
        var f = new Fixture(); f.Tick(); f.Press(0x66); f.Release();
        f.Access.Ready = false; f.Now += TimeSpan.FromSeconds(1); f.Tick();
        f.Access.Items.RemoveAt(1); f.Access.Ready = true;
        f.Press(0x60);
        Assert.Equal(0, f.Access.OpenCount);
        Assert.Equal(AccessibilityStrings.ArmouryListItemChanged, f.Speech.Last());
        f.Release(); f.Press(0x60);
        Assert.Equal(100u, f.Access.Requested!.ItemId);
    }

    [Theory]
    [InlineData(0x64)] [InlineData(0x25)] [InlineData(0x68)] [InlineData(0x26)]
    [InlineData(0x62)] [InlineData(0x28)] [InlineData(0x24)] [InlineData(0x23)]
    [InlineData(0x66)] [InlineData(0x27)]
    [InlineData(0x67)] [InlineData(0x69)] [InlineData(0x21)]
    public void BrowsingWhileDataIsUnavailableCannotMoveTheRememberedSelection(int key)
    {
        var f = new Fixture();
        f.Access.Items.AddRange(Enumerable.Range(0, 10).Select(i => new ArmouryListItem(
            InventoryType.ArmoryBody, 18 + i, (uint)(102 + i), 1, false, "extra " + i, "Extra " + i)));
        f.Tick(); f.Press(0x66); f.Release();
        f.Access.Ready = false; f.Press(key); f.Release();
        f.Access.Ready = true; f.Now += TimeSpan.FromSeconds(1); f.Tick();
        Assert.Contains("Body", f.Speech.Last());
        f.Press(0x60); Assert.Equal(17, f.Access.Requested!.Slot);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void LoadingSpeechDoesNotRepeatOnEveryFrameWithoutANewKey(bool previouslyReady)
    {
        var f = new Fixture();
        if (previouslyReady) f.Tick();
        f.Access.Ready = false; f.Now += TimeSpan.FromSeconds(1); f.Tick();
        Assert.Contains("загружается", f.Speech.Last());
        var count = f.Speech.Count;
        for (var i = 0; i < 10; i++)
        {
            f.Now += TimeSpan.FromMilliseconds(300); f.Tick(); f.Service.HandleKeys(false);
        }
        Assert.Equal(count, f.Speech.Count);
    }

    [Fact]
    public void EmptyArmouryIsReadableButCannotOpenAnItemMenu()
    {
        var f = new Fixture(); f.Access.Items.Clear(); f.Tick(); f.Press(0x60);
        Assert.Null(f.Access.Requested); Assert.Contains(AccessibilityStrings.MenuEmpty, f.Speech.Last());
    }

    [Fact]
    public void NativeMenuOpensForTheExactSelectedContainerAndSlotAndGetsNewPresses()
    {
        var f = new Fixture(); f.Tick(); f.Press(0x66); f.Release(); f.Press(0x60);
        Assert.Equal(InventoryType.ArmoryBody, f.Access.Requested!.Container);
        Assert.Equal(17, f.Access.Requested.Slot);
        f.Access.ContextVisible = true; f.Keys.Down[0x60] = true; f.Tick(focus: "ContextMenu");
        Assert.False(f.Keys.Down[0x60]); Assert.False(f.Service.OwnsInput);
        f.Keys.Down[0x60] = false; f.Tick(focus: "ContextMenu");
        f.Keys.Down[0x60] = true; f.Tick(focus: "ContextMenu");
        Assert.True(f.Keys.Down[0x60]); Assert.False(f.Service.HandleKeys(false));
    }

    [Fact]
    public void WaitingForContextCannotActivateTheNativeSlotUnderneath()
    {
        var f = new Fixture(); f.Tick(); f.Press(0x60); f.Release();
        Assert.True(f.Service.OwnsInput); Assert.False(f.Service.IsBrowsing);
        f.Press(0x62); Assert.False(f.Keys.Down[0x62]); Assert.Equal(1, f.Access.OpenCount);
    }

    [Fact]
    public void AfterNativeActionTheListRefreshesAndKeepsTheNearestRemainingRow()
    {
        var f = new Fixture(); f.Tick(); f.Press(0x60);
        f.Access.ContextVisible = true; f.Tick(focus: "ContextMenu");
        f.Access.Items.RemoveAt(0); f.Access.ContextVisible = false; f.Release();
        Assert.True(f.Service.IsBrowsing); Assert.Contains("Body", f.Speech.Last());
        f.Press(0x60); Assert.Equal(17, f.Access.Requested!.Slot);
    }

    [Theory]
    [InlineData("replacement")] [InlineData("logout")] [InlineData("zone")]
    [InlineData("closed")] [InlineData("newwindow")]
    public void ChangedStateCancelsOurPendingNativeMenu(string change)
    {
        var f = new Fixture(); f.Tick(); f.Press(0x60); f.Access.ContextVisible = true; f.Tick(focus: "ContextMenu");
        switch (change)
        {
            case "replacement": f.Access.Items[0] = f.Access.Items[0] with { Instance = "new" }; break;
            case "logout": f.LoggedIn = false; break;
            case "zone": f.Territory++; break;
            case "closed": f.Access.Window = default; break;
            case "newwindow": f.Access.Window = new(200, 138); break;
        }
        f.Tick(); Assert.Equal(1, f.Access.CancelCount);
    }

    [Fact]
    public void ContextTimeoutRecoversAndReportsFailure()
    {
        var f = new Fixture(); f.Tick(); f.Press(0x60); f.Release();
        f.Now += TimeSpan.FromSeconds(3); f.Tick();
        Assert.True(f.Service.IsBrowsing); Assert.Equal(AccessibilityStrings.ArmouryListMenuFailed, f.Speech.Last());
    }

    [Fact]
    public void DescriptionRechecksTheLiveItemInsteadOfReadingTheRemovedRow()
    {
        var f = new Fixture(); f.Tick(); f.Access.Items.RemoveAt(0); f.Service.ReadSelected(true);
        Assert.Contains("Body", f.Speech.Last()); Assert.Contains("description 101", f.Speech.Last());
        Assert.DoesNotContain("description 100", f.Speech.Last());
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void AChangedSelectionCannotUseTheSameConfirmPressForADifferentItem(bool removed)
    {
        var f = new Fixture(); f.Tick();
        if (removed) f.Access.Items.RemoveAt(0);
        else f.Access.Items[0] = f.Access.Items[0] with { Instance = "replaced" };
        f.Press(0x60);
        Assert.Equal(0, f.Access.OpenCount);
        Assert.Equal(AccessibilityStrings.ArmouryListItemChanged, f.Speech.Last());
        f.Release(); f.Press(0x60); Assert.Equal(1, f.Access.OpenCount);
    }

    [Fact]
    public void DescriptionIncludesConditionAndGearsetWarningForThisExactInstance()
    {
        var f = new Fixture();
        f.Access.Items[0] = f.Access.Items[0] with { Detail = "В комплекте. Прочность 37%." };
        f.Tick(); f.Service.ReadSelected(true);
        Assert.Contains("Прочность 37%", f.Speech.Last()); Assert.Contains("В комплекте", f.Speech.Last());
        f.Press(0x66); f.Release(); f.Service.ReadSelected(true);
        Assert.DoesNotContain("Прочность 37%", f.Speech.Last());
    }

    [Theory]
    [InlineData(0x6E)] [InlineData(0x2E)]
    public void DeleteClosesTheArmouryAndReopeningStartsAFreshList(int key)
    {
        var f = new Fixture(); f.Tick(); f.Press(key);
        Assert.False(f.Service.IsOpen); Assert.Equal(1, f.Access.CloseCount);
        f.Access.Window = new(100, 137); f.Release(); Assert.True(f.Service.IsBrowsing);
        Assert.Contains("все предметы: 2", f.Speech.Last());
    }

    [Theory]
    [InlineData(1908u, false)]
    [InlineData(1001908u, true)]
    [InlineData(501908u, false)]
    public void ItemFromPlayerLogUsesBaseRowForDescriptionAndRawIdForNativeContext(uint rawId, bool hq)
    {
        var f = new Fixture();
        f.Access.Items = [new(InventoryType.ArmoryMainHand, 9, rawId, 1, hq,
            "logged instance", "Лук")];
        f.Tick(); f.Service.ReadSelected(true);
        Assert.Contains("description 1908", f.Speech.Last());
        if (rawId != 1908) Assert.DoesNotContain("description " + rawId, f.Speech.Last());
        f.Press(0x60);
        Assert.Equal(rawId, f.Access.Requested!.ItemId);
        Assert.Equal(9, f.Access.Requested.Slot);
        Assert.Equal(hq, f.Access.Requested.HighQuality);
    }

    [Theory]
    [InlineData(0x67, 11)] [InlineData(0x24, 11)]
    [InlineData(0x69, 1)] [InlineData(0x21, 1)]
    public void CategoryKeysWorkWithBothNumlockStatesAndKeepTheExactHqItem(int key, int category)
    {
        var f = new Fixture();
        f.Access.Items = ArmouryListModel.Containers.SelectMany((type, i) => new[] {
            new ArmouryListItem(type, 9, 1001908, 1, true, "HQ " + i, "First " + i),
            new ArmouryListItem(type, 20, 1908, 1, false, "NQ " + i, "Second " + i) }).ToList();
        f.Tick(); f.Press(0x66); f.Release(); f.Press(key);
        Assert.Contains("First " + category + ",", f.Speech.Last());
        Assert.Contains(ArmouryListModel.CategoryName(ArmouryListModel.Containers[category]), f.Speech.Last());
        Assert.False(f.Keys.Down[key]);
        var speechCount = f.Speech.Count;
        for (var i = 0; i < 3; i++)
        {
            f.Keys.Down[key] = true; f.Tick(); f.Service.HandleKeys(false);
            Assert.False(f.Keys.Down[key]);
        }
        Assert.Equal(speechCount, f.Speech.Count);
        f.Release(); f.Service.ReadSelected(true);
        Assert.Contains("description 1908", f.Speech.Last());
        f.Press(0x60);
        Assert.Equal(ArmouryListModel.Containers[category], f.Access.Requested!.Container);
        Assert.Equal(9, f.Access.Requested.Slot);
        Assert.Equal(1001908u, f.Access.Requested.ItemId);
    }

    [Theory]
    [InlineData(0x65)] [InlineData(0x0C)]
    public void ManualParametersAreNotInterruptedByUnspokenInstanceUpdates(int key)
    {
        var f = new Fixture();
        f.Access.Items[0] = f.Access.Items[0] with { Label = "Лук, Физический урон 17, Ловкость плюс 2" };
        f.Tick(); f.Press(key); f.Release();
        Assert.Contains("Физический урон 17", f.Speech.Last());
        Assert.Contains("description 100", f.Speech.Last());
        var spoken = f.Speech.Count;
        f.Access.Items[0] = f.Access.Items[0] with { Instance = "spiritbond updated" };
        f.Now += TimeSpan.FromSeconds(1); f.Tick();
        Assert.Equal(spoken, f.Speech.Count);
        // Speech stability must not weaken the native instance check.
        f.Access.Items[0] = f.Access.Items[0] with { Instance = "replaced again" };
        f.Press(0x60);
        Assert.Equal(0, f.Access.OpenCount);
        Assert.Equal(AccessibilityStrings.ArmouryListItemChanged, f.Speech.Last());
    }

    [Fact]
    public void ChangedSpokenParametersStillAnnounceTheCurrentItem()
    {
        var f = new Fixture(); f.Tick();
        f.Access.Items[0] = f.Access.Items[0] with { Label = "Лук, Физический урон 17" };
        f.Now += TimeSpan.FromSeconds(1); f.Tick();
        Assert.Contains("Физический урон 17", f.Speech.Last());
        f.Press(0x66); f.Release(); f.Service.ReadSelected(true);
        Assert.DoesNotContain("Физический урон 17", f.Speech.Last());
    }

    private sealed class Fixture
    {
        internal readonly Access Access = new();
        internal readonly ShopQuantityTests.KeyStateProxy Keys;
        internal readonly List<string> Speech = [];
        internal readonly ArmouryListService Service;
        internal DateTime Now = new(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc);
        internal bool LoggedIn = true;
        internal uint Territory = 133;
        internal Fixture()
        {
            var keys = DispatchProxy.Create<IKeyState, ShopQuantityTests.KeyStateProxy>();
            Keys = (ShopQuantityTests.KeyStateProxy)(object)keys;
            var log = NavigationStopTests.Proxy.Of<IPluginLog>(_ => null);
            Service = new(Access, keys, log, () => LoggedIn, () => Territory,
                id => "description " + id, Speech.Add, () => Now);
        }
        internal void Tick(bool active = true, bool typing = false, bool menu = false, string? focus = "ArmouryBoard")
            => Service.Update(active, typing, menu, focus);
        internal void Press(int key) { Keys.Down[key] = true; Tick(); Service.HandleKeys(false); }
        internal void Release() { Array.Clear(Keys.Down); Tick(); }
    }

    private sealed class Access : IArmouryListAccess
    {
        public ArmouryListWindow Window { get; set; } = new(100, 137);
        public bool ContextVisible { get; set; }
        internal bool Ready = true;
        internal int OpenCount, CancelCount, CloseCount;
        internal ArmouryListItem? Requested;
        internal List<ArmouryListItem> Items = [
            new(InventoryType.ArmoryMainHand, 8, 100, 1, false, "a", "Axe"),
            new(InventoryType.ArmoryBody, 17, 101, 1, true, "b", "Body")];
        public bool TryCollect(out List<ArmouryListItem> items) { items = [.. Items]; return Ready; }
        public bool OpenContext(ArmouryListWindow window, ArmouryListItem expected)
        { Requested = expected; OpenCount++; return Ready && Window == window && Matches(expected); }
        public bool ContextMatches(ArmouryListWindow window, ArmouryListItem expected)
            => Window == window && Requested == expected && Matches(expected);
        private bool Matches(ArmouryListItem expected)
            => Items.Any(i => ArmouryListModel.CanAct(expected, i));
        public void CloseContext(ArmouryListWindow window, ArmouryListItem expected)
        { if (Requested != expected) return; CancelCount++; ContextVisible = false; Requested = null; }
        public void Close(ArmouryListWindow window) { if (window == Window) { CloseCount++; Window = default; } }
    }
}
