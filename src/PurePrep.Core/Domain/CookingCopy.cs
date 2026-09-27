using PurePrep.Units;

namespace PurePrep.Domain;

/// <summary>
/// Builds the copy of a saved recipe that Focus Mode cooks from, the same way from every entry point
/// (detail screen, library card, running-timers bar): the displayed translation, then the cook's unit
/// preference, then the remembered servings.
/// </summary>
public static class CookingCopy
{
    /// <summary>
    /// Converts ingredient and step units to <paramref name="target"/>; <c>null</c> ("as written")
    /// returns the recipe unchanged. A same-system target still runs, which cleans dual-unit brackets.
    /// </summary>
    public static ParsedRecipe ConvertUnits(ParsedRecipe recipe, MeasurementSystem? target)
    {
        if (target is null)
            return recipe;

        var ingredients = UnitConverter.ConvertLines(recipe.Ingredients, recipe.SourceSystem, target.Value);
        var steps = recipe.Steps
            .OrderBy(s => s.Order)
            .Select(s => s with { Instruction = UnitConverter.ConvertText(s.Instruction, recipe.SourceSystem, target.Value) })
            .ToArray();

        return recipe with { Ingredients = ingredients.ToArray(), Steps = steps };
    }

    /// <summary>
    /// The recipe as it should be cooked. With a known yield it is scaled by people (the saved
    /// <see cref="ParsedRecipe.ChosenServings"/>); the yield is resolved against the saved recipe's own
    /// text, because re-detecting it from translated, unit-converted text can miss.
    /// </summary>
    /// <param name="saved">The persisted recipe (original text plus translation cache).</param>
    /// <param name="unitTarget">The cook's unit preference, or null for "as written".</param>
    /// <param name="fallbackFactor">Multiplier used only when the yield is unknown (the detail screen's ½×/2× choice).</param>
    public static ParsedRecipe For(ParsedRecipe saved, MeasurementSystem? unitTarget, double fallbackFactor = 1d)
    {
        var display = ConvertUnits(saved.Displayed(), unitTarget);
        var original = ServingsScale.OriginalServings(saved);
        return original is null
            ? RecipeScaling.ScaleRecipe(display, fallbackFactor)
            : ServingsScale.ForCooking(display with { Servings = original, ChosenServings = saved.ChosenServings ?? original });
    }

    /// <summary>
    /// The language the cook copy's text is in, for reading aloud and voice commands: the displayed
    /// translation, else the original language; null when neither is known (the caller falls back to the UI language).
    /// </summary>
    public static string? SpokenLanguage(ParsedRecipe saved) =>
        new[] { saved.DisplayLanguage, saved.OriginalLanguage }.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
