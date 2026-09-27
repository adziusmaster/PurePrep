using System.Collections.ObjectModel;
using System.Collections.Specialized;
using FluentAssertions;
using PurePrep.Domain;

namespace PurePrep.Core.Tests.Domain;

public sealed class ObservableListSyncTests
{
    private static List<NotifyCollectionChangedAction> Record(ObservableCollection<string> target)
    {
        var actions = new List<NotifyCollectionChangedAction>();
        target.CollectionChanged += (_, e) => actions.Add(e.Action);
        return actions;
    }

    [Fact]
    public void SyncTo_WhenItemsFilteredOutAndReordered_ShouldMatchDesiredWithoutReset()
    {
        // Arrange
        var target = new ObservableCollection<string> { "A", "B", "C", "D" };
        var actions = Record(target);

        // Act
        target.SyncTo(["D", "B", "E"]);

        // Assert
        target.Should().Equal("D", "B", "E");
        actions.Should().NotContain(NotifyCollectionChangedAction.Reset);
    }

    [Fact]
    public void SyncTo_WhenDesiredEqualsCurrent_ShouldRaiseNoChanges()
    {
        // Arrange
        var target = new ObservableCollection<string> { "A", "B" };
        var actions = Record(target);

        // Act
        target.SyncTo(["A", "B"]);

        // Assert
        target.Should().Equal("A", "B");
        actions.Should().BeEmpty();
    }

    [Fact]
    public void SyncTo_WhenDesiredIsEmpty_ShouldRemoveItemsOneByOne()
    {
        // Arrange
        var target = new ObservableCollection<string> { "A", "B" };
        var actions = Record(target);

        // Act
        target.SyncTo([]);

        // Assert
        target.Should().BeEmpty();
        actions.Should().Equal(NotifyCollectionChangedAction.Remove, NotifyCollectionChangedAction.Remove);
    }

    [Fact]
    public void SyncTo_WhenTargetIsEmpty_ShouldInsertEveryItemInOrder()
    {
        // Arrange
        var target = new ObservableCollection<string>();
        var actions = Record(target);

        // Act
        target.SyncTo(["A", "B", "C"]);

        // Assert
        target.Should().Equal("A", "B", "C");
        actions.Should().OnlyContain(a => a == NotifyCollectionChangedAction.Add);
    }
    private static ParsedRecipe Recipe(string title) =>
        new() { Title = title, Ingredients = ["flour", "water"], Steps = [new RecipeStep { Order = 1, Instruction = "Mix." }] };

    private static List<NotifyCollectionChangedEventArgs> RecordEvents(ObservableCollection<ParsedRecipe> target)
    {
        var events = new List<NotifyCollectionChangedEventArgs>();
        target.CollectionChanged += (_, e) => events.Add(e);
        return events;
    }

    [Fact]
    public void SyncTo_WhenRecipeUpdatedViaWith_ShouldReplaceStaleInstanceInPlace()
    {
        // Arrange
        var a = Recipe("A");
        var b = Recipe("B");
        var target = new ObservableCollection<ParsedRecipe> { a, b };
        var updated = a.WithFavourite(true) with { Title = "A (edited)" };

        // Act
        target.SyncTo([updated, b]);

        // Assert
        target.Should().HaveCount(2);
        ReferenceEquals(target[0], updated).Should().BeTrue();
        ReferenceEquals(target[1], b).Should().BeTrue();
        target.Should().NotContain(r => ReferenceEquals(r, a));
    }

    [Fact]
    public void SyncTo_WhenOnlyAnotherRecipeChanges_ShouldKeepUnchangedInstanceUntouched()
    {
        // Arrange
        var a = Recipe("A");
        var b = Recipe("B");
        var target = new ObservableCollection<ParsedRecipe> { a, b };
        var events = RecordEvents(target);
        var updatedB = b.WithFavourite(true);

        // Act
        target.SyncTo([a, updatedB]);

        // Assert
        ReferenceEquals(target[0], a).Should().BeTrue();
        events.Should().NotContain(e =>
            (e.OldItems != null && e.OldItems.Cast<ParsedRecipe>().Any(r => ReferenceEquals(r, a))) ||
            (e.NewItems != null && e.NewItems.Cast<ParsedRecipe>().Any(r => ReferenceEquals(r, a))));
    }

    [Fact]
    public void SyncTo_WhenSortOrderChanges_ShouldMoveItemsWithoutReplacingThem()
    {
        // Arrange
        var a = Recipe("A");
        var b = Recipe("B");
        var c = Recipe("C");
        var target = new ObservableCollection<ParsedRecipe> { a, b, c };
        var events = RecordEvents(target);

        // Act
        target.SyncTo([c, a, b]);

        // Assert
        target.Select(r => r.Title).Should().Equal("C", "A", "B");
        ReferenceEquals(target[0], c).Should().BeTrue();
        ReferenceEquals(target[1], a).Should().BeTrue();
        ReferenceEquals(target[2], b).Should().BeTrue();
        events.Should().NotBeEmpty().And.OnlyContain(e => e.Action == NotifyCollectionChangedAction.Move);
    }
}
