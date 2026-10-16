using System.Runtime.InteropServices;
using System.Text.Json;
using FF14Accessibility;
using FF14Accessibility.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Regression.Tests;

[Collection("Language")]
public unsafe sealed class SystemVolumeTests : IDisposable
{
    private readonly List<nint> allocations = [];
    private readonly Dictionary<nint, string> texts = [];
    private readonly HashSet<nint> hidden = [];
    private readonly LanguageMode previous = Loc.Mode;
    public SystemVolumeTests() => Loc.Mode = LanguageMode.Russian;
    public void Dispose() { Loc.Mode = previous; foreach (var p in allocations) NativeMemory.Free((void*)p); }
    private T* Allocate<T>() where T : unmanaged { var p = (T*)NativeMemory.AllocZeroed((nuint)sizeof(T)); allocations.Add((nint)p); return p; }
    private AtkUnitBase* Layout()
    {
        using var stream = typeof(SystemVolumeTests).Assembly.GetManifestResourceStream("Regression.Tests.Fixtures.system-volume-layout.json")!;
        using var json = JsonDocument.Parse(stream);
        var nodes = json.RootElement.GetProperty("Nodes");
        var addon = Allocate<AtkUnitBase>();
        addon->UldManager.NodeListCount = (ushort)nodes.GetArrayLength();
        var list = (AtkResNode**)NativeMemory.AllocZeroed((nuint)(nodes.GetArrayLength() * sizeof(nint)));
        allocations.Add((nint)list); addon->UldManager.NodeList = list;
        var byId = new Dictionary<uint,nint>(); var index = 0;
        foreach (var row in nodes.EnumerateArray())
        {
            var n = Allocate<AtkResNode>(); n->NodeId = row.GetProperty("Id").GetUInt32();
            n->Type = (NodeType)row.GetProperty("Type").GetUInt16();
            list[index++] = n; byId[n->NodeId] = (nint)n;
            if (row.GetProperty("Text").ValueKind != JsonValueKind.Null) texts[(nint)n] = row.GetProperty("Text").GetString()!;
        }
        foreach (var row in nodes.EnumerateArray())
            if (byId.TryGetValue(row.GetProperty("Parent").GetUInt32(), out var parent))
                ((AtkResNode*)byId[row.GetProperty("Id").GetUInt32()])->ParentNode = (AtkResNode*)parent;
        return addon;
    }
    private static AtkResNode* Node(AtkUnitBase* addon, uint id)
    { for (var i = 0; i < addon->UldManager.NodeListCount; i++) if (addon->UldManager.NodeList[i]->NodeId == id) return addon->UldManager.NodeList[i]; return null; }
    private bool Label(AtkUnitBase* addon, AtkResNode* slider, out string text)
        => SystemVolumeNodes.TryLabel(addon, slider, p => !hidden.Contains(p), p => texts.GetValueOrDefault(p, ""), out text);

    [Theory]
    [InlineData(113, "Общая громкость")]
    [InlineData(117, "Громкость музыки")]
    [InlineData(121, "Громкость звуковых эффектов")]
    [InlineData(125, "Громкость голосов персонажей")]
    [InlineData(129, "Громкость системных звуков")]
    [InlineData(133, "Громкость окружающих звуков")]
    [InlineData(137, "Громкость музыкальных инструментов")]
    [InlineData(143, "Громкость эффектов своего персонажа")]
    [InlineData(146, "Громкость эффектов группы")]
    [InlineData(149, "Громкость эффектов других игроков")]
    public void RealLayoutReadsTheRightHandChannelInsteadOfTheGenericHeading(int id, string expected)
    { var addon = Layout(); Assert.True(Label(addon, Node(addon,(uint)id), out var label)); Assert.Equal(expected,label); }

