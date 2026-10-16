using System.Reflection;
using System.Runtime.InteropServices;
using Dalamud.Plugin.Services;
using FF14Accessibility;
using FF14Accessibility.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Regression.Tests;

[Collection("Language")]
public unsafe sealed class TableReaderTests : IDisposable
{
    private readonly LanguageMode previous = Loc.Mode;
    private readonly List<nint> allocations = [];
    public TableReaderTests() => Loc.Mode = LanguageMode.Russian;
    public void Dispose() { Loc.Mode = previous; foreach (var p in allocations) NativeMemory.Free((void*)p); }
    private static TableReader.Cell Cell(string id, string text, float x, float y, float height = 20) => new(id, text, x, y, height);

    [Fact]
    public void DefaultTableKeyDoesNotStealJobGaugeOrAnotherPluginShortcut()
    {
        var plugin = (Plugin)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Plugin));
        typeof(Plugin).GetField("_keySpecCache", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(plugin, new Dictionary<string, (int, bool, bool, bool)>());
        var parse = typeof(Plugin).GetMethod("ParseKeySpec", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var config = new Configuration();
        var binding = parse.Invoke(plugin, [config.KeyReadTable]);
        Assert.Equal((0x79, true, false, true), binding);
        var fields = typeof(Configuration).GetFields().Where(f => f.FieldType == typeof(string) && f.Name.StartsWith("Key"));
        Assert.Single(fields, f => Equals(binding, parse.Invoke(plugin, [f.GetValue(config)!])));
        Assert.Equal((0x79, true, true, false), parse.Invoke(plugin, [config.KeyJobGauge]));
        config.KeyReadTable = "F1"; config.ResetKeysToDefaults();
        Assert.Equal("Strg+Alt+F10", config.KeyReadTable);
    }

    [Fact]
    public void ReversedNodeOrderBecomesLabelThenValueAndKeepsEqualValues()
    {
        var rows = TableReader.Arrange("Характеристики", [Cell("v2", "45", 120, 40), Cell("v1", "45", 120, 10),
            Cell("n2", "Ловкость", 10, 40), Cell("n1", "Живучесть", 10, 10)]);
        Assert.Equal(new[] { "Живучесть", "45", "Ловкость", "45" }, rows.SelectMany(r => r.Cells).Select(c => c.Text));
    }
    [Fact]
    public void KeepsZeroSingleCharacterAndDuplicateTextAtDifferentPositions()
    {
        var rows = TableReader.Arrange("Окно", [Cell("a", "0", 0, 0), Cell("b", "0", 30, 0),
            Cell("a", "0", 0, 0), Cell("c", "1", 50, 0), Cell("d", " ", 60, 0), Cell("bad", "NaN", float.NaN, 0)]);
        Assert.Equal(new[] { "0", "0", "1" }, rows.Single().Cells.Select(c => c.Text));
    }
    [Fact]
    public void SmallOffsetsAlignButCannotChainDifferentRowsTogether()
    {
        var rows = TableReader.Arrange("", [Cell("a", "Сила", 0, 0), Cell("b", "21", 30, 5), Cell("c", "Ловкость", 0, 10)]);
        Assert.Equal(2, rows.Count);
        Assert.Equal(2, rows[0].Cells.Count);
    }
    [Fact]
    public void RefreshKeepsTheSelectedFieldAndReadsItsNewValue()
    {
        var reader = new TableReader();
        reader.Refresh(TableReader.Arrange("Характеристики", [Cell("n", "Сила", 0, 0), Cell("v", "21", 30, 0)]));
        reader.MoveColumn(1);
        reader.Refresh(TableReader.Arrange("Характеристики", [Cell("new", "Заголовок", 0, -30), Cell("n", "Сила", 0, 0), Cell("v", "22", 30, 0)]));
        Assert.Equal(1, reader.RowIndex); Assert.Equal(1, reader.ColumnIndex);
        Assert.Contains("Сила: 22", reader.SpeakCell());
    }
    [Fact]
    public void FieldContextUsesItsOwnLabelInAMultiColumnRow()
    {
        var reader = new TableReader();
        reader.Refresh(TableReader.Arrange("", [Cell("a", "Сила", 0, 0), Cell("b", "21", 20, 0), Cell("c", "Ловкость", 40, 0), Cell("d", "45", 60, 0)]));
        Assert.Equal(2, reader.Rows.Count);
        reader.MoveRow(1);
        reader.MoveColumn(1);
        Assert.EndsWith("Ловкость: 45", reader.SpeakCell());
        Assert.DoesNotContain("Сила", reader.SpeakCell());
    }
    [Fact]
    public void CursorClampsAtBothEndsAndSurvivesRemovedRows()
    {
        var reader = new TableReader();
        reader.Refresh(TableReader.Arrange("", [Cell("a", "A", 0, 0), Cell("b", "B", 0, 40)]));
        reader.MoveRow(int.MaxValue); reader.MoveRow(int.MaxValue);
        Assert.Equal(1, reader.RowIndex);
        reader.MoveColumn(99); Assert.Equal(0, reader.ColumnIndex);
        reader.Refresh(TableReader.Arrange("", [Cell("a", "A", 0, 0)])); Assert.Equal(0, reader.RowIndex);
        reader.MoveRow(int.MinValue); Assert.Equal(0, reader.RowIndex);
        Assert.False(reader.Refresh([])); Assert.Equal(AccessibilityStrings.TableEmpty, reader.SpeakCell());
    }
    [Fact]
    public void SectionNavigationAlwaysStartsAtTheFirstRow()
    {
        var reader = new TableReader();
        reader.Refresh(TableReader.Arrange("A", [Cell("a1", "A1", 0, 0), Cell("a2", "A2", 0, 30)])
            .Concat(TableReader.Arrange("B", [Cell("b1", "B1", 0, 0)])).ToArray());
        reader.MoveSection(1); Assert.Equal(2, reader.RowIndex);
        reader.MoveSection(-1); Assert.Equal(0, reader.RowIndex);
    }
    [Fact]
    public void RecordedIndependentLabelsAreNeverUsedAsEachOthersContext()
    {
        var reader = new TableReader();
        reader.Refresh(TableReader.Arrange("Персонаж", [Cell("a", "Рекомендованное снаряжение", 0, 0),
            Cell("b", "Список комплектов снаряжения", 100, 0)]));
        Assert.Equal(2, reader.Rows.Count);
        reader.MoveRow(1);
        Assert.DoesNotContain("Рекомендованное", reader.SpeakCell());
        // Even an explicitly provided multi-field row must not turn labels into values.
        reader.Refresh([new("", [Cell("hp", "ОЗ", 0, 0), Cell("n", "248", 100, 0), Cell("mp", "ОМ", 200, 0)])]);
        reader.MoveColumn(2);
        Assert.EndsWith("ОМ", reader.SpeakCell());
        Assert.DoesNotContain("ОЗ:", reader.SpeakCell());
    }
    [Fact]
    public void SectionIsSpokenOnlyOnEntryIncludingRepeatRefreshAndReturn()
    {
        var rows = new TableReader.Row[] {
            new("Атака", [Cell("a", "Крит. удар", 0, 0), Cell("v", "96", 100, 0)]),
            new("Атака", [Cell("b", "Решительность", 0, 20), Cell("w", "47", 100, 20)]),
            new("Защита", [Cell("c", "Физическая", 200, 0), Cell("z", "56", 300, 0)]) };
        var reader = new TableReader(); reader.Refresh(rows);
        Assert.StartsWith("Атака. Крит. удар: 96", reader.SpeakRow());
        Assert.DoesNotContain("Атака", reader.SpeakRow());
        reader.Refresh(rows); reader.MoveRow(1);
        Assert.DoesNotContain("Атака", reader.SpeakRow());
        reader.MoveSection(1); Assert.StartsWith("Защита.", reader.SpeakRow());
        reader.MoveSection(-1); Assert.StartsWith("Атака.", reader.SpeakRow());
    }
    [Fact]
    public void NumericControlCannotBorrowLabelFromAnotherControl()
    {
        var rows = TableReader.Arrange("", [Cell("label", "Сила", 0, 0) with { Group = "first" },
            Cell("value", "64", 100, 0) with { Group = "other" }]);
        Assert.Equal(2, rows.Count);
        var reader = new TableReader(); reader.Refresh(rows); reader.MoveRow(1);
        Assert.DoesNotContain("Сила", reader.SpeakCell());
    }
    [Fact]
    public void WindowTitleIsNotRepeatedAsBothSectionAndContent()
    {
        var reader = new TableReader();
        reader.Refresh(TableReader.Arrange("Персонаж", [Cell("title", "Персонаж", 0, 0)]));
        Assert.Equal("Персонаж. Строка 1 из 1.", reader.SpeakRow());
    }
    [Fact]
    public void HiddenAncestorsAndCyclicTreesCannotLeakOtherTabs()
    {
        var parent = Node(NodeType.Res); var child = Node(NodeType.Text);
        child->ParentNode = parent;
        Assert.True(TableNodeReader.IsVisible(child));
        parent->NodeFlags = 0; Assert.False(TableNodeReader.IsVisible(child));
        parent->NodeFlags = NodeFlags.Visible; parent->ParentNode = child;
        Assert.False(TableNodeReader.IsVisible(child));
    }
    [Fact]
    public void NativeReaderSkipsHiddenTextButKeepsSingleDigitAndVisitsOnce()
    {
        var a = Node(NodeType.Text); var hidden = Node(NodeType.Text);
        hidden->NodeFlags = 0;
        var cells = new List<TableReader.Cell>(); var visited = new HashSet<nint>();
        TableNodeReader.Collect(a, cells, visited, _ => "0", _ => null);
        TableNodeReader.Collect(a, cells, visited, _ => throw new Exception("duplicate read"), _ => null);
        TableNodeReader.Collect(hidden, cells, visited, _ => throw new Exception("hidden read"), _ => null);
        Assert.Equal("0", cells.Single().Text);
    }
    [Theory]
    [InlineData(0x0D)] [InlineData(0x60)] [InlineData(0x1B)] [InlineData(0x68)] [InlineData(0x57)]
    [InlineData(0x70)] [InlineData(0x7B)] [InlineData(0xBB)] [InlineData(0xBD)] [InlineData(0x0C)]
    public void ReadModeConsumesConfirmMovementAndHeldExitKeys(int vk)
    {
        var keys = DispatchProxy.Create<IKeyState, ShopQuantityTests.KeyStateProxy>();
        var state = (ShopQuantityTests.KeyStateProxy)(object)keys;
        var input = new MenuInput(keys, null!, UIReaderService.TableKeys);
        state.Down[vk] = true; input.Poll(); input.ConsumeAll(); Assert.False(state.Down[vk]);
        state.Down[vk] = true; input.Poll(); Assert.False(input.Just(vk));
        input.ConsumeAll(); Assert.False(state.Down[vk]); Assert.True(input.AnyDown);
        input.Poll(); Assert.False(input.AnyDown);
    }
    [Theory]
    [InlineData(LanguageMode.Russian, "Строка", "Режим чтения")]
    [InlineData(LanguageMode.English, "Row", "Reading mode")]
    [InlineData(LanguageMode.German, "Zeile", "Lesemodus")]
    public void HelpAndPositionFollowLanguage(LanguageMode mode, string row, string help)
    {
        Loc.Mode = mode; Assert.Contains(row, AccessibilityStrings.TableRow("A", 1, 2, "0"));
        Assert.Contains(help, AccessibilityStrings.TableInstructions);
    }
    private AtkResNode* Node(NodeType type)
    {
        var node = (AtkResNode*)NativeMemory.AllocZeroed((nuint)sizeof(AtkResNode));
        allocations.Add((nint)node); node->Type = type; node->NodeFlags = NodeFlags.Visible; node->Height = 20;
        return node;
    }
}
