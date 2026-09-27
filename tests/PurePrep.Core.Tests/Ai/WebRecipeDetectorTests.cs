using FluentAssertions;
using PurePrep.Ai;

namespace PurePrep.Core.Tests.Ai;

public sealed class WebRecipeDetectorTests
{
    private static readonly Uri Page = new("https://example.com/recipes/pasta");

    [Fact]
    public void Detect_WhenRecipeHasImageObject_ShouldReturnTitleAndResolvedImage()
    {
        // Arrange
        var blocks = new[]
        {
            """{"@type":"Recipe","name":" Pasta Carbonara ","image":{"@type":"ImageObject","url":"/img/pasta.jpg"}}""",
        };

        // Act
        var recipe = WebRecipeDetector.Detect(blocks, Page);

        // Assert
        recipe.Should().Be(new WebRecipeInfo("Pasta Carbonara", new Uri("https://example.com/img/pasta.jpg")));
    }

    [Fact]
    public void Detect_WhenRecipeInsideGraphWithTypeArray_ShouldFindIt()
    {
        // Arrange
        var blocks = new[]
        {
            """{"@graph":[{"@type":"WebPage"},{"@type":["Recipe"],"name":"Pierogi","image":["https://cdn.x/a.jpg","https://cdn.x/b.jpg"]}]}""",
        };

        // Act
        var recipe = WebRecipeDetector.Detect(blocks, Page);

        // Assert
        recipe.Should().Be(new WebRecipeInfo("Pierogi", new Uri("https://cdn.x/a.jpg")));
    }

    [Fact]
    public void Detect_WhenPageIsAnArticleNotARecipe_ShouldReturnNull()
    {
        // Arrange
        var blocks = new[]
        {
            """{"@type":"Article","name":"How pasta is made","image":"https://cdn.x/a.jpg"}""",
        };

        // Act
        var recipe = WebRecipeDetector.Detect(blocks, Page);

        // Assert
        recipe.Should().BeNull();
    }

    [Fact]
    public void Detect_WhenFirstBlockIsMalformedAndSecondIsValid_ShouldDetectTheValidOne()
    {
        // Arrange
        var blocks = new[]
        {
            "{not json",
            """{"@type":"Recipe","name":"Pierogi"}""",
        };

        // Act
        var recipe = WebRecipeDetector.Detect(blocks, Page);

        // Assert
        recipe.Should().Be(new WebRecipeInfo("Pierogi", null));
    }

    [Fact]
    public void Detect_WhenRecipeHasNoName_ShouldReturnNull()
    {
        // Arrange
        var blocks = new[]
        {
            """{"@type":"Recipe","image":"https://cdn.x/a.jpg"}""",
        };

        // Act
        var recipe = WebRecipeDetector.Detect(blocks, Page);

        // Assert
        recipe.Should().BeNull();
    }

    [Fact]
    public void Detect_WhenImageUsesJavascriptScheme_ShouldKeepTitleButDropImage()
    {
        // Arrange
        var blocks = new[]
        {
            """{"@type":"Recipe","name":"Pierogi","image":"javascript:alert(1)"}""",
        };

        // Act
        var recipe = WebRecipeDetector.Detect(blocks, Page);

        // Assert
        recipe.Should().Be(new WebRecipeInfo("Pierogi", null));
    }

    [Fact]
    public void Detect_WhenOnlyMicrodataRecipe_ShouldReturnMicrodataTitleAndImage()
    {
        // Arrange
        var blocks = new[] { """{"@type":"WebSite","name":"Ania Gotuje"}""" };

        // Act
        var recipe = WebRecipeDetector.Detect(blocks, " Pierogi ruskie ", "https://cdn.x/pierogi.jpg", Page);

        // Assert
        recipe.Should().Be(new WebRecipeInfo("Pierogi ruskie", new Uri("https://cdn.x/pierogi.jpg")));
    }

    [Fact]
    public void Detect_WhenJsonLdAndMicrodataBothPresent_ShouldPreferJsonLd()
    {
        // Arrange
        var blocks = new[] { """{"@type":"Recipe","name":"From JSON-LD","image":"https://cdn.x/ld.jpg"}""" };

        // Act
        var recipe = WebRecipeDetector.Detect(blocks, "From microdata", "https://cdn.x/md.jpg", Page);

        // Assert
        recipe.Should().Be(new WebRecipeInfo("From JSON-LD", new Uri("https://cdn.x/ld.jpg")));
    }

    [Fact]
    public void Detect_WhenMicrodataNameIsBlank_ShouldReturnNull()
    {
        // Arrange
        var blocks = Array.Empty<string>();

        // Act
        var recipe = WebRecipeDetector.Detect(blocks, "   ", "https://cdn.x/md.jpg", Page);

        // Assert
        recipe.Should().BeNull();
    }

    [Fact]
    public void Detect_WhenMicrodataImageIsRelative_ShouldResolveAgainstPage()
    {
        // Arrange
        var blocks = Array.Empty<string>();

        // Act
        var recipe = WebRecipeDetector.Detect(blocks, "Pierogi", "/img/pierogi.jpg", Page);

        // Assert
        recipe.Should().Be(new WebRecipeInfo("Pierogi", new Uri("https://example.com/img/pierogi.jpg")));
    }

    [Fact]
    public void Detect_WhenMicrodataImageUsesJavascriptScheme_ShouldKeepTitleButDropImage()
    {
        // Arrange
        var blocks = Array.Empty<string>();

        // Act
        var recipe = WebRecipeDetector.Detect(blocks, "Pierogi", "javascript:alert(1)", Page);

        // Assert
        recipe.Should().Be(new WebRecipeInfo("Pierogi", null));
    }

    [Fact]
    public void Detect_WhenTypeHasUnpairedSurrogate_ShouldSkipItAndFindLaterRecipe()
    {
        // Arrange — "\ud800x" parses as JSON but GetString() throws on it.
        var blocks = new[]
        {
            """{"@type":"\ud800x","name":"Bad"}""",
            """{"@type":"Recipe","name":"Pierogi"}""",
        };

        // Act
        var recipe = WebRecipeDetector.Detect(blocks, Page);

        // Assert
        recipe.Should().Be(new WebRecipeInfo("Pierogi", null));
    }

    [Fact]
    public void Detect_WhenNameHasUnpairedSurrogate_ShouldTreatAsNoNameAndFindLaterRecipe()
    {
        // Arrange
        var blocks = new[]
        {
            """{"@type":"Recipe","name":"\ud800x"}""",
            """{"@type":"Recipe","name":"Pierogi"}""",
        };

        // Act
        var recipe = WebRecipeDetector.Detect(blocks, Page);

        // Assert
        recipe.Should().Be(new WebRecipeInfo("Pierogi", null));
    }

    [Fact]
    public void Detect_WhenImageHasUnpairedSurrogate_ShouldKeepTitleAndDropImage()
    {
        // Arrange
        var blocks = new[] { """{"@type":"Recipe","name":"Pierogi","image":["\ud800x"]}""" };

        // Act
        var recipe = WebRecipeDetector.Detect(blocks, Page);

        // Assert
        recipe.Should().Be(new WebRecipeInfo("Pierogi", null));
    }
}
