using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PurePrep.Ai;
using PurePrep.Application;
using PurePrep.Server.Data;
using PurePrep.Server.Services;
using PurePrep.Units;

namespace PurePrep.Server.Endpoints;

public static class ParseEndpoint
{
    public static async Task<IResult> Parse(
        ParseRequest request,
        HttpContext http,
        ICreditStore credits,
        IFreeCreditPolicy freeCredits,
        IClientIpHasher ipHasher,
        IPageFetcher fetcher,
        IGeminiClient gemini,
        IOptions<CreditOptions> creditOptions,
        IOptions<GeminiOptions> geminiOptions,
        IDbContextFactory<ServerDbContext> dbFactory,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var log = loggerFactory.CreateLogger("ParseEndpoint");

        if (request.DeviceId == Guid.Empty)
            return Fail(StatusCodes.Status400BadRequest, ImportErrorCode.InvalidRequest, "A valid deviceId is required.");
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var url))
            return Fail(StatusCodes.Status400BadRequest, ImportErrorCode.InvalidUrl, "A valid absolute URL is required.");

        // Seed the free allowance the first time this device is seen, subject to the origin cap.
        await CreditsEndpoint.EnsureSeededAsync(request.DeviceId, http, credits, freeCredits, ipHasher, ct);

        var cost = creditOptions.Value.CostPerParse;
        if (!await credits.TrySpendAsync(request.DeviceId, cost, ct))
            return Fail(StatusCodes.Status402PaymentRequired, ImportErrorCode.InsufficientCredits, "Insufficient credits.");

        var deviceHash = ipHasher.Hash(request.DeviceId.ToString()) ?? string.Empty;

        try
        {
            var html = await fetcher.FetchAsync(url, ct);

            // Give the model the page's own schema.org recipe data (unambiguous ingredient and step
            // boundaries, plus the yield) alongside the raw page for context. Structured data is
            // laid down first so a capped input never loses it.
            var structured = StructuredRecipeExtractor.TryExtract(html);
            var input = RecipeExtractionInput.Build(structured, PageText.Extract(html), geminiOptions.Value.MaxInputChars);

            var ai = await gemini.ExtractAsync(input, request.Language, ct);

            // A page that loaded but yielded nothing usable is a distinct outcome from a fetch or
            // service failure: refund and tell the user the page simply had no recipe.
            if (!ai.Ingredients.Any() && !ai.Steps.Any())
                throw new NoRecipeExtractedException($"No recipe extracted from '{url.Host}'.");

            var system = UnitConverter.Detect(ai.Ingredients.Concat(ai.Steps));
            var recipe = new RecipeResponse(
                ai.Title, url.ToString(), system.ToString(), ai.Ingredients, ai.Steps);

            await LogAsync(dbFactory, deviceHash, url.Host, success: true, ct);
            var remaining = await credits.GetBalanceAsync(request.DeviceId, ct);
            return Results.Ok(new ParseResponse(recipe, remaining));
        }
        catch (Exception ex)
        {
            // Never charge for a failed parse. The refund deliberately ignores the request's
            // cancellation token: if the caller walked away mid-request, the credit must still
            // come back — the previous code passed `ct` here and silently skipped the refund.
            await credits.RefundAsync(request.DeviceId, cost, CancellationToken.None);
            await LogAsync(dbFactory, deviceHash, url.Host, success: false, CancellationToken.None);

            // Every failure maps to a stable code + a friendly message. The client localizes the code,
            // so the user never sees a raw "502 Bad Gateway" and each failure mode reads distinctly.
            return ex switch
            {
                UrlNotAllowedException => Fail(StatusCodes.Status400BadRequest, ImportErrorCode.InvalidUrl,
                    "That address can't be imported. Paste a public recipe page link."),

                PageNotFoundException => Fail(StatusCodes.Status400BadRequest, ImportErrorCode.NotFound,
                    "That page could not be found. Check the link and try again."),

                SiteBlockedException => Fail(StatusCodes.Status502BadGateway, ImportErrorCode.SiteBlocked,
                    "That site wouldn't let us read the page. Try opening it in your browser and pasting the recipe, or use a different link."),

                NoRecipeExtractedException => Fail(StatusCodes.Status502BadGateway, ImportErrorCode.NoRecipe,
                    "We couldn't find a recipe on that page. Try a direct link to the recipe itself."),

                OperationCanceledException when ct.IsCancellationRequested => Fail(
                    StatusCodes.Status499ClientClosedRequest, ImportErrorCode.Cancelled, "The import was cancelled."),

                UpstreamFetchException or OperationCanceledException or HttpRequestException => Fail(
                    StatusCodes.Status502BadGateway, ImportErrorCode.Temporary,
                    "The site was slow or unavailable. Please try again in a moment."),

                _ => LogAndFail(log, ex),
            };
        }
    }

    private static IResult Fail(int statusCode, string code, string message) =>
        Results.Json(new ImportError(code, message), statusCode: statusCode);

    private static IResult LogAndFail(ILogger log, Exception ex)
    {
        // The extraction service itself failed (bad API key, quota, malformed response). The user
        // gets a neutral message; the detail belongs in the log, where it is actionable.
        log.LogError(ex, "Recipe extraction failed.");
        return Fail(StatusCodes.Status502BadGateway, ImportErrorCode.ServiceError,
            "We couldn't read a recipe from that page. Your credit has been returned.");
    }

    private static async Task LogAsync(
        IDbContextFactory<ServerDbContext> dbFactory, string deviceHash, string host, bool success, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.UsageLogs.Add(new UsageLog { DeviceHash = deviceHash, Host = host, Success = success, At = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(ct);
    }
}
