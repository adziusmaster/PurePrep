namespace PurePrep.Domain;

/// <summary>Canonical recipe-link comparison: ignores scheme, "www.", case, trailing slash, query, fragment.</summary>
public static class RecipeUrl
{
    public static string Normalize(Uri uri)
    {
        var host = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host;
        var path = uri.AbsolutePath.TrimEnd('/');
        return $"{host.ToLowerInvariant()}{path.ToLowerInvariant()}";
    }

    public static bool SameRecipe(string? a, string? b) =>
        Uri.TryCreate(a, UriKind.Absolute, out var ua)
        && Uri.TryCreate(b, UriKind.Absolute, out var ub)
        && Normalize(ua) == Normalize(ub);
}
