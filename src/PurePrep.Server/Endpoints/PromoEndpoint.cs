using PurePrep.Server.Services;

namespace PurePrep.Server.Endpoints;

/// <summary>
/// Public endpoint to redeem a promo code for smart credits, plus admin endpoints (create/list/revoke)
/// protected by a shared secret so the app owner can mint and manage tester codes.
/// </summary>
public static class PromoEndpoint
{
    public const string SecretHeader = "X-Admin-Secret";
    private const int DefaultCredits = 10;

    public static async Task<IResult> Redeem(RedeemCodeRequest request, IPromoStore promos, CancellationToken ct)
    {
        if (request.DeviceId == Guid.Empty || string.IsNullOrWhiteSpace(request.Code))
            return Results.BadRequest(new { error = "deviceId and code are required." });

        var normalized = SqlitePromoStore.Normalize(request.Code);
        if (normalized.Length != SqlitePromoStore.CodeLength)
            return Results.BadRequest(new { error = $"A code must be {SqlitePromoStore.CodeLength} characters." });

        var result = await promos.RedeemAsync(request.DeviceId, normalized, ct);
        return result.Outcome switch
        {
            RedeemOutcome.Success => Results.Ok(new RedeemCodeResponse(result.CreditsGranted, result.Balance)),
            RedeemOutcome.NotFound => Results.NotFound(new { error = "invalid_code" }),
            RedeemOutcome.Revoked => Results.BadRequest(new { error = "revoked_code" }),
            RedeemOutcome.Expired => Results.BadRequest(new { error = "expired_code" }),
            RedeemOutcome.AlreadyRedeemed => Results.Conflict(new { error = "already_redeemed" }),
            _ => Results.BadRequest(new { error = "invalid_code" }),
        };
    }

    public static async Task<IResult> Create(CreatePromoRequest request, IPromoStore promos, CancellationToken ct)
    {
        var credits = request.Credits is { } c and > 0 ? c : DefaultCredits;
        if (!string.IsNullOrWhiteSpace(request.Code))
        {
            var normalized = SqlitePromoStore.Normalize(request.Code);
            if (normalized.Length != SqlitePromoStore.CodeLength)
                return Results.BadRequest(new { error = $"A custom code must be {SqlitePromoStore.CodeLength} characters." });
        }

        try
        {
            var promo = await promos.CreateAsync(request.Code, credits, request.ExpiresInDays, ct);
            return Results.Ok(ToResponse(promo, 0));
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            return Results.Conflict(new { error = "A code with that value already exists." });
        }
    }

    public static async Task<IResult> List(IPromoStore promos, CancellationToken ct)
    {
        var all = await promos.ListAsync(ct);
        return Results.Ok(all.Select(s => new PromoResponse(
            s.Code, s.Credits, s.CreatedAt, s.ExpiresAt, s.Revoked, s.RedemptionCount)));
    }

    public static async Task<IResult> Revoke(string code, IPromoStore promos, CancellationToken ct)
    {
        var ok = await promos.RevokeAsync(code, ct);
        return ok ? Results.Ok(new { code = SqlitePromoStore.Normalize(code), revoked = true })
                  : Results.NotFound(new { error = "invalid_code" });
    }

    public static async ValueTask<object?> SecretFilter(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var configured = context.HttpContext.RequestServices
            .GetRequiredService<IConfiguration>()["Admin:Secret"];
        var provided = context.HttpContext.Request.Headers[SecretHeader].ToString();

        if (string.IsNullOrEmpty(configured) || !string.Equals(configured, provided, StringComparison.Ordinal))
            return Results.NotFound();

        return await next(context);
    }

    private static PromoResponse ToResponse(Data.PromoCode c, int count) =>
        new(c.Code, c.Credits, c.CreatedAt, c.ExpiresAt, c.Revoked, count);
}