    [Fact]
    public void HiddenMissingEmptyAndForeignRowsDoNotBorrowAnotherChannelsLabel()
    {
        var addon = Layout(); var slider = Node(addon,117); var label = Node(addon,119);
        hidden.Add((nint)label); Assert.False(Label(addon,slider,out _)); hidden.Clear();
        hidden.Add((nint)slider); Assert.False(Label(addon,slider,out _)); hidden.Clear();
        texts[(nint)label] = ""; Assert.False(Label(addon,slider,out _)); texts[(nint)label] = "BGM";
        label->ParentNode = Node(addon,112); Assert.False(Label(addon,slider,out _)); label->ParentNode = Node(addon,116);
        slider->ParentNode = Node(addon,112); Assert.False(Label(addon,slider,out _));
        Assert.False(Label(addon,Node(addon,111),out _));
    }
    [Theory]
    [InlineData(LanguageMode.English)] [InlineData(LanguageMode.German)]
    public void OtherLanguagesKeepTheLiveGameLabel(LanguageMode mode)
    { Loc.Mode = mode; var addon = Layout(); texts[(nint)Node(addon,119)] = "Live music label"; Assert.True(Label(addon,Node(addon,117),out var label)); Assert.Equal("Live music label",label); }

    [Theory]
    [InlineData("0",true,0)] [InlineData("1",true,1)] [InlineData("37",true,37)] [InlineData("99",true,99)]
    [InlineData("100",true,100)] [InlineData("101",false,0)] [InlineData("9999",false,0)] [InlineData("",false,0)]
    public void ExactValuesIncludeMuteAndRejectOutOfRangeWithoutClamping(string text, bool valid, int expected)
    {
        var edit = new SystemVolumeEdit(new(1,2,3,"Music",53)); edit.Clear();
        foreach(var c in text) edit.Digit(c-'0');
        Assert.Equal(valid,edit.TryValue(out var value)); if(valid) Assert.Equal(expected,value);
        Assert.Equal(53,edit.Original.Value);
    }
    [Fact]
    public void FirstDigitReplacesTheOldValueAndCorrectionsDoNotWriteToTheGame()
    {
        var edit = new SystemVolumeEdit(new(1,2,3,"Music",53)); edit.Digit(3); edit.Digit(7);
        Assert.True(edit.TryValue(out var value)); Assert.Equal(37,value); edit.Backspace(); edit.Digit(8);
        Assert.True(edit.TryValue(out value)); Assert.Equal(38,value); Assert.Equal(53,edit.Original.Value);
    }
    [Fact]
    public void ReopeningChangingFocusOrANativeValueChangeInvalidatesAnOldDraft()
    {
        var original = new SystemVolumeEdit.Target(1,2,3,"Music",53); var edit = new SystemVolumeEdit(original);
        Assert.True(edit.Matches(original));
        foreach(var changed in new[] { original with { Addon=4 },original with { Control=4 },original with { Slider=4 },
            original with { Label="Voice" }, original with { Value=52 },default }) Assert.False(edit.Matches(changed));
    }
    [Theory]
    [InlineData(0)] [InlineData(37)] [InlineData(100)]
    public void ApplyWritesTheExactValueToTheSameSliderAndRequiresReadback(int requested)
    {
        var live = new SystemVolumeEdit.Target(1,2,3,"Music",53); var edit=new SystemVolumeEdit(live);
        edit.Clear(); foreach(var c in requested.ToString()) edit.Digit(c-'0');
        var writes=0;
        Assert.True(edit.Apply(()=>live,(p,value)=> { Assert.Equal(3,p); writes++; live=live with { Value=value }; },out var applied));
        Assert.Equal(1,writes); Assert.Equal(requested,applied); Assert.Equal(requested,live.Value);
    }
    [Fact]
    public void InvalidOrStaleInputNeverWritesAndAnIgnoredWriteIsNotReportedAsSuccess()
    {
        var original=new SystemVolumeEdit.Target(1,2,3,"Music",53); var edit=new SystemVolumeEdit(original);
        edit.Digit(3); edit.Digit(7); var writes=0;
        Assert.False(edit.Apply(()=>null,(_,_)=>writes++,out _));
        Assert.False(edit.Apply(()=>original with { Slider=4 },(_,_)=>writes++,out _)); Assert.Equal(0,writes);
        Assert.False(edit.Apply(()=>original,(_,_)=>writes++,out _)); Assert.Equal(1,writes);
        edit.Clear(); edit.Digit(9); edit.Digit(9); edit.Digit(9);
        Assert.False(edit.Apply(()=>original,(_,_)=>writes++,out _)); Assert.Equal(1,writes);
    }
}
