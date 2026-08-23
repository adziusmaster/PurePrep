using Microsoft.EntityFrameworkCore;

namespace PurePrep.Server.Data;

public sealed class ServerDbContext(DbContextOptions<ServerDbContext> options) : DbContext(options)
{
    public DbSet<DeviceCredit> Credits => Set<DeviceCredit>();
    public DbSet<ProcessedPurchase> Purchases => Set<ProcessedPurchase>();
    public DbSet<UsageLog> UsageLogs => Set<UsageLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DeviceCredit>().HasKey(x => x.DeviceId);

        modelBuilder.Entity<ProcessedPurchase>(e =>
        {
            e.HasKey(x => x.PurchaseToken);
            e.HasIndex(x => x.OrderId).IsUnique();
        });

        modelBuilder.Entity<UsageLog>().HasKey(x => x.Id);
    }
}
