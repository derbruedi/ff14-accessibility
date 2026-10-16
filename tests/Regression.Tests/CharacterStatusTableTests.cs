using System.Runtime.InteropServices;
using System.Text.Json;
using FF14Accessibility;
using FF14Accessibility.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Regression.Tests;

[Collection("Language")]
public unsafe sealed class CharacterStatusTableTests : IDisposable
{
    private readonly LanguageMode previous = Loc.Mode;
    private readonly List<nint> allocations = [];
    private readonly Dictionary<nint, string> texts = [];
    private readonly Dictionary<nint, (float X, float Y)> offsets = [];
    private readonly Dictionary<uint, Dictionary<uint, nint>> components = [];
    private readonly Dictionary<uint, nint> nodes;
    private readonly AtkUnitBase* addon;

    public CharacterStatusTableTests()
    {
        Loc.Mode = LanguageMode.Russian;
        using var stream = typeof(CharacterStatusTableTests).Assembly.GetManifestResourceStream(
            "Regression.Tests.Fixtures.character-status-layout.json")!;
        using var fixture = JsonDocument.Parse(stream);
        addon = Allocate<AtkUnitBase>();
        var templates = fixture.RootElement.GetProperty("Components").EnumerateArray()
            .ToDictionary(c => c.GetProperty("Id").GetUInt32(), c => c.GetProperty("Nodes"));
        nodes = Build(fixture.RootElement.GetProperty("Nodes"), &addon->UldManager, null, templates);
        foreach (var address in offsets.Keys)
        {
            float x = 0, y = 0;
            for (var p = (AtkResNode*)address; p != null; p = p->ParentNode)
            { var local = offsets[(nint)p]; x += local.X; y += local.Y; }
            ((AtkResNode*)address)->ScreenX = x; ((AtkResNode*)address)->ScreenY = y;
        }
        // Runtime visibility and text are deliberately separate from the ULD.
        foreach (var id in new uint[] { 14, 20, 66, 73 }) Node(id)->NodeFlags = 0;
        Text(3, "ОЗ"); Text(4, "248"); Text(5, "248");
        Text(9, "ОМ"); Text(10, "10000"); Text(11, "10000");
        Text(15, "ОР"); Text(16, "180"); Text(17, "200");
        Text(21, "ОС"); Text(22, "300"); Text(23, "400");
        Text(29, "Атрибуты"); Text(39, "Атака"); Text(47, "Защита");
        Text(54, "Физическая атака"); Text(61, "Магия");
        Text(69, "Сбор"); Text(76, "Ремесло"); Text(83, "Снаряжение"); Text(89, "Роль");
        Stat(31, "Сила", "22"); Stat(32, "Ловкость", "47"); Stat(33, "Живучесть", "48");
        Stat(34, "Интеллект", "64"); Stat(35, "Дух", "44");
        Stat(41, "Крит. удар", "96"); Stat(42, "Решительность", "47"); Stat(43, "Точный удар", "97");
        Stat(49, "Физическая", "56"); Stat(50, "Магическая", "94");
        Stat(56, "Сила атаки", "22"); Stat(57, "Скорость умений", "96");
        Stat(63, "Сила атаки", "64"); Stat(64, "Сила исцеления", "44"); Stat(65, "Скорость чтения", "98");
        Stat(71, "Сбор", "100"); Stat(72, "Восприятие", "110");
        Stat(78, "Мастерство", "120"); Stat(79, "Контроль", "130");
        Stat(85, "Средний уровень", "7"); Stat(91, "Упорство", "96"); Stat(92, "Благочестие", "46");
    }

