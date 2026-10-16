using System.Reflection;
using System.Runtime.CompilerServices;
using Dalamud.Plugin.Services;
using FF14Accessibility;
using FF14Accessibility.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Regression.Tests;

public sealed class LoginDialogTests
{
    private const string Queue19 = "Сервер на данный момент переполнен.\nИгроков в очереди: 19.";
    private const string Queue9 = "Сервер на данный момент переполнен.\nИгроков в очереди: 9.";

    [Fact]
    public void EmptySetupAndCharacterListRecreationDoNotConsumeTheQueueBody()
    {
        var speech = new SelectOkAnnouncement();
        Assert.Empty(speech.Update("", true, 0));
        Assert.Empty(speech.Update(Queue19, true, 100));
        Assert.Equal(Queue19, speech.Update(Queue19, true, 250));
        Assert.Empty(speech.Update(Queue19, true, 1000));
        Assert.Empty(speech.Update(Queue9, true, 1100));
        Assert.Equal(Queue9, speech.Update(Queue9, true, 1250));
        Assert.Equal(Queue9, speech.Update(Queue9, true, 1300, repeat: true));
        Assert.Empty(speech.Update(Queue9, false, 1400));
        Assert.Empty(speech.Update(Queue9, true, 1500));
        Assert.Equal(Queue9, speech.Update(Queue9, true, 1650));
    }

    [Theory]
    [InlineData("Players in queue: 12.")] [InlineData("Spieler in der Warteschlange: 8.")]
    [InlineData("Nombre de joueurs en attente : 4.")]
    public void ModalKeepsItsRealGameLanguageAndNumber(string text)
    {
        var speech = new SelectOkAnnouncement();
        Assert.Empty(speech.Update(text, true, 0));
        Assert.Equal(text, speech.Update(text, true, 150));
    }

    [Theory]
    [InlineData("_CharaSelectListMenu", true, true)]
    [InlineData("_CharaSelectWorldServer", true, true)]
    [InlineData("CharaSelect", true, true)]
    [InlineData("_CharaSelectListMenu", false, false)]
    [InlineData("SelectOk", true, false)] [InlineData("Inventory", true, false)]
    public unsafe void OnlyCharacterSelectBehindTheVisibleModalIsMuted(string name, bool visible, bool expected)
    {
        AtkUnitBase modal = default; modal.IsVisible = visible;
        var gui = DispatchProxy.Create<IGameGui, DialogGui>();
        ((DialogGui)gui).Modal = (nint)(&modal);
        var reader = (UIReaderService)RuntimeHelpers.GetUninitializedObject(typeof(UIReaderService));
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(UIReaderService).GetField("_gameGui", flags)!.SetValue(reader, gui);
        typeof(UIReaderService).GetField("_config", flags)!.SetValue(reader, new Configuration());
        Assert.Equal(expected, typeof(UIReaderService).GetMethod("IsSuppressedAddon", flags)!.Invoke(reader, [name]));
        if (expected)
            typeof(UIReaderService).GetMethod("ScanAddonTexts", flags)!.Invoke(reader,
                [name, Pointer.Box(null, typeof(AtkUnitBase*)), false]);
    }

    public class DialogGui : DispatchProxy
    {
        public nint Modal;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
            => method?.Name == "GetAddonByName"
                ? Activator.CreateInstance(method.ReturnType, [(string)args![0]! == "SelectOk" ? Modal : nint.Zero])
                : throw new NotSupportedException(method?.Name);
    }
}
