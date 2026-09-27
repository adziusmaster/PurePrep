using FluentAssertions;
using PurePrep.Domain;

namespace PurePrep.Core.Tests.Domain;

public sealed class RecipeMergeTests
{
    [Fact]
    public void KeepStoredPhoto_WhenEditedCopyPredatesAttachedPhoto_ShouldKeepStoredPath()
    {
        // Arrange — the detail page holds the copy returned by the import, before the photo attached.
        var imported = new ParsedRecipe { Title = "Pasta" };
        var stored = imported.WithImage("images/p.jpg");
        var edited = imported.WithNotes("Less salt");

        // Act
        var merged = RecipeMerge.KeepStoredPhoto(edited, stored);

        // Assert
        merged.ImagePath.Should().Be("images/p.jpg");
        merged.Notes.Should().Be("Less salt");
    }

    [Fact]
    public void KeepStoredPhoto_WhenStoredHasNoPhoto_ShouldNotResurrectAStaleOne()
    {
        // Arrange — the photo was removed in the editor; a stale copy still carries the old path.
        var stored = new ParsedRecipe { Title = "Pasta" };
        var stale = stored.WithImage("images/p.jpg").WithFavourite(true);

        // Act
        var merged = RecipeMerge.KeepStoredPhoto(stale, stored);

        // Assert
        merged.ImagePath.Should().BeNull();
        merged.IsFavourite.Should().BeTrue();
    }

    [Fact]
    public void KeepStoredPhoto_WhenNoStoredCopy_ShouldReturnEditedUnchanged()
    {
        // Arrange
        var edited = new ParsedRecipe { Title = "Pasta" }.WithImage("images/p.jpg");

        // Act
        var merged = RecipeMerge.KeepStoredPhoto(edited, null);

        // Assert
        merged.Should().BeSameAs(edited);
    }

    [Fact]
    public void Reimport_WhenReplacingExisting_ShouldCarryIdentityAndCookData()
    {
        // Arrange
        var existing = new ParsedRecipe { Title = "Old", Servings = 4, SavedAt = DateTimeOffset.UnixEpoch }
            .WithNotes("Use less chili").WithFavourite(true).WithChosenServings(6)
            .MarkCooked(DateTimeOffset.UnixEpoch.AddDays(1)).WithImage("images/old.jpg");
        var fresh = new ParsedRecipe { Title = "New", Servings = 4, Ingredients = ["1 chili"] };

        // Act
        var merged = RecipeMerge.Reimport(fresh, existing);

        // Assert
        merged.Id.Should().Be(existing.Id);
        merged.SavedAt.Should().Be(existing.SavedAt);
        merged.Title.Should().Be("New");
        merged.Ingredients.Should().Equal("1 chili");
        merged.Notes.Should().Be("Use less chili");
        merged.IsFavourite.Should().BeTrue();
        merged.Status.Should().Be(RecipeStatus.Cooked);
        merged.CookedAt.Should().Be(existing.CookedAt);
        merged.CookCount.Should().Be(1);
        merged.ChosenServings.Should().Be(6);
        merged.ImagePath.Should().Be("images/old.jpg");
    }

    [Fact]
    public void Reimport_WhenYieldChanged_ShouldDropChosenServings()
    {
        // Arrange
        var existing = new ParsedRecipe { Title = "Old", Servings = 4 }.WithChosenServings(6);
        var fresh = new ParsedRecipe { Title = "New", Servings = 2 };

        // Act
        var merged = RecipeMerge.Reimport(fresh, existing);

        // Assert
        merged.ChosenServings.Should().BeNull();
        merged.Servings.Should().Be(2);
    }
}
