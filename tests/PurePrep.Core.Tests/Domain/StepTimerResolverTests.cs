using FluentAssertions;
using PurePrep.Domain;

namespace PurePrep.Core.Tests.Domain;

public sealed class StepTimerResolverTests
{
    [Fact]
    public void Resolve_WhenStepHasStructuredTimers_ShouldReturnThemUnchanged()
    {
        // Arrange
        var step = new RecipeStep { Order = 1, Instruction = "Fry the onion for 10-12 mins.", Timers = [new RecipeTimer("Fry onion", 600, 720)] };

        // Act
        var timers = StepTimerResolver.Resolve(step);

        // Assert
        timers.Should().ContainSingle().Which.Should().Be(new RecipeTimer("Fry onion", 600, 720));
    }

    [Fact]
    public void Detect_WhenInstructionHasRange_ShouldReturnRangeTimer()
    {
        // Arrange
        const string text = "Heat the oil and fry the onion gently for 10-12 mins.";

        // Act
        var timers = StepTimerResolver.Detect(text);

        // Assert
        var timer = timers.Should().ContainSingle().Subject;
        timer.MinSeconds.Should().Be(600);
        timer.MaxSeconds.Should().Be(720);
        timer.Label.Should().Be("Fry the onion gently");
    }

    [Fact]
    public void Detect_WhenBorrowedClauseHasUntilAndPrepositions_ShouldCutThemBeforeCounting()
    {
        // Arrange
        const string text = "Meanwhile, boil the peeled potatoes in salted water until tender, about 20 minutes, then drain well.";

        // Act
        var timers = StepTimerResolver.Detect(text);

        // Assert
        timers.Should().ContainSingle().Which.Should().Be(new RecipeTimer("Boil the peeled potatoes", 1200, 1200));
    }

    [Fact]
    public void Detect_WhenWordsBeforeDurationAreLong_ShouldUseTheFirstThreeVerbLedWords()
    {
        // Arrange
        const string text = "Marinate the chicken in the fridge for at least 2 hours.";

        // Act
        var timers = StepTimerResolver.Detect(text);

        // Assert
        timers.Should().ContainSingle().Which.Should().Be(new RecipeTimer("Marinate the chicken", 7200, 7200));
    }

    [Fact]
    public void Detect_WhenDurationsStandAloneBetweenCommas_ShouldLabelFromThePreviousClauseAndStayDistinct()
    {
        // Arrange
        const string text = "Boil the potatoes until soft, about 20 minutes, then simmer the sauce, about 10 minutes, then serve.";

        // Act
        var timers = StepTimerResolver.Detect(text);

        // Assert
        timers.Should().Equal(new RecipeTimer("Boil the potatoes", 1200, 1200), new RecipeTimer("Simmer the sauce", 600, 600));
    }

    [Fact]
    public void Detect_WhenPreviousClauseStartsWithMeanwhile_ShouldDropTheConnector()
    {
        // Arrange
        const string text = "Meanwhile, boil the potatoes, about 20 minutes.";

        // Act
        var timers = StepTimerResolver.Detect(text);

        // Assert
        timers.Should().ContainSingle().Which.Label.Should().Be("Boil the potatoes");
    }

    [Fact]
    public void Detect_WhenTwoClausesYieldTheSameLabel_ShouldFallBackToTheNextSourceForTheLaterTimer()
    {
        // Arrange
        const string text = "Stir for 2 minutes. Stir for 5 minutes until thick.";

        // Act
        var timers = StepTimerResolver.Detect(text);

        // Assert
        timers.Select(t => t.Label).Should().Equal("Stir", "Thick");
    }

    [Fact]
    public void Detect_WhenStepHasTwoDurations_ShouldLabelEachFromItsOwnClause()
    {
        // Arrange
        const string text = "Make the dough: mix the flour with the warm water, egg and salt, then knead for about 8 minutes until smooth. Cover and leave to rest for 30 minutes.";

        // Act
        var timers = StepTimerResolver.Detect(text);

        // Assert
        timers.Should().Equal(new RecipeTimer("Knead", 480, 480), new RecipeTimer("Leave to rest", 1800, 1800));
    }

