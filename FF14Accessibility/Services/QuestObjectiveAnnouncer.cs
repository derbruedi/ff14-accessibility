using System;
using System.Collections.Generic;
using System.Diagnostics;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;

namespace FF14Accessibility.Services;

public sealed class QuestObjectiveAnnouncer : IDisposable
{
	private const string TrackerAddon = "_ToDoList";

	private const int MinReadGapMs = 200;

	private readonly IAddonLifecycle _addonLifecycle;

	private readonly QuestMarkerService _questMarkers;

	private readonly TolkService _tolk;

	private readonly IPluginLog _log;

	private readonly Stopwatch _sinceRead = Stopwatch.StartNew();

	private Dictionary<string, string> _last = new Dictionary<string, string>();

	private bool _primed;

	private long _fires;

	private long _changes;

	private DateTime _nextReport = DateTime.UtcNow.AddMinutes(1.0);

	public Func<bool>? IsEnabled { get; set; }

	public Func<bool>? Suppress { get; set; }

	public QuestObjectiveAnnouncer(IAddonLifecycle addonLifecycle, QuestMarkerService questMarkers, TolkService tolk, IPluginLog log)
	{
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Expected Obj, but got Unknown
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Expected Obj, but got Unknown
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Expected Obj, but got Unknown
		_addonLifecycle = addonLifecycle;
		_questMarkers = questMarkers;
		_tolk = tolk;
		_log = log;
		_addonLifecycle.RegisterListener(AddonEvent.PostSetup, "_ToDoList", OnTrackerSetup);
		_addonLifecycle.RegisterListener(AddonEvent.PostUpdate, "_ToDoList", OnTrackerUpdate);
		_addonLifecycle.RegisterListener(AddonEvent.PreFinalize, "_ToDoList", OnTrackerClose);
	}

	private void OnTrackerSetup(AddonEvent type, AddonArgs args)
	{
		_last = _questMarkers.GetQuestObjectives(log: false);
		_primed = true;
	}

	private void OnTrackerClose(AddonEvent type, AddonArgs args)
	{
		_last = new Dictionary<string, string>();
		_primed = false;
	}

	private void OnTrackerUpdate(AddonEvent type, AddonArgs args)
	{
		_fires++;
		Func<bool>? isEnabled = IsEnabled;
		if (isEnabled == null || !isEnabled())
		{
			return;
		}
		Func<bool>? suppress = Suppress;
		if ((suppress != null && suppress()) || _sinceRead.ElapsedMilliseconds < 200)
		{
			return;
		}
		_sinceRead.Restart();
		Dictionary<string, string> questObjectives = _questMarkers.GetQuestObjectives(log: false);
		if (questObjectives.Count == 0)
		{
			return;
		}
		if (!_primed)
		{
			_last = questObjectives;
			_primed = true;
			return;
		}
		List<string>? list = null;
		foreach (var (text3, text4) in questObjectives)
		{
			if (_last.TryGetValue(text3, out var value) && !(value == text4))
			{
				(list ?? (list = new List<string>())).Add(text3 + ": " + text4);
			}
		}
		_last = questObjectives;
		ReportProbe(list?.Count ?? 0);
		if (list != null)
		{
			_changes++;
			_tolk.Speak(string.Join(". ", list));
		}
	}

	private void ReportProbe(int changedNow)
	{
		if (!(DateTime.UtcNow < _nextReport))
		{
			_nextReport = DateTime.UtcNow.AddMinutes(1.0);
			_log.Debug($"[QuestObjectives] tracker reports={_fires}/min, spoken={_changes}, last frame={changedNow} changed", Array.Empty<object>());
			_fires = 0L;
		}
	}

	public void Dispose()
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Expected Obj, but got Unknown
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Expected Obj, but got Unknown
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Expected Obj, but got Unknown
		_addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "_ToDoList", OnTrackerSetup);
		_addonLifecycle.UnregisterListener(AddonEvent.PostUpdate, "_ToDoList", OnTrackerUpdate);
		_addonLifecycle.UnregisterListener(AddonEvent.PreFinalize, "_ToDoList", OnTrackerClose);
	}
}
