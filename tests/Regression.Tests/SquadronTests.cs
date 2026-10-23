using System.Runtime.InteropServices;
using System.Text.Json;
using FF14Accessibility;
using FF14Accessibility.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Regression.Tests;

[Collection("Language")]
public sealed unsafe class SquadronTests : IDisposable
{
    private readonly LanguageMode _previous = Loc.Mode;
    public SquadronTests() => Loc.Mode = LanguageMode.Russian;
    public void Dispose() => Loc.Mode = _previous;

    [Fact]
    public void MissionReadsBothTripletsAndZeroWithoutGuessingFromHoveredMission()
    {
        using var ui = new Layout("GcArmyExpeditionSelect");
        ui.Text("Требуемые характеристики", 39); ui.Text("Текущие характеристики", 42);
        ui.Attributes(40, "80", "14", "16"); ui.Attributes(43, "79", "0", "17");
        ui.Text("Затраты", 16); ui.Text("10500", 17); ui.Text("7500", 21, 2);
        var text = ui.Page("GcArmyExpedition");
        Assert.Contains("Требуемые характеристики: Физические навыки: 80. Ментальные навыки: 14. Тактические навыки: 16", text);
        Assert.Contains("Текущие характеристики: Физические навыки: 79. Ментальные навыки: 0. Тактические навыки: 17", text);
        Assert.Contains("Затраты: 10500", text); Assert.Contains("Опыт: 7500", text);
        Assert.Equal(1, Count(text, "Физические навыки: 80"));
        ui.Attributes(40, "68", "28", "14");
        Assert.DoesNotContain("Физические навыки: 80", ui.Page("GcArmyExpedition"));
        Assert.Contains("Физические навыки: 68", ui.Page("GcArmyExpedition"));
    }

    [Theory]
    [InlineData(35u, "Физические навыки", "80", "79")]
    [InlineData(36u, "Ментальные навыки", "14", "0")]
    [InlineData(37u, "Тактические навыки", "16", "17")]
    public void MissionAttributeIconsReadTheirColumnAndBothValues(uint icon, string label, string required, string current)
    {
        using var ui = new Layout("GcArmyExpeditionSelect");
        ui.Attributes(40, "80", "14", "16"); ui.Attributes(43, "79", "0", "17");
        var text = ui.Focus("GcArmyExpedition", icon);
        Assert.StartsWith(label, text);
        Assert.Contains("Требуемые характеристики: " + required, text);
        Assert.Contains("Текущие характеристики: " + current, text);
    }

    [Fact]
    public void MemberRowFromPlayerLogNamesLevelAllAttributesChemistryAndTactics()
    {
        using var ui = new Layout("GcArmyMemberList");
        ui.Text("Сесиль", 19, 2, 9); ui.Text("8", 19, 2, 7);
        ui.Text("80", 19, 2, 17, 2); ui.Text("14", 19, 2, 19, 2); ui.Text("16", 19, 2, 21, 2);
        ui.Text("Вольно (Мастерство: 1)", 19, 2, 15);
        ui.Text("Награда за совместную миссию", 19, 2, 22);
        var text = ui.Focus("GcArmyMemberList", 19, 2);
        Assert.StartsWith("Сесиль. Уровень 8", text);
        Assert.Contains("Физические навыки: 80. Ментальные навыки: 14. Тактические навыки: 16", text);
        Assert.Contains("Боевая тактика: Вольно (Мастерство: 1)", text);
        Assert.Contains("Совместимость: Награда за совместную миссию", text);
        Assert.DoesNotContain("/", text);
        Assert.Equal(1, Count(ui.Page("GcArmyMemberList"), "Сесиль"));
    }

    [Fact]
    public void ReusedMemberRendererReadsFreshNameAndValuesAndKeepsEqualNumbers()
    {
        using var ui = new Layout("GcArmyMemberList");
        ui.Text("Сесиль", 19, 2, 9); ui.Text("8", 19, 2, 7);
        ui.Text("14", 19, 2, 17, 2); ui.Text("14", 19, 2, 19, 2); ui.Text("14", 19, 2, 21, 2);
        Assert.Contains("Сесиль", ui.Focus("GcArmyMemberList", 19, 2));
        ui.Node(19, 2)->NodeId = 32; // runtime renderer id in the player's log
        ui.Text("Нанасоми", 19, 2, 9); ui.Text("79", 19, 2, 17, 2);
        var text = ui.Focus("GcArmyMemberList", 19, 2);
        Assert.DoesNotContain("Сесиль", text); Assert.StartsWith("Нанасоми", text);
        Assert.Contains("Физические навыки: 79. Ментальные навыки: 14. Тактические навыки: 14", text);
    }

