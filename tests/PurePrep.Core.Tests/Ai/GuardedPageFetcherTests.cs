using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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

    [Fact]
    public async Task FetchAsync_WhenHonestBotIsWalled_ShouldRetryAsTransparentBrowser()
    {
        // Arrange — the site 403s our bot, then serves the page to the browser fallback.
        var handler = new StubHttpMessageHandler()
            .Respond(HttpStatusCode.Forbidden)
            .Respond(HttpStatusCode.OK, "<html>recipe</html>");
        var options = new PageFetchOptions { ContactEmail = "dev@pureprep.app" };
        var fetcher = new GuardedPageFetcher(
            new HttpClient(handler), GuardAllowing("recipes.example.com"), options, new InMemoryFetchHostMemory());

        // Act
        var body = await fetcher.FetchAsync(Public);

        // Assert
        body.Should().Be("<html>recipe</html>");
        handler.SentRequests.Should().HaveCount(2);
        handler.SentRequests[0].UserAgent.Should().Contain("PurePrepBot");
        handler.SentRequests[0].From.Should().BeNull("the honest attempt does not disguise itself");

        var fallback = handler.SentRequests[1];
        fallback.UserAgent.Should().Contain("Mozilla").And.NotContain("PurePrepBot");
        fallback.From.Should().Be("dev@pureprep.app");
        fallback.Purpose.Should().NotBeNullOrWhiteSpace().And.Contain("recipe import");
        fallback.Info.Should().Be("https://pureprep.app/bot");
    }

    [Fact]
    public async Task FetchAsync_WhenBrowserFallbackIsAlsoWalled_ShouldThrowSiteBlocked()
    {
        // Arrange
        var handler = new StubHttpMessageHandler()
            .Respond(HttpStatusCode.Forbidden)
            .Respond(HttpStatusCode.Forbidden);
        var fetcher = new GuardedPageFetcher(
            new HttpClient(handler), GuardAllowing("recipes.example.com"), new PageFetchOptions(), new InMemoryFetchHostMemory());

        // Act
        var act = async () => await fetcher.FetchAsync(Public);

        // Assert
        await act.Should().ThrowAsync<SiteBlockedException>();
        handler.SentRequests.Should().HaveCount(2, "one honest attempt, one browser fallback");
    }

    [Fact]
    public async Task FetchAsync_WhenPageIsNotFound_ShouldThrowWithoutBrowserRetry()
    {
        // Arrange — a 404 is a real "no such page"; masquerading as a browser will not conjure one.
        var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.NotFound);
        var fetcher = new GuardedPageFetcher(
            new HttpClient(handler), GuardAllowing("recipes.example.com"), new PageFetchOptions(), new InMemoryFetchHostMemory());

        // Act
        var act = async () => await fetcher.FetchAsync(Public);

        // Assert
        await act.Should().ThrowAsync<PageNotFoundException>();
        handler.SentRequests.Should().ContainSingle("404 must not trigger the browser fallback");
    }

    [Fact]
    public async Task FetchAsync_WhenHostAlreadyKnownToBlockBot_ShouldStartWithBrowser()
    {
        // Arrange — a prior import taught us this host walls the bot, so we skip the wasted first hop.
        var memory = new InMemoryFetchHostMemory();
        memory.MarkBlocked("recipes.example.com");
        var handler = new StubHttpMessageHandler().Respond(HttpStatusCode.OK, "<html>ok</html>");
        var fetcher = new GuardedPageFetcher(
            new HttpClient(handler), GuardAllowing("recipes.example.com"), new PageFetchOptions(), memory);

        // Act
        var body = await fetcher.FetchAsync(Public);

        // Assert
        body.Should().Be("<html>ok</html>");
        handler.SentRequests.Should().ContainSingle();
        handler.SentRequests[0].UserAgent.Should().Contain("Mozilla");
    }

    [Fact]
    public async Task FetchAsync_WhenUpstreamIsTransientlyBad_ShouldRetrySameIdentityAndSucceed()
    {
        // Arrange — a one-off 502 clears on retry, all under the honest bot identity.
        var handler = new StubHttpMessageHandler()
            .Respond(HttpStatusCode.BadGateway)
            .Respond(HttpStatusCode.OK, "<html>recovered</html>");
        var options = new PageFetchOptions { MaxTransientRetries = 2 };
        var fetcher = new GuardedPageFetcher(
            new HttpClient(handler), GuardAllowing("recipes.example.com"), options, new InMemoryFetchHostMemory());

        // Act
        var body = await fetcher.FetchAsync(Public);

        // Assert
        body.Should().Be("<html>recovered</html>");
        handler.SentRequests.Should().HaveCount(2);
        handler.SentRequests.Should().OnlyContain(r => r.UserAgent!.Contains("PurePrepBot"));
    }

    // Registered as a typed HttpClient, GuardedPageFetcher is created by the DI container's
    // ActivatorUtilities, which requires exactly one applicable constructor. When a second public
    // constructor was added, resolution began throwing "Multiple constructors accepting all given
    // argument types" on every request — a 500 on every import that no other test caught, because
    // the unit tests call a constructor directly and the server tests swap in a fake fetcher.
    [Fact]
    public void TypedClientActivation_ShouldResolveASingleConstructor()
    {
        // Arrange — mirror exactly what AddHttpClient<IPageFetcher, GuardedPageFetcher>() does:
        // build a factory that supplies the HttpClient and resolves the rest from the provider.
        var provider = new StubServiceProvider(
            (typeof(IUrlGuard), GuardAllowing("recipes.example.com")),
            (typeof(IOptions<PageFetchOptions>), Options.Create(new PageFetchOptions())),
            (typeof(IFetchHostMemory), new InMemoryFetchHostMemory()));

        // Act
        var act = () =>
        {
            var factory = ActivatorUtilities.CreateFactory(typeof(GuardedPageFetcher), [typeof(HttpClient)]);
            return factory(provider, [new HttpClient()]);
        };

        // Assert
        act.Should().NotThrow().Which.Should().BeOfType<GuardedPageFetcher>();
    }

    private sealed class StubServiceProvider(params (Type Type, object Instance)[] services) : IServiceProvider
    {
        private readonly Dictionary<Type, object> _services = services.ToDictionary(s => s.Type, s => s.Instance);

        public object? GetService(Type serviceType) =>
            _services.TryGetValue(serviceType, out var instance) ? instance : null;
    }
}
