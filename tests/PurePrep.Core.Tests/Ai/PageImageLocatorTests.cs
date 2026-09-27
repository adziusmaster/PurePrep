using FluentAssertions;
using PurePrep.Ai;

namespace PurePrep.Core.Tests.Ai;

public sealed class PageImageLocatorTests
{
    private static readonly Uri Page = new("https://example.com/recipes/pasta");

    [Fact]
    public void Find_WhenJsonLdHasImageObject_ShouldPreferItOverOgImage()
    {
        // Arrange
        const string html = """
            <html><head><meta property="og:image" content="https://example.com/og.jpg">
            <script type="application/ld+json">{"@type":"Recipe","name":"P","image":{"@type":"ImageObject","url":"/img/pasta.jpg"}}</script>
            </head></html>
            """;

        // Act
        var image = PageImageLocator.Find(html, Page);

        // Assert
        image.Should().Be(new Uri("https://example.com/img/pasta.jpg"));
    }

    [Fact]
    public void Find_WhenJsonLdHasImageArrayInsideGraph_ShouldTakeFirst()
    {
        // Arrange
        const string html = """
            <script type="application/ld+json">{"@graph":[{"@type":"WebPage"},{"@type":["Recipe"],"image":["https://cdn.x/a.jpg","https://cdn.x/b.jpg"]}]}</script>
            """;

        // Act
        var image = PageImageLocator.Find(html, Page);

        // Assert
        image.Should().Be(new Uri("https://cdn.x/a.jpg"));
    }

    [Fact]
    public void Find_WhenOnlyOgImage_ShouldReturnIt()
    {
        // Arrange
        const string html = """<meta property="og:image" content="https://example.com/og.jpg">""";

        // Act
        var image = PageImageLocator.Find(html, Page);

        // Assert
        image.Should().Be(new Uri("https://example.com/og.jpg"));
    }

    [Theory]
    [InlineData("""<meta property="og:image" content="javascript:alert(1)">""")]
    [InlineData("""<meta property="og:image" content="data:image/png;base64,AAAA">""")]
    [InlineData("<html></html>")]
    [InlineData("""<script type="application/ld+json">{not json</script>""")]
    public void Find_WhenNoUsableImage_ShouldReturnNull(string html)
    {
        // Arrange — html from InlineData

        // Act
        var image = PageImageLocator.Find(html, Page);

        // Assert
        image.Should().BeNull();
    }

    [Fact]
    public void Find_WhenTypeArrayHasObjectAndRecipeString_ShouldFindImageWithoutThrowing()
    {
        // Arrange — @type array contains an object (invalid) and "Recipe" string
        const string html = """
            <html><head><meta property="og:image" content="https://example.com/og.jpg">
            <script type="application/ld+json">{"@type":[{"nested":1},"Recipe"],"name":"P","image":"https://cdn.x/recipe.jpg"}</script>
            </head></html>
            """;

        // Act
        var image = PageImageLocator.Find(html, Page);

        // Assert — should find the image without throwing InvalidOperationException
        image.Should().Be(new Uri("https://cdn.x/recipe.jpg"));
    }

    [Fact]
    public void Find_WhenImageUrlIsObject_ShouldFallbackToOgImageWithoutThrowing()
    {
        // Arrange — image.url is an object {"@id": "..."} instead of a string
        const string html = """
            <html><head><meta property="og:image" content="https://example.com/og.jpg">
            <script type="application/ld+json">{"@type":"Recipe","name":"P","image":{"@type":"ImageObject","url":{"@id":"https://cdn.x/img.jpg"}}}</script>
            </head></html>
            """;

        // Act
        var image = PageImageLocator.Find(html, Page);

        // Assert — should fall back to og:image without throwing InvalidOperationException
        image.Should().Be(new Uri("https://example.com/og.jpg"));
    }

    [Fact]
    public void Find_WhenTypeHasUnpairedSurrogate_ShouldSkipBlockWithoutThrowing()
    {
        // Arrange — "\ud800x" parses as JSON but GetString() throws on it.
        const string html = """
            <html><head><meta property="og:image" content="https://example.com/og.jpg">
            <script type="application/ld+json">{"@type":"\ud800x","image":"https://cdn.x/bad.jpg"}</script>
            <script type="application/ld+json">{"@type":"Recipe","name":"P","image":"https://cdn.x/good.jpg"}</script>
            </head></html>
            """;

        // Act
        var image = PageImageLocator.Find(html, Page);

        // Assert
        image.Should().Be(new Uri("https://cdn.x/good.jpg"));
    }
}