    [Fact]
    public void VirtualMemberRowsComeFromLiveRendererBindingsInsteadOfStaticTemplateNodes()
    {
        using var ui = new Layout("GcArmyMemberList");
        ui.Text("Лабонрит", 19, 2, 9); ui.Text("8", 19, 2, 7);
        var list = (AtkComponentList*)((AtkComponentNode*)ui.Node(19))->Component;
        var row = (AtkComponentListItemRenderer*)((AtkComponentNode*)ui.Node(19, 2))->Component;
        list->ListLength = 100; row->ListItemIndex = 40;
        for (var i = 0; i < list->UldManager.NodeListCount; i++)
            if (list->UldManager.NodeList[i] == ui.Node(19, 2)) list->UldManager.NodeList[i] = null;
        Assert.Contains("Лабонрит. Уровень 8", ui.Page("GcArmyMemberList"));
        row->ListItemIndex = 100; // a stale/recycled slot no longer names a row
        Assert.DoesNotContain("Лабонрит", ui.Page("GcArmyMemberList"));
        Assert.Empty(ui.Focus("GcArmyMemberList", 19, 2));
    }

    [Fact]
    public void ItemRewardNameAndQuantityComeFromThatVisibleSlot()
    {
        using var ui = new Layout("GcArmyExpeditionSelect");
        ui.Text("3", 22, 2, 7);
        var itemAddress = (nint)ui.Node(22);
        var page = SquadronReader.Page("GcArmyExpedition", ui.Addon, ui.Read,
            p => p == itemAddress ? "3 × Боевой сборник" : "wrong item");
        Assert.Contains("Основная награда: 3 × Боевой сборник", page);
        Assert.DoesNotContain("wrong item", page);
        ui.Node(22)->NodeFlags = 0;
        Assert.DoesNotContain("Боевой сборник", SquadronReader.Page("GcArmyExpedition", ui.Addon, ui.Read,
            _ => "Боевой сборник"));
    }

    [Fact]
    public void MissionRosterReadsItsOwnDifferentRowLayout()
    {
        using var ui = new Layout("GcArmyExpeditionSelect");
        ui.Text("Асталойя", 44, 2, 3); ui.Text("8", 44, 2, 10);
        ui.Text("68", 44, 2, 8, 2, 2); ui.Text("14", 44, 2, 8, 4, 2); ui.Text("28", 44, 2, 8, 6, 2);
        var text = ui.Focus("GcArmyExpedition", 44, 2);
        Assert.StartsWith("Асталойя. Уровень 8", text);
        Assert.Contains("Физические навыки: 68. Ментальные навыки: 14. Тактические навыки: 28", text);
    }

    [Fact]
    public void ProfileKeepsPersonalDetailsAndDescribesTextlessAttributeControls()
    {
        using var ui = new Layout("GcArmyMemberProfile");
        ui.Text("Сесиль", 9); ui.Text("Хюранка", 10); ui.Text("Лучница", 14); ui.Text("8", 15);
        ui.Text("80", 19); ui.Text("14", 21); ui.Text("16", 23);
        ui.Text("При совместной миссии повышает награду.", 25);
        ui.Text("Новая совместимость", 26); ui.Text("Условия совместимости", 27);
        ui.Text("Вольно", 7, 2); // exact key=70002 in the supplied player log
        ui.Text("Поменять класс", 36, 2);
        var text = ui.Page("GcArmyMenberProfile");
        foreach (var expected in new[] { "Сесиль", "Хюранка", "Лучница", "8", "Физические навыки: 80", "Ментальные навыки: 14", "Тактические навыки: 16", "Совместимость: При совместной миссии", "Новая совместимость", "Боевая тактика: Вольно", "Поменять класс" })
            Assert.Contains(expected, text);
        Assert.Equal("Физические навыки: 80", ui.Focus("GcArmyMenberProfile", 18));
        Assert.Equal("Поменять класс", ui.Focus("GcArmyMenberProfile", 36));
    }

