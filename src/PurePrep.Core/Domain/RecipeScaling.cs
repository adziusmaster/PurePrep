using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PurePrep.Domain;

/// <summary>
/// Scales the quantities in a recipe line by a factor. Handles decimals, ASCII fractions ("1/2"),
/// mixed numbers ("1 1/2"), common unicode fractions ("½"), and simple ranges ("2-3"). A leading
/// quantity is scaled directly; when the amount instead sits later in the line — as in languages
/// that name the ingredient first ("marchewki 500 g", "olej 4 łyżki") — every quantity attached to
/// a known cooking unit is scaled. Lines with no recognisable quantity are returned unchanged.
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

    // Cooking units whose quantities genuinely multiply when a batch is resized: mass, volume,
    // spoons, and cups, across the languages the app supports. Units that must NOT scale — length
    // (tin/pan sizes) and temperature — are deliberately absent, so "bake at 180°C in a 24 cm tin"
    // is left alone while "500 g" or "4 łyżki" is scaled. Matching is case-insensitive.
    private static readonly string[] ScalableUnits =
    [
        // Mass — metric (incl. Polish plurals and dag/dkg)
        "kg", "kilogram", "kilograms", "kilo", "kilos", "kilogramy", "kilogramów",
        "g", "gram", "grams", "gramme", "grammes", "gr", "gramy", "gramów",
        "dag", "dkg", "deka", "dekagram", "dekagramy",
        "mg", "milligram", "milligrams",
        // Mass — imperial
        "lb", "lbs", "pound", "pounds", "oz", "ounce", "ounces",
        // Volume — metric
        "l", "litre", "litres", "liter", "liters", "litr", "litry", "litrów",
        "ml", "millilitre", "millilitres", "milliliter", "milliliters", "dl", "cl",
        // Volume — imperial
        "cup", "cups", "pint", "pints", "quart", "quarts", "gallon", "gallons",
        "fl oz", "floz", "fluid ounce", "fluid ounces",
        // Spoons — English
        "tbsp", "tablespoon", "tablespoons", "tbs", "tbl", "tsp", "teaspoon", "teaspoons",
        // Spoons / cups — Polish
        "łyżka", "łyżki", "łyżek", "łyżeczka", "łyżeczki", "łyżeczek",
        "szklanka", "szklanki", "szklanek",
        // Spoons / cups — German
        "esslöffel", "el", "teelöffel", "tl", "tasse", "tassen",
        // Spoons / cups — Dutch
        "eetlepel", "eetlepels", "theelepel", "theelepels", "kopje", "kopjes",
        // Spoons / cups — French
        "cuillère", "cuillères", "cuillere", "cuilleres", "càs", "càc", "tasses",
        // Spoons / cups — Spanish
        "cucharada", "cucharadas", "cucharadita", "cucharaditas", "taza", "tazas",
        // Spoons / cups — Italian
        "cucchiaio", "cucchiai", "cucchiaino", "cucchiaini", "tazza", "tazze",
    ];

    private static readonly Regex ScalableQuantityRegex = BuildScalableQuantityRegex();

    // Leading quantity: optional mixed/whole part, ASCII fraction, unicode fraction, or decimal,
    // optionally a range separated by - or – (en dash).
    [GeneratedRegex(@"^\s*(?<a>\d+(?:[.,]\d+)?(?:\s+\d+/\d+)?|\d+/\d+|[¼½¾⅓⅔⅕⅖⅗⅘⅛⅜⅝⅞])(?:\s*[-–]\s*(?<b>\d+(?:[.,]\d+)?|\d+/\d+|[¼½¾⅓⅔⅕⅖⅗⅘⅛⅜⅝⅞]))?",
        RegexOptions.CultureInvariant)]
    private static partial Regex LeadingQuantity();

    private static Regex BuildScalableQuantityRegex()
    {
        // Longest aliases first so e.g. "tablespoon" wins over "tbl" and "łyżeczka" over "łyżka".
        var unitGroup = string.Join("|", ScalableUnits
            .Distinct()
            .OrderByDescending(a => a.Length)
            .Select(Regex.Escape));

        const string number = @"(?:\d+\s+\d+\s*/\s*\d+|\d+\s*/\s*\d+|\d+(?:[.,]\d+)?|[¼½¾⅓⅔⅕⅖⅗⅘⅛⅜⅝⅞])";
        var qty = @"(?<qty>" + number + @"(?:\s*[-–—]\s*" + number + @")?)";
        // Require a boundary before the quantity (so a flour type like "typu 500" is not scaled) and a
        // non-letter boundary after the unit (so "l" won't match inside "listki", "g" inside "garść").
        var pattern = @"(?<![\p{L}\d])" + qty + @"\s*(?<unit>" + unitGroup + @")(?![\p{L}])";
        return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    }

    /// <summary>
    /// Returns a copy of <paramref name="recipe"/> with every ingredient quantity multiplied by
    /// <paramref name="factor"/>. Method steps are scaled too, but only for real mass/volume/spoon
    /// amounts: cooking times, temperatures, and tin sizes carry no scalable unit, so "bake for 20
    /// minutes at 180°C" is untouched while "stir in 200 g sugar" is scaled. Identity fields are
    /// preserved so the result can stand in for the original on screen.
    /// </summary>
    public static ParsedRecipe ScaleRecipe(ParsedRecipe recipe, double factor)
    {
        if (Math.Abs(factor - 1d) < 0.0001)
            return recipe;

        return new ParsedRecipe
        {
            Id = recipe.Id,
            Title = recipe.Title,
            SourceUrl = recipe.SourceUrl,
            SourceSystem = recipe.SourceSystem,
            SavedAt = recipe.SavedAt,
            Ingredients = recipe.Ingredients.Select(i => Scale(i, factor)).ToArray(),
            Steps = recipe.Steps
                .Select(s => new RecipeStep { Order = s.Order, Instruction = ScaleText(s.Instruction, factor) })
                .ToArray(),
        };
    }

    /// <summary>
    /// Scales an ingredient line. A leading quantity is scaled directly (so counts like "3 eggs" and
    /// multipliers like "3 x 400 g" keep their current behaviour); otherwise every quantity attached
    /// to a known cooking unit is scaled, which is what fixes amounts that trail the ingredient name.
    /// </summary>
    public static string Scale(string ingredient, double factor)
    {
        if (string.IsNullOrWhiteSpace(ingredient) || Math.Abs(factor - 1d) < 0.0001)
            return ingredient;

        var match = LeadingQuantity().Match(ingredient);
        if (match.Success && match.Length > 0 && TryParseQuantity(match.Groups["a"].Value, out var a))
        {
            var rest = ingredient[match.Length..];
            var scaled = Format(a * factor);
            return match.Groups["b"].Success && TryParseQuantity(match.Groups["b"].Value, out var b)
                ? $"{scaled}–{Format(b * factor)}{rest}"
                : $"{scaled}{rest}";
        }

        // No usable leading number: the amount may sit later in the line ("marchewki 500 g",
        // "olej 4 łyżki"). Scale the unit-bearing quantities wherever they are.
        return ScaleText(ingredient, factor);
    }

    /// <summary>
    /// Scales every "quantity + unit" measurement found anywhere in <paramref name="text"/>, limited
    /// to the curated cooking units in <see cref="ScalableUnits"/>. Bare counts are never scaled here
    /// (a step's "beat 2 eggs", or a leading "1.", must stay put) — only <see cref="Scale"/> scales a
    /// leading count. Used for both trailing ingredient amounts and amounts embedded in method steps.
    /// </summary>
    public static string ScaleText(string text, double factor)
    {
        if (string.IsNullOrWhiteSpace(text) || Math.Abs(factor - 1d) < 0.0001)
            return text;

        return ScalableQuantityRegex.Replace(text, match =>
        {
            // Leave "N x M unit" multipliers alone: the leading count already carries the scaling, so
            // scaling the per-unit amount too would double-count (3 x 400 g -> 6 x 400 g, not 800 g).
            if (PrecededByCountMultiplier(text, match.Index))
                return match.Value;

            var qty = match.Groups["qty"].Value;
            // Replace only the quantity, preserving the exact spacing and unit text that followed it.
            return ScaleQuantityText(qty, factor) + match.Value[qty.Length..];
        });
    }

    private static string ScaleQuantityText(string qtyText, double factor)
    {
        var range = Regex.Split(qtyText, @"\s*[-–—]\s*");
        if (range.Length == 2 && TryParseQuantity(range[0], out var lo) && TryParseQuantity(range[1], out var hi))
            return $"{Format(lo * factor)}–{Format(hi * factor)}";
        return TryParseQuantity(qtyText, out var value) ? Format(value * factor) : qtyText;
    }

    private static bool PrecededByCountMultiplier(string text, int index)
    {
        var head = text.AsSpan(0, index).TrimEnd();
        if (head.Length == 0 || head[^1] is not ('x' or 'X' or '×' or '*'))
            return false;
        var before = head[..^1].TrimEnd();
        return before.Length > 0 && char.IsDigit(before[^1]);
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
