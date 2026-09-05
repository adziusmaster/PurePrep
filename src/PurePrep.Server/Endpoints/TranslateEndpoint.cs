using Microsoft.Extensions.Options;
using PurePrep.Ai;
using PurePrep.Application;
using PurePrep.Server.Services;
using PurePrep.Units;

namespace PurePrep.Server.Endpoints;

/// <summary>
/// Translates an already-parsed recipe into another language with Gemini. Mirrors
/// <see cref="ParseEndpoint"/>: charge one Smart Credit up front, refund on any failure, and map
/// every failure to a stable <see cref="ImportErrorCode"/> the client localizes. There is no page
/// fetch here — the client already holds the recipe and sends its text.
/// </summary>
public static class TranslateEndpoint
{
    public static async Task<IResult> Translate(
        TranslateRequest request,
        HttpContext http,
        ICreditStore credits,
        IFreeCreditPolicy freeCredits,
        IClientIpHasher ipHasher,
        IGeminiClient gemini,
        IOptions<CreditOptions> creditOptions,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var log = loggerFactory.CreateLogger("TranslateEndpoint");

        if (request.DeviceId == Guid.Empty)
            return Fail(StatusCodes.Status400BadRequest, ImportErrorCode.InvalidRequest, "A valid deviceId is required.");

        if (string.IsNullOrWhiteSpace(request.Language) || GeminiClient.BuildTranslatePrompt(request.Language) is null)
            return Fail(StatusCodes.Status400BadRequest, ImportErrorCode.InvalidRequest, "A supported target language is required.");

        var ingredients = (request.Ingredients ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        var steps = (request.Steps ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        if (string.IsNullOrWhiteSpace(request.Title) && ingredients.Length == 0 && steps.Length == 0)
            return Fail(StatusCodes.Status400BadRequest, ImportErrorCode.InvalidRequest, "There is nothing to translate.");

        // A brand-new device might translate before ever importing; seed its free allowance the same
        // way parsing does so first contact still grants the free credits (subject to the origin cap).
        await CreditsEndpoint.EnsureSeededAsync(request.DeviceId, http, credits, freeCredits, ipHasher, ct);

        var cost = creditOptions.Value.CostPerTranslation;
        if (!await credits.TrySpendAsync(request.DeviceId, cost, ct))
            return Fail(StatusCodes.Status402PaymentRequired, ImportErrorCode.InsufficientCredits, "Insufficient credits.");

        try
        {
            var source = new AiRecipe(request.Title ?? string.Empty, ingredients, steps);
            var translated = await gemini.TranslateAsync(source, request.Language, ct);

            if (string.IsNullOrWhiteSpace(translated.Title) && !translated.Ingredients.Any() && !translated.Steps.Any())
                throw new InvalidOperationException("Translation returned no content.");

            var system = UnitConverter.Detect(translated.Ingredients.Concat(translated.Steps));
            var recipe = new RecipeResponse(
                translated.Title, null, system.ToString(), translated.Ingredients, translated.Steps);

            var remaining = await credits.GetBalanceAsync(request.DeviceId, ct);
            return Results.Ok(new TranslateResponse(recipe, remaining));
        }
        catch (Exception ex)
        {
            // Never charge for a failed translation. Refund ignores the request token so a caller that
            // walked away still gets the credit back.
            await credits.RefundAsync(request.DeviceId, cost, CancellationToken.None);

            return ex switch
            {
                OperationCanceledException when ct.IsCancellationRequested => Fail(
                    StatusCodes.Status499ClientClosedRequest, ImportErrorCode.Cancelled, "The translation was cancelled."),

                OperationCanceledException or HttpRequestException => Fail(
                    StatusCodes.Status502BadGateway, ImportErrorCode.Temporary,
                    "The translation service was slow or unavailable. Please try again in a moment."),

                _ => LogAndFail(log, ex),
            };
        }
    }

    private static IResult Fail(int statusCode, string code, string message) =>
        Results.Json(new ImportError(code, message), statusCode: statusCode);

    private static IResult LogAndFail(ILogger log, Exception ex)
    {
        log.LogError(ex, "Recipe translation failed.");
        return Fail(StatusCodes.Status502BadGateway, ImportErrorCode.ServiceError,
            "We couldn't translate that recipe. Your credit has been returned.");
    }
}
