using System.Net;
using System.Text.Json;
using HtmlAgilityPack;

namespace PurePrep.Ai;

/// <summary>Recipe data a page published about itself, before the model sees it.</summary>
public sealed record StructuredRecipe(
    string Title,
    IReadOnlyList<string> Ingredients,
    IReadOnlyList<string> Steps,
    string? Yield);

/// <summary>
/// Reads schema.org Recipe data embedded in a page — JSON-LD first, then microdata.
///
/// Most recipe sites publish this, and it carries what flowed page text only implies: exact
/// ingredient boundaries, step boundaries, and the yield. It is given to the model alongside the
/// raw page rather than instead of it, so the model still has the surrounding context but no
/// longer has to guess where one ingredient ends and the next begins.
///
/// Pure by design: fetching belongs to <see cref="GuardedPageFetcher"/>, which enforces the SSRF
/// policy. This only ever sees HTML that has already been through it.
/// </summary>
public static class StructuredRecipeExtractor
{
    /// <summary>A blob thinner than this is likelier to be a stub than a real recipe.</summary>
    private const int MinIngredients = 2;
    private const int MinSteps = 1;

    public static StructuredRecipe? TryExtract(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        HtmlDocument document;
        try
        {
            document = new HtmlDocument();
            document.LoadHtml(html);
        }
        catch
        {
            return null;
        }

        return FromJsonLd(document) ?? FromMicrodata(document);
    }

    /// <summary>
    /// Whether the extracted data is complete enough to be worth showing the model as authoritative.
    /// Partial blobs (a title and one token ingredient) are common on index and category pages.
    /// </summary>
    public static bool IsUsable(StructuredRecipe? recipe) =>
        recipe is not null
        && !string.IsNullOrWhiteSpace(recipe.Title)
        && recipe.Ingredients.Count >= MinIngredients
        && recipe.Steps.Count >= MinSteps;

    // ---- JSON-LD --------------------------------------------------------------------------

    private static StructuredRecipe? FromJsonLd(HtmlDocument document)
    {
        var nodes = document.DocumentNode.SelectNodes("//script[@type='application/ld+json']")
                    ?? Enumerable.Empty<HtmlNode>();

        foreach (var node in nodes)
        {
            // A few publishers HTML-encode the whole blob, so try the decoded form too.
            foreach (var candidate in new[] { node.InnerText, WebUtility.HtmlDecode(node.InnerText) })
            {
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;
                try
                {
                    using var json = JsonDocument.Parse(candidate);
                    if (FindRecipe(json.RootElement) is { } recipe)
                        return recipe;
                }
                catch (JsonException)
                {
                    // Malformed JSON-LD is common. Fall through to the next candidate or node.
                }
            }
        }

        return null;
    }

    private static StructuredRecipe? FindRecipe(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                if (FindRecipe(item) is { } fromArray)
                    return fromArray;
            return null;
        }

        if (element.ValueKind != JsonValueKind.Object)
            return null;

        if (IsRecipeType(element))
        {
            var title = GetString(element, "name");
            if (!string.IsNullOrWhiteSpace(title))
                return new StructuredRecipe(
                    Clean(title),
                    GetStrings(element, "recipeIngredient"),
                    GetInstructions(element),
                    GetYield(element));
        }

        // Recipe plugins commonly nest the recipe inside an @graph array.
        if (element.TryGetProperty("@graph", out var graph))
            return FindRecipe(graph);

        return null;
    }

    private static bool IsRecipeType(JsonElement element)
    {
        if (!element.TryGetProperty("@type", out var type))
            return false;

        return type.ValueKind switch
        {
            JsonValueKind.String => Matches(type.GetString()),
            JsonValueKind.Array => type.EnumerateArray().Any(x => Matches(x.GetString())),
            _ => false,
        };

        static bool Matches(string? value) => string.Equals(value, "Recipe", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>recipeYield is a string on some sites, a number on others, and a list on a few.</summary>
    private static string? GetYield(JsonElement element)
    {
        if (!element.TryGetProperty("recipeYield", out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.String => Clean(value.GetString() ?? string.Empty) is { Length: > 0 } s ? s : null,
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.Array => value.EnumerateArray()
                .Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : x.ToString())
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) is { } first ? Clean(first) : null,
            _ => null,
        };
    }

    private static IReadOnlyList<string> GetStrings(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
            return [];

        return value.ValueKind switch
        {
            JsonValueKind.Array => value.EnumerateArray()
                .Select(x => x.GetString())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => Clean(x!))
                .Where(x => x.Length > 0)
                .ToArray(),
            JsonValueKind.String => Clean(value.GetString()!) is { Length: > 0 } s ? [s] : [],
            _ => [],
        };
    }

    private static IReadOnlyList<string> GetInstructions(JsonElement element)
    {
        if (!element.TryGetProperty("recipeInstructions", out var value))
            return [];

        var steps = new List<string>();
        Collect(value, steps);
        return steps.Select(Clean).Where(x => x.Length > 0).ToArray();

        static void Collect(JsonElement value, List<string> steps)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.String:
                    steps.Add(value.GetString()!);
                    break;
                case JsonValueKind.Array:
                    foreach (var item in value.EnumerateArray())
                        Collect(item, steps);
                    break;
                case JsonValueKind.Object:
                    // HowToStep carries "text"; HowToSection nests further steps in itemListElement.
                    if (value.TryGetProperty("text", out var text))
                        Collect(text, steps);
                    else if (value.TryGetProperty("itemListElement", out var items))
                        Collect(items, steps);
                    break;
            }
        }
    }

    // ---- Microdata fallback ---------------------------------------------------------------

    private static StructuredRecipe? FromMicrodata(HtmlDocument document)
    {
        var title = FirstText(document, "//*[@itemprop='name']", "//meta[@property='og:title']/@content", "//h1");
        if (string.IsNullOrWhiteSpace(title))
            return null;

        var ingredients = Texts(document, "//*[@itemprop='recipeIngredient']", "//*[@itemprop='ingredients']");
        var steps = Texts(document,
            "//*[@itemprop='recipeInstructions']//*[@itemprop='text']",
            "//*[@itemprop='recipeInstructions']");
        var yield = FirstText(document, "//*[@itemprop='recipeYield']");

        if (ingredients.Count == 0 && steps.Count == 0)
            return null;

        return new StructuredRecipe(Clean(title), ingredients, steps, yield);
    }

    private static string? FirstText(HtmlDocument document, params string[] selectors) =>
        selectors
            .SelectMany(s => document.DocumentNode.SelectNodes(s) ?? Enumerable.Empty<HtmlNode>())
            .Select(n => Clean(n.GetAttributeValue("content", n.InnerText)))
            .FirstOrDefault(x => x.Length > 0);

    private static IReadOnlyList<string> Texts(HtmlDocument document, params string[] selectors) =>
        selectors
            .SelectMany(s => document.DocumentNode.SelectNodes(s) ?? Enumerable.Empty<HtmlNode>())
            .Select(n => Clean(n.InnerText))
            .Where(x => x.Length > 0)
            .Distinct()
            .ToArray();

    private static string GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static string Clean(string value) =>
        WebUtility.HtmlDecode(HtmlEntity.DeEntitize(value) ?? value)
            .Replace('\n', ' ')
            .Replace('\r', ' ')
            .Replace('\t', ' ')
            .Trim();
}
