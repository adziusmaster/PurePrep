using System.Globalization;
using System.Text.RegularExpressions;

namespace PurePrep.Domain;

/// <summary>A cook timer detected inside a step, e.g. "20 min" → 1200 seconds.</summary>
public sealed class StepTimer
{
    public required string Label { get; init; }
    public required int TotalSeconds { get; init; }
}

/// <summary>
/// Finds time durations inside a step instruction so they can be surfaced as tappable timers.
/// Recognises hour/minute/second unit words across the app's supported languages
/// (en, de, fr, es, it, pl, nl) so timers work on recipes in their original language.
/// </summary>
public static partial class StepTimers
{
    // Unit keyword groups. Longer/more specific spellings first to avoid partial matches.
    private const string HourWords = @"hours?|hrs?|h|heures?|stunden?|std|horas?|ore|ora|godzin[ayę]?|godz|uur|uren";
    private const string MinuteWords = @"minutes?|mins?|min|minuten?|minutos?|minuti|minuto|minut[ayę]?|minuut|minuten";
    private const string SecondWords = @"seconds?|secs?|sec|sekunden?|sek|secondes?|segundos?|secondi|secondo|sekund[ayę]?|seconden";

    // Optional filler words that can sit between the number and the unit, e.g. Romanian
    // "30 de minute", Italian "un quarto di ora", English "a couple of minutes". Without this the
    // number and unit must be adjacent, so testers saw timers like "30 de minute" go undetected.
    private const string FillerWords = @"de|di|of";

    [GeneratedRegex(@"(?<num>\d+(?:[.,]\d+)?)\s*(?:(?:" + FillerWords + @")\s+)?(?<unit>" + HourWords + @"|" + MinuteWords + @"|" + SecondWords + @")\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DurationPattern();

    private static readonly Regex Hours = BuildUnit(HourWords);
    private static readonly Regex Minutes = BuildUnit(MinuteWords);

    private static Regex BuildUnit(string words) =>
        new("^(?:" + words + ")$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static IReadOnlyList<StepTimer> Detect(string? instruction)
    {
        if (string.IsNullOrWhiteSpace(instruction))
            return Array.Empty<StepTimer>();

        var results = new List<StepTimer>();
        var seen = new HashSet<int>();

        foreach (Match m in DurationPattern().Matches(instruction))
        {
            var numText = m.Groups["num"].Value.Replace(',', '.');
            if (!double.TryParse(numText, NumberStyles.Any, CultureInfo.InvariantCulture, out var value) || value <= 0)
                continue;

            var unit = m.Groups["unit"].Value;
            var multiplier = Hours.IsMatch(unit) ? 3600 : Minutes.IsMatch(unit) ? 60 : 1;
            var seconds = (int)Math.Round(value * multiplier);
            if (seconds <= 0 || seconds > 24 * 3600)
                continue;

            // De-duplicate identical durations within one step.
            if (!seen.Add(seconds))
                continue;

            results.Add(new StepTimer
            {
                Label = m.Value.Trim(),
                TotalSeconds = seconds
            });
        }

        return results;
    }
}
