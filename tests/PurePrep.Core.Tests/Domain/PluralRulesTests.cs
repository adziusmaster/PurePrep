using FluentAssertions;
using PurePrep.Domain;

namespace PurePrep.Core.Tests.Domain;

public sealed class PluralRulesTests
{
    [Theory]
    [InlineData("en", 1, PluralCategory.One)]
    [InlineData("en", 2, PluralCategory.Other)]
    [InlineData("en", 0, PluralCategory.Other)]
    [InlineData("de", 1, PluralCategory.One)]
    [InlineData("nl", 11, PluralCategory.Other)]
    [InlineData("fr", 0, PluralCategory.One)]
    [InlineData("fr", 2, PluralCategory.Other)]
    public void For_WhenOneOtherLanguage_ShouldSplitSingularFromPlural(string language, int count, PluralCategory expected)
    {
        // Arrange — inputs come from InlineData.

        // Act
        var category = PluralRules.For(language, count);

        // Assert
        category.Should().Be(expected);
    }

    [Theory]
    [InlineData(1, PluralCategory.One)]
    [InlineData(2, PluralCategory.Few)]
    [InlineData(4, PluralCategory.Few)]
    [InlineData(22, PluralCategory.Few)]
    [InlineData(104, PluralCategory.Few)]
    [InlineData(5, PluralCategory.Other)]
    [InlineData(12, PluralCategory.Other)]
    [InlineData(14, PluralCategory.Other)]
    [InlineData(21, PluralCategory.Other)]
    [InlineData(0, PluralCategory.Other)]
    public void For_WhenPolish_ShouldUseOneFewAndMany(int count, PluralCategory expected)
    {
        // Arrange — inputs come from InlineData.

        // Act
        var category = PluralRules.For("pl", count);

        // Assert
        category.Should().Be(expected);
    }

    [Theory]
    [InlineData("pl-PL")]
    [InlineData("PL")]
    public void For_WhenRegionalOrUpperCaseCode_ShouldUseTheLanguageRule(string language)
    {
        // Arrange — inputs come from InlineData.

        // Act
        var category = PluralRules.For(language, 3);

        // Assert
        category.Should().Be(PluralCategory.Few);
    }
}
