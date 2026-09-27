namespace PurePrep.Domain;

/// <summary>The plural form a count takes in a language (the CLDR categories the app's languages use).</summary>
public enum PluralCategory
{
    One,
    Few,
    Other,
}

/// <summary>
/// Picks the plural form for a whole-number count in the app's UI languages. English, German, Dutch,
/// Spanish and Italian split "1" from the rest; French also treats 0 as singular; Polish has three
/// forms: 1 krok, 2–4 kroki (also 22–24, 32–34, … but not 12–14), and 5+ kroków.
/// </summary>
public static class PluralRules
{
    public static PluralCategory For(string? language, int count)
    {
        var code = (language ?? string.Empty).Split('-', '_')[0].ToLowerInvariant();
        var n = Math.Abs(count);
        return code switch
        {
            "pl" when n == 1 => PluralCategory.One,
            "pl" when n % 10 is >= 2 and <= 4 && n % 100 is not (>= 12 and <= 14) => PluralCategory.Few,
            "pl" => PluralCategory.Other,
            "fr" => n is 0 or 1 ? PluralCategory.One : PluralCategory.Other,
            _ => n == 1 ? PluralCategory.One : PluralCategory.Other,
        };
    }
}
