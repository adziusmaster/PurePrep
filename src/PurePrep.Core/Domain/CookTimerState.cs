namespace PurePrep.Domain;

/// <summary>
/// A running cook timer, held as a deadline rather than a countdown.
///
/// Anchoring on <see cref="EndsAt"/> means the remaining time is derived from the clock whenever it
/// is read, so it stays correct across backgrounding, dropped ticks and process restarts. A ticking
/// counter cannot survive any of those — it simply stops decrementing while the app is not running.
/// </summary>
public sealed record CookTimerState(string Label, int TotalSeconds, DateTimeOffset EndsAt)
{
    /// <summary>
    /// Stable identifier for this timer within a run. Timers are concurrent now, so each needs its
    /// own id to key persistence, its Android alarm/notification, and the "stop this one" button.
    /// </summary>
    public int Id { get; init; }

    /// <summary>Upper bound for a range timer; equals <see cref="TotalSeconds"/> for a single duration.</summary>
    public int MaxSeconds { get; init; }

    public bool CanExtend => MaxSeconds > TotalSeconds;

    /// <summary>
    /// The recipe and step this timer was started from, so the app-wide timers bar can jump back into
    /// Focus Mode at the right place. Null for timers with no recipe context (e.g. restored from a
    /// pre-Task-18 backup).
    /// </summary>
    public Guid? RecipeId { get; init; }

    /// <summary>The step index within <see cref="RecipeId"/>'s recipe this timer belongs to.</summary>
    public int? StepIndex { get; init; }

    /// <summary>
    /// Which of the step's timers (its index in the step's timer list) this one was started from, so
    /// a renamed or adjusted start still maps back to its chip. Null for timers restored from JSON
    /// written before it existed; <see cref="StepTimerSlot"/> then falls back to the label.
    /// </summary>
    public int? TimerIndex { get; init; }

    /// <summary>Begins a timer of <paramref name="totalSeconds"/> from <paramref name="now"/>.</summary>
    public static CookTimerState Start(string label, int totalSeconds, DateTimeOffset now) =>
        new(label, totalSeconds, now.AddSeconds(totalSeconds)) { MaxSeconds = totalSeconds };

    /// <summary>Begins a range timer that first counts to <paramref name="minSeconds"/>.</summary>
    public static CookTimerState Start(string label, int minSeconds, int maxSeconds, DateTimeOffset now) =>
        new(label, minSeconds, now.AddSeconds(minSeconds)) { MaxSeconds = Math.Max(minSeconds, maxSeconds) };

    /// <summary>Adds time ("+2 min") without exceeding the range's maximum.</summary>
    public CookTimerState Extend(int seconds)
    {
        var total = Math.Min(TotalSeconds + seconds, MaxSeconds);
        var startedAt = EndsAt.AddSeconds(-TotalSeconds);
        return this with { TotalSeconds = total, EndsAt = startedAt.AddSeconds(total) };
    }

    /// <summary>Seconds left, never negative.</summary>
    public int RemainingSeconds(DateTimeOffset now)
    {
        var remaining = (EndsAt - now).TotalSeconds;
        return remaining <= 0 ? 0 : (int)Math.Ceiling(remaining);
    }

    public bool HasFinished(DateTimeOffset now) => now >= EndsAt;

    /// <summary>
    /// True once a range timer has counted down to its current deadline while it can still be pushed
    /// further ("+2 min") — the moment to offer "Check now" rather than treat it as finished. A timer
    /// already at its maximum (<see cref="CanExtend"/> false) that reaches its deadline is genuinely
    /// finished, not at a checkpoint.
    /// </summary>
    public bool IsAtCheckpoint(DateTimeOffset now) => CanExtend && HasFinished(now);

    /// <summary>
    /// Whether a persisted timer record should still be restored on an app restart. One whose
    /// deadline is still ahead is always kept. One whose deadline has passed is kept only if it's a
    /// range that hadn't yet reached its maximum — it resumes right where it left off, at its
    /// checkpoint — since a genuinely finished timer already alerted via the platform notification
    /// while the app was away, and resurrecting it would just show a done timer as still running.
    /// <paramref name="maxSeconds"/> may be 0 for a pre-Task-18 record with no recorded maximum;
    /// that is treated as equal to <paramref name="totalSeconds"/> (not extendable), never as a
    /// bogus "always extendable" zero.
    /// </summary>
    public static bool ShouldRestore(int totalSeconds, int maxSeconds, DateTimeOffset endsAt, DateTimeOffset now)
    {
        if (now < endsAt)
            return true;

        var effectiveMax = maxSeconds > 0 ? maxSeconds : totalSeconds;
        return effectiveMax > totalSeconds;
    }

    /// <summary>Completion fraction from 0 to 1, for the progress ring.</summary>
    public double Progress(DateTimeOffset now)
    {
        if (TotalSeconds <= 0)
            return 1;

        var elapsed = TotalSeconds - RemainingSeconds(now);
        return Math.Clamp((double)elapsed / TotalSeconds, 0, 1);
    }

    /// <summary>
    /// Formats seconds for reading at arm's length across a kitchen: mm:ss, widening to h:mm:ss only
    /// once there is an hour to show.
    /// </summary>
    public static string Clock(int seconds)
    {
        if (seconds < 0)
            seconds = 0;

        var span = TimeSpan.FromSeconds(seconds);
        return span.TotalHours >= 1
            ? $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}"
            : $"{span.Minutes:00}:{span.Seconds:00}";
    }
}
