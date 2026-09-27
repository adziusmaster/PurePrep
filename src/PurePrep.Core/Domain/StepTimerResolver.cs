using System.Globalization;
using System.Text.RegularExpressions;

namespace PurePrep.Domain;

/// <summary>
/// The timers to offer for a step: the parser's named timers when present, otherwise durations
/// detected in the text (legacy recipes, hand-written steps). Detection understands ranges like
/// "10-12 mins" so the cook is not silently given only the upper bound.
/// </summary>
public static partial class StepTimerResolver
{
    private const string HourWords = @"hours?|hrs?|h|heures?|stunden?|std|horas?|ore|ora|godzin[ayę]?|godz|uur|uren";
    private const string MinuteWords = @"minutes?|mins?|min|minuten?|minutos?|minuti|minuto|minut[ayę]?|minuut|minuten";
    private const string SecondWords = @"seconds?|secs?|sec|sekunden?|sek|secondes?|segundos?|secondi|secondo|sekund[ayę]?|seconden";

    [GeneratedRegex(@"(?<a>\d+(?:[.,]\d+)?)(?:\s*[-–]\s*(?<b>\d+(?:[.,]\d+)?))?\s*(?:(?:de|di|of)\s+)?(?<unit>" + HourWords + "|" + MinuteWords + "|" + SecondWords + @")\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DurationPattern();

    private static readonly Regex Hours = new("^(?:" + HourWords + ")$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex Minutes = new("^(?:" + MinuteWords + ")$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // Clause boundaries for per-timer labels: punctuation, "then" / "and then", and "and" in the UI
    // languages, so "Heat the oil and fry the onion for 10 mins" labels the timer "Fry the onion".
    [GeneratedRegex(@"[.,;:]|\b(?:and\s+)?then\b|\b(?:and|i|und|et|y|e|en)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ClauseSeparator();

    // Words between a verb phrase and its duration ("knead for about 8 minutes until"), per UI language.
    private static readonly HashSet<string> Fillers = new(StringComparer.OrdinalIgnoreCase)
    {
        "for", "about", "approximately", "around", "roughly", "~", "until", "at", "least", "more", "than",
        "przez", "około", "aż", "co", "najmniej", "ponad",
        "für", "etwa", "bis", "mindestens", "mehr", "als",
        "pendant", "environ", "jusqu", "au", "moins", "plus",
        "durante", "unos", "hasta", "al", "menos", "más",
        "per", "circa", "finché", "almeno", "più",
        "gedurende", "ongeveer", "tot", "minstens", "meer", "dan",
    };

    // "until tender" and what follows only qualifies the action, so a label stops before it.
    private static readonly HashSet<string> UntilWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "until", "till", "aż", "bis", "jusqu", "hasta", "finché", "tot",
    };

    // After the verb, a preposition starts the where/with part ("boil the potatoes | in salted water").
    private static readonly HashSet<string> Prepositions = new(StringComparer.OrdinalIgnoreCase)
    {
        "in", "on", "into", "with", "over", "under", "at", "onto", "inside", "from",
        "w", "na", "z", "do", "pod", "auf", "mit", "unter", "dans", "sur", "avec", "sous", "en",
        "con", "sobre", "bajo", "su", "sotto", "op", "met", "onder",
    };

    // Two-word fillers whose halves are too common to drop on their own ("up to", "or so").
    private static readonly (string, string)[] PairFillers = [("up", "to"), ("or", "so")];

    // Words that join a clause to the previous one ("then knead", "meanwhile, boil").
    private static readonly HashSet<string> Connectors = new(StringComparer.OrdinalIgnoreCase)
    {
        "then", "and", "next", "now", "finally", "meanwhile",
        "potem", "następnie", "i", "tymczasem", "międzyczasie",
        "dann", "und", "inzwischen", "währenddessen",
        "puis", "et", "entretemps",
        "luego", "y", "mientras",
        "poi", "e", "intanto", "frattempo",
        "dan", "en", "ondertussen", "intussen",
    };

    private const int MaxLabelWords = 4;

    public static IReadOnlyList<RecipeTimer> Resolve(RecipeStep step) =>
        step.Timers.Count > 0 ? step.Timers : Detect(step.Instruction);

    public static IReadOnlyList<RecipeTimer> Detect(string? instruction)
    {
        if (string.IsNullOrWhiteSpace(instruction))
            return Array.Empty<RecipeTimer>();

        var fallback = FallbackLabel(instruction);
        var durations = DurationPattern().Matches(instruction);
        var clauses = Clauses(instruction, durations);
        var results = new List<RecipeTimer>();
        foreach (Match m in durations)
        {
            var unit = m.Groups["unit"].Value;
            var multiplier = Hours.IsMatch(unit) ? 3600 : Minutes.IsMatch(unit) ? 60 : 1;
            if (!TrySeconds(m.Groups["a"].Value, multiplier, out var a))
                continue;
            var b = m.Groups["b"].Success && TrySeconds(m.Groups["b"].Value, multiplier, out var parsed) ? parsed : a;
            var (min, max) = a <= b ? (a, b) : (b, a);
            if (results.Any(t => t.MinSeconds == min && t.MaxSeconds == max))
                continue;
            results.Add(new RecipeTimer(ClauseLabel(instruction, m, clauses, fallback, results), min, max));
        }
        return results;
    }

    private static bool TrySeconds(string number, int multiplier, out int seconds)
    {
        seconds = 0;
        if (!double.TryParse(number.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var value) || value <= 0)
            return false;
        seconds = (int)Math.Round(value * multiplier);
        return seconds is > 0 and <= 24 * 3600;
    }

    // Each timer is named after the clause holding its duration, so "knead for 8 minutes … leave to
    // rest for 30 minutes" gives "Knead" and "Leave to rest" instead of two copies of the opening words.
    // Sources in order: words before the duration, words after it, the nearest earlier clause; a source
    // that repeats an earlier timer's label in the same step is skipped while another one is left.
    private static string ClauseLabel(string instruction, Match duration, IReadOnlyList<(int Start, int End)> clauses,
        string fallback, IEnumerable<RecipeTimer> earlier)
    {
        var index = clauses.ToList().FindIndex(c => c.Start <= duration.Index && duration.Index < c.End);
        var (start, end) = clauses[index];
        var before = Tidy(Words(instruction[start..duration.Index]));
        var after = AfterWords(instruction[(duration.Index + duration.Length)..end]);
        var previous = PreviousClauseLabel(instruction, clauses, index);

        IEnumerable<string?> chain = before is not null ? [before, after, previous, fallback] : [previous, after, fallback];
        var candidates = chain.Where(c => !string.IsNullOrEmpty(c)).Select(c => c!).ToList();
        var used = earlier.Select(t => t.Label).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return candidates.FirstOrDefault(c => !used.Contains(c)) ?? candidates.FirstOrDefault() ?? fallback;
    }

    // Clause spans between separators, ignoring any separator inside a duration (the "." in "1.5 hours").
    private static List<(int Start, int End)> Clauses(string instruction, MatchCollection durations)
    {
        var spans = new List<(int, int)>();
        var start = 0;
        foreach (Match sep in ClauseSeparator().Matches(instruction))
        {
            if (durations.Any(d => sep.Index >= d.Index && sep.Index < d.Index + d.Length))
                continue;
            spans.Add((start, sep.Index));
            start = sep.Index + sep.Length;
        }
        spans.Add((start, instruction.Length));
        return spans;
    }

    // The nearest earlier clause that says what to do ("boil the potatoes, about 20 minutes").
    private static string? PreviousClauseLabel(string instruction, IReadOnlyList<(int Start, int End)> clauses, int index)
    {
        for (var i = index - 1; i >= 0; i--)
        {
            var text = instruction[clauses[i].Start..clauses[i].End];
            if (DurationPattern().IsMatch(text))
                continue;
            if (Tidy(Words(text)) is { } label)
                return label;
        }
        return null;
    }

    private static string? AfterWords(string text)
    {
        var words = Words(text);
        while (words.Count > 0 && (Fillers.Contains(words[0]) || Connectors.Contains(words[0]) || IsPairStart(words)))
            words.RemoveRange(0, IsPairStart(words) ? 2 : 1);
        return Tidy(words.Take(MaxLabelWords).ToList());
    }

    // Drops leading connectors, cuts at "until" and at the first preposition after the verb, drops
    // trailing fillers; a remainder still longer than four words keeps its verb-led first three.
    private static string? Tidy(List<string> words)
    {
        var list = words.SkipWhile(Connectors.Contains).ToList();
        var until = list.FindIndex(w => UntilWords.Contains(w) || w.StartsWith("jusqu", StringComparison.OrdinalIgnoreCase));
        if (until >= 0)
            list = list.Take(until).ToList();
        var preposition = list.Count > 1 ? list.FindIndex(1, w => Prepositions.Contains(w)) : -1;
        if (preposition > 0)
            list = list.Take(preposition).ToList();
        while (list.Count > 0)
        {
            if (list.Count >= 2 && PairFillers.Contains((list[^2].ToLowerInvariant(), list[^1].ToLowerInvariant())))
                list.RemoveRange(list.Count - 2, 2);
            else if (Fillers.Contains(list[^1]) || Connectors.Contains(list[^1]))
                list.RemoveAt(list.Count - 1);
            else
                break;
        }
        if (list.Count == 0)
            return null;
        if (list.Count > MaxLabelWords)
            list = list.Take(3).ToList();
        var text = string.Join(' ', list);
        return char.ToUpper(text[0], CultureInfo.InvariantCulture) + text[1..];
    }

    private static bool IsPairStart(List<string> words) =>
        words.Count >= 2 && PairFillers.Contains((words[0].ToLowerInvariant(), words[1].ToLowerInvariant()));

    private static List<string> Words(string text) =>
        text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(w => w.Trim(',', '.', ';', ':', '!', '?', '(', ')', '"'))
            .Where(w => w.Length > 0)
            .ToList();

    // Legacy recipes have no named timers; the step's opening words ("Heat the oil") are a better
    // label than the raw duration and avoid asking the cook to type one.
    private static string FallbackLabel(string instruction) => string.Join(' ', Words(instruction).Take(3));
}
