using System.Runtime.InteropServices;
using System.Reflection;
using Dalamud.Plugin.Services;
using FF14Accessibility;
using FF14Accessibility.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Regression.Tests;

[Collection("Language")]
public unsafe sealed class ShopQuantityTests : IDisposable
{
    private readonly List<nint> allocations = [];
    private readonly Dictionary<nint, ComponentType> types = [];
    private readonly LanguageMode previous = Loc.Mode;
    private static ShopQuantityEdit.Target Item => new(1, 2, 3, "Bronze Ingot", 0, 99, 53);

    public ShopQuantityTests() => Loc.Mode = LanguageMode.Russian;
    public void Dispose()
    {
        Loc.Mode = previous;
        foreach (var address in allocations) NativeMemory.Free((void*)address);
    }

    [Fact]
    public void Entering50Replaces53InsteadOfAppendingToIt()
    {
        var edit = new ShopQuantityEdit(Item);
        edit.Digit(5);
        edit.Digit(0);
        Assert.True(edit.TryValue(out var value));
        Assert.Equal(50, value);
        Assert.Equal(53, edit.Original.Value); // draft has not changed the native target
    }

    [Fact]
    public void BackspaceAndDeleteAllowCorrectingTheDraft()
    {
        var edit = new ShopQuantityEdit(Item);
        edit.Backspace();
        Assert.Equal("", edit.Text);
        edit.Digit(2); edit.Digit(5); edit.Digit(7); edit.Backspace();
        Assert.True(edit.TryValue(out var value));
        Assert.Equal(25, value);
        edit.Clear();
        Assert.False(edit.TryValue(out _));
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("0", false)]
    [InlineData("1", true)]
    [InlineData("99", true)]
    [InlineData("100", false)]
    [InlineData("9999999999", false)]
    [InlineData("00000000010", false)]
    public void RejectsEmptyZeroOutOfRangeAndOverflowWithoutClamping(string number, bool expected)
    {
        var edit = new ShopQuantityEdit(Item);
        edit.Clear();
        foreach (var c in number) edit.Digit(c - '0');
        Assert.Equal(expected, edit.TryValue(out _));
    }

    [Fact]
    public void RespectsANonstandardNativeRange()
    {
        var edit = new ShopQuantityEdit(Item with { Minimum = 5, Maximum = 999, Value = 5 });
        edit.Digit(4);
        Assert.False(edit.TryValue(out _));
        edit.Clear();
        edit.Digit(9); edit.Digit(9); edit.Digit(9);
        Assert.True(edit.TryValue(out var value));
        Assert.Equal(999, value);
    }

    [Fact]
    public void RendererReuseWindowChangesAndNativeEditsInvalidateTheDraft()
    {
        var edit = new ShopQuantityEdit(Item);
        Assert.True(edit.Matches(Item));
        foreach (var changed in new[] {
            Item with { Addon = 4 }, Item with { Row = 4 }, Item with { Input = 4 },
            Item with { ItemName = "Copper Ingot" }, Item with { Minimum = 1 },
            Item with { Maximum = 50 }, Item with { Value = 52 }, default
        }) Assert.False(edit.Matches(changed));
    }

    [Theory]
    [InlineData(-1, 99)]
    [InlineData(0, 0)]
    [InlineData(10, 9)]
    public void MissingOrInconsistentNativeLimitsCannotStartAnEdit(int minimum, int maximum)
        => Assert.False(ShopQuantityEdit.IsUsable(Item with { Minimum = minimum, Maximum = maximum }));

    [Fact]
    public void FocusMustBeInsideTheBuyListAndCannotSelectAnotherRowsQuantity()
    {
        var buyList = Component(ComponentType.List);
        var buyback = Component(ComponentType.List);
        var row1 = Component(ComponentType.ListItemRenderer, buyList);
        var row2 = Component(ComponentType.ListItemRenderer, buyList);
        var focus = Allocate<AtkResNode>();
        focus->ParentNode = (AtkResNode*)row2;
        Assert.Equal((nint)row2, (nint)ShopQuantityNodes.FindRow(focus, (AtkResNode*)buyList, TypeOf));
        Assert.Equal(0, (nint)ShopQuantityNodes.FindRow(focus, (AtkResNode*)buyback, TypeOf));
        Assert.Equal(0, (nint)ShopQuantityNodes.FindRow((AtkResNode*)buyList, (AtkResNode*)buyList, TypeOf));
        Assert.False(ShopQuantityNodes.IsWithin(focus, (AtkResNode*)row1));
    }

    [Fact]
    public void NestedQuantityIsFoundButTwoDifferentInputsAreAmbiguous()
    {
        var row = Component(ComponentType.ListItemRenderer);
        var container = Component(ComponentType.Base, row);
        var quantity = Component(ComponentType.NumericInput, container);
        Children(row, (nint)container);
        Children(container, (nint)quantity);
        Assert.Equal((nint)quantity->Component, (nint)ShopQuantityNodes.FindInput((AtkResNode*)row, TypeOf));
        var other = Component(ComponentType.NumericInput, row);
        Children(row, (nint)container, (nint)other);
        Assert.Equal(0, (nint)ShopQuantityNodes.FindInput((AtkResNode*)row, TypeOf));
    }

