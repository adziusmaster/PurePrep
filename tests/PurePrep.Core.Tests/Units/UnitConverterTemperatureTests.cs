using FluentAssertions;
using PurePrep.Domain;
using PurePrep.Units;

namespace PurePrep.Core.Tests.Units;

/// <summary>
/// Temperature detection. "c" and "f" were registered as bare aliases for Celsius and Fahrenheit,
/// but "c" is also the standard American abbreviation for "cup" — so "2 c flour" was read as two
/// degrees Celsius and rewritten as "35.6°F flour".
/// </summary>
public sealed class UnitConverterTemperatureTests
{
    private static string ToImperial(string text) =>
        UnitConverter.ConvertText(text, MeasurementSystem.Metric, MeasurementSystem.Imperial);

    private static string ToMetric(string text) =>
        UnitConverter.ConvertText(text, MeasurementSystem.Imperial, MeasurementSystem.Metric);

    [Theory]
    [InlineData("2 c flour")]
    [InlineData("1 c sugar")]
    [InlineData("1/2 c milk")]
    [InlineData("2 c. flour")]
    public void ConvertText_WhenCAbbreviatesCup_ShouldNotTreatItAsCelsius(string line)
    {
        // Act
        var converted = ToImperial(line);

        // Assert
        converted.Should().NotContain("°F", because: "'c' here is cups, not degrees Celsius");
    }

    [Theory]
    [InlineData("Bake at 200°C for 20 minutes", "390°F")]
    [InlineData("Bake at 200 °C", "390°F")]
    [InlineData("Preheat the oven to 180ºC", "355°F")]
    [InlineData("Heat to 200 celsius", "390°F")]
    public void ConvertText_WhenCelsiusIsUnambiguous_ShouldStillConvert(string line, string expected)
    {
        // Act
        var converted = ToImperial(line);

        // Assert
        converted.Should().Contain(expected);
    }

    [Theory]
    [InlineData("Bake at 350°F", "175°C")]
    [InlineData("Heat to 400 fahrenheit", "205°C")]
    public void ConvertText_WhenFahrenheitIsUnambiguous_ShouldStillConvert(string line, string expected)
    {
        // Act
        var converted = ToMetric(line);

        // Assert
        converted.Should().Contain(expected);
    }

    [Theory]
    [InlineData("Bake at 200 C", "390°F")]
    public void ConvertText_WhenBareCCarriesAnOvenTemperature_ShouldStillConvert(string line, string expected)
    {
        // Act
        var converted = ToImperial(line);

        // Assert
        converted.Should().Contain(expected, because: "recipes often write '200 C' with no degree sign");
    }

    [Fact]
    public void ConvertText_WhenFCouldBeAnythingElse_ShouldNotInventATemperature()
    {
        // Arrange — a bare trailing "f" is not a reliable Fahrenheit signal.
        var line = "3 f something";

        // Act
        var converted = ToMetric(line);

        // Assert
        converted.Should().Be(line);
    }

    [Fact]
    public void Detect_ShouldNotReadCupAbbreviationsAsMetric()
    {
        // Arrange — "2 c flour" wrongly resolving to Celsius also skewed source-system detection
        // towards Metric for American recipes.
        var lines = new[] { "2 c flour", "1 c sugar", "1 lb butter" };

        // Act
        var system = UnitConverter.Detect(lines);

        // Assert
        system.Should().Be(MeasurementSystem.Imperial);
    }
}
