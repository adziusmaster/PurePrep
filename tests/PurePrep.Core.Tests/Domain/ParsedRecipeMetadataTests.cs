using FluentAssertions;
using PurePrep.Domain;

namespace PurePrep.Core.Tests.Domain;

public sealed class ParsedRecipeMetadataTests
{
    private static ParsedRecipe Rich() => new()
    {
        Title = "Pasta",
        Ingredients = ["200 g pasta"],
        Steps = [new RecipeStep
        {
            Order = 1,
            Instruction = "Boil for 10-12 min.",
            Timers = [new RecipeTimer("Boil pasta", 600, 720)],
            IngredientRefs = [0],
        }],
        Servings = 4,
        ServingsNoun = "people",
        PrepMinutes = 5,
        CookMinutes = 12,
        ImagePath = "images/x.jpg",
        Notes = "Less salt",
        IsFavourite = true,
        ChosenServings = 2,
        Translations = new Dictionary<string, RecipeTranslation>(StringComparer.OrdinalIgnoreCase)
        {
            ["pl"] = new RecipeTranslation
            {
                Title = "Makaron",
                Ingredients = ["200 g makaronu"],
                Steps = [new RecipeStep { Order = 1, Instruction = "Gotuj 10-12 min.",
                    Timers = [new RecipeTimer("Gotuj makaron", 600, 720)], IngredientRefs = [0] }],
            },
        },
    };

    [Fact]
    public void Displayed_WhenTranslationActive_ShouldKeepMetadata()
    {
        // Arrange
        var recipe = Rich().WithDisplayLanguage("pl");

        // Act
        var shown = recipe.Displayed();

        // Assert
        shown.Title.Should().Be("Makaron");
        shown.Notes.Should().Be("Less salt");
        shown.ImagePath.Should().Be("images/x.jpg");
        shown.IsFavourite.Should().BeTrue();
        shown.Servings.Should().Be(4);
        shown.Steps[0].Timers[0].Label.Should().Be("Gotuj makaron");
    }

    [Fact]
    public void ScaleRecipe_WhenFactorIsNotOne_ShouldKeepTimersRefsAndMetadata()
    {
        // Arrange
        var recipe = Rich();

        // Act
        var scaled = RecipeScaling.ScaleRecipe(recipe, 2);

        // Assert
        scaled.Ingredients[0].Should().Be("400 g pasta");
        scaled.Steps[0].Timers.Should().ContainSingle().Which.MinSeconds.Should().Be(600);
        scaled.Steps[0].IngredientRefs.Should().Equal(0);
        scaled.Notes.Should().Be("Less salt");
        scaled.OriginalLanguage.Should().Be(recipe.OriginalLanguage);
    }

    [Fact]
    public void MarkCooked_WhenCalledTwice_ShouldIncrementCountAndStampLatestDate()
    {
        // Arrange
        var first = new DateTimeOffset(2026, 9, 1, 18, 0, 0, TimeSpan.Zero);
        var second = first.AddDays(3);

        // Act
        var cooked = Rich().MarkCooked(first).MarkCooked(second);

        // Assert
        cooked.Status.Should().Be(RecipeStatus.Cooked);
        cooked.CookCount.Should().Be(2);
        cooked.CookedAt.Should().Be(second);
    }

    [Fact]
    public void WithChosenServings_WhenOutOfRange_ShouldClampToOneThroughNinetyNine()
    {
        // Arrange
        var recipe = Rich();

        // Act
        var low = recipe.WithChosenServings(0);
        var high = recipe.WithChosenServings(500);

        // Assert
        low.ChosenServings.Should().Be(1);
        high.ChosenServings.Should().Be(99);
    }

    [Theory]
    [InlineData(600, 600, "10 min")]
    [InlineData(600, 720, "10–12 min")]
    [InlineData(45, 45, "45 s")]
    [InlineData(5400, 5400, "1 h 30 min")]
    public void DurationText_ForDurations_ShouldFormatCompactly(int min, int max, string expected)
    {
        // Arrange
        var timer = new RecipeTimer("x", min, max);

        // Act
        var text = timer.DurationText();

        // Assert
        text.Should().Be(expected);
    }
}
