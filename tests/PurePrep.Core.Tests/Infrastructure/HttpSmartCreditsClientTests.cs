using System.Net;
using FluentAssertions;
using NSubstitute;
using PurePrep.Application;
using PurePrep.Core.Tests.TestSupport;
using PurePrep.Infrastructure;

namespace PurePrep.Core.Tests.Infrastructure;

public sealed class HttpSmartCreditsClientTests
{
    private static readonly Guid Device = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private static IDeviceIdentity Identity()
    {
        var identity = Substitute.For<IDeviceIdentity>();
        identity.GetDeviceIdAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(Device));
        return identity;
    }

    private static HttpClient Client(StubHttpMessageHandler handler) =>
        new(handler) { BaseAddress = new Uri("https://api.example.com/") };

    [Fact]
    public async Task GetBalanceAsync_ShouldAskTheSeedingEndpointSoFirstRunGrantsFreeCredits()
    {
        // Arrange — a plain GET no longer seeds, so the client must call the endpoint that does,
        // otherwise a fresh install would report zero credits forever.
        var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, """{"balance":10}""");
        var client = new HttpSmartCreditsClient(Client(handler), Identity());

        // Act
        var balance = await client.GetBalanceAsync();

        // Assert
        balance.Should().Be(10);
        handler.RequestedUris.Should().ContainSingle()
            .Which.AbsolutePath.Should().Be("/api/credits/ensure");
    }

    [Fact]
    public async Task GetBalanceAsync_WhenTheBackendIsUnreachable_ShouldSurfaceTheFailure()
    {
        // Arrange — the caller decides how to degrade; the client must not invent a balance.
        var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.ServiceUnavailable);
        var client = new HttpSmartCreditsClient(Client(handler), Identity());

        // Act
        var act = async () => await client.GetBalanceAsync();

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
