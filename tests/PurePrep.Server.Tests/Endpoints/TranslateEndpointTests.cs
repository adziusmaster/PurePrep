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
    public async Task Translate_WhenStepCountMismatchesOnce_ShouldRetryThenSucceedAndChargeOneCredit()
    {
        // Arrange — first Gemini response drops a step; the endpoint must retry within the same
        // request/charge rather than surface the mismatch, and the second response matches.
        var client = _factory.CreateClient();
        var device = Guid.NewGuid();
        var before = (await client.GetFromJsonAsync<BalanceDto>($"/api/credits/{device}"))!.Balance;
        var calls = 0;
        _factory.Gemini.TranslateAsync(Arg.Any<AiRecipe>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                calls++;
                return calls == 1
                    ? new AiRecipe("Zitronen-Pasta", ["200 g Spaghetti", "1 Zitrone"], ["Die Pasta kochen."])
                    : new AiRecipe("Zitronen-Pasta", ["200 g Spaghetti", "1 Zitrone"], ["Die Pasta kochen.", "Alles vermengen."]);
            });

        // Act
        var response = await TranslateAsync(client, device);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: $"server logs: {string.Join(" | ", _factory.LogLines)}");
        _ = _factory.Gemini.Received(2).TranslateAsync(Arg.Any<AiRecipe>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        var after = (await client.GetFromJsonAsync<BalanceDto>($"/api/credits/{device}"))!.Balance;
        after.Should().Be(before - 1, because: "a step-count mismatch must retry inside the one charge, never charge twice");
    }

    [Fact]
    public async Task Translate_WhenStepCountMismatchesTwice_ShouldRefundAndReturnServiceError()
    {
        // Arrange — every Gemini response drops a step, so the endpoint's one retry is also a mismatch.
        var client = _factory.CreateClient();
        var device = Guid.NewGuid();
        var before = (await client.GetFromJsonAsync<BalanceDto>($"/api/credits/{device}"))!.Balance;
        _factory.Gemini.TranslateAsync(Arg.Any<AiRecipe>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new AiRecipe("Zitronen-Pasta", ["200 g Spaghetti", "1 Zitrone"], ["Die Pasta kochen."])));

        // Act
        var response = await TranslateAsync(client, device);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await ReadCodeAsync(response)).Should().Be(ImportErrorCode.ServiceError);
        _ = _factory.Gemini.Received(2).TranslateAsync(Arg.Any<AiRecipe>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        var after = (await client.GetFromJsonAsync<BalanceDto>($"/api/credits/{device}"))!.Balance;
        after.Should().Be(before, because: "a translation that never settles must refund, never charge for a failure");
    }

    [Fact]
    public async Task Translate_WhenIngredientCountMismatchesOnce_ShouldRetryThenSucceedAndChargeOneCredit()
    {
        // Arrange — first response merges the two ingredient lines into one; the retry matches.
        var client = _factory.CreateClient();
        var device = Guid.NewGuid();
        var before = (await client.GetFromJsonAsync<BalanceDto>($"/api/credits/{device}"))!.Balance;
        var calls = 0;
        _factory.Gemini.TranslateAsync(Arg.Any<AiRecipe>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                calls++;
                return calls == 1
                    ? new AiRecipe("Zitronen-Pasta", ["200 g Spaghetti und 1 Zitrone"], ["Die Pasta kochen.", "Alles vermengen."])
                    : new AiRecipe("Zitronen-Pasta", ["200 g Spaghetti", "1 Zitrone"], ["Die Pasta kochen.", "Alles vermengen."]);
            });

        // Act
        var response = await TranslateAsync(client, device);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: $"server logs: {string.Join(" | ", _factory.LogLines)}");
        _ = _factory.Gemini.Received(2).TranslateAsync(Arg.Any<AiRecipe>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        var after = (await client.GetFromJsonAsync<BalanceDto>($"/api/credits/{device}"))!.Balance;
        after.Should().Be(before - 1, because: "an ingredient-count mismatch must retry inside the one charge");
    }

    [Fact]
    public async Task Translate_WhenIngredientCountMismatchesTwice_ShouldRefundAndReturnServiceError()
    {
        // Arrange — every response drops an ingredient, so the retry is also a mismatch.
        var client = _factory.CreateClient();
        var device = Guid.NewGuid();
        var before = (await client.GetFromJsonAsync<BalanceDto>($"/api/credits/{device}"))!.Balance;
        _factory.Gemini.TranslateAsync(Arg.Any<AiRecipe>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new AiRecipe("Zitronen-Pasta", ["200 g Spaghetti"], ["Die Pasta kochen.", "Alles vermengen."])));

        // Act
        var response = await TranslateAsync(client, device);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await ReadCodeAsync(response)).Should().Be(ImportErrorCode.ServiceError);
        _ = _factory.Gemini.Received(2).TranslateAsync(Arg.Any<AiRecipe>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        var after = (await client.GetFromJsonAsync<BalanceDto>($"/api/credits/{device}"))!.Balance;
        after.Should().Be(before, because: "a translation that never lines up must refund");
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

    [Fact]
    public async Task Translate_WhenTimerLabelsSent_ShouldPassThemAndReturnTranslated()
    {
        // Arrange
        _factory.Gemini.TranslateAsync(Arg.Any<AiRecipe>(), "pl", Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var source = call.ArgAt<AiRecipe>(0);
                source.TimerLabels.Should().Equal("Fry onion");
                return new AiRecipe("Makaron", ["cebula"], ["Smaż cebulę."]) { TimerLabels = ["Smaż cebulę"] };
            });
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/ai/translate", new
        {
            deviceId = Guid.NewGuid(), language = "pl", title = "Pasta",
            ingredients = new[] { "onion" }, steps = new[] { "Fry the onion." }, timerLabels = new[] { "Fry onion" },
        });
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("recipe").GetProperty("timerLabels")[0].GetString().Should().Be("Smaż cebulę");
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ImportErrorDto>())?.Code;

    private sealed record BalanceDto(int Balance);
    private sealed record ImportErrorDto(string Code, string Error);
    private sealed record RecipeDto(string Title, string? SourceUrl, string SourceSystem,
        IReadOnlyList<string> Ingredients, IReadOnlyList<string> Steps);
    private sealed record TranslatePayloadDto(RecipeDto Recipe, int RemainingCredits);
}
