using FluentAssertions;
using PurePrep.Ai;

namespace PurePrep.Core.Tests.Ai;

/// <summary>
/// The translation directive is the fix for a tester report where an imported Romanian recipe stayed
/// in Romanian. It must be appended for supported languages and must be emphatic enough to override
/// the "structured data is authoritative" instruction that previously kept the source language.
/// </summary>
public sealed class GeminiSystemPromptTests
{
    [Theory]
    [InlineData("de", "German")]
    [InlineData("fr", "French")]
    [InlineData("es", "Spanish")]
    [InlineData("it", "Italian")]
    [InlineData("pl", "Polish")]
    [InlineData("nl", "Dutch")]
    [InlineData("en", "English")]
    public void AppendsTranslationDirective_ForSupportedLanguage(string code, string languageName)
    {
        var prompt = GeminiClient.BuildSystemPrompt(code);

        prompt.Should().Contain($"write the Title, Ingredients, and Steps entirely in {languageName}");
        prompt.Should().Contain("must still be translated");
        prompt.Should().Contain("must not contain words left in the source language");
    }

    [Fact]
    public void NormalizesRegionTag_ToBaseLanguage()
    {
        GeminiClient.BuildSystemPrompt("pl-PL")
            .Should().Contain("entirely in Polish");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("ro")] // unsupported UI language: no directive, model still translates via base prompt
    public void OmitsTranslationDirective_WhenNoSupportedLanguage(string? code)
    {
        var prompt = GeminiClient.BuildSystemPrompt(code);

        prompt.Should().NotContain("OUTPUT LANGUAGE");
    }
}
