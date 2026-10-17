using System.Reflection;
using System.Runtime.InteropServices;
using FF14Accessibility;
using FF14Accessibility.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Regression.Tests;

public sealed unsafe class SelectionDialogTests : IDisposable
{
    private readonly List<nint> _memory = [];
    private readonly AddonSelectIconString* _storage;
    private AtkUnitBase* Addon => (AtkUnitBase*)_storage;
    private readonly AtkComponentList* _list;
    private readonly AtkComponentNode* _listOwner;
    private readonly AtkResNode* _focus;
    private readonly AtkComponentNode* _rowOwner;
    private readonly AtkComponentListItemRenderer* _row;

    public SelectionDialogTests()
    {
        _storage = (AddonSelectIconString*)Allocate(Math.Max(sizeof(AddonCutSceneSelectString),
            Math.Max(sizeof(AddonSelectString), sizeof(AddonSelectIconString))));
        Addon->IsVisible = true;
        Addon->RootNode = Node();
        _list = (AtkComponentList*)Allocate(sizeof(AtkComponentList));
        _listOwner = Component((AtkComponentBase*)_list);
        _list->OwnerNode = _listOwner;
        _list->ListLength = 6;
        _list->SelectedItemIndex = -1;
        _list->HoveredItemIndex2 = 0; // stale hover must not outrank actual focus
        _list->AllocatedItemRendererListLength = 1; // one recycled visible slot
        _list->ItemRendererList = (AtkComponentList.ListItem*)Allocate(sizeof(AtkComponentList.ListItem));
        _row = (AtkComponentListItemRenderer*)Allocate(sizeof(AtkComponentListItemRenderer));
        _row->ListItemIndex = 4; // logical item 4, renderer slot 0
        _rowOwner = Component((AtkComponentBase*)_row);
        _row->OwnerNode = _rowOwner;
        _list->ItemRendererList[0].AtkComponentListItemRenderer = _row;
        _focus = Node();
        Children(&_row->UldManager, (nint)_focus);
        Children(&_list->UldManager, (nint)_rowOwner);
        Children(&Addon->UldManager, (nint)_listOwner);
    }

    [Theory]
    [InlineData("SelectString", true)] [InlineData("SelectString", false)]
    [InlineData("SelectIconString", true)] [InlineData("SelectIconString", false)]
    [InlineData("CutSceneSelectString", true)] [InlineData("CutSceneSelectString", false)]
    public void FocusedAnswerWinsOverUnsetSelectionStaleHoverAndRecycledRendererSlot(string name, bool parents)
    {
        Menu(name);
        if (parents) _focus->ParentNode = (AtkResNode*)_rowOwner;
        Assert.True(NativeSelectionDialog.TryResolve(Addon, _focus, out var choice));
        Assert.Equal(4, choice.Index);
        Assert.Equal((nint)_list, choice.List);
        Assert.True(choice.Available);
        Assert.Equal(-1, _list->SelectedItemIndex); // resolving is not selecting or clicking
    }

    [Theory]
    [InlineData("SelectString")] [InlineData("SelectIconString")] [InlineData("CutSceneSelectString")]
    public void DispatchesOnlyOnceEvenWhenNativeActionClosesAndReplacesTheWindow(string name)
    {
        Menu(name);
        var calls = 0;
        Assert.Equal(NativeSelectionDialog.Result.Requested,
            NativeSelectionDialog.Confirm(Addon, _focus, choice =>
            {
                calls++;
                Assert.Equal(4, choice.Index);
                Addon->IsVisible = false;
                _list->ItemRendererList = null;
                _storage->PopupMenu.List = null;
            }));
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("SelectString")] [InlineData("SelectIconString")] [InlineData("CutSceneSelectString")]
    public void DisabledFocusedAnswerIsOwnedButNeverInvokesTheAction(string name)
    {
        Menu(name);
        _list->ItemRendererList[0].IsDisabled = true;
        Assert.True(NativeSelectionDialog.TryResolve(Addon, _focus, out var choice));
        Assert.False(choice.Available);
        Assert.Equal(NativeSelectionDialog.Result.Unavailable,
            NativeSelectionDialog.Confirm(Addon, _focus, _ => Assert.Fail("Disabled answer dispatched")));
    }

