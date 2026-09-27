using FluentAssertions;
using PurePrep.Application;
using Xunit;

namespace PurePrep.Core.Tests.Application;

public class ImportResultTests
{
    [Fact]
    public void FromException_WhenServerSaysInsufficientCredits_ShouldBeOutOfCredits()
    {
        // Arrange
        var error = new InsufficientCreditsException();

        // Act
        var result = ImportResult.FromException(error);

        // Assert
        result.Outcome.Should().Be(ImportOutcome.OutOfCredits);
        result.Recipe.Should().BeNull();
    }

    [Fact]
    public void FromException_WhenBackendReturnsHandledFailure_ShouldBeFailedWithItsCode()
    {
        // Arrange
        var error = new RecipeImportException(ImportErrorCode.NoRecipe);

        // Act
        var result = ImportResult.FromException(error);

        // Assert
        result.Outcome.Should().Be(ImportOutcome.Failed);
        result.ErrorCode.Should().Be(ImportErrorCode.NoRecipe);
    }

    [Fact]
    public void FromException_WhenTransportFails_ShouldBeFailedWithUnknownCodeNotOutOfCredits()
    {
        // Arrange
        var error = new HttpRequestException("Connection refused");

        // Act
        var result = ImportResult.FromException(error);

        // Assert
        result.Outcome.Should().Be(ImportOutcome.Failed);
        result.ErrorCode.Should().Be(ImportErrorCode.Unknown);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(-1, false)]
    [InlineData(1, false)]
    [InlineData(42, false)]
    public void IsOutOfCredits_ForBalance_ShouldOnlyBlockAKnownZeroBalance(int balance, bool expected)
    {
        // Arrange
        // -1 means "not loaded yet": the import is attempted and a 402 still maps to OutOfCredits.

        // Act
        var blocked = ImportResult.IsOutOfCredits(balance);

        // Assert
        blocked.Should().Be(expected);
    }
}
