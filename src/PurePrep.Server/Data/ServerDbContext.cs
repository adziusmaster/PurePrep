using Microsoft.EntityFrameworkCore;

namespace PurePrep.Server.Data;

public sealed class ServerDbContext(DbContextOptions<ServerDbContext> options) : DbContext(options)
{
    public DbSet<DeviceCredit> Credits => Set<DeviceCredit>();
    public DbSet<ProcessedPurchase> Purchases => Set<ProcessedPurchase>();
    public DbSet<UsageLog> UsageLogs => Set<UsageLog>();
    public DbSet<PromoCode> PromoCodes => Set<PromoCode>();
    public DbSet<PromoRedemption> PromoRedemptions => Set<PromoRedemption>();
    public DbSet<DeviceSeed> DeviceSeeds => Set<DeviceSeed>();
    public DbSet<WaitlistSignup> WaitlistSignups => Set<WaitlistSignup>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DeviceCredit>().HasKey(x => x.DeviceId);

        modelBuilder.Entity<ProcessedPurchase>(e =>
        {
            e.HasKey(x => x.PurchaseToken);
            e.HasIndex(x => x.OrderId).IsUnique();
        });

        modelBuilder.Entity<UsageLog>(e =>
        {
            e.HasKey(x => x.Id);
            // Same SQLite constraint as DeviceSeed: the retention sweep filters on this column.
            e.Property(x => x.At).HasConversion(
                v => v.UtcTicks,
                v => new DateTimeOffset(v, TimeSpan.Zero));
            e.HasIndex(x => x.At);
        });

        modelBuilder.Entity<PromoCode>().HasKey(x => x.Code);
        modelBuilder.Entity<PromoRedemption>().HasKey(x => new { x.Code, x.DeviceId });

        modelBuilder.Entity<DeviceSeed>(e =>
        {
            e.HasKey(x => x.DeviceId);
            // EF Core's SQLite provider cannot translate DateTimeOffset comparisons, and the cap
            // query is exactly such a comparison. Persisting UTC ticks keeps the filter in SQL.
            e.Property(x => x.SeededAt).HasConversion(
                v => v.UtcTicks,
                v => new DateTimeOffset(v, TimeSpan.Zero));
            // The cap query is always "how many devices did this origin seed recently".
            e.HasIndex(x => new { x.IpHash, x.SeededAt });
        });

        modelBuilder.Entity<WaitlistSignup>(e =>
        {
            e.HasKey(x => x.Email);
            // Same UTC-ticks trick as DeviceSeed: keeps any date-range admin query translatable.
            e.Property(x => x.CreatedAt).HasConversion(
                v => v.UtcTicks,
                v => new DateTimeOffset(v, TimeSpan.Zero));
            e.Property(x => x.ConsentedAt).HasConversion(
                v => v.HasValue ? v.Value.UtcTicks : (long?)null,
                v => v.HasValue ? new DateTimeOffset(v.Value, TimeSpan.Zero) : (DateTimeOffset?)null);
        });
    }
}
