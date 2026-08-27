using System.Net;
using FluentAssertions;
using NSubstitute;
using PurePrep.Ai;
using PurePrep.Core.Tests.TestSupport;

namespace PurePrep.Core.Tests.Ai;

public sealed class GuardedPageFetcherTests
{
    private static readonly Uri Public = new("https://recipes.example.com/pasta");

    private static IUrlGuard GuardAllowing(params string[] allowedHosts)
    {
        var guard = Substitute.For<IUrlGuard>();
        guard.IsPublicHttpAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(allowedHosts.Contains(call.Arg<Uri>().Host)));
        return guard;
    }

    [Fact]
    public async Task FetchAsync_WhenPageIsPublic_ShouldReturnBody()
    {
        // Arrange
        var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, "<html>pasta</html>");
        var fetcher = new GuardedPageFetcher(new HttpClient(handler), GuardAllowing("recipes.example.com"));

        // Act
        var body = await fetcher.FetchAsync(Public);

        // Assert
        body.Should().Be("<html>pasta</html>");
    }

    [Fact]
    public async Task FetchAsync_WhenRedirectedToPrivateAddress_ShouldThrowAndNotRequestIt()
    {
        // Arrange
        var handler = new StubHttpMessageHandler().RedirectTo("http://127.0.0.1:8080/api/admin/promo");
        var fetcher = new GuardedPageFetcher(new HttpClient(handler), GuardAllowing("recipes.example.com"));

        // Act
        var act = async () => await fetcher.FetchAsync(Public);

        // Assert
        await act.Should().ThrowAsync<UrlNotAllowedException>();
        handler.RequestedUris.Should().ContainSingle().Which.Host.Should().Be("recipes.example.com");
    }

    [Fact]
    public async Task FetchAsync_WhenRedirectedToAnotherPublicPage_ShouldFollowAndReturnBody()
    {
        // Arrange
        var handler = new StubHttpMessageHandler()
            .RedirectTo("https://cdn.example.com/pasta")
            .Respond(HttpStatusCode.OK, "<html>moved</html>");
        var fetcher = new GuardedPageFetcher(
            new HttpClient(handler), GuardAllowing("recipes.example.com", "cdn.example.com"));

        // Act
        var body = await fetcher.FetchAsync(Public);

        // Assert
        body.Should().Be("<html>moved</html>");
        handler.RequestedUris.Should().HaveCount(2);
    }

    [Fact]
    public async Task FetchAsync_WhenRedirectsExceedTheLimit_ShouldThrow()
    {
        // Arrange
        var handler = new StubHttpMessageHandler();
        for (var i = 0; i < 10; i++)
            handler.RedirectTo($"https://recipes.example.com/hop{i}");
        var fetcher = new GuardedPageFetcher(new HttpClient(handler), GuardAllowing("recipes.example.com"));

        // Act
        var act = async () => await fetcher.FetchAsync(Public);

        // Assert
        await act.Should().ThrowAsync<UrlNotAllowedException>()
            .WithMessage("*redirect*");
    }

    [Fact]
    public async Task FetchAsync_WhenInitialUrlIsNotPublic_ShouldThrowWithoutAnyRequest()
    {
        // Arrange
        var handler = new StubHttpMessageHandler();
        var fetcher = new GuardedPageFetcher(new HttpClient(handler), GuardAllowing());

        // Act
        var act = async () => await fetcher.FetchAsync(Public);

        // Assert
        await act.Should().ThrowAsync<UrlNotAllowedException>();
        handler.RequestedUris.Should().BeEmpty();
    }
}
