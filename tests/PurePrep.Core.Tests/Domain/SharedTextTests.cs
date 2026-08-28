using FluentAssertions;
using PurePrep.Domain;

namespace PurePrep.Core.Tests.Domain;

/// <summary>
/// Apps rarely share a bare URL. Chrome sends "Title — https://…", WhatsApp wraps it in a message,
/// Instagram prefixes a caption. This pulls the importable link out of whatever arrives.
/// </summary>
public sealed class SharedTextTests
{
    [Theory]
    [InlineData("https://example.com/recipe")]
    [InlineData("  https://example.com/recipe  ")]
    [InlineData("http://example.com/recipe")]
    public void ExtractUrl_FromABareLink_ShouldReturnIt(string shared)
    {
        // Act & Assert
        SharedText.ExtractUrl(shared).Should().Be(shared.Trim());
    }

    [Fact]
    public void ExtractUrl_FromChromeStyleTitleAndLink_ShouldReturnTheLink()
    {
        // Arrange — Chrome's share sheet sends the page title followed by the URL.
        var shared = "Best Lemon Pasta Recipe\nhttps://example.com/lemon-pasta";

        // Act & Assert
        SharedText.ExtractUrl(shared).Should().Be("https://example.com/lemon-pasta");
    }

    [Fact]
    public void ExtractUrl_FromAMessageAroundTheLink_ShouldReturnTheLink()
    {
        // Arrange
        var shared = "you have to try this https://example.com/pasta?utm_source=whatsapp it's great";

        // Act & Assert
        SharedText.ExtractUrl(shared).Should().Be("https://example.com/pasta?utm_source=whatsapp");
    }

    [Fact]
    public void ExtractUrl_WhenSeveralLinksArePresent_ShouldTakeTheFirst()
    {
        // Arrange
        var shared = "https://example.com/recipe and also https://other.com/thing";

        // Act & Assert
        SharedText.ExtractUrl(shared).Should().Be("https://example.com/recipe");
    }

    [Fact]
    public void ExtractUrl_ShouldNotSwallowTrailingSentencePunctuation()
    {
        // Arrange — "…/pasta." should import the recipe, not a 404 on "pasta."
        var shared = "Try this: https://example.com/pasta.";

        // Act & Assert
        SharedText.ExtractUrl(shared).Should().Be("https://example.com/pasta");
    }

    [Fact]
    public void ExtractUrl_ShouldKeepAMeaningfulTrailingSlash()
    {
        // Act & Assert
        SharedText.ExtractUrl("see https://example.com/pasta/").Should().Be("https://example.com/pasta/");
    }

    [Theory]
    [InlineData("just some text with no link")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ExtractUrl_WhenThereIsNoLink_ShouldReturnNull(string? shared)
    {
        // Act & Assert
        SharedText.ExtractUrl(shared).Should().BeNull();
    }

    [Theory]
    [InlineData("ftp://example.com/file")]
    [InlineData("file:///etc/passwd")]
    [InlineData("javascript:alert(1)")]
    public void ExtractUrl_ShouldIgnoreNonWebSchemes(string shared)
    {
        // Arrange — only http(s) is importable, and the rest have no business being followed.
        // Act & Assert
        SharedText.ExtractUrl(shared).Should().BeNull();
    }

    [Fact]
    public void ExtractUrl_ShouldStripSurroundingAngleBrackets()
    {
        // Arrange — some mail and chat clients wrap links this way.
        // Act & Assert
        SharedText.ExtractUrl("<https://example.com/pasta>").Should().Be("https://example.com/pasta");
    }
}
