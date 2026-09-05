using System.Net;
using System.Net.Http.Json;
using PurePrep.Application;
using PurePrep.Domain;

namespace PurePrep.Infrastructure;

/// <summary>
/// <see cref="IRecipeTranslator"/> that delegates to the backend's AI Smart Translator (Gemini).
/// Mirrors <see cref="AiProxyRecipeParser"/>: the backend charges one Smart Credit atomically, and a
/// 402 (no credits) surfaces as <see cref="InsufficientCreditsException"/> for the paywall. The
/// recipe's <b>original</b> text is sent so re-translations never compound earlier translations.
/// </summary>
public sealed class AiProxyRecipeTranslator(HttpClient http, IDeviceIdentity identity) : IRecipeTranslator
{
    public async Task<RecipeTranslation> TranslateAsync(
        ParsedRecipe recipe, string targetLanguage, CancellationToken cancellationToken = default)
    {
        var deviceId = await identity.GetDeviceIdAsync(cancellationToken);

        using var response = await http.PostAsJsonAsync(
            "api/ai/translate",
            new
            {
                deviceId,
                language = targetLanguage,
                title = recipe.Title,
                ingredients = recipe.Ingredients,
                steps = recipe.Steps.Select(s => s.Instruction).ToArray(),
            },
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.PaymentRequired)
            throw new InsufficientCreditsException();

        if (!response.IsSuccessStatusCode)
        {
            var error = await TryReadErrorAsync(response, cancellationToken);
            throw new RecipeImportException(error?.Code ?? ImportErrorCode.Unknown, error?.Error);
        }

        var payload = await response.Content.ReadFromJsonAsync<TranslatePayload>(cancellationToken)
            ?? throw new InvalidOperationException("The translation service returned an empty response.");

        var translated = payload.Recipe
            ?? throw new InvalidOperationException("The translation service returned no recipe.");

        return new RecipeTranslation
        {
            Title = translated.Title,
            Ingredients = translated.Ingredients ?? Array.Empty<string>(),
            Steps = (translated.Steps ?? Array.Empty<string>())
                .Select((instruction, index) => new RecipeStep { Order = index + 1, Instruction = instruction })
                .ToArray(),
        };
    }

    private sealed record TranslatePayload(RecipePayload? Recipe, int RemainingCredits);

    private sealed record RecipePayload(
        string Title,
        string? SourceUrl,
        string SourceSystem,
        IReadOnlyList<string>? Ingredients,
        IReadOnlyList<string>? Steps);

    private sealed record ImportErrorPayload(string? Code, string? Error);

    private static async Task<ImportErrorPayload?> TryReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<ImportErrorPayload>(ct);
        }
        catch
        {
            return null;
        }
    }
}
