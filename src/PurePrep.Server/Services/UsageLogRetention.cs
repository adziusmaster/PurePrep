using Microsoft.EntityFrameworkCore;
using PurePrep.Server.Data;

namespace PurePrep.Server.Services;

/// <summary>
/// Deletes usage-log entries past their retention window. The log exists to spot abuse, which is a
/// short-horizon question; keeping it forever would turn it into exactly the long-lived activity
/// record the privacy policy says is not retained.
/// </summary>
public static class UsageLogRetention
{
    public static async Task<int> SweepAsync(
        IDbContextFactory<ServerDbContext> factory, TimeSpan maxAge, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var cutoff = DateTimeOffset.UtcNow - maxAge;
        return await db.UsageLogs.Where(x => x.At < cutoff).ExecuteDeleteAsync(ct);
    }
}

/// <summary>Runs the retention sweep on startup and daily thereafter.</summary>
public sealed class UsageLogRetentionService(
    IDbContextFactory<ServerDbContext> factory,
    ILogger<UsageLogRetentionService> logger) : BackgroundService
{
    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(30);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var deleted = await UsageLogRetention.SweepAsync(factory, MaxAge, stoppingToken);
                if (deleted > 0)
                    logger.LogInformation("Usage-log retention removed {Count} entries.", deleted);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Retention failing must never take the API down with it.
                logger.LogError(ex, "Usage-log retention sweep failed.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
