using FluentAssertions;
using PurePrep.Domain;
using PurePrep.Units;

namespace PurePrep.Core.Tests.Units;

/// <summary>
/// "Ounces" of a liquid (spirits, wine, juice, oil…) are fluid ounces, so they must convert by
/// volume (ml), not weight (g). Reported from the field: "2 ounces blanco or silver tequila" was
/// being shown as "55 g". Words that are actually sold by weight (cream cheese, chocolate) must be
/// left as weight.
/// </summary>
public sealed class UnitConverterLiquidOunceTests
{
    private static string ToMetric(string text) =>
        UnitConverter.ConvertText(text, MeasurementSystem.Imperial, MeasurementSystem.Metric);

    [Theory]
    [InlineData("2 ounces blanco or silver tequila")]
    [InlineData("1 oz vodka")]
    [InlineData("4 oz dry white wine")]
    [InlineData("2 ounces fresh lime juice")]
    [InlineData("3 oz vegetable oil")]
    [InlineData("2 oz whole milk")]
    [InlineData("8 oz chicken stock")]
    public void ConvertText_WhenOuncesOfLiquid_ShouldConvertToMillilitres(string line)
    {
        // Act
        var converted = ToMetric(line);

        // Assert
        converted.Should().MatchRegex(@"\d+\s*ml", because: "a liquid ounce is a fluid ounce");
        converted.Should().NotContain(" g", because: "a pourable liquid is not weighed in grams here");
    }

    [Fact]
    public void ConvertText_TequilaExample_ShouldMatchReportedFix()
    {
        // Act
        var converted = ToMetric("2 ounces blanco or silver tequila");

        // Assert: 2 fl oz ≈ 59 ml, rounded to the nearest 5.
        converted.Should().Be("60 ml blanco or silver tequila");
    }

    [Theory]
    [InlineData("8 oz cream cheese, softened")]
    [InlineData("4 oz sour cream")]
    [InlineData("2 oz dark chocolate")]
    [InlineData("6 oz all-purpose flour")]
    public void ConvertText_WhenOuncesOfSolid_ShouldStayGrams(string line)
    {
        // Act
        var converted = ToMetric(line);

        // Assert
        converted.Should().Contain("g", because: "these ingredients are measured by weight");
        converted.Should().NotContain("ml", because: "no liquid is named on the line");
    }
}