    [Fact]
    public void Detect_WhenDurationStartsTheClause_ShouldLabelFromWordsAfterIt()
    {
        // Arrange
        const string text = "10 minutes later, stir the sauce.";

        // Act
        var timers = StepTimerResolver.Detect(text);

        // Assert
        timers.Should().ContainSingle().Which.Should().Be(new RecipeTimer("Later", 600, 600));
    }

    [Fact]
    public void Detect_WhenPolishStepHasTwoDurations_ShouldDropPolishFillersAndConnectors()
    {
        // Arrange
        const string text = "Gotuj ziemniaki przez 20 minut, potem odstaw na 10 minut.";

        // Act
        var timers = StepTimerResolver.Detect(text);

        // Assert
        timers.Should().Equal(new RecipeTimer("Gotuj ziemniaki", 1200, 1200), new RecipeTimer("Odstaw", 600, 600));
    }

    [Fact]
    public void Detect_WhenDurationHasDecimalPoint_ShouldNotSplitTheClauseInsideTheNumber()
    {
        // Arrange
        const string text = "Simmer gently for 1.5 hours.";

        // Act
        var timers = StepTimerResolver.Detect(text);

        // Assert
        timers.Should().ContainSingle().Which.Should().Be(new RecipeTimer("Simmer gently", 5400, 5400));
    }

    [Theory]
    [InlineData("Simmer for 20 minutes.", 1200, 1200)]
    [InlineData("Bake 25–30 min until golden.", 1500, 1800)]
    [InlineData("Rest for 1 hour.", 3600, 3600)]
    // Ported from the retired StepTimersTests (StepTimers.Detect): every case there found a single
    // plain duration, so min == max == the old TotalSeconds.
    [InlineData("Bake for 20 min until golden.", 1200, 1200)]
    [InlineData("Leave 30 mins to cool.", 1800, 1800)]
    [InlineData("Simmer for 1.5 hours.", 5400, 5400)]
    [InlineData("Rest 45 seconds before serving.", 45, 45)]
    [InlineData("Lăsați 30 de minute să se răcească.", 1800, 1800)] // Romanian filler "de"
    [InlineData("Cuocere per 2 di ore.", 7200, 7200)]               // Italian filler "di"
    public void Detect_ForCommonPhrasings_ShouldParseDurations(string text, int min, int max)
    {
        // Arrange — text from InlineData

        // Act
        var timer = StepTimerResolver.Detect(text).Single();

        // Assert
        timer.MinSeconds.Should().Be(min);
        timer.MaxSeconds.Should().Be(max);
    }

    // Ported from StepTimersTests.DetectsDurationsWithFillerWords: this instruction contains two
    // separate durations (10 minutes, then 15 minutes via English filler "of"), so it can't use the
    // single-timer theory above — the old test asserted only that the second duration was found.
    [Fact]
    public void Detect_WhenInstructionHasMultipleDurationsWithFillerWords_ShouldFindEachOne()
    {
        // Arrange
        const string text = "Leave a couple of 10 minutes.. wait 15 of minutes.";

        // Act
        var timers = StepTimerResolver.Detect(text);

        // Assert
        timers.Should().Contain(t => t.MinSeconds == 900 && t.MaxSeconds == 900);
    }

    [Fact]
    public void Detect_WhenRangeIsReversed_ShouldOrderMinBeforeMax()
    {
        // Arrange
        const string text = "Cook 12-10 minutes.";

        // Act
        var timer = StepTimerResolver.Detect(text).Single();

        // Assert
        timer.MinSeconds.Should().Be(600);
        timer.MaxSeconds.Should().Be(720);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Season to taste.")]
    [InlineData("Season the onion to taste.")] // Ported from StepTimersTests.ReturnsEmpty_WhenNoDuration
    public void Detect_WhenNoDuration_ShouldReturnEmpty(string? text)
    {
        // Arrange — text from InlineData

        // Act
        var timers = StepTimerResolver.Detect(text);

        // Assert
        timers.Should().BeEmpty();
    }
}
