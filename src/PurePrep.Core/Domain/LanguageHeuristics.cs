using System.Text.RegularExpressions;

namespace PurePrep.Domain;

/// <summary>
/// Fully offline, dependency-free best-effort language detection. It covers the seven languages
/// PurePrep translates into (en, de, fr, es, it, pl, nl) plus a few extra source languages testers
/// import from (currently Romanian) so those recipes are recognised instead of being mistaken for
/// English. It scores text by counting occurrences of very common function words per language. Good
/// enough to preselect the source language for translation; the user always confirms the target.
/// </summary>
public static class LanguageHeuristics
{
    // High-frequency function words that are reasonably distinctive per language.
    private static readonly Dictionary<string, string[]> Markers = new()
    {
        ["en"] = new[] { "the", "and", "with", "add", "until", "then", "into", "for", "minutes", "salt", "pepper", "sugar", "flour", "oil" },
        ["de"] = new[] { "und", "der", "die", "das", "mit", "eine", "zwei", "geben", "hinzuf\u00fcgen", "salz", "pfeffer", "zucker", "mehl", "bis" },
        ["fr"] = new[] { "et", "le", "la", "les", "avec", "une", "dans", "puis", "ajouter", "sel", "poivre", "sucre", "farine", "jusqu" },
        ["es"] = new[] { "el", "la", "los", "las", "con", "una", "hasta", "luego", "a\u00f1adir", "sal", "pimienta", "az\u00facar", "harina", "aceite" },
        ["it"] = new[] { "il", "la", "con", "una", "poi", "fino", "aggiungere", "sale", "pepe", "zucchero", "farina", "olio", "cuocere", "quindi" },
        ["pl"] = new[] { "i", "oraz", "z", "do", "na", "sk\u0142adniki", "dodac", "dodaj", "sol", "pieprz", "cukier", "mika", "przez", "a\u017c" },
        ["nl"] = new[] { "en", "de", "het", "met", "een", "tot", "voeg", "toe", "zout", "peper", "suiker", "bloem", "dan", "roer" },
        // Romanian is a source-only language (not a translation target): distinctive diacritic words
        // keep it from colliding with the other Romance languages above.
        ["ro"] = new[] { "\u0219i", "\u015fi", "cu", "p\u00e2n\u0103", "apoi", "ad\u0103uga\u021bi", "amesteca\u021bi", "sare", "piper", "z\u0103h\u0103r", "f\u0103in\u0103", "ulei", "linguri", "fierbe\u021bi" },
    };

    private static readonly Regex Tokenizer = new(@"[\p{L}]+", RegexOptions.Compiled);

    /// <summary>
    /// Returns the best-matching ISO code (en/de/fr/es/it/pl/nl/ro) or <c>null</c> when the
    /// text is too short or no language scores above the others.
    /// </summary>
    public static string? Detect(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var tokens = Tokenizer.Matches(text.ToLowerInvariant())
            .Select(m => m.Value)
            .ToHashSet();

        if (tokens.Count < 3)
            return null;

        string? best = null;
        int bestScore = 0;
        int secondScore = 0;

        foreach (var (code, markers) in Markers)
        {
            int score = markers.Count(tokens.Contains);
            if (score > bestScore)
            {
                secondScore = bestScore;
                bestScore = score;
                best = code;
            }
            else if (score > secondScore)
            {
                secondScore = score;
            }
        }

        // Require a clear signal and a margin over the runner-up to avoid noisy guesses.
        if (bestScore == 0 || bestScore == secondScore)
            return null;

        return best;
    }
}
