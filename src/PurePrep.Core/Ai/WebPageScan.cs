using System.Text.Json;

namespace PurePrep.Ai;

/// <summary>
/// What the in-app browser's injected script reports about a page: the raw text of every
/// <c>application/ld+json</c> block, the first schema.org/Recipe microdata item's name and image
/// (as written in the page), and the document title.
/// </summary>
public sealed record WebPageScan(
    IReadOnlyList<string> JsonLdBlocks,
    string? MicrodataName,
    string? MicrodataImage,
    string? Title)
{
    private const int MaxLayers = 3;

    /// <summary>Results longer than this (~2 MB of text) are not parsed at all.</summary>
    public const int MaxResultLength = 2_000_000;

    /// <summary>
    /// Decodes the string a WebView hands back for the script. Android returns the value JSON-encoded,
    /// and MAUI may or may not have stripped its quotes / unescaped it, so the result can be the object
    /// itself, a JSON string containing it (possibly twice), or a quote-stripped escaped string. Each
    /// layer is peeled until an object (or a bare array of ld+json blocks) appears. Anything else —
    /// null, "null", malformed text, undecodable strings — yields null; an oversized result yields an
    /// empty scan. Never throws.
    /// </summary>
    public static WebPageScan? Decode(string? result)
    {
        if (result?.Length > MaxResultLength)
            return new WebPageScan([], null, null, null);

        var text = result;
        for (var layer = 0; layer < MaxLayers && !string.IsNullOrWhiteSpace(text); layer++)
        {
            if (Parse(text) is not { } root)
                return null;

            switch (root.ValueKind)
            {
                case JsonValueKind.Object:
                    return new WebPageScan(
                        root.TryGetProperty("ld", out var ld) ? Strings(ld) : [],
                        StringOrNull(root, "mdName"),
                        StringOrNull(root, "mdImage"),
                        StringOrNull(root, "title"));
                case JsonValueKind.Array:
                    return new WebPageScan(Strings(root), null, null, null);
                case JsonValueKind.String:
                    text = JsonLdRecipeWalker.StringOrNull(root);
                    break;
                default:
                    return null;
            }
        }

        return null;
    }

    private static JsonElement? Parse(string text)
    {
        foreach (var candidate in new[] { text, $"\"{text}\"" })
        {
            try
            {
                using var doc = JsonDocument.Parse(candidate);
                return doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                // Try the quote-restored form next.
            }
        }

        return null;
    }

    private static List<string> Strings(JsonElement array) =>
        array.ValueKind != JsonValueKind.Array
            ? []
            : array.EnumerateArray()
                .Select(JsonLdRecipeWalker.StringOrNull)
                .OfType<string>()
                .ToList();

    private static string? StringOrNull(JsonElement obj, string property) =>
        obj.TryGetProperty(property, out var value) ? JsonLdRecipeWalker.StringOrNull(value) : null;
}
