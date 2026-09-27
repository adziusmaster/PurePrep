using FluentAssertions;
using PurePrep.Domain;

namespace PurePrep.Core.Tests.Domain;

public sealed class ClipboardSuggestionTests
{
    [Fact]
    public void Evaluate_WhenClipboardHasNewRecipeLink_ShouldSuggestIt()
    {
        // Arrange
        const string clip = "Look at this https://www.bbcgoodfood.com/recipes/easy-pancakes!";

        // Act
        var url = ClipboardSuggestion.Evaluate(clip, null, ["https://example.com/other"]);

        // Assert
        url.Should().Be("https://www.bbcgoodfood.com/recipes/easy-pancakes");
    }

    [Fact]
    public void Evaluate_WhenLinkAlreadySavedWithTrivialDifferences_ShouldNotSuggest()
    {
        // Arrange
        const string clip = "http://bbcgoodfood.com/recipes/easy-pancakes/";

        // Act
        var url = ClipboardSuggestion.Evaluate(clip, null, ["https://www.bbcgoodfood.com/recipes/easy-pancakes"]);

        // Assert
        url.Should().BeNull();
    }

    [Fact]
    public void Evaluate_WhenSameLinkWasSuggestedBefore_ShouldNotSuggestAgain()
    {
        // Arrange
        const string clip = "https://a.com/r/1";

        // Act
        var url = ClipboardSuggestion.Evaluate(clip, "https://a.com/r/1/", []);

        // Assert
        url.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("just some text")]
    public void Evaluate_WhenNoLink_ShouldReturnNull(string? clip)
    {
        // Arrange — clip from InlineData

        // Act
        var url = ClipboardSuggestion.Evaluate(clip, null, []);

        // Assert
        url.Should().BeNull();
    }
}
