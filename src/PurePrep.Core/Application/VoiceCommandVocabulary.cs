namespace PurePrep.Application;

/// <summary>One representative spoken word for each navigation command, in a given language.</summary>
public sealed record VoicePhrases(string Next, string Previous, string Repeat);

/// <summary>
/// The words PurePrep listens for during hands-free cooking, and the languages it understands them
/// in. Centralised here (rather than only in the platform recogniser) so the UI can both gate the
/// feature to supported languages and tell the cook exactly what to say in the recipe's language.
///
/// Matching is deliberately forgiving and multilingual: a phrase is checked against every language's
/// keywords at once, so "go back" or "wstecz" both work regardless of which recipe is open.
/// </summary>
public static class VoiceCommandVocabulary
{
    // The language a cook is shown as an example for each command. Codes match the app's supported
    // UI languages, which are exactly the languages we have keyword coverage for below.
    private static readonly Dictionary<string, VoicePhrases> ExamplesByLanguage = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = new("next", "back", "repeat"),
        ["de"] = new("weiter", "zurück", "wiederholen"),
        ["fr"] = new("suivant", "précédent", "répète"),
        ["es"] = new("siguiente", "atrás", "repite"),
        ["it"] = new("avanti", "indietro", "ripeti"),
        ["pl"] = new("dalej", "wstecz", "powtórz"),
        ["nl"] = new("volgende", "terug", "herhaal"),
    };

    // Keyword sets scanned for each command, pooled across all supported languages. Longest-standing
    // near-misses ("go back", "again please") are covered by the extra synonyms.
    private static readonly string[] RepeatWords =
        ["repeat", "again", "read", "powtórz", "wiederhol", "repite", "répète", "ripeti", "herhaal"];

    private static readonly string[] PreviousWords =
        ["back", "previous", "wstecz", "poprzedni", "zurück", "atrás", "précédent", "indietro", "terug"];

    private static readonly string[] NextWords =
        ["next", "forward", "continue", "dalej", "następny", "weiter", "siguiente", "suivant", "avanti", "volgende", "verder"];

    /// <summary>ISO 639-1 codes for which hands-free voice navigation is offered.</summary>
    public static IReadOnlyCollection<string> SupportedLanguages => ExamplesByLanguage.Keys;

    /// <summary>True when spoken navigation is available for the given language code.</summary>
    public static bool IsLanguageSupported(string? languageCode) =>
        languageCode is not null && ExamplesByLanguage.ContainsKey(BaseCode(languageCode));

    /// <summary>Example words to show the cook for the given language, or English if unknown.</summary>
    public static VoicePhrases ExamplesFor(string? languageCode)
    {
        if (languageCode is not null && ExamplesByLanguage.TryGetValue(BaseCode(languageCode), out var phrases))
            return phrases;
        return ExamplesByLanguage["en"];
    }

    /// <summary>Matches a recognised phrase to a command; returns false when nothing is recognised.</summary>
    public static bool TryMatch(string? phrase, out VoiceCommand command)
    {
        command = default;
        if (string.IsNullOrWhiteSpace(phrase))
            return false;

        var text = phrase.ToLowerInvariant();

        // Repeat is checked first: its words ("read", "again") are the least likely to collide with
        // a step's own wording, and a stray repeat is harmless where a stray step-skip is not.
        if (ContainsAny(text, RepeatWords)) { command = VoiceCommand.Repeat; return true; }
        if (ContainsAny(text, PreviousWords)) { command = VoiceCommand.Previous; return true; }
        if (ContainsAny(text, NextWords)) { command = VoiceCommand.Next; return true; }
        return false;
    }

    private static bool ContainsAny(string text, string[] needles)
    {
        foreach (var needle in needles)
        {
            if (text.Contains(needle, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    // Reduces "pl-PL" / "en_US" to the bare ISO 639-1 code used as the table key.
    private static string BaseCode(string code)
    {
        var span = code.AsSpan().Trim();
        var cut = span.IndexOfAny('-', '_');
        if (cut > 0)
            span = span[..cut];
        return span.ToString().ToLowerInvariant();
    }
}
