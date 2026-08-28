using PurePrep.Application;
using PurePrep.Domain;

namespace PurePrep.Services;

/// <summary>
/// Owns the one active cook timer for the whole app.
///
/// It lives here, not on the Focus Mode page, because the timer previously died the moment that
/// page disappeared — stepping out to check a message lost a running bake. The deadline is also
/// persisted, so a timer survives the process being reclaimed and is picked back up on return.
/// </summary>
public sealed class CookTimerService : IDisposable
{
    private const string DeadlineKey = "cook_timer_ends_at";
    private const string LabelKey = "cook_timer_label";
    private const string TotalKey = "cook_timer_total";

    private readonly ICookTimerNotifier _notifier;
    private IDispatcherTimer? _ticker;

    public CookTimerService(ICookTimerNotifier notifier)
    {
        _notifier = notifier;
        Restore();
    }

    /// <summary>The running timer, or <c>null</c> when nothing is counting down.</summary>
    public CookTimerState? Active { get; private set; }

    public bool IsRunning => Active is not null;

    /// <summary>Raised each second while a timer runs, and once more when it finishes.</summary>
    public event EventHandler? Tick;

    /// <summary>Raised when the deadline passes while the app is running.</summary>
    public event EventHandler? Finished;

    public int RemainingSeconds => Active?.RemainingSeconds(DateTimeOffset.UtcNow) ?? 0;
    public string Display => CookTimerState.Clock(RemainingSeconds);
    public string Label => Active?.Label ?? string.Empty;

    public async Task StartAsync(string label, int totalSeconds)
    {
        if (totalSeconds <= 0)
            return;

        await StopAsync();

        Active = CookTimerState.Start(label, totalSeconds, DateTimeOffset.UtcNow);
        Persist();

        // Permission is requested here rather than at launch, so the prompt arrives with obvious
        // context: the user has just asked for a timer.
        if (_notifier.IsSupported && await _notifier.EnsurePermissionAsync())
            await _notifier.ScheduleAsync(label, Active.EndsAt);

        StartTicking();
        Tick?.Invoke(this, EventArgs.Empty);
    }

    public async Task StopAsync()
    {
        StopTicking();
        Active = null;
        Clear();

        if (_notifier.IsSupported)
            await _notifier.CancelAsync();

        Tick?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Re-reads the deadline after the app returns to the foreground. The ticker does not run while
    /// backgrounded, so the timer may well have finished in the meantime.
    /// </summary>
    public void Resume()
    {
        if (Active is null)
            return;

        if (Active.HasFinished(DateTimeOffset.UtcNow))
        {
            Complete();
            return;
        }

        StartTicking();
        Tick?.Invoke(this, EventArgs.Empty);
    }

    private void StartTicking()
    {
        StopTicking();

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
        if (Active is null)
            return;

        if (Active.HasFinished(DateTimeOffset.UtcNow))
        {
            Complete();
            return;
        }

        Tick?.Invoke(this, EventArgs.Empty);
    }

    private void Complete()
    {
        StopTicking();
        Active = null;
        Clear();

        Tick?.Invoke(this, EventArgs.Empty);
        Finished?.Invoke(this, EventArgs.Empty);

        try
        {
            Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(800));
        }
        catch
        {
            // Vibration is best effort; the notification carries the alert regardless.
        }
    }

    private void Persist()
    {
        if (Active is null)
            return;

        Preferences.Set(DeadlineKey, Active.EndsAt.ToUnixTimeMilliseconds());
        Preferences.Set(LabelKey, Active.Label);
        Preferences.Set(TotalKey, Active.TotalSeconds);
    }

    private static void Clear()
    {
        Preferences.Remove(DeadlineKey);
        Preferences.Remove(LabelKey);
        Preferences.Remove(TotalKey);
    }

    private void Restore()
    {
        var deadline = Preferences.Get(DeadlineKey, 0L);
        if (deadline <= 0)
            return;

        var endsAt = DateTimeOffset.FromUnixTimeMilliseconds(deadline);
        if (endsAt <= DateTimeOffset.UtcNow)
        {
            // It finished while the app was gone; the notification already alerted the user.
            Clear();
            return;
        }

        Active = new CookTimerState(
            Preferences.Get(LabelKey, string.Empty),
            Preferences.Get(TotalKey, 0),
            endsAt);
        StartTicking();
    }

    public void Dispose() => StopTicking();
}
