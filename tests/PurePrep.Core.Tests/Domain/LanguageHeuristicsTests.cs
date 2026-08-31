using FluentAssertions;
using PurePrep.Domain;

namespace PurePrep.Core.Tests.Domain;

/// <summary>
/// Offline language detection preselects the translation source. A tester imported a Romanian recipe
/// that was mistaken for English (detection returned null, callers defaulted to "en"), so Romanian is
/// now a recognised source language and unrelated text still returns null rather than a noisy guess.
/// </summary>
public sealed class LanguageHeuristicsTests
{
    [Fact]
    public void DetectsRomanianRecipeText()
    {
        const string text =
            "Amestecați făina cu sarea și adăugați uleiul, apoi fierbeți până se rumenește. " +
            "Adăugați piper și puțin zahăr.";

        LanguageHeuristics.Detect(text).Should().Be("ro");
    }

    [Fact]
    public void DetectsEnglishRecipeText()
    {
        const string text = "Add the flour and salt, then stir into the oil until smooth for 5 minutes.";

        LanguageHeuristics.Detect(text).Should().Be("en");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("xyz qwerty")] // too few tokens / no markers -> unknown, callers must not assume "en"
    public void ReturnsNull_WhenSignalIsInsufficient(string? text)
    {
        LanguageHeuristics.Detect(text).Should().BeNull();
    }
}
