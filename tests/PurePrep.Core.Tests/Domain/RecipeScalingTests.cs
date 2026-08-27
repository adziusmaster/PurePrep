using FluentAssertions;
using PurePrep.Domain;

namespace PurePrep.Core.Tests.Domain;

public sealed class RecipeScalingTests
{
    private static ParsedRecipe Recipe(params string[] ingredients) => new()
    {
        Title = "Pancakes",
        SourceUrl = "https://example.com/pancakes",
        SourceSystem = MeasurementSystem.Metric,
        Ingredients = ingredients,
        Steps = [new RecipeStep { Order = 1, Instruction = "Mix for 2 minutes." }],
    };

    [Theory]
    [InlineData("200 g flour", 2.0, "400 g flour")]
    [InlineData("200 g flour", 0.5, "100 g flour")]
    [InlineData("1 1/2 cups milk", 2.0, "3 cups milk")]
    [InlineData("2-3 eggs", 2.0, "4–6 eggs")]
    public void Scale_ShouldMultiplyTheLeadingQuantity(string line, double factor, string expected)
    {
        // Act & Assert
        RecipeScaling.Scale(line, factor).Should().Be(expected);
    }

    [Fact]
    public void Scale_WhenThereIsNoLeadingQuantity_ShouldLeaveTheLineAlone()
    {
        // Act & Assert
        RecipeScaling.Scale("salt to taste", 3).Should().Be("salt to taste");
    }

    // --- Whole-recipe scaling: what Focus Mode needs ---------------------------------------

    [Fact]
    public void ScaleRecipe_ShouldScaleEveryIngredient()
    {
        // Arrange - the detail screen scales ingredients for display, but Focus Mode was handed a
        // recipe that had only been unit-converted, so cooking always showed 1x quantities.
        var recipe = Recipe("200 g flour", "2 eggs", "salt to taste");

        // Act
        var scaled = RecipeScaling.ScaleRecipe(recipe, 2.0);

        // Assert
        scaled.Ingredients.Should().Equal("400 g flour", "4 eggs", "salt to taste");
    }

    [Fact]
    public void ScaleRecipe_ShouldNotAlterTheMethodSteps()
    {
        // Arrange - "bake for 20 minutes" must not become "bake for 40 minutes".
        var recipe = Recipe("200 g flour");

        // Act
        var scaled = RecipeScaling.ScaleRecipe(recipe, 3.0);

        // Assert
        scaled.Steps.Should().ContainSingle().Which.Instruction.Should().Be("Mix for 2 minutes.");
    }

    [Fact]
    public void ScaleRecipe_ShouldPreserveIdentitySoItCanReplaceTheOriginal()
    {
        // Arrange
        var recipe = Recipe("200 g flour");

        // Act
        var scaled = RecipeScaling.ScaleRecipe(recipe, 2.0);

        // Assert
        scaled.Id.Should().Be(recipe.Id);
        scaled.Title.Should().Be(recipe.Title);
        scaled.SourceUrl.Should().Be(recipe.SourceUrl);
        scaled.SourceSystem.Should().Be(recipe.SourceSystem);
        scaled.SavedAt.Should().Be(recipe.SavedAt);
    }

    [Fact]
    public void ScaleRecipe_AtOneTimes_ShouldReturnTheSameInstance()
    {
        // Arrange
        var recipe = Recipe("200 g flour");

        // Act & Assert
        RecipeScaling.ScaleRecipe(recipe, 1.0).Should().BeSameAs(recipe);
    }
}
