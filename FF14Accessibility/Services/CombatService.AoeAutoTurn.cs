using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace FF14Accessibility.Services;

public sealed partial class CombatService
{
    private readonly List<DangerZone> _autoTurnZones = new();
    internal IReadOnlyList<DangerZone> AutoTurnZones => _autoTurnZones;
    internal bool AutoTurnUncertain { get; private set; }
    internal string? AutoTurnUncertainReason { get; private set; }
    internal bool AutoTurnInDanger { get; private set; }

    internal void SetAutoTurnGuidance(Vector3? spot) => _escape.SetAutoTurnGuidance(AutoTurnInDanger, spot);

    /// <summary>Refresh before modal UI can end the framework frame. Auto-turn
    /// never consumes danger data retained from a previous frame or territory.</summary>
    public void UpdateAreaWarnings()
    {
        _autoTurnZones.Clear();
        AutoTurnUncertain = false;
        AutoTurnUncertainReason = null;
        AutoTurnInDanger = false;
        var player = _objectTable.LocalPlayer;
        if (player == null || player.CurrentHp == 0)
        {
            _aoeWarn.SetActive(false);
            _escape.Clear();
            _escapeSpoken = false;
            _castsAtMe.Clear();
            _aoeInside.Clear();
            return;
        }
        UpdateEnemyCastWarnings(player.GameObjectId, player.Position, player.Rotation);
    }

    private void CollectAutoTurnZone(IBattleChara caster, LuminaAction row, DangerZone? zone,
        bool followsPlayer, Vector3 playerPos)
    {
        if (zone is not { } geometry || !AoeEscapeGeometry.IsValid(geometry))
        {
            AutoTurnUncertain = true;
            AutoTurnUncertainReason ??= $"invalid geometry: action={row.RowId}";
            return;
        }
        AutoTurnInDanger |= geometry.Contains(playerPos);
        var omen = row.Omen.ValueNullable?.Path.ExtractText() ?? string.Empty;
        var circleOnCaster = CircleOnCaster(caster.GameObjectId, caster.CastTargetObjectId, row);
        if (!row.AffectsPosition && ReliableAutoTurnShape(row.CastType, omen, circleOnCaster,
                followsPlayer, row.XAxisModifier))
            _autoTurnZones.Add(geometry);
        else if (row.AffectsPosition || !AoeShape.HasProvenShape(row.CastType)
            || row.CastType is AoeShape.CastTypeCircle or AoeShape.CastTypeCircle5
                && !circleOnCaster
            || AoeEscapeGeometry.ContainsWithMargin(geometry, playerPos, 25f))
        {
            AutoTurnUncertain = true;
            AutoTurnUncertainReason ??= $"unverified shape/centre: action={row.RowId}, type={row.CastType}, target={caster.CastTargetObjectId:X}, omen={omen}";
        }
    }

    internal static bool CircleOnCaster(ulong casterId, ulong targetId, LuminaAction row)
        => !row.AffectsPosition && CircleOnCaster(casterId, targetId, row.TargetArea, row.CanTargetSelf,
            row.CanTargetParty || row.CanTargetAlliance || row.CanTargetHostile || row.CanTargetAlly
            || row.CanTargetOwnPet || row.CanTargetPartyPet, row.Range);

    internal static bool CircleOnCaster(ulong casterId, ulong targetId, bool targetArea,
        bool canTargetSelf, bool canTargetOther, int range)
    {
        if (targetArea) return false;
        if (targetId == casterId && casterId != 0) return true;
        // Self AoEs can have no object target. Accept the game's no-target
        // sentinels only for zero-range, self actions, never a ground target.
        return casterId != 0 && targetId is 0 or 0xE0000000
            && canTargetSelf && !canTargetOther && range == 0;
    }

    internal static bool ReliableAutoTurnShape(byte castType, string omen, bool circleOnCaster,
        bool followsPlayer, float lineWidth)
    {
        if (!AoeShape.HasProvenShape(castType) || string.IsNullOrWhiteSpace(omen) || followsPlayer) return false;
        if (castType is AoeShape.CastTypeCircle or AoeShape.CastTypeCircle5)
            // A moving target's current position is not a measured ground centre.
            // Keep the existing audio guidance, but do not automate this guess.
            return circleOnCaster && omen.Contains("general", StringComparison.OrdinalIgnoreCase);
        if (castType is AoeShape.CastTypeLine or AoeShape.CastTypeLine8 or AoeShape.CastTypeLine12)
            // XAxisModifier as half-width is still an unverified assumption.
            // Keep warning audio; it must not steer the player automatically.
            return false;
        var fan = omen.IndexOf("fan", StringComparison.OrdinalIgnoreCase);
        if (fan < 0) return false;
        var start = fan + 3;
        var end = start;
        while (end < omen.Length && char.IsDigit(omen[end])) end++;
        return end > start && int.TryParse(omen.AsSpan(start, end - start), out var degrees)
            && degrees > 0 && degrees <= 180;
    }
}