    [Fact]
    public void HiddenProfileParentsAndWrongWindowsCannotLeakNumbers()
    {
        using var ui = new Layout("GcArmyMemberProfile");
        ui.Text("80", 19); ui.Node(17)->NodeFlags = 0;
        Assert.DoesNotContain("80", ui.Page("GcArmyMenberProfile"));
        Assert.Empty(ui.Focus("GcArmyMenberProfile", 18));
        ui.Node(17)->NodeFlags = NodeFlags.Visible;
        Assert.Empty(ui.Page("OtherWindow"));
        ui.Addon->IsVisible = false;
        Assert.Empty(ui.Page("GcArmyMenberProfile"));
    }

    [Fact]
    public void ForeignControlWithSameIdAndTooltipDoesNotBecomeASquadronControl()
    {
        using var first = new Layout("GcArmyMemberProfile");
        using var second = new Layout("GcArmyMemberProfile");
        first.Text("80", 19);
        Assert.Empty(SquadronReader.Focus("GcArmyMenberProfile", first.Addon, second.Node(18), first.Read, _ => "wrong"));
    }

    [Fact]
    public void BlockingDialogPreventsReadingUnderlyingSquadronPageAndControls()
    {
        using var ui = new Layout("GcArmyMemberProfile");
        ui.Text("80", 19); ui.Addon->NumBlockingAddons = 1;
        Assert.Empty(ui.Page("GcArmyMenberProfile"));
        Assert.Empty(ui.Focus("GcArmyMenberProfile", 18));
    }

    [Theory]
    [InlineData("GcArmyTrainingList", "GcArmyTraining")]
    [InlineData("GcArmyChangeClass", "GcArmyChangeClass")]
    [InlineData("GcArmyChangeExpeditionTrait", "GcArmyChangeExpeditionTrait")]
    [InlineData("GcArmyChangeTactic", "GcArmyChangeTactic")]
    [InlineData("GcArmyCaptureSelect", "GcArmyCapture")]
    [InlineData("GcArmyExpeditionResult", "GcArmyExpeditionResult")]
    public void AdjacentWindowsPreserveVisibleTextAndSingleDigitNumbers(string layout, string name)
    {
        using var ui = new Layout(layout);
        var texts = ui.TextNodes.Take(2).ToArray();
        ui.Set(texts[0], "Сведения отряда"); ui.Set(texts[1], "0");
        Assert.Contains("Сведения отряда", ui.Page(name));
        Assert.Contains("0", ui.Page(name));
        ((AtkResNode*)texts[1])->NodeFlags = 0;
        Assert.DoesNotContain("0", ui.Page(name));
    }

    [Fact]
    public void TrainingStatsKeepCurrentValueAndChangeIncludingSingleDigitZero()
    {
        using var ui = new Layout("GcArmyTrainingList");
        ui.Text("80", 24, 2, 2); ui.Text("+0", 24, 2, 4);
        Assert.Contains("Физические навыки: сейчас 80. без изменений", ui.Page("GcArmyTraining"));
    }

    [Theory]
    [InlineData("+40", "увеличится на 40")]
    [InlineData("-20", "уменьшится на 20")]
    [InlineData("−20", "уменьшится на 20")]
    [InlineData("＋40", "увеличится на 40")]
    [InlineData("+0", "без изменений")]
    [InlineData("0", "без изменений")]
    [InlineData("", "изменение не показано")]
    [InlineData("40", "изменение по данным окна: 40")]
    public void TrainingExplainsVisibleSignsWithoutInventingMissingChanges(string change, string expected)
    {
        Assert.Equal("Физические навыки: сейчас 80. " + expected, SquadronReader.TrainingAttribute(0, "80", change));
    }

    [Fact]
    public void TrainingHoverStaysBriefWhileManualReadHasFreshGainsLossesAndZero()
    {
        using var ui = new Layout("GcArmyTrainingList");
        ui.Text("Физическая подготовка", 15);
        ui.Text("80", 24, 2, 2); ui.Text("+40", 24, 2, 4);
        ui.Text("14", 25, 2, 2); ui.Text("-20", 25, 2, 4);
        ui.Text("0", 26, 2, 2); ui.Text("0", 26, 2, 4);
        string? Tooltip(nint address) => address == (nint)ui.Node(6) ? "Физическая подготовка\nДлинное описание" : null;
        var focus = ui.Node(6, 8); // collision child, tooltip belongs to its button
        Assert.Equal("Физическая подготовка", SquadronReader.Focus("GcArmyTraining", ui.Addon, focus, ui.Read, Tooltip));
        var details = SquadronReader.TrainingDetails(ui.Addon, focus, ui.Read, Tooltip);
        Assert.Contains("Предпросмотр тренировки: Физическая подготовка", details);
        Assert.Contains("Физические навыки: сейчас 80. увеличится на 40", details);
        Assert.Contains("Ментальные навыки: сейчас 14. уменьшится на 20", details);
        Assert.Contains("Тактические навыки: сейчас 0. без изменений", details);
        Assert.DoesNotContain("Длинное описание", details);
        ui.Text("100", 24, 2, 2); ui.Text("+20", 24, 2, 4);
        Assert.Equal("Физическая подготовка", SquadronReader.Focus("GcArmyTraining", ui.Addon, focus, ui.Read, Tooltip));
        details = SquadronReader.TrainingDetails(ui.Addon, focus, ui.Read, Tooltip);
        Assert.Contains("сейчас 100. увеличится на 20", details);
        Assert.DoesNotContain("сейчас 80", details);
        Assert.Equal("Физические навыки", ui.Focus("GcArmyTraining", 24, 2, 2));
    }

