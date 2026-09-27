using FluentAssertions;
using PurePrep.Domain;

namespace PurePrep.Core.Tests.Domain;

public sealed class ChipLineFitTests
{
    [Fact]
    public void VisibleCount_WhenAllChipsFitInTwoLines_ShouldShowAll()
    {
        // Arrange — 100 + 8 + 100 = 208 fits one 220-wide line; the third chip wraps to line two.
        double[] widths = [100, 100, 100];

        // Act
        var visible = ChipLineFit.VisibleCount([], widths, overflowWidth: 60, availableWidth: 220, spacing: 8, maxLines: 2);

        // Assert
        visible.Should().Be(3);
    }

    [Fact]
    public void VisibleCount_WhenChipsNeedAThirdLine_ShouldLeaveRoomForTheOverflowChip()
    {
        // Arrange — five 100-wide chips need three 220-wide lines; with the 60-wide "+N more" chip
        // after chip three (100 + 8 + 60 = 168) everything fits in two.
        double[] widths = [100, 100, 100, 100, 100];

        // Act
        var visible = ChipLineFit.VisibleCount([], widths, overflowWidth: 60, availableWidth: 220, spacing: 8, maxLines: 2);

        // Assert
        visible.Should().Be(3);
    }

    [Fact]
    public void VisibleCount_WhenOverflowChipDoesNotFitBesideLastChip_ShouldDropOneMoreChip()
    {
        // Arrange — line two holds one 150-wide chip, but 150 + 8 + 70 = 228 > 220, so the overflow
        // chip takes that chip's place.
        double[] widths = [150, 150, 150];

        // Act
        var visible = ChipLineFit.VisibleCount([], widths, overflowWidth: 70, availableWidth: 220, spacing: 8, maxLines: 2);

        // Assert
        visible.Should().Be(1);
    }

    [Fact]
    public void VisibleCount_WhenAChipIsWiderThanTheLine_ShouldGiveItALineOfItsOwn()
    {
        // Arrange — an over-wide chip is clamped to one full line rather than breaking the count.
        double[] widths = [500, 100];

        // Act
        var visible = ChipLineFit.VisibleCount([], widths, overflowWidth: 60, availableWidth: 220, spacing: 8, maxLines: 2);

        // Assert
        visible.Should().Be(2);
    }

    [Fact]
    public void VisibleCount_WhenWidthIsNotYetKnown_ShouldShowAll()
    {
        // Arrange
        double[] widths = [100, 100, 100, 100, 100];

        // Act
        var visible = ChipLineFit.VisibleCount([], widths, overflowWidth: 60, availableWidth: 0, spacing: 8, maxLines: 2);

        // Assert
        visible.Should().Be(5);
    }

    [Fact]
    public void VisibleCount_WhenNoChips_ShouldReturnZero()
    {
        // Arrange
        double[] widths = [];

        // Act
        var visible = ChipLineFit.VisibleCount([], widths, overflowWidth: 60, availableWidth: 220, spacing: 8, maxLines: 2);

        // Assert
        visible.Should().Be(0);
    }

    [Fact]
    public void VisibleCount_WhenLeadingIconsLeaveRoomForShortChips_ShouldKeepEverythingOnOneLine()
    {
        // Arrange — a 92-wide icon pair plus two 150-wide chips: 92 + 8 + 150 + 8 + 150 = 408 ≤ 420.
        double[] leading = [92];
        double[] widths = [150, 150];

        // Act
        var visible = ChipLineFit.VisibleCount(leading, widths, overflowWidth: 60, availableWidth: 420, spacing: 8, maxLines: 1);

        // Assert
        visible.Should().Be(2);
    }

    [Fact]
    public void VisibleCount_WhenLeadingIconsTakeLineOneSpace_ShouldHideChipsNotTheIcons()
    {
        // Arrange — line one holds the 92-wide pair + one 100-wide chip (200 ≤ 220); line two holds
        // one chip + "+N more" (100 + 8 + 60 = 168), so of four chips only two show.
        double[] leading = [92];
        double[] widths = [100, 100, 100, 100];

        // Act
        var visible = ChipLineFit.VisibleCount(leading, widths, overflowWidth: 60, availableWidth: 220, spacing: 8, maxLines: 2);

        // Assert
        visible.Should().Be(2);
    }
}
