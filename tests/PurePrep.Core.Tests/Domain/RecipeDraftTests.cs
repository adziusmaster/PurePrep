using FluentAssertions;
using PurePrep.Domain;

namespace PurePrep.Core.Tests.Domain;

public sealed class RecipeDraftTests
{
    private static ParsedRecipe Recipe() => new()
    {
        Title = "Pasta",
        Ingredients = ["200 g pasta", "1 onion", "2 tbsp oil"],
        Steps =
        [
            new RecipeStep { Order = 1, Instruction = "Fry the onion in oil for 10-12 mins.", IngredientRefs = [1, 2], Timers = [new RecipeTimer("Fry onion", 600, 720)] },
            new RecipeStep { Order = 2, Instruction = "Boil the pasta.", IngredientRefs = [0] },
        ],
        Servings = 2,
    };

    [Fact]
    public void ApplyTo_WhenIngredientsReordered_ShouldRemapStepRefs()
    {
        // Arrange
        var draft = RecipeDraft.FromRecipe(Recipe());
        var keys = draft.Ingredients.Select(i => i.Key).ToList();

        // Act
        draft.ReorderIngredients([keys[2], keys[0], keys[1]]);
        var result = draft.ApplyTo(Recipe());

        // Assert
        result.Ingredients.Should().Equal("2 tbsp oil", "200 g pasta", "1 onion");
        result.Steps[0].IngredientRefs.Should().BeEquivalentTo([2, 0]);
        result.Steps[1].IngredientRefs.Should().Equal(1);
    }

    [Fact]
    public void ApplyTo_WhenIngredientRemoved_ShouldDropItFromStepRefs()
    {
        // Arrange
        var draft = RecipeDraft.FromRecipe(Recipe());

        // Act
        draft.RemoveIngredient(draft.Ingredients[1].Key);
        var result = draft.ApplyTo(Recipe());

        // Assert
        result.Ingredients.Should().Equal("200 g pasta", "2 tbsp oil");
        result.Steps[0].IngredientRefs.Should().Equal(1);
    }

    [Fact]
    public void UpdateStepText_WhenDurationUnchanged_ShouldKeepNamedLabel()
    {
        // Arrange
        var draft = RecipeDraft.FromRecipe(Recipe());
        var step = draft.Steps[0];

        // Act
        draft.UpdateStepText(step.Key, "Gently fry the onion for 10-12 mins, stirring.");

        // Assert
        step.Timers.Should().ContainSingle().Which.Label.Should().Be("Fry onion");
    }

    [Fact]
    public void UpdateStepText_WhenDurationChanged_ShouldDetectNewTimer()
    {
        // Arrange
        var draft = RecipeDraft.FromRecipe(Recipe());
        var step = draft.Steps[0];

        // Act
        draft.UpdateStepText(step.Key, "Fry the onion for 5 minutes.");

        // Assert
        var timer = step.Timers.Should().ContainSingle().Subject;
        timer.MinSeconds.Should().Be(300);
        timer.Label.Should().Be("Fry the onion");
    }

    [Fact]
    public void PasteIngredients_WhenTextHasSeveralLines_ShouldSplitIntoRowsAndSkipBlanks()
    {
        // Arrange
        var draft = RecipeDraft.Empty();

        // Act
        var rows = draft.PasteIngredients(0, "1 egg\n\n  2 cups milk \r\n- 1 tsp salt");

        // Assert
        rows.Select(r => r.Text).Should().Equal("1 egg", "2 cups milk", "1 tsp salt");
        draft.Ingredients.Should().HaveCount(3);
    }

    [Fact]
    public void ToNewRecipe_WhenRowsBlank_ShouldIgnoreThemAndNumberSteps()
    {
        // Arrange
        var draft = RecipeDraft.Empty();
        draft.Title = "Toast";
        draft.InsertIngredient(0, "1 slice bread");
        draft.InsertIngredient(1, "   ");
        draft.InsertStep(0, "Toast it.");
        draft.InsertStep(1, "");

        // Act
        var recipe = draft.ToNewRecipe();

        // Assert
        recipe.Ingredients.Should().Equal("1 slice bread");
        recipe.Steps.Should().ContainSingle().Which.Order.Should().Be(1);
    }

    [Fact]
    public void UpdateStepText_WhenKeyUnknown_ShouldDoNothing()
    {
        // Arrange
        var draft = RecipeDraft.FromRecipe(Recipe());

        // Act
        var act = () => draft.UpdateStepText(Guid.NewGuid(), "Something else.");

        // Assert
        act.Should().NotThrow();
        draft.Steps.Select(s => s.Text).Should().Equal("Fry the onion in oil for 10-12 mins.", "Boil the pasta.");
    }

    [Fact]
    public void ApplyTo_WhenServingsChanged_ShouldClearServingsEstimated()
    {
        // Arrange
        var original = Recipe() with { ServingsEstimated = true };
        var draft = RecipeDraft.FromRecipe(original);
        draft.Servings = 4;

        // Act
        var result = draft.ApplyTo(original);

        // Assert
        result.Servings.Should().Be(4);
        result.ServingsEstimated.Should().BeFalse();
    }

    [Fact]
    public void ApplyTo_WhenServingsUnchanged_ShouldKeepServingsEstimated()
    {
        // Arrange
        var original = Recipe() with { ServingsEstimated = true };
        var draft = RecipeDraft.FromRecipe(original);
        draft.Title = "Better pasta";

        // Act
        var result = draft.ApplyTo(original);

        // Assert
        result.ServingsEstimated.Should().BeTrue();
    }
}
