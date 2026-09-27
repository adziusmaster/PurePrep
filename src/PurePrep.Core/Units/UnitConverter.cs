using System.Globalization;
using System.Text.RegularExpressions;
using PurePrep.Domain;

namespace PurePrep.Units;

/// <summary>
/// Detects the measurement system of free-text recipe content and converts quantities
/// between metric and imperial. Works on any text (ingredient lines or instruction steps):
/// it scans for "quantity + unit" tokens and rewrites only those, leaving the rest intact.
/// </summary>
public static class UnitConverter
{
    private enum Dimension { Mass, Volume, Temperature, Length }

    private sealed record Unit(string Key, Dimension Dimension, MeasurementSystem? System, double ToBase, string[] Aliases);

    // Base units: Mass = gram, Volume = millilitre, Length = millimetre. Temperature is handled separately.
    private static readonly Unit[] Units =
    [
        // Mass
        new("kg", Dimension.Mass, MeasurementSystem.Metric, 1000, ["kg", "kilogram", "kilograms", "kilo", "kilos"]),
        new("g", Dimension.Mass, MeasurementSystem.Metric, 1, ["g", "gram", "grams", "gr"]),
        new("mg", Dimension.Mass, MeasurementSystem.Metric, 0.001, ["mg", "milligram", "milligrams"]),
        new("lb", Dimension.Mass, MeasurementSystem.Imperial, 453.59237, ["lb", "lbs", "pound", "pounds"]),
        new("oz", Dimension.Mass, MeasurementSystem.Imperial, 28.349523, ["oz", "ounce", "ounces"]),

        // Volume
        new("l", Dimension.Volume, MeasurementSystem.Metric, 1000, ["l", "litre", "litres", "liter", "liters"]),
        new("ml", Dimension.Volume, MeasurementSystem.Metric, 1, ["ml", "millilitre", "millilitres", "milliliter", "milliliters", "cc"]),
        new("gallon", Dimension.Volume, MeasurementSystem.Imperial, 3785.411784, ["gallon", "gallons", "gal"]),
        new("quart", Dimension.Volume, MeasurementSystem.Imperial, 946.352946, ["quart", "quarts", "qt"]),
        new("pint", Dimension.Volume, MeasurementSystem.Imperial, 473.176473, ["pint", "pints", "pt"]),
        new("cup", Dimension.Volume, MeasurementSystem.Imperial, 236.588236, ["cup", "cups"]),
        new("floz", Dimension.Volume, MeasurementSystem.Imperial, 29.573529, ["fl oz", "fluid ounce", "fluid ounces", "floz"]),
        // tbsp/tsp are used in both systems; treated as neutral so they don't skew detection.
        new("tbsp", Dimension.Volume, null, 14.7867648, ["tbsp", "tbsp.", "tablespoon", "tablespoons", "tbs", "tbl"]),
        new("tsp", Dimension.Volume, null, 4.9289216, ["tsp", "tsp.", "teaspoon", "teaspoons"]),

        // Length
        new("cm", Dimension.Length, MeasurementSystem.Metric, 10, ["cm", "centimetre", "centimetres", "centimeter", "centimeters"]),
        new("mm", Dimension.Length, MeasurementSystem.Metric, 1, ["mm", "millimetre", "millimetres", "millimeter", "millimeters"]),
        new("inch", Dimension.Length, MeasurementSystem.Imperial, 25.4, ["inch", "inches", "\"", ShortInch]),

        // Temperature (ToBase unused; handled by formulas)
        new("C", Dimension.Temperature, MeasurementSystem.Metric, 0, ["°c", "℃", "celsius", "centigrade", "degrees c", "deg c", "c"]),
        new("F", Dimension.Temperature, MeasurementSystem.Imperial, 0, ["°f", "℉", "fahrenheit", "degrees f", "deg f", "f"]),
    ];

    // "in" is an inch only straight after a number ("8in", "9-in"); spaced out it is the word "in"
    // ("5 in each"), so the token pattern handles it separately from the other aliases.
    private const string ShortInch = "in";

