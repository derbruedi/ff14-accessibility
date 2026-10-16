using FF14Accessibility.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Regression.Tests;

public unsafe sealed class ArmouryWindowFocusTests
{
    [Theory]
    [InlineData("ArmouryBoard")]
    [InlineData("ContextMenu")]
    [InlineData("SelectYesno")]
    [InlineData("Inventory")]
    public void ActualKeyboardWindowWinsOverBackgroundWindowsFromThePlayerLog(string name)
    {
        AtkUnitBase guide = default, achievement = default, active = default;
        guide.NameString = "PlayGuide"; guide.IsVisible = true;
        achievement.NameString = "AchievementInfo"; achievement.IsVisible = true;
        active.NameString = name; active.IsVisible = true;
        var manager = new AtkUnitManager();
        manager.FocusedUnitsList.Entries[0] = &guide;
        manager.FocusedUnitsList.Entries[1] = &achievement;
        manager.FocusedUnitsList.Entries[2] = &active;
        manager.FocusedUnitsList.Count = 3;
        manager.FocusedAddon = &active;
        Assert.Equal(name, UIReaderService.BlockingFocusedAddon(&manager));
    }

    [Theory]
    [InlineData("_TargetInfo")]
    [InlineData("_TargetInfoMainTarget")]
    [InlineData("_TargetInfoBuffDebuff")]
    [InlineData("_PartyList")]
    [InlineData("NamePlate")]
    public void HudFocusDoesNotHandKeysToAnArmouryLeftOpenInTheBackground(string name)
    {
        AtkUnitBase board = default, hud = default;
        board.NameString = "ArmouryBoard"; board.IsVisible = true;
        hud.NameString = name; hud.IsVisible = true;
        var manager = new AtkUnitManager();
        manager.FocusedUnitsList.Entries[0] = &board;
        manager.FocusedUnitsList.Count = 1;
        manager.FocusedAddon = &hud;
        Assert.Null(UIReaderService.BlockingFocusedAddon(&manager));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void MissingOrClosedActiveWindowKeepsTheConservativeVisibleWindowFallback(bool closed)
    {
        AtkUnitBase guide = default, board = default;
        guide.NameString = "PlayGuide"; guide.IsVisible = true;
        board.NameString = "ArmouryBoard";
        var manager = new AtkUnitManager();
        manager.FocusedUnitsList.Entries[0] = &guide;
        manager.FocusedUnitsList.Count = 1;
        manager.FocusedAddon = closed ? &board : null;
        Assert.Equal("PlayGuide", UIReaderService.BlockingFocusedAddon(&manager));
        Assert.Null(UIReaderService.BlockingFocusedAddon(null));
    }
}
