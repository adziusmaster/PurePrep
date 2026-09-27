using PurePrep.Domain;

namespace PurePrep.Services;

/// <summary>
/// Produces a display copy of a recipe with its ingredient and step units converted to the user's chosen
/// system (<see cref="UnitSettings"/>). The conversion itself lives in <see cref="CookingCopy.ConvertUnits"/>.
/// </summary>
public static class RecipeUnits
{
    public static ParsedRecipe ForDisplay(ParsedRecipe recipe) => CookingCopy.ConvertUnits(recipe, UnitSettings.Target);
}
