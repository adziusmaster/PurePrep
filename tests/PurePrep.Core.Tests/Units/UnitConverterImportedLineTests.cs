using FluentAssertions;
using PurePrep.Domain;
using PurePrep.Units;

namespace PurePrep.Core.Tests.Units;

/// <summary>
/// Reported from the field on an imported chicken tikka masala: lines like "1½ lbs. (700 grams)
/// boneless chicken" became "1225 g. (700 grams)" in metric — the whole number before a unicode
/// fraction was dropped from the quantity, the parenthesised equivalent was kept alongside the
/// converted value, the abbreviation's period was left dangling, and spoons turned into "2 ml salt".
/// </summary>
public sealed class UnitConverterImportedLineTests
{
    private static string ToMetric(string text) =>
        UnitConverter.ConvertText(text, MeasurementSystem.Imperial, MeasurementSystem.Metric);

    private static string ToImperial(string text) =>
        UnitConverter.ConvertText(text, MeasurementSystem.Metric, MeasurementSystem.Imperial);

    [Theory]
    [InlineData("1½ lbs. (700 grams) boneless chicken", "700 g boneless chicken")]
    [InlineData("½ cup (120 ml) Greek yogurt", "120 ml Greek yogurt")]
    [InlineData("1½ cups (180 grams) onions", "180 g onions")]
    [InlineData("1 lb. (450 grams) tomatoes", "450 g tomatoes")]
    [InlineData("1 lb (about 450 g) tomatoes", "450 g tomatoes")]
    public void ConvertText_WhenDualUnitLineToMetric_ShouldKeepOnlyTheGivenMetricEquivalent(string line, string expected)
    {
        // Act
        var converted = ToMetric(line);

        // Assert
        converted.Should().Be(expected);
    }

    [Theory]
    [InlineData("1½ lbs. (700 grams) boneless chicken", "1½ lb boneless chicken")]
    [InlineData("½ cup (120 ml) Greek yogurt", "½ cup Greek yogurt")]
    [InlineData("1½ cups (180 grams) onions", "1½ cups onions")]
    [InlineData("1 lb. (450 grams) tomatoes", "1 lb tomatoes")]
    [InlineData("700 g (1½ lbs.) chicken", "1½ lb chicken")]
    public void ConvertText_WhenDualUnitLineToImperial_ShouldKeepOnlyTheImperialValue(string line, string expected)
    {
        // Act
        var converted = UnitConverter.ConvertText(line, MeasurementSystem.Imperial, MeasurementSystem.Imperial);

        // Assert
        converted.Should().Be(expected);
    }

    [Fact]
    public void ConvertText_WhenDualUnitHasNoTargetEquivalent_ShouldConvertThePrimaryOnce()
    {
        // Arrange — both values are imperial, so neither can be kept for a metric reader.
        var line = "1 lb (16 oz) butter";

        // Act
        var converted = ToMetric(line);

        // Assert
        converted.Should().Be("455 g butter");
    }

    [Theory]
    [InlineData("Bake at 350°F (175°C) for 20 minutes", MeasurementSystem.Metric, "Bake at 175°C for 20 minutes")]
    [InlineData("Bake at 350°F (175°C) for 20 minutes", MeasurementSystem.Imperial, "Bake at 350°F for 20 minutes")]
    public void ConvertText_WhenDualTemperature_ShouldKeepTheTargetOne(string line, MeasurementSystem to, string expected)
    {
        // Act
        var converted = UnitConverter.ConvertText(line, MeasurementSystem.Imperial, to);

        // Assert
        converted.Should().Be(expected);
    }

    [Theory]
    [InlineData("1½ lbs boneless chicken", "680 g boneless chicken")]
    [InlineData("1½ cups flour", "355 ml flour")]
    [InlineData("1 ½ cups flour", "355 ml flour")]
    public void ConvertText_WhenWholeNumberPrecedesUnicodeFraction_ShouldConvertTheWholeQuantity(string line, string expected)
    {
        // Act
        var converted = ToMetric(line);

        // Assert
        converted.Should().Be(expected);
    }

    [Theory]
    [InlineData("1 lb. tomatoes", "455 g tomatoes")]
    [InlineData("8 oz. cream cheese", "225 g cream cheese")]
    [InlineData("2 lbs. (1 kg) potatoes", "1 kg potatoes")]
    public void ConvertText_WhenAbbreviationHasTrailingPeriod_ShouldNotLeaveTheDotBehind(string line, string expected)
    {
        // Act
        var converted = ToMetric(line);

        // Assert
        converted.Should().Be(expected);
    }

