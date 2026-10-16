using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;

namespace FF14Accessibility.Services;

public sealed class ModSwitchService
{
	private const int MaxSpokenNames = 6;

	private readonly PenumbraIpc _penumbra;

	private readonly Configuration _config;

	private readonly IPluginLog _log;

	public bool PenumbraReady => _penumbra.IsEnabled;

	public ModSwitchService(PenumbraIpc penumbra, Configuration config, IPluginLog log)
	{
		_penumbra = penumbra;
		_config = config;
		_log = log;
	}

	public ModsOverview Read()
	{
		Dictionary<string, string> modList = _penumbra.GetModList();
		if (_penumbra.LastCallFailed)
		{
			_log.Info("[Mods] Penumbra antwortet nicht auf GetModList.", Array.Empty<object>());
			return new ModsOverview(Answered: false, 0, 0, 0, string.Empty, 0);
		}
		Dictionary<Guid, string> collections = _penumbra.GetCollections();
		if (_penumbra.LastCallFailed)
			return new ModsOverview(Answered: false, modList.Count, 0, 0, string.Empty, 0);
		int num = 0;
		List<string> list = new List<string>();
		foreach (KeyValuePair<string, string> item in modList)
		{
			bool flag = false;
			foreach (KeyValuePair<Guid, string> item2 in collections)
			{
				bool? flag2 = _penumbra.IsModEnabled(item2.Key, item.Key, item.Value);
				if (!flag2.HasValue)
				{
					num++;
				}
				else if (flag2.Value)
				{
					flag = true;
				}
			}
			if (flag)
			{
				list.Add(item.Value);
			}
		}
		if (num > 0)
		{
			_log.Warning($"[Mods] {num} Zustandszahl(en) nicht lesbar - die Zahl kann zu klein sein.", Array.Empty<object>());
		}
		List<string> list2 = modList.Values.Where((string n) => !string.IsNullOrWhiteSpace(n)).Take(6).ToList();
		if (modList.Count > list2.Count)
		{
			list2.Add(AccessibilityStrings.ModsMoreNames(modList.Count - list2.Count));
		}
		_log.Info($"[Mods] {modList.Count} Mod(s), {collections.Count} Sammlung(en), an: {list.Count}.", Array.Empty<object>());
		return new ModsOverview(Answered: true, modList.Count, collections.Count, list.Count, (list2.Count > 0) ? string.Join(", ", list2) : AccessibilityStrings.ModsNoNames, num);
	}

	public ModsSwitchResult AllOff()
	{
		Dictionary<string, string> modList = _penumbra.GetModList();
		if (_penumbra.LastCallFailed)
			return new ModsSwitchResult(Answered: false, NothingToDo: false, 0, 0, 0, Remembered());
		Dictionary<Guid, string> collections = _penumbra.GetCollections();
		if (_penumbra.LastCallFailed)
			return new ModsSwitchResult(Answered: false, NothingToDo: false, modList.Count, 0, 0, Remembered());
		if (modList.Count == 0 || collections.Count == 0)
		{
			return new ModsSwitchResult(Answered: true, NothingToDo: true, modList.Count, 0, 0, Remembered());
		}
		// A second disable must retain mods still awaiting restoration from the first.
		List<string> list = _config.ModsSwitchMemoryValid
			? new List<string>(_config.ModsSwitchMemory) : new List<string>();
		int num = 0;
		int num2 = 0;
		foreach (KeyValuePair<Guid, string> item in collections)
		{
			foreach (KeyValuePair<string, string> item2 in modList)
			{
				bool? flag = _penumbra.IsModEnabled(item.Key, item2.Key, item2.Value);
				if (!flag.HasValue)
				{
					num2++;
				}
				else if (flag.Value)
				{
					int num3 = _penumbra.TrySetMod(item.Key, item2.Key, item2.Value, enabled: false);
					if ((uint)num3 > 1u)
					{
						num2++;
						_log.Warning($"[Mods] Ausschalten von '{item2.Value}' in '{item.Value}' fehlgeschlagen: Status {num3}", Array.Empty<object>());
					}
					else
					{
						num++;
						var key = MemoryKey(item.Key, item2.Key, item2.Value);
						if (!list.Contains(key)) list.Add(key);
					}
				}
			}
		}
		if (num == 0)
		{
			return new ModsSwitchResult(Answered: true, NothingToDo: true, modList.Count, 0, num2, Remembered());
		}
		_config.ModsSwitchMemory = list;
		_config.ModsSwitchMemoryValid = true;
		_log.Info($"[Mods] {num} Mod/Sammlung-Paar(e) ausgeschaltet und gemerkt, {num2} uebersprungen.", Array.Empty<object>());
		return new ModsSwitchResult(Answered: true, NothingToDo: false, modList.Count, num, num2, list.Count);
	}

	public ModsSwitchResult Restore()
	{
		if (!_config.ModsSwitchMemoryValid || _config.ModsSwitchMemory.Count == 0)
		{
			return new ModsSwitchResult(Answered: true, NothingToDo: true, 0, 0, 0, 0);
		}
		int num = 0;
		int num2 = 0;
		var remaining = new List<string>();
		foreach (string item in _config.ModsSwitchMemory)
		{
			if (!TryParseMemoryKey(item, out Guid collectionId, out string directory, out string name))
			{
				num2++;
				remaining.Add(item);
				_log.Warning("[Mods] Gemerkter Eintrag unlesbar: '" + item + "'", Array.Empty<object>());
				continue;
			}
			int num3 = _penumbra.TrySetMod(collectionId, directory, name, enabled: true);
			if ((uint)num3 <= 1u)
			{
				num++;
				continue;
			}
			num2++;
			remaining.Add(item);
			_log.Warning($"[Mods] Zurueckschalten von '{name}' fehlgeschlagen: Status {num3}", Array.Empty<object>());
		}
		_config.ModsSwitchMemory = remaining;
		_config.ModsSwitchMemoryValid = remaining.Count > 0;
		_log.Info($"[Mods] {num} Paar(e) zurueckgeschaltet, {num2} fehlgeschlagen.", Array.Empty<object>());
		return new ModsSwitchResult(Answered: true, NothingToDo: false, 0, num, num2, remaining.Count);
	}

	private int Remembered()
	{
		if (!_config.ModsSwitchMemoryValid)
		{
			return 0;
		}
		return _config.ModsSwitchMemory.Count;
	}

	private static string MemoryKey(Guid collectionId, string directory, string name)
	{
		return $"{collectionId}|{directory}|{name}";
	}

	private static bool TryParseMemoryKey(string entry, out Guid collectionId, out string directory, out string name)
	{
		collectionId = Guid.Empty;
		directory = string.Empty;
		name = string.Empty;
		if (string.IsNullOrEmpty(entry))
		{
			return false;
		}
		string[] array = entry.Split('|', 3);
		if (array.Length != 3)
		{
			return false;
		}
		if (!Guid.TryParse(array[0], out collectionId))
		{
			return false;
		}
		directory = array[1];
		name = array[2];
		if (directory.Length > 0)
		{
			return name.Length > 0;
		}
		return false;
	}
}
