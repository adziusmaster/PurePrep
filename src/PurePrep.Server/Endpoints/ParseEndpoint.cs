using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PurePrep.Ai;
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
            return Results.BadRequest(new { error = "A valid deviceId is required." });
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var url))
            return Results.BadRequest(new { error = "A valid absolute URL is required." });

        // Seed the free allowance the first time this device is seen, subject to the origin cap.
        await CreditsEndpoint.EnsureSeededAsync(request.DeviceId, http, credits, freeCredits, ipHasher, ct);

        var cost = creditOptions.Value.CostPerParse;
        if (!await credits.TrySpendAsync(request.DeviceId, cost, ct))
            return Results.Json(new { error = "Insufficient credits." }, statusCode: StatusCodes.Status402PaymentRequired);

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

            // Distinguish the failure modes instead of blaming every one on the recipe page.
            // Previously an expired Gemini key and an unparseable blog post looked identical.
            return ex switch
            {
                UrlNotAllowedException => Results.BadRequest(
                    new { error = "That address can't be imported. Paste a public recipe page link." }),

                OperationCanceledException => Results.Json(
                    new { error = "The import was cancelled." }, statusCode: StatusCodes.Status499ClientClosedRequest),

                HttpRequestException http404 when http404.StatusCode == System.Net.HttpStatusCode.NotFound =>
                    Results.BadRequest(new { error = "That page could not be found. Check the link and try again." }),

                HttpRequestException httpEx when IsUpstreamRecipeSite(httpEx) => Results.Json(
                    new { error = "That site would not let us read the page. Try a different link." },
                    statusCode: StatusCodes.Status502BadGateway),

                _ => LogAndFail(log, ex),
            };
        }
    }

    private static bool IsUpstreamRecipeSite(HttpRequestException ex) =>
        ex.StatusCode is not null;

    private static IResult LogAndFail(ILogger log, Exception ex)
    {
        // The extraction service itself failed (bad API key, quota, malformed response). The user
        // gets a neutral message; the detail belongs in the log, where it is actionable.
        log.LogError(ex, "Recipe extraction failed.");
        return Results.Json(
            new { error = "We couldn't read a recipe from that page. Your credit has been returned." },
            statusCode: StatusCodes.Status502BadGateway);
    }

    private static async Task LogAsync(
        IDbContextFactory<ServerDbContext> dbFactory, string deviceHash, string host, bool success, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.UsageLogs.Add(new UsageLog { DeviceHash = deviceHash, Host = host, Success = success, At = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(ct);
    }
}
