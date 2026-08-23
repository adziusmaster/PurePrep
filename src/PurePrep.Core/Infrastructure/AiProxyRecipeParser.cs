using System.Net;
using System.Net.Http.Json;
using PurePrep.Application;
using PurePrep.Domain;

namespace PurePrep.Infrastructure;

/// <summary>
/// <see cref="IRecipeParser"/> implementation that delegates parsing to the PurePrep backend's AI
/// Smart Parser (Gemini). The backend fetches the page, extracts a clean recipe, and atomically
/// deducts one Smart Credit from the device. A 402 response (no credits) surfaces as
/// <see cref="InsufficientCreditsException"/> so the UI can show the paywall.
/// </summary>
public sealed class AiProxyRecipeParser(HttpClient http, IDeviceIdentity identity) : IRecipeParser
{
    public async Task<ParsedRecipe> ParseAsync(Uri source, CancellationToken cancellationToken = default)
    {
        var deviceId = await identity.GetDeviceIdAsync(cancellationToken);

        using var response = await http.PostAsJsonAsync(
            "api/ai/parse",
            new { deviceId, url = source.ToString() },
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.PaymentRequired)
            throw new InsufficientCreditsException();

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ParsePayload>(cancellationToken)
            ?? throw new InvalidOperationException("The parser service returned an empty response.");

        var recipe = payload.Recipe
            ?? throw new InvalidOperationException("The parser service returned no recipe.");

        var system = Enum.TryParse<MeasurementSystem>(recipe.SourceSystem, out var parsed)
            ? parsed
            : MeasurementSystem.Metric;

        return new ParsedRecipe
        {
            Title = recipe.Title,
            SourceUrl = recipe.SourceUrl,
            Ingredients = recipe.Ingredients ?? Array.Empty<string>(),
            Steps = (recipe.Steps ?? Array.Empty<string>())
                .Select((instruction, index) => new RecipeStep { Order = index + 1, Instruction = instruction })
                .ToArray(),
            SourceSystem = system,
        };
    }

    private sealed record ParsePayload(RecipePayload? Recipe, int RemainingCredits);

    private sealed record RecipePayload(
        string Title,
        string? SourceUrl,
        string SourceSystem,
        IReadOnlyList<string>? Ingredients,
        IReadOnlyList<string>? Steps);
}