    [Fact]
    public void TrainingReadDoesNotAssignPreviousPreviewToAnotherHoveredExercise()
    {
        using var ui = new Layout("GcArmyTrainingList");
        ui.Text("Физическая подготовка", 15);
        ui.Text("80", 24, 2, 2); ui.Text("+40", 24, 2, 4);
        var details = SquadronReader.TrainingDetails(ui.Addon, ui.Node(7), ui.Read, _ => "Ментальная подготовка");
        Assert.Contains("Предпросмотр тренировки: Физическая подготовка", details);
        Assert.Contains(AccessibilityStrings.SquadronTrainingPreviewDiffers, details);
        Assert.DoesNotContain("Предпросмотр тренировки: Ментальная подготовка", details);
    }

    [Fact]
    public void EmptyTrainingPreviewReportsLoadingInsteadOfInventingZeroChanges()
    {
        using var ui = new Layout("GcArmyTrainingList");
        var text = SquadronReader.TrainingDetails(ui.Addon, ui.Node(6), ui.Read, _ => null);
        Assert.Contains(AccessibilityStrings.SquadronLoading, text);
        Assert.DoesNotContain("без изменений", text);
    }

    [Theory]
    [InlineData(0)] // hidden value
    [InlineData(1)] // blocking confirmation
    [InlineData(2)] // another window
    [InlineData(3)] // invisible window
    public void TrainingReadRejectsHiddenBlockedAndForeignValues(int condition)
    {
        using var ui = new Layout("GcArmyTrainingList");
        using var other = new Layout("GcArmyTrainingList");
        ui.Text("80", 24, 2, 2); ui.Text("+40", 24, 2, 4);
        if (condition == 0) ui.Node(24)->NodeFlags = 0;
        if (condition == 1) ui.Addon->NumBlockingAddons = 1;
        if (condition == 3) ui.Addon->IsVisible = false;
        var details = SquadronReader.TrainingDetails(ui.Addon, condition == 2 ? other.Node(6) : ui.Node(6), ui.Read, _ => null);
        Assert.DoesNotContain("80", details); Assert.DoesNotContain("40", details);
        if (condition > 0) Assert.Empty(details);
    }

    [Theory]
    [InlineData(LanguageMode.Russian, "Физические навыки: сейчас 80. уменьшится на 20")]
    [InlineData(LanguageMode.English, "Physical: current 80. decreases by 20")]
    [InlineData(LanguageMode.German, "Körperlich: aktuell 80. sinkt um 20")]
    public void TrainingChangeUsesSelectedLanguage(LanguageMode mode, string expected)
    {
        Loc.Mode = mode;
        Assert.Equal(expected, SquadronReader.TrainingAttribute(0, "80", "-20"));
    }

    [Fact]
    public void RewardTranslatesOnlyMatchingIconsAndReusesNamesUntilLanguageOrPackChanges()
    {
        var state = new SquadronReward();
        var rows = Enumerable.Range(1, 50000).ToArray();
        var translated = new List<int>();
        string Name(int row) { translated.Add(row); return "Награда " + row; }
        Assert.Equal("Награда 123", state.Resolve(123, LanguageMode.Russian, 1, rows, row => (uint)row, Name));
        Assert.Equal([123], translated);
        Assert.Equal("Награда 123", state.Resolve(123, LanguageMode.Russian, 1, rows, row => (uint)row, Name));
        Assert.Equal([123], translated);
        state.Resolve(123, LanguageMode.English, 1, rows, row => (uint)row, Name);
        state.Resolve(123, LanguageMode.English, 2, rows, row => (uint)row, Name);
        Assert.Equal([123, 123, 123], translated);
        Assert.Empty(state.Resolve(0, LanguageMode.English, 2, rows, row => (uint)row, Name));
        Assert.Empty(state.Resolve(60000, LanguageMode.English, 2, rows, row => (uint)row, Name));
        Assert.Equal([123, 123, 123], translated);
    }

