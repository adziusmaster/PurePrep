using System.Text;

namespace PurePrep.Ai;

/// <summary>
/// Composes what the model reads for one import: the page's own structured recipe data followed by
/// the raw page text.
///
/// Both are included on purpose. The structured block gives unambiguous ingredient and step
/// boundaries; the page gives the surrounding context — notes, tips, headings and asides that
/// schema.org data routinely drops, and which the model needs to write good steps.
///
/// The ordering encodes the budget rule: when the input has to be capped, the raw page is what
/// gets cut, never the structured block. Previously the whole page was sent alone and truncated
/// blindly, so on a long blog post the recipe itself could fall outside the window entirely.
/// </summary>
public static class RecipeExtractionInput
{
    private const string StructuredHeader = "=== STRUCTURED RECIPE DATA (published by the page; treat as authoritative) ===";
    private const string PageHeader = "=== PAGE TEXT (context only; may contain unrelated content) ===";

    public static string Build(StructuredRecipe? structured, string pageText, int maxChars)
    {
        if (maxChars <= 0)
            return string.Empty;

        pageText ??= string.Empty;

        if (!StructuredRecipeExtractor.IsUsable(structured))
            return Truncate(pageText, maxChars);

        var block = FormatStructured(structured!);

        // The structured block is the reliable half, so it is laid down first and in full. Only
        // whatever budget remains is spent on page context.
        if (block.Length >= maxChars)
            return Truncate(block, maxChars);

        var builder = new StringBuilder(maxChars);
        builder.Append(block);

        var remaining = maxChars - block.Length;
        var pageSection = $"\n\n{PageHeader}\n{pageText}";
        if (remaining > PageHeader.Length + 4 && pageText.Length > 0)
            builder.Append(Truncate(pageSection, remaining));

        return builder.ToString();
    }

    private static string FormatStructured(StructuredRecipe recipe)
    {
        var builder = new StringBuilder();
        builder.Append(StructuredHeader).Append('\n');
        builder.Append("Title: ").Append(recipe.Title).Append('\n');

        if (!string.IsNullOrWhiteSpace(recipe.Yield))
            builder.Append("Yield: ").Append(recipe.Yield).Append('\n');

        builder.Append("Ingredients:\n");
        foreach (var ingredient in recipe.Ingredients)
            builder.Append("- ").Append(ingredient).Append('\n');

        builder.Append("Steps:\n");
        for (var i = 0; i < recipe.Steps.Count; i++)
            builder.Append(i + 1).Append(". ").Append(recipe.Steps[i]).Append('\n');

        return builder.ToString();
    }

    private static string Truncate(string value, int maxChars) =>
        value.Length <= maxChars ? value : value[..maxChars];
}
