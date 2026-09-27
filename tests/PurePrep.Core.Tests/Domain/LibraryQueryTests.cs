using FluentAssertions;
using PurePrep.Domain;

namespace PurePrep.Core.Tests.Domain;

public sealed class LibraryQueryTests
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.UnixEpoch;

    private static ParsedRecipe R(string title, int addedDay, bool fav = false, int? cookedDay = null)
    {
        var r = new ParsedRecipe { Title = title, SavedAt = T0.AddDays(addedDay) }.WithFavourite(fav);
        return cookedDay is null ? r : r.MarkCooked(T0.AddDays(cookedDay.Value));
    }

    [Fact]
    public void Apply_WhenSortingByRecentlyAdded_ShouldPinFavouritesFirst()
    {
        // Arrange
        var recipes = new[] { R("A", 1), R("B", 3), R("C", 2, fav: true) };

        // Act
        var result = LibraryQuery.Apply(recipes, null, LibraryFilter.All, LibrarySort.RecentlyAdded);

        // Assert
        result.Select(r => r.Title).Should().Equal("C", "B", "A");
    }

    [Fact]
    public void Apply_WhenFilteringCookedAndSortingRecentlyCooked_ShouldExcludeUncooked()
    {
        // Arrange
        var recipes = new[] { R("A", 1, cookedDay: 5), R("B", 2), R("C", 3, cookedDay: 9) };

        // Act
        var result = LibraryQuery.Apply(recipes, null, LibraryFilter.Cooked, LibrarySort.RecentlyCooked);

        // Assert
        result.Select(r => r.Title).Should().Equal("C", "A");
    }

    [Fact]
    public void Apply_WhenSearchGiven_ShouldMatchTitleCaseInsensitively()
    {
        // Arrange
        var recipes = new[] { R("Lemon Pasta", 1), R("Beet salad", 2) };

        // Act
        var result = LibraryQuery.Apply(recipes, "  PASTA ", LibraryFilter.All, LibrarySort.Alphabetical);

        // Assert
        result.Should().ContainSingle().Which.Title.Should().Be("Lemon Pasta");
    }
}
