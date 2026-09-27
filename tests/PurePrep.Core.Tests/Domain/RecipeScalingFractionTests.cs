using FluentAssertions;
using PurePrep.Domain;

namespace PurePrep.Core.Tests.Domain;

/// <summary>
/// Scaled amounts for non-metric/count units and spoons render as common cooking fractions
/// ("¼ cup", "1½ cup") instead of decimals ("0.25 cup", "1.5 cup"). Metric mass/volume (g, kg,
/// ml, l) keep the existing rounded decimal formatting, since nobody measures "⅓ kg" on a scale.
/// </summary>
public sealed class RecipeScalingFractionTests
{
    [Theory]
    [InlineData("1 cup flour", 0.125, "⅛ cup flour")]
    [InlineData("1 cup flour", 0.25, "¼ cup flour")]
    [InlineData("1 cup flour", 1d / 3, "⅓ cup flour")]
    [InlineData("1 cup flour", 0.5, "½ cup flour")]
    [InlineData("1 cup flour", 2d / 3, "⅔ cup flour")]
    [InlineData("1 cup flour", 0.75, "¾ cup flour")]
    public void Scale_WhenResultIsANiceFraction_ShouldRenderTheUnicodeFraction(string line, double factor, string expected)
    {
        // Act
        var scaled = RecipeScaling.Scale(line, factor);

        // Assert
        scaled.Should().Be(expected);
    }

    [Theory]
    [InlineData("1 cup flour", 1.5, "1½ cup flour")]
    [InlineData("1 cup flour", 2.25, "2¼ cup flour")]
    public void Scale_WhenResultIsAMixedNumber_ShouldRenderWholePlusFraction(string line, double factor, string expected)
    {
        // Act
        var scaled = RecipeScaling.Scale(line, factor);

        // Assert
        scaled.Should().Be(expected);
    }

    [Theory]
    [InlineData("1 g nutmeg", 0.025, "0.1 g nutmeg")]
    [InlineData("2 ml vanilla", 0.01, "0.1 ml vanilla")]
    public void Scale_WhenTinyMetricAmount_ShouldNeverRenderZero(string line, double factor, string expected)
    {
        // Act
        var scaled = RecipeScaling.Scale(line, factor);

        // Assert
        scaled.Should().Be(expected);
    }

    [Fact]
    public void Scale_WhenHalvingAQuarterCup_ShouldRenderAnEighth()
    {
        // Act
        var scaled = RecipeScaling.Scale("½ cup Greek yogurt", 0.5);

        // Assert
        scaled.Should().Be("¼ cup Greek yogurt");
    }

    [Fact]
    public void Scale_WhenUnitTextIsNotTouched_ShouldKeepExistingPluralisationBehaviour()
    {
        // Arrange — RecipeScaling has never pluralised unit words; only the quantity formatting
        // changes here (decimal -> fraction). "cup" stays "cup" whatever the resulting amount is.
        // Act
        var scaled = RecipeScaling.Scale("1 cup sugar", 1.5);

        // Assert
        scaled.Should().Be("1½ cup sugar");
    }

    [Theory]
    [InlineData("200 g flour", 1.5, "300 g flour")]
    [InlineData("1 kg sugar", 0.5, "0.5 kg sugar")]
    [InlineData("100 ml milk", 1.25, "125 ml milk")]
    public void Scale_WhenUnitIsMetric_ShouldKeepDecimalFormattingNotFractions(string line, double factor, string expected)
    {
        // Act
        var scaled = RecipeScaling.Scale(line, factor);

        // Assert
        scaled.Should().Be(expected);
    }

    [Theory]
    [InlineData("1 teaspoon salt", 1.5, "1½ teaspoon salt")]
    [InlineData("2 tablespoons oil", 0.75, "1½ tablespoons oil")]
    public void Scale_WhenUnitIsASpoon_ShouldRenderFractions(string line, double factor, string expected)
    {
        // Act
        var scaled = RecipeScaling.Scale(line, factor);

        // Assert
        scaled.Should().Be(expected);
    }

    [Fact]
    public void Scale_WhenNoUnitFollowsACount_ShouldStillRenderFractionsForNonWholeResults()
    {
        // Act
        var scaled = RecipeScaling.Scale("1 avocado", 0.5);

        // Assert
        scaled.Should().Be("½ avocado");
    }

    // Was "0.4 cup flour" (a raw decimal fallback) before cook-friendly rounding: a spoon/cup amount
    // that isn't a quarter/third/half now snaps to the nearest eighth instead of showing a decimal.
    [Fact]
    public void Scale_WhenResultIsNotCloseToANiceFraction_ShouldSnapToTheNearestEighth()
    {
        // Act
        var scaled = RecipeScaling.Scale("1 cup flour", 0.4);

        // Assert
        scaled.Should().Be("⅜ cup flour");
    }

