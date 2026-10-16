using Dalamud.Game;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

/// <summary>Reads only LocalPlayer.StatusList, independently of target/hotbars.
/// The cooldown service sends the resulting messages through its normal voice.</summary>
public sealed class JobProcService(IObjectTable objects, IClientState client, IDataManager data, IPluginLog log)
{
    private readonly JobProcTracker _tracker = new();
    private readonly List<uint> _ready = [];
    private readonly Dictionary<uint, bool> _validated = [];

    public void Reset() => _tracker.Reset();

    public void Collect(ReadyAnnouncementBatch messages)
    {
        var player = objects.LocalPlayer;
        if (player == null || player.IsDead)
        {
            Reset();
            return;
        }

        _ready.Clear();
        _tracker.Collect(player.GameObjectId, player.ClassJob.RowId, client.TerritoryType,
            player.StatusList.Select(x => x.StatusId), _ready);
        foreach (var id in _ready)
        {
            if (!_validated.TryGetValue(id, out var valid))
            {
                valid = data.GetExcelSheet<Status>(ClientLanguage.English).TryGetRow(id, out var source)
                    && JobProcCatalog.Find(id, player.ClassJob.RowId)!.MatchesSource(source.Name.ExtractText());
                _validated[id] = valid;
                if (!valid) log.Warning($"[JobProc] Status {id} no longer matches the supported game data; skipped.");
            }
            if (!valid || !data.GetExcelSheet<Status>().TryGetRow(id, out var row)) continue;
            var name = RussianGameText.Name(data, row, x => x.Name).Trim();
            if (name.Length == 0) continue;
            messages.Add(AccessibilityStrings.JobProcReady(name));
            log.Info($"[JobProc] Ready: '{name}' status={id} job={player.ClassJob.RowId}");
        }
    }
}
