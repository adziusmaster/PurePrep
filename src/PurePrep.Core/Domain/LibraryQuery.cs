namespace PurePrep.Domain;

public enum LibraryFilter { All, WantToCook, Cooked, Favourites }

public enum LibrarySort { RecentlyAdded, RecentlyCooked, Alphabetical }

/// <summary>What the library shows: search → filter → sort, with favourites always pinned on top.</summary>
public static class LibraryQuery
{
    public static IReadOnlyList<ParsedRecipe> Apply(
        IEnumerable<ParsedRecipe> recipes, string? search, LibraryFilter filter, LibrarySort sort)
    {
        var query = search?.Trim();
        var view = recipes;
        if (!string.IsNullOrEmpty(query))
            view = view.Where(r => r.Title.Contains(query, StringComparison.OrdinalIgnoreCase));

        view = filter switch
        {
            LibraryFilter.WantToCook => view.Where(r => r.Status == RecipeStatus.WantToCook),
            LibraryFilter.Cooked => view.Where(r => r.Status == RecipeStatus.Cooked),
            LibraryFilter.Favourites => view.Where(r => r.IsFavourite),
            _ => view,
        };

        var pinned = view.OrderByDescending(r => r.IsFavourite);
        var sorted = sort switch
        {
            LibrarySort.RecentlyCooked => pinned.ThenByDescending(r => r.CookedAt ?? DateTimeOffset.MinValue).ThenByDescending(r => r.SavedAt),
            LibrarySort.Alphabetical => pinned.ThenBy(r => r.Title, StringComparer.CurrentCultureIgnoreCase),
            _ => pinned.ThenByDescending(r => r.SavedAt),
        };
        return sorted.ToArray();
    }
}
