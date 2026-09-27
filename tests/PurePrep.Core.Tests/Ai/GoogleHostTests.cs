using FluentAssertions;
using PurePrep.Ai;

namespace PurePrep.Core.Tests.Ai;

public sealed class GoogleHostTests
{
    [Theory]
    [InlineData("https://www.google.com/search?q=pierogi")]
    [InlineData("https://google.de/")]
    [InlineData("https://www.google.co.uk/search")]
    [InlineData("https://www.google.com.au/")]
    [InlineData("https://consent.google.pl/ml")]
    public void IsGoogle_WhenRegistrableGoogleHost_ShouldReturnTrue(string url)
    {
        // Arrange
        var uri = new Uri(url);

        // Act
        var result = GoogleHost.IsGoogle(uri);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("https://www.recipetineats.com/pierogi/")]
    [InlineData("https://notgoogle.com/")]
    [InlineData("https://google.recipes-blog.com/pierogi")]
    [InlineData("https://www.google.com.evil.io/")]
    [InlineData("https://blog.example.com/google.html")]
    public void IsGoogle_WhenOtherHost_ShouldReturnFalse(string url)
    {
        // Arrange
        var uri = new Uri(url);

        // Act
        var result = GoogleHost.IsGoogle(uri);

        // Assert
        result.Should().BeFalse();
    }
}
