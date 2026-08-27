using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NSubstitute;
using PurePrep.Server.Services;
using PurePrep.Server.Tests.TestSupport;

namespace PurePrep.Server.Tests.Endpoints;

/// <summary>
/// End-to-end cover for the redeem endpoint. The defect these guard against: the server accepted
/// any non-empty purchase token as proof of payment, so credits could be minted with one request.
/// </summary>
public sealed class BillingEndpointTests : IClassFixture<PurePrepAppFactory>
{
    private readonly PurePrepAppFactory _factory;

    public BillingEndpointTests(PurePrepAppFactory factory) => _factory = factory;

    private static object Redeem(Guid device, string product, string token) =>
        new { deviceId = device, productId = product, purchaseToken = token };

    [Fact]
    public async Task Redeem_WithAForgedPurchaseToken_ShouldNotGrantCredits()
    {
        // Arrange — Google has never heard of this token.
        var device = Guid.NewGuid();
        _factory.PlayLookup
            .GetProductPurchaseAsync("credits_150", "totally-made-up", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PlayPurchase?>(null));
        var client = _factory.CreateClient();

        // Establish the starting balance (the device's free allowance) before attempting forgery.
        var before = (await client.GetFromJsonAsync<BalanceDto>($"/api/credits/{device}"))!.Balance;

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/billing/redeem", Redeem(device, "credits_150", "totally-made-up"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var after = (await client.GetFromJsonAsync<BalanceDto>($"/api/credits/{device}"))!.Balance;
        after.Should().Be(before, because: "a forged token must never move the ledger");
    }

    [Fact]
    public async Task Redeem_WithAPurchaseGoogleConfirms_ShouldGrantThePackCredits()
    {
        // Arrange
        var device = Guid.NewGuid();
        _factory.PlayLookup
            .GetProductPurchaseAsync("credits_20", "real-token", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PlayPurchase?>(new PlayPurchase(0, 0, $"GPA.{Guid.NewGuid()}")));
        var client = _factory.CreateClient();

        var before = (await client.GetFromJsonAsync<BalanceDto>($"/api/credits/{device}"))!.Balance;

        // Act
        var response = await client.PostAsJsonAsync("/api/billing/redeem", Redeem(device, "credits_20", "real-token"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var after = (await client.GetFromJsonAsync<BalanceDto>($"/api/credits/{device}"))!.Balance;
        after.Should().Be(before + 20, because: "a purchase Google confirms grants exactly the pack size");
    }

    [Fact]
    public async Task Redeem_WithAnUnknownProductId_ShouldBeRejected()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/billing/redeem", Redeem(Guid.NewGuid(), "credits_1000000", "real-token"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private sealed record BalanceDto(int Balance);
}
