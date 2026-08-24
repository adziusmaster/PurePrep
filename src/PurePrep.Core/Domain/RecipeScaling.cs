using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PurePrep.Domain;

/// <summary>
/// Scales the leading quantity of an ingredient line by a factor. Handles decimals,
/// ASCII fractions ("1/2"), mixed numbers ("1 1/2"), common unicode fractions ("½"),
/// and simple ranges ("2-3"). Lines without a leading number are returned unchanged.
/// </summary>
public static partial class RecipeScaling
{
    private static readonly Dictionary<char, double> UnicodeFractions = new()
    {
        ['¼'] = 0.25, ['½'] = 0.5, ['¾'] = 0.75,
        ['⅓'] = 1d / 3, ['⅔'] = 2d / 3,
        ['⅕'] = 0.2, ['⅖'] = 0.4, ['⅗'] = 0.6, ['⅘'] = 0.8,
        ['⅛'] = 0.125, ['⅜'] = 0.375, ['⅝'] = 0.625, ['⅞'] = 0.875,
    };

    // Leading quantity: optional mixed/whole part, ASCII fraction, unicode fraction, or decimal,
    // optionally a range separated by - or – (en dash).
    [GeneratedRegex(@"^\s*(?<a>\d+(?:[.,]\d+)?(?:\s+\d+/\d+)?|\d+/\d+|[¼½¾⅓⅔⅕⅖⅗⅘⅛⅜⅝⅞])(?:\s*[-–]\s*(?<b>\d+(?:[.,]\d+)?|\d+/\d+|[¼½¾⅓⅔⅕⅖⅗⅘⅛⅜⅝⅞]))?",
        RegexOptions.CultureInvariant)]
    private static partial Regex LeadingQuantity();

    public static string Scale(string ingredient, double factor)
    {
        if (string.IsNullOrWhiteSpace(ingredient) || Math.Abs(factor - 1d) < 0.0001)
            return ingredient;

        var match = LeadingQuantity().Match(ingredient);
        if (!match.Success || match.Length == 0)
            return ingredient;

        if (!TryParseQuantity(match.Groups["a"].Value, out var a))
            return ingredient;

        var rest = ingredient[match.Length..];
        var scaled = Format(a * factor);

        if (match.Groups["b"].Success && TryParseQuantity(match.Groups["b"].Value, out var b))
            return $"{scaled}–{Format(b * factor)}{rest}";

        return $"{scaled}{rest}";
    }

    private static bool TryParseQuantity(string text, out double value)
    {
        value = 0;
        text = text.Trim();
        if (text.Length == 0)
            return false;

        // Single unicode fraction.
        if (text.Length == 1 && UnicodeFractions.TryGetValue(text[0], out var uf))
        {
            value = uf;
            return true;
        }

        // Mixed number "1 1/2".
        var spaceIdx = text.IndexOf(' ');
        if (spaceIdx > 0 && text.Contains('/'))
        {
            var whole = text[..spaceIdx];
            var frac = text[(spaceIdx + 1)..];
            if (double.TryParse(whole, NumberStyles.Any, CultureInfo.InvariantCulture, out var w) &&
                TryParseFraction(frac, out var f))
            {
                value = w + f;
                return true;
            }
        }

        if (text.Contains('/'))
            return TryParseFraction(text, out value);

        return double.TryParse(text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseFraction(string text, out double value)
    {
        value = 0;
        var parts = text.Split('/');
        if (parts.Length != 2)
            return false;
        if (double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var n) &&
            double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var d) && d != 0)
        {
            value = n / d;
            return true;
        }
        return false;
    }

    private static string Format(double value)
    {
        // Round to a sensible cooking precision and drop trailing zeros.
        var rounded = Math.Round(value, 2, MidpointRounding.AwayFromZero);
        var text = rounded.ToString("0.##", CultureInfo.InvariantCulture);
        return text;
    }
}
