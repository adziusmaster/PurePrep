using FluentAssertions;
using PurePrep.Ai;

namespace PurePrep.Core.Tests.Ai;

/// <summary>
/// Pulls schema.org recipe data out of a page so the model receives unambiguous ingredient and
/// step boundaries alongside the raw page, instead of having to infer them from flowed text.
/// </summary>
public sealed class StructuredRecipeExtractorTests
{
    private static string Page(string head) => $"<html><head>{head}</head><body><p>story</p></body></html>";

    private static string JsonLd(string json) => Page($"<script type=\"application/ld+json\">{json}</script>");

    private const string SimpleRecipe = """
        {"@context":"https://schema.org","@type":"Recipe","name":"Lemon Pasta",
         "recipeYield":"4 servings",
         "recipeIngredient":["200 g spaghetti","1 lemon","2 tbsp olive oil"],
         "recipeInstructions":["Boil the pasta.","Zest the lemon.","Toss together."]}
        """;

    [Fact]
    public void TryExtract_FromPlainJsonLd_ShouldReadTitleIngredientsAndSteps()
    {
        // Act
        var recipe = StructuredRecipeExtractor.TryExtract(JsonLd(SimpleRecipe));

        // Assert
        recipe.Should().NotBeNull();
        recipe!.Title.Should().Be("Lemon Pasta");
        recipe.Ingredients.Should().Equal("200 g spaghetti", "1 lemon", "2 tbsp olive oil");
        recipe.Steps.Should().Equal("Boil the pasta.", "Zest the lemon.", "Toss together.");
    }

    [Fact]
    public void TryExtract_ShouldCaptureTheYield()
    {
        // Arrange — yield often lives only in this field, and the serving scaler needs it.
        // Act
        var recipe = StructuredRecipeExtractor.TryExtract(JsonLd(SimpleRecipe));

        // Assert
        recipe!.Yield.Should().Be("4 servings");
    }

    [Fact]
    public void TryExtract_WhenTheRecipeSitsInsideAGraph_ShouldStillFindIt()
    {
        // Arrange — WordPress recipe plugins almost always nest the recipe in @graph.
        var html = JsonLd($$"""
            {"@context":"https://schema.org","@graph":[
              {"@type":"WebSite","name":"A Food Blog"},
              {{SimpleRecipe}}
            ]}
            """);

        // Act
        var recipe = StructuredRecipeExtractor.TryExtract(html);

        // Assert
        recipe!.Title.Should().Be("Lemon Pasta");
    }

    [Fact]
    public void TryExtract_WhenInstructionsAreHowToStepObjects_ShouldReadTheirText()
    {
        // Arrange
        var html = JsonLd("""
            {"@type":"Recipe","name":"Soup","recipeIngredient":["water","salt"],
             "recipeInstructions":[
               {"@type":"HowToStep","text":"Boil the water."},
               {"@type":"HowToStep","text":"Add salt."}]}
            """);

        // Act
        var recipe = StructuredRecipeExtractor.TryExtract(html);

        // Assert
        recipe!.Steps.Should().Equal("Boil the water.", "Add salt.");
    }

    [Fact]
    public void TryExtract_WhenTypeIsAnArray_ShouldStillRecogniseARecipe()
    {
        // Arrange — some publishers emit "@type":["Recipe","NewsArticle"].
        var html = JsonLd("""
            {"@type":["Recipe","NewsArticle"],"name":"Bread",
             "recipeIngredient":["flour","water"],"recipeInstructions":["Mix.","Bake."]}
            """);

        // Act & Assert
        StructuredRecipeExtractor.TryExtract(html)!.Title.Should().Be("Bread");
    }

    [Fact]
    public void TryExtract_WhenThePageHasNoStructuredData_ShouldReturnNull()
    {
        // Act & Assert
        StructuredRecipeExtractor.TryExtract("<html><body><p>Just a blog post.</p></body></html>")
            .Should().BeNull();
    }

    [Fact]
    public void TryExtract_WhenTheJsonIsMalformed_ShouldReturnNullRatherThanThrow()
    {
        // Arrange — malformed JSON-LD is common; it must degrade to the raw page, not fail the import.
        var html = JsonLd("{ this is not json ");

        // Act
        var act = () => StructuredRecipeExtractor.TryExtract(html);

        // Assert
        act.Should().NotThrow();
        act().Should().BeNull();
    }

    [Fact]
    public void TryExtract_ShouldDecodeHtmlEntities()
    {
        // Arrange
        var html = JsonLd("""
            {"@type":"Recipe","name":"Salt &amp; Pepper Squid",
             "recipeIngredient":["500 g squid","1 tsp salt"],"recipeInstructions":["Fry."]}
            """);

        // Act & Assert
        StructuredRecipeExtractor.TryExtract(html)!.Title.Should().Be("Salt & Pepper Squid");
    }

    // --- The quality gate -------------------------------------------------------------------

    [Fact]
    public void IsUsable_ForACompleteRecipe_ShouldBeTrue()
    {
        // Act & Assert
        StructuredRecipeExtractor.IsUsable(StructuredRecipeExtractor.TryExtract(JsonLd(SimpleRecipe)))
            .Should().BeTrue();
    }

    [Theory]
    [InlineData("""{"@type":"Recipe","name":"Thin","recipeIngredient":["salt"],"recipeInstructions":["Do it."]}""")]
    [InlineData("""{"@type":"Recipe","name":"No steps","recipeIngredient":["a","b"],"recipeInstructions":[]}""")]
    [InlineData("""{"@type":"Recipe","name":"No ingredients","recipeInstructions":["Mix.","Bake."]}""")]
    public void IsUsable_ForAThinOrPartialBlob_ShouldBeFalse(string json)
    {
        // Arrange — a stub blob is worse than the real page, so it must not displace it.
        var recipe = StructuredRecipeExtractor.TryExtract(JsonLd(json));

        // Act & Assert
        StructuredRecipeExtractor.IsUsable(recipe).Should().BeFalse();
    }

    [Fact]
    public void IsUsable_ForNothing_ShouldBeFalse()
    {
        // Act & Assert
        StructuredRecipeExtractor.IsUsable(null).Should().BeFalse();
    }
}