    [Fact]
    public void RewardKeepsLastNonemptyMatchingItemAsBeforeAndDoesNotTranslateUnrelatedRows()
    {
        var state = new SquadronReward();
        var rows = new[] { (Icon: 5u, Name: "Первая"), (Icon: 5u, Name: "Вторая"), (Icon: 5u, Name: ""), (Icon: 9u, Name: "Чужая") };
        var calls = 0;
        Assert.Equal("Вторая", state.Resolve(5, LanguageMode.Russian, 1, rows, row => row.Icon, row => { calls++; return row.Name; }));
        Assert.Equal(3, calls);
    }

    [Fact]
    public void DelayedAndRepeatedPageReadsWaitForStableValuesAndResetOnClose()
    {
        var state = new SquadronAnnouncement();
        Assert.Empty(state.Update("", true, 0));
        Assert.Empty(state.Update("Сесиль. 80", true, 10));
        Assert.Empty(state.Update("Сесиль. 81", true, 100));
        Assert.Equal("Сесиль. 81", state.Update("Сесиль. 81", true, 250));
        Assert.Empty(state.Update("Сесиль. 81", true, 500));
        Assert.Equal("Сесиль. 81", state.Update("Сесиль. 81", true, 501, repeat: true));
        state.Update("", false, 600);
        Assert.Empty(state.Update("Сесиль. 81", true, 700));
        Assert.Equal("Сесиль. 81", state.Update("Сесиль. 81", true, 850));
    }

    [Theory]
    [InlineData("00:15", "00:14")]
    [InlineData("01:00:00", "00:59:59")]
    public void CountdownTicksStayQuietButStatChangesAndManualRepeatReadActualValues(string first, string second)
    {
        var state = new SquadronAnnouncement();
        state.Update("80. " + first, true, 0);
        Assert.Equal("80. " + first, state.Update("80. " + first, true, 150));
        Assert.Empty(state.Update("80. " + second, true, 1000));
        Assert.Equal("80. " + second, state.Update("80. " + second, true, 1001, repeat: true));
        Assert.Empty(state.Update("81. " + second, true, 2000));
        Assert.Equal("81. " + second, state.Update("81. " + second, true, 2150));
    }

    [Theory]
    [InlineData(LanguageMode.Russian, "Физические навыки")]
    [InlineData(LanguageMode.English, "Physical")]
    [InlineData(LanguageMode.German, "Körperlich")]
    public void AddedLabelsRespectSelectedLanguage(LanguageMode mode, string label)
    {
        Loc.Mode = mode;
        Assert.Equal(label, SquadronReader.AttributeName(0));
    }

    [Fact]
    public void CorruptNodeCountsAndCyclicComponentLinksStayBounded()
    {
        using var ui = new Layout("GcArmyMemberProfile");
        ui.Addon->UldManager.NodeListCount = 513;
        Assert.Empty(ui.Page("GcArmyMenberProfile"));
        ui.Addon->UldManager.NodeListCount = 42;
        var component = ((AtkComponentNode*)ui.Node(18))->Component;
        component->UldManager.NodeList[0] = ui.Node(18);
        Assert.NotNull(ui.Page("GcArmyMenberProfile"));
    }

    private static int Count(string text, string value) => text.Split(value).Length - 1;

