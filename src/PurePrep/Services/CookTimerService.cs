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
    // Ids already alerted for the checkpoint they are currently sitting at, so the buzz/notification
    // fires once per checkpoint rather than on every tick while the cook decides whether to check
    // now or push it out further. Extending a timer clears its entry, so reaching the next checkpoint
    // (or the genuine finish) alerts again. In-memory only: deliberately not persisted, so a process
    // restart may re-alert once — far better than a checkpoint silently going unnoticed.
    private readonly HashSet<int> _notifiedCheckpoints = new();
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

    /// <summary>True while <paramref name="timer"/> sits at a range's minimum, still extendable.</summary>
    public bool IsAtCheckpoint(CookTimerState timer) => timer.IsAtCheckpoint(DateTimeOffset.UtcNow);

    /// <summary>Starts a new named single-duration timer alongside any already running.</summary>
    public Task<CookTimerState?> StartAsync(string label, int totalSeconds) =>
        StartAsync(label, totalSeconds, totalSeconds);

    /// <summary>
    /// Starts a new timer alongside any already running. A range (<paramref name="maxSeconds"/> &gt;
    /// <paramref name="minSeconds"/>) first counts down to the minimum, then offers "+2 min" up to the
    /// maximum. <paramref name="recipeId"/>/<paramref name="stepIndex"/> let the app-wide timers bar
    /// jump straight back to the step this timer was started from; <paramref name="timerIndex"/> is
    /// which of that step's timers it is, so Focus Mode can map it back to its chip. Returns the started timer.
    /// </summary>
    public async Task<CookTimerState?> StartAsync(string label, int minSeconds, int maxSeconds, Guid? recipeId = null, int? stepIndex = null, int? timerIndex = null)
    {
        if (minSeconds <= 0)
            return null;

        var id = _nextId++;
        Preferences.Set(NextIdKey, _nextId);

        var timer = CookTimerState.Start(label, minSeconds, maxSeconds, DateTimeOffset.UtcNow)
            with { Id = id, RecipeId = recipeId, StepIndex = stepIndex, TimerIndex = timerIndex };
        _timers.Add(timer);
        Persist();

        // Permission is requested here rather than at launch, so the prompt arrives with obvious
        // context: the user has just asked for a timer.
        await ScheduleNotificationAsync(timer);

        StartTicking();
        Tick?.Invoke(this, EventArgs.Empty);
        return timer;
    }

    /// <summary>
    /// Pushes a range timer's deadline out by <paramref name="seconds"/> ("+2 min"), never past its
    /// maximum. No-ops for a timer that has been stopped, or one that cannot be extended (a plain
    /// single-duration timer, or a range already at its maximum).
    /// </summary>
    public async Task ExtendAsync(int id, int seconds)
    {
        var index = _timers.FindIndex(t => t.Id == id);
        if (index < 0 || !_timers[index].CanExtend)
            return;

        _timers[index] = _timers[index].Extend(seconds);
        // A new deadline is now in play — whether it's another checkpoint or the genuine finish, it
        // hasn't been alerted for yet.
        _notifiedCheckpoints.Remove(id);
        Persist();

        await ScheduleNotificationAsync(_timers[index]);
        Tick?.Invoke(this, EventArgs.Empty);
    }

    // Shared by StartAsync and ExtendAsync: (re)schedules the Android alarm/notification for a
    // timer's current deadline, requesting permission if it hasn't been granted yet.
    private async Task ScheduleNotificationAsync(CookTimerState timer)
    {
        if (_notifier.IsSupported && await _notifier.EnsurePermissionAsync())
            await _notifier.ScheduleAsync(timer.Id, timer.Label, timer.EndsAt, timer.CanExtend);
    }

    /// <summary>Stops and clears a single timer, leaving the others running.</summary>
    public async Task StopAsync(int id)
    {
        var index = _timers.FindIndex(t => t.Id == id);
        if (index < 0)
            return;

        _timers.RemoveAt(index);
        _notifiedCheckpoints.Remove(id);
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
        _notifiedCheckpoints.Clear();
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

    // Removes every timer that has genuinely finished — reached its deadline with nothing left to
    // extend towards — firing Finished + a buzz for each. A range timer that has only reached its
    // minimum (IsAtCheckpoint) is left running: it alerts the same way, once, but stays in Timers so
    // the "Check now" / "+2 min" UI has something to act on instead of the timer having vanished
    // from under it. Returns whether any timers were removed.
    private bool SweepFinished()
    {
        var now = DateTimeOffset.UtcNow;

        var finished = _timers.Where(t => t.HasFinished(now) && !t.CanExtend).ToArray();
        if (finished.Length > 0)
        {
            foreach (var timer in finished)
            {
                _timers.Remove(timer);
                _notifiedCheckpoints.Remove(timer.Id);
            }
            Persist();
        }

        // Newly-reached checkpoints only — Add returns false for one already alerted, so this timer
        // isn't re-buzzed every second while it waits for the cook to check now or extend it.
        var checkpoints = _timers.Where(t => t.IsAtCheckpoint(now) && _notifiedCheckpoints.Add(t.Id)).ToArray();

        if (finished.Length == 0 && checkpoints.Length == 0)
            return false;

        foreach (var timer in finished)
            Finished?.Invoke(this, timer);
        foreach (var timer in checkpoints)
            Finished?.Invoke(this, timer);

        try
        {
            Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(800));
        }
        catch
        {
            // Vibration is best effort; the notification carries the alert regardless.
        }

        return finished.Length > 0;
    }

    private void Persist()
    {
        if (_timers.Count == 0)
        {
            Preferences.Remove(StateKey);
            return;
        }

        var records = _timers
            .Select(t => new PersistedTimer(t.Id, t.Label, t.TotalSeconds, t.EndsAt.ToUnixTimeMilliseconds(), t.MaxSeconds, t.RecipeId, t.StepIndex, t.TimerIndex))
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

            // A genuinely finished timer (deadline passed, nothing left to extend towards) already
            // alerted via its notification while the app was away — drop it. A range timer that had
            // only reached its checkpoint is kept, so it comes back at "Check now" instead of
            // vanishing after a full process restart.
            if (!CookTimerState.ShouldRestore(record.TotalSeconds, record.MaxSeconds, endsAt, now))
                continue;

            // Pre-Task-18 JSON has no MaxSeconds (defaults to 0 on deserialize): treat it as the
            // timer's own total, so it correctly reports as not extendable rather than CanExtend
            // being true with a bogus MaxSeconds of 0.
            var maxSeconds = record.MaxSeconds > 0 ? record.MaxSeconds : record.TotalSeconds;
            var timer = new CookTimerState(record.Label, record.TotalSeconds, endsAt)
                { Id = record.Id, MaxSeconds = maxSeconds, RecipeId = record.RecipeId, StepIndex = record.StepIndex, TimerIndex = record.TimerIndex };
            _timers.Add(timer);

            // A checkpoint reached while the app was away already alerted via the platform
            // notification (if permitted); mark it pre-notified so SweepFinished doesn't buzz again
            // the instant ticking resumes.
            if (timer.IsAtCheckpoint(now))
                _notifiedCheckpoints.Add(timer.Id);
        }

        if (_timers.Count > 0)
            StartTicking();
        else
            Preferences.Remove(StateKey);
    }

    public void Dispose() => StopTicking();

    // MaxSeconds/RecipeId/StepIndex are new for Task 18; JSON written by earlier versions simply
    // omits them, which System.Text.Json binds to their defaults below (0 / null / null) rather than
    // failing to deserialize. TimerIndex (1.4) is optional the same way: older JSON restores as null.
    private sealed record PersistedTimer(int Id, string Label, int TotalSeconds, long EndsAtMs, int MaxSeconds = 0, Guid? RecipeId = null, int? StepIndex = null,
        int? TimerIndex = null);
}
