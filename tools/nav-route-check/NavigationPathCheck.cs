using System.Numerics;

namespace FF14Accessibility.Services;

// Offline mesh survey only. This code is not compiled into the game plugin:
// partial native paths can require the author's measured crossing stages.
internal sealed class NavigationPathCheck
{
    internal sealed record Choice(Vector3 Position, LocalTransfer? Transfer = null);
    private readonly Func<Vector3, Vector3, float, Task<List<Vector3>>?> _request;
    private readonly Vector3 _from;
    private readonly Vector3 _goal;
    private readonly float _range;
    private readonly Queue<Choice> _choices;
    private Task<List<Vector3>>? _task;
    private Choice? _current;
    private bool _checkingArrival;
    private readonly DateTime _started = DateTime.UtcNow;
    internal bool Completed { get; private set; }
    internal Choice? Result { get; private set; }
    internal int Requests { get; private set; }
    internal Vector3 Goal => _goal;

    internal NavigationPathCheck(Vector3 from, Vector3 goal, float range, IEnumerable<Choice> choices,
        Func<Vector3, Vector3, float, Task<List<Vector3>>?> request)
    {
        _from = from; _goal = goal; _range = range; _request = request;
        _choices = new(choices.Take(64));
    }

    internal void Poll()
    {
        if (Completed) return;
        if (!TravelLayout.Finite(_from) || !TravelLayout.Finite(_goal) || !float.IsFinite(_range) || _range < 0)
        { Cancel(); return; }
        if (DateTime.UtcNow - _started > TimeSpan.FromSeconds(30)) { Cancel(); return; }
        if (_task != null)
        {
            if (!_task.IsCompleted) return;
            var point = _checkingArrival ? _goal : _current!.Position;
            var tolerance = _checkingArrival ? _range : _current!.Transfer != null ? AutoWalkService.StopRange : _range;
            var success = _task.IsCompletedSuccessfully && Reaches(_task.Result, point, tolerance);
            // Observe failures even when the caller cancels this selection.
            if (_task.IsFaulted) _ = _task.Exception;
            _task = null;
            if (success && _current!.Transfer != null && !_checkingArrival)
            {
                _checkingArrival = true;
                _task = Request(_current.Transfer.Arrival, _goal, _range);
                if (_task != null) return;
                success = false;
            }
            if (success) { Result = _current; Completed = true; return; }
            _current = null; _checkingArrival = false;
        }
        while (_choices.TryDequeue(out var candidate))
        {
            if (!TravelLayout.Finite(candidate.Position)) continue;
            _current = candidate;
            _task = Request(_from, candidate.Position,
                candidate.Transfer != null ? AutoWalkService.StopRange : _range);
            if (_task != null) return;
        }
        Completed = true;
    }

    private Task<List<Vector3>>? Request(Vector3 from, Vector3 to, float range)
    {
        Requests++;
        try { return _request(from, to, range); }
        catch { return null; } // IPC can disappear between readiness and query.
    }

    internal void Cancel()
    {
        Completed = true; Result = null;
        if (_task != null)
            _ = _task.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
        _task = null;
    }

    internal static bool Reaches(IReadOnlyList<Vector3>? path, Vector3 goal, float range)
        => path is { Count: >= 2 } && TravelLayout.Finite(goal) && float.IsFinite(range) && range >= 0
            && path.All(TravelLayout.Finite) && Vector3.Distance(path[^2], goal) <= range + 0.3f;
}
