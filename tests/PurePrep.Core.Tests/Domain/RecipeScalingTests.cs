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

    // Languages that name the ingredient first put the amount at the end of the line, so a
    // leading-only scaler left "2x" showing the same quantities. Every unit-bearing amount must scale.
    [Theory]
    [InlineData("marchewki 500 g", 2.0, "marchewki 1000 g")]
    [InlineData("mąka pszenna 80 g", 2.0, "mąka pszenna 160 g")]
    [InlineData("woda 125 ml", 2.0, "woda 250 ml")]
    [InlineData("olej 4 łyżki", 2.0, "olej 8 łyżki")]
    [InlineData("sól 1 łyżeczka", 3.0, "sól 3 łyżeczka")]
    [InlineData("wheat flour 80 g", 0.5, "wheat flour 40 g")]
    public void Scale_ShouldMultiplyATrailingUnitQuantity(string line, double factor, string expected)
    {
        // Act & Assert
        RecipeScaling.Scale(line, factor).Should().Be(expected);
    }

    // Numbers that are not cooking amounts must survive: a leek's length, a chocolate's cocoa
    // percentage, a Polish flour type number. None carries a scalable mass/volume/spoon unit.
    [Theory]
    [InlineData("por 25 cm", 2.0, "por 25 cm")]
    [InlineData("mąka pszenna typu 500", 2.0, "mąka pszenna typu 500")]
    [InlineData("czekolada 70%, 100 g", 2.0, "czekolada 70%, 200 g")]
    public void Scale_ShouldLeaveNonCookingNumbersUntouched(string line, double factor, string expected)
    {
        // Act & Assert
        RecipeScaling.Scale(line, factor).Should().Be(expected);
    }

    // A "N x M unit" pack multiplier must scale only the pack count, not the size of each pack.
    [Fact]
    public void Scale_ShouldTreatCountTimesSizeAsAMultiplier()
    {
        // Act & Assert
        RecipeScaling.Scale("3 x 400 g tinned tomatoes", 2.0).Should().Be("6 x 400 g tinned tomatoes");
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
    public void ScaleRecipe_ShouldScaleAmountsInStepsButNotTimes()
    {
        // Arrange - amounts written into a step should track the batch, but "20 minutes" must not
        // become "40 minutes" and a tin size must not grow.
        var recipe = new ParsedRecipe
        {
            Title = "Cake",
            Ingredients = ["200 g flour"],
            Steps =
            [
                new RecipeStep { Order = 1, Instruction = "Stir in 200 g sugar." },
                new RecipeStep { Order = 2, Instruction = "Bake for 20 minutes at 180°C in a 24 cm tin." },
            ],
        };

        // Act
        var scaled = RecipeScaling.ScaleRecipe(recipe, 2.0);

        // Assert
        scaled.Steps.Select(s => s.Instruction).Should().Equal(
            "Stir in 400 g sugar.",
            "Bake for 20 minutes at 180°C in a 24 cm tin.");
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