    [Theory]
    [InlineData("hidden")] [InlineData("blocked")] [InlineData("empty")]
    [InlineData("unfilled")] [InlineData("foreign-focus")] [InlineData("foreign-list")]
    [InlineData("wrong-index")] [InlineData("wrong-window")] [InlineData("missing-renderers")]
    public void IncompleteForeignAndObscuredChoicesDoNotOwnConfirmation(string fault)
    {
        Menu("SelectString");
        var focus = _focus;
        switch (fault)
        {
            case "hidden": Addon->IsVisible = false; break;
            case "blocked": Addon->NumBlockingAddons = 1; break;
            case "empty": _list->ListLength = 0; break;
            case "unfilled": ((AddonSelectString*)Addon)->PopupMenu.EntryCount = 2; break;
            case "foreign-focus": focus = Node(); focus->NodeId = _focus->NodeId; break;
            case "foreign-list": Children(&Addon->UldManager, (nint)Node()); break;
            case "wrong-index": _row->ListItemIndex = 6; break;
            case "wrong-window": Addon->NameString = "GrandCompanyRank"; break;
            case "missing-renderers": _list->ItemRendererList = null; break;
        }
        Assert.False(NativeSelectionDialog.TryResolve(Addon, focus, out _));
        Assert.Equal(NativeSelectionDialog.Result.Unowned,
            NativeSelectionDialog.Confirm(Addon, focus, _ => Assert.Fail("Unowned answer dispatched")));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void HiddenAndDrawDisabledAnswerDoesNotActivate(bool drawDisabled)
    {
        Menu("SelectIconString");
        if (drawDisabled) _rowOwner->AtkResNode.IsDrawDisabled = true;
        else _rowOwner->AtkResNode.NodeFlags &= ~NodeFlags.Visible;
        Assert.True(NativeSelectionDialog.TryResolve(Addon, _focus, out var choice));
        Assert.False(choice.Available);
    }

    [Fact]
    public void SameFocusInTwoDifferentRowsIsRejectedInsteadOfPickingTheFirst()
    {
        Menu("SelectString");
        var other = (AtkComponentListItemRenderer*)Allocate(sizeof(AtkComponentListItemRenderer));
        other->ListItemIndex = 2;
        other->OwnerNode = Component((AtkComponentBase*)other);
        Children(&other->UldManager, (nint)_focus);
        Children(&_list->UldManager, (nint)_rowOwner, (nint)other->OwnerNode);
        _list->AllocatedItemRendererListLength = 2;
        _list->ItemRendererList = (AtkComponentList.ListItem*)Allocate(2 * sizeof(AtkComponentList.ListItem));
        _list->ItemRendererList[0].AtkComponentListItemRenderer = _row;
        _list->ItemRendererList[1].AtkComponentListItemRenderer = other;
        Assert.False(NativeSelectionDialog.TryResolve(Addon, _focus, out _));
    }

    [Fact]
    public void ContextMenuUsesTheSameExactRowAndAvailabilityRules()
    {
        Menu("SelectString"); Addon->NameString = "ContextMenu";
        Assert.True(NativeSelectionDialog.TryResolveRow(Addon, _list, 6, _focus, out var choice));
        Assert.Equal(4, choice.Index);
        Addon->NumBlockingAddons = 1;
        Assert.False(NativeSelectionDialog.TryResolveRow(Addon, _list, 6, _focus, out _));
    }

    private void Menu(string name)
    {
        Addon->NameString = name;
        if (name == "SelectString")
        {
            ((AddonSelectString*)Addon)->PopupMenu.List = _list;
            ((AddonSelectString*)Addon)->PopupMenu.EntryCount = 6;
        }
        else if (name == "SelectIconString") { _storage->PopupMenu.List = _list; _storage->PopupMenu.EntryCount = 6; }
        else ((AddonCutSceneSelectString*)Addon)->OptionList = _list;
    }
    private AtkResNode* Node()
    {
        var node = (AtkResNode*)Allocate(sizeof(AtkResNode));
        node->NodeFlags = NodeFlags.Visible | NodeFlags.Enabled;
        return node;
    }
    private AtkComponentNode* Component(AtkComponentBase* component)
    {
        var owner = (AtkComponentNode*)Allocate(sizeof(AtkComponentNode));
        owner->AtkResNode.Type = (NodeType)1001;
        owner->AtkResNode.NodeFlags = NodeFlags.Visible | NodeFlags.Enabled;
        owner->Component = component;
        return owner;
    }
    private void Children(AtkUldManager* uld, params nint[] children)
    {
        uld->NodeListCount = (ushort)children.Length;
        uld->NodeList = (AtkResNode**)Allocate(children.Length * sizeof(nint));
        for (var i = 0; i < children.Length; i++) uld->NodeList[i] = (AtkResNode*)children[i];
    }
    private nint Allocate(int size)
    {
        var address = (nint)NativeMemory.AllocZeroed((nuint)size); _memory.Add(address); return address;
    }
    public void Dispose() { foreach (var address in _memory) NativeMemory.Free((void*)address); }
}

public sealed class SelectionDialogKeyTests
{
    [Theory]
    [InlineData(0x60)] [InlineData(0x2D)] [InlineData(0x0D)]
    public void ClearedGameStateCannotReleaseAChoiceKeyWhileItIsStillPhysicallyHeld(int key)
    {
        var (plugin, keys) = Input();
        Field("_dialogueConfirmGuard").SetValue(plugin, new DialogueConfirmGuard());
        var physical = true;
        Field("_physicalConfirmKeyDown").SetValue(plugin, (Func<int, bool>)(_ => physical));
        keys.Down[key] = true; Edges(plugin);
        plugin.ApplyDialogueConfirmGuard(true, false);
        Assert.True(plugin.TakeSelectionDialogConfirmKey(true));
        for (var frame = 0; frame < 10; frame++)
        {
            // The previous choice may already have opened a different menu.
            // Neither the cleared bit nor that replacement is a real release.
            Edges(plugin); Suppress(plugin);
            plugin.ApplyDialogueConfirmGuard(true, false);
            Assert.False(plugin.TakeSelectionDialogConfirmKey(true));
            Assert.False(keys.Down[key]);
        }
        physical = false; Edges(plugin); Suppress(plugin);
        plugin.ApplyDialogueConfirmGuard(false, false);
        physical = true; keys.Down[key] = true; Edges(plugin);
        plugin.ApplyDialogueConfirmGuard(true, false);
        Assert.True(plugin.TakeSelectionDialogConfirmKey(true));
    }

