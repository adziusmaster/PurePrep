using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ClearExtensions;
using PurePrep.Ai;
using PurePrep.Server.Tests.TestSupport;

namespace PurePrep.Server.Tests.Endpoints;

/// <summary>
/// End-to-end cover for what the extraction model is actually handed, and for the credit
/// accounting around a failed import.
/// </summary>
public sealed class ParseEndpointTests : IClassFixture<PurePrepAppFactory>
{
    private readonly PurePrepAppFactory _factory;

    public ParseEndpointTests(PurePrepAppFactory factory)
    {
        _factory = factory;
        // The factory is a class fixture shared by every test here, so substitute configuration
        // would otherwise leak between them — one test's throwing model breaks the next.
        _factory.Gemini.ClearSubstitute();
        _factory.PageFetcher.ClearSubstitute();
    }

    private const string PageWithJsonLd = """
        <html><head><script type="application/ld+json">
        {"@type":"Recipe","name":"Lemon Pasta","recipeYield":"4 servings",
         "recipeIngredient":["200 g spaghetti","1 lemon"],
         "recipeInstructions":["Boil the pasta.","Toss together."]}
        </script></head><body><p>A long story about my trip to Sicily.</p></body></html>
        """;

    private string? _captured;

    private void ArrangeGemini()
    {
        _factory.Gemini.ExtractAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                _captured = call.ArgAt<string>(0);
                return Task.FromResult(new AiRecipe("Lemon Pasta", ["200 g spaghetti"], ["Boil the pasta."]));
            });
    }

    private async Task<HttpResponseMessage> ParseAsync(HttpClient client, Guid device) =>
        await client.PostAsJsonAsync("/api/ai/parse",
            new { deviceId = device, url = "https://example.com/lemon-pasta", language = (string?)null });

    [Fact]
    public async Task Parse_WhenThePagePublishesRecipeData_ShouldGiveTheModelBothSections()
    {
        // Arrange — the model gets the structured data for exact boundaries AND the page for context.
        var client = _factory.CreateClient();
        _factory.PageFetcher.FetchAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(PageWithJsonLd));
        ArrangeGemini();

        // Act
        var response = await ParseAsync(client, Guid.NewGuid());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: $"server logs: {string.Join(" | ", _factory.LogLines)}");
        _captured.Should().Contain("STRUCTURED RECIPE DATA")
            .And.Contain("200 g spaghetti")
            .And.Contain("4 servings")
            .And.Contain("PAGE TEXT")
            .And.Contain("trip to Sicily");
    }

    [Fact]
    public async Task Parse_WhenThePageHasNoRecipeData_ShouldStillSendThePageText()
    {
        // Arrange
        var client = _factory.CreateClient();
        _factory.PageFetcher.FetchAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("<html><body><p>Handwritten recipe with no markup.</p></body></html>"));
        ArrangeGemini();

        // Act
        var response = await ParseAsync(client, Guid.NewGuid());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _captured.Should().Contain("Handwritten recipe with no markup.");
        _captured.Should().NotContain("STRUCTURED RECIPE DATA");
    }

    [Fact]
    public async Task Parse_WhenTheUrlIsBlockedBySsrfPolicy_ShouldRefundTheCredit()
    {
        // Arrange
        var client = _factory.CreateClient();
        var device = Guid.NewGuid();
        var before = (await client.GetFromJsonAsync<BalanceDto>($"/api/credits/{device}"))!.Balance;
        _factory.PageFetcher.FetchAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>())
            .Returns<Task<string>>(_ => throw new UrlNotAllowedException("blocked"));

        // Act
        var response = await ParseAsync(client, device);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var after = (await client.GetFromJsonAsync<BalanceDto>($"/api/credits/{device}"))!.Balance;
        after.Should().Be(before, because: "a failed import must never cost a credit");
    }

    [Fact]
    public async Task Parse_WhenExtractionFails_ShouldRefundTheCreditAndNotBlameThePage()
    {
        // Arrange — an expired API key used to be reported as an unparseable recipe page.
        var client = _factory.CreateClient();
        var device = Guid.NewGuid();
        var before = (await client.GetFromJsonAsync<BalanceDto>($"/api/credits/{device}"))!.Balance;
        _factory.PageFetcher.FetchAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(PageWithJsonLd));
        _factory.Gemini.ExtractAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns<Task<AiRecipe>>(_ => throw new InvalidOperationException("Gemini API key is not configured."));

        // Act
        var response = await ParseAsync(client, device);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        var after = (await client.GetFromJsonAsync<BalanceDto>($"/api/credits/{device}"))!.Balance;
        after.Should().Be(before);
    }

    private sealed record BalanceDto(int Balance);
}
