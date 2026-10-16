using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;

namespace FF14Accessibility.Services;

internal sealed class AoeAutoTurnService
{
    private readonly Configuration _config;
    private readonly IObjectTable _objects;
    private readonly IClientState _client;
    private readonly ICondition _condition;
    private readonly CombatService _combat;
    private readonly IPluginLog _log;
    private readonly WarningVoiceService _voice;
    private readonly TolkService _tolk;
    private readonly AoeAutoTurn _turn = new();
    private readonly IAoeTurnControl _control;
    private readonly Func<long> _clock;
    private readonly Action<string>? _speak;
    private Vector3? _pendingPoint;
    private long _pendingAt;
    private ulong _pendingPlayer;
    private uint _pendingTerritory;
    private float _pendingFacing;
    private bool _guidanceOwned;
    private Vector3? _confirmedPoint;
    private bool _cancelNextFrame;
    private string? _lastBlockReason;
    private long _lastBlockAt;

    internal AoeAutoTurnService(Configuration config, IObjectTable objects, IClientState client,
        ICondition condition, CombatService combat, IPluginLog log, WarningVoiceService voice, TolkService tolk,
        IAoeTurnControl? control = null, Func<long>? clock = null, Action<string>? speak = null)
    {
        _config = config; _objects = objects; _client = client; _condition = condition;
        _combat = combat; _log = log; _voice = voice; _tolk = tolk;
        _control = control ?? new AoeTurnControl();
        _clock = clock ?? (() => Environment.TickCount64);
        _speak = speak;
    }

    internal void Cancel()
    {
        _turn.Cancel();
        _cancelNextFrame = true;
        _pendingPoint = _confirmedPoint = null;
        if (_guidanceOwned) _combat.SetAutoTurnGuidance(null);
    }

    private void Reset()
    {
        _turn.Reset();
        _pendingPoint = _confirmedPoint = null;
        if (_guidanceOwned) _combat.SetAutoTurnGuidance(null);
        _guidanceOwned = false;
        _cancelNextFrame = false;
        _lastBlockReason = null;
    }

