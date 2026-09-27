using FluentAssertions;
using PurePrep.Domain;

namespace PurePrep.Core.Tests.Domain;

/// <summary>
/// ASCII fractions as imported from recipe sites ("1/2 tsp", "2/3 cup"). The leading-quantity
/// pattern used to try the plain whole number before the fraction, so "1/2" matched only "1" and
/// scaling produced "2/2 tsp" at 2×. These run the same pipeline the app cooks from.
/// </summary>
public sealed class RecipeScalingAsciiFractionTests
{
    [Theory]
    [InlineData("1/2 tsp cooking salt", 2.0, "1 tsp cooking salt")]
    [InlineData("1/4 tsp black pepper", 2.0, "½ tsp black pepper")]
    [InlineData("2/3 cup sour cream", 2.0, "1⅓ cup sour cream")]
    [InlineData("1/2 cup melted butter", 13d / 12, "½ cup melted butter")]
    [InlineData("1/2 cup melted butter", 2.0, "1 cup melted butter")]
    [InlineData("1 1/2 cups milk", 2.0, "3 cups milk")]
    [InlineData("1-1/2 cups milk", 2.0, "3 cups milk")]
    [InlineData("1/2-1 tsp chilli flakes", 2.0, "1–2 tsp chilli flakes")]
    [InlineData("1/2 onion, diced", 2.0, "1 onion, diced")]
    public void Scale_WhenLeadingQuantityIsAsciiFraction_ShouldScaleTheWholeFraction(string line, double factor, string expected)
    {
        // Arrange — inputs come from InlineData.

        // Act
        var scaled = RecipeScaling.Scale(line, factor);

        // Assert
        scaled.Should().Be(expected);
    }

    [Theory]
    [InlineData("Stir in 1/2 cup cream.", 2.0, "Stir in 1 cup cream.")]
    [InlineData("Add 1-1/2 cups stock.", 2.0, "Add 3 cups stock.")]
    public void ScaleText_WhenStepHasAsciiFraction_ShouldScaleTheWholeFraction(string text, double factor, string expected)
    {
        // Arrange — inputs come from InlineData.

        // Act
        var scaled = RecipeScaling.ScaleText(text, factor);

        // Assert
        scaled.Should().Be(expected);
    }

    [Fact]
    public void For_WhenImperialStroganoffDoubled_ShouldScaleAsciiFractions()
    {
        // Arrange
        var recipe = new ParsedRecipe
        {
            Title = "Beef Stroganoff",
            Ingredients = ["1/2 tsp cooking salt", "1/4 tsp black pepper", "2/3 cup sour cream", "1 tbsp Dijon mustard"],
            Steps = [new RecipeStep { Order = 1, Instruction = "Stir in the sour cream." }],
            Servings = 4,
            SourceSystem = MeasurementSystem.Imperial,
        }.WithChosenServings(8);

        // Act
        var cooking = CookingCopy.For(recipe, MeasurementSystem.Imperial);

        // Assert
        cooking.Ingredients.Should().Equal("1 tsp cooking salt", "½ tsp black pepper", "1⅓ cup sour cream", "2 tbsp Dijon mustard");
    }

    [Fact]
    public void For_WhenMetricTargetAndAsciiFractionCup_ShouldConvertTheWholeFractionBeforeScaling()
    {
        // Arrange
        var recipe = new ParsedRecipe
        {
            Title = "Muffins",
            Ingredients = ["1/2 cup melted butter", "1-1/2 cups milk"],
            Servings = 12,
            SourceSystem = MeasurementSystem.Imperial,
        }.WithChosenServings(24);

        // Act
        var cooking = CookingCopy.For(recipe, MeasurementSystem.Metric);

        // Assert
        cooking.Ingredients.Should().Equal("240 ml melted butter", "710 ml milk");
    }

    [Fact]
    public void Scale_WhenOnlyTheLeadingAmountScales_ShouldKeepOtherMixedNumbersAsWritten()
    {
        // Arrange
        const string line = "2 carrots, cut into 1-1/2 inch pieces";

        // Act
        var scaled = RecipeScaling.Scale(line, 2.0);

        // Assert
        scaled.Should().Be("4 carrots, cut into 1-1/2 inch pieces");
    }
}
