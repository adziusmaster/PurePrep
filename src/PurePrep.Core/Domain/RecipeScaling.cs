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
    private static readonly Regex DualEquivalentRegex = BuildDualEquivalentRegex();
    private static readonly Regex LeadingUnitRegex = BuildLeadingUnitRegex();

    // Metric mass/volume units keep the existing rounded decimal formatting ("0.5 kg", "125 ml") —
    // nobody measures "⅓ kg" on a scale. Everything else (imperial units, cups, spoons, bare counts)
    // renders common cooking fractions instead.
    private static readonly HashSet<string> MetricUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        "kg", "kilogram", "kilograms", "kilo", "kilos", "kilogramy", "kilogramów",
        "g", "gram", "grams", "gramme", "grammes", "gr", "gramy", "gramów",
        "dag", "dkg", "deka", "dekagram", "dekagramy",
        "mg", "milligram", "milligrams",
        "l", "litre", "litres", "liter", "liters", "litr", "litry", "litrów",
        "ml", "millilitre", "millilitres", "milliliter", "milliliters", "dl", "cl",
    };

    // Spoon units — the ones an actual measuring-spoon set is marked in. These snap to the nearest
    // common kitchen fraction (see FormatSpoon), never to an arbitrary eighth like ⅝: nobody owns a
    // ⅝-teaspoon. Cup words are deliberately excluded even where a "spoons / cups" comment groups
    // them above — a cup is closer to imperial volume and keeps the finer eighth rounding.
    private static readonly HashSet<string> SpoonUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        "tbsp", "tablespoon", "tablespoons", "tbs", "tbl", "tsp", "teaspoon", "teaspoons",
        "łyżka", "łyżki", "łyżek", "łyżeczka", "łyżeczki", "łyżeczek",
        "esslöffel", "el", "teelöffel", "tl",
        "eetlepel", "eetlepels", "theelepel", "theelepels",
        "cuillère", "cuillères", "cuillere", "cuilleres", "càs", "càc",
        "cucharada", "cucharadas", "cucharadita", "cucharaditas",
        "cucchiaio", "cucchiai", "cucchiaino", "cucchiaini",
    };

    // The common cooking fractions a scaled amount snaps to when it lands close enough to one.
    private static readonly (double Value, string Symbol)[] NiceFractions =
    [
        (0.125, "⅛"), (0.25, "¼"), (1d / 3, "⅓"), (0.5, "½"), (2d / 3, "⅔"), (0.75, "¾"),
    ];

    // The candidate grid FormatSpoon snaps a fractional part to: the six common kitchen fractions,
    // plus the 0/1 endpoints so a value near a whole number resolves to that whole number.
    private static readonly (double Value, string Symbol)[] SpoonFractionGrid =
    [
        (0d, ""), (0.125, "⅛"), (0.25, "¼"), (1d / 3, "⅓"), (0.5, "½"), (2d / 3, "⅔"), (0.75, "¾"), (1d, ""),
    ];

    // Fallback glyphs for a spoon/imperial amount that isn't close to a "nice" fraction above — the
    // finest a measuring-spoon set actually offers is an eighth, so the amount snaps there instead
    // of showing a raw decimal ("0.4 cup" -> "⅜ cup").
    private static readonly Dictionary<int, string> EighthSymbols = new()
    {
        [1] = "⅛", [2] = "¼", [3] = "⅜", [4] = "½", [5] = "⅝", [6] = "¾", [7] = "⅞",
    };

    private const double FractionTolerance = 0.02;

    // Leading quantity: whole number with a unicode fraction ("1½"), mixed number ("1 1/2"), ASCII
    // fraction, decimal, or unicode fraction, optionally a range separated by -, – (en dash) or "to".
    // The fraction forms must be tried before the plain number: regex alternation takes the first
    // branch that matches, so a plain-number branch first reads "1/2" as just "1" ("2/2 tsp" at 2×).
    [GeneratedRegex(@"^\s*(?<a>\d+\s?[¼½¾⅓⅔⅕⅖⅗⅘⅛⅜⅝⅞]|\d+\s+\d+/\d+|\d+/\d+|\d+(?:[.,]\d+)?|[¼½¾⅓⅔⅕⅖⅗⅘⅛⅜⅝⅞])(?:(?:\s*[-–]\s*|\s+to\s+)(?<b>\d+\s?[¼½¾⅓⅔⅕⅖⅗⅘⅛⅜⅝⅞]|\d+\s+\d+/\d+|\d+/\d+|\d+(?:[.,]\d+)?|[¼½¾⅓⅔⅕⅖⅗⅘⅛⅜⅝⅞]))?",
        RegexOptions.CultureInvariant)]
    private static partial Regex LeadingQuantity();

    // American recipes hyphenate mixed numbers: "1-1/2 cups" is 1½ cups, not a range from 1 down to ½.
    [GeneratedRegex(@"(?<![\d/.,])(?<whole>\d+)-(?<num>\d+)/(?<den>\d+)(?![\d/])", RegexOptions.CultureInvariant)]
    private static partial Regex HyphenatedMixedNumber();

    [GeneratedRegex(@"^\s*\d+-\d+/\d+(?![\d/])", RegexOptions.CultureInvariant)]
    private static partial Regex LeadingHyphenatedMixedNumber();

    /// <summary>
    /// Rewrites hyphenated mixed numbers ("1-1/2") as spaced ones ("1 1/2") so they parse as one
    /// quantity. Only a proper fraction qualifies, so a genuine range like "1-3/2" is left alone.
    /// </summary>
    internal static string NormalizeMixedNumbers(string text) =>
        text.Contains('-') && text.Contains('/')
            ? HyphenatedMixedNumber().Replace(text, m =>
                int.TryParse(m.Groups["num"].Value, CultureInfo.InvariantCulture, out var num)
                && int.TryParse(m.Groups["den"].Value, CultureInfo.InvariantCulture, out var den)
                && num < den
                    ? $"{m.Groups["whole"].Value} {m.Groups["num"].Value}/{m.Groups["den"].Value}"
                    : m.Value)
            : text;

    [GeneratedRegex(@"\s*[-–—]\s*|\s+to\s+", RegexOptions.CultureInvariant)]
    private static partial Regex RangeSeparator();

    private static Regex BuildScalableQuantityRegex()
    {
        // Longest aliases first so e.g. "tablespoon" wins over "tbl" and "łyżeczka" over "łyżka".
        var unitGroup = string.Join("|", ScalableUnits
            .Distinct()
            .OrderByDescending(a => a.Length)
            .Select(Regex.Escape));

        const string number = @"(?:\d+\s?[¼½¾⅓⅔⅕⅖⅗⅘⅛⅜⅝⅞]|\d+\s+\d+\s*/\s*\d+|\d+\s*/\s*\d+|\d+(?:[.,]\d+)?|[¼½¾⅓⅔⅕⅖⅗⅘⅛⅜⅝⅞])";
        var qty = @"(?<qty>" + number + @"(?:(?:\s*[-–—]\s*|\s+to\s+)" + number + @")?)";
        // Require a boundary before the quantity (so a flour type like "typu 500" is not scaled) and a
        // non-letter boundary after the unit (so "l" won't match inside "listki", "g" inside "garść").
        var pattern = @"(?<![\p{L}\d])" + qty + @"\s*(?<unit>" + unitGroup + @")(?![\p{L}])";
        return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    }

    // The unit after a leading quantity followed by a bracketed equivalent: the "lbs. (700 grams)" in
    // "1½ lbs. (700 grams) chicken". Only a real unit qualifies, so the per-can size in
    // "2 cans (400 g each)" is not mistaken for an equivalent of the count.
    private static Regex BuildDualEquivalentRegex()
    {
        var unitGroup = string.Join("|", ScalableUnits
            .Distinct()
            .OrderByDescending(a => a.Length)
            .Select(Regex.Escape));
        var pattern = @"^\s*(?:" + unitGroup + @")\.?\s*\([^)]*\)";
        return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    }

    // The unit word immediately after a leading quantity — "cup" in "½ cup Greek yogurt", "lbs" in
    // "1½ lbs. (700 grams) chicken" — used to decide fraction vs. decimal formatting when scaling.
    private static Regex BuildLeadingUnitRegex()
    {
        var unitGroup = string.Join("|", ScalableUnits
            .Distinct()
            .OrderByDescending(a => a.Length)
            .Select(Regex.Escape));
        var pattern = @"^\s*(?<unit>" + unitGroup + @")(?![\p{L}])";
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

        return recipe with
        {
            Ingredients = recipe.Ingredients.Select(i => Scale(i, factor)).ToArray(),
            Steps = recipe.Steps
                .Select(s => s with { Instruction = ScaleText(s.Instruction, factor) })
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

        // Only the leading amount is rewritten here, so only a leading "1-1/2" is read as 1½; any other
        // mixed number in the line ("cut into 1-1/2 inch pieces") stays as written.
        if (LeadingHyphenatedMixedNumber().Match(ingredient) is { Success: true } lead)
            ingredient = NormalizeMixedNumbers(lead.Value) + ingredient[lead.Length..];
        var match = LeadingQuantity().Match(ingredient);
        if (match.Success && match.Length > 0 && TryParseQuantity(match.Groups["a"].Value, out var a))
        {
            var tail = ingredient[match.Length..];
            var unitMatch = LeadingUnitRegex.Match(tail);
            var unit = unitMatch.Success ? unitMatch.Groups["unit"].Value : null;

            // A bracketed equivalent ("1½ lbs. (700 grams)") is the same amount in another unit, so it
            // scales together with the leading quantity; otherwise the two would disagree.
            var rest = DualEquivalentRegex.Replace(tail, m => ScaleText(m.Value, factor), 1);
            var scaled = FormatQuantity(a * factor, unit);
            return match.Groups["b"].Success && TryParseQuantity(match.Groups["b"].Value, out var b)
                ? $"{scaled}–{FormatQuantity(b * factor, unit)}{rest}"
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
            var unit = match.Groups["unit"].Value;
            // Replace only the quantity, preserving the exact spacing and unit text that followed it.
            return ScaleQuantityText(qty, factor, unit) + match.Value[qty.Length..];
        });
    }

    private static string ScaleQuantityText(string qtyText, double factor, string unit)
    {
        // "1-1/2" is a hyphenated mixed number, not a range from 1 down to ½.
        qtyText = NormalizeMixedNumbers(qtyText);
        var range = RangeSeparator().Split(qtyText);
        if (range.Length == 2 && TryParseQuantity(range[0], out var lo) && TryParseQuantity(range[1], out var hi))
            return $"{FormatQuantity(lo * factor, unit)}–{FormatQuantity(hi * factor, unit)}";
        return TryParseQuantity(qtyText, out var value) ? FormatQuantity(value * factor, unit) : qtyText;
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

        // Unicode fraction, alone ("½") or after a whole number ("1½", "1 ½").
        if (UnicodeFractions.TryGetValue(text[^1], out var uf))
        {
            var wholePart = text[..^1].Trim();
            if (wholePart.Length == 0)
            {
                value = uf;
                return true;
            }
            if (!double.TryParse(wholePart, NumberStyles.Any, CultureInfo.InvariantCulture, out var whole))
                return false;
            value = whole + uf;
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

    /// <summary>
    /// Formats a scaled quantity for display. Metric mass/volume units (<see cref="MetricUnits"/>)
    /// round to a cook-friendly precision (<see cref="FormatMetric"/>) instead of a raw decimal —
    /// nobody weighs out "758.33 g" on a kitchen scale. Spoon units (<see cref="SpoonUnits"/>) snap
    /// to the nearest common kitchen fraction (<see cref="FormatSpoon"/>) — nobody owns a
    /// ⅝-teaspoon. Imperial weight/volume (oz, lb, cup, fl oz, …) snaps to the nearest eighth
    /// (<see cref="FormatEighths"/>); bare counts with no unit (eggs, onions) snap to the nearest
    /// half (<see cref="FormatHalves"/>) — nobody cuts an onion into eighths.
    /// </summary>
    private static string FormatQuantity(double value, string? unit)
    {
        if (value < 0)
            return Format(value);
        if (unit is null)
            return FormatHalves(value);
        if (MetricUnits.Contains(unit))
            return FormatMetric(value);
        if (SpoonUnits.Contains(unit))
            return FormatSpoon(value);
        return FormatEighths(value);
    }

    /// <summary>
    /// Rounds a metric mass/volume amount to how a cook would actually measure it: below 10, one
    /// decimal place with a trailing ".0" dropped ("7.5 g"); 10–100, the nearest whole unit
    /// ("58 ml"); 100–1000, the nearest 5 ("758.33" -> "760 g"); 1000 and up, the nearest 10
    /// ("1166.7" -> "1170 g"). Units are never converted here — that is the unit converter's job.
    /// A positive amount never collapses to "0": 0.1, the smallest step shown, is the floor.
    /// </summary>
    private static string FormatMetric(double value)
    {
        const double smallestStep = 0.1;
        var abs = Math.Abs(value);
        return abs switch
        {
            > 0 and < smallestStep => Format(smallestStep),
            < 10 => Format(Math.Round(value, 1, MidpointRounding.AwayFromZero)),
            < 100 => Math.Round(value, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture),
            < 1000 => (Math.Round(value / 5, MidpointRounding.AwayFromZero) * 5).ToString("0", CultureInfo.InvariantCulture),
            _ => (Math.Round(value / 10, MidpointRounding.AwayFromZero) * 10).ToString("0", CultureInfo.InvariantCulture),
        };
    }

    /// <summary>
    /// Renders an imperial weight/volume amount (oz, lb, cup, fl oz, quart, …) as a common cooking
    /// fraction. A "nice" fraction close enough within <see cref="FractionTolerance"/> (¼, ⅓, ½, ⅔,
    /// ¾, …) wins first, so exact thirds keep reading as thirds; otherwise the amount snaps to the
    /// nearest eighth — the finest a set of measuring cups actually offers — rendered with
    /// <see cref="EighthSymbols"/>. A nonzero amount never collapses to "0"; ⅛ is the smallest
    /// amount ever shown. Spoon units use <see cref="FormatSpoon"/> instead, which never produces
    /// ⅜/⅝/⅞ — no measuring-spoon set has those.
    /// </summary>
    private static string FormatEighths(double value)
    {
        var whole = Math.Floor(value);
        var frac = value - whole;

        foreach (var (niceValue, symbol) in NiceFractions)
        {
            if (Math.Abs(frac - niceValue) <= FractionTolerance)
                return whole <= 0 ? symbol : $"{(long)whole}{symbol}";
        }

        // Close enough to a whole number that a fraction would be misleadingly precise.
        if (frac <= FractionTolerance)
            return ((long)whole).ToString(CultureInfo.InvariantCulture);
        if (frac >= 1 - FractionTolerance)
            return ((long)whole + 1).ToString(CultureInfo.InvariantCulture);

        var totalEighths = (long)Math.Round(value * 8, MidpointRounding.AwayFromZero);
        if (totalEighths == 0 && value > 0)
            totalEighths = 1;

        var wholePart = totalEighths / 8;
        var eighthPart = (int)(totalEighths - wholePart * 8);
        if (eighthPart == 0)
            return wholePart.ToString(CultureInfo.InvariantCulture);

        var eighthSymbol = EighthSymbols[eighthPart];
        return wholePart <= 0 ? eighthSymbol : $"{wholePart}{eighthSymbol}";
    }

    /// <summary>
    /// Renders a spoon amount (tsp/tbsp and their non-English equivalents) snapped to the nearest of
    /// the six common kitchen fractions in <see cref="SpoonFractionGrid"/> — the markings an actual
    /// measuring-spoon set has. Unlike <see cref="FormatEighths"/>, this never produces ⅜/⅝/⅞: no
    /// spoon set has those. On an exact tie between two candidates, rounds down (safer for salt and
    /// spices, e.g. ½ tsp × 7/6 = 0.5833, exactly between ½ and ⅔, resolves to ½). A nonzero amount
    /// never collapses to "0"; ⅛ is the smallest amount ever shown.
    /// </summary>
    private static string FormatSpoon(double value)
    {
        var whole = Math.Floor(value);
        var frac = value - whole;

        var bestValue = 0d;
        var bestSymbol = "";
        var bestDistance = double.MaxValue;
        foreach (var (candidateValue, symbol) in SpoonFractionGrid)
        {
            var distance = Math.Abs(frac - candidateValue);
            // Only replace the current best on a real improvement — on a tie (within floating-point
            // noise) the earlier, smaller candidate already in `bestValue` wins, which is the round-
            // down behaviour we want.
            if (distance < bestDistance - 1e-9)
            {
                bestDistance = distance;
                bestValue = candidateValue;
                bestSymbol = symbol;
            }
        }

        if (bestValue == 0d && whole <= 0 && value > 0)
        {
            bestValue = 0.125;
            bestSymbol = "⅛";
        }

        if (bestValue == 1d)
            whole += 1;

        return bestSymbol.Length == 0
            ? ((long)whole).ToString(CultureInfo.InvariantCulture)
            : whole <= 0 ? bestSymbol : $"{(long)whole}{bestSymbol}";
    }

    /// <summary>
    /// Renders a bare count (no recognised unit — "2 eggs", "1 onion") snapped to the nearest half;
    /// a cook halves an onion, not eighths it. A nonzero amount never collapses to "0".
    /// </summary>
    private static string FormatHalves(double value)
    {
        var totalHalves = (long)Math.Round(value * 2, MidpointRounding.AwayFromZero);
        if (totalHalves == 0 && value > 0)
            totalHalves = 1;

        var wholePart = totalHalves / 2;
        var halfPart = totalHalves - wholePart * 2;
        if (halfPart == 0)
            return wholePart.ToString(CultureInfo.InvariantCulture);

        return wholePart <= 0 ? "½" : $"{wholePart}½";
    }
}
