namespace PurePrep.Domain;

/// <summary>Rules for combining two copies of the same recipe without losing the cook's data.</summary>
public static class RecipeMerge
{
    /// <summary>
    /// A general edit (notes, favourite, servings, translation, cooked) keeps the photo already stored for
    /// the recipe: the photo is attached asynchronously after an import, so the edited copy may predate it.
    /// Only the editor's explicit add/remove photo changes it, and that goes through its own path.
    /// </summary>
    public static ParsedRecipe KeepStoredPhoto(ParsedRecipe edited, ParsedRecipe? stored) =>
        stored is null || stored.Id != edited.Id ? edited : edited with { ImagePath = stored.ImagePath };

    /// <summary>
    /// A "Replace" re-import: the fresh parse replaces the recipe's text, but its identity and everything
    /// the cook added (notes, favourite, status, cook history, photo) carry over. The remembered servings
    /// only carry over when the yield is unchanged — otherwise they would scale against a different base.
    /// The old photo stays until the new import's photo overwrites the same file.
    /// </summary>
    public static ParsedRecipe Reimport(ParsedRecipe fresh, ParsedRecipe existing) =>
        fresh with
        {
            Id = existing.Id,
            SavedAt = existing.SavedAt,
            Notes = existing.Notes,
            IsFavourite = existing.IsFavourite,
            Status = existing.Status,
            CookedAt = existing.CookedAt,
            CookCount = existing.CookCount,
            ChosenServings = fresh.Servings == existing.Servings ? existing.ChosenServings : null,
            ImagePath = fresh.ImagePath ?? existing.ImagePath,
        };
}
