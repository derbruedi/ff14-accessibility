using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;

namespace FF14Accessibility.Services;

// Only the IPC calls needed to inspect and switch already installed mods.
public sealed class PenumbraIpc
{
    private readonly IPluginLog _log;
    private readonly ICallGateSubscriber<Guid, string, string, bool, int> _trySetMod;
    private readonly ICallGateSubscriber<Dictionary<string, string>> _getModList;
    private readonly ICallGateSubscriber<Dictionary<Guid, string>> _getCollections;
    private readonly ICallGateSubscriber<bool> _getEnabledState;
    private readonly ICallGateSubscriber<Guid, string, string, bool,
        (int, (bool, int, Dictionary<string, List<string>>, bool)?)> _getCurrentModSettings;

    public bool LastCallFailed { get; private set; }
    public bool IsEnabled => Call(_getEnabledState, false, "GetEnabledState");

    public PenumbraIpc(IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        _log = log;
        _trySetMod = pluginInterface.GetIpcSubscriber<Guid, string, string, bool, int>("Penumbra.TrySetMod.V5");
        _getModList = pluginInterface.GetIpcSubscriber<Dictionary<string, string>>("Penumbra.GetModList");
        _getCollections = pluginInterface.GetIpcSubscriber<Dictionary<Guid, string>>("Penumbra.GetCollections.V5");
        _getEnabledState = pluginInterface.GetIpcSubscriber<bool>("Penumbra.GetEnabledState");
        _getCurrentModSettings = pluginInterface.GetIpcSubscriber<Guid, string, string, bool,
            (int, (bool, int, Dictionary<string, List<string>>, bool)?)>("Penumbra.GetCurrentModSettings.V5");
    }

    private T Call<T>(ICallGateSubscriber<T> gate, T fallback, string name)
    {
        try
        {
            var result = gate.InvokeFunc();
            LastCallFailed = false;
            return result;
        }
        catch (Exception ex)
        {
            LastCallFailed = true;
            _log.Verbose(ex, $"[Penumbra] IPC '{name}' nicht erreichbar");
            return fallback;
        }
    }

    public bool? IsModEnabled(Guid collectionId, string directory, string name)
    {
        try
        {
            var result = _getCurrentModSettings.InvokeFunc(collectionId, directory, name, false);
            LastCallFailed = result.Item1 != 0;
            if (LastCallFailed)
            {
                _log.Verbose($"[Penumbra] GetCurrentModSettings failed: status={result.Item1}");
                return null;
            }
            return result.Item2?.Item1 ?? false;
        }
        catch (Exception ex)
        {
            LastCallFailed = true;
            _log.Verbose(ex, "[Penumbra] IPC 'GetCurrentModSettings' nicht erreichbar");
            return null;
        }
    }

    public Dictionary<string, string> GetModList()
        => Call(_getModList, new Dictionary<string, string>(), "GetModList");

    public Dictionary<Guid, string> GetCollections()
        => Call(_getCollections, new Dictionary<Guid, string>(), "GetCollections");

    public int TrySetMod(Guid collectionId, string directory, string name, bool enabled)
    {
        try
        {
            var result = _trySetMod.InvokeFunc(collectionId, directory, name, enabled);
            LastCallFailed = false;
            return result;
        }
        catch (Exception ex)
        {
            LastCallFailed = true;
            _log.Verbose(ex, "[Penumbra] IPC 'TrySetMod' nicht erreichbar");
            return 255;
        }
    }
}
