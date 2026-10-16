using System.Runtime.InteropServices;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using FF14Accessibility.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Regression.Tests;

public sealed unsafe class NativeControlScopeTests : IDisposable
{
    private readonly List<nint> _memory = [];

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void CollisionFocusResolvesItsOwnControlEvenWithoutAParentChain(bool parentChain)
    {
        var window = Window();
        var left = Component(); var right = Component();
        var focus = Node(); focus->NodeId = 4;
        var otherFocus = Node(); otherFocus->NodeId = 4;
        Add(left, (nint)focus); Add(right, (nint)otherFocus);
        if (parentChain) { focus->ParentNode = (AtkResNode*)left; otherFocus->ParentNode = (AtkResNode*)right; }
        Add(window, (nint)left, (nint)right);
        Assert.True(NativeControlScope.Owns(window, focus));
        Assert.Equal((nint)left, (nint)NativeControlScope.Control(window, focus));
        Assert.Equal((nint)right, (nint)NativeControlScope.Control(window, otherFocus));
        var leftAddress = (nint)left;
        var rightAddress = (nint)right;
        Assert.Equal("ОК", NativeControlScope.Text(window, focus, p => p == leftAddress ? "ОК" : "Отмена"));
        Assert.Equal("Отмена", NativeControlScope.Text(window, otherFocus, p => p == rightAddress ? "Отмена" : "ОК"));
    }

    [Fact]
    public void EmptyControlDoesNotBorrowItsNeighbourOrSharedContainerLabel()
    {
        var window = Window(); var wrapper = Component();
        var left = Component(); var right = Component(); var focus = Node();
        focus->ParentNode = (AtkResNode*)left;
        left->AtkResNode.ParentNode = (AtkResNode*)wrapper;
        right->AtkResNode.ParentNode = (AtkResNode*)wrapper;
        Add(left, (nint)focus); Add(wrapper, (nint)left, (nint)right);
        Add(window, (nint)wrapper);
        var leftAddress = (nint)left;
        Assert.Empty(NativeControlScope.Text(window, focus, p => p == leftAddress ? "" : "Начать, Отказаться"));
    }

    [Fact]
    public void ForeignWindowAndRepeatedIdsNeverSupplyAnOwner()
    {
        var window = Window(); var button = Component(); var foreign = Component();
        button->AtkResNode.NodeId = foreign->AtkResNode.NodeId = 9;
        var focus = Node(); focus->ParentNode = (AtkResNode*)foreign;
        Add(window, (nint)button);
        Assert.False(NativeControlScope.Owns(window, focus));
        Assert.Equal(0, (nint)NativeControlScope.Control(window, focus));
    }

    [Fact]
    public void SharedFocusInUnrelatedControlsIsRejectedAsAmbiguous()
    {
        var window = Window(); var left = Component(); var right = Component(); var focus = Node();
        Add(left, (nint)focus); Add(right, (nint)focus); Add(window, (nint)left, (nint)right);
        Assert.Equal(0, (nint)NativeControlScope.Control(window, focus));
    }

    [Fact]
    public void CyclicComponentListsAreBoundedAndCannotReachANeighbour()
    {
        var window = Window(); var control = Component(); var focus = Node();
        Add(control, (nint)control, (nint)focus); Add(window, (nint)control);
        Assert.Equal((nint)control, (nint)NativeControlScope.Control(window, focus));
        Assert.Equal(0, (nint)NativeControlScope.Control(window, Node()));
    }

    [Fact]
    public void DrawDisabledButtonsCannotBeActivated()
    {
        var control = Node(); control->NodeFlags = NodeFlags.Visible | NodeFlags.Enabled;
        Assert.True(NativeDialogButtons.Available(control));
        control->IsDrawDisabled = true;
        Assert.False(NativeDialogButtons.Available(control));
    }

    [Theory]
    [InlineData("W", true, "W")]
    [InlineData("1", true, "1")]
    [InlineData("Скрытая подпись", false, "")]
    [InlineData("\u0002H\u0002\u0001\u0003ОК", true, "ОК")]
    public void GenericFocusReadsVisibleSingleCharacterAndFormattedLabels(string raw, bool visible, string expected)
    {
        var text = (AtkTextNode*)Allocate(sizeof(AtkTextNode));
        text->AtkResNode.Type = NodeType.Text;
        text->AtkResNode.NodeFlags = visible ? NodeFlags.Visible : 0;
        var bytes = Encoding.UTF8.GetBytes(raw + "\0"); var buffer = Allocate(bytes.Length);
        Marshal.Copy(bytes, 0, buffer, bytes.Length);
        text->NodeText.StringPtr = (byte*)buffer; text->NodeText.BufSize = text->NodeText.BufUsed = bytes.Length;
        var reader = (UIReaderService)RuntimeHelpers.GetUninitializedObject(typeof(UIReaderService));
        var method = typeof(UIReaderService).GetMethod("GetVisibleTextFromNodeTree", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.Equal(expected, method.Invoke(reader, [Pointer.Box(text, typeof(AtkResNode*)), 0]));
    }

    private AtkResNode* Node() => (AtkResNode*)Allocate(sizeof(AtkResNode));
    private AtkUnitBase* Window() => (AtkUnitBase*)Allocate(sizeof(AtkUnitBase));
    private AtkComponentNode* Component()
    {
        var node = (AtkComponentNode*)Allocate(sizeof(AtkComponentNode));
        node->AtkResNode.Type = (NodeType)1001;
        node->Component = (AtkComponentBase*)Allocate(sizeof(AtkComponentButton));
        return node;
    }
    private void Add(AtkComponentNode* node, params nint[] children)
    {
        node->Component->UldManager.NodeList = Nodes(children);
        node->Component->UldManager.NodeListCount = (ushort)children.Length;
    }
    private void Add(AtkUnitBase* window, params nint[] children)
    {
        window->UldManager.NodeList = Nodes(children);
        window->UldManager.NodeListCount = (ushort)children.Length;
    }
    private AtkResNode** Nodes(nint[] children)
    {
        var nodes = (AtkResNode**)Allocate(children.Length * sizeof(nint));
        for (var i = 0; i < children.Length; i++) nodes[i] = (AtkResNode*)children[i];
        return nodes;
    }
    private nint Allocate(int size)
    {
        var address = (nint)NativeMemory.AllocZeroed((nuint)size); _memory.Add(address); return address;
    }
    public void Dispose() { foreach (var address in _memory) NativeMemory.Free((void*)address); }
}