    internal void Update(bool allowControl, string? blockedReason = null)
    {
        if (!_config.AutoTurnAoe)
        {
            Reset();
            return;
        }
        var player = _objects.LocalPlayer;
        if (player == null || player.CurrentHp == 0 || !_client.IsLoggedIn)
        {
            Reset();
            return;
        }
        var nativeAllowed = GameWindowFocus.IsActive && player.Address != 0
            && !_condition[ConditionFlag.BetweenAreas] && !_condition[ConditionFlag.BetweenAreas51]
            && !_condition[ConditionFlag.WatchingCutscene] && !_condition[ConditionFlag.WatchingCutscene78]
            && !_condition[ConditionFlag.OccupiedInEvent] && !_condition[ConditionFlag.OccupiedInQuestEvent]
            && !_condition[ConditionFlag.OccupiedInCutSceneEvent]
            && !_condition[ConditionFlag.InFlight] && !_condition[ConditionFlag.Jumping]
            && !_condition[ConditionFlag.Jumping61] && !_condition[ConditionFlag.Diving]
            && !_condition[ConditionFlag.Swimming] && !_condition[ConditionFlag.Mounted];
        if (!nativeAllowed) blockedReason = "inactive window or blocked player state";
        allowControl &= nativeAllowed;
        AoeControlState state = default;
        if (allowControl && _combat.AutoTurnInDanger)
        {
            state = _control.Read(player);
            allowControl &= state.Available && !state.ManualInput;
            if (!allowControl) blockedReason = state.BlockedReason ?? "camera/input unavailable or manual steering";
        }
        var now = _clock();
        var territory = _client.TerritoryType;
        if (_pendingPlayer != player.GameObjectId || _pendingTerritory != territory)
            _pendingPoint = _confirmedPoint = null;
        if ((!allowControl || _combat.AutoTurnUncertain) && _pendingPoint != null) Cancel();
        if (_combat.AutoTurnUncertain && _confirmedPoint != null) Cancel();
        if (allowControl && _confirmedPoint != null && !state.Faces(_pendingFacing)) Cancel();
        // A successful write is not proof that the game kept the rotation.
        // Check both views on a later frame before giving a forward instruction.
        if (_pendingPoint is { } pending && now - _pendingAt >= 150)
        {
            _pendingPoint = null;
            if (state.Faces(_pendingFacing)
                && !_combat.AutoTurnZones.Any(z => AoeEscapeGeometry.ContainsWithMargin(z, pending, 2f))
                && _combat.AutoTurnZones.Any(z => z.Contains(player.Position))
                && _control.CheckPath(player.Position, pending, _combat.AutoTurnZones) is { } checkedPoint
                && Vector3.DistanceSquared(checkedPoint, pending) <= 0.01f)
            {
                _confirmedPoint = pending;
                var distance = Vector2.Distance(new(player.Position.X, player.Position.Z), new(pending.X, pending.Z));
                Speak(AccessibilityStrings.AoeAutoTurnForward(AccessibilityStrings.FormatDistance(distance)));
                _log.Info($"[AoeTurn] Confirmed facing {pending}, distance={distance:F1}m; movement remains manual.");
            }
            else
            {
                Cancel();
                Speak(AccessibilityStrings.AoeAutoTurnNotConfirmed);
                _log.Info("[AoeTurn] Delayed facing/path check failed; guidance cancelled.");
            }
        }
        float? camera = allowControl && _combat.AutoTurnZones.Count > 0 ? state.CameraFacing : null;
        if (_cancelNextFrame) allowControl = false;
        var result = _turn.Update(player.GameObjectId, _client.TerritoryType, _config.AutoTurnAoe,
            allowControl, _combat.AutoTurnUncertain, player.Position, camera,
            _combat.AutoTurnZones, now, _control.CheckPath,
            spot => _control.Turn(player, spot));
        if (_cancelNextFrame) { _turn.Cancel(); _cancelNextFrame = false; }
        // Cancel after context initialization so even the first dangerous
        // frame honours deliberate input before an automatic turn.
        if (state.ManualInput) Cancel();
        if (result == AoeTurnResult.Turned && _turn.TurnedSpot is { } point)
        {
            _confirmedPoint = null;
            _pendingPoint = point;
            _pendingAt = now;
            _pendingPlayer = player.GameObjectId;
            _pendingTerritory = territory;
            _pendingFacing = MathF.Atan2(point.X - player.Position.X, point.Z - player.Position.Z);
        }
        else if (result == AoeTurnResult.NoPath)
        {
            Speak(AccessibilityStrings.AoeAutoTurnNoPath);
            _log.Info("[AoeTurn] No confirmed direct exit; no rotation applied.");
        }
        if (_turn.GuidanceSpot != _confirmedPoint) _confirmedPoint = null;
        // Log the first refusal and changes of cause during this danger. A
        // short cast ends before an exhaustive search can report NoPath.
        if (!_combat.AutoTurnInDanger) _lastBlockReason = null;
        else if (_combat.AutoTurnUncertain)
            TraceBlock(_combat.AutoTurnUncertainReason ?? "unverified attack geometry");
        else if (!allowControl) TraceBlock(blockedReason ?? "control suspended");
        else if (_pendingPoint == null && _confirmedPoint == null && _control.LastPathFailure is { } failure)
            TraceBlock(failure);
        _combat.SetAutoTurnGuidance(allowControl && !_combat.AutoTurnUncertain ? _confirmedPoint : null);
        _guidanceOwned = true;
    }

    private void TraceBlock(string reason)
    {
        if (_lastBlockReason == reason) return;
        var now = _clock();
        if (_lastBlockReason != null && now - _lastBlockAt < 1000) return;
        _lastBlockReason = reason;
        _lastBlockAt = now;
        _log?.Info($"[AoeTurn] Blocked: {reason}; confirmedZones={_combat.AutoTurnZones.Count}.");
    }

    private void Speak(string text)
    {
        if (_speak != null) { _speak(text); return; }
        if (!_voice.Speak(text)) _tolk.SpeakInterrupt(text);
    }
}
