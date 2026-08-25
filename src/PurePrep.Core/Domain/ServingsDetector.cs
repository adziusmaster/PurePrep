using System.Text.RegularExpressions;

namespace PurePrep.Domain;

/// <summary>
/// Best-effort detection of how many people a recipe serves, read from its own text
/// (title, then steps, then ingredients). Multilingual: recognises the yield/serving
/// phrases used across the app's supported languages (en/de/fr/es/it/pl/nl). Returns
/// <c>null</c> when no reliable serving count is found.
/// </summary>
public static partial class ServingsDetector
{
    // "4 servings", "4 portions", "4 personen", "4 osób", "4-6 servings" (takes the first number).
    [GeneratedRegex(
        @"\b(\d{1,3})\s*(?:[-–]\s*\d{1,3}\s*)?(?:servings?|portions?|persons?|people|porzioni|porties|porcj\w*|raci[oó]n\w*|porci[oó]n\w*|personen|personnes|persone|personas|os[oó]b\w*|osob\w*)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NumberThenNoun();

    // "serves 4", "makes 4", "yield: 4", "rinde 4", "ergibt 4".
    [GeneratedRegex(
        @"\b(?:serves?|serving|makes|yields?|rinde|ergibt|ergibt für|serveert|serveren)\s*:?\s*(\d{1,3})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VerbThenNumber();

    // "for 4 people", "für 4 personen", "pour 4 personnes", "para 4 personas", "per 4 persone",
    // "voor 4 personen", "dla 4 osób", "na 4 osoby".
    [GeneratedRegex(
        @"\b(?:for|f[uü]r|pour|para|per|voor|dla|na)\s+(\d{1,3})\s+(?:people|persons?|personen|personnes|persone|personas|os[oó]b\w*|osob\w*)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PrepNumberNoun();

    public static int? Detect(string? title, IEnumerable<string>? ingredients, IEnumerable<string>? steps)
    {
        // Title first (most likely to carry the yield), then steps, then ingredients.
        var segments = new List<string>();
        if (!string.IsNullOrWhiteSpace(title))
            segments.Add(title!);
        if (steps is not null)
            segments.AddRange(steps.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (ingredients is not null)
            segments.AddRange(ingredients.Where(s => !string.IsNullOrWhiteSpace(s)));

        foreach (var text in segments)
        {
            if (TryMatch(NumberThenNoun(), text, out var a)) return a;
            if (TryMatch(VerbThenNumber(), text, out var b)) return b;
            if (TryMatch(PrepNumberNoun(), text, out var c)) return c;
        }

        return null;
    }

    private static bool TryMatch(Regex regex, string text, out int value)
    {
        value = 0;
        var match = regex.Match(text);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var n) && n is > 0 and <= 99)
        {
            value = n;
            return true;
        }
        return false;
    }
}
