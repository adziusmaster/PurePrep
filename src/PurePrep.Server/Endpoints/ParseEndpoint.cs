using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PurePrep.Ai;
using PurePrep.Domain;
using PurePrep.Server.Data;
using PurePrep.Server.Services;
using PurePrep.Units;

namespace PurePrep.Server.Endpoints;

public static class ParseEndpoint
{
    public static async Task<IResult> Parse(
        ParseRequest request,
        ICreditStore credits,
        IUrlGuard urlGuard,
        IGeminiClient gemini,
        IHttpClientFactory httpFactory,
        IOptions<CreditOptions> creditOptions,
        IDbContextFactory<ServerDbContext> dbFactory,
        CancellationToken ct)
    {
        if (request.DeviceId == Guid.Empty)
            return Results.BadRequest(new { error = "A valid deviceId is required." });
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var url))
            return Results.BadRequest(new { error = "A valid absolute URL is required." });
        if (!await urlGuard.IsPublicHttpAsync(url, ct))
            return Results.BadRequest(new { error = "URL is not an allowed public http(s) address." });

        var cost = creditOptions.Value.CostPerParse;
        if (!await credits.TrySpendAsync(request.DeviceId, cost, ct))
            return Results.Json(new { error = "Insufficient credits." }, statusCode: StatusCodes.Status402PaymentRequired);

        try
        {
            var http = httpFactory.CreateClient("fetch");
            using var page = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            page.EnsureSuccessStatusCode();
            var html = await page.Content.ReadAsStringAsync(ct);

            var text = PageText.Extract(html);
            var ai = await gemini.ExtractAsync(text, ct);

            var system = UnitConverter.Detect(ai.Ingredients.Concat(ai.Steps));
            var recipe = new RecipeResponse(
                ai.Title,
                url.ToString(),
                system.ToString(),
                ai.Ingredients,
                ai.Steps);

            await LogAsync(dbFactory, request.DeviceId, url.Host, success: true, ct);
            var remaining = await credits.GetBalanceAsync(request.DeviceId, ct);
            return Results.Ok(new ParseResponse(recipe, remaining));
        }
        catch (Exception)
        {
            // Never charge for a failed parse.
            await credits.RefundAsync(request.DeviceId, cost, ct);
            await LogAsync(dbFactory, request.DeviceId, url.Host, success: false, ct);
            return Results.Json(new { error = "Failed to extract a recipe from this page." },
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static async Task LogAsync(IDbContextFactory<ServerDbContext> dbFactory, Guid deviceId, string host, bool success, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.Database.EnsureCreatedAsync(ct);
        db.UsageLogs.Add(new UsageLog { DeviceId = deviceId, Host = host, Success = success, At = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(ct);
    }
}
