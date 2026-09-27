using System.Text.Json;

namespace PurePrep.Ai;

/// <summary>
/// Shared JSON-LD walking rules for locating a schema.org Recipe node (through arrays and
/// <c>@graph</c> nesting, with <c>@type</c> as either a string or an array) and for resolving its
/// image references. <see cref="PageImageLocator"/> and <see cref="WebRecipeDetector"/> both
/// build on this so the walking/guarding rules live in exactly one place.
/// </summary>
internal static class JsonLdRecipeWalker
{
    public static JsonElement? FindRecipe(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    if (FindRecipe(item) is { } found) return found;
                return null;
            case JsonValueKind.Object:
                if (IsRecipe(element)) return element;
                if (element.TryGetProperty("@graph", out var graph)) return FindRecipe(graph);
                return null;
            default:
                return null;
        }
    }

    private static bool IsRecipe(JsonElement element) =>
        element.TryGetProperty("@type", out var type) &&
        (StringOrNull(type) == "Recipe" ||
         type.ValueKind == JsonValueKind.Array && type.EnumerateArray().Any(t => StringOrNull(t) == "Recipe"));

    public static string? FirstImage(JsonElement image) => image.ValueKind switch
    {
        JsonValueKind.String => StringOrNull(image),
        JsonValueKind.Array => image.EnumerateArray().Select(FirstImage).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)),
        JsonValueKind.Object when image.TryGetProperty("url", out var url) => StringOrNull(url),
        _ => null,
    };

    /// <summary>
    /// The element's string value, or null when it isn't a string or can't be decoded. JSON like
    /// <c>"\ud800x"</c> (an unpaired surrogate escape) parses fine, but <see cref="JsonElement.GetString"/>
    /// throws on it — page content must never be able to crash the reader.
    /// </summary>
    public static string? StringOrNull(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.String)
            return null;
        try
        {
            return element.GetString();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public static Uri? Resolve(string? candidate, Uri pageUrl)
    {
        if (string.IsNullOrWhiteSpace(candidate) || !Uri.TryCreate(pageUrl, candidate.Trim(), out var uri))
            return null;
        return uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp ? uri : null;
    }
}
