using FluentAssertions;
using PurePrep.Domain;

namespace PurePrep.Core.Tests.Domain;

public sealed class CookingCopyTests
{
    private static ParsedRecipe Recipe() => new()
    {
        Title = "Soup",
        Ingredients = ["400 g carrots"],
        Steps = [new RecipeStep { Order = 1, Instruction = "Simmer 200 g carrots." }],
        Servings = 4,
        OriginalLanguage = "en",
    };

    [Fact]
    public void For_WhenChosenServingsAndTranslationDisplayed_ShouldScaleTheTranslatedText()
    {
        // Arrange
        var recipe = Recipe()
            .WithTranslation("de", new RecipeTranslation
            {
                Title = "Suppe",
                Ingredients = ["400 g Karotten"],
                Steps = [new RecipeStep { Order = 1, Instruction = "200 g Karotten köcheln." }],
            })
            .WithChosenServings(2);

        // Act
        var cooking = CookingCopy.For(recipe, unitTarget: null);

        // Assert
        cooking.Title.Should().Be("Suppe");
        cooking.Ingredients.Should().Equal("200 g Karotten");
        cooking.Steps[0].Instruction.Should().Be("100 g Karotten köcheln.");
    }

    [Fact]
    public void For_WhenUnitTargetDiffers_ShouldConvertUnits()
    {
        // Arrange
        var recipe = Recipe();

        // Act
        var cooking = CookingCopy.For(recipe, MeasurementSystem.Imperial);

        // Assert
        cooking.Ingredients[0].Should().NotContain(" g ");
        cooking.Ingredients[0].Should().Contain("oz");
    }

    [Fact]
    public void For_WhenYieldUnknown_ShouldUseFallbackFactor()
    {
        // Arrange
        var recipe = new ParsedRecipe { Title = "Dressing", Ingredients = ["100 ml oil"] };

        // Act
        var cooking = CookingCopy.For(recipe, unitTarget: null, fallbackFactor: 2);

        // Assert
        cooking.Ingredients.Should().Equal("200 ml oil");
    }

    [Fact]
    public void For_WhenNothingChosen_ShouldReturnTheTextAsSaved()
    {
        // Act
        var cooking = CookingCopy.For(Recipe(), unitTarget: null);

        // Assert
        cooking.Ingredients.Should().Equal("400 g carrots");
    }

    [Theory]
    [InlineData("de", "en", "de")]
    [InlineData(null, "en", "en")]
    [InlineData(null, null, null)]
    public void SpokenLanguage_ShouldPreferDisplayedThenOriginal(string? display, string? original, string? expected)
    {
        // Arrange
        var recipe = new ParsedRecipe { Title = "Soup", DisplayLanguage = display, OriginalLanguage = original };

        // Act
        var language = CookingCopy.SpokenLanguage(recipe);

        // Assert
        language.Should().Be(expected);
    }
}
