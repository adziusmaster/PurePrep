namespace PurePrep.Domain;

/// <summary>Scaling by people rather than multipliers: factor = chosen ÷ original servings.</summary>
public static class ServingsScale
{
    public static int? OriginalServings(ParsedRecipe recipe) =>
        recipe.Servings is > 0
            ? recipe.Servings
            : ServingsDetector.Detect(recipe.Title, recipe.Ingredients, recipe.Steps.Select(s => s.Instruction));

    public static double Factor(int? original, int? chosen) =>
        original is > 0 && chosen is > 0 ? (double)chosen.Value / original.Value : 1d;

    public static ParsedRecipe ForCooking(ParsedRecipe recipe) =>
        RecipeScaling.ScaleRecipe(recipe, Factor(OriginalServings(recipe), recipe.ChosenServings));
}
