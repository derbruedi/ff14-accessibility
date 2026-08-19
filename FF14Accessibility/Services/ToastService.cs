using System;
using System.Collections.Generic;
using Dalamud.Game.Gui.Toast;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;

namespace FF14Accessibility.Services;

/// <summary>
/// Reads the game's toast popups aloud via IToastGui. Error toasts ("Das
/// Ziel ist zu weit entfernt.", "Die Aktion ist noch nicht bereit.") live
/// ONLY in the _TextError overlay: its PostRefresh never fires (log
/// 2026-07-17: the sole lifecycle event all session was the empty PostSetup
/// at login) and most error toasts are not mirrored into the chat log
/// either - so neither the notification handler nor ChatReaderService ever
/// saw them. The IToastGui events fire on the game's own show-toast call
/// (interface verified via ilspycmd on Dalamud.dll, 2026-07-17).
/// </summary>
public sealed class ToastService : IDisposable
{
    private readonly IToastGui     _toastGui;
    private readonly TolkService   _tolk;
    private readonly Configuration _config;
    private readonly IPluginLog    _log;
#if DEBUG
    private readonly ITargetManager _targets;
#endif

    public ToastService(IToastGui toastGui, ITargetManager targets, TolkService tolk,
                        Configuration config, IPluginLog log)
    {
        _toastGui = toastGui;
        _tolk     = tolk;
        _config   = config;
        _log      = log;
#if DEBUG
        _targets  = targets;
#endif

        _toastGui.ErrorToast += OnErrorToast;
        _toastGui.Toast      += OnNormalToast;
        _toastGui.QuestToast += OnQuestToast;
    }

    private void OnErrorToast(ref SeString message, ref bool isHandled)
    {
        if (!_config.AnnounceErrorToasts) return;
        var text = message.TextValue;
        if (string.IsNullOrWhiteSpace(text)) return;
        _log.Info($"[Toast] Fehler: '{text}'");
#if DEBUG
        LogTargetState(text);
#endif
        // Feedback for an action the user just attempted - interrupt so the
        // reason arrives while the key press is still fresh. Identical
        // repeats within 0.5s are caught by the Tolk debounce.
        _tolk.SpeakInterrupt(text);
    }

#if DEBUG
    /// <summary>
    /// SONDE (2026-08-19): Zustand des ANVISIERTEN Gegners im Moment einer
    /// Fehlermeldung.
    ///
    /// WAS SIE BEANTWORTEN SOLL: Fang-Freibriefe verlangen "schwaeche den Gegner,
    /// dann besaenftige ihn" (Log 2026-08-19: "Kann noch nicht verwendet werden."
    /// fuenfmal, Zaehler blieb bei 0/3). Ab WANN der Gegner schwach genug ist,
    /// steht in KEINER der bekannten Strukturen - weder das Leve-Sheet noch der
    /// Director fuehren eine Schwelle, und Kampflogik ist nichts, was dieses Repo
    /// raten darf. Also wird gemessen statt vermutet: bei jeder Ablehnung landen
    /// HP-Anteil und die vollstaendige Statusliste des Ziels im Log. Sobald ein
    /// Versuch GELINGT, klammern die Zeilen davor und danach die Schwelle ein -
    /// und die Statusliste zeigt zugleich, ob es ueberhaupt an den HP haengt oder
    /// an einem Zustand, den der Gegner erst bekommt.
    ///
    /// Nur bei FEHLERN, nur mit Ziel, nur im Debug-Build: ein paar Zeilen je
    /// misslungenem Versuch. Faellt raus, sobald die Schwelle bekannt ist
    /// (siehe debug_probe_convention).
    /// </summary>
    private void LogTargetState(string toast)
    {
        if (_targets.Target is not Dalamud.Game.ClientState.Objects.Types.IBattleChara bc) return;

        var hp = bc.MaxHp == 0 ? -1 : (int)(bc.CurrentHp * 100u / bc.MaxHp);
        var stati = new List<string>();
        foreach (var s in bc.StatusList)
        {
            if (s.StatusId == 0) continue;
            stati.Add($"{s.StatusId}:'{s.GameData.ValueNullable?.Name.ExtractText() ?? "?"}'");
        }

        _log.Info($"[FangSonde] '{toast}' -> Ziel '{bc.Name.TextValue}' " +
                  $"HP {bc.CurrentHp}/{bc.MaxHp} ({hp} Prozent) " +
                  $"Status: {(stati.Count > 0 ? string.Join(" | ", stati) : "keine")}");
    }
#endif

    private void OnNormalToast(ref SeString message, ref ToastOptions options, ref bool isHandled)
        => AnnounceInfo(message, "Toast");

    private void OnQuestToast(ref SeString message, ref QuestToastOptions options, ref bool isHandled)
        => AnnounceInfo(message, "Quest-Toast");

    private void AnnounceInfo(SeString message, string kind)
    {
        if (!_config.AnnounceInfoToasts) return;
        var text = message.TextValue;
        if (string.IsNullOrWhiteSpace(text)) return;
        // Some info toasts are ALSO drawn via _WideText/_ScreenText (spoken
        // by the notification handler) or echoed into the chat log - skip
        // when the same text just went out on another path.
        if (_tolk.WasRecentlySpoken(text, 6)) return;
        _log.Info($"[Toast] {kind}: '{text}'");
        // Not time-critical - queue behind whatever is being spoken.
        _tolk.Speak(text);
    }

    public void Dispose()
    {
        _toastGui.ErrorToast -= OnErrorToast;
        _toastGui.Toast      -= OnNormalToast;
        _toastGui.QuestToast -= OnQuestToast;
    }
}
