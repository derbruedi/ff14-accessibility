using FF14Accessibility;
using FF14Accessibility.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Regression.Tests;

[Collection("Language")]
public unsafe sealed class NativeDialogButtonTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void ActualFocusedButtonChoosesTheAnswerRegardlessOfCachedSpokenLabels(int selected)
    {
        var buttons = stackalloc AtkComponentButton[3];
        var owners = stackalloc AtkComponentNode[3];
        var children = stackalloc AtkResNode[3];
        for (var i=0;i<3;i++)
        {
            owners[i].AtkResNode.NodeId = (uint)(8+i*3);
            owners[i].AtkResNode.NodeFlags = NodeFlags.Visible | NodeFlags.Enabled;
            owners[i].AtkResNode.Type = (NodeType)1005;
            owners[i].Component = (AtkComponentBase*)&buttons[i];
            buttons[i].OwnerNode = &owners[i];
            children[i].NodeId = 4; // actual SelectYesno focus id in player's log
            children[i].ParentNode = (AtkResNode*)&owners[i];
        }
        var addon = new AddonSelectYesno { YesButton=&buttons[0],NoButton=&buttons[1],AtkComponentButton238=&buttons[2] };
        Assert.True(NativeDialogButtons.TrySelect(&addon,&children[selected],out var result));
        Assert.Equal(selected,result.Index);
        Assert.Equal((nint)(&owners[selected]),result.Node);
        Assert.False(result.Held);
        Assert.True(NativeDialogButtons.TrySelect(&addon,(AtkResNode*)&owners[selected],out result));
        Assert.Equal(selected,result.Index);
    }

    [Theory]
    [InlineData(false,false,false)] [InlineData(true,false,false)]
    [InlineData(true,true,true)]
    public void HiddenOrDisabledConfirmationNeverBecomesAWorkingButton(bool visible,bool enabled,bool expected)
    {
        var flags = (visible ? NodeFlags.Visible : 0) | (enabled ? NodeFlags.Enabled : 0);
        var owner = new AtkComponentNode(); owner.AtkResNode.NodeFlags=flags;
        var button = new AtkComponentButton {OwnerNode=&owner};
        var focus = new AtkResNode {ParentNode=(AtkResNode*)&owner};
        var addon = new AddonSelectYesno {YesButton=&button};
        Assert.Equal(expected,NativeDialogButtons.TrySelect(&addon,&focus,out _));
    }

    [Fact]
    public void AnotherWindowUnrelatedFocusOrHiddenAncestorCannotConfirm()
    {
        var owner = new AtkComponentNode(); owner.AtkResNode.NodeFlags=NodeFlags.Visible|NodeFlags.Enabled;
        var button = new AtkComponentButton {OwnerNode=&owner};
        var addon = new AddonSelectYesno {YesButton=&button};
        var unrelated = new AtkResNode();
        Assert.False(NativeDialogButtons.TrySelect(&addon,&unrelated,out _));
        Assert.False(NativeDialogButtons.TrySelect(&addon,null,out _));
        Assert.False(NativeDialogButtons.TrySelect(null,&unrelated,out _));
        var parent = new AtkResNode(); owner.AtkResNode.ParentNode=&parent;
        Assert.False(NativeDialogButtons.TrySelect(&addon,(AtkResNode*)&owner,out _));
    }

    [Fact]
    public void HoldConfirmationRemainsAHoldInsteadOfAnOrdinaryCallback()
    {
        var normalOwner = new AtkComponentNode();
        var holdOwner = new AtkComponentNode(); holdOwner.AtkResNode.NodeFlags=NodeFlags.Visible|NodeFlags.Enabled;
        var normal = new AtkComponentButton {OwnerNode=&normalOwner};
        var hold = new AtkComponentHoldButton {OwnerNode=&holdOwner};
        var focus = new AtkResNode {ParentNode=(AtkResNode*)&holdOwner};
        var addon = new AddonSelectYesno {YesButton=&normal,AtkComponentHoldButton278=&hold};
        Assert.True(NativeDialogButtons.TrySelect(&addon,&focus,out var selected));
        Assert.True(selected.Held); Assert.Equal(0,selected.Index);
    }

    [Fact]
    public void ComponentChildrenIdentifyFocusEvenWhenItsParentChainIsMissing()
    {
        var child = new AtkResNode(); var children = stackalloc AtkResNode*[1]; children[0]=&child;
        var button = new AtkComponentButton();
        button.UldManager.NodeList=children; button.UldManager.NodeListCount=1;
        var owner = new AtkComponentNode {Component=(AtkComponentBase*)&button};
        owner.AtkResNode.NodeFlags=NodeFlags.Visible|NodeFlags.Enabled;owner.AtkResNode.Type=(NodeType)1005;
        button.OwnerNode=&owner;
        var addon=new AddonSelectYesno {NoButton=&button};
        Assert.True(NativeDialogButtons.TrySelect(&addon,&child,out var selected));
        Assert.Equal(1,selected.Index);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void SecondaryButtonVariantsKeepTheirActualSide(int side)
    {
        var buttons = stackalloc AtkComponentButton[3];
        var owners = stackalloc AtkComponentNode[3];
        var children = stackalloc AtkResNode[3];
        for (var i = 0; i < 3; i++)
        {
            owners[i].AtkResNode.NodeFlags = NodeFlags.Visible | NodeFlags.Enabled;
            buttons[i].OwnerNode = &owners[i];
            children[i].ParentNode = (AtkResNode*)&owners[i];
        }
        var addon = new AddonSelectYesno { AtkComponentButton260 = &buttons[0],
            AtkComponentButton268 = &buttons[1], AtkComponentButton270 = &buttons[2] };
        Assert.True(NativeDialogButtons.TrySelect(&addon, &children[side], out var selected));
        Assert.Equal(side, selected.Index);
        Assert.False(selected.Held);
        Assert.Equal((nint)(&buttons[side]), (nint)NativeDialogButtons.ButtonForSide(&addon, side));
    }

    [Fact]
    public void DisabledPrimaryDoesNotHideAnAvailableHoldOnTheSameSide()
    {
        var primaryOwner = new AtkComponentNode(); primaryOwner.AtkResNode.NodeFlags = NodeFlags.Visible;
        var holdOwner = new AtkComponentNode(); holdOwner.AtkResNode.NodeFlags = NodeFlags.Visible | NodeFlags.Enabled;
        var primary = new AtkComponentButton { OwnerNode = &primaryOwner };
        var hold = new AtkComponentHoldButton { OwnerNode = &holdOwner };
        var addon = new AddonSelectYesno { YesButton = &primary, AtkComponentHoldButton278 = &hold };
        Assert.Equal((nint)(&hold), (nint)NativeDialogButtons.ButtonForSide(&addon, 0));
        Assert.Equal(0, (nint)NativeDialogButtons.ButtonForSide(&addon, 1));
        Assert.Equal(0, (nint)NativeDialogButtons.ButtonForSide(&addon, -1));
        Assert.Equal(0, (nint)NativeDialogButtons.ButtonForSide(&addon, 3));
    }

    [Theory]
    [InlineData("CharacterInspect",17,true)] [InlineData("CharacterInspect",43,true)]
    [InlineData("CharacterInspect",55,true)] [InlineData("CharacterInspect",18,false)]
    [InlineData("Character",17,false)] [InlineData("JournalAccept",17,false)]
    public void OnlyMeasuredSilentInspectCellsReceiveTheNeutralLabel(string window,uint id,bool named)
    {
        var old=Loc.Mode;
        try {Loc.Mode=LanguageMode.Russian;
            Assert.Equal(named ? "Ячейка экипировки" : "",UIReaderService.InspectControlLabel(window,id));}
        finally {Loc.Mode=old;}
    }

    [Theory]
    [InlineData(0x60)] [InlineData(0x2D)] [InlineData(0x0D)]
    public void HeldConfirmCannotReachTheNextWindowUntilItIsReleased(int key)
    {
        var ownership = new NativeButtonKeyOwnership();
        Assert.False(ownership.Suppress(key, true)); // Native key before we handle a button.
        ownership.Claim(key);
        Assert.True(ownership.Suppress(key, true));
        // The dialog closed and another window opened while the key stayed down.
        Assert.True(ownership.Suppress(key, true));
        Assert.False(ownership.Suppress(key, false));
        Assert.False(ownership.Suppress(key, true)); // A new press belongs to the new window.
    }

    [Fact]
    public void ClaimingOneConfirmationKeyDoesNotConsumeOtherKeys()
    {
        var ownership = new NativeButtonKeyOwnership(); ownership.Claim(0x60);
        Assert.False(ownership.Suppress(0x0D, true));
        Assert.False(ownership.Suppress(0x2D, true));
        Assert.True(ownership.Suppress(0x60, true));
    }
}
