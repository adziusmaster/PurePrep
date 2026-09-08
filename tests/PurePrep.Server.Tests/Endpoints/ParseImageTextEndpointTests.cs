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
/// Cover for the two non-URL import sources: a photo/screenshot and pasted text. Both share the
/// URL parse pipeline, so these focus on the source-specific validation and — for images — the
/// higher credit price and its full refund on failure.
/// </summary>
public sealed class ParseImageTextEndpointTests : IClassFixture<PurePrepAppFactory>
{
    private readonly PurePrepAppFactory _factory;

    private static int _originCounter;

    public ParseImageTextEndpointTests(PurePrepAppFactory factory)
    {
        _factory = factory;
        _factory.Gemini.ClearSubstitute();
        _factory.PageFetcher.ClearSubstitute();
        // A fresh origin per test so the per-IP free-credit cap never couples tests.
        var n = System.Threading.Interlocked.Increment(ref _originCounter);
        _factory.ClientAddress = System.Net.IPAddress.Parse($"198.51.101.{n}");
    }

    private static readonly string SampleImageBase64 = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5 });

    private static Task<HttpResponseMessage> PostImageAsync(HttpClient client, Guid device, string? mime = "image/jpeg", string? image = null) =>
        client.PostAsJsonAsync("/api/ai/parse-image",
            new { deviceId = device, imageBase64 = image ?? SampleImageBase64, mimeType = mime, language = (string?)null });

    private static Task<HttpResponseMessage> PostTextAsync(HttpClient client, Guid device, string text) =>
        client.PostAsJsonAsync("/api/ai/parse-text",
            new { deviceId = device, text, language = (string?)null });

    private void ArrangeImageOk() =>
        _factory.Gemini.ExtractFromImageAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new AiRecipe("Photo Pancakes", ["200 g flour"], ["Mix.", "Fry."])));

    private void ArrangeTextOk() =>
        _factory.Gemini.ExtractAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new AiRecipe("Pasted Pasta", ["100 g pasta"], ["Boil."])));

    [Fact]
    public async Task ParseImage_WhenSuccessful_ShouldReturnRecipeAndChargeTwoCredits()
    {
        var client = _factory.CreateClient();
        var device = Guid.NewGuid();
        var before = (await client.GetFromJsonAsync<BalanceDto>($"/api/credits/{device}"))!.Balance;
        ArrangeImageOk();

        var response = await PostImageAsync(client, device);

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: $"server logs: {string.Join(" | ", _factory.LogLines)}");
        var payload = await response.Content.ReadFromJsonAsync<ParseDto>();
        payload!.Recipe.Title.Should().Be("Photo Pancakes");
        payload.Recipe.Steps.Should().HaveCount(2);
        var after = (await client.GetFromJsonAsync<BalanceDto>($"/api/credits/{device}"))!.Balance;
        after.Should().Be(before - 2, because: "an image import is priced at two credits");
    }

    [Fact]
    public async Task ParseImage_WhenNoRecipeFound_ShouldRefundBothCredits()
    {
        var client = _factory.CreateClient();
        var device = Guid.NewGuid();
        var before = (await client.GetFromJsonAsync<BalanceDto>($"/api/credits/{device}"))!.Balance;
        _factory.Gemini.ExtractFromImageAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new AiRecipe("", Array.Empty<string>(), Array.Empty<string>())));

        var response = await PostImageAsync(client, device);

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await ReadCodeAsync(response)).Should().Be(ImportErrorCode.NoRecipe);
        var after = (await client.GetFromJsonAsync<BalanceDto>($"/api/credits/{device}"))!.Balance;
        after.Should().Be(before, because: "a failed image import must return the full two-credit charge");
    }

    [Fact]
    public async Task ParseImage_WhenBase64IsInvalid_ShouldRejectWithoutCharging()
    {
        var client = _factory.CreateClient();
        var device = Guid.NewGuid();
        var before = (await client.GetFromJsonAsync<BalanceDto>($"/api/credits/{device}"))!.Balance;

        var response = await PostImageAsync(client, device, image: "not-valid-base64!!!");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadCodeAsync(response)).Should().Be(ImportErrorCode.InvalidRequest);
        var after = (await client.GetFromJsonAsync<BalanceDto>($"/api/credits/{device}"))!.Balance;
        after.Should().Be(before);
    }

    [Fact]
    public async Task ParseImage_WhenImageTypeUnsupported_ShouldRejectWithoutCharging()
    {
        var client = _factory.CreateClient();
        var device = Guid.NewGuid();
        var before = (await client.GetFromJsonAsync<BalanceDto>($"/api/credits/{device}"))!.Balance;

        var response = await PostImageAsync(client, device, mime: "image/gif");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadCodeAsync(response)).Should().Be(ImportErrorCode.InvalidRequest);
        var after = (await client.GetFromJsonAsync<BalanceDto>($"/api/credits/{device}"))!.Balance;
        after.Should().Be(before);
    }

    [Fact]
    public async Task ParseText_WhenSuccessful_ShouldReturnRecipeAndChargeOneCredit()
    {
        var client = _factory.CreateClient();
        var device = Guid.NewGuid();
        var before = (await client.GetFromJsonAsync<BalanceDto>($"/api/credits/{device}"))!.Balance;
        ArrangeTextOk();

        var response = await PostTextAsync(client, device, "100 g pasta\nboil it");

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: $"server logs: {string.Join(" | ", _factory.LogLines)}");
        var payload = await response.Content.ReadFromJsonAsync<ParseDto>();
        payload!.Recipe.Title.Should().Be("Pasted Pasta");
        var after = (await client.GetFromJsonAsync<BalanceDto>($"/api/credits/{device}"))!.Balance;
        after.Should().Be(before - 1, because: "pasted text is priced the same as a URL import");
    }

    [Fact]
    public async Task ParseText_WhenTextIsBlank_ShouldRejectWithoutCharging()
    {
        var client = _factory.CreateClient();
        var device = Guid.NewGuid();
        var before = (await client.GetFromJsonAsync<BalanceDto>($"/api/credits/{device}"))!.Balance;

        var response = await PostTextAsync(client, device, "   ");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadCodeAsync(response)).Should().Be(ImportErrorCode.InvalidRequest);
        var after = (await client.GetFromJsonAsync<BalanceDto>($"/api/credits/{device}"))!.Balance;
        after.Should().Be(before);
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ImportErrorDto>())?.Code;

    private sealed record BalanceDto(int Balance);
    private sealed record ImportErrorDto(string Code, string Error);
    private sealed record RecipeDto(string Title, string? SourceUrl, string SourceSystem, string[] Ingredients, string[] Steps);
    private sealed record ParseDto(RecipeDto Recipe, int RemainingCredits);
}
