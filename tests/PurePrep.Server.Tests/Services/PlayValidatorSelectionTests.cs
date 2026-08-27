using FluentAssertions;
using PurePrep.Server.Services;

namespace PurePrep.Server.Tests.Services;

/// <summary>
/// Which validator the composition root picks. The original defect was that the development
/// validator — which accepts any non-empty string as proof of purchase — was registered
/// unconditionally, so a forged token earned real credits in production. The rule under test is
/// that production cannot silently fall back to it.
/// </summary>
public sealed class PlayValidatorSelectionTests : IDisposable
{
    private readonly string _keyFile = Path.Combine(Path.GetTempPath(), $"pureprep-key-{Guid.NewGuid():N}.json");

    private PlayOptions Configured()
    {
        File.WriteAllText(_keyFile, "{}");
        return new PlayOptions { ServiceAccountJsonPath = _keyFile };
    }

    private static PlayOptions NotConfigured() => new() { ServiceAccountJsonPath = null };

    public void Dispose()
    {
        if (File.Exists(_keyFile))
            File.Delete(_keyFile);
    }

    [Fact]
    public void Select_InProductionWithCredentials_ShouldChooseTheRealValidator()
    {
        // Arrange & Act
        var choice = PlayValidatorSelection.Select(isProduction: true, Configured());

        // Assert
        choice.Should().Be(PlayValidatorChoice.GooglePlay);
    }

    [Fact]
    public void Select_InProductionWithoutCredentials_ShouldThrowRatherThanAcceptForgedPurchases()
    {
        // Arrange
        var options = NotConfigured();

        // Act
        var act = () => PlayValidatorSelection.Select(isProduction: true, options);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Play__ServiceAccountJsonPath*");
    }

    [Fact]
    public void Select_InProductionWhenTheKeyFileIsMissing_ShouldThrow()
    {
        // Arrange — a configured path that does not exist is a deployment mistake, not a dev mode.
        var options = new PlayOptions { ServiceAccountJsonPath = "/nonexistent/play-key.json" };

        // Act
        var act = () => PlayValidatorSelection.Select(isProduction: true, options);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Select_OutsideProductionWithoutCredentials_ShouldChooseTheDevelopmentValidator()
    {
        // Arrange & Act
        var choice = PlayValidatorSelection.Select(isProduction: false, NotConfigured());

        // Assert
        choice.Should().Be(PlayValidatorChoice.Development);
    }

    [Fact]
    public void Select_OutsideProductionWithCredentials_ShouldStillPreferTheRealValidator()
    {
        // Arrange & Act
        var choice = PlayValidatorSelection.Select(isProduction: false, Configured());

        // Assert
        choice.Should().Be(PlayValidatorChoice.GooglePlay);
    }

    [Fact]
    public void IsConfigured_WhenPathPointsAtNothing_ShouldBeFalse()
    {
        // Arrange & Act & Assert
        new PlayOptions { ServiceAccountJsonPath = "/nonexistent/key.json" }.IsConfigured.Should().BeFalse();
    }
}
