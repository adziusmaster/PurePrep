using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PurePrep.Server.Data;
using PurePrep.Server.Services;

namespace PurePrep.Server.Endpoints;

public static class BillingEndpoint
{
    public static async Task<IResult> Redeem(
        RedeemRequest request,
        IPlayValidator validator,
        ICreditStore credits,
        IOptions<CreditOptions> creditOptions,
        IDbContextFactory<ServerDbContext> dbFactory,
        CancellationToken ct)
    {
        if (request.DeviceId == Guid.Empty || string.IsNullOrWhiteSpace(request.PurchaseToken) || string.IsNullOrWhiteSpace(request.ProductId))
            return Results.BadRequest(new { error = "deviceId, productId and purchaseToken are required." });

        if (!creditOptions.Value.Packs.TryGetValue(request.ProductId, out var creditsForPack))
            return Results.BadRequest(new { error = $"Unknown product '{request.ProductId}'." });

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        // Replay protection: a purchase token can only ever be redeemed once.
        if (await db.Purchases.AnyAsync(p => p.PurchaseToken == request.PurchaseToken, ct))
            return Results.Conflict(new { error = "This purchase has already been redeemed." });

        var validation = await validator.ValidateAsync(request.ProductId, request.PurchaseToken, ct);
        if (!validation.Valid)
            return Results.BadRequest(new { error = "Purchase could not be validated with Google Play." });

        // OrderId is globally unique per purchase; guard against reuse across tokens too.
        if (await db.Purchases.AnyAsync(p => p.OrderId == validation.OrderId, ct))
            return Results.Conflict(new { error = "This order has already been redeemed." });

        db.Purchases.Add(new ProcessedPurchase
        {
            PurchaseToken = request.PurchaseToken,
            OrderId = validation.OrderId,
            ProductId = request.ProductId,
            DeviceId = request.DeviceId,
            CreditsGranted = creditsForPack,
            RedeemedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(ct);

        var balance = await credits.GrantAsync(request.DeviceId, creditsForPack, ct);
        return Results.Ok(new RedeemResponse(creditsForPack, balance));
    }
}
