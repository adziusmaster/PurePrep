using FluentAssertions;
using PurePrep.Domain;

namespace PurePrep.Core.Tests.Domain;

/// <summary>
/// The first-launch sample recipe (pierogi ruskie, based on Ania Gotuje's recipe with no egg in the
/// dough) is a full 1.4 recipe: it drives servings scaling, Focus Mode's named timer chips, and the
/// ingredient-reference "You'll need" chips, so its shape matters as much as any imported recipe's.
/// It is localised (English original + cached Polish), so structural invariants are checked for both.
/// </summary>
public sealed class SampleRecipeTests
{
    private static ParsedRecipe English() => SampleRecipe.Create().Displayed();

    private static ParsedRecipe Polish() => SampleRecipe.Create().WithDisplayLanguage("pl").Displayed();

    public static TheoryData<string, ParsedRecipe> Languages() => new()
    {
        { "en", English() },
        { "pl", Polish() },
    };

    [Fact]
    public void Create_ShouldSetSixServings_NotEstimated()
    {
        var recipe = SampleRecipe.Create();

        recipe.Servings.Should().Be(6);
        recipe.ServingsEstimated.Should().BeFalse();
        recipe.ServingsNoun.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldPointAtTheAniaGotujeSource()
    {
        var recipe = SampleRecipe.Create();

        recipe.SourceUrl.Should().Be("https://aniagotuje.pl/przepis/pierogi-ruskie");
    }

    [Fact]
    public void Create_ShouldSetRealisticPrepAndCookTimes()
    {
        var recipe = SampleRecipe.Create();

        recipe.PrepMinutes.Should().Be(60);
        recipe.CookMinutes.Should().Be(30);
    }

    [Fact]
    public void Create_ShouldDefaultToWantToCookWithNoImage()
    {
        var recipe = SampleRecipe.Create();

        recipe.Status.Should().Be(RecipeStatus.WantToCook);
        recipe.ImagePath.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldIncludeAFriendlyNote()
    {
        var recipe = SampleRecipe.Create();

        recipe.Notes.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Create_ShouldContainNoEggIngredient()
    {
        var recipe = SampleRecipe.Create();

        recipe.Ingredients.Should().NotContain(i => i.Contains("egg", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Create_EveryStepIngredientRef_ShouldBeWithinIngredientRange(string language, ParsedRecipe recipe)
    {
        _ = language; // xUnit needs a display name for the theory row.

        foreach (var step in recipe.Steps)
        foreach (var reference in step.IngredientRefs)
            reference.Should().BeInRange(0, recipe.Ingredients.Count - 1);
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Create_ShouldHaveAtLeastFourNamedTimers_IncludingRanges(string language, ParsedRecipe recipe)
    {
        _ = language;

        var timers = recipe.Steps.SelectMany(s => s.Timers).ToList();

        timers.Should().HaveCountGreaterThanOrEqualTo(4);
        timers.Should().OnlyContain(t => !string.IsNullOrWhiteSpace(t.Label));
        timers.Should().Contain(t => t.IsRange);
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Create_ShouldHaveElevenIngredientsAndEightSteps(string language, ParsedRecipe recipe)
    {
        _ = language;

        recipe.Ingredients.Should().HaveCount(11);
        recipe.Steps.Should().HaveCount(8);
    }

    [Fact]
    public void ForCooking_WithTwelveServings_ShouldDoubleTheFlour()
    {
        // Arrange
        var recipe = SampleRecipe.Create().WithChosenServings(12);

        // Act
        var cooking = ServingsScale.ForCooking(recipe);

        // Assert
        cooking.Ingredients.Should().Contain(i => i.StartsWith("1000 g plain flour", StringComparison.Ordinal));
    }

    // Regression for the cook-friendly rounding bug: scaling 6 -> 7 servings (factor 7/6) used to
    // show "758.33 g potatoes" and "0.58 tsp salt" — decimals nobody would actually measure out.
    // The salt/pepper lines (½ tsp x 7/6 = 0.58333) sit exactly between ½ and ⅔ tsp; per the
    // controller ruling, spoons snap only to the six common kitchen fractions and round down on an
    // exact tie, so these resolve to ½ tsp, not ⅝ tsp.
    [Fact]
    public void ForCooking_WithSevenServings_ShouldRoundEveryIngredientLikeACookWouldWriteIt()
    {
        // Arrange
        var recipe = SampleRecipe.Create().WithChosenServings(7);

        // Act
        var cooking = ServingsScale.ForCooking(recipe);

        // Assert
        cooking.Ingredients.Should().Equal(
            "760 g potatoes (about 500 g once cooked)",
            "½ tsp salt (for the filling)",
            "½ tsp pepper",
            "350 g onion",
            "2⅓ tbsp clarified butter",
            "350 g semi-fat cottage cheese (twaróg)",
            "585 g plain flour",
            "½ tsp salt (for the dough)",
            "58 ml oil",
            "290 ml hot water",
            "2⅓ tbsp butter, for frying (optional)");
    }

    [Fact]
    public void PolishTranslation_ShouldAlsoContainNoEgg()
    {
        var recipe = Polish();

        recipe.Ingredients.Should().NotContain(i => i.Contains("jajko", StringComparison.OrdinalIgnoreCase));
    }
}
