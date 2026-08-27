using FluentAssertions;
using PurePrep.Ai;

namespace PurePrep.Core.Tests.Ai;

/// <summary>
/// Builds what the model actually reads: the page's own structured recipe data plus the raw page
/// for context. The budget rule matters — the structured block is the reliable part, so it must
/// never be the half that gets cut when the input is capped.
/// </summary>
public sealed class RecipeExtractionInputTests
{
    private static StructuredRecipe Structured() => new(
        "Lemon Pasta",
        ["200 g spaghetti", "1 lemon"],
        ["Boil the pasta.", "Toss together."],
        "4 servings");

    [Fact]
    public void Build_WithStructuredData_ShouldIncludeTitleIngredientsAndSteps()
    {
        // Act
        var input = RecipeExtractionInput.Build(Structured(), "page text", maxChars: 10_000);

        // Assert
        input.Should().Contain("Lemon Pasta")
            .And.Contain("200 g spaghetti")
            .And.Contain("Boil the pasta.");
    }

    [Fact]
    public void Build_WithStructuredData_ShouldIncludeTheYield()
    {
        // Arrange — the serving scaler reads the yield out of the recipe text later.
        // Act
        var input = RecipeExtractionInput.Build(Structured(), "page text", maxChars: 10_000);

        // Assert
        input.Should().Contain("4 servings");
    }

    [Fact]
    public void Build_ShouldIncludeTheRawPageAsWell()
    {
        // Arrange — structured data can be subtly wrong, so the model still sees the real page.
        // Act
        var input = RecipeExtractionInput.Build(Structured(), "the full blog post text", maxChars: 10_000);

        // Assert
        input.Should().Contain("the full blog post text");
    }

    [Fact]
    public void Build_ShouldPlaceTheStructuredDataBeforeThePage()
    {
        // Act
        var input = RecipeExtractionInput.Build(Structured(), "PAGEBODY", maxChars: 10_000);

        // Assert
        input.IndexOf("200 g spaghetti", StringComparison.Ordinal)
            .Should().BeLessThan(input.IndexOf("PAGEBODY", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_WhenThePageIsHuge_ShouldTruncateThePageAndKeepTheStructuredDataIntact()
    {
        // Arrange — this is the whole point of the ordering rule.
        var hugePage = new string('x', 500_000);

        // Act
        var input = RecipeExtractionInput.Build(Structured(), hugePage, maxChars: 2_000);

        // Assert
        input.Length.Should().BeLessThanOrEqualTo(2_000);
        input.Should().Contain("200 g spaghetti");
        input.Should().Contain("Toss together.");
    }

    [Fact]
    public void Build_WithoutStructuredData_ShouldFallBackToThePageAlone()
    {
        // Act
        var input = RecipeExtractionInput.Build(null, "just the page", maxChars: 10_000);

        // Assert
        input.Should().Contain("just the page");
    }

    [Fact]
    public void Build_WithoutStructuredData_ShouldStillRespectTheBudget()
    {
        // Act
        var input = RecipeExtractionInput.Build(null, new string('y', 100_000), maxChars: 5_000);

        // Assert
        input.Length.Should().BeLessThanOrEqualTo(5_000);
    }

    [Fact]
    public void Build_ShouldLabelTheTwoSectionsSoTheModelCanTellThemApart()
    {
        // Arrange — without labels the model cannot know which half is authoritative.
        // Act
        var input = RecipeExtractionInput.Build(Structured(), "page", maxChars: 10_000);

        // Assert
        input.Should().Contain("STRUCTURED RECIPE DATA").And.Contain("PAGE TEXT");
    }

    [Fact]
    public void Build_WhenThePageIsEmpty_ShouldStillReturnTheStructuredData()
    {
        // Act
        var input = RecipeExtractionInput.Build(Structured(), string.Empty, maxChars: 10_000);

        // Assert
        input.Should().Contain("Lemon Pasta");
    }
}
