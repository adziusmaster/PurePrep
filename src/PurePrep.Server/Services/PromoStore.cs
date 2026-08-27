using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using PurePrep.Server.Data;

namespace PurePrep.Server.Services;

public enum RedeemOutcome
{
    Success,
    NotFound,
    Revoked,
    Expired,
    AlreadyRedeemed,
}

public sealed record RedeemResult(RedeemOutcome Outcome, int CreditsGranted, int Balance);

public sealed record PromoSummary(
    string Code,
    int Credits,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    bool Revoked,
    int RedemptionCount);

public interface IPromoStore
{
    Task<RedeemResult> RedeemAsync(Guid deviceId, string code, CancellationToken ct = default);
    Task<PromoCode> CreateAsync(string? code, int credits, int? expiresInDays, CancellationToken ct = default);
    Task<IReadOnlyList<PromoSummary>> ListAsync(CancellationToken ct = default);
    Task<bool> RevokeAsync(string code, CancellationToken ct = default);
}

public sealed class SqlitePromoStore(IDbContextFactory<ServerDbContext> factory, ICreditStore credits) : IPromoStore
{
    // Unambiguous alphabet: no 0/O, 1/I, so codes are easy to read out loud and type.
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    public const int CodeLength = 5;

    public static string Normalize(string code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    public async Task<RedeemResult> RedeemAsync(Guid deviceId, string code, CancellationToken ct = default)
    {
        var normalized = Normalize(code);
        await using var db = await factory.CreateDbContextAsync(ct);

        var promo = await db.PromoCodes.AsNoTracking().FirstOrDefaultAsync(x => x.Code == normalized, ct);
        if (promo is null)
            return new RedeemResult(RedeemOutcome.NotFound, 0, await credits.GetBalanceAsync(deviceId, ct));
        if (promo.Revoked)
            return new RedeemResult(RedeemOutcome.Revoked, 0, await credits.GetBalanceAsync(deviceId, ct));
        if (promo.ExpiresAt is { } exp && exp <= DateTimeOffset.UtcNow)
            return new RedeemResult(RedeemOutcome.Expired, 0, await credits.GetBalanceAsync(deviceId, ct));

        // Record the redemption first; the composite PK makes a double-redeem a duplicate-key error.
        db.PromoRedemptions.Add(new PromoRedemption
        {
            Code = normalized,
            DeviceId = deviceId,
            CreditsGranted = promo.Credits,
            RedeemedAt = DateTimeOffset.UtcNow,
        });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // This device already redeemed this code.
            return new RedeemResult(RedeemOutcome.AlreadyRedeemed, 0, await credits.GetBalanceAsync(deviceId, ct));
        }

        var balance = await credits.GrantAsync(deviceId, promo.Credits, ct);
        return new RedeemResult(RedeemOutcome.Success, promo.Credits, balance);
    }

    public async Task<PromoCode> CreateAsync(string? code, int credits, int? expiresInDays, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var normalized = string.IsNullOrWhiteSpace(code) ? await GenerateUniqueAsync(db, ct) : Normalize(code);
        var promo = new PromoCode
        {
            Code = normalized,
            Credits = credits,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresInDays is { } days and > 0 ? DateTimeOffset.UtcNow.AddDays(days) : null,
            Revoked = false,
        };
        db.PromoCodes.Add(promo);
        await db.SaveChangesAsync(ct);
        return promo;
    }

    public async Task<IReadOnlyList<PromoSummary>> ListAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var codes = await db.PromoCodes.AsNoTracking().ToListAsync(ct);
        var counts = await db.PromoRedemptions.AsNoTracking()
            .GroupBy(x => x.Code)
            .Select(g => new { Code = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Code, x => x.Count, ct);

        return codes
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new PromoSummary(
                c.Code, c.Credits, c.CreatedAt, c.ExpiresAt, c.Revoked,
                counts.TryGetValue(c.Code, out var n) ? n : 0)).ToList();
    }

    public async Task<bool> RevokeAsync(string code, CancellationToken ct = default)
    {
        var normalized = Normalize(code);
        await using var db = await factory.CreateDbContextAsync(ct);
        var promo = await db.PromoCodes.FirstOrDefaultAsync(x => x.Code == normalized, ct);
        if (promo is null) return false;
        promo.Revoked = true;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static async Task<string> GenerateUniqueAsync(ServerDbContext db, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var candidate = RandomCode();
            if (!await db.PromoCodes.AnyAsync(x => x.Code == candidate, ct))
                return candidate;
        }
        throw new InvalidOperationException("Could not generate a unique promo code.");
    }

    private static string RandomCode()
    {
        Span<char> chars = stackalloc char[CodeLength];
        for (var i = 0; i < CodeLength; i++)
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return new string(chars);
    }
}
