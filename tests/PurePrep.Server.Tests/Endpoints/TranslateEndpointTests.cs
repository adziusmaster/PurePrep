using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ClearExtensions;
using PurePrep.Ai;
using PurePrep.Application;
using PurePrep.Server.Tests.TestSupport;

namespace PurePrep.Server.Tests.Endpoints;

/// <summary>
/// Covers the AI translate endpoint: it charges a credit, returns the translated content, and — the
/// part that matters most — never charges for a failure and rejects unsupported input.
/// </summary>
public sealed class TranslateEndpointTests : IClassFixture<PurePrepAppFactory>
{
    private readonly PurePrepAppFactory _factory;

    private static int _originCounter = 100;

    public TranslateEndpointTests(PurePrepAppFactory factory)
    {
        _factory = factory;
        _factory.Gemini.ClearSubstitute();
        _factory.PageFetcher.ClearSubstitute();
        var n = System.Threading.Interlocked.Increment(ref _originCounter);
        _factory.ClientAddress = System.Net.IPAddress.Parse($"198.51.100.{n}");
    }

    private static object Body(Guid device, string language = "de") => new
    {
        deviceId = device,
        language,
        title = "Lemon Pasta",
        ingredients = new[] { "200 g spaghetti", "1 lemon" },
        steps = new[] { "Boil the pasta.", "Toss together." },
    };

    private Task<HttpResponseMessage> TranslateAsync(HttpClient client, Guid device, string language = "de") =>
        client.PostAsJsonAsync("/api/ai/translate", Body(device, language));

    private void ArrangeGemini()
    {
        _factory.Gemini.TranslateAsync(Arg.Any<AiRecipe>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new AiRecipe(
                "Zitronen-Pasta", ["200 g Spaghetti", "1 Zitrone"], ["Die Pasta kochen.", "Alles vermengen."])));
    }

    [Fact]
    public async Task Translate_WhenSuccessful_ShouldReturnTranslatedContentAndChargeOneCredit()
    {
        var client = _factory.CreateClient();
        var device = Guid.NewGuid();
        var before = (await client.GetFromJsonAsync<BalanceDto>($"/api/credits/{device}"))!.Balance;
        ArrangeGemini();

        var response = await TranslateAsync(client, device);

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: $"server logs: {string.Join(" | ", _factory.LogLines)}");
        var payload = await response.Content.ReadFromJsonAsync<TranslatePayloadDto>();
        payload!.Recipe.Title.Should().Be("Zitronen-Pasta");
        payload.Recipe.Ingredients.Should().Contain("1 Zitrone");
        payload.RemainingCredits.Should().Be(before - 1);
    }

    [Fact]
    public async Task Translate_WhenLanguageUnsupported_ShouldRejectWithoutCharging()
    {
        var client = _factory.CreateClient();
        var device = Guid.NewGuid();
        var before = (await client.GetFromJsonAsync<BalanceDto>($"/api/credits/{device}"))!.Balance;

        var response = await TranslateAsync(client, device, language: "xx");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadCodeAsync(response)).Should().Be(ImportErrorCode.InvalidRequest);
        var after = (await client.GetFromJsonAsync<BalanceDto>($"/api/credits/{device}"))!.Balance;
        after.Should().Be(before, because: "a rejected request must never cost a credit");
    }

    [Fact]
    public async Task Translate_WhenTheModelFails_ShouldRefundTheCredit()
    {
        var client = _factory.CreateClient();
        var device = Guid.NewGuid();
        var before = (await client.GetFromJsonAsync<BalanceDto>($"/api/credits/{device}"))!.Balance;
        _factory.Gemini.TranslateAsync(Arg.Any<AiRecipe>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<AiRecipe>>(_ => throw new InvalidOperationException("Gemini API key is not configured."));

        var response = await TranslateAsync(client, device);

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await ReadCodeAsync(response)).Should().Be(ImportErrorCode.ServiceError);
        var after = (await client.GetFromJsonAsync<BalanceDto>($"/api/credits/{device}"))!.Balance;
        after.Should().Be(before);
    }

    [Fact]
    public async Task Translate_WhenNoCredits_ShouldReturnPaymentRequired()
    {
        var client = _factory.CreateClient();
        var device = Guid.NewGuid();
        ArrangeGemini();

        // Drain the free balance by translating until the ledger is empty, then one more must 402.
        HttpResponseMessage last;
        do
        {
            last = await TranslateAsync(client, device);
        } while (last.StatusCode == HttpStatusCode.OK
                 && (await client.GetFromJsonAsync<BalanceDto>($"/api/credits/{device}"))!.Balance > 0);

        var response = await TranslateAsync(client, device);

        response.StatusCode.Should().Be(HttpStatusCode.PaymentRequired);
        (await ReadCodeAsync(response)).Should().Be(ImportErrorCode.InsufficientCredits);
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ImportErrorDto>())?.Code;

    private sealed record BalanceDto(int Balance);
    private sealed record ImportErrorDto(string Code, string Error);
    private sealed record RecipeDto(string Title, string? SourceUrl, string SourceSystem,
        IReadOnlyList<string> Ingredients, IReadOnlyList<string> Steps);
    private sealed record TranslatePayloadDto(RecipeDto Recipe, int RemainingCredits);
}
