using FluentAssertions;
using PurePrep.Application;

namespace PurePrep.Core.Tests.Application;

public sealed class VoiceCommandVocabularyTests
{
    [Theory]
    [InlineData("stop")]
    [InlineData("stop reading")]
    [InlineData("stopp")]
    [InlineData("przestań")]
    [InlineData("arrête")]
    public void TryMatch_WhenPhraseMeansStop_ShouldReturnStop(string phrase)
    {
        // Arrange — phrase from InlineData

        // Act
        var matched = VoiceCommandVocabulary.TryMatch(phrase, out var command);

        // Assert
        matched.Should().BeTrue();
        command.Should().Be(VoiceCommand.Stop);
    }

    [Fact]
    public void TryMatch_WhenPhraseIsRepeat_ShouldStillReturnRepeat()
    {
        // Arrange
        const string phrase = "read it again";

        // Act
        VoiceCommandVocabulary.TryMatch(phrase, out var command);

        // Assert
        command.Should().Be(VoiceCommand.Repeat);
    }

    [Fact]
    public void ExamplesFor_WhenPolish_ShouldIncludeStopWord()
    {
        // Arrange — "pl"

        // Act
        var phrases = VoiceCommandVocabulary.ExamplesFor("pl");

        // Assert
        phrases.Stop.Should().Be("stop");
    }
}
