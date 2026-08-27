using System.Net.Http.Json;
using FluentAssertions;
using PurePrep.Server.Tests.TestSupport;

namespace PurePrep.Server.Tests.Endpoints;

public sealed class CreditsEndpointTests : IClassFixture<PurePrepAppFactory>
{
    private readonly PurePrepAppFactory _factory;

    public CreditsEndpointTests(PurePrepAppFactory factory) => _factory = factory;

    [Fact]
    public async Task GetBalance_ForAnAppBuildStillUsingTheOldEndpoint_ShouldStillSeedTheAllowance()
    {
        // Arrange — installs of the shipped APK read their balance with a GET and depend on it
        // seeding on first contact. Making it read-only would strand them on zero credits.
        var client = _factory.CreateClient();
        var device = Guid.NewGuid();

        // Act
        var balance = await client.GetFromJsonAsync<BalanceDto>($"/api/credits/{device}");

        // Assert
        balance!.Balance.Should().Be(10);
    }

    [Fact]
    public async Task GetBalance_CalledRepeatedly_ShouldNotStackCredits()
    {
        // Arrange
        var client = _factory.CreateClient();
        var device = Guid.NewGuid();

        // Act
        await client.GetFromJsonAsync<BalanceDto>($"/api/credits/{device}");
        var second = await client.GetFromJsonAsync<BalanceDto>($"/api/credits/{device}");

        // Assert
        second!.Balance.Should().Be(10);
    }

    [Fact]
    public async Task Ensure_ForANewDevice_ShouldSeedTheFreeAllowance()
    {
        // Arrange
        var client = _factory.CreateClient();
        var device = Guid.NewGuid();

        // Act
        var response = await client.PostAsJsonAsync("/api/credits/ensure", new { deviceId = device });

        // Assert
        var balance = await response.Content.ReadFromJsonAsync<BalanceDto>();
        balance!.Balance.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Ensure_CalledRepeatedlyForOneDevice_ShouldNotStackCredits()
    {
        // Arrange
        var client = _factory.CreateClient();
        var device = Guid.NewGuid();

        // Act
        await client.PostAsJsonAsync("/api/credits/ensure", new { deviceId = device });
        var second = await client.PostAsJsonAsync("/api/credits/ensure", new { deviceId = device });

        // Assert
        var balance = await second.Content.ReadFromJsonAsync<BalanceDto>();
        balance!.Balance.Should().Be(10);
    }

    private sealed record BalanceDto(int Balance);
}
