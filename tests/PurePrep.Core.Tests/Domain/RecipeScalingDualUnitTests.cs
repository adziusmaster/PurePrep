using FluentAssertions;
using PurePrep.Domain;

namespace PurePrep.Core.Tests.Domain;

/// <summary>
/// Imported lines like "1½ lbs. (700 grams) boneless chicken" carry the same amount twice. Scaling
/// must move both numbers together (never just one), must not drop the whole number in front of a
/// unicode fraction, and must scale both ends of a worded range ("½ to 1 teaspoon").
/// </summary>
public sealed class RecipeScalingDualUnitTests
{
    [Theory]
    [InlineData("1½ lbs. (700 grams) boneless chicken", 2.0, "3 lbs. (1400 grams) boneless chicken")]
    [InlineData("1½ cups (180 grams) onions", 2.0, "3 cups (360 grams) onions")]
    [InlineData("½ cup (120 ml) Greek yogurt", 0.5, "¼ cup (60 ml) Greek yogurt")]
    public void Scale_WhenDualUnitLine_ShouldScaleBothAmounts(string line, double factor, string expected)
    {
        // Act
        var scaled = RecipeScaling.Scale(line, factor);

        // Assert
        scaled.Should().Be(expected);
    }

    [Theory]
    [InlineData("½ to 1 teaspoon chili powder", 2.0, "1–2 teaspoon chili powder")]
    [InlineData("1 to 1½ teaspoon cumin powder", 2.0, "2–3 teaspoon cumin powder")]
    public void Scale_WhenWordedRange_ShouldScaleBothEnds(string line, double factor, string expected)
    {
        // Act
        var scaled = RecipeScaling.Scale(line, factor);

        // Assert
        scaled.Should().Be(expected);
    }

    [Fact]
    public void Scale_WhenWholeNumberPrecedesUnicodeFraction_ShouldScaleTheWholeQuantity()
    {
        // Act
        var scaled = RecipeScaling.Scale("1½ lbs chicken", 2.0);

        // Assert
        scaled.Should().Be("3 lbs chicken");
    }

    [Fact]
    public void ScaleText_WhenDualUnitInStep_ShouldScaleBothAmounts()
    {
        // Act
        var scaled = RecipeScaling.ScaleText("Add 1½ cups (180 grams) onions", 2.0);

        // Assert
        scaled.Should().Be("Add 3 cups (360 grams) onions");
    }
}
