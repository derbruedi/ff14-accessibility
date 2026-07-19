using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace FF14Accessibility.Services;

/// <summary>
/// Makes Dalamud's own plugin list readable. Dalamud renders its plugin
/// installer and settings in ImGui, which has no node tree and is therefore
/// invisible to both a screen reader and this plugin's Atk-based UI reader.
///
/// Instead of scraping the UI, this reads the DATA behind it through Dalamud's
/// public plugin API (IDalamudPluginInterface.InstalledPlugins, ilspycmd-verified
/// 2026-07-19): every entry carries Name, Version and its load/health state.
/// That API is public and versioned, so this cannot break the way reflection
/// into Dalamud internals would.
///
/// Deliberately read-only: installing, updating and removing plugins live in
/// Dalamud.Plugin.Internal.PluginManager, which is internal. Those paths stay
/// with the external installer EXE (see docs/installer-architektur.md).
/// </summary>
public sealed class DalamudPluginsService
{
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly TolkService             _tolk;
    private readonly IPluginLog              _log;

    private readonly List<IExposedPlugin> _plugins = new();
    private int  _index = -1;
    private bool _announcedOverview;

    public DalamudPluginsService(IDalamudPluginInterface pluginInterface, TolkService tolk, IPluginLog log)
    {
        _pluginInterface = pluginInterface;
        _tolk            = tolk;
        _log             = log;
    }

    /// <summary>Announces the next installed plugin in the alphabetical list.</summary>
    public void CycleNext() => Cycle(+1);

    /// <summary>Announces the previous installed plugin.</summary>
    public void CyclePrev() => Cycle(-1);

    private void Cycle(int direction)
    {
        if (!RefreshList()) return;

        // The very first press answers "is everything running?" before diving
        // into single entries - that is the question a blind player actually
        // has, and it costs no extra hotkey.
        if (!_announcedOverview)
        {
            _announcedOverview = true;
            _tolk.SpeakInterrupt(BuildOverview());
            _index = -1;
        }

        _index = ((_index + direction) % _plugins.Count + _plugins.Count) % _plugins.Count;
        _tolk.Speak($"{Describe(_plugins[_index])}, {_index + 1} von {_plugins.Count}");
    }

    /// <summary>
    /// Opens the config window of the selected plugin. Note this window is ImGui
    /// as well, so it is only useful with a sighted helper present - the spoken
    /// confirmation exists so the user knows the window is now on screen.
    /// </summary>
    public void OpenConfigOfSelected()
    {
        if (!RefreshList()) return;
        if (_index < 0) { _tolk.SpeakInterrupt("Kein Plugin gewählt. Erst durchblättern."); return; }

        var plugin = _plugins[_index];
        var name   = SafeName(plugin);

        if (!TryGet(() => plugin.HasConfigUi, out var hasConfig) || !hasConfig)
        {
            _tolk.SpeakInterrupt($"{name} hat keine Einstellungen.");
            return;
        }

        // External Dalamud call: documented to throw InvalidOperationException,
        // and the plugin behind it is foreign code we do not control.
        try
        {
            plugin.OpenConfigUi();
            _tolk.SpeakInterrupt($"Einstellungen von {name} geöffnet. Das Fenster ist nicht vorlesbar.");
            _log.Info($"[DalamudPlugins] ConfigUi geöffnet: {name}");
        }
        catch (Exception ex)
        {
            _tolk.SpeakInterrupt($"Einstellungen von {name} lassen sich nicht öffnen.");
            _log.Error(ex, $"[DalamudPlugins] OpenConfigUi fehlgeschlagen für {name}");
        }
    }

    /// <summary>
    /// Re-reads the list on every use: plugins can be loaded, unloaded or updated
    /// while the game runs, and a cached list would announce a state that is no
    /// longer true.
    /// </summary>
    private bool RefreshList()
    {
        var previous = _index >= 0 && _index < _plugins.Count ? SafeName(_plugins[_index]) : null;

        _plugins.Clear();
        try
        {
            _plugins.AddRange(_pluginInterface.InstalledPlugins.OrderBy(SafeName, StringComparer.CurrentCultureIgnoreCase));
        }
        catch (Exception ex)
        {
            _tolk.SpeakInterrupt("Plugin-Liste nicht verfügbar.");
            _log.Error(ex, "[DalamudPlugins] InstalledPlugins nicht lesbar");
            return false;
        }

        if (_plugins.Count == 0)
        {
            _tolk.SpeakInterrupt("Keine Plugins installiert.");
            _log.Info("[DalamudPlugins] Liste leer");
            return false;
        }

        // Keep the cursor on the same plugin when the list changed underneath us.
        if (previous != null)
            _index = _plugins.FindIndex(p => string.Equals(SafeName(p), previous, StringComparison.Ordinal));

        return true;
    }

    private string BuildOverview()
    {
        var problems = new List<string>();
        var notLoaded = _plugins.Count(p => TryGet(() => p.IsLoaded, out var v) && !v);
        var outdated  = _plugins.Count(p => TryGet(() => p.IsOutdated, out var v) && v);
        var banned    = _plugins.Count(p => TryGet(() => p.IsBanned, out var v) && v);

        if (notLoaded > 0) problems.Add($"{notLoaded} nicht geladen");
        if (outdated  > 0) problems.Add($"{outdated} veraltet");
        if (banned    > 0) problems.Add($"{banned} gesperrt");

        var state = problems.Count == 0 ? "alle geladen" : string.Join(", ", problems);
        var msg   = $"{_plugins.Count} Plugins, {state}.";
        _log.Info($"[DalamudPlugins] Übersicht: {msg}");
        return msg;
    }

    private string Describe(IExposedPlugin plugin)
    {
        var parts = new List<string> { SafeName(plugin) };

        if (TryGet(() => plugin.Version, out var version) && version != null)
            parts.Add($"Version {version}");
        else
            _log.Info($"[DalamudPlugins] {SafeName(plugin)}: keine Version gemeldet");

        // State first in importance order - "not loaded" is the one that explains
        // why a feature is missing (e.g. auto-walk without vnavmesh).
        if (TryGet(() => plugin.IsLoaded, out var loaded))
            parts.Add(loaded ? "geladen" : "nicht geladen");
        if (TryGet(() => plugin.IsOutdated, out var outdated) && outdated) parts.Add("veraltet");
        if (TryGet(() => plugin.IsBanned,   out var banned)   && banned)   parts.Add("gesperrt");
        if (TryGet(() => plugin.IsDev,      out var dev)      && dev)      parts.Add("Entwickler-Plugin");
        if (TryGet(() => plugin.HasConfigUi, out var config)  && config)   parts.Add("hat Einstellungen");

        return string.Join(", ", parts) + ".";
    }

    private string SafeName(IExposedPlugin plugin)
    {
        if (TryGet(() => plugin.Name, out var name) && !string.IsNullOrWhiteSpace(name)) return name;
        if (TryGet(() => plugin.InternalName, out var internalName) && !string.IsNullOrWhiteSpace(internalName)) return internalName;
        return "Unbenanntes Plugin";
    }

    /// <summary>
    /// Reads one property of a foreign plugin's manifest. Each getter reaches into
    /// another plugin's data, so a single broken entry must not take down the whole
    /// list - but it is logged rather than silently skipped.
    /// </summary>
    private bool TryGet<T>(Func<T> getter, out T value)
    {
        try
        {
            value = getter();
            return true;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[DalamudPlugins] Eigenschaft nicht lesbar");
            value = default!;
            return false;
        }
    }
}
