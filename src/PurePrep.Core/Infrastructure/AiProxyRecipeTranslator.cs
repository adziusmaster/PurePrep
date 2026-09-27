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
/// The server already retries once, within that same charge, when the model changes the step or ingredient count —
/// this client makes a single request and only defensively checks the step and ingredient counts on the way back, so
/// a still-mismatched response (which the server would have turned into a refunded <c>ServiceError</c>)
/// never gets re-attached to the wrong steps.
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
                timerLabels = recipe.Steps.SelectMany(s => s.Timers).Select(t => t.Label).ToArray(),
                servingsNoun = recipe.ServingsNoun,
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

        // Defensive: the server already retried once within the same charge. A mismatch here would
        // desync the index-based re-attachment below, so fail closed rather than risk mislabeled steps.
        // The server drops blank ingredient lines before translating, so compare against the same count.
        var sentIngredients = recipe.Ingredients.Count(i => !string.IsNullOrWhiteSpace(i));
        if ((translated.Steps?.Count ?? 0) != recipe.Steps.Count || (translated.Ingredients?.Count ?? 0) != sentIngredients)
            throw new RecipeImportException(ImportErrorCode.ServiceError, "Translation changed the recipe's structure.");

        // Re-attach the original timers (durations, ingredient refs) to the translated steps: the model
        // only ever translates wording, so the source structure stays authoritative. Labels come back
        // flattened in step order; fall back to the original labels when the count doesn't line up.
        var originalTimers = recipe.Steps.SelectMany(s => s.Timers).ToArray();
        var labels = translated.TimerLabels is { } l && l.Count == originalTimers.Length
            ? l
            : originalTimers.Select(t => t.Label).ToArray();
        var cursor = 0;
        var steps = recipe.Steps.Select((original, index) =>
        {
            var timers = original.Timers.Select(t => t with { Label = labels[cursor++] }).ToArray();
            return original with { Instruction = translated.Steps![index], Timers = timers };
        }).ToArray();

        return new RecipeTranslation
        {
            Title = translated.Title,
            Ingredients = translated.Ingredients ?? Array.Empty<string>(),
            Steps = steps,
        };
    }

    private sealed record TranslatePayload(RecipePayload? Recipe, int RemainingCredits);

    private sealed record RecipePayload(
        string Title,
        string? SourceUrl,
        string SourceSystem,
        IReadOnlyList<string>? Ingredients,
        IReadOnlyList<string>? Steps)
    {
        public IReadOnlyList<string>? TimerLabels { get; init; }
    }

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