    private static readonly Dictionary<string, Unit> AliasToUnit = BuildAliasLookup();
    private static readonly Regex Token = new(BuildTokenPattern(string.Empty), RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DualToken = BuildDualTokenRegex();
    private static readonly Regex SlashDualToken = BuildSlashDualTokenRegex();

    // Range separators between two quantities: "1-2", "1–2", "1 to 2".
    private const string RangeSeparator = @"\s*(?:-|–|—|to)\s*";
    private const string FractionChars = "¼½¾⅓⅔⅛⅜⅝⅞⅕⅖";

    // Units kept as written are shown with their standard abbreviation ("lbs." -> "lb", "grams" -> "g");
    // other units (cups, pints, inches) keep the recipe's own word.
    private static readonly HashSet<string> AbbreviatedKeys = ["kg", "g", "mg", "lb", "oz", "l", "ml", "cm", "mm"];

    // Ingredients named here are liquids that cooks measure in *fluid* ounces even when the recipe
    // just writes "ounces" (spirits, wine, and other pourable liquids). Without this, "2 ounces
    // tequila" is converted as weight (~55 g) instead of volume (~60 ml). Kept deliberately
    // conservative: words commonly sold by weight (cheese, chocolate, "cream cheese", "tomato
    // sauce") are omitted, so only clear liquids trigger the fluid reading. English-only, matching
    // the rest of the alias set.
    private static readonly Regex LiquidContext = new(
        @"\b(tequila|vodka|rum|gin|whisk(?:e)?y|bourbon|brandy|cognac|scotch|mezcal|sake|sherry|vermouth|" +
        @"liqueur|schnapps|aperol|campari|amaretto|cointreau|triple\s+sec|absinthe|wine|prosecco|champagne|" +
        @"beer|ale|lager|cider|water|milk|buttermilk|cream|juice|broth|stock|oil|vinegar|syrup|soda|cola|" +
        @"tonic|seltzer|espresso|coffee|brine|liquor)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // "cream" is a liquid, but "cream cheese" and "sour cream" are sold and measured by weight, so
    // an ounce there really is an ounce. Exclude those spellings from the liquid reading.
    private static readonly Regex SolidCreamContext = new(
        @"\b(cream\s+cheese|sour\s+cream|creamed|creamy)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static bool IsLiquidContext(string text) =>
        LiquidContext.IsMatch(text) && !SolidCreamContext.IsMatch(text);

    private static readonly Dictionary<char, double> UnicodeFractions = new()
    {
        ['¼'] = 0.25, ['½'] = 0.5, ['¾'] = 0.75, ['⅓'] = 1.0 / 3, ['⅔'] = 2.0 / 3,
        ['⅛'] = 0.125, ['⅜'] = 0.375, ['⅝'] = 0.625, ['⅞'] = 0.875, ['⅕'] = 0.2, ['⅖'] = 0.4,
    };

    /// <summary>Detects the dominant measurement system across the supplied text segments.</summary>
    public static MeasurementSystem Detect(IEnumerable<string> segments)
    {
        int metric = 0, imperial = 0;
        foreach (var raw in segments)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var segment = Normalize(raw);
            foreach (Match match in Token.Matches(segment))
            {
                if (!TryResolveUnit(match.Groups["unit"].Value, out var unit)) continue;
                if (IsAmbiguousBareTemperature(match.Groups["unit"].Value, match.Groups["qty"].Value)) continue;
                if (unit.System == MeasurementSystem.Metric) metric++;
                else if (unit.System == MeasurementSystem.Imperial) imperial++;
            }
        }
        return imperial > metric ? MeasurementSystem.Imperial : MeasurementSystem.Metric;
    }

    public static IReadOnlyList<string> ConvertLines(IEnumerable<string> lines, MeasurementSystem from, MeasurementSystem to) =>
        lines.Select(line => ConvertText(line, from, to)).ToArray();

    /// <summary>
    /// Rewrites every measurement token found in <paramref name="text"/> into the target system.
    /// The <paramref name="from"/> hint is advisory only: conversion is idempotent (tokens already in
    /// <paramref name="to"/> are left untouched), so mixed-unit recipes are fully normalised to the
    /// display system even when it matches the detected source system. Teaspoons and tablespoons are
    /// used in both systems and are never rewritten. A measurement followed by its equivalent in
    /// brackets ("1½ lbs. (700 grams)") collapses to a single value in the target system.
    /// </summary>
    public static string ConvertText(string text, MeasurementSystem from, MeasurementSystem to)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        text = Normalize(text);
        var liquidContext = IsLiquidContext(text);
        text = DualToken.Replace(text, match => ResolveDual(match, to, liquidContext));
        text = SlashDualToken.Replace(text, match => ResolveSlashDual(match, to, liquidContext));
        return Token.Replace(text, match =>
        {
            if (!TryReadToken(match, string.Empty, liquidContext, out var unit, out var quantityText))
                return match.Value;

            // Already in the target system for this dimension, or a spoon measure valid in both.
            if (unit.System == to || unit.System is null)
                return match.Value;

            return ConvertQuantity(quantityText, unit, to) ?? match.Value;
        });
    }

    // Keeps exactly one of "primary (equivalent)": whichever is already in the target system (the
    // primary first), else a spoon measure, else the primary converted once. Never both.
    private static string ResolveDual(Match match, MeasurementSystem to, bool liquidContext)
    {
        if (!TryReadToken(match, "a", liquidContext, out var primary, out var primaryQty)
            || !TryReadToken(match, "b", liquidContext, out var equivalent, out var equivalentQty))
            return match.Value;
        if ((primary.Dimension == Dimension.Temperature) != (equivalent.Dimension == Dimension.Temperature))
            return match.Value; // not an equivalent of the same measurement

        var primaryUnit = match.Groups["aunit"].Value;
        var equivalentUnit = match.Groups["bunit"].Value;
        if (primary.System == to) return AsWritten(primaryQty, primaryUnit, primary);
        if (equivalent.System == to) return AsWritten(equivalentQty, equivalentUnit, equivalent);
        if (primary.System is null) return AsWritten(primaryQty, primaryUnit, primary);
        if (equivalent.System is null) return AsWritten(equivalentQty, equivalentUnit, equivalent);
        return ConvertQuantity(primaryQty, primary, to) ?? match.Value;
    }

    // "375°F/190°C", "200g/7oz" or "1 tsp/5ml": the same measurement given twice, joined by a slash.
    // Only a pair from two different systems counts, or a spoon (valid in both) with a volume; two
    // spoons ("1 tbsp/1 tsp") are two measures and are left alone. It keeps one value the way the
    // bracketed form does, so converting each half on its own cannot print it twice. A slash after the
    // pair ("…/Gas Mark 5") is spaced out, as it now joins just two items.
    private static string ResolveSlashDual(Match match, MeasurementSystem to, bool liquidContext)
    {
        if (!TryReadToken(match, "a", liquidContext, out var first, out _)
            || !TryReadToken(match, "b", liquidContext, out var second, out _)
            || !IsSlashEquivalent(first, second))
            return match.Value;

        var resolved = ResolveDual(match, to, liquidContext);
        return match.Groups["tail"].Success ? resolved + " / " : resolved;
    }

    private static bool IsSlashEquivalent(Unit first, Unit second)
    {
        if ((first.Dimension == Dimension.Temperature) != (second.Dimension == Dimension.Temperature))
            return false;
        if (first.System is null && second.System is null)
            return false;
        if (first.System is null || second.System is null)
            return first.Dimension == Dimension.Volume && second.Dimension == Dimension.Volume;
        return first.System != second.System;
    }

    private static string AsWritten(string quantityText, string unitText, Unit unit)
    {
        var quantity = quantityText.Trim();
        if (unit.Dimension == Dimension.Temperature)
            return $"{quantity}{(unit.System == MeasurementSystem.Metric ? "°C" : "°F")}";
        var written = unitText.Trim().TrimEnd('.');
        // The short "in" reads as the word "in" once spaced out, so it is shown as "inch".
        var display = AbbreviatedKeys.Contains(unit.Key) ? unit.Key
            : unit.Key == "inch" && written.Equals("in", StringComparison.OrdinalIgnoreCase) ? "inch"
            : written;
        return $"{quantity} {display}";
    }

    private static bool TryReadToken(Match match, string prefix, bool liquidContext, out Unit unit, out string quantityText)
    {
        var unitText = match.Groups[prefix + "unit"].Value;
        quantityText = match.Groups[prefix + "qty"].Value;
        if (!TryResolveUnit(unitText, out unit) || IsAmbiguousBareTemperature(unitText, quantityText))
            return false;

        // A weight-ounce in a line naming a pourable liquid is really a fluid ounce, so it
        // converts by volume (ml) rather than mass (g).
        if (unit.Dimension == Dimension.Mass && unit.Key == "oz" && liquidContext
            && TryResolveUnit("floz", out var fluid))
            unit = fluid;
        return true;
    }

    // Converts a single quantity or a range ("½ to 1") into the target system. Both ends of a range
    // share one unit, chosen from the first value, so "½ to 1 cup" reads "120–235 ml". Returns null
    // when any part fails to parse, so the caller leaves the original text untouched.
    private static string? ConvertQuantity(string quantityText, Unit source, MeasurementSystem to)
    {
        // "1-1/2" is a hyphenated mixed number, not a range from 1 down to ½.
        quantityText = RecipeScaling.NormalizeMixedNumbers(quantityText);
        var parts = Regex.Split(quantityText, RangeSeparator, RegexOptions.IgnoreCase)
            .Where(p => p.Trim().Length > 0).ToArray();
        if (parts.Length == 0) return null;

        var values = new List<double>();
        foreach (var part in parts)
        {
            if (!TryParseQuantity(part, out var value)) return null;
            values.Add(value);
        }

        if (source.Dimension == Dimension.Temperature)
        {
            var suffix = to == MeasurementSystem.Metric ? "°C" : "°F";
            return string.Join("–", values.Select(v => Trim(ConvertTemperature(v, source)))) + suffix;
        }

        var bases = values.Select(v => v * source.ToBase).ToArray();
        var target = SelectTarget(source.Dimension, to, bases[0]);
        return $"{string.Join("–", bases.Select(b => target.format(b / target.factor)))} {target.suffix}";
    }

    // Converts one temperature between scales, rounded to the nearest 5°.
    private static double ConvertTemperature(double value, Unit source)
    {
        double converted = source.System == MeasurementSystem.Metric
            ? value * 9 / 5 + 32   // C -> F
            : (value - 32) * 5 / 9; // F -> C
        return Math.Round(converted / 5, MidpointRounding.AwayFromZero) * 5;
    }

    private static (double factor, string suffix, Func<double, string> format) SelectTarget(Dimension dimension, MeasurementSystem to, double baseValue) =>
        (dimension, to) switch
        {
            (Dimension.Mass, MeasurementSystem.Metric) => baseValue >= 1000
                ? (1000, "kg", (Func<double, string>)(v => Trim(Math.Round(v, 2))))
                : (1, "g", v => Trim(RoundNice(v))),
            (Dimension.Mass, MeasurementSystem.Imperial) => baseValue >= 453.59237
                ? (453.59237, "lb", v => Trim(Math.Round(v, 2)))
                : (28.349523, "oz", Fraction),

            (Dimension.Volume, MeasurementSystem.Metric) => baseValue >= 1000
                ? (1000, "l", v => Trim(Math.Round(v, 2)))
                : (1, "ml", v => Trim(RoundNice(v))),
            (Dimension.Volume, MeasurementSystem.Imperial) => SelectImperialVolume(baseValue),

            (Dimension.Length, MeasurementSystem.Metric) => baseValue >= 10
                ? (10, "cm", v => Trim(Math.Round(v * 2, MidpointRounding.AwayFromZero) / 2))
                : (1, "mm", v => Trim(Math.Round(v))),
            (Dimension.Length, MeasurementSystem.Imperial) => (25.4, "inch", FractionEighths),

            _ => (1, string.Empty, v => Trim(v)),
        };

    private static (double factor, string suffix, Func<double, string> format) SelectImperialVolume(double ml) => ml switch
    {
        < 14 => (4.9289216, "tsp", Fraction),
        < 45 => (14.7867648, "tbsp", Fraction),
        < 946.352946 => (236.588236, "cup", Fraction),
        < 3785.411784 => (946.352946, "quart", v => Trim(Math.Round(v, 2))),
        _ => (3785.411784, "gallon", v => Trim(Math.Round(v, 2))),
    };

    // Rounds grams/millilitres to friendly values: nearest 5 above 50, otherwise nearest whole.
    private static double RoundNice(double v) => v >= 50
        ? Math.Round(v / 5, MidpointRounding.AwayFromZero) * 5
        : Math.Round(v);

    // Formats to the nearest quarter, using unicode fractions for readability (e.g. 1½, ¾).
    private static string Fraction(double value)
    {
        var quarters = Math.Round(value * 4, MidpointRounding.AwayFromZero);
        var whole = (int)(quarters / 4);
        var remainder = (int)(quarters - whole * 4);
        var frac = remainder switch { 1 => "¼", 2 => "½", 3 => "¾", _ => string.Empty };
        if (frac.Length == 0) return whole.ToString(CultureInfo.InvariantCulture);
        return whole > 0 ? $"{whole}{frac}" : frac;
    }

    // Formats to the nearest eighth (how recipes express inches, e.g. 1/8", 3/8"). A small positive
    // value never collapses to "0": it is clamped up to the smallest eighth so "3 mm" -> "⅛ inch".
    private static string FractionEighths(double value)
    {
        var eighths = (int)Math.Round(value * 8, MidpointRounding.AwayFromZero);
        if (eighths == 0 && value > 0) eighths = 1;
        var whole = eighths / 8;
        var remainder = eighths - whole * 8;
        var frac = remainder switch
        {
            1 => "⅛", 2 => "¼", 3 => "⅜", 4 => "½", 5 => "⅝", 6 => "¾", 7 => "⅞", _ => string.Empty,
        };
        if (frac.Length == 0) return whole.ToString(CultureInfo.InvariantCulture);
        return whole > 0 ? $"{whole}{frac}" : frac;
    }

    private static string Trim(double value)
    {
        var rounded = Math.Round(value, 2);
        return rounded.ToString("0.##", CultureInfo.InvariantCulture);
    }

    // Normalises the masculine-ordinal indicator (º, U+00BA) often used for degrees to the
    // real degree sign (°, U+00B0) so "200ºC" is recognised as a temperature.
    private static string Normalize(string text) => text.Replace('\u00BA', '\u00B0');

    private static bool TryParseQuantity(string text, out double value)
    {
        value = 0;
        text = text.Trim();
        if (text.Length == 0) return false;

        // Trailing unicode fraction, e.g. "1½"
        if (UnicodeFractions.TryGetValue(text[^1], out var trailingFrac))
        {
            var wholePart = text[..^1].Trim();
            if (wholePart.Length == 0) { value = trailingFrac; return true; }
            if (double.TryParse(wholePart, NumberStyles.Any, CultureInfo.InvariantCulture, out var w)) { value = w + trailingFrac; return true; }
            return false;
        }

        // Mixed number "1 1/2"
        var mixed = Regex.Match(text, @"^(\d+)\s+(\d+)\s*/\s*(\d+)$");
        if (mixed.Success)
        {
            value = double.Parse(mixed.Groups[1].Value, CultureInfo.InvariantCulture)
                + double.Parse(mixed.Groups[2].Value, CultureInfo.InvariantCulture) / double.Parse(mixed.Groups[3].Value, CultureInfo.InvariantCulture);
            return true;
        }

        // Simple fraction "3/4"
        var frac = Regex.Match(text, @"^(\d+)\s*/\s*(\d+)$");
        if (frac.Success)
        {
            value = double.Parse(frac.Groups[1].Value, CultureInfo.InvariantCulture) / double.Parse(frac.Groups[2].Value, CultureInfo.InvariantCulture);
            return true;
        }

        return double.TryParse(text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryResolveUnit(string alias, out Unit unit)
    {
        alias = alias.Trim().ToLowerInvariant().TrimEnd('.');
        return AliasToUnit.TryGetValue(alias, out unit!);
    }

    // "c" is the standard American abbreviation for "cup" as well as for Celsius, and "f" is
    // similarly weak on its own. Reading "2 c flour" as two degrees Celsius silently corrupted the
    // ingredient. Recipes do also write "Bake at 200 C" with no degree sign, so rather than dropping
    // the bare aliases entirely we accept them only at magnitudes that can only be an oven
    // temperature: no recipe calls for 180 cups, and none bakes at 2 degrees.
    private const double MinBareCelsius = 100;
    private const double MinBareFahrenheit = 200;

    private static bool IsAmbiguousBareTemperature(string rawAlias, string quantityText)
    {
        var alias = rawAlias.Trim().ToLowerInvariant().TrimEnd('.');
        if (alias is not ("c" or "f"))
            return false;

        // Take the first number of a range: "180-200 C" is still an oven temperature.
        var firstPart = Regex.Split(quantityText, RangeSeparator, RegexOptions.IgnoreCase)
            .FirstOrDefault(p => p.Trim().Length > 0) ?? quantityText;

        if (!TryParseQuantity(firstPart, out var value))
            return true;

        var threshold = alias == "c" ? MinBareCelsius : MinBareFahrenheit;
        return value < threshold;
    }

    private static Dictionary<string, Unit> BuildAliasLookup()
    {
        var map = new Dictionary<string, Unit>(StringComparer.OrdinalIgnoreCase);
        foreach (var unit in Units)
            foreach (var alias in unit.Aliases)
                map[alias.TrimEnd('.')] = unit;
        return map;
    }

    // A "quantity + unit" token whose groups are named with <paramref name="prefix"/>, so two tokens
    // can live in one pattern. A period straight after the unit ("lb.", "oz.") belongs to the
    // abbreviation when the line carries on in lower case or with a bracket or comma; before a capital
    // or at the end it is a full stop and is left alone.
    private static string BuildTokenPattern(string prefix)
    {
        // Longest aliases first so e.g. "cup" wins over "c", "tbsp" over "tsp".
        var aliases = Units.SelectMany(u => u.Aliases)
            .Where(a => a != ShortInch)
            .OrderByDescending(a => a.Length)
            .Select(Regex.Escape);
        var unitGroup = string.Join("|", aliases) + $@"|(?<=\d-?){ShortInch}";

        // "1½" / "1 ½" (a whole number before a unicode fraction) comes first so the whole number is
        // not dropped from the quantity.
        const string number = @"(?:\d+\s?[" + FractionChars + @"]|\d+\s+\d+\s*/\s*\d+|\d+\s*/\s*\d+|\d+(?:[.,]\d+)?|[" + FractionChars + "])";
        var qty = $@"(?<{prefix}qty>{number}(?:{RangeSeparator}{number})?)";
        // Optional space, or a hyphen for sizes like "9-inch"; require a non-letter boundary after the
        // unit so "c" won't match inside "clove".
        return $@"{qty}(?:\s*|-)(?<{prefix}unit>{unitGroup})(?![A-Za-z])(?:\.(?-i:(?=\s*[\p{{Ll}}(),;])))?";
    }

    // "1½ lbs. (700 grams)" / "350°F (175°C)" / "1 lb (about 450 g)": a measurement followed by its
    // equivalent in brackets.
    private static Regex BuildDualTokenRegex()
    {
        const string hedge = @"(?:(?:about|approx\.?|approximately|roughly|around|~)\s*)?";
        var pattern = $@"{BuildTokenPattern("a")}\s*\(\s*{hedge}{BuildTokenPattern("b")}\s*\)";
        return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    // "375°F/190°C/Gas Mark 5" / "200g/7oz": a measurement and its equivalent joined by a slash,
    // plus the slash that may follow the pair.
    private static Regex BuildSlashDualTokenRegex()
    {
        var pattern = $@"{BuildTokenPattern("a")}\s*/\s*{BuildTokenPattern("b")}(?<tail>\s*/\s*)?";
        return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }
}
