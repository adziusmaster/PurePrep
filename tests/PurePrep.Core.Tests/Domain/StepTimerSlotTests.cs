using FluentAssertions;
using PurePrep.Domain;

namespace PurePrep.Core.Tests.Domain;

public sealed class StepTimerSlotTests
{
    private static readonly Guid RecipeId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

    private static CookTimerState Running(string label, int? stepIndex, int? timerIndex, Guid? recipeId = null) =>
        CookTimerState.Start(label, 600, Now) with { RecipeId = recipeId ?? RecipeId, StepIndex = stepIndex, TimerIndex = timerIndex };

    [Fact]
    public void FindRunning_WhenRenamedTimerRunsInSameSlot_ShouldMatchByTimerIndex()
    {
        // Arrange — "Bake" was renamed to "Oven" before starting; the slot index still ties them.
        var live = new[] { Running("Oven", stepIndex: 0, timerIndex: 2) };

        // Act
        var match = StepTimerSlot.FindRunning(live, RecipeId, stepIndex: 0, timerIndex: 2, label: "Bake");

        // Assert
        match.Should().BeSameAs(live[0]);
    }

    [Fact]
    public void FindRunning_WhenSameLabelRunsInAnotherSlot_ShouldNotMatch()
    {
        // Arrange — two "Boil" timers in one step are different slots.
        var live = new[] { Running("Boil", stepIndex: 0, timerIndex: 0) };

        // Act
        var match = StepTimerSlot.FindRunning(live, RecipeId, stepIndex: 0, timerIndex: 3, label: "Boil");

        // Assert
        match.Should().BeNull();
    }

    [Fact]
    public void FindRunning_WhenLegacyTimerHasNoIndex_ShouldFallBackToLabel()
    {
        // Arrange — restored from JSON written before TimerIndex existed.
        var live = new[] { Running("Boil", stepIndex: 0, timerIndex: null) };

        // Act
        var match = StepTimerSlot.FindRunning(live, RecipeId, stepIndex: 0, timerIndex: 1, label: "Boil");

        // Assert
        match.Should().BeSameAs(live[0]);
    }

    [Fact]
    public void FindRunning_WhenTimerBelongsToAnotherRecipeOrStep_ShouldNotMatch()
    {
        // Arrange
        var live = new[]
        {
            Running("Boil", stepIndex: 1, timerIndex: 0),
            Running("Boil", stepIndex: 0, timerIndex: 0, recipeId: Guid.NewGuid()),
        };

        // Act
        var match = StepTimerSlot.FindRunning(live, RecipeId, stepIndex: 0, timerIndex: 0, label: "Boil");

        // Assert
        match.Should().BeNull();
    }
}
