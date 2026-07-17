using System;
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

    public ToastService(IToastGui toastGui, TolkService tolk, Configuration config, IPluginLog log)
    {
        _toastGui = toastGui;
        _tolk     = tolk;
        _config   = config;
        _log      = log;

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
        // Feedback for an action the user just attempted - interrupt so the
        // reason arrives while the key press is still fresh. Identical
        // repeats within 0.5s are caught by the Tolk debounce.
        _tolk.SpeakInterrupt(text);
    }

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