    // Non-English cooking units (Polish, German, ...) get the same fraction treatment as English
    // ones; the unit word itself is never inflected by RecipeScaling (no pluralisation/declension
    // logic exists here or before this change), so "szklanki"/"łyżka"/"EL" stay exactly as written.
    [Theory]
    [InlineData("½ szklanki mąki", 0.5, "¼ szklanki mąki")]
    [InlineData("1 łyżka oliwy", 1.5, "1½ łyżka oliwy")]
    [InlineData("2 EL Öl", 0.25, "½ EL Öl")]
    [InlineData("100 g mąki", 1.5, "150 g mąki")]
    public void Scale_WhenUnitIsNonEnglish_ShouldRenderFractionsWithoutInflectingTheUnitWord(string line, double factor, string expected)
    {
        // Act
        var scaled = RecipeScaling.Scale(line, factor);

        // Assert
        scaled.Should().Be(expected);
    }

    // --- Cook-friendly rounding (758.33 g / 0.58 tsp bug) ------------------------------------

    // Metric mass/volume never renders fractions or raw decimals beyond a cook's tolerance: below
    // 10 it keeps one decimal (dropping a trailing ".0"), 10-100 rounds to the nearest whole unit,
    // 100-1000 rounds to the nearest 5, and 1000+ rounds to the nearest 10.
    [Theory]
    [InlineData("6 g salt", 1.25, "7.5 g salt")]
    [InlineData("6 g salt", 4d / 3, "8 g salt")]
    [InlineData("50 ml oil", 7d / 6, "58 ml oil")]
    [InlineData("650 g potatoes", 7d / 6, "760 g potatoes")]
    [InlineData("500 g flour", 7d / 6, "585 g flour")]
    [InlineData("250 ml water", 7d / 6, "290 ml water")]
    [InlineData("1000 g stock", 1.1667, "1170 g stock")]
    public void Scale_WhenUnitIsMetric_ShouldRoundToCookFriendlyPrecision(string line, double factor, string expected)
    {
        // Act
        var scaled = RecipeScaling.Scale(line, factor);

        // Assert
        scaled.Should().Be(expected);
    }

    // Imperial weight/volume units (oz, lb, cup, fl oz, …) snap to the nearest eighth (the finest a
    // set of measuring cups actually offers) instead of a raw decimal, using the fraction glyph set
    // extended with ⅜ ⅝ ⅞. A nonzero amount never collapses to "0" — ⅛ is the smallest amount ever
    // shown. Spoon units are excluded from this eighth-rounding — see the tests below.
    [Theory]
    [InlineData("2 oz butter", 0.7, "1⅜ oz butter")]
    [InlineData("1 lb flour", 0.05, "⅛ lb flour")]
    public void Scale_WhenUnitIsImperial_ShouldSnapToTheNearestEighth(string line, double factor, string expected)
    {
        // Act
        var scaled = RecipeScaling.Scale(line, factor);

        // Assert
        scaled.Should().Be(expected);
    }

    // Spoons (tsp/tbsp and non-English spoon words) snap only to the six common kitchen fractions a
    // real measuring-spoon set is marked with (⅛ ¼ ⅓ ½ ⅔ ¾) — never to an arbitrary eighth like ⅝.
    // On an exact tie between two candidates, the amount rounds down (safer for salt and spices).
    [Theory]
    // 0.5 tsp x 7/6 = 0.58333, exactly between ½ (1/12 away) and ⅔ (1/12 away) — this is the
    // original bug report: it must resolve to ½, not ⅝.
    [InlineData("½ tsp salt", 7d / 6, "½ tsp salt")]
    // 2 tbsp x 7/6 = 2⅓ exactly — an existing nice-fraction match, confirming it is untouched.
    [InlineData("2 tbsp butter", 7d / 6, "2⅓ tbsp butter")]
    // 0.7 is closer to ⅔ (0.0333 away) than ¾ (0.05 away) — nearest-fraction, not eighth-rounded.
    [InlineData("1 tsp cinnamon", 0.7, "⅔ tsp cinnamon")]
    // 0.1875 sits exactly between ⅛ and ¼ — another exact tie, also rounds down.
    [InlineData("1 tsp herb", 0.1875, "⅛ tsp herb")]
    // A tiny amount never collapses to "0" — ⅛ is the smallest a spoon amount is ever shown as.
    [InlineData("1 tsp cinnamon", 0.05, "⅛ tsp cinnamon")]
    // Non-English spoon word, same tie-break-down rule as the Polish sample recipe.
    [InlineData("½ łyżeczki soli", 7d / 6, "½ łyżeczki soli")]
    public void Scale_WhenUnitIsASpoon_ShouldSnapToTheNearestCommonKitchenFraction(string line, double factor, string expected)
    {
        // Act
        var scaled = RecipeScaling.Scale(line, factor);

        // Assert
        scaled.Should().Be(expected);
    }

    // Bare counts (no recognised unit — eggs, onions, avocados) round to the nearest half rather
    // than the nearest eighth: nobody cuts an onion into eighths.
    [Theory]
    [InlineData("2 eggs", 7d / 6, "2½ eggs")]
    [InlineData("1 onion", 7d / 6, "1 onion")]
    [InlineData("1 onion", 0.05, "½ onion")]
    public void Scale_WhenNoUnitFollowsACount_ShouldSnapToTheNearestHalf(string line, double factor, string expected)
    {
        // Act
        var scaled = RecipeScaling.Scale(line, factor);

        // Assert
        scaled.Should().Be(expected);
    }
}
