using Microsoft.EntityFrameworkCore;
using PurePrep.Server.Data;

namespace PurePrep.Server.Services;

public interface ICreditStore
{
    Task<int> GetBalanceAsync(Guid deviceId, CancellationToken ct = default);

    /// <summary>
    /// Ensures a credit row exists for the device, seeding it with <paramref name="initialCredits"/>
    /// free credits on first contact. Returns the current balance. Safe to call repeatedly.
    /// </summary>
    Task<int> EnsureDeviceAsync(Guid deviceId, int initialCredits, CancellationToken ct = default);

    /// <summary>Atomically deducts <paramref name="amount"/> credits. Returns false if insufficient.</summary>
    Task<bool> TrySpendAsync(Guid deviceId, int amount, CancellationToken ct = default);

    /// <summary>Refunds credits (used when a paid operation fails after deduction).</summary>
    Task RefundAsync(Guid deviceId, int amount, CancellationToken ct = default);

    /// <summary>Grants credits to a device, creating the row if needed. Returns the new balance.</summary>
    Task<int> GrantAsync(Guid deviceId, int amount, CancellationToken ct = default);
}

public sealed class SqliteCreditStore(IDbContextFactory<ServerDbContext> factory) : ICreditStore
{
    public async Task<int> GetBalanceAsync(Guid deviceId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var row = await db.Credits.AsNoTracking().FirstOrDefaultAsync(x => x.DeviceId == deviceId, ct);
        return row?.Balance ?? 0;
    }

    public async Task<int> EnsureDeviceAsync(Guid deviceId, int initialCredits, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var existing = await db.Credits.AsNoTracking().FirstOrDefaultAsync(x => x.DeviceId == deviceId, ct);
        if (existing is not null) return existing.Balance;

        var now = DateTimeOffset.UtcNow;
        db.Credits.Add(new DeviceCredit { DeviceId = deviceId, Balance = initialCredits, CreatedAt = now, UpdatedAt = now });
        try
        {
            await db.SaveChangesAsync(ct);
            return initialCredits;
        }
        catch (DbUpdateException)
        {
            // A concurrent request already seeded this device; return the persisted balance.
            return await GetBalanceAsync(deviceId, ct);
        }
    }

    public async Task<bool> TrySpendAsync(Guid deviceId, int amount, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        // Atomic conditional decrement: only succeeds when the device has enough credits.
        var affected = await db.Database.ExecuteSqlRawAsync(
            "UPDATE Credits SET Balance = Balance - {0}, UpdatedAt = {1} WHERE DeviceId = {2} AND Balance >= {0}",
            [amount, DateTimeOffset.UtcNow, deviceId], ct);
        return affected > 0;
    }

    public async Task RefundAsync(Guid deviceId, int amount, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE Credits SET Balance = Balance + {0}, UpdatedAt = {1} WHERE DeviceId = {2}",
            [amount, DateTimeOffset.UtcNow, deviceId], ct);
    }

    public async Task<int> GrantAsync(Guid deviceId, int amount, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var row = await db.Credits.FirstOrDefaultAsync(x => x.DeviceId == deviceId, ct);
        if (row is null)
        {
            row = new DeviceCredit { DeviceId = deviceId, Balance = amount, CreatedAt = now, UpdatedAt = now };
            db.Credits.Add(row);
        }
        else
        {
            row.Balance += amount;
            row.UpdatedAt = now;
        }
        await db.SaveChangesAsync(ct);
        return row.Balance;
    }
}
