using System.Reflection;
using System.Runtime.CompilerServices;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using FF14Accessibility.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace Regression.Tests;

public class ExistingBehaviorTests
{
    [Theory]
    [InlineData(BattleNpcSubKind.Pet)]
    [InlineData(BattleNpcSubKind.Buddy)]
    [InlineData(BattleNpcSubKind.Player)]
    [InlineData(BattleNpcSubKind.RaceChocobo)]
    [InlineData(BattleNpcSubKind.LovmMinion)]
    [InlineData(BattleNpcSubKind.NpcPartyMember)]
    public void CompanionsNeverBecomeEnemies(BattleNpcSubKind kind)
        => Assert.False(CombatSide.IsEnemy(Npc(kind, 0)));

    [Theory]
    [InlineData(StatusFlags.PartyMember)]
    [InlineData(StatusFlags.AllianceMember)]
    public void PartyAndAllianceStayAllies(StatusFlags flags)
        => Assert.False(CombatSide.IsEnemy(Npc(BattleNpcSubKind.Combatant, flags)));

    [Fact]
    public void NonAggressiveMonstersRemainDiscoverable()
        => Assert.True(CombatSide.IsEnemy(Npc(BattleNpcSubKind.Combatant, 0)));

    [Fact]
    public void CraftingSelectionRetainsEachActionsRealType()
    {
        var service = (HotbarService)RuntimeHelpers.GetUninitializedObject(typeof(HotbarService));
        var source = typeof(HotbarService).GetNestedType("AssignSource", BindingFlags.NonPublic)!;
        Set(service, "_menuSource", Enum.Parse(source, "CraftActions"));
        Set(service, "_craftSkills", new List<(uint, string, byte, RaptureHotbarModule.HotbarSlotType)>
        {
            (100001, "Basic Synthesis", 1, RaptureHotbarModule.HotbarSlotType.CraftAction),
            (252, "Steady Hand", 9, RaptureHotbarModule.HotbarSlotType.Action)
        });
        var type = typeof(HotbarService).GetProperty("SkillSlotType", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Set(service, "_craftIndex", 0);
        Assert.Equal(RaptureHotbarModule.HotbarSlotType.CraftAction, type.GetValue(service));
        Set(service, "_craftIndex", 1);
        Assert.Equal(RaptureHotbarModule.HotbarSlotType.Action, type.GetValue(service));
        Set(service, "_menuSource", Enum.Parse(source, "Skills"));
        Assert.Equal(RaptureHotbarModule.HotbarSlotType.Action, type.GetValue(service));
    }

    [Fact]
    public void RestartPreservesThePreviousTransferLog()
    {
        var root = Path.Combine(Path.GetTempPath(), "ff14-log-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var old = Path.Combine(root, "FF14Accessibility.log");
        File.WriteAllText(old, "previous session: confirmed move");
        try
        {
            PluginFileLog.Open(root, "test");
            PluginFileLog.Write("new session");
            Assert.Contains("new session", File.ReadAllText(old));
            var archived = Assert.Single(Directory.GetFiles(Path.Combine(root, "logs")));
            Assert.Equal("previous session: confirmed move", File.ReadAllText(archived));
        }
        finally
        {
            typeof(PluginFileLog).GetField("_path", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, null);
            // This generated test directory is the only cleanup target.
            if (Path.GetFullPath(root).StartsWith(Path.Combine(Path.GetTempPath(), "ff14-log-test-"), StringComparison.OrdinalIgnoreCase))
                Directory.Delete(root, recursive: true);
        }
    }

    private static IBattleNpc Npc(BattleNpcSubKind kind, StatusFlags flags) => ChatPlayerTests.Proxy.Of<IBattleNpc>(m => m.Name switch
    {
        "get_ObjectKind" => ObjectKind.BattleNpc,
        "get_BattleNpcKind" => kind,
        "get_StatusFlags" => flags,
        _ => throw new InvalidOperationException(m.Name)
    });

    private static void Set(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(target, value);
}
