namespace PurePrep.Domain;

/// <summary>
/// Decides whether a link on the clipboard is worth offering as an import: it must be a link, not
/// already in the library, and not the link we offered last time (so dismissing it sticks).
/// </summary>
public static class ClipboardSuggestion
{
    public static string? Evaluate(string? clipboardText, string? lastSuggestedUrl, IEnumerable<string?> savedSourceUrls)
    {
        var url = SharedText.ExtractUrl(clipboardText);
        if (url is null)
            return null;
        if (RecipeUrl.SameRecipe(url, lastSuggestedUrl))
            return null;
        if (savedSourceUrls.Any(saved => RecipeUrl.SameRecipe(url, saved)))
            return null;
        return url;
    }
}