    [Fact]
    public void SimultaneousConfirmKeysAreClaimedTogetherForOneAction()
    {
        var (plugin, keys) = Input();
        keys.Down[0x60] = keys.Down[0x0D] = true; Edges(plugin);
        Assert.True(plugin.TakeSelectionDialogConfirmKey(true));
        Assert.False(keys.Down[0x60]); Assert.False(keys.Down[0x0D]);
        keys.Down[0x60] = keys.Down[0x0D] = true; Edges(plugin); Suppress(plugin);
        Assert.False(plugin.TakeSelectionDialogConfirmKey(true));
        Assert.False(keys.Down[0x60]); Assert.False(keys.Down[0x0D]);
    }

    [Theory]
    [InlineData(0x60)] [InlineData(0x2D)] [InlineData(0x0D)]
    public void OnePressOwnsOneAnswerAndCannotAlsoActivateTheNextDialog(int key)
    {
        var (plugin, keys) = Input();
        keys.Down[key] = true; Edges(plugin);
        Assert.True(plugin.TakeSelectionDialogConfirmKey(true));
        Assert.False(keys.Down[key]);
        for (var frame = 0; frame < 20; frame++)
        {
            keys.Down[key] = true;
            Edges(plugin);
            Suppress(plugin);
            Assert.False(plugin.TakeSelectionDialogConfirmKey(true));
            Assert.False(keys.Down[key]);
        }
        Edges(plugin); Suppress(plugin); // released
        keys.Down[key] = true; Edges(plugin);
        Assert.True(plugin.TakeSelectionDialogConfirmKey(true));
    }

    [Theory]
    [InlineData(0x60)] [InlineData(0x2D)] [InlineData(0x0D)]
    public void ForeignMenusTextInputAndModifiedChordsKeepTheirKeys(int key)
    {
        var (plugin, keys) = Input();
        keys.Down[key] = true; Edges(plugin);
        Assert.False(plugin.TakeSelectionDialogConfirmKey(false));
        Assert.True(keys.Down[key]);
        keys.Down[0x11] = true;
        Assert.False(plugin.TakeSelectionDialogConfirmKey(true));
        Assert.True(keys.Down[key]);
        keys.Down[0x11] = false;
        Field("_textInputActive").SetValue(plugin, true);
        Assert.False(plugin.TakeSelectionDialogConfirmKey(true));
        Assert.True(keys.Down[key]);
    }

    private static (Plugin, ShopQuantityTests.KeyStateProxy) Input()
    {
        var (plugin, keys) = FollowTargetKeyTests.CreateInput();
        Field("_nativeButtonKeys").SetValue(plugin, new NativeButtonKeyOwnership());
        return (plugin, keys);
    }
    private static FieldInfo Field(string name) => typeof(Plugin).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static void Edges(Plugin plugin) => Method("UpdateKeyEdges").Invoke(plugin, null);
    private static void Suppress(Plugin plugin) => Method("SuppressOwnedNativeButtonHold").Invoke(plugin, null);
    private static MethodInfo Method(string name) => typeof(Plugin).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!;
}