    [Fact]
    public void RecordedStatsAreReadAsSeparatePairsWithinTheirOwnSections()
    {
        var rows = Read();
        Assert.Equal(20, rows.Count);
        Assert.All(rows, row => Assert.Equal(2, row.Cells.Count));
        Assert.Equal(new[] { "Сила", "Ловкость", "Живучесть", "Интеллект", "Дух" },
            rows.Where(r => r.Section == "Атрибуты").Select(r => r.Cells[0].Text));
        Assert.Equal(new[] { "Крит. удар", "Решительность", "Точный удар" },
            rows.Where(r => r.Section == "Атака").Select(r => r.Cells[0].Text));
        Assert.Equal(new[] { "Физическая", "Магическая" },
            rows.Where(r => r.Section == "Защита").Select(r => r.Cells[0].Text));
        Assert.Contains(rows, r => r.Section == "Физическая атака" && r.Cells[0].Text == "Сила атаки" && r.Cells[1].Text == "22");
        Assert.Contains(rows, r => r.Section == "Магия" && r.Cells[0].Text == "Сила атаки" && r.Cells[1].Text == "64");
        Assert.DoesNotContain(rows, r => r.Section is "Сбор" or "Ремесло");
    }

    [Fact]
    public void CurrentAndMaximumHaveOneLabelAndNeverBecomeOrphanNumbers()
    {
        var rows = Read();
        Assert.Equal(new[] { "ОЗ", "248 из 248" }, rows[0].Cells.Select(c => c.Text));
        Assert.Equal(new[] { "ОМ", "10000 из 10000" }, rows[1].Cells.Select(c => c.Text));
        var reader = new TableReader(); reader.Refresh(rows);
        Assert.StartsWith("Характеристики. ОЗ: 248 из 248", reader.SpeakRow());
        reader.MoveColumn(1); Assert.EndsWith("ОЗ: 248 из 248", reader.SpeakCell());
        reader.MoveRow(1); Assert.StartsWith("ОМ: 10000 из 10000", reader.SpeakRow());
    }

    [Fact]
    public void RepeatingAfterValueChangeKeepsTheSameStatAndDoesNotRepeatHeading()
    {
        var reader = new TableReader(); reader.Refresh(Read()); reader.MoveRow(2); reader.MoveColumn(1);
        Assert.EndsWith("Сила: 22", reader.SpeakCell());
        Stat(31, "Сила", "0"); reader.Refresh(Read());
        Assert.Equal(2, reader.RowIndex); Assert.Equal(1, reader.ColumnIndex);
        Assert.EndsWith("Сила: 0", reader.SpeakCell());
        Assert.DoesNotContain("Атрибуты", reader.SpeakRow());
    }

    [Fact]
    public void HiddenPoolsAndPanelsCannotLeakAcrossJobChanges()
    {
        Node(8)->NodeFlags = 0; Node(14)->NodeFlags = NodeFlags.Visible;
        Node(58)->NodeFlags = 0; Node(73)->NodeFlags = NodeFlags.Visible;
        var rows = Read();
        Assert.DoesNotContain(rows, r => r.Cells[0].Text == "ОМ" || r.Section == "Магия");
        Assert.Contains(rows, r => r.Cells[0].Text == "ОР" && r.Cells[1].Text == "180 из 200");
        Assert.Equal(new[] { "Мастерство", "Контроль" }, rows.Where(r => r.Section == "Ремесло").Select(r => r.Cells[0].Text));
    }

    [Fact]
    public void HiddenOrEmptyFieldDoesNotBorrowAValueFromAnotherStat()
    {
        ((AtkResNode*)components[31][3])->NodeFlags = 0;
        Stat(34, "Интеллект", "");
        var rows = Read();
        Assert.DoesNotContain(rows, r => r.Cells[0].Text is "Сила" or "Интеллект");
        Assert.Contains(rows, r => r.Cells[0].Text == "Ловкость" && r.Cells[1].Text == "47");
    }

    [Fact]
    public void UnknownParentageRejectsSpecializedLayout()
    {
        Node(47)->ParentNode = Node(37); // A defence heading attached to attack.
        Assert.False(CharacterStatusTable.TryRead(addon, "Характеристики", GetText, out _));
    }

