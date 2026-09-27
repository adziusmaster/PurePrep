using System.Net;
using FluentAssertions;
using NSubstitute;
using PurePrep.Application;
using PurePrep.Core.Tests.TestSupport;
using PurePrep.Infrastructure;

namespace PurePrep.Core.Tests.Infrastructure;

public sealed class AiProxyRecipeImageGeneratorTests
{
    private readonly IDeviceIdentity _identity = Substitute.For<IDeviceIdentity>();

    public AiProxyRecipeImageGeneratorTests() =>
        _identity.GetDeviceIdAsync(Arg.Any<CancellationToken>()).Returns(Guid.NewGuid());

    private AiProxyRecipeImageGenerator Create(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") }, _identity);

    [Fact]
    public async Task GenerateAsync_WhenServerReturnsImage_ShouldReturnBytes()
    {
        // Arrange
        var sut = Create(StubHttpMessageHandler.Bytes(HttpStatusCode.OK, [4, 5], "image/png"));

        // Act
        var bytes = await sut.GenerateAsync("t1", "Pasta", ["pasta"], CancellationToken.None);

        // Assert
        bytes.Should().Equal(4, 5);
    }

    [Fact]
    public async Task GenerateAsync_WhenTicketRejected_ShouldReturnNull()
    {
        // Arrange
        var sut = Create(StubHttpMessageHandler.Bytes(HttpStatusCode.Forbidden, [], "text/plain"));

        // Act
        var bytes = await sut.GenerateAsync("t1", "Pasta", ["pasta"], CancellationToken.None);

        // Assert
        bytes.Should().BeNull();
    }

    [Fact]
    public async Task GenerateAsync_WhenHandlerThrowsWebException_ShouldReturnNull()
    {
        // Arrange — AndroidMessageHandler surfaces socket/TLS failures as WebException.
        var sut = Create(new ThrowingHandler(new WebException("Socket closed")));

        // Act
        var bytes = await sut.GenerateAsync("t1", "Pasta", ["pasta"], CancellationToken.None);

        // Assert
        bytes.Should().BeNull();
    }

    [Fact]
    public async Task GenerateAsync_WhenCallerCancels_ShouldThrowOperationCanceled()
    {
        // Arrange
        var sut = Create(StubHttpMessageHandler.Bytes(HttpStatusCode.OK, [4, 5], "image/png"));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        Func<Task> act = async () => await sut.GenerateAsync("t1", "Pasta", ["pasta"], cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(exception);
    }
}