    private sealed class Layout : IDisposable
    {
        private readonly List<nint> allocations = [];
        private readonly Dictionary<nint, string> texts = [];
        private readonly Dictionary<nint, Dictionary<uint, nint>> children = [];
        private readonly Dictionary<uint, nint> nodes;
        internal AtkUnitBase* Addon { get; }
        internal IEnumerable<nint> TextNodes => texts.Keys;
        internal Layout(string name)
        {
            using var stream = typeof(SquadronTests).Assembly.GetManifestResourceStream("Regression.Tests.Fixtures.squadron-layout.json")!;
            using var json = JsonDocument.Parse(stream);
            var layout = json.RootElement.EnumerateArray().Single(l => l.GetProperty("Name").GetString() == name);
            var templates = layout.GetProperty("Components").EnumerateArray().ToDictionary(c => c.GetProperty("Id").GetUInt32());
            Addon = Allocate<AtkUnitBase>(); Addon->IsVisible = true;
            nodes = Build(layout.GetProperty("Nodes"), &Addon->UldManager, null, templates);
            Addon->RootNode = (AtkResNode*)nodes[1];
        }
        internal AtkResNode* Node(params uint[] path)
        {
            var current = nodes[path[0]];
            foreach (var id in path.Skip(1)) current = children[current][id];
            return (AtkResNode*)current;
        }
        internal void Text(string value, params uint[] path) => Set((nint)Node(path), value);
        internal void Set(nint node, string value) => texts[node] = value;
        internal string Read(nint node) => texts.GetValueOrDefault(node, "");
        internal string Page(string name) => SquadronReader.Page(name, Addon, Read);
        internal string Focus(string name, params uint[] path) => SquadronReader.Focus(name, Addon, Node(path), Read, _ => null);
        internal void Attributes(uint id, string physical, string mental, string tactical)
        { Text(physical, id, 2, 2); Text(mental, id, 4, 2); Text(tactical, id, 6, 2); }
        private T* Allocate<T>(int count = 1) where T : unmanaged
        { var p = (T*)NativeMemory.AllocZeroed((nuint)(sizeof(T) * count)); allocations.Add((nint)p); return p; }
        private Dictionary<uint, nint> Build(JsonElement entries, AtkUldManager* manager, AtkResNode* parent, Dictionary<uint, JsonElement> templates)
        {
            var rows = entries.EnumerateArray().ToArray();
            Dictionary<uint, nint> map = [];
            foreach (var row in rows)
            {
                var type = row.GetProperty("Type").GetInt32();
                var node = (AtkResNode*)Allocate<AtkComponentNode>();
                node->NodeId = row.GetProperty("Id").GetUInt32(); node->Type = (NodeType)type;
                node->NodeFlags = NodeFlags.Visible | NodeFlags.Enabled;
                node->X = row.GetProperty("X").GetSingle(); node->Y = row.GetProperty("Y").GetSingle();
                map[node->NodeId] = (nint)node;
                if (type == 3) texts[(nint)node] = ""; // ULD defaults are not runtime values.
            }
            manager->NodeList = (AtkResNode**)Allocate<nint>(rows.Length); manager->NodeListCount = (ushort)rows.Length;
            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i]; var node = (AtkResNode*)map[row.GetProperty("Id").GetUInt32()];
                manager->NodeList[i] = node;
                var parentId = row.GetProperty("Parent").GetUInt32(); node->ParentNode = parentId == 0 ? parent : (AtkResNode*)map[parentId];
                var type = row.GetProperty("Type").GetUInt32();
                if (type < 1000) continue;
                var component = (AtkComponentBase*)Allocate<AtkComponentList>(); ((AtkComponentNode*)node)->Component = component;
                component->OwnerNode = (AtkComponentNode*)node;
                var info = Allocate<AtkUldComponentInfo>(); info->ComponentType = (ComponentType)templates[type].GetProperty("Type").GetInt32();
                component->UldManager.Objects = (AtkUldObjectInfo*)info;
                children[(nint)node] = Build(templates[type].GetProperty("Nodes"), &component->UldManager, node, templates);
                if (info->ComponentType == ComponentType.List)
                {
                    var list = (AtkComponentList*)component;
                    var renderers = templates[type].GetProperty("Nodes").EnumerateArray()
                        .Where(n => n.GetProperty("Type").GetUInt32() >= 1000
                            && templates[n.GetProperty("Type").GetUInt32()].GetProperty("Type").GetInt32() == (int)ComponentType.ListItemRenderer)
                        .Select(n => children[(nint)node][n.GetProperty("Id").GetUInt32()]).ToArray();
                    list->ListLength = renderers.Length; list->AllocatedItemRendererListLength = renderers.Length;
                    list->ItemRendererList = Allocate<AtkComponentList.ListItem>(Math.Max(1, renderers.Length));
                    for (var slot = 0; slot < renderers.Length; slot++)
                    {
                        var renderer = (AtkComponentListItemRenderer*)((AtkComponentNode*)renderers[slot])->Component;
                        renderer->ListItemIndex = slot; list->ItemRendererList[slot].AtkComponentListItemRenderer = renderer;
                    }
                }
            }
            return map;
        }
        public void Dispose() { foreach (var p in allocations) NativeMemory.Free((void*)p); }
    }
}
