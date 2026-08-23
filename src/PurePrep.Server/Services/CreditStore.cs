using Microsoft.EntityFrameworkCore;
using PurePrep.Server.Data;

namespace PurePrep.Server.Services;

public interface ICreditStore
{
    Task<int> GetBalanceAsync(Guid deviceId, CancellationToken ct = default);

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
        await db.Database.EnsureCreatedAsync(ct);
        var row = await db.Credits.AsNoTracking().FirstOrDefaultAsync(x => x.DeviceId == deviceId, ct);
        return row?.Balance ?? 0;
    }

    public async Task<bool> TrySpendAsync(Guid deviceId, int amount, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Database.EnsureCreatedAsync(ct);
        // Atomic conditional decrement: only succeeds when the device has enough credits.
        var affected = await db.Database.ExecuteSqlRawAsync(
            "UPDATE Credits SET Balance = Balance - {0}, UpdatedAt = {1} WHERE DeviceId = {2} AND Balance >= {0}",
            [amount, DateTimeOffset.UtcNow, deviceId], ct);
        return affected > 0;
    }

    public async Task RefundAsync(Guid deviceId, int amount, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Database.EnsureCreatedAsync(ct);
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE Credits SET Balance = Balance + {0}, UpdatedAt = {1} WHERE DeviceId = {2}",
            [amount, DateTimeOffset.UtcNow, deviceId], ct);
    }

    public async Task<int> GrantAsync(Guid deviceId, int amount, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Database.EnsureCreatedAsync(ct);
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
