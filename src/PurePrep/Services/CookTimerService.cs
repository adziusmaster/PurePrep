using System.Text.Json;
using PurePrep.Application;
using PurePrep.Domain;

namespace PurePrep.Services;

/// <summary>
/// Owns every running cook timer for the whole app.
///
/// It lives here, not on the Focus Mode page, because timers previously died the moment that page
/// disappeared — stepping out to check a message lost a running bake. Multiple timers can now run at
/// once (a roast, a sauce and a rest, say), each with its own name and deadline. Every deadline is
/// persisted, so timers survive the process being reclaimed and are picked back up on return.
/// </summary>
public sealed class CookTimerService : IDisposable
{
    private const string StateKey = "cook_timers_v2";
    private const string NextIdKey = "cook_timers_next_id";

    private readonly ICookTimerNotifier _notifier;
    private readonly List<CookTimerState> _timers = new();
    private IDispatcherTimer? _ticker;
    private int _nextId;

    public CookTimerService(ICookTimerNotifier notifier)
    {
        _notifier = notifier;
        _nextId = Preferences.Get(NextIdKey, 1);
        Restore();
    }

    /// <summary>Every timer currently counting down, in the order they were started.</summary>
    public IReadOnlyList<CookTimerState> Timers => _timers;

    public bool IsRunning => _timers.Count > 0;

    /// <summary>Raised each second while any timer runs, and when the set of timers changes.</summary>
    public event EventHandler? Tick;

    /// <summary>Raised when a timer's deadline passes while the app is running.</summary>
    public event EventHandler<CookTimerState>? Finished;

    public int RemainingSeconds(CookTimerState timer) => timer.RemainingSeconds(DateTimeOffset.UtcNow);
    public string Display(CookTimerState timer) => CookTimerState.Clock(RemainingSeconds(timer));

    /// <summary>Starts a new named timer alongside any already running. Returns the started timer.</summary>
    public async Task<CookTimerState?> StartAsync(string label, int totalSeconds)
    {
        if (totalSeconds <= 0)
            return null;

        var id = _nextId++;
        Preferences.Set(NextIdKey, _nextId);

        var timer = CookTimerState.Start(label, totalSeconds, DateTimeOffset.UtcNow) with { Id = id };
        _timers.Add(timer);
        Persist();

        // Permission is requested here rather than at launch, so the prompt arrives with obvious
        // context: the user has just asked for a timer.
        if (_notifier.IsSupported && await _notifier.EnsurePermissionAsync())
            await _notifier.ScheduleAsync(timer.Id, timer.Label, timer.EndsAt);

        StartTicking();
        Tick?.Invoke(this, EventArgs.Empty);
        return timer;
    }

    /// <summary>Stops and clears a single timer, leaving the others running.</summary>
    public async Task StopAsync(int id)
    {
        var index = _timers.FindIndex(t => t.Id == id);
        if (index < 0)
            return;

        _timers.RemoveAt(index);
        Persist();

        if (_notifier.IsSupported)
            await _notifier.CancelAsync(id);

        if (_timers.Count == 0)
            StopTicking();

        Tick?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Stops every running timer.</summary>
    public async Task StopAllAsync()
    {
        var ids = _timers.Select(t => t.Id).ToArray();
        _timers.Clear();
        Persist();
        StopTicking();

        if (_notifier.IsSupported)
        {
            foreach (var id in ids)
                await _notifier.CancelAsync(id);
        }

        Tick?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Re-reads deadlines after the app returns to the foreground. The ticker does not run while
    /// backgrounded, so some timers may well have finished in the meantime.
    /// </summary>
    public void Resume()
    {
        if (_timers.Count == 0)
            return;

        SweepFinished();

        if (_timers.Count > 0)
            StartTicking();

        Tick?.Invoke(this, EventArgs.Empty);
    }

    private void StartTicking()
    {
        if (_ticker is not null || _timers.Count == 0)
            return;

        var dispatcher = Microsoft.Maui.Controls.Application.Current?.Dispatcher;
        if (dispatcher is null)
            return;

        _ticker = dispatcher.CreateTimer();
        _ticker.Interval = TimeSpan.FromSeconds(1);
        _ticker.Tick += OnTick;
        _ticker.Start();
    }

    private void StopTicking()
    {
        if (_ticker is null)
            return;

        _ticker.Stop();
        _ticker.Tick -= OnTick;
        _ticker = null;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        SweepFinished();

        if (_timers.Count == 0)
            StopTicking();

        // Always raise so live displays update every second, even when nothing finished this tick.
        Tick?.Invoke(this, EventArgs.Empty);
    }

    // Removes every timer whose deadline has passed, firing Finished + a buzz for each. Returns
    // whether any were removed.
    private bool SweepFinished()
    {
        var now = DateTimeOffset.UtcNow;
        var finished = _timers.Where(t => t.HasFinished(now)).ToArray();
        if (finished.Length == 0)
            return false;

        foreach (var timer in finished)
            _timers.Remove(timer);
        Persist();

        foreach (var timer in finished)
            Finished?.Invoke(this, timer);

        try
        {
            Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(800));
        }
        catch
        {
            // Vibration is best effort; the notification carries the alert regardless.
        }

        return true;
    }

    private void Persist()
    {
        if (_timers.Count == 0)
        {
            Preferences.Remove(StateKey);
            return;
        }

        var records = _timers
            .Select(t => new PersistedTimer(t.Id, t.Label, t.TotalSeconds, t.EndsAt.ToUnixTimeMilliseconds()))
            .ToArray();
        Preferences.Set(StateKey, JsonSerializer.Serialize(records));
    }

    private void Restore()
    {
        var json = Preferences.Get(StateKey, string.Empty);
        if (string.IsNullOrEmpty(json))
            return;

        PersistedTimer[]? records;
        try
        {
            records = JsonSerializer.Deserialize<PersistedTimer[]>(json);
        }
        catch
        {
            Preferences.Remove(StateKey);
            return;
        }

        if (records is null)
            return;

        var now = DateTimeOffset.UtcNow;
        foreach (var record in records)
        {
            var endsAt = DateTimeOffset.FromUnixTimeMilliseconds(record.EndsAtMs);
            // Anything that elapsed while the app was gone already alerted via its notification.
            if (endsAt <= now)
                continue;

            _timers.Add(new CookTimerState(record.Label, record.TotalSeconds, endsAt) { Id = record.Id });
        }

        if (_timers.Count > 0)
            StartTicking();
        else
            Preferences.Remove(StateKey);
    }

    public void Dispose() => StopTicking();

    private sealed record PersistedTimer(int Id, string Label, int TotalSeconds, long EndsAtMs);
}
