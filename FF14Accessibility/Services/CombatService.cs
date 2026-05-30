using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin.Services;

namespace FF14Accessibility.Services;

public sealed class CombatService
{
    private readonly IObjectTable _objectTable;
    private readonly TolkService  _tolk;
    private readonly IPluginLog   _log;

    private bool _wasInCombat   = false;
    private int  _lastHpPercent = 100;

    private static readonly int[] HpThresholds = [75, 50, 25, 10];

    public CombatService(IObjectTable objectTable, TolkService tolk, IPluginLog log)
    {
        _objectTable = objectTable;
        _tolk        = tolk;
        _log         = log;
    }

    // Wird jeden Frame aus Plugin.OnFrameworkUpdate aufgerufen
    public void Update()
    {
        var player = _objectTable.LocalPlayer;
        if (player == null) return;

        var inCombat = (player.StatusFlags & StatusFlags.InCombat) != 0;

        if (inCombat && !_wasInCombat)
        {
            _lastHpPercent = HpPercent(player.CurrentHp, player.MaxHp);
            _tolk.Speak("Kampf.");
        }
        else if (!inCombat && _wasInCombat)
        {
            _tolk.Speak("Kampf vorbei.");
        }
        _wasInCombat = inCombat;

        if (!inCombat) return;

        var hp = HpPercent(player.CurrentHp, player.MaxHp);
        foreach (var threshold in HpThresholds)
        {
            if (_lastHpPercent > threshold && hp <= threshold)
            {
                _tolk.SpeakInterrupt($"HP: {hp} Prozent.");
                break;
            }
        }
        _lastHpPercent = hp;
    }

    // Auf Tastendruck: aktueller HP/MP-Status
    public void AnnounceStatus()
    {
        var player = _objectTable.LocalPlayer;
        if (player == null)
        {
            _tolk.SpeakInterrupt("Nicht eingeloggt.");
            return;
        }

        var hp = HpPercent(player.CurrentHp, player.MaxHp);
        var mp = HpPercent(player.CurrentMp, player.MaxMp);

        var text = player.MaxMp > 0
            ? $"HP {hp} Prozent, MP {mp} Prozent."
            : $"HP {hp} Prozent.";

        _tolk.SpeakInterrupt(text);
    }

    private static int HpPercent(uint current, uint max) =>
        max == 0 ? 0 : (int)(current * 100u / max);
}