    [Fact]
    public void ConvertText_WhenAbbreviationEndsASentence_ShouldKeepTheFullStop()
    {
        // Arrange
        var step = "Add 1 lb. Stir well.";

        // Act
        var converted = ToMetric(step);

        // Assert
        converted.Should().Be("Add 455 g. Stir well.");
    }

    [Theory]
    [InlineData("½ to 1 cup milk", "120–235 ml milk")]
    [InlineData("1 to 1½ cups stock", "235–355 ml stock")]
    [InlineData("1-2 cups stock", "235–475 ml stock")]
    public void ConvertText_WhenRange_ShouldConvertBothEndsIntoOneUnit(string line, string expected)
    {
        // Act
        var converted = ToMetric(line);

        // Assert
        converted.Should().Be(expected);
    }

    [Fact]
    public void ConvertText_WhenTemperatureRange_ShouldConvertBothEnds()
    {
        // Act
        var converted = ToImperial("Bake at 180-200°C");

        // Assert
        converted.Should().Be("Bake at 355–390°F");
    }

    [Theory]
    [InlineData("½ to 1 teaspoon Kashmiri red chili powder")]
    [InlineData("1 to 1½ teaspoon cumin powder")]
    [InlineData("3 tablespoons oil")]
    [InlineData("¼ teaspoon turmeric")]
    [InlineData("1 tbsp. lemon juice")]
    public void ConvertText_WhenSpoonMeasure_ShouldKeepItInBothSystems(string line)
    {
        // Act
        var metric = ToMetric(line);
        var imperial = ToImperial(line);

        // Assert
        metric.Should().Be(line, because: "teaspoons and tablespoons are used in metric recipes too");
        imperial.Should().Be(line);
    }

    [Theory]
    [InlineData("1 cup hot water", "235 ml hot water")]
    [InlineData("½ cup heavy cream", "120 ml heavy cream")]
    [InlineData("200 g flour", "200 g flour")]
    public void ConvertText_WhenSingleUnit_ShouldRoundSensibly(string line, string expected)
    {
        // Act
        var converted = ToMetric(line);

        // Assert
        converted.Should().Be(expected);
    }

    // RecipeUnits.ForDisplay now runs the conversion even when the chosen target equals the
    // recipe's own SourceSystem, instead of returning the raw text unchanged. That relies on
    // ConvertText being idempotent for a same-system call: a dual-unit import line still needs
    // its duplicated value collapsed, while a plain line with nothing to collapse is untouched.
    [Theory]
    [InlineData("1½ lbs. (700 grams) boneless chicken", "1½ lb boneless chicken")]
    [InlineData("1 lb chicken breast", "1 lb chicken breast")]
    [InlineData("½ teaspoon salt", "½ teaspoon salt")]
    public void ConvertText_WhenTargetEqualsSourceSystem_ShouldStillCleanDualUnitsAndLeavePlainLinesUnchanged(string line, string expected)
    {
        // Act
        var converted = UnitConverter.ConvertText(line, MeasurementSystem.Imperial, MeasurementSystem.Imperial);

        // Assert
        converted.Should().Be(expected);
    }

    // Controller ruling: when a same-system call finds an amount that isn't in the chosen system —
    // here a per-can size noted in grams inside an otherwise-Imperial line — converting it to that
    // system is the correct, wanted behaviour (the user picked Imperial; every amount should read in
    // Imperial). "2 cans (400 g each)" is not a dual-equivalent bracket (the count "2 cans" has no
    // unit to pair with), so DualToken never touches it; the plain Token pass then converts the
    // "400 g" it finds inside the parens like any other off-system measurement.
    [Fact]
    public void ConvertText_WhenSameSystemLineHasAnOffSystemAmountInBrackets_ShouldConvertItToTheChosenSystem()
    {
        // Act
        var converted = UnitConverter.ConvertText("2 cans (400 g each) tomatoes", MeasurementSystem.Imperial, MeasurementSystem.Imperial);

        // Assert
        converted.Should().Be("2 cans (14 oz each) tomatoes");
    }

    // Cups are not treated as neutral the way tsp/tbsp are (Units: cup.System == Imperial). So even
    // inside a Metric-sourced recipe, a "cup" amount is off-system for a Metric target and converts —
    // same-system idempotence doesn't mean "leave every unit as written", it means "leave what's
    // already in the target system alone, normalise the rest".
    [Fact]
    public void ConvertText_WhenSameSystemLineHasAnOffSystemCup_ShouldConvertItToTheChosenSystem()
    {
        // Act
        var converted = UnitConverter.ConvertText("1 cup milk", MeasurementSystem.Metric, MeasurementSystem.Metric);

        // Assert
        converted.Should().Be("235 ml milk");
    }
}
