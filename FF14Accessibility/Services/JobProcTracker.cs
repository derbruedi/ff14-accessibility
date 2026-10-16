namespace FF14Accessibility.Services;

/// <summary>Only rising edges of player status presence. Refreshing the timer,
/// moving a status between slots or changing stacks does not duplicate speech.</summary>
public sealed class JobProcTracker
{
    private HashSet<uint> _previous = [];
    private HashSet<uint> _current = [];
    private bool _initialized;
    private (ulong Player, uint Job, uint Territory) _context;

    public void Reset()
    {
        _previous.Clear();
        _current.Clear();
        _initialized = false;
    }

    public void Collect(ulong playerId, uint jobId, uint territory, IEnumerable<uint> statuses, List<uint> becameReady)
    {
        _current.Clear();
        foreach (var id in statuses)
            if (JobProcCatalog.Find(id, jobId) != null) _current.Add(id);
        var context = (playerId, jobId, territory);
        if (_initialized && context == _context)
            foreach (var entry in JobProcCatalog.All)
                if (_current.Contains(entry.StatusId) && !_previous.Contains(entry.StatusId))
                    becameReady.Add(entry.StatusId);

        (_previous, _current) = (_current, _previous);
        _context = context;
        _initialized = true;
    }
}
