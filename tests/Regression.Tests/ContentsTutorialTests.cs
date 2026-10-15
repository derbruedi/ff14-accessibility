using System.Runtime.InteropServices;
using System.Text;
using FF14Accessibility;
using FF14Accessibility.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Regression.Tests;

[Collection("Language")]
public sealed unsafe class ContentsTutorialTests : IDisposable
{
    private readonly LanguageMode _mode = Loc.Mode;
    private readonly List<nint> _allocations = [];
    private readonly AtkUnitBase* _addon;
    private readonly AtkResNode** _nodes;

    public ContentsTutorialTests()
    {
        Loc.Mode = LanguageMode.Russian;
        _addon = (AtkUnitBase*)Allocate(sizeof(AtkUnitBase));
        _addon->IsVisible = true;
        _nodes = (AtkResNode**)Allocate(13 * sizeof(nint));
        _addon->UldManager.NodeList = _nodes;
        _addon->UldManager.NodeListCount = 13;
    }

    [Fact]
    public void RealDungeonsPageParsesFormattingAndKeepsParagraphAndWordBoundaries()
    {
        Text(2, "Подземелья");
        Text(7, "3/8");
        Text(5, "\u0002H\u0002\b\u0003\u0002I\u0002\u0004\u0003Вглубь подземелий: подземелья с НИП"
            + "\u0002I\u0002\u0001\u0003\u0002H\u0002\u0001\u0003\u0002\u0010\u0001\u0003\u0002\u0010\u0001\u0003"
            + "Даже самому самостоятельному\u0002\u0010\u0001\u0003и умелому искателю не осилить подземелье.");
        var page = ContentsTutorialText.Page(_addon, "Ctrl+F10");
        Assert.StartsWith("Подземелья. Страница 3 из 8. Вглубь подземелий: подземелья с НИП. Даже", page);
        Assert.Contains("самостоятельному и умелому", page);
        Assert.Contains("Ctrl+F10 повторяет эту страницу", page);
        Assert.DoesNotContain('\u0002', page);
        Assert.DoesNotContain('\u0003', page);
        Assert.DoesNotContain("Enter листает дальше", page);
    }

    [Fact]
    public void PreviousTitleAndHiddenOrUnfilledBodyCannotBeAnnounced()
    {
        Text(2, "Южный фронт Бозьи");
        Assert.Empty(ContentsTutorialText.Page(_addon, "Ctrl+F10"));
        var body = Text(5, "Текст другого обучения");
        body->AtkResNode.NodeFlags = 0;
        Assert.Empty(ContentsTutorialText.Page(_addon, "Ctrl+F10"));
        body->AtkResNode.NodeFlags = NodeFlags.Visible;
        _addon->IsVisible = false;
        Assert.Empty(ContentsTutorialText.Page(_addon, "Ctrl+F10"));
    }

    [Theory]
    [InlineData(8u, "Предыдущая страница")]
    [InlineData(9u, "Дальше")]
    [InlineData(11u, "Закрыть")]
    public void MeasuredIconButtonsAreNamedFromTheirOwningControl(uint id, string expected)
    {
        var owner = Control(id);
        var focus = Child(owner);
        Assert.Equal(expected, ContentsTutorialText.FocusLabel(_addon, focus));
        Assert.Equal(expected, ContentsTutorialText.FocusLabel(_addon, (AtkResNode*)owner));
        owner->AtkResNode.NodeFlags &= ~NodeFlags.Visible;
        Assert.Empty(ContentsTutorialText.FocusLabel(_addon, focus));
    }

