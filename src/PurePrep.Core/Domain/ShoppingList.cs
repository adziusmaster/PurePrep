using System.Globalization;
using System.Text.RegularExpressions;

namespace PurePrep.Domain;

/// <summary>One line on the shopping list, and the recipe it was added for.</summary>
public sealed record ShoppingListItem(string Text, string? Source, bool IsChecked = false);

/// <summary>
/// Builds a shopping list from recipe ingredient lines.
///
/// The merging is the point. Adding two recipes that both want flour should produce one line with
/// the total, not two lines to add up in the shop. Quantities are only combined when the unit
/// matches — 200 g and 1 cup of flour cannot be summed without guessing a density.
/// </summary>
public static partial class ShoppingList
{
    // Leading quantity (decimal, fraction, mixed or unicode), then an optional unit word.
    [GeneratedRegex(
        @"^\s*(?<qty>\d+\s+\d+/\d+|\d+/\d+|\d+(?:[.,]\d+)?|[¼½¾⅓⅔⅕⅖⅗⅘⅛⅜⅝⅞])\s*(?<unit>[a-zA-Z]+\.?)?\s*(?<name>.*)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex Quantified();

    private static readonly Dictionary<char, double> UnicodeFractions = new()
    {
        ['¼'] = 0.25, ['½'] = 0.5, ['¾'] = 0.75, ['⅓'] = 1d / 3, ['⅔'] = 2d / 3,
        ['⅕'] = 0.2, ['⅖'] = 0.4, ['⅗'] = 0.6, ['⅘'] = 0.8,
        ['⅛'] = 0.125, ['⅜'] = 0.375, ['⅝'] = 0.625, ['⅞'] = 0.875,
    };

    public static IReadOnlyList<ShoppingListItem> Add(
        IReadOnlyList<ShoppingListItem> existing, IEnumerable<string> lines, string? source)
    {
        var result = existing.ToList();

        foreach (var raw in lines)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var line = raw.Trim();
            var parsed = Parse(line);
            var index = result.FindIndex(item => Matches(Parse(item.Text), parsed));

            if (index < 0)
            {
                result.Add(new ShoppingListItem(line, source));
                continue;
            }

            var existingItem = result[index];
            var existingParsed = Parse(existingItem.Text);

            // Unquantified duplicates ("salt to taste") just collapse to the one entry.
            var text = existingParsed.Quantity is { } a && parsed.Quantity is { } b
                ? Compose(a + b, parsed.Unit, parsed.Name)
                : existingItem.Text;

            // Reset the tick: the amount needed has gone up, so a previously satisfied line is
            // no longer satisfied and must not be walked past in the shop.
            result[index] = existingItem with { Text = text, IsChecked = false };
        }

        return result;
    }

    public static IReadOnlyList<ShoppingListItem> RemoveChecked(IReadOnlyList<ShoppingListItem> items) =>
        items.Where(i => !i.IsChecked).ToArray();

    private static bool Matches(ParsedLine a, ParsedLine b) =>
        string.Equals(a.Name, b.Name, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.Unit ?? string.Empty, b.Unit ?? string.Empty, StringComparison.OrdinalIgnoreCase);

    private static string Compose(double quantity, string? unit, string name)
    {
        var rounded = Math.Round(quantity, 2, MidpointRounding.AwayFromZero);
        var number = rounded.ToString("0.##", CultureInfo.InvariantCulture);
        return string.IsNullOrEmpty(unit) ? $"{number} {name}".Trim() : $"{number} {unit} {name}".Trim();
    }

    private static ParsedLine Parse(string line)
    {
        var match = Quantified().Match(line);
        if (!match.Success || !TryParseQuantity(match.Groups["qty"].Value, out var quantity))
            return new ParsedLine(null, null, Normalize(line));

        var unit = match.Groups["unit"].Success ? match.Groups["unit"].Value.TrimEnd('.') : null;
        var name = match.Groups["name"].Value;

        // A word after the number is only a unit if something remains to name the ingredient;
        // in "2 eggs" the word IS the ingredient.
        if (string.IsNullOrWhiteSpace(name))
        {
            name = unit ?? string.Empty;
            unit = null;
        }

        return new ParsedLine(quantity, unit, Normalize(name));
    }

    private static string Normalize(string value) => value.Trim().TrimEnd('.', ',').Trim();

    private static bool TryParseQuantity(string text, out double value)
    {
        value = 0;
        text = text.Trim();
        if (text.Length == 0)
            return false;

        if (text.Length == 1 && UnicodeFractions.TryGetValue(text[0], out var fraction))
        {
            value = fraction;
            return true;
        }

        var space = text.IndexOf(' ');
        if (space > 0 && text.Contains('/')
            && double.TryParse(text[..space], NumberStyles.Any, CultureInfo.InvariantCulture, out var whole)
            && TryParseFraction(text[(space + 1)..], out var part))
        {
            value = whole + part;
            return true;
        }

        if (text.Contains('/'))
            return TryParseFraction(text, out value);

        return double.TryParse(text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseFraction(string text, out double value)
    {
        value = 0;
        var parts = text.Split('/');
        return parts.Length == 2
            && double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var numerator)
            && double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var denominator)
            && denominator != 0
            && TrySet(numerator / denominator, out value);

        static bool TrySet(double computed, out double target)
        {
            target = computed;
            return true;
        }
    }

    private readonly record struct ParsedLine(double? Quantity, string? Unit, string Name);
}
