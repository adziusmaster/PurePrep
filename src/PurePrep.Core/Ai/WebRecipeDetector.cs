using System.Text.Json;

namespace PurePrep.Ai;

/// <summary>The bits of a web page's Recipe (JSON-LD or microdata) worth surfacing before importing it.</summary>
public sealed record WebRecipeInfo(string Title, Uri? ImageUrl);

/// <summary>
/// Detects whether a page the user is browsing is a recipe, from the raw text of its
/// <c>&lt;script type="application/ld+json"&gt;</c> blocks alone — no fetch, no model call. Walks
/// each block exactly like <see cref="PageImageLocator"/> (shared in <see cref="JsonLdRecipeWalker"/>):
/// object / array / <c>@graph</c>, <c>@type</c> as a string or an array containing "Recipe". A
/// Recipe node without a usable <c>name</c> is not treated as a recipe.
/// </summary>
public static class WebRecipeDetector
{
    /// <param name="jsonLdBlocks">Raw text of every <c>&lt;script type="application/ld+json"&gt;</c> on the page.</param>
    /// <param name="pageUrl">The page the blocks came from, used to resolve relative image URLs.</param>
    public static WebRecipeInfo? Detect(IEnumerable<string> jsonLdBlocks, Uri pageUrl)
    {
        foreach (var block in jsonLdBlocks)
        {
            if (string.IsNullOrWhiteSpace(block))
                continue;

            try
            {
                using var json = JsonDocument.Parse(block);
                if (JsonLdRecipeWalker.FindRecipe(json.RootElement) is not { } recipe)
                    continue;
                if (!TryGetTitle(recipe, out var title))
                    continue;

                var image = recipe.TryGetProperty("image", out var imageElement)
                    ? JsonLdRecipeWalker.Resolve(JsonLdRecipeWalker.FirstImage(imageElement), pageUrl)
                    : null;
                return new WebRecipeInfo(title, image);
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException)
            {
                // A malformed blob on one script never hides a good one elsewhere.
            }
        }

        return null;
    }

    /// <summary>
    /// Like <see cref="Detect(IEnumerable{string}, Uri)"/>, falling back to schema.org/Recipe
    /// microdata when no JSON-LD Recipe is found. JSON-LD wins when both exist. The same rules apply:
    /// a blank name is not a recipe; the image resolves against <paramref name="pageUrl"/> and must be
    /// http(s).
    /// </summary>
    /// <param name="microdataName">Text/content of the Recipe item's first <c>itemprop="name"</c>.</param>
    /// <param name="microdataImage">src/content/href of the Recipe item's first <c>itemprop="image"</c>.</param>
    public static WebRecipeInfo? Detect(IEnumerable<string> jsonLdBlocks, string? microdataName, string? microdataImage, Uri pageUrl)
    {
        if (Detect(jsonLdBlocks, pageUrl) is { } fromJsonLd)
            return fromJsonLd;

        var title = microdataName?.Trim();
        return string.IsNullOrEmpty(title)
            ? null
            : new WebRecipeInfo(title, JsonLdRecipeWalker.Resolve(microdataImage, pageUrl));
    }

    private static bool TryGetTitle(JsonElement recipe, out string title)
    {
        title = "";
        if (!recipe.TryGetProperty("name", out var name))
            return false;

        var trimmed = JsonLdRecipeWalker.StringOrNull(name)?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return false;

        title = trimmed;
        return true;
    }
}
