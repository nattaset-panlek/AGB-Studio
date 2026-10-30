using Avalonia.Threading;

namespace AgbStudio.Editor.Services;

/// <summary>A repeating timer, abstracted so tests can tick it by hand.</summary>
public interface IRepeatingTimer
{
    TimeSpan Interval { get; set; }
    bool IsRunning { get; }
    event Action? Tick;
    void Start();
    void Stop();
}

public sealed class DispatcherRepeatingTimer : IRepeatingTimer
{
    readonly DispatcherTimer _t = new();
    public DispatcherRepeatingTimer() => _t.Tick += (_, _) => Tick?.Invoke();
    public TimeSpan Interval { get => _t.Interval; set => _t.Interval = value; }
    public bool IsRunning => _t.IsEnabled;
    public event Action? Tick;
    public void Start() => _t.Start();
    public void Stop() => _t.Stop();
}

/// <summary>Autosave every N minutes (P10.10, Preferences · Editor "Autosave · every 2 min"; 0 = off).</summary>
public sealed class AutosaveService : IDisposable
{
    readonly IRepeatingTimer _timer;
    readonly Func<int> _minutes;
    readonly Func<Task<int>> _saveDirty;
    bool _busy;

    public AutosaveService(IRepeatingTimer timer, Func<int> minutes, Func<Task<int>> saveDirty)
    {
        _timer = timer;
        _minutes = minutes;
        _saveDirty = saveDirty;
        _timer.Tick += OnTick;
    }

    public DateTime? LastRun { get; private set; }
    public int LastSavedCount { get; private set; }
    public event Action<int>? Autosaved;

    /// <summary>Starts, restarts or stops the timer from the current preference.</summary>
    public void Refresh()
    {
        var m = _minutes();
        _timer.Stop();
        if (m <= 0) return;
        _timer.Interval = TimeSpan.FromMinutes(m);
        _timer.Start();
    }

    async void OnTick()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            LastSavedCount = await _saveDirty();
            LastRun = DateTime.Now;
            Autosaved?.Invoke(LastSavedCount);
        }
        finally { _busy = false; }
    }

    public void Dispose() => _timer.Tick -= OnTick;
}
