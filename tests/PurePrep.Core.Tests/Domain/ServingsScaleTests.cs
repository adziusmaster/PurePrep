using FluentAssertions;
using PurePrep.Domain;

namespace PurePrep.Core.Tests.Domain;

public sealed class ServingsScaleTests
{
    [Fact]
    public void ForCooking_WhenChosenServingsDiffers_ShouldScaleIngredients()
    {
        // Arrange
        var recipe = new ParsedRecipe { Title = "Soup", Ingredients = ["400 g carrots"], Servings = 4 }.WithChosenServings(6);

        // Act
        var cooking = ServingsScale.ForCooking(recipe);

        // Assert
        cooking.Ingredients[0].Should().Be("600 g carrots");
    }

    [Fact]
    public void OriginalServings_WhenFieldMissing_ShouldFallBackToDetector()
    {
        // Arrange
        var recipe = new ParsedRecipe { Title = "Pancakes for 4 people", Ingredients = ["100 g flour"] };

        // Act
        var servings = ServingsScale.OriginalServings(recipe);

        // Assert
        servings.Should().Be(4);
    }

    [Theory]
    [InlineData(null, 3, 1.0)]
    [InlineData(4, null, 1.0)]
    [InlineData(4, 2, 0.5)]
    [InlineData(0, 2, 1.0)]
    public void Factor_ForEdgeCases_ShouldNeverDivideByZero(int? original, int? chosen, double expected)
    {
        // Arrange — values from InlineData

        // Act
        var factor = ServingsScale.Factor(original, chosen);

        // Assert
        factor.Should().BeApproximately(expected, 0.0001);
    }
}
