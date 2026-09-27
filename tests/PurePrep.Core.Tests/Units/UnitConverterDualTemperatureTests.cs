using FluentAssertions;
using PurePrep.Domain;
using PurePrep.Units;

namespace PurePrep.Core.Tests.Units;

/// <summary>
/// UK recipes often give the oven temperature in both scales ("375°F/190°C/Gas Mark 5", "180°C
/// (350°F)"). Converting each half on its own printed the target value twice ("375°F/375°F/");
/// an already-dual temperature must collapse to the one value in the target system.
/// </summary>
public sealed class UnitConverterDualTemperatureTests
{
    private const string Muffins = "Preheat the oven to 375°F/190°C/Gas Mark 5, no fan.";

    [Theory]
    [InlineData(MeasurementSystem.Imperial, "Preheat the oven to 375°F / Gas Mark 5, no fan.")]
    [InlineData(MeasurementSystem.Metric, "Preheat the oven to 190°C / Gas Mark 5, no fan.")]
    public void ConvertText_WhenSlashSeparatedDualTemperature_ShouldKeepOnlyTheTargetValue(MeasurementSystem target, string expected)
    {
        // Arrange — inputs come from InlineData.

        // Act
        var converted = UnitConverter.ConvertText(Muffins, MeasurementSystem.Imperial, target);

        // Assert
        converted.Should().Be(expected);
    }

    [Theory]
    [InlineData("Bake at 180°C (350°F) for 20 minutes.", MeasurementSystem.Imperial, "Bake at 350°F for 20 minutes.")]
    [InlineData("Bake at 180°C (350°F) for 20 minutes.", MeasurementSystem.Metric, "Bake at 180°C for 20 minutes.")]
    [InlineData("Bake at 350°F (180°C) for 20 minutes.", MeasurementSystem.Imperial, "Bake at 350°F for 20 minutes.")]
    [InlineData("Bake at 350°F (180°C) for 20 minutes.", MeasurementSystem.Metric, "Bake at 180°C for 20 minutes.")]
    [InlineData("Bake at 200°C/400°F.", MeasurementSystem.Imperial, "Bake at 400°F.")]
    public void ConvertText_WhenDualTemperature_ShouldKeepOnlyTheTargetValue(string text, MeasurementSystem target, string expected)
    {
        // Arrange — inputs come from InlineData.

        // Act
        var converted = UnitConverter.ConvertText(text, MeasurementSystem.Metric, target);

        // Assert
        converted.Should().Be(expected);
    }

    [Theory]
    [InlineData("200g/7oz butter", MeasurementSystem.Imperial, "7 oz butter")]
    [InlineData("200g/7oz butter", MeasurementSystem.Metric, "200 g butter")]
    public void ConvertText_WhenSlashSeparatedDualMass_ShouldKeepOnlyTheTargetValue(string text, MeasurementSystem target, string expected)
    {
        // Arrange — inputs come from InlineData.

        // Act
        var converted = UnitConverter.ConvertText(text, MeasurementSystem.Metric, target);

        // Assert
        converted.Should().Be(expected);
    }

    [Fact]
    public void ConvertText_WhenSlashJoinsTwoSpoonMeasures_ShouldLeaveTextUnchanged()
    {
        // Arrange
        const string text = "1 tbsp/1 tsp oil";

        // Act
        var converted = UnitConverter.ConvertText(text, MeasurementSystem.Metric, MeasurementSystem.Imperial);

        // Assert
        converted.Should().Be(text);
    }

    [Theory]
    [InlineData("1 tsp/5ml vanilla", MeasurementSystem.Imperial, "1 tsp vanilla")]
    [InlineData("1 tsp/5ml vanilla", MeasurementSystem.Metric, "5 ml vanilla")]
    [InlineData("2 tbsp/30ml oil", MeasurementSystem.Imperial, "2 tbsp oil")]
    [InlineData("2 tbsp/30ml oil", MeasurementSystem.Metric, "30 ml oil")]
    public void ConvertText_WhenSpoonSlashMillilitres_ShouldKeepOneValue(string text, MeasurementSystem target, string expected)
    {
        // Arrange — the spoon is valid in both systems, the ml half is metric.

        // Act
        var converted = UnitConverter.ConvertText(text, MeasurementSystem.Metric, target);

        // Assert
        converted.Should().Be(expected);
    }

    [Theory]
    [InlineData("Line a 9-inch/23cm tin.", MeasurementSystem.Imperial, "Line a 9 inch tin.")]
    [InlineData("Line a 9-inch/23cm tin.", MeasurementSystem.Metric, "Line a 23 cm tin.")]
    [InlineData("Use a 20cm/8in tin.", MeasurementSystem.Imperial, "Use a 8 inch tin.")]
    [InlineData("Use a 20cm/8in tin.", MeasurementSystem.Metric, "Use a 20 cm tin.")]
    public void ConvertText_WhenDualTinSize_ShouldKeepOnlyTheTargetValue(string text, MeasurementSystem target, string expected)
    {
        // Arrange — "9-inch" is hyphenated and "8in" uses the short alias glued to the number.

        // Act
        var converted = UnitConverter.ConvertText(text, MeasurementSystem.Metric, target);

        // Assert
        converted.Should().Be(expected);
    }

    [Theory]
    [InlineData("Cook in 2 batches, 5 in each.")]
    [InlineData("Fold in 2 eggs.")]
    public void ConvertText_WhenInIsAWord_ShouldNotReadItAsInches(string text)
    {
        // Arrange — "in" is only an inch after a number with nothing between them.

        // Act
        var converted = UnitConverter.ConvertText(text, MeasurementSystem.Imperial, MeasurementSystem.Metric);

        // Assert
        converted.Should().Be(text);
    }

    [Theory]
    [InlineData("1-1/2 cups milk", MeasurementSystem.Imperial)]
    [InlineData("1-1/2 tsp salt", MeasurementSystem.Metric)]
    public void ConvertText_WhenHyphenatedMixedNumberIsNotConverted_ShouldKeepItAsWritten(string text, MeasurementSystem target)
    {
        // Arrange — cups are already imperial; teaspoons are never converted.

        // Act
        var converted = UnitConverter.ConvertText(text, MeasurementSystem.Imperial, target);

        // Assert
        converted.Should().Be(text);
    }
}
