using FluentAssertions;
using NSubstitute;
using PurePrep.Server.Services;

namespace PurePrep.Server.Tests.Services;

/// <summary>
/// The rules that decide whether a Google Play purchase token earns credits. These are the gate
/// that stands between the redeem endpoint and anyone who can send an HTTP request, so each
/// rejection case is asserted explicitly.
/// </summary>
public sealed class AndroidPublisherPlayValidatorTests
{
    private const string Product = "credits_10";
    private const string Token = "token-abc";

    private readonly IPlayPurchaseLookup _lookup = Substitute.For<IPlayPurchaseLookup>();

    private AndroidPublisherPlayValidator CreateValidator() => new(_lookup);

    private void LookupReturns(PlayPurchase? purchase) =>
        _lookup.GetProductPurchaseAsync(Product, Token, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(purchase));

    [Fact]
    public async Task ValidateAsync_WhenGooglePurchaseIsCompleteAndUnconsumed_ShouldBeValid()
    {
        // Arrange
        LookupReturns(new PlayPurchase(PurchaseState: 0, ConsumptionState: 0, OrderId: "GPA.1234"));
        var validator = CreateValidator();

        // Act
        var result = await validator.ValidateAsync(Product, Token);

        // Assert
        result.Valid.Should().BeTrue();
        result.OrderId.Should().Be("GPA.1234");
    }

    [Fact]
    public async Task ValidateAsync_WhenGoogleDoesNotKnowTheToken_ShouldBeInvalid()
    {
        // Arrange — a forged token is the attack this whole class exists to stop.
        LookupReturns(null);
        var validator = CreateValidator();

        // Act
        var result = await validator.ValidateAsync(Product, Token);

        // Assert
        result.Valid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_WhenPurchaseIsCancelled_ShouldBeInvalid()
    {
        // Arrange — purchaseState 1 is Cancelled.
        LookupReturns(new PlayPurchase(PurchaseState: 1, ConsumptionState: 0, OrderId: "GPA.1234"));
        var validator = CreateValidator();

        // Act
        var result = await validator.ValidateAsync(Product, Token);

        // Assert
        result.Valid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_WhenPurchaseIsStillPending_ShouldBeInvalid()
    {
        // Arrange — purchaseState 2 is Pending; the money has not moved yet.
        LookupReturns(new PlayPurchase(PurchaseState: 2, ConsumptionState: 0, OrderId: "GPA.1234"));
        var validator = CreateValidator();

        // Act
        var result = await validator.ValidateAsync(Product, Token);

        // Assert
        result.Valid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_WhenPurchaseWasAlreadyConsumed_ShouldBeInvalid()
    {
        // Arrange — consumptionState 1 means Google already saw it consumed, so credits were granted.
        LookupReturns(new PlayPurchase(PurchaseState: 0, ConsumptionState: 1, OrderId: "GPA.1234"));
        var validator = CreateValidator();

        // Act
        var result = await validator.ValidateAsync(Product, Token);

        // Assert
        result.Valid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_WhenGoogleHasNoOrderId_ShouldBeInvalid()
    {
        // Arrange — replay protection keys on the order id, so a purchase without one cannot be trusted.
        LookupReturns(new PlayPurchase(PurchaseState: 0, ConsumptionState: 0, OrderId: null));
        var validator = CreateValidator();

        // Act
        var result = await validator.ValidateAsync(Product, Token);

        // Assert
        result.Valid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_WhenGoogleCallFails_ShouldBeInvalidRatherThanThrow()
    {
        // Arrange — an outage must not become a free-credit bypass, nor a 500 for the user.
        _lookup.GetProductPurchaseAsync(Product, Token, Arg.Any<CancellationToken>())
            .Returns<Task<PlayPurchase?>>(_ => throw new HttpRequestException("Play API unreachable"));
        var validator = CreateValidator();

        // Act
        var result = await validator.ValidateAsync(Product, Token);

        // Assert
        result.Valid.Should().BeFalse();
    }
}
