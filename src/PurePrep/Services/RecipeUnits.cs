using PurePrep.Domain;
using PurePrep.Units;

namespace PurePrep.Services;

/// <summary>
/// Produces a display copy of a recipe with its ingredient and step units converted to the
/// user's chosen system (<see cref="UnitSettings"/>). Returns the original instance unchanged
/// when "as written" is selected or the target already matches the source system.
/// </summary>
public static class RecipeUnits
{
    public static ParsedRecipe ForDisplay(ParsedRecipe recipe)
    {
        var target = UnitSettings.Target;
        if (target is null || target == recipe.SourceSystem)
            return recipe;

        var ingredients = UnitConverter.ConvertLines(recipe.Ingredients, recipe.SourceSystem, target.Value);
        var steps = recipe.Steps
            .OrderBy(s => s.Order)
            .Select(s => new RecipeStep
            {
                Order = s.Order,
                Instruction = UnitConverter.ConvertText(s.Instruction, recipe.SourceSystem, target.Value)
            })
            .ToArray();

        return new ParsedRecipe
        {
            Id = recipe.Id,
            Title = recipe.Title,
            SourceUrl = recipe.SourceUrl,
            SourceSystem = recipe.SourceSystem,
            SavedAt = recipe.SavedAt,
            Ingredients = ingredients.ToArray(),
            Steps = steps
        };
    }
}
