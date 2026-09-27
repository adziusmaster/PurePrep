using System.Text.Json;
using HtmlAgilityPack;

namespace PurePrep.Ai;

/// <summary>
/// Finds the photo a recipe page publishes for itself: schema.org Recipe.image first, then og:image.
/// Never asks the model — an invented URL is worse than none. Only http(s) links are returned; the
/// client downloads the image itself.
/// </summary>
public static class PageImageLocator
{
    public static Uri? Find(string html, Uri pageUrl)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        var document = new HtmlDocument();
        document.LoadHtml(html);

        foreach (var script in document.DocumentNode.SelectNodes("//script[@type='application/ld+json']") ?? Enumerable.Empty<HtmlNode>())
        {
            try
            {
                using var json = JsonDocument.Parse(System.Net.WebUtility.HtmlDecode(script.InnerText));
                if (JsonLdRecipeWalker.FindRecipe(json.RootElement) is { } recipe && recipe.TryGetProperty("image", out var image)
                    && JsonLdRecipeWalker.Resolve(JsonLdRecipeWalker.FirstImage(image), pageUrl) is { } found)
                    return found;
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException)
            {
                // A malformed blob on one script never hides a good one elsewhere.
            }
        }

        var og = document.DocumentNode.SelectSingleNode("//meta[@property='og:image']")?.GetAttributeValue("content", null);
        return JsonLdRecipeWalker.Resolve(og, pageUrl);
    }
}
