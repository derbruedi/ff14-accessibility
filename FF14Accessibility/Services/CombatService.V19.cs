using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace FF14Accessibility.Services;

public sealed partial class CombatService
{
    private long _attackerWindowUntilMs;
    private readonly Dictionary<uint, byte> _warnedStrong = new();
    private readonly List<uint> _strongStale = [];
    private long _strongScanAtMs;
    private long _strongWarnAtMs;

    // Nach Kampfbeginn gibt die Aggro-Liste Auskunft, wer den Spieler angegriffen hat.
    private unsafe void UpdateAttacker(ICharacter player)
    {
        if (!_config.AnnounceAttacker || Environment.TickCount64 > _attackerWindowUntilMs)
        {
            _attackerWindowUntilMs = 0;
            return;
        }

        var ui = UIState.Instance();
        if (ui == null) return;
        var haters = ui->Hater.Haters;
        var count = Math.Min(ui->Hater.HaterCount, haters.Length);
        uint nearestId = 0;
        var nearestDistance = float.MaxValue;
        var attackerCount = 0;
        for (var i = 0; i < count; i++)
        {
            var id = haters[i].EntityId;
            if (id == 0) continue;
            attackerCount++;
            var candidate = _objectTable.SearchByEntityId(id);
            if (candidate == null) continue;
            var distance = Vector3.Distance(player.Position, candidate.Position);
            if (distance >= nearestDistance) continue;
            nearestDistance = distance;
            nearestId = id;
        }

        if (nearestId == 0 || _objectTable.SearchByEntityId(nearestId) is not IBattleChara attacker)
            return;
        _attackerWindowUntilMs = 0;
        var name = attacker.Name.TextValue;
        if (string.IsNullOrWhiteSpace(name)) name = AccessibilityStrings.TargetFallbackName;
        _tolk.Speak(AccessibilityStrings.AttackedBy(name, attacker.Level,
            AccessibilityStrings.LevelRelation(attacker.Level - player.Level), attackerCount - 1));
    }

    // Nahe Gegner ab drei Stufen ueber dem Spieler werden einmal je Begegnung genannt.
    private void UpdateStrongerEnemyWarning(ICharacter player)
    {
        if (!_config.WarnStrongerEnemies)
        {
            _warnedStrong.Clear();
            return;
        }
        if (ReadRestedAreaIndicator() == true) return;

        var now = Environment.TickCount64;
        if (now - _strongScanAtMs < 250) return;
        _strongScanAtMs = now;

        _strongStale.Clear();
        foreach (var id in _warnedStrong.Keys)
        {
            var candidate = _objectTable.SearchByEntityId(id) as IBattleChara;
            if (candidate == null || candidate.CurrentHp == 0 ||
                Vector3.Distance(player.Position, candidate.Position) > 25f)
                _strongStale.Add(id);
        }
        foreach (var id in _strongStale) _warnedStrong.Remove(id);

        foreach (var obj in _objectTable)
        {
            if (obj is not IBattleChara enemy || !CombatSide.IsEnemy(obj) ||
                !obj.IsTargetable || enemy.CurrentHp == 0 || enemy.Level < player.Level + 3)
                continue;
            var distance = Vector3.Distance(player.Position, obj.Position);
            if (distance > 20f || _warnedStrong.ContainsKey(obj.EntityId) ||
                now - _strongWarnAtMs < 4000) continue;

            _warnedStrong[obj.EntityId] = 1;
            _strongWarnAtMs = now;
            var name = obj.Name.TextValue;
            if (string.IsNullOrWhiteSpace(name)) name = AccessibilityStrings.TargetFallbackName;
            _tolk.SpeakInterrupt(AccessibilityStrings.StrongerEnemyNearby(name, enemy.Level,
                AccessibilityStrings.FormatDistance(distance),
                RouteService.CompassAdjective(player.Position, obj.Position)));
            break;
        }
    }
}
