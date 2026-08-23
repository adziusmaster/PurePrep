using System.Net;
using System.Text.Json;
using HtmlAgilityPack;
using PurePrep.Application;
using PurePrep.Domain;

namespace PurePrep.Infrastructure;

public sealed class RecipeParser(HttpClient httpClient) : IRecipeParser
{
    public async Task<ParsedRecipe> ParseAsync(Uri source, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(source, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var document = new HtmlDocument();
        document.LoadHtml(html);

        var recipe = ParseJsonLd(document, source) ?? ParseSemanticHtml(document, source);
        if (recipe is null || (recipe.Ingredients.Count == 0 && recipe.Steps.Count == 0))
            throw new InvalidOperationException("No recipe data was found on this page.");

        return recipe;
    }

    private static ParsedRecipe? ParseJsonLd(HtmlDocument document, Uri source)
    {
        foreach (var node in document.DocumentNode.SelectNodes("//script[@type='application/ld+json']") ?? Enumerable.Empty<HtmlNode>())
        {
            try
            {
                using var json = JsonDocument.Parse(WebUtility.HtmlDecode(node.InnerText));
                var recipe = FindRecipe(json.RootElement, source);
                if (recipe is not null)
                    return recipe;
            }
            catch (JsonException)
            {
                // Some publishers emit malformed JSON-LD; semantic HTML can still work.
            }
        }
        return null;
    }

    private static ParsedRecipe? FindRecipe(JsonElement element, Uri source)
    {
        if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray())
                if (FindRecipe(item, source) is { } recipe) return recipe;

        if (element.ValueKind != JsonValueKind.Object)
            return null;

        if (IsRecipeType(element))
        {
            var title = GetString(element, "name");
            var ingredients = GetStrings(element, "recipeIngredient");
            var steps = GetInstructions(element, "recipeInstructions");
            if (!string.IsNullOrWhiteSpace(title))
                return CreateRecipe(title, ingredients, steps, source);
        }

        if (element.TryGetProperty("@graph", out var graph))
            return FindRecipe(graph, source);

        return null;
    }

    private static bool IsRecipeType(JsonElement element)
    {
        if (!element.TryGetProperty("@type", out var type)) return false;
        return type.ValueKind == JsonValueKind.String
            ? string.Equals(type.GetString(), "Recipe", StringComparison.OrdinalIgnoreCase)
            : type.ValueKind == JsonValueKind.Array && type.EnumerateArray().Any(x => x.GetString()?.Equals("Recipe", StringComparison.OrdinalIgnoreCase) == true);
    }

    private static IReadOnlyList<string> GetStrings(JsonElement element, string property) =>
        !element.TryGetProperty(property, out var value) ? [] : value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => Clean(x!)).ToArray()
            : value.ValueKind == JsonValueKind.String ? [Clean(value.GetString()!)] : [];

    private static IReadOnlyList<RecipeStep> GetInstructions(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value)) return [];
        var steps = new List<string>();
        AddInstructions(value, steps);
        return steps.Where(x => x.Length > 0).Select((text, index) => new RecipeStep { Order = index + 1, Instruction = Clean(text) }).ToArray();
    }

    private static void AddInstructions(JsonElement value, List<string> steps)
    {
        if (value.ValueKind == JsonValueKind.String) { steps.Add(value.GetString()!); return; }
        if (value.ValueKind != JsonValueKind.Array)
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                if (value.TryGetProperty("text", out var text))
                    AddInstructions(text, steps);
                else if (value.TryGetProperty("itemListElement", out var items))
                    AddInstructions(items, steps);
            }
            return;
        }
        foreach (var item in value.EnumerateArray()) AddInstructions(item, steps);
    }

    private static ParsedRecipe? ParseSemanticHtml(HtmlDocument document, Uri source)
    {
        var title = FirstText(document, "//*[@itemprop='name']", "//h1", "//meta[@property='og:title']/@content");
        var ingredients = Texts(document, "//*[@itemprop='recipeIngredient']", "//*[contains(translate(@class,'INGREDIENT','ingredient'),'ingredient')]");
        var instructionNodes = document.DocumentNode.SelectNodes("//*[@itemprop='recipeInstructions']//*[@itemprop='text'] | //*[@itemprop='recipeInstructions']") ?? Enumerable.Empty<HtmlNode>();
        var steps = instructionNodes.Select(n => Clean(n.InnerText)).Where(x => x.Length > 0).Distinct().Select((text, index) => new RecipeStep { Order = index + 1, Instruction = text }).ToArray();
        return string.IsNullOrWhiteSpace(title) ? null : CreateRecipe(title, ingredients, steps, source);
    }

    private static string? FirstText(HtmlDocument document, params string[] selectors) => selectors.SelectMany(selector => document.DocumentNode.SelectNodes(selector) ?? Enumerable.Empty<HtmlNode>()).Select(n => Clean(n.GetAttributeValue("content", n.InnerText))).FirstOrDefault(x => x.Length > 0);
    private static IReadOnlyList<string> Texts(HtmlDocument document, params string[] selectors) => selectors.SelectMany(selector => document.DocumentNode.SelectNodes(selector) ?? Enumerable.Empty<HtmlNode>()).Select(n => Clean(n.InnerText)).Where(x => x.Length > 0).Distinct().ToArray();
    private static ParsedRecipe CreateRecipe(string title, IReadOnlyList<string> ingredients, IReadOnlyList<RecipeStep> steps, Uri source) => new() { Title = Clean(title), Ingredients = ingredients, Steps = steps, SourceUrl = source.ToString(), SourceSystem = Units.UnitConverter.Detect(ingredients.Concat(steps.Select(s => s.Instruction))) };
    private static string GetString(JsonElement element, string property) => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
    private static string Clean(string value) => WebUtility.HtmlDecode(HtmlEntity.DeEntitize(value)).Replace("\n", " ").Replace("\r", " ").Trim();
}
