using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ClearExtensions;
using PurePrep.Ai;
using PurePrep.Server.Services;
using PurePrep.Server.Tests.TestSupport;

namespace PurePrep.Server.Tests.Endpoints;

public sealed class ImageEndpointTests : IClassFixture<PurePrepAppFactory>
{
    private readonly PurePrepAppFactory _factory;

    public ImageEndpointTests(PurePrepAppFactory factory)
    {
        _factory = factory;
        _factory.Gemini.ClearSubstitute();
    }

    private string IssueTicket(Guid device) =>
        _factory.Services.GetRequiredService<IImageTicketStore>().Issue(device);

    [Fact]
    public async Task Image_WhenTicketValid_ShouldReturnGeneratedBytes()
    {
        // Arrange
        var device = Guid.NewGuid();
        _factory.Gemini.GenerateImageAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new GeneratedImage([9, 9, 9], "image/png"));
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/ai/image", new { deviceId = device, ticket = IssueTicket(device), title = "Pasta", ingredients = new[] { "pasta" } });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        (await response.Content.ReadAsByteArrayAsync()).Should().Equal(9, 9, 9);
    }

    [Fact]
    public async Task Image_WhenInputOversized_ShouldCapTitleAndIngredientsBeforeCallingModel()
    {
        // Arrange
        var device = Guid.NewGuid();
        _factory.Gemini.GenerateImageAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new GeneratedImage([1], "image/png"));
        var client = _factory.CreateClient();
        var ingredients = Enumerable.Range(0, 50).Select(i => new string('x', 300)).ToArray();

        // Act
        var response = await client.PostAsJsonAsync("/api/ai/image",
            new { deviceId = device, ticket = IssueTicket(device), title = new string('t', 500), ingredients });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.Gemini.Received(1).GenerateImageAsync(
            Arg.Is<string>(t => t.Length == 200),
            Arg.Is<IReadOnlyList<string>>(list => list.Count == 30 && list.All(i => i.Length == 120)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Image_WhenModelReturnsNonImageMimeType_ShouldReturn502()
    {
        // Arrange
        var device = Guid.NewGuid();
        _factory.Gemini.GenerateImageAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new GeneratedImage([1, 2], "text/html"));
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/ai/image", new { deviceId = device, ticket = IssueTicket(device), title = "Pasta" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
    }

    [Fact]
    public async Task Image_WhenTicketUnknown_ShouldReturn403AndNotCallModel()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/ai/image", new { deviceId = Guid.NewGuid(), ticket = "nope", title = "Pasta" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await _factory.Gemini.DidNotReceiveWithAnyArgs().GenerateImageAsync(default!, default!, default);
    }

    [Fact]
    public async Task Image_WhenModelFails_ShouldReturn502()
    {
        // Arrange
        var device = Guid.NewGuid();
        _factory.Gemini.GenerateImageAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns<GeneratedImage>(_ => throw new HttpRequestException("boom"));
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/ai/image", new { deviceId = device, ticket = IssueTicket(device), title = "Pasta" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
    }
}