    [Fact]
    public void SameNodeNumberInAnotherWindowCannotBeNamedAsATutorialArrow()
    {
        Control(9);
        var foreign = Control(9, addToWindow: false);
        Assert.Empty(ContentsTutorialText.FocusLabel(_addon, Child(foreign)));
        Assert.Empty(ContentsTutorialText.FocusLabel(_addon, null));
        Assert.Empty(ContentsTutorialText.FocusLabel(null, (AtkResNode*)foreign));
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)] [InlineData(true, true)]
    public void OnlyAvailableRightHandArrowCanBeTheForwardFallback(bool visible, bool enabled)
    {
        var previous = Control(8);
        var next = Control(9);
        next->AtkResNode.NodeFlags = (visible ? NodeFlags.Visible : 0) | (enabled ? NodeFlags.Enabled : 0);
        Assert.Equal(visible && enabled ? (nint)next : 0, (nint)ContentsTutorialText.DefaultButton(_addon));
        Assert.NotEqual((nint)previous, (nint)ContentsTutorialText.DefaultButton(_addon));
        _addon->NumBlockingAddons = 1;
        Assert.Equal(0, (nint)ContentsTutorialText.DefaultButton(_addon));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void VisibleCloseUsesItsActualEnabledStateWithoutAcknowledgingForThePlayer(bool enabled)
    {
        Control(9);
        var checkbox = Control(10);
        var close = Control(11);
        if (!enabled) close->AtkResNode.NodeFlags &= ~NodeFlags.Enabled;
        Assert.Equal(enabled ? (nint)close : 0, (nint)ContentsTutorialText.DefaultButton(_addon));
        Assert.False(((AtkComponentButton*)checkbox->Component)->IsChecked);
        Assert.Contains("выкл", ContentsTutorialText.FocusLabel(_addon, Child(checkbox)));
    }

    [Fact]
    public void AcknowledgementKeepsNativeEnterInsteadOfActivatingTheDefaultCloseButton()
    {
        Control(11);
        Assert.True(ContentsTutorialText.UsesNativeToggle(_addon, Child(Control(10))));
        Assert.False(ContentsTutorialText.UsesNativeToggle(_addon, Child(Control(9))));
        Assert.False(ContentsTutorialText.UsesNativeToggle(_addon, Child(Control(10, addToWindow: false))));
        Assert.False(ContentsTutorialText.UsesNativeToggle(_addon, null));
        _addon->IsVisible = false;
        Assert.False(ContentsTutorialText.UsesNativeToggle(_addon, (AtkResNode*)_nodes[10]));
    }

    [Fact]
    public void FilledPagesSettleOnceRepeatImmediatelyAndReadAgainAfterReopening()
    {
        var state = new SelectOkAnnouncement();
        Assert.Empty(state.Update("", true, 0));
        Assert.Empty(state.Update("Страница 1 из 8. Текст.", true, 10));
        Assert.NotEmpty(state.Update("Страница 1 из 8. Текст.", true, 160));
        Assert.Empty(state.Update("Страница 1 из 8. Текст.", true, 500));
        Assert.NotEmpty(state.Update("Страница 1 из 8. Текст.", true, 501, repeat: true));
        Assert.Empty(state.Update("Страница 2 из 8. Другой текст.", true, 600));
        Assert.NotEmpty(state.Update("Страница 2 из 8. Другой текст.", true, 750));
        Assert.Empty(state.Update("", false, 800));
        Assert.Empty(state.Update("Страница 2 из 8. Другой текст.", true, 900));
        Assert.NotEmpty(state.Update("Страница 2 из 8. Другой текст.", true, 1050));
    }

    [Theory]
    [InlineData(LanguageMode.Russian, "Предыдущая страница", "Дальше")]
    [InlineData(LanguageMode.English, "Previous page", "Next")]
    [InlineData(LanguageMode.German, "Vorherige Seite", "Weiter")]
    public void ControlPromptsFollowThePluginLanguageWithoutChangingGameText(LanguageMode mode, string previous, string next)
    {
        Loc.Mode = mode;
        Text(2, "Donjon"); Text(5, "Texte du jeu."); Text(7, "1/1");
        Assert.Equal(previous, ContentsTutorialText.FocusLabel(_addon, Child(Control(8))));
        Assert.Equal(next, ContentsTutorialText.FocusLabel(_addon, Child(Control(9))));
        Assert.Contains("Texte du jeu.", ContentsTutorialText.Page(_addon, "Alt+F10"));
    }

    private AtkTextNode* Text(uint id, string text)
    {
        var node = (AtkTextNode*)Allocate(sizeof(AtkTextNode));
        node->AtkResNode.NodeId = id;
        node->AtkResNode.Type = NodeType.Text;
        node->AtkResNode.NodeFlags = NodeFlags.Visible;
        var bytes = Encoding.UTF8.GetBytes(text + "\0");
        var buffer = Allocate(bytes.Length);
        Marshal.Copy(bytes, 0, buffer, bytes.Length);
        node->NodeText.StringPtr = (byte*)buffer;
        node->NodeText.BufSize = bytes.Length;
        node->NodeText.BufUsed = bytes.Length;
        _nodes[id] = &node->AtkResNode;
        return node;
    }

    private AtkComponentNode* Control(uint id, bool addToWindow = true)
    {
        var owner = (AtkComponentNode*)Allocate(sizeof(AtkComponentNode));
        owner->AtkResNode.NodeId = id;
        owner->AtkResNode.Type = (NodeType)1001;
        owner->AtkResNode.NodeFlags = NodeFlags.Visible | NodeFlags.Enabled;
        owner->Component = (AtkComponentBase*)Allocate(sizeof(AtkComponentButton));
        if (addToWindow) _nodes[id] = &owner->AtkResNode;
        return owner;
    }

    private AtkResNode* Child(AtkComponentNode* owner)
    {
        var focus = (AtkResNode*)Allocate(sizeof(AtkResNode));
        focus->NodeId = 3;
        focus->NodeFlags = NodeFlags.Visible;
        focus->ParentNode = (AtkResNode*)owner;
        return focus;
    }

    private nint Allocate(int length)
    {
        var memory = (nint)NativeMemory.AllocZeroed((nuint)length);
        _allocations.Add(memory);
        return memory;
    }

    public void Dispose()
    {
        Loc.Mode = _mode;
        foreach (var memory in _allocations) NativeMemory.Free((void*)memory);
    }
}