    [Fact]
    public void NativeGenericReaderKeepsComponentsSeparateAtTheSameScreenHeight()
    {
        var cells = new List<TableReader.Cell>(); var visited = new HashSet<nint>();
        TableNodeReader.Collect(Node(31), cells, visited, GetText, _ => null);
        TableNodeReader.Collect(Node(34), cells, visited, GetText, _ => null);
        var rows = TableReader.Arrange("", cells);
        Assert.Equal(2, rows.Count);
        Assert.Equal(new[] { "Сила", "22" }, rows[0].Cells.Select(c => c.Text));
        Assert.Equal(new[] { "Интеллект", "64" }, rows[1].Cells.Select(c => c.Text));
    }

    [Theory]
    [InlineData(LanguageMode.English, "180 of 200")]
    [InlineData(LanguageMode.German, "180 von 200")]
    [InlineData(LanguageMode.Russian, "180 из 200")]
    public void PoolFormattingUsesSelectedLanguage(LanguageMode language, string expected)
    {
        Loc.Mode = language;
        Assert.Equal(expected, AccessibilityStrings.TableCurrentMaximum("180", "200"));
    }

    private IReadOnlyList<TableReader.Row> Read()
    {
        Assert.True(CharacterStatusTable.TryRead(addon, "Характеристики", GetText, out var rows));
        return rows;
    }
    private string GetText(nint pointer) => texts.GetValueOrDefault(pointer, "");
    private AtkResNode* Node(uint id) => (AtkResNode*)nodes[id];
    private void Text(uint id, string value) => texts[nodes[id]] = value;
    private void Stat(uint id, string label, string value)
    {
        texts[components[id][2]] = label; texts[components[id][3]] = value;
    }
    private T* Allocate<T>(int count = 1) where T : unmanaged
    {
        var p = (T*)NativeMemory.AllocZeroed((nuint)(sizeof(T) * count)); allocations.Add((nint)p); return p;
    }
    private Dictionary<uint, nint> Build(JsonElement source, AtkUldManager* manager, AtkResNode* parent,
        Dictionary<uint, JsonElement> templates)
    {
        var entries = source.EnumerateArray().Select(n => n.EnumerateArray().Select(v => v.GetInt32()).ToArray()).ToArray();
        var map = new Dictionary<uint, nint>();
        foreach (var n in entries)
        {
            var node = n[2] >= 1000 ? (AtkResNode*)Allocate<AtkComponentNode>() : Allocate<AtkResNode>();
            node->NodeId = (uint)n[0]; node->Type = (NodeType)n[2]; node->NodeFlags = NodeFlags.Visible;
            node->ScreenX = n[3]; node->ScreenY = n[4]; node->Width = (ushort)n[5]; node->Height = (ushort)n[6];
            offsets[(nint)node] = (n[3], n[4]);
            map[(uint)n[0]] = (nint)node;
        }
        var list = (AtkResNode**)NativeMemory.AllocZeroed((nuint)(sizeof(nint) * entries.Length)); allocations.Add((nint)list);
        manager->NodeList = list; manager->NodeListCount = (ushort)entries.Length;
        for (var i = 0; i < entries.Length; i++)
        {
            var n = entries[i]; var node = (AtkResNode*)map[(uint)n[0]];
            list[i] = node; node->ParentNode = n[1] == 0 ? parent : (AtkResNode*)map[(uint)n[1]];
            if (n[2] >= 1000)
            {
                var component = Allocate<AtkComponentBase>(); ((AtkComponentNode*)node)->Component = component;
                components[(uint)n[0]] = Build(templates[(uint)n[2]], &component->UldManager, node, templates);
            }
        }
        return map;
    }
    public void Dispose()
    {
        Loc.Mode = previous;
        foreach (var p in allocations) NativeMemory.Free((void*)p);
    }
}
