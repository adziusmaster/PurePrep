using Microsoft.EntityFrameworkCore;

namespace PurePrep.Server.Data;

/// <summary>
/// Brings the database up to the current shape, exactly once, at startup.
///
/// The live database predates any migration history — it was built by <c>EnsureCreated</c>, which
/// creates missing tables but never alters an existing one. Rather than retrofit EF migrations onto
/// an unversioned production database holding real credit balances, schema setup is centralised
/// here: idempotent, additive, and run once on boot instead of on every single request as before.
/// </summary>
public static class SchemaInitializer
{
    public static async Task InitializeAsync(IDbContextFactory<ServerDbContext> factory, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        // Creates every table for a brand-new database; a no-op for tables that already exist.
        await db.Database.EnsureCreatedAsync(ct);

        // Tables introduced after the original database was created. EnsureCreated will not add
        // these to an existing file, so they are created explicitly and idempotently.
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "PromoCodes" (
                "Code" TEXT NOT NULL CONSTRAINT "PK_PromoCodes" PRIMARY KEY,
                "Credits" INTEGER NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                "ExpiresAt" TEXT NULL,
                "Revoked" INTEGER NOT NULL
            );
            """, ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "PromoRedemptions" (
                "Code" TEXT NOT NULL,
                "DeviceId" TEXT NOT NULL,
                "CreditsGranted" INTEGER NOT NULL,
                "RedeemedAt" TEXT NOT NULL,
                CONSTRAINT "PK_PromoRedemptions" PRIMARY KEY ("Code", "DeviceId")
            );
            """, ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "DeviceSeeds" (
                "DeviceId" TEXT NOT NULL CONSTRAINT "PK_DeviceSeeds" PRIMARY KEY,
                "IpHash" TEXT NULL,
                "SeededAt" INTEGER NOT NULL
            );
            """, ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS "IX_DeviceSeeds_IpHash_SeededAt" ON "DeviceSeeds" ("IpHash", "SeededAt");
            """, ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "WaitlistSignups" (
                "Email" TEXT NOT NULL CONSTRAINT "PK_WaitlistSignups" PRIMARY KEY,
                "Source" TEXT NOT NULL,
                "IpHash" TEXT NULL,
                "CreatedAt" INTEGER NOT NULL
            );
            """, ct);

        // UsageLogs predates the switch from DeviceId to a salted DeviceHash.
        await AddColumnIfMissingAsync(db, "UsageLogs", "DeviceHash", "TEXT NOT NULL DEFAULT ''", ct);
        await DropColumnIfPresentAsync(db, "UsageLogs", "DeviceId", ct);
    }

    private static async Task<bool> HasColumnAsync(ServerDbContext db, string table, string column, CancellationToken ct)
    {
        var columns = await db.Database
            .SqlQueryRaw<string>($"SELECT name AS Value FROM pragma_table_info('{table}')")
            .ToListAsync(ct);
        return columns.Contains(column);
    }

    private static async Task AddColumnIfMissingAsync(
        ServerDbContext db, string table, string column, string definition, CancellationToken ct)
    {
        if (!await HasColumnAsync(db, table, column, ct))
            await db.Database.ExecuteSqlRawAsync($"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {definition}", ct);
    }

    private static async Task DropColumnIfPresentAsync(
        ServerDbContext db, string table, string column, CancellationToken ct)
    {
        // Dropping the raw device id is the point of the change, not a tidy-up: it is what stops the
        // audit trail being a per-device history of the sites someone cooks from.
        if (await HasColumnAsync(db, table, column, ct))
            await db.Database.ExecuteSqlRawAsync($"ALTER TABLE \"{table}\" DROP COLUMN \"{column}\"", ct);
    }
}
