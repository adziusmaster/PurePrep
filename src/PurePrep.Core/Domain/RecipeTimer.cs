namespace PurePrep.Domain;

/// <summary>
/// A named cook timer attached to a step, e.g. "Fry onion" for 10–12 minutes. A single duration has
/// <see cref="MinSeconds"/> == <see cref="MaxSeconds"/>; a range counts down to the minimum, then the
/// cook can extend towards the maximum.
/// </summary>
public sealed record RecipeTimer(string Label, int MinSeconds, int MaxSeconds)
{
    public bool IsRange => MaxSeconds > MinSeconds;

    /// <summary>"10 min", "10–12 min", "45 s", "1 h 30 min".</summary>
    public string DurationText() =>
        IsRange && MinSeconds % 60 == 0 && MaxSeconds % 60 == 0 && MaxSeconds < 3600
            ? $"{MinSeconds / 60}–{MaxSeconds / 60} min"
            : IsRange ? $"{Format(MinSeconds)}–{Format(MaxSeconds)}" : Format(MinSeconds);

    private static string Format(int seconds)
    {
        if (seconds < 60)
            return $"{seconds} s";
        var span = TimeSpan.FromSeconds(seconds);
        if (span.TotalHours < 1)
            return span.Seconds == 0 ? $"{(int)span.TotalMinutes} min" : $"{(int)span.TotalMinutes} min {span.Seconds} s";
        return span.Minutes == 0 ? $"{(int)span.TotalHours} h" : $"{(int)span.TotalHours} h {span.Minutes} min";
    }
}