    [Fact]
    public void DuplicateNodeReferencesAndCyclesDoNotPickAWrongInputOrLoop()
    {
        var row = Component(ComponentType.ListItemRenderer);
        var quantity = Component(ComponentType.NumericInput, row);
        Children(row, (nint)row, (nint)quantity, (nint)quantity);
        Assert.Equal((nint)quantity->Component, (nint)ShopQuantityNodes.FindInput((AtkResNode*)row, TypeOf));
        var unrelated = Allocate<AtkResNode>();
        unrelated->ParentNode = unrelated;
        Assert.False(ShopQuantityNodes.IsWithin(unrelated, (AtkResNode*)row));
    }

    [Theory]
    [InlineData(LanguageMode.Russian, "количество 1", "от 1 до 99", "Escape")]
    [InlineData(LanguageMode.English, "quantity 1", "1 to 99", "Escape")]
    [InlineData(LanguageMode.German, "Menge 1", "1 bis 99", "Escape")]
    public void SingleDigitQuantityHasALabelAndLocalizedHelp(LanguageMode mode, string count, string range, string cancel)
    {
        Loc.Mode = mode;
        Assert.Contains(count, AccessibilityStrings.ShopQuantityValue("Item", 1));
        Assert.Contains(range, AccessibilityStrings.ShopQuantityRange(1, 99));
        Assert.Contains(cancel, AccessibilityStrings.ShopQuantityInstructions);
    }

    [Fact]
    public void NewDefaultDoesNotConflictWithAnotherPluginDefaultAndSurvivesReset()
    {
        var config = new Configuration();
        var keys = typeof(Configuration).GetFields().Where(f => f.Name.StartsWith("Key") && f.FieldType == typeof(string));
        Assert.Single(keys, f => (string)f.GetValue(config)! == config.KeyShopQuantity);
        config.KeyShopQuantity = "F1";
        config.ResetKeysToDefaults();
        Assert.Equal("Strg+Umschalt+B", config.KeyShopQuantity);
    }

    [Theory]
    [InlineData(0x0D)] // applying a draft must not also confirm the purchase
    [InlineData(0x1B)] // cancelling the draft must not also close the store
    [InlineData(0x60)] // numpad zero is a digit while editing, not game Confirm
    [InlineData(0x35)] // hotbar digit
    [InlineData(0x70)] // F1 can also be a reassigned editor opening key
    [InlineData(0x7B)] // F12
    [InlineData(0xBB)] // =/+
    [InlineData(0xBD)] // -/_
    [InlineData(0xBC)] // comma
    [InlineData(0xBE)] // period
    [InlineData(0xE2)] // additional key on international keyboards
    public void EditorKeysAreConsumedOnPressAndWhileHeld(int vk)
    {
        var keys = DispatchProxy.Create<IKeyState, KeyStateProxy>();
        var state = (KeyStateProxy)(object)keys;
        var input = new MenuInput(keys, null!, UIReaderService.ShopQuantityKeys);
        state.Down[vk] = true;
        input.Poll();
        Assert.True(input.Just(vk));
        input.ConsumeAll();
        Assert.False(state.Down[vk]);
        Assert.True(input.AnyDown); // snapshot still blocks a held key after closing
        state.Down[vk] = true; // next game frame: still physically held
        input.Poll();
        Assert.False(input.Just(vk));
        input.ConsumeAll();
        Assert.False(state.Down[vk]);
        input.Poll(); // released
        Assert.False(input.AnyDown);
    }

    public class KeyStateProxy : DispatchProxy
    {
        public readonly bool[] Down = new bool[256];
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "IsVirtualKeyValid") return true;
            var vk = Convert.ToInt32(args![0]);
            if (method.Name == "get_Item") return Down[vk];
            if (method.Name == "set_Item") { Down[vk] = (bool)args[1]!; return null; }
            throw new InvalidOperationException(method.Name);
        }
    }

    private T* Allocate<T>() where T : unmanaged
    {
        var ptr = (T*)NativeMemory.AllocZeroed((nuint)sizeof(T));
        allocations.Add((nint)ptr);
        return ptr;
    }

    private AtkComponentNode* Component(ComponentType type, AtkComponentNode* parent = null)
    {
        var node = Allocate<AtkComponentNode>();
        node->Type = (NodeType)1000;
        node->ParentNode = (AtkResNode*)parent;
        node->Component = Allocate<AtkComponentBase>();
        node->Component->OwnerNode = node;
        types[(nint)node->Component] = type;
        return node;
    }

    private ComponentType TypeOf(nint component) => types[component];

    private void Children(AtkComponentNode* parent, params nint[] children)
    {
        var array = (AtkResNode**)NativeMemory.AllocZeroed((nuint)(sizeof(nint) * children.Length));
        allocations.Add((nint)array);
        for (var i = 0; i < children.Length; i++) array[i] = (AtkResNode*)children[i];
        parent->Component->UldManager.NodeList = array;
        parent->Component->UldManager.NodeListCount = (ushort)children.Length;
    }
}
