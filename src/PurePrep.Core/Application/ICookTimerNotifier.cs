namespace PurePrep.Application;

/// <summary>
/// Raises the alert when a cook timer reaches its deadline, including when the app is not running.
///
/// While PurePrep is alive an in-process timer handles the alert; this is the backstop for the case
/// the app was backgrounded and the process reclaimed — which, for a forty-minute bake with the
/// phone in a pocket, is the normal case rather than the edge case.
/// </summary>
public interface ICookTimerNotifier
{
    /// <summary>False where the platform cannot post notifications (web preview, desktop).</summary>
    bool IsSupported { get; }

    /// <summary>
    /// Asks for notification permission if it has not been granted. Returns whether alerts can
    /// actually be delivered; callers keep the timer running either way, since the in-app
    /// countdown still works without it.
    /// </summary>
    Task<bool> EnsurePermissionAsync(CancellationToken cancellationToken = default);

    /// <summary>Schedules the alert for <paramref name="endsAt"/>, replacing any existing one.</summary>
    Task ScheduleAsync(string label, DateTimeOffset endsAt, CancellationToken cancellationToken = default);

    /// <summary>Cancels a pending alert, and clears one already showing.</summary>
    Task CancelAsync(CancellationToken cancellationToken = default);
}

/// <summary>Used where notifications are unavailable. The in-app countdown still functions.</summary>
public sealed class UnsupportedCookTimerNotifier : ICookTimerNotifier
{
    public bool IsSupported => false;
    public Task<bool> EnsurePermissionAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task ScheduleAsync(string label, DateTimeOffset endsAt, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task CancelAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
