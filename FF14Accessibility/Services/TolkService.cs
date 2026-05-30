using System.Diagnostics;
using Dalamud.Plugin.Services;
using FF14Accessibility.Native;

namespace FF14Accessibility.Services;

public sealed class TolkService : IDisposable
{
    private readonly IPluginLog _log;

    public bool IsAvailable => TolkNative.Tolk_IsLoaded();
    public string? DetectedScreenReader { get; private set; }

    public TolkService(IPluginLog log)
    {
        _log = log;
        Initialize();
    }

    private void Initialize()
    {
        try
        {
            TolkNative.Tolk_Load();
            if (TolkNative.Tolk_IsLoaded())
            {
                DetectedScreenReader = TolkNative.GetScreenReaderName();
                _log.Info($"[Accessibility] Tolk geladen. Screenreader: {DetectedScreenReader ?? "Keiner erkannt"}");
            }
            else
            {
                _log.Warning("[Accessibility] Tolk konnte nicht geladen werden.");
            }
        }
        catch (Exception ex)
        {
            _log.Error($"[Accessibility] Tolk Initialisierungsfehler: {ex.Message}");
        }
    }

    public void Speak(string text)
    {
        if (!IsAvailable) return;
        TolkNative.Tolk_Speak(text, false);
    }

    public void SpeakInterrupt(string text)
    {
        if (!IsAvailable) return;
        TolkNative.Tolk_Speak(text, true);
    }

    public void Silence()
    {
        if (!IsAvailable) return;
        TolkNative.Tolk_Silence();
    }

    public void Dispose()
    {
        if (IsAvailable)
        {
            TolkNative.Tolk_Unload();
            _log.Info("[Accessibility] Tolk entladen.");
        }
    }
}
