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
    /// <summary>Begins a timer of <paramref name="totalSeconds"/> from <paramref name="now"/>.</summary>
    public static CookTimerState Start(string label, int totalSeconds, DateTimeOffset now) =>
        new(label, totalSeconds, now.AddSeconds(totalSeconds));

    /// <summary>Seconds left, never negative.</summary>
    public int RemainingSeconds(DateTimeOffset now)
    {
        var remaining = (EndsAt - now).TotalSeconds;
        return remaining <= 0 ? 0 : (int)Math.Ceiling(remaining);
    }

    public bool HasFinished(DateTimeOffset now) => now >= EndsAt;

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
