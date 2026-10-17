using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using FF14Accessibility.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Regression.Tests;

public sealed unsafe class NativeListRowsTests : IDisposable
{
    private readonly List<nint> _memory = [];
    private readonly AtkComponentList* _list;
    private readonly AtkComponentListItemRenderer* _first;
    private readonly AtkComponentListItemRenderer* _second;
    private readonly UIReaderService _reader = (UIReaderService)RuntimeHelpers.GetUninitializedObject(typeof(UIReaderService));

    public NativeListRowsTests()
    {
        _list = (AtkComponentList*)Allocate(sizeof(AtkComponentList));
        _list->ListLength = 100;
        _list->AllocatedItemRendererListLength = 2;
        _list->ItemRendererList = (AtkComponentList.ListItem*)Allocate(2 * sizeof(AtkComponentList.ListItem));
        _first = Row(40, "Сорок первый пункт");
        _second = Row(41, "Сорок второй пункт");
        _list->ItemRendererList[0].AtkComponentListItemRenderer = _first;
        _list->ItemRendererList[1].AtkComponentListItemRenderer = _second;
    }

    [Fact]
    public void ScrolledListSpeaksLogicalRowInsteadOfTreatingItsIndexAsARendererSlot()
    {
        Assert.Equal("Сорок второй пункт", Read("ReadListItemText", 41));
        Assert.Equal("Сорок первый пункт", Read("ReadListItemText", 40));
        Assert.Empty(Read("ReadListItemText", 0));
        Assert.Empty(Read("ReadListItemText", 99));
    }

    [Fact]
    public void RecyclingTheSamePointerImmediatelyChangesItsSpokenIdentity()
    {
        Assert.Equal((nint)_second, (nint)NativeListRows.Renderer(_list, 41));
        _second->ListItemIndex = 73;
        Assert.Equal(0, (nint)NativeListRows.Renderer(_list, 41));
        Assert.Equal((nint)_second, (nint)NativeListRows.Renderer(_list, 73));
        Assert.Equal("Сорок второй пункт", Read("ReadListItemText", 73));
    }

    [Fact]
    public void HighlightedRendererReportsItsLogicalIndexInsteadOfItsSlot()
    {
        _list->ItemRendererList[1].IsHighlighted = true;
        var state = ((int, int, int, int, int, string))typeof(UIReaderService)
            .GetMethod("ReadListIndices", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [Pointer.Box(_list, typeof(AtkComponentList*))])!;
        Assert.Equal("41", state.Item6);
    }

    [Fact]
    public void JournalTreeListKeepsItsMeasuredRendererSlotMappingWhenBaseLengthIsZero()
    {
        _list->ListLength = 0;
        var info = (AtkUldComponentInfo*)Allocate(sizeof(AtkUldComponentInfo));
        info->ComponentType = ComponentType.TreeList;
        _list->UldManager.Objects = (AtkUldObjectInfo*)info;
        Assert.Equal("Сорок второй пункт", Read("ReadListItemText", 1));
        Assert.Equal(1, NativeListRows.IndexAtSlot(_list, 1));
        Assert.Empty(Read("ReadListItemText", 41));
    }

    [Theory]
    [InlineData(-1)] [InlineData(100)]
    public void InvalidLogicalIdentityIsNeverReplacedByThePhysicalSlot(int index)
    {
        _second->ListItemIndex = index;
        Assert.Equal(-1, NativeListRows.IndexAtSlot(_list, 1));
        Assert.Equal(0, (nint)NativeListRows.Renderer(_list, 1));
    }

    [Fact]
    public void TwoDifferentRenderersClaimingTheSameRowAreRejected()
    {
        _first->ListItemIndex = 41;
        Assert.Equal(0, (nint)NativeListRows.Renderer(_list, 41));
        Assert.Empty(Read("ReadListItemText", 41));
    }

    [Fact]
    public void EmptyAndMissingRendererArraysStaySilent()
    {
        _list->ListLength = 0;
        Assert.Empty(Read("ReadListItemText", 0));
        _list->ItemRendererList = null;
        Assert.Empty(Read("ReadListItemText", 0));
        Assert.Equal(-1, NativeListRows.IndexAtSlot(_list, 0));
    }

    [Fact]
    public void GenericReadingKeepsOnlyVisibleCleanTextAndPreservesOneCharacterLabels()
    {
        _second->UldManager.NodeList[0] = (AtkResNode*)Text("\u0002H\u0002\u0001\u0003W");
        Assert.Equal("W", Read("ReadListItemText", 41));
        _second->UldManager.NodeList[0]->NodeFlags = 0;
        Assert.Empty(Read("ReadListItemText", 41));
    }

    [Fact]
    public void KeybindReaderUsesTheScrolledCommandInsteadOfAnotherVisibleSlot()
    {
        _first->UldManager.NodeList[0]->NodeId = 2;
        _second->UldManager.NodeList[0]->NodeId = 2;
        Assert.Equal("Сорок второй пункт", Read("ReadConfigKeybindRow", 41));
        Assert.Empty(Read("ReadConfigKeybindRow", 1));
    }

    [Fact]
    public void SealShopReaderUsesNamePriceAndOwnedAmountFromOneScrolledRow()
    {
        _second->UldManager.NodeListCount = 3;
        _second->UldManager.NodeList = (AtkResNode**)Allocate(3 * sizeof(nint));
        var name = Text("Предмет для обмена"); name->AtkResNode.NodeId = 4;
        var price = Text("123"); price->AtkResNode.NodeId = 7;
        var owned = Text("17"); owned->AtkResNode.NodeId = 10;
        _second->UldManager.NodeList[0] = (AtkResNode*)name;
        _second->UldManager.NodeList[1] = (AtkResNode*)price;
        _second->UldManager.NodeList[2] = (AtkResNode*)owned;
        var spoken = Read("ReadGrandCompanyRow", 41);
        Assert.Contains("Предмет для обмена", spoken);
        Assert.Contains("123", spoken); Assert.Contains("17", spoken);
        Assert.DoesNotContain("Сорок первый", spoken);
        Assert.Empty(Read("ReadGrandCompanyRow", 1));
    }

    private string Read(string method, int index) => (string)typeof(UIReaderService)
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(_reader, [Pointer.Box(_list, typeof(AtkComponentList*)), index])!;

    private AtkComponentListItemRenderer* Row(int index, string label)
    {
        var row = (AtkComponentListItemRenderer*)Allocate(sizeof(AtkComponentListItemRenderer));
        row->ListItemIndex = index;
        row->UldManager.NodeListCount = 1;
        row->UldManager.NodeList = (AtkResNode**)Allocate(sizeof(nint));
        row->UldManager.NodeList[0] = (AtkResNode*)Text(label);
        return row;
    }
    private AtkTextNode* Text(string text)
    {
        var node = (AtkTextNode*)Allocate(sizeof(AtkTextNode));
        node->AtkResNode.Type = NodeType.Text;
        node->AtkResNode.NodeFlags = NodeFlags.Visible;
        var bytes = Encoding.UTF8.GetBytes(text + "\0"); var buffer = Allocate(bytes.Length);
        Marshal.Copy(bytes, 0, buffer, bytes.Length);
        node->NodeText.StringPtr = (byte*)buffer;
        node->NodeText.BufSize = node->NodeText.BufUsed = bytes.Length;
        return node;
    }
    private nint Allocate(int size)
    {
        var address = (nint)NativeMemory.AllocZeroed((nuint)size); _memory.Add(address); return address;
    }
    public void Dispose() { foreach (var address in _memory) NativeMemory.Free((void*)address); }
}
