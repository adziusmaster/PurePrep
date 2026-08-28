using FluentAssertions;
using PurePrep.Domain;

namespace PurePrep.Core.Tests.Domain;

/// <summary>
/// Combining ingredient lines into a shopping list. The value is in the merging: a list that shows
/// "200 g flour" and "100 g flour" as two lines is worse than no list at all, because you have to
/// do the arithmetic in the shop.
/// </summary>
public sealed class ShoppingListTests
{
    [Fact]
    public void Add_ToAnEmptyList_ShouldKeepTheLinesAsWritten()
    {
        // Act
        var list = ShoppingList.Add([], ["200 g flour", "2 eggs"], "Pancakes");

        // Assert
        list.Select(i => i.Text).Should().Equal("200 g flour", "2 eggs");
    }

    [Fact]
    public void Add_ShouldRecordWhichRecipeEachLineCameFrom()
    {
        // Arrange — in the shop you need to know what a stray item was for.
        // Act
        var list = ShoppingList.Add([], ["200 g flour"], "Pancakes");

        // Assert
        list.Single().Source.Should().Be("Pancakes");
    }

    [Fact]
    public void Add_TheSameIngredientInTheSameUnit_ShouldCombineTheQuantities()
    {
        // Arrange
        var list = ShoppingList.Add([], ["200 g flour"], "Pancakes");

        // Act
        var combined = ShoppingList.Add(list, ["100 g flour"], "Bread");

        // Assert
        combined.Should().ContainSingle();
        combined.Single().Text.Should().Be("300 g flour");
    }

    [Fact]
    public void Add_TheSameIngredientInADifferentUnit_ShouldKeepThemSeparate()
    {
        // Arrange — 200 g and 1 cup of flour cannot be summed without a density guess.
        var list = ShoppingList.Add([], ["200 g flour"], "Pancakes");

        // Act
        var combined = ShoppingList.Add(list, ["1 cup flour"], "Bread");

        // Assert
        combined.Should().HaveCount(2);
    }

    [Fact]
    public void Add_ShouldMatchIngredientNamesRegardlessOfCase()
    {
        // Arrange
        var list = ShoppingList.Add([], ["200 g Flour"], "Pancakes");

        // Act
        var combined = ShoppingList.Add(list, ["100 g flour"], "Bread");

        // Assert
        combined.Should().ContainSingle();
    }

    [Fact]
    public void Add_AnIngredientWithNoQuantity_ShouldNotBeDuplicated()
    {
        // Arrange
        var list = ShoppingList.Add([], ["salt to taste"], "Pancakes");

        // Act
        var combined = ShoppingList.Add(list, ["salt to taste"], "Bread");

        // Assert
        combined.Should().ContainSingle();
    }

    [Fact]
    public void Add_WhenAnItemIsAlreadyTicked_ShouldUntickItSoTheExtraIsNotMissed()
    {
        // Arrange — you already bought 200 g, now another recipe needs 100 g more.
        var list = ShoppingList.Add([], ["200 g flour"], "Pancakes")
            .Select(i => i with { IsChecked = true }).ToList();

        // Act
        var combined = ShoppingList.Add(list, ["100 g flour"], "Bread");

        // Assert
        combined.Single().IsChecked.Should().BeFalse();
        combined.Single().Text.Should().Be("300 g flour");
    }

    [Fact]
    public void Add_ShouldIgnoreBlankLines()
    {
        // Act
        var list = ShoppingList.Add([], ["200 g flour", "   ", ""], "Pancakes");

        // Assert
        list.Should().ContainSingle();
    }

    [Fact]
    public void Add_ShouldCombineFractionalQuantities()
    {
        // Arrange
        var list = ShoppingList.Add([], ["1/2 tsp salt"], "A");

        // Act
        var combined = ShoppingList.Add(list, ["1/2 tsp salt"], "B");

        // Assert
        combined.Single().Text.Should().Be("1 tsp salt");
    }

    [Fact]
    public void RemoveChecked_ShouldKeepOnlyWhatIsStillNeeded()
    {
        // Arrange
        var list = ShoppingList.Add([], ["200 g flour", "2 eggs"], "Pancakes");
        var ticked = list.Select(i => i.Text.Contains("eggs") ? i with { IsChecked = true } : i).ToList();

        // Act
        var remaining = ShoppingList.RemoveChecked(ticked);

        // Assert
        remaining.Should().ContainSingle().Which.Text.Should().Be("200 g flour");
    }
}
